using System.Windows;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.TextExtraction;

/// <summary>
/// Sentence-mode only extractor. Reads clipboard via Dispatcher (STA thread required).
/// ExtractWordAtPointAsync always returns null.
/// </summary>
public class ClipboardTextExtractor : ITextExtractor
{
    public Task<WordExtractionResult?> ExtractWordAtPointAsync(Point screenPoint)
        => Task.FromResult<WordExtractionResult?>(null);

    public Task<string?> ExtractSelectedTextAsync()
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var text = Clipboard.GetText();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch
            {
                return (string?)null;
            }
        }).Task;
    }
}
