using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.Llm;

/// <summary>
/// Calls Google Gemini API for translation.
/// Base URL: https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent
/// Auth via ?key={apiKey} query param.
/// </summary>
public class GeminiLlmClient : ILlmClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

    private readonly HttpClient _httpClient;

    public GeminiLlmClient(HttpClient httpClient)
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
