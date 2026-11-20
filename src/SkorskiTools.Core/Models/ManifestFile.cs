namespace SkorskiTools.Core.Models;

/// <summary>A file referenced by a manifest, with its expected SHA-256.</summary>
public sealed class ManifestFile
{
    public string File { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public TargetArchitecture Arch { get; init; } = TargetArchitecture.Any;
}
