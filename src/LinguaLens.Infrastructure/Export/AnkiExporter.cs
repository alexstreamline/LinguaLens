using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;
using Microsoft.Data.Sqlite;

namespace LinguaLens.Infrastructure.Export;

/// <summary>
/// Exports vocab entries to Anki .apkg format (ZIP with collection.anki2 SQLite + empty media file).
/// Front: word + pos. Back: translation + context sentence.
/// Implemented from scratch — no AnkiSharp dependency.
/// </summary>
public class AnkiExporter(CsvVocabExporter csvExporter) : IVocabExporter
{
    private const long ModelId = 1342697561L;
    private const long DeckId = 1L;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public Task ExportCsvAsync(IReadOnlyList<VocabEntry> entries, string filePath)
        => csvExporter.ExportCsvAsync(entries, filePath);

    public async Task ExportAnkiAsync(IReadOnlyList<VocabEntry> entries, string filePath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = Path.Combine(tempDir, "collection.anki2");
            await CreateAnkiDatabaseAsync(dbPath, entries);

            var mediaPath = Path.Combine(tempDir, "media");
            await File.WriteAllTextAsync(mediaPath, "{}");

            if (File.Exists(filePath)) File.Delete(filePath);
            using var zip = ZipFile.Open(filePath, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(dbPath, "collection.anki2");
            zip.CreateEntryFromFile(mediaPath, "media");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static async Task CreateAnkiDatabaseAsync(string dbPath, IReadOnlyList<VocabEntry> entries)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath};");
        await conn.OpenAsync();

        await ExecAsync(conn, "PRAGMA journal_mode=WAL;");
        await CreateSchemaAsync(conn);
        await InsertCollectionAsync(conn);
        await InsertNotesAndCardsAsync(conn, entries);
    }

    private static async Task CreateSchemaAsync(SqliteConnection conn)
    {
        await ExecAsync(conn, """
            CREATE TABLE IF NOT EXISTS col (
                id    INTEGER PRIMARY KEY,
                crt   INTEGER NOT NULL,
                mod   INTEGER NOT NULL,
                scm   INTEGER NOT NULL,
                ver   INTEGER NOT NULL,
                dty   INTEGER NOT NULL,
                usn   INTEGER NOT NULL,
                ls    INTEGER NOT NULL,
                conf  TEXT NOT NULL,
                models TEXT NOT NULL,
                decks  TEXT NOT NULL,
                dconf  TEXT NOT NULL,
                tags   TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS notes (
                id    INTEGER PRIMARY KEY,
                guid  TEXT NOT NULL,
                mid   INTEGER NOT NULL,
                mod   INTEGER NOT NULL,
                usn   INTEGER NOT NULL,
                tags  TEXT NOT NULL,
                flds  TEXT NOT NULL,
                sfld  TEXT NOT NULL,
                csum  INTEGER NOT NULL,
                flags INTEGER NOT NULL,
                data  TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS cards (
                id     INTEGER PRIMARY KEY,
                nid    INTEGER NOT NULL,
                did    INTEGER NOT NULL,
                ord    INTEGER NOT NULL,
                mod    INTEGER NOT NULL,
                usn    INTEGER NOT NULL,
                type   INTEGER NOT NULL,
                queue  INTEGER NOT NULL,
                due    INTEGER NOT NULL,
                ivl    INTEGER NOT NULL,
                factor INTEGER NOT NULL,
                reps   INTEGER NOT NULL,
                lapses INTEGER NOT NULL,
                left   INTEGER NOT NULL,
                odue   INTEGER NOT NULL,
                odid   INTEGER NOT NULL,
                flags  INTEGER NOT NULL,
                data   TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS graves (usn INTEGER NOT NULL, oid INTEGER NOT NULL, type INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS revlog (
                id      INTEGER PRIMARY KEY,
                cid     INTEGER NOT NULL,
                usn     INTEGER NOT NULL,
                ease    INTEGER NOT NULL,
                ivl     INTEGER NOT NULL,
                lastIvl INTEGER NOT NULL,
                factor  INTEGER NOT NULL,
                time    INTEGER NOT NULL,
                type    INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_notes_usn ON notes (usn);
            CREATE INDEX IF NOT EXISTS ix_cards_nid ON cards (nid);
            CREATE INDEX IF NOT EXISTS ix_cards_usn ON cards (usn);
            CREATE INDEX IF NOT EXISTS ix_revlog_usn ON revlog (usn);
            CREATE INDEX IF NOT EXISTS ix_revlog_cid ON revlog (cid);
            """);
    }

