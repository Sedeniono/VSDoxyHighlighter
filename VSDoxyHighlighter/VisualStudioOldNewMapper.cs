using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;


namespace VSDoxyHighlighter
{
  //==============================================================================
  // VisualStudioNewToOldTextBufferMapper
  //==============================================================================

  /// <summary>
  /// As far as I understand, Microsoft.VisualStudio.Text.ITextBuffer and similar classes are the "new" .NET managed classes.
  /// On the other hand, the stuff in the EnvDTE namespace (e.g. EnvDTE.Document and EnvDTE.TextDocument) represent 'old' classes,
  /// predating the .NET implementations. They are always COM interfaces. However, they are still relevant for certain things,
  /// e.g. the FileCodeModel. Things like IVsTextBuffer seem to be wrappers/adapters around the old classes. We can get from a 
  /// "new world" object (such as ITextBuffer) to the adapter via the IVsEditorAdaptersFactoryService service, resulting in e.g. 
  /// a IVsTextBuffer. (I think that service is just getting some object from ITextBuffer.Properties.) Digging through decompiled 
  /// VS .NET code, from the adapter we get to the "old world" object via IExtensibleObject. Note that the documentation of 
  /// IExtensibleObject states that it is Microsoft internal. We ignore this warning here. The only valid arguments to
  /// IExtensibleObject.GetAutomationObject() seem to be "Document" (giving an EnvDTE.Document) and "TextDocument" (giving
  /// an EnvDTE.TextDocument).
  /// </summary>
  struct VisualStudioNewToOldTextBufferMapper
  {
    public IVsTextBuffer VsTextBuffer { get; private set; }
    public IVsTextLines VsTextLines { get; private set; }
    public IExtensibleObject ExtensibleObject { get; private set; }

    public EnvDTE.Document Document {
      get {
        ThreadHelper.ThrowIfNotOnUIThread();
        object docObj = null;
        ExtensibleObject?.GetAutomationObject("Document", null, out docObj);
        return docObj as EnvDTE.Document;
      }
    }

    public EnvDTE.TextDocument TextDocument {
      get {
        ThreadHelper.ThrowIfNotOnUIThread();
        object docObj = null;
        ExtensibleObject?.GetAutomationObject("TextDocument", null, out docObj);
        return docObj as EnvDTE.TextDocument;
      }
    }

    public VisualStudioNewToOldTextBufferMapper(IVsEditorAdaptersFactoryService adapterService, ITextBuffer textBuffer)
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      VsTextBuffer = adapterService?.GetBufferAdapter(textBuffer);
      VsTextLines = VsTextBuffer as IVsTextLines;
      ExtensibleObject = VsTextBuffer as IExtensibleObject;
    }
  }
}
