using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using SkorskiTools.Core.Models;
using Wpf.Ui.Controls;

namespace SkorskiTools.App.ViewModels;

/// <summary>A catalog entry prepared for display in a section list.</summary>
public sealed class ResourceItemViewModel : ObservableObject
{
    private bool _isInstalled;
    private string? _installedVersion;
    private string _detectionSource = string.Empty;
    private string? _customStatus;
    private bool _isSelected;

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

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool HasIcon => !string.IsNullOrWhiteSpace(IconPath);
    public bool HasNoIcon => !HasIcon;

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (SetProperty(ref _isInstalled, value))
            {
                OnPropertyChanged(nameof(CanInstall));
                OnPropertyChanged(nameof(CanUninstall));
                OnPropertyChanged(nameof(CanReinstall));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string? InstalledVersion
    {
        get => _installedVersion;
        set
        {
            if (SetProperty(ref _installedVersion, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    public string DetectionSource
    {
        get => _detectionSource;
        set => SetProperty(ref _detectionSource, value);
    }

    public bool CanInstall => !IsInstalled;
    public bool CanUninstall => IsInstalled;
    public bool CanReinstall => IsInstalled;

    public string? CustomStatus
    {
        get => _customStatus;
        set
        {
            if (SetProperty(ref _customStatus, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    public string StatusText => !string.IsNullOrWhiteSpace(CustomStatus)
        ? CustomStatus
        : IsInstalled
            ? string.IsNullOrWhiteSpace(InstalledVersion)
                ? "Installed"
                : $"Installed {InstalledVersion}"
            : "Not installed";

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
