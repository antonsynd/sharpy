using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// A <c>None</c> test on a non-nullable CLR reference type has ONE spelling (#2221, ruling R-DX part 1):
/// <c>is None</c>, the reference null check (<c>v is null</c> — it never runs an overloaded
/// <c>op_Equality</c>, #2224 ruling R-EJ). <c>== None</c> on it is refused (SPY0222) with the
/// <c>is None</c> steer; <c>== None</c> on <c>T | None</c> is the spelling that runs the overload
/// (pinned with a fake-null CLR type in <see cref="IsNoneReferenceCheckMatrixTests"/>).
/// </summary>
/// <remarks>
/// <para>Axes: operand {CLR reference with an overloaded <c>==</c> (<c>System.Version</c>), CLR reference
/// without one (<c>StringBuilder</c>), Sharpy class, <c>T | None</c> over a CLR reference, <c>T?</c> over a
/// CLR reference} × spelling {<c>is None</c>, <c>is not None</c>, <c>== None</c>, <c>!= None</c>} × position
/// {<c>return</c>, <c>if</c>, <c>assert</c>, <c>assert</c> under the <c>@test</c> host}.</para>
/// <para>The verdict table (<see cref="IsRefused"/>) is spelled by hand. Every admitted cell is EXECUTED
/// for each subject state the operand can hold (a non-nullable operand is only ever present), and its
/// value is the python3 truth value of the test; the assert positions run in whichever of the holding /
/// violated directions the states allow. Every refused cell must report SPY0222 with the steer.</para>
/// </remarks>
[Collection("HeavyCompilation")]
public class ClrReferenceNoneTestMatrixTests : IntegrationTestBase
{
    public ClrReferenceNoneTestMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths().Append(typeof(Xunit.Assert).Assembly.Location);

    public enum Operand { ClrOverloadedEq, ClrPlainReference, SharpyClass, ClrNullable, ClrOptional }

    public enum Spelling { IsNone, IsNotNone, EqNone, NotEqNone }

    public enum Position { Return, If, Assert, TestHostAssert }

    private const string Prelude = "from system import Version\nfrom system.text import StringBuilder\n\n"
        + "class Box:\n    n: int\n\n    def __init__(self, n: int) -> None:\n        self.n = n\n\n";

    private static bool CanBeAbsent(Operand operand) => operand is Operand.ClrNullable or Operand.ClrOptional;

    private static string Declaration(Operand operand, bool present) => operand switch
    {
        Operand.ClrOverloadedEq => "v: Version = Version(1, 2)",
        Operand.ClrPlainReference => "v: StringBuilder = StringBuilder()",
        Operand.SharpyClass => "v: Box = Box(1)",
        Operand.ClrNullable => present ? "v: Version | None = Version(1, 2)" : "v: Version | None = None",
        Operand.ClrOptional => present ? "v: Version? = Some(Version(1, 2))" : "v: Version? = None()",
        _ => throw new ArgumentOutOfRangeException(nameof(operand)),
    };

    private static string Test(Spelling spelling, bool noneLeft = false)
    {
        var op = spelling switch
        {
            Spelling.IsNone => "is",
            Spelling.IsNotNone => "is not",
            Spelling.EqNone => "==",
            Spelling.NotEqNone => "!=",
            _ => throw new ArgumentOutOfRangeException(nameof(spelling)),
        };
        return noneLeft ? $"None {op} v" : $"v {op} None";
    }

    /// <summary>
    /// Hand-spelled verdicts: the equality spellings are refused on a non-nullable CLR reference (#2221)
    /// and, as before, on the Optional family; a Sharpy class keeps the #901 null check and
    /// <c>T | None</c> keeps the native <c>== null</c> (for these operands — no fake-null overload —
    /// both agree with <c>is None</c>).
    /// </summary>
    private static bool IsRefused(Operand operand, Spelling spelling)
        => spelling is Spelling.EqNone or Spelling.NotEqNone
            && operand is Operand.ClrOverloadedEq or Operand.ClrPlainReference or Operand.ClrOptional;

    /// <summary>Python truth value of the test for a present / absent subject.</summary>
    private static bool Truth(Spelling spelling, bool present)
        => spelling is Spelling.IsNotNone or Spelling.NotEqNone ? present : !present;

    private static string Steer(Spelling spelling)
        => spelling == Spelling.EqNone ? "Did you mean 'is None'?" : "Did you mean 'is not None'?";

    public static TheoryData<Operand, Spelling, Position> AdmittedCells() => Cells(refused: false);

    public static TheoryData<Operand, Spelling, Position> RefusedCells() => Cells(refused: true);

    private static TheoryData<Operand, Spelling, Position> Cells(bool refused)
    {
        var data = new TheoryData<Operand, Spelling, Position>();
        foreach (var operand in Enum.GetValues<Operand>())
            foreach (var spelling in Enum.GetValues<Spelling>())
                foreach (var position in Enum.GetValues<Position>())
                    if (IsRefused(operand, spelling) == refused)
                        data.Add(operand, spelling, position);
        return data;
    }

