using SkorskiTools.Core.Infrastructure;

namespace SkorskiTools.Tests;

internal static class TestData
{
    public static string RepoRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SkorskiTools.sln")))
                directory = directory.Parent;

            return directory?.FullName
                   ?? throw new InvalidOperationException("Could not locate the repository root.");
        }
    }

    public static string ResourcesDirectory => Path.Combine(RepoRoot, AppPaths.ResourcesFolderName);

    public static string SchemasDirectory => Path.Combine(ResourcesDirectory, AppPaths.SchemasFolderName);

    public static string CreateTempResources()
    {
        var root = Path.Combine(
            Path.GetTempPath(), "skorski-tools-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var schemas = Path.Combine(root, "schemas");
        DirectoryExtensions.Copy(SchemasDirectory, schemas, recursive: true);

        File.WriteAllText(
            Path.Combine(root, "categories.json"),
            """
            { "categories": [ { "id": "utilities", "name": "Utilities" } ] }
            """);

        return root;
    }

    public static string CreateResource(
        string resourcesRoot,
        string folder,
        string fileName,
        string fileContent = "payload")
    {
        var directory = Path.Combine(resourcesRoot, folder);
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, fileName);
        File.WriteAllText(filePath, fileContent);
        return Sha256Verifier.ComputeHex(filePath);
    }

    public static void WriteManifest(string resourcesRoot, string folder, string json)
    {
        var directory = Path.Combine(resourcesRoot, folder);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "manifest.json"), json);
    }
}

internal static class DirectoryExtensions
{
    public static void Copy(string source, string destination, bool recursive)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

        if (!recursive)
            return;

        foreach (var directory in Directory.GetDirectories(source))
            Copy(directory, Path.Combine(destination, Path.GetFileName(directory)), recursive: true);
    }
}
