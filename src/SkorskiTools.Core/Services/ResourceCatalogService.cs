using System.Text.Json;
using Serilog;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;
using SkorskiTools.Core.Serialization;

namespace SkorskiTools.Core.Services;

/// <summary>Discovers and validates every resource in the resources directory.</summary>
public interface IResourceCatalogService
{
    Task<CatalogLoadResult> LoadAsync(
        string resourcesDirectory,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Scans the category directories for manifest.json files, validates each one and
/// builds the catalog. Invalid resources are reported and skipped without stopping
/// the rest of the discovery.
/// </summary>
public sealed class ResourceCatalogService : IResourceCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    private readonly ILogger _logger;

    public ResourceCatalogService(ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    public Task<CatalogLoadResult> LoadAsync(
        string resourcesDirectory,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(resourcesDirectory, cancellationToken), cancellationToken);

    private CatalogLoadResult Load(string resourcesDirectory, CancellationToken cancellationToken)
    {
        var issues = new List<CatalogIssue>();
        var fullPath = Path.GetFullPath(resourcesDirectory);

        if (!Directory.Exists(fullPath))
        {
            issues.Add(new CatalogIssue(IssueSeverity.Error, "Resources directory not found", Path: fullPath));
            _logger.Warning("Resources directory not found: {Path}", fullPath);
            return new CatalogLoadResult { ResourcesDirectory = fullPath, Issues = issues };
        }

        var schemasDirectory = Path.Combine(fullPath, AppPaths.SchemasFolderName);
        var categories = LoadCategories(fullPath, schemasDirectory, issues);
        var reader = new ManifestReader(schemasDirectory, categories.Select(c => c.Id));

        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var applications = new List<ApplicationManifest>();
        var browsers = new List<ApplicationManifest>();
        var portables = new List<PortableManifest>();
        var auxiliary = new List<AuxiliaryManifest>();

        Discover(fullPath, "installers", ManifestKind.Application, reader, usedIds, issues,
            m => applications.Add((ApplicationManifest)m));
        Discover(fullPath, "browsers", ManifestKind.Browser, reader, usedIds, issues,
            m => browsers.Add((ApplicationManifest)m));
        Discover(fullPath, "portables", ManifestKind.Portable, reader, usedIds, issues,
            m => portables.Add((PortableManifest)m));
        Discover(fullPath, "auxiliary", ManifestKind.Auxiliary, reader, usedIds, issues,
            m => auxiliary.Add((AuxiliaryManifest)m));

        cancellationToken.ThrowIfCancellationRequested();
        var scripts = LoadScripts(fullPath, schemasDirectory, usedIds, issues);

        _logger.Information(
            "Catalog loaded: {Applications} applications, {Browsers} browsers, {Portables} portables, " +
            "{Auxiliary} auxiliary, {Scripts} scripts, {Issues} issues",
            applications.Count, browsers.Count, portables.Count, auxiliary.Count, scripts.Count, issues.Count);

        return new CatalogLoadResult
        {
            ResourcesDirectory = fullPath,
            Categories = categories,
            Applications = applications,
            Browsers = browsers,
            Portables = portables,
            Auxiliary = auxiliary,
            Scripts = scripts,
            Issues = issues
        };
    }

    private void Discover(
        string resourcesDirectory,
        string folderName,
        ManifestKind kind,
        ManifestReader reader,
        HashSet<string> usedIds,
        List<CatalogIssue> issues,
        Action<ManifestBase> add)
    {
        var categoryDirectory = Path.Combine(resourcesDirectory, folderName);
        if (!Directory.Exists(categoryDirectory))
            return;

        var manifestPaths = new List<string>();
        var rootManifest = Path.Combine(categoryDirectory, "manifest.json");
        if (File.Exists(rootManifest))
            manifestPaths.Add(rootManifest);

        manifestPaths.AddRange(
            Directory.EnumerateDirectories(categoryDirectory)
                .Select(d => Path.Combine(d, "manifest.json"))
                .Where(File.Exists));

        foreach (var manifestPath in manifestPaths)
        {
            var result = reader.Read(manifestPath, kind);
            if (!result.IsValid || result.Manifest is null)
            {
                foreach (var error in result.Errors)
                {
                    issues.Add(new CatalogIssue(IssueSeverity.Error, error, Path: manifestPath));
                    _logger.Warning("Invalid manifest {Path}: {Error}", manifestPath, error);
                }
                continue;
            }

            var manifest = result.Manifest;
            if (!usedIds.Add(manifest.Id))
            {
                issues.Add(new CatalogIssue(
                    IssueSeverity.Error, $"Duplicate id '{manifest.Id}' (first one kept)",
                    manifest.Id, manifestPath));
                _logger.Warning("Duplicate resource id {Id} at {Path}", manifest.Id, manifestPath);
                continue;
            }

            add(manifest);
        }
    }

    private static IReadOnlyList<Category> LoadCategories(
        string resourcesDirectory,
        string schemasDirectory,
        List<CatalogIssue> issues)
    {
        var path = Path.Combine(resourcesDirectory, "categories.json");
        var result = ReadJson<CategoriesFileDto>(path, Path.Combine(schemasDirectory, "categories.schema.json"), issues);
        if (result is null)
            return new[] { new Category { Id = "other", Name = "Other" } };

        var categories = new List<Category>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dto in result.Categories ?? new List<CategoryDto>())
        {
            if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Name))
            {
                issues.Add(new CatalogIssue(IssueSeverity.Error, "Category requires id and name", Path: path));
                continue;
            }
            if (!ids.Add(dto.Id))
            {
                issues.Add(new CatalogIssue(IssueSeverity.Error, $"Duplicate category id '{dto.Id}'", Path: path));
                continue;
            }
            categories.Add(new Category { Id = dto.Id, Name = dto.Name, Icon = dto.Icon });
        }

