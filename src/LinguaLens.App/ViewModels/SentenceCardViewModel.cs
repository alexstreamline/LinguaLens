using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.App.ViewModels;

public enum SentenceMode
{
    Normal,
    Picking,
    Saved
}

public class SentenceCardViewModel : INotifyPropertyChanged
{
    private readonly IVocabRepository? _vocab;
    private readonly string _detectedLang;
    private readonly string _sourceApp;
    private readonly string _contextSentence;
    private DispatcherTimer? _savedResetTimer;

    public string Translation { get; }
    public string Comment { get; }
    public bool HasComment { get; }
    public string ContextSentence { get; }
    public bool HasContextSentence => !string.IsNullOrWhiteSpace(ContextSentence);
    public IReadOnlyList<AlignedPairViewModel> Pairs { get; }
    public bool HasPairs => Pairs.Count > 0;

    /// <summary>Заголовочная строка вида "SENTENCE MODE · EN → RU".</summary>
    public string ModeLabel { get; }

    private SentenceMode _mode = SentenceMode.Normal;
    public SentenceMode Mode
    {
        get => _mode;
        private set
        {
            if (_mode == value) return;
            _mode = value;
            foreach (var p in Pairs) p.IsInPickingMode = value == SentenceMode.Picking;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNormal));
            OnPropertyChanged(nameof(IsPicking));
            OnPropertyChanged(nameof(IsSaved));
            OnPropertyChanged(nameof(HintText));
            OnPropertyChanged(nameof(HintIsSaved));
        }
    }
    public bool IsNormal  => Mode == SentenceMode.Normal;
    public bool IsPicking => Mode == SentenceMode.Picking;
    public bool IsSaved   => Mode == SentenceMode.Saved;

    public string HintText => Mode switch
    {
        SentenceMode.Picking => "кликай слова — добавятся в словарь",
        SentenceMode.Saved   => "✓ сохранено",
        _                    => HasPairs ? "наведи на слово →" : ""
    };
    public bool HintIsSaved => Mode == SentenceMode.Saved;

    private int _selectedCount;
    public int SelectedCount
    {
        get => _selectedCount;
        private set
        {
            if (_selectedCount == value) return;
            _selectedCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SaveButtonText));
        }
    }
    public string SaveButtonText => SelectedCount > 0 ? $"Сохранить {SelectedCount}" : "Сохранить";

    public ICommand StartPickingCommand { get; }
    public ICommand CancelPickingCommand { get; }
    public ICommand SaveSelectedCommand { get; }
    public ICommand TogglePairCommand { get; }

    public SentenceCardViewModel(
        SentenceTranslationResult result,
        string contextSentence = "",
        string sourceApp = "Sentence picker",
        IVocabRepository? vocab = null)
    {
        Translation = result.Translation;
        Comment = result.Comment ?? "";
        HasComment = !string.IsNullOrEmpty(Comment);
        ContextSentence = contextSentence ?? "";
        _detectedLang = result.DetectedLang;
        _contextSentence = ContextSentence;
        _sourceApp = sourceApp;
        _vocab = vocab;

        Pairs = (result.Pairs ?? Array.Empty<AlignedPair>())
            .Select((p, i) => new AlignedPairViewModel(p, i))
            .ToList();

        ModeLabel = $"SENTENCE MODE · {_detectedLang.ToUpperInvariant()} → RU";

        StartPickingCommand  = new RelayCommand(StartPicking,  () => HasPairs && Mode == SentenceMode.Normal);
        CancelPickingCommand = new RelayCommand(CancelPicking, () => Mode == SentenceMode.Picking);
        SaveSelectedCommand  = new RelayCommand(SaveSelected,  () => Mode == SentenceMode.Picking && SelectedCount > 0);
        TogglePairCommand    = new RelayCommand<AlignedPairViewModel>(TogglePair);
    }

    private void StartPicking()
    {
        ResetSelection();
        Mode = SentenceMode.Picking;
    }

    private void CancelPicking()
    {
        ResetSelection();
        Mode = SentenceMode.Normal;
    }

    private void SaveSelected()
    {
        var selected = Pairs.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0) return;

        // Fire-and-forget: сохраняем как vocab entries; ошибки тут не должны валить UI.
        if (_vocab != null)
        {
            foreach (var pair in selected)
            {
                var synthetic = new TranslationResult(
                    Word:          pair.Original,
                    DetectedLang:  _detectedLang,
                    Pos:           "",
                    Transcription: "",
                    Translation:   pair.Translation,
                    Comment:       "",
                    Examples:      Array.Empty<ExamplePair>());
                _ = _vocab.SaveAsync(synthetic, _contextSentence, _sourceApp);
            }
        }

        ResetSelection();
        Mode = SentenceMode.Saved;

        _savedResetTimer?.Stop();
        _savedResetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
        _savedResetTimer.Tick += (_, _) =>
        {
            _savedResetTimer?.Stop();
            _savedResetTimer = null;
            if (Mode == SentenceMode.Saved) Mode = SentenceMode.Normal;
        };
        _savedResetTimer.Start();
    }

    private void TogglePair(AlignedPairViewModel? pair)
    {
        if (pair is null || Mode != SentenceMode.Picking) return;
        pair.IsSelected = !pair.IsSelected;
        SelectedCount = Pairs.Count(p => p.IsSelected);
    }

    private void ResetSelection()
    {
        foreach (var p in Pairs) p.IsSelected = false;
        SelectedCount = 0;
    }

    /// <summary>Подсветка пары — вызывается из code-behind на MouseEnter/Leave Border'а.</summary>
    public void SetHovered(int? index)
    {
        for (int i = 0; i < Pairs.Count; i++)
            Pairs[i].IsHovered = index.HasValue && i == index.Value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
