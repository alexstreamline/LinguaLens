using System.Windows;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.TextExtraction;

/// <summary>
/// Extracts word at cursor position using UI Automation (System.Windows.Automation).
/// All calls must happen on a background thread — never invoke from UI thread.
/// </summary>
public class UiaTextExtractor : ITextExtractor
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
