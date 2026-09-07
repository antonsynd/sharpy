using System.Linq;
using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The class-scope rule as a matrix (#1786, R-Y): a name declared in a class or struct body is not
/// visible by its bare name inside any function-like body nested in it.
/// </summary>
/// <remarks>
/// <para><b>Contract.</b> A bare READ of such a name is unresolved (SPY0200) and says which member
/// it names, with a steer that COMPILES. A bare STORE is refused by name (SPY0606) in every store
/// form — plain, augmented, walrus, tuple element, <c>??=</c> — never silently declaring a local.
/// The rule is variable-only: nested types, method names and module names are unaffected, and a
/// signature position (a parameter default) still resolves in the declaring scope. R-AA (#1803,
/// 2026-09-06): a member is a member whether the enclosing body declares it or a base's body does,
/// and whether it is a field or a property — the store refusal and the read steer share ONE
/// membership predicate, so the two sides cannot disagree about which names are members.</para>
///
/// <para><b>Axes.</b> Use (9) x member (10) x host (8) x typing (2), plus the R-AA product
/// origin (5) x store form (5). The full product is not enumerated; the plan's rule is to vary one
/// axis at a time so agreement on one axis is not read as evidence for another: use x typing at
/// one host and member, member x use at one host, host x use at one member, origin x store form
/// at one host. Every cell executes.</para>
///
/// <para><b>Controls.</b> A module read from a method, the shadow idiom, a module variable shadowed
/// by a class attribute, a parameter default naming a class member, a nested type in a return
/// annotation and a bare method name all keep their pre-rule behaviour.</para>
/// </remarks>
[Collection("HeavyCompilation")]
public class ScopeChainMatrixTests : IntegrationTestBase
{
    public ScopeChainMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string BareStoreCode = "SPY0606";
    private const string UnresolvedCode = "SPY0200";

    // ── Axis sizes, anchored to the InlineData row counts they describe ──────────────────
    private const int UseFormCount = 9;
    private const int MemberKindCount = 10;
    private const int HostCount = 8;
    private const int TypingCount = 2;
    private const int MemberOriginCount = 5;
    private const int StoreFormCount = 5;

    // ════════════════════════════════════════════════════════════════════════════════════
    // Part 1 — use x typing, at host = class method, member = instance field
    // ════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("read", "same")]
    [InlineData("read", "mistyped")]
    [InlineData("plain_store", "same")]
    [InlineData("plain_store", "mistyped")]
    [InlineData("augmented_store", "same")]
    [InlineData("augmented_store", "mistyped")]
    [InlineData("walrus", "same")]
    [InlineData("walrus", "mistyped")]
    [InlineData("tuple_element", "same")]
    [InlineData("tuple_element", "mistyped")]
    [InlineData("coalesce_store", "same")]
    [InlineData("coalesce_store", "mistyped")]
    [InlineData("comprehension_read", "same")]
    [InlineData("comprehension_read", "mistyped")]
    [InlineData("lambda_field_initializer_read", "same")]
    [InlineData("lambda_field_initializer_read", "mistyped")]
    public void UseForm_OnAnInstanceField_IsRefusedByName(string use, string typing)
    {
        var source = InstanceFieldUse(use, typing);
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(
            $"a bare {use} of a class attribute is refused (#1786, R-Y); it must never compile as a "
            + $"fresh local.\n{Report(result)}");

        var expected = IsStore(use) ? BareStoreCode : UnresolvedCode;
        Codes(result).Should().Contain(expected,
            $"a bare {use} reports {expected}.\n{Report(result)}");

        // A refused store names a MEMBER, so no local was declared and nothing is "assigned but
        // never used". Measured red at fa0c9e39a for the plain, walrus and tuple-element forms: the
        // refusal returned before any TargetBinding was recorded, and the validator's syntactic
        // fallback then counted the target as a definition (#1803, the own-field twin).
        Codes(result).Should().NotContain("SPY0451",
            $"a refused {use} declares no local, so SPY0451 must not fire under SPY0606.\n{Report(result)}");
    }

    /// <summary>
    /// The ninth use form. A pattern head is NOT a read: it names the constant it matches, and
    /// degrading it to a capture silently changes what the program matches (it also makes a
    /// following wildcard arm unreachable, SPY0700).
    /// </summary>
    [Fact]
    public void UseForm_PatternHead_BindsTheClassConstant()
    {
        var source = @"
class C:
    const A: str = ""x""

    def m(self, v: str) -> None:
        match v:
            case A:
                print(""hit"")
            case _:
                print(""miss"")

def main() -> None:
    C().m(""x"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"a pattern head naming a class const binds it.\n{Report(result)}");
        result.StandardOutput.Should().Contain("hit");
        Codes(result).Should().NotContain("SPY0700",
            "the head is a constant pattern, not a capture, so no arm becomes unreachable");
    }

    /// <summary>
    /// The same head with an int-typed constant, which needs the const-eligibility fact as well as
    /// the binding: a C# case label must be a compile-time constant.
    /// </summary>
    [Fact]
    public void UseForm_PatternHead_IntConstant_IsAConstantPatternNotACapture()
    {
        var source = @"
class C:
    const A: int = 3

    def m(self, x: int) -> None:
        match x:
            case A:
                print(""hit"")
            case _:
                print(""miss"")

def main() -> None:
    C().m(3)
";
        var result = CompileAndExecute(source);

        Errors(result).Should().NotContain(e => e.Contains("makes remaining patterns unreachable"),
            $"a class const in a pattern head is a constant pattern, never a capture: as a capture "
            + $"it would swallow the wildcard arm (SPY0700).\n{Report(result)}");
        result.Success.Should().BeTrue(
            $"the head names the OWNING TYPE's const symbol, which is the one carrying the "
            + $"compile-time-constant fact a C# case label needs.\n{Report(result)}");
        result.StandardOutput.Should().Contain("hit");
    }

    /// <summary>
    /// The two controls that localize the pattern head's symbol identity: a module-level const in a
    /// bare head, and a class const reached through a QUALIFIED head. Both bind the declaration's
    /// own symbol without crossing anything, so both worked before the crossed head did.
    /// </summary>
    [Theory]
    [InlineData("module const, bare head",
        "const A: str = \"x\"\n\ndef m(v: str) -> None:\n    match v:\n        case A:\n"
        + "            print(\"hit\")\n        case _:\n            print(\"miss\")\n\n"
        + "def main() -> None:\n    m(\"x\")")]
    [InlineData("class const, qualified head",
        "class C:\n    const A: str = \"x\"\n\n    def m(self, v: str) -> None:\n"
        + "        match v:\n            case C.A:\n                print(\"hit\")\n"
        + "            case _:\n                print(\"miss\")\n\n"
        + "def main() -> None:\n    C().m(\"x\")")]
    public void PatternHead_Control_BindsWithoutCrossing(string cell, string source)
    {
        var result = CompileAndExecute("\n" + source.Replace("\\n", "\n") + "\n");
        result.Success.Should().BeTrue($"[{cell}]\n{Report(result)}");
        result.StandardOutput.Should().Contain("hit", $"[{cell}]");
    }

    // ════════════════════════════════════════════════════════════════════════════════════
    // Part 2 — member kind x use, at host = class method
    // ════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("instance_field", "read", UnresolvedCode, "self.v")]
    [InlineData("instance_field", "plain_store", BareStoreCode, "self.v = ...")]
    [InlineData("class_const", "read", UnresolvedCode, "C.v")]
    [InlineData("class_const", "plain_store", BareStoreCode, "is a constant and cannot be assigned")]
    [InlineData("static_field", "read", UnresolvedCode, "C.v")]
    [InlineData("static_field", "plain_store", BareStoreCode, "C.v = ...")]
    [InlineData("property_name", "read", UnresolvedCode, "self.v")]
    [InlineData("property_name", "plain_store", BareStoreCode, "self.v = ...")]
    [InlineData("inherited_field", "read", UnresolvedCode, "self.v")]
    [InlineData("inherited_field", "plain_store", BareStoreCode, "self.v = ...")]
    [InlineData("inherited_property", "read", UnresolvedCode, "self.v")]
    [InlineData("inherited_property", "plain_store", BareStoreCode, "self.v = ...")]
    [InlineData("inherited_const", "read", UnresolvedCode, "Base.v")]
    [InlineData("inherited_const", "plain_store", BareStoreCode, "is a constant and cannot be assigned")]
    [InlineData("inherited_static_field", "read", UnresolvedCode, "Base.v")]
    [InlineData("inherited_static_field", "plain_store", BareStoreCode, "Base.v = ...")]
    public void MemberKind_IsRefused_WithASteerThatCompiles(
        string member, string use, string expectedCode, string expectedSteer)
    {
        var source = MemberKindUse(member, use);
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"a bare {use} of a {member} is refused.\n{Report(result)}");
        Codes(result).Should().Contain(expectedCode,
            $"the {member} {use} reports {expectedCode}.\n{Report(result)}");
        Errors(result).Should().Contain(e => e.Contains(expectedSteer),
            $"the {member} {use} steers to '{expectedSteer}'.\n{Report(result)}");
    }

    /// <summary>
    /// R-AA (#1803): a member the enclosing body does NOT declare — a property, an inherited field
    /// or property at any depth, a struct's property — is refused by name in every store form,
    /// exactly as a body-declared field is.
    /// </summary>
    /// <remarks>
    /// Direction, measured with the fa0c9e39a binary: every origin below compiled and printed the
    /// fresh local (the member untouched), so each cell is wrong-output-before → SPY0606-now, and
    /// no working program is turned red: the local never wrote the member, and the typed
    /// shadowing local (<see cref="Control_TypedShadowingLocal_OnAnInheritedField_Compiles"/>)
    /// still compiles. The product is generated from two arrays so a dropped origin or store form
    /// fails the totality anchor instead of leaving the survivors green.
    /// </remarks>
    [Theory]
    [MemberData(nameof(MemberOriginByStoreForm))]
    public void MemberNotInTheClassBody_BareStore_IsRefusedInEveryForm(string origin, string form)
    {
        var source = MemberOriginStore(origin, form);
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(
            $"[{origin} x {form}] a bare store to a member the body does not declare is refused "
            + $"(R-AA, #1803); at fa0c9e39a it declared a silent local.\n{source}\n{Report(result)}");
        Codes(result).Should().Contain(BareStoreCode,
            $"[{origin} x {form}] the refusal is SPY0606, the same code as a body-declared field.\n{Report(result)}");
        Codes(result).Should().NotContain("SPY0451",
            $"[{origin} x {form}] no local was declared, so nothing is 'assigned but never used'.\n{Report(result)}");

        if (origin.StartsWith("inherited", StringComparison.Ordinal))
        {
            Errors(result).Should().Contain(e => e.Contains("inherited class attribute 'v'"),
                $"[{origin} x {form}] the wording says the member is inherited, so the reader is "
                + $"not sent looking for a declaration in the body in front of them.\n{Report(result)}");
        }
    }

    /// <summary>The escape hatch R-AA leaves open: a typed declaration is a new local, and the member is untouched.</summary>
    [Fact]
    public void Control_TypedShadowingLocal_OnAnInheritedField_Compiles()
    {
        var source = @"
class Base:
    v: int = 5

class Derived(Base):
    def m(self) -> int:
        v: int = 7
        print(v)
        return self.v

def main() -> None:
    print(Derived().m())
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"the steer's shadowing local must compile for an inherited field too\n{Report(result)}");
        result.StandardOutput.Should().Contain("7");
        result.StandardOutput.Should().Contain("5", "the inherited field keeps its value");
    }

    private static readonly string[] MemberOrigins =
    {
        "property", "inherited_field", "inherited_property", "inherited_field_two_levels", "struct_property",
    };

    private static readonly string[] StoreForms =
    {
        "plain_store", "augmented_store", "walrus", "tuple_element", "coalesce_store",
    };

    public static IEnumerable<object[]> MemberOriginByStoreForm()
        => MemberOrigins.SelectMany(origin => StoreForms.Select(form => new object[] { origin, form }));

    /// <summary>Control member kind: a bare METHOD name is an ordinary unresolved name, as before the rule.</summary>
    [Fact]
    public void MemberKind_MethodName_StaysAnOrdinaryUnresolvedName()
    {
        var source = @"
class C:
    def helper(self) -> int:
        return 1

    def m(self) -> int:
        return helper()

def main() -> None:
    print(C().m())
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        Codes(result).Should().Contain(UnresolvedCode,
            $"a bare method name was already unresolved and stays so.\n{Report(result)}");
    }

    /// <summary>Control member kind: a nested TYPE resolves bare — the rule is variable-only.</summary>
    [Fact]
    public void MemberKind_NestedType_StillResolvesBare()
    {
        var source = @"
class C:
    enum Color:
        RED = 1

    def m(self) -> str:
        c: Color = Color.RED
        return c.name

def main() -> None:
    print(C().m())
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"nested types keep resolving bare.\n{Report(result)}");
        result.StandardOutput.Should().Contain("RED");
    }

    // ════════════════════════════════════════════════════════════════════════════════════
    // Part 3 — host x use, at member = instance field
    // ════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("class_method", "read")]
    [InlineData("class_method", "plain_store")]
    [InlineData("struct_method", "read")]
    [InlineData("struct_method", "plain_store")]
    [InlineData("property_get", "read")]
    [InlineData("property_get", "plain_store")]
    [InlineData("property_set", "read")]
    [InlineData("property_set", "plain_store")]
    [InlineData("event_accessor", "read")]
    [InlineData("event_accessor", "plain_store")]
    [InlineData("lambda_in_method", "read")]
    [InlineData("lambda_in_method", "plain_store")]
    [InlineData("static_method", "read")]
    [InlineData("static_method", "plain_store")]
    [InlineData("nested_def", "read")]
    [InlineData("nested_def", "plain_store")]
    public void Host_IsCoveredByTheWalk(string host, string use)
    {
        var source = HostUse(host, use);
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(
            $"the {host} host is a function-like scope, so the rule reaches it.\n{Report(result)}");

        var expected = use == "read" ? UnresolvedCode : BareStoreCode;
        Codes(result).Should().Contain(expected,
            $"the {host} host reports {expected}, not an internal compiler error.\n{Report(result)}");
        Codes(result).Should().NotContain("SPY0908",
            $"the {host} host must refuse by name rather than reach the emitter.\n{Report(result)}");
        Codes(result).Should().NotContain("SPY0909",
            $"the {host} host must refuse by name rather than reach the emitter.\n{Report(result)}");
    }

    // ════════════════════════════════════════════════════════════════════════════════════
    // Part 4 — controls that must keep working
    // ════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Control_ModuleVariableReadFromAMethod_StillBinds()
    {
        var source = @"
top: int = 5

class C:
    def m(self) -> int:
        return top

def main() -> None:
    print(C().m())
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(Report(result));
        result.StandardOutput.Should().Contain("5");
    }

    [Fact]
    public void Control_ShadowIdiom_DeclaresALocal()
    {
        var source = @"
class Counter:
    count: int = 0

    def m(self) -> None:
        count: int = 7
        print(count)

def main() -> None:
    Counter().m()
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(Report(result));
        result.StandardOutput.Should().Contain("7");
    }

    /// <summary>
    /// The module variable wins over a same-named class attribute — Python's answer — and the
    /// EMITTED access must reach it. A bare name in the generated C# method body binds the field,
    /// so the checker records the crossing and codegen qualifies (#1786; python3 prints 99).
    /// </summary>
    [Fact]
    public void Control_ModuleVariableShadowedByAClassAttribute_BindsTheModuleVariable()
    {
        var source = @"
v: int = 99

class C:
    v: int = 1

    def m(self) -> int:
        return v

def main() -> None:
    print(C().m())
";
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(
            "the module variable binds — Python resolves the module value too");

        var crossings = CrossingIdentifiers(result);
        crossings.Should().Contain("v",
            "the read bound the module variable after crossing a class body that declares the same "
            + "name; a bare name in the emitted C# method body would bind the FIELD, so the "
            + "crossing is recorded for codegen to qualify (#1786, SemanticInfo.MergeFrom).");
    }

    /// <summary>
    /// Positive control for the fact above: with no class attribute of that name, nothing crosses
    /// and nothing is recorded, so the fact cannot be a constant true.
    /// </summary>
    [Fact]
    public void Control_ModuleVariableWithNoClassAttribute_RecordsNoCrossing()
    {
        var source = @"
v: int = 99

class C:
    other: int = 1

    def m(self) -> int:
        return v

def main() -> None:
    print(C().m())
";
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue();
        CrossingIdentifiers(result).Should().BeEmpty(
            "no class body declares 'v', so the read crosses nothing and codegen must not qualify");
    }

    /// <summary>Every identifier the checker marked as a module access that crosses a class member.</summary>
    /// <summary>
    /// How many nodes of any kind the checker marked as a module access crossing a class member.
    /// Counted over every node, not only identifiers: a walrus has no target identifier node, so
    /// its store keys on the WalrusExpression itself.
    /// </summary>
    private static int MarkedNodeCount(CompilationResult result)
    {
        var info = result.SemanticInfo!;
        int count = 0;
        foreach (var node in AllNodes(result.Module!))
        {
            if (node is Sharpy.Compiler.Parser.Ast.Expression expression
                && info.ModuleAccessCrossesClassMember(expression))
            {
                count++;
            }
        }
        return count;
    }

    private static System.Collections.Generic.IEnumerable<Sharpy.Compiler.Parser.Ast.Node> AllNodes(
        Sharpy.Compiler.Parser.Ast.Node node)
    {
        yield return node;
        foreach (var child in node.GetChildNodes())
        {
            foreach (var nested in AllNodes(child))
                yield return nested;
        }
    }

    private static System.Collections.Generic.List<string> CrossingIdentifiers(CompilationResult result)
    {
        var info = result.SemanticInfo!;
        var names = new System.Collections.Generic.List<string>();
        foreach (var identifier in AllIdentifiers(result.Module!))
        {
            if (info.ModuleAccessCrossesClassMember(identifier))
                names.Add(identifier.Name);
        }
        return names;
    }

    private static System.Collections.Generic.IEnumerable<Sharpy.Compiler.Parser.Ast.Identifier> AllIdentifiers(
        Sharpy.Compiler.Parser.Ast.Node node)
    {
        if (node is Sharpy.Compiler.Parser.Ast.Identifier id)
            yield return id;

        foreach (var child in node.GetChildNodes())
        {
            foreach (var nested in AllIdentifiers(child))
                yield return nested;
        }
    }

    [Fact]
    public void Control_ParameterDefaultNamingAClassConst_StillBinds()
    {
        var source = @"
class Grid:
    const SIZE: int = 8

    def resize(self, n: int = SIZE) -> None:
        print(n)

def main() -> None:
    Grid().resize()
";
        var result = CompileAndExecute(source);
        Codes(result).Should().NotContain(UnresolvedCode,
            $"a parameter default is resolved in the DECLARING scope — the class body — exactly as "
            + $"C# resolves a signature, so the class const still binds.\n{Report(result)}");
    }

    [Fact]
    public void Control_ReturnAnnotationNamingANestedType_StillBinds()
    {
        var source = @"
class C:
    enum Color:
        RED = 1

    def make(self) -> Color:
        return Color.RED

def main() -> None:
    print(C().make().name)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"an annotation is resolved as a TYPE, and the rule is variable-only.\n{Report(result)}");
        result.StandardOutput.Should().Contain("RED");
    }

    [Fact]
    public void Control_SelfSteerOnAnInheritedField_Compiles()
    {
        var source = @"
class Base:
    v: int = 5

class Derived(Base):
    def m(self) -> int:
        return self.v

def main() -> None:
    print(Derived().m())
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"the steer offered for an inherited field must be one that compiles.\n{Report(result)}");
        result.StandardOutput.Should().Contain("5");
    }

    /// <summary>
    /// A nested class's method naming the OUTER class's instance field gets the explanation and NO
    /// steer: neither <c>self.v</c> (self is the inner class) nor <c>Outer.v</c> (an instance field
    /// through a type name is SPY0290) compiles there.
    /// </summary>
    [Fact]
    public void Control_OuterClassInstanceField_GetsNoSteerBecauseNoneCompiles()
    {
        var source = @"
class Outer:
    v: int = 1

    class Inner:
        def m(self) -> int:
            return v

def main() -> None:
    print(Outer.Inner().m())
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(Report(result));
        Codes(result).Should().Contain(UnresolvedCode, Report(result));
        Errors(result).Should().Contain(e => e.Contains("class attribute of 'Outer'"), Report(result));
        Errors(result).Should().NotContain(e => e.Contains("self.v") || e.Contains("Outer.v"),
            $"neither spelling compiles from a nested class, so neither is offered.\n{Report(result)}");
    }

    // ════════════════════════════════════════════════════════════════════════════════════
    // Part 4b — the signature-position axis: the two default kinds resolve in DIFFERENT scopes
    // ════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// An EAGER default (<c>=</c>) is resolved in the scope that declares the function, so it sees
    /// the class body and NOT the signature's own earlier parameters. A LATE-BOUND default
    /// (<c>=&gt;</c>, PEP 671) is evaluated at call time inside the body, so it sees the earlier
    /// parameters and the class body is invisible to it exactly as it is to any other body position.
    /// </summary>
    /// <remarks>
    /// Collapsing the two kinds breaks one column or the other, which is why both are rows here.
    /// Measured by direction against BASE (646b9cf08): the eager sibling cell was SPY0401 and is
    /// SPY0200, a refusal either way; the late-bound sibling cell printed 7 and still does; the two
    /// late-bound class-member cells were SPY0909 internal errors and are now named refusals.
    /// </remarks>
    [Theory]
    [InlineData("eager, sibling parameter", "def f(x: int = 1, y: int = x) -> int:\n    return y", UnresolvedCode)]
    [InlineData("late-bound, class const", "CLASS_CONST", UnresolvedCode)]
    [InlineData("late-bound, class field", "CLASS_FIELD", UnresolvedCode)]
    public void SignaturePosition_DefaultKind_ResolvesInItsOwnScope(
        string cell, string shape, string expectedCode)
    {
        var source = shape switch
        {
            "CLASS_CONST" => @"
class C:
    const K: int = 3

    def m(self, n: int => K) -> None:
        print(n)

def main() -> None:
    C().m()
",
            "CLASS_FIELD" => @"
class C:
    v: int = 3

    def m(self, n: int => v) -> None:
        print(n)

def main() -> None:
    C().m()
",
            _ => "\n" + shape.Replace("\\n", "\n") + @"

def main() -> None:
    print(f())
",
        };

        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"[{cell}]\n{source}\n{Report(result)}");
        Codes(result).Should().Contain(expectedCode, $"[{cell}]\n{Report(result)}");
        Codes(result).Should().NotContain("SPY0909",
            $"[{cell}] a name in a default is refused by name, never by the emitter\n{Report(result)}");
    }

    /// <summary>
    /// The late-bound default's positive control, and the cell that fails if the two default kinds
    /// are collapsed onto the declaring scope: <c>y</c>'s default names the earlier parameter
    /// <c>x</c>, which is the entire point of the form.
    /// </summary>
    [Fact]
    public void SignaturePosition_LateBoundDefault_SeesTheEarlierParameter()
    {
        var result = CompileAndExecute(@"
def f(x: int, y: int => x + 1) -> int:
    return x + y

def main() -> None:
    print(f(3))
");
        result.Success.Should().BeTrue(
            $"a late-bound default is evaluated at call time, in the body's scope\n{Report(result)}");
        result.StandardOutput.Should().Contain("7");
    }

    // ════════════════════════════════════════════════════════════════════════════════════
    // Part 4c — the module-shadow crossing is recorded at every position, not only the read
    // ════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A bare STORE to a name a class body shadows writes through to the MODULE variable
    /// (variable_scoping.md, Write-Through Assignment), and a bare name in the emitted C# would
    /// write the field's storage instead. Every store form records the crossing, so codegen can
    /// qualify each of them.
    /// </summary>
    [Theory]
    [InlineData("plain", "        v = 5")]
    [InlineData("augmented", "        v += 1")]
    [InlineData("walrus", "        w: int = (v := 7)\n        print(w)")]
    [InlineData("tuple element", "        v, k = 5, 1\n        print(k)")]
    public void ModuleShadowCrossing_IsRecordedAtEveryStoreForm(string form, string body)
    {
        var source = @"
v: int = 99

class C:
    v: int = 1

    def store(self) -> None:
" + body.Replace("\\n", "\n") + @"

def main() -> None:
    C().store()
";
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(
            $"[{form}] the store writes through to the module variable\n"
            + string.Join("\n", result.Diagnostics.GetAll().Select(d => $"{d.Code}: {d.Message}")));

        MarkedNodeCount(result).Should().BeGreaterThan(0,
            $"[{form}] the crossing must be recorded on the store's own node; the emitter has no "
            + "other way to know the bare name means the module variable (#1786)");
    }

    /// <summary>
    /// Positive control for the store recording: with no class attribute of that name, no store
    /// form records anything, so the fact cannot be a constant true.
    /// </summary>
    [Theory]
    [InlineData("plain", "        v = 5")]
    [InlineData("augmented", "        v += 1")]
    [InlineData("walrus", "        w: int = (v := 7)\n        print(w)")]
    [InlineData("tuple element", "        v, k = 5, 1\n        print(k)")]
    public void ModuleStoreWithNoClassAttribute_RecordsNoCrossing(string form, string body)
    {
        var source = @"
v: int = 99

class C:
    other: int = 1

    def store(self) -> None:
" + body.Replace("\\n", "\n") + @"

def main() -> None:
    C().store()
";
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue();
        MarkedNodeCount(result).Should().Be(0,
            $"[{form}] nothing shadows 'v', so codegen must not qualify the access");
    }

    // ════════════════════════════════════════════════════════════════════════════════════
    // Part 5 — totality anchors, tied to the rows above
    // ════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ties each axis constant to the rows that exercise it. A constant compared to its own literal
    /// cannot fail; these compare against the theories' own InlineData counts, so deleting a row
    /// fails the anchor.
    /// </summary>
    [Fact]
    public void TotalityAnchors_TieAxisSizesToTheRows()
    {
        int useRows = InlineDataCount(nameof(UseForm_OnAnInstanceField_IsRefusedByName));
        int memberRows = InlineDataCount(nameof(MemberKind_IsRefused_WithASteerThatCompiles));
        int hostRows = InlineDataCount(nameof(Host_IsCoveredByTheWalk));

        // Eight use forms are exercised by the use x typing theory; the ninth (pattern head) has
        // its own two facts because a head is not a read and cannot share the assertion.
        (useRows / TypingCount + 1).Should().Be(UseFormCount,
            "every use form in the axis has a row (8 in the theory + the pattern head)");
        useRows.Should().Be((UseFormCount - 1) * TypingCount,
            "the use theory is a full use x typing product");

        // Eight member kinds are refused in BOTH directions (instance field, class const, @static
        // field, property name, inherited field, inherited property, inherited const, inherited
        // @static field — R-AA, #1803); the remaining two are controls with their own facts.
        memberRows.Should().Be(16, "eight member kinds x {read, store}");
        (8 + 2).Should().Be(MemberKindCount,
            "every member kind is covered: eight refused in both directions, the method name and "
            + "the nested type");

        (hostRows / 2).Should().Be(HostCount, "every host has a read row and a store row");

        // The R-AA product: its two arrays are anchored to literals so that deleting an origin or
        // a store form fails here rather than shrinking the product silently.
        MemberOrigins.Length.Should().Be(MemberOriginCount,
            "origins = property, inherited field, inherited property, inherited field two levels up, struct property");
        StoreForms.Length.Should().Be(StoreFormCount,
            "store forms = plain, augmented, walrus, tuple element, ??= — the five R-Y refuses");
        StoreForms.Should().OnlyContain(form => IsStore(form),
            "every store form of the product is one the use axis knows as a store");
        MemberOriginByStoreForm().Count().Should().Be(MemberOriginCount * StoreFormCount,
            "the product is total over its two axes");
    }

    private static int InlineDataCount(string testMethodName)
        => typeof(ScopeChainMatrixTests)
            .GetMethod(testMethodName)!
            .GetCustomAttributes(typeof(InlineDataAttribute), inherit: false)
            .Length;

    // ── Source builders ─────────────────────────────────────────────────────────────────

    private static bool IsStore(string use)
        => use is "plain_store" or "augmented_store" or "walrus" or "tuple_element" or "coalesce_store";

    private static string InstanceFieldUse(string use, string typing)
    {
        // The mistyped column stores (or reads into) the WRONG type. The rule refuses the name
        // before the type question, so both columns report the same code -- which is the point:
        // a mistyped store must not be reported as a type error against a local that never existed.
        string fieldType = use == "coalesce_store" ? "int?" : "int";
        string body = (use, typing) switch
        {
            ("read", "same") => "        n: int = v\n        print(n)",
            ("read", "mistyped") => "        s: str = v\n        print(s)",
            ("plain_store", "same") => "        v = 1",
            ("plain_store", "mistyped") => "        v = \"x\"",
            ("augmented_store", "same") => "        v += 1",
            ("augmented_store", "mistyped") => "        v += \"x\"",
            ("walrus", "same") => "        print(v := 1)",
            ("walrus", "mistyped") => "        print(v := \"x\")",
            ("tuple_element", "same") => "        v, n = 1, 2\n        print(n)",
            ("tuple_element", "mistyped") => "        v, n = \"x\", 2\n        print(n)",
            ("coalesce_store", "same") => "        v ??= 1",
            ("coalesce_store", "mistyped") => "        v ??= \"x\"",
            ("comprehension_read", "same") => "        xs: list[int] = [v + i for i in [1, 2]]\n        print(len(xs))",
            ("comprehension_read", "mistyped") => "        xs: list[str] = [v for i in [1, 2]]\n        print(len(xs))",
            _ => string.Empty,
        };

        if (use == "lambda_field_initializer_read")
        {
            string lambdaType = typing == "same" ? "() -> int" : "() -> str";
            return $@"
class C:
    v: int = 3
    f: {lambdaType} = lambda: v

def main() -> None:
    print(C().f())
";
        }

        return $@"
class C:
    v: {fieldType} = {(fieldType == "int?" ? "Some(0)" : "0")}

    def m(self) -> None:
{body}

def main() -> None:
    C().m()
";
    }

    private static string MemberKindUse(string member, string use)
    {
        string statement = use == "read" ? "        n: int = v\n        print(n)" : "        v = 1";

        return member switch
        {
            "instance_field" => $@"
class C:
    v: int = 0

    def m(self) -> None:
{statement}

def main() -> None:
    C().m()
",
            "class_const" => $@"
class C:
    const v: int = 3

    def m(self) -> None:
{statement}

def main() -> None:
    C().m()
",
            "static_field" => $@"
class C:
    @static
    v: int = 3

    def m(self) -> None:
{statement}

def main() -> None:
    C().m()
",
            "property_name" => $@"
class C:
    property get v(self) -> int:
        return 1

    def m(self) -> None:
{statement}

def main() -> None:
    C().m()
",
            // R-AA (#1803): the four inherited kinds mirror the four own kinds above, one class down.
            "inherited_field" => $@"
class Base:
    v: int = 0

class C(Base):
    def m(self) -> None:
{statement}

def main() -> None:
    C().m()
",
            "inherited_property" => $@"
class Base:
    property get v(self) -> int:
        return 1

class C(Base):
    def m(self) -> None:
{statement}

def main() -> None:
    C().m()
",
            "inherited_const" => $@"
class Base:
    const v: int = 3

class C(Base):
    def m(self) -> None:
{statement}

def main() -> None:
    C().m()
",
            "inherited_static_field" => $@"
class Base:
    @static
    v: int = 3

class C(Base):
    def m(self) -> None:
{statement}

def main() -> None:
    C().m()
",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// One program per (origin, store form) cell of the R-AA product. The member is <c>int?</c> so
    /// the <c>??=</c> form is admissible, and every store form is the spelling the use axis uses.
    /// </summary>
    private static string MemberOriginStore(string origin, string form)
    {
        string store = form switch
        {
            "plain_store" => "v = Some(1)",
            "augmented_store" => "v += 1",
            "walrus" => "print(v := Some(1))",
            "tuple_element" => "v, n = Some(1), 2\n        print(n)",
            "coalesce_store" => "v ??= Some(1)",
            _ => throw new ArgumentOutOfRangeException(nameof(form)),
        };

        string method = $@"
    def m(self) -> None:
        {store}
";
        return origin switch
        {
            "property" => $@"
class C:
    property get v(self) -> int?:
        return Some(0)
{method}
def main() -> None:
    C().m()
",
            "inherited_field" => $@"
class Base:
    v: int? = Some(0)

class C(Base):
{method}
def main() -> None:
    C().m()
",
            "inherited_property" => $@"
class Base:
    property get v(self) -> int?:
        return Some(0)

class C(Base):
{method}
def main() -> None:
    C().m()
",
            "inherited_field_two_levels" => $@"
class Root:
    v: int? = Some(0)

class Mid(Root):
    pass

class C(Mid):
{method}
def main() -> None:
    C().m()
",
            "struct_property" => $@"
struct S:
    property get v(self) -> int?:
        return Some(0)
{method}
def main() -> None:
    s: S = S()
    s.m()
",
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
    }

    private static string HostUse(string host, string use)
    {
        // Body lines at method-body depth (8 spaces) and at accessor depth (8 spaces); the nested
        // def needs one level more.
        string body = use == "read" ? "n: int = v\n        print(n)" : "v = 1";
        string nestedBody = use == "read" ? "n: int = v\n            print(n)" : "v = 1";

        return host switch
        {
            "class_method" => $@"
class C:
    v: int = 0

    def m(self) -> None:
        {body}

def main() -> None:
    C().m()
",
            "struct_method" => $@"
struct S:
    v: int = 0

    def m(self) -> None:
        {body}

def main() -> None:
    s: S = S()
    s.m()
",
            "property_get" => $@"
class C:
    v: int = 0

    property get w(self) -> int:
        {body}
        return 1

def main() -> None:
    print(C().w)
",
            "property_set" => $@"
class C:
    v: int = 0
    backing: int = 0

    property get w(self) -> int:
        return self.backing

    property set w(self, value: int) -> None:
        {body}

def main() -> None:
    c: C = C()
    c.w = 1
",
            "event_accessor" => $@"
delegate Handler() -> None

class Bus:
    v: int = 0
    handlers: list[Handler] = []

    event add on_ping(self, handler: Handler):
        {body}

    event remove on_ping(self, handler: Handler):
        self.handlers.remove(handler)

def main() -> None:
    print(1)
",
            "lambda_in_method" => $@"
class C:
    v: int = 0

    def m(self) -> None:
        f: () -> None = lambda: {(use == "read" ? "print(v)" : "print(v := 1)")}
        f()

def main() -> None:
    C().m()
",
            "static_method" => $@"
class C:
    v: int = 0

    @static
    def m() -> None:
        {body}

def main() -> None:
    C.m()
",
            "nested_def" => $@"
class C:
    v: int = 0

    def m(self) -> None:
        def inner() -> None:
            {nestedBody}
        inner()

def main() -> None:
    C().m()
",
            _ => string.Empty,
        };
    }

    private static System.Collections.Generic.List<string> Errors(ExecutionResult result)
        => result.CompilationErrors;

    /// <summary>
    /// The diagnostic CODES the compilation produced. <c>CompilationErrors</c> holds message text
    /// only, so asserting a code against it would pass on any message that happened to quote the
    /// number and fail on every message that does not.
    /// </summary>
    private static System.Collections.Generic.List<string> Codes(ExecutionResult result)
        => result.RawDiagnostics.Select(d => d.Code ?? string.Empty).ToList();

    private static string Report(ExecutionResult result)
        => "errors: " + string.Join(" | ", result.CompilationErrors)
           + "\ncodes: " + string.Join(",", Codes(result))
           + "\nwarnings: " + string.Join(" | ", result.CompilationWarnings)
           + "\nstdout: " + result.StandardOutput;
}
