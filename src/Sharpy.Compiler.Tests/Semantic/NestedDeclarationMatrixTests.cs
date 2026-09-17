using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Tests.Infrastructure;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The host × kind × route × use acceptance matrix for nested type-declaring statements
/// (#1729, R-I, P6 Phase 1 Task T). Every cell either <b>executes and asserts a discriminating
/// printed value</b> (one python3 also produces) or, for a host×kind×use the compiler refuses,
/// asserts the exact named diagnostic — the matrix never asserts success for a broken cell.
///
/// <para>Axes covered:
/// <list type="bullet">
///   <item><b>host</b>: class, struct, interface, generic class;</item>
///   <item><b>kind</b>: class, struct, interface, enum, union, delegate, alias, event, dataclass;</item>
///   <item><b>route</b>: single-file (<see cref="IntegrationTestBase.CompileAndExecute"/>) and
///         cross-module two-file (<see cref="ProjectCompilationHelper"/>);</item>
///   <item><b>use</b>: qualified from outside (<c>Outer.Shape.Circle(…)</c>), bare inside the host,
///         <c>match</c> inside the host, as a field type, and as a method parameter/return type.</item>
/// </list></para>
///
/// <para>Two cell classes are rostered to open bugs rather than made to pass, so the matrix does
/// not lie and each cell flips to a green execution assertion when its bug lands:
/// <list type="bullet">
///   <item><b>[BUG(#1894)]</b> — a bare nested-type reference used AS A TYPE ANNOTATION inside a
///         <i>generic</i> host emits <c>Box.Inner</c> dropping the host's <c>&lt;T&gt;</c> → CS0305
///         (pre-existing, reproduces for a nested class too). The DECLARATION cells in a generic host
///         DO run (nested type not referenced) and are asserted green; only the reference cells fail.</item>
///   <item><b>out-of-scope #1729 (events)</b> — a nested <c>event</c> invocation is SPY0230
///         (<c>Action | None</c> not callable, probe n05); out of scope for this plan.</item>
/// </list></para>
///
/// <para>A <c>union</c> with DATA cases nested in an <i>interface</i> host once dropped every case
/// field (#1895): <c>CheckInterface</c> enumerated only method/property/const members and never
/// type-checked nested type declarations, so <c>CheckUnion</c> never populated the case fields. Fixed
/// by routing nested type declarations through the classifier in <c>CheckInterface</c>; the cells now
/// assert execution alongside their class/struct positive controls.</para>
///
/// <para>The final fact is a <b>scan-vs-roster totality assertion</b>: it reflects over the seven
/// switch arms of the nested-declaration classifier <c>TryGetNestedDeclaration</c> in
/// <c>StatementExtensions.cs</c> and asserts they equal a LITERAL roster of the seven type-declaring
/// AST kinds (not derived from the same enum — the totality-count lesson). This makes the matrix a
/// valid <c>guarded-by:NestedDeclarationMatrixTests</c> guard for the classifier's
/// <c>DispatchSiteInventoryTests</c> row: the citation validator requires the guarded test's source
/// to scan the site.</para>
/// </summary>
public class NestedDeclarationMatrixTests : IntegrationTestBase
{
    public NestedDeclarationMatrixTests(ITestOutputHelper output) : base(output) { }

    // ═══════════════════════════════════════════════════════════════════════
    // Execution cells: single-file, host × kind × use — each runs and prints a
    // discriminating value python3 also produces (verified with python3 -c).
    // ═══════════════════════════════════════════════════════════════════════

