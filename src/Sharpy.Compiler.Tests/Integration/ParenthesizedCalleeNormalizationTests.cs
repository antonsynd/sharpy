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
}
