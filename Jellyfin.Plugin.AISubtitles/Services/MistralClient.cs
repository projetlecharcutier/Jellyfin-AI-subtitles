using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AISubtitles.Services;

/// <summary>
/// Minimal client for the Mistral chat completions API.
/// </summary>
internal sealed class MistralClient
{
    private const string Endpoint = "https://api.mistral.ai/v1/chat/completions";
    private const int MaxAttempts = 3;

    private static readonly HttpClient HttpClient = new();

    private readonly ILogger<MistralClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MistralClient"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public MistralClient(ILogger<MistralClient> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Translates a list of subtitle text lines into the target language.
    /// </summary>
    /// <param name="apiKey">The Mistral API key.</param>
    /// <param name="model">The Mistral model name.</param>
    /// <param name="targetLanguage">The full target language name.</param>
    /// <param name="lines">The source lines, in order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The translated lines, in the same order and count.</returns>
    public async Task<IReadOnlyList<string>> TranslateLinesAsync(
        string apiKey,
        string model,
        string targetLanguage,
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("La clé API Mistral n'est pas configurée.");
        }

        var system = "You are an expert audiovisual subtitle translator. Translate each numbered line into "
            + targetLanguage
            + ". Rules: produce exactly one output line per input line, in the same order; preserve the meaning, tone and register of the dialogue; keep proper nouns, brands and onomatopoeia unchanged unless they are commonly translated; never merge, split, add or remove lines; never add explanations, notes or numbering; write natural, idiomatic "
            + targetLanguage
            + " suited to subtitles (concise, readable). Reply ONLY with a JSON object of the form {\"lines\":[...]} containing exactly "
            + lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " translated strings, in the same order as the input.";
        var user = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            user.Append(i + 1).Append(": ").AppendLine(lines[i]);
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var payload = new
                {
                    model,
                    messages = new object[]
                    {
                        new { role = "system", content = system },
                        new { role = "user", content = user.ToString() }
                    },
                    response_format = new { type = "json_object" },
                    temperature = 0.3
                };
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(180));
                using var response = await HttpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException("Erreur API Mistral " + ((int)response.StatusCode) + ": " + body);
                }

                return ParseTranslatedLines(body, lines);
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(ex, "Traduction Mistral: tentative {Attempt}/{Max} échouée.", attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Traduction Mistral échouée après " + MaxAttempts + " tentatives.");
    }

    private IReadOnlyList<string> ParseTranslatedLines(string responseBody, IReadOnlyList<string> sourceLines)
    {
        using var document = JsonDocument.Parse(responseBody);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        var translated = TryParseJsonLines(content) ?? SplitRawLines(content);
        if (translated.Count != sourceLines.Count)
        {
            _logger.LogWarning(
                "Mistral a renvoyé {Received} lignes au lieu de {Expected}: complétion/troncature appliquée.",
                translated.Count,
                sourceLines.Count);
        }

        var result = new List<string>(sourceLines.Count);
        for (var i = 0; i < sourceLines.Count; i++)
        {
            result.Add(i < translated.Count && translated[i].Length > 0 ? translated[i] : sourceLines[i]);
        }

        return result;
    }

    private static List<string>? TryParseJsonLines(string content)
    {
        try
        {
            var start = content.IndexOf('{', System.StringComparison.Ordinal);
            var end = content.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            using var linesDocument = JsonDocument.Parse(content.Substring(start, end - start + 1));
            if (!linesDocument.RootElement.TryGetProperty("lines", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new List<string>();
            foreach (var element in array.EnumerateArray())
            {
                result.Add(element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.ToString());
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<string> SplitRawLines(string content)
    {
        var result = new List<string>();
        foreach (var raw in content.Split('\n'))
        {
            var line = System.Text.RegularExpressions.Regex.Replace(raw.Trim(), @"^\d+[.):\-]\s*", string.Empty).Trim();
            if (line.Length > 0 && !line.StartsWith('{') && !line.StartsWith('}'))
            {
                result.Add(line);
            }
        }

        return result;
    }
}
