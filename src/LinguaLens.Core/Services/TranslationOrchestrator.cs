using System.Windows;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaLens.Core.Services;

public class TranslationOrchestrator
{
    private readonly ITextExtractor _extractor;
    private readonly ILanguageDetector _detector;
    private readonly ITranslationCache _cache;
    private readonly ILlmClientFactory _factory;
    private readonly IVocabRepository _vocab;
    private readonly IAppSettings _settings;
    private readonly ILogger<TranslationOrchestrator> _logger;

    public TranslationOrchestrator(
        ITextExtractor extractor,
        ILanguageDetector detector,
        ITranslationCache cache,
        ILlmClientFactory factory,
        IVocabRepository vocab,
        IAppSettings settings,
        ILogger<TranslationOrchestrator>? logger = null)
    {
        _extractor = extractor;
        _detector = detector;
        _cache = cache;
        _factory = factory;
        _vocab = vocab;
        _settings = settings;
        _logger = logger ?? NullLogger<TranslationOrchestrator>.Instance;
    }

    /// <summary>
    /// Fast pre-check: extracts word at point and validates language settings.
    /// Returns null if nothing to translate (no word, unsupported lang, disabled lang).
    /// Does NOT call LLM or cache.
    /// </summary>
    public async Task<WordExtractionResult?> ExtractAsync(Point screenPoint)
    {
        try
        {
            var extraction = await _extractor.ExtractWordAtPointAsync(screenPoint);
            if (extraction is null || string.IsNullOrWhiteSpace(extraction.Word)) return null;
            var lang = _detector.Detect(extraction.Word);
            if (lang is null) return null;
            if (lang == "en" && !_settings.DetectEnglish) return null;
            if (lang == "es" && !_settings.DetectSpanish) return null;
            return extraction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting word at {Point}", screenPoint);
            return null;
        }
    }

    /// <summary>Called by DebounceController after debounce fires on mouse hover.</summary>
    /// <param name="extracted">Pre-extracted word from ExtractAsync to avoid double UIA call.</param>
    public async Task<TranslationResult?> ProcessHoverAsync(Point screenPoint, CancellationToken ct,
        WordExtractionResult? extracted = null)
    {
        try
        {
            var extraction = extracted ?? await _extractor.ExtractWordAtPointAsync(screenPoint);
            if (extraction is null || string.IsNullOrWhiteSpace(extraction.Word))
                return null;

            var lang = _detector.Detect(extraction.Word);
            if (lang is null)
                return null;

            if (lang == "en" && !_settings.DetectEnglish)
                return null;
            if (lang == "es" && !_settings.DetectSpanish)
                return null;

            ct.ThrowIfCancellationRequested();

            var cacheKey = _cache.BuildKey(lang, extraction.Word, extraction.Sentence);
            var cached = await _cache.GetAsync(cacheKey);
            if (cached is not null)
                return cached;

            ct.ThrowIfCancellationRequested();

            var result = await _factory.Create().TranslateWordAsync(
                extraction.Word, extraction.Sentence, lang, ct);

            _ = _cache.SetAsync(cacheKey, result);

            if (_settings.AutoSaveToVocab)
                _ = _vocab.SaveAsync(result, extraction.Sentence, extraction.SourceApp);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing hover translation at {Point}", screenPoint);
            return null;
        }
    }

    /// <summary>Called when user triggers selection/sentence mode.</summary>
    /// <param name="preExtracted">Already-read selection text — pass to avoid re-reading after focus changes.</param>
    public async Task<SentenceTranslationResult?> ProcessSelectionAsync(CancellationToken ct,
        string? preExtracted = null)
    {
        try
        {
            var text = preExtracted ?? await _extractor.ExtractSelectedTextAsync();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            ct.ThrowIfCancellationRequested();

            var firstWord = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? "";
            var lang = _detector.Detect(firstWord) ?? "en";

            return await _factory.Create().TranslateSentenceAsync(text, lang, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing selection translation");
            return null;
        }
    }
}
