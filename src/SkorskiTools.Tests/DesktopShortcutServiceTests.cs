using System.Reflection;
using SkorskiTools.Core.Services;

namespace SkorskiTools.Tests;

public sealed class DesktopShortcutServiceTests
{
    [Fact]
    public void Create_WritesRealShortcutWithRequestedIcon()
    {
        var directory = Path.Combine(Path.GetTempPath(), "skorski-lnk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var shortcutPath = Path.Combine(directory, "Test.lnk");
        var iconPath = Path.Combine(directory, "icon.ico");
        File.WriteAllBytes(iconPath, Array.Empty<byte>());

        try
        {
            var service = new DesktopShortcutService();
            service.Create(
                shortcutPath,
                @"C:\Windows\System32\notepad.exe",
                @"C:\Windows\System32",
                "Test shortcut",
                iconPath);

            Assert.True(File.Exists(shortcutPath), "The .lnk file was not created.");

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            Assert.NotNull(shellType);
            var shell = Activator.CreateInstance(shellType);
            var shortcut = shellType.InvokeMember(
                "CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Assert.NotNull(shortcut);

            var icon = shortcut.GetType().InvokeMember(
                "IconLocation", BindingFlags.GetProperty, null, shortcut, null) as string;

            Assert.Equal($"{iconPath},0", icon);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
