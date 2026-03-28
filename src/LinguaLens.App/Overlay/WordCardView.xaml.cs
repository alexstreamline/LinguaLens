using WpfUserControl = System.Windows.Controls.UserControl;
using LinguaLens.App.ViewModels;

namespace LinguaLens.App.Overlay;

public partial class WordCardView : WpfUserControl
{
    public WordCardView()
    {
        InitializeComponent();
    }

    public void Bind(WordCardViewModel viewModel)
    {
        throw new NotImplementedException();
    }
}
