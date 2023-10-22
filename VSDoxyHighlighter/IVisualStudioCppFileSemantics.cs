using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


namespace VSDoxyHighlighter
{
  class FunctionInfo 
  {
    public string FunctionName { get; set; }
    public IEnumerable<string> ParameterNames { get; set; }
    public IEnumerable<string> TemplateParameterNames { get; set; }
  }

  class ClassInfo 
  {
    public string ClassName { get; set; }
    public IEnumerable<string> TemplateParameterNames { get; set; }
  }


  //==============================================================================
  // IVisualStudioCppFileSemantics
  //==============================================================================

  /// <summary>
  /// Wraps access to Visual Studio components that have knowledge of the semantic elements in a certain C/C++ file.
  /// </summary>
  interface IVisualStudioCppFileSemantics
  {
    /// <summary>
    /// If the next C++ element after the given 'point' in the file is a function, returns information about that function.
    /// </summary>
    FunctionInfo TryGetFunctionInfoIfNextIsAFunction(SnapshotPoint point);

    /// <summary>
    /// If the next C++ element after the given 'point' in the file is a **template** class or struct, returns information about that class/struct.
    /// </summary>
    ClassInfo TryGetClassInfoIfNextIsATemplateClass(SnapshotPoint point);
  }




  //==============================================================================
  // VisualStudioCppFileSemanticsFromCache
  //==============================================================================

  /// <summary>
  /// Provides access to semantic information in a C/C++ file by exploiting the undocumented SemanticTokensCache.
  /// 
  /// Visual Studio actually comes with an API to access semantic information of a file: FileCodeModel. In case
  /// of C/C++ files, there is also a more specific one available called VCFileCodeModel. Unfortunately, it is
  /// buggy. Namely, it for some reason knows nothing about global function declarations. This makes it pretty
  /// useless for our purpose (if used alone), since in C/C++ code global function declarations are pretty common.
  /// 
  /// Internally, VS does have to know about them somewhere. Looking through decompiled code, there are promising
  /// internals that we could use. Specifically, there is a service Microsoft.VisualStudio.CppSvc.Internal.CPPThreadSafeServiceClass
  /// that we can actually get (e.g. via GetService(typeof(CPPThreadSafeServiceClass))). The service itself
  /// implements IVCCodeStoreManager which has promising functions such as GetCodeItems(). Getting a hold of a
  /// reference to the service isn't the problem, or calling functions via reflection. The problem is that it is 
  /// a COM handle for a specific C++ parsing thread. When calling any function on it, we get a RPC_E_WRONG_THREAD
  /// exception. The COM handle lives in a (so called) apartment which we cannot simply access. Basically, VS has
  /// a C++ parsing thread running, which then makes appropriate direct calls to IntelliSense or sets certain
  /// variables that are then later accessed by the main thread.
  /// 
  /// The best place I could find to somehow get the semantic infos from the C++ parsing thread is via a property
  /// of a text buffer, which has the type 'SemanticTokensCache'. Every time the file changes, it gets updated. 
  /// There we do not have any threading issues, and it knows about global function declarations.
  /// 
  /// Caveat: The SemanticTokensCache does not know about non-type template parameters (NTTP) (for example an
  /// integer as template argument). Also, for parameters etc it does not store the actual type.
  /// </summary>
  class VisualStudioCppFileSemanticsFromCache : IVisualStudioCppFileSemantics 
  {
    /// <summary>
    /// Same as the VS internal enum Microsoft.VisualStudio.CppSvc.Internal.SemanticTokenKind
    /// </summary>
    private enum SemanticTokenKind
    {
      cppNone = 1,
      cppMacro,
      cppEnumerator,
      cppGlobalVariable,
      cppLocalVariable,
      cppParameter,
      cppType,
      cppRefType,
      cppValueType,
      cppFunction,
      cppMemberFunction,
      cppMemberField,
      cppStaticMemberFunction,
      cppStaticMemberField,
      cppProperty,
      cppEvent,
      cppClassTemplate,
      cppGenericType,
      cppFunctionTemplate,
      cppNamespace,
      cppLabel,
      cppUserDefinedLiteralRaw,
      cppUserDefinedLiteralNumber,
      cppUserDefinedLiteralString,
      cppOperator,
      cppMemberOperator,
      cppNewDelete,
      cppLast
    }


