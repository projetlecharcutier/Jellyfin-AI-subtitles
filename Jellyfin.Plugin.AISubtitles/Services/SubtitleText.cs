using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.AISubtitles.Services;

/// <summary>
/// Tolerant SRT/VTT parsing and SRT serialization.
/// </summary>
internal static class SubtitleText
{
    private static readonly Regex BlockSplitter = new(@"\n[ \t]*\n", RegexOptions.Compiled);

    /// <summary>
    /// Parses SRT or VTT content into cues.
    /// </summary>
    /// <param name="content">The raw subtitle file content.</param>
    /// <returns>The parsed cues.</returns>
    public static List<SubtitleCue> Parse(string content)
    {
        var cues = new List<SubtitleCue>();
        var normalized = content.Replace("\r\n", "\n", System.StringComparison.Ordinal).Replace('\r', '\n');
        foreach (var rawBlock in BlockSplitter.Split(normalized))
        {
            var lines = rawBlock.Split('\n');
            var timecodeIndex = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("-->", System.StringComparison.Ordinal))
                {
                    timecodeIndex = i;
                    break;
                }
            }

            if (timecodeIndex < 0)
            {
                continue;
            }

            var first = lines[0].Trim();
            if (first.StartsWith("WEBVTT", System.StringComparison.Ordinal) || first.StartsWith("NOTE", System.StringComparison.Ordinal))
            {
                continue;
            }

            var index = cues.Count + 1;
            if (timecodeIndex == 1 && int.TryParse(first, out var parsedIndex))
            {
                index = parsedIndex;
            }

            var cue = new SubtitleCue { Index = index, Timecode = lines[timecodeIndex].Trim() };
            for (var i = timecodeIndex + 1; i < lines.Length; i++)
            {
                if (lines[i].Length > 0)
                {
                    cue.Lines.Add(lines[i]);
                }
            }

            if (cue.Lines.Count > 0)
            {
                cues.Add(cue);
            }
        }

        return cues;
    }

    /// <summary>
    /// Serializes cues back to SRT format (UTF-8, comma decimal separator).
    /// </summary>
    /// <param name="cues">The cues to serialize.</param>
    /// <returns>The SRT content.</returns>
    public static string ToSrt(IReadOnlyList<SubtitleCue> cues)
    {
        var sb = new StringBuilder();
        foreach (var cue in cues)
        {
            sb.Append(cue.Index).Append('\n');
            sb.Append(cue.Timecode.Replace('.', ',')).Append('\n');
            foreach (var line in cue.Lines)
            {
                sb.Append(line).Append('\n');
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }
}
