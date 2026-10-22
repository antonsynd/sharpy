using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// The CPython numeric-literal twin (#2300 row C1, P68 Phase 1): every numeric-literal spelling
/// in the grammar Sharpy SHARES with CPython lexes to CPython's verdict and value.
/// </summary>
/// <remarks>
/// <para><b>Oracle altitude.</b> Each side answers ONE question per spelling: "a single numeric
/// constant with value V, or refused?" CPython: <c>ast.parse(s, mode="eval")</c> whose body is an
/// int/float <c>Constant</c> (<c>build_tools/differential_lex/number_oracle.py</c>). Sharpy:
/// <c>Lexer.TokenizeAll()</c> yields exactly <c>[Integer|Float, Eof]</c> with no error. Token
/// boundaries are deliberately NOT compared: CPython tokenizes <c>.5e</c> as NUMBER <c>.5</c> +
/// NAME <c>e</c> and refuses at parse time, Sharpy refuses in the lexer — both "refused".</para>
/// <para><b>Matrix.</b> form {decimal int, decimal float, trailing-dot float, leading-dot float,
/// hex, binary, octal} × separator placement {none, interior, consecutive, interior of the
/// fraction, interior of the exponent, leading, after the base prefix, trailing, before/after
/// <c>.</c>, before/after <c>e</c>, after the exponent sign} × exponent {none, <c>e</c>/<c>E</c> ×
/// sign {none, <c>+</c>, <c>-</c>} × digits, digits missing} × following character. A following
/// character that cannot glue onto a number (end of file, newline, space, <c>)</c>) is a Sharpy
/// self-consistency axis: the spelling followed by it must lex to the bare spelling's verdict
/// followed by that character's own token. A following character that CAN glue (<c>_</c>,
/// <c>.</c>, the non-suffix letters <c>g</c>/<c>x</c>/<c>z</c>) makes a new spelling, judged by
/// CPython like any other.</para>
/// <para><b>Sharpy-only syntax</b> is never compared with CPython. The suffix roster and the
/// imaginary <c>j</c> are excluded by construction (no generated spelling contains a roster
/// letter after a number); the trailing-dot floats and the leading-zero decimal integers are
/// excluded by <see cref="SharpyOnly"/>, a literal (predicate, spec sentence, Sharpy verdict)
/// table whose cells are still CHECKED — Sharpy must give the row's verdict (refuse a
/// trailing-dot float, accept <c>01</c>), and CPython the opposite one (an exclusion CPython
/// agrees with is over-broad). Every
/// excluded family is pinned to Sharpy's own token or diagnostic by
/// <see cref="SharpyOnlyRows_LexAsTheSpecSays"/>.</para>
/// <para><b>Ratchet.</b> A disagreement fails unless its spelling is a row of
/// <c>numeric-literal-twin-allowlist.txt</c> (<c>spelling =&gt; pinned-verdict # #NNNN reason</c>); a
/// disagreement whose Sharpy verdict differs from the row's pin fails. A row without a
/// <c>#NNNN</c> cite fails, a row naming a spelling the matrix does not compare fails, and a row
/// whose spelling now agrees fails as stale (drain on fix).</para>
/// <para><b>Python absent:</b> a loud skip, unless <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI's
/// Compiler shards), where it fails.</para>
/// </remarks>
public class NumericLiteralTwinTests
{
    private const string AllowlistFileName = "numeric-literal-twin-allowlist.txt";

    private readonly ITestOutputHelper _output;

    public NumericLiteralTwinTests(ITestOutputHelper output) => _output = output;

    // ===================================================================== //
    // The closed enumeration
    // ===================================================================== //

    /// <summary>One numeric spelling split at the points a separator can be placed.</summary>
    private sealed record Parts(string Form, string Prefix, string Main, string Dot, string Fraction,
        string Mark, string Sign, string ExponentDigits);

    private sealed record Spelling(string Text, string Origin);

    private static readonly string[] DecimalDigits = { "0", "1", "10", "12", "123", "1000", "00", "01" };
    private static readonly string[] FloatIntegerParts = { "0", "1", "12", "00", "01" };
    private static readonly string[] FractionDigits = { "5", "12", "05" };
    private static readonly string[] LeadingDotFractions = { "5", "12", "125", "05" };
    private static readonly string[] TrailingDotIntegerParts = { "0", "1", "12", "01" };
    private static readonly string[] HexPrefixes = { "0x", "0X" };
    private static readonly string[] HexDigits = { "F", "FF", "fF", "10" };
    private static readonly string[] BinaryPrefixes = { "0b", "0B" };
    private static readonly string[] BinaryDigits = { "1", "101" };
    private static readonly string[] OctalPrefixes = { "0o", "0O" };
    private static readonly string[] OctalDigits = { "7", "755" };
    private static readonly string[] ExponentMarks = { "e", "E" };
    private static readonly string[] ExponentSigns = { "", "+", "-" };
    private static readonly string[] ExponentDigitStrings = { "1", "3", "10" };

