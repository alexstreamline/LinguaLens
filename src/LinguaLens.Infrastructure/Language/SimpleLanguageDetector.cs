using LinguaLens.Core.Interfaces;

namespace LinguaLens.Infrastructure.Language;

/// <summary>
/// Detects "en" or "es" based on character set heuristics. Returns null for non-latin.
/// Ignores words shorter than 2 chars and pure numbers.
/// </summary>
public class SimpleLanguageDetector : ILanguageDetector
{
    private static readonly HashSet<char> SpanishChars = new("ñáéíóúüÁÉÍÓÚÜÑ¿¡");

    public string? Detect(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 2)
            return null;

        // Ignore pure numbers
        if (word.All(c => char.IsDigit(c) || c == '.' || c == ','))
            return null;

        // Must contain at least one letter
        if (!word.Any(char.IsLetter))
            return null;

        // Check for Spanish-specific characters
        if (word.Any(c => SpanishChars.Contains(c)))
            return "es";

        // All letters must be latin (a-z, A-Z, plus apostrophe/hyphen allowed)
        foreach (var c in word)
        {
            if (char.IsLetter(c) && (c < 'A' || (c > 'Z' && c < 'a') || c > 'z'))
                return null; // non-latin letter
        }

        return "en";
    }
}
