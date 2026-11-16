using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AISubtitles.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AISubtitles.Services;

/// <summary>
/// Scans library items, finds an existing subtitle to use as source and generates
/// AI translated "-AI" subtitle files next to the media file.
/// </summary>
internal sealed class SubtitleProcessingService
{
    private const int CuesPerRequest = 40;
    private const int MaxScanWindow = 500;
    private const int MaxProcessedLogEntries = 20;
    private const int RequestDelayMs = 500;

    private readonly Plugin _plugin;
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly IDirectoryService _directoryService;
    private readonly MistralClient _mistral;
    private readonly ILogger<SubtitleProcessingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleProcessingService"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="mediaEncoder">The media encoder (ffmpeg path).</param>
    /// <param name="directoryService">The directory service.</param>
    /// <param name="mistral">The Mistral client.</param>
    /// <param name="logger">The logger.</param>
    public SubtitleProcessingService(
        Plugin plugin,
        ILibraryManager libraryManager,
        IMediaEncoder mediaEncoder,
        IDirectoryService directoryService,
        MistralClient mistral,
        ILogger<SubtitleProcessingService> logger)
    {
        _plugin = plugin;
        _libraryManager = libraryManager;
        _mediaEncoder = mediaEncoder;
        _directoryService = directoryService;
        _mistral = mistral;
        _logger = logger;
    }

