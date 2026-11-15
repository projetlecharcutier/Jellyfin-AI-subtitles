using System.Collections.Generic;

namespace Jellyfin.Plugin.AISubtitles.Services;

/// <summary>
/// A single subtitle cue (index, timecode and text lines).
/// </summary>
internal sealed class SubtitleCue
{
    /// <summary>
    /// Gets or sets the cue index.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the timecode line (e.g. "00:00:01,000 --&gt; 00:00:04,000").
    /// </summary>
    public string Timecode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text lines of the cue.
    /// </summary>
    public List<string> Lines { get; set; } = new();
}
