using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// Holds the two <c>json.loads</c> doors to the same answer (#1425).
///
/// <para>
/// <c>json.loads</c> (untyped) is a hand-written <c>JsonParser</c>; <c>json.loads[T]</c> (typed) is
/// System.Text.Json behind a bare-non-finite quoting pass. Two implementations of one contract,
/// each collecting independently — the parallel-site defect class this round kept hitting
/// (<c>BuiltinRegistry</c> vs <c>ModuleRegistry</c> in #1367, the abstractness formula copies in
/// #1371–#1374). It is why the untyped side accepted <c>Infinity</c>/<c>NaN</c> for as long as it
/// existed while the typed side rejected them until <c>e7e3dc96a</c>, and why #1296 could fix "the
/// loads side" and leave half of it broken (#1353).
/// </para>
///
/// <para>
/// This asserts the class, not those instances: one shared corpus goes through BOTH doors and they
/// must agree on accept/reject, and — where both accept — on the decoded value. A document that
/// only one door can read is a divergence whether or not anyone has named it yet.
/// </para>
///
/// <para>
/// The typed door is instantiated at <see cref="JsonElement"/> because that is the only target that
/// exercises the reader without also imposing a shape: any other <c>T</c> would fail documents for
/// not matching <c>T</c>, which tests the binder rather than the parser.
/// </para>
/// </summary>
public class JsonLoadsAgreementTests
{
    /// <summary>
    /// Documents both doors must answer identically. Names are the failure labels, so they say what
    /// the row is for rather than repeating the text.
    /// </summary>
    public static TheoryData<string, string> Corpus()
    {
        var data = new TheoryData<string, string>();

        // --- containers ---
        data.Add("empty-object", "{}");
        data.Add("empty-array", "[]");
        data.Add("flat-object", "{\"a\": 1, \"b\": 2}");
        data.Add("flat-array", "[1, 2, 3]");
        data.Add("nested-mixed", "{\"a\": [1, {\"b\": [true, null]}], \"c\": {\"d\": {}}}");
        data.Add("array-of-objects", "[{\"x\": 1}, {\"x\": 2}]");
        data.Add("deep-nesting", string.Concat(Enumerable.Repeat("[", 40))
            + "1" + string.Concat(Enumerable.Repeat("]", 40)));

        // --- scalars, every kind ---
        data.Add("bare-true", "true");
        data.Add("bare-false", "false");
        data.Add("bare-null", "null");
        data.Add("bare-string", "\"hello\"");
        data.Add("bare-int", "42");
        data.Add("bare-negative-int", "-42");
        data.Add("bare-zero", "0");
        data.Add("bare-negative-zero", "-0");
        data.Add("bare-float", "3.5");
        data.Add("exponent-upper", "1E3");
        data.Add("exponent-lower", "1e3");
        data.Add("exponent-signed", "1.5e-7");

        // --- numeric magnitude ---
        data.Add("int64-max", "9223372036854775807");
        data.Add("int64-min", "-9223372036854775808");
        data.Add("beyond-int64", "9223372036854775808");
        data.Add("very-big-integer", "123456789012345678901234567890");
        data.Add("tiny-float", "1e-320");
        data.Add("huge-float", "1e308");

        // --- non-finite tokens (CPython's spellings; #1296/#1353) ---
        data.Add("bare-nan", "NaN");
        data.Add("bare-infinity", "Infinity");
        data.Add("bare-negative-infinity", "-Infinity");
        data.Add("non-finite-in-array", "[NaN, Infinity, -Infinity]");
        data.Add("non-finite-as-value", "{\"a\": NaN}");

        // --- strings and escapes ---
        data.Add("empty-string", "\"\"");
        data.Add("unicode-escape", "\"\\u00e9\\u4e2d\"");
        data.Add("surrogate-pair-escape", "\"\\ud83d\\ude00\"");
        data.Add("escaped-controls", "\"a\\tb\\nc\\r\\\\d\\\"e\\/f\"");
        data.Add("escaped-backspace-formfeed", "\"\\b\\f\"");
        data.Add("literal-non-ascii", "\"héllo 中\"");
        data.Add("string-with-braces", "\"{not: json}\"");
        data.Add("key-with-escape", "{\"a\\tb\": 1}");
        data.Add("nul-escape", "\"\\u0000\"");

        // --- whitespace ---
        data.Add("leading-whitespace", "   {\"a\": 1}");
        data.Add("trailing-whitespace", "{\"a\": 1}   ");
        data.Add("interior-newlines", "{\n  \"a\" : 1\n}");

        // --- documents that must be REFUSED by both ---
        data.Add("empty-input", "");
        data.Add("whitespace-only", "   ");
        data.Add("truncated-object", "{\"a\": 1");
        data.Add("truncated-array", "[1, 2");
        data.Add("truncated-string", "\"abc");
        data.Add("trailing-garbage", "{\"a\": 1} garbage");
        data.Add("trailing-comma-object", "{\"a\": 1,}");
        data.Add("trailing-comma-array", "[1, 2,]");
        data.Add("two-documents", "{} {}");
        data.Add("lowercase-infinity", "infinity");
        data.Add("lowercase-nan", "nan");
        data.Add("uppercase-nan", "NAN");
        data.Add("python-none", "None");
        data.Add("python-true", "True");
        data.Add("single-quoted-string", "'abc'");
        data.Add("unquoted-key", "{a: 1}");
        data.Add("leading-plus", "+1");
        data.Add("leading-zero", "01");
        data.Add("bare-decimal-point", ".5");
        data.Add("trailing-decimal-point", "1.");
        data.Add("hex-number", "0x10");
        data.Add("missing-value", "{\"a\": }");
        data.Add("missing-colon", "{\"a\" 1}");
        data.Add("mismatched-brackets", "{\"a\": 1]");
        data.Add("comment", "{\"a\": 1} // note");
        data.Add("naked-comma", ",");

        // --- the places two independent readers drift apart ---
        // Reader limits, not grammar: System.Text.Json stops at a default depth of 64, and a
        // hand-written recursive-descent parser has no such notion. Both sides of that boundary
        // are here so a limit change on either door shows up as a disagreement.
        data.Add("nesting-at-64", Nest(64));
        data.Add("nesting-past-64", Nest(65));
        data.Add("nesting-far-past-64", Nest(200));

        // Duplicate keys: JSON does not say who wins, so the two doors are free to differ — which
        // is exactly why it must be pinned rather than assumed.
        data.Add("duplicate-keys", "{\"a\": 1, \"a\": 2}");
        data.Add("duplicate-keys-nested", "{\"a\": {\"b\": 1, \"b\": 2}}");

        // The quoting pass rewrites BARE non-finite tokens only. A quoted one is data and must
        // survive untouched; an identifier merely starting with one must not be split.
        data.Add("quoted-nan-is-data", "\"NaN\"");
        data.Add("quoted-infinity-is-data", "{\"a\": \"Infinity\"}");
        data.Add("nan-prefixed-identifier", "NaNx");
        data.Add("infinity-prefixed-identifier", "Infinityx");
        data.Add("nan-inside-string-value", "\"prefix NaN suffix\"");
        data.Add("escaped-quote-then-nan", "\"\\\"NaN\"");

        // Malformed UTF-16 and raw control characters — the two readers validate strings
        // separately, so this is a parallel-site seam in its own right.
        data.Add("lone-high-surrogate", "\"\\ud800\"");
        data.Add("lone-low-surrogate", "\"\\udc00\"");
        data.Add("reversed-surrogates", "\"\\udc00\\ud800\"");
        data.Add("raw-control-char", "\"a\u0001b\"");
        data.Add("raw-newline-in-string", "\"a\nb\"");
        data.Add("raw-tab-in-string", "\"a\tb\"");
        data.Add("invalid-escape", "\"\\x41\"");
        data.Add("truncated-unicode-escape", "\"\\u12\"");
        data.Add("non-hex-unicode-escape", "\"\\uZZZZ\"");

        // Numeric overflow and precision, where "parse to a CLR number" and "keep the text" differ.
        data.Add("float-overflow", "1e999");
        data.Add("float-underflow", "1e-999");
        data.Add("negative-float-overflow", "-1e999");
        data.Add("precision-beyond-double", "0.1000000000000000055511151231257827");
        data.Add("negative-zero-float", "-0.0");

        // Whitespace the grammar allows, and one it does not.
        data.Add("tab-and-cr-separated", "{\"a\"\t:\r1}");
        data.Add("formfeed-separator", "{\"a\":\f1}");
        data.Add("bom-prefixed", "\ufeff{}");

        return data;
    }

