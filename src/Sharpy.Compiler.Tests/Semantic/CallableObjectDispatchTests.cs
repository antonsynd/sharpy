using FluentAssertions;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// <c>obj(args)</c> IS the member call <c>obj.__call__(args)</c> and resolves through the SAME
/// path an ordinary method call takes (#1672, owner ruling Q3).
///
/// <para>The defect class: <c>TryResolveCallableObject</c> was a private re-implementation of
/// method-call resolution — it read <c>typeSymbol.Methods</c> (own methods only), took the first
/// same-named candidate, hand-rolled arity and type checks, and never read the keyword-argument
/// types at all. Every capability the shared path has was therefore missing at this one call
/// shape: an inherited <c>__call__</c> was "not callable" (SPY0201), a keyword argument reached
/// codegen unbound (CS7036 behind SPY0908), <c>*args</c> was an arity error (SPY0224), and an
/// overload pair resolved to whichever member was declared first (SPY0220). python3 accepts all
/// four (verified with 3.12.13).</para>
///
/// <para>Each row below is paired with the same shape written as an ORDINARY method call, which
/// is the positive control: if a row fails for both spellings the cause is not this seam.</para>
///
/// <para>MUTATION-TESTED: see this file's commit body. Reverting <c>TryResolveCallableObject</c>
/// to the own-methods/first-candidate lookup turns the inherited, keyword-argument, variadic and
/// overload rows red while the ordinary-method controls stay green.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class CallableObjectDispatchTests : IntegrationTestBase
{
    public CallableObjectDispatchTests(ITestOutputHelper output) : base(output) { }

    private static string FormatErrors(ExecutionResult result)
        => string.Join("\n", result.CompilationErrors);

    private void ShouldPrint(string source, string expected)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(FormatErrors(result));
        result.StandardOutput.Trim().Should().Be(expected);
    }

    // ── Inheritance: the base walk ──

    [Fact]
    public void InheritedCall_ResolvesThroughTheBaseWalk()
    {
        // python3: `class Base: __call__`; `class Derived(Base): pass`; `Derived()(21)` -> 42
        ShouldPrint(@"
class Base:
    def __call__(self, x: int) -> int:
        return x * 2

class Derived(Base):
    pass

def main() -> None:
    d: Derived = Derived()
    print(d(21))
", "42");
    }

    [Fact]
    public void InheritedOrdinaryMethod_ResolvesThroughTheBaseWalk_Control()
    {
        ShouldPrint(@"
class Base:
    def go(self, x: int) -> int:
        return x * 2

class Derived(Base):
    pass

def main() -> None:
    d: Derived = Derived()
    print(d.go(21))
", "42");
    }

    // ── Keyword arguments and defaults ──

    [Fact]
    public void KeywordArguments_BindByName()
    {
        // python3: g(name="bob") -> "hello bob"; g("bob", greeting="yo") -> "yo bob"
        ShouldPrint(@"
class Greeter:
    def __call__(self, name: str, greeting: str = ""hello"") -> str:
        return greeting + "" "" + name

def main() -> None:
    g: Greeter = Greeter()
    print(g(name=""bob""))
    print(g(""bob"", greeting=""yo""))
", "hello bob\nyo bob");
    }

    [Fact]
    public void KeywordArguments_BindByName_OrdinaryMethodControl()
    {
        ShouldPrint(@"
class Greeter:
    def go(self, name: str, greeting: str = ""hello"") -> str:
        return greeting + "" "" + name

def main() -> None:
    g: Greeter = Greeter()
    print(g.go(name=""bob""))
    print(g.go(""bob"", greeting=""yo""))
", "hello bob\nyo bob");
    }

    [Fact]
    public void UnknownKeywordName_IsRefused()
    {
        var result = CompileAndExecute(@"
class Greeter:
    def __call__(self, name: str) -> str:
        return name

def main() -> None:
    g: Greeter = Greeter()
    print(g(nome=""bob""))
");
        result.Success.Should().BeFalse();
        string.Join("\n", result.CompilationErrors).Should().Contain("nome",
            "keyword names validate on this route now that it shares ValidateCallArguments");
    }

    // ── Variadic ──

    [Fact]
    public void VariadicCall_AbsorbsSurplusArguments()
    {
        // python3: Summer()(1, 2, 3) -> 6
        ShouldPrint(@"
class Summer:
    def __call__(self, *args: int) -> int:
        total: int = 0
        for a in args:
            total += a
        return total

def main() -> None:
    s: Summer = Summer()
    print(s(1, 2, 3))
", "6");
    }

    [Fact]
    public void VariadicOrdinaryMethod_AbsorbsSurplusArguments_Control()
    {
        ShouldPrint(@"
class Summer:
    def go(self, *args: int) -> int:
        total: int = 0
        for a in args:
            total += a
        return total

def main() -> None:
    s: Summer = Summer()
    print(s.go(1, 2, 3))
", "6");
    }

    // ── Overload set ──

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OverloadedCall_SelectsByArgumentType_InEitherDeclarationOrder(bool intFirst)
    {
        var intDecl = "    def __call__(self, x: int) -> str:\n        return \"int\"\n";
        var strDecl = "    def __call__(self, x: str) -> str:\n        return \"str\"\n";
        var source = "class Poly:\n"
            + (intFirst ? intDecl + "\n" + strDecl : strDecl + "\n" + intDecl)
            + @"
def main() -> None:
    p: Poly = Poly()
    print(p(1))
    print(p(""a""))
";
        ShouldPrint(source, "int\nstr");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OverloadedOrdinaryMethod_SelectsByArgumentType_Control(bool intFirst)
    {
        var intDecl = "    def go(self, x: int) -> str:\n        return \"int\"\n";
        var strDecl = "    def go(self, x: str) -> str:\n        return \"str\"\n";
        var source = "class Poly:\n"
            + (intFirst ? intDecl + "\n" + strDecl : strDecl + "\n" + intDecl)
            + @"
def main() -> None:
    p: Poly = Poly()
    print(p.go(1))
    print(p.go(""a""))
";
        ShouldPrint(source, "int\nstr");
    }

    // ── Callee shapes: what stands in the callee position ──

    [Fact]
    public void AttributeCallee_Dispatches()
    {
        ShouldPrint(@"
class Doubler:
    def __call__(self, x: int) -> int:
        return x * 2

class Holder:
    op: Doubler

    def __init__(self) -> None:
        self.op = Doubler()

def main() -> None:
    h: Holder = Holder()
    print(h.op(21))
", "42");
    }

    [Fact]
    public void IndexedCallee_Dispatches()
    {
        ShouldPrint(@"
class Doubler:
    def __call__(self, x: int) -> int:
        return x * 2

def main() -> None:
    ops: list[Doubler] = [Doubler()]
    print(ops[0](21))
", "42");
    }

    [Fact]
    public void CallResultCallee_Dispatches()
    {
        ShouldPrint(@"
class Doubler:
    def __call__(self, x: int) -> int:
        return x * 2

def make() -> Doubler:
    return Doubler()

def main() -> None:
    print(make()(21))
", "42");
    }

    [Fact]
    public void CallableObjectInArgumentPosition_Dispatches()
    {
        ShouldPrint(@"
class Doubler:
    def __call__(self, x: int) -> int:
        return x * 2

def g(v: int) -> int:
    return v + 1

def main() -> None:
    c: Doubler = Doubler()
    print(g(c(3)))
", "7");
    }

    // ── Refusals: the two arms that stay closed ──

    [Fact]
    public void ExplicitDunderInvocation_StaysRefused()
    {
        // python3 permits `c.__call__(3)`; Sharpy refuses it (dunder_invocation_rules.md) — this
        // is the positive control for the refusal, not a gap in the dispatch above.
        var result = CompileAndExecute(@"
class C:
    def __call__(self, x: int) -> int:
        return x

def main() -> None:
    c: C = C()
    print(c.__call__(3))
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0427",
            "explicit dunder invocation is refused independently of obj(args) dispatch");
    }

    [Fact]
    public void NonCallableIdentifier_IsRefusedWithSPY0230_NamingTheType()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    x: int = 5
    x()
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0230",
            "a bound name whose type has no __call__ is 'not callable' (SPY0230), not an "
            + "'undefined function' (SPY0201)");
        string.Join("\n", result.CompilationErrors).Should().Contain("int32",
            "the message names the offending type");
    }

    [Fact]
    public void NonCallableExpression_IsRefusedWithSPY0230_NamingTheType()
    {
        var result = CompileAndExecute(@"
class Box:
    value: float

    def __init__(self) -> None:
        self.value = 1.5

def main() -> None:
    b: Box = Box()
    b.value()
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0230");
        string.Join("\n", result.CompilationErrors).Should().Contain("float64");
    }

    [Fact]
    public void ClassWithoutCall_IsStillNotCallable()
    {
        var result = CompileAndExecute(@"
class Plain:
    pass

def main() -> None:
    p: Plain = Plain()
    p(1)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0230",
            "the __call__ route declines and the one 'not callable' arm reports");
        string.Join("\n", result.CompilationErrors).Should().Contain("Plain");
    }
}
