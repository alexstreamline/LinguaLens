using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.App.ViewModels;

public class VocabViewModel : INotifyPropertyChanged
{
    private readonly IVocabRepository _repository;
    private readonly IVocabExporter _exporter;

    private string? _langFilter;
    private string _searchText = "";
    private bool? _isLearnedFilter;

    public ObservableCollection<VocabEntry> Entries { get; } = [];

    public string? LangFilter
    {
        get => _langFilter;
        set { _langFilter = value; OnPropertyChanged(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); }
    }

    public VocabViewModel(IVocabRepository repository, IVocabExporter exporter)
    {
        _repository = repository;
        _exporter = exporter;
    }

    public Task LoadAsync()
    {
        throw new NotImplementedException();
    }

    public Task DeleteSelectedAsync(IReadOnlyList<VocabEntry> selected)
    {
        throw new NotImplementedException();
    }

    public Task ExportCsvAsync(string filePath)
    {
        throw new NotImplementedException();
    }

    public Task ExportAnkiAsync(string filePath)
    {
        throw new NotImplementedException();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
