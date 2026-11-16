using System;

namespace Jellyfin.Plugin.AISubtitles.Api;

/// <summary>
/// One generated AI subtitle file.
/// </summary>
internal sealed class HistoryItem
{
    /// <summary>
    /// Gets or sets the full file path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the last write time (UTC).
    /// </summary>
    public DateTime Modified { get; set; }
}
