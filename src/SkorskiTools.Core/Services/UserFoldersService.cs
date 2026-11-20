using Serilog;
using SkorskiTools.Core.Infrastructure;

namespace SkorskiTools.Core.Services;

/// <summary>Result of preparing the user folders on startup.</summary>
public sealed record UserFoldersResult(
    string RootPath,
    IReadOnlyList<string> CreatedFolders,
    IReadOnlyList<string> ExistingFolders);

/// <summary>Ensures the user-writable folders exist.</summary>
public interface IUserFoldersService
{
    UserFoldersResult EnsureCreated();
}

/// <summary>
/// Creates the reserved user folders on first launch. Existing folders are never
/// modified or deleted. A custom root can be injected for testing.
/// </summary>
public sealed class UserFoldersService : IUserFoldersService
{
    private readonly string _rootPath;
    private readonly ILogger _logger;

    public UserFoldersService(string? rootPath = null, ILogger? logger = null)
    {
        _rootPath = rootPath ?? AppPaths.GetUserRoot();
        _logger = logger ?? Log.Logger;
    }

    public UserFoldersResult EnsureCreated()
    {
        var created = new List<string>();
        var existing = new List<string>();

        Directory.CreateDirectory(_rootPath);

        foreach (var folder in AppPaths.UserFolderNames)
        {
            var fullPath = Path.Combine(_rootPath, folder);

            if (Directory.Exists(fullPath))
            {
                existing.Add(fullPath);
                continue;
            }

            Directory.CreateDirectory(fullPath);
            created.Add(fullPath);
            _logger.Information("Created user folder {Folder}", fullPath);
        }

        return new UserFoldersResult(_rootPath, created, existing);
    }
}
