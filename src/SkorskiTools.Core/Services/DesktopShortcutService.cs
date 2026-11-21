using System.Reflection;
using Serilog;
using SkorskiTools.Core.Infrastructure;

namespace SkorskiTools.Core.Services;

/// <summary>Creates .lnk files through the Windows Shell COM object.</summary>
public sealed class DesktopShortcutService : IShortcutService
{
    private readonly ILogger _logger;

    public DesktopShortcutService(ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    public void Create(
        string shortcutPath,
        string targetPath,
        string workingDirectory,
        string description,
        string? iconPath = null)
    {
        try
        {
            var resolvedIcon = ResolveIcon(iconPath);
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

            // WScript.Shell is a late-bound IDispatch object: GetProperty/GetMethod
            // return null, so properties must be set through InvokeMember.
            var type = shortcut.GetType();
            type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
            type.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });

            if (resolvedIcon is not null)
            {
                type.InvokeMember(
                    "IconLocation",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { $"{resolvedIcon},0" });
            }

            type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

            _logger.Information(
                "Created shortcut {Shortcut} -> {Target} (icon: {Icon})",
                shortcutPath, targetPath, resolvedIcon ?? "default");
        }
        catch (Exception ex)
        {
            // A failed shortcut must not roll back an otherwise correct deployment.
            _logger.Warning(ex, "Could not create shortcut {Shortcut}", shortcutPath);
        }
    }

    private static string? ResolveIcon(string? iconPath)
    {
        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
            return iconPath;

        var shipped = Path.Combine(AppContext.BaseDirectory, "SkorskiTools.ico");
        return File.Exists(shipped) ? shipped : null;
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