    /// <summary>An array nested <paramref name="depth"/> deep around a single scalar.</summary>
    private static string Nest(int depth)
        => string.Concat(Enumerable.Repeat("[", depth)) + "1" + string.Concat(Enumerable.Repeat("]", depth));

    /// <summary>
    /// Rows whose DECODED VALUE is known to differ, each with the reason it is tolerated. Ratchets:
    /// <see cref="EveryValueExemptionStillDiverges"/> fails on an entry that has stopped diverging,
    /// so a fix cannot leave its exemption behind. Acceptance is never exempted — every row in the
    /// corpus must still be read-or-refused identically by both doors.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> ValueExemptions = new(StringComparer.Ordinal)
    {
        // Not a defect: the quoting pass turns a BARE non-finite token into the quoted spelling
        // System.Text.Json accepts, so at JsonElement it is a string. At the numeric T the pass
        // exists for it reconstitutes as the double — which is asserted directly by
        // BareNonFiniteTokensReadAsDoublesThroughTheTypedDoor rather than papered over here.
        ["bare-nan"] = "JsonElement representation of the non-finite quoting pass; see the numeric-T test",
        ["bare-infinity"] = "JsonElement representation of the non-finite quoting pass; see the numeric-T test",
        ["bare-negative-infinity"] = "JsonElement representation of the non-finite quoting pass; see the numeric-T test",
        ["non-finite-in-array"] = "JsonElement representation of the non-finite quoting pass; see the numeric-T test",
        ["non-finite-as-value"] = "JsonElement representation of the non-finite quoting pass; see the numeric-T test",
    };

