using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// Cell-by-cell coverage of <see cref="JsonSurrogateEscapes.FirstUnpaired"/> — the one
/// authority both json.loads doors consult before parsing (#1487).
/// </summary>
public class JsonSurrogateEscapesTests
{
    [Fact]
    public void LoneHighSurrogate_IsUnpaired()
    {
        var result = JsonSurrogateEscapes.FirstUnpaired("\"\\ud800\"");
        Assert.NotNull(result);
        Assert.Equal("\\ud800", result!.Value.escapeText);
        Assert.Equal(1, result.Value.position);
    }

    [Fact]
    public void LoneHighSurrogate_UpperCase_IsUnpaired()
    {
        var result = JsonSurrogateEscapes.FirstUnpaired("\"\\uD800\"");
        Assert.NotNull(result);
        Assert.Equal("\\uD800", result!.Value.escapeText);
    }

    [Fact]
    public void LoneLowSurrogate_IsUnpaired()
    {
        var result = JsonSurrogateEscapes.FirstUnpaired("\"\\udc00\"");
        Assert.NotNull(result);
        Assert.Equal("\\udc00", result!.Value.escapeText);
        Assert.Equal(1, result.Value.position);
    }

    [Fact]
    public void ReversedSurrogates_LowBeforeHigh_IsUnpaired()
    {
        var result = JsonSurrogateEscapes.FirstUnpaired("\"\\udc00\\ud800\"");
        Assert.NotNull(result);
        // The low surrogate comes first — it is immediately unpaired
        Assert.Equal("\\udc00", result!.Value.escapeText);
        Assert.Equal(1, result.Value.position);
    }

    [Fact]
    public void ValidSurrogatePair_Passes()
    {
        var result = JsonSurrogateEscapes.FirstUnpaired("\"\\ud83d\\ude00\"");
        Assert.Null(result);
    }

    [Fact]
    public void ValidSurrogatePair_MixedCase_Passes()
    {
        var result = JsonSurrogateEscapes.FirstUnpaired("\"\\uD83D\\uDE00\"");
        Assert.Null(result);
    }

    [Fact]
    public void EscapedBackslash_NotAFalsePositive()
    {
        // "\\ud800" in JSON means literal text \ud800, not an escape sequence.
        // The scanner must not trip on it.
        var result = JsonSurrogateEscapes.FirstUnpaired("\"\\\\ud800\"");
        Assert.Null(result);
    }

    [Fact]
    public void HighSurrogateFollowedByNonSurrogate_IsUnpaired()
    {
        // \ud800 followed by \u0041 (A) — not a low surrogate
        var result = JsonSurrogateEscapes.FirstUnpaired("\"\\ud800\\u0041\"");
        Assert.NotNull(result);
        Assert.Equal("\\ud800", result!.Value.escapeText);
    }

    [Fact]
    public void NonSurrogateEscapes_Pass()
    {
        var result = JsonSurrogateEscapes.FirstUnpaired("\"\\u00e9\\u4e2d\"");
        Assert.Null(result);
    }

    [Fact]
    public void NoStringContent_Passes()
    {
        Assert.Null(JsonSurrogateEscapes.FirstUnpaired("42"));
        Assert.Null(JsonSurrogateEscapes.FirstUnpaired("{}"));
        Assert.Null(JsonSurrogateEscapes.FirstUnpaired("[]"));
    }

    [Fact]
    public void EmptyString_Passes()
    {
        Assert.Null(JsonSurrogateEscapes.FirstUnpaired("\"\""));
    }

    [Fact]
    public void SurrogateEscapeOutsideString_Ignored()
    {
        // \ud800 outside any JSON string — not our concern, parser rejects the document separately
        Assert.Null(JsonSurrogateEscapes.FirstUnpaired("\\ud800"));
    }

    [Fact]
    public void HighSurrogateAtEndOfString_IsUnpaired()
    {
        var result = JsonSurrogateEscapes.FirstUnpaired("[\"abc\\ud800\"]");
        Assert.NotNull(result);
        Assert.Equal("\\ud800", result!.Value.escapeText);
    }

    [Fact]
    public void MultiplePairs_AllValid_Passes()
    {
        var result = JsonSurrogateEscapes.FirstUnpaired("\"\\ud83d\\ude00\\ud83d\\ude01\"");
        Assert.Null(result);
    }

    [Fact]
    public void MixedValidAndLoneSurrogate_FindsFirst()
    {
        // Valid pair followed by lone high surrogate
        var result = JsonSurrogateEscapes.FirstUnpaired("\"\\ud83d\\ude00\\ud800\"");
        Assert.NotNull(result);
        Assert.Equal("\\ud800", result!.Value.escapeText);
    }

    [Fact]
    public void HighSurrogateInFirstString_FoundDespiteMultipleStrings()
    {
        var result = JsonSurrogateEscapes.FirstUnpaired("{\"a\": \"\\ud800\", \"b\": \"ok\"}");
        Assert.NotNull(result);
        Assert.Equal("\\ud800", result!.Value.escapeText);
    }

    [Fact]
    public void LoneSurrogateInKey_IsDetected()
    {
        var result = JsonSurrogateEscapes.FirstUnpaired("{\"\\ud800\": 1}");
        Assert.NotNull(result);
        Assert.Equal("\\ud800", result!.Value.escapeText);
    }
}
