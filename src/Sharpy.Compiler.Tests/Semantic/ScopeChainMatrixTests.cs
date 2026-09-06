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
/// signature position (a parameter default) still resolves in the declaring scope.</para>
///
/// <para><b>Axes.</b> Use (9) x member (6) x host (8) x typing (2). The full product is not
/// enumerated; the plan's rule is to vary one axis at a time so agreement on one axis is not read
/// as evidence for another: use x typing at one host and member, member x use at one host, host x
/// use at one member. Every cell executes.</para>
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
    private const int MemberKindCount = 6;
    private const int HostCount = 8;
    private const int TypingCount = 2;

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
    /// The same head with an int-typed constant. Asserted on the BINDING rather than on execution:
    /// whether an integer class const emits as a C# <c>const</c> is the const-eligibility fact
    /// (plan-202526), not this rule's.
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
    /// A bare STORE to a property's name declares a local, as it did before the rule.
    /// </summary>
    /// <remarks>
    /// R-Y refuses a store to a name declared in the class BODY, and a property is not in that
    /// scope — it lives on the type symbol. The program below printed 7 at BASE (646b9cf08) and
    /// still does: refusing it would turn a working program red for a rule that was never about
    /// it. The READ arm still names the property, because a bare read was already unresolved and
    /// only the message improves. The inherited-field store is the same shape, one row down.
    /// </remarks>
    [Theory]
    [InlineData("property")]
    [InlineData("inherited field")]
    public void MemberNotInTheClassBody_BareStore_StillDeclaresALocal(string kind)
    {
        var source = kind == "property"
            ? @"
class C:
    property get v(self) -> int:
        return 1

    def m(self) -> None:
        v = 7
        print(v)

def main() -> None:
    C().m()
"
            : @"
class Base:
    v: int = 5

class Derived(Base):
    def m(self) -> None:
        v = 7
        print(v)

def main() -> None:
    Derived().m()
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"[{kind}] this printed 7 at BASE and must keep doing so\n{Report(result)}");
        result.StandardOutput.Should().Contain("7");
    }

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

        // Three member kinds are refused in BOTH directions (instance field, class const, @static
        // field) plus the property name's read; the remaining two are controls with their own facts.
        memberRows.Should().Be(7,
            "three member kinds x {read, store} plus the property name's read");
        (3 + 1 + 2).Should().Be(MemberKindCount,
            "every member kind is covered: three refused in both directions, the property name "
            + "(read refused, store declares a local), the method name and the nested type");

        (hostRows / 2).Should().Be(HostCount, "every host has a read row and a store row");
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
            _ => string.Empty,
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
