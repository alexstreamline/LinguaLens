using LinguaLens.Core.Interfaces;

namespace LinguaLens.Infrastructure.Llm;

/// <summary>
/// Selects the active LLM client based on IAppSettings.LlmProvider at call time,
/// allowing the provider to be switched in settings without restarting the app.
/// </summary>
public class LlmClientFactory : ILlmClientFactory
{
    private readonly IAppSettings _settings;
    private readonly GroqLlmClient _groq;
    private readonly GeminiLlmClient _gemini;

    public LlmClientFactory(IAppSettings settings, GroqLlmClient groq, GeminiLlmClient gemini)
    {
        _settings = settings;
        _groq = groq;
        _gemini = gemini;
    }

    public virtual ILlmClient Create() => _settings.LlmProvider switch
    {
        "gemini" => _gemini,
        _        => _groq
    };
}
