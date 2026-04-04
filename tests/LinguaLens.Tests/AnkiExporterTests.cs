using System.IO.Compression;
using LinguaLens.Core.Models;
using LinguaLens.Infrastructure.Export;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LinguaLens.Tests;

public class AnkiExporterTests : IDisposable
{
    private readonly string _tempOutputPath;
    private readonly AnkiExporter _exporter;

    public AnkiExporterTests()
    {
        _tempOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".apkg");
        _exporter = new AnkiExporter(new CsvVocabExporter());
    }

    public void Dispose()
    {
        if (File.Exists(_tempOutputPath))
            File.Delete(_tempOutputPath);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static VocabEntry MakeEntry(int id = 1, string word = "test", string translation = "тест") =>
        new(id, word, "en", translation, "noun", "This is a test.", "notepad", "{}", DateTime.UtcNow, false);

    private static IReadOnlyList<VocabEntry> SampleEntries() =>
        [MakeEntry(1, "hello", "привет"), MakeEntry(2, "world", "мир")];

    // ── Output is a valid ZIP ─────────────────────────────────────────────────

    [Fact]
    public async Task ExportAnkiAsync_ProducesValidZipFile()
    {
        await _exporter.ExportAnkiAsync(SampleEntries(), _tempOutputPath);

        Assert.True(File.Exists(_tempOutputPath));
        // ZIP magic bytes: PK (0x50 0x4B)
        var header = new byte[2];
        await using var fs = File.OpenRead(_tempOutputPath);
        await fs.ReadAsync(header);
        Assert.Equal(0x50, header[0]);
        Assert.Equal(0x4B, header[1]);
    }

    [Fact]
    public async Task ExportAnkiAsync_ZipContainsCollectionAnki2()
    {
        await _exporter.ExportAnkiAsync(SampleEntries(), _tempOutputPath);

        using var zip = ZipFile.OpenRead(_tempOutputPath);
        Assert.Contains(zip.Entries, e => e.FullName == "collection.anki2");
    }

    [Fact]
    public async Task ExportAnkiAsync_ZipContainsMediaFile()
    {
        await _exporter.ExportAnkiAsync(SampleEntries(), _tempOutputPath);

        using var zip = ZipFile.OpenRead(_tempOutputPath);
        Assert.Contains(zip.Entries, e => e.FullName == "media");
    }

    [Fact]
    public async Task ExportAnkiAsync_MediaFileIsEmptyJsonObject()
    {
        await _exporter.ExportAnkiAsync(SampleEntries(), _tempOutputPath);

        using var zip = ZipFile.OpenRead(_tempOutputPath);
        var mediaEntry = zip.Entries.Single(e => e.FullName == "media");
        using var reader = new StreamReader(mediaEntry.Open());
        var content = await reader.ReadToEndAsync();
        Assert.Equal("{}", content);
    }

    // ── SQLite schema inside collection.anki2 ────────────────────────────────

    private async Task<string> ExtractAnki2ToTempAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".anki2");
        using var zip = ZipFile.OpenRead(_tempOutputPath);
        var entry = zip.Entries.Single(e => e.FullName == "collection.anki2");
        entry.ExtractToFile(dbPath);
        return dbPath;
    }

    [Theory]
    [InlineData("col")]
    [InlineData("notes")]
    [InlineData("cards")]
    [InlineData("graves")]
    [InlineData("revlog")]
    public async Task ExportAnkiAsync_Anki2HasRequiredTable(string tableName)
    {
        await _exporter.ExportAnkiAsync(SampleEntries(), _tempOutputPath);
        var dbPath = await ExtractAnki2ToTempAsync();

        try
        {
            await using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False;");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}';";
            var result = await cmd.ExecuteScalarAsync();
            Assert.Equal(tableName, result?.ToString());
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    // ── Notes and cards content ───────────────────────────────────────────────

    [Fact]
    public async Task ExportAnkiAsync_NoteCountMatchesEntryCount()
    {
        var entries = SampleEntries();
        await _exporter.ExportAnkiAsync(entries, _tempOutputPath);
        var dbPath = await ExtractAnki2ToTempAsync();

        try
        {
            await using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False;");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM notes;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(entries.Count, count);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ExportAnkiAsync_CardCountMatchesEntryCount()
    {
        var entries = SampleEntries();
        await _exporter.ExportAnkiAsync(entries, _tempOutputPath);
        var dbPath = await ExtractAnki2ToTempAsync();

        try
        {
            await using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False;");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM cards;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(entries.Count, count);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ExportAnkiAsync_NoteFldsContainsWordAndTranslation()
    {
        await _exporter.ExportAnkiAsync([MakeEntry(1, "hello", "привет")], _tempOutputPath);
        var dbPath = await ExtractAnki2ToTempAsync();

        try
        {
            await using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False;");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT flds FROM notes LIMIT 1;";
            var flds = (string?)await cmd.ExecuteScalarAsync();

            Assert.NotNull(flds);
            Assert.Contains("hello", flds);
            Assert.Contains("привет", flds);
            // Fields separated by \x1f
            Assert.Contains('\x1f', flds);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ExportAnkiAsync_ColTableHasOneRow()
    {
        await _exporter.ExportAnkiAsync(SampleEntries(), _tempOutputPath);
        var dbPath = await ExtractAnki2ToTempAsync();

        try
        {
            await using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False;");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM col;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(1, count);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ExportAnkiAsync_EmptyEntryList_ProducesValidFileWithNoNotes()
    {
        await _exporter.ExportAnkiAsync([], _tempOutputPath);
        var dbPath = await ExtractAnki2ToTempAsync();

        try
        {
            await using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False;");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM notes;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    // ── Overwrite existing file ───────────────────────────────────────────────

    [Fact]
    public async Task ExportAnkiAsync_ExistingFile_OverwritesSuccessfully()
    {
        await File.WriteAllTextAsync(_tempOutputPath, "old content");
        await _exporter.ExportAnkiAsync(SampleEntries(), _tempOutputPath);

        // Should be a valid ZIP now, not the old content
        using var zip = ZipFile.OpenRead(_tempOutputPath);
        Assert.Contains(zip.Entries, e => e.FullName == "collection.anki2");
    }
}
