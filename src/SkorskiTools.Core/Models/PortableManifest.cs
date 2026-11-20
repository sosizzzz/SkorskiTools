namespace SkorskiTools.Core.Models;

/// <summary>A portable application deployed into the user profile.</summary>
public sealed class PortableManifest : ManifestBase
{
    public ManifestFile Package { get; init; } = new();
    public string Executable { get; init; } = string.Empty;
    public string ShortcutName { get; init; } = string.Empty;
}
