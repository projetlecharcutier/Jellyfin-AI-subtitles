using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AISubtitles.Api;

/// <summary>
/// API endpoints to trigger AI subtitle generation on demand.
/// </summary>
[ApiController]
[Route("AISubtitles")]
[Authorize(Policy = "RequiresElevation")]
public class AiSubtitlesController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<AiSubtitlesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiSubtitlesController"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="logger">The logger.</param>
    public AiSubtitlesController(ILibraryManager libraryManager, ILogger<AiSubtitlesController> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Launches a background scan of the most recently added movies and episodes.
    /// </summary>
    /// <returns>202 Accepted.</returns>
    [HttpPost("Scan/Recent")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult ScanRecent()
    {
        var engine = GetEngine();
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await engine.ScanRecentItemsAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Le scan des derniers médias a échoué.");
                }
            });
        return Accepted(new { message = "Scan des derniers médias lancé en arrière-plan." });
    }

    /// <summary>
    /// Launches AI subtitle generation for a single movie or episode.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>202 Accepted, or 404 if the item does not exist.</returns>
    [HttpPost("Scan/Item/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ScanItem(Guid itemId)
    {
        if (_libraryManager.GetItemById(itemId) == null)
        {
            return NotFound(new { message = "Média introuvable: " + itemId });
        }

        var engine = GetEngine();
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await engine.ProcessSingleItemAsync(itemId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Le traitement du média {ItemId} a échoué.", itemId);
                }
            });
        return Accepted(new { message = "Traitement du média lancé en arrière-plan: " + itemId });
    }

    /// <summary>
    /// Launches AI subtitle generation for a whole series (bounded by configuration).
    /// </summary>
    /// <param name="seriesId">The series id.</param>
    /// <returns>202 Accepted.</returns>
    [HttpPost("Scan/Series/{seriesId:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult ScanSeries(Guid seriesId)
    {
        var engine = GetEngine();
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await engine.ScanSeriesAsync(seriesId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Le traitement de la série {SeriesId} a échoué.", seriesId);
                }
            });
        return Accepted(new { message = "Traitement de la série lancé en arrière-plan: " + seriesId });
    }

    private static Services.SubtitleProcessingService GetEngine()
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("Le plugin n'est pas initialisé.");
        return plugin.Engine;
    }
}