    private bool IsFunction(SemanticTokenKind kind)
    {
      return kind == SemanticTokenKind.cppFunction
        || kind == SemanticTokenKind.cppMemberFunction
        || kind == SemanticTokenKind.cppStaticMemberFunction
        || kind == SemanticTokenKind.cppFunctionTemplate;
    }


    /// <summary>
    /// More or less the same as the internal VS class Microsoft.VisualStudio.VC.SemanticTokensCache.SemanticToken. 
    /// </summary>
    [DebuggerDisplay("Kind={SemanticTokenKind}, Span={Span}")]
    private class SemanticToken
    {
      public SemanticTokenKind SemanticTokenKind { get; set; }
      public SnapshotSpan Span { get; set; }

      public ITextSnapshot Snapshot => Span.Snapshot;
      public int Start => Span.Start.Position;
      public int Length => Span.Length;
      public int End => Start + Length;
      public string Text => Span.GetText();
    }


    /// <summary>
    /// Wrapper around the Visual Studio SemanticsTokenCache, exposing its functions (or at least those that we need) 
    /// to  the extension code. It encapsulates the reflection magic which is necessary because we don't want to have
    /// explicit dependencies to VS internals.
    /// </summary>
    private class VSSemanticsTokenCache
    {
      public VSSemanticsTokenCache(object semanticsTokenCache)
      {
        mSemanticsTokenCache = semanticsTokenCache;
        Debug.Assert(mSemanticsTokenCache != null);
        if (mSemanticsTokenCache != null) {
          mGetTokensMethod = mSemanticsTokenCache.GetType().GetMethod("GetTokens");
        }
      }

      public IEnumerable<SemanticToken> GetTokens(NormalizedSnapshotSpanCollection spans)
      {
        if (mSemanticsTokenCache == null || mGetTokensMethod == null) {
          yield break;
        }

        int outVersion = 0;
        object[] args = new object[] { spans, outVersion };
        IEnumerable allTokens = mGetTokensMethod.Invoke(mSemanticsTokenCache, args) as IEnumerable;
        foreach (object token in allTokens) {
          if (token != null) {
            yield return GetSemanticTokenInfo(token);
          }
        }
      }

      private SemanticToken GetSemanticTokenInfo(object semanticToken)
      {
        if (mKindGetter == null || mSpanGetter == null) {
          // semanticTokenType == class Microsoft.VisualStudio.VC.SemanticTokensCache.SemanticToken
          var semanticTokenType = semanticToken.GetType();
          mKindGetter = semanticTokenType.GetProperty("Kind");
          mSpanGetter = semanticTokenType.GetProperty("Span");
        }

        if (mKindGetter == null || mSpanGetter == null) {
          Debug.Assert(false);
          return null;
        }

        var kind = mKindGetter.GetValue(semanticToken);
        var span = mSpanGetter.GetValue(semanticToken);
        if (kind == null || span == null) {
          Debug.Assert(false);
          return null;
        }

        // 'kind' is the enum Microsoft.VisualStudio.CppSvc.Internal.SemanticTokenKind. We map it to our
        // own SemanticTokenKind via string instead by number in case the VS enum changes in a later version
        // of VS. It is hopefully more robust that way.
        string kindStr = kind.ToString();
        if (!Enum.TryParse(kindStr, out SemanticTokenKind kindEnum)) {
          Debug.Assert(false);
          return null;
        }

        return new SemanticToken { SemanticTokenKind = kindEnum, Span = (SnapshotSpan)span };
      }

      private readonly object mSemanticsTokenCache = null;
      private readonly MethodInfo mGetTokensMethod = null;
      private PropertyInfo mKindGetter = null;
      private PropertyInfo mSpanGetter = null;
    }


    public VisualStudioCppFileSemanticsFromCache(ITextBuffer textBuffer) 
    { 
      mTextBuffer = textBuffer;
      InitializeLazily();
    }


    public void InitializeLazily()
    {
      FindVSSemanticsTokenCache();
    }


    public FunctionInfo TryGetFunctionInfoIfNextIsAFunction(SnapshotPoint point) 
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      var tokens = GetTokensAfter(point);
      if (tokens == null) {
        return null;
      }

