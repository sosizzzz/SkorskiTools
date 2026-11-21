using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.App.Services;

/// <summary>Result of a full install batch shown in the UI.</summary>
public sealed record FullInstallOutcome(bool Ok, string Message);

/// <summary>Confirms, runs and announces a sequential full install batch.</summary>
public interface IFullInstallRunner
{
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

        var lines = plan.Items.Select(item => $"• {item.Action}: {item.Manifest.Name}");
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
