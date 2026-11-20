using System.Reflection;

namespace SkorskiTools.App.Infrastructure;

/// <summary>Exposes the assembly version for the UI and logs.</summary>
public static class AppVersion
{
    public static string Current { get; } =
        typeof(AppVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
