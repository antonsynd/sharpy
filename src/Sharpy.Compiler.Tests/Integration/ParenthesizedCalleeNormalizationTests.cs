using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// The paren-normalization contract (#1170): redundant parentheses around a callee never change
/// what a call denotes. Every shape that the TypeChecker dispatches on the callee's surface syntax
/// — construction, overload resolution, special forms, generic references, protocol lowering — must
/// behave identically wrapped and unwrapped. Before the fix these programs either mis-resolved
/// (SPY0224/SPY0220 against an arbitrary overload) or emitted C# that would not bind (SPY0908).
/// </summary>
[Collection("HeavyCompilation")]
public class ParenthesizedCalleeNormalizationTests : IntegrationTestBase
{
    public ParenthesizedCalleeNormalizationTests(ITestOutputHelper output) : base(output)
    {
    }

    private void AssertOutput(string source, string expected)
    {
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal(expected, result.StandardOutput);
    }

    [Fact]
    public void ParenthesizedMethodCallee_ResolvesTheArgumentMatchingOverload()
    {
        // list.pop has a zero-argument and a one-argument overload; the wrapped callee used to bind
        // the zero-argument one and then reject the call.
        AssertOutput(@"
def main() -> None:
    xs: list[int] = [1, 2, 3]
    print((xs.pop)(0))
    print(xs)
", "1\n[2, 3]\n");
    }

    [Fact]
    public void ParenthesizedBuiltinCallee_ResolvesTheSetOverloadOfSorted()
    {
        AssertOutput(@"
def main() -> None:
    a_set: set[int] = {3, 1, 2}
    print((sorted)(a_set))
", "[1, 2, 3]\n");
    }

    [Fact]
    public void ParenthesizedCollectionConstructors_ConstructWithTheirTypeArguments()
    {
        AssertOutput(@"
def main() -> None:
    d: dict[str, int] = (dict)()
    d[""k""] = 1
    xs: list[int] = (list)()
    xs.append(5)
    s: set[int] = (set)()
    s.add(9)
    print(d[""k""])
    print(xs[0])
    print(len(s))
", "1\n5\n1\n");
    }

    [Fact]
    public void ParenthesizedConstructorCallee_ConstructsAnEventBearingClass()
    {
        AssertOutput(@"
delegate Cb(v: int) -> None

class Box:
    event on_change: Cb

    def __init__(self):
        pass

    def fire(self, v: int) -> None:
        self.on_change?.invoke(v)

def log(v: int) -> None:
    print(v)

def main() -> None:
    b = (Box)()
    b.on_change += log
    b.fire(7)
", "7\n");
    }

    [Fact]
    public void ParenthesizedGenericCallee_KeepsItsTypeArguments()
    {
        AssertOutput(@"
def identity[T](x: T) -> T:
    return x

def main() -> None:
    print((identity[int])(5))
", "5\n");
    }

    [Fact]
    public void ParenthesizedUnionVariantCallee_ConstructsTheVariant()
    {
        AssertOutput(@"
union Shape:
    case Circle(r: float)
    case Square(s: float)

def describe(sh: Shape) -> str:
    match sh:
        case Circle(r):
            return ""circle""
        case Square(s):
            return ""square""

def main() -> None:
    print(describe((Shape.Circle)(5.0)))
    print(describe((Shape.Square)(2.0)))
", "circle\nsquare\n");
    }

    [Fact]
    public void NestedParenthesesAroundCallee_Unwrap()
    {
        AssertOutput(@"
def double(x: int) -> int:
    return x * 2

def main() -> None:
    print(((double))(4))
", "8\n");
    }

    [Fact]
    public void ParenthesizedCallExpression_StaysACallThroughACallableValue()
    {
        // Normalization strips parentheses only: it must not look through a call, so this remains
        // an ordinary invocation of the returned callable rather than a resolved direct target.
        AssertOutput(@"
def double(x: int) -> int:
    return x * 2

def get_fn() -> (int) -> int:
    return double

def main() -> None:
    print((get_fn())(5))
", "10\n");
    }

    [Fact]
    public void ParenthesizedLambdaCallee_StaysAnImmediatelyInvokedLambda()
    {
        AssertOutput(@"
def main() -> None:
    print((lambda x: x + 1)(4))
", "5\n");
    }

    [Fact]
    public void ParenthesizedPlaceholderTarget_InfersItsParameterTypes()
    {
        AssertOutput(@"
def add(a: int, b: int) -> int:
    return a + b

def main() -> None:
    add_five = (add)(5, _)
    print(add_five(3))
", "8\n");
    }

    [Fact]
    public void ParenthesizedDunderCallee_IsStillAnImmediateCall()
    {
        // SPY0429 rejects capturing a dunder reference; the parentheses used to hide the fact that
        // this one is invoked immediately.
        AssertOutput(@"
class Ordered:
    value: int

    def __init__(self, value: int):
        self.value = value

    def __eq__(self, other: object) -> bool:
        return False

    def __hash__(self) -> int:
        return self.value

    def __lt__(self, other: Ordered) -> bool:
        return self.value < other.value

    def __gt__(self, other: Ordered) -> bool:
        return self.value > other.value

    def __le__(self, other: Ordered) -> bool:
        return (self.__lt__)(other) or (self.__eq__)(other)

    def __ge__(self, other: Ordered) -> bool:
        return (self.__gt__)(other) or (self.__eq__)(other)

def main():
    print(Ordered(1) <= Ordered(2))
    print(Ordered(2) <= Ordered(1))
", "True\nFalse\n");
    }

    [Fact]
    public void CapturedDunderReference_IsStillRejected()
    {
        // The negative half of the contract: normalization must not weaken SPY0429 for a genuine
        // capture.
        var result = CompileAndExecute(@"
class Box:
    n: int

    def __init__(self, n: int):
        self.n = n

    def __len__(self) -> int:
        return self.n

    def bad(self) -> int:
        f = self.__len__
        return f()

def main() -> None:
    print(Box(4).bad())
");

        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors, e => e.Contains("Cannot capture dunder method reference"));
    }

    [Fact]
    public void ParenthesizedSelfInitCallee_LowersToConstructorDelegation()
    {
        AssertOutput(@"
class Point:
    x: int
    y: int
    z: int

    def __init__(self, x: int, y: int, z: int):
        self.x = x
        self.y = y
        self.z = z

    def __init__(self, x: int, y: int):
        (self.__init__)(x, y, 0)

def main():
    p = Point(4, 5)
    print(p.x, p.y, p.z)
", "4 5 0\n");
    }

    [Fact]
    public void ParenthesizedSuperInitCallee_LowersToBaseConstructorCall()
    {
        AssertOutput(@"
class Base:
    v: int

    def __init__(self, v: int):
        self.v = v

class Derived(Base):
    w: int

    def __init__(self, v: int):
        (super().__init__)(v)
        self.w = v * 2

def main() -> None:
    d = Derived(3)
    print(d.v)
    print(d.w)
", "3\n6\n");
    }

    [Fact]
    public void ParenthesizedPipeTarget_ResolvesTheUserFunction()
    {
        // A pipe target is a callee too. `(triple)` used to be read as whatever the parenthesized
        // expression evaluated to, so a target sharing a name with a primitive silently became a
        // conversion; `(double)()` emitted a C# cast.
        AssertOutput(@"
def triple(x: int) -> int:
    return x * 3

def add_one(x: int) -> int:
    return x + 1

def main():
    print(5 |> (triple))
    print(5 |> (triple)() |> (add_one)())
", "15\n16\n");
    }

    [Fact]
    public void ParenthesizedMutableDefault_IsStillRejected()
    {
        // Callee-shape dispatch in the validators normalizes too: `(list)()` is as mutable a
        // default as `list()`.
        var result = CompileAndExecute(@"
def f(items: list[int] = (list)()) -> int:
    return len(items)

def main() -> None:
    print(f())
");

        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors, e => e.Contains("Mutable default value is not allowed"));
    }

    [Fact]
    public void ArityDivergentMethodReference_IsRejectedRatherThanBoundToAnArbitraryOverload()
    {
        // list.pop has a zero-argument and a one-argument overload. The reference used to bind
        // whichever the lookup found first, so `g(0)` failed an arity check against a signature the
        // user never chose (#1170).
        var result = CompileAndExecute(@"
def main() -> None:
    xs: list[int] = [1, 2, 3]
    g = xs.pop
    print(g(0))
");

        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors,
            e => e.Contains("overloads taking different numbers of arguments"));
        // The rejection must not cascade into a bogus "'g' is not callable" at the use site.
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("is not callable"));
    }

    [Fact]
    public void ArityDivergentBuiltinReference_IsRejected()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    a_set: set[int] = {3, 1, 2}
    g = sorted
    print(g(a_set))
");

        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors,
            e => e.Contains("overloads taking different numbers of arguments"));
    }

    [Fact]
    public void AnnotatedTargetSelectsTheMatchingOverload()
    {
        // Target-typed selection: the annotation names exactly one candidate, so the reference binds
        // it and the call through the binding behaves like Python's `g = xs.pop; g(0)`.
        AssertOutput(@"
def main() -> None:
    xs: list[int] = [1, 2, 3]
    g: (int) -> int = xs.pop
    print(g(0))
    print(xs)
", "1\n[2, 3]\n");
    }

    [Fact]
    public void SingleOverloadMethodReference_BindsThatOverload()
    {
        AssertOutput(@"
class Box:
    n: int

    def __init__(self, n: int):
        self.n = n

    def get(self) -> int:
        return self.n

def main() -> None:
    g = Box(4).get
    print(g())
", "4\n");
    }

    [Fact]
    public void ArityUniformOverloadSets_StayUsableAsFunctionReferences()
    {
        // The conversion families and single-argument builtins all have several overloads that differ
        // only in parameter type. Whichever binds, calls through it pass the same arity check, so
        // passing them as a key/map function remains supported.
        AssertOutput(@"
def main() -> None:
    words: list[str] = [""ccc"", ""a"", ""bb""]
    print(min(words, key=len))
    nums: list[str] = [""3"", ""1"", ""2""]
    print(list(map(int, nums)))
", "a\n[3, 1, 2]\n");
    }
}
