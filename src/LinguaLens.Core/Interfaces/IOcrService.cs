using System.Windows;
using LinguaLens.Core.Models;

namespace LinguaLens.Core.Interfaces;

/// <summary>
/// OCR-fallback для приложений, не поддерживающих UI Automation TextPattern
/// (PDF-читалки, кастомные рендеры, графические редакторы).
/// Захватывает регион экрана вокруг точки и распознаёт текст.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Распознать текст в регионе вокруг указанной точки экрана (в физических px).
    /// </summary>
    /// <param name="screenPoint">Точка курсора в координатах экрана (физические px).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>OcrResult или null, если ничего не распознано / точка не над текстом.</returns>
    Task<OcrResult?> ExtractTextNearAsync(Point screenPoint, CancellationToken ct);
}
