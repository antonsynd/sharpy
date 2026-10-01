using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.PrettyTests;

/// <summary>
/// P22b Phase 3 Task 2 (#2157): every name position writes its backtick escape back — the unparser
/// routes every user-written name through <c>WriteName</c>/<c>WriteDottedName</c>. One cell per
/// escape position (the escaped column of <see cref="StructuralEqualityComparerEscapeFlagTests.Pairs"/>),
/// two oracles each: (a) the escaped-token multiset of the source equals that of its unparse — a
/// lexer-level count that does not trust the comparer — and (b) the unparse re-parses to a
/// structurally equal AST (which, since the comparer sees every flag, also catches a dropped escape).
/// The same two oracles run through the production path, <see cref="FormatterService.Format(string)"/>,
/// which must report no diagnostics (a refusal would leave the text unchanged and pass vacuously).
/// </summary>
public class UnparserBacktickEscapeRoundTripTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse("must lex: " + source);
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            "must parse: " + source + " — "
            + string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message}")));
        return module;
    }

    private static string[] EscapedNames(string source) =>
        new LexerNs.Lexer(source).TokenizeAll()
            .Where(t => t.IsBacktickEscaped)
            .Select(t => t.Value)
            .OrderBy(v => v, System.StringComparer.Ordinal)
            .ToArray();

    private static void AssertRoundTrips(string cell, string source, string output)
    {
        var expected = EscapedNames(source);
        expected.Should().NotBeEmpty($"{cell}: positive control — the source carries an escape");
        EscapedNames(output).Should().Equal(expected, $"{cell}: every escape is written back:\n{output}");
        StructuralEqualityComparer.Instance.Equals(
            AstNormalizer.Instance.NormalizeModule(Parse(source)),
            AstNormalizer.Instance.NormalizeModule(Parse(output)))
            .Should().BeTrue($"{cell}: the output re-parses to the same AST:\n{output}");
    }

    [Theory]
    [MemberData(nameof(StructuralEqualityComparerEscapeFlagTests.Pairs), MemberType = typeof(StructuralEqualityComparerEscapeFlagTests))]
    public void Unparse_WritesEveryEscapeBack(string cell, string escaped, string plain)
    {
        _ = plain;
        AssertRoundTrips(cell, escaped, Unparser.Unparse(Parse(escaped)));
    }

    [Theory]
    [MemberData(nameof(StructuralEqualityComparerEscapeFlagTests.Pairs), MemberType = typeof(StructuralEqualityComparerEscapeFlagTests))]
    public void Format_WritesEveryEscapeBack(string cell, string escaped, string plain)
    {
        _ = plain;
        var result = FormatterService.Format(escaped);
        result.Diagnostics.Should().BeEmpty($"{cell}: Format must not refuse");
        AssertRoundTrips(cell, escaped, result.FormattedText);
    }
}
