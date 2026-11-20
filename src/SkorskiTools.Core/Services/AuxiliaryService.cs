using Serilog;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;

namespace SkorskiTools.Core.Services;

/// <summary>Copies auxiliary files without ever executing them.</summary>
public interface IAuxiliaryService
{
    bool AreCopied(AuxiliaryManifest manifest);

    Task<OperationResult> CopyAsync(
        AuxiliaryManifest manifest,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Verifies and copies owner-provided files into the user auxiliary folder.
/// No file is launched, executed or modified.
/// </summary>
public sealed class AuxiliaryService : IAuxiliaryService
{
    private readonly string _userRoot;
    private readonly ILogger _logger;

    public AuxiliaryService(string? userRoot = null, ILogger? logger = null)
    {
        _userRoot = userRoot ?? AppPaths.GetUserRoot();
        _logger = logger ?? Log.Logger;
    }

    public bool AreCopied(AuxiliaryManifest manifest)
    {
        var destination = GetDestination(manifest);
        return manifest.Files.Count > 0
               && manifest.Files.All(file =>
                   File.Exists(Path.Combine(destination, Path.GetFileName(file.File))));
    }

    public Task<OperationResult> CopyAsync(
        AuxiliaryManifest manifest,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (manifest.Files.Count == 0)
            return Task.FromResult(OperationResult.Fail("The manifest does not define any files."));

        var sourcePaths = new List<(string Source, string Name)>();
        foreach (var file in manifest.Files)
        {
            var source = ResourcePathGuard.Resolve(manifest.ResourceDirectory, file.File);
            if (source is null || !File.Exists(source))
                return Task.FromResult(OperationResult.Fail($"File '{file.File}' was not found."));

            if (!Sha256Verifier.Matches(source, file.Sha256))
                return Task.FromResult(OperationResult.Fail(
                    $"SHA-256 mismatch for '{file.File}'. No file was copied."));

            sourcePaths.Add((source, Path.GetFileName(file.File)));
        }

        try
        {
            var destination = GetDestination(manifest);
            Directory.CreateDirectory(destination);

            foreach (var (source, name) in sourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(source, Path.Combine(destination, name), overwrite: true);
            }

            _logger.Information(
                "Copied {Count} auxiliary file(s) for {Id} to {Path}",
                sourcePaths.Count, manifest.Id, destination);

            return Task.FromResult(OperationResult.Ok(
                $"Copied {sourcePaths.Count} file(s) for {manifest.Name}."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error(ex, "Could not copy auxiliary files for {Id}", manifest.Id);
            return Task.FromResult(OperationResult.Fail($"Could not copy files: {ex.Message}"));
        }
    }

    private string GetDestination(AuxiliaryManifest manifest)
    {
        var root = Path.GetFullPath(Path.Combine(_userRoot, "auxiliary"));
        var destination = Path.GetFullPath(Path.Combine(root, manifest.Id));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Auxiliary id '{manifest.Id}' is not a safe directory name.");

        return destination;
    }
}
