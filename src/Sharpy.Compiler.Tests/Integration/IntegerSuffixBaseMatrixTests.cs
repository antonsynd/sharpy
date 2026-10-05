using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #2208 — an integer suffix is accepted on EVERY literal base. Matrix:
/// <b>base {dec, hex, bin, oct} × suffix (the decimal integer roster + unsuffixed) × digit
/// separators {none, inner <c>_</c>}</b> = 4 × 9 × 2 = 72 run cells, plus the hex-digit ambiguity
/// cells and the refusal cells.
///
/// <para><b>Base classification (measured with <c>sharpyc run</c> at 6b208066e):</b> every
/// suffixed hex/bin/oct cell was SPY0103 "Expected end of statement, got Identifier" (the lexer
/// stopped at the last digit and the suffix lexed as an identifier); every decimal cell ran.</para>
///
/// <para><b>Type oracle.</b> A run cell prints <c>x, ~x, x &lt;&lt; 40</c>. That triple
/// distinguishes all four CLR widths the suffix roster selects — <c>~</c> separates signed from
/// unsigned, and C# masks a shift count to 5 bits for 32-bit operands but 6 for 64-bit ones — so a
/// cell whose suffix was dropped or mis-typed prints a different triple. The expected triple is
/// computed here by C# itself on a value of the expected CLR type.</para>
///
/// <para><b>Roster anchoring.</b> <see cref="SuffixRoster"/> is spelled as literals, not read from
/// the lexer's table: a count read from the same source as the code under test is vacuous.</para>
/// </summary>
public class IntegerSuffixBaseMatrixTests : IntegrationTestBase
{
    public IntegerSuffixBaseMatrixTests(ITestOutputHelper output) : base(output) { }

    private enum Width { Int32, Int64, UInt32, UInt64 }

    /// <summary>The decimal integer-suffix roster (integer_literals.md; Lexer.ReadNumber), plus "" for unsuffixed.</summary>
    private static readonly (string Suffix, Width Width)[] SuffixRoster =
    {
        ("", Width.Int32),
        ("l", Width.Int64), ("L", Width.Int64),
        ("u", Width.UInt32), ("U", Width.UInt32),
        ("ul", Width.UInt64), ("UL", Width.UInt64), ("uL", Width.UInt64), ("Ul", Width.UInt64),
    };

    /// <summary>255 spelled in each base, without and with an inner digit separator.</summary>
    private static readonly Dictionary<(string Base, bool Separated), string> Spellings = new()
    {
        [("dec", false)] = "255",
        [("dec", true)] = "2_55",
        [("hex", false)] = "0xFF",
        [("hex", true)] = "0xF_F",
        [("bin", false)] = "0b11111111",
        [("bin", true)] = "0b1111_1111",
        [("oct", false)] = "0o377",
        [("oct", true)] = "0o3_77",
    };

    public static IEnumerable<object[]> BaseCells() =>
        Spellings.Keys.Select(k => new object[] { k.Base, k.Separated });

    private static string ExpectedTriple(Width width) => width switch
    {
        Width.Int32 => Triple(255, ~255, 255 << (40 & 31)),
        Width.Int64 => Triple(255L, ~255L, 255L << 40),
        Width.UInt32 => Triple(255u, ~255u, 255u << (40 & 31)),
        Width.UInt64 => Triple(255ul, ~255ul, 255ul << 40),
        _ => throw new ArgumentOutOfRangeException(nameof(width)),
    };

    private static string Triple(IFormattable a, IFormattable b, IFormattable c) =>
        string.Join(" ", new[] { a, b, c }.Select(v => v.ToString(null, CultureInfo.InvariantCulture)));

