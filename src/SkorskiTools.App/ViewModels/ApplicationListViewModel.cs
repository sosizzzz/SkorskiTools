using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SkorskiTools.App.Services;
using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.App.ViewModels;

/// <summary>
/// Shared logic for the Applications and Browsers sections. Browsers reuse this
/// class instead of duplicating detection and installation behavior.
/// </summary>
public abstract partial class ApplicationListViewModel : CatalogListViewModel
{
    private readonly IInstallationDetector _detector;
    private readonly IApplicationInstaller _installer;
    private readonly IConfirmationService _confirmation;

    private bool _busy;

    [ObservableProperty]
    private bool _isDetecting;

    [ObservableProperty]
    private string _detectionSummary = "Detection has not run yet.";

    protected ApplicationListViewModel(
        IInstallationDetector detector,
        IApplicationInstaller installer,
        IConfirmationService confirmation)
    {
        _detector = detector;
        _installer = installer;
        _confirmation = confirmation;
    }

    public void SetApplications(
        IEnumerable<ApplicationManifest> manifests,
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
            var installed = 0;
            foreach (var item in Items.ToList())
            {
                if (item.Manifest is not ApplicationManifest manifest)
                    continue;

                var result = await _detector.DetectAsync(manifest).ConfigureAwait(true);
                Apply(item, result);
                if (result.IsInstalled)
                    installed++;
            }

            DetectionSummary = $"{installed} of {Items.Count} installed";
            Log.Information("Detection finished for {Section}: {Summary}", Title, DetectionSummary);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Detection failed in {Section}", Title);
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
        if (_busy || item?.Manifest is not ApplicationManifest manifest)
            return;

        _busy = true;
        try
        {
            var result = await _installer.InstallAsync(manifest).ConfigureAwait(true);
            if (!result.Success)
            {
                _confirmation.ShowError("Installation failed", result.Message);
                DetectionSummary = result.Message;
                return;
            }

            Apply(item, await _detector.DetectAsync(manifest).ConfigureAwait(true));
            DetectionSummary = result.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    [RelayCommand]
    private async Task UninstallAsync(ResourceItemViewModel? item)
    {
        if (_busy || item?.Manifest is not ApplicationManifest manifest || !item.IsInstalled)
            return;

        if (!_confirmation.Confirm(
                "Uninstall",
                $"Do you want to uninstall {item.Title}?\n\nThe original registered uninstaller will be used."))
            return;

        _busy = true;
        try
        {
            var detection = await _detector.DetectAsync(manifest).ConfigureAwait(true);
            var result = await _installer.UninstallAsync(manifest, detection).ConfigureAwait(true);

            if (!result.Success)
            {
                _confirmation.ShowError("Uninstall failed", result.Message);
                DetectionSummary = result.Message;
                return;
            }

            Apply(item, await _detector.DetectAsync(manifest).ConfigureAwait(true));
            DetectionSummary = result.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    [RelayCommand]
    private async Task ReinstallAsync(ResourceItemViewModel? item)
    {
        if (_busy || item?.Manifest is not ApplicationManifest manifest || !item.IsInstalled)
            return;

        if (!_confirmation.Confirm(
                "Reinstall",
                $"Do you want to reinstall {item.Title}?\n\nThe original installer will run in repair/upgrade mode. " +
                "Skorski Tools will not uninstall it automatically."))
            return;

        _busy = true;
        try
        {
            var result = await _installer.InstallAsync(manifest).ConfigureAwait(true);
            if (!result.Success)
            {
                _confirmation.ShowError("Reinstall failed", result.Message);
                DetectionSummary = result.Message;
                return;
            }

            Apply(item, await _detector.DetectAsync(manifest).ConfigureAwait(true));
            DetectionSummary = result.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    private static void Apply(ResourceItemViewModel item, DetectionResult result)
    {
        item.IsInstalled = result.IsInstalled;
        item.InstalledVersion = result.Version;
        item.DetectionSource = result.Source ?? string.Empty;
    }
}