    /// <summary>Exponent missing its digits: (mark, sign).</summary>
    private static readonly (string Mark, string Sign)[] ExponentsMissingDigits = { ("e", ""), ("E", "+"), ("e", "-") };

    /// <summary>Following characters that glue onto a number: the spelling + char is a NEW spelling.</summary>
    private static readonly string[] GluedFollowers = { "_", ".", "g", "x", "z" };

    /// <summary>
    /// Following characters that end a number: (name, text, the tokens the text itself lexes to).
    /// End of file is the bare spelling.
    /// </summary>
    private static readonly (string Name, string Text, TokenType[] Tokens)[] SeparatingFollowers =
    {
        ("newline", "\n", new[] { TokenType.Newline }),
        ("space", " ", Array.Empty<TokenType>()),
        ("rparen", ")", new[] { TokenType.RightParen }),
    };

    /// <summary>
    /// The suffix roster (integer_literals.md, float_literals.md) plus the imaginary <c>j</c>: no
    /// generated spelling may hold one of these letters, so the shared matrix never meets them.
    /// </summary>
    private static readonly string SharpyOnlyLetters = "fFdDmMlLuUjJ";

    /// <summary>
    /// Shared-grammar spellings whose Sharpy verdict is fixed by the spec, not by CPython. Each
    /// cell matching a predicate must get the row's Sharpy verdict (<c>SharpyAccepts</c>) AND the
    /// opposite verdict from CPython (else the exclusion is over-broad);
    /// <see cref="SharpyOnlyRows_LexAsTheSpecSays"/> pins the tokens.
    /// </summary>
    private static readonly (string Name, Regex Predicate, string SpecSentence, bool SharpyAccepts)[] SharpyOnly =
    {
        ("trailing-dot float",
            new Regex(@"^[0-9](?:_?[0-9])*\.(?:[eE][+-]?[0-9](?:_?[0-9])*)?$", RegexOptions.CultureInvariant),
            "float_literals.md: \"Invalid - trailing decimal point without digit … # invalid = 5.   # ERROR: use 5.0 instead\"",
            false),
        // A decimal INTEGER only: CPython accepts leading zeros in a float (01.5, 01e3), and an
        // all-zero integer (00, 0_0), so neither is excluded.
        ("leading-zero decimal integer",
            new Regex(@"^(?=[0-9_]*[1-9])0(?:_?[0-9])+$", RegexOptions.CultureInvariant),
            "integer_literals.md: \"Leading zeros are digits (C#'s rule): 007 is the decimal integer 7 and 0_1 is 1\" — "
                + "docs/deviations.yaml leading-zero-decimal-integer (R-HT, #2300)",
            true),
    };

    private static IEnumerable<Parts> BaseParts()
    {
        foreach (var d in DecimalDigits)
            yield return new Parts("decimal-int", "", d, "", "", "", "", "");
        foreach (var i in FloatIntegerParts)
            foreach (var f in FractionDigits)
                yield return new Parts("decimal-float", "", i, ".", f, "", "", "");
        foreach (var i in TrailingDotIntegerParts)
            yield return new Parts("trailing-dot-float", "", i, ".", "", "", "", "");
        foreach (var f in LeadingDotFractions)
            yield return new Parts("leading-dot-float", "", "", ".", f, "", "", "");
        foreach (var p in HexPrefixes)
            foreach (var d in HexDigits)
                yield return new Parts("hex", p, d, "", "", "", "", "");
        foreach (var p in BinaryPrefixes)
            foreach (var d in BinaryDigits)
                yield return new Parts("binary", p, d, "", "", "", "", "");
        foreach (var p in OctalPrefixes)
            foreach (var d in OctalDigits)
                yield return new Parts("octal", p, d, "", "", "", "", "");
    }

    private static IEnumerable<Parts> WithExponents(Parts b)
    {
        yield return b;
        foreach (var mark in ExponentMarks)
            foreach (var sign in ExponentSigns)
                foreach (var digits in ExponentDigitStrings)
                    yield return b with { Mark = mark, Sign = sign, ExponentDigits = digits };
        foreach (var (mark, sign) in ExponentsMissingDigits)
            yield return b with { Mark = mark, Sign = sign, ExponentDigits = "" };
    }

