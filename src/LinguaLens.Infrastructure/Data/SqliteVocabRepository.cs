using System.Text.Json;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LinguaLens.Infrastructure.Data;

/// <summary>
/// EF Core + SQLite vocabulary repository.
/// SaveAsync skips duplicates (same word+lang on same day).
/// GetAllAsync ordered by created_at DESC.
/// </summary>
public class SqliteVocabRepository(LinguaLensDbContext db) : IVocabRepository
{
    public async Task SaveAsync(TranslationResult result, string contextSentence, string sourceApp)
    {
        var today = DateTime.UtcNow.Date;
        var exists = await db.VocabEntries.AnyAsync(e =>
            e.Word == result.Word &&
            e.DetectedLang == result.DetectedLang &&
            e.CreatedAt >= today &&
            e.CreatedAt < today.AddDays(1));

        if (exists)
            return;

        db.VocabEntries.Add(new VocabEntryEntity
        {
            Word = result.Word,
            DetectedLang = result.DetectedLang,
            Translation = result.Translation,
            Pos = result.Pos,
            ContextSentence = contextSentence,
            SourceApp = sourceApp,
            ResponseJson = JsonSerializer.Serialize(result),
            CreatedAt = DateTime.UtcNow,
            IsLearned = false
        });
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<VocabEntry>> GetAllAsync(string? langFilter = null, bool? isLearnedFilter = null)
    {
        var query = db.VocabEntries.AsQueryable();

        if (langFilter is not null)
            query = query.Where(e => e.DetectedLang == langFilter);

        if (isLearnedFilter is not null)
            query = query.Where(e => e.IsLearned == isLearnedFilter.Value);

        var entities = await query.OrderByDescending(e => e.CreatedAt).ToListAsync();

        return entities.Select(e => new VocabEntry(
            e.Id,
            e.Word,
            e.DetectedLang,
            e.Translation,
            e.Pos,
            e.ContextSentence,
            e.SourceApp,
            e.ResponseJson,
            e.CreatedAt,
            e.IsLearned)).ToList();
    }

    public async Task MarkLearnedAsync(int id, bool learned)
    {
        var entry = await db.VocabEntries.FindAsync(id);
        if (entry is null) return;
        entry.IsLearned = learned;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entry = await db.VocabEntries.FindAsync(id);
        if (entry is null) return;
        db.VocabEntries.Remove(entry);
        await db.SaveChangesAsync();
    }
}
