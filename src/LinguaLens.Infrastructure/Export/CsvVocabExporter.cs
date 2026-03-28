using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.Export;

/// <summary>
/// Exports vocab entries to CSV (UTF-8 with BOM for Excel compatibility).
/// Columns: Word, Language, Translation, PartOfSpeech, Context, Source, Date, Learned
/// </summary>
public class CsvVocabExporter : IVocabExporter
{
    public Task ExportCsvAsync(IReadOnlyList<VocabEntry> entries, string filePath)
    {
        throw new NotImplementedException();
    }

    public Task ExportAnkiAsync(IReadOnlyList<VocabEntry> entries, string filePath)
    {
        throw new NotImplementedException();
    }
}
