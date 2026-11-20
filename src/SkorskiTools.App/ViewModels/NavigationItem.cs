using Wpf.Ui.Controls;

namespace SkorskiTools.App.ViewModels;

/// <summary>A side navigation entry and the page it activates.</summary>
public sealed class NavigationItem
{
    public NavigationItem(string title, SymbolRegular glyph, ViewModelBase page)
    {
        Title = title;
        Glyph = glyph;
        Page = page;
    }

    public string Title { get; }
    public SymbolRegular Glyph { get; }
    public ViewModelBase Page { get; }
}
