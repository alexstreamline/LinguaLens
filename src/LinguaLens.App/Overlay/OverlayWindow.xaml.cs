using WpfPoint = System.Windows.Point;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using LinguaLens.App.ViewModels;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.App.Overlay;

public partial class OverlayWindow : Window
{
    private readonly IVocabRepository _vocab;
    private readonly IAppSettings _settings;
    private System.Threading.Timer? _hideTimer;
    private WordCardViewModel? _currentVm;
    private WpfPoint _lastPhysicalCursor;

    public event EventHandler<string>? TranslateSentenceRequested;
    public event EventHandler? RetryRequested;
    public event EventHandler? OpenSettingsRequested;

    public OverlayWindow(IVocabRepository vocab, IAppSettings settings)
    {
        _vocab = vocab;
        _settings = settings;
        InitializeComponent();

        MouseEnter += (_, _) => { _hideTimer?.Dispose(); _hideTimer = null; };
        MouseLeave += (_, _) => ScheduleHide();

        CloseButton.Click += (_, _) => HideOverlay();
        RetryButton.Click += (_, _) => RetryRequested?.Invoke(this, EventArgs.Empty);
        SettingsButton.Click += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    // physicalCursorPos is in raw Win32 physical pixels (from WH_MOUSE_LL hook)
    public void ShowAtPoint(WpfPoint physicalCursorPos)
    {
        _lastPhysicalCursor = physicalCursorPos;

        if (Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
            Opacity = 0;
        }
        UpdateLayout();

        RepositionWindow();
    }

    /// <summary>Reposition только если окно вылезло за пределы рабочей области экрана.</summary>
    private void RepositionIfOffscreen()
    {
        var source = PresentationSource.FromVisual(this);
        var scaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        var scaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;
        var physPoint = new System.Drawing.Point((int)_lastPhysicalCursor.X, (int)_lastPhysicalCursor.Y);
        var screen = System.Windows.Forms.Screen.FromPoint(physPoint);
        var wb = screen.WorkingArea;
        double wRight  = wb.Right  * scaleX;
        double wBottom = wb.Bottom * scaleY;
        if (Left + ActualWidth > wRight || Top + ActualHeight > wBottom)
            RepositionWindow();
    }

    private void RepositionWindow()
    {
        // WPF Window.Left/Top are in DIPs; hook coords are physical px — must convert
        var source = PresentationSource.FromVisual(this);
        var scaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        var scaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        double dipX = _lastPhysicalCursor.X * scaleX;
        double dipY = _lastPhysicalCursor.Y * scaleY;

        // WorkingArea is in physical px — convert to DIPs for consistent comparison
        var physPoint = new System.Drawing.Point((int)_lastPhysicalCursor.X, (int)_lastPhysicalCursor.Y);
        var screen = System.Windows.Forms.Screen.FromPoint(physPoint);
        var wb = screen.WorkingArea;
        double wLeft   = wb.Left   * scaleX;
        double wTop    = wb.Top    * scaleY;
        double wRight  = wb.Right  * scaleX;
        double wBottom = wb.Bottom * scaleY;

        double left = dipX + 16;
        double top  = dipY + 8;

        if (left + ActualWidth > wRight)
            left = dipX - ActualWidth - 8;
        if (top + ActualHeight > wBottom)
            top = dipY - ActualHeight - 8;

        Left = Math.Max(wLeft, left);
        Top  = Math.Max(wTop, top);
    }

    public void ShowLoading(string? statusOverride = null)
    {
        // Если передали явный статус — используем его, иначе дефолт «Перевожу… (model)».
        if (statusOverride is not null)
        {
            LoadingStatusText.Text = statusOverride;
        }
        else
        {
            var provider = _settings.LlmProvider ?? "groq";
            var modelLabel = provider == "gemini" ? "Gemini · gemini-2.0-flash" : "Groq · llama-3.1-8b";
            LoadingStatusText.Text = $"Перевожу… ({modelLabel})";
        }

        Width = 320;
        RestoreNormalBorder();
        LoadingPanel.Visibility = Visibility.Visible;
        WordCard.Visibility = Visibility.Collapsed;
        SentenceCard.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        FadeIn();
    }

    public void ShowResult(TranslationResult result, string contextSentence = "")
    {
        if (_currentVm != null)
            _currentVm.TranslateSentenceRequested -= OnTranslateSentenceRequested;

        _currentVm = new WordCardViewModel(result, _vocab, _settings, contextSentence);
        _currentVm.TranslateSentenceRequested += OnTranslateSentenceRequested;

        WordCard.DataContext = _currentVm;
        Width = 420;
        RestoreNormalBorder();
        LoadingPanel.Visibility = Visibility.Collapsed;
        WordCard.Visibility = Visibility.Visible;
        SentenceCard.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        UpdateLayout();
        // Окно перемещаем только если вылезло за экран (новый Width шире чем было).
        RepositionIfOffscreen();
        FadeIn();
    }

    public void ShowSentenceResult(SentenceTranslationResult result, string contextSentence = "", string sourceApp = "Sentence picker")
    {
        SentenceCard.DataContext = new SentenceCardViewModel(result, contextSentence, sourceApp, _vocab);
        Width = 580;
        RestoreNormalBorder();
        LoadingPanel.Visibility = Visibility.Collapsed;
        WordCard.Visibility = Visibility.Collapsed;
        SentenceCard.Visibility = Visibility.Visible;
        ErrorPanel.Visibility = Visibility.Collapsed;
        UpdateLayout();
        RepositionIfOffscreen();
        FadeIn();
    }

    public void ShowError()
    {
        Width = 320;
        // Перекрашиваем рамку всей карточки в BadBrush — соответствие дизайну StateError.
        RootBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "BadBrush");
        LoadingPanel.Visibility = Visibility.Collapsed;
        WordCard.Visibility = Visibility.Collapsed;
        SentenceCard.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        UpdateLayout();
        RepositionIfOffscreen();
        FadeIn();
    }

    private void RestoreNormalBorder()
    {
        RootBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "CardBorderBrush");
    }

    public void HideOverlay()
    {
        _hideTimer?.Dispose();
        _hideTimer = null;
        FadeOut(() => Visibility = Visibility.Collapsed);
    }

    private void FadeIn()
    {
        if (Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
            Opacity = 0;
        }
        BeginAnimation(OpacityProperty, new DoubleAnimation(Opacity, 1, TimeSpan.FromMilliseconds(150)));
    }

    private void FadeOut(Action? onComplete = null)
    {
        var anim = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(100));
        anim.Completed += (_, _) => onComplete?.Invoke();
        BeginAnimation(OpacityProperty, anim);
    }

    private void ScheduleHide()
    {
        _hideTimer?.Dispose();
        _hideTimer = new System.Threading.Timer(_ =>
        {
            _hideTimer = null;
            Dispatcher.InvokeAsync(HideOverlay);
        }, null, 300, System.Threading.Timeout.Infinite);
    }

    private void OnTranslateSentenceRequested(object? sender, string contextSentence)
        => TranslateSentenceRequested?.Invoke(this, contextSentence);

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) HideOverlay();
        base.OnKeyDown(e);
    }
}
