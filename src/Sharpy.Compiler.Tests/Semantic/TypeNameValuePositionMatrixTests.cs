using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1676: a type name in value position is typed as its constructor-reference carrier
/// (<c>SelfConstructionOf</c>/<c>TypeParameterType</c>) at the IDENTIFIER, instead of a standing
/// <c>Unknown</c>+<c>DeliberatelyPermissive</c> mark resolved only at the call site.
/// <see cref="TypeChecker.Expressions.Access.Calls.Overloads.CheckConstructorReference"/> (the
/// #1170/#1211 machinery) runs unchanged on top and re-derives every classification from the
/// SYMBOL, so this file's job is proving every measured position still resolves or refuses
/// exactly as before, plus the one NEW acceptance direction (an ICE becomes a named diagnostic).
///
/// <para>Matrix: position x kind. <c>constructor_reference_tripwire.spy</c> (a whole fixture, not
/// a cell) is the positive control the plan names — every position a type name legitimately
/// occupied before the constructor-reference rules existed, executed here byte-for-byte against
/// its own <c>.expected</c> so a regression fails loudly in this file rather than only in the
/// sixteen-minute corpus sweep.</para>
///
/// <para>Direction record: <c>print(Point)</c> was SPY0908/CS8917 (an untyped lambda into an
/// <c>object</c> slot with no delegate to infer) — now SPY0342, naming the real refusal.
/// <c>print(E)</c> (a class with no explicit constructor) silently printed
/// <c>System.Func`1[T06b+E]</c> (python3: <c>&lt;class '__main__.E'&gt;</c>) — now SPY0342 too,
/// classified wrong-output -> diagnostic.</para>
/// </summary>
public class TypeNameValuePositionMatrixTests : IntegrationTestBase
{
    public TypeNameValuePositionMatrixTests(ITestOutputHelper output) : base(output) { }

    private static bool HasCode(ExecutionResult result, string code)
        => result.RawDiagnostics.Exists(d => d.Code == code);

    private const string PointClass = """
        class Point:
            x: int
            def __init__(self, x: int):
                self.x = x
        """;

