using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ImagesCompressor.Services;

/// <summary>
/// Keeps the details of the latest runs of the compression task in a JSON file in the Jellyfin data folder.
/// </summary>
public sealed class RunHistoryStore
{
    private const int MaxRuns = 50;

    private readonly string _path;
    private readonly ILogger<RunHistoryStore> _logger;
    private readonly object _lock = new();
    private List<RunRecord>? _runs;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunHistoryStore"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{RunHistoryStore}"/> interface.</param>
    public RunHistoryStore(IApplicationPaths applicationPaths, ILogger<RunHistoryStore> logger)
    {
        _path = Path.Combine(applicationPaths.DataPath, "images-compressor", "runs.json");
        _logger = logger;
    }

    /// <summary>
    /// Gets the stored runs, newest first.
    /// </summary>
    /// <returns>The runs.</returns>
    public IReadOnlyList<RunRecord> GetAll()
    {
        lock (_lock)
        {
            return EnsureLoaded().OrderByDescending(r => r.StartTime).ToList();
        }
    }

    /// <summary>
    /// Adds a run, dropping the oldest ones beyond the limit, and saves the file.
    /// </summary>
    /// <param name="run">The run to add.</param>
    public void Add(RunRecord run)
    {
        lock (_lock)
        {
            var runs = EnsureLoaded();
            runs.Add(run);
            if (runs.Count > MaxRuns)
            {
                runs.RemoveRange(0, runs.Count - MaxRuns);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(runs));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "{Prefix}Unable to write {Path}", Plugin.LogPrefix, _path);
            }
        }
    }

    private List<RunRecord> EnsureLoaded()
    {
        if (_runs is not null)
        {
            return _runs;
        }

        _runs = [];
        if (File.Exists(_path))
        {
            try
            {
                _runs = JsonSerializer.Deserialize<List<RunRecord>>(File.ReadAllText(_path)) ?? [];
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "{Prefix}Unable to read {Path}, starting with an empty history", Plugin.LogPrefix, _path);
            }
        }

        return _runs;
    }
}
