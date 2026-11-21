using SkorskiTools.App.Services;
using SkorskiTools.Core.Services;

namespace SkorskiTools.App.ViewModels.Pages;

public sealed class BrowsersViewModel : ApplicationListViewModel
{
    public BrowsersViewModel(
        IInstallationDetector detector,
        IApplicationInstaller installer,
        IConfirmationService confirmation,
        IFullInstallRunner fullInstall,
        ISettingsService settings)
        : base(detector, installer, confirmation, fullInstall, settings)
    {
    }

    public override string Title => "Browsers";

    public string Subtitle => "Browsers are managed with the same logic as applications.";
}
