using System.Windows;
using LinguaLens.Core.Models;

namespace LinguaLens.Core.Interfaces;

public interface ITextExtractor
{
    /// <summary>Returns null if nothing found or not EN/ES.</summary>
    Task<WordExtractionResult?> ExtractWordAtPointAsync(Point screenPoint);

    Task<string?> ExtractSelectedTextAsync();
}
