using System.IO;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.App.Services;

/// <summary>Result of a full install batch shown in the UI.</summary>
public sealed record FullInstallOutcome(bool Ok, string Message);

/// <summary>One line of a dry-run preview. Nothing is executed.</summary>
public sealed record FullInstallPreviewLine(
    string Name,
    string Action,
    string Scope,
    string Command,
    bool HashValid,
    string? Note);

/// <summary>Result of a simulation, safe to show before any real action.</summary>
public sealed class FullInstallPreview
{
    public IReadOnlyList<FullInstallPreviewLine> Lines { get; init; } =
        Array.Empty<FullInstallPreviewLine>();

    public IReadOnlyList<(string Name, string Reason)> Skipped { get; init; } =
        Array.Empty<(string, string)>();

    public bool AllHashesValid => Lines.All(l => l.HashValid);
}

/// <summary>Confirms, runs and announces a sequential full install batch.</summary>
public interface IFullInstallRunner
{
    Task<FullInstallPreview> PreviewAsync(
        IEnumerable<ManifestBase> selected,
        CancellationToken cancellationToken = default);

    Task<FullInstallOutcome> RunAsync(
        IEnumerable<ManifestBase> selected,
        IProgress<FullAutoProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared by Applications, Browsers and Portables so the confirmation, sequence and
/// completion feedback behave identically in every section.
/// </summary>
public sealed class FullInstallRunner : IFullInstallRunner
{
    private readonly IFullAutoService _fullAuto;
    private readonly IConfirmationService _confirmation;
    private readonly ICompletionSoundService _sound;

    public FullInstallRunner(
        IFullAutoService fullAuto,
        IConfirmationService confirmation,
        ICompletionSoundService sound)
    {
        _fullAuto = fullAuto;
        _confirmation = confirmation;
        _sound = sound;
    }

    public async Task<FullInstallPreview> PreviewAsync(
        IEnumerable<ManifestBase> selected,
        CancellationToken cancellationToken = default)
    {
        var plan = await _fullAuto.BuildPlanAsync(selected, cancellationToken).ConfigureAwait(true);
        var lines = new List<FullInstallPreviewLine>();

        foreach (var item in plan.Items)
        {
            string command;
            string scope;
            bool hashValid;
            string? note = null;

            if (item.Manifest is ApplicationManifest application)
            {
                var file = ArchitectureSelector.Select(application.Files);
                var path = file is null
                    ? null
                    : ResourcePathGuard.Resolve(application.ResourceDirectory, file.File);

                var args = application.FullAuto!.InstallArguments;
                command = path is null
                    ? "<installer not found>"
                    : $"{path} {string.Join(" ", args)}".TrimEnd();
                hashValid = file is not null && path is not null && File.Exists(path)
                            && Sha256Verifier.Matches(path, file.Sha256);

                var config = application.FullAuto!;
                scope = config.Scope == InstallationScope.Machine
                    ? "team (UAC prompt for this installer)"
                    : "current user";
                note = config.ConfigurationFile;
            }
            else
            {
                var portable = (PortableManifest)item.Manifest;
                var path = ResourcePathGuard.Resolve(portable.ResourceDirectory, portable.Package.File);
                command = path is null
                    ? "<package not found>"
                    : $"{path} -> %USERPROFILE%\\Skorski-Tools\\portables\\{portable.Id}";
                hashValid = path is not null && File.Exists(path)
                            && Sha256Verifier.Matches(path, portable.Package.Sha256);
                scope = "user profile";
            }

            lines.Add(new FullInstallPreviewLine(
                item.Manifest.Name, item.Action.ToString(), scope, command, hashValid, note));
        }

        return new FullInstallPreview { Lines = lines, Skipped = plan.Skipped };
    }

    public async Task<FullInstallOutcome> RunAsync(
        IEnumerable<ManifestBase> selected,
        IProgress<FullAutoProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var plan = await _fullAuto.BuildPlanAsync(selected, cancellationToken).ConfigureAwait(true);

        if (plan.Count == 0)
            return new FullInstallOutcome(
                false,
                "No selected resource can be installed automatically. Applications need verified silent switches in their manifest.");

        var lines = plan.Items.Select(item =>
        {
            var scope = item.Manifest is ApplicationManifest application && application.FullAuto is not null
                ? application.FullAuto.Scope == InstallationScope.Machine ? "team (UAC)" : "current user"
                : "portable";
            return $"• {item.Action}: {item.Manifest.Name} [{scope}]";
        });
        var skipped = plan.Skipped.Select(s => $"• Skipped: {s.Name} ({s.Reason})");
        var message = string.Join(Environment.NewLine, lines.Concat(skipped));

        var confirmed = _confirmation.Confirm(
            "Full install",
            $"The following resources will be installed one by one, without interaction:{Environment.NewLine}" +
            $"{Environment.NewLine}{message}{Environment.NewLine}{Environment.NewLine}" +
            "Reinstalled portables will lose data stored in their own folder. Continue?");

        if (!confirmed)
            return new FullInstallOutcome(false, "Full install cancelled.");

        var result = await _fullAuto.ExecuteAsync(plan, progress, cancellationToken).ConfigureAwait(true);

        var summary = $"{result.Succeeded} succeeded · {result.Failed} failed · {result.RequiringRestart} need restart";
        var outcome = new FullInstallOutcome(result.Failed == 0, summary);
        _sound.Play(outcome.Ok);

        return outcome;
    }
}
