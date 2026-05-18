using System.Windows;

namespace LinguaLens.Core.Models;

public record WordExtractionResult(string Word, string Sentence, string SourceApp, Point ScreenPoint);

public record TranslationResult(
    string Word,
    string DetectedLang,
    string Pos,
    string Transcription,
    string Translation,
    string Comment,
    IReadOnlyList<ExamplePair> Examples);

public record ExamplePair(string Original, string Translation);

/// <summary>
/// Семантический фрагмент перевода предложения — пара original ↔ translation,
/// используется для подсветки выровненных кусков в SentenceCard.
/// </summary>
public record AlignedPair(string Original, string Translation);

public record SentenceTranslationResult(
    string Translation,
    string Comment,
    IReadOnlyList<AlignedPair>? Pairs = null,
    string DetectedLang = "en");

public record VocabEntry(
    int Id,
    string Word,
    string DetectedLang,
    string Translation,
    string Pos,
    string ContextSentence,
    string SourceApp,
    string ResponseJson,
    DateTime CreatedAt,
    bool IsLearned);

public record TokenUsageEntry(
    int Id, DateTime Timestamp, int PromptTokens, int CompletionTokens,
    string Provider, string Model, string Mode);

public record DailyUsageSummary(
    DateTime Date, int TotalTokens, int RequestCount, decimal EstimatedCostUsd);

/// <summary>
/// Распознанная строка OCR — текст + bbox в физических пикселях экрана.
/// </summary>
public record OcrLine(string Text, Rect Bounds);

/// <summary>
/// Результат OCR на регионе вокруг курсора.
/// WordAtPoint — слово, в bbox которого попала точка курсора (или null, если курсор не над словом).
/// ContextSentence — все слова из той же строки, объединённые пробелами (контекст для LLM).
/// </summary>
public record OcrResult(string? WordAtPoint, string ContextSentence, IReadOnlyList<OcrLine> Lines);
