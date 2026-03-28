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

public record SentenceTranslationResult(string Translation, string Comment);

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
