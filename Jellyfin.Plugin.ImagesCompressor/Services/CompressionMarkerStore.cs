using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ImagesCompressor.Services;

/// <summary>
/// Remembers which files have already been compressed. The primary storage is the
/// <c>user.compressed</c> extended attribute (compatible with the original script); when the file
/// system does not support it, a manifest file in the Jellyfin data folder is used instead, keyed
/// by path and validated against file size and modification time.
/// </summary>
public sealed class CompressionMarkerStore
{
    private const string AttributeName = "user.compressed";

    private readonly string _manifestPath;
    private readonly ILogger<CompressionMarkerStore> _logger;
    private readonly object _lock = new();
    private Dictionary<string, string> _manifest = new(PathUtils.Comparer);
    private bool _dirty;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompressionMarkerStore"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{CompressionMarkerStore}"/> interface.</param>
    public CompressionMarkerStore(IApplicationPaths applicationPaths, ILogger<CompressionMarkerStore> logger)
    {
        _manifestPath = Path.Combine(applicationPaths.DataPath, "images-compressor", "manifest.json");
        _logger = logger;
    }

    /// <summary>
    /// Loads the fallback manifest from disk, dropping entries of files that no longer exist.
    /// </summary>
    public void Load()
    {
        lock (_lock)
        {
            _manifest = new Dictionary<string, string>(PathUtils.Comparer);
            _dirty = false;

            if (!File.Exists(_manifestPath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_manifestPath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (loaded is null)
                {
                    return;
                }

                foreach (var entry in loaded)
                {
                    if (File.Exists(entry.Key))
                    {
                        _manifest[entry.Key] = entry.Value;
                    }
                    else
                    {
                        _dirty = true;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "{Prefix}Unable to read {Path}, starting with an empty manifest", Plugin.LogPrefix, _manifestPath);
            }
        }
    }

    /// <summary>
    /// Saves the fallback manifest if it changed.
    /// </summary>
    public void Save()
    {
        lock (_lock)
        {
            if (!_dirty)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_manifestPath)!);
                File.WriteAllText(_manifestPath, JsonSerializer.Serialize(_manifest));
                _dirty = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "{Prefix}Unable to write {Path}", Plugin.LogPrefix, _manifestPath);
            }
        }
    }

    /// <summary>
    /// Checks whether the file has already been compressed.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <returns><c>true</c> if the file is marked as compressed.</returns>
    public bool IsMarked(string path)
    {
        if (XattrInterop.Has(path, AttributeName))
        {
            return true;
        }

        lock (_lock)
        {
            return _manifest.TryGetValue(path, out var signature)
                && string.Equals(signature, GetSignature(path), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Marks the file as compressed. Call it after the final size and modification time are set.
    /// </summary>
    /// <param name="path">File path.</param>
    public void Mark(string path)
    {
        if (XattrInterop.SetTrue(path, AttributeName))
        {
            return;
        }

        lock (_lock)
        {
            _manifest[path] = GetSignature(path);
            _dirty = true;
        }
    }

    private static string GetSignature(string path)
    {
        var info = new FileInfo(path);
        return string.Create(CultureInfo.InvariantCulture, $"{info.Length}|{info.LastWriteTimeUtc.Ticks}");
    }
}
