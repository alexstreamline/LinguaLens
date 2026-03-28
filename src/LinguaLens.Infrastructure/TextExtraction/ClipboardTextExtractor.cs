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
    {
        return Task.FromResult<WordExtractionResult?>(null);
    }

    public Task<string?> ExtractSelectedTextAsync()
    {
        throw new NotImplementedException();
    }
}
