using CommunityToolkit.Mvvm.ComponentModel;
using SkorskiTools.App.Infrastructure;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Services;

namespace SkorskiTools.App.ViewModels.Pages;

/// <summary>Information and preferences about the current installation.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private bool _fullAutoEnabled;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        _fullAutoEnabled = settings.Current.FullAutoEnabled;
        settings.SettingsChanged += (_, _) => FullAutoEnabled = settings.Current.FullAutoEnabled;
    }

    public override string Title => "Settings";

    public string Subtitle => "Version information, paths and installation preferences.";

    public string VersionText => $"v{AppVersion.Current}";

    public string ResourcesPath =>
        AppPaths.FindResourcesDirectory() ?? "Resources directory not found";

    public string UserFilesPath => AppPaths.GetUserRoot();

    public string LogsPath => AppPaths.GetLogsDirectory();

    public string InstallationMode => "Manual";

    public string InstallationModeDescription =>
        "Installers are shown to the user and completed manually unless Full Auto is enabled.";

    public string FullAutoDescription =>
        "When enabled, every list shows selection checkboxes and a Full install button. " +
        "Selected resources are installed one by one with the silent switches declared in their manifests; " +
        "resources without verified switches are skipped.";

    public string FullAutoStatus => FullAutoEnabled ? "Enabled" : "Disabled";

    partial void OnFullAutoEnabledChanged(bool value)
    {
        _settings.SetFullAutoEnabled(value);
        OnPropertyChanged(nameof(FullAutoStatus));
    }
}
