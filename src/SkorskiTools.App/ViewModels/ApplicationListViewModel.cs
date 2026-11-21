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
    private readonly IFullInstallRunner _fullInstall;
    private readonly ISettingsService _settings;
    private readonly IOperationHistoryService _history;

    private bool _busy;

    [ObservableProperty]
    private bool _isDetecting;

    [ObservableProperty]
    private bool _isFullAutoEnabled;

    [ObservableProperty]
    private bool _isDeveloperModeEnabled;

    [ObservableProperty]
    private bool _canSimulate;

    [ObservableProperty]
    private bool _isFullAutoRunning;

    [ObservableProperty]
    private int _fullAutoProgress;

    [ObservableProperty]
    private int _fullAutoTotal;

    [ObservableProperty]
    private string _fullAutoProgressText = "0/0";

    [ObservableProperty]
    private string _fullAutoResultMessage = string.Empty;

    [ObservableProperty]
    private bool _hasFullAutoResult;

    [ObservableProperty]
    private bool _fullAutoResultOk;

    [ObservableProperty]
    private string _detectionSummary = "Detection has not run yet.";

    protected ApplicationListViewModel(
        IInstallationDetector detector,
        IApplicationInstaller installer,
        IConfirmationService confirmation,
        IFullInstallRunner fullInstall,
        ISettingsService settings,
        IOperationHistoryService history)
    {
        _detector = detector;
        _installer = installer;
        _confirmation = confirmation;
        _fullInstall = fullInstall;
        _settings = settings;
        _history = history;

        _isFullAutoEnabled = settings.Current.FullAutoEnabled;
        _isDeveloperModeEnabled = settings.Current.DeveloperModeEnabled;
        settings.SettingsChanged += (_, _) =>
        {
            IsFullAutoEnabled = settings.Current.FullAutoEnabled;
            IsDeveloperModeEnabled = settings.Current.DeveloperModeEnabled;
        };

        UpdateCanSimulate();
    }

    partial void OnIsFullAutoEnabledChanged(bool value) => UpdateCanSimulate();

    partial void OnIsDeveloperModeEnabledChanged(bool value) => UpdateCanSimulate();

    private void UpdateCanSimulate() => CanSimulate = IsFullAutoEnabled && IsDeveloperModeEnabled;

    partial void OnFullAutoProgressChanged(int value) => UpdateProgressText();

    partial void OnFullAutoTotalChanged(int value) => UpdateProgressText();

    private void UpdateProgressText() => FullAutoProgressText = $"{FullAutoProgress}/{FullAutoTotal}";

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
            Record(item, "Install", result);
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
            Record(item, "Uninstall", result);

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
            Record(item, "Reinstall", result);
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

    [RelayCommand]
    private async Task RunFullInstallAsync()
    {
        if (_busy || !IsFullAutoEnabled)
            return;

        var selected = Items.Where(i => i.IsSelected && i.Manifest is ApplicationManifest).ToList();
        if (selected.Count == 0)
        {
            ShowFullInstallResult(false, "Select at least one application first.");
            return;
        }

        _busy = true;
        IsFullAutoRunning = true;
        FullAutoTotal = selected.Count;
        FullAutoProgress = 0;
        FullAutoProgressText = $"0/{selected.Count}";
        DetectionSummary = "Full install running...";
        HasFullAutoResult = false;

        try
        {
            var progress = new Progress<FullAutoProgress>(p =>
            {
                FullAutoProgress = p.Current;
                DetectionSummary = $"{p.Current}/{p.Total}: {p.Message}";
            });

            var manifests = selected.Select(i => (ManifestBase)i.Manifest!).ToList();
            var outcome = await _fullInstall.RunAsync(manifests, progress).ConfigureAwait(true);

            foreach (var item in selected)
            {
                if (item.Manifest is ApplicationManifest manifest)
                    Apply(item, await _detector.DetectAsync(manifest).ConfigureAwait(true));

                item.IsSelected = false;
            }

            ShowFullInstallResult(outcome.Ok, outcome.Message);
            DetectionSummary = outcome.Message;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Full install failed in {Section}", Title);
            ShowFullInstallResult(false, "Full install failed. See the log for details.");
        }
        finally
        {
            IsFullAutoRunning = false;
            _busy = false;
        }
    }

    [RelayCommand]
    private async Task SimulateAsync()
    {
        if (_busy || !CanSimulate)
            return;

        var selected = Items.Where(i => i.IsSelected && i.Manifest is ApplicationManifest).ToList();
        if (selected.Count == 0)
        {
            ShowFullInstallResult(false, "Select at least one application first.");
            return;
        }

        var preview = await _fullInstall
            .PreviewAsync(selected.Select(i => (ManifestBase)i.Manifest!))
            .ConfigureAwait(true);

        var text = new List<string> { "SIMULATION - nothing was executed." };
        foreach (var line in preview.Lines)
        {
            text.Add($"• {line.Action}: {line.Name} [{line.Scope}]");
            text.Add($"    {line.Command}");
            text.Add($"    SHA-256: {(line.HashValid ? "OK" : "MISMATCH")}");
            if (!string.IsNullOrWhiteSpace(line.Note))
                text.Add($"    config: {line.Note}");
        }
        foreach (var skipped in preview.Skipped)
            text.Add($"• SKIPPED: {skipped.Name} - {skipped.Reason}");

        ShowFullInstallResult(preview.AllHashesValid, string.Join(Environment.NewLine, text));
        DetectionSummary = "Simulation ready.";
    }

    private void Record(ResourceItemViewModel item, string action, OperationResult result) =>
        _history.Add(new OperationHistoryEntry
        {
            Timestamp = DateTimeOffset.Now,
            ResourceId = item.Id,
            ResourceName = item.Title,
            Action = action,
            Success = result.Success,
            Message = result.Message,
            ExitCode = result.ExitCode
        });

    private void ShowFullInstallResult(bool ok, string message)
    {
        FullAutoResultOk = ok;
        FullAutoResultMessage = message;
        HasFullAutoResult = true;
    }

    private static void Apply(ResourceItemViewModel item, DetectionResult result)
    {
        item.IsInstalled = result.IsInstalled;
        item.InstalledVersion = result.Version;
        item.DetectionSource = result.Source ?? string.Empty;
    }
}
