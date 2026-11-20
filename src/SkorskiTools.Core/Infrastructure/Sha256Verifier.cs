using System.Security.Cryptography;

namespace SkorskiTools.Core.Infrastructure;

/// <summary>SHA-256 helpers used before any resource file is used.</summary>
public static class Sha256Verifier
{
    public static string ComputeHex(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static bool Matches(string filePath, string expectedHash) =>
        string.Equals(
            ComputeHex(filePath),
            expectedHash.Trim().ToLowerInvariant(),
            StringComparison.Ordinal);

    public static bool IsValidHashFormat(string? hash) =>
        hash is not null
        && hash.Length == 64
        && hash.All(Uri.IsHexDigit);
}
