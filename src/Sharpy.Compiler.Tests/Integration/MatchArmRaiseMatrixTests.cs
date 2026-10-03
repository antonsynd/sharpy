using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Formatting;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #2190 (ruling R-DR) — <c>raise &lt;exception&gt;</c> is accepted as a match-EXPRESSION arm body
/// ONLY, lowers to a C# throw, and contributes no type to the match expression. Matrix:
/// <b>arm position {last, middle, every, nested match expression} × value position {return,
/// assignment, call argument via a local}</b> = 12 run cells (the 3 <c>every</c> cells are
/// refused: with no value-producing arm there is no type — the checker types a match expression
/// from its arms alone, it has no slot-directed typing); plus the <c>is</c>-chain cells (a raising
/// arm next to a hoisting arm, and a raising arm that hoists itself), the refusal cells (raise as a
/// general expression, bare raise in an arm, <c>raise … from</c>, a non-exception), the
/// exhaustiveness cell (unchanged SPY0416), and the formatter round trip.
///
/// <para><b>Base classification (measured with <c>sharpyc run</c> at 6b208066e):</b> all 12 matrix
/// cells were SPY0100 "Unexpected token: Raise".</para>
/// </summary>
[Collection("HeavyCompilation")]
public class MatchArmRaiseMatrixTests : IntegrationTestBase
{
    public MatchArmRaiseMatrixTests(ITestOutputHelper output) : base(output) { }

    private static readonly Dictionary<string, string[]> ArmShapes = new()
    {
        ["last"] = new[] { "case 0: \"zero\"", "case 1: \"one\"", "case _: raise ValueError(f\"bad {n}\")" },
        ["middle"] = new[] { "case 0: \"zero\"", "case 1: raise ValueError(f\"bad {n}\")", "case _: \"many\"" },
        ["every"] = new[] { "case 0: raise ValueError(\"zero\")", "case _: raise ValueError(f\"bad {n}\")" },
        ["nested"] = new[] { "case 0: \"zero\"", "case _: match n:\n{I}    case 7: \"big\"\n{I}    case _: raise ValueError(f\"bad {n}\")" },
    };

    /// <summary>What f(n) does for n in [0, 1, 7, 3]: a value, or "raised &lt;message&gt;".</summary>
    private static readonly Dictionary<string, string> Expected = new()
    {
        ["last"] = "zero\none\nraised bad 7\nraised bad 3\n",
        ["middle"] = "zero\nraised bad 1\nmany\nmany\n",
        ["nested"] = "zero\nraised bad 1\nbig\nraised bad 3\n",
    };

    private static readonly string[] ValuePositions = { "return", "assignment", "argument" };

    public static IEnumerable<object[]> Cells() =>
        from arm in ArmShapes.Keys from pos in ValuePositions select new object[] { arm, pos };

    private static string MatchExpr(string arm, string indent) =>
        "match n:" + string.Concat(ArmShapes[arm].Select(a => $"\n{indent}    " + a.Replace("{I}", indent + "    ", StringComparison.Ordinal)));

    private static string Program(string arm, string position)
    {
        var fn = position switch
        {
            "return" => "def f(n: int) -> str:\n    return " + MatchExpr(arm, "    ") + "\n",
            "assignment" => "def f(n: int) -> str:\n    s = " + MatchExpr(arm, "    ") + "\n    return s\n",
            "argument" => "def g(s: str) -> str:\n    return s\n\ndef f(n: int) -> str:\n    s = " + MatchExpr(arm, "    ") + "\n    return g(s)\n",
            _ => throw new ArgumentOutOfRangeException(nameof(position)),
        };
        return fn + "\ndef main():\n    for n in [0, 1, 7, 3]:\n        try:\n            print(f(n))\n"
            + "        except ValueError as e:\n            print(\"raised\", e)\n";
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void RaiseArm_InEveryArmPosition_AndValuePosition(string arm, string position)
    {
        var source = Program(arm, position);
        var result = CompileAndExecute(source);

        if (arm == "every")
        {
            result.Success.Should().BeFalse(source);
            result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0242"
                && d.Message.Contains("Every arm of this match expression raises", StringComparison.Ordinal), source);
            return;
        }

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Should().Be(Expected[arm], source);
        // The arm is a C# throw — never a value the switch has to type.
        result.GeneratedCSharp.Should().Contain("throw new", source);
    }

