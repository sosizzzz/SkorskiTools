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
    private readonly IFullInstallRunner _fullInstall;
    private readonly ISettingsService _settings;
    private bool _busy;

    [ObservableProperty]
    private bool _isDetecting;

    [ObservableProperty]
    private bool _isFullAutoEnabled;

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

    protected PortableListViewModel(
        IPortableService portables,
        IConfirmationService confirmation,
        IFullInstallRunner fullInstall,
        ISettingsService settings)
    {
        _portables = portables;
        _confirmation = confirmation;
        _fullInstall = fullInstall;
        _settings = settings;

        _isFullAutoEnabled = settings.Current.FullAutoEnabled;
        settings.SettingsChanged += (_, _) => IsFullAutoEnabled = settings.Current.FullAutoEnabled;
    }

    partial void OnFullAutoProgressChanged(int value) => UpdateProgressText();

    partial void OnFullAutoTotalChanged(int value) => UpdateProgressText();

    private void UpdateProgressText() => FullAutoProgressText = $"{FullAutoProgress}/{FullAutoTotal}";

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

    [RelayCommand]
    private async Task RunFullInstallAsync()
    {
        if (_busy || !IsFullAutoEnabled)
            return;

        var selected = Items.Where(i => i.IsSelected && i.Manifest is PortableManifest).ToList();
        if (selected.Count == 0)
        {
            ShowFullInstallResult(false, "Select at least one portable first.");
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

            var outcome = await _fullInstall
                .RunAsync(selected.Select(i => (ManifestBase)i.Manifest!), progress)
                .ConfigureAwait(true);

            foreach (var item in selected)
            {
                if (item.Manifest is PortableManifest manifest)
                    item.IsInstalled = _portables.IsInstalled(manifest);

                item.IsSelected = false;
            }

            ShowFullInstallResult(outcome.Ok, outcome.Message);
            DetectionSummary = outcome.Message;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Full install failed in Portables");
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
        if (_busy || !IsFullAutoEnabled)
            return;

        var selected = Items.Where(i => i.IsSelected && i.Manifest is PortableManifest).ToList();
        if (selected.Count == 0)
        {
            ShowFullInstallResult(false, "Select at least one portable first.");
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
        }
        foreach (var skipped in preview.Skipped)
            text.Add($"• SKIPPED: {skipped.Name} - {skipped.Reason}");

        ShowFullInstallResult(preview.AllHashesValid, string.Join(Environment.NewLine, text));
        DetectionSummary = "Simulation ready.";
    }

    private void ShowFullInstallResult(bool ok, string message)
    {
        FullAutoResultOk = ok;
        FullAutoResultMessage = message;
        HasFullAutoResult = true;
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