    public static IEnumerable<object[]> ExecutionCells()
    {
        // ---- class host ----

        // class in class (n16) — construct + field read. python3: 11
        yield return Cell("class_in_class", """
            class Outer:
                @public
                class Inner:
                    v: int
                    def __init__(self, v: int):
                        self.v = v

            def main():
                i: Outer.Inner = Outer.Inner(11)
                print(i.v)
            """, "11\n");

        // struct in class — construct + method. python3: 3+4 = 7
        yield return Cell("struct_in_class", """
            class Geometry:
                @public
                struct Point:
                    x: int
                    y: int
                    def __init__(self, x: int, y: int):
                        self.x = x
                        self.y = y
                    def total(self) -> int:
                        return self.x + self.y

            def main():
                p: Geometry.Point = Geometry.Point(3, 4)
                print(p.total())
            """, "7\n");

        // interface in class — implement + call across the nested interface.
        yield return Cell("interface_in_class", """
            class Animal:
                @public
                interface Speakable:
                    def speak(self) -> str:
                        ...

                @public
                class Dog(Animal.Speakable):
                    def speak(self) -> str:
                        return "Woof"

            def main():
                d: Animal.Dog = Animal.Dog()
                print(d.speak())
            """, "Woof\n");

        // enum in class — parameter type + comparison. python3: True
        yield return Cell("enum_in_class", """
            class TrafficLight:
                @public
                enum State:
                    Red = 1
                    Green = 3

            def is_green(s: TrafficLight.State) -> bool:
                return s == TrafficLight.State.Green

            def main():
                print(is_green(TrafficLight.State.Green))
            """, "True\n");

        // union in class (n01/n11) — qualified construct from outside + match inside a method.
        // python3: 3*3 = 9
        yield return Cell("union_in_class", """
            class Outer:
                @public
                union Shape:
                    case Circle(radius: int)
                    case Square(side: int)

                @public
                def area(self, s: Outer.Shape) -> int:
                    match s:
                        case Outer.Shape.Circle(r):
                            return r * r
                        case Outer.Shape.Square(side):
                            return side * side

            def main():
                o: Outer = Outer()
                print(o.area(Outer.Shape.Circle(3)))
            """, "9\n");

        // delegate in class (n02/n14) — as a field type + invoked. python3: 4+5 = 9
        yield return Cell("delegate_in_class", """
            class Outer:
                delegate Op(a: int, b: int) -> int

                handler: Outer.Op

                def __init__(self, h: Outer.Op):
                    self.handler = h

                def apply(self, x: int, y: int) -> int:
                    return self.handler(x, y)

            def add(a: int, b: int) -> int:
                return a + b

            def main():
                o: Outer = Outer(add)
                print(o.apply(4, 5))
            """, "9\n");

        // alias in class, bare inside (n13) — the type_aliases.md §Class-level aliases form.
        // python3: ['a']
        yield return Cell("alias_in_class_bare", """
            class Box:
                type Names = list[str]

                def make(self) -> Names:
                    n: Names = ['a']
                    return n

            def main():
                print(Box().make())
            """, "['a']\n");

        // alias in class, qualified from outside (n03). python3: [1, 2, 3]
        yield return Cell("alias_in_class_qualified", """
            class Box:
                type Ints = list[int]

                def make(self) -> Box.Ints:
                    xs: Box.Ints = [1, 2, 3]
                    return xs

            def main():
                b: Box = Box()
                print(b.make())
            """, "[1, 2, 3]\n");

        // dataclass in class (n06) — a decorated nested class runs. python3: 42
        yield return Cell("dataclass_in_class", """
            class Registry:
                @dataclass
                class Entry:
                    key: str
                    value: int

                def make(self) -> int:
                    e: Registry.Entry = Registry.Entry("k", 42)
                    return e.value

            def main():
                print(Registry().make())
            """, "42\n");

        // ---- struct host ----

        // union in struct (n07) — construct + match through a free function. python3: 7, then len("hey") = 3
        yield return Cell("union_in_struct", """
            struct Container:
                @public
                union Tag:
                    case A(n: int)
                    case B(s: str)

                label: int
                def __init__(self, label: int):
                    self.label = label

            def classify(t: Container.Tag) -> int:
                match t:
                    case Container.Tag.A(n):
                        return n
                    case Container.Tag.B(s):
                        return len(s)

            def main():
                print(classify(Container.Tag.A(7)))
                print(classify(Container.Tag.B("hey")))
            """, "7\n3\n");

        // enum in struct — parameter type + comparison. python3: True, False
        yield return Cell("enum_in_struct", """
            struct Cfg:
                @public
                enum Mode:
                    Fast = 1
                    Slow = 2

            def is_fast(mode: Cfg.Mode) -> bool:
                return mode == Cfg.Mode.Fast

            def main():
                print(is_fast(Cfg.Mode.Fast))
                print(is_fast(Cfg.Mode.Slow))
            """, "True\nFalse\n");

        // struct in struct — nested struct declaration + construct. python3: 5
        yield return Cell("struct_in_struct", """
            struct Outer:
                @public
                struct Inner:
                    v: int
                    def __init__(self, v: int):
                        self.v = v

                tag: int
                def __init__(self, tag: int):
                    self.tag = tag

            def main():
                i: Outer.Inner = Outer.Inner(5)
                print(i.v)
            """, "5\n");

        // ---- interface host ----

        // union in interface, UNIT cases (n08) — the interface member path handles unit-only cases.
        // (data cases are #1895, below.) python3: on
        yield return Cell("union_in_interface_unit", """
            interface IState:
                @public
                union S:
                    case On
                    case Off

            def name(s: IState.S) -> str:
                match s:
                    case IState.S.On():
                        return "on"
                    case IState.S.Off():
                        return "off"

            def main():
                print(name(IState.S.On()))
            """, "on\n");

        // delegate in interface (n08b) — as a value slot + invoked. python3: 2+3 = 5
        yield return Cell("delegate_in_interface", """
            interface IEvents:
                delegate Op(a: int, b: int) -> int

            def add(a: int, b: int) -> int:
                return a + b

            def main():
                f: IEvents.Op = add
                print(f(2, 3))
            """, "5\n");

        // alias in interface, qualified from outside (n09). python3: [1, 2]
        yield return Cell("alias_in_interface_qualified", """
            interface IReg:
                type Ints = list[int]

            def main():
                xs: IReg.Ints = [1, 2]
                print(xs)
            """, "[1, 2]\n");

        // alias in interface, bare inside (n09b — was SPY0202, now the interface scope registers it).
        // python3: [3, 4]
        yield return Cell("alias_in_interface_bare", """
            interface IReg:
                type Ints = list[int]

                def make(self) -> Ints:
                    ...

            class Impl(IReg):
                def make(self) -> IReg.Ints:
                    return [3, 4]

            def main():
                i: IReg = Impl()
                print(i.make())
            """, "[3, 4]\n");

        // struct in interface (n12 control). python3: 3+4 = 7
        yield return Cell("struct_in_interface", """
            interface IHolder:
                @public
                struct Coord:
                    x: int
                    y: int
                    def __init__(self, x: int, y: int):
                        self.x = x
                        self.y = y

            def sum_coord(c: IHolder.Coord) -> int:
                return c.x + c.y

            def main():
                print(sum_coord(IHolder.Coord(3, 4)))
            """, "7\n");

        // ---- generic class host: DECLARATION cells ----
        // A nested union/class DECLARED in a generic host emits correctly and the host runs, as long
        // as the nested type is not REFERENCED in a <T>-dropping way (that reference is #1894, below).
        // These cells prove the declaration path works in a generic host (n17's declaration half).

        // union declared in a generic host (n17 decl). python3: 42
        yield return Cell("union_in_generic_declaration", """
            class Cell[T]:
                value: T
                def __init__(self, value: T):
                    self.value = value

                union State:
                    case Full(n: int)
                    case Empty

                def get(self) -> T:
                    return self.value

            def main():
                c: Cell[int] = Cell[int](42)
                print(c.get())
            """, "42\n");

        // class declared in a generic host. python3: 42
        yield return Cell("class_in_generic_declaration", """
            class Box[T]:
                value: T
                def __init__(self, value: T):
                    self.value = value

                class Inner:
                    tag: int
                    def __init__(self, tag: int):
                        self.tag = tag

                def get(self) -> T:
                    return self.value

            def main():
                b: Box[int] = Box[int](42)
                print(b.get())
            """, "42\n");
    }