    /// <summary>
    /// The <c>is</c>-chain lowering (an arm hoists, so the match expression becomes statements): a
    /// raise there is a throw STATEMENT, since C# refuses a throw expression as an assignment's
    /// right-hand side (CS8115).
    /// </summary>
    [Theory]
    [InlineData("case 0: [v * v for v in xs]\n        case _: raise ValueError(f\"none {n}\")", "[1, 4, 9]\nraised none 2\n")]
    [InlineData("case 0: [9]\n        case _: raise ValueError(str([v for v in xs]))", "[9]\nraised [1, 2, 3]\n")]
    public void RaiseArm_InTheIsChainLowering(string arms, string expected)
    {
        var source = "def f(n: int) -> list[int]:\n    xs: list[int] = [1, 2, 3]\n    return match n:\n        " + arms + "\n\n"
            + "def main():\n    for n in [0, 2]:\n        try:\n            print(f(n))\n        except ValueError as e:\n            print(\"raised\", e)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Should().Be(expected, source);
        result.GeneratedCSharp.Should().Contain("__matchTaken", "the hoisting arm must force the is-chain lowering");
    }

    /// <summary>Refusals: raise stays a statement everywhere else, and the arm body follows raise's own rules.</summary>
    [Theory]
    [InlineData("def main():\n    x = raise ValueError(\"no\")\n", "SPY0100", "Unexpected token: Raise")]
    [InlineData("def main():\n    print(raise ValueError(\"no\"))\n", "SPY0100", "Unexpected token: Raise")]
    [InlineData("def f(n: int) -> int:\n    return match n:\n        case 0: 1\n        case _: raise\n\ndef main():\n    print(f(0))\n", "SPY0100", "A raising match-expression arm needs an exception")]
    [InlineData("def f(n: int) -> int:\n    return match n:\n        case 0: 1\n        case _: raise ValueError(\"a\") from ValueError(\"b\")\n\ndef main():\n    print(f(0))\n", "SPY0100", "'raise ... from' is not supported in a match-expression arm")]
    [InlineData("def f(n: int) -> int:\n    return match n:\n        case 0: 1\n        case _: raise 42\n\ndef main():\n    print(f(0))\n", "SPY0242", "Cannot raise a value of type 'int32'")]
    public void RaiseOutsideItsPosition_OrMalformed_IsRefused(string source, string code, string message)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(source);
        result.RawDiagnostics.Should().Contain(d => d.Code == code && d.Message.Contains(message, StringComparison.Ordinal),
            $"{source}\n{string.Join("; ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
    }

    /// <summary>Exhaustiveness is unchanged: a raising arm is an arm like any other (SPY0416 without a catch-all).</summary>
    [Fact]
    public void RaiseArm_DoesNotChangeExhaustiveness()
    {
        var source = "def f(n: int) -> str:\n    return match n:\n        case 0: \"zero\"\n        case 1: raise ValueError(\"one\")\n\n"
            + "def main():\n    print(f(0))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0416");
    }

    /// <summary>
    /// The formatter writes the raise back and is idempotent. (Not byte-equality with the source:
    /// the formatter adds a blank line after any match expression statement — with or without a
    /// raising arm — a findings-ledger cell of this round outside #2190.)
    /// </summary>
    [Fact]
    public void Formatter_RoundTripsARaiseArm()
    {
        var source = "def f(n: int) -> str:\n    return match n:\n        case 0: \"zero\"\n        case _: raise ValueError(f\"bad {n}\")\n";
        var formatted = FormatterService.Format(source);
        formatted.Diagnostics.Should().BeEmpty();
        formatted.FormattedText.Should().StartWith(source.TrimEnd('\n'));
        formatted.FormattedText.Should().Contain("        case _: raise ValueError(f\"bad {n}\")\n");
        FormatterService.Format(formatted.FormattedText).FormattedText.Should().Be(formatted.FormattedText, "idempotent");

        var lexer = new LexerNs.Lexer(formatted.FormattedText);
        var module = new ParserNs.Parser(lexer.TokenizeAll()).ParseModule();
        var fn = module.Body.Should().ContainSingle().Subject.Should().BeOfType<Sharpy.Compiler.Parser.Ast.FunctionDef>().Subject;
        var ret = fn.Body.Should().ContainSingle().Subject.Should().BeOfType<Sharpy.Compiler.Parser.Ast.ReturnStatement>().Subject;
        var match = ret.Value.Should().BeOfType<Sharpy.Compiler.Parser.Ast.MatchExpression>().Subject;
        match.Arms.Select(a => a.IsRaise).Should().Equal(false, true);
    }
}
