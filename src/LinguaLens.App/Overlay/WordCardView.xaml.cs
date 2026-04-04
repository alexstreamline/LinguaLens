using WpfUserControl = System.Windows.Controls.UserControl;
using System.Windows;
using LinguaLens.App.ViewModels;

namespace LinguaLens.App.Overlay;

public partial class WordCardView : WpfUserControl
{
    public WordCardView()
    {
        InitializeComponent();
        CopyButton.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(TranslationText.Text))
                System.Windows.Clipboard.SetText(TranslationText.Text);
        };
    }

    public void Bind(WordCardViewModel viewModel)
    {
        WordText.Text = viewModel.Word;
        PosText.Text = viewModel.Pos;
        PosBadge.Visibility = string.IsNullOrWhiteSpace(viewModel.Pos)
            ? Visibility.Collapsed : Visibility.Visible;

        TranscriptionText.Text = string.IsNullOrWhiteSpace(viewModel.Transcription)
            ? "" : $"[{viewModel.Transcription}]";
        TranscriptionText.Visibility = string.IsNullOrWhiteSpace(viewModel.Transcription)
            ? Visibility.Collapsed : Visibility.Visible;

        TranslationText.Text = viewModel.Translation;

        CommentText.Text = viewModel.Comment;
        CommentText.Visibility = viewModel.HasComment ? Visibility.Visible : Visibility.Collapsed;

        ExamplesList.ItemsSource = viewModel.Examples;
        ExamplesList.Visibility = viewModel.Examples.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