    /// <summary>
    /// Scans the most recently added movies and episodes (limited by configuration).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scan summary.</returns>
    public async Task<ScanSummary> ScanRecentItemsAsync(CancellationToken cancellationToken)
    {
        var config = _plugin.Configuration;
        var query = new InternalItemsQuery
        {
            Recursive = true,
            IsFolder = false,
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode }
        };
        var items = _libraryManager.GetItemList(query)
            .Where(i => !string.IsNullOrEmpty(i.Path) && File.Exists(i.Path))
            .OrderByDescending(i => i.DateCreated)
            .Take(MaxScanWindow)
            .ToList();
        _logger.LogInformation("Scan des {Count} derniers médias, arrêt après {Limit} média(s) traité(s).", items.Count, config.MaxTreatments);
        var summary = await ProcessItemsAsync(items, Math.Max(1, config.MaxTreatments), cancellationToken).ConfigureAwait(false);
        PersistProcessedLog(summary);
        return summary;
    }

    /// <summary>
    /// Scans all episodes of a series (limited by configuration).
    /// </summary>
    /// <param name="seriesId">The series id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scan summary.</returns>
    public async Task<ScanSummary> ScanSeriesAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var config = _plugin.Configuration;
        var query = new InternalItemsQuery
        {
            Recursive = true,
            IsFolder = false,
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            AncestorIds = new[] { seriesId }
        };
        var items = _libraryManager.GetItemList(query)
            .Where(i => !string.IsNullOrEmpty(i.Path) && File.Exists(i.Path))
            .OrderByDescending(i => i.DateCreated)
            .Take(Math.Max(1, config.SeriesEpisodeLimit))
            .ToList();
        _logger.LogInformation("Scan de la série {SeriesId}: {Count} épisodes ({Limit} max).", seriesId, items.Count, config.SeriesEpisodeLimit);
        var summary = await ProcessItemsAsync(items, 0, cancellationToken).ConfigureAwait(false);
        PersistProcessedLog(summary);
        return summary;
    }

    /// <summary>
    /// Processes a single item (movie or episode).
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scan summary.</returns>
    public Task<ScanSummary> ProcessSingleItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item == null)
        {
            throw new InvalidOperationException("Média introuvable: " + itemId);
        }

        return ProcessItemsAsync(new[] { item }, 0, cancellationToken).ContinueWith(
            t =>
            {
                PersistProcessedLog(t.Result);
                return t.Result;
            },
            cancellationToken,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private async Task<ScanSummary> ProcessItemsAsync(IReadOnlyList<BaseItem> items, int maxTreatments, CancellationToken cancellationToken)
    {
        var summary = new ScanSummary();
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var generated = 0;
            var treated = false;
            try
            {
                (treated, generated) = await ProcessItemAsync(item, summary, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                summary.Errors++;
                summary.Messages.Add("Erreur sur " + item.Path + ": " + ex.Message);
                _logger.LogError(ex, "Erreur lors du traitement de {Path}.", item.Path);
            }

            summary.ItemsProcessed++;
            if (treated)
            {
                summary.ItemsTreated++;
                summary.ProcessedEntries.Add(
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                    + " | " + item.Path + " | " + generated + " fichier(s) généré(s)");
            }

            if (maxTreatments > 0 && summary.ItemsTreated >= maxTreatments)
            {
                _logger.LogInformation("Limite de {Max} média(s) traité(s) atteinte, arrêt du scan.", maxTreatments);
                break;
            }
        }

        return summary;
    }

    private void PersistProcessedLog(ScanSummary summary)
    {
        if (summary.ProcessedEntries.Count == 0)
        {
            return;
        }

        var config = _plugin.Configuration;
        var lines = new List<string>(summary.ProcessedEntries);
        if (!string.IsNullOrEmpty(config.LastProcessed))
        {
            lines.AddRange(config.LastProcessed.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }

        config.LastProcessed = string.Join('\n', lines.Take(MaxProcessedLogEntries));
        _plugin.SaveConfiguration();
    }

    private async Task<(bool Treated, int Generated)> ProcessItemAsync(BaseItem item, ScanSummary summary, CancellationToken cancellationToken)
    {
        var config = _plugin.Configuration;
        var textStreams = GetTextSubtitleStreams(item);
        if (textStreams.Count == 0)
        {
            summary.ItemsSkippedNoSubtitle++;
            _logger.LogInformation("Aucun sous-titre existant pour {Path}: rien à faire.", item.Path);
            return (false, 0);
        }

        var source = textStreams.FirstOrDefault(s => string.Equals(s.Language, "eng", StringComparison.OrdinalIgnoreCase))
            ?? textStreams.FirstOrDefault(s => s.IsExternal)
            ?? textStreams[0];

        var content = await ReadStreamContentAsync(item, source, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(content))
        {
            summary.ItemsSkippedNoSubtitle++;
            _logger.LogWarning("Sous-titre source illisible pour {Path}: aucune action effectuée.", item.Path);
            return (false, 0);
        }

        var cues = SubtitleText.Parse(content);
        if (cues.Count == 0)
        {
            summary.ItemsSkippedNoSubtitle++;
            _logger.LogWarning("Sous-titre illisible pour {Path}: aucune action effectuée.", item.Path);
            return (false, 0);
        }

        var sourceLanguage = source.Language ?? string.Empty;
        var targets = (config.TargetLanguages ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mediaDir = Path.GetDirectoryName(item.Path)!;
        var baseName = Path.GetFileNameWithoutExtension(item.Path);
        var generated = 0;
        foreach (var lang in targets)
        {
            if (string.Equals(lang, sourceLanguage, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var outputPath = Path.Combine(mediaDir, baseName + "." + LanguageNames.FullName(lang) + "-AI.srt");
            if (File.Exists(outputPath))
            {
                summary.FilesSkippedExisting++;
                _logger.LogInformation("Fichier existant ignoré: {Path}.", outputPath);
                continue;
            }

            var translated = await TranslateCuesAsync(config, lang, cues, cancellationToken).ConfigureAwait(false);
            try
            {
                using (var fileStream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(fileStream, new UTF8Encoding(false)))
                {
                    await writer.WriteAsync(SubtitleText.ToSrt(translated).AsMemory(), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
                summary.FilesSkippedExisting++;
                _logger.LogInformation("Fichier existant ignoré (aucun écrasement): {Path}.", outputPath);
                continue;
            }

            generated++;
            summary.FilesGenerated++;
            summary.Messages.Add("Généré: " + outputPath);
            _logger.LogInformation("Sous-titre IA généré: {Path}.", outputPath);
        }

        if (generated > 0)
        {
            await RefreshItemAsync(item, cancellationToken).ConfigureAwait(false);
        }

        return (true, generated);
    }

    private async Task<List<SubtitleCue>> TranslateCuesAsync(
        PluginConfiguration config,
        string langCode,
        List<SubtitleCue> cues,
        CancellationToken cancellationToken)
    {
        var result = cues
            .Select(c => new SubtitleCue { Index = c.Index, Timecode = c.Timecode, Lines = new List<string>(c.Lines) })
            .ToList();
        var languageName = LanguageNames.FullName(langCode);
        for (var start = 0; start < cues.Count; start += CuesPerRequest)
        {
            var chunk = cues.Skip(start).Take(CuesPerRequest).ToList();
            var mapping = new List<(int Cue, int Line)>();
            var lines = new List<string>();
            for (var c = 0; c < chunk.Count; c++)
            {
                for (var l = 0; l < chunk[c].Lines.Count; l++)
                {
                    mapping.Add((c, l));
                    lines.Add(chunk[c].Lines[l]);
                }
            }

            if (lines.Count == 0)
            {
                continue;
            }

            var translated = await _mistral.TranslateLinesAsync(
                config.MistralApiKey, config.MistralModel, languageName, lines, cancellationToken).ConfigureAwait(false);
            await Task.Delay(RequestDelayMs, cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < mapping.Count; i++)
            {
                result[start + mapping[i].Cue].Lines[mapping[i].Line] = translated[i];
            }
        }

        return result;
    }

    private static List<MediaStream> GetTextSubtitleStreams(BaseItem item)
    {
        if (item is not IHasMediaSources hasMediaSources)
        {
            return new List<MediaStream>();
        }

        return hasMediaSources.GetMediaSources(false)
            .SelectMany(s => s.MediaStreams)
            .Where(s => s.Type == MediaStreamType.Subtitle && s.IsTextSubtitleStream)
            .ToList();
    }

    private async Task<string?> ReadStreamContentAsync(BaseItem item, MediaStream stream, CancellationToken cancellationToken)
    {
        if (stream.IsExternal && !string.IsNullOrEmpty(stream.Path) && File.Exists(stream.Path))
        {
            return await File.ReadAllTextAsync(stream.Path, cancellationToken).ConfigureAwait(false);
        }

        return await ExtractEmbeddedStreamAsync(item, stream, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> ExtractEmbeddedStreamAsync(BaseItem item, MediaStream stream, CancellationToken cancellationToken)
    {
        var encoder = _mediaEncoder.EncoderPath;
        if (string.IsNullOrEmpty(encoder))
        {
            _logger.LogWarning("Aucun encodeur ffmpeg configuré: sous-titre embarqué non extractible pour {Path}, média ignoré.", item.Path);
            return null;
        }

        var tempFile = Path.Combine(Path.GetTempPath(), "aisub-" + Guid.NewGuid().ToString("N") + ".srt");
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = encoder,
                Arguments = "-hide_banner -loglevel error -y -i \"" + item.Path + "\" -map 0:" + stream.Index.ToString(System.Globalization.CultureInfo.InvariantCulture) + " -c:s srt \"" + tempFile + "\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            System.ComponentModel.Win32Exception? startError = null;
            Process? process = null;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                startError = ex;
            }

            if (process == null)
            {
                _logger.LogWarning(startError, "Impossible de lancer ffmpeg ({Encoder}): média ignoré pour {Path}.", encoder, item.Path);
                return null;
            }

            using (process)
            {
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(tempFile))
            {
                throw new InvalidOperationException("ffmpeg a échoué: " + error);
            }

            return await File.ReadAllTextAsync(tempFile, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private async Task RefreshItemAsync(BaseItem item, CancellationToken cancellationToken)
    {
        try
        {
            var options = new MetadataRefreshOptions(_directoryService)
            {
                MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                ForceSave = true
            };
            await item.RefreshMetadata(options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Le rafraîchissement des métadonnées a échoué pour {Path}.", item.Path);
        }
    }
}
