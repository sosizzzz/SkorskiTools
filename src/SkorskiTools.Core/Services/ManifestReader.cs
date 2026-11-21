using System.Text.Json;
using System.Text.RegularExpressions;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;
using SkorskiTools.Core.Serialization;

namespace SkorskiTools.Core.Services;

/// <summary>
/// Validates a manifest against its JSON Schema and the project rules, then maps it
/// to a strongly typed model. A single invalid manifest never affects the others.
/// </summary>
public sealed partial class ManifestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    private readonly string _schemasDirectory;
    private readonly IReadOnlySet<string> _validCategories;
    private readonly bool _verifyFileHashes;

    public ManifestReader(
        string schemasDirectory,
        IEnumerable<string>? validCategories = null,
        bool verifyFileHashes = true)
    {
        _schemasDirectory = schemasDirectory;
        _validCategories = validCategories?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                           ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _verifyFileHashes = verifyFileHashes;
    }

    public ManifestReadResult Read(string manifestPath, ManifestKind kind)
    {
        var resourceDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? string.Empty;
        var schemaFile = kind is ManifestKind.Application or ManifestKind.Browser
            ? "application.schema.json"
            : kind == ManifestKind.Portable ? "portable.schema.json" : "auxiliary.schema.json";

        string json;
        try
        {
            json = File.ReadAllText(manifestPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ManifestReadResult.Failure(new[] { $"Cannot read manifest: {ex.Message}" });
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return ManifestReadResult.Failure(new[] { $"Invalid JSON: {ex.Message}" });
        }

        var errors = new List<string>(SchemaValidator.Validate(root, Path.Combine(_schemasDirectory, schemaFile)));

        ManifestDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<ManifestDto>(json, JsonOptions) ?? new ManifestDto();
        }
        catch (JsonException ex)
        {
            errors.Add($"Cannot deserialize manifest: {ex.Message}");
            return ManifestReadResult.Failure(errors);
        }

        ValidateCommon(dto, resourceDirectory, errors);

        return kind switch
        {
            ManifestKind.Application or ManifestKind.Browser =>
                ReadApplication(dto, resourceDirectory, kind, errors),
            ManifestKind.Portable =>
                ReadPortable(dto, resourceDirectory, errors),
            _ => ReadAuxiliary(dto, resourceDirectory, errors)
        };
    }

    private ManifestReadResult ReadApplication(
        ManifestDto dto,
        string resourceDirectory,
        ManifestKind kind,
        List<string> errors)
    {
        var files = ValidateFiles(dto.Files, resourceDirectory, "files", errors);
        var detection = ValidateDetection(dto.Detection, errors);
        var uninstallation = ValidateUninstallation(dto.Uninstallation, errors);
        var fullAuto = ValidateFullAuto(dto.FullAuto, errors);
        var arguments = dto.Installation?.Arguments ?? new List<string>();

        if (arguments.Any(string.IsNullOrWhiteSpace))
            errors.Add("installation.arguments must not contain empty values");

        if (errors.Count > 0)
            return ManifestReadResult.Failure(errors);

        return ManifestReadResult.Success(new ApplicationManifest
        {
            SchemaVersion = dto.SchemaVersion,
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            Version = dto.Version,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "other" : dto.Category,
            Icon = dto.Icon,
            ManifestPath = Path.Combine(resourceDirectory, "manifest.json"),
            ResourceDirectory = resourceDirectory,
            Kind = kind,
            Files = files,
            Detection = detection,
            Installation = new InstallationConfig { Arguments = arguments.ToArray() },
            Uninstallation = uninstallation,
            FullAuto = fullAuto
        });
    }

    private ManifestReadResult ReadPortable(
        ManifestDto dto,
        string resourceDirectory,
        List<string> errors)
    {
        if (dto.Package is null)
        {
            errors.Add("package is required");
            return ManifestReadResult.Failure(errors);
        }

        var package = ValidateFiles(new[] { dto.Package }, resourceDirectory, "package", errors);
        if (string.IsNullOrWhiteSpace(dto.Executable))
            errors.Add("executable is required");

        if (errors.Count > 0)
            return ManifestReadResult.Failure(errors);

        return ManifestReadResult.Success(new PortableManifest
        {
            SchemaVersion = dto.SchemaVersion,
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            Version = dto.Version,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "other" : dto.Category,
            Icon = dto.Icon,
            ManifestPath = Path.Combine(resourceDirectory, "manifest.json"),
            ResourceDirectory = resourceDirectory,
            Kind = ManifestKind.Portable,
            Package = package[0],
            Executable = dto.Executable,
            ShortcutName = string.IsNullOrWhiteSpace(dto.ShortcutName) ? dto.Name : dto.ShortcutName
        });
    }

    private ManifestReadResult ReadAuxiliary(
        ManifestDto dto,
        string resourceDirectory,
        List<string> errors)
    {
        var files = ValidateFiles(dto.Files, resourceDirectory, "files", errors);

        if (errors.Count > 0)
            return ManifestReadResult.Failure(errors);

        return ManifestReadResult.Success(new AuxiliaryManifest
        {
            SchemaVersion = dto.SchemaVersion,
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            Version = dto.Version,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "other" : dto.Category,
            Icon = dto.Icon,
            ManifestPath = Path.Combine(resourceDirectory, "manifest.json"),
            ResourceDirectory = resourceDirectory,
            Kind = ManifestKind.Auxiliary,
            Files = files
        });
    }

    private void ValidateCommon(ManifestDto dto, string resourceDirectory, List<string> errors)
    {
        if (dto.SchemaVersion != 1)
            errors.Add("schemaVersion must be 1");

        if (string.IsNullOrWhiteSpace(dto.Id))
            errors.Add("id is required");
        else if (!IdRegex().IsMatch(dto.Id))
            errors.Add("id must contain only lowercase letters, digits and hyphens");

        if (string.IsNullOrWhiteSpace(dto.Name))
            errors.Add("name is required");

        var category = string.IsNullOrWhiteSpace(dto.Category) ? "other" : dto.Category;
        if (_validCategories.Count > 0 && !_validCategories.Contains(category))
            errors.Add($"category '{category}' is not defined in categories.json");

        if (!string.IsNullOrWhiteSpace(dto.Icon)
            && ResourcePathGuard.Resolve(resourceDirectory, dto.Icon) is null)
            errors.Add($"icon '{dto.Icon}' is not a safe relative path");
    }

    private IReadOnlyList<ManifestFile> ValidateFiles(
        IEnumerable<ManifestFileDto>? files,
        string resourceDirectory,
        string fieldName,
        List<string> errors)
    {
        var list = files?.ToList() ?? new List<ManifestFileDto>();
        if (list.Count == 0)
        {
            errors.Add($"{fieldName} must contain at least one file");
            return Array.Empty<ManifestFile>();
        }

        var result = new List<ManifestFile>();
        foreach (var file in list)
        {
            var resolved = ResourcePathGuard.Resolve(resourceDirectory, file.File);
            if (resolved is null)
            {
                errors.Add($"{fieldName}.file '{file.File}' is not a safe relative path");
                continue;
            }

            if (!File.Exists(resolved))
            {
                errors.Add($"{fieldName}.file '{file.File}' does not exist");
                continue;
            }

            if (!Sha256Verifier.IsValidHashFormat(file.Sha256))
            {
                errors.Add($"{fieldName}.sha256 of '{file.File}' must be a 64-character hexadecimal value");
                continue;
            }

            if (_verifyFileHashes && !Sha256Verifier.Matches(resolved, file.Sha256))
            {
                errors.Add($"SHA-256 mismatch for '{file.File}'");
                continue;
            }

            if (!TryParseArchitecture(file.Arch, out var arch))
            {
                errors.Add($"{fieldName}.arch '{file.Arch}' is invalid (expected x64, x86, arm64 or any)");
                continue;
            }

            result.Add(new ManifestFile
            {
                File = file.File,
                Sha256 = file.Sha256.ToLowerInvariant(),
                Arch = arch
            });
        }

        return result;
    }

    private static DetectionConfig ValidateDetection(DetectionDto? dto, List<string> errors)
    {
        if (dto is null)
            return new DetectionConfig();

        DetectionType type;
        switch (dto.Type.ToLowerInvariant())
        {
            case "registry": type = DetectionType.Registry; break;
            case "msi-product-code": type = DetectionType.MsiProductCode; break;
            case "file-exists": type = DetectionType.FileExists; break;
            case "path": type = DetectionType.Path; break;
            default:
                errors.Add($"detection.type '{dto.Type}' is invalid");
                type = DetectionType.Registry;
                break;
        }

        NameMatchMode matchMode;
        switch (dto.MatchMode.ToLowerInvariant())
        {
            case "":
            case "exact": matchMode = NameMatchMode.Exact; break;
            case "startswith": matchMode = NameMatchMode.StartsWith; break;
            default:
                errors.Add($"detection.matchMode '{dto.MatchMode}' is invalid");
                matchMode = NameMatchMode.Exact;
                break;
        }

        if (type == DetectionType.Registry && string.IsNullOrWhiteSpace(dto.DisplayName))
            errors.Add("detection.displayName is required for registry detection");
        if (type == DetectionType.MsiProductCode && string.IsNullOrWhiteSpace(dto.ProductCode))
            errors.Add("detection.productCode is required for MSI detection");
        if (type is DetectionType.FileExists or DetectionType.Path
            && string.IsNullOrWhiteSpace(dto.Path))
            errors.Add("detection.path is required for file/path detection");

        return new DetectionConfig
        {
            Type = type,
            DisplayName = dto.DisplayName,
            MatchMode = matchMode,
            ProductCode = dto.ProductCode,
            Path = dto.Path,
            Executable = dto.Executable
        };
    }

    private static FullAutoConfig? ValidateFullAuto(FullAutoDto? dto, List<string> errors)
    {
        if (dto is null)
            return null;

        var arguments = dto.InstallArguments ?? new List<string>();
        if (dto.Enabled && arguments.Count == 0)
            errors.Add("fullAuto.installArguments must contain verified silent switches when fullAuto.enabled is true");

        if (arguments.Any(string.IsNullOrWhiteSpace))
            errors.Add("fullAuto.installArguments must not contain empty values");

        InstallationScope scope;
        switch (dto.Scope.ToLowerInvariant())
        {
            case "":
            case "user": scope = InstallationScope.User; break;
            case "machine": scope = InstallationScope.Machine; break;
            default:
                errors.Add($"fullAuto.scope '{dto.Scope}' is invalid (expected user or machine)");
                scope = InstallationScope.User;
                break;
        }

        if (dto.ExpectedExitCodes is { Count: 0 })
            errors.Add("fullAuto.expectedExitCodes must not be empty");

        if (dto.RestartExitCodes is { Count: 0 })
            errors.Add("fullAuto.restartExitCodes must not be empty");

        var uninstallArguments = dto.Uninstall?.Arguments ?? new List<string>();
        if (uninstallArguments.Any(string.IsNullOrWhiteSpace))
            errors.Add("fullAuto.uninstall.arguments must not contain empty values");

        return new FullAutoConfig
        {
            Enabled = dto.Enabled,
            Scope = scope,
            InstallArguments = arguments.ToArray(),
            ExpectedExitCodes = dto.ExpectedExitCodes?.ToArray() ?? new[] { 0 },
            RestartExitCodes = dto.RestartExitCodes?.ToArray() ?? new[] { 3010, 1641 },
            Uninstall = new FullAutoUninstallConfig
            {
                UseQuietUninstallString = dto.Uninstall?.UseQuietUninstallString ?? true,
                Arguments = uninstallArguments.ToArray()
            }
        };
    }

    private static UninstallationConfig ValidateUninstallation(UninstallationDto? dto, List<string> errors)
    {
        if (dto is null)
            return new UninstallationConfig();

        UninstallMode mode;
        switch (dto.Mode.ToLowerInvariant())
        {
            case "":
            case "registered-uninstaller": mode = UninstallMode.RegisteredUninstaller; break;
            case "msi-product-code": mode = UninstallMode.MsiProductCode; break;
            default:
                errors.Add($"uninstallation.mode '{dto.Mode}' is invalid");
                mode = UninstallMode.RegisteredUninstaller;
                break;
        }

        if (mode == UninstallMode.MsiProductCode && string.IsNullOrWhiteSpace(dto.ProductCode))
            errors.Add("uninstallation.productCode is required for MSI uninstallation");

        return new UninstallationConfig { Mode = mode, ProductCode = dto.ProductCode };
    }

    private static bool TryParseArchitecture(string value, out TargetArchitecture arch)
    {
        switch (value.ToLowerInvariant())
        {
            case "":
            case "any": arch = TargetArchitecture.Any; return true;
            case "x64": arch = TargetArchitecture.X64; return true;
            case "x86": arch = TargetArchitecture.X86; return true;
            case "arm64": arch = TargetArchitecture.Arm64; return true;
            default: arch = TargetArchitecture.Any; return false;
        }
    }

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex IdRegex();
}