    // ═══════════════════════════════════════════════════════════════════════
    // Assignment position (t01): a class name assigned to a variable has no
    // signature to pin — SPY0342, unaffected by #1676 (a pre-existing refusal).
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ClassWithOneConstructor_Assignment_IsSpy0342()
    {
        var result = CompileAndExecute(PointClass + """

            def main() -> None:
                f = Point
                print(f(3).x)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.UnpinnedConstructorReference),
            $"Expected SPY0342, got: {string.Join(", ", result.CompilationErrors)}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // `print(...)` argument / object slot (t06, t06b): the direct-call-argument arm now checks
    // the parameter slot is delegate-shaped (Overloads.cs's IsDelegateShapedOrOpenSlot) before
    // pinning a single-constructor shape — into `object` it refuses instead of reaching codegen
    // as an untyped lambda no non-delegate slot can convert.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ClassWithOneConstructor_PrintArgument_IsSpy0342_NotAnIce()
    {
        // t06 — the issue's own repro. Direction: was SPY0908/CS8917 ("the delegate type could
        // not be inferred"), now a named refusal.
        var result = CompileAndExecute(PointClass + """

            def main() -> None:
                print(Point)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.UnpinnedConstructorReference),
            $"Expected SPY0342, got: {string.Join(", ", result.CompilationErrors)}");
        Assert.False(HasCode(result, DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError),
            "print(Point) must not reach codegen as an untyped lambda into a non-delegate slot (#1676)");
    }

    [Fact]
    public void ClassWithNoExplicitConstructor_PrintArgument_IsSpy0342_NotSilentWrongOutput()
    {
        // t06b — direction: this used to RUN and silently print "System.Func`1[T06b+E]" (python3:
        // "<class '__main__.E'>"). Classified wrong-output -> diagnostic (#1676).
        var result = CompileAndExecute("""
            class E:
                pass

            def main() -> None:
                print(E)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.UnpinnedConstructorReference),
            $"Expected SPY0342, got: {string.Join(", ", result.CompilationErrors)}");
        Assert.DoesNotContain("System.Func", result.StandardOutput);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // No-construction kinds (t02/t03/t08): enum, interface, union — SPY0346, ahead of and
    // distinct from the SPY0342 tier ("this position supplies no signature" presumes the type
    // offers one; these offer none at all).
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Enum_ValueUse_IsSpy0346()
    {
        var result = CompileAndExecute("""
            enum Color:
                Red = 1
                Green = 2

            def main() -> None:
                f = Color
                print(f)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.NonConstructibleTypeReference),
            $"Expected SPY0346, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Interface_ValueUse_IsSpy0346()
    {
        var result = CompileAndExecute("""
            interface Shape:
                def area(self) -> float:
                    ...

            def main() -> None:
                f = Shape
                print(f)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.NonConstructibleTypeReference),
            $"Expected SPY0346, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Union_ValueUse_IsSpy0346()
    {
        var result = CompileAndExecute("""
            union Shape:
                case Circle(radius: int)
                case Square(side: int)

            def main() -> None:
                f = Shape
                print(f)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.NonConstructibleTypeReference),
            $"Expected SPY0346, got: {string.Join(", ", result.CompilationErrors)}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Positive controls: positions that legitimately run and must keep running.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void DirectConstruction_StillRuns()
    {
        // t16
        var result = CompileAndExecute(PointClass + """

            def main() -> None:
                print(Point(3).x)
            """);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("3\n", result.StandardOutput);
    }

    [Fact]
    public void MapWithUserClassConstructorReference_StillRuns()
    {
        // t17 — #1211 capability: a delegate-shaped slot (map's `(T) -> R`) still pins.
        var result = CompileAndExecute(PointClass + """

            def main() -> None:
                xs = [3, 4]
                ys = list(map(Point, xs))
                print(len(ys))
            """);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("2\n", result.StandardOutput);
    }

    [Fact]
    public void IsinstanceTypeTestArgument_StillRuns()
    {
        var result = CompileAndExecute(PointClass + """

            def main() -> None:
                p = Point(3)
                print(isinstance(p, Point))
            """);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("True\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_DirectConstruction_StillRuns()
    {
        var result = CompileAndExecute("""
            class Box[T]:
                v: T
                def __init__(self, v: T):
                    self.v = v

            def main() -> None:
                b = Box(3)
                print(b.v)
            """);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("3\n", result.StandardOutput);
    }

    /// <summary>
    /// The regression this matrix exists to pin: a NESTED generic type's qualifier
    /// (<c>Outer.Inner[int](6)</c>, #1211) must keep denoting "a type", never "an instance
    /// receiver". <see cref="TypeChecker.CheckIdentifier"/>'s #1676 arm excludes the
    /// member-access-qualifier position from SelfConstructionOf for exactly this reason — found
    /// during verification, not anticipated by the plan: giving the qualifier a real
    /// (non-Unknown) type made GenericReferenceResolver's `ownerType is not UnknownType` branch
    /// treat <c>Outer</c> as an instance receiver, reporting the false "Type 'Outer' has no
    /// member 'Inner'".
    /// </summary>
    [Fact]
    public void NestedGenericTypeQualifier_StillResolvesAsAType_NotAnInstanceReceiver()
    {
        var result = CompileAndExecute("""
            class Outer:
                @public
                class Inner[T]:
                    v: T
                    def __init__(self, v: T):
                        self.v = v

            def main() -> None:
                print(Outer.Inner[int](6).v)
            """);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("6\n", result.StandardOutput);
    }

    /// <summary>
    /// The plan's named positive control: every position a type name legitimately occupied
    /// before the constructor-reference rules existed (#1182, #1211), executed byte-for-byte
    /// against the fixture's own <c>.expected</c>. A regression anywhere in this file's blast
    /// radius fails here in seconds instead of only in the corpus sweep.
    /// </summary>
    [Fact]
    public void ConstructorReferenceTripwire_MatchesExpectedOutput()
    {
        var result = CompileAndExecute("""
            class Box[T]:
                value: T

                def __init__(self, value: T):
                    self.value = value


            enum Suit:
                HEARTS = 1
                SPADES = 2


            class Point:
                x: int = 0

                # No `self`, so this is a static member reached through the class name.
                def of(v: int) -> Point:
                    p = Point()
                    p.x = v
                    return p


            class Outer:
                @public
                class Inner[T]:
                    v: T

                    def __init__(self, v: T):
                        self.v = v


            def main():
                xs: list[str] = ["3", "1", "2"]

                print(list(map(int, xs)))
                print(sorted(xs, key=int))

                parsed: int !ValueError = int.parse("7")
                print(parsed.unwrap())

                fk = dict.fromkeys(["a", "b"], 0)
                print(fk)

                x: object = 5
                print(isinstance(x, int))

                b: Box[int] = Box[int](4)
                print(b.value)

                print(list(map(str, [1, 2])))
                print(list(filter(bool, [0, 1, 0, 2])))

                print(list(map(int, xs)))

                print(Point.of(5).x)

                o: object = Point()
                print(isinstance(o, Point))

                bp: Box[Point] = Box[Point](Point())
                print(bp.value.x)

                print(Outer.Inner[int](6).v)
                print(Outer.Inner[Point](Point()).v.x)

                for suit in Suit:
                    print(suit.name)

                print([suit.name for suit in Suit])
            """);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal(
            "[3, 1, 2]\n['1', '2', '3']\n7\n{'a': 0, 'b': 0}\nTrue\n4\n['1', '2']\n[1, 2]\n"
            + "[3, 1, 2]\n5\nTrue\n0\n6\n0\nHEARTS\nSPADES\n['HEARTS', 'SPADES']\n",
            result.StandardOutput);
    }
}
