using LinguaLens.Core.Models;

namespace LinguaLens.Core.Interfaces;

public interface ILlmClient
{
    Task<TranslationResult> TranslateWordAsync(string word, string sentence, string lang, CancellationToken ct);
    Task<SentenceTranslationResult> TranslateSentenceAsync(string text, string lang, CancellationToken ct);
}
