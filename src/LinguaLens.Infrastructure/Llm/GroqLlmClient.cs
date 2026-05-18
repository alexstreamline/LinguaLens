using System.Text;
using System.Text.Json;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.Llm;

/// <summary>
/// Calls Groq API (OpenAI-compatible) for translation.
/// Word model: llama-3.1-8b-instant
/// Sentence model: llama-3.3-70b-versatile
/// Per-request timeout: 10 seconds via linked CancellationTokenSource.
/// </summary>
public class GroqLlmClient : ILlmClient
{
    private const string BaseUrl = "https://api.groq.com/openai/v1/chat/completions";
    private const string WordModel = "llama-3.1-8b-instant";
    private const string SentenceModel = "llama-3.3-70b-versatile";

    private readonly HttpClient _http;
    private readonly ITokenUsageRepository? _usageRepo;

    public GroqLlmClient(HttpClient http, ITokenUsageRepository? usageRepo = null)
    {
        _http = http;
        _usageRepo = usageRepo;
    }

    public async Task<TranslationResult> TranslateWordAsync(
        string word, string sentence, string lang, CancellationToken ct)
    {
        var prompt = LlmPrompts.WordTranslation(word, sentence, lang);
        var (raw, pt, ct2) = await SendAsync(WordModel, prompt, ct);
        if (_usageRepo != null) _ = _usageRepo.RecordAsync(pt, ct2, "groq", WordModel, "word");
        return ParseWordResult(raw);
    }

    public async Task<SentenceTranslationResult> TranslateSentenceAsync(
        string text, string lang, CancellationToken ct)
    {
        var prompt = LlmPrompts.SentenceTranslation(text, lang);
        var (raw, pt, ct2) = await SendAsync(SentenceModel, prompt, ct);
        if (_usageRepo != null) _ = _usageRepo.RecordAsync(pt, ct2, "groq", SentenceModel, "sentence");
        return ParseSentenceResult(raw, lang);
    }

    private async Task<(string Content, int PromptTokens, int CompletionTokens)> SendAsync(
        string model, string prompt, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var body = JsonSerializer.Serialize(new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.1,
            max_tokens = 600
        });

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(BaseUrl, content, cts.Token);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
        using var doc = JsonDocument.Parse(responseBody);

        var contentStr = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        if (string.IsNullOrEmpty(contentStr))
            throw new TranslationParseException("");

        int promptTokens = 0, completionTokens = 0;
        if (doc.RootElement.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt)) promptTokens = pt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var ct2)) completionTokens = ct2.GetInt32();
        }

        return (contentStr, promptTokens, completionTokens);
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

            return new TranslationResult(
                Word:          root.GetProperty("word").GetString() ?? "",
                DetectedLang:  root.GetProperty("detected_lang").GetString() ?? "",
                Pos:           root.GetProperty("pos").GetString() ?? "",
                Transcription: root.GetProperty("transcription").GetString() ?? "",
                Translation:   root.GetProperty("translation").GetString() ?? "",
                Comment:       root.GetProperty("comment").GetString() ?? "",
                Examples:      examples);
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
