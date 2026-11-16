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
        MaxTreatments = 5;
        AutoScanEnabled = false;
        ScanIntervalHours = 24;
        SeriesEpisodeLimit = 10;
        LastAutoScanUtc = string.Empty;
        LastProcessed = string.Empty;
        AllowAnyUserScan = true;
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
    /// Gets or sets the maximum number of items actually treated (having a source subtitle) per scan.
    /// </summary>
    public int MaxTreatments { get; set; }

    /// <summary>
    /// Gets or sets the newline-separated log of the most recently processed items (for display in the dashboard).
    /// </summary>
    public string LastProcessed { get; set; }

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

    /// <summary>
    /// Gets or sets a value indicating whether any authenticated user (not only admins) may trigger AI subtitle scans.
    /// </summary>
    public bool AllowAnyUserScan { get; set; }
}
