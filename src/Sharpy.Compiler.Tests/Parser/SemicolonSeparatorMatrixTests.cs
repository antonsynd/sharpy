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
/// A <c>;</c> in statement position is refused by name — SPY0150 with the steer "put each
/// statement on its own line" (R-IO, #2355; deviation row <c>semicolon-statement-separator</c>).
///
/// <para>The matrix is separator position {between simple statements, trailing, leading,
/// <c>;;</c>, after a block header's <c>:</c>} × simple-statement kind × context {module, def
/// body, class body, loop body} (the REPL line is <c>ReplSessionTests.Semicolon_IsTheOneNamedRefusal</c>,
/// in the ConsoleCapture collection). Every cell must produce exactly ONE parser error,
/// SPY0150, at the first <c>;</c>'s own (line, column), derived from the generated source.
/// Before #2355 the same cells were SPY0100 (leading), SPY0102 (after a header) or SPY0103 (the
/// rest), none of which said why.</para>
/// </summary>
public class SemicolonSeparatorMatrixTests
{
    private static readonly string[] StatementKinds =
    {
        "a = 1",
        "print(1)",
        "a += 1",
        "x: int = 1",
        "pass",
        "import math",
        "from math import pi",
        "raise ValueError(\"e\")",
        "assert True",
        "type T = int",
    };

    private static readonly string[] Positions = { "between", "trailing", "leading", "double" };

    private static string Place(string position, string stmt) => position switch
    {
        "between" => $"{stmt}; pass",
        "trailing" => $"{stmt};",
        "leading" => $";{stmt}",
        "double" => $"{stmt};; pass",
        _ => throw new ArgumentOutOfRangeException(nameof(position)),
    };

    private static IEnumerable<(string Name, string Source)> Programs()
    {
        foreach (var position in Positions)
        {
            foreach (var stmt in StatementKinds)
            {
                var line = Place(position, stmt);
                yield return ($"module/{position}/{stmt}", "a = 0\n" + line + "\n");
                yield return ($"def/{position}/{stmt}", "def f():\n    a = 0\n    " + line + "\n");
                yield return ($"loop/{position}/{stmt}", "for i in range(1):\n    a = 0\n    " + line + "\n");
            }

            foreach (var field in new[] { "x: int = 1", "pass" })
                yield return ($"class/{position}/{field}", "class C:\n    " + Place(position, field) + "\n");

            yield return ($"def-return/{position}", "def f() -> int:\n    " + Place(position, "return 1") + "\n");
            yield return ($"loop-break/{position}", "while True:\n    " + Place(position, "break") + "\n");
        }

        foreach (var header in new[] { "if True:", "while False:", "for i in range(1):", "def f():", "class C:", "try:" })
            yield return ($"after-header/{header}", header + ";\n    pass\n");
        yield return ("after-header/else:", "if True:\n    pass\nelse:;\n    pass\n");
        yield return ("after-header/finally:", "try:\n    pass\nfinally:;\n    pass\n");
    }

    private static (int Line, int Column) LocateFirstSemicolon(string source)
    {
        var index = source.IndexOf(';', StringComparison.Ordinal);
        var lineStart = source.LastIndexOf('\n', index) + 1;
        return (1 + source.Take(index).Count(c => c == '\n'), index - lineStart + 1);
    }

    private static IReadOnlyList<CompilerDiagnostic> ParseErrors(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var parser = new ParserNs.Parser(lexer.TokenizeAll());
        parser.ParseModule();
        return parser.Diagnostics.GetErrors();
    }

    [Fact]
    public void EverySeparatorPosition_IsTheOneNamedRefusal()
    {
        var programs = Programs().ToList();
        programs.Should().HaveCount(4 * (10 * 3 + 2 + 2) + 8);

        var failures = new List<string>();
        foreach (var (name, source) in programs)
        {
            var errors = ParseErrors(source);
            var (line, column) = LocateFirstSemicolon(source);
            if (errors.Count != 1
                || errors[0].Code != DiagnosticCodes.Parser.SemicolonStatementSeparator
                || errors[0].Line != line
                || errors[0].Column != column)
            {
                failures.Add($"{name}: expected one SPY0150 @{line}:{column}, got "
                    + string.Join("; ", errors.Select(e => $"{e.Code} @{e.Line}:{e.Column} {e.Message}")));
            }
        }

        failures.Should().BeEmpty();
    }

    [Fact]
    public void TheRefusalCarriesTheSteer()
    {
        var error = ParseErrors("a = 1; b = 2\n").Single();
        error.Message.Should().Be("';' does not separate statements in Sharpy; put each statement on its own line");
    }

    [Fact]
    public void PositiveControl_TheSameStatementsOnTheirOwnLinesParse()
    {
        foreach (var stmt in StatementKinds)
            ParseErrors("a = 0\n" + stmt + "\npass\n").Should().BeEmpty(stmt);
    }
}
