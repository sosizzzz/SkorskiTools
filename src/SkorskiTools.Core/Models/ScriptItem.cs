namespace SkorskiTools.Core.Models;

/// <summary>A script shown for reading and copying only.</summary>
public sealed class ScriptItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}
