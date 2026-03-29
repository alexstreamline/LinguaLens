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
    public async Task<TranslationResult?> ProcessHoverAsync(Point screenPoint, CancellationToken ct)
    {
        var extraction = await extractor.ExtractWordAtPointAsync(screenPoint);
        if (extraction is null || string.IsNullOrWhiteSpace(extraction.Word))
            return null;

        var lang = detector.Detect(extraction.Word);
        if (lang is null)
            return null;

        if (lang == "en" && !settings.DetectEnglish)
            return null;
        if (lang == "es" && !settings.DetectSpanish)
            return null;

        ct.ThrowIfCancellationRequested();

        var cacheKey = cache.BuildKey(lang, extraction.Word, extraction.Sentence);
        var cached = await cache.GetAsync(cacheKey);
        if (cached is not null)
            return cached;

        ct.ThrowIfCancellationRequested();

        var result = await llmClient.TranslateWordAsync(extraction.Word, extraction.Sentence, lang, ct);

        await cache.SetAsync(cacheKey, result);

        if (settings.AutoSaveToVocab)
            await vocab.SaveAsync(result, extraction.Sentence, extraction.SourceApp);

        return result;
    }

    /// <summary>Called when user triggers selection/sentence mode.</summary>
    public async Task<SentenceTranslationResult?> ProcessSelectionAsync(CancellationToken ct)
    {
        var text = await extractor.ExtractSelectedTextAsync();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        ct.ThrowIfCancellationRequested();

        // Detect language from the first word of the selection
        var firstWord = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "";
        var lang = detector.Detect(firstWord) ?? "en";

        return await llmClient.TranslateSentenceAsync(text, lang, ct);
    }
}
