namespace SkorskiTools.Core.Models;

/// <summary>A conventional application or browser.</summary>
public sealed class ApplicationManifest : ManifestBase
{
    public IReadOnlyList<ManifestFile> Files { get; init; } = Array.Empty<ManifestFile>();
    public DetectionConfig? Detection { get; init; }
    public InstallationConfig Installation { get; init; } = new();
    public UninstallationConfig Uninstallation { get; init; } = new();

    /// <summary>Optional unattended installation settings. Never enabled implicitly.</summary>
    public FullAutoConfig? FullAuto { get; init; }
}
