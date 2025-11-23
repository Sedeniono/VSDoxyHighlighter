using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace VSDoxyHighlighter
{
  [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
  
  // InstalledProductRegistration: Causes the extension to show up in the Visual Studio Help->About dialog.
  [InstalledProductRegistration(
    "VSDoxyHighlighter",
    "Provides syntax highlighting, IntelliSense and quick infos for doxygen/javadoc style comments in C/C++. Github page: https://github.com/Sedeniono/VSDoxyHighlighter",
    "1.9.0")]
  
  // ProvideOptionPage: Causes VS to show a page in the VS options.
  // The resource IDs refer to the Resources.resx file.
  // The categoryName is the name of the main node that appears in the VS options: "VSDoxyHighlighter"
  // The pageName is the name of the child node of the main node: "General"
  [ProvideOptionPage(
    typeof(GeneralOptionsPage), 
    categoryName: MainSettingsCategory, 
    pageName: GeneralOptionsPage.PageCategory, 
    categoryResourceID: 300, 
    pageNameResourceID: 301, 
    supportsAutomation: true)]

  // ProvideProfile: Causes VS to show a node in the import & export settings dialog.
  // The resource IDs refer to the Resources.resx file.
  // For some reason that I don't understand, the objectName is the name that appears in the import & export settings dialog, so this is set to "VSDoxyHighlighter".
  // On the other hand, the ProvideProfile.categoryName does not appear in the dialog, only in the vssettings file. I think the ProvideProfile.categoryName is something
  // entirely different than the ProvideOptionPage.categoryName. So we just use some "arbitrary" sensible string for ProvideProfile.categoryName.
  // The DescriptionResourceID shows up as description in the import & export settings dialog for the "VSDoxyHighlighter" node.
  [ProvideProfile(
    typeof(GeneralOptionsPage), 
    categoryName: "VSDoxyHighlighterSettings", 
    objectName: MainSettingsCategory, 
    categoryResourceID: 303, 
    objectNameResourceID: 300, 
    isToolsOptionPage: true, 
    DescriptionResourceID = 302)]
  
  [Guid(VSDoxyHighlighterPackage.PackageGuidString)]
  public sealed class VSDoxyHighlighterPackage : AsyncPackage
  {
    public static IGeneralOptions GeneralOptions {
      get {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (mGeneralOptions == null) {
          LoadPackage();
        }
        return mGeneralOptions;
      }
    }

    public static CommentParser CommentParser {
      get {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (mCommentParser == null) {
          LoadPackage();
        }
        return mCommentParser;
      }
    }

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
      await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

      mGeneralOptions = (GeneralOptionsPage)GetDialogPage(typeof(GeneralOptionsPage));
      mDoxygenCommands = new DoxygenCommands(mGeneralOptions);
      mCommentParser = new CommentParser(mDoxygenCommands);
      
      HandleRateNoticeAfterLoad();
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing) {
        mCommentParser?.Dispose();
        mDoxygenCommands?.Dispose();
      }
      base.Dispose(disposing);
    }


    // Loads our package, especially getting the options pages.
    private static void LoadPackage() 
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      if (mInLoadPackage) {
        throw new VSDoxyHighlighterException("VSDoxyHighlighter: Recursive LoadPackage() detected.");
      }
      mInLoadPackage = true;

      try {
        var vsShell = (IVsShell)ServiceProvider.GlobalProvider.GetService(typeof(IVsShell));
        if (vsShell == null) {
          // Note: Not attempting to show an info bar, since showing the info bar requires an IVsShell, too.
          ActivityLog.LogError("VSDoxyHighlighter", "Failed to get IVsShell service.");
          throw new VSDoxyHighlighterException("VSDoxyHighlighter: Failed to get IVsShell service.");
        }

        var errorCode = vsShell.LoadPackage(ref PackageGuid, out IVsPackage loadedPackage);
        if (errorCode != Microsoft.VisualStudio.VSConstants.S_OK) {
          string errorMessage = $"VSDoxyHighlighter: Failed to load package. Error code: 0x{errorCode:X} = {errorCode}.";
          ActivityLog.LogError("VSDoxyHighlighter", errorMessage);
          InfoBar.ShowMessage(KnownMonikers.StatusError, errorMessage);
          throw new VSDoxyHighlighterException(errorMessage);
        }
      }
      finally { 
        mInLoadPackage = false;
      }
    }


    enum RateNoticeAction
    {
      None,
      Initialize,
      Show,
    }


    private void HandleRateNoticeAfterLoad()
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      switch (GetRateNoticeAction()) {
        case RateNoticeAction.Initialize:
          // 30 days after the extension got loaded for the first time seems like a reasonable date that gave
          // users enough time to try out the extension.
          mGeneralOptions.SaveRateNoticeDate(DateTime.Now.AddDays(30));
          break;
        case RateNoticeAction.Show:
          ShowRateNoticeNow();
          break;
      }
    }


    private RateNoticeAction GetRateNoticeAction()
    {
      if (string.IsNullOrEmpty(mGeneralOptions.RateNoticeDateStr)) {
        return RateNoticeAction.Initialize;
      }

      if (DateTime.TryParseExact(mGeneralOptions.RateNoticeDateStr, "o", CultureInfo.InvariantCulture,
              DateTimeStyles.RoundtripKind, out DateTime rateNoticeDate)) {
        if (DateTime.Now >= rateNoticeDate) {
          return RateNoticeAction.Show;
        }
        else {
          return RateNoticeAction.None;
        }
      }
      else {
        ActivityLog.LogError("VSDoxyHighlighter", $"Failed to parse RateNoticeDateStr '{mGeneralOptions.RateNoticeDateStr}'. Resetting...");
        return RateNoticeAction.Initialize;
      }
    }


    private void ShowRateNoticeNow()
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      InfoBar.ShowMessage(
        icon: KnownMonikers.StatusInformation,
        message: "You are using the VSDoxyHighlighter extension which provides syntax highlighting, IntelliSense and " +
          "quick infos for doxygen/javadoc style comments. If you find it useful, please consider giving the " +
          "project a star on GitHub and leaving a rating on the Visual Studio Marketplace.",
        showCloseButton: false,
        actions: new (string uiText, bool asButton, Func<bool /*closeInfoBar*/> callback)[] {
          (
            uiText: "Open GitHub page",
            asButton: false,
            callback: () => {
              Process.Start(new ProcessStartInfo("https://github.com/Sedeniono/VSDoxyHighlighter") { UseShellExecute = true });
              return false;
            }
          ),
          (
            uiText: "Open Marketplace",
            asButton: false,
            callback: () => {
              Process.Start(new ProcessStartInfo("https://marketplace.visualstudio.com/items?itemName=Sedenion.VSDoxyHighlighter") { UseShellExecute = true });
              return false;
            }
          ),
          (
            uiText: "Remind me in 5 days",
            asButton: true,
            callback: () => {
              // We don't choose 7 days and instead some "uneven" number of days and hours to prevent
              // the notice from appearing again at the same time of day and the same day of the week,
              // in case it originally interrupted a daily or weekly meeting.
              mGeneralOptions.SaveRateNoticeDate(DateTime.Now.AddDays(5).AddHours(4));
              return true;
            }
          ),
          (
            uiText: "Never show this again",
            asButton: true,
            callback: () => { 
              mGeneralOptions.SaveRateNoticeDate(DateTime.MaxValue);
              return true;
            }
          )
        });
    }


    private const string PackageGuidString = "72c95c0a-4101-4d94-a4e0-5be5e28bdf02";
    private static Guid PackageGuid = new Guid(PackageGuidString);

    // The string that appears in the tools -> options dialog in the main list.
    private const string MainSettingsCategory = "VSDoxyHighlighter";

    private static GeneralOptionsPage mGeneralOptions;
    private static DoxygenCommands mDoxygenCommands;
    private static CommentParser mCommentParser;

    private static bool mInLoadPackage = false;
  }
}
