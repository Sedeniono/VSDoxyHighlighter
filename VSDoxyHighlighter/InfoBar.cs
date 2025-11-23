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
    ///          actions: new(string uiText, bool asButton, Func<bool /*closeInfoBar*/> callback)[] { 
    ///            ("yes", false, () => { MessageBox.Show("You clicked Yes!"); return false; }),
    ///            ("no", false, () => { MessageBox.Show("You clicked No!"); return false; }),
    ///          }
    ///        );
    ///        
    /// Note: Must be called from the main thread!
    /// 
    /// Implementation based on https://github.com/onlyutkarsh/InfoBarDemo
    /// </summary>
    /// 
    /// <param name="icon">Use the members of KnownMonikers. E.g. KnownMonikers.StatusInformation</param>
    /// <param name="message">The displayed message.</param>
    /// <param name="actions">For each element in the array, a hyperlink or button with the given string as message gets created.
    ///   When the user clicks on one of these, the given callback gets executed. If the callback returns true, the info bar is
    ///   closed afterwards. If the callback return false, the info bar stays open.
    ///   Set the actions parameter to "null" or an empty array in case you do not need any interactive elements (or use the 
    ///   dedicated overload without an "actions" parameter).
    ///   </param>
    public static void ShowMessage(
        ImageMoniker icon, 
        string message, 
        bool showCloseButton,
        (string uiText, bool asButton, Func<bool /*closeInfoBar*/> callback)[] actions)
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

      var actionItems = new List<IVsInfoBarActionItem>();
      if (actions != null) {
        foreach (var action in actions) {
          if (action.asButton) {
            actionItems.Add(new InfoBarButton(action.uiText, action.callback));
          }
          else {
            actionItems.Add(new InfoBarHyperlink(action.uiText, action.callback));
          }
        }
      }

      InfoBarModel infoBarModel = new InfoBarModel(message, actionItems, icon, isCloseButtonVisible: showCloseButton);

      IVsInfoBarUIElement uiElement = infoBarFactory.CreateInfoBar(infoBarModel);
      if (actionItems.Count > 0) {
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
      ShowMessage(icon, message, true, null);
    }


    /// <summary>
    /// Helper class containing the callbacks from the actionItems in the info bar message.
    /// </summary>
    private class InfoBarEvents : IVsInfoBarUIEvents
    {
      public InfoBarEvents() { }

      public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
      {
        ThreadHelper.ThrowIfNotOnUIThread();
        var callback = (Func<bool>)actionItem.ActionContext;
        if (callback()) {
          infoBarUIElement.Close();
        }
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
