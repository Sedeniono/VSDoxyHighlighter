using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.VCCodeModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;


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

  class MacroInfo 
  {
    public string MacroName { get; set; }
    public IEnumerable<string> Parameters { get; set; }
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
    /// If the next C++ element after the given 'point' in the file is a **template** class, struct or using, returns information about it.
    /// </summary>
    ClassInfo TryGetClassInfoIfNextIsATemplateClass(SnapshotPoint point);

    /// <summary>
    /// If the next C++ element after the given 'point' in the file is a #define, returns information about it.
    /// </summary>
    MacroInfo TryGetMacroInfoIfNextIsAMacro(SnapshotPoint point);
  }


  //==============================================================================
  // SemanticsFromFileCodeModelAndCache
  //==============================================================================

  /// <summary>
  /// Visual Studio has an official API to access semantic information about the code base: FileCodeModel. For C++,
  /// there is also a more specific version called VCFileCodeModel. This class uses the FileCodeModel to get the
  /// requested information.
  /// However, the FileCodeModel is somewhat arcane and buggy: It does not know anything about global function/class
  /// declarations. Moreover, we can only query it to give the code element a very specific text point, rather than
  /// a span of text. Actually, we could also get all code elements and manually figure out the interesting pieces.
  /// But for long files I am afraid that this is very slow.
  /// The SemanticTokenCache (see VisualStudioCppFileSemanticsFromCache), on the other hand, seems to support efficient
  /// querying and knows about global declarations. Unfortunatly, it does not know about non-type template parameters
  /// or macro parameters. So, the idea of SemanticsFromFileCodeModelAndCache is the following: First, get information 
  /// about the interesting semantic piece from VisualStudioCppFileSemanticsFromCache. This especially includes position
  /// information. Then use the position information to query the FileCodeModel, and amend the semantic piece with 
  /// information from FileCodeModel.
  /// </summary>
  class SemanticsFromFileCodeModelAndCache : IVisualStudioCppFileSemantics
  {
    public SemanticsFromFileCodeModelAndCache(IVsEditorAdaptersFactoryService adapterService, ITextBuffer textBuffer)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      mTextBuffer = textBuffer;
      mSemanticCache = new VisualStudioCppFileSemanticsFromCache(textBuffer);

      // As far as I understand, Microsoft.VisualStudio.Text.ITextBuffer and similar classes are the "new" .NET managed classes.
      // On the other hand, the stuff in the EnvDTE namespace (e.g. EnvDTE.Document and EnvDTE.TextDocument) represent 'old' classes,
      // predating the .NET implementations. They are always COM interfaces. However, they are still relevant for certain things.
      // Things like IVsTextBuffer seem to be wrappers/adapters around the old classes. We can get from a "new world" object
      // (such as ITextBuffer) to the adapter via the IVsEditorAdaptersFactoryService service, resulting in e.g. a IVsTextBuffer.
      // (I think that service is just getting some object from ITextBuffer.Properties.) Digging through decompiled VS .NET code,
      // from the adapter we get to the "old world" object via IExtensibleObject. Note that the documentation of IExtensibleObject
      // states that it is Microsoft internal. We ignore this warning here. The only valid arguments to
      // IExtensibleObject.GetAutomationObject() seem to be "Document" (giving an EnvDTE.Document) and "TextDocument" (giving
      // an EnvDTE.TextDocument).
      //
      // In our case here we are interested in the "FileCodeModel" which is only accessible in the 'old' world, specifically
      // via "EnvDTE.Document". There is one "FileCodeModel" per "Document.ProjectItem" in the solution.
      if (adapterService != null) {
        IVsTextBuffer vsTextBuffer = adapterService.GetBufferAdapter(mTextBuffer);
        IVsTextLines vsTextLines = vsTextBuffer as IVsTextLines;
        IExtensibleObject extObj = vsTextBuffer as IExtensibleObject;
        if (vsTextLines != null && extObj != null) {
          //extObj.GetAutomationObject("TextDocument", null, out object textDocObj);
          //EnvDTE.TextDocument textDoc = textDocObj as EnvDTE.TextDocument;

          extObj.GetAutomationObject("Document", null, out object docObj);
          EnvDTE.Document doc = docObj as EnvDTE.Document;

          if (doc != null) {
            mFileCodeModel = doc.ProjectItem.FileCodeModel;
            if (mFileCodeModel != null) {
              mVsTextLines = vsTextLines;
            }
          }
        }
      }
    }


    public FunctionInfo TryGetFunctionInfoIfNextIsAFunction(SnapshotPoint point)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      (var functionTokenIter, var _) = mSemanticCache.TryGetSemanticFunctionInfoIfNextIsAFunction(point);
      if (functionTokenIter == null) {
        // Either the next thing after 'point' is not a function, or some error occurred. In the latter case we could try
        // to successively iterate through the following lines and query FileCodeModel for information. But in my experiments
        // the SemanticTokenCache never failed to get function information. So we don't do such a fallback.
        return null;
      }

      VCCodeFunction codeElement = TryGetCodeElementFor(functionTokenIter.Current) as VCCodeFunction;
      if (codeElement == null) {
        // Most likely we have a global function declaration. The FileCodeModel is buggy here and does not know about it.
        // Just return the info from the SemanticTokenCache. Unfortunately it lacks information about non-type template parameters.
        // Not much we can do here (except write a code parser ourselves...).
        return mSemanticCache.TryGetFunctionInfoIfNextIsAFunction(point);
      }

      // If we were able to get function information from the FileCodeModel, we believe it: The SemanticTokenCache does
      // not know about non-type template parameters, while the FileCodeModel does. Also, in my experiments the SemanticTokenCache
      // was aware of ALL functions in the file. So no need to check whether the FileCodeModel or the SemanticTokenCache found a
      // function which comes earlier than the other; they should be the same at this point.
      string funcName = codeElement.Name;
      Debug.Assert(funcName == functionTokenIter.Current.Text);
      var parameters = new List<string>();
      foreach (CodeElement param in codeElement.Parameters) {
        parameters.Add(param.Name);
      }
      var templateParameters = new List<string>();
      foreach (CodeElement param in codeElement.TemplateParameters) {
        templateParameters.Add(param.Name);
      }

      return new FunctionInfo { FunctionName = funcName, ParameterNames = parameters, TemplateParameterNames = templateParameters };
    }


    public ClassInfo TryGetClassInfoIfNextIsATemplateClass(SnapshotPoint point) 
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      // Note: Structure and reasoning the same as in TryGetFunctionInfoIfNextIsAFunction().
      // Especially: Prefer the FileCodeModel information since it contains information about non-type template parameters.

      (var classToken, _) = mSemanticCache.TryGetSemanticClassInfoIfNextIsATemplateClass(point);
      if (classToken == null) {
        return null;
      }

      CodeElement codeElement = TryGetCodeElementFor(classToken);
      if (codeElement == null) { 
        return mSemanticCache.TryGetClassInfoIfNextIsATemplateClass(point); 
      }

      string className = codeElement.Name;
      Debug.Assert(className == classToken.Text);

      CodeElements templateParameters = null;
      if (codeElement is VCCodeClass cls) {
        templateParameters = cls.TemplateParameters;
      }
      else if (codeElement is VCCodeStruct st) {
        templateParameters = st.TemplateParameters;
      }
      else if (codeElement is VCCodeUsingAlias us) {
        templateParameters = us.TemplateParameters;
      }

      if (templateParameters == null) {
        return mSemanticCache.TryGetClassInfoIfNextIsATemplateClass(point);
      }

      var templateParameterNames = new List<string>();
      foreach (CodeElement param in templateParameters) {
        templateParameterNames.Add(param.Name);
      }

      return new ClassInfo { ClassName = className, TemplateParameterNames = templateParameterNames };
    }


    public MacroInfo TryGetMacroInfoIfNextIsAMacro(SnapshotPoint point) 
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      // Note: Structure and reasoning the same as in TryGetFunctionInfoIfNextIsAFunction().
      // Especially: Prefer the FileCodeModel information since it contains information about macro parameters.

      var macroToken = mSemanticCache.TryGetSemanticMacroInfoIfNextIsAMacro(point);
      if (macroToken == null) {
        return null;
      }

      VCCodeMacro codeElement = TryGetCodeElementFor(macroToken) as VCCodeMacro;
      if (codeElement == null) {
        return mSemanticCache.TryGetMacroInfoIfNextIsAMacro(point);
      }

      string macroName = codeElement.Name;
      Debug.Assert(macroName == macroToken.Text);

      var parameters = new List<string>();
      foreach (CodeElement param in codeElement.Parameters) {
        parameters.Add(param.Name);
      }

      return new MacroInfo { MacroName = macroName, Parameters = parameters };
    }


    private bool IsFileCodeModelAvailable() 
    {
      return mTextBuffer != null && mVsTextLines != null && mFileCodeModel != null;
    }


    private CodeElement TryGetCodeElementFor(VisualStudioCppFileSemanticsFromCache.SemanticToken token)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      if (token == null || !IsFileCodeModelAvailable()) {
        return null;
      }

      try {
        // We can query the FileCodeModel for a CodeElement only at positions (rather than for whole spans). The most
        // reliable thing to do seems to query the FileCodeModel for information in the middle of the elements name.
        int posInMiddleOfFuncName = (token.Start + token.End) / 2;
        ITextSnapshotLine funcNameLine = token.Span.Snapshot.GetLineFromPosition(posInMiddleOfFuncName);

        var offsetInLine0Based = posInMiddleOfFuncName - funcNameLine.Start;
        Debug.Assert(offsetInLine0Based >= 0);
        var lineNumber0Based = funcNameLine.LineNumber;

        // Note: IVsTextLines.CreateEditPoint() wants 0-based indices. On the other hand, TextDocument.CreateEditPoint()
        // (which we could have also used) wants 1-based indices. The indicies stored on an EditPoint are all 1-based.
        mVsTextLines.CreateEditPoint(lineNumber0Based, offsetInLine0Based, out object editPointObj);
        EditPoint editPoint = editPointObj as EditPoint;
        if (editPoint == null) {
          return null;
        }

        var offsetInLine1Based = offsetInLine0Based + 1;
        var lineNumber1Based = lineNumber0Based + 1;
        Debug.Assert(editPoint.Line == lineNumber1Based);
        Debug.Assert(editPoint.LineCharOffset == offsetInLine1Based);

        // In my tests, it did not matter which value of vsCMElement we use. So we simply use the first one (vsCMElementOther).
        return editPoint.CodeElement[vsCMElement.vsCMElementOther];
      }
      catch (COMException ex) {
        // COMException happens sometimes (maybe if something is not yet fully initialized).
        ActivityLog.LogWarning("VSDoxyHighlighter", $"COMException in 'TryGetCodeElementFor()': {ex.ToString()}");
        return null;
      }
      catch (AggregateException ex) {
        // AggregateException happens sometimes (maybe if something is not yet fully initialized).
        ActivityLog.LogWarning("VSDoxyHighlighter", $"COMException in 'TryGetCodeElementFor()': {ex.ToString()}");
        return null;
      }
    }


    private readonly ITextBuffer mTextBuffer;
    private readonly IVsTextLines mVsTextLines = null;
    private readonly FileCodeModel mFileCodeModel = null;
    private readonly VisualStudioCppFileSemanticsFromCache mSemanticCache;
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
    public enum SemanticTokenKind
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
    public class SemanticToken
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

      (IEnumerator<SemanticToken> functionTokenIter, IEnumerable<SemanticToken> templateParameters)
        = TryGetSemanticFunctionInfoIfNextIsAFunction(point);
      if (functionTokenIter == null) {
        return null;
      }
      return GetFunctionInfoFromFunctionToken(functionTokenIter, templateParameters);
    }


    public (IEnumerator<SemanticToken> functionTokenIter, IEnumerable<SemanticToken> templateParameters) 
        TryGetSemanticFunctionInfoIfNextIsAFunction(SnapshotPoint point) 
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      var tokens = GetTokensAfter(point);
      if (tokens == null) {
        return (null, null);
      }

      List<SemanticToken> tokensBeforeThatAreCppTypes = null;
      IEnumerator<SemanticToken> enumerator = tokens.GetEnumerator();
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

        return (enumerator, tokensBeforeThatAreCppTypes);
      }

      return (null, null);
    }


    public ClassInfo TryGetClassInfoIfNextIsATemplateClass(SnapshotPoint point)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      (SemanticToken classToken, IEnumerable<SemanticToken> templateParameters) = TryGetSemanticClassInfoIfNextIsATemplateClass(point);
      if (classToken == null) {
        return null;
      }

      IEnumerable<string> templateNames = Enumerable.Empty<string>();
      if (templateParameters != null) {
        templateNames = templateParameters.Select(t => t.Text).ToList();
      }
      return new ClassInfo { ClassName = classToken.Text, TemplateParameterNames = templateNames };
    }


    public (SemanticToken classToken, IEnumerable<SemanticToken> templateParameters)
        TryGetSemanticClassInfoIfNextIsATemplateClass(SnapshotPoint point)
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      var tokens = GetTokensAfter(point);
      if (tokens == null) {
        return (null, null);
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
          return (token, tokensBeforeThatAreCppTypes);
        }
      }

      return (null, null);
    }


    public MacroInfo TryGetMacroInfoIfNextIsAMacro(SnapshotPoint point) 
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      SemanticToken token = TryGetSemanticMacroInfoIfNextIsAMacro(point);
      if (token != null) {
        // The SemanticTokenCache doesn't know anything about the macro parameters, unfortunately.
        return new MacroInfo { MacroName = token.Text, Parameters = Enumerable.Empty<string>() };
      }
      return null;
    }


    public SemanticToken TryGetSemanticMacroInfoIfNextIsAMacro(SnapshotPoint point)
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      var tokens = GetTokensAfter(point);
      if (tokens == null) {
        return null;
      }
      foreach (SemanticToken token in tokens) {
        if (token != null && token.SemanticTokenKind == SemanticTokenKind.cppMacro) { 
          return token;
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
