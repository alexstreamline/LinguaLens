using System.Windows;
using LinguaLens.App.ViewModels;

namespace LinguaLens.App.Windows;

public partial class VocabWindow : Window
{
    private readonly VocabViewModel _viewModel;

    public VocabWindow(VocabViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        throw new NotImplementedException();
    }
}
