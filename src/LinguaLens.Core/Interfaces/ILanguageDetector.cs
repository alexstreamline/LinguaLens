namespace LinguaLens.Core.Interfaces;

public interface ILanguageDetector
{
    /// <summary>Returns "en", "es", or null if not a supported language.</summary>
    string? Detect(string word);
}
