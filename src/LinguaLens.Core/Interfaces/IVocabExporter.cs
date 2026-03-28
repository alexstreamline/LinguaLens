using LinguaLens.Core.Models;

namespace LinguaLens.Core.Interfaces;

public interface IVocabExporter
{
    Task ExportCsvAsync(IReadOnlyList<VocabEntry> entries, string filePath);
    Task ExportAnkiAsync(IReadOnlyList<VocabEntry> entries, string filePath);
}
