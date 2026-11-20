namespace SkorskiTools.Core.Models;

public enum IssueSeverity
{
    Warning,
    Error
}

/// <summary>A non-fatal problem found while loading the catalog.</summary>
public sealed record CatalogIssue(
    IssueSeverity Severity,
    string Message,
    string? ResourceId = null,
    string? Path = null);
