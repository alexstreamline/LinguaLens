using System.Windows;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Models;
using LinguaLens.Core.Services;
using Moq;
using Xunit;

namespace LinguaLens.Tests;

public class TranslationOrchestratorTests
{
    // ── fixtures ─────────────────────────────────────────────────────────────

    private readonly Mock<ITextExtractor> _extractor = new();
    private readonly Mock<ILanguageDetector> _detector = new();
    private readonly Mock<ITranslationCache> _cache = new();
    private readonly Mock<ILlmClient> _llmClient = new();
    private readonly Mock<IVocabRepository> _vocab = new();
    private readonly Mock<IAppSettings> _settings = new();

    private TranslationOrchestrator CreateOrchestrator() => new(
        _extractor.Object,
        _detector.Object,
        _cache.Object,
        _llmClient.Object,
        _vocab.Object,
        _settings.Object);

    private static TranslationResult FakeResult(string word = "test") => new(
        word, "en", "noun", "[tɛst]", "тест", "", []);

    private void SetupDefaultSettings(bool detectEn = true, bool detectEs = true, bool autoSave = true)
    {
        _settings.SetupGet(s => s.DetectEnglish).Returns(detectEn);
        _settings.SetupGet(s => s.DetectSpanish).Returns(detectEs);
        _settings.SetupGet(s => s.AutoSaveToVocab).Returns(autoSave);
    }

    // ── ProcessHoverAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessHoverAsync_ExtractorReturnsNull_ReturnsNull()
    {
        SetupDefaultSettings();
        _extractor.Setup(e => e.ExtractWordAtPointAsync(It.IsAny<Point>()))
            .ReturnsAsync((WordExtractionResult?)null);

        var result = await CreateOrchestrator().ProcessHoverAsync(new Point(0, 0), CancellationToken.None);

        Assert.Null(result);
        _llmClient.Verify(l => l.TranslateWordAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessHoverAsync_ExtractorReturnsEmptyWord_ReturnsNull()
    {
        SetupDefaultSettings();
        _extractor.Setup(e => e.ExtractWordAtPointAsync(It.IsAny<Point>()))
            .ReturnsAsync(new WordExtractionResult("", "ctx", "app", new Point(0, 0)));

        var result = await CreateOrchestrator().ProcessHoverAsync(new Point(0, 0), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessHoverAsync_DetectorReturnsNull_ReturnsNull()
    {
        SetupDefaultSettings();
        _extractor.Setup(e => e.ExtractWordAtPointAsync(It.IsAny<Point>()))
            .ReturnsAsync(new WordExtractionResult("привет", "ctx", "app", new Point(0, 0)));
        _detector.Setup(d => d.Detect("привет")).Returns((string?)null);

        var result = await CreateOrchestrator().ProcessHoverAsync(new Point(0, 0), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessHoverAsync_CacheHit_ReturnsCachedResultWithoutCallingLlm()
    {
        SetupDefaultSettings();
        var extraction = new WordExtractionResult("test", "This is a test.", "app", new Point(0, 0));
        _extractor.Setup(e => e.ExtractWordAtPointAsync(It.IsAny<Point>())).ReturnsAsync(extraction);
        _detector.Setup(d => d.Detect("test")).Returns("en");
        _cache.Setup(c => c.BuildKey("en", "test", "This is a test.")).Returns("en:test:abc12345");
        _cache.Setup(c => c.GetAsync("en:test:abc12345")).ReturnsAsync(FakeResult("test"));

        var result = await CreateOrchestrator().ProcessHoverAsync(new Point(0, 0), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("test", result.Word);
        _llmClient.Verify(l => l.TranslateWordAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessHoverAsync_CacheMiss_CallsLlmAndSavesToCache()
    {
        SetupDefaultSettings();
        var extraction = new WordExtractionResult("test", "ctx", "app", new Point(0, 0));
        _extractor.Setup(e => e.ExtractWordAtPointAsync(It.IsAny<Point>())).ReturnsAsync(extraction);
        _detector.Setup(d => d.Detect("test")).Returns("en");
        _cache.Setup(c => c.BuildKey("en", "test", "ctx")).Returns("en:test:key");
        _cache.Setup(c => c.GetAsync("en:test:key")).ReturnsAsync((TranslationResult?)null);
        var llmResult = FakeResult("test");
        _llmClient.Setup(l => l.TranslateWordAsync("test", "ctx", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult);

        var result = await CreateOrchestrator().ProcessHoverAsync(new Point(0, 0), CancellationToken.None);

        Assert.NotNull(result);
        _llmClient.Verify(l => l.TranslateWordAsync("test", "ctx", "en", It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.SetAsync("en:test:key", llmResult), Times.Once);
    }

    [Fact]
    public async Task ProcessHoverAsync_AutoSaveEnabled_SavesToVocab()
    {
        SetupDefaultSettings(autoSave: true);
        var extraction = new WordExtractionResult("test", "ctx", "myapp", new Point(0, 0));
        _extractor.Setup(e => e.ExtractWordAtPointAsync(It.IsAny<Point>())).ReturnsAsync(extraction);
        _detector.Setup(d => d.Detect("test")).Returns("en");
        _cache.Setup(c => c.BuildKey(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("key");
        _cache.Setup(c => c.GetAsync("key")).ReturnsAsync((TranslationResult?)null);
        var llmResult = FakeResult("test");
        _llmClient.Setup(l => l.TranslateWordAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(llmResult);

        await CreateOrchestrator().ProcessHoverAsync(new Point(0, 0), CancellationToken.None);

        _vocab.Verify(v => v.SaveAsync(llmResult, "ctx", "myapp"), Times.Once);
    }

    [Fact]
    public async Task ProcessHoverAsync_AutoSaveDisabled_DoesNotSaveToVocab()
    {
        SetupDefaultSettings(autoSave: false);
        var extraction = new WordExtractionResult("test", "ctx", "app", new Point(0, 0));
        _extractor.Setup(e => e.ExtractWordAtPointAsync(It.IsAny<Point>())).ReturnsAsync(extraction);
        _detector.Setup(d => d.Detect("test")).Returns("en");
        _cache.Setup(c => c.BuildKey(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("key");
        _cache.Setup(c => c.GetAsync("key")).ReturnsAsync((TranslationResult?)null);
        _llmClient.Setup(l => l.TranslateWordAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(FakeResult());

        await CreateOrchestrator().ProcessHoverAsync(new Point(0, 0), CancellationToken.None);

        _vocab.Verify(v => v.SaveAsync(It.IsAny<TranslationResult>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessHoverAsync_DetectEnglishFalse_EnglishWordReturnsNull()
    {
        SetupDefaultSettings(detectEn: false);
        var extraction = new WordExtractionResult("hello", "ctx", "app", new Point(0, 0));
        _extractor.Setup(e => e.ExtractWordAtPointAsync(It.IsAny<Point>())).ReturnsAsync(extraction);
        _detector.Setup(d => d.Detect("hello")).Returns("en");

        var result = await CreateOrchestrator().ProcessHoverAsync(new Point(0, 0), CancellationToken.None);

        Assert.Null(result);
        _llmClient.Verify(l => l.TranslateWordAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessHoverAsync_DetectSpanishFalse_SpanishWordReturnsNull()
    {
        SetupDefaultSettings(detectEs: false);
        var extraction = new WordExtractionResult("año", "ctx", "app", new Point(0, 0));
        _extractor.Setup(e => e.ExtractWordAtPointAsync(It.IsAny<Point>())).ReturnsAsync(extraction);
        _detector.Setup(d => d.Detect("año")).Returns("es");

        var result = await CreateOrchestrator().ProcessHoverAsync(new Point(0, 0), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessHoverAsync_CacheHit_DoesNotSaveToVocab()
    {
        SetupDefaultSettings(autoSave: true);
        var extraction = new WordExtractionResult("test", "ctx", "app", new Point(0, 0));
        _extractor.Setup(e => e.ExtractWordAtPointAsync(It.IsAny<Point>())).ReturnsAsync(extraction);
        _detector.Setup(d => d.Detect("test")).Returns("en");
        _cache.Setup(c => c.BuildKey(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("key");
        _cache.Setup(c => c.GetAsync("key")).ReturnsAsync(FakeResult());

        await CreateOrchestrator().ProcessHoverAsync(new Point(0, 0), CancellationToken.None);

        _vocab.Verify(v => v.SaveAsync(It.IsAny<TranslationResult>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ── ProcessSelectionAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ProcessSelectionAsync_EmptyText_ReturnsNull()
    {
        _extractor.Setup(e => e.ExtractSelectedTextAsync()).ReturnsAsync((string?)null);

        var result = await CreateOrchestrator().ProcessSelectionAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessSelectionAsync_WhitespaceText_ReturnsNull()
    {
        _extractor.Setup(e => e.ExtractSelectedTextAsync()).ReturnsAsync("   ");

        var result = await CreateOrchestrator().ProcessSelectionAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessSelectionAsync_ValidText_CallsLlmWithDetectedLang()
    {
        const string selectedText = "Hello world this is a test";
        _extractor.Setup(e => e.ExtractSelectedTextAsync()).ReturnsAsync(selectedText);
        _detector.Setup(d => d.Detect("Hello")).Returns("en");
        var sentenceResult = new SentenceTranslationResult("Привет мир это тест", "");
        _llmClient.Setup(l => l.TranslateSentenceAsync(selectedText, "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sentenceResult);

        var result = await CreateOrchestrator().ProcessSelectionAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Привет мир это тест", result.Translation);
        _llmClient.Verify(l => l.TranslateSentenceAsync(selectedText, "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessSelectionAsync_UnrecognizedFirstWord_DefaultsToEn()
    {
        // detector returns null for first word → should default to "en"
        const string text = "日本語テキスト example text";
        _extractor.Setup(e => e.ExtractSelectedTextAsync()).ReturnsAsync(text);
        _detector.Setup(d => d.Detect(It.IsAny<string>())).Returns((string?)null);
        _llmClient.Setup(l => l.TranslateSentenceAsync(text, "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SentenceTranslationResult("перевод", ""));

        var result = await CreateOrchestrator().ProcessSelectionAsync(CancellationToken.None);

        Assert.NotNull(result);
        _llmClient.Verify(l => l.TranslateSentenceAsync(text, "en", It.IsAny<CancellationToken>()), Times.Once);
    }
}
