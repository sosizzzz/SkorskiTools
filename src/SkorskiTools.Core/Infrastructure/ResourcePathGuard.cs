namespace SkorskiTools.Core.Infrastructure;

/// <summary>
/// Rejects manifest paths that could escape the resource directory.
/// </summary>
public static class ResourcePathGuard
{
    public static bool IsSafeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        // Absolute paths (drive letters, UNC and rooted paths) are never allowed.
        if (Path.IsPathRooted(relativePath))
            return false;

        var segments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            return false;

        foreach (var segment in segments)
        {
            if (segment is ".." or ".")
                return false;

            if (segment.IndexOf(':') >= 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves a manifest-relative path and verifies that it stays inside its resource directory.
    /// Returns null when the path is unsafe.
    /// </summary>
    public static string? Resolve(string resourceDirectory, string? relativePath)
    {
        if (!IsSafeRelativePath(relativePath))
            return null;

        var root = Path.GetFullPath(resourceDirectory);
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath!));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? candidate
            : null;
    }
}
