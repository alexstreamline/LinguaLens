using System.Text;
using System.Text.Json;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.Llm;

/// <summary>
/// Calls Google Gemini API for translation.
/// Auth via ?key={apiKey} query param (read from IAppSettings per-request).
/// Base URL: https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent
/// </summary>
public class GeminiLlmClient : ILlmClient
{
    private const string BaseUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";
    private const string ModelName = "gemini-2.0-flash";

    private readonly HttpClient _http;
    private readonly IAppSettings _settings;
    private readonly ITokenUsageRepository? _usageRepo;

    public GeminiLlmClient(HttpClient http, IAppSettings settings, ITokenUsageRepository? usageRepo = null)
    {
        _http = http;
        _settings = settings;
        _usageRepo = usageRepo;
    }

    public async Task<TranslationResult> TranslateWordAsync(
        string word, string sentence, string lang, CancellationToken ct)
    {
        var prompt = LlmPrompts.WordTranslation(word, sentence, lang);
        var (raw, pt, ct2) = await SendAsync(prompt, ct);
        if (_usageRepo != null) _ = _usageRepo.RecordAsync(pt, ct2, "gemini", ModelName, "word");
        return ParseWordResult(raw);
    }

    public async Task<SentenceTranslationResult> TranslateSentenceAsync(
        string text, string lang, CancellationToken ct)
    {
        var prompt = LlmPrompts.SentenceTranslation(text, lang);
        var (raw, pt, ct2) = await SendAsync(prompt, ct);
        if (_usageRepo != null) _ = _usageRepo.RecordAsync(pt, ct2, "gemini", ModelName, "sentence");
        return ParseSentenceResult(raw, lang);
    }

    private async Task<(string Content, int PromptTokens, int CompletionTokens)> SendAsync(
        string prompt, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var url = $"{BaseUrl}?key={_settings.ApiKey}";
        var body = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        });

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content, cts.Token);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
        using var doc = JsonDocument.Parse(responseBody);

        var textContent = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";

        int promptTokens = 0, completionTokens = 0;
        if (doc.RootElement.TryGetProperty("usageMetadata", out var usage))
        {
            if (usage.TryGetProperty("promptTokenCount", out var pt)) promptTokens = pt.GetInt32();
            if (usage.TryGetProperty("candidatesTokenCount", out var ct2)) completionTokens = ct2.GetInt32();
        }

        return (textContent, promptTokens, completionTokens);
    }

    private static TranslationResult ParseWordResult(string raw)
    {
        var json = StripMarkdownFences(raw);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var examples = root.GetProperty("examples")
                .EnumerateArray()
                .Select(e => new ExamplePair(
                    e.GetProperty("original").GetString() ?? "",
                    e.GetProperty("translation").GetString() ?? ""))
                .ToList();

            var synonyms = new List<string>();
            if (root.TryGetProperty("synonyms", out var synEl) && synEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in synEl.EnumerateArray())
                {
                    var str = s.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) synonyms.Add(str);
                }
            }

            var definition = root.TryGetProperty("definition", out var defEl)
                ? defEl.GetString() ?? "" : "";

            return new TranslationResult(
                Word:          root.GetProperty("word").GetString() ?? "",
                DetectedLang:  root.GetProperty("detected_lang").GetString() ?? "",
                Pos:           root.GetProperty("pos").GetString() ?? "",
                Transcription: root.GetProperty("transcription").GetString() ?? "",
                Translation:   root.GetProperty("translation").GetString() ?? "",
                Comment:       root.GetProperty("comment").GetString() ?? "",
                Examples:      examples,
                Definition:    definition,
                Synonyms:      synonyms);
        }
        catch (Exception ex) when (ex is not TranslationParseException)
        {
            throw new TranslationParseException(raw, ex);
        }
    }

    private static SentenceTranslationResult ParseSentenceResult(string raw, string lang)
    {
        var json = StripMarkdownFences(raw);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var pairs = new List<AlignedPair>();
            if (root.TryGetProperty("pairs", out var pairsEl) && pairsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in pairsEl.EnumerateArray())
                {
                    var original    = p.TryGetProperty("original",    out var o) ? o.GetString() ?? "" : "";
                    var translation = p.TryGetProperty("translation", out var t) ? t.GetString() ?? "" : "";
                    if (original.Length > 0 || translation.Length > 0)
                        pairs.Add(new AlignedPair(original, translation));
                }
            }

            return new SentenceTranslationResult(
                Translation:  root.GetProperty("translation").GetString() ?? "",
                Comment:      root.GetProperty("comment").GetString() ?? "",
                Pairs:        pairs,
                DetectedLang: lang);
        }
        catch (Exception ex) when (ex is not TranslationParseException)
        {
            throw new TranslationParseException(raw, ex);
        }
    }

    private static string StripMarkdownFences(string raw)
    {
        var s = raw.Trim();
        if (!s.StartsWith("```")) return s;
        var start = s.IndexOf('\n') + 1;
        var end = s.LastIndexOf("```");
        return end > start ? s[start..end].Trim() : s;
    }
}
