using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SkorskiTools.App.Services;
using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.App.ViewModels;

/// <summary>Shared state and commands for the Portables section.</summary>
public abstract partial class PortableListViewModel : CatalogListViewModel
{
    private readonly IPortableService _portables;
    private readonly IConfirmationService _confirmation;
    private bool _busy;

    [ObservableProperty]
    private bool _isDetecting;

    [ObservableProperty]
    private string _detectionSummary = "Detection has not run yet.";

    protected PortableListViewModel(IPortableService portables, IConfirmationService confirmation)
    {
        _portables = portables;
        _confirmation = confirmation;
    }

    public void SetPortables(
        IEnumerable<PortableManifest> manifests,
        IReadOnlyDictionary<string, string> categoryNames)
    {
        Load(manifests.Select(m => ResourceItemViewModel.From(m, categoryNames)));
    }

    public async Task DetectAllAsync()
    {
        if (_busy)
            return;

        _busy = true;
        IsDetecting = true;
        DetectionSummary = "Detecting...";

        try
        {
            var deployed = 0;
            foreach (var item in Items.ToList())
            {
                if (item.Manifest is not PortableManifest manifest)
                    continue;

                item.IsInstalled = _portables.IsInstalled(manifest);
                if (item.IsInstalled)
                    deployed++;
            }

            DetectionSummary = $"{deployed} of {Items.Count} deployed";
            Log.Information("Portable detection finished: {Summary}", DetectionSummary);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Portable detection failed");
            DetectionSummary = "Detection failed. See the log for details.";
        }
        finally
        {
            IsDetecting = false;
            _busy = false;
        }
    }

    [RelayCommand]
    private async Task DetectAsync() => await DetectAllAsync();

    [RelayCommand]
    private async Task InstallAsync(ResourceItemViewModel? item)
    {
        if (_busy || item?.Manifest is not PortableManifest manifest)
            return;

        _busy = true;
        try
        {
            Apply(item, await _portables.InstallAsync(manifest).ConfigureAwait(true));
        }
        finally
        {
            _busy = false;
        }
    }

    [RelayCommand]
    private async Task ReinstallAsync(ResourceItemViewModel? item)
    {
        if (_busy || item?.Manifest is not PortableManifest manifest)
            return;

        if (!_confirmation.Confirm(
                "Reinstall portable",
                $"Replace {item.Title} with the version bundled in Skorski Tools?\n\n" +
                "Any data stored inside its folder will be lost."))
            return;

        _busy = true;
        try
        {
            Apply(item, await _portables.ReinstallAsync(manifest).ConfigureAwait(true));
        }
        finally
        {
            _busy = false;
        }
    }

    [RelayCommand]
    private async Task UninstallAsync(ResourceItemViewModel? item)
    {
        if (_busy || item?.Manifest is not PortableManifest manifest || !item.IsInstalled)
            return;

        if (!_confirmation.Confirm(
                "Remove portable",
                $"Remove {item.Title}?\n\nWARNING: any data stored inside this portable's folder will be lost.\n" +
                "Only its own program folder and desktop shortcut will be deleted."))
            return;

        _busy = true;
        try
        {
            Apply(item, await _portables.UninstallAsync(manifest).ConfigureAwait(true));
        }
        finally
        {
            _busy = false;
        }
    }

    private void Apply(ResourceItemViewModel item, OperationResult result)
    {
        if (!result.Success)
            _confirmation.ShowError("Portable operation failed", result.Message);

        if (item.Manifest is PortableManifest manifest)
            item.IsInstalled = _portables.IsInstalled(manifest);

        DetectionSummary = result.Message;
    }
}
