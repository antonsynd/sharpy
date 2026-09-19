using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// '...' (Ellipsis) in a TYPE position is refused by name — SPY0149 (#1852, R-AM).
///
/// <para>One parser seam, <c>ParseTypeAnnotationCore</c>, is reached from every type position
/// (type arguments, tuple shorthand elements, alias RHS, parameters, returns, class fields), so
/// this matrix crosses the spellings that carry '...' with the positions that recurse into that
/// seam — a single entry check must cover the whole cross product, not one check per position.</para>
///
/// <para>These are PARSE assertions: the diagnostic's code and the ellipsis token's own (line,
/// column) are the whole answer, computed from the generated source rather than hand-counted so a
/// change to any one embedding cannot silently drift the expected position out of sync with the
/// assertion. The executing counterparts (compile-and-run) live in
/// <c>Integration/TestFixtures/types/tuple_ellipsis_refused_1852*</c> and
/// <c>callable_ellipsis_refused_1852*</c>.</para>
/// </summary>
public class EllipsisTypePositionTests
{
    private static IReadOnlyList<CompilerDiagnostic> ParseErrors(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var parser = new ParserNs.Parser(lexer.TokenizeAll());
        parser.ParseModule();
        return parser.Diagnostics.GetErrors();
    }

    /// <summary>
    /// Locates '...' in the generated source and returns its 1-indexed (line, column) — the same
    /// convention <see cref="CompilerDiagnostic"/> uses — so each case's expected position is
    /// derived from the source it actually parsed instead of hand-counted per spelling/position.
    /// </summary>
    private static (int Line, int Column) LocateEllipsis(string source)
    {
        var index = source.IndexOf("...", StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, "the generated source must contain '...'");

        var line = 1;
        var lastNewline = -1;
        for (var i = 0; i < index; i++)
        {
            if (source[i] == '\n')
            {
                line++;
                lastNewline = i;
            }
        }

        return (line, index - lastNewline);
    }

    // ── spelling axis: every syntactic shape that carries '...' in a type-argument list ─────────

    public static IEnumerable<object[]> Spellings => new[]
    {
        new object[] { "tuple[int, ...]", "trailing ellipsis (runtime-arity tuple)" },
        new object[] { "tuple[..., int]", "leading ellipsis" },
        new object[] { "Callable[..., int]", "Callable's argument-list ellipsis" },
        new object[] { "list[...]", "list's sole type argument" },
        new object[] { "dict[str, tuple[int, ...]]", "nested inside a dict value" },
        new object[] { "(int, ...)", "tuple shorthand" },
        new object[] { "Box[...]", "a user-defined generic's argument" },
    };

    // ── position axis: every place that recurses into ParseTypeAnnotationCore ───────────────────

    private static string Embed(string typeExpr, string position) => position switch
    {
        "local annotation" => $"x: {typeExpr}\n",
        "parameter" => $"def f(a: {typeExpr}):\n    pass\n",
        "return" => $"def f() -> {typeExpr}:\n    pass\n",
        "alias" => $"type Row = {typeExpr}\n",
        "class field" => $"class C:\n    x: {typeExpr}\n",
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, "unknown position")
    };

    public static IEnumerable<object[]> Positions => new[]
    {
        new object[] { "local annotation" },
        new object[] { "parameter" },
        new object[] { "return" },
        new object[] { "alias" },
        new object[] { "class field" },
    };

    public static IEnumerable<object[]> SpellingPositionMatrix =>
        from spelling in Spellings
        from position in Positions
        select new object[] { spelling[0], spelling[1], position[0] };

    [Theory]
    [MemberData(nameof(SpellingPositionMatrix))]
    public void EllipsisInTypePosition_IsRefusedAtItsOwnSpan(string typeExpr, string label, string position)
    {
        var source = Embed(typeExpr, position);
        var errors = ParseErrors(source);
        var (expectedLine, expectedColumn) = LocateEllipsis(source);

        var diag = errors.FirstOrDefault(e => e.Code == DiagnosticCodes.Parser.EllipsisInTypePosition);

        diag.Should().NotBeNull(
            $"{label} at {position} must be refused as SPY0149; got "
            + string.Join("; ", errors.Select(e => $"{e.Code}:{e.Message}"))
            + $"\nsource:\n{source}");
        diag!.Line.Should().Be(expectedLine, $"{label} at {position}: line of the ellipsis token");
        diag.Column.Should().Be(expectedColumn, $"{label} at {position}: column of the ellipsis token");
        diag.Message.Should().Contain("list[int]").And.Contain("IEnumerable[int]").And.Contain("#1870");
    }

    /// <summary>
    /// The literal alias-statement spelling named in the plan's acceptance matrix — the same cell
    /// the (<c>tuple[int, ...]</c>, alias) row of the matrix above already produces, pinned
    /// explicitly so a change to alias-RHS parsing (<c>Parser.Definitions.ParseTypeAlias</c>)
    /// cannot silently drop it without also breaking a directly-named case.
    /// </summary>
    [Fact]
    public void EllipsisInTypeAlias_IsRefused()
    {
        const string source = "type Row = tuple[int, ...]\n";
        var errors = ParseErrors(source);
        var (expectedLine, expectedColumn) = LocateEllipsis(source);

        errors.Should().Contain(e => e.Code == DiagnosticCodes.Parser.EllipsisInTypePosition
            && e.Line == expectedLine && e.Column == expectedColumn);
    }

    // ── controls: spellings and positions the refusal must NOT touch ────────────────────────────

    [Fact]
    public void FixedArityTuple_StillParsesCleanly()
    {
        var errors = ParseErrors("x: tuple[int, str]\n");

        errors.Should().BeEmpty("a fixed-arity tuple type is not the ellipsis refusal's subject");
    }

    [Fact]
    public void EllipsisAsAValue_StillParses()
    {
        // '...' in VALUE position (not a type) is unrelated — SPY0227 ("cannot infer a type") is a
        // semantic-phase diagnostic this parser-only test cannot see; the parser's job is only to
        // accept the literal, which it does as an EllipsisLiteral (Parser.Primaries.cs).
        var errors = ParseErrors("x = ...\n");

        errors.Should().NotContain(e => e.Code == DiagnosticCodes.Parser.EllipsisInTypePosition);
    }

    [Fact]
    public void EllipsisStubBody_StillParsesCleanly()
    {
        var errors = ParseErrors("def f(): ...\n");

        errors.Should().BeEmpty("the inline-stub spelling is unrelated to a type-position ellipsis");
    }
}
