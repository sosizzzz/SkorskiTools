using System.Diagnostics;
using System.Runtime.InteropServices;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;

namespace SkorskiTools.Core.Services;

/// <summary>Detects installed applications independently of how they were installed.</summary>
public interface IInstallationDetector
{
    Task<DetectionResult> DetectAsync(
        ApplicationManifest manifest,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements all four detection strategies defined by the manifest.</summary>
public sealed class InstallationDetector : IInstallationDetector
{
    private readonly IUninstallEntryProvider _entries;

    public InstallationDetector(IUninstallEntryProvider? entries = null)
    {
        _entries = entries ?? new WindowsUninstallEntryProvider();
    }

    public Task<DetectionResult> DetectAsync(
        ApplicationManifest manifest,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var strategy = manifest.Detection;
        if (strategy is null)
            return Task.FromResult(DetectionResult.NotInstalled);

        var result = strategy.Type switch
        {
            DetectionType.Registry => DetectByDisplayName(strategy),
            DetectionType.MsiProductCode => DetectByProductCode(strategy),
            DetectionType.FileExists => DetectByFile(strategy, requireExecutable: false),
            DetectionType.Path => DetectByFile(strategy, requireExecutable: true),
            _ => DetectionResult.NotInstalled
        };

        return Task.FromResult(result);
    }

    private DetectionResult DetectByDisplayName(DetectionConfig strategy)
    {
        if (string.IsNullOrWhiteSpace(strategy.DisplayName))
            return DetectionResult.NotInstalled;

        var comparison = StringComparison.OrdinalIgnoreCase;
        var entry = _entries.GetEntries().FirstOrDefault(e =>
            strategy.MatchMode == NameMatchMode.StartsWith
                ? e.DisplayName.StartsWith(strategy.DisplayName, comparison)
                : string.Equals(e.DisplayName, strategy.DisplayName, comparison));

        return entry is null
            ? DetectionResult.NotInstalled
            : DetectionResult.Installed(entry.DisplayVersion, "Registry", entry);
    }

    private DetectionResult DetectByProductCode(DetectionConfig strategy)
    {
        if (string.IsNullOrWhiteSpace(strategy.ProductCode))
            return DetectionResult.NotInstalled;

        var entry = _entries.GetEntries().FirstOrDefault(e =>
            string.Equals(e.KeyName, strategy.ProductCode, StringComparison.OrdinalIgnoreCase));

        return entry is null
            ? DetectionResult.NotInstalled
            : DetectionResult.Installed(entry.DisplayVersion, "MSI product code", entry);
    }

    private static DetectionResult DetectByFile(DetectionConfig strategy, bool requireExecutable)
    {
        if (string.IsNullOrWhiteSpace(strategy.Path))
            return DetectionResult.NotInstalled;

        var expanded = Environment.ExpandEnvironmentVariables(strategy.Path);
        var path = requireExecutable && !string.IsNullOrWhiteSpace(strategy.Executable)
            ? Path.Combine(expanded, strategy.Executable)
            : expanded;

        if (!File.Exists(path))
            return DetectionResult.NotInstalled;

        var version = ReadFileVersion(path);
        return DetectionResult.Installed(version, requireExecutable ? "Path" : "File exists");
    }

    private static string? ReadFileVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return !string.IsNullOrWhiteSpace(info.ProductVersion)
                ? info.ProductVersion
                : info.FileVersion;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>Selects the bundled file matching the current CPU architecture.</summary>
public static class ArchitectureSelector
{
    public static ManifestFile? Select(IEnumerable<ManifestFile> files)
    {
        var list = files.ToList();
        if (list.Count == 0)
            return null;

        var current = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => TargetArchitecture.X64,
            Architecture.X86 => TargetArchitecture.X86,
            Architecture.Arm64 => TargetArchitecture.Arm64,
            _ => TargetArchitecture.Any
        };

        return list.FirstOrDefault(f => f.Arch == current)
               ?? list.FirstOrDefault(f => f.Arch == TargetArchitecture.Any)
               ?? list[0];
    }
}
