using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ImagesCompressor.Services;

/// <summary>
/// Details of a single run of the compression task.
/// </summary>
public sealed class RunRecord
{
    /// <summary>
    /// Gets the start time (UTC).
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Gets the end time (UTC).
    /// </summary>
    public DateTime EndTime { get; init; }

    /// <summary>
    /// Gets the total run time in seconds.
    /// </summary>
    public long DurationSeconds { get; init; }

    /// <summary>
    /// Gets the status of the run: Completed, Cancelled or Failed.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets the analyzed directories.
    /// </summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>
    /// Gets the compression quality used.
    /// </summary>
    public int Quality { get; init; }

    /// <summary>
    /// Gets the number of files to process at the start of the run.
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Gets the number of files processed.
    /// </summary>
    public int ProcessedFiles { get; init; }

    /// <summary>
    /// Gets the number of files that could not be compressed.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Gets the errors of the run (limited to the first ones).
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets the total size in bytes of the files to process, before the run.
    /// </summary>
    public long InitialSize { get; init; }

    /// <summary>
    /// Gets the total size in bytes of the same files after the run.
    /// </summary>
    public long FinalSize { get; init; }

    /// <summary>
    /// Gets the space saved in bytes.
    /// </summary>
    public long SavedSize { get; init; }
}
