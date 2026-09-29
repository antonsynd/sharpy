using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Position refusals for <c>let</c> (#1974, P21a): a <c>let</c> binding is local by definition, so
/// it is refused at module level (SPY0340 with the R-BL steer to <c>const</c>) and in every type body
/// the grammar lets it reach (SPY0340 with the field steer). Ranged as spelling × host; every refused
/// cell must produce EXACTLY ONE diagnostic of any severity — the SPY0340 at the <c>let</c> token —
/// which is what proves a refused <c>let</c> leaves no field, module variable or checker noise behind.
/// The <c>errors/let_*</c> fixtures are the file-based twins.
/// </summary>
/// <remarks>
/// A union body is ranged separately: the union grammar admits only <c>case</c>, <c>def</c> and
/// <c>pass</c>, so a <c>let</c> there is the parser's single refusal before semantic analysis runs.
/// </remarks>
[Collection("HeavyCompilation")]
public class LetPositionRefusalTests : IntegrationTestBase
{
    public LetPositionRefusalTests(ITestOutputHelper output) : base(output) { }

    private const string ModuleSteer = "module-scope bindings are `const`: use 'const NAME[: T] = ...'";

    /// <summary>The three <c>let</c> spellings, each a single statement line.</summary>
    private static readonly (string Name, string Line)[] Spellings =
    {
        ("bare", "let x = 5"),
        ("annotated", "let x: int = 5"),
        ("tuple", "let a, b = 1, 2"),
    };

    /// <summary>
    /// Hosts: a program template with a <c>{0}</c> slot for the <c>let</c> line (already indented
    /// for the host), the 1-based line and column of the <c>let</c> token, and the expected steer.
    /// </summary>
    private static readonly (string Name, string Template, int Line, int Column, string Steer)[] Hosts =
    {
        ("module", "{0}\n\ndef main():\n    print(1)\n", 1, 1, ModuleSteer),
        ("class", "class C:\n    y: int = 0\n    {0}\n\ndef main():\n    print(C().y)\n", 3, 5,
            "`let` is not allowed in a class body; declare a field as 'x: T = ...' or a constant as 'const X = ...'"),
        ("struct", "struct S:\n    y: int = 0\n    {0}\n\ndef main():\n    print(S().y)\n", 3, 5,
            "`let` is not allowed in a struct body; declare a field as 'x: T = ...' or a constant as 'const X = ...'"),
        ("interface", "interface I:\n    {0}\n    def f(self) -> int: ...\n\ndef main():\n    print(1)\n", 2, 5,
            "`let` is not allowed in an interface body; declare a field as 'x: T = ...' or a constant as 'const X = ...'"),
        ("nested class", "class O:\n    class N:\n        {0}\n\ndef main():\n    print(1)\n", 3, 9,
            "`let` is not allowed in a class body; declare a field as 'x: T = ...' or a constant as 'const X = ...'"),
    };

    public static IEnumerable<object[]> Cells()
    {
        foreach (var host in Hosts)
            foreach (var spelling in Spellings)
                yield return new object[] { host.Name, spelling.Name };
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Let_InADeclarationOnlyPosition_IsExactlyOneSpy0340WithTheSteer(string hostName, string spellingName)
    {
        var host = Hosts.Single(h => h.Name == hostName);
        var spelling = Spellings.Single(s => s.Name == spellingName);
        var source = string.Format(System.Globalization.CultureInfo.InvariantCulture, host.Template, spelling.Line);

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"`{spelling.Line}` in a {host.Name} position must be refused");
        result.RawDiagnostics.Should().ContainSingle(
            $"a refused `let` is exactly one diagnostic of any severity; got: {Describe(result)}");
        var diagnostic = result.RawDiagnostics[0];
        diagnostic.Code.Should().Be(DiagnosticCodes.Semantic.ModuleLevelExecutableStatement);
        diagnostic.Severity.Should().Be(CompilerDiagnosticSeverity.Error);
        diagnostic.Message.Should().Contain(host.Steer);
        diagnostic.Line.Should().Be(host.Line, "the diagnostic is at the `let` token");
        diagnostic.Column.Should().Be(host.Column, "the diagnostic is at the `let` token");
    }

    /// <summary>
    /// The union host: its grammar admits only <c>case</c>, <c>def</c> and <c>pass</c>, so every
    /// <c>let</c> spelling is the parser's ONE refusal at the <c>let</c> token — no SPY0340 (semantic
    /// analysis never runs) and no recovery cascade after it.
    /// </summary>
    [Theory]
    [InlineData("let x = 5")]
    [InlineData("let x: int = 5")]
    [InlineData("let a, b = 1, 2")]
    public void Let_InAUnionBody_IsTheUnionGrammarsSingleRefusal(string letLine)
    {
        var result = CompileAndExecute($"union U:\n    case A(v: int)\n    {letLine}\n\ndef main():\n    print(1)\n");

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().ContainSingle(
            $"a union-body `let` is exactly one diagnostic of any severity; got: {Describe(result)}");
        var diagnostic = result.RawDiagnostics[0];
        diagnostic.Code.Should().Be(DiagnosticCodes.Parser.ExpectedToken);
        diagnostic.Message.Should().Contain("Expected Case, got Let");
        diagnostic.Line.Should().Be(3, "the diagnostic is at the `let` token");
        diagnostic.Column.Should().Be(5, "the diagnostic is at the `let` token");
    }

    [Fact]
    public void ModuleConst_PositiveControl_Runs()
    {
        var result = CompileAndExecute("const LIMIT: int = 10\n\ndef main():\n    print(LIMIT)\n");

        result.Success.Should().BeTrue(Describe(result));
        result.StandardOutput.Trim().Should().Be("10");
    }

    [Fact]
    public void ClassBodyConstAndField_PositiveControls_Run()
    {
        var result = CompileAndExecute(
            "class Counter:\n    count: int = 1\n    const STEP: int = 2\n\n"
            + "def main():\n    print(Counter().count)\n    print(Counter.STEP)\n");

        result.Success.Should().BeTrue(Describe(result));
        result.StandardOutput.ReplaceLineEndings("\n").Trim().Should().Be("1\n2");
    }

    [Fact]
    public void FunctionBodyLet_PositiveControl_RunsWithoutSpy0340()
    {
        var result = CompileAndExecute("def main():\n    let x = 5\n    let y: int = 6\n    let a, b = 7, 8\n    print(x + y + a + b)\n");

        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.ModuleLevelExecutableStatement);
        result.Success.Should().BeTrue(Describe(result));
        result.StandardOutput.Trim().Should().Be("26");
    }

    private static string Describe(ExecutionResult result)
        => string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Severity} {d.Code} ({d.Line}:{d.Column}) {d.Message}"));
}