      List<SemanticToken> tokensBeforeThatAreCppTypes = null;
      var enumerator = tokens.GetEnumerator();
      while (enumerator.MoveNext()) {
        SemanticToken token = enumerator.Current;
        if (token == null) {
          continue;
        }

        // Template parameters of template functions come before. So we need to skip and remember them.
        // Note: The SemanticTokenCache does not know about non-type template parameters unfortunately.
        if (token.SemanticTokenKind == SemanticTokenKind.cppType) {
          if (tokensBeforeThatAreCppTypes == null) {
            tokensBeforeThatAreCppTypes = new List<SemanticToken>();
          }
          tokensBeforeThatAreCppTypes.Add(token);
          continue;
        }

        if (!IsFunction(token.SemanticTokenKind)) {
          break;
        }
        
        return GetFunctionInfoFromFunctionToken(enumerator, tokensBeforeThatAreCppTypes);
      }

      return null;
    }


    public ClassInfo TryGetClassInfoIfNextIsATemplateClass(SnapshotPoint point)
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      var tokens = GetTokensAfter(point);
      if (tokens == null) {
        return null;
      }

      List<SemanticToken> tokensBeforeThatAreCppTypes = null;
      foreach (SemanticToken token in tokens) {
        if (token == null) {
          continue;
        }

        // Template parameters of template class come before. So we need to skip and remember them.
        // Note: The SemanticTokenCache does not know about non-type template parameters unfortunately.
        if (token.SemanticTokenKind == SemanticTokenKind.cppType) {
          if (tokensBeforeThatAreCppTypes == null) {
            tokensBeforeThatAreCppTypes = new List<SemanticToken>();
          }
          tokensBeforeThatAreCppTypes.Add(token);
          continue;
        }

        if (token.SemanticTokenKind == SemanticTokenKind.cppClassTemplate) {
          IEnumerable<string> templateNames = Enumerable.Empty<string>();
          if (tokensBeforeThatAreCppTypes != null) {
            templateNames = tokensBeforeThatAreCppTypes.Select(t => t.Text).ToList();
          }
          return new ClassInfo { ClassName = token.Text, TemplateParameterNames = templateNames };
        }
      }

      return null;
    }


    private IEnumerable<SemanticToken> GetTokensAfter(SnapshotPoint point)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      var vsCache = FindVSSemanticsTokenCache();
      if (vsCache == null) {
        return null;
      }

      // Get (lazily) the tokens starting at 'point' till the end of the file.
      // Span from 'point' to the end of the file.
      var span = new NormalizedSnapshotSpanCollection(
        new SnapshotSpan(mTextBuffer.CurrentSnapshot, point.Position, mTextBuffer.CurrentSnapshot.Length - point.Position));
      return vsCache.GetTokens(span);
    }


    private FunctionInfo GetFunctionInfoFromFunctionToken(
        IEnumerator<SemanticToken> tokenIter, 
        IEnumerable<SemanticToken> tokensBeforeThatAreCppTypes) 
    {
      SemanticTokenKind functionKind = tokenIter.Current.SemanticTokenKind;
      Debug.Assert(IsFunction(functionKind));
      string functionName = tokenIter.Current.Text;

      var parameterNames = new List<string>();
      while (tokenIter.MoveNext()) {
        SemanticToken token = tokenIter.Current;
        // 'cppType' appears for parameters whose type is a template argument, and also for class declarations in
        // the parameter list (e.g. `void foo(class MyClass c);`). So we need to skip them.
        if (token.SemanticTokenKind == SemanticTokenKind.cppType) {
          continue;
        }
        if (token.SemanticTokenKind != SemanticTokenKind.cppParameter) {
          break;
        }
        parameterNames.Add(token.Text);
      }

      // The tokens of type 'cppType' coming directly before a function are the template parameters of that function.
      var templateNames = Enumerable.Empty<string>();
      if (tokensBeforeThatAreCppTypes != null) {
        templateNames = tokensBeforeThatAreCppTypes.Select(t => t.Text).ToList();
      }

      return new FunctionInfo { FunctionName = functionName, ParameterNames = parameterNames, TemplateParameterNames = templateNames };
    }


    private VSSemanticsTokenCache FindVSSemanticsTokenCache() 
    {
      if (mVSSemanticsTokenCache == null) {
        string name = "Microsoft.VisualStudio.VC.SemanticTokensCache".ToUpper();
        foreach (var kvp in mTextBuffer.Properties.PropertyList) {
          if (kvp.Key.ToString().ToUpper() == name) {
            mVSSemanticsTokenCache = new VSSemanticsTokenCache(kvp.Value);
            break;
          }
        }
      }

      return mVSSemanticsTokenCache;
    }


    private readonly ITextBuffer mTextBuffer;
    private VSSemanticsTokenCache mVSSemanticsTokenCache;
  }
}
