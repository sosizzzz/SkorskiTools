namespace SkorskiTools.Core.Models;

/// <summary>How a resource is handled by the unattended installation engine.</summary>
public sealed class FullAutoConfig
{
    /// <summary>Fallback guidance when a vendor does not document a specific failure code.</summary>
    public const string GenericFailureHint =
        "The installer did not finish successfully. Check the log, free disk space, " +
        "firewall/antivirus settings and, for web installers, the internet connection.";

    public bool Enabled { get; init; }

    public InstallationScope Scope { get; init; } = InstallationScope.User;

    /// <summary>False when the vendor's installer cannot run unattended.</summary>
    public bool Supported { get; init; } = true;

    /// <summary>Why unattended installation is not possible, shown to the user.</summary>
    public string UnsupportedReason { get; init; } = string.Empty;

    /// <summary>Optional config file passed to the installer, verified like other files.</summary>
    public string ConfigurationFile { get; init; } = string.Empty;

    public string ConfigurationSha256 { get; init; } = string.Empty;

    /// <summary>Silent switches passed to the original installer as separate arguments.</summary>
    public IReadOnlyList<string> InstallArguments { get; init; } = Array.Empty<string>();

    /// <summary>Exit codes treated as success. Defaults to 0.</summary>
    public IReadOnlyList<int> ExpectedExitCodes { get; init; } = new[] { 0 };

    /// <summary>Exit codes that mean "finished, but a restart is required".</summary>
    public IReadOnlyList<int> RestartExitCodes { get; init; } = new[] { 3010, 1641 };

    /// <summary>
    /// Plain-language explanations for known failure codes, keyed by exit code. Adding a
    /// hint here requires no code change, so the owner can document vendor-specific errors.
    /// </summary>
    public IReadOnlyDictionary<int, string> ExitCodeHints { get; init; } =
        new Dictionary<int, string>();

    public FullAutoUninstallConfig Uninstall { get; init; } = new();

    /// <summary>Builds an actionable message for an installer code that is not expected.</summary>
    public string DescribeUnexpectedExitCode(int exitCode)
    {
        var hint = ExitCodeHints.TryGetValue(exitCode, out var text) ? text : GenericFailureHint;
        return $"Installer exited with code {exitCode}. {hint}";
    }
}

/// <summary>Registry-based unattended uninstall options.</summary>
public sealed class FullAutoUninstallConfig
{
    public bool UseQuietUninstallString { get; init; } = true;

    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
}
