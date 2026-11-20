using System.Security;
using Microsoft.Win32;
using Serilog;
using SkorskiTools.Core.Models;

namespace SkorskiTools.Core.Services;

/// <summary>Reads all three Windows uninstall locations.</summary>
public sealed class WindowsUninstallEntryProvider : IUninstallEntryProvider
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string Wow6432Path = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly (RegistryKey Root, string Path)[] Locations =
    {
        (Registry.LocalMachine, UninstallPath),
        (Registry.LocalMachine, Wow6432Path),
        (Registry.CurrentUser, UninstallPath)
    };

    private readonly ILogger _logger;

    public WindowsUninstallEntryProvider(ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    public IReadOnlyList<UninstallEntry> GetEntries()
    {
        var entries = new List<UninstallEntry>();

        foreach (var (root, path) in Locations)
        {
            try
            {
                using var key = root.OpenSubKey(path);
                if (key is null)
                    continue;

                foreach (var name in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(name);
                        if (subKey is null)
                            continue;

                        var displayName = Read(subKey, "DisplayName");
                        if (string.IsNullOrWhiteSpace(displayName))
                            continue;

                        entries.Add(new UninstallEntry(
                            name,
                            displayName,
                            Read(subKey, "DisplayVersion"),
                            Read(subKey, "UninstallString"),
                            Read(subKey, "QuietUninstallString"),
                            Read(subKey, "Publisher")));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _logger.Debug(ex, "Could not read uninstall subkey {Key}", name);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                _logger.Warning(ex, "Could not read uninstall location {Path}", path);
            }
        }

        return entries;
    }

    private static string Read(RegistryKey key, string name) =>
        key.GetValue(name) as string ?? string.Empty;
}
