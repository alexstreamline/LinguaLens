using LinguaLens.Core.Models;

namespace LinguaLens.Core.Interfaces;

public interface ITokenUsageRepository
{
    Task RecordAsync(int promptTokens, int completionTokens, string provider, string model, string mode);
    Task<DailyUsageSummary> GetTodaySummaryAsync();
    Task<DailyUsageSummary> GetMonthSummaryAsync();
    Task ResetAsync();
}