    [Fact]
    public void Matrix_CoversEveryCell()
    {
        // Anchored to literals: 5 operands × 4 spellings × 4 positions = 80, of which
        // 3 operands × 2 equality spellings × 4 positions = 24 are refusals.
        AdmittedCells().Count().Should().Be(56);
        RefusedCells().Count().Should().Be(24);
    }

    private static string Indent(string line, int spaces) => new string(' ', spaces) + line;

    /// <summary>A program evaluating the test in <paramref name="position"/> for one subject state.</summary>
    private static string Program(Operand operand, Spelling spelling, Position position, bool present,
        bool noneLeft = false)
    {
        var test = Test(spelling, noneLeft);
        var decl = Declaration(operand, present);
        var lines = position switch
        {
            Position.Return => new[]
            {
                "def probe() -> bool:", Indent(decl, 4), Indent($"return {test}", 4), "",
                "def main() -> None:", "    print(probe())",
            },
            Position.If => new[]
            {
                "def main() -> None:", Indent(decl, 4), Indent($"if {test}:", 4), Indent("print(True)", 8),
                Indent("else:", 4), Indent("print(False)", 8),
            },
            Position.Assert => new[]
            {
                "def main() -> None:", Indent(decl, 4), Indent($"assert {test}", 4), Indent("print(\"pass\")", 4),
            },
            Position.TestHostAssert => new[]
            {
                "class Cell:", "    @test", "    def test_cell(self) -> None:", Indent(decl, 8),
                Indent($"assert {test}", 8), "",
                "def main() -> None:", "    Cell().test_cell()", "    print(\"pass\")",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(position)),
        };
        return Prelude + string.Join("\n", lines) + "\n";
    }

    [Theory]
    [MemberData(nameof(AdmittedCells))]
    public void AdmittedCell_ObservesThePythonTruthValue(Operand operand, Spelling spelling, Position position)
    {
        var states = CanBeAbsent(operand) ? new[] { true, false } : new[] { true };
        foreach (var present in states)
        {
            var result = CompileAndExecute(Program(operand, spelling, position, present));
            var truth = Truth(spelling, present);
            var because = $"present={present}: {string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}";

            if (position is Position.Return or Position.If)
            {
                result.Success.Should().BeTrue(because);
                result.StandardOutput.Trim().Should().Be(truth ? "True" : "False", because);
            }
            else if (truth)
            {
                // The assertion holds: the program passes it.
                result.Success.Should().BeTrue(because);
                result.StandardOutput.Trim().Should().Be("pass", because);
            }
            else
            {
                // The assertion is violated: it must stop the program (non-vacuity direction).
                result.Success.Should().BeFalse(because);
                result.StandardOutput.Should().NotContain("pass", because);
                result.StandardError.Should().Contain(
                    position == Position.TestHostAssert ? "Xunit.Sdk." : "AssertionError", because);
            }

            if (position == Position.TestHostAssert)
                result.GeneratedCSharp.Should().Contain("Xunit.Assert.", "the cell must exercise the test-host rewrite");
        }
    }

    [Theory]
    [MemberData(nameof(RefusedCells))]
    public void RefusedCell_IsSpy0222WithTheIsNoneSteer(Operand operand, Spelling spelling, Position position)
    {
        var result = CompileAndExecute(Program(operand, spelling, position, present: true));

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation && d.Message.Contains(Steer(spelling)));
    }

    /// <summary>The None-left order and a comparison-chain link refuse exactly where the binary form does.</summary>
    [Theory]
    [InlineData(Operand.ClrOverloadedEq, Spelling.EqNone)]
    [InlineData(Operand.ClrOverloadedEq, Spelling.NotEqNone)]
    [InlineData(Operand.ClrPlainReference, Spelling.EqNone)]
    [InlineData(Operand.ClrPlainReference, Spelling.NotEqNone)]
    public void RefusedCell_NoneLeftAndChainLink_AreRefusedToo(Operand operand, Spelling spelling)
    {
        var noneLeft = CompileAndExecute(Program(operand, spelling, Position.Return, present: true, noneLeft: true));
        noneLeft.Success.Should().BeFalse();
        noneLeft.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation && d.Message.Contains(Steer(spelling)));

        var op = spelling == Spelling.EqNone ? "==" : "!=";
        var chain = CompileAndExecute(Prelude + string.Join("\n", new[]
        {
            "def main() -> None:", Indent(Declaration(operand, true), 4), "    w: bool = True",
            $"    print(v {op} None == w)", "",
        }));
        chain.Success.Should().BeFalse();
        // The `None == w` link is refused on its own (bool is a value type); the assertion names the
        // CLR operand so only the `v <op> None` link can satisfy it.
        var typeName = operand == Operand.ClrOverloadedEq ? "Version" : "StringBuilder";
        chain.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation
            && d.Message.Contains($"Type '{typeName}'") && d.Message.Contains(Steer(spelling)));
    }
}
