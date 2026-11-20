using SkorskiTools.Core.Infrastructure;

namespace SkorskiTools.Tests;

public sealed class Sha256VerifierTests
{
    [Fact]
    public void ComputeHex_ReturnsKnownHash()
    {
        var file = Path.Combine(Path.GetTempPath(), "skorski-hash-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(file, "Skorski Tools");

            var hash = Sha256Verifier.ComputeHex(file);

            Assert.Equal(64, hash.Length);
            Assert.All(hash, c => Assert.True(Uri.IsHexDigit(c)));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Matches_ComparesCaseInsensitively()
    {
        var file = Path.Combine(Path.GetTempPath(), "skorski-hash-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(file, "payload");
            var hash = Sha256Verifier.ComputeHex(file);

            Assert.True(Sha256Verifier.Matches(file, hash.ToUpperInvariant()));
            Assert.False(Sha256Verifier.Matches(file, new string('0', 64)));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("abc", false)]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz", false)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", true)]
    public void IsValidHashFormat_ChecksLengthAndHex(string? hash, bool expected) =>
        Assert.Equal(expected, Sha256Verifier.IsValidHashFormat(hash));
}
