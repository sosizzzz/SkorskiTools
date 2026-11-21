using System.Text.Json;
using Serilog;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;

namespace SkorskiTools.Core.Services;

/// <summary>Loads and saves the small set of user preferences.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    event EventHandler? SettingsChanged;

    void SetFullAutoEnabled(bool enabled);
}

/// <summary>Persists settings as JSON in %USERPROFILE%\Skorski-Tools\settings.json.</summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly ILogger _logger;

    public AppSettings Current { get; private set; }

    public event EventHandler? SettingsChanged;

    public SettingsService(string? userRoot = null, ILogger? logger = null)
    {
        _path = Path.Combine(userRoot ?? AppPaths.GetUserRoot(), "settings.json");
        _logger = logger ?? Log.Logger;
        Current = Load();
    }

    public void SetFullAutoEnabled(bool enabled)
    {
        if (Current.FullAutoEnabled == enabled)
            return;

        Current.FullAutoEnabled = enabled;
        Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        _logger.Information("Full Auto setting changed to {Enabled}", enabled);
    }

    private AppSettings Load()
    {
        if (!File.Exists(_path))
            return new AppSettings();

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions)
                   ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.Warning(ex, "Could not read settings from {Path}; using defaults", _path);
            return new AppSettings();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_path, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(ex, "Could not save settings to {Path}", _path);
        }
    }
}
