namespace LinguaLens.Core.Interfaces;

/// <summary>
/// Returns the active ILlmClient based on current settings.
/// Resolved at call time so the provider can be switched without restarting.
/// </summary>
public interface ILlmClientFactory
{
    ILlmClient Create();
}
