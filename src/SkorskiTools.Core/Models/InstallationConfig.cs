namespace SkorskiTools.Core.Models;

/// <summary>Installer options. Installations are manual in 1.0.</summary>
public sealed class InstallationConfig
{
    /// <summary>Arguments are always an array, never a concatenated string.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
}
