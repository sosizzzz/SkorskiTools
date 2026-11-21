using SkorskiTools.App.Services;
using SkorskiTools.Core.Services;

namespace SkorskiTools.App.ViewModels.Pages;

public sealed class PortablesViewModel : PortableListViewModel
{
    public PortablesViewModel(
        IPortableService portables,
        IConfirmationService confirmation,
        IFullInstallRunner fullInstall,
        ISettingsService settings)
        : base(portables, confirmation, fullInstall, settings)
    {
    }

    public override string Title => "Portables";

    public string Subtitle => "Deploy, replace and remove portable applications.";
}
