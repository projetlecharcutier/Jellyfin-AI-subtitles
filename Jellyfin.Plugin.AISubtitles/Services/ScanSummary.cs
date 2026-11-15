using System.Collections.Generic;

namespace Jellyfin.Plugin.AISubtitles.Services;

/// <summary>
/// Aggregated result of a scan run.
/// </summary>
internal sealed class ScanSummary
{
    /// <summary>
    /// Gets or sets the number of items processed.
    /// </summary>
    public int ItemsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the number of items skipped because they have no subtitle.
    /// </summary>
    public int ItemsSkippedNoSubtitle { get; set; }

    /// <summary>
    /// Gets or sets the number of subtitle files skipped because the "-AI" file already exists.
    /// </summary>
    public int FilesSkippedExisting { get; set; }

    /// <summary>
    /// Gets or sets the number of subtitle files generated.
    /// </summary>
    public int FilesGenerated { get; set; }

    /// <summary>
    /// Gets or sets the number of errors encountered.
    /// </summary>
    public int Errors { get; set; }

    /// <summary>
    /// Gets the human readable log messages.
    /// </summary>
    public List<string> Messages { get; } = new();
}
