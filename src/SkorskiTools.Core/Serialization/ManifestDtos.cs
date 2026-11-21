namespace SkorskiTools.Core.Serialization;

/// <summary>Internal DTOs that mirror the JSON shape before validation and mapping.</summary>
internal sealed class ManifestDto
{
    public int SchemaVersion { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<string>? Highlights { get; set; }
    public List<ManifestFileDto>? Files { get; set; }
    public DetectionDto? Detection { get; set; }
    public InstallationDto? Installation { get; set; }
    public UninstallationDto? Uninstallation { get; set; }
    public FullAutoDto? FullAuto { get; set; }
    public ManifestFileDto? Package { get; set; }
    public string Executable { get; set; } = string.Empty;
    public string ShortcutName { get; set; } = string.Empty;
}

internal sealed class ManifestFileDto
{
    public string File { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Arch { get; set; } = string.Empty;
}

internal sealed class DetectionDto
{
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string MatchMode { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Executable { get; set; } = string.Empty;
}

internal sealed class InstallationDto
{
    public List<string>? Arguments { get; set; }
}

internal sealed class UninstallationDto
{
    public string Mode { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
}

internal sealed class FullAutoDto
{
    public bool Enabled { get; set; }
    public string Scope { get; set; } = string.Empty;
    public bool Supported { get; set; } = true;
    public string UnsupportedReason { get; set; } = string.Empty;
    public string ConfigurationFile { get; set; } = string.Empty;
    public string ConfigurationSha256 { get; set; } = string.Empty;
    public List<string>? InstallArguments { get; set; }
    public List<int>? ExpectedExitCodes { get; set; }
    public List<int>? RestartExitCodes { get; set; }
    public FullAutoUninstallDto? Uninstall { get; set; }
}

internal sealed class FullAutoUninstallDto
{
    public bool UseQuietUninstallString { get; set; } = true;
    public List<string>? Arguments { get; set; }
}

internal sealed class ScriptsFileDto
{
    public int SchemaVersion { get; set; }
    public List<ScriptDto>? Scripts { get; set; }
}

internal sealed class ScriptDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

internal sealed class CategoriesFileDto
{
    public List<CategoryDto>? Categories { get; set; }
}

internal sealed class CategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}
