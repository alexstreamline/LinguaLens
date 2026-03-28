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
        throw new NotImplementedException();
    }

    private void OnMouseMoved(object? sender, WpfPoint point)
    {
        throw new NotImplementedException();
    }

    private async Task ProcessPointAsync(WpfPoint point, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _cts?.Dispose();
    }
}
