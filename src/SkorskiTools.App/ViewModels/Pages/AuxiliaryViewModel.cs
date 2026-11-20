using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SkorskiTools.App.Services;
using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.App.ViewModels.Pages;

/// <summary>Helper files are copied only. Nothing here can execute them.</summary>
public sealed partial class AuxiliaryViewModel : CatalogListViewModel
{
    private readonly IAuxiliaryService _auxiliary;
    private readonly IConfirmationService _confirmation;

    [ObservableProperty]
    private string _copySummary = "No files copied yet.";

    public AuxiliaryViewModel(IAuxiliaryService auxiliary, IConfirmationService confirmation)
    {
        _auxiliary = auxiliary;
        _confirmation = confirmation;
    }

    public override string Title => "Auxiliary";

    public string Subtitle => "Copy owner-provided helper files without executing them.";

    public void SetAuxiliary(
        IEnumerable<AuxiliaryManifest> manifests,
        IReadOnlyDictionary<string, string> categoryNames)
    {
        Load(manifests.Select(manifest =>
        {
            var item = ResourceItemViewModel.From(manifest, categoryNames);
            item.CustomStatus = _auxiliary.AreCopied(manifest) ? "Copied" : "Not copied";
            return item;
        }));
    }

    [RelayCommand]
    private async Task CopyAsync(ResourceItemViewModel? item)
    {
        if (item?.Manifest is not AuxiliaryManifest manifest)
            return;

        try
        {
            var result = await _auxiliary.CopyAsync(manifest).ConfigureAwait(true);
            if (!result.Success)
                _confirmation.ShowError("Copy failed", result.Message);

            item.CustomStatus = _auxiliary.AreCopied(manifest) ? "Copied" : "Not copied";
            CopySummary = result.Message;
            Log.Information("Auxiliary copy result: {Result}", result.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Auxiliary copy failed for {Id}", manifest.Id);
            _confirmation.ShowError("Copy failed", "The files could not be copied. See the log for details.");
        }
    }
}
