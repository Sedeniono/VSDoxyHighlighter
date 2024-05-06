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
  class ParameterInfo 
  {
    public string Name { get; set; }
    // Can be null if the type is not known.
    public string Type { get; set; }
  }

  class FunctionInfo 
  {
    public string FunctionName { get; set; }
    public IEnumerable<ParameterInfo> Parameters { get; set; }
    public IEnumerable<string> TemplateParameters { get; set; }
  }

  class ClassOrAliasInfo 
  {
    public string ClassName { get; set; }
    public string Type { get; set; } // "Class", "Struct" or "Alias"
    public IEnumerable<string> TemplateParameters { get; set; }
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
  /// Interface for classes that wrap access to Visual Studio components that have knowledge of the semantic elements in a certain C/C++ file.
  /// The functions are intended to be called from within a comment, in order to figure out what C++ element comes after the comment.
  /// Usage in other contexts might not work as expected.
  /// </summary>
  interface IVisualStudioCppFileSemantics
  {
    /// <summary>
    /// If the next C++ element after the given 'point' in the file (ignoring comments) is a function, returns information about that function.
    /// Note: It is implementation defined what is returned if 'point' is not a point before the start of the function. I.e. 'point' should be
    /// a point before the function's 'template' keyword (if it is a template function) or return value (if it is a non-template function).
    /// </summary>
    FunctionInfo TryGetFunctionInfoIfNextIsAFunction(SnapshotPoint point);

    /// <summary>
    /// If the next C++ element after the given 'point' in the file (ignoring comments) is a **template** class, struct or using, returns information about it.
    /// Note: It is implementation defined what is returned if 'point' is not a point before the start of the class. I.e. 'point' should be
    /// a point before the class's 'template' keyword (if it is a template class) or the 'class'/'struct'/'using' keyword (for non-template cases).
    /// </summary>
    ClassOrAliasInfo TryGetClassInfoIfNextIsATemplateClassOrAlias(SnapshotPoint point);

    /// <summary>
    /// If the next C++ element after the given 'point' in the file (ignoring comments) is a #define, returns information about it.
    /// Note: It is implementation defined what is returned if 'point' is not a point before the '#define'.
    /// </summary>
    MacroInfo TryGetMacroInfoIfNextIsAMacro(SnapshotPoint point);

    /// <summary>
    /// Tries to find the next C++ element (variable, function, class, etc.) **before** the given "point", but at most "withinNumLinesBefore" before.
    /// The limit "withinNumLinesBefore" is necessary because this is a potentially expensive operation.
    /// Returns the end position of the found C++ element (offset from the start of the buffer), or the start position of the line indicated
    /// by "withinNumLinesBefore".
    /// </summary>
    int TryGetEndPositionOfCppElementBefore(SnapshotPoint point, uint withinNumLinesBefore);
  }



  //==============================================================================
  // CppFileSemanticsFromVSCodeModelAndCache
  //==============================================================================

  /// <summary>
  /// A class that is used to retrieve information about the next C++ element that comes after some text point (where
  /// the text point is typically some point in a comment).
  /// 
  /// Visual Studio has an official API to access semantic information about the code base in a file: FileCodeModel. 
  /// For C++, there is also a more specific version called VCFileCodeModel. The FileCodeModel can be used to get the
  /// desired information. Unfortunately, the FileCodeModel is somewhat arcane to use and buggy:
  /// - We can query the FileCodeModel to give the code element only at a very specific text point, rather than a whole
  ///   span of text. So there is no direct way to ask it for all code elements in a span (rather than a point). Or
  ///   for the C++ element that comes after a given text point. We would need to manually step through the text buffer
  ///   and query the FileCodeModel each time, until we hit something that it knows. Alternatively, we could also get 
  ///   **all** classes/functions/etc. in a file and create a map of locations from this. But for long files I am afraid 
  ///   that this is slow. After all, all we want is the next C++ piece after a given point.
  /// - The FileCodeModel does not know anything about global function/class declarations. It knows only about definitions.
  ///   In other words, using a text point that is in the middle of the function name of a function declaration and asking
  ///   the FileCodeModel.CodeElementFromPoint() about information, it returns none.
  /// 
  /// On the other hand, Visual Studio clearly knows semantic information, including declarations. The objects that know
  /// it are, however, not accessible via a documented API. But we can still access one nevertheless: the SemanticTokensCache.
  /// We expose it via the dedicated CppFileSemanticsFromSemanticTokensCache class. For more information, see there. It knows
  /// about global function/class declaration. Moreover, it knows the span of the semantic C++ elements. But besides being an
  /// internal VS implementation details, it also does not provide 100% of the information that we need:
  /// - The SemanticTokensCache does not know about non-type template parameters.
  /// - It also does not know about macro parameters.
  /// 
  /// So, the idea of CppFileSemanticsFromVSCodeModelAndCache is to use both sources as information. We first query the
  /// SemanticTokensCache for the next C++ elements. This especially gives us the span of the element. Using the middle point
  /// of the span, we can then query the FileCodeModel for that text point. If the FileCodeModel returns information, we 
  /// believe the FileCodeModel (because it knows about non-type template parameters and macro parameters; also, it knows
  /// additional things that we might want to know about in the future). If the FileCodeModel does not know anything, we simply
  /// use the (limited) information available from the SemanticTokensCache. With this combination, we get efficient lookup
  /// of the next C++ element and support decelarations as good as possible. The only thing that does not work at all are
  /// non-type template parameters in function/class declarations.
  /// 
  /// Note: An alternative would have been to write a custom parser. But that is of course quite hard for C++.
  /// </summary>
  class CppFileSemanticsFromVSCodeModelAndCache : IVisualStudioCppFileSemantics
  {
    public CppFileSemanticsFromVSCodeModelAndCache(IVsEditorAdaptersFactoryService adapterService, ITextBuffer textBuffer)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      mTextBuffer = textBuffer;
      mSemanticCache = new CppFileSemanticsFromSemanticTokensCache(textBuffer);

      // In our case here we are interested in the "FileCodeModel" which is only accessible in the 'old' world, specifically
      // via "EnvDTE.Document". There is one "FileCodeModel" per "Document.ProjectItem" in the solution.
      var newOldMapper = new VisualStudioNewToOldTextBufferMapper(adapterService, textBuffer);
      mFileCodeModel = newOldMapper.Document?.ProjectItem?.FileCodeModel;
      mVsTextLines = newOldMapper.VsTextLines;
    }


    public FunctionInfo TryGetFunctionInfoIfNextIsAFunction(SnapshotPoint point)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      (var functionTokenIter, var templateParameterTokens) = mSemanticCache.TryGetSemanticFunctionInfoIfNextIsAFunction(point);
      if (functionTokenIter == null) {
        // Either the next thing after 'point' is not a function, or some error occurred. In the latter case we could try
        // to successively iterate through the following lines and query FileCodeModel for information. But in my experiments
        // the SemanticTokenCache never failed to get function information. So we don't do such a fallback.
        return null;
      }

      // The SemanticTokensCache interprets all 'SemanticTokenKind.cppType' elements as template parameters because
      // it doesn't have sufficient information. When we have e.g. a non-templated struct and the user types '@param'
      // above it, the non-templated struct is therefore seen as a template parameter. Also, return types can be
      // 'SemanticTokenKind.cppType'. To fix this, we query the FileCodeModel here whether the assumed template parameter
      // is one; the FileCodeModel returns a null CodeElement reference for template parameters.
      if (templateParameterTokens != null) {
        foreach (var paramToken in templateParameterTokens) {
          CodeElement paramCodeElement = TryGetCodeElementFor(paramToken);
          if (paramCodeElement != null) {
            var kind = paramCodeElement.Kind;
            if (kind != vsCMElement.vsCMElementFunction // vsCMElementFunction is returned for function return types.
                && kind != vsCMElement.vsCMElementParameter) { // vsCMElementParameter is just a guess if the FileCodeModel ever gets fixed.
              return null; // SemanticTokensCache was wrong. The next element after 'point' is not a function.
            }
          }
        }
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
      var parameters = new List<ParameterInfo>();
      foreach (CodeElement param in codeElement.Parameters) {
        string name = TrimAndStripEllipsis(param.Name);
        if (name != "") {
          string type = (param as VCCodeParameter)?.TypeString;
          parameters.Add(new ParameterInfo { Name = name, Type = type});
        }
      }
      var templateParameters = new List<string>();
      foreach (CodeElement param in codeElement.TemplateParameters) {
        string name = TrimAndStripEllipsis(param.Name);
        if (name != "") {
          templateParameters.Add(name);
        }
      }

      return new FunctionInfo { FunctionName = funcName, Parameters = parameters, TemplateParameters = templateParameters };
    }


    public ClassOrAliasInfo TryGetClassInfoIfNextIsATemplateClassOrAlias(SnapshotPoint point) 
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      // Note: Structure and reasoning the same as in TryGetFunctionInfoIfNextIsAFunction().
      // Especially: Prefer the FileCodeModel information since it contains information about non-type template parameters.

      (var classToken, var templateParameterTokens) = mSemanticCache.TryGetSemanticClassInfoIfNextIsATemplateClass(point);
      if (classToken == null) {
        return null;
      }

      // The SemanticTokensCache interprets all 'SemanticTokenKind.cppType' elements as template parameters because
      // it doesn't have sufficient information. When we have e.g. a non-templated struct and the user types '@tparam'
      // above it, the non-templated struct is therefore seen as a template parameter. To fix this, we query the
      // FileCodeModel here whether the assumed template parameter is actually one.
      if (templateParameterTokens != null) {
        foreach (var paramToken in templateParameterTokens) {
          CodeElement paramCodeElement = TryGetCodeElementFor(paramToken);
          // Note that the FileCodeModel returns null for template parameters. (The check for vsCMElementParameter is
          // just an educated guess in case the FileCodeModel gets ever improved.)
          if (paramCodeElement != null && paramCodeElement.Kind != vsCMElement.vsCMElementParameter) {
            return null; // SemanticTokensCache was wrong. The next element after 'point' is not a class/alias.
          }
        }
      }

      CodeElement codeElement = TryGetCodeElementFor(classToken);
      if (codeElement == null) {
        // Most likely we have a global class declaration. The FileCodeModel is buggy here and does not know about it.
        // Just return the info from the SemanticTokenCache. Unfortunately it lacks information about non-type template parameters.
        // Not much we can do here (except write a code parser ourselves...).
        return mSemanticCache.TryGetClassInfoIfNextIsATemplateClassOrAlias(point); 
      }

      string className = codeElement.Name;
      Debug.Assert(className == classToken.Text);

      string type = "Unknown";
      CodeElements templateParameters = null;
      if (codeElement is VCCodeClass cls) {
        templateParameters = cls.TemplateParameters;
        type = "Class";
      }
      else if (codeElement is VCCodeStruct st) {
        templateParameters = st.TemplateParameters;
        type = "Struct";
      }
      else if (codeElement is VCCodeUsingAlias us) {
        templateParameters = us.TemplateParameters;
        type = "Alias";
      }

      if (templateParameters == null) {
        return mSemanticCache.TryGetClassInfoIfNextIsATemplateClassOrAlias(point);
      }

      var templateParametersInfo = new List<string>();
      foreach (CodeElement param in templateParameters) {
        string name = TrimAndStripEllipsis(param.Name);
        if (name != "") {
          templateParametersInfo.Add(name);
        }
      }

      return new ClassOrAliasInfo { ClassName = className, Type = type, TemplateParameters = templateParametersInfo };
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
        string name = TrimAndStripEllipsis(param.Name);
        if (name != "") {
          parameters.Add(param.Name);
        }
      }

      return new MacroInfo { MacroName = macroName, Parameters = parameters };
    }


    public int TryGetEndPositionOfCppElementBefore(SnapshotPoint point, uint withinNumLinesBefore)
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      return mSemanticCache.TryGetEndPositionOfCppElementBefore(point, withinNumLinesBefore);
    }


    private bool IsFileCodeModelAvailable() 
    {
      return mTextBuffer != null && mVsTextLines != null && mFileCodeModel != null;
    }


    private CodeElement TryGetCodeElementFor(CppFileSemanticsFromSemanticTokensCache.SemanticToken token)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      if (token == null || !IsFileCodeModelAvailable()) {
        return null;
      }

      try {
        // We can query the FileCodeModel for a CodeElement only at positions (rather than for whole spans). The most
        // reliable thing to do seems to query the FileCodeModel for information in the middle of the elements name.
        int posInMiddleOfElemName = (token.Start + token.End) / 2;
        ITextSnapshotLine lineContainingElem = token.Span.Snapshot.GetLineFromPosition(posInMiddleOfElemName);

        var offsetInLine0Based = posInMiddleOfElemName - lineContainingElem.Start;
        Debug.Assert(offsetInLine0Based >= 0);
        var lineNumber0Based = lineContainingElem.LineNumber;

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


    private string TrimAndStripEllipsis(string str) 
    {
      if (str == null) {
        return "";
      }

      str = str.Trim();

      // The names of template parameter packs and their use as function parameters retain the "...". We don't want it.
      if (str.StartsWith("...")) { 
        str = str.Substring(3);
      }
      if (str.EndsWith("...")) {
        str = str.Substring(0, str.Length - 3);
      }

      return str;
    }


    private readonly ITextBuffer mTextBuffer;
    private readonly IVsTextLines mVsTextLines = null;
    private readonly FileCodeModel mFileCodeModel = null;
    private readonly CppFileSemanticsFromSemanticTokensCache mSemanticCache;
  }


  //==============================================================================
  // CppFileSemanticsFromSemanticTokensCache
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
  /// variables that are then later accessed by the main thread. To use IVCCodeStoreManager directly, we would 
  /// somehow need to execute code on the C++ parsing thread, which we can't.
  /// 
  /// The best place I could find to somehow get the semantic infos from the C++ parsing thread is via a property
  /// of a text buffer, which has the type 'SemanticTokensCache'. Every time the file changes, it gets updated. 
  /// There we do not have any threading issues, and it knows about global function declarations. In other words,
  /// the 'SemanticTokensCache' is updated regularly by the C++ parsing thread, and we can use the information
  /// on the 'SemanticTokensCache'.
  /// 
  /// Caveats: The SemanticTokensCache does not know about non-type template parameters (NTTP) (for example an
  /// integer as template argument). Also, for parameters etc. it does not store the actual type. And it is just
  /// a 'stream' of tokens without additional semantics. For example, a SemanticTokenKind.cppType can occur for
  /// a template parameter or a return type or a function parameter. There are many ambiguities in the infos
  /// that the SemanticTokensCache carries. So using it comes with all sorts of cases where it doesn't work. But
  /// it was the only way I could find to get at least somewhat decent information for global declarations without
  /// attempting to write a custom C++ parser.
  /// </summary>
  class CppFileSemanticsFromSemanticTokensCache : IVisualStudioCppFileSemantics 
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
      cppClassTemplate, // Also structs and templated-using-alias.
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
        || kind == SemanticTokenKind.cppFunctionTemplate
        || kind == SemanticTokenKind.cppOperator
        || kind == SemanticTokenKind.cppMemberOperator;
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
    /// to the extension code. It encapsulates the reflection magic which is necessary because we don't want to have
    /// explicit dependencies to VS internals.
    /// </summary>
    private class VSSemanticTokensCache
    {
      public VSSemanticTokensCache(object semanticsTokenCache)
      {
        mSemanticsTokenCache = semanticsTokenCache;
        Debug.Assert(mSemanticsTokenCache != null);
        mGetTokensMethod = mSemanticsTokenCache?.GetType().GetMethod("GetTokens");
      }

      public IEnumerable<SemanticToken> GetTokens(NormalizedSnapshotSpanCollection spans)
      {
        if (mSemanticsTokenCache == null || mGetTokensMethod == null) {
          yield break;
        }

        int outVersion = 0;
        object[] args = new object[] { spans, outVersion };
        IEnumerable allTokens = mGetTokensMethod.Invoke(mSemanticsTokenCache, args) as IEnumerable;
        if (allTokens != null) {
          foreach (object token in allTokens) {
            if (token != null) {
              yield return GetSemanticTokenInfo(token);
            }
          }
        }
      }

      private SemanticToken GetSemanticTokenInfo(object semanticToken)
      {
        Debug.Assert(semanticToken != null);

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


    public CppFileSemanticsFromSemanticTokensCache(ITextBuffer textBuffer) 
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
        // Note: The SemanticTokensCache does not know about non-type template parameters unfortunately.
        // Note: If the return type of a function is a type, we get also some CppTypes before. Possibly multiple, e.g.
        // in case of "std::pair<Type1, Type2>". So we actually get a mixture of template parameters and return types here.
        // Unfortunately, there seems to be no simple way to distinguish them without parsing the text ourselves. The best
        // idea might be to exploit the assumption that "point" is in a comment before the function. We can then simply check
        // whether "template <" comes before the very first token. This isn't foolproof, however, because NTTP don't appear in
        // the SematicTokensCache (so all sorts of weird expression might come between "template <" and the first token we see
        // here). For now, we leave it like this; the worst thing that happens is that the return types appear in the
        // autocomplete of "@tparam".
        if (token.SemanticTokenKind == SemanticTokenKind.cppType) {
          if (tokensBeforeThatAreCppTypes == null) {
            tokensBeforeThatAreCppTypes = new List<SemanticToken>();
          }
          tokensBeforeThatAreCppTypes.Add(token);
        }
        // For example, if we have "template <std::integral T>", the "std" appears as cppNamespace and the "integral"
        // as "cppClassTemplate". Ignore them. This means that we increase the chance for wrong results when the user
        // types "@param" before something that is not a function, but this seems less like an issue than not showing
        // information for actual functions. "cppMacro" in case the type is given by macro.
        else if (token.SemanticTokenKind == SemanticTokenKind.cppNamespace
                 || token.SemanticTokenKind == SemanticTokenKind.cppClassTemplate
                 || token.SemanticTokenKind == SemanticTokenKind.cppMacro) {
          continue;
        }
        else {
          if (IsFunction(token.SemanticTokenKind)) {
            return (enumerator, tokensBeforeThatAreCppTypes);
          }
          // Stop if hit anything that cannot be part of a function.
          return (null, null);
        }
      }

      return (null, null);
    }


    public ClassOrAliasInfo TryGetClassInfoIfNextIsATemplateClassOrAlias(SnapshotPoint point)
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
      return new ClassOrAliasInfo { ClassName = classToken.Text, Type = "Class", TemplateParameters = templateNames };
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
      SemanticTokenKind kindBefore = SemanticTokenKind.cppNone;
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
          kindBefore = token.SemanticTokenKind;
        }
        // For example, if we have "template <std::integral T>", the "std" appears as cppNamespace and the "integral"
        // as "cppClassTemplate". Ignore them. This means that we increase the chance for wrong results when the user
        // types "@tparam" before something that is not a class/alias, but this seems less like an issue than not showing
        // information for actual class/alias. "cppMacro" in case the type is given by macro.
        else if (token.SemanticTokenKind == SemanticTokenKind.cppNamespace
                 || (token.SemanticTokenKind == SemanticTokenKind.cppClassTemplate && kindBefore == SemanticTokenKind.cppNamespace)
                 || token.SemanticTokenKind == SemanticTokenKind.cppMacro) {
          kindBefore = token.SemanticTokenKind;
        }
        // cppClassTemplate: Also structs and templated-using-alias.
        else if (token.SemanticTokenKind == SemanticTokenKind.cppClassTemplate) {
          return (token, tokensBeforeThatAreCppTypes);
        }
        else {
          // Stop if hit anything else that cannot be part of a class/struct/templated-using-alias.
          return (null, null);
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
        if (token == null) { 
          continue; 
        }
        if (token.SemanticTokenKind == SemanticTokenKind.cppMacro) { 
          return token;
        }
        // Stop if hit anything that cannot be part of a macro.
        return null;
      }
      return null;
    }


    public int TryGetEndPositionOfCppElementBefore(SnapshotPoint point, uint withinNumLinesBefore)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      int searchStartLineNumber0Based = Math.Max(0, point.GetContainingLineNumber() - (int)withinNumLinesBefore);
      SnapshotPoint searchStartPoint = point.Snapshot.GetLineFromLineNumber(searchStartLineNumber0Based).Start;
      var tokens = GetTokensIn(new SnapshotSpan(searchStartPoint, point));
      if (tokens != null) {
        // Note: The following line is potentially expensive because it retrieves all tokens in the span, stores them
        // and then returns them reversed. So the 'yield' mechanism underlying GetTokensAfter() is useless. Unfortunately,
        // the SemanticTokensCache does not provide a reverse enumerator. This is the reason for the 'withinNumLinesBefore'
        // parameter, so that the number of retrieved tokens is limited. 
        var reversedTokens = tokens.Reverse();
        SemanticToken nearestTokenBeforePoint = reversedTokens.FirstOrDefault();
        if (nearestTokenBeforePoint != null) {
          Debug.Assert(nearestTokenBeforePoint.End <= point.Position);
          return nearestTokenBeforePoint.End;
        }
      }

      return searchStartPoint.Position;
    }


    private IEnumerable<SemanticToken> GetTokensAfter(SnapshotPoint point)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      // Get an enumerator for the tokens starting at 'point' till the end of the file. They are retriveved lazily ('yield'),
      // so it is not a performance issue that we try to get all of them till the end of the file. As long as we don't try
      // to retrieve all of them.
      return GetTokensIn(new SnapshotSpan(mTextBuffer.CurrentSnapshot, point.Position, mTextBuffer.CurrentSnapshot.Length - point.Position));
    }


    private IEnumerable<SemanticToken> GetTokensIn(SnapshotSpan span)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      var vsCache = FindVSSemanticsTokenCache();
      if (vsCache == null) {
        return null;
      }

      var spanCollection = new NormalizedSnapshotSpanCollection(span);
      return vsCache.GetTokens(spanCollection);
    }


    private FunctionInfo GetFunctionInfoFromFunctionToken(
        IEnumerator<SemanticToken> tokenIter, 
        IEnumerable<SemanticToken> tokensBeforeThatAreCppTypes) 
    {
      SemanticTokenKind functionKind = tokenIter.Current.SemanticTokenKind;
      Debug.Assert(IsFunction(functionKind));
      string functionName = tokenIter.Current.Text;

      var parameters = new List<ParameterInfo>();
      while (tokenIter.MoveNext()) {
        SemanticToken token = tokenIter.Current;
        // 'cppType' appears for parameters whose type is a template argument, and also for class declarations in
        // the parameter list (e.g. `void foo(class MyClass c);`). So we need to skip them.
        if (token.SemanticTokenKind == SemanticTokenKind.cppType) {
          continue;
        }
        // For example, if we have "template <std::integral T>", the "std" appears as cppNamespace and the "integral"
        // as "cppClassTemplate". Ignore them. This means that we increase the chance for wrong results when the user
        // types "@param" before something that is not a function, but this seems less like an issue than not showing
        // information for actual functions. "cppMacro" in case the type is given by macro.
        else if (token.SemanticTokenKind == SemanticTokenKind.cppNamespace
                 || token.SemanticTokenKind == SemanticTokenKind.cppClassTemplate
                 || token.SemanticTokenKind == SemanticTokenKind.cppMacro) {
          continue;
        }
        else if (token.SemanticTokenKind != SemanticTokenKind.cppParameter) {
          // Reached end of parameter list.
          break;
        }

        // The SemanticTokensCache does not provide information about the type the parameter (with some exceptions).
        // We therefore use a simple heuristic to get the type. We assume that the whole type is on a single line.
        // There are cases where this will be wrong, but in most cases it should be correct. The simple heuristic
        // should be reasonable considering that the type will just appear in the autocomplete box as additional info.
        ITextSnapshotLine lineContainingParameter = token.Span.Start.GetContainingLine();
        int paramNameOffsetInLine = token.Start - lineContainingParameter.Start.Position;
        Debug.Assert(paramNameOffsetInLine >= 0);
        string paramType = SemanticsUtilities.ExtractTypeOfParam(lineContainingParameter.GetText().Substring(0, paramNameOffsetInLine));

        parameters.Add(new ParameterInfo { Name = token.Text, Type = paramType });
      }

      // The tokens of type 'cppType' coming directly before a function are the template parameters of that function.
      var templateParameters = Enumerable.Empty<string>();
      if (tokensBeforeThatAreCppTypes != null) {
        templateParameters = tokensBeforeThatAreCppTypes.Select(t => t.Text).ToList();
      }

      return new FunctionInfo { FunctionName = functionName, Parameters = parameters, TemplateParameters = templateParameters };
    }


    private VSSemanticTokensCache FindVSSemanticsTokenCache() 
    {
      if (mVSSemanticTokensCache == null) {
        string name = "Microsoft.VisualStudio.VC.SemanticTokensCache".ToUpper();
        foreach (var kvp in mTextBuffer.Properties.PropertyList) {
          if (kvp.Key.ToString().ToUpper() == name) {
            mVSSemanticTokensCache = new VSSemanticTokensCache(kvp.Value);
            break;
          }
        }
      }

      return mVSSemanticTokensCache;
    }


    private readonly ITextBuffer mTextBuffer;
    private VSSemanticTokensCache mVSSemanticTokensCache;
  }


  //==============================================================================
  // Utilities
  //==============================================================================

  public static class SemanticsUtilities
  {
    /// <summary>
    /// Given a line such as
    ///    "void function(int param1, double param2)"
    /// and the goal to get the type of e.g. "param2", this function here expects the part of the line
    /// before "param2", i.e. it expects the string
    ///    "void function(int param1, double "
    /// It then tries to get the type of "param2", in this example "double". It basically searches backwards 
    /// till the next comma, skipping over commas in nested parentheses.
    /// 
    /// However, this is just a simple heuristic. It breaks down in at least the following cases:
    /// - If the type stretches over multiple lines. We simply assume that the whole type is on a single line.
    /// - If there are C-style comments or string literals in the middle.
    /// - In case of array parameters ("void func(int arr[])"), the "[]" is after the name, and therefore
    ///   will not be included.
    /// </summary>
    public static string ExtractTypeOfParam(string lineOfParamBeforeParamName)
    {
      if (lineOfParamBeforeParamName == null || lineOfParamBeforeParamName.Length == 0) {
        return null;
      }

      int? idx = lineOfParamBeforeParamName.Length - 1;
      while (idx.HasValue && idx >= 0) {
        int idxVal = idx.Value;
        char c = lineOfParamBeforeParamName[idxVal];
        // Skip over portions of the string that are enclosed in matching parentheses, brackets, etc.
        // Note: We simply assume that there are no C-style comments "/*...*/" or string literals.
        if (c == ')') {
          idx = FindOpeningOfBalancedPairBackward(lineOfParamBeforeParamName, idxVal, '(');
        }
        else if (c == '>') {
          idx = FindOpeningOfBalancedPairBackward(lineOfParamBeforeParamName, idxVal, '<');
        }
        else if (c == '}') {
          idx = FindOpeningOfBalancedPairBackward(lineOfParamBeforeParamName, idxVal, '{');
        }
        else if (c == ']') {
          idx = FindOpeningOfBalancedPairBackward(lineOfParamBeforeParamName, idxVal, '[');
        }
        else if (c == ',') {
          break; // We found the end of the previous function parameter. So everything after the current index is the type.
        }
        else if (c == '(') {
          break; // We found the start of the function parameter list.
        }
        else {
          idx = idxVal - 1;
        }
      }

      if (!idx.HasValue) {
        // Unbalanced parentheses, so the type is stretching over multiple lines.
        return null;
      }

      // We either found the comma that indicates the end of the previous parameter or the '(' that starts the function parameter list.
      // In this case, the type of our parameter is everything afterwards.
      // Or we hit the start of the string. In this case we take a leap of faith and assume that everything afterwards is the type. If
      // the type stretches over multiple lines (e.g. "long \n double paramName"), we are wrong. We simply ignore this.
      return lineOfParamBeforeParamName.Substring(Math.Max(0, idx.Value + 1)).Trim();
    }


    // For example, if str[closingIdx] == ')' and openingChar == '(', searches the str backwards
    // until the openingChar is found, taking into account additional nesting. Returns the index
    // of the character before the found opening character.
    // Returns a negative value if none was found.
    private static int? FindOpeningOfBalancedPairBackward(string str, int closingIdx, char openingChar)
    {
      char closingChar = str[closingIdx];
      int level = 1;
      int idx = closingIdx - 1;
      for (; idx >= 0 && level >= 1; --idx) {
        char c = str[idx];
        if (c == closingChar) {
          ++level;
        }
        else if (c == openingChar) {
          --level;
        }
      }

      if (idx < 0) {
        return null;
      }

      return idx;
    }
  }

}
