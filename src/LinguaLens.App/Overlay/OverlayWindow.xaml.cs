using WpfPoint = System.Windows.Point;
using System.Windows;
using System.Windows.Input;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using LinguaLens.Core.Models;

namespace LinguaLens.App.Overlay;

/// <summary>
/// Transparent topmost popup window for displaying translation results.
/// Positioned at cursor + (16px right, 8px down) with edge detection.
/// FadeIn 150ms, FadeOut 100ms. Hidden on MouseLeave (300ms delay) or Escape.
/// Shows loading shimmer while LLM request is in flight.
/// </summary>
public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
    }

    public void ShowAtPoint(WpfPoint screenPoint)
    {
        throw new NotImplementedException();
    }

    public void ShowLoading()
    {
        throw new NotImplementedException();
    }

    public void ShowResult(TranslationResult result)
    {
        throw new NotImplementedException();
    }

    public void ShowError(string message)
    {
        throw new NotImplementedException();
    }

    public void HideOverlay()
    {
        throw new NotImplementedException();
    }

    protected override void OnKeyDown(WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            HideOverlay();
        base.OnKeyDown(e);
    }
}
