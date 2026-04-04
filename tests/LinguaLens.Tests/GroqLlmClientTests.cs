using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using LinguaLens.Core.Models;
using LinguaLens.Infrastructure.Llm;
using Moq;
using Moq.Protected;
using Xunit;

namespace LinguaLens.Tests;

public class GroqLlmClientTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static HttpClient BuildHttpClient(string contentJson)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(contentJson, Encoding.UTF8, "application/json")
            });

        return new HttpClient(handlerMock.Object);
    }

    /// <summary>Wraps inner content in the Groq chat-completion response envelope.</summary>
    private static string GroqEnvelope(string innerContent) =>
        JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = innerContent } } }
        });

    private static TranslationResult SampleResult() => new(
        "hello", "en", "noun", "[həˈloʊ]", "привет", "приветствие", []);

    private static string SampleResultJson() => JsonSerializer.Serialize(new
    {
        word = "hello",
        detected_lang = "en",
        pos = "noun",
        transcription = "[həˈloʊ]",
        translation = "привет",
        comment = "приветствие",
        examples = Array.Empty<object>()
    });

    // ── TranslateWordAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task TranslateWordAsync_ValidJson_ReturnsTranslationResult()
    {
        var payload = GroqEnvelope(SampleResultJson());
        var client = new GroqLlmClient(BuildHttpClient(payload));

        var result = await client.TranslateWordAsync("hello", "Hello world", "en", CancellationToken.None);

        Assert.Equal("hello", result.Word);
        Assert.Equal("en", result.DetectedLang);
        Assert.Equal("noun", result.Pos);
        Assert.Equal("[həˈloʊ]", result.Transcription);
        Assert.Equal("привет", result.Translation);
        Assert.Equal("приветствие", result.Comment);
    }

    [Fact]
    public async Task TranslateWordAsync_JsonWithMarkdownFences_StripsFencesAndParses()
    {
        var withFences = "```json\n" + SampleResultJson() + "\n```";
        var payload = GroqEnvelope(withFences);
        var client = new GroqLlmClient(BuildHttpClient(payload));

        var result = await client.TranslateWordAsync("hello", "Hello world", "en", CancellationToken.None);

        Assert.Equal("hello", result.Word);
        Assert.Equal("привет", result.Translation);
    }

    [Fact]
    public async Task TranslateWordAsync_JsonWithPlainMarkdownFences_StripsFencesAndParses()
    {
        var withFences = "```\n" + SampleResultJson() + "\n```";
        var payload = GroqEnvelope(withFences);
        var client = new GroqLlmClient(BuildHttpClient(payload));

        var result = await client.TranslateWordAsync("hello", "Hello world", "en", CancellationToken.None);

        Assert.Equal("hello", result.Word);
    }

    [Fact]
    public async Task TranslateWordAsync_WithExamples_DeserializesExamples()
    {
        var json = JsonSerializer.Serialize(new
        {
            word = "run",
            detected_lang = "en",
            pos = "verb",
            transcription = "[rʌn]",
            translation = "бежать",
            comment = "",
            examples = new[]
            {
                new { original = "I run every day", translation = "Я бегаю каждый день" },
                new { original = "Run fast!", translation = "Беги быстро!" }
            }
        });

        var payload = GroqEnvelope(json);
        var client = new GroqLlmClient(BuildHttpClient(payload));

        var result = await client.TranslateWordAsync("run", "I run every day", "en", CancellationToken.None);

        Assert.Equal(2, result.Examples.Count);
        Assert.Equal("I run every day", result.Examples[0].Original);
        Assert.Equal("Я бегаю каждый день", result.Examples[0].Translation);
    }

    [Fact]
    public async Task TranslateWordAsync_InvalidJson_ThrowsTranslationParseException()
    {
        var payload = GroqEnvelope("not valid json at all {{{{");
        var client = new GroqLlmClient(BuildHttpClient(payload));

        await Assert.ThrowsAsync<TranslationParseException>(
            () => client.TranslateWordAsync("hello", "Hello world", "en", CancellationToken.None));
    }

    [Fact]
    public async Task TranslateWordAsync_InvalidJson_ExceptionContainsRawResponse()
    {
        const string rawContent = "oops not json";
        var payload = GroqEnvelope(rawContent);
        var client = new GroqLlmClient(BuildHttpClient(payload));

        var ex = await Assert.ThrowsAsync<TranslationParseException>(
            () => client.TranslateWordAsync("hello", "ctx", "en", CancellationToken.None));

        Assert.Equal(rawContent, ex.RawResponse);
    }

    // ── TranslateSentenceAsync ───────────────────────────────────────────────

    [Fact]
    public async Task TranslateSentenceAsync_ValidJson_ReturnsSentenceResult()
    {
        var json = JsonSerializer.Serialize(new
        {
            translation = "Привет мир",
            comment = ""
        });
        var payload = GroqEnvelope(json);
        var client = new GroqLlmClient(BuildHttpClient(payload));

        var result = await client.TranslateSentenceAsync("Hello world", "en", CancellationToken.None);

        Assert.Equal("Привет мир", result.Translation);
        Assert.Equal("", result.Comment);
    }

    [Fact]
    public async Task TranslateSentenceAsync_JsonWithMarkdownFences_Parses()
    {
        var json = "```json\n" + JsonSerializer.Serialize(new
        {
            translation = "Привет мир",
            comment = "нюанс"
        }) + "\n```";

        var payload = GroqEnvelope(json);
        var client = new GroqLlmClient(BuildHttpClient(payload));

        var result = await client.TranslateSentenceAsync("Hello world", "en", CancellationToken.None);

        Assert.Equal("Привет мир", result.Translation);
        Assert.Equal("нюанс", result.Comment);
    }

    [Fact]
    public async Task TranslateSentenceAsync_InvalidJson_ThrowsTranslationParseException()
    {
        var payload = GroqEnvelope("{broken");
        var client = new GroqLlmClient(BuildHttpClient(payload));

        await Assert.ThrowsAsync<TranslationParseException>(
            () => client.TranslateSentenceAsync("Hello", "en", CancellationToken.None));
    }

    // ── HTTP error handling ──────────────────────────────────────────────────

    [Fact]
    public async Task TranslateWordAsync_HttpError_ThrowsHttpRequestException()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized });

        var client = new GroqLlmClient(new HttpClient(handlerMock.Object));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.TranslateWordAsync("hello", "ctx", "en", CancellationToken.None));
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task TranslateWordAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var client = new GroqLlmClient(new HttpClient(handlerMock.Object));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TranslateWordAsync("hello", "ctx", "en", cts.Token));
    }
}
