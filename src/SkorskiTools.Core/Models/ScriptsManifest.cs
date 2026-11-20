namespace SkorskiTools.Core.Models;

/// <summary>The contents of resources/scripts/scripts.json.</summary>
public sealed class ScriptsManifest
{
    public int SchemaVersion { get; init; }
    public IReadOnlyList<ScriptItem> Scripts { get; init; } = Array.Empty<ScriptItem>();
}
