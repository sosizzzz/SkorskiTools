namespace SkorskiTools.Core.Models;

/// <summary>Everything discovered in the resources directory during one load.</summary>
public sealed class CatalogLoadResult
{
    public string? ResourcesDirectory { get; init; }
    public IReadOnlyList<Category> Categories { get; init; } = Array.Empty<Category>();
    public IReadOnlyList<ApplicationManifest> Applications { get; init; } = Array.Empty<ApplicationManifest>();
    public IReadOnlyList<ApplicationManifest> Browsers { get; init; } = Array.Empty<ApplicationManifest>();
    public IReadOnlyList<PortableManifest> Portables { get; init; } = Array.Empty<PortableManifest>();
    public IReadOnlyList<AuxiliaryManifest> Auxiliary { get; init; } = Array.Empty<AuxiliaryManifest>();
    public IReadOnlyList<ScriptItem> Scripts { get; init; } = Array.Empty<ScriptItem>();
    public IReadOnlyList<CatalogIssue> Issues { get; init; } = Array.Empty<CatalogIssue>();
}
