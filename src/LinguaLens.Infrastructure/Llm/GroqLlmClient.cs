using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.Llm;

/// <summary>
/// Calls Groq API (OpenAI-compatible) for translation.
/// Word model: llama-3.1-8b-instant
/// Sentence model: llama-3.3-70b-versatile
/// Timeout: 10 seconds. Strips markdown fences before JSON parsing.
/// </summary>
public class GroqLlmClient : ILlmClient
{
    private const string BaseUrl = "https://api.groq.com/openai/v1/chat/completions";
    private const string WordModel = "llama-3.1-8b-instant";
    private const string SentenceModel = "llama-3.3-70b-versatile";

    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public GroqLlmClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<TranslationResult> TranslateWordAsync(string word, string sentence, string lang, CancellationToken ct)
    {
        var prompt = BuildWordPrompt(word, sentence, lang);
        var raw = await CallApiAsync(WordModel, prompt, ct);
        var stripped = StripMarkdownFences(raw);

        try
        {
            var dto = JsonSerializer.Deserialize<TranslationResultDto>(stripped, JsonOptions)
                      ?? throw new TranslationParseException(raw);
            return dto.ToModel();
        }
        catch (JsonException ex)
        {
            throw new TranslationParseException(raw, ex);
        }
    }

    public async Task<SentenceTranslationResult> TranslateSentenceAsync(string text, string lang, CancellationToken ct)
    {
        var prompt = BuildSentencePrompt(text, lang);
        var raw = await CallApiAsync(SentenceModel, prompt, ct);
        var stripped = StripMarkdownFences(raw);

        try
        {
            var dto = JsonSerializer.Deserialize<SentenceTranslationResultDto>(stripped, JsonOptions)
                      ?? throw new TranslationParseException(raw);
            return new SentenceTranslationResult(dto.Translation ?? "", dto.Comment ?? "");
        }
        catch (JsonException ex)
        {
            throw new TranslationParseException(raw, ex);
        }
    }

    private async Task<string> CallApiAsync(string model, string prompt, CancellationToken ct)
    {
        var request = new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.1,
            max_tokens = 500
        };

        using var response = await _httpClient.PostAsJsonAsync(BaseUrl, request, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }

    private static string StripMarkdownFences(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("```"))
        {
            var firstNewline = t.IndexOf('\n');
            if (firstNewline >= 0)
                t = t[(firstNewline + 1)..];
            if (t.EndsWith("```"))
                t = t[..^3].TrimEnd();
        }
        return t.Trim();
    }

    private static string BuildWordPrompt(string word, string sentence, string lang) => $$"""
        You are a language learning assistant. The user is reading text in {{lang}} and needs help understanding a word.

        Word: "{{word}}"
        Context sentence: "{{sentence}}"
        Target language for translation: Russian

        Respond with ONLY valid JSON, no markdown, no explanation:
        {
          "word": "original word",
          "detected_lang": "en|es",
          "pos": "noun|verb|adjective|adverb|other",
          "transcription": "IPA or standard transcription, EN only, empty string for ES",
          "translation": "перевод на русский (с учётом контекста)",
          "comment": "краткий комментарий: контекстный нюанс, если слово многозначное или есть ловушка",
          "examples": [
            {"original": "example sentence", "translation": "перевод примера"},
            {"original": "example sentence 2", "translation": "перевод примера 2"}
          ]
        }
        """;

    private static string BuildSentencePrompt(string text, string lang) => $$"""
        Translate the following {{lang}} text to Russian. Provide a natural translation.
        Respond with ONLY valid JSON:
        {
          "translation": "перевод на русский",
          "comment": "краткий комментарий если есть важный нюанс, иначе пустая строка"
        }

        Text: "{{text}}"
        """;

    private class TranslationResultDto
    {
        public string? Word { get; set; }
        public string? DetectedLang { get; set; }
        public string? Pos { get; set; }
        public string? Transcription { get; set; }
        public string? Translation { get; set; }
        public string? Comment { get; set; }
        public List<ExamplePairDto>? Examples { get; set; }

        public TranslationResult ToModel() => new(
            Word ?? "",
            DetectedLang ?? "",
            Pos ?? "",
            Transcription ?? "",
            Translation ?? "",
            Comment ?? "",
            Examples?.Select(e => new ExamplePair(e.Original ?? "", e.Translation ?? "")).ToList()
                ?? []);
    }

    private class ExamplePairDto
    {
        public string? Original { get; set; }
        public string? Translation { get; set; }
    }

    private class SentenceTranslationResultDto
    {
        public string? Translation { get; set; }
        public string? Comment { get; set; }
    }
}
