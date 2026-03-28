namespace LinguaLens.Core.Models;

public class TranslationParseException : Exception
{
    public string RawResponse { get; }

    public TranslationParseException(string rawResponse, Exception? inner = null)
        : base("Failed to parse LLM translation response.", inner)
    {
        RawResponse = rawResponse;
    }
}
