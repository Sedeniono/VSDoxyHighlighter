using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System;
using Microsoft.VisualStudio.Shell;

namespace VSDoxyHighlighter
{
  //==============================================================================
  // IVisualStudioCppColorer
  //==============================================================================

  /// <summary>
  /// Wraps the access to Visual Studio's default tagger for C/C++ code.
  /// Should be disposed at the end to ensure that the event gets unsubscribed.
  /// 
  /// Also compare the comment in CommentExtractor.SplitIntoComments() for the reason why we need this.
  /// </summary>
  interface IVisualStudioCppColorer : IDisposable
  {
    /// <summary>
    /// Event that gets called when the default VS cpp colorer updated some colors. 
    /// For example, if you open a C-style comment ("/*"), the cpp colorer updates the text afterwards delayed. 
    /// This event is fired once this happens.
    /// </summary>
    event EventHandler<SnapshotSpanEventArgs> CppColorerReclassifiedSpan;

    /// <summary>
    /// Since the default Visual Studio tagger might not be available immediately when an instance 
    /// of IVisualStudioCppColorer gets created, this should be called before the start of any classification
    /// to ensure that the <CppColorerReclassifiedSpan> event is set up properly.
    /// </summary>
    void InitializeLazily();

    /// <summary>
    /// Returns the classification of the Visual Studio cpp colorer. Returns null if the Visual Studio colorer is 
    /// not yet available.
    /// </summary>
    IEnumerable<ITagSpan<IClassificationTag>> TryGetTags(NormalizedSnapshotSpanCollection spans);
  }


  //==============================================================================
  // DefaultVSCppColorerImpl
  //==============================================================================

  class DefaultVSCppColorerImpl : IVisualStudioCppColorer
  {
    public DefaultVSCppColorerImpl(ITextBuffer textBuffer) 
    {
      mTextBuffer = textBuffer;
      InitializeLazily();
    }


    public void InitializeLazily() 
    {
      FindDefaultVSCppTagger();
    }


    public void Dispose() 
    {
      if (mDisposed) {
        return;
      }
      mDisposed = true;

      // Unsubscribe from the TagsChanged event.
      ResetDefaultVSCppTagger();
    }


    public event EventHandler<SnapshotSpanEventArgs> CppColorerReclassifiedSpan;


    public IEnumerable<ITagSpan<IClassificationTag>> TryGetTags(NormalizedSnapshotSpanCollection spans)
    {
      // vsCppTagger.GetTags() seems to return no tags at all if we are not on the main thread.
      ThreadHelper.ThrowIfNotOnUIThread();

      var vsCppTagger = FindDefaultVSCppTagger();
      if (vsCppTagger == null) {
        return null;
      }

      try {
        return vsCppTagger.GetTags(spans);
      }
      catch (System.NullReferenceException ex) {
        // The "vsCppTagger" throws a NullReferenceException if one renames a file that has not a C++ ending (.cpp, .cc, etc.)
        // (and thus has initially no syntax highlighting) to a name with a C++ ending (e.g. .cpp). I think the tagger that
        // we find for the very first classification is the wrong one, or something like that. The problem vanishes when we
        // search the default tagger again when a re-classification gets triggered a bit later.
        ActivityLog.LogWarning(
          "VSDoxyHighlighter",
          $"The 'vsCppTagger' threw a NullReferenceException. Exception message: {ex.ToString()}");
        ResetDefaultVSCppTagger();
        return null;
      }
    }



    /// <summary>
    /// Retrives and setups the tagger of Visual Studio that is responsible for classifying C++ code. 
    /// </summary>
    private ITagger<IClassificationTag> FindDefaultVSCppTagger()
    {
      if (mDefaultVSCppTagger == null) {
        string nameOfDefaultCppTagger = "Microsoft.VisualC.CppColorer".ToUpper();
        foreach (var kvp in mTextBuffer.Properties.PropertyList) {
          if (kvp.Value is ITagger<IClassificationTag> casted) {
            if (kvp.Key.ToString().ToUpper() == nameOfDefaultCppTagger) {
              mDefaultVSCppTagger = casted;
              mDefaultVSCppTagger.TagsChanged += OnDefaultVSCppTaggerChangedTags;
              break;
            }
          }
        }
      }

      return mDefaultVSCppTagger;
    }


    private void ResetDefaultVSCppTagger()
    {
      if (mDefaultVSCppTagger != null) {
        mDefaultVSCppTagger.TagsChanged -= OnDefaultVSCppTaggerChangedTags;
        mDefaultVSCppTagger = null;
      }
    }


    private void OnDefaultVSCppTaggerChangedTags(object sender, SnapshotSpanEventArgs e) 
    {
      CppColorerReclassifiedSpan?.Invoke(this, e);
    }


    private readonly ITextBuffer mTextBuffer;

    // Cached tagger that is used by Visual Studio to classify C/C++ code.
    private ITagger<IClassificationTag> mDefaultVSCppTagger = null;

    private bool mDisposed = false;
  }
}

