using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
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
        return Task.Run<WordExtractionResult?>(() =>
        {
            try
            {
                var element = AutomationElement.FromPoint(new System.Windows.Point(screenPoint.X, screenPoint.Y));
                if (element is null) return null;

                if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var patternObj))
                    return null;

                var textPattern = (TextPattern)patternObj;

                TextPatternRange range;
                try
                {
                    range = textPattern.RangeFromPoint(new System.Windows.Point(screenPoint.X, screenPoint.Y));
                }
                catch
                {
                    return null;
                }

                range.ExpandToEnclosingUnit(TextUnit.Word);
                var word = range.GetText(256).Trim().TrimEnd('.', ',', '!', '?', ';', ':', '"', '\'', ')');
                if (string.IsNullOrWhiteSpace(word)) return null;

                string sentence;
                try
                {
                    var sentenceRange = range.Clone();
                    sentenceRange.ExpandToEnclosingUnit(TextUnit.Paragraph);
                    var paragraph = sentenceRange.GetText(600).Trim();
                    sentence = paragraph.Length > 0 ? paragraph : word;
                }
                catch
                {
                    sentence = word;
                }

                string sourceName;
                try
                {
                    sourceName = Process.GetProcessById(element.Current.ProcessId).ProcessName;
                }
                catch
                {
                    sourceName = "unknown";
                }

                return new WordExtractionResult(word, sentence, sourceName, screenPoint);
            }
            catch
            {
                return null;
            }
        });
    }

    public Task<string?> ExtractSelectedTextAsync() => Task.FromResult<string?>(null);
}
