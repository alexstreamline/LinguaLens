using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LinguaLens.Infrastructure.Data;

/// <summary>
/// EF Core + SQLite translation cache.
/// BuildKey: "{lang}:{word.ToLower()}:{SHA256(first100chars)[..8]}"
/// On Get: increments hit_count. No TTL in v1.
/// </summary>
public class SqliteTranslationCache(LinguaLensDbContext db) : ITranslationCache
{
    public async Task<TranslationResult?> GetAsync(string cacheKey)
    {
        var entry = await db.TranslationCache
            .FirstOrDefaultAsync(e => e.CacheKey == cacheKey);

        if (entry is null)
            return null;

        entry.HitCount++;
        await db.SaveChangesAsync();

        return JsonSerializer.Deserialize<TranslationResult>(entry.ResponseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task SetAsync(string cacheKey, TranslationResult result)
    {
        var existing = await db.TranslationCache
            .FirstOrDefaultAsync(e => e.CacheKey == cacheKey);

        if (existing is not null)
            return;

        db.TranslationCache.Add(new CacheEntry
        {
            CacheKey = cacheKey,
            Word = result.Word,
            ResponseJson = JsonSerializer.Serialize(result),
            CreatedAt = DateTime.UtcNow,
            HitCount = 0
        });
        await db.SaveChangesAsync();
    }

    public string BuildKey(string lang, string word, string sentence)
    {
        var context = sentence.Length > 100 ? sentence[..100] : sentence;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(context));
        var hash = Convert.ToHexString(bytes)[..8].ToLower();
        return $"{lang}:{word.ToLower()}:{hash}";
    }
}
