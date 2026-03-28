using LinguaLens.Core.Models;

namespace LinguaLens.Core.Interfaces;

public interface ITranslationCache
{
    Task<TranslationResult?> GetAsync(string cacheKey);
    Task SetAsync(string cacheKey, TranslationResult result);
    string BuildKey(string lang, string word, string sentence);
}
