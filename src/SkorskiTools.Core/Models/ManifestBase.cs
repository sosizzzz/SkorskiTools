namespace SkorskiTools.Core.Models;

/// <summary>Fields shared by every resource manifest.</summary>
public abstract class ManifestBase
{
    public int SchemaVersion { get; init; }
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Category { get; init; } = "other";
    public string Icon { get; init; } = string.Empty;

    /// <summary>Three short benefits shown in the detail panel.</summary>
    public IReadOnlyList<string> Highlights { get; init; } = Array.Empty<string>();

    /// <summary>Absolute path of the manifest.json file.</summary>
    public string ManifestPath { get; init; } = string.Empty;

    /// <summary>Absolute path of the directory containing the resource files.</summary>
    public string ResourceDirectory { get; init; } = string.Empty;

    /// <summary>Catalog section this manifest was discovered in.</summary>
    public ManifestKind Kind { get; init; }
}
