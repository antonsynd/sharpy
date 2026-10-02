using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// SPY0132 ("'_' placeholder can only be used inside function call arguments …") is the refusal of
/// an UNESCAPED <c>_</c> that reached the checker outside a call or operator section. A
/// backtick-escaped <c>`_`</c> is the variable <c>_</c> (identifiers.md § Literal Names, #2166): bound,
/// it reads its value; unbound, it is an ordinary undefined name (SPY0200), never the placeholder
/// refusal. The bare rows are the positive controls (the <c>errors/placeholder_outside_call</c>
/// fixture is the same cell as a file).
/// </summary>
[Collection("HeavyCompilation")]
public class PlaceholderDiagnosticTests : IntegrationTestBase
{
    public PlaceholderDiagnosticTests(ITestOutputHelper output) : base(output) { }

    public static TheoryData<string, string, string> UnboundReads() => new()
    {
        // cell, source, expected code
        { "escaped_assignment", "def main():\n    y = `_`\n    print(y)\n", DiagnosticCodes.Semantic.UndefinedVariable },
        { "escaped_return", "def f() -> int:\n    return `_`\n\ndef main():\n    print(f())\n", DiagnosticCodes.Semantic.UndefinedVariable },
        { "escaped_parenthesized_operand", "def main():\n    y = (`_` + 1)\n    print(y)\n", DiagnosticCodes.Semantic.UndefinedVariable },
        { "bare_assignment", "def main():\n    y = _\n    print(y)\n", DiagnosticCodes.Parser.PlaceholderOutsideCallOrOperator },
        { "bare_return", "def f() -> int:\n    return _\n\ndef main():\n    print(f())\n", DiagnosticCodes.Parser.PlaceholderOutsideCallOrOperator },
    };

    [Theory]
    [MemberData(nameof(UnboundReads))]
    public void AnUnboundUnderscoreRead_ReportsThePlaceholderRefusal_OnlyWhenUnescaped(string cell, string source, string code)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{cell}]");
        var errors = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).ToList();
        var listing = string.Join(" | ", errors.Select(d => d.Code + " " + d.Message));
        errors.Should().ContainSingle($"[{cell}] {listing}");
        errors[0].Code.Should().Be(code, $"[{cell}] {listing}");
    }

    /// <summary>A bound escaped <c>`_`</c> reads its value — the arm above is not reached for it.</summary>
    [Fact]
    public void ABoundEscapedUnderscore_ReadsItsValue()
    {
        var result = CompileAndExecute("def main():\n    _ = 3\n    y = `_`\n    print(y)\n");

        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Replace("\r\n", "\n").Should().Be("3\n");
    }
}
