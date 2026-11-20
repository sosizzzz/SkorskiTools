using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.Tests;

public sealed class PortableServiceTests
{
    [Fact]
    public async Task Install_DeploysFilesAndCreatesShortcut()
    {
        var (resources, userRoot, manifest, shortcuts) = await SetupAsync("tool.exe", "payload");
        try
        {
            var service = new PortableService(userRoot, shortcuts);

            var result = await service.InstallAsync(manifest);
            var installDirectory = Path.Combine(userRoot, "portables", manifest.Id);

            Assert.True(result.Success, result.Message);
            Assert.True(File.Exists(Path.Combine(installDirectory, "tool.exe")));
            Assert.True(service.IsInstalled(manifest));
            Assert.Single(shortcuts.Created);
            Assert.Equal(Path.Combine(installDirectory, "tool.exe"), shortcuts.Created[0].Target);
        }
        finally
        {
            Cleanup(resources, userRoot);
        }
    }

    [Fact]
    public async Task Install_Twice_FailsWithoutOverwriting()
    {
        var (resources, userRoot, manifest, shortcuts) = await SetupAsync("tool.exe", "payload");
        try
        {
            var service = new PortableService(userRoot, shortcuts);
            await service.InstallAsync(manifest);

            var second = await service.InstallAsync(manifest);

            Assert.False(second.Success);
            Assert.Single(shortcuts.Created);
        }
        finally
        {
            Cleanup(resources, userRoot);
        }
    }

    [Fact]
    public async Task Uninstall_DeletesOnlyItsOwnFolderAndShortcut()
    {
        var (resources, userRoot, manifest, shortcuts) = await SetupAsync("tool.exe", "payload");
        try
        {
            var service = new PortableService(userRoot, shortcuts);
            await service.InstallAsync(manifest);

            var other = Path.Combine(userRoot, "portables", "other-portable");
            Directory.CreateDirectory(other);
            File.WriteAllText(Path.Combine(other, "keep.txt"), "keep");

            var result = await service.UninstallAsync(manifest);

            Assert.True(result.Success, result.Message);
            Assert.False(Directory.Exists(Path.Combine(userRoot, "portables", manifest.Id)));
            Assert.True(File.Exists(Path.Combine(other, "keep.txt")));
            Assert.Single(shortcuts.Deleted);
        }
        finally
        {
            Cleanup(resources, userRoot);
        }
    }

    [Fact]
    public async Task Reinstall_ReplacesExistingFiles()
    {
        var (resources, userRoot, manifest, shortcuts) = await SetupAsync("tool.exe", "payload");
        try
        {
            var service = new PortableService(userRoot, shortcuts);
            await service.InstallAsync(manifest);

            var userFile = Path.Combine(userRoot, "portables", manifest.Id, "user-data.txt");
            File.WriteAllText(userFile, "data");

            var result = await service.ReinstallAsync(manifest);

            Assert.True(result.Success, result.Message);
            Assert.False(File.Exists(userFile));
            Assert.True(File.Exists(Path.Combine(userRoot, "portables", manifest.Id, "tool.exe")));
        }
        finally
        {
            Cleanup(resources, userRoot);
        }
    }

    [Fact]
    public async Task Install_WrongHash_DeploysNothing()
    {
        var (resources, userRoot, manifest, shortcuts) = await SetupAsync("tool.exe", "payload");
        try
        {
            manifest = new PortableManifest
            {
                Id = manifest.Id,
                Name = manifest.Name,
                Category = manifest.Category,
                ResourceDirectory = manifest.ResourceDirectory,
                Package = new ManifestFile
                {
                    File = "tool.exe",
                    Sha256 = new string('0', 64)
                },
                Executable = manifest.Executable,
                ShortcutName = manifest.ShortcutName
            };
            var service = new PortableService(userRoot, shortcuts);

            var result = await service.InstallAsync(manifest);

            Assert.False(result.Success);
            Assert.False(service.IsInstalled(manifest));
            Assert.Empty(shortcuts.Created);
        }
        finally
        {
            Cleanup(resources, userRoot);
        }
    }

    private static async Task<(
        string Resources,
        string UserRoot,
        PortableManifest Manifest,
        FakeShortcuts Shortcuts)> SetupAsync(string fileName, string content)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var resources = Path.Combine(Path.GetTempPath(), "skorski-portable-res-" + suffix);
        var packageDirectory = Path.Combine(resources, "portables", "test");
        Directory.CreateDirectory(packageDirectory);

        var packagePath = Path.Combine(packageDirectory, fileName);
        await File.WriteAllTextAsync(packagePath, content);

        var manifest = new PortableManifest
        {
            Id = "test-portable-" + suffix[..8],
            Name = "Test Portable",
            Category = "utilities",
            ResourceDirectory = packageDirectory,
            Package = new ManifestFile
            {
                File = fileName,
                Sha256 = Sha256Verifier.ComputeHex(packagePath)
            },
            Executable = fileName,
            ShortcutName = "Test Portable"
        };

        return (
            resources,
            Path.Combine(Path.GetTempPath(), "skorski-portable-user-" + suffix),
            manifest,
            new FakeShortcuts());
    }

    private static void Cleanup(string resources, string userRoot)
    {
        if (Directory.Exists(resources))
            Directory.Delete(resources, recursive: true);
        if (Directory.Exists(userRoot))
            Directory.Delete(userRoot, recursive: true);
    }

    private sealed class FakeShortcuts : IShortcutService
    {
        public List<(string Path, string Target)> Created { get; } = new();
        public List<string> Deleted { get; } = new();

        public void Create(string shortcutPath, string targetPath, string workingDirectory, string description) =>
            Created.Add((shortcutPath, targetPath));

        public void Delete(string shortcutPath) => Deleted.Add(shortcutPath);
    }
}
