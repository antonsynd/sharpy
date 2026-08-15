using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// Feasibility probe for #1488's sentinel-object design, run BEFORE committing to it.
///
/// <para>
/// The defect: <c>Json.QuoteBareNonFiniteTokens</c> rewrites CPython's bare <c>NaN</c> /
/// <c>Infinity</c> / <c>-Infinity</c> into the quoted spellings <c>System.Text.Json</c> accepts,
/// because a bare token fails <c>Utf8JsonReader</c> tokenization before any converter can run
/// (measured, and recorded in <c>Json.cs</c>). After the rewrite a bare token and a legitimately
/// quoted one are indistinguishable, so <c>loads[str]("NaN")</c> returns <c>Ok("NaN")</c> where
/// CPython reads a float and a decode error is owed.
/// </para>
///
/// <para>
/// The proposed fix rewrites bare tokens to a discriminating sentinel OBJECT that only the
/// numeric door accepts. This file probes whether System.Text.Json can actually carry that design
/// through every position a float can occupy, and — the question the design note did not ask —
/// what it does to the <c>JsonElement</c> door, which is how
/// <c>JsonLoadsAgreementTests</c> reads every document.
/// </para>
///
/// <para>
/// A probe rather than a test of shipped behaviour: nothing here touches product code. The
/// converters below are prototypes local to this file, and the findings are recorded on #1488.
/// </para>
/// </summary>
public class JsonBareNonFiniteSentinelProbeTests
{
    private const string SentinelKey = "$sharpy.bare-nonfinite";

    /// <summary>Prototype of the numeric converter the design would register.</summary>
    private sealed class SentinelDoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using JsonDocument document = JsonDocument.ParseValue(ref reader);
                string spelling = document.RootElement.GetProperty(SentinelKey).GetString()!;
                return spelling switch
                {
                    "NaN" => double.NaN,
                    "Infinity" => double.PositiveInfinity,
                    "-Infinity" => double.NegativeInfinity,
                    _ => throw new JsonException($"unknown non-finite spelling {spelling}"),
                };
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string text = reader.GetString()!;
                return text switch
                {
                    "NaN" => double.NaN,
                    "Infinity" => double.PositiveInfinity,
                    "-Infinity" => double.NegativeInfinity,
                    _ => throw new JsonException($"expected a number, got the string {text}"),
                };
            }

            return reader.GetDouble();
        }

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value);
    }

    private static JsonSerializerOptions ProbeOptions() => new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new SentinelDoubleConverter() },
    };

    private static string Sentinel(string spelling) => $"{{\"{SentinelKey}\":\"{spelling}\"}}";

    private sealed class Holder
    {
        public double Value { get; set; }
    }

    /// <summary>
    /// Q1/Q2 — is the custom converter reached, and can it read an OBJECT, at every position a
    /// float occupies: whole document, array element, and object property.
    /// </summary>
    [Fact]
    public void Probe_TheConverterIsReachedAndReadsTheSentinel_InEveryFloatPosition()
    {
        JsonSerializerOptions options = ProbeOptions();

        Assert.Equal(double.NaN, System.Text.Json.JsonSerializer.Deserialize<double>(Sentinel("NaN"), options));
        Assert.Equal(
            double.PositiveInfinity,
            System.Text.Json.JsonSerializer.Deserialize<double>(Sentinel("Infinity"), options));

        var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<double>>(
            $"[{Sentinel("NaN")},1.0]", options)!;
        Assert.Equal(2, list.Count);
        Assert.True(double.IsNaN(list[0]));
        Assert.Equal(1.0, list[1]);

        var holder = System.Text.Json.JsonSerializer.Deserialize<Holder>($"{{\"value\":{Sentinel("-Infinity")}}}", options)!;
        Assert.Equal(double.NegativeInfinity, holder.Value);

        // And ordinary numbers still work, which the converter must not break.
        Assert.Equal(2.5, System.Text.Json.JsonSerializer.Deserialize<double>("2.5", options));
    }

    /// <summary>
    /// Q4 — a string-typed target meeting the sentinel must fail in a CATCHABLE way, so the
    /// existing door can turn it into <c>Err(JSONDecodeError)</c> rather than letting an exception
    /// escape <c>loads</c> (the #1425 crash-safety contract).
    /// </summary>
    [Fact]
    public void Probe_AStringTargetMeetingTheSentinel_FailsCatchably()
    {
        JsonSerializerOptions options = ProbeOptions();

        Assert.Throws<JsonException>(
            () => System.Text.Json.JsonSerializer.Deserialize<string>(Sentinel("NaN"), options));
    }

    /// <summary>
    /// Q5 — THE DECIDING QUESTION, and one the design note did not ask: what the sentinel does to
    /// the <c>JsonElement</c> door.
    ///
    /// <para>
    /// <c>JsonLoadsAgreementTests</c> reads every document through <c>Json.Loads&lt;JsonElement&gt;</c>
    /// and canonicalises the result to compare the typed and untyped doors. <c>JsonElement</c> is
    /// not <c>double</c>, so a <c>JsonConverter&lt;double&gt;</c> is never consulted for it — the
    /// sentinel would arrive as a literal OBJECT where CPython (and Sharpy's untyped door) see a
    /// float. If that holds, the sentinel is not transparent, and adopting it silently changes
    /// what the agreement corpus observes.
    /// </para>
    /// </summary>
    [Fact]
    public void Probe_TheSentinelIsNotTransparentToTheJsonElementDoor()
    {
        JsonSerializerOptions options = ProbeOptions();

        var element = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(Sentinel("NaN"), options);

        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.Equal("NaN", element.GetProperty(SentinelKey).GetString());
    }

    /// <summary>
    /// The baseline the fix has to preserve, stated here so the probe records both sides: with
    /// today's quoting rewrite the numeric door already reads bare tokens correctly, and that
    /// behaviour is guarded by
    /// <c>JsonLoadsAgreementTests.BareNonFiniteTokensReadAsDoublesThroughTheTypedDoor</c>.
    /// </summary>
    [Theory]
    [InlineData("NaN", double.NaN)]
    [InlineData("Infinity", double.PositiveInfinity)]
    [InlineData("-Infinity", double.NegativeInfinity)]
    public void Probe_TodaysNumericDoorAlreadyReadsBareTokens(string document, double expected)
    {
        Result<double, JSONDecodeError> typed = Json.Loads<double>(document);

        Assert.True(typed.IsOk);
        Assert.Equal(expected, typed.Unwrap());
    }

    /// <summary>
    /// The defect itself, pinned: a bare token read into a STRING target comes back Ok because
    /// the rewrite erased the distinction. CPython raises. This is what #1488 must flip.
    /// </summary>
    [Fact]
    public void Probe_TheDefect_BareTokenAtAStringTargetIsAcceptedToday()
    {
        Result<string, JSONDecodeError> typed = Json.Loads<string>("NaN");

        Assert.True(typed.IsOk);
        Assert.Equal("NaN", typed.Unwrap());
    }

    /// <summary>
    /// The control that must NOT move: a genuinely quoted <c>"NaN"</c> is data, and reads as the
    /// string on every design.
    /// </summary>
    [Fact]
    public void Probe_AQuotedNaNIsDataAndStaysAString()
    {
        Result<string, JSONDecodeError> typed = Json.Loads<string>("\"NaN\"");

        Assert.True(typed.IsOk);
        Assert.Equal("NaN", typed.Unwrap());
    }
}