    [Theory]
    [MemberData(nameof(BaseCells))]
    public void Suffix_IsAccepted_AndSelectsTheWidth_OnEveryBase(string baseName, bool separated)
    {
        var digits = Spellings[(baseName, separated)];
        var source = new StringBuilder("def main():\n");
        var expected = new StringBuilder();
        for (var i = 0; i < SuffixRoster.Length; i++)
        {
            var (suffix, width) = SuffixRoster[i];
            source.Append(CultureInfo.InvariantCulture, $"    x{i} = {digits}{suffix}\n");
            source.Append(CultureInfo.InvariantCulture, $"    print(x{i}, ~x{i}, x{i} << 40)\n");
            expected.Append(ExpectedTriple(width)).Append('\n');
        }

        var result = CompileAndExecute(source.ToString());

        result.Success.Should().BeTrue(
            $"base {baseName} (separated={separated}) must accept every integer suffix; errors: {string.Join("; ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Should().Be(expected.ToString(), $"source:\n{source}");
    }

    /// <summary>
    /// The hex ambiguity: a trailing hex digit that is also a letter — including the float-suffix
    /// letters <c>f</c>/<c>d</c> — stays a DIGIT, and an integer suffix after such a digit is
    /// still a suffix. Pinned at the parser (Value/Suffix) and by run.
    /// </summary>
    [Theory]
    [InlineData("0xFFf", "0xFFf", null)]
    [InlineData("0x1D", "0x1D", null)]
    [InlineData("0xABCDEF", "0xABCDEF", null)]
    [InlineData("0xFFfu", "0xFFf", "u")]
    [InlineData("0x1DL", "0x1D", "L")]
    [InlineData("0xCAFEul", "0xCAFE", "ul")]
    [InlineData("0xbu", "0xb", "u")]
    public void HexDigitLetters_StayDigits_BeforeASuffix(string literal, string value, string? suffix)
    {
        var parsed = ParseSingleLiteral(literal);
        parsed.Value.Should().Be(value);
        parsed.Suffix.Should().Be(suffix);
    }

    [Fact]
    public void HexDigitLetters_Run()
    {
        var result = CompileAndExecute(
            "def main():\n" +
            "    print(0xFFf)\n" +
            "    print(0x1D)\n" +
            "    print(0xFFfu, ~0xFFfu)\n" +
            "    print(0x1DL << 40)\n" +
            "    print(0x41C64E6Du)\n" +
            "    print(0xFFFFFFFFL)\n" +
            "    print(0xFFFFFFFFFFFFFFFFul)\n" +
            "    print(-0xFFL)\n");

        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Should().Be(
            "4095\n29\n4095 4294963200\n31885837205504\n1103515245\n4294967295\n18446744073709551615\n-255\n");
    }

    /// <summary>
    /// A float suffix (<c>f d m</c>) has no meaning on a prefixed literal (C# refuses <c>0x1m</c>),
    /// and a non-roster suffix is refused as on decimal (<c>5lu</c> is refused too). The refusal is
    /// SPY0023 naming the base, not the SPY0103 "got Identifier" of the base commit.
    /// </summary>
    [Theory]
    [InlineData("0xFFm", "a hexadecimal")]
    [InlineData("0xFFM", "a hexadecimal")]
    [InlineData("0xFFlu", "a hexadecimal")]
    [InlineData("0xFFz", "a hexadecimal")]
    [InlineData("0b11f", "a binary")]
    [InlineData("0b11d", "a binary")]
    [InlineData("0b11m", "a binary")]
    [InlineData("0o17f", "an octal")]
    [InlineData("0o17D", "an octal")]
    [InlineData("0o17m", "an octal")]
    public void NonIntegerSuffix_OnPrefixedLiteral_IsRefused_NamingTheBase(string literal, string article)
    {
        var lexer = new LexerNs.Lexer($"x = {literal}\n");
        lexer.TokenizeAll();
        var errors = lexer.Diagnostics.GetErrors().ToList();
        errors.Should().ContainSingle();
        errors[0].Code.Should().Be("SPY0023");
        errors[0].Message.Should().StartWith("Invalid numeric suffix: ")
            .And.Contain($"({article} literal accepts only the integer suffixes u, l and ul)");
    }

    private static IntegerLiteral ParseSingleLiteral(string literal)
    {
        var lexer = new LexerNs.Lexer(literal + "\n");
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse(string.Join("; ", lexer.Diagnostics.GetErrors().Select(d => d.Message)));
        var module = new ParserNs.Parser(tokens).ParseModule();
        var stmt = module.Body.Should().ContainSingle().Subject.Should().BeOfType<ExpressionStatement>().Subject;
        return stmt.Expression.Should().BeOfType<IntegerLiteral>().Subject;
    }
}
