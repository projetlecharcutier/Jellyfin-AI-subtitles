using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    /// <summary>
    /// Lists all generated "-AI" subtitle files found in the library folders, paginated.
    /// </summary>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The page size (max 200).</param>
    /// <returns>The paginated list.</returns>
    [HttpGet("History")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var roots = _libraryManager.GetVirtualFolders()
            .SelectMany(v => v.Locations)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var files = new List<HistoryItem>();
        foreach (var root in roots)
        {
            IEnumerable<string> found;
            try
            {
                found = Directory.EnumerateFiles(root, "*-AI.srt", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lecture du dossier impossible: {Root}.", root);
                continue;
            }

            foreach (var file in found)
            {
                try
                {
                    var info = new FileInfo(file);
                    files.Add(new HistoryItem { Path = file, Size = info.Length, Modified = info.LastWriteTimeUtc });
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        var ordered = files.OrderByDescending(f => f.Modified).ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new { total = ordered.Count, page, pageSize, items });
    }

    private static Services.SubtitleProcessingService GetEngine()
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("Le plugin n'est pas initialisé.");
        return plugin.Engine;
    }
}
