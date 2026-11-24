using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.Tests;

/// <summary>
/// End-to-end checks of the manual install path: a real process is started, its exit
/// code is captured and the manifest hint is surfaced to the user.
/// </summary>
public sealed class ApplicationInstallerTests
{
    [Fact]
    public async Task Install_ManualFailure_ReportsRealCodeAndManifestHint()
    {
        var root = Path.Combine(Path.GetTempPath(), "skorski-installer-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var setup = Path.Combine(root, "setup.cmd");
            File.WriteAllText(setup, "@exit /b 57" + Environment.NewLine);

            var manifest = new ApplicationManifest
            {
                SchemaVersion = 1,
                Id = "test-app",
                Name = "Test App",
                ResourceDirectory = root,
                Files = new[]
                {
                    new ManifestFile { File = "setup.cmd", Sha256 = Sha256Verifier.ComputeHex(setup) }
                },
                FullAuto = new FullAutoConfig
                {
                    Enabled = true,
                    InstallArguments = new[] { "/silent" },
                    ExitCodeHints = new Dictionary<int, string> { [57] = "Check the network." }
                }
            };

            var result = await new ApplicationInstaller().InstallAsync(manifest);

            Assert.False(result.Success);
            Assert.Equal(57, result.ExitCode);
            Assert.Contains("57", result.Message, StringComparison.Ordinal);
            Assert.Contains("Check the network.", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Install_ManualSuccess_IsReportedAsSuccess()
    {
        var root = Path.Combine(Path.GetTempPath(), "skorski-installer-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var setup = Path.Combine(root, "setup.cmd");
            File.WriteAllText(setup, "@exit /b 0" + Environment.NewLine);

            var manifest = new ApplicationManifest
            {
                SchemaVersion = 1,
                Id = "test-app",
                Name = "Test App",
                ResourceDirectory = root,
                Files = new[]
                {
                    new ManifestFile { File = "setup.cmd", Sha256 = Sha256Verifier.ComputeHex(setup) }
                }
            };

            var result = await new ApplicationInstaller().InstallAsync(manifest);

            Assert.True(result.Success);
            Assert.Equal(0, result.ExitCode);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
