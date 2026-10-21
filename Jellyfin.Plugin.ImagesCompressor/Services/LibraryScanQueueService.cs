using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ImagesCompressor.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ImagesCompressor.Services;

/// <summary>
/// Queues the compression task after library scans. A scan is detected through the items it adds
/// or updates: once no item has changed for a short quiet period, the task is queued. The end of
/// the standard "Scan Media Library" task also queues it.
/// </summary>
public sealed class LibraryScanQueueService : IHostedService, IDisposable
{
    private const string RefreshLibraryTaskKey = "RefreshLibrary";
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMinutes(2);

    private readonly ILibraryManager _libraryManager;
    private readonly ITaskManager _taskManager;
    private readonly ILogger<LibraryScanQueueService> _logger;
    private Timer? _timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryScanQueueService"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="taskManager">Instance of the <see cref="ITaskManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{LibraryScanQueueService}"/> interface.</param>
    public LibraryScanQueueService(
        ILibraryManager libraryManager,
        ITaskManager taskManager,
        ILogger<LibraryScanQueueService> logger)
    {
        _libraryManager = libraryManager;
        _taskManager = taskManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(OnTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _libraryManager.ItemAdded += OnLibraryChanged;
        _libraryManager.ItemUpdated += OnLibraryChanged;
        _taskManager.TaskCompleted += OnTaskCompleted;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnLibraryChanged;
        _libraryManager.ItemUpdated -= OnLibraryChanged;
        _taskManager.TaskCompleted -= OnTaskCompleted;
        _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer?.Dispose();
    }

    private void OnLibraryChanged(object? sender, ItemChangeEventArgs e)
    {
        Schedule();
    }

    private void OnTaskCompleted(object? sender, TaskCompletionEventArgs e)
    {
        if (string.Equals(e.Task.ScheduledTask.Key, RefreshLibraryTaskKey, StringComparison.Ordinal))
        {
            Schedule();
        }
    }

    private void Schedule()
    {
        var enabled = (Plugin.Instance?.Configuration ?? new PluginConfiguration()).EnabledPaths;
        if (enabled is null || enabled.Length == 0)
        {
            return;
        }

        // Every change restarts the countdown, so the task starts only when the scan is quiet.
        _timer?.Change(QuietPeriod, Timeout.InfiniteTimeSpan);
    }

    private void OnTimerElapsed(object? state)
    {
        try
        {
            _taskManager.QueueScheduledTask<ImagesCompressorTask>();
            _logger.LogInformation("{Prefix}queued the compression task after a library change", Plugin.LogPrefix);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Prefix}unable to queue the compression task", Plugin.LogPrefix);
        }
    }
}
