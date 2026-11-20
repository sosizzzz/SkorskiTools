using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SkorskiTools.Core.Models;

namespace SkorskiTools.App.ViewModels.Pages;

/// <summary>Scripts are read-only content. This view never runs them.</summary>
public sealed partial class ScriptsViewModel : CatalogListViewModel
{
    [ObservableProperty]
    private string _copySummary = string.Empty;

    public override string Title => "Scripts";

    public string Subtitle => "Browse and copy scripts. Scripts are never executed.";

    public void SetScripts(IEnumerable<ScriptItem> scripts)
    {
        Load(scripts.Select(script =>
        {
            var item = ResourceItemViewModel.FromScript(script);
            item.CustomStatus = script.Language;
            return item;
        }));

        SelectedItem = Items.FirstOrDefault();
    }

    [RelayCommand]
    private void Copy(ResourceItemViewModel? item)
    {
        if (string.IsNullOrEmpty(item?.Code))
            return;

        try
        {
            Clipboard.SetText(item.Code);
            CopySummary = $"Copied '{item.Title}' to the clipboard.";
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            Log.Warning(ex, "Could not copy script to clipboard");
            CopySummary = "The clipboard is busy. Try again in a moment.";
        }
    }
}
