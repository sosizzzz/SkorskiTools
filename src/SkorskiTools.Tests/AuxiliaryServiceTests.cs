using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;
using SkorskiTools.Core.Services;

namespace SkorskiTools.Tests;

public sealed class AuxiliaryServiceTests
{
    [Fact]
    public async Task Copy_CopiesFilesVerbatim()
    {
        var (resources, userRoot, manifest) = CreateSetup();
        try
        {
            var service = new AuxiliaryService(userRoot);

            var result = await service.CopyAsync(manifest);
            var destination = Path.Combine(userRoot, "auxiliary", manifest.Id);

            Assert.True(result.Success, result.Message);
            Assert.True(service.AreCopied(manifest));
            Assert.Equal("payload one", File.ReadAllText(Path.Combine(destination, "one.txt")));
            Assert.Equal("payload two", File.ReadAllText(Path.Combine(destination, "two.txt")));
        }
        finally
        {
            Cleanup(resources, userRoot);
        }
    }

    [Fact]
    public async Task Copy_WrongHash_CopiesNothing()
    {
        var (resources, userRoot, manifest) = CreateSetup();
        try
        {
            manifest = new AuxiliaryManifest
            {
                Id = manifest.Id,
                Name = manifest.Name,
                ResourceDirectory = manifest.ResourceDirectory,
                Files = new[]
                {
                    new ManifestFile { File = "one.txt", Sha256 = new string('0', 64) }
                }
            };
            var service = new AuxiliaryService(userRoot);

            var result = await service.CopyAsync(manifest);

            Assert.False(result.Success);
            Assert.False(Directory.Exists(Path.Combine(userRoot, "auxiliary", manifest.Id)));
        }
        finally
        {
            Cleanup(resources, userRoot);
        }
    }

    [Fact]
    public async Task Copy_UsesItsOwnSubfolder()
    {
        var (resources, userRoot, manifest) = CreateSetup();
        try
        {
            var other = Path.Combine(userRoot, "auxiliary", "other-resource");
            Directory.CreateDirectory(other);
            File.WriteAllText(Path.Combine(other, "keep.txt"), "keep");

            var service = new AuxiliaryService(userRoot);
            await service.CopyAsync(manifest);

            Assert.True(File.Exists(Path.Combine(other, "keep.txt")));
            Assert.True(File.Exists(Path.Combine(userRoot, "auxiliary", manifest.Id, "one.txt")));
        }
        finally
        {
            Cleanup(resources, userRoot);
        }
    }

    private static (string Resources, string UserRoot, AuxiliaryManifest Manifest) CreateSetup()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var resources = Path.Combine(Path.GetTempPath(), "skorski-aux-res-" + suffix);
        Directory.CreateDirectory(resources);

        var first = Path.Combine(resources, "one.txt");
        var second = Path.Combine(resources, "two.txt");
        File.WriteAllText(first, "payload one");
        File.WriteAllText(second, "payload two");

        var manifest = new AuxiliaryManifest
        {
            Id = "test-aux-" + suffix[..8],
            Name = "Test Auxiliary",
            Category = "system",
            ResourceDirectory = resources,
            Files = new[]
            {
                new ManifestFile { File = "one.txt", Sha256 = Sha256Verifier.ComputeHex(first) },
                new ManifestFile { File = "two.txt", Sha256 = Sha256Verifier.ComputeHex(second) }
            }
        };

        return (resources, Path.Combine(Path.GetTempPath(), "skorski-aux-user-" + suffix), manifest);
    }

    private static void Cleanup(string resources, string userRoot)
    {
        if (Directory.Exists(resources))
            Directory.Delete(resources, recursive: true);
        if (Directory.Exists(userRoot))
            Directory.Delete(userRoot, recursive: true);
    }
}
