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

        var system = "You are a professional subtitle translator. Translate every line into "
            + targetLanguage
            + ". Keep the meaning, tone and approximate length. Do not add, remove or reorder lines. "
            + "Reply ONLY with a JSON object of the form {\"lines\":[...]} containing exactly "
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

                return ParseTranslatedLines(body, lines.Count);
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(ex, "Traduction Mistral: tentative {Attempt}/{Max} échouée.", attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Traduction Mistral échouée après " + MaxAttempts + " tentatives.");
    }

    private static IReadOnlyList<string> ParseTranslatedLines(string responseBody, int expectedCount)
    {
        using var document = JsonDocument.Parse(responseBody);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        var start = content.IndexOf('{', System.StringComparison.Ordinal);
        var end = content.LastIndexOf('}');
        var json = start >= 0 && end > start ? content.Substring(start, end - start + 1) : content;

        using var linesDocument = JsonDocument.Parse(json);
        var array = linesDocument.RootElement.GetProperty("lines");
        var result = new List<string>(expectedCount);
        foreach (var element in array.EnumerateArray())
        {
            result.Add(element.GetString() ?? string.Empty);
        }

        if (result.Count != expectedCount)
        {
            throw new InvalidOperationException(
                "Réponse Mistral invalide: " + result.Count + " lignes reçues, " + expectedCount + " attendues.");
        }

        return result;
    }
}
