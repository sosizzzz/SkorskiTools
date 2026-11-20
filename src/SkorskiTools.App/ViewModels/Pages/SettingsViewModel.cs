using SkorskiTools.App.Infrastructure;
using SkorskiTools.Core.Infrastructure;

namespace SkorskiTools.App.ViewModels.Pages;

/// <summary>Read-only information about the current installation and preferences.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    public override string Title => "Settings";

    public string Subtitle => "Version information, paths and installation preferences.";

    public string VersionText => $"v{AppVersion.Current}";

    public string ResourcesPath =>
        AppPaths.FindResourcesDirectory() ?? "Resources directory not found";

    public string UserFilesPath => AppPaths.GetUserRoot();

    public string LogsPath => AppPaths.GetLogsDirectory();

    public string InstallationMode => "Manual";

    public string InstallationModeDescription =>
        "Installers are shown to the user and completed manually in 1.0.";

    public bool FullAutoEnabled => false;

    public string FullAutoStatus => "Coming soon";

    public string FullAutoDescription =>
        "Unattended installation is not part of 1.0. This option does nothing.";
}
