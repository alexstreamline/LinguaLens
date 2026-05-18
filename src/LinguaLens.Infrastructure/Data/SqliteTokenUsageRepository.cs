using Microsoft.EntityFrameworkCore;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;

namespace LinguaLens.Infrastructure.Data;

/// <summary>
/// Stores LLM token usage records in SQLite for daily/monthly tracking.
/// </summary>
public class SqliteTokenUsageRepository : ITokenUsageRepository
{
    private readonly LinguaLensDbContext _context;

    public SqliteTokenUsageRepository(LinguaLensDbContext context)
    {
        _context = context;
    }

    public async Task RecordAsync(int promptTokens, int completionTokens, string provider, string model, string mode)
    {
        _context.TokenUsage.Add(new TokenUsageEntity
        {
            Timestamp = DateTime.UtcNow,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            Provider = provider,
            Model = model,
            Mode = mode
        });
        await _context.SaveChangesAsync();
    }

    public async Task<DailyUsageSummary> GetTodaySummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var entries = await _context.TokenUsage
            .Where(e => e.Timestamp >= today && e.Timestamp < tomorrow)
            .ToListAsync();

        var totalTokens = entries.Sum(e => e.PromptTokens + e.CompletionTokens);
        var requestCount = entries.Count;
        var estimatedCost = EstimateCost(entries);

        return new DailyUsageSummary(today, totalTokens, requestCount, estimatedCost);
    }

    public async Task<DailyUsageSummary> GetMonthSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var entries = await _context.TokenUsage
            .Where(e => e.Timestamp >= monthStart)
            .ToListAsync();

        var totalTokens = entries.Sum(e => e.PromptTokens + e.CompletionTokens);
        var requestCount = entries.Count;
        var estimatedCost = EstimateCost(entries);

        return new DailyUsageSummary(monthStart, totalTokens, requestCount, estimatedCost);
    }

    public async Task ResetAsync()
    {
        var all = await _context.TokenUsage.ToListAsync();
        _context.TokenUsage.RemoveRange(all);
        await _context.SaveChangesAsync();
    }

    private static decimal EstimateCost(IEnumerable<TokenUsageEntity> entries)
    {
        // Groq is free; Gemini Flash ~$0.075/1M input tokens, $0.30/1M output tokens
        decimal total = 0;
        foreach (var e in entries)
        {
            if (e.Provider == "gemini")
            {
                total += (decimal)e.PromptTokens / 1_000_000m * 0.075m;
                total += (decimal)e.CompletionTokens / 1_000_000m * 0.30m;
            }
            // Groq is free
        }
        return total;
    }
}