        if (ids.Add("other"))
            categories.Add(new Category { Id = "other", Name = "Other" });

        return categories;
    }

    private static IReadOnlyList<ScriptItem> LoadScripts(
        string resourcesDirectory,
        string schemasDirectory,
        HashSet<string> usedIds,
        List<CatalogIssue> issues)
    {
        var path = Path.Combine(resourcesDirectory, "scripts", "scripts.json");
        if (!File.Exists(path))
            return Array.Empty<ScriptItem>();

        var result = ReadJson<ScriptsFileDto>(path, Path.Combine(schemasDirectory, "scripts.schema.json"), issues);
        if (result is null)
            return Array.Empty<ScriptItem>();

        if (result.SchemaVersion != 1)
            issues.Add(new CatalogIssue(IssueSeverity.Error, "scripts.json schemaVersion must be 1", Path: path));

        var scripts = new List<ScriptItem>();
        foreach (var dto in result.Scripts ?? new List<ScriptDto>())
        {
            if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Title))
            {
                issues.Add(new CatalogIssue(IssueSeverity.Error, "Script requires id and title", Path: path));
                continue;
            }
            if (!usedIds.Add(dto.Id))
            {
                issues.Add(new CatalogIssue(
                    IssueSeverity.Error, $"Duplicate id '{dto.Id}' (first one kept)", dto.Id, path));
                continue;
            }
            scripts.Add(new ScriptItem
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                Language = dto.Language,
                Code = dto.Code
            });
        }

        return scripts;
    }

    private static T? ReadJson<T>(string path, string schemaPath, List<CatalogIssue> issues) where T : class
    {
        if (!File.Exists(path))
        {
            issues.Add(new CatalogIssue(IssueSeverity.Warning, $"{Path.GetFileName(path)} not found", Path: path));
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            foreach (var error in SchemaValidator.Validate(document.RootElement, schemaPath))
                issues.Add(new CatalogIssue(IssueSeverity.Error, error, Path: path));

            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            issues.Add(new CatalogIssue(IssueSeverity.Error, $"Cannot read {Path.GetFileName(path)}: {ex.Message}", Path: path));
            return null;
        }
    }
}
