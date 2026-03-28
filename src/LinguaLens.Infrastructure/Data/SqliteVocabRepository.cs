using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.Data;

/// <summary>
/// EF Core + SQLite vocabulary repository.
/// SaveAsync skips duplicates (same word+lang on same day).
/// GetAllAsync ordered by created_at DESC.
/// </summary>
public class SqliteVocabRepository(LinguaLensDbContext db) : IVocabRepository
{
    public Task SaveAsync(TranslationResult result, string contextSentence, string sourceApp)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<VocabEntry>> GetAllAsync(string? langFilter = null, bool? isLearnedFilter = null)
    {
        throw new NotImplementedException();
    }

    public Task MarkLearnedAsync(int id, bool learned)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(int id)
    {
        throw new NotImplementedException();
    }
}
