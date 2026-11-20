using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.Tests;

public sealed class ManifestReaderTests
{
    private static readonly string[] Categories = ["utilities", "networking", "other"];

    [Fact]
    public void Read_ValidApplication_ReturnsManifest()
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "installers/test-app", "setup.exe");
        TestData.WriteManifest(root, "installers/test-app", AppJson(hash));

        var result = CreateReader().Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.NotNull(result.Manifest);
        var app = Assert.IsType<ApplicationManifest>(result.Manifest);
        Assert.Equal("test-app", app.Id);
        Assert.Equal(TargetArchitecture.X64, app.Files[0].Arch);
        Assert.Equal(DetectionType.Registry, app.Detection!.Type);
    }

    [Fact]
    public void Read_ValidPortable_ReturnsManifest()
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "portables/test-portable", "tool.exe");
        TestData.WriteManifest(root, "portables/test-portable", $$"""
            {
              "schemaVersion": 1,
              "id": "test-portable",
              "name": "Test Portable",
              "category": "utilities",
              "package": { "file": "tool.exe", "sha256": "{{hash}}" },
              "executable": "tool.exe",
              "shortcutName": "Test Portable"
            }
            """);

        var result = CreateReader().Read(Path.Combine(root, "portables", "test-portable", "manifest.json"),
            ManifestKind.Portable);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var portable = Assert.IsType<PortableManifest>(result.Manifest);
        Assert.Equal("tool.exe", portable.Executable);
    }

    [Theory]
    [InlineData("UPPERCASE")]
    [InlineData("has spaces")]
    [InlineData("invalid_id")]
    public void Read_InvalidId_IsRejected(string id)
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "installers/test-app", "setup.exe");
        TestData.WriteManifest(root, "installers/test-app", AppJson(hash, id: id));

        var result = CreateReader().Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_WrongSha256_IsRejected()
    {
        var root = TestData.CreateTempResources();
        TestData.CreateResource(root, "installers/test-app", "setup.exe");
        TestData.WriteManifest(root, "installers/test-app", AppJson(new string('0', 64)));

        var result = CreateReader().Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_EscapingFilePath_IsRejected()
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "installers/test-app", "setup.exe");
        TestData.WriteManifest(root, "installers/test-app", AppJson(hash, file: "../outside.exe"));

        var result = CreateReader().Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("safe relative path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_UnknownCategory_IsRejected()
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "installers/test-app", "setup.exe");
        TestData.WriteManifest(root, "installers/test-app", AppJson(hash, category: "missing"));

        var result = CreateReader().Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not defined", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_WrongSchemaVersion_IsRejected()
    {
        var root = TestData.CreateTempResources();
        var hash = TestData.CreateResource(root, "installers/test-app", "setup.exe");
        TestData.WriteManifest(root, "installers/test-app", AppJson(hash, schemaVersion: 2));

        var result = CreateReader().Read(Path.Combine(root, "installers", "test-app", "manifest.json"),
            ManifestKind.Application);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Read_MissingReferencedFile_IsRejected()
    {
        var root = TestData.CreateTempResources();
        var dir = Path.Combine(root, "installers", "test-app");
        Directory.CreateDirectory(dir);
        TestData.WriteManifest(root, "installers/test-app", AppJson(new string('0', 64)));

        var result = CreateReader().Read(Path.Combine(dir, "manifest.json"), ManifestKind.Application);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }

    private static ManifestReader CreateReader() => new(TestData.SchemasDirectory, Categories, verifyFileHashes: true);

    private static string AppJson(
        string sha,
        string id = "test-app",
        string file = "setup.exe",
        string category = "utilities",
        int schemaVersion = 1) => $$"""
        {
          "schemaVersion": {{schemaVersion}},
          "id": "{{id}}",
          "name": "Test App",
          "category": "{{category}}",
          "files": [ { "file": "{{file}}", "sha256": "{{sha}}", "arch": "x64" } ],
          "detection": { "type": "registry", "displayName": "Test App" },
          "installation": { "arguments": [] },
          "uninstallation": { "mode": "registered-uninstaller" }
        }
        """;
}
