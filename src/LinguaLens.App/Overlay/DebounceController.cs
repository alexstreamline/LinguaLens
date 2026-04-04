using WpfPoint = System.Windows.Point;
using LinguaLens.Core.Services;
using LinguaLens.Core.Interfaces;
using LinguaLens.Infrastructure.Hooks;

namespace LinguaLens.App.Overlay;

/// <summary>
/// Listens to GlobalMouseHook.MouseMoved events, debounces by DebounceMs (default 400ms),
/// then calls TranslationOrchestrator.ProcessHoverAsync.
/// Cancels in-flight requests when cursor moves to a different word.
/// Tracks last translated word to avoid re-requesting the same word.
/// </summary>
public sealed class DebounceController : IDisposable
{
    private readonly TranslationOrchestrator _orchestrator;
    private readonly IAppSettings _settings;
    private readonly OverlayWindow _overlay;
    private readonly GlobalMouseHook _hook;

    private CancellationTokenSource? _cts;
    private System.Threading.Timer? _debounceTimer;
    private WpfPoint _lastPoint;
    private string? _lastTranslatedWord;

    public DebounceController(
        TranslationOrchestrator orchestrator,
        IAppSettings settings,
        OverlayWindow overlay,
        GlobalMouseHook hook)
    {
        _orchestrator = orchestrator;
        _settings = settings;
        _overlay = overlay;
        _hook = hook;
        _hook.MouseMoved += OnMouseMoved;
    }

    private void OnMouseMoved(object? sender, WpfPoint point)
    {
        _lastPoint = point;
        _cts?.Cancel();

        var debounceMs = _settings.DebounceMs;
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ =>
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;

            var cts = new CancellationTokenSource();
            _cts = cts;
            _ = ProcessPointAsync(_lastPoint, cts.Token);
        }, null, debounceMs, Timeout.Infinite);
    }

    private async Task ProcessPointAsync(WpfPoint point, CancellationToken ct)
    {
        try
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _overlay.ShowAtPoint(point);
                _overlay.ShowLoading();
            });

            var result = await _orchestrator.ProcessHoverAsync(point, ct);
            if (ct.IsCancellationRequested) return;

            if (result is null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => _overlay.HideOverlay());
                return;
            }

            // Skip if same word as last translation
            if (result.Word == _lastTranslatedWord) return;
            _lastTranslatedWord = result.Word;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => _overlay.ShowResult(result));
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (!ct.IsCancellationRequested)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    _overlay.ShowError("Не удалось получить перевод"));
            }
        }
    }

    public void Dispose()
    {
        _hook.MouseMoved -= OnMouseMoved;
        _debounceTimer?.Dispose();
        _cts?.Dispose();
    }
}
