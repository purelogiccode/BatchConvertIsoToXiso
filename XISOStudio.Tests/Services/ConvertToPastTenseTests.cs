using XISOStudio.Services;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests verb past-tense conversion in <c>ConvertToPastTense</c>.</summary>
public class ConvertToPastTenseTests
{
    [Theory]
    [InlineData("conversion", "converted")]
    [InlineData("Conversion", "converted")]
    [InlineData("CONVERSION", "converted")]
    [InlineData("test", "tested")]
    [InlineData("Test", "tested")]
    [InlineData("process", "processed")]
    [InlineData("copy", "copied")]
    [InlineData("move", "moved")]
    [InlineData("create", "created")]
    [InlineData("carry", "carried")]
    [InlineData("play", "played")]
    [InlineData("upload", "uploaded")]
    public void GetPastTenseReturnsExpectedPastTense(string verb, string expected)
    {
        var result = ConvertToPastTense.GetPastTense(verb);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetPastTenseEmptyStringReturnsEd()
    {
        var result = ConvertToPastTense.GetPastTense("");
        Assert.Equal("ed", result);
    }

    [Fact]
    public void GetPastTenseUnknownVerbAppendsEdLowercased()
    {
        var result = ConvertToPastTense.GetPastTense("DOWNLOAD");
        Assert.Equal("downloaded", result);
    }

    [Fact]
    public void GetPastTenseSingleCharacterAppendsEd()
    {
        var result = ConvertToPastTense.GetPastTense("x");
        Assert.Equal("xed", result);
    }

    [Theory]
    [InlineData("dance", "danced")]
    [InlineData("love", "loved")]
    [InlineData("bake", "baked")]
    [InlineData("tie", "tied")]
    [InlineData("free", "freed")]
    [InlineData("see", "seed")]
    [InlineData("be", "bed")]
    [InlineData("e", "ed")]
    public void GetPastTenseVerbsEndingInEReturnVerbPlusD(string verb, string expected)
    {
        var result = ConvertToPastTense.GetPastTense(verb);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("study", "studied")]
    [InlineData("deny", "denied")]
    [InlineData("apply", "applied")]
    [InlineData("dry", "dried")]
    [InlineData("try", "tried")]
    [InlineData("fly", "flied")]
    [InlineData("already", "alreadied")]
    public void GetPastTenseConsonantBeforeYReturnsIed(string verb, string expected)
    {
        var result = ConvertToPastTense.GetPastTense(verb);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("stay", "stayed")]
    [InlineData("enjoy", "enjoyed")]
    [InlineData("buy", "buyed")]
    [InlineData("say", "sayed")]
    [InlineData("key", "keyed")]
    [InlineData("toy", "toyed")]
    [InlineData("obey", "obeyed")]
    public void GetPastTenseVowelBeforeYAppendsEd(string verb, string expected)
    {
        var result = ConvertToPastTense.GetPastTense(verb);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("upload", "uploaded")]
    [InlineData("download", "downloaded")]
    [InlineData("connect", "connected")]
    [InlineData("open", "opened")]
    [InlineData("run", "runed")]
    [InlineData("stop", "stoped")]
    [InlineData("echo", "echoed")]
    [InlineData("visit", "visited")]
    public void GetPastTenseDefaultVerbsAppendEd(string verb, string expected)
    {
        var result = ConvertToPastTense.GetPastTense(verb);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("moved", "moveded")]
    [InlineData("tested", "testeded")]
    [InlineData("played", "playeded")]
    [InlineData("used", "useded")]
    [InlineData("created", "createded")]
    [InlineData("copied", "copieded")]
    public void GetPastTenseAlreadyPastTenseVerbsAppendEdAgain(string verb, string expected)
    {
        var result = ConvertToPastTense.GetPastTense(verb);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("MOVE", "moved")]
    [InlineData("Move", "moved")]
    [InlineData("CoPy", "copied")]
    [InlineData("TEST", "tested")]
    [InlineData("TeSt", "tested")]
    [InlineData("DOWNLOAD", "downloaded")]
    [InlineData("Conversion", "converted")]
    public void GetPastTenseIsCaseInsensitive(string verb, string expected)
    {
        var result = ConvertToPastTense.GetPastTense(verb);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("a", "aed")]
    [InlineData("o", "oed")]
    [InlineData("y", "yed")]
    [InlineData("z", "zed")]
    public void GetPastTenseSingleNonECharacterVerbsAppendEd(string verb, string expected)
    {
        var result = ConvertToPastTense.GetPastTense(verb);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(" ", " ed")]
    [InlineData("\t", "\ted")]
    [InlineData("\n", "\ned")]
    public void GetPastTenseWhitespaceInputAppendsEd(string verb, string expected)
    {
        var result = ConvertToPastTense.GetPastTense(verb);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123", "123ed")]
    [InlineData("42!", "42!ed")]
    public void GetPastTenseNonLetterInputAppendsEd(string verb, string expected)
    {
        var result = ConvertToPastTense.GetPastTense(verb);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetPastTenseLeadingWhitespaceVerbEndingInEReturnsVerbPlusD()
    {
        var result = ConvertToPastTense.GetPastTense(" move");
        Assert.Equal(" moved", result);
    }

    [Fact]
    public void GetPastTenseNullThrowsNullReferenceException()
    {
        Assert.Throws<NullReferenceException>(static () => ConvertToPastTense.GetPastTense(null!));
    }
}