using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Services;

namespace SkorskiTools.Tests;

public sealed class UserFoldersServiceTests
{
    [Fact]
    public void EnsureCreated_CreatesAllConfiguredFolders()
    {
        var root = CreateTempRoot();

        try
        {
            var service = new UserFoldersService(root);
            var result = service.EnsureCreated();

            Assert.Equal(root, result.RootPath);
            Assert.Equal(AppPaths.UserFolderNames.Count, result.CreatedFolders.Count);
            Assert.Empty(result.ExistingFolders);

            foreach (var folder in AppPaths.UserFolderNames)
                Assert.True(Directory.Exists(Path.Combine(root, folder)), $"Missing folder: {folder}");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnsureCreated_IsIdempotent()
    {
        var root = CreateTempRoot();

        try
        {
            var service = new UserFoldersService(root);
            service.EnsureCreated();

            var second = service.EnsureCreated();

            Assert.Empty(second.CreatedFolders);
            Assert.Equal(AppPaths.UserFolderNames.Count, second.ExistingFolders.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), "skorski-tools-folders-" + Guid.NewGuid().ToString("N"));
}