    [Theory]
    [MemberData(nameof(ExecutionCells))]
    public void NestedDeclaration_ExecutionCell_PrintsDiscriminatingValue(
        string id, string source, string expectedStdout)
    {
        Output.WriteLine($"[execution] {id}");
        var result = CompileAndExecute(source);
        Assert.True(result.Success,
            $"{id}: expected to compile and run, but failed: "
            + string.Join(" | ", result.CompilationErrors));
        Assert.Equal(expectedStdout, result.StandardOutput);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Cross-module cells (route 2): the nested type is declared in one .spy and
    // used in another through `from <mod> import Outer`, built and run through
    // ProjectCompilationHelper.
    // ═══════════════════════════════════════════════════════════════════════

    public static IEnumerable<object[]> CrossModuleCells()
    {
        // union in a class, cross-module qualified construct + match. python3: 3*3 = 9, 4*4 = 16
        yield return new object[]
        {
            "union_cross_module",
            "class Outer:\n"
            + "    @public\n"
            + "    union Shape:\n"
            + "        case Circle(radius: int)\n"
            + "        case Square(side: int)\n",
            "from lib import Outer\n"
            + "\n"
            + "def area(s: Outer.Shape) -> int:\n"
            + "    match s:\n"
            + "        case Outer.Shape.Circle(r):\n"
            + "            return r * r\n"
            + "        case Outer.Shape.Square(side):\n"
            + "            return side * side\n"
            + "\n"
            + "def main():\n"
            + "    print(area(Outer.Shape.Circle(3)))\n"
            + "    print(area(Outer.Shape.Square(4)))\n",
            "9\n16\n"
        };

        // delegate in a class, cross-module field-value + invoke. python3: 6+7 = 13
        yield return new object[]
        {
            "delegate_cross_module",
            "class Outer:\n"
            + "    delegate Op(a: int, b: int) -> int\n",
            "from lib import Outer\n"
            + "\n"
            + "def add(a: int, b: int) -> int:\n"
            + "    return a + b\n"
            + "\n"
            + "def main():\n"
            + "    f: Outer.Op = add\n"
            + "    print(f(6, 7))\n",
            "13\n"
        };

        // class-level alias, cross-module qualified use. python3: [10, 20]
        yield return new object[]
        {
            "alias_cross_module",
            "class Box:\n"
            + "    type Ints = list[int]\n",
            "from lib import Box\n"
            + "\n"
            + "def main():\n"
            + "    xs: Box.Ints = [10, 20]\n"
            + "    print(xs)\n",
            "[10, 20]\n"
        };
    }

    [Theory]
    [MemberData(nameof(CrossModuleCells))]
    public void NestedDeclaration_CrossModuleCell_PrintsDiscriminatingValue(
        string id, string libSource, string mainSource, string expectedStdout)
    {
        Output.WriteLine($"[cross-module] {id}");
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("NestedDeclMatrix")
            .WithEntryPoint("main.spy")
            .AddSourceFile("lib.spy", libSource)
            .AddSourceFile("main.spy", mainSource)
            .CreateProjectFile();

        var result = helper.CompileAndExecute();
        Assert.True(result.Success,
            $"{id}: expected to compile and run, but failed: "
            + string.Join(" | ", result.CompilationErrors));
        Assert.Equal(expectedStdout, result.StandardOutput);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // [BUG(#1894)] generic-host nested-type REFERENCE cells. A bare nested-type
    // name used as a type annotation inside a generic host emits `Host.Nested`
    // dropping the host's <T> → CS0305. These assert the CURRENT failing behavior
    // so the matrix is honest; each flips to a green execution assertion when
    // #1894 lands (the emitter carries the enclosing generic args).
    // ═══════════════════════════════════════════════════════════════════════

    public static IEnumerable<object[]> Bug1894Cells()
    {
        // nested CLASS referenced as a method-parameter annotation inside a generic host (#1894 repro).
        yield return Cell("bug1894_nested_class_param", """
            class Box[T]:
                @public
                class Inner:
                    tag: int
                    def __init__(self, tag: int):
                        self.tag = tag

                @public
                def describe(self, c: Inner) -> int:
                    return c.tag

            def main():
                b: Box[int] = Box[int]()
                print("built")
            """, "CS0305");

        // nested UNION referenced as a method-parameter annotation inside a generic host
        // (n17's referencing half). Same CS0305 root; NOT union-specific.
        yield return Cell("bug1894_nested_union_param", """
            class Cell[T]:
                @public
                union State:
                    case Full(value: int)
                    case Empty

                @public
                def check(self, s: State) -> int:
                    match s:
                        case State.Full(v):
                            return v
                        case State.Empty():
                            return 0

            def main():
                c: Cell[int] = Cell[int]()
                print("built")
            """, "CS0305");
    }

    [Theory]
    [MemberData(nameof(Bug1894Cells))]
    public void NestedDeclaration_GenericHostReference_IsBlockedBy1894(
        string id, string source, string expectedDiagnostic)
    {
        // BUG(#1894): drains to a green execution assertion when #1894 lands. Until then the
        // generic host emits `Host.Nested` without <T> → CS0305 behind SPY0908.
        Output.WriteLine($"[BUG(#1894)] {id}");
        var result = CompileAndExecute(source);
        AssertFailsWith(result, id, expectedDiagnostic);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // A union with DATA cases nested in an interface host: its case fields are
    // populated by CheckUnion, which CheckInterface now reaches (#1895, #1729).
    // The unit-case control (union_in_interface_unit, above) and the class/struct
    // hosts (union_in_class, union_in_struct) are the positive controls.
    // ═══════════════════════════════════════════════════════════════════════

    public static IEnumerable<object[]> UnionDataCaseInInterfaceCells()
    {
        // construction of a data case in an interface-nested union (was CS1729: the case class had
        // only a zero-arg constructor). Construct Flat(9), extract its field by match. python3: 9
        yield return Cell("union_interface_data_construct", """
            interface IShape:
                @public
                union Kind:
                    case Round(r: int)
                    case Flat(w: int)

            def main():
                k: IShape.Kind = IShape.Kind.Flat(9)
                match k:
                    case IShape.Kind.Round(r):
                        print(r)
                    case IShape.Kind.Flat(w):
                        print(w)
            """, "9\n");

        // positional match on a data case (was SPY0367: type 'Round' had 0 fields). python3: 5
        yield return Cell("union_interface_data_match", """
            interface IShape:
                @public
                union Kind:
                    case Round(r: int)
                    case Flat(w: int)

            def area(k: IShape.Kind) -> int:
                match k:
                    case IShape.Kind.Round(r):
                        return r
                    case IShape.Kind.Flat(w):
                        return w

            def main():
                print(area(IShape.Kind.Round(5)))
            """, "5\n");
    }

    [Theory]
    [MemberData(nameof(UnionDataCaseInInterfaceCells))]
    public void NestedDeclaration_UnionDataCaseInInterface_Runs(
        string id, string source, string expectedStdout)
    {
        // A union with data cases nested in an interface runs: CheckInterface type-checks the nested
        // union so CheckUnion populates the case fields, and the emitter's interface arm carries them
        // (construction + positional match), exactly as the class/struct hosts do (#1895, #1729).
        Output.WriteLine($"[execution] {id}");
        var result = CompileAndExecute(source);
        Assert.True(result.Success,
            $"{id}: expected to compile and run, but failed: "
            + string.Join(" | ", result.CompilationErrors));
        Assert.Equal(expectedStdout, result.StandardOutput);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Out-of-scope #1729 (events, plan Out-of-scope note / probe n05): a nested
    // event invocation is SPY0230 (the handler slot is `Notify | None`, not
    // callable). Rostered asserting current behavior — NOT made to run here.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void NestedEvent_InvocationRefused_IsOutOfScope_n05()
    {
        // out-of-scope #1729 (events, see plan Out-of-scope note): self.changed() on `Notify | None`
        // is SPY0230. This is not a nested-declaration classifier defect and is not made to run here.
        const string source = """
            delegate Notify(msg: str) -> None

            class Publisher:
                event changed: Notify

                def fire(self):
                    self.changed("hi")

            def main():
                print("built")
            """;
        var result = CompileAndExecute(source);
        AssertFailsWith(result, "event_in_class_oos", "SPY0230");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Scan-vs-roster totality: the classifier's seven switch arms equal a LITERAL
    // roster of the seven type-declaring AST kinds. This is what upgrades the
    // DispatchSiteInventoryTests row for
    // Parser/Ast/StatementExtensions.cs::StatementExtensions.TryGetNestedDeclaration
    // to guarded-by:NestedDeclarationMatrixTests — the citation validator requires
    // this test's SOURCE to name StatementExtensions.cs and "TryGetNestedDeclaration".
    // ═══════════════════════════════════════════════════════════════════════

    private const string ClassifierFile = "src/Sharpy.Compiler/Parser/Ast/StatementExtensions.cs";
    private const string ClassifierMethod = "TryGetNestedDeclaration";

    /// <summary>
    /// The seven AST statement kinds a nested declaration can be — written out as a literal roster,
    /// NOT derived from <c>NestedDeclarationKind</c> or from the switch it guards (the totality-count
    /// lesson: a count taken from the same source it checks is vacuous). Removing an arm from the
    /// classifier's switch makes the scanned set unequal to this literal roster → red.
    /// </summary>
    private static readonly IReadOnlySet<string> SevenTypeDeclaringAstKinds = new HashSet<string>
    {
        "ClassDef",
        "StructDef",
        "InterfaceDef",
        "EnumDef",
        "UnionDef",
        "DelegateDef",
        "TypeAlias",
    };

    [Fact]
    public void Classifier_ArmSet_EqualsSevenTypeDeclaringKinds_ByLiteralRoster()
    {
        var scanned = SwitchArmScan.CaseTypeNames(ClassifierFile, ClassifierMethod);

        Output.WriteLine($"scanned {ClassifierMethod} arms: {string.Join(", ", scanned.OrderBy(s => s))}");

        var missing = SevenTypeDeclaringAstKinds.Except(scanned).ToList();
        var extra = scanned.Except(SevenTypeDeclaringAstKinds).ToList();

        Assert.True(missing.Count == 0,
            $"{ClassifierMethod} no longer classifies: {string.Join(", ", missing)} "
            + "(an arm was removed from the classifier switch, or a kind left the type-declaring universe).");
        Assert.True(extra.Count == 0,
            $"{ClassifierMethod} classifies an unrostered kind: {string.Join(", ", extra)} "
            + "(a new type-declaring kind must join the literal roster AND the host×kind matrix cells above).");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static object[] Cell(string id, string source, string expected)
        => new object[] { id, source, expected };

    /// <summary>
    /// Asserts a cell failed to compile/run and that the failure is the expected one, identified
    /// EITHER by a Sharpy diagnostic <c>Code</c> on <see cref="IntegrationTestBase.ExecutionResult.RawDiagnostics"/>
    /// (semantic-time codes like SPY0367 / SPY0230) OR by a substring of the rendered compilation
    /// errors (Roslyn <c>CSxxxx</c> ids carried inside the mapped SPY0908 message).
    /// </summary>
    private static void AssertFailsWith(ExecutionResult result, string id, string expectedDiagnostic)
    {
        Assert.False(result.Success,
            $"{id}: expected the compile/run to fail with {expectedDiagnostic}, but it succeeded "
            + $"(stdout: {result.StandardOutput.Replace("\n", "\\n")}).");

        var byCode = result.RawDiagnostics.Any(d => d.Code == expectedDiagnostic);
        var byText = result.CompilationErrors.Any(e => e.Contains(expectedDiagnostic))
                     || result.RawDiagnostics.Any(d => d.Message.Contains(expectedDiagnostic));

        Assert.True(byCode || byText,
            $"{id}: expected {expectedDiagnostic}, but the failure was:\n  "
            + string.Join("\n  ", result.CompilationErrors)
            + "\n  codes: " + string.Join(", ", result.RawDiagnostics.Select(d => d.Code)));
    }
}
