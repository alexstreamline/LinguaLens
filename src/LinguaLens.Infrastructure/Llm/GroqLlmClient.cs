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

    public GroqLlmClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<TranslationResult> TranslateWordAsync(string word, string sentence, string lang, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<SentenceTranslationResult> TranslateSentenceAsync(string text, string lang, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
