using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ImagesCompressor.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ImagesCompressor.Services;

/// <summary>
/// Scheduled task that compresses the images of the enabled directories (metadata folder and
/// library folders). Files already compressed are skipped.
/// </summary>
public sealed class ImagesCompressorTask : IScheduledTask
{
    private const int SaveEvery = 200;

    private readonly ILibraryManager _libraryManager;
    private readonly IServerApplicationPaths _applicationPaths;
    private readonly ImageCompressor _compressor;
    private readonly CompressionMarkerStore _markers;
    private readonly ILogger<ImagesCompressorTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImagesCompressorTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="applicationPaths">Instance of the <see cref="IServerApplicationPaths"/> interface.</param>
    /// <param name="compressor">The image compressor.</param>
    /// <param name="markers">The store of already compressed files.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ImagesCompressorTask}"/> interface.</param>
    public ImagesCompressorTask(
        ILibraryManager libraryManager,
        IServerApplicationPaths applicationPaths,
        ImageCompressor compressor,
        CompressionMarkerStore markers,
        ILogger<ImagesCompressorTask> logger)
    {
        _libraryManager = libraryManager;
        _applicationPaths = applicationPaths;
        _compressor = compressor;
        _markers = markers;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Compress images";

    /// <inheritdoc />
    public string Key => "ImagesCompressor";

    /// <inheritdoc />
    public string Description => "Compresses the images of the metadata folder and of the libraries enabled in the Images compressor settings. Files already compressed are skipped.";

    /// <inheritdoc />
    public string Category => "Images compressor";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        return Task.Run(() => Run(progress, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // No fixed schedule: the task is queued automatically after library scans
        // (see LibraryScanQueueService). Triggers can still be added from the dashboard.
        return Array.Empty<TaskTriggerInfo>();
    }

    /// <summary>
    /// Gets the directories offered in the configuration page: the metadata folder and the folders of all libraries.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="applicationPaths">The server application paths.</param>
    /// <returns>The distinct directory paths.</returns>
    internal static List<string> GetKnownDirectories(ILibraryManager libraryManager, IServerApplicationPaths applicationPaths)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(PathUtils.Comparer);

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (seen.Add(PathUtils.Normalize(path)))
            {
                result.Add(path);
            }
        }

        Add(applicationPaths.InternalMetadataPath);
        foreach (var folder in libraryManager.GetVirtualFolders())
        {
            foreach (var location in folder.Locations ?? Array.Empty<string>())
            {
                Add(location);
            }
        }

        return result;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.##} {units[unit]}");
    }

    private void Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var quality = Math.Clamp(config.Quality, 1, 100);

        var enabled = new HashSet<string>(PathUtils.Comparer);
        foreach (var path in config.EnabledPaths ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                enabled.Add(PathUtils.Normalize(path));
            }
        }

        // Only directories that still exist in Jellyfin (metadata folder or current libraries) are processed.
        var roots = new List<string>();
        foreach (var directory in GetKnownDirectories(_libraryManager, _applicationPaths))
        {
            if (enabled.Contains(PathUtils.Normalize(directory)) && Directory.Exists(directory))
            {
                roots.Add(directory);
            }
        }

        if (roots.Count == 0)
        {
            _logger.LogInformation("{Prefix}no directory enabled, nothing to do", Plugin.LogPrefix);
            progress.Report(100);
            return;
        }

        if (!_compressor.IsAvailable)
        {
            _logger.LogWarning("{Prefix}the Jellyfin image encoder is not available (native Skia library missing?), aborting", Plugin.LogPrefix);
            progress.Report(100);
            return;
        }

        _markers.Load();
        try
        {
            var pending = CollectPendingFiles(roots, cancellationToken);
            if (pending.Count == 0)
            {
                _logger.LogInformation("{Prefix}no new images to compress", Plugin.LogPrefix);
                progress.Report(100);
                return;
            }

            _logger.LogInformation(
                "{Prefix}compressing {Count} images (quality {Quality}) in {Roots} directories",
                Plugin.LogPrefix,
                pending.Count,
                quality,
                roots.Count);

            CompressFiles(pending, quality, progress, cancellationToken);
        }
        finally
        {
            _markers.Save();
        }
    }

    private List<string> CollectPendingFiles(List<string> roots, CancellationToken cancellationToken)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        var seen = new HashSet<string>(PathUtils.Comparer);
        var pending = new List<string>();

        foreach (var root in roots)
        {
            foreach (var file in Directory.EnumerateFiles(PathUtils.Resolve(root), "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!ImageCompressor.IsSupportedImage(file) || !seen.Add(file))
                {
                    continue;
                }

                if (!_markers.IsMarked(file))
                {
                    pending.Add(file);
                }
            }
        }

        return pending;
    }

    private void CompressFiles(List<string> files, int quality, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var processed = 0;
        var compressed = 0;
        var errors = 0;
        long savedBytes = 0;
        var lastPercent = -1;

        try
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var saved = _compressor.Compress(file, quality);
                    if (saved > 0)
                    {
                        compressed++;
                        savedBytes += saved;
                    }

                    // Marked also when the result was not smaller, so the file is not retried at every run.
                    _markers.Mark(file);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    errors++;
                    _logger.LogWarning(ex, "{Prefix}compression failed for {File}", Plugin.LogPrefix, file);
                }

                processed++;
                var percent = (int)(processed * 100L / files.Count);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress.Report(percent);
                }

                if (processed % SaveEvery == 0)
                {
                    _markers.Save();
                }
            }
        }
        finally
        {
            _logger.LogInformation(
                "{Prefix}processed {Processed}/{Total} images, {Compressed} compressed, {Errors} errors, {Saved} saved",
                Plugin.LogPrefix,
                processed,
                files.Count,
                compressed,
                errors,
                FormatBytes(savedBytes));
        }
    }
}
