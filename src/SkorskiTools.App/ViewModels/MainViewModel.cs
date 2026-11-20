using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SkorskiTools.App.Infrastructure;
using SkorskiTools.App.Services;
using SkorskiTools.App.ViewModels.Pages;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Services;
using Wpf.Ui.Controls;

namespace SkorskiTools.App.ViewModels;

/// <summary>Holds navigation state, the active page and the loaded catalog.</summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IResourceCatalogService _catalogService;
    private readonly DashboardViewModel _dashboard;
    private readonly ApplicationsViewModel _applications;
    private readonly BrowsersViewModel _browsers;
    private readonly PortablesViewModel _portables;
    private readonly AuxiliaryViewModel _auxiliary;
    private readonly ScriptsViewModel _scripts;

    public override string Title => "Skorski Tools";

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public UserFoldersResult UserFolders { get; }

    public string VersionText => $"v{AppVersion.Current}";

    public string StatusText => $"User files: {UserFolders.RootPath}";

    [ObservableProperty]
    private string _catalogSummary = "Loading catalog...";

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainViewModel(
        UserFoldersResult userFolders,
        IResourceCatalogService? catalogService = null,
        IInstallationDetector? detector = null,
        IApplicationInstaller? installer = null,
        IConfirmationService? confirmation = null)
    {
        UserFolders = userFolders;
        _catalogService = catalogService ?? new ResourceCatalogService();

        var installationDetector = detector ?? new InstallationDetector();
        var applicationInstaller = installer ?? new ApplicationInstaller();
        var confirmationService = confirmation ?? new ConfirmationService();

        _dashboard = new DashboardViewModel();
        _applications = new ApplicationsViewModel(installationDetector, applicationInstaller, confirmationService);
        _browsers = new BrowsersViewModel(installationDetector, applicationInstaller, confirmationService);
        _portables = new PortablesViewModel();
        _auxiliary = new AuxiliaryViewModel();
        _scripts = new ScriptsViewModel();

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Dashboard", SymbolRegular.Home24, _dashboard),
            new("Applications", SymbolRegular.Grid24, _applications),
            new("Browsers", SymbolRegular.Globe24, _browsers),
            new("Portables", SymbolRegular.Toolbox24, _portables),
            new("Auxiliary", SymbolRegular.Folder24, _auxiliary),
            new("Scripts", SymbolRegular.Script24, _scripts),
            new("Settings", SymbolRegular.Settings24, new SettingsViewModel()),
        };

        _selectedItem = NavigationItems[0];
        _currentPage = NavigationItems[0].Page;
    }

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (value is not null)
            CurrentPage = value.Page;
    }

    [RelayCommand]
    private void Navigate(NavigationItem item) => SelectedItem = item;

    public async Task LoadCatalogAsync()
    {
        try
        {
            var resourcesDirectory = AppPaths.FindResourcesDirectory();
            if (resourcesDirectory is null)
            {
                CatalogSummary = "Resources directory not found.";
                Log.Warning("Resources directory could not be located");
                return;
            }

            var catalog = await _catalogService.LoadAsync(resourcesDirectory).ConfigureAwait(true);
            var categoryNames = catalog.Categories.ToDictionary(
                c => c.Id, c => c.Name, StringComparer.OrdinalIgnoreCase);

            _applications.SetApplications(catalog.Applications, categoryNames);
            _browsers.SetApplications(catalog.Browsers, categoryNames);
            _portables.Load(catalog.Portables.Select(m => ResourceItemViewModel.From(m, categoryNames)));
            _auxiliary.Load(catalog.Auxiliary.Select(m => ResourceItemViewModel.From(m, categoryNames)));
            _scripts.Load(catalog.Scripts.Select(ResourceItemViewModel.FromScript));

            CatalogSummary =
                $"{catalog.Applications.Count} applications · {catalog.Browsers.Count} browsers · " +
                $"{catalog.Portables.Count} portables · {catalog.Auxiliary.Count} auxiliary · " +
                $"{catalog.Scripts.Count} scripts";

            if (catalog.Issues.Count > 0)
                CatalogSummary += $" · {catalog.Issues.Count} catalog issue(s)";

            _dashboard.SetSummary(CatalogSummary);

            Log.Information(
                "Catalog applied to UI: applications {A}, browsers {B}, portables {P}, auxiliary {X}, scripts {S}",
                _applications.Items.Count, _browsers.Items.Count, _portables.Items.Count,
                _auxiliary.Items.Count, _scripts.Items.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load the catalog");
            CatalogSummary = "The catalog could not be loaded. See the log for details.";
            _dashboard.SetSummary(CatalogSummary);
        }
    }

    /// <summary>Loads the catalog and runs the initial installation detection.</summary>
    public async Task LoadCatalogAndDetectAsync()
    {
        await LoadCatalogAsync();
        await Task.WhenAll(_applications.DetectAllAsync(), _browsers.DetectAllAsync());
    }

    [RelayCommand]
    private async Task RefreshCatalogAsync() => await LoadCatalogAndDetectAsync();
}
