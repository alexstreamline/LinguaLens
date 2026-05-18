using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.App.ViewModels;

public class WordCardViewModel : INotifyPropertyChanged
{
    private readonly IVocabRepository _vocab;
    private readonly TranslationResult _result;
    private readonly string _contextSentence;
    private DispatcherTimer? _savedResetTimer;

    public string Word { get; }
    public string Pos { get; }
    public string PosUpper { get; }
    public bool HasPos { get; }
    public string Transcription { get; }
    public bool HasTranscription { get; }
    public string LangCode { get; }
    public string HintLabel { get; }
    public string Definition { get; }
    public bool HasDefinition { get; }
    public string Translation { get; }
    public string Comment { get; }
    public bool HasComment { get; }
    public string SynonymsLine { get; }
    public bool HasSynonyms { get; }
    public bool HasExamples { get; }
    public string ExamplesHeader { get; }
    public IReadOnlyList<ExamplePair> Examples { get; }

    private bool _isSaved;
    public bool IsSaved
    {
        get => _isSaved;
        private set
        {
            if (_isSaved == value) return;
            _isSaved = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SaveButtonLabel));
        }
    }

    public string SaveButtonLabel => IsSaved ? "✓ Сохранено" : "＋ Сохранить";

    public ICommand SaveToVocabCommand { get; }
    public ICommand TranslateSentenceCommand { get; }

    public event EventHandler<string>? TranslateSentenceRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public WordCardViewModel(TranslationResult result, IVocabRepository vocab, IAppSettings settings,
                             string contextSentence = "")
    {
        _result = result;
        _vocab = vocab;
        _contextSentence = contextSentence;

        Word = result.Word;
        Pos = result.Pos ?? "";
        PosUpper = Pos.ToUpperInvariant();
        HasPos = !string.IsNullOrEmpty(Pos);
        Transcription = result.Transcription ?? "";
        HasTranscription = !string.IsNullOrEmpty(Transcription);
        LangCode = (result.DetectedLang ?? "en").ToUpperInvariant();
        HintLabel = $"WORD MODE · {LangCode}";
        Definition = result.Definition ?? "";
        HasDefinition = !string.IsNullOrEmpty(Definition);
        Translation = result.Translation;
        Comment = result.Comment ?? "";
        HasComment = !string.IsNullOrEmpty(Comment);
        var synonyms = result.Synonyms ?? Array.Empty<string>();
        SynonymsLine = string.Join(", ", synonyms);
        HasSynonyms = synonyms.Count > 0;
        Examples = result.Examples ?? Array.Empty<ExamplePair>();
        HasExamples = Examples.Count > 0;
        ExamplesHeader = HasExamples ? $"Примеры ({Examples.Count})" : "Примеры";

        SaveToVocabCommand = new RelayCommand(SaveToVocab, () => !IsSaved);
        TranslateSentenceCommand = new RelayCommand(() =>
            TranslateSentenceRequested?.Invoke(this, _contextSentence));
    }

    private void SaveToVocab()
    {
        // Fire-and-forget save; UI feedback не должен ждать ответа БД.
        _ = _vocab.SaveAsync(_result, "", "manual");

        IsSaved = true;
        _savedResetTimer?.Stop();
        _savedResetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _savedResetTimer.Tick += (_, _) =>
        {
            _savedResetTimer?.Stop();
            _savedResetTimer = null;
            IsSaved = false;
        };
        _savedResetTimer.Start();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
