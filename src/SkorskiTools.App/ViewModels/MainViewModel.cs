using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkorskiTools.App.Infrastructure;
using SkorskiTools.App.ViewModels.Pages;
using SkorskiTools.Core.Services;
using Wpf.Ui.Controls;

namespace SkorskiTools.App.ViewModels;

/// <summary>Holds navigation state and the currently displayed page.</summary>
public sealed partial class MainViewModel : ViewModelBase
{
    public override string Title => "Skorski Tools";

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public UserFoldersResult UserFolders { get; }

    public string VersionText => $"v{AppVersion.Current}";

    public string StatusText => $"User files: {UserFolders.RootPath}";

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainViewModel(UserFoldersResult userFolders)
    {
        UserFolders = userFolders;

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Dashboard", SymbolRegular.Home24, new DashboardViewModel()),
            new("Applications", SymbolRegular.Grid24, new ApplicationsViewModel()),
            new("Browsers", SymbolRegular.Globe24, new BrowsersViewModel()),
            new("Portables", SymbolRegular.Toolbox24, new PortablesViewModel()),
            new("Auxiliary", SymbolRegular.Folder24, new AuxiliaryViewModel()),
            new("Scripts", SymbolRegular.Script24, new ScriptsViewModel()),
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
}
