using CommunityToolkit.Mvvm.ComponentModel;

namespace SkorskiTools.App.ViewModels.Pages;

public sealed partial class DashboardViewModel : ViewModelBase
{
    public override string Title => "Dashboard";

    public string Subtitle => "Overview of Skorski Tools and your local setup.";

    [ObservableProperty]
    private string _catalogSummary = "Loading catalog...";

    public void SetSummary(string summary) => CatalogSummary = summary;
}
