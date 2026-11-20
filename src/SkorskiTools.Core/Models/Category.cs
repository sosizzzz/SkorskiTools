namespace SkorskiTools.Core.Models;

/// <summary>A display category loaded from categories.json.</summary>
public sealed class Category
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
}
