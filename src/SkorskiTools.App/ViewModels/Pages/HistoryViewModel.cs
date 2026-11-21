using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.App.ViewModels.Pages;

/// <summary>Read-only timeline of installation operations.</summary>
public sealed partial class HistoryViewModel : ViewModelBase
{
    private readonly IOperationHistoryService _history;

    public HistoryViewModel(IOperationHistoryService history)
    {
        _history = history;
        Refresh();
    }

    public override string Title => "History";

    public string Subtitle => "Results of manual and full install operations on this PC.";

    public ObservableCollection<OperationHistoryEntry> Entries { get; } = new();

    public bool HasEntries => Entries.Count > 0;

    public void Refresh()
    {
        Entries.Clear();
        foreach (var entry in _history.GetEntries())
            Entries.Add(entry);

        OnPropertyChanged(nameof(HasEntries));
    }

    [RelayCommand]
    private void Clear()
    {
        _history.Clear();
        Refresh();
    }
}