    private static string Join(Parts p) => p.Prefix + p.Main + p.Dot + p.Fraction + p.Mark + p.Sign + p.ExponentDigits;

    private static string InsertInterior(string run, string separator) => run[..1] + separator + run[1..];

    /// <summary>Every separator placement that applies to <paramref name="p"/>: (name, spelling).</summary>
    private static IEnumerable<(string Name, string Text)> WithSeparators(Parts p)
    {
        yield return ("none", Join(p));
        // The "main" digit run is the integer part, or the fraction of a leading-dot float.
        bool mainIsFraction = p.Main.Length == 0;
        string main = mainIsFraction ? p.Fraction : p.Main;
        if (main.Length >= 2)
        {
            yield return ("interior", Join(mainIsFraction ? p with { Fraction = InsertInterior(main, "_") } : p with { Main = InsertInterior(main, "_") }));
            yield return ("consecutive", Join(mainIsFraction ? p with { Fraction = InsertInterior(main, "__") } : p with { Main = InsertInterior(main, "__") }));
        }
        if (!mainIsFraction && p.Fraction.Length >= 2)
            yield return ("interior-fraction", Join(p with { Fraction = InsertInterior(p.Fraction, "_") }));
        if (p.ExponentDigits.Length >= 2)
            yield return ("interior-exponent", Join(p with { ExponentDigits = InsertInterior(p.ExponentDigits, "_") }));
        yield return ("leading", "_" + Join(p));
        if (p.Prefix.Length > 0)
            yield return ("after-prefix", Join(p with { Prefix = p.Prefix + "_" }));
        yield return ("trailing", Join(p) + "_");
        if (p.Dot.Length > 0 && p.Main.Length > 0)
            yield return ("before-dot", Join(p with { Main = p.Main + "_" }));
        if (p.Dot.Length > 0)
            yield return ("after-dot", Join(p with { Dot = "._" }));
        if (p.Mark.Length > 0)
        {
            yield return ("before-exponent", Join(p with { Mark = "_" + p.Mark }));
            yield return ("after-exponent", Join(p with { Mark = p.Mark + "_" }));
        }
        if (p.Sign.Length > 0)
            yield return ("after-sign", Join(p with { Sign = p.Sign + "_" }));
    }

