using System.Runtime.InteropServices;
using WpfPoint = System.Windows.Point;
using WpfApp = System.Windows.Application;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Services;
using LinguaLens.Infrastructure.Hooks;
using LinguaLens.Infrastructure.TextExtraction;
using LinguaLens.App.Tray;

namespace LinguaLens.App.Overlay;

/// <summary>
/// Listens to GlobalMouseHook events, debounces, drives TranslationOrchestrator.
///
/// Hover flow (two-phase — eliminates loading flicker on non-text areas):
///   1. Debounce fires → ExtractAsync (fast UIA call, no LLM)
///   2. No word found → do nothing (no overlay)
///   3. Word found → ShowLoading → LLM → ShowResult
///
/// Selection flow (auto sentence translation):
///   WM_LBUTTONUP → 200ms debounce → read UIA selection
///   If ≥ 20 chars selected → translate as sentence
///   (uses UIA only, no clipboard fallback, to avoid false triggers)
/// </summary>
public sealed class DebounceController : IDisposable
{
    private readonly TranslationOrchestrator _orchestrator;
    private readonly IAppSettings _settings;
    private readonly OverlayWindow _overlay;
    private readonly GlobalMouseHook _hook;
    private readonly ITokenUsageRepository _usageRepo;
    private readonly TrayIconManager _trayManager;
    private readonly UiaTextExtractor _uiaExtractor;

    private const int SelectionDebounceMs = 200;
    private const int SelectionMinChars = 20;

    private CancellationTokenSource? _cts;
    private System.Threading.Timer? _debounceTimer;
    private System.Threading.Timer? _selectionTimer;
    private System.Threading.Timer? _midnightTimer;
    private WpfPoint _lastPoint;
    private string? _lastTranslatedWord;
    private bool _warningSentToday;

    public DebounceController(
        TranslationOrchestrator orchestrator,
        IAppSettings settings,
        OverlayWindow overlay,
        GlobalMouseHook hook,
        ITokenUsageRepository usageRepo,
        TrayIconManager trayManager,
        UiaTextExtractor uiaExtractor)
    {
        _orchestrator = orchestrator;
        _settings = settings;
        _overlay = overlay;
        _hook = hook;
        _usageRepo = usageRepo;
        _trayManager = trayManager;
        _uiaExtractor = uiaExtractor;

        _hook.MouseMoved += OnMouseMoved;
        _hook.SelectionChanged += OnSelectionChanged;
        _overlay.TranslateSentenceRequested += OnTranslateSentenceRequested;
        _overlay.RetryRequested += OnRetryRequested;

        // Reset last-word cache when overlay hides so re-hovering same word works
        _overlay.IsVisibleChanged += (_, e) =>
        {
            if (!(bool)e.NewValue) _lastTranslatedWord = null;
        };

        // Reset warning flag at midnight
        var now = DateTime.Now;
        var midnight = now.Date.AddDays(1);
        var msUntilMidnight = (long)(midnight - now).TotalMilliseconds;
        _midnightTimer = new System.Threading.Timer(
            _ => _warningSentToday = false, null,
            msUntilMidnight,
            (long)TimeSpan.FromDays(1).TotalMilliseconds);
    }

