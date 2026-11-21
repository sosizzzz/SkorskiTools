namespace SkorskiTools.Core.Models;

/// <summary>How a resource is handled by the unattended installation engine.</summary>
public sealed class FullAutoConfig
{
    public bool Enabled { get; init; }

    /// <summary>Silent switches passed to the original installer as separate arguments.</summary>
    public IReadOnlyList<string> InstallArguments { get; init; } = Array.Empty<string>();

    /// <summary>Exit codes treated as success. Defaults to 0.</summary>
    public IReadOnlyList<int> ExpectedExitCodes { get; init; } = new[] { 0 };

    /// <summary>Exit codes that mean "finished, but a restart is required".</summary>
    public IReadOnlyList<int> RestartExitCodes { get; init; } = new[] { 3010, 1641 };

    public FullAutoUninstallConfig Uninstall { get; init; } = new();
}

/// <summary>Registry-based unattended uninstall options.</summary>
public sealed class FullAutoUninstallConfig
{
    public bool UseQuietUninstallString { get; init; } = true;

    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
}
