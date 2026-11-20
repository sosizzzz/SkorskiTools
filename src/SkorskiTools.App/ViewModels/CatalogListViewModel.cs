using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SkorskiTools.App.ViewModels;

/// <summary>
/// Shared list state for catalog sections: items, instant search and category filter.
/// </summary>
public abstract partial class CatalogListViewModel : ViewModelBase
{
    private const string AllCategories = "All";

    private string _searchText = string.Empty;
    private string _selectedCategory = AllCategories;

    [ObservableProperty]
    private ResourceItemViewModel? _selectedItem;

    protected CatalogListViewModel()
    {
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = Matches;
    }

    public ObservableCollection<ResourceItemViewModel> Items { get; } = new();

    public ICollectionView ItemsView { get; }

    public ObservableCollection<string> Categories { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ItemsView.Refresh();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
                ItemsView.Refresh();
        }
    }

    public bool HasItems => Items.Count > 0;

    public string EmptyMessage => Items.Count == 0
        ? "No resources found in this section."
        : "No results for the current search or filter.";

    public void Load(IEnumerable<ResourceItemViewModel> items)
    {
        var list = items.ToList();

        Items.Clear();
        foreach (var item in list)
            Items.Add(item);

        Categories.Clear();
        Categories.Add(AllCategories);
        foreach (var category in list.Select(i => i.CategoryName).Distinct(StringComparer.OrdinalIgnoreCase))
            Categories.Add(category);

        _selectedCategory = AllCategories;
        OnPropertyChanged(nameof(SelectedCategory));
        ItemsView.Refresh();
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    private bool Matches(object? item)
    {
        if (item is not ResourceItemViewModel entry)
            return false;

        if (_selectedCategory != AllCategories
            && !string.Equals(entry.CategoryName, _selectedCategory, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        var query = _searchText.Trim();
        return entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
               || entry.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
               || entry.Version.Contains(query, StringComparison.OrdinalIgnoreCase)
               || entry.Id.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