    /// <summary>
    /// The closed enumeration: every bare spelling, then every bare spelling + a glued follower,
    /// deduplicated by text (first origin wins), in a deterministic order.
    /// </summary>
    private static List<Spelling> Spellings()
    {
        var bare = new List<Spelling>();
        foreach (var b in BaseParts())
            foreach (var p in WithExponents(b))
                foreach (var (sep, text) in WithSeparators(p))
                    bare.Add(new Spelling(text, $"{p.Form} sep={sep} exp={p.Mark}{p.Sign}{p.ExponentDigits}"));

        var all = new List<Spelling>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in bare)
        {
            if (seen.Add(s.Text))
                all.Add(s);
        }
        foreach (var s in bare)
        {
            foreach (var follower in GluedFollowers)
            {
                var text = s.Text + follower;
                if (seen.Add(text))
                    all.Add(new Spelling(text, $"{s.Origin} follow='{follower}'"));
            }
        }
        return all;
    }

    private static string? ExclusionOf(string text) => SharpyOnlyRowOf(text)?.Name;

    private static (string Name, Regex Predicate, string SpecSentence, bool SharpyAccepts)? SharpyOnlyRowOf(string text)
    {
        foreach (var row in SharpyOnly)
        {
            if (row.Predicate.IsMatch(text))
                return row;
        }
        return null;
    }

    // ===================================================================== //
    // Verdicts
    // ===================================================================== //

    /// <summary>
    /// A side's answer for one spelling: a numeric constant (<see cref="Value"/> is canonical:
    /// <c>int:&lt;decimal&gt;</c> or <c>float:&lt;IEEE bits&gt;</c>) or refused.
    /// </summary>
    private sealed record Verdict(bool Ok, string Value, string Detail)
    {
        public static Verdict Refused(string detail) => new(false, "", detail);

        public string Show() => Ok ? $"{Value} ({Detail})" : $"refused ({Detail})";

        public bool SameAs(Verdict other) => Ok == other.Ok && (!Ok || Value == other.Value);
    }

    /// <summary>Sharpy's verdict for <paramref name="text"/>, whose tail is expected to lex to <paramref name="tail"/>.</summary>
    private static Verdict SharpyVerdict(string text, IReadOnlyList<TokenType> tail, List<string> throws)
    {
        var lexer = new LexerNs.Lexer(text);
        List<LexerNs.Token> tokens;
        try
        {
            tokens = lexer.TokenizeAll();
        }
        catch (Exception ex)
        {
            // An escaped exception is never a refusal: it is an instrument-level failure.
            throws.Add($"THROW {ex.GetType().Name} on {Show(text)}: {ex.Message}");
            return Verdict.Refused("threw");
        }

        if (lexer.Diagnostics.HasErrors)
            return Verdict.Refused(string.Join(",", lexer.Diagnostics.GetErrors().Select(d => d.Code)));

        string shape = string.Join(" ", tokens.Select(t => t.Type));
        if (tokens.Count != tail.Count + 2
            || (tokens[0].Type != TokenType.Integer && tokens[0].Type != TokenType.Float)
            || tokens[^1].Type != TokenType.Eof
            || !tokens.Skip(1).Take(tail.Count).Select(t => t.Type).SequenceEqual(tail))
        {
            return Verdict.Refused($"tokens {shape}");
        }

        var number = tokens[0];
        return number.Type == TokenType.Integer
            ? new Verdict(true, CanonicalInteger(number.Value), $"Integer '{number.Value}'")
            : new Verdict(true, CanonicalFloat(number.Value), $"Float '{number.Value}'");
    }

    private static string CanonicalInteger(string value)
    {
        BigInteger result;
        if (value.StartsWith("0x", StringComparison.Ordinal))
        {
            if (!BigInteger.TryParse("0" + value[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result))
                return $"unparseable-int:{value}";
        }
        else if (value.StartsWith("0b", StringComparison.Ordinal) || value.StartsWith("0o", StringComparison.Ordinal))
        {
            int radix = value[1] == 'b' ? 2 : 8;
            result = BigInteger.Zero;
            foreach (var c in value[2..])
            {
                int digit = c - '0';
                if (digit < 0 || digit >= radix)
                    return $"unparseable-int:{value}";
                result = result * radix + digit;
            }
        }
        else if (!BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result))
        {
            return $"unparseable-int:{value}";
        }
        return "int:" + result.ToString(CultureInfo.InvariantCulture);
    }

    private static string CanonicalFloat(string value)
    {
        double d;
        switch (value)
        {
            case "inf":
                d = double.PositiveInfinity;
                break;
            case "-inf":
                d = double.NegativeInfinity;
                break;
            case "nan":
                d = double.NaN;
                break;
            default:
                // Only digits, one '.', an exponent and its sign: a value like M2's "0.5-3" is no number.
                if (!Regex.IsMatch(value, @"^[0-9]*\.?[0-9]*(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant)
                    || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                {
                    return $"unparseable-float:{value}";
                }
                break;
        }
        return "float:" + BitConverter.DoubleToInt64Bits(d).ToString(CultureInfo.InvariantCulture);
    }

    private static Verdict PythonVerdict(JsonElement root)
    {
        if (!root.GetProperty("ok").GetBoolean())
            return Verdict.Refused(root.GetProperty("error").GetString() ?? "");
        string kind = root.GetProperty("kind").GetString() ?? "";
        string value = root.GetProperty("value").GetString() ?? "";
        return kind switch
        {
            "int" => new Verdict(true, CanonicalInteger(value), $"int {value}"),
            "float" => new Verdict(true, CanonicalFloat(value), $"float {value}"),
            _ => throw new InvalidOperationException($"number_oracle.py returned kind '{kind}'"),
        };
    }

    private static string Show(string text) => JsonSerializer.Serialize(text);

    // ===================================================================== //
    // Allowlist
    // ===================================================================== //

    /// <summary>
    /// One allowlist row: the spelling, the Sharpy verdict it pins (<see cref="Pin"/>), and the cite.
    /// The pin keeps a row from absorbing a SECOND change on its own spelling: the row admits
    /// today's disagreement, not any disagreement.
    /// </summary>
    private sealed record AllowlistRow(int Line, string Key, string Pinned, string Rest);

    /// <summary>The verdict an allowlist row pins: the canonical value, or <c>refused</c>.</summary>
    private static string Pin(Verdict v) => v.Ok ? v.Value : "refused";

    private static string AllowlistPath()
    {
        return PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Lexer", AllowlistFileName)
            ?? throw new FileNotFoundException(
                $"{AllowlistFileName} not found under the repository root (src/Sharpy.Compiler.Tests/Lexer/).");
    }

    private static List<AllowlistRow> ReadAllowlist(string path)
    {
        var rows = new List<AllowlistRow>();
        var lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            int hash = line.IndexOf('#');
            string head = (hash < 0 ? line : line[..hash]).Trim();
            string rest = hash < 0 ? "" : line[hash..];
            int arrow = head.IndexOf("=>", StringComparison.Ordinal);
            string key = (arrow < 0 ? head : head[..arrow]).Trim();
            string pinned = arrow < 0 ? "" : head[(arrow + 2)..].Trim();
            rows.Add(new AllowlistRow(i + 1, key, pinned, rest));
        }
        return rows;
    }

    /// <summary>The cite and universe rules — checkable without python.</summary>
    private static List<string> AllowlistShapeViolations(IReadOnlyList<AllowlistRow> rows, ISet<string> compared)
    {
        var violations = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!Regex.IsMatch(row.Rest, @"^#\s*#\d+\b", RegexOptions.CultureInvariant))
                violations.Add($"line {row.Line}: '{row.Key}' has no '# #NNNN reason' issue cite");
            if (!Regex.IsMatch(row.Pinned, @"^(?:refused|int:-?[0-9]+|float:-?[0-9]+)$", RegexOptions.CultureInvariant))
                violations.Add($"line {row.Line}: '{row.Key}' pins no Sharpy verdict ('<spelling> => int:<n> | float:<bits> | refused # #NNNN reason')");
            if (!compared.Contains(row.Key))
                violations.Add($"line {row.Line}: '{row.Key}' is not a spelling the twin compares (not enumerated, or Sharpy-only)");
            if (!seen.Add(row.Key))
                violations.Add($"line {row.Line}: '{row.Key}' is listed twice");
        }
        return violations;
    }

    // ===================================================================== //
    // Tests
    // ===================================================================== //

    [Fact]
    public void Enumeration_HoldsTheIssuesRepresentativeCells_AndNoSharpyOnlyLetter()
    {
        var spellings = Spellings();
        var texts = spellings.Select(s => s.Text).ToHashSet(StringComparer.Ordinal);

        // #2300's C1 cells and the plan's measured cells, anchored as literals (not derived from the generator).
        string[] representative =
        {
            ".5e3", ".5e-3", ".1__2", ".12_", ".5e", ".5e1_", "0xF_", "01", "00", "0_0", "0x_FF", "0XFF",
            "1E+3", "1_0", "12.", "1.e3", "0b1_", "1._5", "1_.5", "1e_3", "1e+_3", "0xFF.", "12g", "0xFFx",
        };
        var missing = representative.Where(r => !texts.Contains(r)).ToList();
        Assert.True(missing.Count == 0, "enumeration lacks: " + string.Join(" ", missing));

        // The suffix/imaginary families are excluded by construction: no roster letter right after a digit.
        var leaked = spellings
            .Where(s => Regex.IsMatch(s.Text, $"[0-9_.][{SharpyOnlyLetters}]", RegexOptions.CultureInvariant)
                && !s.Text.StartsWith("0x", StringComparison.Ordinal) && !s.Text.StartsWith("0X", StringComparison.Ordinal)
                && !s.Text.StartsWith("_0x", StringComparison.Ordinal) && !s.Text.StartsWith("_0X", StringComparison.Ordinal))
            .Select(s => s.Text)
            .ToList();
        Assert.True(leaked.Count == 0, "Sharpy-only suffix letters leaked into the shared matrix: " + string.Join(" ", leaked.Take(20)));

        Assert.Equal("trailing-dot float", ExclusionOf("12."));
        Assert.Equal("trailing-dot float", ExclusionOf("1_0.e-3"));
        Assert.Null(ExclusionOf("1._5"));
        Assert.Null(ExclusionOf("1.5."));
        Assert.Null(ExclusionOf("0xFF."));

        Assert.Equal("leading-zero decimal integer", ExclusionOf("01"));
        Assert.Equal("leading-zero decimal integer", ExclusionOf("0_1"));
        Assert.Equal("leading-zero decimal integer", ExclusionOf("007"));
        Assert.Null(ExclusionOf("00"));     // all zeros: CPython accepts it, so it stays compared
        Assert.Null(ExclusionOf("0_0"));
        Assert.Null(ExclusionOf("0__1"));   // consecutive separators: both refuse, compared
        Assert.Null(ExclusionOf("01_"));
        Assert.Null(ExclusionOf("01.5"));   // a float: CPython accepts its leading zero
        Assert.Null(ExclusionOf("01e3"));
        Assert.Null(ExclusionOf("10"));
    }

    [Fact]
    public void Allowlist_EveryRowCitesAnIssue_AndNamesAComparedSpelling()
    {
        var compared = Spellings().Select(s => s.Text).Where(t => ExclusionOf(t) is null).ToHashSet(StringComparer.Ordinal);
        var violations = AllowlistShapeViolations(ReadAllowlist(AllowlistPath()), compared);
        Assert.True(violations.Count == 0, string.Join("\n", violations));

        // Positive control: the rules fire on rows built to break them.
        var broken = AllowlistShapeViolations(new[]
        {
            new AllowlistRow(1, "10", "int:10", "# no cite here"),
            new AllowlistRow(2, "0x1p3", "int:8", "# #2300 hex float is not in the matrix"),
            new AllowlistRow(3, "12.", "refused", "# #2300 Sharpy-only spelling"),
            new AllowlistRow(4, "00", "", "# #2300 no pinned verdict"),
        }, compared);
        Assert.Equal(4, broken.Count);
    }

    [Fact]
    public void SharedGrammar_AgreesWithCPython()
    {
        var stopwatch = Stopwatch.StartNew();
        var spellings = Spellings();
        var compared = spellings.Select(s => s.Text).Where(t => ExclusionOf(t) is null).ToHashSet(StringComparer.Ordinal);
        var allowlist = ReadAllowlist(AllowlistPath());
        var shapeViolations = AllowlistShapeViolations(allowlist, compared);
        Assert.True(shapeViolations.Count == 0, string.Join("\n", shapeViolations));

        var python = PythonInterpreter.Locate();
        var script = PythonInterpreter.FindRepoFile("build_tools", "differential_lex", "number_oracle.py");
        if (python is null || script is null)
        {
            string why = python is null ? "no Python 3.12+ interpreter (python3/python) on PATH" : "build_tools/differential_lex/number_oracle.py not found";
            if (PythonInterpreter.Required)
                Assert.Fail($"{why}, and {PythonInterpreter.RequireEnvironmentVariable}=1 makes the numeric twin's oracle required.");
            _output.WriteLine($"SKIP: {why}; the numeric twin did not run. Set {PythonInterpreter.RequireEnvironmentVariable}=1 to make this a failure.");
            return;
        }

        var requests = spellings.Select((s, i) => JsonSerializer.Serialize(new { id = i, source = s.Text }));
        var replies = python.RunBatch(script, requests);
        var pythonVerdicts = new Dictionary<int, Verdict>();
        foreach (var reply in replies)
        {
            using var doc = JsonDocument.Parse(reply);
            pythonVerdicts[doc.RootElement.GetProperty("id").GetInt32()] = PythonVerdict(doc.RootElement);
        }
        Assert.Equal(spellings.Count, pythonVerdicts.Count);

        var allowed = allowlist.ToDictionary(r => r.Key, StringComparer.Ordinal);
        var failures = new List<string>();
        var throws = new List<string>();
        var stillDisagreeing = new HashSet<string>(StringComparer.Ordinal);
        int agree = 0, refusedBoth = 0, allowlisted = 0, sharpyOnly = 0, followCells = 0;

        for (int i = 0; i < spellings.Count; i++)
        {
            var s = spellings[i];
            var sharpy = SharpyVerdict(s.Text, Array.Empty<TokenType>(), throws);
            var cpython = pythonVerdicts[i];

            // Separating followers: Sharpy must give the bare spelling's verdict, then the follower's token.
            foreach (var (name, text, tail) in SeparatingFollowers)
            {
                followCells++;
                var followed = SharpyVerdict(s.Text + text, tail, throws);
                if (!followed.SameAs(sharpy))
                    failures.Add($"FOLLOW {Show(s.Text)} + {name}: bare {sharpy.Show()} but followed {followed.Show()}  [{s.Origin}]");
            }

            if (SharpyOnlyRowOf(s.Text) is { } only)
            {
                string exclusion = only.Name;
                sharpyOnly++;
                if (only.SharpyAccepts)
                {
                    if (!sharpy.Ok)
                        failures.Add($"SHARPY-ONLY {Show(s.Text)} ({exclusion}): the spec accepts it, Sharpy gives {sharpy.Show()}");
                    if (cpython.Ok)
                        failures.Add($"OVER-BROAD {Show(s.Text)} ({exclusion}): CPython accepts it too ({cpython.Detail}) — compare it instead of excluding it");
                }
                else
                {
                    if (sharpy.Ok)
                        failures.Add($"SHARPY-ONLY {Show(s.Text)} ({exclusion}): the spec refuses it, Sharpy lexes {sharpy.Show()}");
                    if (!cpython.Ok)
                        failures.Add($"OVER-BROAD {Show(s.Text)} ({exclusion}): CPython refuses it too ({cpython.Detail}) — compare it instead of excluding it");
                }
                continue;
            }

            if (sharpy.SameAs(cpython))
            {
                if (sharpy.Ok)
                    agree++;
                else
                    refusedBoth++;
                continue;
            }

            stillDisagreeing.Add(s.Text);
            if (allowed.TryGetValue(s.Text, out var row))
            {
                if (Pin(sharpy) != row.Pinned)
                    failures.Add($"PIN allowlist line {row.Line}: {Show(s.Text)} pins Sharpy {row.Pinned} but Sharpy now gives {sharpy.Show()} — a second change on an allowlisted spelling is not absorbed  [{s.Origin}]");
                allowlisted++;
                continue;
            }
            failures.Add($"DISAGREE {Show(s.Text)}: CPython {cpython.Show()}, Sharpy {sharpy.Show()}  [{s.Origin}]");
        }

        foreach (var row in allowlist)
        {
            if (!stillDisagreeing.Contains(row.Key))
                failures.Add($"STALE allowlist line {row.Line}: '{row.Key}' now agrees with CPython — delete the row (drain on fix)");
        }

        failures.InsertRange(0, throws);
        stopwatch.Stop();
        _output.WriteLine($"NUMERIC-TWIN spellings {compared.Count} agree {agree} refusedBoth {refusedBoth} allowlisted {allowlisted}");
        _output.WriteLine($"NUMERIC-TWIN python {python.Version} ({python.Executable}) sharpyOnly {sharpyOnly} followCells {followCells} elapsed {stopwatch.Elapsed.TotalSeconds:F1}s");
        foreach (var f in failures.Take(200))
            _output.WriteLine(f);

        Assert.True(failures.Count == 0,
            $"{failures.Count} numeric-twin failure(s); first {Math.Min(failures.Count, 40)}:\n" + string.Join("\n", failures.Take(40))
            + $"\nA disagreement outside the contract is a findings-ledger line and an allowlist row in {AllowlistFileName} citing its issue — never a changed expectation.");
    }

    // ===================================================================== //
    // The Sharpy-only families, pinned to the spec
    // ===================================================================== //

    private static readonly string[] FloatSuffixes = { "f", "F", "d", "D", "m", "M" };
    private static readonly string[] IntegerSuffixes = { "l", "L", "u", "U", "ul", "UL", "uL", "Ul" };

    /// <summary>Not in either roster: <c>j</c>/<c>J</c> (no complex literal), <c>lu</c> (C# spelling the spec does not list), <c>x</c>.</summary>
    private static readonly string[] InvalidSuffixes = { "j", "J", "lu", "x" };

    private enum SpellingKind
    {
        /// <summary>Decimal digits, no <c>.</c>/exponent: takes integer AND float suffixes (<c>42L</c>, <c>42f</c>).</summary>
        DecimalInteger,
        /// <summary>A fraction or exponent, any spelling: takes only the float suffixes.</summary>
        DecimalFloat,
        Hex,
        BinaryOrOctal,
    }

    /// <summary>
    /// The suffix matrix's spelling axis (#2339): (source digits, the token value the lexer gives
    /// them, kind). Every spelling of a kind must answer a suffix the same way — the leading-dot
    /// <c>.5</c> exactly as <c>0.5</c> (float_literals.md: ".5 — Same as 0.5"), the exponent-only
    /// <c>1e5</c> and the underscored spellings as their plain twins.
    /// </summary>
    private static readonly (string Source, string Value, SpellingKind Kind)[] SuffixSpellings =
    {
        ("42", "42", SpellingKind.DecimalInteger),
        ("1_000", "1000", SpellingKind.DecimalInteger),
        ("0.5", "0.5", SpellingKind.DecimalFloat),
        ("3.14", "3.14", SpellingKind.DecimalFloat),
        (".5", "0.5", SpellingKind.DecimalFloat),
        (".1_5", "0.15", SpellingKind.DecimalFloat),
        ("1e5", "1e5", SpellingKind.DecimalFloat),
        ("1E+5", "1E+5", SpellingKind.DecimalFloat),
        (".5e3", "0.5e3", SpellingKind.DecimalFloat),
        ("1_0.2_5", "10.25", SpellingKind.DecimalFloat),
        ("1_0e1_0", "10e10", SpellingKind.DecimalFloat),
        ("0xFF", "0xFF", SpellingKind.Hex),
        ("0xF_F", "0xFF", SpellingKind.Hex),
        ("0b101", "0b101", SpellingKind.BinaryOrOctal),
        ("0b1_01", "0b101", SpellingKind.BinaryOrOctal),
        ("0o17", "0o17", SpellingKind.BinaryOrOctal),
        ("0o1_7", "0o17", SpellingKind.BinaryOrOctal),
    };

    /// <summary>
    /// (source, expected): expected is the token list without <c>Eof</c> as <c>Type 'Value'</c>
    /// joined by <c>", "</c>, or <c>error SPY####</c>.
    /// </summary>
    public static TheoryData<string, string> SharpyOnlyCases()
    {
        var data = new TheoryData<string, string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string source, string expected)
        {
            if (seen.Add(source))
                data.Add(source, expected);
        }
        const string Refused = "error SPY0023";

        // kind {decimal int, decimal float, hex, binary/octal} × spelling (SuffixSpellings) × suffix
        // {every integer suffix, every float suffix, j/J and the other non-roster suffixes, none} — the
        // suffix rosters carry both cases. integer_literals.md: L/l, u/U, ul/UL, "The integer suffixes work
        // on every base"; "A hex digit is never read as a suffix … 0xFFf is 0xFFF"; the float suffixes
        // "apply to decimal literals only; on a binary or octal literal, and m on a hex literal, they are
        // refused (SPY0023)". float_literals.md: f/F, d/D, m/M on floats; an integer suffix on a float is
        // refused (SPY0023), as in C# (#2339).
        foreach (var (source, value, kind) in SuffixSpellings)
        {
            string integerToken = $"Integer '{value}'";
            string floatToken = $"Float '{value}'";
            Add(source, kind == SpellingKind.DecimalFloat ? floatToken : integerToken);

            foreach (var s in IntegerSuffixes)
                Add(source + s, kind == SpellingKind.DecimalFloat ? Refused : $"Integer '{value}{s}'");

            foreach (var s in FloatSuffixes)
            {
                Add(source + s, kind switch
                {
                    SpellingKind.DecimalInteger or SpellingKind.DecimalFloat => $"Float '{value}{s}'",
                    SpellingKind.Hex when s is not ("m" or "M") => $"Integer '{value}{s}'",
                    _ => Refused,
                });
            }

            foreach (var s in InvalidSuffixes)
                Add(source + s, Refused);
        }

        // float_literals.md: "Invalid - trailing decimal point without digit … ERROR: use 5.0 instead" —
        // the lexer leaves the dot to the parser, and a letter after the dot is a member name, never a
        // suffix, for every suffix of either kind.
        Add("1.", "Integer '1', Dot '.'");
        Add("12.", "Integer '12', Dot '.'");
        Add("1_000.", "Integer '1000', Dot '.'");
        Add("1.e5", "Integer '1', Dot '.', Identifier 'e5'");
        foreach (var s in IntegerSuffixes.Concat(FloatSuffixes).Concat(InvalidSuffixes))
            Add("5." + s, $"Integer '5', Dot '.', Identifier '{s}'");

        // integer_literals.md: "Leading zeros are digits (C#'s rule): 007 is the decimal integer 7 and
        // 0_1 is 1" — docs/deviations.yaml leading-zero-decimal-integer (R-HT, #2300); CPython refuses
        // them, so they left the twin's allowlist for this table.
        Add("01", "Integer '01'");
        Add("0_1", "Integer '01'");
        Add("007", "Integer '007'");
        Add("0_0_7", "Integer '007'");
        Add("01L", "Integer '01L'");
        return data;
    }

    [Theory]
    [MemberData(nameof(SharpyOnlyCases))]
    public void SharpyOnlyRows_LexAsTheSpecSays(string source, string expected)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        string actual = lexer.Diagnostics.HasErrors
            ? string.Join(", ", lexer.Diagnostics.GetErrors().Select(d => "error " + d.Code))
            : string.Join(", ", tokens.Where(t => t.Type != TokenType.Eof).Select(t => $"{t.Type} '{t.Value}'"));
        Assert.Equal(expected, actual);
    }
}
