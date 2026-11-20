using System.Reflection;
using Serilog;

namespace SkorskiTools.Core.Services;

/// <summary>Creates .lnk files through the Windows Shell COM object.</summary>
public sealed class DesktopShortcutService : IShortcutService
{
    private readonly ILogger _logger;

    public DesktopShortcutService(ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    public void Create(string shortcutPath, string targetPath, string workingDirectory, string description)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                            ?? throw new InvalidOperationException("Windows Shell is not available.");
            var shell = Activator.CreateInstance(shellType)
                        ?? throw new InvalidOperationException("Could not create the Shell object.");

            var shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });

            if (shortcut is null)
                throw new InvalidOperationException("Could not create the shortcut object.");

            var type = shortcut.GetType();
            type.GetProperty("TargetPath")?.SetValue(shortcut, targetPath);
            type.GetProperty("WorkingDirectory")?.SetValue(shortcut, workingDirectory);
            type.GetProperty("Description")?.SetValue(shortcut, description);
            type.GetMethod("Save")?.Invoke(shortcut, null);

            _logger.Information("Created shortcut {Shortcut} -> {Target}", shortcutPath, targetPath);
        }
        catch (Exception ex)
        {
            // A failed shortcut must not roll back an otherwise correct deployment.
            _logger.Warning(ex, "Could not create shortcut {Shortcut}", shortcutPath);
        }
    }

    public void Delete(string shortcutPath)
    {
        try
        {
            if (File.Exists(shortcutPath))
                File.Delete(shortcutPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(ex, "Could not delete shortcut {Shortcut}", shortcutPath);
        }
    }
}
