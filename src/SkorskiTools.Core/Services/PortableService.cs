using System.IO.Compression;
using Serilog;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;

namespace SkorskiTools.Core.Services;

/// <summary>Deploys, replaces and removes portable applications.</summary>
public interface IPortableService
{
    bool IsInstalled(PortableManifest manifest);

    Task<OperationResult> InstallAsync(PortableManifest manifest, CancellationToken cancellationToken = default);

    Task<OperationResult> ReinstallAsync(PortableManifest manifest, CancellationToken cancellationToken = default);

    Task<OperationResult> UninstallAsync(PortableManifest manifest, CancellationToken cancellationToken = default);
}

/// <summary>
/// Deploys into a temporary directory first and only moves it into place once the
/// package and its hash have been validated. No half-deployed portable is left behind.
/// </summary>
public sealed class PortableService : IPortableService
{
    private readonly string _userRoot;
    private readonly IShortcutService _shortcuts;
    private readonly ILogger _logger;

    public PortableService(
        string? userRoot = null,
        IShortcutService? shortcuts = null,
        ILogger? logger = null)
    {
        _userRoot = userRoot ?? AppPaths.GetUserRoot();
        _shortcuts = shortcuts ?? new DesktopShortcutService();
        _logger = logger ?? Log.Logger;
    }

    public bool IsInstalled(PortableManifest manifest) =>
        Directory.Exists(GetInstallDirectory(manifest));

    public Task<OperationResult> InstallAsync(
        PortableManifest manifest,
        CancellationToken cancellationToken = default)
    {
        if (IsInstalled(manifest))
            return Task.FromResult(OperationResult.Fail($"{manifest.Name} is already deployed."));

        return DeployAsync(manifest, cancellationToken);
    }

    public Task<OperationResult> ReinstallAsync(
        PortableManifest manifest,
        CancellationToken cancellationToken = default) =>
        DeployAsync(manifest, cancellationToken);

    public Task<OperationResult> UninstallAsync(
        PortableManifest manifest,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string directory;
        try
        {
            directory = GetInstallDirectory(manifest);
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(OperationResult.Fail(ex.Message));
        }

        if (!Directory.Exists(directory))
            return Task.FromResult(OperationResult.Fail($"{manifest.Name} is not deployed."));

        try
        {
            Directory.Delete(directory, recursive: true);
            _shortcuts.Delete(GetShortcutPath(manifest));
            _logger.Information("Removed portable {Id} from {Path}", manifest.Id, directory);
            return Task.FromResult(OperationResult.Ok($"{manifest.Name} was removed."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error(ex, "Could not remove portable {Id}", manifest.Id);
            return Task.FromResult(OperationResult.Fail($"Could not remove {manifest.Name}: {ex.Message}"));
        }
    }

    private async Task<OperationResult> DeployAsync(
        PortableManifest manifest,
        CancellationToken cancellationToken)
    {
        var package = ResourcePathGuard.Resolve(manifest.ResourceDirectory, manifest.Package.File);
        if (package is null || !File.Exists(package))
            return OperationResult.Fail($"Package '{manifest.Package.File}' was not found.");

        if (!Sha256Verifier.Matches(package, manifest.Package.Sha256))
            return OperationResult.Fail($"SHA-256 mismatch for '{manifest.Package.File}'. Nothing was deployed.");

        if (!ResourcePathGuard.IsSafeRelativePath(manifest.Executable))
            return OperationResult.Fail("The manifest executable is not a safe relative path.");

        string installDirectory;
        try
        {
            installDirectory = GetInstallDirectory(manifest);
        }
        catch (ArgumentException ex)
        {
            return OperationResult.Fail(ex.Message);
        }

        var tempDirectory = Path.Combine(
            Path.GetTempPath(), "skorski-portable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (package.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                ZipFile.ExtractToDirectory(package, tempDirectory);
            else
                File.Copy(package, Path.Combine(tempDirectory, Path.GetFileName(package)), overwrite: true);

            var executable = Directory
                .EnumerateFiles(tempDirectory, manifest.Executable, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (executable is null)
                return OperationResult.Fail($"Executable '{manifest.Executable}' was not found in the package.");

            var parent = Directory.GetParent(installDirectory)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);

            if (Directory.Exists(installDirectory))
                Directory.Delete(installDirectory, recursive: true);

            var relativeExecutable = Path.GetRelativePath(tempDirectory, executable);
            Directory.Move(tempDirectory, installDirectory);
            var finalExecutable = Path.Combine(installDirectory, relativeExecutable);

            _shortcuts.Create(
                GetShortcutPath(manifest),
                finalExecutable,
                installDirectory,
                manifest.Description);

            _logger.Information("Deployed portable {Id} to {Path}", manifest.Id, installDirectory);
            return OperationResult.Ok($"{manifest.Name} was deployed.");
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or InvalidDataException
                                   or OperationCanceledException)
        {
            _logger.Error(ex, "Could not deploy portable {Id}", manifest.Id);
            return OperationResult.Fail($"Could not deploy {manifest.Name}: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                try
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
                catch (IOException)
                {
                    _logger.Warning("Could not clean temporary directory {Path}", tempDirectory);
                }
            }
        }
    }

    private string GetInstallDirectory(PortableManifest manifest)
    {
        var root = Path.GetFullPath(Path.Combine(_userRoot, "portables"));
        var directory = Path.GetFullPath(Path.Combine(root, manifest.Id));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Portable id '{manifest.Id}' is not a safe directory name.");

        return directory;
    }

    private static string GetShortcutPath(PortableManifest manifest)
    {
        var name = manifest.ShortcutName;
        foreach (var character in Path.GetInvalidFileNameChars())
            name = name.Replace(character, '-');

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            name + ".lnk");
    }
}
