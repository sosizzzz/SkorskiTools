using System.Diagnostics;
using Serilog;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;

namespace SkorskiTools.Core.Services;

/// <summary>Runs the original installers and registered uninstallers.</summary>
public interface IApplicationInstaller
{
    Task<OperationResult> InstallAsync(
        ApplicationManifest manifest,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UninstallAsync(
        ApplicationManifest manifest,
        DetectionResult detection,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Installations are manual in 1.0: the original installer is shown to the user and
/// this method waits for it to finish. All arguments are passed as a list.
/// </summary>
public sealed class ApplicationInstaller : IApplicationInstaller
{
    private readonly ILogger _logger;

    public ApplicationInstaller(ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    public async Task<OperationResult> InstallAsync(
        ApplicationManifest manifest,
        CancellationToken cancellationToken = default)
    {
        var file = ArchitectureSelector.Select(manifest.Files);
        if (file is null)
            return OperationResult.Fail("The manifest does not define any installer file.");

        var path = ResourcePathGuard.Resolve(manifest.ResourceDirectory, file.File);
        if (path is null || !File.Exists(path))
            return OperationResult.Fail($"Installer '{file.File}' was not found.");

        if (!Sha256Verifier.Matches(path, file.Sha256))
            return OperationResult.Fail($"SHA-256 mismatch for '{file.File}'. The operation was cancelled.");

        try
        {
            var start = new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                WorkingDirectory = manifest.ResourceDirectory
            };

            foreach (var argument in manifest.Installation.Arguments)
            {
                if (!string.IsNullOrWhiteSpace(argument))
                    start.ArgumentList.Add(argument);
            }

            _logger.Information("Starting installer for {Id}: {Path}", manifest.Id, path);
            using var process = Process.Start(start);
            if (process is null)
                return OperationResult.Fail("The installer could not be started.");

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            _logger.Information("Installer for {Id} finished with code {Code}", manifest.Id, process.ExitCode);

            return process.ExitCode == 0
                ? OperationResult.Ok($"Installer for {manifest.Name} finished successfully.")
                : OperationResult.Fail($"Installer for {manifest.Name} exited with code {process.ExitCode}.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            _logger.Error(ex, "Installer for {Id} failed", manifest.Id);
            return OperationResult.Fail($"The installer could not be started: {ex.Message}");
        }
    }

    public async Task<OperationResult> UninstallAsync(
        ApplicationManifest manifest,
        DetectionResult detection,
        CancellationToken cancellationToken = default)
    {
        var entry = detection.Entry;
        if (entry is null || string.IsNullOrWhiteSpace(entry.UninstallString))
        {
            // A QuietUninstallString alone is still a validated registry value.
            if (entry is not null && !string.IsNullOrWhiteSpace(entry.QuietUninstallString))
                return await RunCommandAsync(manifest, entry.QuietUninstallString, cancellationToken)
                    .ConfigureAwait(false);

            return OperationResult.Fail(
                $"No registered uninstaller was found for {manifest.Name}. Nothing was removed.");
        }

        return await RunCommandAsync(manifest, entry.UninstallString, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<OperationResult> RunCommandAsync(
        ApplicationManifest manifest,
        string command,
        CancellationToken cancellationToken)
    {
        var (executable, arguments) = SplitCommand(command);

        if (string.IsNullOrWhiteSpace(executable))
            return OperationResult.Fail("The registered uninstall command is empty.");

        if (Path.IsPathRooted(executable) && !File.Exists(executable))
            return OperationResult.Fail("The registered uninstaller executable does not exist.");

        try
        {
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = true
            };

            foreach (var argument in ParseArguments(arguments))
                start.ArgumentList.Add(argument);

            _logger.Information("Starting uninstaller for {Id}", manifest.Id);
            using var process = Process.Start(start);
            if (process is null)
                return OperationResult.Fail("The uninstaller could not be started.");

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return OperationResult.Ok($"Uninstaller for {manifest.Name} finished.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            _logger.Error(ex, "Uninstaller for {Id} failed", manifest.Id);
            return OperationResult.Fail($"The uninstaller could not be started: {ex.Message}");
        }
    }

    /// <summary>Splits a registered command into executable and argument text.</summary>
    public static (string Executable, string Arguments) SplitCommand(string command)
    {
        var text = command.Trim();

        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            if (end > 1)
                return (text[1..end], text[(end + 1)..].Trim());
        }

        var separator = text.IndexOfAny(new[] { ' ', '\t' });
        return separator < 0
            ? (text, string.Empty)
            : (text[..separator], text[(separator + 1)..].Trim());
    }

    /// <summary>Parses arguments into separate tokens, keeping quoted values together.</summary>
    public static IReadOnlyList<string> ParseArguments(string arguments)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;

        foreach (var character in arguments)
        {
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return result;
    }
}
