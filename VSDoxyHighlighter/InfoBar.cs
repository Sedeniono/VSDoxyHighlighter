using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;

namespace VSDoxyHighlighter
{
  internal static class InfoBar
  {
    /// <summary>
    /// Shows the given message at the top of the Visual Studio main window as an information bar.
    /// 
    /// For example:
    ///        InfoBar.ShowMessage(
    ///          icon: KnownMonikers.StatusError, 
    ///          message: "test message", 
    ///          actions: new(string, Action)[] { 
    ///            ("yes", () => MessageBox.Show("You clicked Yes!")),
    ///            ("no", () => MessageBox.Show("You clicked No!"))}
    ///        );
    ///        
    /// Note: Must be called from the main thread!
    /// 
    /// Implementation based on https://github.com/onlyutkarsh/InfoBarDemo
    /// </summary>
    /// 
    /// <param name="icon">Use the members of KnownMonikers. E.g. KnownMonikers.StatusInformation</param>
    /// <param name="message">The displayed message.</param>
    /// <param name="actions">For each element in the array, a hyperlink with the given string as message gets created.
    ///   When the user clicks on the hyperlinks, the given action gets executed. Use "null" or an empty array in case 
    ///   you do not need any hyperlinks (or the dedicated overload without an "actions" item.</param>
    public static void ShowMessage(ImageMoniker icon, string message, (string, Action)[] actions)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      var serviceProvider = ServiceProvider.GlobalProvider;
      IVsShell shell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
      if (shell == null) {
        ActivityLog.LogError("VSDoxyHighlighter", $"Failed to show info bar message because IVsShell is null. Message: {message}");
        return;
      }

      // Get the main window handle to host our InfoBar
      shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var obj);
      IVsInfoBarHost host = obj as IVsInfoBarHost;
      if (host == null) {
        ActivityLog.LogError("VSDoxyHighlighter", $"Failed to show info bar message because the main window is null. Message: {message}");
        return;
      }

      IVsInfoBarUIFactory infoBarFactory = serviceProvider.GetService(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
      if (infoBarFactory == null) {
        ActivityLog.LogError("VSDoxyHighlighter", $"Failed to show info bar message because the info bar factory is null. Message: {message}");
        return;
      }

      List<InfoBarHyperlink> hyperlinks = new List<InfoBarHyperlink>();
      if (actions != null) {
        foreach (var action in actions) {
          hyperlinks.Add(new InfoBarHyperlink(action.Item1, action.Item2));
        }
      }

      InfoBarModel infoBarModel = new InfoBarModel(message, hyperlinks, icon, isCloseButtonVisible: true);

      IVsInfoBarUIElement uiElement = infoBarFactory.CreateInfoBar(infoBarModel);
      if (hyperlinks.Count > 0) {
        InfoBarEvents events = new InfoBarEvents();
        uiElement.Advise(events, out events.mCookie);
      }
      host.AddInfoBar(uiElement);
    }


    /// <summary>
    /// Same as the other "ShowMessage()" function, except that it does not support actions.
    /// </summary>
    public static void ShowMessage(ImageMoniker icon, string message)
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      ShowMessage(icon, message, null);
    }


    /// <summary>
    /// Helper class containing the callbacks from the hyperlinks in the info bar message.
    /// </summary>
    private class InfoBarEvents : IVsInfoBarUIEvents
    {
      public InfoBarEvents() { }

      public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
      {
        ThreadHelper.ThrowIfNotOnUIThread();
        Action action = (Action)actionItem.ActionContext;
        action();
      }

      public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
      {
        ThreadHelper.ThrowIfNotOnUIThread();
        infoBarUIElement.Unadvise(mCookie);
      }

      public uint mCookie = 0;
    }
  }
}
