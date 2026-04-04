using WpfPoint = System.Windows.Point;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using LinguaLens.App.ViewModels;
using LinguaLens.Core.Models;

namespace LinguaLens.App.Overlay;

/// <summary>
/// Transparent topmost popup window for displaying translation results.
/// Positioned at cursor + (16px right, 8px down) with edge detection.
/// FadeIn 150ms, FadeOut 100ms. Hidden on MouseLeave (300ms delay) or Escape.
/// Shows loading state while LLM request is in flight.
/// </summary>
public partial class OverlayWindow : Window
{
    private System.Threading.Timer? _hideTimer;

    public OverlayWindow()
    {
        InitializeComponent();
        MouseEnter += (_, _) =>
        {
            _hideTimer?.Dispose();
            _hideTimer = null;
        };
        MouseLeave += (_, _) => ScheduleHide();
    }

    public void ShowAtPoint(WpfPoint screenPoint)
    {
        const double estimatedWidth = 340;
        const double estimatedHeight = 220;

        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        double left = screenPoint.X + 16;
        double top = screenPoint.Y + 8;

        if (left + estimatedWidth > screenWidth) left = screenPoint.X - estimatedWidth - 4;
        if (top + estimatedHeight > screenHeight) top = screenPoint.Y - estimatedHeight - 4;

        Left = Math.Max(0, left);
        Top = Math.Max(0, top);

        if (Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }
    }

    public void ShowLoading()
    {
        LoadingText.Visibility = Visibility.Visible;
        WordCard.Visibility = Visibility.Collapsed;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    public void ShowResult(TranslationResult result)
    {
        var vm = WordCardViewModel.FromResult(result, contextSentence: "", sourceApp: "");
        WordCard.Bind(vm);
        LoadingText.Visibility = Visibility.Collapsed;
        WordCard.Visibility = Visibility.Visible;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    public void ShowError(string message)
    {
        ErrorText.Text = message;
        LoadingText.Visibility = Visibility.Collapsed;
        WordCard.Visibility = Visibility.Collapsed;
        ErrorText.Visibility = Visibility.Visible;
    }

    public void HideOverlay()
    {
        _hideTimer?.Dispose();
        _hideTimer = null;

        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
        fadeOut.Completed += (_, _) => Visibility = Visibility.Collapsed;
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ScheduleHide()
    {
        _hideTimer?.Dispose();
        _hideTimer = new System.Threading.Timer(_ =>
        {
            _hideTimer = null;
            Dispatcher.InvokeAsync(HideOverlay);
        }, null, 300, Timeout.Infinite);
    }

    protected override void OnKeyDown(WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            HideOverlay();
        base.OnKeyDown(e);
    }
}
