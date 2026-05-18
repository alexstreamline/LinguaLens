using System.ComponentModel;
using System.Runtime.CompilerServices;
using LinguaLens.Core.Models;

namespace LinguaLens.App.ViewModels;

/// <summary>
/// Один выровненный фрагмент в SentenceCard. Знает свой индекс, исходный и
/// переведённый текст, и три булевых свойства состояния (IsHovered / IsSelected /
/// IsInPickingMode) — XAML биндится на них для смены фона/жирности.
/// </summary>
public sealed class AlignedPairViewModel : INotifyPropertyChanged
{
    public int Index { get; }
    public string Original { get; }
    public string Translation { get; }

    private bool _isHovered;
    public bool IsHovered
    {
        get => _isHovered;
        internal set { if (_isHovered != value) { _isHovered = value; OnPropertyChanged(); } }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        internal set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    private bool _isInPickingMode;
    public bool IsInPickingMode
    {
        get => _isInPickingMode;
        internal set { if (_isInPickingMode != value) { _isInPickingMode = value; OnPropertyChanged(); } }
    }

    public AlignedPairViewModel(AlignedPair pair, int index)
    {
        Index = index;
        Original = pair.Original;
        Translation = pair.Translation;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
