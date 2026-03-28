using LinguaLens.Core.Interfaces;

namespace LinguaLens.Infrastructure.Language;

/// <summary>
/// Detects "en" or "es" based on character set heuristics. Returns null for non-latin.
/// Ignores words shorter than 2 chars and pure numbers.
/// </summary>
public class SimpleLanguageDetector : ILanguageDetector
{
    public string? Detect(string word)
    {
        throw new NotImplementedException();
    }
}
