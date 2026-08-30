using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Pattern conformance matrix for the #1670 defect class: ClassifyTypeTestAnnotation routing
/// (2fef25bf2), TypeTestLowering consumption (3e3ba202f), fill-from-subject for closed scrutinees
/// (c648af831), and subsumption recording.
/// <para>
/// <b>Groups 9–13 are the POSITIONAL-CAPTURE arm</b> — <c>case list(xs):</c>, the spelling that had
/// no test and no use anywhere in the repository. Its cells are total over
/// builtin {list, dict, set} × subject {object, matching closed generic, non-matching closed
/// generic, impossible} × position {top level, nested in a sequence pattern, nested in a class
/// positional pattern}, because the arm is one rule and a position must not be able to opt out of
/// it. Every runnable cell asserts stdout against python3 3.12 (quoted per group); every refused
/// cell asserts the diagnostic code.
/// </para>
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

    // ── Group 9: positional capture — builtin × subject × position (36 cells) ──
    //
    // python3 3.12, one runnable cell per position (list shown; dict/set are identical):
    //   >>> def check(o):
    //   ...     match o:
    //   ...         case list(v): print("hit")
    //   ...         case _:       print("miss")
    //   >>> check([1, 2])
    //   hit
    //   >>> def seq(xs):
    //   ...     match xs:
    //   ...         case [list(v)]: print("hit")
    //   ...         case _:         print("miss")
    //   >>> seq([[1, 2]])
    //   hit
    //   >>> class Box:
    //   ...     __match_args__ = ("value",)
    //   ...     def __init__(self, value): self.value = value
    //   >>> def cls(b):
    //   ...     match b:
    //   ...         case Box(list(v)): print("hit")
    //   ...         case _:            print("miss")
    //   >>> cls(Box([1, 2]))
    //   hit
    //
    // The two static-impossibility columns are where Sharpy departs from python3 ON PURPOSE
    // (owner ruling Q1, #1670): python3 answers `miss` at run time, Sharpy refuses the arm with
    // SPY0361 because a `str` — or a `dict[str, int]` — can never be a list, so the arm is dead
    // code, not a run-time outcome. `case list(xs):` had ZERO uses in the repository before this
    // matrix, which is how it shipped emitting a test against `Sharpy.List<object>` (the erased
    // CAPTURE type) and silently taking the `_` arm on every object subject.

    private static readonly Dictionary<string, (string Closed, string Literal)> BuiltinSubjects =
        new()
        {
            ["list"] = ("list[int]", "[1, 2]"),
            ["dict"] = ("dict[str, int]", "{\"a\": 1}"),
            ["set"] = ("set[int]", "{1, 2}"),
        };

    public static IEnumerable<object[]> PositionalCaptureCells()
    {
        foreach (var builtin in new[] { "list", "dict", "set" })
        {
            foreach (var subjectKind in new[] { "object", "closed", "nonmatching", "impossible" })
            {
                foreach (var position in new[] { "top", "sequence", "class" })
                {
                    yield return new object[] { builtin, subjectKind, position };
                }
            }
        }
    }

    private static (string Type, string Literal) SubjectFor(string builtin, string subjectKind)
        => subjectKind switch
        {
            "object" => ("object", BuiltinSubjects[builtin].Literal),
            "closed" => BuiltinSubjects[builtin],
            // A closed generic of a DIFFERENT collection: statically impossible, and the cell that
            // reached the C# compiler as CS8121 ("List<int> cannot be handled by Dict<object,object>").
            "nonmatching" => builtin == "list" ? BuiltinSubjects["dict"] : BuiltinSubjects["list"],
            "impossible" => ("str", "\"hi\""),
            _ => throw new ArgumentOutOfRangeException(nameof(subjectKind), subjectKind, null),
        };

    private static string PositionalCaptureSource(
        string builtin, string subjectType, string literal, string position)
    {
        var template = position switch
        {
            "top" => @"
def check(o: @SUBJ@) -> None:
    match o:
        case @B@(v):
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    x: @SUBJ@ = @LIT@
    check(x)
",
            "sequence" => @"
def check(xs: list[@SUBJ@]) -> None:
    match xs:
        case [@B@(v)]:
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    x: list[@SUBJ@] = [@LIT@]
    check(x)
",
            "class" => @"
class Box:
    value: @SUBJ@

    def __init__(self, value: @SUBJ@):
        self.value = value

def check(b: Box) -> None:
    match b:
        case Box(@B@(v)):
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    x: @SUBJ@ = @LIT@
    check(Box(x))
",
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
        };

        return template
            .Replace("@SUBJ@", subjectType, StringComparison.Ordinal)
            .Replace("@LIT@", literal, StringComparison.Ordinal)
            .Replace("@B@", builtin, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(PositionalCaptureCells))]
    public void PositionalCapture_BuiltinBySubjectByPosition(
        string builtin, string subjectKind, string position)
    {
        var (subjectType, literal) = SubjectFor(builtin, subjectKind);
        var source = PositionalCaptureSource(builtin, subjectType, literal, position);
        var result = CompileAndExecute(source);

        var staticallyImpossible = subjectKind is "nonmatching" or "impossible";
        if (staticallyImpossible)
        {
            result.Success.Should().BeFalse(
                $"`case {builtin}(v):` cannot match a '{subjectType}' subject at the {position} "
                + $"position, so the arm is dead code. Output was: {result.StandardOutput}");
            result.RawDiagnostics.Should().Contain(
                d => d.Code == DiagnosticCodes.Semantic.TypePatternIncompatible,
                $"an impossible class pattern is refused with SPY0361, not left to CS8121 behind "
                + $"SPY0908. Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        }
        else
        {
            result.Success.Should().BeTrue(
                $"`case {builtin}(v):` on a '{subjectType}' subject at the {position} position must "
                + $"compile. Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
            result.StandardOutput.TrimEnd().Should().Be(
                "hit",
                $"`case {builtin}(v):` must match a real {builtin} at the {position} position — "
                + "python3 3.12 prints `hit` for every one of these cells");
        }
    }

    // ── Group 9b: the two-element nested sequence, the shape #1670 names ──

    [Fact]
    public void PositionalCapture_NestedSequenceTwoElements_Runs()
    {
        // python3 3.12:
        //   >>> def d(o):
        //   ...     match o:
        //   ...         case [list(inner), int(n)]: print('seq')
        //   ...         case _:                     print('other')
        //   >>> d([[1], 2])
        //   seq
        const string source = @"
def check(o: list[object]) -> None:
    match o:
        case [list(inner), int(n)]:
            print(""seq"")
        case _:
            print(""other"")

def main() -> None:
    xs: list[object] = [[1], 2]
    check(xs)
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"nested positional captures compile. Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        result.StandardOutput.TrimEnd().Should().Be("seq", "python3 3.12 prints `seq`");
    }

    // ── Group 10: refusals that stay refusals ──

    public static IEnumerable<object[]> RefusedClassPatternCells()
    {
        // `bytearray` and `range` are self-matching NAMES with no registered type: the arm used to
        // fall back to the scrutinee's own type, which made `case bytearray(v):` an irrefutable
        // `case object v:` that matched the int 1 and printed "bytearray" (python3 prints "other").
        yield return new object[] { "bytearray(v)", DiagnosticCodes.Semantic.UndefinedType };
        yield return new object[] { "range(v)", DiagnosticCodes.Semantic.UndefinedType };
        // tuple/frozenset ARE generic types with no erasure interface in Sharpy.Core, so nothing
        // determines their arguments and nothing can be tested — the honest refusal, not a silently
        // wrong 0-tuple test (which is what the emitter produced before #1670's checker half).
        yield return new object[] { "tuple(v)", DiagnosticCodes.Semantic.OpenGenericTypeTest };
        yield return new object[] { "frozenset(v)", DiagnosticCodes.Semantic.OpenGenericTypeTest };
        // A pattern cannot name type arguments (parser rule, until #1619).
        yield return new object[] { "list[int](v)", DiagnosticCodes.Parser.GenericTypeInPattern };
    }

    [Theory]
    [MemberData(nameof(RefusedClassPatternCells))]
    public void RefusedClassPattern_ReportsItsCode(string patternText, string expectedCode)
    {
        var source = @"
def check(o: object) -> None:
    match o:
        case @P@:
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    check(1)
".Replace("@P@", patternText, StringComparison.Ordinal);

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(
            $"`case {patternText}:` names no testable type. Output was: {result.StandardOutput}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == expectedCode,
            $"`case {patternText}:` is refused with {expectedCode}. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
    }

    // ── Group 11: what the capture is TYPED as (the annotated destination is the probe) ──

    public static IEnumerable<object[]> CaptureTypingCells()
    {
        // `object` subject: the test erases to Sharpy.IList, so the capture surface is list[object].
        yield return new object[] { "object", "[1, 2]", "list[object]" };
        // Closed subject: the vector is filled FROM THE SUBJECT, so the capture keeps its elements.
        yield return new object[] { "list[int]", "[1, 2]", "list[int32]" };
    }

    [Theory]
    [MemberData(nameof(CaptureTypingCells))]
    public void PositionalCapture_TypedAs(string subjectType, string literal, string expectedTypeName)
    {
        var source = @"
def check(o: @SUBJ@) -> None:
    match o:
        case list(xs):
            b: bool = xs
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    x: @SUBJ@ = @LIT@
    check(x)
".Replace("@SUBJ@", subjectType, StringComparison.Ordinal)
 .Replace("@LIT@", literal, StringComparison.Ordinal);

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse("`b: bool = xs` is the type probe and must not compile");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch
                && d.Message.Contains(expectedTypeName, StringComparison.Ordinal),
            $"the capture of `case list(xs):` on a '{subjectType}' subject is typed "
            + $"'{expectedTypeName}'. Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
    }

    // ── Group 12: subsumption — an earlier arm that matches every value of its type ──

    public static IEnumerable<object[]> SubsumingEarlierArmCells()
    {
        yield return new object[] { "int()" };
        yield return new object[] { "int(n)" };
        yield return new object[] { "int() as n" };
    }

    [Theory]
    [MemberData(nameof(SubsumingEarlierArmCells))]
    public void Subsumption_EarlierArmCoversLaterLiteral_SPY0700(string earlierPattern)
    {
        var source = @"
def check(o: object) -> None:
    match o:
        case @EARLIER@:
            print(""int"")
        case 99:
            print(""ninety-nine"")
        case _:
            print(""other"")

def main() -> None:
    check(1)
".Replace("@EARLIER@", earlierPattern, StringComparison.Ordinal);

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(
            $"`case {earlierPattern}:` matches every int, so `case 99:` is unreachable. "
            + $"Output was: {result.StandardOutput}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.ValidationOverflow.IrrefutablePatternNotLast,
            $"the subsumed arm is SPY0700, not CS8120 behind SPY0908. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
    }

    public static IEnumerable<object[]> SubsumptionPositiveControlCells()
    {
        // A literal first: it refutes on a VALUE, so the type arm behind it is still reachable.
        yield return new object[]
        {
            "case 99:\n            print(\"ninety-nine\")\n        case int():\n            print(\"int\")",
            "int",
        };
        // A GUARDED total arm decides nothing statically, so the arm behind it stays reachable (R1′).
        yield return new object[]
        {
            "case int() if always():\n            print(\"int\")\n        case 99:\n            print(\"ninety-nine\")",
            "int",
        };
        // Different runtime types: `case float():` does not match a boxed int, even though int is
        // implicitly convertible to float. python3 3.12 prints `one` here too.
        yield return new object[]
        {
            "case float():\n            print(\"float\")\n        case 1:\n            print(\"one\")",
            "one",
        };
    }

    [Theory]
    [MemberData(nameof(SubsumptionPositiveControlCells))]
    public void Subsumption_PositiveControls_Run(string arms, string expected)
    {
        var source = @"
def always() -> bool:
    return True

def check(o: object) -> None:
    match o:
        @ARMS@
        case _:
            print(""other"")

def main() -> None:
    check(1)
".Replace("@ARMS@", arms, StringComparison.Ordinal);

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            "this arm order is reachable and must NOT be refused — the subsumption rule is only "
            + $"falsifiable if it lets these through. Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        result.StandardOutput.TrimEnd().Should().Be(expected);
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
