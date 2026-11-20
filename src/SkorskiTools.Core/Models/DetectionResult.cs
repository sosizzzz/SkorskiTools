namespace SkorskiTools.Core.Models;

/// <summary>The outcome of detecting whether an application is installed.</summary>
public sealed record DetectionResult
{
    public bool IsInstalled { get; init; }
    public string? Version { get; init; }
    public string? Source { get; init; }
    public UninstallEntry? Entry { get; init; }

    public static DetectionResult NotInstalled => new();

    public static DetectionResult Installed(
        string? version, string source, UninstallEntry? entry = null) =>
        new() { IsInstalled = true, Version = version, Source = source, Entry = entry };
}
