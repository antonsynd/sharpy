using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using KnownRedRefusal = Sharpy.Compiler.Tests.Properties.Generators.Typed.ErrorInjector.KnownRedRefusal;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// The compiler-side defects a typed generator's RAW output draws, each pinned to its tracker — ONE
/// definition read by both the roster's raw positive control (<c>TypedGeneratorRosterTests</c>) and
/// the generated lane's <see cref="RejectionSink"/> (#2288), so a pin cannot drift between them.
/// <para>
/// A sample is explained by a pin only when its PRE-unparse AST has the pinned shape AND re-parsing
/// the unparsed source yields parse errors that ALL carry the pin's code: any other parse error, any
/// semantic refusal of a parseable program, and any throw stay unexplained. Each pin must keep
/// firing (the roster's <c>ParsePins_StillFire</c>), so it drains with its fix instead of going
/// stale silently. Pins are constructed as <c>new KnownRedRefusal("#N", …)</c> so
/// <c>build_tools/allowlist_issue_state.py</c>'s <c>KNOWNRED_CELL_RE</c> sees the cite — a pin
/// citing a CLOSED issue reddens the <c>/push</c> gate.
/// </para>
/// </summary>
internal static class KnownGeneratorPins
{
    /// <summary>A parse-stage pin: <paramref name="Code"/> is the only parse error the pinned defect
    /// produces, <paramref name="AstShape"/> the pre-unparse node shape that produces it.</summary>
    public sealed record ParsePin(KnownRedRefusal Pin, string Code, Func<Node, bool> AstShape);

    public static readonly IReadOnlyList<ParsePin> ParsePins = new ParsePin[]
    {
        new(new KnownRedRefusal("#2169", "Unparser prints a conditional in a conditional's test position without parentheses → SPY0104"),
            "SPY0104", n => n is ConditionalExpression { Test: ConditionalExpression }),
    };

    /// <summary>The pin that explains <paramref name="parseErrors"/> — the re-parse errors of the
    /// source unparsed from <paramref name="origin"/> — or null. An empty error list is never
    /// explained (a program that parses is refused, if at all, for another reason).</summary>
    public static ParsePin? Match(Node origin, IReadOnlyCollection<CompilerDiagnostic> parseErrors)
    {
        if (parseErrors.Count == 0)
            return null;
        return ParsePins.FirstOrDefault(p => parseErrors.All(d => d.Code == p.Code)
            && AstDescendants.SelfAndDescendants(origin).Any(p.AstShape));
    }

    /// <summary>Re-parses <paramref name="source"/> (unparsed from <paramref name="origin"/>) with the
    /// real lexer and parser and returns the pin that explains its parse errors, or null — including
    /// when it parses clean or the lexer/parser throws.</summary>
    public static ParsePin? Explain(Node origin, string source)
    {
        try
        {
            var parser = new Sharpy.Compiler.Parser.Parser(new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll());
            parser.ParseModule();
            return Match(origin, parser.Diagnostics.GetErrors().ToList());
        }
        catch (Exception)
        {
            return null;
        }
    }
}
