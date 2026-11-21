namespace SkorskiTools.Core.Models;

/// <summary>A record of an installation-related action shown in the UI history.</summary>
public sealed class OperationHistoryEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public string ResourceId { get; init; } = string.Empty;
    public string ResourceName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int? ExitCode { get; init; }
}
