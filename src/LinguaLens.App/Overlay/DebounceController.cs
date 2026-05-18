using System.Runtime.InteropServices;
using WpfPoint = System.Windows.Point;
using WpfApp = System.Windows.Application;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Services;
using LinguaLens.Infrastructure.TextExtraction;
using LinguaLens.App.Tray;

namespace LinguaLens.App.Overlay;

/// <summary>
/// Drives TranslationOrchestrator from hotkey and overlay UI actions.
/// Translation is triggered exclusively via TriggerManualAsync (hotkey Ctrl+Shift+Space).
/// </summary>
public sealed class DebounceController : IDisposable
{
    private readonly TranslationOrchestrator _orchestrator;
    private readonly IAppSettings _settings;
    private readonly OverlayWindow _overlay;
    private readonly ITokenUsageRepository _usageRepo;
    private readonly TrayIconManager _trayManager;
    private readonly UiaTextExtractor _uiaExtractor;

    private CancellationTokenSource? _cts;
    private System.Threading.Timer? _midnightTimer;
    private WpfPoint _lastPoint;
    private string? _lastTranslatedWord;
    private bool _warningSentToday;

    public DebounceController(
        TranslationOrchestrator orchestrator,
        IAppSettings settings,
        OverlayWindow overlay,
        ITokenUsageRepository usageRepo,
        TrayIconManager trayManager,
        UiaTextExtractor uiaExtractor)
    {
        _orchestrator = orchestrator;
        _settings = settings;
        _overlay = overlay;
        _usageRepo = usageRepo;
        _trayManager = trayManager;
        _uiaExtractor = uiaExtractor;

        _overlay.TranslateSentenceRequested += OnTranslateSentenceRequested;
        _overlay.RetryRequested += OnRetryRequested;

        _overlay.IsVisibleChanged += (_, e) =>
        {
            if (!(bool)e.NewValue) _lastTranslatedWord = null;
        };

        var now = DateTime.Now;
        var msUntilMidnight = (long)(now.Date.AddDays(1) - now).TotalMilliseconds;
        _midnightTimer = new System.Threading.Timer(
            _ => _warningSentToday = false, null,
            msUntilMidnight,
            (long)TimeSpan.FromDays(1).TotalMilliseconds);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out CursorPoint pt);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint { public int X, Y; }

    /// <summary>
    /// Triggered by hotkey (Ctrl+Shift+Space). Translates selected text if any,
    /// otherwise translates the word under the cursor.
    /// </summary>
    public async Task TriggerManualAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var cts = _cts;

        _lastTranslatedWord = null;

        GetCursorPos(out var raw);
        var point = new WpfPoint(raw.X, raw.Y);
        _lastPoint = point;

        try
        {
            var selectedText = await _uiaExtractor.ExtractSelectedTextAsync();
            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                {
                    _overlay.ShowAtPoint(point);
                    _overlay.ShowLoading();
                });

                var sentenceResult = await _orchestrator.ProcessSelectionAsync(cts.Token, selectedText);
                if (cts.Token.IsCancellationRequested) return;

                await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (sentenceResult is null) _overlay.HideOverlay();
                    else _overlay.ShowSentenceResult(sentenceResult, selectedText);
                });
            }
            else
            {
                await ProcessWordAtPointAsync(point, cts.Token);
            }

            _ = CheckUsageWarningAsync();
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (!cts.Token.IsCancellationRequested)
                await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.ShowError());
        }
    }

    private async Task ProcessWordAtPointAsync(WpfPoint point, CancellationToken ct)
    {
        var extracted = await _orchestrator.ExtractAsync(point);
        if (extracted is null) return;

        await WpfApp.Current.Dispatcher.InvokeAsync(() =>
        {
            _overlay.ShowAtPoint(point);
            _overlay.ShowLoading();
        });

        var result = await _orchestrator.ProcessHoverAsync(point, ct, extracted);
        if (ct.IsCancellationRequested) return;

        if (result is null)
        {
            await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.HideOverlay());
            return;
        }

        _lastTranslatedWord = result.Word;
        var sentence = extracted.Sentence;
        await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.ShowResult(result, sentence));
    }

    private async void OnTranslateSentenceRequested(object? sender, string contextSentence)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var cts = _cts;
        _lastTranslatedWord = null;

        try
        {
            // Окно уже видимо (мы внутри word-карточки), окно не перемещаем —
            // только подменяем содержимое на loading с явным статусом.
            await WpfApp.Current.Dispatcher.InvokeAsync(() =>
            {
                _overlay.ShowLoading("Перевожу всё предложение…");
            });

            // Если карточка слова передала контекст-предложение — используем его как preExtracted,
            // иначе fallback на чтение выделения через UIA.
            var preExtracted = string.IsNullOrWhiteSpace(contextSentence) ? null : contextSentence;
            var result = await _orchestrator.ProcessSelectionAsync(cts.Token, preExtracted);
            if (cts.Token.IsCancellationRequested) return;

            await WpfApp.Current.Dispatcher.InvokeAsync(() =>
            {
                if (result is null) _overlay.HideOverlay();
                else _overlay.ShowSentenceResult(result, contextSentence ?? "");
            });

            _ = CheckUsageWarningAsync();
        }
        catch (OperationCanceledException) { }
        catch
        {
            await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.ShowError());
        }
    }

    private void OnRetryRequested(object? sender, EventArgs e)
    {
        _lastTranslatedWord = null;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = ProcessWordAtPointAsync(_lastPoint, _cts.Token);
    }

    private async Task CheckUsageWarningAsync()
    {
        try
        {
            var summary = await _usageRepo.GetTodaySummaryAsync();
            var limit = _settings.DailyTokenLimit;
            if (limit <= 0 || !_settings.WarnAtEightyPercent) return;
            var pct = (double)summary.TotalTokens / limit * 100;
            if (pct >= 80 && !_warningSentToday)
            {
                _warningSentToday = true;
                _trayManager.ShowWarning(
                    $"Использовано {pct:F0}% дневного лимита токенов",
                    "Откройте настройки для деталей.");
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _overlay.TranslateSentenceRequested -= OnTranslateSentenceRequested;
        _overlay.RetryRequested -= OnRetryRequested;
        _midnightTimer?.Dispose();
        _cts?.Dispose();
    }
}
