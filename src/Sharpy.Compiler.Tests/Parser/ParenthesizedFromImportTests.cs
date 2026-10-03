using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// #2188 (ruling R-DS) — the PEP 328 parenthesized <c>from m import (a, b,\n c)</c> form. Cells:
/// <b>{single line in parens, multi-line, trailing comma, <c>as</c> aliases, comments between
/// names, a backtick-escaped name}</b> parse to the same names as the unparenthesized form with
/// <see cref="FromImportStatement.IsParenthesized"/> recorded; <b>{<c>(*)</c>, <c>()</c>, an
/// unparenthesized trailing comma, <c>(,)</c>, <c>(a,,)</c>, an unclosed paren}</b> are refused as
/// in python3 (checked: SyntaxError for each). The formatter keeps the written form —
/// parenthesized stays parenthesized, unparenthesized stays unparenthesized. Run cells live in the
/// fixture <c>imports/from_import_parenthesized</c>, which also feeds the formatter
/// meaning-preservation sweep's corpus.
///
/// <para><b>Base classification (measured with <c>sharpyc run</c> at 6b208066e):</b> every
/// parenthesized cell was SPY0101 "Expected identifier, got LeftParen".</para>
/// </summary>
public class ParenthesizedFromImportTests
{
    public static IEnumerable<object[]> AcceptedCells() => new[]
    {
        new object[] { "single", "from math import (sqrt, floor)\n", new[] { "sqrt", "floor" }, new string?[] { null, null } },
        new object[] { "single_trailing_comma", "from math import (sqrt,)\n", new[] { "sqrt" }, new string?[] { null } },
        new object[] { "multi", "from math import (\n    sqrt,\n    floor\n)\n", new[] { "sqrt", "floor" }, new string?[] { null, null } },
        new object[] { "multi_trailing_comma", "from math import (\n    sqrt,\n    floor,\n)\n", new[] { "sqrt", "floor" }, new string?[] { null, null } },
        new object[] { "aliases", "from math import (sqrt as root,\n    floor as down,)\n", new[] { "sqrt", "floor" }, new string?[] { "root", "down" } },
        new object[] { "comments", "from math import (  # c1\n    # c2\n    sqrt,  # c3\n    floor\n)  # c4\n", new[] { "sqrt", "floor" }, new string?[] { null, null } },
        new object[] { "escaped", "from math import (`sqrt` as `root`, pi)\n", new[] { "sqrt", "pi" }, new string?[] { "root", null } },
    };

    [Theory]
    [MemberData(nameof(AcceptedCells))]
    public void ParenthesizedNames_Parse_AndRecordTheWrittenSpelling(string cell, string source, string[] names, string?[] aliases)
    {
        var (module, errors) = Parse(source);

        errors.Should().BeEmpty($"cell {cell}:\n{source}");
        var stmt = module.Body.Should().ContainSingle().Subject.Should().BeOfType<FromImportStatement>().Subject;
        stmt.IsParenthesized.Should().BeTrue();
        stmt.ImportAll.Should().BeFalse();
        stmt.Module.Should().Be("math");
        stmt.Names.Select(n => n.Name).Should().Equal(names);
        stmt.Names.Select(n => n.AsName).Should().Equal(aliases);
    }

    [Fact]
    public void UnparenthesizedNames_AreNotMarkedParenthesized()
    {
        var (module, errors) = Parse("from math import sqrt, floor as down\n");
        errors.Should().BeEmpty();
        var stmt = module.Body.Should().ContainSingle().Subject.Should().BeOfType<FromImportStatement>().Subject;
        stmt.IsParenthesized.Should().BeFalse();
        stmt.Names.Select(n => n.Name).Should().Equal("sqrt", "floor");
    }

    [Theory]
    [InlineData("from math import (*)\n", "'import *' cannot be parenthesized")]
    [InlineData("from math import ()\n", "Expected identifier, got RightParen")]
    [InlineData("from math import sqrt,\n", "Expected identifier, got Newline")]
    [InlineData("from math import (,)\n", "Expected identifier, got Comma")]
    [InlineData("from math import (sqrt,,)\n", "Expected identifier, got Comma")]
    [InlineData("from math import (sqrt, floor\n", "RightParen")]
    public void PythonRefusedSpellings_AreRefused(string source, string message)
    {
        var (_, errors) = Parse(source);
        errors.Should().NotBeEmpty(source);
        errors[0].Message.Should().Contain(message, source);
    }

    /// <summary>The formatter keeps the written form: R-DS (the R-DL precedent).</summary>
    [Theory]
    [InlineData("from math import (sqrt, floor)\n", "from math import (sqrt, floor)\n")]
    [InlineData("from math import (\n    sqrt,\n    floor,\n)\n", "from math import (sqrt, floor)\n")]
    [InlineData("from math import (sqrt as root,)\n", "from math import (sqrt as root)\n")]
    [InlineData("from math import sqrt, floor\n", "from math import sqrt, floor\n")]
    [InlineData("from math import (  # c1\n    sqrt,  # c2\n    floor,\n)\n", "from math import (  # c1\n    sqrt,  # c2\n    floor,\n)\n")]
    public void Formatter_KeepsTheWrittenParenthesization(string source, string expected)
    {
        var result = FormatterService.Format(source);

        result.Diagnostics.Should().BeEmpty(source);
        result.FormattedText.Should().Be(expected);
        FormatterService.Format(result.FormattedText).FormattedText.Should().Be(expected, "the formatter is idempotent");
    }

    private static (Module Module, List<Sharpy.Compiler.Diagnostics.CompilerDiagnostic> Errors) Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();
        return (module, lexer.Diagnostics.GetErrors().Concat(parser.Diagnostics.GetErrors()).ToList());
    }
}
