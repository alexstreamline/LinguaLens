using LinguaLens.Infrastructure.Language;
using Xunit;

namespace LinguaLens.Tests;

public class SimpleLanguageDetectorTests
{
    private readonly SimpleLanguageDetector _detector = new();

    // --- English ---

    [Theory]
    [InlineData("hello")]
    [InlineData("Hello")]
    [InlineData("WORLD")]
    [InlineData("running")]
    [InlineData("it's")]
    [InlineData("well-known")]
    [InlineData("ok")]
    public void Detect_EnglishWords_ReturnsEn(string word)
    {
        Assert.Equal("en", _detector.Detect(word));
    }

    [Theory]
    [InlineData("hello", "en")]
    [InlineData("Hello", "en")]
    [InlineData("WORLD", "en")]
    [InlineData("running", "en")]
    [InlineData("it's", "en")]
    [InlineData("well-known", "en")]
    [InlineData("ab", "en")]
    public void Detect_EnglishWords_TableDriven(string word, string expected)
    {
        Assert.Equal(expected, _detector.Detect(word));
    }

    // --- Spanish ---

    [Theory]
    [InlineData("año", "es")]
    [InlineData("niño", "es")]
    [InlineData("café", "es")]
    [InlineData("señor", "es")]
    [InlineData("jalapeño", "es")]
    [InlineData("acción", "es")]
    [InlineData("fútbol", "es")]
    [InlineData("güero", "es")]
    public void Detect_SpanishWords_TableDriven(string word, string expected)
    {
        Assert.Equal(expected, _detector.Detect(word));
    }

    // --- Null cases ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]       // too short (< 2 chars)
    [InlineData("z")]       // too short
    [InlineData("123")]     // pure numbers
    [InlineData("3.14")]    // pure number with decimal
    [InlineData("100,000")] // pure number with comma
    [InlineData("привет")]  // Cyrillic
    [InlineData("мир")]     // Cyrillic
    [InlineData("日本語")]   // Japanese
    [InlineData("αβγ")]     // Greek
    public void Detect_NullCases_ReturnsNull(string? word)
    {
        Assert.Null(_detector.Detect(word!));
    }

    [Fact]
    public void Detect_TwoCharEnglishWord_ReturnsEn()
    {
        Assert.Equal("en", _detector.Detect("ok"));
    }

    [Fact]
    public void Detect_OneCharWord_ReturnsNull()
    {
        Assert.Null(_detector.Detect("a"));
    }

    [Fact]
    public void Detect_PureNumber_ReturnsNull()
    {
        Assert.Null(_detector.Detect("42"));
    }

    [Fact]
    public void Detect_WordWithNonLatinLetters_ReturnsNull()
    {
        // Mixed latin + cyrillic
        Assert.Null(_detector.Detect("testтест"));
    }

    [Fact]
    public void Detect_SpanishSpecialChar_IgnoresCase()
    {
        // Uppercase Spanish char
        Assert.Equal("es", _detector.Detect("SEÑOR"));
    }

    [Fact]
    public void Detect_WordWithApostropheOnly_ReturnsNull()
    {
        // Only non-letter chars with apostrophe, no letters → null (no IsLetter check passes)
        Assert.Null(_detector.Detect("''"));
    }

    [Fact]
    public void Detect_EnglishWordWithHyphenAndApostrophe_ReturnsEn()
    {
        Assert.Equal("en", _detector.Detect("don't"));
    }
}
