using LinguaLens.Core.Models;
using LinguaLens.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinguaLens.Tests;

/// <summary>
/// Integration tests for SqliteTranslationCache using an in-memory SQLite connection.
/// The SqliteConnection is kept open for the lifetime of the test so the in-memory DB persists.
/// </summary>
public class SqliteTranslationCacheTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LinguaLensDbContext _db;
    private readonly SqliteTranslationCache _cache;

    public SqliteTranslationCacheTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LinguaLensDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new LinguaLensDbContext(options);
        _db.Database.EnsureCreated();

        _cache = new SqliteTranslationCache(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static TranslationResult MakeResult(string word = "test") => new(
        word, "en", "noun", "[tɛst]", "тест", "тестовый объект",
        [new ExamplePair("This is a test.", "Это тест.")]);

    // ── BuildKey ──────────────────────────────────────────────────────────────

    [Fact]
    public void BuildKey_SameInputs_ReturnsSameKey()
    {
        var key1 = _cache.BuildKey("en", "hello", "Hello world");
        var key2 = _cache.BuildKey("en", "hello", "Hello world");

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void BuildKey_DifferentLang_ReturnsDifferentKey()
    {
        var keyEn = _cache.BuildKey("en", "hello", "Hello world");
        var keyEs = _cache.BuildKey("es", "hello", "Hello world");

        Assert.NotEqual(keyEn, keyEs);
    }

    [Fact]
    public void BuildKey_DifferentWords_ReturnsDifferentKey()
    {
        var key1 = _cache.BuildKey("en", "hello", "Hello world");
        var key2 = _cache.BuildKey("en", "world", "Hello world");

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void BuildKey_WordCaseInsensitive_SameKey()
    {
        var key1 = _cache.BuildKey("en", "Hello", "ctx");
        var key2 = _cache.BuildKey("en", "hello", "ctx");

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void BuildKey_HasCorrectFormat()
    {
        var key = _cache.BuildKey("en", "test", "some sentence");

        // Format: "{lang}:{word}:{8-char hex hash}"
        var parts = key.Split(':');
        Assert.Equal(3, parts.Length);
        Assert.Equal("en", parts[0]);
        Assert.Equal("test", parts[1]);
        Assert.Equal(8, parts[2].Length);
    }

    [Fact]
    public void BuildKey_SentenceTruncatedAt100Chars_SameHashForLongSentences()
    {
        var sentence100 = new string('a', 100);
        var sentence200 = sentence100 + new string('b', 100); // first 100 chars identical

        var key1 = _cache.BuildKey("en", "word", sentence100);
        var key2 = _cache.BuildKey("en", "word", sentence200);

        // Context hash is SHA256 of first 100 chars — both should match
        Assert.Equal(key1, key2);
    }

    // ── GetAsync / SetAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsNull()
    {
        var result = await _cache.GetAsync("en:nonexistent:00000000");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsCachedResult()
    {
        var result = MakeResult();
        var key = _cache.BuildKey("en", "test", "This is a test.");

        await _cache.SetAsync(key, result);
        var cached = await _cache.GetAsync(key);

        Assert.NotNull(cached);
        Assert.Equal(result.Word, cached.Word);
        Assert.Equal(result.Translation, cached.Translation);
        Assert.Equal(result.Pos, cached.Pos);
        Assert.Equal(result.Transcription, cached.Transcription);
        Assert.Equal(result.Comment, cached.Comment);
    }

    [Fact]
    public async Task GetAsync_ExistingEntry_IncrementsHitCount()
    {
        var result = MakeResult();
        var key = _cache.BuildKey("en", "test", "ctx");
        await _cache.SetAsync(key, result);

        // Initial hit count should be 0
        var entry = _db.TranslationCache.First(e => e.CacheKey == key);
        Assert.Equal(0, entry.HitCount);

        await _cache.GetAsync(key);
        await _db.Entry(entry).ReloadAsync();
        Assert.Equal(1, entry.HitCount);

        await _cache.GetAsync(key);
        await _db.Entry(entry).ReloadAsync();
        Assert.Equal(2, entry.HitCount);
    }

    [Fact]
    public async Task SetAsync_DuplicateKey_DoesNotInsertSecondEntry()
    {
        var result = MakeResult();
        var key = _cache.BuildKey("en", "test", "ctx");

        await _cache.SetAsync(key, result);
        await _cache.SetAsync(key, result); // second insert must be ignored

        var count = _db.TranslationCache.Count(e => e.CacheKey == key);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SetAsync_PreservesExamplesInJson()
    {
        var result = MakeResult();
        var key = _cache.BuildKey("en", "test", "ctx");
        await _cache.SetAsync(key, result);

        var cached = await _cache.GetAsync(key);

        Assert.NotNull(cached);
        Assert.Single(cached.Examples);
        Assert.Equal("This is a test.", cached.Examples[0].Original);
        Assert.Equal("Это тест.", cached.Examples[0].Translation);
    }

    [Fact]
    public async Task SetAsync_StoresWordInEntry()
    {
        var result = MakeResult("hello");
        var key = _cache.BuildKey("en", "hello", "ctx");
        await _cache.SetAsync(key, result);

        var entry = _db.TranslationCache.Single(e => e.CacheKey == key);
        Assert.Equal("hello", entry.Word);
    }
}
