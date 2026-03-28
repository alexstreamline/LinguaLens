using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.Export;

/// <summary>
/// Exports vocab entries to Anki .apkg format (ZIP with collection.anki2 SQLite + empty media file).
/// Front: word + transcription. Back: translation + pos + first example.
/// Implemented from scratch — no AnkiSharp dependency.
/// </summary>
public class AnkiExporter : IVocabExporter
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
