using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Focused by-direction cells for the total coercion-possibility classifier (#1713, R-E). A
/// statically-impossible <c>as?</c>/<c>as!</c> is refused at SEMANTIC time with SPY0610, naming the
/// pair — it never reaches Roslyn as CS8121/CS0030 (SPY0908) as it did before the classifier existed.
/// The refusal is uniform across <c>as?</c> and <c>as!</c> and across every consumption (value,
/// truthiness, assert): the check is on the expression, not its consumer.
///
/// <para>These seed the Phase 6 guard mutations (Rule 12): (6a) restore CoercionPossibility.Classify's
/// impossible verdict to "allow" and every refused cell reaches codegen → SPY0908 instead of SPY0610
/// (refusals surface under codegen); (6b) drop the InterfaceSatisfiable arm and the interface controls
/// below turn red. The full Phase 7 <c>CoercionPossibilityMatrixTests</c> folds and re-cuts these.</para>
/// </summary>
public class CoercionPossibilityTests : IntegrationTestBase
{
    public CoercionPossibilityTests(ITestOutputHelper output) : base(output)
    {
    }

    private const string Spy0610 = "SPY0610";

    // ── Refused: statically-impossible pairs (were CS8121/CS0030 → SPY0908, or the retired SPY0228) ──

    [Fact]
    public void BytesToLong_AsOptional_Refused() // f01/f03b/f04 family (was CS8121 ICE)
    {
        var result = CompileAndExecute(@"
def main() -> None:
    o: bytes = b""abc""
    r = o as? long
    print(r is not None)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610);
        string.Join("\n", result.CompilationErrors).Should().Contain("bytes", "the steer names the pair");
    }

    [Fact]
    public void StrToDouble_AsChecked_Refused() // f02 (was CS0030 ICE)
    {
        var result = CompileAndExecute(@"
def main() -> None:
    s: str = ""test""
    d = s as! double
    print(d)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610);
    }

    [Fact]
    public void IntToStr_Refused() // f09 (was SPY0228, primitive→str arm)
    {
        var result = CompileAndExecute(@"
def main() -> None:
    n: int = 5
    r = n as? str
    print(r is not None)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610);
        string.Join("\n", result.CompilationErrors).Should().Contain("str(x)",
            "a primitive→str refusal steers to str(x)");
    }

    [Fact]
    public void StrToInt_Refused_SteersToIntParse() // f17 (was CS8121 ICE)
    {
        var result = CompileAndExecute(@"
def main() -> None:
    s: str = ""5""
    r = s as? int
    print(r is not None)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610);
        string.Join("\n", result.CompilationErrors).Should().Contain("try int(s)",
            "a str→number refusal steers to int(s) / try int(s)");
    }

    [Fact]
    public void BoolToInt_Refused() // f16 (was CS8121 ICE — bool is not numeric)
    {
        var result = CompileAndExecute(@"
def main() -> None:
    b: bool = True
    r = b as? int
    print(r is not None)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610);
    }

    [Fact]
    public void IntToUnrelatedClass_Refused() // f11b (was CS8121 ICE)
    {
        var result = CompileAndExecute(@"
class Dog:
    pass

def main() -> None:
    n: int = 5
    r = n as? Dog
    print(r is not None)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610);
    }

    [Fact]
    public void UnrelatedClasses_AsOptional_Refused() // f05 (was SPY0228, unrelated-class arm)
    {
        var result = CompileAndExecute(@"
class Dog:
    pass

class Cat:
    pass

def main() -> None:
    d: Dog = Dog()
    r = d as? Cat
    print(r is not None)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610);
        string.Join("\n", result.CompilationErrors).Should().Contain("unrelated",
            "unrelated user classes steer to 'the types are unrelated'");
    }

    [Fact]
    public void UnrelatedClasses_AsChecked_Refused() // f08 — the as! twin is refused uniformly
    {
        var result = CompileAndExecute(@"
class Dog:
    pass

class Cat:
    pass

def main() -> None:
    d: Dog = Dog()
    c = d as! Cat
    print(""done"")
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610);
    }

    [Fact]
    public void SameCollectionDifferentArgs_Refused() // b21b (was CS8121 ICE, #1330)
    {
        var result = CompileAndExecute(@"
def main() -> None:
    xs: list[int] = [1, 2]
    r = xs as? list[str]
    print(r is not None)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610);
        string.Join("\n", result.CompilationErrors).Should().Contain("matching type arguments",
            "same collection with different args steers to matching type arguments");
    }

    [Fact]
    public void DifferentCollections_Refused() // f14b (was CS8121 ICE)
    {
        var result = CompileAndExecute(@"
def main() -> None:
    xs: list[int] = [1, 2]
    r = xs as? dict[str, int]
    print(r is not None)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610);
    }

    [Fact]
    public void RefusedInAssertConsumption_IsUniform() // f01 assert consumption
    {
        var result = CompileAndExecute(@"
def main() -> None:
    o: bytes = b""abc""
    assert o as? long, ""cast""
    print(""pass"")
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610,
            "the refusal is on the expression, not its consumer — assert is no different from a value");
    }

    // ── Allowed: possible pairs must still compile and run (controls for the mutations) ──

    [Fact]
    public void ClassToInterface_Allowed_InterfaceSatisfiableArm() // guards mutation 6b
    {
        // Rock does not implement IPet, but IPet is an INTERFACE and Sharpy classes are emitted
        // unsealed, so the coercion is possible (returns None at runtime). Declared with `interface`
        // deliberately: with `class IPet` this pair is impossible and refused.
        var result = CompileAndExecute(@"
interface IPet:
    def speak(self) -> str:
        ...

class Rock:
    pass

def main() -> None:
    r: Rock = Rock()
    x = r as? IPet
    print(x is not None)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.RawDiagnostics.Should().NotContain(d => d.Code == Spy0610);
        result.StandardOutput.Trim().Should().Be("False");
    }

    [Fact]
    public void Inheritance_Allowed() // f13b
    {
        var result = CompileAndExecute(@"
class Animal:
    pass

class Dog(Animal):
    pass

def main() -> None:
    a: Animal = Dog()
    r = a as? Dog
    print(r is not None)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.RawDiagnostics.Should().NotContain(d => d.Code == Spy0610);
        result.StandardOutput.Trim().Should().Be("True");
    }

    [Fact]
    public void EnumToBackingInt_Allowed() // f18
    {
        var result = CompileAndExecute(@"
enum Color:
    RED = 1
    BLUE = 2

def main() -> None:
    c: Color = Color.RED
    r = c as! int
    print(r)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.RawDiagnostics.Should().NotContain(d => d.Code == Spy0610);
        result.StandardOutput.Trim().Should().Be("1");
    }

    [Fact]
    public void ObjectSource_Allowed() // f19
    {
        var result = CompileAndExecute(@"
def main() -> None:
    o: object = ""not a long""
    r = o as? long
    print(r is not None)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.RawDiagnostics.Should().NotContain(d => d.Code == Spy0610);
        result.StandardOutput.Trim().Should().Be("False");
    }

    [Fact]
    public void NumericToNumeric_Allowed()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    n: long = 5
    r = n as? int
    print(r is not None)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.RawDiagnostics.Should().NotContain(d => d.Code == Spy0610);
        result.StandardOutput.Trim().Should().Be("True");
    }

    [Fact]
    public void AliasedClrType_IsIdentity_Allowed()
    {
        // re_module regression: `Match as NetMatch` is an import alias for the SAME CLR type. Casting a
        // Match-typed value to NetMatch is identity — the classifier must compare by underlying CLR
        // symbol (IsSameType), not by the display-name spellings, or it over-refuses a valid cast that
        // compiled and ran pre-Phase-6.
        var result = CompileAndExecute(@"
from system.text.regular_expressions import Regex as NetRegex, Match as NetMatch, MatchCollection

def main() -> None:
    r: NetRegex = NetRegex(""a"")
    matches: MatchCollection = r.matches(""aaa"")
    for m_raw in matches:
        m: NetMatch = m_raw as! NetMatch
        print(m.value)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.RawDiagnostics.Should().NotContain(d => d.Code == Spy0610);
        result.StandardOutput.Replace("\r\n", "\n").Trim().Should().Be("a\na\na");
    }

    // ── R-P5-2: possibility is judged on the type the EMITTER casts from, not the flow-narrowed read ──

    [Fact]
    public void ReassignmentNarrowedObject_AsCoercion_Runs()
    {
        // #1712: `o: object` reassigned to a str narrows the checker's live type to str, but the emitted
        // C# local stays `object` (`o is long`, valid, false at runtime). Possibility must use the
        // DECLARED type (object → Unboxing), not the narrowed str — refusing here is a false positive.
        var result = CompileAndExecute(@"
def main() -> None:
    o: object = long(42)
    o = ""not a long""
    if o as? long:
        print(""some"")
    else:
        print(""none"")
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.RawDiagnostics.Should().NotContain(d => d.Code == Spy0610);
        result.StandardOutput.Trim().Should().Be("none");
    }

    [Fact]
    public void IsinstanceNarrowedOperand_ImpossibleCoercion_Refused()
    {
        // The complementary direction: an isinstance/type-guard narrowing DOES make the emitter cast the
        // read (`(str)o`), so `(str)o as? long` would be the CS8121 the refusal exists to catch. The
        // Cast read-lowering is recorded here, so possibility uses the narrowed str → SPY0610.
        var result = CompileAndExecute(@"
def main() -> None:
    o: object = ""hi""
    if isinstance(o, str):
        x = o as? long
        print(x is not None)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == Spy0610,
            "an isinstance-narrowed str operand casting to long is statically impossible in the emitted C#");
    }
}
