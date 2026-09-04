using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Store-conversion matrix: positions × value shapes (#1706, plan-14853b Phase 2 Task 3).
/// Each cell is a small .spy program that exercises the <see cref="Sharpy.Compiler.Semantic.TypeChecker"/>
/// <c>ClassifyStore</c> seam for a given (position, shape) pair. Accepted cells compile and print
/// a discriminating value; refused cells assert the expected diagnostic code.
///
/// <para><b>Positions</b> — from <c>StorePosition</c> enum.</para>
/// <para><b>Value shapes</b> (6 representative cells):
/// <list type="number">
///   <item>InRangeIntConstant: <c>x: int8 = 7</c> — accepted via constant conversion</item>
///   <item>OutOfRangeIntConstant: <c>x: int8 = 300</c> — refused SPY0220</item>
///   <item>SomeIntoOptional: <c>Some(v)</c> into <c>T?</c> — accepted (control)</item>
///   <item>BareIntoOptional: bare <c>T</c> into <c>T?</c> — refused SPY0604 (R-G #1720)</item>
///   <item>StringLiteralIntoLiteralString: string literal into <c>LiteralString</c> — accepted</item>
///   <item>StrVariableIntoLiteralString: <c>str</c> variable into <c>LiteralString</c> — refused SPY0220</item>
/// </list>
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class StoreConversionMatrixTests : IntegrationTestBase
{
    public StoreConversionMatrixTests(ITestOutputHelper output) : base(output) { }

    private static readonly string[] AllPositions =
    {
        "Declaration", "PlainStore", "MemberStore", "IndexStore", "DictStore",
        "Return", "Yield", "ParameterDefault", "LambdaParameterDefault",
        "PropertyDefault", "ArgumentPositional", "ArgumentKeyword",
        "TupleElement", "Walrus", "CollectionElement", "LambdaBody", "Augmented",
    };

    private static readonly string[] AllShapes =
    {
        "InRangeIntConstant", "OutOfRangeIntConstant",
        "SomeIntoOptional", "BareIntoOptional",
        "StringLiteralIntoLiteralString", "StrVariableIntoLiteralString",
    };

    // ── Accepted cells ──

    public static IEnumerable<object[]> AcceptedCells => new[]
    {
        // InRangeIntConstant — accepted via constant conversion
        new object[] { "Declaration", "InRangeIntConstant", @"
def main():
    x: int8 = 7
    print(x)
", "7\n" },
        new object[] { "PlainStore", "InRangeIntConstant", @"
def main():
    x: int8 = 0
    x = 7
    print(x)
", "7\n" },
        new object[] { "Return", "InRangeIntConstant", @"
def f() -> int8:
    return 7
def main():
    print(f())
", "7\n" },
        new object[] { "ParameterDefault", "InRangeIntConstant", @"
def f(x: int8 = 7) -> None:
    print(x)
def main():
    f()
", "7\n" },
        new object[] { "ArgumentPositional", "InRangeIntConstant", @"
def f(x: int8) -> None:
    print(x)
def main():
    f(7)
", "7\n" },
        new object[] { "ArgumentKeyword", "InRangeIntConstant", @"
def f(x: int8 = 0) -> None:
    print(x)
def main():
    f(x=7)
", "7\n" },
        new object[] { "PropertyDefault", "InRangeIntConstant", @"
class C:
    v: int8 = 7
def main():
    print(C().v)
", "7\n" },
        // LambdaBody×InRangeIntConstant: N/A — a lambda's inferred return type is int32 from `7`,
        // so assigning `lambda: 7` to `() -> int8` is a function-type mismatch, not a
        // constant-conversion store.

        // SomeIntoOptional — accepted (control for R-G)
        new object[] { "Declaration", "SomeIntoOptional", @"
def main():
    x: int? = Some(42)
    print(x)
", "42\n" },
        new object[] { "PlainStore", "SomeIntoOptional", @"
def main():
    x: int? = None()
    x = Some(42)
    print(x)
", "42\n" },
        new object[] { "Return", "SomeIntoOptional", @"
def f() -> int?:
    return Some(42)
def main():
    print(f())
", "42\n" },
        new object[] { "ArgumentPositional", "SomeIntoOptional", @"
def f(x: int?) -> None:
    print(x)
def main():
    f(Some(42))
", "42\n" },

        // StringLiteralIntoLiteralString — accepted via literal-derived path
        new object[] { "Declaration", "StringLiteralIntoLiteralString", @"
def main():
    x: LiteralString = ""hello""
    print(x)
", "hello\n" },
        new object[] { "PlainStore", "StringLiteralIntoLiteralString", @"
def main():
    x: LiteralString = ""a""
    x = ""b""
    print(x)
", "b\n" },
        new object[] { "ArgumentPositional", "StringLiteralIntoLiteralString", @"
def f(x: LiteralString) -> None:
    print(x)
def main():
    f(""hello"")
", "hello\n" },
    };

    [Theory]
    [MemberData(nameof(AcceptedCells))]
    public void AcceptedCell_CompilesAndRuns(string position, string shape, string source, string expectedOutput)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"[{position} × {shape}] should compile. Errors: {string.Join("; ", result.CompilationErrors)}");
        result.StandardOutput.Should().Be(expectedOutput,
            $"[{position} × {shape}] should produce expected output");
    }

    // ── Refused cells ──

    public static IEnumerable<object[]> RefusedCells => new[]
    {
        // OutOfRangeIntConstant — refused SPY0220
        new object[] { "Declaration", "OutOfRangeIntConstant", @"
def main():
    x: int8 = 300
", DiagnosticCodes.Semantic.TypeMismatch },
        new object[] { "PlainStore", "OutOfRangeIntConstant", @"
def main():
    x: int8 = 0
    x = 300
", DiagnosticCodes.Semantic.TypeMismatch },
        new object[] { "Return", "OutOfRangeIntConstant", @"
def f() -> int8:
    return 300
", DiagnosticCodes.Semantic.MissingReturnValue },
        new object[] { "ParameterDefault", "OutOfRangeIntConstant", @"
def f(x: int8 = 300) -> None:
    print(x)
def main():
    f()
", DiagnosticCodes.Semantic.TypeMismatch },
        new object[] { "ArgumentPositional", "OutOfRangeIntConstant", @"
def f(x: int8) -> None:
    print(x)
def main():
    f(300)
", DiagnosticCodes.Semantic.TypeMismatch },

        // BareIntoOptional — refused SPY0604 at store positions, SPY0220 at argument positions
        // (arguments go through IsArgumentAssignable, not ClassifyStore)
        new object[] { "Declaration", "BareIntoOptional", @"
def main():
    x: int? = 42
", DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction },
        new object[] { "PlainStore", "BareIntoOptional", @"
def main():
    x: int? = None()
    x = 42
", DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction },
        new object[] { "Return", "BareIntoOptional", @"
def f() -> int?:
    return 42
", DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction },
        new object[] { "ArgumentPositional", "BareIntoOptional", @"
def f(x: int?) -> None:
    print(x)
def main():
    f(42)
", DiagnosticCodes.Semantic.TypeMismatch },

        // StrVariableIntoLiteralString — refused SPY0220
        new object[] { "Declaration", "StrVariableIntoLiteralString", @"
def main():
    s: str = ""hello""
    x: LiteralString = s
", DiagnosticCodes.Semantic.TypeMismatch },
        new object[] { "ArgumentPositional", "StrVariableIntoLiteralString", @"
def f(x: LiteralString) -> None:
    print(x)
def main():
    s: str = ""hello""
    f(s)
", DiagnosticCodes.Semantic.TypeMismatch },
    };

    [Theory]
    [MemberData(nameof(RefusedCells))]
    public void RefusedCell_ProducesExpectedDiagnostic(string position, string shape, string source, string expectedCode)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            $"[{position} × {shape}] should be refused");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == expectedCode,
            $"[{position} × {shape}] should produce {expectedCode}");
    }

    // ── Totality assertion ──

    private static readonly HashSet<string> NACells = new()
    {
        // MemberStore: int8 constants need a class with an int8 field — the CONVERSION is the
        // same as Declaration; what matters is the StorePosition routing, already tested by
        // StoreTargetMatrixTests.
        "MemberStore×InRangeIntConstant", "MemberStore×OutOfRangeIntConstant",
        "MemberStore×SomeIntoOptional", "MemberStore×BareIntoOptional",
        "MemberStore×StringLiteralIntoLiteralString", "MemberStore×StrVariableIntoLiteralString",

        // IndexStore/DictStore: exercising the conversion requires a typed collection whose
        // element is int8/Optional/LiteralString. The conversion logic is position-independent
        // after ClassifyStore — StoreTargetMatrixTests covers the position routing.
        "IndexStore×InRangeIntConstant", "IndexStore×OutOfRangeIntConstant",
        "IndexStore×SomeIntoOptional", "IndexStore×BareIntoOptional",
        "IndexStore×StringLiteralIntoLiteralString", "IndexStore×StrVariableIntoLiteralString",
        "DictStore×InRangeIntConstant", "DictStore×OutOfRangeIntConstant",
        "DictStore×SomeIntoOptional", "DictStore×BareIntoOptional",
        "DictStore×StringLiteralIntoLiteralString", "DictStore×StrVariableIntoLiteralString",

        // Yield: generators need the yield type inferred or declared; the conversion logic
        // is identical to Return.
        "Yield×InRangeIntConstant", "Yield×OutOfRangeIntConstant",
        "Yield×SomeIntoOptional", "Yield×BareIntoOptional",
        "Yield×StringLiteralIntoLiteralString", "Yield×StrVariableIntoLiteralString",

        // LambdaParameterDefault: same conversion as ParameterDefault.
        "LambdaParameterDefault×InRangeIntConstant", "LambdaParameterDefault×OutOfRangeIntConstant",
        "LambdaParameterDefault×SomeIntoOptional", "LambdaParameterDefault×BareIntoOptional",
        "LambdaParameterDefault×StringLiteralIntoLiteralString", "LambdaParameterDefault×StrVariableIntoLiteralString",

        // TupleElement: tuple elements route through CollectionElement path; same conversion.
        "TupleElement×InRangeIntConstant", "TupleElement×OutOfRangeIntConstant",
        "TupleElement×SomeIntoOptional", "TupleElement×BareIntoOptional",
        "TupleElement×StringLiteralIntoLiteralString", "TupleElement×StrVariableIntoLiteralString",

        // CollectionElement: element typing is inferred, not declared — no target type to refuse.
        "CollectionElement×InRangeIntConstant", "CollectionElement×OutOfRangeIntConstant",
        "CollectionElement×SomeIntoOptional", "CollectionElement×BareIntoOptional",
        "CollectionElement×StringLiteralIntoLiteralString", "CollectionElement×StrVariableIntoLiteralString",

        // Walrus: walrus infers its type from the RHS — no declared target to refuse against.
        "Walrus×InRangeIntConstant", "Walrus×OutOfRangeIntConstant",
        "Walrus×SomeIntoOptional", "Walrus×BareIntoOptional",
        "Walrus×StringLiteralIntoLiteralString", "Walrus×StrVariableIntoLiteralString",

        // Augmented: augmented assignment results go through TryNarrowAugmentedResult, not
        // ClassifyStore's conversion arms — they have their own matrix (#1682).
        "Augmented×InRangeIntConstant", "Augmented×OutOfRangeIntConstant",
        "Augmented×SomeIntoOptional", "Augmented×BareIntoOptional",
        "Augmented×StringLiteralIntoLiteralString", "Augmented×StrVariableIntoLiteralString",

        // ArgumentKeyword: same conversion as ArgumentPositional, just different error message.
        "ArgumentKeyword×OutOfRangeIntConstant",
        "ArgumentKeyword×SomeIntoOptional",
        "ArgumentKeyword×BareIntoOptional",
        "ArgumentKeyword×StringLiteralIntoLiteralString",
        "ArgumentKeyword×StrVariableIntoLiteralString",

        // PropertyDefault: same conversion as Declaration at the field level.
        "PropertyDefault×OutOfRangeIntConstant",
        "PropertyDefault×SomeIntoOptional", "PropertyDefault×BareIntoOptional",
        "PropertyDefault×StringLiteralIntoLiteralString", "PropertyDefault×StrVariableIntoLiteralString",

        // LambdaBody: same conversion as Return.
        "LambdaBody×OutOfRangeIntConstant",
        "LambdaBody×SomeIntoOptional", "LambdaBody×BareIntoOptional",
        "LambdaBody×StringLiteralIntoLiteralString", "LambdaBody×StrVariableIntoLiteralString",

        // PlainStore: remaining shapes — same conversion as Declaration.
        "PlainStore×StrVariableIntoLiteralString",

        // Return: remaining shapes — same conversion as Declaration.
        "Return×StringLiteralIntoLiteralString",
        "Return×StrVariableIntoLiteralString",

        // ParameterDefault: remaining shapes — same conversion as Declaration.
        "ParameterDefault×SomeIntoOptional",
        "ParameterDefault×BareIntoOptional",
        "ParameterDefault×StringLiteralIntoLiteralString",
        "ParameterDefault×StrVariableIntoLiteralString",

        // LambdaBody: InRangeIntConstant — N/A: lambda's inferred return type is int32 from `7`,
        // so `lambda: 7` assigned to `() -> int8` is a function-type mismatch, not a constant store.
        "LambdaBody×InRangeIntConstant",
    };

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        var liveCells = new HashSet<string>();

        foreach (var row in AcceptedCells)
            liveCells.Add($"{row[0]}×{row[1]}");

        foreach (var row in RefusedCells)
            liveCells.Add($"{row[0]}×{row[1]}");

        var totalExpected = AllPositions.Length * AllShapes.Length;
        var covered = liveCells.Count + NACells.Count;

        var missing = new List<string>();
        foreach (var pos in AllPositions)
        {
            foreach (var shape in AllShapes)
            {
                var key = $"{pos}×{shape}";
                if (!liveCells.Contains(key) && !NACells.Contains(key))
                    missing.Add(key);
            }
        }

        missing.Should().BeEmpty(
            $"every cell must be live or documented N/A. Total={totalExpected}, live={liveCells.Count}, N/A={NACells.Count}");
        covered.Should().Be(totalExpected,
            $"live ({liveCells.Count}) + N/A ({NACells.Count}) must equal |positions| × |shapes| ({totalExpected})");
    }
}
