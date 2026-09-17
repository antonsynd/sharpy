using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Coercion-possibility matrix (P5, #1713, R-E). A cast is classified ONCE, at semantic time, and the
/// classification is identical for <c>as?</c> and <c>as!</c>: a statically-impossible coercion is
/// refused <b>SPY0610</b> uniformly across both operators (it never reaches Roslyn as CS8121/CS0030
/// behind SPY0908); a possible coercion compiles for both. Total over the possibility taxonomy
/// {identity, boxing, unboxing, numeric, inheritance (both directions), interface-satisfiable,
/// enum-backing, object-downcast, generic-same-definition} × {impossible: no-coercion,
/// unrelated-classes, same-definition-different-arguments, …} × operator {as?, as!}.
/// <para>
/// Two lowering divergences the classifier does not (yet) prevent are rostered N/A with a positive
/// control: (enum-backing × as?) ICEs (#1892), and the R-P5-2 source-type rule is pinned by the
/// reassignment-narrowed (runs) vs isinstance-narrowed (SPY0610) cells. The aliased-CLR-type-identity
/// control guards against over-refusing an import alias for the same runtime type.
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class CoercionPossibilityMatrixTests : IntegrationTestBase
{
    public CoercionPossibilityMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string SPY0610 = DiagnosticCodes.SemanticOverflow.ImpossibleCoercion;
    private const string SPY0908 = DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError;

    private sealed record Case(
        string Name, string Prelude, string Setup, string Target, bool Possible, string? Steer, string AsqOutput);

    private const string AnimalDog = "class Animal:\n    pass\n\nclass Dog(Animal):\n    pass\n\n";
    private const string DogCat = "class Dog:\n    x: int = 0\n\nclass Cat:\n    y: int = 0\n\n";
    private const string PetRock = "interface IPet:\n    def speak(self) -> str:\n        ...\n\nclass Rock:\n    pass\n\n";
    private const string BoxDecl = "class Box[T]:\n    value: T\n\n    def __init__(self, value: T):\n        self.value = value\n\n";
    private const string ColorDecl = "enum Color:\n    RED = 1\n    BLUE = 2\n\n";

    private static readonly Case[] PossibleCases =
    {
        new("identity", "", "x: int = 5", "int", true, null, "True"),
        new("boxing", "", "x: int = 5", "object", true, null, "True"),
        new("unboxing", "", "x: object = 5", "int", true, null, "True"),
        new("numeric", "", "x: int = 5", "float", true, null, "True"),
        new("inheritance_down", AnimalDog, "x: Animal = Dog()", "Dog", true, null, "True"),
        new("inheritance_up", AnimalDog, "x: Dog = Dog()", "Animal", true, null, "True"),
        new("interface_satisfiable", PetRock, "x: Rock = Rock()", "IPet", true, null, "False"),
        new("object_downcast", BoxDecl, "b: Box[str] = Box[str](\"hi\")\n    x: object = b", "Box[str]", true, null, "True"),
        new("generic_same_def", "", "xs: list[int] = [1, 2]\n    x: object = xs", "list[int]", true, null, "True"),
        new("enum_backing", ColorDecl, "x: Color = Color.RED", "int", true, null, "1"),
    };

    private static readonly Case[] ImpossibleCases =
    {
        new("int_to_str", "", "x: int = 5", "str", false, "str(x)", ""),
        new("str_to_int", "", "x: str = \"5\"", "int", false, "int(s)", ""),
        new("unrelated_classes", DogCat, "x: Dog = Dog()", "Cat", false, "unrelated", ""),
        new("same_def_diff_args", "", "x: list[int] = [1, 2]", "list[str]", false, "matching type arguments", ""),
        new("bytes_to_long", "", "x: bytes = b\"abc\"", "long", false, null, ""),
        new("bool_to_int", "", "x: bool = True", "int", false, null, ""),
    };

    private static Case C(string name)
        => PossibleCases.Concat(ImpossibleCases).First(c => c.Name == name);

    // (enum-backing × as?) ICEs today (#1892) — as? lowers enum→int to `is int` (CS8121). Rostered.
    private static bool IsRostered(string caseName, string op)
        => caseName == "enum_backing" && op == "as?";

    private static string Source(Case c, string op)
    {
        var castOp = op == "as?" ? "as?" : "as!";
        var consume = op == "as?" ? "print(r is not None)" : "print(r)";
        return $@"
{c.Prelude}def main() -> None:
    {c.Setup}
    r = x {castOp} {c.Target}
    {consume}
";
    }

    // ── Possible: both operators compile; as? runs with the measured result ───────────────────

    public static IEnumerable<object[]> PossibleCells()
    {
        foreach (var c in PossibleCases)
            foreach (var op in new[] { "as?", "as!" })
                if (!IsRostered(c.Name, op))
                    yield return new object[] { c.Name, op };
    }

    [Theory]
    [MemberData(nameof(PossibleCells))]
    public void Possible_BothOperators_NotRefused(string caseName, string op)
    {
        var c = C(caseName);
        var source = Source(c, op);
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(d => d.Code == SPY0610,
            $"[{caseName}/{op}] is a possible coercion — the classifier must not refuse it\n{source}");
        result.RawDiagnostics.Should().NotContain(d => d.Code == SPY0908,
            $"[{caseName}/{op}] must compile — a possible coercion never reaches Roslyn as an ICE\n{source}");

        if (op == "as?")
        {
            // as? is total (yields Optional, never throws), so it runs to completion with a fixed result.
            result.Success.Should().BeTrue(
                $"[{caseName}/as?] runs. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            result.StandardOutput.TrimEnd().Should().Be(c.AsqOutput, $"[{caseName}/as?]\n{source}");
        }
    }

    // enum-backing runs under as! (the rostered as? cell is #1892); pinned so the roster is falsifiable.
    [Fact]
    public void EnumBacking_AsChecked_Runs()
    {
        var result = CompileAndExecute(Source(C("enum_backing"), "as!"));
        result.RawDiagnostics.Should().NotContain(d => d.Code == SPY0610);
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("1");
    }

    [Fact]
    public void EnumBacking_AsOptional_Rostered_ICE_1892()
    {
        // Positive control for the roster: as? enum→int lowers to `is int` → CS8121 → SPY0908.
        var result = CompileAndExecute(Source(C("enum_backing"), "as?"));
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0908,
            "the (enum-backing × as?) roster is falsifiable: it ICEs today (#1892) while as! runs");
    }

    // ── Impossible: SPY0610 uniformly across as? and as! ──────────────────────────────────────

    public static IEnumerable<object[]> ImpossibleCells()
    {
        foreach (var c in ImpossibleCases)
            foreach (var op in new[] { "as?", "as!" })
                yield return new object[] { c.Name, op };
    }

    [Theory]
    [MemberData(nameof(ImpossibleCells))]
    public void Impossible_RefusedUniformly(string caseName, string op)
    {
        var c = C(caseName);
        var source = Source(c, op);
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{caseName}/{op}] is statically impossible\n{source}");
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0610,
            $"[{caseName}/{op}] must be refused SPY0610, not left to Roslyn as SPY0908\n{source}");
        result.RawDiagnostics.Should().NotContain(d => d.Code == SPY0908,
            $"[{caseName}/{op}] the refusal is ours, not an ICE\n{source}");
        if (c.Steer is not null)
        {
            string.Join("\n", result.CompilationErrors).Should().Contain(c.Steer,
                $"[{caseName}/{op}] the message steers to the possible spelling\n{source}");
        }
    }

    // Cross-operator agreement, made explicit: for every impossible case, both operators refuse.
    [Theory]
    [MemberData(nameof(ImpossibleCells))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026", Justification = "op unused; the pair is asserted once per case")]
    public void Impossible_AsqAndAsx_BothRefuse(string caseName, string op)
    {
        if (op != "as?")
            return; // assert the pair once per case, driven from the as? row

        var c = C(caseName);
        var asq = CompileAndExecute(Source(c, "as?"));
        var asx = CompileAndExecute(Source(c, "as!"));

        asq.RawDiagnostics.Any(d => d.Code == SPY0610).Should().BeTrue($"[{caseName}] as? refuses");
        asx.RawDiagnostics.Any(d => d.Code == SPY0610).Should().BeTrue($"[{caseName}] as! refuses");
    }

    // ── Totality ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        PossibleCases.Length.Should().Be(10);
        ImpossibleCases.Length.Should().Be(6);
        PossibleCases.Select(c => c.Name).Should().OnlyHaveUniqueItems();
        ImpossibleCases.Select(c => c.Name).Should().OnlyHaveUniqueItems();

        var possibleProduct = PossibleCases.Length * 2; // × {as?, as!}
        var possibleExecuting = PossibleCells().Count();
        (possibleProduct - possibleExecuting).Should().Be(1,
            "the only rostered coercion cell is (enum-backing × as?) — #1892");
    }

    // ── R-P5-2: possibility is judged on the type the EMITTER casts from ──────────────────────

    [Fact]
    public void ReassignmentNarrowedObject_Coercion_Runs()
    {
        // #1712: `o: object` reassigned to a str narrows the checker but the emitted local stays object
        // (`o is long`, valid, false). Possibility uses the DECLARED object → possible → runs.
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
        result.RawDiagnostics.Should().NotContain(d => d.Code == SPY0610);
        result.StandardOutput.TrimEnd().Should().Be("none");
    }

    [Fact]
    public void IsinstanceNarrowedOperand_ImpossibleCoercion_Refused()
    {
        // The complementary direction: an isinstance narrowing makes the emitter cast the read (`(str)o`),
        // so `(str)o as? long` is the CS8121 the refusal catches. Possibility uses the narrowed str → SPY0610.
        var result = CompileAndExecute(@"
def main() -> None:
    o: object = ""hi""
    if isinstance(o, str):
        x = o as? long
        print(x is not None)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0610,
            "an isinstance-narrowed str operand casting to long is statically impossible in the emitted C#");
    }

    // ── Aliased CLR type identity: an import alias is the SAME type, not unrelated ─────────────

    [Fact]
    public void AliasedClrType_IsIdentity_Allowed()
    {
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
        result.RawDiagnostics.Should().NotContain(d => d.Code == SPY0610,
            "`Match as NetMatch` casting a Match to NetMatch is identity — the classifier compares by CLR symbol");
        result.StandardOutput.Replace("\r\n", "\n").Trim().Should().Be("a\na\na");
    }
}
