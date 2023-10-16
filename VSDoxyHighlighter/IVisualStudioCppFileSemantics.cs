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
    public string Name { get; set; }
    public List<string> ParameterNames { get; set; }
    public List<string> TemplateParameterNames { get; set; }
  }


  //==============================================================================
  // IVisualStudioCppFileSemantics
  //==============================================================================

  /// <summary>
  /// Wraps access to Visual Studio components that have knowledge of the semantic elements in a certain C/C++ file.
  /// </summary>
  interface IVisualStudioCppFileSemantics
  {
    FunctionInfo TryGetFunctionInfoIfNextIsAFunction(SnapshotPoint point);
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
  /// </summary>
  class VisualStudioCppFileSemanticsFromCache : IVisualStudioCppFileSemantics 
  {
    // Same as Microsoft.VisualStudio.CppSvc.Internal.SemanticTokenKind
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


    // More or less the same as Microsoft.VisualStudio.VC.SemanticTokensCache.SemanticToken.
    private class SemanticToken
    {
      public SemanticTokenKind SemanticTokenKind { get; set; }
      public SnapshotSpan Span { get; set; }

      public ITextSnapshot Snapshot => Span.Snapshot;
      public int Start => Span.Start.Position;
      public int Length => Span.Length;
      public int End => Start + Length;
    }


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
        // own SemanticTokenKind via string instead by number in case the VS enum changed in a later version
        // of VS. It is more robust that way.
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

      var vsCache = FindVSSemanticsTokenCache();
      if (vsCache == null) {
        return null;
      }

      // Get (lazily) the tokens starting at 'point' till the end of the file.
      // Span from 'point' to the end of the file.
      var span = new NormalizedSnapshotSpanCollection(
        new SnapshotSpan(mTextBuffer.CurrentSnapshot, point.Position, mTextBuffer.CurrentSnapshot.Length - point.Position));
      var tokens = vsCache.GetTokens(span);

      var enumerator = tokens.GetEnumerator();
      while (enumerator.MoveNext()) { 
        var token = enumerator.Current;
        if (token == null) {
          continue;
        }

        // TODO: Must skip cppType if a cppFunctionTemplate comes...
        if (!IsFunction(token.SemanticTokenKind)) {
          continue;
        }
        
        return GetFunctionInfoFromFunctionToken(tokens, enumerator);
      }

      return null;
    }


    private FunctionInfo GetFunctionInfoFromFunctionToken(IEnumerable<SemanticToken> allTokens, IEnumerator<SemanticToken> tokenIter) 
    {
      SemanticTokenKind functionKind = tokenIter.Current.SemanticTokenKind;
      Debug.Assert(IsFunction(functionKind));
      string functionName = tokenIter.Current.Span.GetText();

      var parameterNames = new List<string>();
      while (tokenIter.MoveNext()) {
        if (tokenIter.Current.SemanticTokenKind != SemanticTokenKind.cppParameter) {
          break;
        }
        parameterNames.Add(tokenIter.Current.Span.GetText());
      }

      // TODO: Get template parameters of function. They come before.
      var templateNames = new List<string>();
      return new FunctionInfo { Name = functionName, ParameterNames = parameterNames, TemplateParameterNames = templateNames };
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
