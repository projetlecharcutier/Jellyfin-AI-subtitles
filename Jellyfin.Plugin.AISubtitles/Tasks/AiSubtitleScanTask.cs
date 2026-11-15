using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AISubtitles.Tasks;

/// <summary>
/// Scheduled task that scans the most recent library items for AI subtitle generation.
/// </summary>
internal sealed class AiSubtitleScanTask : IScheduledTask
{
    private readonly Plugin _plugin;
    private readonly ILogger<AiSubtitleScanTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiSubtitleScanTask"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="logger">The logger.</param>
    public AiSubtitleScanTask(Plugin plugin, ILogger<AiSubtitleScanTask> logger)
    {
        _plugin = plugin;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "AI Subtitles: scan des derniers médias";

    /// <inheritdoc />
    public string Key => "AISubtitlesScan";

    /// <inheritdoc />
    public string Description => "Scanne les sous-titres des derniers médias ajoutés et génère les traductions IA configurées.";

    /// <inheritdoc />
    public string Category => "AI Subtitles";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        => new[]
        {
            new TaskTriggerInfo { Type = TaskTriggerInfoType.DailyTrigger, TimeOfDayTicks = TimeSpan.FromHours(3).Ticks }
        };

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = _plugin.Configuration;
        if (!config.AutoScanEnabled)
        {
            return;
        }

        if (DateTime.TryParse(config.LastAutoScanUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastRun)
            && DateTime.UtcNow - lastRun < TimeSpan.FromHours(Math.Max(1, config.ScanIntervalHours)))
        {
            _logger.LogDebug("Scan auto ignoré: l'intervalle de {Hours}h n'est pas atteint.", config.ScanIntervalHours);
            return;
        }

        var summary = await _plugin.Engine.ScanRecentItemsAsync(cancellationToken).ConfigureAwait(false);
        config.LastAutoScanUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        _plugin.SaveConfiguration();
        _logger.LogInformation(
            "Scan auto terminé: {Processed} médias traités, {Generated} fichiers générés, {NoSub} sans sous-titre, {Skipped} existants ignorés, {Errors} erreurs.",
            summary.ItemsProcessed,
            summary.FilesGenerated,
            summary.ItemsSkippedNoSubtitle,
            summary.FilesSkippedExisting,
            summary.Errors);
    }
}
