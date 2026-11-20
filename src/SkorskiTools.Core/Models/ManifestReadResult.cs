namespace SkorskiTools.Core.Models;

/// <summary>Result of validating and deserializing a single manifest.</summary>
public sealed class ManifestReadResult
{
    public bool IsValid => Errors.Count == 0;

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public ManifestBase? Manifest { get; init; }

    public static ManifestReadResult Success(ManifestBase manifest) =>
        new() { Manifest = manifest };

    public static ManifestReadResult Failure(IEnumerable<string> errors) =>
        new() { Errors = errors.ToArray() };
}
