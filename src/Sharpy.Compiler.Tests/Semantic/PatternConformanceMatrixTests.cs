using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Representative pattern conformance matrix covering the key cell groups from the
/// #1670 defect class: ClassifyTypeTestAnnotation routing (2fef25bf2), TypeTestLowering
/// consumption (3e3ba202f), fill-from-subject for closed scrutinees (c648af831), and
/// subsumption recording.
/// </summary>
[Collection("HeavyCompilation")]
public class PatternConformanceMatrixTests : IntegrationTestBase
{
    public PatternConformanceMatrixTests(ITestOutputHelper output) : base(output) { }

    // ── Group 1: Builtin × object subject (erased path) ──

    public static IEnumerable<object[]> BuiltinErasedCells()
    {
        yield return new object[] { "list", "[1, 2]", "erased-list" };
        yield return new object[] { "dict", "{\"a\": 1}", "erased-dict" };
        yield return new object[] { "set", "{1, 2}", "erased-set" };
    }

    [Theory]
    [MemberData(nameof(BuiltinErasedCells))]
    public void BuiltinOnObject_Erased_Runs(string builtinName, string literal, string label)
    {
        var source = $@"
def check(o: object) -> None:
    match o:
        case {builtinName}():
            print(""{label}"")
        case _:
            print(""other"")

def main() -> None:
    check({literal})
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"builtin {builtinName} erased on object should compile and run");
        result.StandardOutput.TrimEnd().Should().Be(label);
    }

    // ── Group 2: Builtin × closed subject (fill path) ──

    public static IEnumerable<object[]> BuiltinFilledCells()
    {
        yield return new object[] { "list", "list[int]", "[1, 2]", "filled-list" };
        yield return new object[] { "dict", "dict[str, int]", "{\"a\": 1}", "filled-dict" };
        yield return new object[] { "set", "set[int]", "{1, 2}", "filled-set" };
    }

    [Theory]
    [MemberData(nameof(BuiltinFilledCells))]
    public void BuiltinOnClosed_Filled_Runs(string builtinName, string closedType, string literal, string label)
    {
        var source = $@"
def check(xs: {closedType}) -> None:
    match xs:
        case {builtinName}():
            print(""{label}"")
        case _:
            print(""other"")

def main() -> None:
    check({literal})
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"builtin {builtinName} filled on {closedType} should compile and run");
        result.StandardOutput.TrimEnd().Should().Be(label);
    }

    // ── Group 3: Cross-collection incompatible (SPY0361) ──

    [Fact]
    public void CrossCollection_ListOnDict_SPY0361()
    {
        const string source = @"
def main() -> None:
    d: dict[str, int] = {""a"": 1}
    match d:
        case list():
            print(""never"")
        case _:
            print(""dict"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse("cross-collection pattern should be refused");
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypePatternIncompatible,
            "cross-collection should produce SPY0361");
    }

    // ── Group 4: Self-matching positional ──

    public static IEnumerable<object[]> SelfMatchingCells()
    {
        yield return new object[] { "int", "42", "int 43", "int(n) binds whole subject" };
        yield return new object[] { "str", "\"hi\"", "str HI", "str(s) binds whole subject" };
    }

    [Theory]
    [MemberData(nameof(SelfMatchingCells))]
    public void SelfMatching_OnObject_Runs(string typeName, string value, string expected, string desc)
    {
        var source = typeName == "int"
            ? $@"
def describe(x: object) -> str:
    match x:
        case int(n):
            m: int = n + 1
            return f""int {{m}}""
        case _:
            return ""other""

def main() -> None:
    print(describe({value}))
"
            : $@"
def describe(x: object) -> str:
    match x:
        case str(s):
            t: str = s.upper()
            return f""str {{t}}""
        case _:
            return ""other""

def main() -> None:
    print(describe({value}))
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{desc} should compile and run");
        result.StandardOutput.TrimEnd().Should().Be(expected);
    }

    // ── Group 5: User generic fill ──

    [Fact]
    public void UserGeneric_BoxOnBoxInt_Filled_Runs()
    {
        const string source = @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

def main() -> None:
    b: Box[int] = Box[int](7)
    match b:
        case Box():
            print(""box filled"")
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue("Box pattern on Box[int] should fill and run");
        result.StandardOutput.TrimEnd().Should().Be("box filled");
    }

    // ── Group 6: Subsumption — total class pattern makes later arm unreachable (SPY0700) ──

    [Fact]
    public void Subsumption_IntThenLiteral_SPY0700()
    {
        const string source = @"
def main() -> None:
    x: int = 42
    match x:
        case int() as n:
            print(n)
        case 99:
            print(""ninety-nine"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse("subsumed arm should be refused");
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.ValidationOverflow.IrrefutablePatternNotLast,
            "subsumption should produce SPY0700");
    }

    // ── Group 7: Normal int positive control ──

    [Fact]
    public void NormalInt_LiteralThenWildcard_Runs()
    {
        const string source = @"
def main() -> None:
    x: int = 99
    match x:
        case 99:
            print(""ninety-nine"")
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue("normal int literal match should compile and run");
        result.StandardOutput.TrimEnd().Should().Be("ninety-nine");
    }

    // ── Group 8: As-pattern capture ──

    [Fact]
    public void AsPatternCapture_StrOnObject_TypedAsStr()
    {
        const string source = @"
def main() -> None:
    x: object = ""hello""
    match x:
        case str() as s:
            t: str = s.upper()
            print(t)
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue("as-pattern capture on str should compile and run");
        result.StandardOutput.TrimEnd().Should().Be("HELLO");
    }
}