    [Theory]
    [MemberData(nameof(Corpus))]
    public void BothDoorsAgreeOnAcceptance(string name, string document)
    {
        var untyped = ReadUntyped(document);
        var typed = ReadTyped(document);

        Assert.True(
            untyped.Accepted == typed.Accepted,
            $"'{name}' splits the two json.loads implementations (#1425).\n"
            + $"  document: {Describe(document)}\n"
            + $"  untyped json.loads:    {untyped.Describe()}\n"
            + $"  typed json.loads[T]:   {typed.Describe()}\n"
            + "One door can read this and the other cannot. Fix the disagreeing side or file it; "
            + "do not relax the corpus.");
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void BothDoorsAgreeOnDecodedValue(string name, string document)
    {
        var untyped = ReadUntyped(document);
        var typed = ReadTyped(document);

        if (!untyped.Accepted || !typed.Accepted)
        {
            // Acceptance disagreement is the other test's finding; reporting it twice buries it.
            return;
        }

        if (ValueExemptions.ContainsKey(name))
        {
            return;
        }

        Assert.True(
            untyped.Canonical == typed.Canonical,
            $"'{name}' decodes to different values through the two json.loads implementations (#1425).\n"
            + $"  document: {Describe(document)}\n"
            + $"  untyped json.loads:    {untyped.Canonical}\n"
            + $"  typed json.loads[T]:   {typed.Canonical}");
    }

    /// <summary>
    /// The corpus is only a guard while it exercises both answers. An accept-only corpus would let
    /// every rejection row silently stop being a rejection, and the agreement assertions above would
    /// still pass — an absence assertion that holds vacuously.
    /// </summary>
    [Fact]
    public void CorpusExercisesBothAcceptanceAndRefusal()
    {
        var documents = Corpus().Select(row => (string)row[1]!).ToArray();
        int accepted = documents.Count(d => ReadUntyped(d).Accepted);
        int refused = documents.Length - accepted;

        Assert.True(accepted >= 20, $"corpus accepts only {accepted} documents; it has stopped covering the reading path");
        Assert.True(refused >= 20, $"corpus refuses only {refused} documents; it has stopped covering the refusal path");
    }

    /// <summary>
    /// The untyped door had no depth limit, so a deeply nested document exhausted the thread stack
    /// inside the recursive descent — and .NET stack overflow cannot be caught, so it killed the
    /// process rather than raising anything a caller could handle. Reading untrusted JSON could
    /// take the host down (measured at 5,000 levels while triaging #1425).
    ///
    /// <para>
    /// This must stay a refusal. If the limit regresses, this test does not fail politely — it
    /// crashes the test host, which is the honest signal for the defect it guards.
    /// </para>
    /// </summary>
    [Fact]
    public void DeeplyNestedDocumentIsRefusedRatherThanCrashingTheProcess()
    {
        string document = Nest(5000);

        var thrown = Assert.Throws<JSONDecodeError>(() => Json.Loads(document));
        Assert.Contains("nesting depth", thrown.Message, StringComparison.Ordinal);

        // The other door refuses the same document, and by the same number.
        Assert.False(Json.Loads<JsonElement>(document).IsOk);
    }

    /// <summary>
    /// Drain-on-fix. An exemption that no longer describes a real difference is worse than none: it
    /// is a claim the reader will believe about a divergence that has already been fixed.
    /// </summary>
    [Fact]
    public void EveryValueExemptionStillDiverges()
    {
        var documents = Corpus().ToDictionary(row => (string)row[0]!, row => (string)row[1]!, StringComparer.Ordinal);

        foreach (var (name, reason) in ValueExemptions)
        {
            Assert.True(documents.ContainsKey(name), $"exemption '{name}' names no corpus row — delete it ({reason})");

            var untyped = ReadUntyped(documents[name]);
            var typed = ReadTyped(documents[name]);
            if (!untyped.Accepted || !typed.Accepted)
            {
                continue;
            }

            Assert.True(
                untyped.Canonical != typed.Canonical,
                $"exemption '{name}' no longer diverges — the two doors now agree, so delete the "
                + $"entry and let the row be asserted ({reason})");
        }
    }

    /// <summary>
    /// What the non-finite exemptions above are standing in for: at a numeric target the quoting
    /// pass round-trips CPython's bare tokens back into the doubles the untyped door produces. This
    /// is the assertion that makes those exemptions safe rather than a hole.
    /// </summary>
    [Theory]
    [InlineData("NaN", double.NaN)]
    [InlineData("Infinity", double.PositiveInfinity)]
    [InlineData("-Infinity", double.NegativeInfinity)]
    public void BareNonFiniteTokensReadAsDoublesThroughTheTypedDoor(string document, double expected)
    {
        var typed = Json.Loads<double>(document);
        Assert.True(typed.IsOk, $"typed json.loads[float] refused the bare token {document}");
        Assert.Equal(expected, typed.Unwrap());
        Assert.Equal(expected, Assert.IsType<double>(Json.Loads(document)));
    }

    private readonly record struct Reading(bool Accepted, string Canonical, string? Failure)
    {
        public string Describe() => Accepted ? $"accepted → {Canonical}" : $"refused ({Failure})";
    }

    private static Reading ReadUntyped(string document)
    {
        try
        {
            return new Reading(true, CanonicalizeUntyped(Json.Loads(document)), null);
        }
        catch (Exception ex)
        {
            return new Reading(false, string.Empty, ex.GetType().Name);
        }
    }

    private static Reading ReadTyped(string document)
    {
        Result<JsonElement, JSONDecodeError> result;
        try
        {
            result = Json.Loads<JsonElement>(document);
        }
        catch (Exception ex)
        {
            return new Reading(false, string.Empty, ex.GetType().Name);
        }

        return result.IsOk
            ? new Reading(true, CanonicalizeTyped(result.Unwrap()), null)
            : new Reading(false, string.Empty, nameof(JSONDecodeError));
    }

    private static string CanonicalizeUntyped(object? value)
    {
        switch (value)
        {
            case null:
                return "null";
            case bool b:
                return b ? "true" : "false";
            case string s:
                return Quote(s);
            case Dict<string, object?> dict:
                var pairs = ((System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object?>>)dict)
                    .Select(pair => Quote(pair.Key) + ":" + CanonicalizeUntyped(pair.Value))
                    .OrderBy(rendered => rendered, StringComparer.Ordinal);
                return "{" + string.Join(",", pairs) + "}";
            case List<object?> list:
                return "[" + string.Join(",", ((System.Collections.Generic.IEnumerable<object?>)list).Select(CanonicalizeUntyped)) + "]";
            default:
                return CanonicalizeNumber(value);
        }
    }

    private static string CanonicalizeTyped(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null or JsonValueKind.Undefined:
                return "null";
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.String:
                // GetString() throws InvalidOperationException on malformed UTF-16 that the reader
                // itself accepted, so the failure is reported as a divergence rather than escaping
                // as an error in the harness.
                try
                {
                    return Quote(element.GetString()!);
                }
                catch (InvalidOperationException ex)
                {
                    return "<unreadable: " + ex.Message + ">";
                }
            case JsonValueKind.Number:
                return CanonicalizeNumber(element.GetRawText());
            case JsonValueKind.Array:
                return "[" + string.Join(",", element.EnumerateArray().Select(CanonicalizeTyped)) + "]";
            case JsonValueKind.Object:
                // JsonElement is a DOM and keeps every duplicate key; the untyped door and every
                // bound T take last-wins (measured: json.loads[dict[str, object]] on
                // {"a":1,"a":2} yields a=2, matching CPython). Deduplicating here compares the two
                // READERS rather than the DOM's retention policy.
                var members = element.EnumerateObject()
                    .GroupBy(property => property.Name, StringComparer.Ordinal)
                    .Select(group => Quote(group.Key) + ":" + CanonicalizeTyped(group.Last().Value))
                    .OrderBy(rendered => rendered, StringComparer.Ordinal);
                return "{" + string.Join(",", members) + "}";
            default:
                return "?" + element.ValueKind;
        }
    }

