namespace SkorskiTools.Core.Models;

/// <summary>A registry uninstall entry used for detection and uninstallation.</summary>
public sealed record UninstallEntry(
    string KeyName,
    string DisplayName,
    string DisplayVersion,
    string UninstallString,
    string QuietUninstallString,
    string Publisher);
