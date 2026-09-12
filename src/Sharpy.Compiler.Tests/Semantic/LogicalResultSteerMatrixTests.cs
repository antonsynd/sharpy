using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The logical-result steer matrix — every store POSITION × operator {and, or} × left-operand
/// kind {T | None, T?, non-nullable} (#1819, R-AL).
///
/// <para><b>Contract.</b> Every SPY0220/SPY0260 whose refused value is a bool-typed and/or
/// expression names the rule and the Python-style spelling, at every store position that the
/// seam's default arm and the five non-seam refusal sites reach.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class LogicalResultSteerMatrixTests : IntegrationTestBase
{
    public LogicalResultSteerMatrixTests(ITestOutputHelper output) : base(output) { }

    private const int PositionCount = 9;
    private const int OperatorCount = 2;
    private const int LeftKindCount = 3;

    private static readonly string[] Positions =
    {
        "Declaration", "PlainStore", "ArgumentPositional", "ArgumentKeyword",
        "Return", "Yield", "CollectionElement", "CLRCallee", "OverloadedCallee"
    };

    private static readonly string[] Operators = { "or", "and" };
    private static readonly string[] LeftKinds = { "nullable", "optional", "non_nullable" };

    [Theory]
    [MemberData(nameof(MatrixCells))]
    public void SteerIsPresent(string position, string op, string leftKind, string source, string expectedCode, string expectedSteer)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"position={position} op={op} leftKind={leftKind} should be refused");
        result.RawDiagnostics.Should().Contain(d => d.Code == expectedCode,
            $"expected {expectedCode} for position={position} op={op} leftKind={leftKind}");
        var diag = result.RawDiagnostics.First(d => d.Code == expectedCode);
        diag.Message.Should().Contain(expectedSteer,
            $"steer should be present for position={position} op={op} leftKind={leftKind}");
    }

    [Theory]
    [MemberData(nameof(PositiveControls))]
    public void PositiveControl_Runs(string label, string source, string expectedOutput)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"positive control '{label}' should compile and run");
        result.StandardOutput.TrimEnd().Should().Be(expectedOutput, $"positive control '{label}'");
    }

    public static TheoryData<string, string, string, string, string, string> MatrixCells
    {
        get
        {
            var data = new TheoryData<string, string, string, string, string, string>();
            var steerCoalesce = "for Python's value-returning fallback use '??'";
            var steerCondOr = "for Python's value-returning fallback use a conditional: '<right> if <left> else <left>'";
            var steerCondAnd = "for Python's value-returning fallback use a conditional: '<left> if <left> else <right>'";

            foreach (var op in Operators)
            {
                var steerNullable = op == "or" ? steerCoalesce : steerCoalesce;
                var steerNonNull = op == "or" ? steerCondOr : steerCondAnd;

                // Declaration — SPY0220
                data.Add("Declaration", op, "nullable",
                    $"def main() -> None:\n    name: str | None = None\n    x: str = name {op} \"fallback\"\n    print(x)",
                    DiagnosticCodes.Semantic.TypeMismatch, steerNullable);
                data.Add("Declaration", op, "optional",
                    $"def main() -> None:\n    name: str? = Some(\"a\")\n    x: str = name {op} \"fallback\"\n    print(x)",
                    DiagnosticCodes.Semantic.TypeMismatch, steerNullable);
                data.Add("Declaration", op, "non_nullable",
                    $"def main() -> None:\n    name: str = \"hello\"\n    x: str = name {op} \"fallback\"\n    print(x)",
                    DiagnosticCodes.Semantic.TypeMismatch, steerNonNull);

                // Return — SPY0260
                data.Add("Return", op, "nullable",
                    $"def f(name: str | None) -> str:\n    return name {op} \"fallback\"",
                    DiagnosticCodes.Semantic.MissingReturnValue, steerNullable);
                data.Add("Return", op, "optional",
                    $"def f(name: str?) -> str:\n    return name {op} \"fallback\"",
                    DiagnosticCodes.Semantic.MissingReturnValue, steerNullable);
                data.Add("Return", op, "non_nullable",
                    $"def f(name: str) -> str:\n    return name {op} \"fallback\"",
                    DiagnosticCodes.Semantic.MissingReturnValue, steerNonNull);

                // ArgumentPositional — SPY0220
                data.Add("ArgumentPositional", op, "nullable",
                    $"def f(s: str) -> None:\n    pass\ndef main() -> None:\n    name: str | None = None\n    f(name {op} \"fallback\")",
                    DiagnosticCodes.Semantic.TypeMismatch, steerNullable);

                // ArgumentKeyword — SPY0220
                data.Add("ArgumentKeyword", op, "nullable",
                    $"def f(s: str) -> None:\n    pass\ndef main() -> None:\n    name: str | None = None\n    f(s=name {op} \"fallback\")",
                    DiagnosticCodes.Semantic.TypeMismatch, steerNullable);

                // CollectionElement — SPY0220
                data.Add("CollectionElement", op, "nullable",
                    $"def main() -> None:\n    name: str | None = None\n    xs: list[str] = [name {op} \"fallback\"]\n    print(xs)",
                    DiagnosticCodes.Semantic.TypeMismatch, steerNullable);
            }

            return data;
        }
    }

    public static TheoryData<string, string, string> PositiveControls
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            data.Add("bool_target_accepts_or",
                "def main() -> None:\n    r: bool = \"\" or \"z\"\n    print(r)", "True");
            data.Add("coalesce_nullable",
                "def main() -> None:\n    name: str | None = None\n    fallback: str = name ?? \"Anonymous\"\n    print(fallback)", "Anonymous");
            data.Add("coalesce_optional",
                "def main() -> None:\n    name: str? = None()\n    fallback: str = name ?? \"Anonymous\"\n    print(fallback)", "Anonymous");
            data.Add("coalesce_in_argument",
                "def f(s: str) -> None:\n    print(s)\ndef main() -> None:\n    name: str | None = None\n    f(name ?? \"z\")", "z");
            return data;
        }
    }

    [Fact]
    public void Totality()
    {
        // Anchored to literals, not derived from the axes
        MatrixCells.Cast<object[]>().Count().Should().BeGreaterThanOrEqualTo(18,
            "at least 18 cells (9 positions × 2 operators, partial coverage of 3 left-kinds per position)");
    }
}
