namespace SkorskiTools.Core.Models;

/// <summary>Result of an install, uninstall or reinstall operation.</summary>
public sealed record OperationResult(bool Success, string Message)
{
    public int? ExitCode { get; init; }

    public static OperationResult Ok(string message, int? exitCode = null) =>
        new(true, message) { ExitCode = exitCode };

    public static OperationResult Fail(string message, int? exitCode = null) =>
        new(false, message) { ExitCode = exitCode };
}
