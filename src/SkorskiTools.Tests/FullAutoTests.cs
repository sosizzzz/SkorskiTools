using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.Tests;

public sealed class FullAutoTests
{
    [Fact]
    public void ManifestReader_ValidFullAuto_IsParsedWithDefaults()
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "installers/test-app", "setup.exe");
        TestData.WriteManifest(root, "installers/test-app", AppJson(hash, """
            ,"fullAuto": { "enabled": true, "installArguments": ["/S"] }
            """));

        var reader = new ManifestReader(TestData.SchemasDirectory, new[] { "utilities" });
        var result = reader.Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var manifest = Assert.IsType<ApplicationManifest>(result.Manifest);
        Assert.True(manifest.FullAuto!.Enabled);
        Assert.Equal(new[] { "/S" }, manifest.FullAuto.InstallArguments);
        Assert.Equal(new[] { 0 }, manifest.FullAuto.ExpectedExitCodes);
        Assert.Equal(new[] { 3010, 1641 }, manifest.FullAuto.RestartExitCodes);
        Assert.True(manifest.FullAuto.Uninstall.UseQuietUninstallString);
    }

    [Fact]
    public void ManifestReader_EnabledWithoutSilentArguments_IsRejected()
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "installers/test-app", "setup.exe");
        TestData.WriteManifest(root, "installers/test-app", AppJson(hash, """
            ,"fullAuto": { "enabled": true }
            """));

        var reader = new ManifestReader(TestData.SchemasDirectory, new[] { "utilities" });
        var result = reader.Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("silent switches", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ManifestReader_ParsesMachineScope()
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "installers/test-app", "setup.exe");
        TestData.WriteManifest(root, "installers/test-app", AppJson(hash, """
            ,"fullAuto": {
              "enabled": true,
              "scope": "machine",
              "installArguments": ["/VERYSILENT"]
            }
            """));

        var reader = new ManifestReader(TestData.SchemasDirectory, new[] { "utilities" });
        var result = reader.Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var manifest = Assert.IsType<ApplicationManifest>(result.Manifest);
        Assert.Equal(InstallationScope.Machine, manifest.FullAuto!.Scope);
    }

    [Fact]
    public void ManifestReader_UnsupportedWithoutReason_IsRejected()
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "installers/test-app", "setup.exe");
        TestData.WriteManifest(root, "installers/test-app", AppJson(hash, """
            ,"fullAuto": { "enabled": false, "supported": false }
            """));

        var reader = new ManifestReader(TestData.SchemasDirectory, new[] { "utilities" });
        var result = reader.Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unsupportedReason", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ManifestReader_ConfigurationFileIsHashVerified()
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "installers/test-app", "setup.exe");
        var config = Path.Combine(root, "installers", "test-app", "configuration.xml");
        File.WriteAllText(config, "<Configuration />");
        var configHash = Sha256Verifier.ComputeHex(config);

        TestData.WriteManifest(root, "installers/test-app", AppJson(hash, $$"""
            ,"fullAuto": {
              "enabled": true,
              "installArguments": ["/configure", "configuration.xml"],
              "configurationFile": "configuration.xml",
              "configurationSha256": "{{configHash}}"
            }
            """));

        var reader = new ManifestReader(TestData.SchemasDirectory, new[] { "utilities" });
        var result = reader.Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var manifest = Assert.IsType<ApplicationManifest>(result.Manifest);
        Assert.Equal("configuration.xml", manifest.FullAuto!.ConfigurationFile);
    }

    [Fact]
    public async Task BuildPlan_UsesDetectionAndSkipsIneligibleApps()
    {
        var eligible = Manifest("eligible", true);
        var installed = Manifest("installed", true);
        var ineligible = Manifest("ineligible", false);
        var detector = new FakeDetector(installed.Id);
        var service = new FullAutoService(detector, new FakeInstaller());

        var plan = await service.BuildPlanAsync(new[] { eligible, installed, ineligible });

        Assert.Equal(2, plan.Count);
        Assert.Equal(FullAutoAction.Install, plan.Items[0].Action);
        Assert.Equal(FullAutoAction.Reinstall, plan.Items[1].Action);
        Assert.Single(plan.Skipped);
    }

    [Fact]
    public async Task Execute_ClassifiesExitCodes()
    {
        var app = Manifest("app", true);
        var plan = new FullAutoPlan
        {
            Items = new[]
            {
                new FullAutoPlanItem
                {
                    Manifest = app,
                    Detection = DetectionResult.NotInstalled,
                    Action = FullAutoAction.Install
                }
            }
        };
        var installer = new FakeInstaller { InstallResult = OperationResult.Ok("done", 3010) };
        var service = new FullAutoService(new FakeDetector(), installer);

        var result = await service.ExecuteAsync(plan);

        Assert.Equal(0, result.Succeeded);
        Assert.Equal(1, result.RequiringRestart);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task Execute_FailedQuietUninstall_SkipsInstallation()
    {
        var app = Manifest("app", true);
        var plan = new FullAutoPlan
        {
            Items = new[]
            {
                new FullAutoPlanItem
                {
                    Manifest = app,
                    Detection = DetectionResult.Installed("1.0", "Registry"),
                    Action = FullAutoAction.Reinstall
                }
            }
        };
        var installer = new FakeInstaller
        {
            QuietUninstallResult = OperationResult.Fail("registry entry missing")
        };
        var service = new FullAutoService(new FakeDetector(), installer);

        var result = await service.ExecuteAsync(plan);

        Assert.Equal(1, result.Failed);
        Assert.Equal(0, installer.InstallCalls);
        Assert.Contains(result.Messages, m => m.Contains("uninstall failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildPlan_IncludesSelectedPortables()
    {
        var portable = new PortableManifest
        {
            Id = "portable-app",
            Name = "Portable App",
            Package = new ManifestFile { File = "tool.exe", Sha256 = new string('0', 64) },
            Executable = "tool.exe"
        };
        var portables = new FakePortableService { Installed = true };
        var service = new FullAutoService(new FakeDetector(), new FakeInstaller(), portables);

        var plan = await service.BuildPlanAsync(new[] { portable });

        Assert.Single(plan.Items);
        Assert.Equal(FullAutoAction.Reinstall, plan.Items[0].Action);
    }

    [Fact]
    public void SettingsService_PersistsFullAutoFlag()
    {
        var root = Path.Combine(Path.GetTempPath(), "skorski-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new SettingsService(root);
            Assert.False(service.Current.FullAutoEnabled);

            var changes = 0;
            service.SettingsChanged += (_, _) => changes++;
            service.SetFullAutoEnabled(true);

            Assert.True(service.Current.FullAutoEnabled);
            Assert.Equal(1, changes);
            Assert.True(new SettingsService(root).Current.FullAutoEnabled);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildInstallCommand_UsesMsiexecForMsiPackages()
    {
        var path = @"C:\Installers\LibreOffice x64.msi";

        var result = ApplicationInstaller.BuildInstallCommand(path, new[] { "/qn", "/norestart" });

        Assert.Equal("msiexec.exe", result.Executable);
        Assert.Equal(new[] { "/i", path, "/qn", "/norestart" }, result.Arguments);
    }

    [Fact]
    public void BuildInstallCommand_UsesExecutableDirectlyForExe()
    {
        var path = @"C:\Installers\vlc.exe";

        var result = ApplicationInstaller.BuildInstallCommand(path, new[] { "/S" });

        Assert.Equal(path, result.Executable);
        Assert.Equal(new[] { "/S" }, result.Arguments);
    }

    [Fact]
    public void OperationHistoryService_PersistsAndClearsEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), "skorski-history-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new OperationHistoryService(root);
            Assert.Empty(service.GetEntries());

            service.Add(new OperationHistoryEntry
            {
                Timestamp = DateTimeOffset.Now,
                ResourceId = "test",
                ResourceName = "Test App",
                Action = "Install",
                Success = true,
                Message = "done"
            });

            Assert.Single(service.GetEntries());
            Assert.Equal("Test App", new OperationHistoryService(root).GetEntries()[0].ResourceName);

            service.Clear();
            Assert.Empty(service.GetEntries());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SettingsService_PersistsDeveloperModeFlag()
    {
        var root = Path.Combine(Path.GetTempPath(), "skorski-devmode-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new SettingsService(root);
            Assert.False(service.Current.DeveloperModeEnabled);

            service.SetDeveloperModeEnabled(true);

            Assert.True(service.Current.DeveloperModeEnabled);
            Assert.True(new SettingsService(root).Current.DeveloperModeEnabled);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ManifestReader_ParsesExitCodeHints()
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "installers/test-app", "setup.exe");
        TestData.WriteManifest(root, "installers/test-app", AppJson(hash, """
            ,"fullAuto": {
              "enabled": true,
              "installArguments": ["/S"],
              "exitCodeHints": { "57": "Check the network." }
            }
            """));

        var reader = new ManifestReader(TestData.SchemasDirectory, new[] { "utilities" });
        var result = reader.Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var manifest = Assert.IsType<ApplicationManifest>(result.Manifest);
        Assert.Equal("Check the network.", manifest.FullAuto!.ExitCodeHints[57]);
    }

    [Fact]
    public async Task Execute_UnexpectedExitCode_UsesManifestHint()
    {
        var app = new ApplicationManifest
        {
            SchemaVersion = 1,
            Id = "chrome",
            Name = "Chrome",
            FullAuto = new FullAutoConfig
            {
                Enabled = true,
                InstallArguments = new[] { "/silent", "/install" },
                ExitCodeHints = new Dictionary<int, string> { [57] = "Check the network." }
            }
        };
        var plan = Plan(app);
        var installer = new FakeInstaller { InstallResult = OperationResult.Ok("started", 57) };
        var service = new FullAutoService(new FakeDetector(), installer);

        var result = await service.ExecuteAsync(plan);

        Assert.Equal(1, result.Failed);
        Assert.Contains(result.Messages,
            m => m.Contains("57", StringComparison.Ordinal)
                 && m.Contains("Check the network.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Execute_ZeroExitButNotDetected_IsFailure()
    {
        var app = Manifest("app", true);
        var installer = new FakeInstaller { InstallResult = OperationResult.Ok("done", 0) };
        var detector = new FakeDetector(); // reports nothing installed
        var service = new FullAutoService(detector, installer);

        var result = await service.ExecuteAsync(Plan(app));

        Assert.Equal(1, result.Failed);
        Assert.Contains(result.Messages, m => m.Contains("not detected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Execute_RecordsRealExitCodeInHistory()
    {
        var app = Manifest("app", true);
        var history = new FakeHistory();
        var installer = new FakeInstaller { InstallResult = OperationResult.Ok("done", 3010) };
        var detector = new FakeDetector(app.Id); // installed, so verification passes
        var service = new FullAutoService(detector, installer, history: history);

        await service.ExecuteAsync(Plan(app));

        var entry = Assert.Single(history.Entries);
        Assert.Equal(3010, entry.ExitCode);
        Assert.False(entry.Success); // restart required is not a clean success
    }

    private static FullAutoPlan Plan(ApplicationManifest app) => new()
    {
        Items = new[]
        {
            new FullAutoPlanItem
            {
                Manifest = app,
                Detection = DetectionResult.NotInstalled,
                Action = FullAutoAction.Install
            }
        }
    };

    private static ApplicationManifest Manifest(string id, bool enabled) => new()
    {
        SchemaVersion = 1,
        Id = id,
        Name = id,
        FullAuto = new FullAutoConfig
        {
            Enabled = enabled,
            InstallArguments = enabled ? new[] { "/S" } : Array.Empty<string>()
        }
    };

    private static string AppJson(string sha, string extra) => $$"""
        {
          "schemaVersion": 1,
          "id": "test-app",
          "name": "Test App",
          "category": "utilities",
          "files": [ { "file": "setup.exe", "sha256": "{{sha}}" } ],
          "detection": { "type": "registry", "displayName": "Test App" }
          {{extra}}
        }
        """;

    private sealed class FakeDetector : IInstallationDetector
    {
        private readonly string _installedId;

        public FakeDetector(string installedId = "") => _installedId = installedId;

        public Task<DetectionResult> DetectAsync(
            ApplicationManifest manifest,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(manifest.Id == _installedId
                ? DetectionResult.Installed("1.0", "Registry")
                : DetectionResult.NotInstalled);
    }

    private sealed class FakePortableService : IPortableService
    {
        public bool Installed { get; set; }

        public bool IsInstalled(PortableManifest manifest) => Installed;

        public Task<OperationResult> InstallAsync(
            PortableManifest manifest, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Ok("deployed"));

        public Task<OperationResult> ReinstallAsync(
            PortableManifest manifest, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Ok("replaced"));

        public Task<OperationResult> UninstallAsync(
            PortableManifest manifest, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Ok("removed"));
    }

    private sealed class FakeInstaller : IApplicationInstaller
    {
        public OperationResult InstallResult { get; set; } = OperationResult.Ok("done", 0);
        public OperationResult QuietUninstallResult { get; set; } = OperationResult.Ok("removed", 0);
        public int InstallCalls { get; private set; }

        public Task<OperationResult> InstallAsync(
            ApplicationManifest manifest, CancellationToken cancellationToken = default) =>
            Task.FromResult(InstallResult);

        public Task<OperationResult> UninstallAsync(
            ApplicationManifest manifest,
            DetectionResult detection,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Ok("removed"));

        public Task<OperationResult> InstallAutomatedAsync(
            ApplicationManifest manifest, CancellationToken cancellationToken = default)
        {
            InstallCalls++;
            return Task.FromResult(InstallResult);
        }

        public Task<OperationResult> UninstallQuietAsync(
            ApplicationManifest manifest,
            DetectionResult detection,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(QuietUninstallResult);
    }

    private sealed class FakeHistory : IOperationHistoryService
    {
        public List<OperationHistoryEntry> Entries { get; } = new();

        public event EventHandler? HistoryChanged;

        public IReadOnlyList<OperationHistoryEntry> GetEntries() => Entries;

        public void Add(OperationHistoryEntry entry)
        {
            Entries.Add(entry);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            Entries.Clear();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
