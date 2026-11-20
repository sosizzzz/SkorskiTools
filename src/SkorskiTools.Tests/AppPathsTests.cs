using SkorskiTools.Core.Infrastructure;

namespace SkorskiTools.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void GetUserRoot_CombinesProfileWithFolderName()
    {
        var root = AppPaths.GetUserRoot(@"C:\Users\TestUser");

        Assert.Equal(Path.Combine(@"C:\Users\TestUser", "Skorski-Tools"), root);
    }

    [Fact]
    public void GetUserFolder_RejectsUnknownFolder()
    {
        Assert.Throws<ArgumentException>(() =>
            AppPaths.GetUserFolder("unknown", @"C:\Users\TestUser"));
    }

    [Fact]
    public void GetLogsDirectory_UsesLocalAppData()
    {
        var logs = AppPaths.GetLogsDirectory(@"C:\Users\TestUser\AppData\Local");

        Assert.Equal(
            Path.Combine(@"C:\Users\TestUser\AppData\Local", "Skorski Tools", "logs"),
            logs);
    }

    [Fact]
    public void FindResourcesDirectory_WalksUpParents()
    {
        var temp = Path.Combine(
            Path.GetTempPath(), "skorski-tools-paths-" + Guid.NewGuid().ToString("N"));
        var resources = Path.Combine(temp, "resources");
        var nested = Path.Combine(temp, "src", "App", "bin", "Debug");

        try
        {
            Directory.CreateDirectory(resources);
            Directory.CreateDirectory(nested);

            var found = AppPaths.FindResourcesDirectory(nested);

            Assert.Equal(resources, found);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
