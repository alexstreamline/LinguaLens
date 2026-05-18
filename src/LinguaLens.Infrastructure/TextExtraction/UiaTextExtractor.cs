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
                var pt = new System.Windows.Point(screenPoint.X, screenPoint.Y);
                var element = AutomationElement.FromPoint(pt);
                if (element is null) return null;

                if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var patternObj))
                    return null;

                var textPattern = (TextPattern)patternObj;

                var range = FindWordRangeAtPoint(textPattern, pt);
                if (range is null) return null;

                var word = range.GetText(256).Trim().TrimEnd('.', ',', '!', '?', ';', ':', '"', '\'', ')');
                if (string.IsNullOrWhiteSpace(word)) return null;

                string sentence;
                try
                {
                    var sentenceRange = range.Clone();
                    sentenceRange.ExpandToEnclosingUnit(TextUnit.Paragraph);
                    // 1500 символов — целый абзац / несколько коротких. Чем больше контекст,
                    // тем точнее LLM разрешает омонимию (bank: финансовый vs. речной vs. memory).
                    var paragraph = sentenceRange.GetText(1500).Trim();
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

    // Fast path: RangeFromPoint → validate bounding rect.
    // Fallback: scan GetVisibleRanges() word-by-word — handles apps where
    // RangeFromPoint always returns position 0 (broken UIA providers, e.g. some browsers).
    private static TextPatternRange? FindWordRangeAtPoint(TextPattern textPattern, System.Windows.Point pt)
    {
        // Fast path
        try
        {
            var r = textPattern.RangeFromPoint(pt);
            r.ExpandToEnclosingUnit(TextUnit.Word);
            if (RangeContainsPoint(r, pt)) return r;
        }
        catch { }

        // Fallback: visible range scan
        try
        {
            foreach (var vr in textPattern.GetVisibleRanges())
            {
                // Skip ranges whose vertical band doesn't contain cursor
                var vrRects = SafeGetRects(vr);
                if (vrRects.Length > 0 && !vrRects.Any(r => pt.Y >= r.Top && pt.Y <= r.Bottom))
                    continue;

                var found = ScanWordsInRange(vr, pt);
                if (found is not null) return found;
            }
        }
        catch { }

        return null;
    }

    // Walks words inside a visible range and returns the first whose rect contains pt.
    // One GetBoundingRectangles() call per word; stops early when cursor is passed.
    private static TextPatternRange? ScanWordsInRange(TextPatternRange paragraphRange, System.Windows.Point pt)
    {
        // Start a degenerate cursor at the beginning of the range
        var cursor = paragraphRange.Clone();
        try
        {
            cursor.MoveEndpointByRange(
                TextPatternRangeEndpoint.End, cursor, TextPatternRangeEndpoint.Start);
        }
        catch { return null; }

        const int maxWords = 300;
        for (int i = 0; i < maxWords; i++)
        {
            try
            {
                var wordRange = cursor.Clone();
                if (wordRange.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Word, 1) == 0)
                    break;

                var rects = SafeGetRects(wordRange);

                if (rects.Any(r => pt.X >= r.Left && pt.X <= r.Right &&
                                   pt.Y >= r.Top  && pt.Y <= r.Bottom))
                    return wordRange;

                // Stop if we've moved past the cursor's position on this line
                if (rects.Length > 0 && rects[0].Top >= pt.Y - 5 && rects[0].Left > pt.X + 50)
                    break;

                // Advance cursor to end of current word
                cursor = wordRange.Clone();
                cursor.MoveEndpointByRange(
                    TextPatternRangeEndpoint.Start, cursor, TextPatternRangeEndpoint.End);
            }
            catch { break; }
        }

        return null;
    }

    private static bool RangeContainsPoint(TextPatternRange range, System.Windows.Point pt)
    {
        var rects = SafeGetRects(range);
        return rects.Any(r => pt.X >= r.Left && pt.X <= r.Right &&
                              pt.Y >= r.Top  && pt.Y <= r.Bottom);
    }

    private static System.Windows.Rect[] SafeGetRects(TextPatternRange range)
    {
        try { return range.GetBoundingRectangles(); }
        catch { return []; }
    }

    public Task<string?> ExtractSelectedTextAsync()
    {
        return Task.Run<string?>(() =>
        {
            try
            {
                var focused = AutomationElement.FocusedElement;
                if (focused is null) return null;
                if (!focused.TryGetCurrentPattern(TextPattern.Pattern, out var obj)) return null;
                var tp = (TextPattern)obj;
                var ranges = tp.GetSelection();
                if (ranges.Length == 0) return null;
                var text = ranges[0].GetText(-1).Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch { return null; }
        });
    }
}
