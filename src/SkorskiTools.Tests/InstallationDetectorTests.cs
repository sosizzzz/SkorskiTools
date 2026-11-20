using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.Tests;

public sealed class InstallationDetectorTests
{
    [Fact]
    public async Task Registry_ExactMatch_ReturnsVersionAndEntry()
    {
        var entry = Entry("7-Zip 24.09", "24.09");
        var detector = new InstallationDetector(new FakeProvider(entry));
        var manifest = AppManifest(new DetectionConfig
        {
            Type = DetectionType.Registry,
            DisplayName = "7-Zip 24.09",
            MatchMode = NameMatchMode.Exact
        });

        var result = await detector.DetectAsync(manifest);

        Assert.True(result.IsInstalled);
        Assert.Equal("24.09", result.Version);
        Assert.Equal("Registry", result.Source);
        Assert.Same(entry, result.Entry);
    }

    [Fact]
    public async Task Registry_StartsWith_MatchesLocalizedName()
    {
        var detector = new InstallationDetector(new FakeProvider(Entry("Firefox 141.0 (x64 es-ES)", "141.0")));
        var manifest = AppManifest(new DetectionConfig
        {
            Type = DetectionType.Registry,
            DisplayName = "Firefox",
            MatchMode = NameMatchMode.StartsWith
        });

        var result = await detector.DetectAsync(manifest);

        Assert.True(result.IsInstalled);
        Assert.Equal("141.0", result.Version);
    }

    [Fact]
    public async Task Registry_ExactMode_DoesNotMatchLongerName()
    {
        var detector = new InstallationDetector(new FakeProvider(Entry("Firefox 141.0", "141.0")));
        var manifest = AppManifest(new DetectionConfig
        {
            Type = DetectionType.Registry,
            DisplayName = "Firefox",
            MatchMode = NameMatchMode.Exact
        });

        var result = await detector.DetectAsync(manifest);

        Assert.False(result.IsInstalled);
    }

    [Fact]
    public async Task MsiProductCode_MatchesRegistryKeyName()
    {
        const string code = "{12345678-1234-1234-1234-123456789012}";
        var detector = new InstallationDetector(new FakeProvider(
            Entry("Product", "1.2.3", keyName: code)));
        var manifest = AppManifest(new DetectionConfig
        {
            Type = DetectionType.MsiProductCode,
            ProductCode = code
        });

        var result = await detector.DetectAsync(manifest);

        Assert.True(result.IsInstalled);
        Assert.Equal("MSI product code", result.Source);
    }

    [Fact]
    public async Task NoMatch_ReturnsNotInstalled()
    {
        var detector = new InstallationDetector(new FakeProvider(Entry("Other app", "1.0")));
        var manifest = AppManifest(new DetectionConfig
        {
            Type = DetectionType.Registry,
            DisplayName = "Missing"
        });

        var result = await detector.DetectAsync(manifest);

        Assert.False(result.IsInstalled);
        Assert.Null(result.Entry);
    }

    [Fact]
    public async Task FileExists_DetectsFile()
    {
        var file = Path.Combine(Path.GetTempPath(), "skorski-detect-" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            File.WriteAllText(file, "test");
            var detector = new InstallationDetector(new FakeProvider());
            var manifest = AppManifest(new DetectionConfig
            {
                Type = DetectionType.FileExists,
                Path = file
            });

            var result = await detector.DetectAsync(manifest);

            Assert.True(result.IsInstalled);
            Assert.Equal("File exists", result.Source);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Path_RequiresDirectoryAndExecutable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "skorski-detect-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "tool.exe"), "test");

            var detector = new InstallationDetector(new FakeProvider());
            var manifest = AppManifest(new DetectionConfig
            {
                Type = DetectionType.Path,
                Path = directory,
                Executable = "tool.exe"
            });

            var result = await detector.DetectAsync(manifest);

            Assert.True(result.IsInstalled);
            Assert.Equal("Path", result.Source);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SplitCommand_SeparatesExecutableAndArguments()
    {
        var (exe, arguments) = ApplicationInstaller.SplitCommand(
            @"""C:\Program Files\App\uninstall.exe"" /S /D=""C:\App""");

        Assert.Equal(@"C:\Program Files\App\uninstall.exe", exe);
        Assert.Equal(@"/S /D=""C:\App""", arguments);
    }

    [Fact]
    public void ParseArguments_KeepsQuotedValuesTogether()
    {
        var arguments = ApplicationInstaller.ParseArguments("/S /D=\"C:\\Program Files\\App\"");

        Assert.Equal(new[] { "/S", "/D=C:\\Program Files\\App" }, arguments);
    }

    private static ApplicationManifest AppManifest(DetectionConfig detection) => new()
    {
        SchemaVersion = 1,
        Id = "test-app",
        Name = "Test App",
        Detection = detection
    };

    private static UninstallEntry Entry(
        string displayName,
        string version,
        string keyName = "key",
        string uninstallString = @"C:\uninstall.exe") =>
        new(keyName, displayName, version, uninstallString, string.Empty, "Owner");

    private sealed class FakeProvider : IUninstallEntryProvider
    {
        private readonly IReadOnlyList<UninstallEntry> _entries;

        public FakeProvider(params UninstallEntry[] entries) => _entries = entries;

        public IReadOnlyList<UninstallEntry> GetEntries() => _entries;
    }
}
