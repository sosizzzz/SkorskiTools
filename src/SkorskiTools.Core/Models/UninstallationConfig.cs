namespace SkorskiTools.Core.Models;

/// <summary>Describes the uninstall mechanism for an application.</summary>
public sealed class UninstallationConfig
{
    public UninstallMode Mode { get; init; } = UninstallMode.RegisteredUninstaller;
    public string ProductCode { get; init; } = string.Empty;
}
