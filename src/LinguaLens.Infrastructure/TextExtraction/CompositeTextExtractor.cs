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
    {
        throw new NotImplementedException();
    }

    public Task<string?> ExtractSelectedTextAsync()
    {
        throw new NotImplementedException();
    }
}
