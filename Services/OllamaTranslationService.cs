using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SnapNoteStudio.Services;

public static class OllamaTranslationService
{
    // No client-wide timeout: translation duration depends entirely on the user's
    // hardware/model, so callers control the deadline via the CancellationToken instead.
    private static readonly HttpClient HttpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task<string> TranslateAsync(
        string text, string targetLanguage, string endpoint, string model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("Ollama endpoint is not configured.");

        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("Ollama model is not configured.");

        var prompt =
            $"Translate the following text to {targetLanguage}. " +
            "Preserve the exact paragraph structure: keep blank lines between paragraphs, and keep each " +
            "bulleted or numbered list item on its own line exactly as in the original. Do not merge " +
            "paragraphs or remove blank lines. " +
            "Output only the translated text, with no explanation, notes, or quotation marks.\n\n" +
            text;

        var requestBody = JsonSerializer.Serialize(new
        {
            model,
            prompt,
            stream = false,
            // Hybrid reasoning models (qwen3, gpt-oss, etc.) can spend minutes generating a
            // hidden "thinking" trace before the actual answer; skip it for a plain translation.
            think = false,
            // Keep formatting (blank lines, list breaks) consistent across runs instead of
            // varying with the model's default sampling temperature.
            options = new { temperature = 0 }
        });

        using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        var url = endpoint.TrimEnd('/') + "/api/generate";

        HttpResponseMessage response;
        try
        {
            response = await HttpClient.PostAsync(url, content, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not reach Ollama at {endpoint}. Is it running? ({ex.Message})", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidOperationException($"Model \"{model}\" was not found on the Ollama server. Pull it first with: ollama pull {model}");

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);

            return doc.RootElement.TryGetProperty("response", out var responseProp)
                ? responseProp.GetString()?.Trim() ?? string.Empty
                : string.Empty;
        }
    }

    public static async Task<List<string>> GetAvailableModelsAsync(string endpoint)
    {
        var models = new List<string>();

        if (string.IsNullOrWhiteSpace(endpoint))
            return models;

        try
        {
            var url = endpoint.TrimEnd('/') + "/api/tags";
            using var response = await HttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return models;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("models", out var modelsArray))
            {
                foreach (var item in modelsArray.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameProp) && nameProp.GetString() is string name)
                        models.Add(name);
                }
            }
        }
        catch
        {
            // Ollama server not reachable; caller treats an empty list as "no models found".
        }

        return models;
    }
}
