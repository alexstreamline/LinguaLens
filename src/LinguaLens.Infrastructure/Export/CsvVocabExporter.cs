using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.Export;

/// <summary>
/// Exports vocab entries to CSV (UTF-8 with BOM for Excel compatibility).
/// Columns: Word, Language, Translation, PartOfSpeech, Context, Source, Date, Learned
/// </summary>
public class CsvVocabExporter : IVocabExporter
{
    public async Task ExportCsvAsync(IReadOnlyList<VocabEntry> entries, string filePath)
    {
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await writer.WriteLineAsync("Word,Language,Translation,PartOfSpeech,Context,Source,Date,Learned");

        foreach (var entry in entries)
        {
            await writer.WriteLineAsync(string.Join(",",
                CsvEscape(entry.Word),
                CsvEscape(entry.DetectedLang),
                CsvEscape(entry.Translation),
                CsvEscape(entry.Pos),
                CsvEscape(entry.ContextSentence),
                CsvEscape(entry.SourceApp),
                entry.CreatedAt.ToString("yyyy-MM-dd"),
                entry.IsLearned ? "Yes" : "No"));
        }
    }

    public Task ExportAnkiAsync(IReadOnlyList<VocabEntry> entries, string filePath)
        => throw new NotSupportedException("Use AnkiExporter for Anki export.");

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
