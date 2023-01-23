using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace VSDoxyHighlighter
{
  [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
  
  // InstalledProductRegistration: Causes the extension to show up in the Visual Studio Help->About dialog.
  [InstalledProductRegistration(
    "VSDoxyHighlighter",
    "Extension for Visual Studio to provide syntax highlighting for doxygen/javadoc style comments in C/C++. Github page: https://github.com/Sedeniono/VSDoxyHighlighter", 
    "1.0.2")]
  
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
    public static GeneralOptionsPage GeneralOptions {
      get {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (mGeneralOptions == null) {
          LoadPackage();
        }
        return mGeneralOptions;
      }
    }


    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
      await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
      mGeneralOptions = (GeneralOptionsPage)GetDialogPage(typeof(GeneralOptionsPage));
    }


    // Loads our package, especially getting the options pages.
    private static void LoadPackage() 
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      var vsShell = (IVsShell)ServiceProvider.GlobalProvider.GetService(typeof(IVsShell));
      if (vsShell == null) {
        ActivityLog.LogError("VSDoxyHighlighter", "Failed to get IVsShell service.");
        throw new Exception("VSDoxyHighlighter: Failed to get IVsShell service.");
      }

      var errorCode = vsShell.LoadPackage(ref PackageGuid, out IVsPackage loadedPackage);
      if (errorCode != Microsoft.VisualStudio.VSConstants.S_OK) {
        ActivityLog.LogError("VSDoxyHighlighter", $"Failed to load own package. Error code: {errorCode}");
        throw new Exception($"VSDoxyHighlighter: Failed to load own package. Error code: {errorCode}");
      }
    }


    private const string PackageGuidString = "72c95c0a-4101-4d94-a4e0-5be5e28bdf02";
    private static Guid PackageGuid = new Guid(PackageGuidString);

    // The string that appears in the tools -> options dialog in the main list.
    private const string MainSettingsCategory = "VSDoxyHighlighter";

    private static GeneralOptionsPage mGeneralOptions;
  }
}