    private static async Task InsertCollectionAsync(SqliteConnection conn)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var conf = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["nextPos"] = 1, ["estTimes"] = true, ["activeDecks"] = new[] { DeckId },
            ["sortType"] = "noteFld", ["timeLim"] = 0, ["sortBackwards"] = false,
            ["addToCur"] = true, ["curDeck"] = DeckId, ["newBury"] = true,
            ["newSpread"] = 0, ["dueCounts"] = true,
            ["curModel"] = ModelId.ToString(), ["collapseTime"] = 1200
        }, JsonOpts);

        // Anki template syntax: {{Front}}, {{Back}}, {{FrontSide}} — these are literal strings, not C# interpolation
        var frontQfmt = "{{Front}}";
        var frontAfmt = "{{FrontSide}}\n\n<hr id=answer>\n\n{{Back}}";

        var models = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [ModelId.ToString()] = new Dictionary<string, object?>
            {
                ["id"] = ModelId, ["name"] = "Basic", ["type"] = 0, ["mod"] = 0,
                ["usn"] = 0, ["sortf"] = 0, ["did"] = null,
                ["tmpls"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["name"] = "Card 1", ["ord"] = 0,
                        ["qfmt"] = frontQfmt, ["afmt"] = frontAfmt,
                        ["bqfmt"] = "", ["bafmt"] = "", ["did"] = null, ["bfont"] = "", ["bsize"] = 0
                    }
                },
                ["flds"] = new[]
                {
                    new Dictionary<string, object> { ["name"] = "Front", ["ord"] = 0, ["sticky"] = false, ["rtl"] = false, ["font"] = "Arial", ["size"] = 20 },
                    new Dictionary<string, object> { ["name"] = "Back",  ["ord"] = 1, ["sticky"] = false, ["rtl"] = false, ["font"] = "Arial", ["size"] = 20 }
                },
                ["css"] = ".card { font-family: arial; font-size: 20px; text-align: center; color: black; background-color: white; }",
                ["latexPre"] = "\\documentclass[12pt]{article}\n\\special{papersize=3in,5in}\n\\usepackage[utf8]{inputenc}\n\\usepackage{amssymb,amsmath}\n\\pagestyle{empty}\n\\setlength{\\parindent}{0in}\n\\begin{document}\n",
                ["latexPost"] = "\\end{document}",
                ["latexsvg"] = false,
                ["req"] = new object[] { new object[] { 0, "any", new[] { 0 } } }
            }
        }, JsonOpts);

        var decks = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [DeckId.ToString()] = new Dictionary<string, object>
            {
                ["id"] = DeckId, ["name"] = "LinguaLens", ["desc"] = "",
                ["extendRev"] = 50, ["usn"] = 0, ["collapsed"] = false, ["browserCollapsed"] = false,
                ["newToday"] = new[] { 0, 0 }, ["revToday"] = new[] { 0, 0 },
                ["lrnToday"] = new[] { 0, 0 }, ["timeToday"] = new[] { 0, 0 },
                ["conf"] = 1, ["mod"] = 0
            }
        }, JsonOpts);

        var newConfig = new Dictionary<string, object>
        {
            ["perDay"] = 20, ["delays"] = new[] { 1, 10 }, ["separate"] = true,
            ["ints"] = new[] { 1, 4, 7 }, ["initialFactor"] = 2500, ["bury"] = true, ["order"] = 1
        };
        var dconf = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["1"] = new Dictionary<string, object>
            {
                ["id"] = 1, ["name"] = "Default", ["replayq"] = true,
                ["lapse"] = new Dictionary<string, object> { ["leechFails"] = 8, ["minInt"] = 1, ["delays"] = new[] { 10 }, ["leechAction"] = 0, ["mult"] = 0.0 },
                ["rev"] = new Dictionary<string, object> { ["perDay"] = 100, ["ease4"] = 1.3, ["fuzz"] = 0.05, ["minSpace"] = 1, ["ivlFct"] = 1.0, ["maxIvl"] = 36500, ["bury"] = true },
                ["timer"] = 0, ["maxTaken"] = 60, ["usn"] = 0,
                ["new"] = newConfig,
                ["mod"] = 0, ["autoplay"] = true
            }
        }, JsonOpts);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO col (id,crt,mod,scm,ver,dty,usn,ls,conf,models,decks,dconf,tags) VALUES (1,@crt,@mod,@scm,11,0,0,0,@conf,@models,@decks,@dconf,'{}');";
        cmd.Parameters.AddWithValue("@crt", now);
        cmd.Parameters.AddWithValue("@mod", now);
        cmd.Parameters.AddWithValue("@scm", now * 1000);
        cmd.Parameters.AddWithValue("@conf", conf);
        cmd.Parameters.AddWithValue("@models", models);
        cmd.Parameters.AddWithValue("@decks", decks);
        cmd.Parameters.AddWithValue("@dconf", dconf);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertNotesAndCardsAsync(SqliteConnection conn, IReadOnlyList<VocabEntry> entries)
    {
        var now = (long)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var rng = new Random();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var noteId = now * 1000 + i;
            var cardId = noteId + 1;

            var front = BuildFront(entry);
            var back = BuildBack(entry);
            var flds = front + "\x1f" + back;
            var guid = GenerateGuid(rng);
            var csum = ComputeCsum(front);

            await using var noteCmd = conn.CreateCommand();
            noteCmd.CommandText = "INSERT INTO notes (id,guid,mid,mod,usn,tags,flds,sfld,csum,flags,data) VALUES (@id,@guid,@mid,@mod,-1,'',@flds,@sfld,@csum,0,'');";
            noteCmd.Parameters.AddWithValue("@id", noteId);
            noteCmd.Parameters.AddWithValue("@guid", guid);
            noteCmd.Parameters.AddWithValue("@mid", ModelId);
            noteCmd.Parameters.AddWithValue("@mod", now);
            noteCmd.Parameters.AddWithValue("@flds", flds);
            noteCmd.Parameters.AddWithValue("@sfld", front);
            noteCmd.Parameters.AddWithValue("@csum", csum);
            await noteCmd.ExecuteNonQueryAsync();

            await using var cardCmd = conn.CreateCommand();
            cardCmd.CommandText = "INSERT INTO cards (id,nid,did,ord,mod,usn,type,queue,due,ivl,factor,reps,lapses,left,odue,odid,flags,data) VALUES (@id,@nid,@did,0,@mod,-1,0,0,@due,0,0,0,0,0,0,0,0,'');";
            cardCmd.Parameters.AddWithValue("@id", cardId);
            cardCmd.Parameters.AddWithValue("@nid", noteId);
            cardCmd.Parameters.AddWithValue("@did", DeckId);
            cardCmd.Parameters.AddWithValue("@mod", now);
            cardCmd.Parameters.AddWithValue("@due", i + 1);
            await cardCmd.ExecuteNonQueryAsync();
        }
    }

    private static string BuildFront(VocabEntry entry)
    {
        var pos = string.IsNullOrWhiteSpace(entry.Pos) ? "" : $" <i>({HtmlEncode(entry.Pos)})</i>";
        return HtmlEncode(entry.Word) + pos;
    }

    private static string BuildBack(VocabEntry entry)
    {
        var sb = new StringBuilder(HtmlEncode(entry.Translation));
        if (!string.IsNullOrWhiteSpace(entry.ContextSentence))
            sb.Append($"<br><br><small><i>{HtmlEncode(entry.ContextSentence)}</i></small>");
        return sb.ToString();
    }

    private static string HtmlEncode(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string GenerateGuid(Random rng)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Range(0, 10).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }

    private static long ComputeCsum(string field)
    {
        var text = field.Length > 8 ? field[..8] : field;
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(text));
        var hex = Convert.ToHexString(hash)[..8];
        return Convert.ToInt64(hex, 16);
    }

    private static async Task ExecAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
