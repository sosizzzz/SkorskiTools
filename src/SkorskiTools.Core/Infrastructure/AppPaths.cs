namespace SkorskiTools.Core.Infrastructure;

/// <summary>
/// Centralizes the well-known file system paths used by Skorski Tools.
/// Paths are resolved from the environment, never from the process working directory.
/// </summary>
public static class AppPaths
{
    public const string ApplicationName = "Skorski Tools";
    public const string UserRootFolderName = "Skorski-Tools";
    public const string ResourcesFolderName = "resources";

    /// <summary>Folders created in the user profile on first launch.</summary>
    public static IReadOnlyList<string> UserFolderNames { get; } =
        new[] { "auxiliary", "installers", "portables", "browsers", "scripts" };

    public static string GetUserRoot(string? userProfilePath = null)
    {
        var profile = userProfilePath
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return Path.Combine(profile, UserRootFolderName);
    }

    public static string GetUserFolder(string folderName, string? userProfilePath = null)
    {
        if (!UserFolderNames.Contains(folderName, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown user folder: '{folderName}'.", nameof(folderName));

        return Path.Combine(GetUserRoot(userProfilePath), folderName);
    }

    public static string GetLogsDirectory(string? localAppDataPath = null)
    {
        var localAppData = localAppDataPath
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(localAppData, ApplicationName, "logs");
    }

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for the resources directory.
    /// This keeps resource discovery independent of the current working directory.
    /// </summary>
    public static string? FindResourcesDirectory(string? startDirectory = null)
    {
        var directory = new DirectoryInfo(startDirectory ?? AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ResourcesFolderName);
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        return null;
    }
}
