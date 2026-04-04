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

    public async Task LoadAsync()
    {
        var all = await _repository.GetAllAsync(_langFilter);

        var filtered = string.IsNullOrWhiteSpace(_searchText)
            ? all
            : all.Where(e =>
                e.Word.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                e.Translation.Contains(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();

        Entries.Clear();
        foreach (var entry in filtered)
            Entries.Add(entry);
    }

    public async Task DeleteSelectedAsync(IReadOnlyList<VocabEntry> selected)
    {
        foreach (var entry in selected)
            await _repository.DeleteAsync(entry.Id);
        await LoadAsync();
    }

    public Task ExportCsvAsync(string filePath)
        => _exporter.ExportCsvAsync(Entries.ToList(), filePath);

    public Task ExportAnkiAsync(string filePath)
        => _exporter.ExportAnkiAsync(Entries.ToList(), filePath);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
