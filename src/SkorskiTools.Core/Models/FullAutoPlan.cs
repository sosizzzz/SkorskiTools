namespace SkorskiTools.Core.Models;

public enum FullAutoAction
{
    Install,
    Reinstall
}

/// <summary>One confirmed action in an unattended batch.</summary>
public sealed class FullAutoPlanItem
{
    public required ManifestBase Manifest { get; init; }
    public required DetectionResult Detection { get; init; }
    public FullAutoAction Action { get; init; }
}

/// <summary>A batch prepared for confirmation.</summary>
public sealed class FullAutoPlan
{
    public IReadOnlyList<FullAutoPlanItem> Items { get; init; } = Array.Empty<FullAutoPlanItem>();

    public IReadOnlyList<(string Name, string Reason)> Skipped { get; init; } =
        Array.Empty<(string, string)>();

    public int Count => Items.Count;
}

/// <summary>Progress event for the UI.</summary>
public sealed record FullAutoProgress(int Current, int Total, string Name, string Message);

/// <summary>Outcome of an unattended batch.</summary>
public sealed class FullAutoResult
{
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public int RequiringRestart { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();

    public bool AllSucceeded => Failed == 0 && RequiringRestart == 0;
}
