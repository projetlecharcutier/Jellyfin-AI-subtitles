using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AISubtitles.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        MistralApiKey = string.Empty;
        MistralModel = "mistral-small-latest";
        TargetLanguages = "fre";
        RecentItemsLimit = 10;
        AutoScanEnabled = false;
        ScanIntervalHours = 24;
        SeriesEpisodeLimit = 10;
        LastAutoScanUtc = string.Empty;
    }

    /// <summary>
    /// Gets or sets the Mistral API key.
    /// </summary>
    public string MistralApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Mistral model to use for translations.
    /// </summary>
    public string MistralModel { get; set; }

    /// <summary>
    /// Gets or sets the comma-separated ISO 639-1 codes of the languages to generate.
    /// </summary>
    public string TargetLanguages { get; set; }

    /// <summary>
    /// Gets or sets the number of most recent items processed by the automatic scan.
    /// </summary>
    public int RecentItemsLimit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the automatic scan is enabled.
    /// </summary>
    public bool AutoScanEnabled { get; set; }

    /// <summary>
    /// Gets or sets the interval in hours between two automatic scans.
    /// </summary>
    public int ScanIntervalHours { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of episodes processed when a whole series is targeted.
    /// </summary>
    public int SeriesEpisodeLimit { get; set; }

    /// <summary>
    /// Gets or sets the UTC date-time of the last automatic scan (ISO 8601 round-trip).
    /// </summary>
    public string LastAutoScanUtc { get; set; }
}
