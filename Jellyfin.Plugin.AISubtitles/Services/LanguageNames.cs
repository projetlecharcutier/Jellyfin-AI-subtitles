using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AISubtitles.Services;

/// <summary>
/// Maps ISO 639-1 codes to full lowercase language names used in generated file names.
/// </summary>
internal static class LanguageNames
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ara"] = "arabic",
        ["zho"] = "chinese",
        ["deu"] = "german",
        ["eng"] = "english",
        ["spa"] = "spanish",
        ["fre"] = "french",
        ["hin"] = "hindi",
        ["ita"] = "italian",
        ["jpn"] = "japanese",
        ["kor"] = "korean",
        ["nld"] = "dutch",
        ["pol"] = "polish",
        ["por"] = "portuguese",
        ["rus"] = "russian",
        ["swe"] = "swedish",
        ["tur"] = "turkish"
    };

    /// <summary>
    /// Gets the full lowercase language name for an ISO 639-1 code (falls back to the code itself).
    /// </summary>
    /// <param name="isoCode">The ISO 639-1 code.</param>
    /// <returns>The full language name.</returns>
    public static string FullName(string isoCode)
        => Map.TryGetValue(isoCode, out var name) ? name : isoCode;
}
