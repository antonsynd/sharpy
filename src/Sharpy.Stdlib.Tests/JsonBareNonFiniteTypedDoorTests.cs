using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// A bare non-finite token is a NUMBER, not a string, at every target (#1488).
///
/// <para>
/// CPython reads <c>json.loads("NaN")</c> as a float, so <c>loads[str]("NaN")</c> owes a decode
/// error. Sharpy answered <c>Ok("NaN")</c>, because the pre-edit that makes bare tokens survive
/// <c>Utf8JsonReader</c> tokenization rewrote them to the QUOTED spelling — erasing the one fact
/// it knew and the reader could not recover, that the token was bare. A rewritten <c>NaN</c> and
/// an author's <c>"NaN"</c> became the same bytes.
/// </para>
///
/// <para>
/// The pre-edit now writes a sentinel object instead, which <c>BareNonFiniteJsonConverter</c>
/// reads back at any float target and every other target rejects naturally. Design and probe
/// results recorded on #1488.
/// </para>
/// </summary>
public class JsonBareNonFiniteTypedDoorTests
{
    /// <summary>
    /// The flip: a bare token at a STRING target is a decode error, because it is a number.
    /// </summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void BareToken_AtAStringTarget_IsADecodeError(string document)
    {
        Result<string, JSONDecodeError> typed = Json.Loads<string>(document);

        Assert.True(typed.IsErr, $"loads[str]({document}) should refuse — it is a number");
    }

    /// <summary>
    /// The control that gives the flip its meaning, and the cell that must never move: a
    /// genuinely QUOTED token is data. If this moved, the fix would be refusing strings rather
    /// than distinguishing them.
    /// </summary>
    [Theory]
    [InlineData("\"NaN\"")]
    [InlineData("\"Infinity\"")]
    [InlineData("\"-Infinity\"")]
    public void QuotedToken_AtAStringTarget_IsData(string document)
    {
        Result<string, JSONDecodeError> typed = Json.Loads<string>(document);

        Assert.True(typed.IsOk, $"loads[str]({document}) should accept — it is a quoted string");
        Assert.Equal(document.Trim('"'), typed.Unwrap());
    }

    /// <summary>
    /// Nested, so the fix is positional rather than a whole-document special case — the property
    /// the documented fallback design could NOT have provided.
    /// </summary>
    [Fact]
    public void BareToken_NestedAtAStringTarget_IsAlsoADecodeError()
    {
        Assert.True(Json.Loads<List<string>>("[NaN]").IsErr);
        Assert.True(Json.Loads<List<string>>("[\"ok\", NaN]").IsErr);
    }

    /// <summary>
    /// And nested quoted tokens still read as data.
    /// </summary>
    [Fact]
    public void QuotedToken_NestedAtAStringTarget_IsStillData()
    {
        Result<List<string>, JSONDecodeError> typed = Json.Loads<List<string>>("[\"NaN\"]");

        Assert.True(typed.IsOk);
        Assert.Equal("NaN", typed.Unwrap()[0]);
    }

    /// <summary>
    /// The numeric door, at every position a float occupies. This is the half the sentinel exists
    /// to preserve, and the reason a plain "reject bare tokens" fix would have been wrong.
    /// </summary>
    [Fact]
    public void BareToken_AtFloatTargets_StillReadsTheNonFiniteValue()
    {
        Assert.Equal(double.NaN, Json.Loads<double>("NaN").Unwrap());
        Assert.Equal(double.PositiveInfinity, Json.Loads<double>("Infinity").Unwrap());
        Assert.Equal(double.NegativeInfinity, Json.Loads<double>("-Infinity").Unwrap());

        Result<List<double>, JSONDecodeError> list = Json.Loads<List<double>>("[NaN, 1.0]");
        Assert.True(list.IsOk);
        Assert.True(double.IsNaN(list.Unwrap()[0]));
        Assert.Equal(1.0, list.Unwrap()[1]);

        // float, not just double — the converter is registered for both.
        Assert.True(float.IsNaN(Json.Loads<float>("NaN").Unwrap()));
        Assert.Equal(float.PositiveInfinity, Json.Loads<float>("Infinity").Unwrap());
    }

    /// <summary>
    /// The converter REPLACES System.Text.Json's built-in reader for these types, so it has to
    /// keep doing everything the built-in did. Ordinary numbers are the arm most easily lost.
    /// </summary>
    [Fact]
    public void TheConverterStillReadsOrdinaryNumbers()
    {
        Assert.Equal(2.5, Json.Loads<double>("2.5").Unwrap());
        Assert.Equal(-17.0, Json.Loads<double>("-17").Unwrap());
        Assert.Equal(0.0, Json.Loads<double>("0").Unwrap());
        Assert.Equal(1.5f, Json.Loads<float>("1.5").Unwrap());

        Result<List<double>, JSONDecodeError> list = Json.Loads<List<double>>("[1.0, 2.0, 3.5]");
        Assert.True(list.IsOk);
        Assert.Equal(3.5, list.Unwrap()[2]);
    }

    /// <summary>
    /// The quoted spellings <c>AllowNamedFloatingPointLiterals</c> accepts must keep working at a
    /// numeric target — that option is why <c>dumps ∘ loads[float]</c> round-trips (#1353), and
    /// replacing the built-in reader would silently have taken it away.
    /// </summary>
    [Fact]
    public void TheConverterStillReadsTheQuotedSpellingsAtNumericTargets()
    {
        Assert.Equal(double.NaN, Json.Loads<double>("\"NaN\"").Unwrap());
        Assert.Equal(double.PositiveInfinity, Json.Loads<double>("\"Infinity\"").Unwrap());
        Assert.True(Json.Loads<double>("\"nonsense\"").IsErr);
    }

    /// <summary>
    /// #1425 crash safety: a refusal is an <c>Err</c> travelling through the ordinary door, never
    /// an exception escaping <c>loads</c>. Stated as its own cell because "returns Err" and "does
    /// not throw" are different claims and only one of them is checked by the cells above.
    /// </summary>
    [Fact]
    public void ARefusalIsAnErrNotAnEscapedException()
    {
        Result<string, JSONDecodeError> typed = Json.Loads<string>("NaN");

        Assert.True(typed.IsErr);
        Assert.IsType<JSONDecodeError>(typed.UnwrapErr());
    }

    /// <summary>
    /// The round trip #1353 added the pre-edit for, end to end: what <c>dumps</c> writes for a
    /// non-finite float, <c>loads[float]</c> reads back.
    /// </summary>
    [Fact]
    public void DumpsThenLoadsRoundTripsNonFiniteFloats()
    {
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            string written = Json.Dumps(value);
            Result<double, JSONDecodeError> read = Json.Loads<double>(written);

            Assert.True(read.IsOk, $"loads[float] refused what dumps wrote: {written}");
            Assert.Equal(value, read.Unwrap());
        }
    }
}