    /// <summary>
    /// Numbers are compared by value, not by spelling: the untyped door hands back a CLR numeric
    /// box and the typed door hands back the raw source text, so <c>1E3</c> and <c>1000</c> are the
    /// same reading and must not be reported as a divergence.
    ///
    /// <para>
    /// An integral-looking value is NOT normalized through <c>long</c>, because that erases the
    /// sign of negative zero: <c>-0.0</c> and <c>0</c> are different doubles, CPython keeps them
    /// apart (<c>-0.0</c> vs <c>0</c>), and round-tripping through <c>long</c> silently made the two
    /// doors look like they disagreed when they did not.
    /// </para>
    /// </summary>
    private static string CanonicalizeNumber(object value)
    {
        if (value is string raw)
        {
            // Raw JSON text from the typed door. Integer spellings stay exact so magnitudes beyond
            // double survive the comparison; anything with a fraction or exponent is a double.
            bool integral = raw.IndexOfAny(new[] { '.', 'e', 'E' }) < 0;
            return integral && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long exact)
                ? exact.ToString(CultureInfo.InvariantCulture)
                : double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double fromText)
                    ? fromText.ToString("R", CultureInfo.InvariantCulture)
                    : raw;
        }

        return value is double asDouble
            ? asDouble.ToString("R", CultureInfo.InvariantCulture)
            : Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }

    private static string Quote(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (char c in value)
        {
            builder.Append(c is '"' or '\\' ? "\\" + c
                : c < ' ' ? "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture)
                : c.ToString());
        }

        return builder.Append('"').ToString();
    }

    private static string Describe(string document)
        => document.Length == 0 ? "(empty)" : Quote(document);
}