    private void OnMouseMoved(object? sender, WpfPoint point)
    {
        _lastPoint = point;
        _cts?.Cancel();
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ =>
        {
            _cts = new CancellationTokenSource();
            _ = ProcessPointAsync(_lastPoint, _cts.Token);
        }, null, _settings.DebounceMs, System.Threading.Timeout.Infinite);
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        _selectionTimer?.Dispose();
        _selectionTimer = new System.Threading.Timer(_ =>
        {
            _ = ProcessSelectionFromHookAsync();
        }, null, SelectionDebounceMs, System.Threading.Timeout.Infinite);
    }

    private async Task ProcessPointAsync(WpfPoint point, CancellationToken ct)
    {
        try
        {
            // Phase 1: fast UIA extraction — no UI change yet
            var extracted = await _orchestrator.ExtractAsync(point);
            if (extracted is null || ct.IsCancellationRequested) return;

            // Phase 2: word confirmed — show loading, then call LLM
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

            if (result.Word == _lastTranslatedWord)
            {
                await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.HideOverlay());
                return;
            }
            _lastTranslatedWord = result.Word;

            await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.ShowResult(result));
            _ = CheckUsageWarningAsync();
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (!ct.IsCancellationRequested)
                await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.ShowError());
        }
    }

    private async Task ProcessSelectionFromHookAsync()
    {
        try
        {
            // Read UIA selection only — clipboard fallback would give false positives
            var text = await _uiaExtractor.ExtractSelectedTextAsync();
            if (string.IsNullOrWhiteSpace(text) || text.Length < SelectionMinChars) return;

            // Selection confirmed — cancel any pending hover translation
            _cts?.Cancel();
            _debounceTimer?.Dispose();
            _debounceTimer = null;

            var cts = new CancellationTokenSource();
            _cts = cts;
            _lastTranslatedWord = null;

            await WpfApp.Current.Dispatcher.InvokeAsync(() =>
            {
                _overlay.ShowAtPoint(_lastPoint);
                _overlay.ShowLoading();
            });

            // Pass pre-read text so it's not re-read after focus may have changed
            var result = await _orchestrator.ProcessSelectionAsync(cts.Token, text);
            if (cts.Token.IsCancellationRequested) return;

            if (result is null)
            {
                await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.HideOverlay());
                return;
            }
            await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.ShowSentenceResult(result));
            _ = CheckUsageWarningAsync();
        }
        catch (OperationCanceledException) { }
        catch
        {
            await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.ShowError());
        }
    }

    private async void OnTranslateSentenceRequested(object? sender, EventArgs e)
    {
        _lastTranslatedWord = null;
        var cts = new CancellationTokenSource();
        try
        {
            await WpfApp.Current.Dispatcher.InvokeAsync(() =>
            {
                _overlay.ShowAtPoint(_lastPoint);
                _overlay.ShowLoading();
            });

            var result = await _orchestrator.ProcessSelectionAsync(cts.Token);
            if (result is null)
            {
                await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.HideOverlay());
                return;
            }
            await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.ShowSentenceResult(result));
            _ = CheckUsageWarningAsync();
        }
        catch (OperationCanceledException) { }
        catch
        {
            await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.ShowError());
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out CursorPoint pt);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint { public int X, Y; }

    /// <summary>
    /// Triggered by manual hotkey. Translates selected text if any, otherwise the word under cursor.
    /// </summary>
    public async Task TriggerManualAsync()
    {
        _cts?.Cancel();
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _lastTranslatedWord = null;

        var cts = new CancellationTokenSource();
        _cts = cts;

        // Get physical cursor position at the moment hotkey was pressed
        GetCursorPos(out var raw);
        var point = new WpfPoint(raw.X, raw.Y);
        _lastPoint = point;

        try
        {
            // Prefer selected text — translate as sentence; fall back to word at cursor
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

                if (sentenceResult is null)
                    await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.HideOverlay());
                else
                    await WpfApp.Current.Dispatcher.InvokeAsync(() => _overlay.ShowSentenceResult(sentenceResult));
            }
            else
            {
                await ProcessPointAsync(point, cts.Token);
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

    private void OnRetryRequested(object? sender, EventArgs e)
    {
        _lastTranslatedWord = null;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = ProcessPointAsync(_lastPoint, _cts.Token);
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
        catch { /* non-critical */ }
    }

    public void Dispose()
    {
        _hook.MouseMoved -= OnMouseMoved;
        _hook.SelectionChanged -= OnSelectionChanged;
        _overlay.TranslateSentenceRequested -= OnTranslateSentenceRequested;
        _overlay.RetryRequested -= OnRetryRequested;
        _debounceTimer?.Dispose();
        _selectionTimer?.Dispose();
        _midnightTimer?.Dispose();
        _cts?.Dispose();
    }
}
