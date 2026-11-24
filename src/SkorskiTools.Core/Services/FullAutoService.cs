using Serilog;
using SkorskiTools.Core.Models;

namespace SkorskiTools.Core.Services;

/// <summary>Prepares and executes selected unattended installations from manifest data only.</summary>
public interface IFullAutoService
{
    Task<FullAutoPlan> BuildPlanAsync(
        IEnumerable<ManifestBase> manifests,
        CancellationToken cancellationToken = default);

    Task<FullAutoResult> ExecuteAsync(
        FullAutoPlan plan,
        IProgress<FullAutoProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic sequential engine: it never contains application names or vendor switches.
/// Every decision comes from the manifest, detection or the portable deployment state.
/// </summary>
public sealed class FullAutoService : IFullAutoService
{
    private readonly IInstallationDetector _detector;
    private readonly IApplicationInstaller _installer;
    private readonly IPortableService _portables;
    private readonly IOperationHistoryService? _history;
    private readonly ILogger _logger;

    public FullAutoService(
        IInstallationDetector detector,
        IApplicationInstaller installer,
        IPortableService? portables = null,
        IOperationHistoryService? history = null,
        ILogger? logger = null)
    {
        _detector = detector;
        _installer = installer;
        _portables = portables ?? new PortableService();
        _history = history;
        _logger = logger ?? Log.Logger;
    }

    public async Task<FullAutoPlan> BuildPlanAsync(
        IEnumerable<ManifestBase> manifests,
        CancellationToken cancellationToken = default)
    {
        var items = new List<FullAutoPlanItem>();
        var skipped = new List<(string, string)>();

        foreach (var manifest in manifests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DetectionResult detection;
            if (manifest is ApplicationManifest application)
            {
                var config = application.FullAuto;
                if (config is null or { Supported: false })
                {
                    skipped.Add((manifest.Name,
                        config?.UnsupportedReason ?? "No unattended configuration in the manifest"));
                    continue;
                }

                if (!config.Enabled || config.InstallArguments.Count == 0)
                {
                    skipped.Add((manifest.Name, "No verified silent switches in the manifest"));
                    continue;
                }

                detection = await _detector.DetectAsync(application, cancellationToken).ConfigureAwait(false);
            }
            else if (manifest is PortableManifest portable)
            {
                detection = _portables.IsInstalled(portable)
                    ? DetectionResult.Installed(null, "Portable folder")
                    : DetectionResult.NotInstalled;
            }
            else
            {
                skipped.Add((manifest.Name, "This resource type cannot be installed"));
                continue;
            }

            items.Add(new FullAutoPlanItem
            {
                Manifest = manifest,
                Detection = detection,
                Action = detection.IsInstalled ? FullAutoAction.Reinstall : FullAutoAction.Install
            });
        }

        _logger.Information(
            "Full install plan built: {Count} action(s), {Skipped} skipped",
            items.Count, skipped.Count);

        return new FullAutoPlan { Items = items, Skipped = skipped };
    }

    public async Task<FullAutoResult> ExecuteAsync(
        FullAutoPlan plan,
        IProgress<FullAutoProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var succeeded = 0;
        var failed = 0;
        var restarting = 0;
        var messages = new List<string>();

        for (var index = 0; index < plan.Items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = plan.Items[index];
            var name = item.Manifest.Name;

            progress?.Report(new FullAutoProgress(
                index + 1, plan.Items.Count, name, $"{item.Action}: {name}"));

            var outcome = item.Manifest is ApplicationManifest application
                ? await ExecuteApplicationAsync(application, item, cancellationToken).ConfigureAwait(false)
                : await ExecutePortableAsync(item.Manifest, item.Action, cancellationToken).ConfigureAwait(false);

            if (!outcome.Success)
            {
                failed++;
                messages.Add($"{name}: {outcome.Message}");
                _logger.Warning("Full install failed for {Id}: {Message}", item.Manifest.Id, outcome.Message);
            }
            else if (outcome.RestartRequired)
            {
                restarting++;
                messages.Add($"{name}: installed, restart required");
            }
            else
            {
                succeeded++;
                messages.Add($"{name}: installed successfully");
                _logger.Information("Full install finished for {Id}", item.Manifest.Id);
            }

            _history?.Add(new OperationHistoryEntry
            {
                Timestamp = DateTimeOffset.Now,
                ResourceId = item.Manifest.Id,
                ResourceName = name,
                Action = item.Action.ToString(),
                Success = outcome.Success && !outcome.RestartRequired,
                Message = outcome.Message,
                ExitCode = outcome.ExitCode
            });
        }

        var result = new FullAutoResult
        {
            Succeeded = succeeded,
            Failed = failed,
            RequiringRestart = restarting,
            Messages = messages
        };

        _logger.Information(
            "Full install batch finished: succeeded {Succeeded}, failed {Failed}, restart {Restart}",
            succeeded, failed, restarting);

        return result;
    }

    private sealed record ExecuteOutcome(
        bool Success,
        string Message,
        bool RestartRequired,
        int? ExitCode);

    private async Task<ExecuteOutcome> ExecuteApplicationAsync(
        ApplicationManifest manifest,
        FullAutoPlanItem item,
        CancellationToken cancellationToken)
    {
        if (item.Action == FullAutoAction.Reinstall)
        {
            var removal = await _installer
                .UninstallQuietAsync(manifest, item.Detection, cancellationToken)
                .ConfigureAwait(false);

            if (!removal.Success)
                return new ExecuteOutcome(false, $"uninstall failed - {removal.Message}", false, removal.ExitCode);
        }

        var install = await _installer
            .InstallAutomatedAsync(manifest, cancellationToken)
            .ConfigureAwait(false);

        var exitCode = install.ExitCode ?? -1;
        if (!install.Success)
            return new ExecuteOutcome(false, install.Message, false, install.ExitCode);

        var config = manifest.FullAuto!;

        if (config.RestartExitCodes.Contains(exitCode))
            return new ExecuteOutcome(true, $"installed, exit code {exitCode} (restart required)", true, exitCode);

        if (!config.ExpectedExitCodes.Contains(exitCode))
            return new ExecuteOutcome(false, config.DescribeUnexpectedExitCode(exitCode), false, exitCode);

        // A zero exit code is not proof of installation: web installers can fail after
        // downloading and still report success. Trust detection, not the process alone.
        var verification = await _detector.DetectAsync(manifest, cancellationToken).ConfigureAwait(false);
        if (!verification.IsInstalled)
        {
            return new ExecuteOutcome(
                false,
                $"installer exited with code {exitCode} but {manifest.Name} was not detected afterwards. " +
                "Check the log and run detection again.",
                false,
                exitCode);
        }

        return new ExecuteOutcome(true, $"installed, exit code {exitCode}", false, exitCode);
    }

    private async Task<ExecuteOutcome> ExecutePortableAsync(
        ManifestBase manifest,
        FullAutoAction action,
        CancellationToken cancellationToken)
    {
        if (manifest is not PortableManifest portable)
            return new ExecuteOutcome(false, "Unsupported portable manifest", false, null);

        var result = action == FullAutoAction.Reinstall
            ? await _portables.ReinstallAsync(portable, cancellationToken).ConfigureAwait(false)
            : await _portables.InstallAsync(portable, cancellationToken).ConfigureAwait(false);

        return new ExecuteOutcome(result.Success, result.Message, false, result.ExitCode);
    }
}
