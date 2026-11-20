using System.IO;
using SkorskiTools.Core.Models;
using Wpf.Ui.Controls;

namespace SkorskiTools.App.ViewModels;

/// <summary>A catalog entry prepared for display in a section list.</summary>
public sealed class ResourceItemViewModel
{
    private ResourceItemViewModel()
    {
    }

    public string Id { get; private init; } = string.Empty;
    public string Title { get; private init; } = string.Empty;
    public string Description { get; private init; } = string.Empty;
    public string Version { get; private init; } = string.Empty;
    public string CategoryId { get; private init; } = string.Empty;
    public string CategoryName { get; private init; } = string.Empty;
    public SymbolRegular Glyph { get; private init; }
    public string IconPath { get; private init; } = string.Empty;
    public string FilesText { get; private init; } = string.Empty;
    public string Code { get; private init; } = string.Empty;
    public string Language { get; private init; } = string.Empty;
    public object? Manifest { get; private init; }

    public bool HasIcon => !string.IsNullOrWhiteSpace(IconPath);
    public bool HasNoIcon => !HasIcon;

    public static ResourceItemViewModel From(
        ManifestBase manifest,
        IReadOnlyDictionary<string, string> categoryNames)
    {
        var iconPath = string.IsNullOrWhiteSpace(manifest.Icon)
            ? string.Empty
            : Path.Combine(manifest.ResourceDirectory, manifest.Icon);

        if (!string.IsNullOrWhiteSpace(iconPath) && !File.Exists(iconPath))
            iconPath = string.Empty;

        return new ResourceItemViewModel
        {
            Id = manifest.Id,
            Title = manifest.Name,
            Description = manifest.Description,
            Version = manifest.Version,
            CategoryId = manifest.Category,
            CategoryName = categoryNames.TryGetValue(manifest.Category, out var name)
                ? name
                : manifest.Category,
            Glyph = CategoryIcons.For(manifest.Category),
            IconPath = iconPath,
            FilesText = manifest switch
            {
                ApplicationManifest app => string.Join(", ", app.Files.Select(f => f.File)),
                PortableManifest portable => portable.Package.File,
                AuxiliaryManifest aux => string.Join(", ", aux.Files.Select(f => f.File)),
                _ => string.Empty
            },
            Manifest = manifest
        };
    }

    public static ResourceItemViewModel FromScript(ScriptItem script) => new()
    {
        Id = script.Id,
        Title = script.Title,
        Description = script.Description,
        CategoryId = "scripts",
        CategoryName = "Scripts",
        Glyph = SymbolRegular.Script24,
        Language = script.Language,
        Code = script.Code,
        Manifest = script
    };
}

/// <summary>Maps category ids to generic Fluent icons.</summary>
internal static class CategoryIcons
{
    public static SymbolRegular For(string? categoryId) => categoryId switch
    {
        "system" => SymbolRegular.Window24,
        "development" => SymbolRegular.Code24,
        "utilities" => SymbolRegular.Toolbox24,
        "multimedia" => SymbolRegular.Image24,
        "networking" => SymbolRegular.Globe24,
        _ => SymbolRegular.Apps24
    };
}
