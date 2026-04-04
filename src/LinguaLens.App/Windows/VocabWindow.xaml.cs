using System.Windows;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;
using LinguaLens.App.ViewModels;
using LinguaLens.Core.Models;

namespace LinguaLens.App.Windows;

public partial class VocabWindow : Window
{
    private readonly VocabViewModel _viewModel;

    public VocabWindow(VocabViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        LangFilter.ItemsSource = new[] { "Все", "en", "es" };
        LangFilter.SelectedIndex = 0;

        Loaded += async (_, _) => await _viewModel.LoadAsync();

        LangFilter.SelectionChanged += async (_, _) =>
        {
            _viewModel.LangFilter = LangFilter.SelectedIndex == 0 ? null : LangFilter.SelectedItem?.ToString();
            await _viewModel.LoadAsync();
        };

        SearchBox.TextChanged += async (_, _) =>
        {
            _viewModel.SearchText = SearchBox.Text;
            await _viewModel.LoadAsync();
        };

        ExportCsvButton.Click += async (_, _) =>
        {
            var dlg = new WpfSaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "lingualens_vocab.csv" };
            if (dlg.ShowDialog(this) == true)
                await _viewModel.ExportCsvAsync(dlg.FileName);
        };

        ExportAnkiButton.Click += async (_, _) =>
        {
            var dlg = new WpfSaveFileDialog { Filter = "Anki package (*.apkg)|*.apkg", FileName = "lingualens_vocab.apkg" };
            if (dlg.ShowDialog(this) == true)
                await _viewModel.ExportAnkiAsync(dlg.FileName);
        };

        DeleteButton.Click += async (_, _) =>
        {
            var selected = VocabGrid.SelectedItems.Cast<VocabEntry>().ToList();
            if (selected.Count == 0) return;
            await _viewModel.DeleteSelectedAsync(selected);
        };

        VocabGrid.ItemsSource = _viewModel.Entries;
    }
}
