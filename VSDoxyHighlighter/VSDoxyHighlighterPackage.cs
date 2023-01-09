using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace VSDoxyHighlighter
{
  [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
  [InstalledProductRegistration(
    "VSDoxyHighlighter",
    "Extension for Visual Studio to provide syntax highlighting for doxygen/javadoc style comments in C/C++. Github page: https://github.com/Sedeniono/VSDoxyHighlighter", 
    "1.0.1")]
  [ProvideOptionPage(typeof(GeneralOptionsPage), VSDoxyHighlighterPackage.MainSettingsCategory, GeneralOptionsPage.PageCategory, 0, 0, true)]
  [ProvideProfile(typeof(GeneralOptionsPage), VSDoxyHighlighterPackage.MainSettingsCategory, GeneralOptionsPage.PageCategory, 0, 0, true)]
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
