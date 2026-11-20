namespace SkorskiTools.Core.Models;

/// <summary>Result of an install, uninstall or reinstall operation.</summary>
public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);

    public static OperationResult Fail(string message) => new(false, message);
}
