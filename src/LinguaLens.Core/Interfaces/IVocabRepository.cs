using LinguaLens.Core.Models;

namespace LinguaLens.Core.Interfaces;

public interface IVocabRepository
{
    Task SaveAsync(TranslationResult result, string contextSentence, string sourceApp);
    Task<IReadOnlyList<VocabEntry>> GetAllAsync(string? langFilter = null, bool? isLearnedFilter = null);
    Task MarkLearnedAsync(int id, bool learned);
    Task DeleteAsync(int id);
}
