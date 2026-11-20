namespace SkorskiTools.Core.Models;

/// <summary>Helper files that are copied but never executed.</summary>
public sealed class AuxiliaryManifest : ManifestBase
{
    public IReadOnlyList<ManifestFile> Files { get; init; } = Array.Empty<ManifestFile>();
}
