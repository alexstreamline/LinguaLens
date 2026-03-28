using System.Windows;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Core.Services;

public class TranslationOrchestrator(
    ITextExtractor extractor,
    ILanguageDetector detector,
    ITranslationCache cache,
    ILlmClient llmClient,
    IVocabRepository vocab,
    IAppSettings settings)
{
    /// <summary>Called by DebounceController after debounce fires on mouse hover.</summary>
    public Task<TranslationResult?> ProcessHoverAsync(Point screenPoint, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    /// <summary>Called when user triggers selection/sentence mode.</summary>
    public Task<SentenceTranslationResult?> ProcessSelectionAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
