using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.Data;

/// <summary>
/// EF Core + SQLite translation cache.
/// BuildKey: "{lang}:{word.ToLower()}:{SHA256(first100chars)[..8]}"
/// On Get: increments hit_count. No TTL in v1.
/// </summary>
public class SqliteTranslationCache(LinguaLensDbContext db) : ITranslationCache
{
    public Task<TranslationResult?> GetAsync(string cacheKey)
    {
        throw new NotImplementedException();
    }

    public Task SetAsync(string cacheKey, TranslationResult result)
    {
        throw new NotImplementedException();
    }

    public string BuildKey(string lang, string word, string sentence)
    {
        throw new NotImplementedException();
    }
}
