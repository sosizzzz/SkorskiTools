namespace SkorskiTools.Core.Models;

/// <summary>Describes how an application installation is detected.</summary>
public sealed class DetectionConfig
{
    public DetectionType Type { get; init; } = DetectionType.Registry;
    public string DisplayName { get; init; } = string.Empty;
    public NameMatchMode MatchMode { get; init; } = NameMatchMode.Exact;
    public string ProductCode { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Executable { get; init; } = string.Empty;
}
