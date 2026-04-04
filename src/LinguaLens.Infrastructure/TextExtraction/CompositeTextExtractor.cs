using System.Windows;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.TextExtraction;

/// <summary>
/// Tries UiaTextExtractor first; falls back to ClipboardTextExtractor for sentence mode.
/// </summary>
public class CompositeTextExtractor(UiaTextExtractor uia, ClipboardTextExtractor clipboard) : ITextExtractor
{
    public Task<WordExtractionResult?> ExtractWordAtPointAsync(Point screenPoint)
        => uia.ExtractWordAtPointAsync(screenPoint);

    public async Task<string?> ExtractSelectedTextAsync()
    {
        var result = await uia.ExtractSelectedTextAsync();
        if (!string.IsNullOrEmpty(result)) return result;
        return await clipboard.ExtractSelectedTextAsync();
    }
}
