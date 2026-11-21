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

    /// <summary>Runs the installer with the manifest's unattended argument list.</summary>
    Task<OperationResult> InstallAutomatedAsync(
        ApplicationManifest manifest,
        CancellationToken cancellationToken = default);

    /// <summary>Runs the registered quiet uninstaller for a confirmed full-auto batch.</summary>
    Task<OperationResult> UninstallQuietAsync(
        ApplicationManifest manifest,
        DetectionResult detection,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Manual installations are shown to the user. Automated installations use only the
/// silent switches declared in the manifest; nothing is guessed by this class.
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
        var result = await RunInstallerAsync(
            manifest, manifest.Installation.Arguments, "Installer", cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
            return result;

        return result.ExitCode == 0
            ? OperationResult.Ok($"Installer for {manifest.Name} finished successfully.", result.ExitCode)
            : OperationResult.Fail($"Installer for {manifest.Name} exited with code {result.ExitCode}.", result.ExitCode);
    }

    public async Task<OperationResult> InstallAutomatedAsync(
        ApplicationManifest manifest,
        CancellationToken cancellationToken = default)
    {
        var fullAuto = manifest.FullAuto;
        if (fullAuto is null || !fullAuto.Enabled)
            return OperationResult.Fail($"{manifest.Name} is not configured for Full Auto.");

        if (fullAuto.InstallArguments.Count == 0)
            return OperationResult.Fail($"{manifest.Name} has no verified silent switches.");

        return await RunInstallerAsync(
            manifest, fullAuto.InstallArguments, "Full Auto installer", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OperationResult> UninstallAsync(
        ApplicationManifest manifest,
        DetectionResult detection,
        CancellationToken cancellationToken = default)
    {
        var entry = detection.Entry;
        if (entry is null || string.IsNullOrWhiteSpace(entry.UninstallString))
        {
            if (entry is not null && !string.IsNullOrWhiteSpace(entry.QuietUninstallString))
                return await RunCommandAsync(
                        manifest, entry.QuietUninstallString, Array.Empty<string>(), cancellationToken)
                    .ConfigureAwait(false);

            return OperationResult.Fail(
                $"No registered uninstaller was found for {manifest.Name}. Nothing was removed.");
        }

        return await RunCommandAsync(
                manifest, entry.UninstallString, Array.Empty<string>(), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OperationResult> UninstallQuietAsync(
        ApplicationManifest manifest,
        DetectionResult detection,
        CancellationToken cancellationToken = default)
    {
        var entry = detection.Entry;
        if (entry is null)
            return OperationResult.Fail($"No registry entry was found for {manifest.Name}.");

        var preferQuiet = manifest.FullAuto?.Uninstall.UseQuietUninstallString ?? true;
        var command = preferQuiet && !string.IsNullOrWhiteSpace(entry.QuietUninstallString)
            ? entry.QuietUninstallString
            : entry.UninstallString;

        if (string.IsNullOrWhiteSpace(command))
            return OperationResult.Fail($"No registered uninstaller was found for {manifest.Name}.");

        var arguments = manifest.FullAuto?.Uninstall.Arguments ?? Array.Empty<string>();
        return await RunCommandAsync(manifest, command, arguments, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<OperationResult> RunInstallerAsync(
        ApplicationManifest manifest,
        IReadOnlyList<string> arguments,
        string label,
        CancellationToken cancellationToken)
    {
        var file = ArchitectureSelector.Select(manifest.Files);
        if (file is null)
            return OperationResult.Fail("The manifest does not define any installer file.");

        var path = ResourcePathGuard.Resolve(manifest.ResourceDirectory, file.File);
        if (path is null || !File.Exists(path))
            return OperationResult.Fail($"Installer '{file.File}' was not found.");

        if (!Sha256Verifier.Matches(path, file.Sha256))
            return OperationResult.Fail($"SHA-256 mismatch for '{file.File}'. The operation was cancelled.");

        var start = new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            WorkingDirectory = manifest.ResourceDirectory
        };

        if (manifest.FullAuto?.Scope == InstallationScope.Machine)
            start.Verb = "runas";

        foreach (var argument in arguments)
        {
            if (!string.IsNullOrWhiteSpace(argument))
                start.ArgumentList.Add(argument);
        }

        try
        {
            _logger.Information("Starting {Label} for {Id}: {Path}", label, manifest.Id, path);
            using var process = Process.Start(start);
            if (process is null)
                return OperationResult.Fail("The installer could not be started.");

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            _logger.Information("{Label} for {Id} exited with code {Code}", label, manifest.Id, process.ExitCode);

            return new OperationResult(true, $"{label} for {manifest.Name} finished.")
            {
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex) when (ex is IOException
                                   or InvalidOperationException
                                   or UnauthorizedAccessException
                                   or OperationCanceledException)
        {
            _logger.Error(ex, "{Label} for {Id} failed", label, manifest.Id);
            return OperationResult.Fail($"The installer could not be started: {ex.Message}");
        }
    }

    private async Task<OperationResult> RunCommandAsync(
        ApplicationManifest manifest,
        string command,
        IReadOnlyList<string> extraArguments,
        CancellationToken cancellationToken)
    {
        var (executable, argumentText) = SplitCommand(command);

        if (string.IsNullOrWhiteSpace(executable))
            return OperationResult.Fail("The registered uninstall command is empty.");

        if (Path.IsPathRooted(executable) && !File.Exists(executable))
            return OperationResult.Fail("The registered uninstaller executable does not exist.");

        var start = new ProcessStartInfo(executable) { UseShellExecute = true };

        if (manifest.FullAuto?.Scope == InstallationScope.Machine)
            start.Verb = "runas";

        foreach (var argument in ParseArguments(argumentText).Concat(extraArguments))
        {
            if (!string.IsNullOrWhiteSpace(argument))
                start.ArgumentList.Add(argument);
        }

        try
        {
            _logger.Information("Starting uninstaller for {Id}", manifest.Id);
            using var process = Process.Start(start);
            if (process is null)
                return OperationResult.Fail("The uninstaller could not be started.");

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            _logger.Information("Uninstaller for {Id} exited with code {Code}", manifest.Id, process.ExitCode);

            return OperationResult.Ok($"Uninstaller for {manifest.Name} finished.", process.ExitCode);
        }
        catch (Exception ex) when (ex is IOException
                                   or InvalidOperationException
                                   or UnauthorizedAccessException
                                   or OperationCanceledException)
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
