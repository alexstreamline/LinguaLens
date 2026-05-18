using Microsoft.EntityFrameworkCore;

namespace LinguaLens.Infrastructure.Data;

public class LinguaLensDbContext : DbContext
{
    public DbSet<CacheEntry> TranslationCache => Set<CacheEntry>();
    public DbSet<VocabEntryEntity> VocabEntries => Set<VocabEntryEntity>();
    public DbSet<TokenUsageEntity> TokenUsage => Set<TokenUsageEntity>();

    public LinguaLensDbContext(DbContextOptions<LinguaLensDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CacheEntry>(e =>
        {
            e.ToTable("translation_cache");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CacheKey).IsUnique();
        });

        modelBuilder.Entity<VocabEntryEntity>(e =>
        {
            e.ToTable("vocab_entries");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<TokenUsageEntity>(e =>
        {
            e.ToTable("token_usage");
            e.HasKey(x => x.Id);
        });
    }
}

public class CacheEntry
{
    public int Id { get; set; }
    public string CacheKey { get; set; } = "";
    public string Word { get; set; } = "";
    public string ResponseJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int HitCount { get; set; }
}

public class VocabEntryEntity
{
    public int Id { get; set; }
    public string Word { get; set; } = "";
    public string DetectedLang { get; set; } = "";
    public string Translation { get; set; } = "";
    public string Pos { get; set; } = "";
    public string ContextSentence { get; set; } = "";
    public string SourceApp { get; set; } = "";
    public string ResponseJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsLearned { get; set; }
}

public class TokenUsageEntity
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string Mode { get; set; } = "";
}
