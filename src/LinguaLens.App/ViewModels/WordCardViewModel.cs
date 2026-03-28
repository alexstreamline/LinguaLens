using System.ComponentModel;
using System.Runtime.CompilerServices;
using LinguaLens.Core.Models;

namespace LinguaLens.App.ViewModels;

public class WordCardViewModel : INotifyPropertyChanged
{
    public string Word { get; init; } = "";
    public string Pos { get; init; } = "";
    public string Transcription { get; init; } = "";
    public string DetectedLang { get; init; } = "";
    public string Translation { get; init; } = "";
    public string Comment { get; init; } = "";
    public IReadOnlyList<ExamplePair> Examples { get; init; } = [];
    public string ContextSentence { get; init; } = "";
    public string SourceApp { get; init; } = "";

    public bool HasComment => !string.IsNullOrWhiteSpace(Comment);

    public static WordCardViewModel FromResult(TranslationResult result, string contextSentence, string sourceApp)
        => new()
        {
            Word = result.Word,
            Pos = result.Pos,
            Transcription = result.Transcription,
            DetectedLang = result.DetectedLang,
            Translation = result.Translation,
            Comment = result.Comment,
            Examples = result.Examples,
            ContextSentence = contextSentence,
            SourceApp = sourceApp
        };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
