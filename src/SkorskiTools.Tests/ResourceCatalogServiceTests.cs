using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.Tests;

public sealed class ResourceCatalogServiceTests
{
    [Fact]
    public async Task LoadAsync_RealCatalog_DiscoversAllResourcesWithoutErrors()
    {
        var result = await new ResourceCatalogService().LoadAsync(TestData.ResourcesDirectory);

        Assert.Equal(4, result.Applications.Count);
        Assert.Equal(2, result.Browsers.Count);
        Assert.Equal(2, result.Portables.Count);
        Assert.Single(result.Auxiliary);
        Assert.Equal(2, result.Scripts.Count);
        Assert.DoesNotContain(result.Issues, i => i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task LoadAsync_InvalidManifest_DoesNotBlockOtherResources()
    {
        var root = TestData.CreateTempResources();
        try
        {
            var hash = TestData.CreateResource(root, "installers/good", "setup.exe");
            TestData.WriteManifest(root, "installers/good", ValidApp("good", "setup.exe", hash));
            TestData.WriteManifest(root, "installers/bad", ValidApp("BAD ID", "setup.exe", new string('0', 64)));

            var result = await new ResourceCatalogService().LoadAsync(root);

            Assert.Single(result.Applications);
            Assert.Equal("good", result.Applications[0].Id);
            Assert.Contains(result.Issues, i => i.Severity == IssueSeverity.Error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_DuplicateId_KeepsFirstAndReportsIssue()
    {
        var root = TestData.CreateTempResources();
        try
        {
            var firstHash = TestData.CreateResource(root, "installers/first", "a.exe", "a");
            var secondHash = TestData.CreateResource(root, "installers/second", "b.exe", "b");
            TestData.WriteManifest(root, "installers/first", ValidApp("same-id", "a.exe", firstHash));
            TestData.WriteManifest(root, "installers/second", ValidApp("same-id", "b.exe", secondHash));

            var result = await new ResourceCatalogService().LoadAsync(root);

            Assert.Single(result.Applications);
            Assert.Contains(result.Issues, i => i.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_MissingDirectory_ReturnsErrorIssue()
    {
        var missing = Path.Combine(Path.GetTempPath(), "skorski-missing-" + Guid.NewGuid().ToString("N"));

        var result = await new ResourceCatalogService().LoadAsync(missing);

        Assert.Contains(result.Issues, i => i.Severity == IssueSeverity.Error);
    }

    private static string ValidApp(string id, string file, string sha) => $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "Test",
          "category": "utilities",
          "files": [ { "file": "{{file}}", "sha256": "{{sha}}" } ],
          "detection": { "type": "registry", "displayName": "Test" }
        }
        """;
}
