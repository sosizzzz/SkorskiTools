using System.Text.Json;
using Serilog;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Models;

namespace SkorskiTools.Core.Services;

/// <summary>Stores a small, read-only operation timeline in the user folder.</summary>
public interface IOperationHistoryService
{
    IReadOnlyList<OperationHistoryEntry> GetEntries();

    void Add(OperationHistoryEntry entry);

    void Clear();

    event EventHandler? HistoryChanged;
}

/// <summary>Persists up to <see cref="MaxEntries"/> recent operations as JSON.</summary>
public sealed class OperationHistoryService : IOperationHistoryService
{
    public const int MaxEntries = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private readonly List<OperationHistoryEntry> _entries;

    public event EventHandler? HistoryChanged;

    public OperationHistoryService(string? userRoot = null, ILogger? logger = null)
    {
        _path = Path.Combine(userRoot ?? AppPaths.GetUserRoot(), "operation-history.json");
        _logger = logger ?? Log.Logger;
        _entries = Load();
    }

    public IReadOnlyList<OperationHistoryEntry> GetEntries()
    {
        lock (_gate)
            return _entries.OrderByDescending(e => e.Timestamp).ToList();
    }

    public void Add(OperationHistoryEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
            while (_entries.Count > MaxEntries)
                _entries.RemoveAt(0);

            Save();
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            Save();
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private List<OperationHistoryEntry> Load()
    {
        if (!File.Exists(_path))
            return new List<OperationHistoryEntry>();

        try
        {
            return JsonSerializer.Deserialize<List<OperationHistoryEntry>>(
                       File.ReadAllText(_path), JsonOptions)
                   ?? new List<OperationHistoryEntry>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.Warning(ex, "Could not read operation history from {Path}", _path);
            return new List<OperationHistoryEntry>();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_path, JsonSerializer.Serialize(_entries, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(ex, "Could not save operation history to {Path}", _path);
        }
    }
}
