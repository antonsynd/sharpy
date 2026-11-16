using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Differential;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;
using Harness = Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Stage 0 of plan-775c50 (P77, #2420; verification contract §11). The contract: what a reference to a module value
/// compiles to does not depend on what that value's initializer reads. A module value <c>t</c> is declared after a
/// dependency, its initializer reaches the dependency through one expression node, and <c>t</c> is read, called or
/// passed at one site. Every cell prints the number its python twin prints, or is refused where the site takes
/// constants only.
///
/// Four axes, each the image of an enumeration, so a new value is red until it is generated or excluded with a reason:
/// <list type="bullet">
/// <item>the node the initializer reaches the dependency through: the concrete subclasses of <see cref="Expression"/>;</item>
/// <item>what the dependency is: <see cref="BindingKind"/>;</item>
/// <item>where <c>t</c> is used: <see cref="ReadPosition"/> through the P77a harness's own placement tables, on every
/// host and nesting those tables render, plus the entry point;</item>
/// <item>how <c>t</c> holds its value and is used: the closed sets <see cref="Holder"/> and <see cref="Use"/>.</item>
/// </list>
/// The pool is three sub-products (<see cref="Product"/>), not the full product. Declaration order is fixed:
/// dependency, then <c>t</c>, then the reader. The order axis is the P77a pool's.
///
/// Three more families ride the same sweep, allowlist and ratchet, each in its own file and each closing axes the
/// plan's first two verifications found hand-listed (the owner's rulings of 2026-11-16, #2422 comments 6923063788 and
/// 6925297897):
/// <list type="bullet">
/// <item><see cref="Family.Operand"/>: a <c>ref</c> / <c>out</c> / <c>in</c> operand, <see cref="ParameterModifier"/> ×
/// <see cref="BindingKind"/>;</item>
/// <item><see cref="Family.Location"/>: a use of a module or <c>@static</c> value, <see cref="LocationUse"/> (fed by the
/// AST's target-bearing nodes, the emitter's store seam, <see cref="StoreForm"/> and <see cref="ParameterModifier"/>) ×
/// holder × <see cref="HostForm"/> × value kind × when; and the cross-type forward read, reader × target
/// <see cref="HostForm"/> (the fourth stage-0 commit, on the owner's ruling of 2026-11-16, #2422 comment 6927434861);</item>
/// <item><see cref="Family.Slot"/>: every child slot of every concrete <see cref="Expression"/> subclass, classified as
/// evaluated whenever its parent is, or not; and the ring cells, the kind of the values on a cycle
/// (<see cref="NodeKinds"/>, the image of <see cref="BindingKind"/>).</item>
/// </list>
///
/// Known-red cells are rows of <c>initializer-dependency-differential-allowlist.txt</c>. A run writes its report, every
/// cell and the rows it would propose under <c>.claude/tmp/initializer-dependency-differential-*</c>.
/// </summary>
[Trait("Category", "GapDiscovery")]
public sealed partial class InitializerDependencyDifferentialTests : IntegrationTestBase
{
    private const string AllowlistFileName = "initializer-dependency-differential-allowlist.txt";
    private const int SharpyExecTimeoutMs = 12_000;
    private const int PythonBatchTimeoutMs = 900_000;

    /// <summary>The value every dependency carries. A storage default is <see cref="StorageDefault"/>, and every
    /// shape's model tells the two apart.</summary>
    internal const int DependencyValue = 3;

    internal const int StorageDefault = 0;

    /// <summary>The issue whose fix turns a red cell green: the reference to the module value is emitted as a name C# cannot resolve.</summary>
    private const string ClassIssue = "#2420";

    /// <summary>The tracker of rule N3, which the three later families' red cells wait on: a value read before it is ready,
    /// a cycle that prints a default, an operand the C# compiler refuses in the checker's place.</summary>
    private const string ReadinessIssue = "#2422";

    private const string Operand = "{d}";

    private const string MainPrintingT = "def main() -> None:\n    print(t)\n";

    public InitializerDependencyDifferentialTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    /// <summary>Why a source value generates no cell. An exclusion that rests on a diagnostic or on another class's open
    /// issue carries the program that shows it and the code that program must still draw
    /// (<see cref="Exclusions_ThatRestOnADiagnostic_StillDrawIt"/>): when the code goes, the exclusion is stale.</summary>
    internal sealed record Exclusion(string Reason, string? Program = null, string? Code = null);

    // ------------------------------------------------------------------------------------------- //
    // Axis 1: the node the initializer reaches the dependency through
    // ------------------------------------------------------------------------------------------- //

    /// <summary>One initializer text per node. <see cref="Operand"/> is where the dependency is read.
    /// <see cref="Twin"/> is the Sharpy text the python twin renders from where python has no spelling of the node.</summary>
    internal sealed record Shape(string Sharpy, Func<int, int> Model, string? Twin = null, string? ValueHolderOnly = null, string? NeedsStorage = null);

    internal static readonly IReadOnlyDictionary<Type, Shape> Shapes = new Dictionary<Type, Shape>
    {
        [typeof(Identifier)] = new("{d}", v => v),
        [typeof(Parenthesized)] = new("({d})", v => v),
        [typeof(UnaryOp)] = new("-{d}", v => -v),
        [typeof(BinaryOp)] = new("{d} + 1", v => v + 1),
        [typeof(ComparisonChain)] = new("int(0 < {d} < 5)", v => 0 < v && v < 5 ? 1 : 0),
        [typeof(ConditionalExpression)] = new("{d} if 1 > 0 else 0", v => v),
        [typeof(FunctionCall)] = new("inc({d})", v => v + 1),
        [typeof(MemberAccess)] = new("Box({d}).v", v => v),
        [typeof(IndexAccess)] = new("[7, 8, 9, 10][{d}]", v => 7 + v),
        [typeof(SliceAccess)] = new("[7, 8, 9, 10][{d}:][0]", v => 7 + v),
        [typeof(ListLiteral)] = new("[{d}][0]", v => v),
        [typeof(TupleLiteral)] = new("({d}, 1)[0]", v => v),
        [typeof(DictLiteral)] = new("{1: {d}}[1]", v => v),
        [typeof(SetLiteral)] = new("max({{d}})", v => v),
        [typeof(ListComprehension)] = new("[v0 + {d} for v0 in [1]][0]", v => v + 1),
        [typeof(SetComprehension)] = new("max({v0 + {d} for v0 in [1]})", v => v + 1),
        [typeof(DictComprehension)] = new("{v0: {d} for v0 in [1]}[1]", v => v),
        [typeof(GeneratorExpression)] = new("sum(v0 + {d} for v0 in [1])", v => v + 1),
        [typeof(DictSpreadComprehension)] = new("{**m0 for m0 in [{1: {d}}]}[1]", v => v, Twin: "{1: {d}}[1]"),
        [typeof(FStringLiteral)] = new("int(f\"{{d}}5\")", v => v * 10 + 5),
        // The template's own read is the interpolation. Its count is 1 whatever it holds, so a second read carries the number.
        [typeof(TStringLiteral)] = new("len(t\"{{d}}\".interpolations) + {d}", v => v + 1, Twin: "len([{d}]) + {d}"),
        [typeof(LambdaExpression)] = new("(lambda: {d})()", v => v),
        [typeof(TypeCoercion)] = new("({d} as! int)", v => v),
        [typeof(WalrusExpression)] = new("(w0 := {d})", v => v),
        [typeof(TryExpression)] = new("(try inc({d})).unwrap()", v => v + 1, Twin: "inc({d})"),
        [typeof(MaybeExpression)] = new("(maybe opt({d})).unwrap()", v => v, Twin: "opt({d})"),
        [typeof(SpreadElement)] = new("[*[{d}]][0]", v => v),
        [typeof(MatchExpression)] = new(
            "match {d}:\n    case 3: 30\n    case _: 0",
            v => v == 3 ? 30 : 0,
            Twin: "30 if {d} == 3 else 0",
            ValueHolderOnly: "its arms start their own lines, and a lambda body or an argument has none (SPY0102)"),
        [typeof(ModifiedArgument)] = new(
            "bump(ref {d})",
            v => v + 1,
            Twin: "inc({d})",
            NeedsStorage: "a `ref` operand is a variable (SPY0389), so the literal control has no spelling"),
    };

    internal static readonly IReadOnlyDictionary<Type, Exclusion> ExcludedShapes = new Dictionary<Type, Exclusion>
    {
        [typeof(IntegerLiteral)] = new("the leaf of the control: it is the `None` dependency, under every shape"),
        [typeof(FloatLiteral)] = new("a leaf: it has no operand to carry a read"),
        [typeof(StringLiteral)] = new("a leaf: it has no operand to carry a read"),
        [typeof(BytesLiteralExpression)] = new("a leaf: it has no operand to carry a read"),
        [typeof(BooleanLiteral)] = new("a leaf: it has no operand to carry a read"),
        [typeof(NoneLiteral)] = new("a leaf: it has no operand to carry a read"),
        [typeof(EllipsisLiteral)] = new("a leaf: it has no operand to carry a read"),
        [typeof(SuperExpression)] = new("names the base instance, which only a method body has"),
        [typeof(StarExpression)] = new("an unpacking target (`first, *rest = xs`): it reads nothing"),
        [typeof(TypeCheck)] = new("refused in every program: `x is T` is retired as a type test", "t: int = int(3 is int)\n" + MainPrintingT, "SPY0349"),
        [typeof(MultiAxisAccess)] = new("its receiver is an NdArray, and on a list it is refused", "t: int = [[1, 2], [3, 4]][1, 0]\n" + MainPrintingT, "SPY0220"),
        [typeof(QuestionMarkExpression)] = new(
            "refused at a module initializer: `?` needs an enclosing function",
            "def opt(v: int) -> int | None:\n    return v\nt: int = opt(3)?\n" + MainPrintingT,
            "SPY0462"),
        [typeof(AwaitExpression)] = new("refused at a module initializer: `await` needs an `async def`", "t: int = await 3\n" + MainPrintingT, "SPY0273"),
    };

    // ------------------------------------------------------------------------------------------- //
    // Axis 2: what the dependency is
    // ------------------------------------------------------------------------------------------- //

    /// <summary>One way an initializer's operand is bound: its declaration lines and its read text, worth
    /// <see cref="DependencyValue"/>. <see cref="IsStorage"/>: the read names a variable a `ref` may take.</summary>
    internal sealed record Dependency(string Key, BindingKind Kind, string[] Declaration, string Read, bool IsStorage = false, bool NeedsStdlib = false);

    private static readonly Dependency Control = new("None", BindingKind.None, Array.Empty<string>(), "3");

    private static readonly Dependency ModuleVariable = new("ModuleVar", BindingKind.ModuleVar, new[] { "k: int = 3" }, "k", IsStorage: true);

    internal static readonly IReadOnlyList<Dependency> Dependencies = new[]
    {
        Control,
        ModuleVariable,
        // The same kind under the two other facts a declaration can carry: a constant-case name, and an initializer that itself reads a module variable.
        new("ModuleVar.ConstantCase", BindingKind.ModuleVar, new[] { "KK: int = 3" }, "KK", IsStorage: true),
        new("ModuleVar.Dependent", BindingKind.ModuleVar, new[] { "j: int = 2", "k: int = j + 1" }, "k", IsStorage: true),
        new("ModuleLiteralConst", BindingKind.ModuleLiteralConst, new[] { "const k: int = 3" }, "k"),
        new("ModuleComputedConst", BindingKind.ModuleComputedConst, new[] { "const k: int = three()" }, "k"),
        new("ModuleProperty", BindingKind.ModuleProperty, new[] { "property k: int = 3" }, "k"),
        new("ModuleDef", BindingKind.ModuleDef, new[] { "def k() -> int:", "    return 3" }, "k()"),
        new("ModuleClass", BindingKind.ModuleClass, new[] { "class K0:", "    v: int = 3" }, "K0().v"),
        new("ModuleStruct", BindingKind.ModuleStruct, new[] { "struct K0:", "    v: int = 3" }, "K0().v"),
        new("ModuleEnum", BindingKind.ModuleEnum, new[] { "enum K0:", "    RED = 3" }, "K0.RED.value"),
        new("StaticField", BindingKind.StaticField, new[] { "class D0:", "    @static", "    k: int = 3" }, "D0.k"),
        new("StaticProperty", BindingKind.StaticProperty, new[] { "class D0:", "    @static", "    property k: int = 3" }, "D0.k"),
        new("StaticMethod", BindingKind.StaticMethod, new[] { "class D0:", "    @static", "    def k() -> int:", "        return 3" }, "D0.k()"),
        new("ClassLiteralConst", BindingKind.ClassLiteralConst, new[] { "class D0:", "    const k: int = 3" }, "D0.k"),
        new("ClassComputedConst", BindingKind.ClassComputedConst, new[] { "class D0:", "    const k: int = three()" }, "D0.k"),
        new("NestedType", BindingKind.NestedType, new[] { "class D0:", "    class K0:", "        v: int = 3" }, "D0.K0().v"),
        new("InstanceField", BindingKind.InstanceField, new[] { "class D0:", "    k: int = 3" }, "D0().k"),
        new("InstanceProperty", BindingKind.InstanceProperty, new[] { "class D0:", "    property k: int = 3" }, "D0().k"),
        new("InstanceMethod", BindingKind.InstanceMethod, new[] { "class D0:", "    def k(self) -> int:", "        return 3" }, "D0().k()"),
        new("Constructor", BindingKind.Constructor, new[] { "class D0:", "    v: int", "    def __init__(self) -> None:", "        self.v = 3" }, "D0().v"),
        // The initializer's own binder, named like a module variable worth something else: the read means the inner binding.
        new("Inner.Comprehension", BindingKind.Inner, new[] { "k: int = 9" }, "[k for k in [3]][0]"),
        new("Inner.LambdaParameter", BindingKind.Inner, new[] { "k: int = 9" }, "(lambda k: int: k)(3)"),
        new("Builtin", BindingKind.Builtin, Array.Empty<string>(), "abs(-3)"),
        new("ImportedName", BindingKind.ImportedName, new[] { "from string import digits" }, "(len(digits) - 7)", NeedsStdlib: true),
    };

    private const string NoEnclosingBody = "a module initializer has no enclosing body to declare it in";

    internal static readonly IReadOnlyDictionary<BindingKind, Exclusion> ExcludedDependencies = new Dictionary<BindingKind, Exclusion>
    {
        [BindingKind.ModuleLet] = new("a module `let` is refused where it is declared: no program holds one", "let t = 3\n" + MainPrintingT, "SPY0340"),
        [BindingKind.ModuleTypeAlias] = new(
            "its read is a constructor call through the alias, an internal error wherever it is written (#1925): no cell isolates the dependency",
            "class Tgt0:\n    v: int = 3\ntype K0 = Tgt0\nt: int = K0().v\n" + MainPrintingT,
            "SPY0908"),
        [BindingKind.ModuleUnion] = new("has no expression read: a case is built and matched, which the P77a pool does from a body"),
        [BindingKind.ModuleInterface] = new("has no expression read: it is a parameter's type"),
        [BindingKind.EnclosingLocal] = new(NoEnclosingBody),
        [BindingKind.Parameter] = new(NoEnclosingBody),
        [BindingKind.LocalVar] = new(NoEnclosingBody),
        [BindingKind.LocalLet] = new(NoEnclosingBody),
        [BindingKind.LocalLiteralConst] = new(NoEnclosingBody),
        [BindingKind.LocalComputedConst] = new(NoEnclosingBody),
        [BindingKind.LocalDef] = new(NoEnclosingBody),
        [BindingKind.LambdaTypedLocal] = new(NoEnclosingBody),
    };

    // ------------------------------------------------------------------------------------------- //
    // Axis 3: how the module value holds what it read, and how it is used
    // ------------------------------------------------------------------------------------------- //

    internal enum Holder
    {
        /// <summary><c>t: int = e</c></summary>
        Value,

        /// <summary><c>t: () -> int = lambda: e</c></summary>
        Lambda,

        /// <summary><c>t: () -> int = mk(e)</c></summary>
        Factory,
    }

    internal enum Use
    {
        Read,
        Call,
        Pass,
    }

    /// <summary>An int is read. A function is called or passed as a value.</summary>
    internal static bool Admits(Holder holder, Use use) => holder == Holder.Value ? use == Use.Read : use != Use.Read;

    private static IEnumerable<(Holder Holder, Use Use)> Forms() =>
        from holder in Enum.GetValues<Holder>()
        from use in Enum.GetValues<Use>()
        where Admits(holder, use)
        select (holder, use);

    /// <summary>The two forms the shape and dependency products ride: the value read and the #2420 call.</summary>
    private static readonly (Holder Holder, Use Use)[] SweepForms = { (Holder.Value, Use.Read), (Holder.Lambda, Use.Call) };

    private static IEnumerable<string> TargetDeclaration(Holder holder, string initializer) => holder switch
    {
        Holder.Value => $"t: int = {initializer}".Split('\n'),
        Holder.Lambda => new[] { $"t: () -> int = lambda: {initializer}" },
        Holder.Factory => new[] { $"t: () -> int = mk({initializer})" },
        _ => throw new ArgumentOutOfRangeException(nameof(holder)),
    };

    private static string UseText(Use use) => use switch
    {
        Use.Read => "t",
        Use.Call => "t()",
        Use.Pass => "ap(t)",
        _ => throw new ArgumentOutOfRangeException(nameof(use)),
    };

    // ------------------------------------------------------------------------------------------- //
    // Axis 4: where the module value is used
    // ------------------------------------------------------------------------------------------- //

    internal enum Host
    {
        Module,
        Type,
    }

    /// <summary>A use site: a position on a host, under a nesting. A null position is the entry point.</summary>
    internal sealed record Site(Host Host, ReadPosition? Position, Harness.Nesting Nesting)
    {
        public string Key => $"{Host}.{(Position is ReadPosition p ? p.ToString() : "EntryPoint")}{(Nesting == Harness.Nesting.Direct ? "" : "+" + Nesting)}";

        /// <summary>The reader is itself a module variable whose initializer reads <c>t</c>: the axis again, one level up.</summary>
        public bool ReaderIsAModuleVariable => Host == Host.Module && Position == ReadPosition.ModuleInitializer;

        /// <summary>The C# target is an optional parameter, so a module value is refused there whatever it holds.</summary>
        public bool TakesConstantsOnly => Position is ReadPosition p && Harness.IsConstantOnly(p);
    }

    internal static readonly Site EntryPoint = new(Host.Module, null, Harness.Nesting.Direct);

    internal static readonly IReadOnlyDictionary<ReadPosition, Exclusion> ExcludedPositions = new Dictionary<ReadPosition, Exclusion>
    {
        [ReadPosition.TypeAliasTarget] = new("a type position: it reads no value"),
        [ReadPosition.BaseList] = new("a type position: it reads no value"),
        [ReadPosition.EnumMemberValue] = new(
            "a module variable there is an internal error with a literal initializer too (#2432): no cell isolates the dependency",
            "t: int = 3\nenum R0:\n    RED = t\ndef main() -> None:\n    print(R0.RED.value)\n",
            "SPY0908"),
    };

    private const string PlacementProbe = "E0";

    /// <summary>The placement of <paramref name="e"/> at a site: the reader's lines at module level, and how <c>main</c> reaches it.
    /// Null where the harness's tables have no row for the position on that host.</summary>
    private static (List<string> Lines, string Access, string[]? Setup)? Place(Host host, ReadPosition position, Harness.Nesting nesting, string e)
    {
        try
        {
            if (host == Host.Module)
            {
                var (reader, access) = Harness.PlaceAtModule(position, nesting, e);
                return (reader, access, null);
            }

            var placed = Harness.PlaceInClass(position, nesting, e);
            var lines = new List<string>(Harness.HostHeader(position).Split('\n'));
            lines.AddRange(Harness.Indent(placed.Members));
            return (lines, placed.Access, placed.Setup);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Every site the placement tables render for a position: each host with a row, and each nesting whose text differs from the direct one.</summary>
    internal static IEnumerable<Site> SitesOf(ReadPosition position)
    {
        foreach (var host in Enum.GetValues<Host>())
        {
            if (Place(host, position, Harness.Nesting.Direct, PlacementProbe) is not { } direct)
                continue;
            yield return new Site(host, position, Harness.Nesting.Direct);
            foreach (var nesting in Enum.GetValues<Harness.Nesting>().Where(n => n != Harness.Nesting.Direct))
            {
                if (Place(host, position, nesting, PlacementProbe) is { } nested && !nested.Lines.SequenceEqual(direct.Lines))
                    yield return new Site(host, position, nesting);
            }
        }
    }

    internal static readonly IReadOnlyList<Site> Sites = new[] { EntryPoint }
        .Concat(Enum.GetValues<ReadPosition>().Where(p => !ExcludedPositions.ContainsKey(p)).SelectMany(SitesOf))
        .ToList();

    /// <summary>The two sites the shape and dependency products ride: inside the module's own class, and inside another type.</summary>
    private static readonly Site[] SweepSites =
    {
        new(Host.Module, ReadPosition.FunctionBody, Harness.Nesting.Direct),
        new(Host.Type, ReadPosition.MethodBody, Harness.Nesting.Direct),
    };

    // ------------------------------------------------------------------------------------------- //
    // The cell, its program, its twin and its expectation
    // ------------------------------------------------------------------------------------------- //

    internal enum Family
    {
        /// <summary>The first four axes: what a reference to a module value compiles to.</summary>
        Reference,
        Operand,
        Location,
        Slot,
    }

    /// <summary>What the python twin of a cell does. A cell python has no spelling for carries no twin, and says why where it is made.</summary>
    internal enum PythonExpectation
    {
        Prints,
        NameError,
        NoTwin,
    }

    /// <summary>The key of a <see cref="Family.Reference"/> cell on its four axes.</summary>
    internal sealed record ReferenceKey(Site Site, Holder Holder, Use Use, Type Node, Dependency Dependency);

    /// <summary>The cite of a red cell whose failure is its class's own, or null: a red with no route is red until one names it.</summary>
    internal delegate string? Route(VerdictClass verdict, string? csCode, string? unresolvedName);

    internal sealed record Cell(string Id, Family Family, string Source, string? TwinSource, string Model)
    {
        /// <summary>The code the cell must be refused with. Null: it runs and prints <see cref="Model"/>, or ends as <see cref="Raises"/> says.</summary>
        public string? RefusalCode { get; init; }

        public bool MustRefuse => RefusalCode != null;

        /// <summary>A code a must-refuse cell is refused with today by a validator the plan retires (D9), accepted beside
        /// <see cref="RefusalCode"/>. It is named by its <c>DiagnosticCodes</c> constant, so the retirement removes the acceptance.</summary>
        public string? RetiringRefusal { get; init; }

        /// <summary>The exception an uncaught run of the cell must end with: it compiles, and stops with that type on stderr.
        /// A run that prints, a refusal and any other exception are red.</summary>
        public string? Raises { get; init; }

        /// <summary>What python's twin prints where that is not <see cref="Model"/>: a divergence the plan records
        /// (<c>docs/deviations.yaml</c>), held here so the oracle measures it.</summary>
        public string? PythonModel { get; init; }

        /// <summary>A cell with nothing of its family's axis in it. It is green or the generator is wrong, and no row may list it.</summary>
        public bool IsControl { get; init; }

        public bool NeedsStdlib { get; init; }

        public PythonExpectation Python { get; init; } = PythonExpectation.Prints;

        public Route Route { get; init; } = static (_, _, _) => null;

        public ReferenceKey? Reference { get; init; }
    }

    private static readonly (string Marker, string[] Lines)[] HelperTable =
    {
        ("three()", new[] { "def three() -> int:", "    return 3" }),
        ("inc(", new[] { "def inc(v: int) -> int:", "    return v + 1" }),
        ("opt(", new[] { "def opt(v: int) -> int | None:", "    return v" }),
        ("bump(", new[] { "def bump(a: ref int) -> int:", "    a = a + 1", "    return a" }),
        ("Box(", new[] { "class Box:", "    v: int", "    def __init__(self, v: int) -> None:", "        self.v = v" }),
        ("mk(", new[] { "def mk(v: int) -> () -> int:", "    return lambda: v" }),
        ("ap(", new[] { "def ap(f: () -> int) -> int:", "    return f()" }),
        ("fill(", new[] { "def fill(a: out int) -> int:", "    a = 42", "    return 1" }),
        ("peek(", new[] { "def peek(a: in int) -> int:", "    return a" }),
        ("peek0(", new[] { "def peek0(a: int) -> int:", "    return a" }),
        ("grow(", new[] { "def grow(a: ref int, by: int) -> int:", "    a = a + by", "    return a" }),
        ("one()", new[] { "async def one() -> int:", "    return 1" }),
        ("ap0(", new[] { "def ap0(f: () -> int) -> int:", "    return 5" }),
        ("P(", new[] { "struct P:", "    x: int", "    def __init__(self, x: int) -> None:", "        self.x = x", "    def add(self, by: int) -> None:", "        self.x = self.x + by" }),
        ("Q(", new[] { "class Q:", "    x: int", "    def __init__(self, x: int) -> None:", "        self.x = x", "    def add(self, by: int) -> None:", "        self.x = self.x + by" }),
        ("seven()", new[] { "def seven() -> int:", "    return 7" }),
        ("fill2(", new[] { "def fill2(a: out int, by: int) -> int:", "    a = 41 + by", "    return 1" }),
        ("peek2(", new[] { "def peek2(a: in int, by: int) -> int:", "    return a + by" }),
        ("CM0(", new[] { "class CM0:", "    v: int", "    def __init__(self, v: int) -> None:", "        self.v = v", "    def __enter__(self) -> int:", "        return self.v", "    def __exit__(self) -> None:", "        pass" }),
    };

    /// <summary>A program's text under the helpers it names, each declared once, after its imports.</summary>
    private static string WithHelpers(string text, IEnumerable<string>? imports = null)
    {
        var head = (imports ?? Array.Empty<string>())
            .Concat(HelperTable.Where(h => text.Contains(h.Marker, StringComparison.Ordinal)).SelectMany(h => h.Lines))
            .ToList();
        return head.Count == 0 ? text : Harness.Join(head) + text;
    }

    private static string Program(Site site, Holder holder, Use use, string shape, Dependency dependency)
    {
        var body = new List<string>();
        var imports = dependency.Declaration.Where(l => l.StartsWith("from ", StringComparison.Ordinal)).ToList();
        body.AddRange(dependency.Declaration.Except(imports));
        body.AddRange(TargetDeclaration(holder, shape.Replace(Operand, dependency.Read, StringComparison.Ordinal)));

        string main;
        var e = UseText(use);
        if (site.Position is not ReadPosition position)
        {
            main = Harness.MainBlock($"print({e})");
        }
        else
        {
            var placed = Place(site.Host, position, site.Nesting, e)
                ?? throw new InvalidOperationException($"{site.Key} has no placement");
            body.AddRange(placed.Lines);
            main = Harness.MainPrinting(placed.Access, placed.Setup);
        }

        return WithHelpers(Harness.Join(body) + main, imports);
    }

    /// <summary>Python has no module-level accessor: its twin reads from a function, as the P77a pool's does.</summary>
    private static Site TwinSite(Site site) =>
        site.Position == ReadPosition.ModuleAccessorBody ? site with { Position = ReadPosition.FunctionBody } : site;

    private static Cell MakeCell(Site site, Holder holder, Use use, Type node, Dependency dependency)
    {
        var shape = Shapes[node];
        return new Cell(
            $"{site.Key}/{holder}.{use}/{node.Name}/{dependency.Key}",
            Family.Reference,
            Program(site, holder, use, shape.Sharpy, dependency),
            Program(TwinSite(site), holder, use, shape.Twin ?? shape.Sharpy, dependency),
            shape.Model(DependencyValue).ToString(CultureInfo.InvariantCulture))
        {
            RefusalCode = site.TakesConstantsOnly ? ConstantOnlyRefusal : null,
            // A literal initializer, read from a site that is not a module variable.
            IsControl = dependency.Kind == BindingKind.None && !site.ReaderIsAModuleVariable,
            NeedsStdlib = dependency.NeedsStdlib,
            Route = ReferenceRoute,
            Reference = new ReferenceKey(site, holder, use, node, dependency),
        };
    }

    private static bool ShapeAdmits(Type node, Holder holder, Dependency dependency)
    {
        var shape = Shapes[node];
        return (shape.ValueHolderOnly == null || holder == Holder.Value) && (shape.NeedsStorage == null || dependency.IsStorage);
    }

    /// <summary>The three sub-products, as keys: every site × every form, every node, every dependency. Each of the last
    /// two rides the two sweep forms at the two sweep sites, and the first two ride the literal control beside the module variable.</summary>
    private static IEnumerable<(Site Site, Holder Holder, Use Use, Type Node, Dependency Dependency)> Product()
    {
        var pair = new[] { Control, ModuleVariable };
        foreach (var site in Sites)
            foreach (var (holder, use) in Forms())
                foreach (var dependency in pair)
                    yield return (site, holder, use, typeof(Identifier), dependency);
        foreach (var node in Shapes.Keys)
            foreach (var site in SweepSites)
                foreach (var (holder, use) in SweepForms)
                    foreach (var dependency in pair.Where(d => ShapeAdmits(node, holder, d)))
                        yield return (site, holder, use, node, dependency);
        foreach (var dependency in Dependencies)
            foreach (var site in SweepSites)
                foreach (var (holder, use) in SweepForms)
                    yield return (site, holder, use, typeof(Identifier), dependency);
    }

    private static readonly Lazy<IReadOnlyList<Cell>> Pool = new(() => Product()
        .Distinct()
        .Select(k => MakeCell(k.Site, k.Holder, k.Use, k.Node, k.Dependency))
        .Concat(OperandCells())
        .Concat(LocationCells())
        .Concat(CrossTypeCells())
        .Concat(SlotCells())
        .Concat(RingCells())
        .OrderBy(c => c.Id, StringComparer.Ordinal)
        .ToList());

    internal static IReadOnlyList<Cell> GeneratedCells() => Pool.Value;

    // ------------------------------------------------------------------------------------------- //
    // Totality
    // ------------------------------------------------------------------------------------------- //

    private static string Both(bool generated) => generated ? "both generated and excluded" : "neither generated nor excluded";

    /// <summary>One line per way an axis fails to be the image of its source, or the pool to be fed by its axes.</summary>
    internal static List<string> TotalityFailures(IReadOnlyList<Cell> everyCell)
    {
        var failures = new List<string>();
        foreach (var dup in everyCell.GroupBy(c => c.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
            failures.Add($"duplicate cell id {dup.Key}");
        foreach (var family in Enum.GetValues<Family>().Where(f => everyCell.All(c => c.Family != f)))
            failures.Add($"Family.{family} has no cell");
        failures.AddRange(OperandTotalityFailures(everyCell));
        failures.AddRange(LocationTotalityFailures(everyCell));
        failures.AddRange(SlotTotalityFailures(everyCell));

        var pool = everyCell.Where(c => c.Family == Family.Reference).ToList();

        var nodes = typeof(Expression).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(Expression).IsAssignableFrom(t))
            .ToList();
        if (nodes.Count < 30)
            failures.Add($"the scan found {nodes.Count} concrete Expression subclasses: it reads nothing");
        foreach (var node in nodes)
        {
            bool generated = Shapes.ContainsKey(node), excluded = ExcludedShapes.ContainsKey(node);
            if (generated == excluded)
                failures.Add($"Expression subclass {node.Name} is {Both(generated)}");
        }
        foreach (var node in Shapes.Keys.Concat(ExcludedShapes.Keys).Where(n => !nodes.Contains(n)))
            failures.Add($"{node.Name} is not a concrete Expression subclass");
        foreach (var (node, shape) in Shapes)
        {
            foreach (var text in new[] { shape.Sharpy, shape.Twin ?? shape.Sharpy })
            {
                if (!text.Contains(Operand, StringComparison.Ordinal))
                    failures.Add($"{node.Name}: '{text}' reads no dependency");
            }
            if (shape.Model(DependencyValue) == shape.Model(StorageDefault))
                failures.Add($"{node.Name}: the model prints {shape.Model(DependencyValue)} for the value and for a storage default");
        }

        foreach (var kind in Enum.GetValues<BindingKind>())
        {
            bool generated = Dependencies.Any(d => d.Kind == kind), excluded = ExcludedDependencies.ContainsKey(kind);
            if (generated == excluded)
                failures.Add($"BindingKind.{kind} is {Both(generated)}");
        }
        foreach (var dup in Dependencies.GroupBy(d => d.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
            failures.Add($"dependency key {dup.Key} is declared twice");

        foreach (var position in Enum.GetValues<ReadPosition>())
        {
            bool placed = SitesOf(position).Any(), excluded = ExcludedPositions.ContainsKey(position);
            if (!placed && !excluded)
                failures.Add($"ReadPosition.{position} has no placement and no exclusion");
            if (excluded && Sites.Any(s => s.Position == position))
                failures.Add($"ReadPosition.{position} is both generated and excluded");
        }
        foreach (var nesting in Enum.GetValues<Harness.Nesting>().Where(n => Sites.All(s => s.Nesting != n)))
            failures.Add($"Nesting.{nesting} rides no site");
        foreach (var host in Enum.GetValues<Host>().Where(h => Sites.All(s => s.Host != h)))
            failures.Add($"Host.{host} has no site");

        foreach (var (what, exclusion) in AllExclusions())
        {
            if (string.IsNullOrWhiteSpace(exclusion.Reason))
                failures.Add($"{what} is excluded without a reason");
            if (exclusion.Program == null != (exclusion.Code == null))
                failures.Add($"{what}: an exclusion carries its program and its code together");
        }

        // The pool against its axes: each sub-product is whole.
        var ids = pool.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var forms = Forms().ToList();
        foreach (var holder in Enum.GetValues<Holder>().Where(h => forms.All(f => f.Holder != h)))
            failures.Add($"Holder.{holder} has no use");
        foreach (var use in Enum.GetValues<Use>().Where(u => forms.All(f => f.Use != u)))
            failures.Add($"Use.{use} has no holder");

        var owed = new List<string>();
        foreach (var site in Sites)
            foreach (var (holder, use) in forms)
                foreach (var dependency in new[] { Control, ModuleVariable })
                    owed.Add($"{site.Key}/{holder}.{use}/{nameof(Identifier)}/{dependency.Key}");
        foreach (var node in Shapes.Keys)
            foreach (var site in SweepSites)
            {
                // A shape's two flags are the only cells it may leave out, and each carries its reason.
                var holders = Shapes[node].ValueHolderOnly == null ? SweepForms : SweepForms.Where(f => f.Holder == Holder.Value);
                var dependencies = Shapes[node].NeedsStorage == null ? new[] { Control, ModuleVariable } : new[] { ModuleVariable };
                foreach (var (holder, use) in holders)
                    foreach (var dependency in dependencies)
                        owed.Add($"{site.Key}/{holder}.{use}/{node.Name}/{dependency.Key}");
            }
        foreach (var dependency in Dependencies)
            foreach (var site in SweepSites)
                foreach (var (holder, use) in SweepForms)
                    owed.Add($"{site.Key}/{holder}.{use}/{nameof(Identifier)}/{dependency.Key}");
        var owedIds = owed.ToHashSet(StringComparer.Ordinal);
        foreach (var id in owedIds.Where(id => !ids.Contains(id)).Take(40))
            failures.Add($"the axes owe {id} and the pool does not hold it");
        foreach (var id in ids.Where(id => !owedIds.Contains(id)).Take(40))
            failures.Add($"the pool holds {id} and the axes do not owe it");
        if (owedIds.Count != pool.Count)
            failures.Add($"the generator made {pool.Count} cells; the axes give {owedIds.Count}");

        // Every cell off the control has its control in the pool, except under a shape that takes a variable only.
        foreach (var cell in pool)
        {
            if (cell.Reference is not { } key)
            {
                failures.Add($"{cell.Id} is a reference cell without its key");
                continue;
            }
            if (key.Dependency.Kind == BindingKind.None || Shapes[key.Node].NeedsStorage != null)
                continue;
            var control = $"{key.Site.Key}/{key.Holder}.{key.Use}/{key.Node.Name}/{Control.Key}";
            if (!ids.Contains(control))
                failures.Add($"{cell.Id} has no control {control}");
        }

        return failures;
    }

    private static IEnumerable<(string What, Exclusion Exclusion)> AllExclusions() =>
        ExcludedShapes.Select(e => ($"Expression subclass {e.Key.Name}", e.Value))
            .Concat(ExcludedDependencies.Select(e => ($"BindingKind.{e.Key}", e.Value)))
            .Concat(ExcludedPositions.Select(e => ($"ReadPosition.{e.Key}", e.Value)))
            .Concat(OperandExclusions())
            .Concat(LocationExclusions())
            .Concat(SlotExclusions());

    [Fact]
    public void Axes_AreTheImageOfTheirSources_AndThePoolIsFedByThem()
    {
        var pool = GeneratedCells();

        var failures = TotalityFailures(pool);

        Assert.True(failures.Count == 0, $"{failures.Count} totality failure(s):\n{string.Join("\n", failures)}");
        Assert.True(pool.Count(c => c.IsControl) >= 200 && pool.Count(c => !c.IsControl) >= 200,
            $"The pool holds {pool.Count(c => c.IsControl)} controls and {pool.Count(c => !c.IsControl)} other cells: a product is empty.");
        foreach (var family in Enum.GetValues<Family>())
        {
            Assert.True(pool.Count(c => c.Family == family) >= 60, $"Family.{family} holds {pool.Count(c => c.Family == family)} cells: a product is empty.");
            Assert.Contains(pool, c => c.Family == family && c.IsControl);
        }
    }

    /// <summary>An exclusion that says "refused" or "an internal error" is a claim about the compiler, and this is its
    /// positive control: the program still draws the code. When it stops, the value is owed its cells.</summary>
    [Fact]
    public void Exclusions_ThatRestOnADiagnostic_StillDrawIt()
    {
        var probes = AllExclusions().Where(e => e.Exclusion.Program != null).ToList();
        Assert.True(probes.Count >= 10, $"Found {probes.Count} exclusions with a program.");

        var stale = new List<string>();
        foreach (var (what, exclusion) in probes)
        {
            var observed = RunSharpy(exclusion.Program!, needsStdlib: false, SharpyExecTimeoutMs);
            var run = observed.Run;
            // A code may name the C# error behind an internal error: `SPY0908 CS1612`.
            var codes = exclusion.Code!.Split(' ');
            if (!run.AllCodes.Contains(codes[0]) || (codes.Length > 1 && run.CsCode != codes[1]))
                stale.Add($"{what} is excluded for {exclusion.Code} and its program drew [{string.Join(",", run.AllCodes.Distinct())}{(run.CsCode == null ? "" : " " + run.CsCode)}] (exit {run.ExitCode}): generate the value");
        }

        Assert.True(stale.Count == 0, string.Join("\n", stale));
    }

    // ------------------------------------------------------------------------------------------- //
    // The Sharpy arm, the verdict and the route
    // ------------------------------------------------------------------------------------------- //

    private bool _currentCellNeedsStdlib;

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
    {
        if (_currentCellNeedsStdlib)
            yield return SharpyStdlibReference.Location;
    }

    private static readonly Regex UnresolvedName = new(@"CS0103: The name '(\w+)'", RegexOptions.CultureInvariant);

    /// <summary>What the Sharpy arm observed, and the name C# could not resolve when the failure is CS0103.</summary>
    internal sealed record Observed(Harness.SharpyRun Run, string? UnresolvedName);

    private Observed RunSharpy(string source, bool needsStdlib, int timeoutMs)
    {
        _currentCellNeedsStdlib = needsStdlib;
        var r = CompileAndExecute(source, "initializer_dependency.spy", executionTimeoutMs: timeoutMs);
        var diagnostics = r.RawDiagnostics;
        var all = diagnostics.Select(d => d.Code).Where(c => c != null).Select(c => c!).ToList();
        var errors = diagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
            .Select(d => d.Code).Where(c => c != null).Select(c => c!).ToList();
        var ice = diagnostics.Where(d => d.Code == "SPY0908").Select(d => d.Message).FirstOrDefault();
        var cs = ice == null ? null : Regex.Match(ice, @"\bCS\d{4}\b") is { Success: true } m ? m.Value : null;
        var name = ice == null ? null : UnresolvedName.Match(ice) is { Success: true } n ? n.Groups[1].Value : null;
        return new Observed(
            new Harness.SharpyRun(r.Success, r.ExitCode, r.Exception != null, r.TimedOut, r.StandardOutput, r.StandardError, errors, all, cs),
            name);
    }

    /// <summary>One slow cell on a loaded runner is not a verdict: a timed-out run gets one more at twice the timeout, as the P77a sweep's does.</summary>
    private Observed RunSharpyRetryingATimeout(Cell cell)
    {
        var observed = RunSharpy(cell.Source, cell.NeedsStdlib, SharpyExecTimeoutMs);
        return observed.Run.TimedOut ? RunSharpy(cell.Source, cell.NeedsStdlib, SharpyExecTimeoutMs * 2) : observed;
    }

    /// <summary>The refusal a constants-only site gives a module value.</summary>
    private const string ConstantOnlyRefusal = "SPY0401";

    private static readonly Regex UncaughtException = new(@"^Unhandled exception\. (?:[\w.]+\.)?(\w+)(?::|$)", RegexOptions.CultureInvariant);

    /// <summary>The type of the exception an uncaught run ended with: the first line of its stderr. An exception a type
    /// initializer wrapped reads as the wrapper, which is what a caller's <c>except</c> sees.</summary>
    internal static string? UncaughtType(string stderr)
        => UncaughtException.Match(stderr.TrimStart().Split('\n', 2)[0].TrimEnd()) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Expectation × Sharpy outcome → verdict class.</summary>
    internal static VerdictClass Judge(Cell cell, SharpyKind kind, string stdout, string stderr = "")
    {
        if (kind is SharpyKind.TimedOut or SharpyKind.Harness)
            return VerdictClass.Harness;
        if (kind == SharpyKind.Ice)
            return VerdictClass.Ice;
        if (cell.Raises != null)
        {
            return kind switch
            {
                SharpyKind.Ran => VerdictClass.SilentWrong,
                SharpyKind.Refused => VerdictClass.FalseRefusal,
                _ => UncaughtType(stderr) == cell.Raises ? VerdictClass.Match : VerdictClass.RuntimeThrow,
            };
        }
        if (cell.MustRefuse)
        {
            return kind switch
            {
                SharpyKind.Refused => VerdictClass.Match,
                SharpyKind.Ran => VerdictClass.SilentWrong,
                _ => VerdictClass.RuntimeThrow,
            };
        }

        return kind switch
        {
            SharpyKind.Ran => Harness.Normalize(stdout) == cell.Model ? VerdictClass.Match : VerdictClass.SilentWrong,
            SharpyKind.Refused => VerdictClass.FalseRefusal,
            _ => VerdictClass.RuntimeThrow,
        };
    }

    /// <summary>The names a reference to the module value, or to a module variable reading it, is emitted under.</summary>
    private static readonly HashSet<string> ModuleValueNames = new(StringComparer.Ordinal) { "t", "T", "r", "R" };

    /// <summary>The route of the first four axes. The class's own failure is the one it names: the reference emitted as a
    /// name C# cannot resolve.</summary>
    internal static string? ReferenceRoute(VerdictClass verdict, string? csCode, string? unresolvedName)
        => verdict == VerdictClass.Ice && csCode == "CS0103" && unresolvedName != null && ModuleValueNames.Contains(unresolvedName)
            ? $"{ClassIssue} ice"
            : null;

    internal static string Unrouted(VerdictClass verdict, string? csCode, string? unresolvedName)
        => $"UNROUTED({verdict}{(csCode == null ? "" : " " + csCode)}{(unresolvedName == null ? "" : " " + unresolvedName)})";

    /// <summary>The row a red cell carries: the issue whose fix turns it green, by its family's route. Any other red has no
    /// route, and is red until one names it.</summary>
    internal static string ProposedCite(Cell cell, VerdictClass verdict, string? csCode, string? unresolvedName)
        => cell.Route(verdict, csCode, unresolvedName) ?? Unrouted(verdict, csCode, unresolvedName);

    internal sealed record AllowRow(VerdictClass Class, string Cite);

    private static readonly Regex IssueCite = new(@"#\d+", RegexOptions.CultureInvariant);

    /// <summary>The allowlist's rows: <c>&lt;cell-id&gt; &lt;class&gt; # #&lt;issue&gt; &lt;lane&gt;</c>. A missing file is no rows.</summary>
    internal static Dictionary<string, AllowRow> ParseAllowlist(IEnumerable<string> lines, IReadOnlySet<string> universe, List<string> problems)
    {
        var rows = new Dictionary<string, AllowRow>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var head = split < 0 ? line : line[..split].Trim();
            var cite = split < 0 ? "" : line[(split + 2)..].Trim();
            var space = head.LastIndexOf(' ');
            if (space <= 0 || !Enum.TryParse<VerdictClass>(head[(space + 1)..], out var cls)
                || cls is not (VerdictClass.Ice or VerdictClass.FalseRefusal or VerdictClass.SilentWrong or VerdictClass.RuntimeThrow))
            {
                problems.Add($"'{line}' must be '<cell-id> <Ice|FalseRefusal|SilentWrong|RuntimeThrow> # #<issue> <lane>'");
                continue;
            }
            var id = head[..space].Trim();
            if (!IssueCite.IsMatch(cite))
                problems.Add($"'{line}' must cite an issue (#N)");
            if (!universe.Contains(id))
                problems.Add($"'{id}' names no generated cell");
            if (!rows.TryAdd(id, new AllowRow(cls, cite)))
                problems.Add($"'{id}' is listed twice");
        }
        return rows;
    }

    internal sealed record Result(Cell Cell, Observed Observed, SharpyKind Kind, VerdictClass Class, string? PythonFailure);

    /// <summary>The ratchet over judged cells: one line per red cell with no row, row with no red cell, row of another class or cite,
    /// red control, refusal under another code than the site's, and cell python or the harness could not judge.</summary>
    internal static List<string> RatchetFailures(IReadOnlyList<Result> results, IReadOnlyDictionary<string, AllowRow> rows)
    {
        var failures = new List<string>();
        foreach (var r in results)
        {
            rows.TryGetValue(r.Cell.Id, out var row);
            if (r.PythonFailure != null)
            {
                failures.Add($"GeneratorBug {r.Cell.Id}: {r.PythonFailure}{(row == null ? "" : " (and a row lists it: not allowlistable)")}");
                continue;
            }
            if (r.Class == VerdictClass.Harness)
            {
                failures.Add($"Harness {r.Cell.Id}: Sharpy {Describe(r)}");
                continue;
            }
            if (r.Cell.MustRefuse && r.Kind == SharpyKind.Refused && !r.Observed.Run.ErrorCodes.Contains(r.Cell.RefusalCode!)
                && (r.Cell.RetiringRefusal == null || !r.Observed.Run.ErrorCodes.Contains(r.Cell.RetiringRefusal)))
                failures.Add($"RefusalReason {r.Cell.Id}: the cell's refusal is {r.Cell.RefusalCode} and Sharpy {Describe(r)}");

            var isRed = r.Class != VerdictClass.Match;
            if (isRed && r.Cell.IsControl)
                failures.Add($"Control {r.Cell.Id} is {r.Class}: Sharpy {Describe(r)}. A cell with nothing of its axis in it is red, so no cell beside it isolates the axis: exclude the value with its reason");
            else if (isRed && row == null)
                failures.Add($"{r.Class} {r.Cell.Id}: Sharpy {Describe(r)}; model '{r.Cell.Model}'");
            else if (isRed && row!.Class != r.Class)
                failures.Add($"{r.Cell.Id}: listed as {row.Class}, now {r.Class}: MOVED, update or delete the row");
            else if (!isRed && row != null)
                failures.Add($"{r.Cell.Id}: listed as {row.Class}, now {r.Class}: STALE, delete the row");
            else if (isRed && row!.Cite != ProposedCite(r.Cell, r.Class, r.Observed.Run.CsCode, r.Observed.UnresolvedName))
                failures.Add($"{r.Cell.Id}: the row cites '{row.Cite}' and the route says '{ProposedCite(r.Cell, r.Class, r.Observed.Run.CsCode, r.Observed.UnresolvedName)}'");
        }
        return failures;
    }

    private static string Describe(Result r)
        => r.Kind == SharpyKind.Ran
            ? $"Ran '{Harness.Normalize(r.Observed.Run.Stdout)}'"
            : $"{r.Kind} [{string.Join(",", r.Observed.Run.AllCodes.Distinct())}{(r.Observed.Run.CsCode != null ? " " + r.Observed.Run.CsCode : "")}"
                + $"{(r.Observed.UnresolvedName != null ? " '" + r.Observed.UnresolvedName + "'" : "")}]"
                + (r.Kind == SharpyKind.RuntimeThrow ? $" stderr '{r.Observed.Run.Stderr.TrimStart().Split('\n', 2)[0].TrimEnd()}'" : "");

    // ------------------------------------------------------------------------------------------- //
    // The sweep
    // ------------------------------------------------------------------------------------------- //

    [SkippableFact]
    public void InitializerDependency_Sweep()
    {
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 12));
        var wall = Stopwatch.StartNew();
        var pool = GeneratedCells();
        var failures = TotalityFailures(pool);

        // --- Python arm: one batch. A twin the renderer cannot produce is a generator bug, never a skip. ---
        var requests = new List<PyRequest>(pool.Count);
        var twinFailures = new Dictionary<int, string>();
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].TwinSource is not { } twin)
                continue;
            try
            {
                requests.Add(new PyRequest(i, TwinRenderer.Render(twin)));
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
            {
                twinFailures[i] = $"renderer: {ex.Message}";
            }
        }
        var pythonById = oracle.RunBatch(requests, PythonBatchTimeoutMs);

        // --- Sharpy arm: production compile + execute, sequential, `run` and never `emit`. ---
        var results = new List<Result>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            var cell = pool[i];
            string? pythonFailure = twinFailures.GetValueOrDefault(i);
            if (cell.TwinSource == null != (cell.Python == PythonExpectation.NoTwin))
                pythonFailure = "a cell carries its twin and its python expectation together";
            if (pythonFailure == null && cell.TwinSource != null)
            {
                var python = pythonById.TryGetValue(i, out var verdict)
                    ? Harness.ClassifyPython(verdict)
                    : new PyOutcome(PyKind.SyntaxError, "no verdict from the runner");
                var printed = cell.PythonModel ?? cell.Model;
                if (cell.Python == PythonExpectation.Prints && python != PyOutcome.Ran(printed))
                    pythonFailure = $"python's twin is modelled to print '{printed}' and python {python}";
                if (cell.PythonModel != null && (cell.Python != PythonExpectation.Prints || cell.PythonModel == cell.Model))
                    pythonFailure = "a cell names what python prints only where python prints something else than the model";
                if (cell.Python == PythonExpectation.NameError && python.Kind != PyKind.NameError)
                    pythonFailure = $"the model raises NameError and python {python}";
            }

            var observed = RunSharpyRetryingATimeout(cell);
            var kind = Harness.ClassifySharpy(observed.Run);
            results.Add(new Result(cell, observed, kind, Judge(cell, kind, observed.Run.Stdout, observed.Run.Stderr), pythonFailure));
        }

        // --- Ratchet. ---
        var universe = pool.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var path = PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Conformance", AllowlistFileName);
        var rows = ParseAllowlist(path == null ? Array.Empty<string>() : File.ReadAllLines(path), universe, failures);
        failures.AddRange(RatchetFailures(results, rows));

        wall.Stop();
        var report = WriteReport(pool, results, rows, wall.Elapsed);
        Output.WriteLine(report);
        Assert.True(failures.Count == 0, $"{failures.Count} failures. {report}\n{string.Join("\n", failures.Take(120))}");
    }

    private static string WriteReport(IReadOnlyList<Cell> pool, IReadOnlyList<Result> results, IReadOnlyDictionary<string, AllowRow> rows, TimeSpan wall)
    {
        var red = results.Where(r => r.Class is VerdictClass.Ice or VerdictClass.FalseRefusal or VerdictClass.SilentWrong or VerdictClass.RuntimeThrow).ToList();
        var byClass = results.GroupBy(r => r.Class).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count());
        var dir = ReportDirectory();
        File.WriteAllText(
            Path.Combine(dir, "initializer-dependency-differential-report.json"),
            JsonSerializer.Serialize(
                new
                {
                    pool = pool.Count,
                    byFamily = pool.GroupBy(c => c.Family).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                    redByFamily = red.GroupBy(r => r.Cell.Family).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                    controls = pool.Count(c => c.IsControl),
                    mustRefuse = pool.Count(c => c.MustRefuse),
                    twins = pool.Count(c => c.TwinSource != null),
                    rings = pool.Count(c => c.Id.StartsWith("Slot/Ring/", StringComparison.Ordinal)),
                    genericHost = pool.Count(c => c.Id.Contains("+Generic/", StringComparison.Ordinal)),
                    mustRaise = pool.Count(c => c.Raises != null),
                    pythonDiverges = pool.Count(c => c.PythonModel != null),
                    crossType = pool.Count(c => c.Id.StartsWith("Location/CrossType/", StringComparison.Ordinal)),
                    sites = Sites.Count,
                    shapes = Shapes.Count,
                    excludedShapes = ExcludedShapes.Count,
                    dependencies = Dependencies.Count,
                    excludedDependencies = ExcludedDependencies.Count,
                    excludedPositions = ExcludedPositions.Count,
                    byClass,
                    red = red.Count,
                    allowlistSize = rows.Count,
                    pythonAgreed = results.Count(r => r.Cell.TwinSource != null && r.PythonFailure == null),
                    wallSeconds = wall.TotalSeconds,
                },
                new JsonSerializerOptions { WriteIndented = true }));
        using (var writer = new StreamWriter(Path.Combine(dir, "initializer-dependency-differential-cells.jsonl")))
        {
            foreach (var r in results)
            {
                writer.WriteLine(JsonSerializer.Serialize(new
                {
                    id = r.Cell.Id,
                    verdict = r.Class.ToString(),
                    control = r.Cell.IsControl,
                    mustRefuse = r.Cell.MustRefuse,
                    model = r.Cell.Model,
                    python = r.PythonFailure,
                    sharpy = Describe(r),
                    source = r.Cell.Source,
                }));
            }
        }
        File.WriteAllLines(
            Path.Combine(dir, "initializer-dependency-differential-proposed-allowlist.txt"),
            red.OrderBy(r => r.Cell.Id, StringComparer.Ordinal)
                .Select(r => $"{r.Cell.Id} {r.Class} # {ProposedCite(r.Cell, r.Class, r.Observed.Run.CsCode, r.Observed.UnresolvedName)}"));

        return $"initializer-dependency-differential: pool {pool.Count} ({pool.Count(c => c.IsControl)} controls, {pool.Count(c => c.MustRefuse)} must-refuse, {pool.Count(c => c.Raises != null)} must-raise), "
            + $"sites {Sites.Count}, shapes {Shapes.Count}, dependencies {Dependencies.Count}, "
            + $"python agreed {results.Count(r => r.Cell.TwinSource != null && r.PythonFailure == null)}/{pool.Count(c => c.TwinSource != null)}, "
            + $"red {red.Count}, allowlist {rows.Count}, wall {wall.TotalSeconds:F1}s. "
            + string.Join(" ", byClass.Select(c => $"{c.Key}={c.Value}")) + ". "
            + string.Join(" ", Enum.GetValues<Family>().Select(f => $"{f}={pool.Count(c => c.Family == f)}/red {red.Count(r => r.Cell.Family == f)}"));
    }

    private static string ReportDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;
        var tmp = Path.Combine(dir?.FullName ?? Path.GetTempPath(), ".claude", "tmp");
        Directory.CreateDirectory(tmp);
        return tmp;
    }

    // ------------------------------------------------------------------------------------------- //
    // The ratchet's own facts: plain data in, no compile
    // ------------------------------------------------------------------------------------------- //

    private static Result ResultOf(Cell cell, VerdictClass cls, string? csCode = null, string? name = null, string? pythonFailure = null, string[]? refusal = null)
        => new(
            cell,
            new Observed(
                new Harness.SharpyRun(cls == VerdictClass.Match, 0, false, false, cell.Model, "", refusal ?? Array.Empty<string>(), refusal ?? Array.Empty<string>(), csCode),
                name),
            refusal != null ? SharpyKind.Refused : cls == VerdictClass.Ice ? SharpyKind.Ice : SharpyKind.Ran,
            cls,
            pythonFailure);

    [Fact]
    public void Ratchet_ARedCell_NeedsItsRow_AndARowNeedsItsRedCell()
    {
        var pool = GeneratedCells();
        var onAxis = pool.First(c => c.Family == Family.Reference && !c.IsControl && !c.MustRefuse);
        var control = pool.First(c => c.Family == Family.Reference && c.IsControl && !c.MustRefuse);
        var refusing = pool.First(c => c.Family == Family.Reference && c.MustRefuse);
        var none = new Dictionary<string, AllowRow>();
        var listed = new Dictionary<string, AllowRow> { [onAxis.Id] = new(VerdictClass.Ice, $"{ClassIssue} ice") };

        Assert.Empty(RatchetFailures(new[] { ResultOf(onAxis, VerdictClass.Match) }, none));
        Assert.Empty(RatchetFailures(new[] { ResultOf(onAxis, VerdictClass.Ice, "CS0103", "t") }, listed));
        Assert.Empty(RatchetFailures(new[] { ResultOf(refusing, VerdictClass.Match, refusal: new[] { ConstantOnlyRefusal }) }, none));

        Assert.Contains("model", Assert.Single(RatchetFailures(new[] { ResultOf(onAxis, VerdictClass.Ice, "CS0103", "t") }, none)));
        Assert.Contains("STALE", Assert.Single(RatchetFailures(new[] { ResultOf(onAxis, VerdictClass.Match) }, listed)));
        Assert.Contains("MOVED", Assert.Single(RatchetFailures(new[] { ResultOf(onAxis, VerdictClass.SilentWrong) }, listed)));
        Assert.Contains("the route says", Assert.Single(RatchetFailures(new[] { ResultOf(onAxis, VerdictClass.Ice, "CS0103", "other") }, listed)));
        Assert.Contains("GeneratorBug", Assert.Single(RatchetFailures(new[] { ResultOf(onAxis, VerdictClass.Match, pythonFailure: "python 4") }, none)));
        Assert.Contains("RefusalReason", Assert.Single(RatchetFailures(new[] { ResultOf(refusing, VerdictClass.Match, refusal: new[] { "SPY0200" }) }, none)));

        // A red control is never a row's to hold.
        var controlRow = new Dictionary<string, AllowRow> { [control.Id] = new(VerdictClass.Ice, $"{ClassIssue} ice") };
        Assert.Contains("Control", Assert.Single(RatchetFailures(new[] { ResultOf(control, VerdictClass.Ice, "CS0103", "t") }, controlRow)));
    }

    [Theory]
    [InlineData(VerdictClass.Ice, "CS0103", "t", "#2420 ice")]
    [InlineData(VerdictClass.Ice, "CS0103", "T", "#2420 ice")]
    [InlineData(VerdictClass.Ice, "CS0103", "r", "#2420 ice")]
    [InlineData(VerdictClass.Ice, "CS0103", "k", "UNROUTED(Ice CS0103 k)")]
    [InlineData(VerdictClass.Ice, "CS0029", null, "UNROUTED(Ice CS0029)")]
    [InlineData(VerdictClass.SilentWrong, null, null, "UNROUTED(SilentWrong)")]
    [InlineData(VerdictClass.FalseRefusal, null, null, "UNROUTED(FalseRefusal)")]
    internal void Route_OnlyTheUnresolvedModuleValue_IsTheClassIssue(VerdictClass verdict, string? cs, string? name, string cite)
        => Assert.Equal(cite, ReferenceRoute(verdict, cs, name) ?? Unrouted(verdict, cs, name));

    /// <summary>A cell that must raise is green on an uncaught exception of its type, read from the first line of stderr:
    /// one a type initializer wrapped is the wrapper's, and red.</summary>
    [Theory]
    [InlineData(SharpyKind.RuntimeThrow, "Unhandled exception. Sharpy.NameError: name 't' is not defined\n   at M.Use()", VerdictClass.Match)]
    [InlineData(SharpyKind.RuntimeThrow, "Unhandled exception. System.TypeInitializationException: The type initializer for 'H' threw an exception.\n ---> Sharpy.NameError: name 't' is not defined", VerdictClass.RuntimeThrow)]
    [InlineData(SharpyKind.RuntimeThrow, "Unhandled exception. System.NullReferenceException: Object reference not set to an instance of an object.", VerdictClass.RuntimeThrow)]
    [InlineData(SharpyKind.Ran, "", VerdictClass.SilentWrong)]
    [InlineData(SharpyKind.Refused, "", VerdictClass.FalseRefusal)]
    internal void Judge_ACellThatMustRaise_IsGreenOnItsExceptionOnly(SharpyKind kind, string stderr, VerdictClass verdict)
    {
        var cell = GeneratedCells().First(c => c.Raises == CycleError);

        Assert.Equal(verdict, Judge(cell, kind, "1", stderr));
    }

    [Fact]
    public void Ratchet_ARetiringRefusal_IsAcceptedBesideTheCellsOwn_AndNoOtherCodeIs()
    {
        var cell = GeneratedCells().First(c => c.RetiringRefusal != null);
        var none = new Dictionary<string, AllowRow>();

        Assert.NotEqual(cell.RefusalCode, cell.RetiringRefusal);
        Assert.Empty(RatchetFailures(new[] { ResultOf(cell, VerdictClass.Match, refusal: new[] { cell.RefusalCode! }) }, none));
        Assert.Empty(RatchetFailures(new[] { ResultOf(cell, VerdictClass.Match, refusal: new[] { cell.RetiringRefusal! }) }, none));
        Assert.Contains("RefusalReason", Assert.Single(RatchetFailures(new[] { ResultOf(cell, VerdictClass.Match, refusal: new[] { "SPY0200" }) }, none)));
    }

    [Fact]
    public void Allowlist_ARow_NamesAGeneratedCellARedClassAndAnIssue()
    {
        var universe = new HashSet<string>(StringComparer.Ordinal) { "a/b", "c/d" };
        var problems = new List<string>();

        var rows = ParseAllowlist(new[] { "# a comment", "", "a/b Ice # #2420 ice" }, universe, problems);

        Assert.Empty(problems);
        Assert.Equal(new AllowRow(VerdictClass.Ice, "#2420 ice"), Assert.Single(rows).Value);

        ParseAllowlist(new[] { "a/b Match # #2420 ice", "c/d Ice # no issue", "e/f Ice # #2420 ice", "a/b Ice # #1 ice", "a/b Ice # #1 ice" }, universe, problems);
        Assert.Equal(4, problems.Count);
        Assert.Contains(problems, p => p.Contains("must be", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("must cite", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("names no generated cell", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("listed twice", StringComparison.Ordinal));
    }
}
