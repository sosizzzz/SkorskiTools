using SkorskiTools.App.Services;
using SkorskiTools.Core.Services;

namespace SkorskiTools.App.ViewModels.Pages;

public sealed class ApplicationsViewModel : ApplicationListViewModel
{
    public ApplicationsViewModel(
        IInstallationDetector detector,
        IApplicationInstaller installer,
        IConfirmationService confirmation,
        IFullInstallRunner fullInstall,
        ISettingsService settings,
        IOperationHistoryService history)
        : base(detector, installer, confirmation, fullInstall, settings, history)
    {
    }

    public override string Title => "Applications";

    public string Subtitle => "Install, detect, uninstall and reinstall conventional applications.";
}
