using SkorskiTools.Core.Infrastructure;

namespace SkorskiTools.Tests;

public sealed class ResourcePathGuardTests
{
    [Theory]
    [InlineData("file.exe")]
    [InlineData("sub/file.exe")]
    [InlineData("sub\\file.exe")]
    public void IsSafeRelativePath_AcceptsNormalRelativePaths(string path) =>
        Assert.True(ResourcePathGuard.IsSafeRelativePath(path));

    [Theory]
    [InlineData("../file.exe")]
    [InlineData("sub/../../file.exe")]
    [InlineData(@"C:\Windows\system32\cmd.exe")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    public void IsSafeRelativePath_RejectsEscapingPaths(string path) =>
        Assert.False(ResourcePathGuard.IsSafeRelativePath(path));

    [Fact]
    public void Resolve_RejectsPathOutsideResourceDirectory()
    {
        var result = ResourcePathGuard.Resolve(@"C:\Resources\app", @"..\other\file.exe");

        Assert.Null(result);
    }
}
