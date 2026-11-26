using System.Reflection;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Shared;
using Xunit;
using AstFunctionType = Sharpy.Compiler.Parser.Ast.FunctionType;
using AstTypeAlias = Sharpy.Compiler.Parser.Ast.TypeAlias;
using AstTypeAnnotation = Sharpy.Compiler.Parser.Ast.TypeAnnotation;
using Harness = Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests;
using ScopeKind = Sharpy.Compiler.Semantic.SymbolTable.ScopeKind;
using TypeKind = Sharpy.Compiler.Semantic.TypeKind;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The axes of <see cref="ScopeMeaningDifferentialTests"/>, each beside the enumeration it is the image of, with no
/// default arm: a value the enumeration gains has no entry, and <see cref="Axes_EveryValueIsGeneratedOrExcluded"/> is red
/// until it is generated or excluded with its reason.
///
/// <list type="table">
/// <item><term>no-initializer × host</term><description><c>TypeKind</c> (<c>Enum.GetValues</c>)</description></item>
/// <item><term>no-initializer × declared type</term><description>the concrete subclasses of <c>SemanticType</c> (reflection)</description></item>
/// <item><term>typed-read × spelling, × reader position</term><description><c>Spelling</c> and <see cref="ReadPosition"/>, as the
/// P77a pool runs a read of the kind (<see cref="TypedReaders"/>): the pool's own rosters decide where a read is legal</description></item>
/// <item><term>callee × outer</term><description><see cref="BindingKind"/> (<c>Enum.GetValues</c>)</description></item>
/// <item><term>constant-kind × host</term><description>the call sites of <c>ConstantDefaultClassifier.IsAdmitted</c> in the compiler's
/// source (a census: <c>ScopeMeaningDifferentialTests.Readers.cs</c>), each with the <c>AdmissionTable</c> it passes; the one
/// site that judges a <c>const</c> at every scope is refined by <c>ScopeKind</c> × <c>TypeKind</c></description></item>
/// <item><term>constant-kind × member kind</term><description>what the compiler records for a member, per where the member
/// lives: a referenced module's value (a census of discovery's <c>FieldSignature.IsConst</c> × <c>IsGuarded</c> over the
/// Stdlib), a referenced type's member (the subclasses of <c>ClrMemberResolution</c>, a field refined by what C# reads as a
/// constant: <c>FieldInfo.IsLiteral</c>, or a <c>DecimalConstantAttribute</c>), the program's own enum
/// (<c>EnumMemberAccessKind</c>), and any other name of the program (<see cref="BindingKind"/>)</description></item>
/// <item><term>dataclass-init × site, × host, × option</term><description><c>ScopeKind</c>, <c>TypeKind</c> (<c>Enum.GetValues</c>)
/// and the public properties of <c>DataclassOptions</c> (reflection)</description></item>
/// <item><term>dataclass-init × reader slot</term><description><see cref="ReadPosition"/> × <c>Nesting</c>, as the typed-read set
/// reads a module function (<see cref="InitReaderSlots"/>)</description></item>
/// <item><term>dataclass-init × reader, × fit, × base, × field default, × reference slot, × through, × name, × placement</term>
/// <description>literal sets, each an enum with its partition (<see cref="ScopeInitReader"/>, <see cref="ScopeInitFit"/>,
/// <see cref="ScopeDataclassBase"/>, <see cref="ScopeFieldDefault"/>, <see cref="ScopeReferenceSlot"/>,
/// <see cref="ScopeInitThrough"/>, <see cref="ScopeDataclassName"/>, <see cref="ScopeInitPlacement"/>): the compiler has no
/// enumeration of how a program reads a synthesized <c>__init__</c>. Base and reference slot are the shapes measured, and
/// open; the others say in their summaries why they are closed. Five are projected, each value varied alone from the
/// matrix's first (<see cref="InitProjections"/>): base, field default, reference slot, through and name, beside the two
/// projected values of the site axis (<c>generic</c>, <c>deep</c>). Placement is no projection: it moves the reader
/// (<see cref="EnclosedSlots"/>, <see cref="OtherDataclassOrders"/>)</description></item>
/// <item><term>written-init × reader, × fit, × argument</term><description><see cref="ScopeInitReader"/> and
/// <see cref="ScopeInitFit"/> again, and <see cref="ScopeInitArgument"/>, a literal set varied alone from its first value</description></item>
/// <item><term>dataclass-member × member, × read</term><description>the names <c>DataclassSynthesis.SynthesizeMembers</c> looks for
/// among a class's own methods (a census of its source: <see cref="SynthesizedMemberNames"/>), and
/// <see cref="ScopeMemberRead"/>, a literal set</description></item>
/// <item><term>lambda-default × owner, × position</term><description><c>LambdaExpression.IsArrowSyntax</c> with the <c>def</c>
/// control, and the positions at which the P77a pool runs a bare read of the member from its own type
/// (<see cref="LambdaDefaultPositions"/>)</description></item>
/// <item><term>alias-target × site</term><description><c>ScopeKind</c> × <c>TypeKind</c>: the scope an alias's binder declares into</description></item>
/// <item><term>alias-target × shape</term><description>the public properties of the alias's three AST records
/// (<c>TypeAlias</c>, <c>TypeAnnotation</c>, <c>FunctionType</c>): each place a target can hold a type's name</description></item>
/// </list>
/// </summary>
public sealed partial class ScopeMeaningDifferentialTests
{
    // ------------------------------------------------------------------------------------------- //
    // Exclusions that rest on what the compiler does today
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A program and what the compiler does with it today. An exclusion whose reason is one of these keys is true
    /// only while the probe holds: when the behaviour changes the probe is red, and the value is generated or re-reasoned.
    /// A reason that says more than one thing of the compiler names the others in <paramref name="Also"/>, each held the same way.</summary>
    internal sealed record BehaviourProbe(string Source, string Expected, Func<Harness.SharpyRun, bool> Holds, bool Stdlib = false, BehaviourProbe[]? Also = null)
    {
        /// <summary>This probe and every other program its reason rests on.</summary>
        public IEnumerable<BehaviourProbe> All => (Also ?? Array.Empty<BehaviourProbe>()).Prepend(this);
    }

    private static bool Refused(Harness.SharpyRun run, string code, string? cs = null)
        => run.ErrorCodes.Contains(code) && (cs == null || run.CsCode == cs);

    /// <summary>The program compiled, ran and printed <paramref name="stdout"/>.</summary>
    private static bool Printed(Harness.SharpyRun run, string stdout)
        => run is { Success: true, ExitCode: 0, TimedOut: false } && run.ErrorCodes.Count == 0 && Harness.Normalize(run.Stdout) == stdout;

    private static BehaviourProbe Prints(string source, string stdout, BehaviourProbe[]? Also = null) => new(source, $"a run that prints '{stdout}'", r => Printed(r, stdout), Also: Also);

    /// <summary>What the review of PR #2767's test code found excluded under a false reason, and the owner then ruled on (#2423
    /// comment 7052893050, item 26): the reason is corrected and probed, and no cell is generated.</summary>
    private const string ByItem26 = "not generated, by the owner's decision (#2423 comment 7052893050, item 26: the fix round after the independent review adds no cell)";

    private const string TailMain = "def main() -> None:\n    print(1)\n";

    private const string ModuleLetRefused = "a module `let` is refused at every order (SPY0340, P21b)";
    private const string InAnInterfaceEachKindIsOut = "in an interface each declaration kind is out for its own reason. A `@static` value member is SPY0908 CS0120 with its initializer and without (#2383's class), so it has no running control there. An instance field declares no member a read finds: `h.x` through the interface is SPY0203 with its initializer and without (#2383's class too). An instance `property x: T` is a signature an implementing class fills, and it runs (`class C(H): property x: int = 5` prints `5`): that control, of a kind R-QI leaves alone, is " + ByItem26 + ", and no tracker holds it, since it is green";
    private const string EnumBodyHoldsCases = "an enum body holds `NAME = value` cases and nothing else (SPY0101 / SPY0103)";
    private const string UnionBodyHoldsCases = "a union body holds cases and methods and nothing else (SPY0104)";

    internal static readonly IReadOnlyDictionary<string, BehaviourProbe> BehaviourProbes = new Dictionary<string, BehaviourProbe>(StringComparer.Ordinal)
    {
        [ModuleLetRefused] = new("let x = 5\n" + TailMain, "SPY0340", r => Refused(r, "SPY0340")),
        [InAnInterfaceEachKindIsOut] = new("interface H:\n    @static\n    x: int = 5\ndef main() -> None:\n    print(H.x)\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120"), Also: new BehaviourProbe[]
        {
            new("interface H:\n    @static\n    x: int\n    def m(self) -> int: ...\ndef main() -> None:\n    print(H.x)\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120")),
            new("interface H:\n    @static\n    property x: int = 5\n    def m(self) -> int: ...\ndef main() -> None:\n    print(H.x)\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120")),
            new("interface H:\n    @static\n    property x: int\n    def m(self) -> int: ...\ndef main() -> None:\n    print(H.x)\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120")),
            new(InterfaceMember("x: int = 5", "x: int = 5"), "SPY0203", r => Refused(r, "SPY0203")),
            new(InterfaceMember("x: int", "x: int = 5"), "SPY0203", r => Refused(r, "SPY0203")),
            Prints(InterfaceMember("property x: int", "property x: int = 5"), "5"),
        }),
        [ACalleeBinderIsOut(LocalTarget.MatchCapture)] = CalleeBinderProbe("f: () -> int = one\n    match f:\n        case x:\n            print(x())"),
        [ACalleeBinderIsOut(LocalTarget.AsPatternCapture)] = CalleeBinderProbe("f: () -> int = one\n    match f:\n        case _ as x:\n            print(x())"),
        [ACalleeBinderIsOut(LocalTarget.OutLet)] = CalleeBinderProbe("give(out let x)\n    print(x())", "type F = () -> int\ndef give(f: out F) -> None:\n    f = one\n"),
        [AnExceptionIsCalled] = Prints("x: int = 12\n" + CallableException, "1", Also: new[] { Prints(CallableException, "1") }),
        [AThirdPlacement(DeclaringRelation.Base)] = ThirdPlacementProbe("class B:\n    @static\n    x: int = 5\n", "class H(B):\n    @static\n    r: int = nb(B.x)\n"),
        [AThirdPlacement(DeclaringRelation.Other)] = ThirdPlacementProbe("class B:\n    @static\n    x: int = 5\n", "class H:\n    @static\n    r: int = nb(B.x)\n"),
        [AGenericReceiver] = ThirdPlacementProbe("class G[T]:\n    @static\n    x: int = 5\n", "def r() -> int:\n    return nb(G[int].x)\n"),
        [AThirdPlacement(DeclaringRelation.Enclosing)] = new(NestedReader(memberAbove: false, "x: int = 5"), "SPY0220", r => Refused(r, "SPY0220"), Also: new BehaviourProbe[]
        {
            new(NestedReader(memberAbove: true, "x: int = 5"), "SPY0220", r => Refused(r, "SPY0220")),
            new(NestedReader(memberAbove: false, "x: auto = 5"), "SPY0908 CS1503", r => Refused(r, "SPY0908", "CS1503")),
            new(NestedReader(memberAbove: true, "x: auto = 5"), "SPY0908 CS1503", r => Refused(r, "SPY0908", "CS1503")),
        }),
        [EnumBodyHoldsCases] = new("enum H:\n    type K0 = int\n    RED = 1\n" + TailMain, "SPY0103", r => Refused(r, "SPY0103") || Refused(r, "SPY0101")),
        [UnionBodyHoldsCases] = new("union H:\n    type K0 = int\n    case A()\n" + TailMain, "SPY0104", r => Refused(r, "SPY0104")),
        [NeedsItsValue] = new("const x: int\ndef main() -> None:\n    let y: int\n    print(1)\n", "SPY0104", r => r.ErrorCodes.Count(c => c == "SPY0104") == 2),
        [DelegateHasNoBody] = new("delegate D(x: int) -> int:\n    type K0 = int\n" + TailMain, "SPY0100", r => Refused(r, "SPY0100")),
        [EnumBodyHoldsNoConst] = new("enum H:\n    const K: int = 1\n    RED = 1\n" + TailMain, "SPY0101", r => Refused(r, "SPY0101")),
        [UnionBodyHoldsNoConst] = new("union H:\n    const K: int = 1\n    case A()\n" + TailMain, "SPY0104", r => Refused(r, "SPY0104")),
        [LambdaTakesNoCallableSlot] = new("def five() -> int:\n    return 5\ndef main() -> None:\n    f = lambda p: () -> int = five: p\n    print(1)\n", "SPY0103", r => Refused(r, "SPY0103"), Also: new BehaviourProbe[]
        {
            new("def five() -> int:\n    return 5\ndef main() -> None:\n    f = (p: () -> int = five) -> p\n    print(1)\n", "SPY0401", r => Refused(r, "SPY0401")),
            new("import math\ndef main() -> None:\n    f = (p: (float) -> float = math.sqrt) -> p\n    print(1)\n", "SPY0908 CS1736", r => Refused(r, "SPY0908", "CS1736"), Stdlib: true),
        }),
        [AnIntEnum] = new("enum H:\n    RED = 1.5\n" + TailMain, "SPY0253", r => Refused(r, "SPY0253")),
        [AnEscapedNameIsMissingAsItsPlainOne] = new("type K0 = `nosuch`\ndef f(k: K0) -> int:\n    return 1\n" + TailMain, "SPY0202", r => Refused(r, "SPY0202"),
            Also: new BehaviourProbe[] { new("type K0 = nosuch\ndef f(k: K0) -> int:\n    return 1\n" + TailMain, "SPY0202", r => Refused(r, "SPY0202")) }),
        [NamesAType] = new("class G:\n    class x:\n        v: int = 5\ndef f(p: int = G.x) -> int:\n    return p\n" + TailMain, "SPY0220", r => Refused(r, "SPY0220")),
        [NoSuchMember] = new("def f(p: int = int.nosuch) -> int:\n    return p\n" + TailMain, "SPY0203", r => Refused(r, "SPY0203")),
        [ACallByShape] = new("def five() -> int:\n    return 5\ndef f(p: int = five()) -> int:\n    return p\n" + TailMain, "SPY0401", r => Refused(r, "SPY0401")),
        [ALocalInAConstantPosition] = new("def main() -> None:\n    y: int = 5\n    f = lambda p: int = y: p\n    print(f())\n", "SPY0401", r => Refused(r, "SPY0401")),
        [NamesAModuleType] = new("import pathlib\ndef f(p: int = pathlib.Path) -> int:\n    return p\n" + TailMain, "SPY0220", r => Refused(r, "SPY0220"), Stdlib: true),
        [AnInstanceMemberThroughItsType] = new("from system import DateTime\ndef main() -> None:\n    x: object = DateTime.year\n    print(1)\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120")),
        [ASiblingCaseIsNoName] = new("enum H:\n    RED = 5\n    BLUE = RED\ndef main() -> None:\n    print(H.BLUE.value)\n", "SPY0200", r => Refused(r, "SPY0200")),
        [NoSelfAtATypeBody] = new("class H:\n    const x: int = 5\n    const K: int = self.x\ndef main() -> None:\n    print(H.K)\n", "SPY0200", r => Refused(r, "SPY0200")),
        [AGenericMethodThroughItsType] = new("from system import Array\ndef main() -> None:\n    x: object = Array.resize\n    print(1)\n", "SPY0908 CS8917", r => Refused(r, "SPY0908", "CS8917")),
        [LambdaTakesOneNameForAType] = new("import system\ndef main() -> None:\n    f = lambda p: system.DayOfWeek: p\n    print(1)\n", "SPY0103", r => Refused(r, "SPY0103"), Also: new[]
        {
            Prints("import system\ndef main() -> None:\n    f = (p: system.DayOfWeek = system.DayOfWeek.Monday) -> p\n    print(f())\n", "DayOfWeek.Monday"),
        }),
        [AliasedIntEnumRead(EnumMemberAccessKind.IntValue)] = new(IntEnum + "\ntype A = E0\ndef main() -> None:\n    print(A.RED.value)\n", "SPY0908 CS1061", r => Refused(r, "SPY0908", "CS1061")),
        [AliasedIntEnumRead(EnumMemberAccessKind.IntName)] = new(IntEnum + "\ntype A = E0\ndef main() -> None:\n    print(A.RED.name)\n", "SPY0908 CS1061", r => Refused(r, "SPY0908", "CS1061")),
        [InterfaceStaticIsRefused("field")] = new("interface H:\n    @static\n    x: int = 5\n    def m(self) -> int: ...\ndef main() -> None:\n    print(H.x)\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120")),
        [InterfaceStaticIsRefused("property")] = new("interface H:\n    @static\n    property x: int = 5\n    def m(self) -> int: ...\ndef main() -> None:\n    print(H.x)\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120")),
        [CharNamesNoReceiver] = new("def main() -> None:\n    x: object = char.is_ascii_digit\n    print(1)\n", "SPY0200", r => Refused(r, "SPY0200")),
        [AStoredLambdaLosesItsDefault] = new("class H:\n    @static\n    g: auto = lambda p1: int = 5: p1\ndef main() -> None:\n    print(H.g())\n", "SPY0908 CS7036", r => Refused(r, "SPY0908", "CS7036")),
        [ALambdaDefaultInAPropertyDefault(false)] = new("class H:\n    property r: int = (lambda p1: int = 5: p1)()\ndef main() -> None:\n    print(H().r)\n", "SPY0908 CS0103", r => Refused(r, "SPY0908", "CS0103")),
        [ALambdaDefaultInAPropertyDefault(true)] = new("class H:\n    @static\n    property r: int = (lambda p1: int = 5: p1)()\ndef main() -> None:\n    print(H.r)\n", "SPY0908 CS0103", r => Refused(r, "SPY0908", "CS0103")),
        [ACallInAConstantOnlyPosition] = new("class H:\n    def r(self, p0: int = (lambda p1: int = 5: p1)()) -> int:\n        return p0\n" + TailMain, "SPY0401", r => Refused(r, "SPY0401")),
        [NoSelfInitOnADataclass] = Refuses(DataclassField + "    def again(self) -> None:\n        self.__init__(7)\n" + TailMain, "SPY0350") with
        {
            Also = new[]
            {
                Refuses(PlainField + "    def again(self) -> None:\n        self.__init__(7)\n" + TailMain, "SPY0350"),
                Prints(DataclassField + "    def __init__(self):\n        self.v = 7\ndef main() -> None:\n    print(DC().v)\n", "7"),
            },
        },
        [AConstructionIsCheckedAtNeitherOrder] = UncheckedConstruction("", "DC().v", above: true) with
        {
            Also = new[]
            {
                UncheckedConstruction("def r() -> int:\n    return DC().v\n", "r()", above: false),
                UncheckedConstruction("class D(DC):\n    pass\n", "D().v", above: true),
                UncheckedConstruction("class D(DC):\n    pass\n", "D().v", above: false),
                // Below, the deriving class's declaration alone compiles: the one defect of the probe above is its construction.
                Prints(Ordered(DataclassField, "class D(DC):\n    pass\n", above: false, "1"), "1"),
                UncheckedConstruction("@dataclass\nclass E(DC):\n    w: int\n", "E(5).v", above: true),
                Prints(Ordered(DataclassField, "@dataclass\nclass E(DC):\n    w: int\n", above: true, "1"), "1"),
            },
        },
        [AClassInAFunctionDeclaresNoName] = Refuses("def main() -> None:\n    @dataclass\n    class DC:\n        v: int\n    print(1)\n", "SPY0708") with
        {
            Also = new[]
            {
                Refuses("def main() -> None:\n    class DC:\n        v: int = 5\n    print(1)\n", "SPY0708"),
                Refuses("def main() -> None:\n    if True:\n        @dataclass\n        class DC:\n            v: int\n    print(1)\n", "SPY0708"),
                Refuses("@dataclass\nclass Rd:\n    w: int\n    def t(self) -> int:\n        class D(Rd):\n            pass\n        return 1\n" + TailMain, "SPY0708"),
            },
        },
        [OnlyAClassIsADataclass] = new("@dataclass\nstruct DC:\n    v: int\n" + TailMain, "SPY0380", r => Refused(r, "SPY0380"), Also: new BehaviourProbe[]
        {
            new("@dataclass\ninterface DC:\n    def f(self) -> int: ...\n" + TailMain, "SPY0380", r => Refused(r, "SPY0380")),
            new("@dataclass\nenum DC:\n    RED = 1\n" + TailMain, "SPY0380", r => Refused(r, "SPY0380")),
        }),
        // What tells "accepted unchecked" from "read": the unknown decorator runs on a union too, and is refused on a class.
        [AUnionTakesADecoratorUnchecked] = Prints("@dataclass\nunion DC:\n    case A()\n" + TailMain, "1", Also: new[]
        {
            Prints("@nonsense\nunion DC:\n    case A()\n" + TailMain, "1"),
            Refuses("@nonsense\nclass DC:\n    v: int = 1\n" + TailMain, "SPY0444"),
        }),
        [ADelegateTakesNoDecorator] = new("@dataclass\ndelegate DC(x: int) -> int\n" + TailMain, "SPY0105", r => Refused(r, "SPY0105")),
        [AnOptionLeavesTheParameters(OptionSpelling(nameof(DataclassOptions.Frozen)))] = OptionProbe(OptionSpelling(nameof(DataclassOptions.Frozen))),
        [AnOptionLeavesTheParameters(OptionSpelling(nameof(DataclassOptions.Eq)))] = OptionProbe(OptionSpelling(nameof(DataclassOptions.Eq))),
        [AnOptionLeavesTheParameters(OptionSpelling(nameof(DataclassOptions.Repr)))] = OptionProbe(OptionSpelling(nameof(DataclassOptions.Repr))),
        [NoSelfInitInAClass] = Refuses(PlainField + "    def again(self) -> None:\n        self.__init__(7)\n" + TailMain, "SPY0350"),
        [AForwardedConstructionIsUnchecked] = Crashes(Ordered(PlainField, ForwardingClass, above: true, "D(\"a\").v"), "CS1503") with
        {
            Also = new[]
            {
                Crashes(Ordered(PlainField, ForwardingClass, above: false, "D(\"a\").v"), "CS1503"),
                // The forwarding class alone is no defect: handed what the `__init__` takes, it runs at both orders.
                Prints(Ordered(PlainField, ForwardingClass, above: true, "D(5).v"), "5"),
                Prints(Ordered(PlainField, ForwardingClass, above: false, "D(5).v"), "5"),
                // The same under each argument shape, over a nested class, and for the two other ways of not fitting.
                Crashes(Ordered(ListTakingClass + "\n", ForwardingClass, above: true, "D(\"a\").v"), "CS1503"),
                Crashes(Ordered(ListTakingClass + "\n", ForwardingClass, above: false, "D(\"a\").v"), "CS1503"),
                Crashes(Ordered(DictTakingClass + "\n", ForwardingClass, above: true, "D(\"a\").v"), "CS1503"),
                Crashes(Ordered(DictTakingClass + "\n", ForwardingClass, above: false, "D(\"a\").v"), "CS1503"),
                Crashes(Ordered(GenericWrittenClass + "\n", "class D(DC[int]):\n    pass\n", above: true, "D(\"a\").v"), "CS1503"),
                Crashes(Ordered(GenericWrittenClass + "\n", "class D(DC[int]):\n    pass\n", above: false, "D(\"a\").v"), "CS1503"),
                Crashes(Ordered(NestedWrittenClass + "\n", "class D(O.DC):\n    pass\n", above: true, "D(\"a\").v"), "CS1503"),
                Crashes(Ordered(NestedWrittenClass + "\n", "class D(O.DC):\n    pass\n", above: false, "D(\"a\").v"), "CS1503"),
                Crashes(Ordered(PlainField, ForwardingClass, above: true, "D().v"), "CS7036"),
                Crashes(Ordered(PlainField, ForwardingClass, above: false, "D().v"), "CS7036"),
                Crashes(Ordered(PlainField, ForwardingClass, above: true, "D(w=5).v"), "CS1739"),
                Crashes(Ordered(PlainField, ForwardingClass, above: false, "D(w=5).v"), "CS1739"),
            },
        },
        [AForwardedArgumentHasNoParameterType] = Refuses(Ordered(ListTakingClass + "\n", ForwardingClass, above: true, "D([]).v"), "SPY0227") with
        {
            Also = new[]
            {
                Refuses(Ordered(ListTakingClass + "\n", ForwardingClass, above: false, "D([]).v"), "SPY0227"),
                // Handed to the class itself, the empty list has its type from the parameter.
                Prints(Ordered(ListTakingClass + "\n", "def r() -> int:\n    return DC([]).v\n", above: true, "r()"), "5"),
                Refuses(Ordered(DictTakingClass + "\n", ForwardingClass, above: true, "D({}).v"), "SPY0227"),
                Refuses(Ordered(DictTakingClass + "\n", ForwardingClass, above: false, "D({}).v"), "SPY0227"),
                Prints(Ordered(DictTakingClass + "\n", "def r() -> int:\n    return DC({}).v\n", above: true, "r()"), "5"),
            },
        },
        [AStructIsNoBase] = Refuses(Ordered(StructWrittenClass + "\n", ForwardingClass, above: true, "1"), "SPY0281") with
        {
            Also = new[]
            {
                Refuses(Ordered(StructWrittenClass + "\n", ForwardingClass, above: false, "1"), "SPY0281"),
                Refuses(Ordered(StructWrittenClass + "\n", "struct D(DC):\n    pass\n", above: true, "1"), "SPY0281"),
            },
        },
        [AnObjectOperandIsComparedByReference] = Prints(Ordered(DataclassField, AgainstAnObject + "\n", above: true, "r()"), "False") with
        {
            Also = new[] { Refuses(Ordered(DataclassField, AgainstAnObject + "\n", above: false, "r()"), "SPY0222") },
        },
        // The run dies in a child process, which the harness waits for: no code, a non-zero exit and the runtime's own words.
        [AStrThatReadsItsOwnReprCallsItself] = new(Ordered(DataclassField, StrThroughSelfRepr + "\n", above: true, "str(D(5))"), "a run that dies of a stack overflow",
            r => r.ErrorCodes.Count == 0 && r.ExitCode is not (null or 0) && r.Stderr.Contains("Stack overflow", StringComparison.Ordinal), Also: new[]
            {
                Refuses(Ordered(DataclassField, StrThroughSelfRepr + "\n", above: false, "str(D(5))"), "SPY0203"),
                // The `super()` spelling of the same read, which is the cell beside it, runs.
                Prints(Ordered(DataclassField, StrThroughSuperRepr + "\n", above: true, "str(D(5))"), "v=5)"),
            }),
        [TheBaseInitIsBoundPastTheDataclass] = Refuses(OverABase(BaseWithInit, DerivedCallsSuper, above: true, "D().v"), "SPY0224") with
        {
            Also = new[]
            {
                Refuses(OverABase(BaseWithInit, DerivedCallsSuper, above: false, "D().v"), "SPY0224"),
                Crashes(OverABase(BaseWithInit, DerivedCallsSuperWithNothing, above: true, "D().v"), "CS7036"),
                Crashes(OverABase(BaseWithInit, DerivedCallsSuperWithNothing, above: false, "D().v"), "CS7036"),
            },
        },
        [ABaseInitWithParametersIsRefused] = new(OverABase("class G:\n    a: int = 0\n    def __init__(self, a: int):\n        self.a = a\n", "", above: true, "DC(5).v"), "SPY0908 CS7036", r => Refused(r, "SPY0908", "CS7036"),
            Also: new BehaviourProbe[]
            {
                new(OverABase("class G:\n    a: int = 0\n    def __init__(self, a: int):\n        self.a = a\n", DerivedCallsSuper, above: true, "D().v"), "SPY0908 CS7036", r => Refused(r, "SPY0908", "CS7036")),
                new(OverABase("class G:\n    a: int = 0\n    def __init__(self, a: int):\n        self.a = a\n", DerivedCallsSuper, above: false, "D().v"), "SPY0908 CS7036", r => Refused(r, "SPY0908", "CS7036")),
            }),
        [AReaderInsideTheDataclass] = Prints("def mk(f: (int) -> DC) -> int:\n    return f(5).v\n" + DataclassField + "    def t(self) -> int:\n        return mk(DC)\ndef main() -> None:\n    print(DC(1).t())\n", "5"),
        [ADerivingHostLosesTheDefault] = Crashes(Ordered(DefaultedDataclass + "\n", "class D(DC):\n    pass\n", above: true, "D().v"), "CS7036") with
        {
            Also = new[]
            {
                Crashes(Ordered(DefaultedDataclass + "\n", "class D(DC):\n    pass\n", above: false, "D().v"), "CS7036"),
                Crashes(Ordered(DefaultedDataclass + "\n", "@dataclass\nclass E(DC):\n    w: int = 6\n", above: true, "E().v"), "CS7036"),
                Prints(Ordered(DefaultedDataclass + "\n", "@dataclass\nclass E(DC):\n    w: int = 6\n", above: false, "E().v"), "3"),
            },
        },
        [AConstructedGenericIsNoReference] = Refuses(Ordered(GenericDataclass, "def r() -> int:\n    return mk(DC[int])\n", above: true, "r()", GenericHelper), "SPY0339") with
        {
            Also = new[]
            {
                Refuses(Ordered(GenericDataclass, "def r() -> int:\n    return mk(DC[int])\n", above: false, "r()", GenericHelper), "SPY0339"),
                Refuses(Ordered("class DC[T]:\n    v: T\n    def __init__(self, v: T):\n        self.v = v\n", "def r() -> int:\n    return mk(DC[int])\n", above: true, "r()", GenericHelper), "SPY0339"),
            },
        },
        [ADataclassOverAConstructedGeneric] = Crashes(Ordered(GenericDataclass, "@dataclass\nclass E(DC[int]):\n    w: int\n", above: true, "E(5, 6).v"), "CS0246") with
        {
            Also = new[] { Crashes(Ordered(GenericDataclass, "@dataclass\nclass E(DC[int]):\n    w: int\n", above: false, "E(5, 6).v"), "CS1729", "CS7036") },
        },
        [ATypeNestedTwoDeepIsNoReference] = Crashes(Ordered(DeepClass("@dataclass\n", "v: int"), "def r() -> int:\n    return mk(O.P.DC)\n", above: true, "r()", DeepHelper), "CS0119") with
        {
            Also = new[]
            {
                Crashes(Ordered(DeepClass("@dataclass\n", "v: int"), "def r() -> int:\n    return mk(O.P.DC)\n", above: false, "r()", DeepHelper), "CS0119"),
                Crashes(Ordered(DeepClass("", "v: int = 0\ndef __init__(self, v: int):\n    self.v = v"), "def r() -> int:\n    return mk(O.P.DC)\n", above: true, "r()", DeepHelper), "CS0119"),
            },
        },
        [AnAliasIsNotConstructed] = Crashes(Ordered(DataclassField + "type A = DC\n", "def r() -> int:\n    return A(5).v\n", above: true, "r()"), "CS0103") with
        {
            Also = new[]
            {
                Crashes(Ordered(DataclassField + "type A = DC\n", "def r() -> int:\n    return A(5).v\n", above: false, "r()"), "CS0103"),
                Crashes(Ordered(PlainField + "type A = DC\n", "def r() -> int:\n    return A(5).v\n", above: true, "r()"), "CS0103"),
            },
        },
        [AnAliasIsNoBase] = Refuses(Ordered(DataclassField + "type A = DC\n", "class D(A):\n    pass\n", above: true, "D(5).v"), "SPY0202") with
        {
            Also = new[]
            {
                Refuses(Ordered(DataclassField + "type A = DC\n", "class D(A):\n    pass\n", above: false, "D(5).v"), "SPY0202"),
                Refuses(Ordered(DataclassField + "type A = DC\n", "class D(A):\n    def __init__(self):\n        super().__init__(5)\n", above: true, "D().v"), "SPY0202"),
                Refuses(Ordered(DataclassField + "type A = DC\n", "@dataclass\nclass E(A):\n    w: int\n", above: true, "E(5, 6).v"), "SPY0202"),
                Refuses(Ordered(PlainField + "type A = DC\n", "class D(A):\n    pass\n", above: true, "D(5).v"), "SPY0202"),
            },
        },
        [ADataclassOverAForwardingClass] = Crashes(Ordered(DataclassField, "class Mid(DC):\n    pass\n@dataclass\nclass E(Mid):\n    w: int\n", above: true, "E(5, 6).v"), "CS1729", "CS7036") with
        {
            Also = new[]
            {
                Crashes(Ordered(DataclassField, "class Mid(DC):\n    pass\n@dataclass\nclass E(Mid):\n    w: int\n", above: false, "E(5, 6).v"), "CS1729", "CS7036"),
                // The dataclass over the forwarding class is the defect: the forwarding class alone runs.
                Prints(Ordered(DataclassField, "class Mid(DC):\n    pass\n", above: true, "Mid(5).v"), "5"),
            },
        },
        [MapChecksNoElement] = Crashes(Ordered(DataclassField, "def r() -> int:\n    return len(list(map(DC, [\"a\"])))\n", above: true, "r()"), "CS1503") with
        {
            Also = new[]
            {
                Crashes(Ordered(PlainField, "def r() -> int:\n    return len(list(map(DC, [\"a\"])))\n", above: true, "r()"), "CS1503"),
                // The slot's own control: handed what the field takes, the reference runs.
                Prints(Ordered(DataclassField, "def r() -> int:\n    return list(map(DC, [5]))[0].v\n", above: true, "r()"), "5"),
            },
        },
        [ABareNestedNameIsAnotherQuestionFirst] = Prints(Enclosed("@dataclass\n", "v: int", "def r(self) -> int:\n    return mk(DC)", above: true), "5") with
        {
            Also = new[]
            {
                Refuses(Enclosed("@dataclass\n", "v: int", "def r(self) -> int:\n    return mk(DC)", above: false), "SPY0342"),
                Crashes(Enclosed("@dataclass\n", "v: int", "def r(self) -> int:\n    return DC(5).v", above: true), "CS1955"),
                Crashes(Enclosed("@dataclass\n", "v: int", "def r(self) -> int:\n    return DC(5).v", above: false), "CS1955"),
                Crashes(Enclosed("", "v: int = 0\ndef __init__(self, v: int):\n    self.v = v", "def r(self) -> int:\n    return DC(5).v", above: true), "CS1955"),
            },
        },
        [InterfaceStaticIsRefused("method, read uncalled", "#2111")] = new("interface H:\n    @static\n    def x() -> int:\n        return 5\n    def m(self) -> int: ...\ndef main() -> None:\n    f: () -> int = H.x\n    print(f())\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120")),
    };

    /// <summary>What the compiler does with a program when it runs in a process of its own: the behaviour is a crash, which a
    /// probe in this process would not survive. <paramref name="Control"/> is a program the same child compiles, so a child
    /// that dies for a reason of its own (a missing assembly ends a process with the same exit code) is not read as the crash.</summary>
    internal sealed record CrashProbe(string Source, string Control, string Expected, Func<ChildCompile, bool> Holds);

    internal static readonly IReadOnlyDictionary<string, CrashProbe> CrashProbes = new Dictionary<string, CrashProbe>(StringComparer.Ordinal)
    {
        [OverflowsTheCompiler] = new("type K0 = list[K0]\ndef f(k: K0) -> int:\n    return 1\n" + TailMain, "type K0 = list[K0]\n" + TailMain,
            "a stack overflow that ends the process", c => c.Overflowed),
    };

    // ------------------------------------------------------------------------------------------- //
    // no-initializer (R-QI): kind × initializer × host × declared type
    // ------------------------------------------------------------------------------------------- //

    /// <summary>How a kind declares <c>x</c> with a type and no value (<c>{T}</c> the type, <c>{L}</c> a value of it), whether the
    /// declaration sits in a type and how <c>main</c> reads it. <see cref="Stored"/>: the statements that give it a value before
    /// the read, for the kinds R-QI leaves alone.</summary>
    internal sealed record Declared(string Text, bool InType, string Read, string? Accessor = null, string[]? Stored = null, bool Twin = false);

    private static readonly string[] StoredInInit = { "def __init__(self):", "    self.x = {L}" };

    internal static readonly IReadOnlyDictionary<BindingKind, Declared> Declarations = new Dictionary<BindingKind, Declared>
    {
        [BindingKind.ModuleVar] = new("x: {T}", false, "x", Twin: true),
        [BindingKind.ModuleProperty] = new("property x: {T}", false, "x", Accessor: "property get x() -> {T}:\n    return {L}"),
        [BindingKind.StaticField] = new("@static\nx: {T}", true, "H.x", Twin: true),
        [BindingKind.StaticProperty] = new("@static\nproperty x: {T}", true, "H.x", Accessor: "@static\nproperty get x() -> {T}:\n    return {L}"),
        [BindingKind.InstanceField] = new("x: {T}", true, "H().x", Stored: StoredInInit, Twin: true),
        [BindingKind.InstanceProperty] = new("property x: {T}", true, "H().x", Accessor: "property get x(self) -> {T}:\n    return {L}", Stored: StoredInInit),
        [BindingKind.LocalVar] = new("x: {T}", false, "x", Stored: new[] { "x = {L}" }, Twin: true),
    };

    private const string NeedsItsValue = "the declaration's grammar takes a value: there is no spelling without one (SPY0104)";
    private const string NoValueSlot = "declares no storage with a type annotation, so it has no initializer to leave out";

    internal static readonly IReadOnlyDictionary<BindingKind, string> ExcludedDeclarations = new Dictionary<BindingKind, string>
    {
        [BindingKind.ModuleLet] = ModuleLetRefused,
        [BindingKind.ModuleLiteralConst] = NeedsItsValue,
        [BindingKind.ModuleComputedConst] = NeedsItsValue,
        [BindingKind.ClassLiteralConst] = NeedsItsValue,
        [BindingKind.ClassComputedConst] = NeedsItsValue,
        [BindingKind.LocalLet] = NeedsItsValue,
        [BindingKind.LocalLiteralConst] = NeedsItsValue,
        [BindingKind.LocalComputedConst] = NeedsItsValue,
        [BindingKind.LambdaTypedLocal] = "a local `x: T` with another type: the LocalVar cell's `callable` type is its shape",
        [BindingKind.ModuleDef] = NoValueSlot,
        [BindingKind.ModuleClass] = NoValueSlot,
        [BindingKind.ModuleStruct] = NoValueSlot,
        [BindingKind.ModuleEnum] = NoValueSlot,
        [BindingKind.ModuleUnion] = NoValueSlot,
        [BindingKind.ModuleInterface] = NoValueSlot,
        [BindingKind.ModuleTypeAlias] = NoValueSlot,
        [BindingKind.StaticMethod] = NoValueSlot,
        [BindingKind.NestedType] = NoValueSlot,
        [BindingKind.InstanceMethod] = NoValueSlot,
        [BindingKind.Constructor] = NoValueSlot,
        [BindingKind.LocalDef] = NoValueSlot,
        [BindingKind.EnclosingLocal] = "a local read from a nested function: the LocalVar cell's declaration",
        [BindingKind.Parameter] = "a parameter's value is its argument",
        [BindingKind.Inner] = "bound by its comprehension or lambda",
        [BindingKind.Builtin] = "not declared by the program",
        [BindingKind.ImportedName] = "not declared by the program",
        [BindingKind.None] = "no binding",
    };

    /// <summary>A host of a member declaration as a TYPED part of a cell's key: its kind and the keyword that opens its body,
    /// which is how the id spells it. Typed, because the keywords <c>class</c> and <c>struct</c> are also the labels of two
    /// declared types (<see cref="ValueTypes"/>): as text, a cell of either carried both, and the coverage check below could not
    /// tell a host from a type (the review of PR #2767, F7).</summary>
    internal sealed record DeclarationHost(TypeKind Kind, string Keyword)
    {
        public override string ToString() => Keyword;
    }

    /// <summary>The hosts of a member declaration, total over <c>TypeKind</c> with <see cref="ExcludedDeclarationHosts"/>.</summary>
    internal static readonly IReadOnlyDictionary<TypeKind, DeclarationHost[]> DeclarationHosts = new Dictionary<TypeKind, DeclarationHost[]>
    {
        [TypeKind.Class] = new[] { new DeclarationHost(TypeKind.Class, "class") },
        [TypeKind.Struct] = new[] { new DeclarationHost(TypeKind.Struct, "struct") },
    };

    /// <summary>The host of a no-initializer cell, or null for a declaration outside every type.</summary>
    internal static DeclarationHost? HostOf(NameCell cell) => cell.Key!.Parts.OfType<DeclarationHost>().FirstOrDefault();

    internal static readonly IReadOnlyDictionary<TypeKind, string> ExcludedDeclarationHosts = new Dictionary<TypeKind, string>
    {
        [TypeKind.Interface] = InAnInterfaceEachKindIsOut,
        [TypeKind.Enum] = EnumBodyHoldsCases,
        [TypeKind.Union] = UnionBodyHoldsCases,
        [TypeKind.Delegate] = DelegateHasNoBody,
    };

    /// <summary>An interface that declares <paramref name="declared"/>, a class that implements it with
    /// <paramref name="implemented"/>, and a read of the member through the interface.</summary>
    private static string InterfaceMember(string declared, string implemented)
        => $"interface H:\n    {declared}\n    def m(self) -> int: ...\nclass C(H):\n    {implemented}\n    def m(self) -> int:\n        return 1\ndef main() -> None:\n    h: H = C()\n    print(h.x)\n";

    /// <summary>A declared type: its label in a cell's id, its spelling, a value of it, how <c>main</c> shows that value as
    /// <c>5</c> (<c>{0}</c> the read), the declarations it needs, and whether the value is one a constant position admits
    /// (a literal, an enum member, a collection literal at a field: <c>AdmissionTable.PerInstanceFieldDefault</c>).</summary>
    internal sealed record ValueType(string Label, string Spelling, string Literal, string Show, string? Prelude = null, bool Twin = false, bool Constant = false);

    /// <summary>The declared types, total over the concrete subclasses of <c>SemanticType</c> with <see cref="ExcludedValueTypes"/>.
    /// What a missing initializer leaves behind differs by type (<c>0</c>, <c>None</c>, a default struct), and R-QI refuses all of them.</summary>
    internal static readonly IReadOnlyDictionary<Type, ValueType[]> ValueTypes = new Dictionary<Type, ValueType[]>
    {
        [typeof(BuiltinType)] = new[] { new ValueType("int", "int", "5", "{0}", Twin: true, Constant: true), new ValueType("str", "str", "\"5\"", "{0}", Constant: true) },
        [typeof(GenericType)] = new[] { new ValueType("list", "list[int]", "[5]", "{0}[0]", Constant: true) },
        [typeof(UserDefinedType)] = new[]
        {
            new ValueType("class", "C0", "C0()", "{0}.v", "class C0:\n    v: int = 5"),
            new ValueType("struct", "S0", "S0()", "{0}.v", "struct S0:\n    v: int = 5"),
            new ValueType("enum", "E0", "E0.RED", "{0}.value", "enum E0:\n    RED = 5", Constant: true),
        },
        [typeof(OptionalType)] = new[] { new ValueType("optional", "int?", "Some(5)", "{0}.unwrap()") },
        [typeof(NullableType)] = new[] { new ValueType("nullable", "str | None", "\"5\"", "{0}", Constant: true) },
        [typeof(ResultType)] = new[] { new ValueType("result", "int !str", "Ok(5)", "{0}.unwrap()") },
        [typeof(Sharpy.Compiler.Semantic.FunctionType)] = new[] { new ValueType("callable", "() -> int", "lambda: 5", "{0}()") },
        [typeof(TupleType)] = new[] { new ValueType("tuple", "tuple[int, int]", "(5, 6)", "{0}[0]") },
        [typeof(LiteralStringType)] = new[] { new ValueType("literal-string", "LiteralString", "\"5\"", "{0}", Constant: true) },
    };

    private const string NoAnnotationSpellsIt = "the checker's type of an expression: no annotation spells it, so no declaration has it";

    internal static readonly IReadOnlyDictionary<Type, string> ExcludedValueTypes = new Dictionary<Type, string>
    {
        [typeof(UnknownType)] = NoAnnotationSpellsIt,
        [typeof(VoidType)] = "`None` as a return type: no storage has it",
        [typeof(ModuleType)] = NoAnnotationSpellsIt,
        [typeof(GenericFunctionType)] = NoAnnotationSpellsIt,
        [typeof(ConstructorReferenceType)] = NoAnnotationSpellsIt,
        [typeof(UnmappedClrType)] = NoAnnotationSpellsIt,
        [typeof(TypeParameterType)] = "needs a generic host: the 8951 pool's excluded axis (ii), InterfaceUnionEnumGenericHosts",
        [typeof(SelfType)] = "`Self` is a member signature's spelling of its own type: a slot of it is the UserDefinedType cell",
        [typeof(UnionType)] = "a placeholder record (its own doc comment): a declared `union` is a UserDefinedType, and only the symbol deserializer builds this one",
        [typeof(TaskType)] = "the type the checker gives an `async def`'s return: a slot of it needs an awaited value, and async is outside N1 to N7",
    };

    // ------------------------------------------------------------------------------------------- //
    // typed-read: kind × annotation × order × spelling × reader position
    // ------------------------------------------------------------------------------------------- //

    internal enum MemberLevel
    {
        Module,
        Type,
        Instance,
    }

    /// <summary>An int-valued declaration of <c>x</c> per kind, with its inferred spelling where the kind has one, its level and
    /// the text that reads it after its receiver (<c>x</c>, or <c>x()</c> for a callable).</summary>
    internal sealed record Typed(string Annotated, string? Inferred, MemberLevel Level, string Member, bool NeedsFive = false);

    internal static readonly IReadOnlyDictionary<BindingKind, Typed> TypedReads = new Dictionary<BindingKind, Typed>
    {
        [BindingKind.ModuleVar] = new("x: int = 5", "x: auto = 5", MemberLevel.Module, "x"),
        [BindingKind.ModuleLiteralConst] = new("const x: int = 5", "const x = 5", MemberLevel.Module, "x"),
        [BindingKind.ModuleComputedConst] = new("const x: int = five()", "const x = five()", MemberLevel.Module, "x", NeedsFive: true),
        [BindingKind.ModuleProperty] = new("property x: int = 5", null, MemberLevel.Module, "x"),
        [BindingKind.ModuleDef] = new("def x() -> int:\n    return 5", null, MemberLevel.Module, "x()"),
        [BindingKind.StaticField] = new("@static\nx: int = 5", "@static\nx: auto = 5", MemberLevel.Type, "x"),
        [BindingKind.StaticProperty] = new("@static\nproperty x: int = 5", null, MemberLevel.Type, "x"),
        [BindingKind.StaticMethod] = new("@static\ndef x() -> int:\n    return 5", null, MemberLevel.Type, "x()"),
        [BindingKind.ClassLiteralConst] = new("const x: int = 5", "const x = 5", MemberLevel.Type, "x"),
        [BindingKind.ClassComputedConst] = new("const x: int = five()", "const x = five()", MemberLevel.Type, "x", NeedsFive: true),
        [BindingKind.InstanceField] = new("x: int = 5", "x: auto = 5", MemberLevel.Instance, "x"),
        [BindingKind.InstanceProperty] = new("property x: int = 5", null, MemberLevel.Instance, "x"),
        [BindingKind.InstanceMethod] = new("def x(self) -> int:\n    return 5", null, MemberLevel.Instance, "x()"),
    };

    private const string NotAnIntValue = "its read is no int value at a module or type level: the mismatch against bool is not a question of order";
    private const string ALocal = "a local: a read above it is R-KX's (P80)";

    internal static readonly IReadOnlyDictionary<BindingKind, string> ExcludedTypedReads = new Dictionary<BindingKind, string>
    {
        [BindingKind.ModuleLet] = ModuleLetRefused,
        [BindingKind.ModuleClass] = NotAnIntValue,
        [BindingKind.ModuleStruct] = NotAnIntValue,
        [BindingKind.ModuleEnum] = NotAnIntValue,
        [BindingKind.ModuleUnion] = NotAnIntValue,
        [BindingKind.ModuleInterface] = NotAnIntValue,
        [BindingKind.ModuleTypeAlias] = NotAnIntValue,
        [BindingKind.NestedType] = NotAnIntValue,
        [BindingKind.Constructor] = NotAnIntValue,
        [BindingKind.EnclosingLocal] = ALocal,
        [BindingKind.Parameter] = ALocal,
        [BindingKind.Inner] = ALocal,
        [BindingKind.LocalVar] = ALocal,
        [BindingKind.LocalLet] = ALocal,
        [BindingKind.LocalLiteralConst] = ALocal,
        [BindingKind.LocalComputedConst] = ALocal,
        [BindingKind.LocalDef] = ALocal,
        [BindingKind.LambdaTypedLocal] = ALocal,
        [BindingKind.Builtin] = "not declared by the program",
        [BindingKind.ImportedName] = "not declared by the program",
        [BindingKind.None] = "no binding",
    };

    /// <summary>The spellings with no typed-read cell. <c>Call</c> is the 8951 pool's <c>get()</c>: a helper whose DECLARED return
    /// type is what the reader sees, so the read itself sits in the helper's body, which is the function-body cell.</summary>
    internal static readonly IReadOnlyDictionary<Harness.Spelling, string> ExcludedTypedSpellings = new Dictionary<Harness.Spelling, string>
    {
        [Harness.Spelling.Call] = "the reader sees the helper's declared return type; the read sits in the helper's body, which is the FunctionBody cell",
    };

    internal static readonly IReadOnlyDictionary<ReadPosition, string> ExcludedTypedPositions = new Dictionary<ReadPosition, string>
    {
        [ReadPosition.EnumMemberValue] = "an enum member's value has no annotated slot and admits no call: nothing takes the probe (its reads are constant-kind's and the 8951 pool's)",
        [ReadPosition.TypeAliasTarget] = "holds a type's name: no value is read there",
        [ReadPosition.BaseList] = "holds a type's name: no value is read there",
    };

    /// <summary>
    /// The reason a reader in a THIRD type has no typed-read cell (the review of PR #2767, F2: 137 must-run reads of the 8951
    /// pool stood in such a type and were dropped unseen, by a filter that asked for a spelling and an order before it asked
    /// where the reader stood). The reads run in that pool; whether they are TYPED is what no cell here holds, and each
    /// placement's probes are the programs that say what they are today. A switch and no table, as <see cref="ACalleeBinderIsOut"/>.
    /// </summary>
    private const string TheOrdersClass = ": D2's class, which the generated placements hold red (#2423, which holds the program as a cell)";

    private static string AThirdPlacement(DeclaringRelation placement)
    {
        const string BelowIsUntyped = "Handed to a `bool` slot, an annotated member's read is SPY0220 where the member stands above, and SPY0908 CS1503 where ";
        (string where, string today) = placement switch
        {
            DeclaringRelation.Base => ("in a subclass of the member's type (`class H(B)` reads `B.x`, `H.x` or `self.x`)", BelowIsUntyped + "the base stands below its subclass" + TheOrdersClass),
            DeclaringRelation.Other => ("in a type unrelated to the member's (`class H` reads `B.x`)", BelowIsUntyped + "the member's type stands below the reader's" + TheOrdersClass),
            DeclaringRelation.Enclosing => ("in a type nested in the member's (`class I` in `H` reads `H.x`)",
                "Handed to a `bool` slot, an annotated member's read is SPY0220 above and below the nested type, and an inferred one's (`x: auto = 5`) is SPY0908 CS1503 at both, as it is from every generated placement (#2417's class, which the generated rows hold)"),
            _ => throw new ArgumentOutOfRangeException(nameof(placement), placement, "the member's own type, or a placement the 8951 pool does not spell"),
        };
        return $"the reader stands {where}: the 8951 pool runs the read from there, and this set places the reader of a type's member in that type or outside every type. {today}. The placement is {ByItem26}";
    }

    /// <summary>The probe of a read this set has no reader for: the reader above the member's type (untyped today) and below it (typed).</summary>
    private static BehaviourProbe ThirdPlacementProbe(string member, string reader)
        => new($"{Probe}\n{reader}{member}{TailMain}", "SPY0908 CS1503", r => Refused(r, "SPY0908", "CS1503"),
            Also: new BehaviourProbe[] { new($"{Probe}\n{member}{reader}{TailMain}", "SPY0220", r => Refused(r, "SPY0220")) });

    private static string NestedReader(bool memberAbove, string member)
    {
        string declared = $"    @static\n    {member}\n";
        const string Nested = "    class I:\n        @static\n        r: int = nb(H.x)\n";
        return $"{Probe}\nclass H:\n{(memberAbove ? declared + Nested : Nested + declared)}{TailMain}";
    }

    /// <summary>The placements of a reader against the member's type with no typed-read cell: each one the 8951 pool spells a
    /// must-run read from (<see cref="TypedThirdPlacements"/>), and no other.</summary>
    internal static readonly IReadOnlyDictionary<DeclaringRelation, string> ExcludedTypedPlacements = new Dictionary<DeclaringRelation, string>
    {
        [DeclaringRelation.Base] = AThirdPlacement(DeclaringRelation.Base),
        [DeclaringRelation.Other] = AThirdPlacement(DeclaringRelation.Other),
        [DeclaringRelation.Enclosing] = AThirdPlacement(DeclaringRelation.Enclosing),
    };

    private const string AGenericReceiver = "the member is read through its generic type's constructed name (`G[int].x`), which the 8951 pool spells by a label of its own and no `Spelling`; a typed-read kind is declared in a type with no type parameter. Handed to a `bool` slot, an annotated member's read is SPY0220 where the type stands above its reader, and SPY0908 CS1503 where it stands below" + TheOrdersClass + ". The receiver is " + ByItem26;

    /// <summary>The reads the 8951 pool spells by a label of its own, with no <c>Spelling</c>, and no typed-read cell: keyed by
    /// the label, each one the pool spells a must-run read under (<see cref="TypedLabelledReads"/>), and no other. Found by
    /// re-measuring the review's F2: six reads, dropped by the same filter as the third placements.</summary>
    internal static readonly IReadOnlyDictionary<string, string> ExcludedTypedReceivers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GenericReceiver"] = AGenericReceiver,
    };

    /// <summary>A reader of a kind: under which spelling, from which position and nesting, and whether it sits INSIDE the member's own type.</summary>
    internal sealed record TypedReader(BindingKind Kind, Harness.Spelling Spelling, ReadPosition Position, Harness.Nesting Nesting, bool Inside);

    /// <summary>The positions a module-level reader is placed at; every other position is a member of a reader class.</summary>
    internal static readonly IReadOnlySet<ReadPosition> ModulePlacedPositions = Enum.GetValues<ReadPosition>().Where(p =>
    {
        try
        {
            Harness.PlaceAtModule(p, Harness.Nesting.Direct, "0");
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }).ToHashSet();

    /// <summary>
    /// Where a typed read of each kind is legal, READ from the 8951 pool: a (kind, spelling, position, nesting) is a reader exactly
    /// when that pool spells a read of it that must run, declared before its reader, with no outer binding, in a class or at
    /// module level (its sub-generators' routes are that pool's own axes). So the two axes are the images of <c>Spelling</c> and
    /// <see cref="ReadPosition"/> under the pool's own rosters, and a position, nesting or spelling the pool gains for a kind is a
    /// typed-read cell with no edit here. A reader stands in the member's own type or outside every type. The pool also reads from
    /// a third type (a subclass, an unrelated type, a nested one), under labels of its own that carry no <c>Spelling</c> and no
    /// order, so WHERE the reader stands is asked first: such a read is counted per placement
    /// (<see cref="TypedThirdPlacements"/>), each placement is excluded with its reason (<see cref="ExcludedTypedPlacements"/>),
    /// and a read from a placement with none is <see cref="UnplacedTypedReaders"/>, which the axes fact holds empty. A read from
    /// the two placements this set has that carries no <c>Spelling</c> is held the same way, by its label
    /// (<see cref="ExcludedTypedReceivers"/>). One reader
    /// the pool does not spell is added: an instance member read through its type's name is read off a constructed instance
    /// (<c>H().x</c>, <c>A().x</c>), from wherever the pool reads a <c>@static</c> field through that name from outside its type.
    /// </summary>
    private static readonly Lazy<(IReadOnlyList<TypedReader> Readers, IReadOnlyList<string> Unplaced, IReadOnlyDictionary<DeclaringRelation, int> Third, IReadOnlyDictionary<string, int> Labelled)> DerivedTypedReaders = new(() =>
    {
        var readers = new HashSet<TypedReader>();
        var unplaced = new List<string>();
        var third = new SortedDictionary<DeclaringRelation, int>();
        var labelled = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in Harness.GeneratedCells())
        {
            // A read of a typed-read kind that must run, with no outer binding, at a position this set reads from, among the
            // pool's unrouted cells.
            if (c.Family != Family.Read || c.Tag != SharpyTag.MustRun || c.Outer != BindingKind.None || c.Position is not { } position
                || c.Key is not { Route: null } key || !TypedReads.ContainsKey(c.Target) || ExcludedTypedPositions.ContainsKey(position))
                continue;
            if (c.Relation is { } placement && placement != DeclaringRelation.Self)
            {
                third[placement] = third.GetValueOrDefault(placement) + 1;
                if (!ExcludedTypedPlacements.ContainsKey(placement))
                    unplaced.Add($"{c.Id} (relation {placement})");
                continue;
            }
            if (key.Part<Harness.Spelling>() is not { } spelling)
            {
                // Spelled by a label of the pool's own: counted and owed its reason, as a third placement is.
                string label = string.Join("+", key.Marks);
                labelled[label] = labelled.GetValueOrDefault(label) + 1;
                if (!ExcludedTypedReceivers.ContainsKey(label))
                    unplaced.Add($"{c.Id} (spelled by the label '{label}', which is no Spelling)");
                continue;
            }
            // The order is this set's own axis: a reader the pool runs above its member is generated at both orders.
            if (key.Part<Harness.Order>() != Harness.Order.Earlier || ExcludedTypedSpellings.ContainsKey(spelling))
                continue;
            readers.Add(new TypedReader(c.Target, spelling, position, key.Part<Harness.Nesting>() ?? Harness.Nesting.Direct, c.Relation == DeclaringRelation.Self));
        }
        var instances = TypedReads.Where(t => t.Value.Level == MemberLevel.Instance).Select(t => t.Key).ToList();
        foreach (var r in readers.Where(r => r is { Kind: BindingKind.StaticField, Inside: false, Spelling: Harness.Spelling.Qualified or Harness.Spelling.Alias }).ToList())
        {
            foreach (var instance in instances)
                readers.Add(r with { Kind = instance });
        }
        return (readers.OrderBy(r => r.Kind).ThenBy(r => r.Spelling).ThenBy(r => r.Position).ThenBy(r => r.Nesting).ThenBy(r => r.Inside).ToList(), unplaced, third, labelled);
    });

    internal static IReadOnlyList<TypedReader> TypedReaders => DerivedTypedReaders.Value.Readers;

    /// <summary>The pool's must-run reads this set has no reader for and no reason against: the reader stands in a third type
    /// under a placement with no exclusion, or the read is spelled by a label with none.</summary>
    internal static IReadOnlyList<string> UnplacedTypedReaders => DerivedTypedReaders.Value.Unplaced;

    /// <summary>How many must-run reads the pool spells from each third placement: what an exclusion of one rests on.</summary>
    internal static IReadOnlyDictionary<DeclaringRelation, int> TypedThirdPlacements => DerivedTypedReaders.Value.Third;

    /// <summary>How many must-run reads the pool spells under each label that is no <c>Spelling</c>, from the member's own type
    /// or outside every type.</summary>
    internal static IReadOnlyDictionary<string, int> TypedLabelledReads => DerivedTypedReaders.Value.Labelled;

    private const string AReadOfItself = "a declaration that reads itself, directly or through a ring, is N3-cycle's: refused or thrown whatever its slot's type (the 8951 pool's Self and Cycle cells)";
    private const string ARecursion = "a constructor that calls itself is a recursion, not an order of two declarations";
    private const string AReaderInItsOwnClass = "a reader in the class's own body is no second declaration: there is no order of two to vary";
    private const string ARingIsNotVaried = "the set is a star varied from one base cell (the owner's ruling 37): where the reader stands, and the ring with it, is the dataclass-init set's axis and, over a written `__init__`, the written-init set's variation";
    private const string OverflowsTheCompiler = "#2769: named by an annotation, an alias whose target reaches the alias overflows the compiler's stack (`TypeResolver.ExpandTypeAlias`), and a process that dies leaves no verdict a cell can read (its probe compiles in a child process). The class is unruled and out of P78 (owner, #2423 comment 7042723494 item 9), so only the UNUSED alias has cells, and they keep running";

    /// <summary>The orders with no cell, per cell set that has the axis: total over <c>Order</c> with the two every set generates.</summary>
    internal static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<Harness.Order, string>> ExcludedOrders = new Dictionary<string, IReadOnlyDictionary<Harness.Order, string>>(StringComparer.Ordinal)
    {
        ["typed-read"] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = AReadOfItself, [Harness.Order.Cycle] = AReadOfItself },
        ["init-overload"] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = ARecursion, [Harness.Order.Cycle] = ARecursion },
        [DataclassInitRoute] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = AReaderInsideTheDataclass },
        [WrittenInitRoute] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = AReaderInItsOwnClass },
        [DataclassMemberRoute] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = AReaderInItsOwnClass, [Harness.Order.Cycle] = ARingIsNotVaried },
        ["alias-target"] = new Dictionary<Harness.Order, string>(),
        ["constant-kind"] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = AReadOfItself, [Harness.Order.Cycle] = AReadOfItself },
        ["lambda-default"] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = AReadOfItself, [Harness.Order.Cycle] = AReadOfItself },
    };

    /// <summary>The target × use pairs with no cell, total over the two enums with the pairs a cell carries.</summary>
    internal static readonly IReadOnlyDictionary<(ScopeAliasTarget Target, ScopeAliasUse Use), string> ExcludedAliasUses = new Dictionary<(ScopeAliasTarget, ScopeAliasUse), string>
    {
        [(ScopeAliasTarget.Itself, ScopeAliasUse.Used)] = OverflowsTheCompiler,
    };

    /// <summary>The shapes whose name cannot be the alias itself, by label: every other shape has its self-reaching cells.</summary>
    internal static readonly IReadOnlyDictionary<string, string> ExcludedSelfReachingShapes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["dotted"] = "the name is qualified by a module, and an alias is no member of one: `pathlib.K0` names nothing, which is the Missing cell",
    };

    // ------------------------------------------------------------------------------------------- //
    // callee: binder × outer
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The statements that bind <c>{n}</c> to a function returning 1 and print its call, per binder.</summary>
    internal static readonly IReadOnlyDictionary<LocalTarget, string[]> CalleeBinders = new Dictionary<LocalTarget, string[]>
    {
        [LocalTarget.For] = new[] { "for {n} in [one]:", "    print({n}())" },
        [LocalTarget.Comprehension] = new[] { "print([{n}() for {n} in [one]][0])" },
        [LocalTarget.WithAs] = new[] { "with CM() as {n}:", "    print({n}())" },
    };

    /// <summary>The reason of a binder that could carry a callee cell and has none (the review of PR #2767, F1: the earlier
    /// reasons said these bind nothing a call calls, which a typed subject refutes). Each rests on two programs. A switch and no
    /// table: the probes' keys are built from it before any later field of this file is.</summary>
    private static string ACalleeBinderIsOut(LocalTarget binder)
    {
        string binds = binder switch
        {
            LocalTarget.MatchCapture => "`case x:` over a function-typed local (`f: () -> int = one`, `match f:`)",
            LocalTarget.AsPatternCapture => "`case _ as x:` over a function-typed local (`f: () -> int = one`, `match f:`)",
            LocalTarget.OutLet => "`give(out let x)`, its parameter typed through an alias (`type F = () -> int`, `def give(f: out F)`)",
            _ => throw new ArgumentOutOfRangeException(nameof(binder), binder, "a generated binder, or one with a reason of its own"),
        };
        return $"{binds} binds a function value: called, the name prints `1` with no outer binding, and under a module `x: int = 12` the call is SPY0908 CS1955, the callee lookup's class the generated binders hold red (#2423, which holds the program as a cell). The binder is {ByItem26}";
    }

    private const string AnExceptionIsCalled = "`except E as x:` binds an exception, and one whose class declares `__call__` is called: `x()` prints `1` with and without a module `x: int = 12`. The binder is " + ByItem26 + ", and no tracker holds the program, since it is green";

    private const string CallableException = "class E(Exception):\n    def __call__(self) -> int:\n        return 1\ndef main() -> None:\n    try:\n        raise E()\n    except E as x:\n        print(x())\n";

    /// <summary>The probe of <see cref="ACalleeBinderIsOut"/>: the binder's statements in <c>main</c> under a module
    /// <c>x: int = 12</c> (the red program), and the same with no outer binding (its control, which runs).</summary>
    private static BehaviourProbe CalleeBinderProbe(string statements, string declarations = "")
    {
        string program = $"{One}\n{declarations}{Main}    {statements}\n";
        return new("x: int = 12\n" + program, "SPY0908 CS1955", r => Refused(r, "SPY0908", "CS1955"), Also: new[] { Prints(program, "1") });
    }

    internal static readonly IReadOnlyDictionary<LocalTarget, string> ExcludedCalleeBinders = new Dictionary<LocalTarget, string>
    {
        [LocalTarget.ExceptAs] = AnExceptionIsCalled,
        [LocalTarget.MatchCapture] = ACalleeBinderIsOut(LocalTarget.MatchCapture),
        [LocalTarget.AsPatternCapture] = ACalleeBinderIsOut(LocalTarget.AsPatternCapture),
        [LocalTarget.OutLet] = ACalleeBinderIsOut(LocalTarget.OutLet),
    };

    /// <summary>Where an outer binding of the name sits against the binder: at module level (or nowhere, or the global scope),
    /// as a member of the type whose method holds the binder, or as a local of the function whose nested def holds it.</summary>
    internal enum OuterScope
    {
        Module,
        Member,
        Enclosing,
    }

    /// <summary>An outer binding: the name both bindings share, its declaration, where it sits, whether python runs the same
    /// program, and whether it imports the Stdlib.</summary>
    internal sealed record OuterBinding(string Name, string Text, OuterScope Scope, bool Twin = false, bool Stdlib = false);

    /// <summary>The outer bindings of the callee, total over <see cref="BindingKind"/> with <see cref="ExcludedCalleeOuters"/>:
    /// every kind that can hold the name outside the binder's own scope. The outer's value is 12 and the binder's function
    /// returns 1, so a call that reaches the outer binding prints something else or does not compile.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, OuterBinding> CalleeOuters = new Dictionary<BindingKind, OuterBinding>
    {
        [BindingKind.None] = new("x", "", OuterScope.Module, Twin: true),
        [BindingKind.ModuleVar] = new("x", "x: int = 12", OuterScope.Module, Twin: true),
        [BindingKind.ModuleLiteralConst] = new("x", "const x: int = 12", OuterScope.Module),
        [BindingKind.ModuleComputedConst] = new("x", "const x: int = twelve()", OuterScope.Module),
        [BindingKind.ModuleProperty] = new("x", "property x: int = 12", OuterScope.Module),
        [BindingKind.ModuleDef] = new("x", "def x() -> int:\n    return 12", OuterScope.Module, Twin: true),
        [BindingKind.ModuleClass] = new("x", "class x:\n    v: int = 12", OuterScope.Module, Twin: true),
        [BindingKind.ModuleStruct] = new("x", "struct x:\n    v: int = 12", OuterScope.Module),
        [BindingKind.ModuleEnum] = new("x", "enum x:\n    RED = 12", OuterScope.Module),
        [BindingKind.ModuleUnion] = new("x", "union x:\n    case A(v: int)", OuterScope.Module),
        [BindingKind.ModuleInterface] = new("x", "interface x:\n    def m(self) -> int: ...", OuterScope.Module),
        [BindingKind.ModuleTypeAlias] = new("x", "class Tgt:\n    v: int = 12\ntype x = Tgt", OuterScope.Module),
        [BindingKind.Builtin] = new("abs", "", OuterScope.Module, Twin: true),
        [BindingKind.ImportedName] = new("floor", "from math import floor", OuterScope.Module, Stdlib: true),
        [BindingKind.StaticField] = new("x", "@static\nx: int = 12", OuterScope.Member),
        [BindingKind.StaticProperty] = new("x", "@static\nproperty x: int = 12", OuterScope.Member),
        [BindingKind.StaticMethod] = new("x", "@static\ndef x() -> int:\n    return 12", OuterScope.Member),
        [BindingKind.ClassLiteralConst] = new("x", "const x: int = 12", OuterScope.Member),
        [BindingKind.ClassComputedConst] = new("x", "const x: int = twelve()", OuterScope.Member),
        [BindingKind.NestedType] = new("x", "class x:\n    v: int = 12", OuterScope.Member),
        [BindingKind.InstanceField] = new("x", "x: int = 12", OuterScope.Member),
        [BindingKind.InstanceProperty] = new("x", "property x: int = 12", OuterScope.Member),
        [BindingKind.InstanceMethod] = new("x", "def x(self) -> int:\n    return 12", OuterScope.Member),
        [BindingKind.EnclosingLocal] = new("x", "x: int = 12", OuterScope.Enclosing, Twin: true),
        [BindingKind.Parameter] = new("x", "", OuterScope.Enclosing),
        [BindingKind.LocalLet] = new("x", "let x = 12", OuterScope.Enclosing),
        [BindingKind.LocalLiteralConst] = new("x", "const x: int = 12", OuterScope.Enclosing),
        [BindingKind.LocalComputedConst] = new("x", "const x: int = twelve()", OuterScope.Enclosing),
        [BindingKind.LocalDef] = new("x", "def x() -> int:\n    return 12", OuterScope.Enclosing),
        [BindingKind.LambdaTypedLocal] = new("x", "x: () -> int = lambda: 12", OuterScope.Enclosing),
    };

    internal static readonly IReadOnlyDictionary<BindingKind, string> ExcludedCalleeOuters = new Dictionary<BindingKind, string>
    {
        [BindingKind.ModuleLet] = ModuleLetRefused,
        [BindingKind.Constructor] = "`__init__` is called through its type's name: the ModuleClass and NestedType cells",
        [BindingKind.Inner] = "binds only inside its own lambda or comprehension, where no statement binder sits; a comprehension target over it is the same target",
        [BindingKind.LocalVar] = "seen from the nested def that holds the binder it IS EnclosingLocal, which is generated; in the binder's own function it is S2's target reuse (P80)",
    };

    // ------------------------------------------------------------------------------------------- //
    // constant-kind (#2432): reader site × host × kind
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A host of a constant position: its label, the program around <c>{e}</c> typed <c>{T}</c> (with <c>{s}</c> where
    /// <c>main</c> shows the slot's value, read into the local <c>v</c>), the code its refusal carries, the 8951 pool's position
    /// where it has one, the imports the program itself needs, whether the program prints <c>1</c> and not the value, whether
    /// it takes an int only, whether its slot can be given a function type, and whether python has the same position.
    /// <see cref="Inside"/> is the same slot in the read member's OWN type <c>H</c>: the member's declaration stands at
    /// <c>{above}</c> or <c>{below}</c>, and <c>{after}</c> holds what follows the type. <see cref="InMethod"/>: that slot sits
    /// in a method's body, where <c>self</c> is bound and a class member is not visible bare (N1). A host with no such slot
    /// says why in <see cref="NoInside"/>. <see cref="Interface"/>: the member's own type is an interface there
    /// (<see cref="InterfaceStatics"/>).</summary>
    internal sealed record ConstantHost(string Label, string Program, string Refusal, ReadPosition? Position = null, string? Imports = null, bool PrintsOne = false,
        bool IntOnly = false, bool NoCallableSlot = false, bool Twin = false, string? Inside = null, bool InMethod = false, string? NoInside = null, bool Interface = false);

    /// <summary><c>main</c> for a host whose slot is read by <paramref name="read"/>, after <paramref name="before"/>.</summary>
    private static string ShowIn(string read, string before = "") => $"{Main}{before}    v: {{T}} = {read}\n    print({{s}})";

    private const string AttributeTarget = "    def f(self) -> int:\n        return 1\n" + Main + "    print(H().f())";

    /// <summary>The attribute hosts' program with the member's own slots: <c>{above}</c> and <c>{below}</c> around the attributed method.</summary>
    private const string AttributeTargetInside = "    def f(self) -> int:\n        return 1\n{below}{after}" + Main + "    print(H().f())";

    private const string AModuleConstIsOutsideEveryType = "a `const` at module level stands outside every type by definition: in the member's own type the same declaration is the class-const, struct-const or interface-const host";

    private const string DelegateHasNoBody = "a `delegate` declaration is a signature on one line (`delegate D(x: int) -> int`, `DelegateDef`): it has no body to hold a member, a `const` or an alias (SPY0100)";
    private const string EnumBodyHoldsNoConst = "an enum body holds `NAME = value` cases and nothing else: a `const` there is SPY0101";
    private const string UnionBodyHoldsNoConst = "a union body holds cases and methods and nothing else: a `const` there is SPY0104";
    private const string NotADeclarationScope = "the scope of an initializer EXPRESSION's own locals: a declaration statement is no expression";
    private const string HoldsTypeParameters = "pushed to hold type parameters for one resolution step: it declares nothing a program writes";

    /// <summary>Where a <c>const</c> is declared, total over <c>ScopeKind</c> with <see cref="ExcludedConstScopes"/>: the one
    /// classifier call that judges a <c>const</c> initializer (<c>ConstEligibility.Compute</c>) judges it at every scope a
    /// declaration can stand in. A type body is refined by <see cref="ConstTypeHosts"/>.</summary>
    internal static readonly IReadOnlyDictionary<ScopeKind, ConstantHost[]> ConstSites = new Dictionary<ScopeKind, ConstantHost[]>
    {
        [ScopeKind.Module] = new[] { new ConstantHost("module-const", "const K: {T} = {e}\n" + ShowIn("K"), "SPY0401", NoInside: AModuleConstIsOutsideEveryType) },
        [ScopeKind.TypeBody] = Array.Empty<ConstantHost>(),
        [ScopeKind.FunctionLike] = new[]
        {
            new ConstantHost("local-const", ShowIn("K", "    const K: {T} = {e}\n"), "SPY0401",
                Inside: "class H:\n{above}    def r(self) -> {T}:\n        const K: {T} = {e}\n        return K\n{below}{after}" + ShowIn("H().r()"), InMethod: true),
        },
        [ScopeKind.Block] = new[]
        {
            new ConstantHost("block-const", $"{Main}    if True:\n        const K: {{T}} = {{e}}\n        v: {{T}} = K\n        print({{s}})", "SPY0401",
                Inside: "class H:\n{above}    def r(self) -> None:\n        if True:\n            const K: {T} = {e}\n            v: {T} = K\n            print({s})\n{below}{after}" + Main + "    H().r()", InMethod: true),
        },
    };

    internal static readonly IReadOnlyDictionary<ScopeKind, string> ExcludedConstScopes = new Dictionary<ScopeKind, string>
    {
        [ScopeKind.InitializerHost] = NotADeclarationScope,
        [ScopeKind.Transparent] = HoldsTypeParameters,
    };

    /// <summary>The type bodies a <c>const</c> is declared in, total over <c>TypeKind</c> with <see cref="ExcludedConstTypeHosts"/>.</summary>
    internal static readonly IReadOnlyDictionary<TypeKind, ConstantHost[]> ConstTypeHosts = new Dictionary<TypeKind, ConstantHost[]>
    {
        [TypeKind.Class] = new[]
        {
            new ConstantHost("class-const", "class H:\n    const K: {T} = {e}\n" + ShowIn("H.K"), "SPY0401", ReadPosition.ConstInitializer,
                Inside: "class H:\n{above}    const K: {T} = {e}\n{below}{after}" + ShowIn("H.K")),
        },
        [TypeKind.Struct] = new[]
        {
            new ConstantHost("struct-const", "struct H:\n    const K: {T} = {e}\n    w: int = 1\n" + ShowIn("H.K"), "SPY0401",
                Inside: "struct H:\n{above}    const K: {T} = {e}\n    w: int = 1\n{below}{after}" + ShowIn("H.K")),
        },
        [TypeKind.Interface] = new[]
        {
            new ConstantHost("interface-const", "interface H:\n    const K: {T} = {e}\n    def m(self) -> int: ...\n" + ShowIn("H.K"), "SPY0401",
                Inside: "interface H:\n{above}    const K: {T} = {e}\n    def m(self) -> int: ...\n{below}{after}" + ShowIn("H.K"), Interface: true),
        },
    };

    internal static readonly IReadOnlyDictionary<TypeKind, string> ExcludedConstTypeHosts = new Dictionary<TypeKind, string>
    {
        [TypeKind.Enum] = EnumBodyHoldsNoConst,
        [TypeKind.Union] = UnionBodyHoldsNoConst,
        [TypeKind.Delegate] = DelegateHasNoBody,
    };

    internal static IEnumerable<ConstantHost> AllConstSites()
        => ConstSites.OrderBy(e => e.Key).SelectMany(e => e.Key == ScopeKind.TypeBody ? ConstTypeHosts.OrderBy(h => h.Key).SelectMany(h => h.Value) : e.Value);

    private const string OutsideOneFile = "the cross-module route: `ModuleLoader` judging a const another module of a project exports, with no SemanticInfo; this type compiles one file, and the route is plan-777361's hand control (A3)";

    /// <summary>
    /// The hosts of a constant position, keyed by the READER that judges them: a call site of
    /// <c>ConstantDefaultClassifier.IsAdmitted</c>, as <see cref="ScanClassifierReaders"/> finds it in the compiler's source
    /// (<c>File:Method</c>, <c>#n</c> for a method's nth site; a site whose table is its method's parameter is counted at each
    /// caller that passes one). Total over that census with <see cref="ExcludedConstantReaders"/>, so a reader the compiler gains
    /// has no host here and is red, and a reader whose host is deleted is red too (the owner's ruling 8 of 2026-11-20). A host's
    /// <c>AdmissionTable</c> is READ from its reader's call, never written here.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, ConstantHost[]> ConstantReaders = new Dictionary<string, ConstantHost[]>(StringComparer.Ordinal)
    {
        ["ConstantPositionValidator.cs:ValidateFunctionDefaults"] = new[]
        {
            new ConstantHost("def", "def f(p: {T} = {e}) -> {T}:\n    return p\n" + ShowIn("f()"), "SPY0401", ReadPosition.ParameterDefault, Twin: true,
                Inside: "class H:\n{above}    def f(self, p: {T} = {e}) -> {T}:\n        return p\n{below}{after}" + ShowIn("H().f()")),
        },
        ["ConstantPositionValidator.cs:ValidateLambdaDefaults"] = new[]
        {
            new ConstantHost("lambda", ShowIn("f()", "    f = lambda p: {T} = {e}: p\n"), "SPY0401", NoCallableSlot: true,
                Inside: "class H:\n{above}    def r(self) -> {T}:\n        f = lambda p: {T} = {e}: p\n        return f()\n{below}{after}" + ShowIn("H().r()"), InMethod: true),
        },
        // The two loops of one method: an attribute's positional arguments, then its named ones.
        ["ConstantPositionValidator.cs:ValidateDecoratorConstantArguments"] = new[]
        {
            new ConstantHost("attribute", "class H:\n    @[system.component_model.default_value({e})]\n" + AttributeTarget, "SPY0425", PrintsOne: true,
                Inside: "class H:\n{above}    @[system.component_model.default_value({e})]\n" + AttributeTargetInside),
        },
        ["ConstantPositionValidator.cs:ValidateDecoratorConstantArguments#2"] = new[]
        {
            new ConstantHost("attribute-keyword", "class Mark(Attribute):\n    w: object = 0\nclass H:\n    @[Mark(w={e})]\n" + AttributeTarget, "SPY0425",
                Imports: "from system import Attribute", PrintsOne: true,
                Inside: "class Mark(Attribute):\n    w: object = 0\nclass H:\n{above}    @[Mark(w={e})]\n" + AttributeTargetInside),
        },
        ["ConstantPositionValidator.cs:ValidateDataclassFieldDefaults"] = new[]
        {
            new ConstantHost("dataclass", "@dataclass\nclass H:\n    p: {T} = {e}\n" + ShowIn("H().p"), "SPY0401", ReadPosition.DataclassFieldInitializer, Twin: true,
                Inside: "@dataclass\nclass H:\n{above}    p: {T} = {e}\n{below}{after}" + ShowIn("H().p")),
        },
        ["ConstantPositionValidator.cs:ValidateStructFieldDefaults"] = new[]
        {
            new ConstantHost("struct", "struct H:\n    p: {T} = {e}\n" + ShowIn("H().p"), "SPY0401", ReadPosition.StructFieldInitializer, Twin: true,
                Inside: "struct H:\n{above}    p: {T} = {e}\n{below}{after}" + ShowIn("H().p")),
        },
        ["ConstEligibility.cs:Compute"] = AllConstSites().ToArray(),
        // A struct's defaulted auto-property (#1938): a constant default becomes the synthesized constructor's parameter default,
        // and any other default keeps its initializer, so every expression must run here (ruling 8).
        ["CodeGenInfoComputer.cs:MarkStructConstructorProperty"] = new[]
        {
            new ConstantHost("struct-auto-property", "struct H:\n    property p: {T} = {e}\n" + ShowIn("H().p"), "SPY0401",
                Inside: "struct H:\n{above}    property p: {T} = {e}\n{below}{after}" + ShowIn("H().p")),
        },
    };

    internal static readonly IReadOnlyDictionary<string, string> ExcludedConstantReaders = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ConstEligibility.cs:IsExportedConstCompileTime"] = OutsideOneFile,
    };

    internal static readonly IReadOnlyDictionary<AdmissionTable, string> ExcludedAdmissionTables = new Dictionary<AdmissionTable, string>();

    /// <summary>A constant position no classifier call judges, and the one such position this set generates: an enum member's
    /// value. A table value added for it has no reader here, which is red until this host moves under it. It is not the only
    /// position without a classifier call (/verify-plan pass 4, finding 2): a <c>delegate</c>'s parameter default, a
    /// signature-only interface method's default, a bracket attribute on anything but a class's method, and a union's attribute
    /// arguments have none either. Those hosts are #2779's group, out of plan-777361 by the owner's decision (#2423 comment
    /// 7050154806, item 22): no host is generated for them here, and the census of call sites cannot see them.</summary>
    internal static readonly ConstantHost EnumMemberHost = new("enum-member", $"enum H:\n    RED = {{e}}\n{Main}    print(H.RED.value)", "SPY0401", ReadPosition.EnumMemberValue, IntOnly: true,
        NoInside: EnumBodyHoldsCases);

    /// <summary>Every host with the table its reader passes; the enum member's value last, with none.</summary>
    internal static IEnumerable<(AdmissionTable? Table, ConstantHost Host)> AllConstantHosts()
    {
        var census = ClassifierReaders.Value.Sites;
        return ConstantReaders.Where(r => census.ContainsKey(r.Key))
            .SelectMany(r => r.Value.Select(h => (Table: (AdmissionTable?)census[r.Key], Host: h)))
            .OrderBy(h => h.Table).ThenBy(h => h.Host.Label, StringComparer.Ordinal)
            .Append((Table: null, Host: EnumMemberHost));
    }

    // The lambda host is the `lambda` spelling (`ConstantReaders`), and both reasons are true of that spelling alone (the review
    // of PR #2767, F10): the arrow spelling takes both slots, and the constant-kind set has no arrow-lambda host.
    private const string LambdaTakesNoCallableSlot = "the host's lambda is the `lambda` spelling, whose parameter annotation cannot be a function type and take a default: `lambda p: () -> int = five: p` is SPY0103. The arrow spelling takes one: `(p: () -> int = five) -> p` is SPY0401, the refusal the position owes, and `(p: (float) -> float = math.sqrt) -> p` is SPY0908 CS1736, the defect the `def` host's cell holds red for the same member (#2432, which holds the program as a cell). An arrow-lambda host is " + ByItem26;
    private const string LambdaTakesOneNameForAType = "the host's lambda is the `lambda` spelling, whose parameter annotation is one name: a type spelled through its module (`lambda p: system.DayOfWeek: p`) is SPY0103 with no default at all, so the slot cannot be declared there and the parse error is no constant position's verdict; the same type under a from-import is generated. The arrow spelling takes the dotted type and runs (`(p: system.DayOfWeek = system.DayOfWeek.Monday) -> p` prints `DayOfWeek.Monday`): an arrow-lambda host is " + ByItem26 + ", and no tracker holds that program, since it is green";
    private const string AnIntEnum = "the host is an int enum, whose member's value is an int: a `str` value declares a string enum, another host, and any other type is SPY0253";

    /// <summary>Why a host has no cell for a slot of <paramref name="type"/>, or null when it has one.</summary>
    internal static string? HostSkips(ConstantHost host, string type)
        => host.IntOnly && type != "int" ? AnIntEnum
            : host.NoCallableSlot && type.Contains("->", StringComparison.Ordinal) ? LambdaTakesNoCallableSlot
            : host.NoCallableSlot && type.Contains('.', StringComparison.Ordinal) ? LambdaTakesOneNameForAType
            : null;

    /// <summary>The expression: its label, text, type and what it prints; the import line and the declarations it needs; how
    /// <c>main</c> shows a value of its type (<c>{0}</c> the read); the codes its own kind's refusal carries beside the host's;
    /// the binding kind a bare name of it has; the member of a referenced module it names (<c>module.name</c>, what
    /// <see cref="StdlibModuleMembers"/> is keyed by) or the member of a referenced type; its read form where it has one; and
    /// the spelling it is read under (<see cref="ImportSpellings"/>, <see cref="TypeAliasSpelling"/>, <see cref="BuiltinNameSpelling"/>,
    /// <see cref="EnumSpellings"/>), which is a part of its cell's id.</summary>
    internal sealed record ConstantExpression(string Label, string Text, string Type, string Prints, string? Import = null, string? Prelude = null, string Show = "{0}",
        string[]? Refusals = null, BindingKind Target = BindingKind.None, string? ModuleValue = null, Type? Owner = null, string? Member = null, Harness.Form? Form = null,
        string? Spelling = null)
    {
        public bool Stdlib => ModuleValue != null;
    }

    /// <summary>
    /// How a program names what it imports: the image of the two enumerations the compiler keeps for it.
    /// <c>ImportBindingForm</c> is the statement that bound the name (<c>import m</c>, <c>import m as N</c>, a from-import, whose
    /// record also says whether it was a star: <c>ImportBinding.IsStar</c>), and <c>ImportKind</c> how the imported symbol is
    /// then read (through its module, by its own name, by an alias). <c>{m}</c> is the module, <c>{n}</c> the name; <see cref="Read"/>
    /// is the spelling of <c>{n}</c> a read uses. A spelling changes no verdict (N7), and a missing one has no cell
    /// (<see cref="AxisProblems"/>).
    /// </summary>
    internal sealed record ImportSpelling(string Label, ImportBindingForm Form, ImportKind Kind, bool Star, string Import, string Read);

    internal static readonly IReadOnlyList<ImportSpelling> ImportSpellings = new[]
    {
        new ImportSpelling("import", ImportBindingForm.ModuleRoot, ImportKind.ModuleImport, false, "import {m}", "{m}.{n}"),
        new ImportSpelling("import-as", ImportBindingForm.ModuleAlias, ImportKind.ModuleImport, false, "import {m} as m0", "m0.{n}"),
        new ImportSpelling("from-import", ImportBindingForm.FromImport, ImportKind.FromImport, false, "from {m} import {n}", "{n}"),
        new ImportSpelling("from-import-as", ImportBindingForm.FromImport, ImportKind.FromImportWithAlias, false, "from {m} import {n} as n0", "n0"),
        new ImportSpelling("star-import", ImportBindingForm.FromImport, ImportKind.FromImport, true, "from {m} import *", "{n}"),
    };

    internal static readonly IReadOnlyDictionary<ImportKind, string> ExcludedImportKinds = new Dictionary<ImportKind, string>
    {
        [ImportKind.None] = "not imported: the program's own names, which have their own spellings (OwnSpellings)",
    };

    /// <summary>A referenced type named through an alias the program declares (<c>type A = Math</c>): the 8951 pool's
    /// <c>Spelling.Alias</c>, for a type no program declares.</summary>
    internal const string TypeAliasSpelling = "type-alias";

    /// <summary>A referenced type named by the builtin name it has (<c>int</c> for <c>System.Int32</c>).</summary>
    internal const string BuiltinNameSpelling = "builtin-name";

    private static string Spell(string template, string module, string name)
        => template.Replace("{m}", module, StringComparison.Ordinal).Replace("{n}", name, StringComparison.Ordinal);

    /// <summary>A member of a referenced module, before a spelling: a value or a function, read uncalled.</summary>
    internal sealed record ModuleMember(string Module, string Name, string Type, string Prints, string Show = "{0}", Harness.Form? Form = null);

    /// <summary>The members of referenced modules a constant position is handed, one or two per kind (a hand choice: each is
    /// checked against what discovery records for it, <see cref="StdlibModuleMembers"/>); every one is read under every
    /// <see cref="ImportSpellings"/> entry.</summary>
    internal static readonly IReadOnlyDictionary<ScopeConstantKind, ModuleMember[]> ModuleMembers = new Dictionary<ScopeConstantKind, ModuleMember[]>
    {
        [ScopeConstantKind.ClrLiteralField] = new[] { new ModuleMember("subprocess", "PIPE", "int", "-1") },
        [ScopeConstantKind.StdlibModuleVariable] = new[] { new ModuleMember("socket", "AF_INET", "int", "2"), new ModuleMember("math", "pi", "float", "3.141592653589793") },
        [ScopeConstantKind.ClrStaticMember] = new[] { new ModuleMember("sys", "maxsize", "int", "2147483647") },
        [ScopeConstantKind.ModuleFunction] = new[] { new ModuleMember("math", "sqrt", "(float) -> float", "5.0", "{0}(25.0)", Harness.Form.Value) },
        [ScopeConstantKind.ModuleFunctionGroup] = new[] { new ModuleMember("math", "log", "(float) -> float", "0.0", "{0}(1.0)", Harness.Form.Value) },
    };

    /// <summary>A member of a referenced type, before a spelling: the module that exports the type, the type's name there, its
    /// builtin name where it has one, and the member. <c>{R}</c> in <see cref="Type"/> is the type as the spelling names it.</summary>
    internal sealed record TypeMemberBase(string Module, string TypeName, Type Owner, string Member, string Type, string Prints, string Show = "{0}",
        string[]? Refusals = null, Harness.Form? Form = null, string? BuiltinName = null);

    /// <summary>The members of referenced types a constant position is handed, one or two per kind (a hand choice: each is
    /// checked against what the checker's resolver answers for it); every one is read under every
    /// <see cref="ImportSpellings"/> entry of its type, through an alias of the type, and by the type's builtin name where it has one:
    /// a type of the compiler's <c>PrimitiveCatalog</c>, read by one of the names the catalog gives it, or
    /// <see cref="ExcludedBuiltinReceivers"/> says why the name is no receiver (<see cref="AxisProblems"/> holds the three together).</summary>
    internal static readonly IReadOnlyDictionary<ScopeConstantKind, TypeMemberBase[]> TypeMembers = new Dictionary<ScopeConstantKind, TypeMemberBase[]>
    {
        [ScopeConstantKind.ClrTypeLiteralField] = new[]
        {
            new TypeMemberBase("system", "Int32", typeof(int), "max_value", "int", "2147483647", BuiltinName: "int"),
            new TypeMemberBase("system", "Math", typeof(Math), "PI", "float", "3.141592653589793"),
        },
        [ScopeConstantKind.ClrTypeDecimalConstant] = new[] { new TypeMemberBase("system", "Decimal", typeof(decimal), "one", "decimal", "1", BuiltinName: "decimal") },
        [ScopeConstantKind.ClrTypeEnumMember] = new[] { new TypeMemberBase("system", "DayOfWeek", typeof(DayOfWeek), "Monday", "{R}", "DayOfWeek.Monday") },
        [ScopeConstantKind.ClrTypeStaticField] = new[]
        {
            new TypeMemberBase("system", "BitConverter", typeof(BitConverter), "is_little_endian", "bool", "True"),
            new TypeMemberBase("system", "String", typeof(string), "empty", "str", "True", "{0} == \"\"", BuiltinName: "str"),
        },
        [ScopeConstantKind.ClrTypeStaticProperty] = new[] { new TypeMemberBase("system", "Array", typeof(Array), "max_length", "int", "2147483591") },
        [ScopeConstantKind.ClrTypeMethod] = new[] { new TypeMemberBase("system", "Math", typeof(Math), "sqrt", "(float) -> float", "5.0", "{0}(25.0)", Form: Harness.Form.Value) },
        [ScopeConstantKind.ClrTypeDeclinedMethod] = new[] { new TypeMemberBase("system", "Char", typeof(char), "is_ascii_digit", "object", "True", "{0} is not None", Form: Harness.Form.Value) },
        // The slot's function type selects the overload (the owner's ruling 29, #2423 comment 7052893050), so the read is a
        // function value like `Math.sqrt`. SPY0336 is what every read of the group answers today (#2277), and it stays the
        // answer where no function type stands at the slot: an attribute argument.
        [ScopeConstantKind.ClrTypeMethodGroup] = new[]
        {
            new TypeMemberBase("system", "Math", typeof(Math), "abs", "(int) -> int", "5", "{0}(-5)", new[] { "SPY0336" }, Harness.Form.Value),
        },
    };

    private const string CharNamesNoReceiver = "`char` names a type in an annotation and nothing in an expression: `char.is_ascii_digit` is SPY0200, so a member of `Char` is read through the type's own name alone";

    /// <summary>The primitive types whose builtin name is no receiver of a member read, each with its probe.</summary>
    internal static readonly IReadOnlyDictionary<Type, string> ExcludedBuiltinReceivers = new Dictionary<Type, string>
    {
        [typeof(char)] = CharNamesNoReceiver,
    };

    /// <summary>The enum reads, total over <c>EnumMemberAccessKind</c> (which half of which enum shape a <c>.name</c> / <c>.value</c>
    /// read takes) and the member itself per shape, which has no access kind.</summary>
    internal static readonly IReadOnlyDictionary<ScopeConstantKind, (bool IntShape, EnumMemberAccessKind? Access)> EnumReadKinds = new Dictionary<ScopeConstantKind, (bool, EnumMemberAccessKind?)>
    {
        [ScopeConstantKind.IntEnumMember] = (true, null),
        [ScopeConstantKind.IntEnumValue] = (true, EnumMemberAccessKind.IntValue),
        [ScopeConstantKind.IntEnumName] = (true, EnumMemberAccessKind.IntName),
        [ScopeConstantKind.StringEnumMember] = (false, null),
        [ScopeConstantKind.StringEnumValue] = (false, EnumMemberAccessKind.StringValue),
        [ScopeConstantKind.StringEnumName] = (false, EnumMemberAccessKind.StringName),
    };

    private const string IntEnum = "enum E0:\n    RED = 5";
    private const string StringEnum = "enum E0:\n    RED = \"r\"";

    /// <summary>The reads of the program's own enum, before a spelling: <c>{E}</c> is the enum as the spelling names it.</summary>
    private static readonly IReadOnlyDictionary<ScopeConstantKind, ConstantExpression> EnumReads = new Dictionary<ScopeConstantKind, ConstantExpression>
    {
        [ScopeConstantKind.IntEnumMember] = new("member", "{E}.RED", "E0", "5", Prelude: IntEnum, Show: "{0}.value", Target: BindingKind.ModuleEnum),
        [ScopeConstantKind.IntEnumValue] = new("value", "{E}.RED.value", "int", "5", Prelude: IntEnum, Target: BindingKind.ModuleEnum, Form: Harness.Form.EnumValue),
        [ScopeConstantKind.IntEnumName] = new("name", "{E}.RED.name", "str", "RED", Prelude: IntEnum, Target: BindingKind.ModuleEnum),
        [ScopeConstantKind.StringEnumMember] = new("member", "{E}.RED", "E0", "r", Prelude: StringEnum, Show: "{0}.value", Target: BindingKind.ModuleEnum),
        [ScopeConstantKind.StringEnumValue] = new("value", "{E}.RED.value", "str", "r", Prelude: StringEnum, Target: BindingKind.ModuleEnum, Form: Harness.Form.EnumValue),
        [ScopeConstantKind.StringEnumName] = new("name", "{E}.RED.name", "str", "RED", Prelude: StringEnum, Target: BindingKind.ModuleEnum),
    };

    /// <summary>How the program's own enum is named, total over the 8951 pool's <c>Spelling</c> with
    /// <see cref="ExcludedEnumSpellings"/>: its label in a cell's id, the receiver, and the declaration the spelling needs.</summary>
    internal static readonly IReadOnlyDictionary<Harness.Spelling, (string Label, string Receiver, string? Declares)> EnumSpellings = new Dictionary<Harness.Spelling, (string, string, string?)>
    {
        [Harness.Spelling.Qualified] = ("type-name", "E0", null),
        [Harness.Spelling.Alias] = (TypeAliasSpelling, "A", "type A = E0"),
    };

    private const string ASiblingCaseIsNoName = "bare, a case is in scope nowhere: outside its enum it is read through the enum, and inside (`BLUE = RED`) it is SPY0200 today, which is #2069's cell and outside plan-777361";

    internal static readonly IReadOnlyDictionary<Harness.Spelling, string> ExcludedEnumSpellings = new Dictionary<Harness.Spelling, string>
    {
        [Harness.Spelling.Bare] = ASiblingCaseIsNoName,
        [Harness.Spelling.Call] = ACallByShape,
        [Harness.Spelling.SelfDot] = EnumBodyHoldsCases,
    };

    private static string AliasedIntEnumRead(EnumMemberAccessKind access)
        => $"through an alias of its enum (`type A = E0`), an int enum member's {access} read is SPY0908 CS1061 in a function's body, with no constant position in sight (the member is taken for an instance's field, SPY0459): a cell would hold that defect and no constant position's verdict. The member itself (`A.RED`) and a string enum's reads run there and are generated";

    /// <summary>Why a read of the program's enum has no cell under a spelling the enum has, or null when it has one.</summary>
    internal static string? EnumReadSkips(Harness.Spelling spelling, ScopeConstantKind kind)
        => spelling == Harness.Spelling.Alias && EnumReadKinds[kind] is (true, { } access) ? AliasedIntEnumRead(access) : null;

    /// <summary>
    /// What a constant position is handed, per kind: every member of <see cref="ModuleMembers"/> and <see cref="TypeMembers"/>
    /// under every spelling it has, every enum read under every spelling of the enum, and the two chains. GENERATED from those
    /// lists, so a spelling is on every member or on none.
    /// </summary>
    internal static readonly IReadOnlyDictionary<ScopeConstantKind, ConstantExpression[]> ConstantExpressions = BuildConstantExpressions();

    private static IReadOnlyDictionary<ScopeConstantKind, ConstantExpression[]> BuildConstantExpressions()
    {
        var all = new Dictionary<ScopeConstantKind, ConstantExpression[]>();
        foreach (var (kind, members) in ModuleMembers)
        {
            all[kind] = members.SelectMany(m => ImportSpellings.Select(s => new ConstantExpression($"{m.Module}.{m.Name}", Spell(s.Read, m.Module, m.Name), m.Type, m.Prints,
                Import: Spell(s.Import, m.Module, m.Name), Show: m.Show, Target: s.Form == ImportBindingForm.FromImport ? BindingKind.ImportedName : BindingKind.None,
                ModuleValue: $"{m.Module}.{m.Name}", Form: m.Form, Spelling: s.Label))).ToArray();
        }
        foreach (var (kind, members) in TypeMembers)
        {
            var expressions = new List<ConstantExpression>();
            foreach (var m in members)
            {
                ConstantExpression Named(string spelling, string receiver, string? import, string? prelude = null)
                    => new($"{m.TypeName}.{m.Member}", $"{receiver}.{m.Member}", m.Type.Replace("{R}", receiver, StringComparison.Ordinal), m.Prints, Import: import, Prelude: prelude,
                        Show: m.Show, Refusals: m.Refusals, Owner: m.Owner, Member: m.Member, Form: m.Form, Spelling: spelling);
                expressions.AddRange(ImportSpellings.Select(s => Named(s.Label, Spell(s.Read, m.Module, m.TypeName), Spell(s.Import, m.Module, m.TypeName))));
                expressions.Add(Named(TypeAliasSpelling, "A", $"from {m.Module} import {m.TypeName}", $"type A = {m.TypeName}"));
                if (m.BuiltinName != null)
                    expressions.Add(Named(BuiltinNameSpelling, m.BuiltinName, null));
            }
            all[kind] = expressions.ToArray();
        }
        foreach (var (kind, read) in EnumReads)
        {
            all[kind] = EnumSpellings.OrderBy(s => s.Key).Where(s => EnumReadSkips(s.Key, kind) == null).Select(s => read with
            {
                Text = read.Text.Replace("{E}", s.Value.Receiver, StringComparison.Ordinal),
                Prelude = s.Value.Declares == null ? read.Prelude : read.Prelude + "\n" + s.Value.Declares,
                Spelling = s.Value.Label,
            }).ToArray();
        }
        all[ScopeConstantKind.ComparisonChain] = new[] { new ConstantExpression("ordered", "1 < 2 < 3", "bool", "True") };
        all[ScopeConstantKind.ChainWithNonConstantLink] = new[] { new ConstantExpression("membership", "0 < 2 in [1, 2]", "bool", "True") };
        return all;
    }

    private const string LiteralValueShape = nameof(ModuleOverloads.Fields) + "/literal";
    private const string GuardedValueShape = nameof(ModuleOverloads.Fields) + "/guarded";
    private const string StaticValueShape = nameof(ModuleOverloads.Fields) + "/static";
    private const string GuardedLiteralShape = nameof(ModuleOverloads.Fields) + "/literal+guarded";
    private const string OneFunctionShape = nameof(ModuleOverloads.Functions) + "/one";
    private const string FunctionGroupShape = nameof(ModuleOverloads.Functions) + "/group";
    private const string ModuleTypeShape = nameof(ModuleOverloads.Types);

    /// <summary>
    /// What discovery records for a member of a referenced module, and the kind that is its image (the owner's ruling 13 of
    /// 2026-11-20, #2423 comment 7046304965): the collection of <c>ModuleOverloads</c> that holds the member, refined by the
    /// facts a constant position's answer can read: a field's <c>FieldSignature.IsConst</c> × <c>IsGuarded</c>, and whether a
    /// function's name has one overload or several. Total over the shapes a census of the Stdlib finds
    /// (<see cref="StdlibModuleMembers"/>) with <see cref="ExcludedModuleMemberShapes"/>; the collections themselves are total
    /// over the properties of <c>ModuleOverloads</c> with <see cref="ExcludedModuleCollections"/>.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, ScopeConstantKind> ModuleMemberShapes = new Dictionary<string, ScopeConstantKind>(StringComparer.Ordinal)
    {
        [LiteralValueShape] = ScopeConstantKind.ClrLiteralField,
        [GuardedValueShape] = ScopeConstantKind.StdlibModuleVariable,
        [StaticValueShape] = ScopeConstantKind.ClrStaticMember,
        [OneFunctionShape] = ScopeConstantKind.ModuleFunction,
        [FunctionGroupShape] = ScopeConstantKind.ModuleFunctionGroup,
    };

    private const string NamesAModuleType = "names a type the module exports: handed to a slot of another type it is the checker's own mismatch (SPY0220), and a member read through it is a referenced type's member, which has its own kinds (ClrTypeMemberShapes)";

    /// <summary>The shapes with no cell. <see cref="UnproducedModuleMemberShapes"/> are those the census must hold at 0.</summary>
    internal static readonly IReadOnlyDictionary<string, string> ExcludedModuleMemberShapes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [GuardedLiteralShape] = "a literal has no storage to guard",
        [ModuleTypeShape] = NamesAModuleType,
    };

    internal static readonly IReadOnlySet<string> UnproducedModuleMemberShapes = new HashSet<string>(StringComparer.Ordinal) { GuardedLiteralShape };

    private const string DescribesTheModule = "describes the module itself (its name, its documentation, where its C# lives, how it loads): it holds no member a program reads";

    /// <summary>The properties of <c>ModuleOverloads</c> that hold no member kind, total with the three collections the shapes are keyed by.</summary>
    internal static readonly IReadOnlyDictionary<string, string> ExcludedModuleCollections = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(ModuleOverloads.ModuleName)] = DescribesTheModule,
        [nameof(ModuleOverloads.Documentation)] = DescribesTheModule,
        [nameof(ModuleOverloads.CSharpNamespace)] = DescribesTheModule,
        [nameof(ModuleOverloads.CSharpClassName)] = DescribesTheModule,
        [nameof(ModuleOverloads.LoadEntry)] = DescribesTheModule,
    };

    /// <summary>Every member of every module of the Stdlib, as discovery records it: <c>module.name</c> and its shape.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string>> StdlibModuleMembers = new(() =>
    {
        var index = new OverloadIndexBuilder().BuildFromAssembly(SharpyStdlibReference.Assembly);
        var members = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (module, overloads) in index.Modules)
        {
            foreach (var (name, field) in overloads.Fields)
                members[$"{module}.{name}"] = field.IsConst ? field.IsGuarded ? GuardedLiteralShape : LiteralValueShape : field.IsGuarded ? GuardedValueShape : StaticValueShape;
            foreach (var (name, signatures) in overloads.Functions)
                members.TryAdd($"{module}.{name}", signatures.Count == 1 ? OneFunctionShape : FunctionGroupShape);
            foreach (var type in overloads.Types)
                members.TryAdd($"{module}.{type.Name}", ModuleTypeShape);
        }
        return members;
    });

    private const string LiteralFieldShape = "Field/literal";
    private const string EnumFieldShape = "Field/enum";
    private const string DecimalConstantShape = "Field/decimal-constant";
    private const string StaticFieldShape = "Field/static";

    /// <summary>
    /// What the checker's resolver of a referenced type's member (<c>ClrMemberTypeResolver</c>) answers for a member read through
    /// the type, and the kind that is its image: the subclasses of <c>ClrMemberResolution</c>, by name, with a field refined by
    /// the facts reflection has and discovery does not: what C# reads as a constant from metadata (<c>FieldInfo.IsLiteral</c>,
    /// or a <c>DecimalConstantAttribute</c> on a <c>static readonly</c> field, which is how a <c>const decimal</c> is compiled:
    /// <see cref="IsDecimalConstant"/>; the owner's ruling 17), and a literal of an enum type is the enum's member. Total over
    /// those subclasses with <see cref="ExcludedClrTypeMemberShapes"/>.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, ScopeConstantKind> ClrTypeMemberShapes = new Dictionary<string, ScopeConstantKind>(StringComparer.Ordinal)
    {
        [LiteralFieldShape] = ScopeConstantKind.ClrTypeLiteralField,
        [DecimalConstantShape] = ScopeConstantKind.ClrTypeDecimalConstant,
        [EnumFieldShape] = ScopeConstantKind.ClrTypeEnumMember,
        [StaticFieldShape] = ScopeConstantKind.ClrTypeStaticField,
        [nameof(ClrMemberResolution.Property)] = ScopeConstantKind.ClrTypeStaticProperty,
        [nameof(ClrMemberResolution.Method)] = ScopeConstantKind.ClrTypeMethod,
        [nameof(ClrMemberResolution.MethodGroup)] = ScopeConstantKind.ClrTypeMethodGroup,
        [DeclinedMethodShape] = ScopeConstantKind.ClrTypeDeclinedMethod,
    };

    // The two answers that say nothing of the member, refined by what reflection finds under the name: the resolver's own
    // reasons for giving them (`ClrMemberTypeResolver.ResolveCore`).
    private const string DeclinedMethodShape = nameof(ClrMemberResolution.InconclusiveResult) + "/static-method";
    private const string DeclinedValueShape = nameof(ClrMemberResolution.InconclusiveResult) + "/static-value";
    private const string OppositeHalfShape = nameof(ClrMemberResolution.InconclusiveResult) + "/instance-half";
    private const string GenericMethodShape = nameof(ClrMemberResolution.AbsentResult) + "/generic-method";
    private const string NoMemberShape = nameof(ClrMemberResolution.AbsentResult) + "/none";

    /// <summary>The refinements of the two answers, which <see cref="AxisProblems"/> partitions the resolver's subclasses with.</summary>
    internal static readonly IReadOnlyDictionary<string, string[]> RefinedClrTypeMemberShapes = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [nameof(ClrMemberResolution.Field)] = new[] { LiteralFieldShape, DecimalConstantShape, EnumFieldShape, StaticFieldShape },
        [nameof(ClrMemberResolution.InconclusiveResult)] = new[] { DeclinedMethodShape, DeclinedValueShape, OppositeHalfShape },
        [nameof(ClrMemberResolution.AbsentResult)] = new[] { GenericMethodShape, NoMemberShape },
    };

    private const string UncheckedAtAnAttribute = "; at an attribute argument, which the checker does not visit today, it is one more cell of the class the instance-member and method-group cells there hold red, with the same cure";
    private const string NamesAType = "names a type: handed to a slot of another type it is the checker's own mismatch (SPY0220) at every position the checker checks" + UncheckedAtAnAttribute;
    private const string NoSuchMember = "no member of the name: the checker refuses the read itself (SPY0203) and binds nothing" + UncheckedAtAnAttribute;
    private const string AnInstanceMemberThroughItsType = "the name is on the type's INSTANCE half (`DateTime.year`): the resolver declines to answer, the checker types the read `object` and refuses it under no kind, and the read is SPY0908 CS0120 in a function's body as in a parameter default, a `const` and an attribute argument, so it is a missing kind refusal (#1975's class) and no constant position's verdict. A type that fails to load gets the same answer for every name, and no program names one";
    private const string NoDeclinedValue = "a static field or property whose TYPE has no Sharpy spelling: the census of the `System` namespace finds none (AxisProblems), so no program reads one; when it finds one the shape is owed its kind";
    private const string AGenericMethodThroughItsType = "a generic method, which the resolver does not look at (`Array.resize`): read uncalled its delegate type cannot be inferred, SPY0908 CS8917 in a function's body as in a `const` (#2277's class), so no constant position's verdict";

    internal static readonly IReadOnlyDictionary<string, string> ExcludedClrTypeMemberShapes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(ClrMemberResolution.NestedType)] = NamesAType,
        [NoMemberShape] = NoSuchMember,
        [GenericMethodShape] = AGenericMethodThroughItsType,
        [OppositeHalfShape] = AnInstanceMemberThroughItsType,
        [DeclinedValueShape] = NoDeclinedValue,
    };

    /// <summary>The member each probe of an excluded shape reads, which <see cref="AxisProblems"/> ties to the shape.</summary>
    internal static readonly IReadOnlyDictionary<string, (Type Owner, string Member)> ExcludedShapeMembers = new Dictionary<string, (Type, string)>(StringComparer.Ordinal)
    {
        [OppositeHalfShape] = (typeof(DateTime), "year"),
        [GenericMethodShape] = (typeof(Array), "resize"),
        [NoMemberShape] = (typeof(int), "nosuch"),
    };

    /// <summary>The shape of <paramref name="member"/> read through <paramref name="owner"/>, as the checker's resolver answers it.</summary>
    internal static string ClrTypeMemberShape(Type owner, string member)
    {
        var resolution = new ClrMemberTypeResolver(new ClrTypeBridge()).Resolve(owner, member, ClrReceiverKind.StaticType);
        const BindingFlags Statics = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        bool Named(MemberInfo m, ReverseNameContext context) => m.Name == member || NameMangler.ToSharpyName(m.Name, context) == member;
        if (resolution is ClrMemberResolution.InconclusiveResult)
        {
            return owner.GetMethods(Statics).Any(m => !m.IsSpecialName && Named(m, ReverseNameContext.Method)) ? DeclinedMethodShape
                : owner.GetProperties(Statics).Cast<MemberInfo>().Concat(owner.GetFields(Statics)).Any(m => Named(m, ReverseNameContext.Property)) ? DeclinedValueShape
                : OppositeHalfShape;
        }
        if (resolution is ClrMemberResolution.AbsentResult)
            return owner.GetMethods(Statics).Any(m => m.IsGenericMethodDefinition && Named(m, ReverseNameContext.Method)) ? GenericMethodShape : NoMemberShape;
        if (resolution is not ClrMemberResolution.Field field)
            return resolution.GetType().Name;
        var info = owner.GetField(field.ClrName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        return info is { IsLiteral: true } ? owner.IsEnum ? EnumFieldShape : LiteralFieldShape
            : info != null && IsDecimalConstant(info) ? DecimalConstantShape
            : StaticFieldShape;
    }

    /// <summary>Whether C# reads a field that is no metadata literal as a constant: a <c>const decimal</c> is compiled to a
    /// <c>static readonly</c> field carrying <c>DecimalConstantAttribute</c>, and the C# compiler reads it back as the constant.</summary>
    internal static bool IsDecimalConstant(FieldInfo field)
        => field is { IsStatic: true, IsInitOnly: true } && field.FieldType == typeof(decimal)
            && field.CustomAttributes.Any(a => a.AttributeType == typeof(System.Runtime.CompilerServices.DecimalConstantAttribute));

    /// <summary>The kinds that are no member: closed by call 45 (a chain is admitted when each link is).</summary>
    internal static readonly IReadOnlySet<ScopeConstantKind> ChainKinds = new HashSet<ScopeConstantKind> { ScopeConstantKind.ComparisonChain, ScopeConstantKind.ChainWithNonConstantLink };

    /// <summary>A read of a name the program declares: the kind, the spelling of its declaration's type, the spelling of the
    /// read, where the reader stands against the member's type (null: outside it; <c>Base</c>: outside, naming the member
    /// through a subclass; <c>Self</c>: in the member's own type), the member's order against a bare sibling reader and the
    /// outer binding of its name there, its read form where it is a callable, the declarations outside the host, the member's
    /// declaration inside it, what follows the host's type, the read, the slot's type, how <c>main</c> shows the value, what the
    /// cell prints if it runs, the codes the kind's own refusal carries beside the host's, and the codes of a refusal that is
    /// not the host's at all.</summary>
    internal sealed record OwnRead(BindingKind Kind, ScopeAnnotation? Annotation, Harness.Spelling Spelling, DeclaringRelation? Relation, Harness.Order? Order, BindingKind Outer,
        Harness.Form? Form, string? Prelude, string? Member, string? After, string Text, string Type, string Show, string Prints, string[]? Refusals, string[]? Only);

    private const string ACallByShape = "read as a call it is `EmittableConstantKind.Call`, which every table refuses by the expression's shape before a name is resolved (SPY0401 at a default), so no member's kind is asked; at an enum member's value, which no table judges, a call is the 8951 pool's kind:enum-member-constant cell";
    private const string ALocalInAConstantPosition = "a local: only a lambda's default and a local `const` can name one, as a bare name the identifier resolver answers (a local that is no const is SPY0401 at a lambda's default)";

    /// <summary>The kinds with no cell as a name of the program's own, total over <see cref="BindingKind"/> with the kinds
    /// <see cref="OwnReads"/> generates, <c>ModuleEnum</c> (the enum reads) and <c>ImportedName</c> (a referenced module's value
    /// named bare).</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, string> ExcludedOwnReads = new Dictionary<BindingKind, string>
    {
        [BindingKind.ModuleLet] = ModuleLetRefused,
        [BindingKind.ModuleClass] = NamesAType,
        [BindingKind.ModuleStruct] = NamesAType,
        [BindingKind.ModuleUnion] = NamesAType,
        [BindingKind.ModuleInterface] = NamesAType,
        [BindingKind.ModuleTypeAlias] = NamesAType,
        [BindingKind.NestedType] = NamesAType,
        [BindingKind.Constructor] = ACallByShape,
        [BindingKind.EnclosingLocal] = ALocalInAConstantPosition,
        [BindingKind.Parameter] = ALocalInAConstantPosition,
        [BindingKind.Inner] = ALocalInAConstantPosition,
        [BindingKind.LocalVar] = ALocalInAConstantPosition,
        [BindingKind.LocalLet] = ALocalInAConstantPosition,
        [BindingKind.LocalLiteralConst] = ALocalInAConstantPosition,
        [BindingKind.LocalComputedConst] = ALocalInAConstantPosition,
        [BindingKind.LocalDef] = ALocalInAConstantPosition,
        [BindingKind.LambdaTypedLocal] = ALocalInAConstantPosition,
        [BindingKind.None] = "no binding",
    };

    /// <summary>The read forms with no cell, total over the 8951 pool's <c>Form</c> with the two a cell carries (<c>Value</c>: a
    /// callable read uncalled; <c>EnumValue</c>: an enum member's <c>.value</c>).</summary>
    internal static readonly IReadOnlyDictionary<Harness.Form, string> ExcludedConstantForms = new Dictionary<Harness.Form, string>
    {
        [Harness.Form.Call] = ACallByShape,
        [Harness.Form.Ctor] = ACallByShape,
    };

    /// <summary>The spellings with no cell for a name of the program's own, total over the 8951 pool's <c>Spelling</c> with
    /// the spellings <see cref="OwnSpellings"/> derives.</summary>
    internal static readonly IReadOnlyDictionary<Harness.Spelling, string> ExcludedOwnSpellings = new Dictionary<Harness.Spelling, string>
    {
        [Harness.Spelling.Call] = ACallByShape,
    };

    private const string NotKeyedByIt = "the reader in a type that is not the member's: every host that is a type's member is one in its outside cells (the attributed class H reads G's member), under each spelling, and no cell is keyed by it";
    private const string ThePoolsRelationCells = "a constant position in a type NESTED in the member's type, or beside it in one enclosing type: bare, the read is refused by D4 (R-KG) whatever the position, and qualified it binds what the outside cell binds. The 8951 pool holds both at an initializer and a method body (its `enclosing` and `sibling` cells); no constant-position host is generated there, by decision (plan-777361's Generator row)";

    /// <summary>The placements of a reader against the member's type with no cell keyed by them, total over
    /// <c>DeclaringRelation</c> with <c>Self</c> (the member's own type) and <c>Base</c> (the member named through a subclass).</summary>
    internal static readonly IReadOnlyDictionary<DeclaringRelation, string> ExcludedOwnPlacements = new Dictionary<DeclaringRelation, string>
    {
        [DeclaringRelation.Other] = NotKeyedByIt,
        [DeclaringRelation.Enclosing] = ThePoolsRelationCells,
        [DeclaringRelation.Sibling] = ThePoolsRelationCells,
    };

    private const string NoSelfAtATypeBody = "`self` is bound in a method's body alone: at a type body's own level it names nothing (SPY0200), so a read through it has a host only where the slot sits in a method (a lambda's default, a local or block `const`)";

    /// <summary>
    /// Under which spellings, and from where against its type, a name of a kind is read in a constant position (the owner's
    /// ruling 12 of 2026-11-20, #2423 comment 7046304965): READ from the typed-read set's readers, which are themselves read
    /// from the 8951 pool (<see cref="TypedReaders"/>), with the reader's position dropped, since a constant position's hosts
    /// are this set's own. So the axis is the image of <c>Spelling</c> and of the reader's placement (in the member's own
    /// type, or outside it) under that pool's rosters, and a spelling the pool gains for a kind is owed here at every host
    /// (<see cref="AxisProblems"/>). An instance member's read through its type's name stays the kind refusal it is in the
    /// pool (<c>H.x</c>, SPY0290): a constructed instance is a call by shape.
    /// </summary>
    internal static IReadOnlyList<(Harness.Spelling Spelling, bool Inside)> OwnSpellings(BindingKind kind)
        => TypedReaders.Where(r => r.Kind == kind).Select(r => (r.Spelling, r.Inside)).Distinct().OrderBy(r => r.Spelling).ThenBy(r => r.Inside).ToList();

    private static string InterfaceStaticIsRefused(string what, string tracker = "#2383")
        => $"an interface's `@static` {what} is SPY0908 CS0120 read from a function's body, with no constant position in sight ({tracker}'s class): in the interface's own constant positions a cell would hold that defect and no constant position's verdict. An interface's `const` is generated there";

    /// <summary>The kinds with no cell where the member's own type is an interface (<see cref="ConstantHost.Interface"/>), each
    /// with its own probe.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, string> InterfaceStatics = new Dictionary<BindingKind, string>
    {
        [BindingKind.StaticField] = InterfaceStaticIsRefused("field"),
        [BindingKind.StaticProperty] = InterfaceStaticIsRefused("property"),
        [BindingKind.StaticMethod] = InterfaceStaticIsRefused("method, read uncalled", "#2111"),
    };

    /// <summary>Why a host has no cell for a kind in the member's own type, or null when it has one.</summary>
    internal static string? OwnKindSkips(ConstantHost host, BindingKind kind, bool inside)
        => inside && host.Interface ? InterfaceStatics.GetValueOrDefault(kind) : null;

    /// <summary>Why a host has no cell for a spelling and placement, or null when it has one.</summary>
    internal static string? OwnSpellingSkips(ConstantHost host, Harness.Spelling spelling, bool inside)
        => !inside ? null
            : host.Inside == null ? host.NoInside ?? "the host names no slot in the member's own type, and does not say why"
            : spelling == Harness.Spelling.SelfDot && !host.InMethod ? NoSelfAtATypeBody
            : null;

    /// <summary>The module binding a bare sibling read is set against, by the member's form: a constant where the member is a
    /// value, so a read that binds the wrong one is ADMITTED and prints the wrong value, and a function where it is a callable.</summary>
    internal static BindingKind SiblingOuter(bool callable) => callable ? BindingKind.ModuleDef : BindingKind.ModuleLiteralConst;

    internal const string BareSibling = "bare-sibling";
    internal const string InMethod = "in-method";

    /// <summary>
    /// The program's own names as a host's constant position is handed them: every kind of the typed-read set
    /// (<see cref="TypedReads"/>: each kind with an int-valued declaration of <c>x</c>), under each spelling of its declaration's
    /// type, under every spelling and placement <see cref="OwnSpellings"/> derives for the kind, a callable read UNCALLED
    /// (called, it is <see cref="ACallByShape"/>); the same member named through a subclass; and a builtin function, which is
    /// declared by no program. A bare read of a sibling in the member's own type is the class-body rule's (D3), so it is
    /// generated at both orders, with and without a module binding of the name: earlier, or with no such binding, it is the
    /// member; at-or-after with one it is N2's refusal (SPY0619, which lands with plan-777361's T8); in a method's body the
    /// member is not visible bare (N1) and the read is the module's binding, or SPY0200.
    /// </summary>
    internal static IEnumerable<OwnRead> OwnReadsOf(ConstantHost host)
    {
        foreach (var (kind, t) in TypedReads.OrderBy(e => e.Key))
        {
            bool callable = t.Member.EndsWith("()", StringComparison.Ordinal);
            string name = callable ? t.Member[..^2] : t.Member;
            string type = callable ? "() -> int" : "int";
            string show = callable ? "{0}()" : "{0}";
            Harness.Form? form = callable ? Harness.Form.Value : null;
            foreach (var annotation in Enum.GetValues<ScopeAnnotation>())
            {
                var declaration = annotation == ScopeAnnotation.Annotated ? t.Annotated : t.Inferred;
                if (declaration == null)
                    continue;
                var helpers = t.NeedsFive ? new[] { Five } : Array.Empty<string>();
                string Outside(params string[] lines) => string.Join("\n", helpers.Concat(lines));
                foreach (var (spelling, inside) in OwnSpellings(kind))
                {
                    if ((OwnSpellingSkips(host, spelling, inside) ?? OwnKindSkips(host, kind, inside)) != null)
                        continue;
                    // Through a type's name an instance member is the kind refusal; through `self` it is a value like any other.
                    var kindRefusal = t.Level == MemberLevel.Instance && spelling != Harness.Spelling.SelfDot ? new[] { "SPY0290" } : null;
                    if (!inside)
                    {
                        string receiver = spelling switch
                        {
                            Harness.Spelling.Bare when t.Level == MemberLevel.Module => "",
                            Harness.Spelling.Qualified when t.Level != MemberLevel.Module => "G.",
                            Harness.Spelling.Alias when t.Level != MemberLevel.Module => "A.",
                            _ => throw new ArgumentOutOfRangeException(nameof(host), $"{kind} read {spelling} from outside its type has no receiver here"),
                        };
                        string prelude = t.Level == MemberLevel.Module ? Outside(declaration)
                            : spelling == Harness.Spelling.Alias ? Outside("class G:\n" + Indent(declaration), "type A = G")
                            : Outside("class G:\n" + Indent(declaration));
                        yield return new OwnRead(kind, annotation, spelling, null, null, BindingKind.None, form, prelude, null, null, receiver + name, type, show, "5", kindRefusal, null);
                        continue;
                    }
                    string member = Indent(declaration) + "\n";
                    if (spelling != Harness.Spelling.Bare)
                    {
                        string receiver = spelling switch
                        {
                            Harness.Spelling.Qualified => "H.",
                            Harness.Spelling.Alias => "A.",
                            Harness.Spelling.SelfDot => "self.",
                            _ => throw new ArgumentOutOfRangeException(nameof(host), $"{kind} read {spelling} in its own type has no receiver here"),
                        };
                        yield return new OwnRead(kind, annotation, spelling, DeclaringRelation.Self, null, BindingKind.None, form, helpers.Length == 0 ? null : Outside(), member,
                            spelling == Harness.Spelling.Alias ? "type A = H\n" : null, receiver + name, type, show, "5", kindRefusal, null);
                        continue;
                    }
                    foreach (var order in new[] { Harness.Order.Earlier, Harness.Order.Later })
                    {
                        foreach (var outer in new[] { BindingKind.None, SiblingOuter(callable) })
                        {
                            string? prelude = outer == BindingKind.None ? (helpers.Length == 0 ? null : Outside())
                                : Outside(callable ? "def x() -> int:\n    return 12" : "const x: int = 12");
                            // In a method's body the member is not visible bare; at the type body's level it is, unless N2 refuses.
                            string[]? only = host.InMethod ? outer == BindingKind.None ? new[] { "SPY0200" } : null
                                : order == Harness.Order.Later && outer != BindingKind.None ? new[] { "SPY0619" } : null;
                            yield return new OwnRead(kind, annotation, spelling, DeclaringRelation.Self, order, outer, form, prelude, member, null, name, type, show,
                                host.InMethod && outer != BindingKind.None ? "12" : "5", null, only);
                        }
                    }
                }
                if (t.Level != MemberLevel.Module)
                {
                    yield return new OwnRead(kind, annotation, Harness.Spelling.Qualified, DeclaringRelation.Base, null, BindingKind.None, form,
                        Outside("class G:\n" + Indent(declaration), "class D(G):\n    pass"), null, null, "D." + name, type, show, "5",
                        t.Level == MemberLevel.Instance ? new[] { "SPY0290" } : null, null);
                }
            }
        }
        yield return new OwnRead(BindingKind.Builtin, null, Harness.Spelling.Bare, null, null, BindingKind.None, Harness.Form.Value, null, null, null, "abs", "(int) -> int", "{0}(-5)", "5", null, null);
    }

    // ------------------------------------------------------------------------------------------- //
    // dataclass-init: reader × fit × order × site × reader slot, and the projected axes
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A reader of a synthesized <c>__init__</c>, written over an <see cref="InitShape"/>. An EXPRESSION reader
    /// (<paramref name="Shows"/> null) is an int-valued expression placed at a slot; any other is a declaration at module level,
    /// and <c>main</c> prints <paramref name="Shows"/>. A helper is declared last, below the dataclass.
    /// <paramref name="Refusal"/> is the code the control of a read that does not fit carries, where every shape's control
    /// carries the same one. <paramref name="WhenCalled"/>: a reader that is a declaration and reads the type's name in a
    /// function's body, so python reads it when <c>main</c> calls it and not as the module loads (an expression reader's slot
    /// says so itself: <see cref="CalledSlots"/>). <paramref name="Python"/>: python has the read, under a construct the twin
    /// renderer has a row for.</summary>
    internal sealed record InitRead(string Fits, string? Misfits = null, string? FitsHelper = null, string? MisfitsHelper = null, string? Shows = null, string? Refusal = null,
        bool WhenCalled = false, bool Python = true);

    /// <summary>What a reader's program is written over: the dataclass's declaration; the name a reader spells (<c>{N}</c>) and
    /// the one a type position spells (<c>{T}</c>); what a read that fits hands (<c>{A}</c>) and the types of what the
    /// <c>__init__</c> takes (<c>{P}</c>); what a read that does not fit hands (<c>{X}</c>) and offers to take (<c>{M}</c>); the
    /// default a deriving dataclass's own field needs after a defaulted one (<c>{W}</c>); what a cell that runs prints; the code
    /// the control of a constructor reference that does not fit carries (null where no such cell is generated); a declaration
    /// that stands above the reader at both orders, so that the one forward read is the dataclass's; whether python has the
    /// shape, with the same meaning; what the type derives from, which stands FIRST at both orders, above the type and its
    /// reader; and the name a base list spells (<c>{B}</c>), where it is not the reader's.</summary>
    internal sealed record InitShape(string Declaration, string Name, string? ReferenceRefusal, string? Type = null, string Args = "5", string Params = "int", string MisfitArgs = "",
        string MisfitParams = "", string DerivedDefault = "", string Prints = "5", string? Above = null, bool Twin = false, string? First = null, string? Base = null)
    {
        public string Fill(string text) => text
            .Replace("{B}", Base ?? Name, StringComparison.Ordinal)
            .Replace("{N}", Name, StringComparison.Ordinal).Replace("{T}", Type ?? Name, StringComparison.Ordinal)
            .Replace("{A}", Args, StringComparison.Ordinal).Replace("{P}", Params, StringComparison.Ordinal)
            .Replace("{X}", MisfitArgs, StringComparison.Ordinal).Replace("{M}", MisfitParams, StringComparison.Ordinal)
            .Replace("{W}", DerivedDefault, StringComparison.Ordinal);
    }

    /// <summary>The readers, total over <see cref="ScopeInitReader"/> with <see cref="ExcludedInitReaders"/>. Over the matrix's
    /// shape the dataclass has one field, <c>v: int</c>, and every read that fits hands it 5.</summary>
    internal static readonly IReadOnlyDictionary<ScopeInitReader, InitRead> InitReaders = new Dictionary<ScopeInitReader, InitRead>
    {
        [ScopeInitReader.Construction] = new("sh({N}({A}))", FitsHelper: "def sh(d: {T}) -> int:\n    return d.v"),
        [ScopeInitReader.ConstructorReference] = new("mk({N})", "mk({N})", "def mk(f: ({P}) -> {T}) -> int:\n    return f({A}).v", "def mk(f: ({M}) -> {T}) -> int:\n    return 1"),
        [ScopeInitReader.SuperInit] = new("class D({N}):\n    def __init__(self):\n        super().__init__({A})", "class D({N}):\n    def __init__(self):\n        super().__init__({X})",
            Shows: "D().v", Refusal: "SPY0224"),
        [ScopeInitReader.Forwarding] = new("class D({N}):\n    pass", Shows: "D({A}).v"),
        [ScopeInitReader.InheritedFields] = new("@dataclass\nclass E({N}):\n    w: int{W}", Shows: "E({A}, 6).v"),
    };

    private const string NoSelfInitOnADataclass = "a dataclass that declares an `__init__` synthesizes none (it runs its own), and one that declares none has no `self.__init__` to chain to: from a method the call is SPY0350, as it is in any class";

    internal static readonly IReadOnlyDictionary<ScopeInitReader, string> ExcludedInitReaders = new Dictionary<ScopeInitReader, string>
    {
        [ScopeInitReader.SelfInit] = NoSelfInitOnADataclass,
    };

    private const string AConstructionIsCheckedAtNeitherOrder = "#2021's class: a construction handed arguments the synthesized `__init__` does not take is SPY0908 CS7036 with the dataclass above the call as below it, of the dataclass and of a class deriving from it; of a dataclass deriving from it, it is SPY0908 CS7036 with the dataclass above (below, that deriving dataclass's declaration alone is the generated cell's red). The checker binds the call at neither order, so the cell has no refusing control and its red is no order's";

    /// <summary>The reader × fit pairs with no cell, total over the two enums with the pairs a cell carries. They hold over
    /// every shape: no projection or placement generates one.</summary>
    internal static readonly IReadOnlyDictionary<(ScopeInitReader Reader, ScopeInitFit Fit), string> ExcludedInitFits = new Dictionary<(ScopeInitReader, ScopeInitFit), string>
    {
        [(ScopeInitReader.Construction, ScopeInitFit.Misfits)] = AConstructionIsCheckedAtNeitherOrder,
        [(ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = AConstructionIsCheckedAtNeitherOrder,
        [(ScopeInitReader.InheritedFields, ScopeInitFit.Misfits)] = AConstructionIsCheckedAtNeitherOrder,
    };

    /// <summary>A site a dataclass is declared at: its declaration, the name a reader outside it spells, and the code the
    /// control of a constructor reference that does not fit carries there (measured @ 28f10cd51: a nested type's qualified name
    /// handed to a function-typed slot it does not fit is the slot's own mismatch). A PROJECTED site is no part of the matrix:
    /// it is one more value of the axis, generated as <see cref="InitProjections"/> are.</summary>
    internal sealed record DataclassSite(string Label, string Declaration, string Name, string? ReferenceRefusal, bool Projected = false)
    {
        public InitShape Shape => new(Declaration, Name, ReferenceRefusal, Twin: true);
    }

    private const string ModuleDataclass = "@dataclass\nclass DC:\n    v: int";

    /// <summary>The sites, total over <c>ScopeKind</c> with <see cref="ExcludedDataclassScopes"/>: the checker keeps the module's
    /// dataclasses in a set of their own (<c>_moduleDataclassSymbols</c>), which a nested one is not in, so the two sites are two
    /// routes to the same read. A type body is a class's: one enclosing type, by decision. Projected (ruling 35): a module's
    /// dataclass with a type parameter, read by its constructed name, and a dataclass nested two deep.</summary>
    internal static readonly IReadOnlyDictionary<ScopeKind, DataclassSite[]> DataclassSites = new Dictionary<ScopeKind, DataclassSite[]>
    {
        [ScopeKind.Module] = new[]
        {
            new DataclassSite("module", ModuleDataclass, "DC", "SPY0342"),
            new DataclassSite("generic", "@dataclass\nclass DC[T]:\n    v: T", "DC[int]", null, Projected: true),
        },
        [ScopeKind.TypeBody] = new[]
        {
            new DataclassSite("nested", "class O:\n    @dataclass\n    class DC:\n        v: int", "O.DC", "SPY0220"),
            new DataclassSite("deep", "class O:\n    class P:\n        @dataclass\n        class DC:\n            v: int", "O.P.DC", null, Projected: true),
        },
    };

    private const string AClassInAFunctionDeclaresNoName = "a class cannot be declared in a function's body or a block, dataclass or not: SPY0708";

    internal static readonly IReadOnlyDictionary<ScopeKind, string> ExcludedDataclassScopes = new Dictionary<ScopeKind, string>
    {
        [ScopeKind.FunctionLike] = AClassInAFunctionDeclaresNoName,
        [ScopeKind.Block] = AClassInAFunctionDeclaresNoName,
        [ScopeKind.InitializerHost] = NotADeclarationScope,
        [ScopeKind.Transparent] = HoldsTypeParameters,
    };

    internal static IEnumerable<DataclassSite> AllDataclassSites() => DataclassSites.OrderBy(e => e.Key).SelectMany(e => e.Value);

    private const string OnlyAClassIsADataclass = "`@dataclass` is a class's decorator: on a struct, an interface and an enum it is SPY0380";
    private const string AUnionTakesADecoratorUnchecked = "#2797's class: a decorator on a union is accepted and never checked. `@dataclass union` runs, and so does `@nonsense union`, where an unknown decorator on a class is SPY0444: nothing says the decorator was read, so no cell has a dataclass to read there";
    private const string ADelegateTakesNoDecorator = "a delegate takes no decorator at all: SPY0105";

    /// <summary>The kinds of type <c>@dataclass</c> synthesizes an <c>__init__</c> for, total over <c>TypeKind</c> with
    /// <see cref="ExcludedDataclassHosts"/>.</summary>
    internal static readonly IReadOnlySet<TypeKind> DataclassHosts = new HashSet<TypeKind> { TypeKind.Class };

    internal static readonly IReadOnlyDictionary<TypeKind, string> ExcludedDataclassHosts = new Dictionary<TypeKind, string>
    {
        [TypeKind.Struct] = OnlyAClassIsADataclass,
        [TypeKind.Interface] = OnlyAClassIsADataclass,
        [TypeKind.Enum] = OnlyAClassIsADataclass,
        [TypeKind.Union] = AUnionTakesADecoratorUnchecked,
        [TypeKind.Delegate] = ADelegateTakesNoDecorator,
    };

    /// <summary>Why an option of the decorator has no cell: the synthesis of <c>__init__</c> does not read it
    /// (<c>DataclassSynthesis.SynthesizeMembers</c>), so the parameters are the fields' whatever its value, and its probes say
    /// so by running the generated cell's program under it, set and unset, at both orders.</summary>
    private static string AnOptionLeavesTheParameters(string option)
        => $"`@dataclass({option}=…)` is an option the synthesis of `__init__` does not read: the parameters are the fields' whatever its value. A reference that fits runs with the dataclass above its reader and is SPY0342 with it below, with the option set and unset, as the generated cell is (#2423, which holds that cell)";

    private static string OptionSpelling(string property) => property.ToLowerInvariant();

    /// <summary>The decorator's options, total over the properties of <c>DataclassOptions</c>: none is generated, and one that
    /// changes what the synthesized <c>__init__</c> takes is owed its cells. <c>eq</c> and <c>repr</c> decide whether the other
    /// members are generated at all; the <c>dataclass-member</c> set reads those members under the default, where each is.</summary>
    internal static readonly IReadOnlyDictionary<string, string> ExcludedDataclassOptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(DataclassOptions.Frozen)] = AnOptionLeavesTheParameters(OptionSpelling(nameof(DataclassOptions.Frozen))),
        [nameof(DataclassOptions.Eq)] = AnOptionLeavesTheParameters(OptionSpelling(nameof(DataclassOptions.Eq))),
        [nameof(DataclassOptions.Repr)] = AnOptionLeavesTheParameters(OptionSpelling(nameof(DataclassOptions.Repr))),
    };

    private static BehaviourProbe OptionProbe(string option)
    {
        string Program(string value, bool above)
        {
            string dataclass = $"@dataclass({option}={value})\nclass DC:\n    v: int\n";
            const string Reader = "def r() -> int:\n    return mk(DC)\n";
            return $"{(above ? dataclass + Reader : Reader + dataclass)}def mk(f: (int) -> DC) -> int:\n    return f(5).v\ndef main() -> None:\n    print(r())\n";
        }
        BehaviourProbe Below(string value) => new(Program(value, above: false), "SPY0342", r => Refused(r, "SPY0342"));
        return Prints(Program("True", above: true), "5", Also: new[] { Below("True"), Prints(Program("False", above: true), "5"), Below("False") });
    }

    // --- the projected axes: one value varied alone from the matrix's first --- //

    private const string ABaseInitWithParametersIsRefused = "#2789's class, by the owner's ruling 33 (#2423 comment 7180845467): over a base whose `__init__` takes an argument the dataclass's declaration is to be refused, with a steer to write `__init__` by hand, so no read of it has a cell. Today the dataclass is SPY0908 CS7036 wherever it stands and whoever reads it, since its emitted constructor chains to a base constructor it hands nothing (rows 2 and 3 of #2423 comment 7171020757)";

    /// <summary>The bases with no cell at all, total over <see cref="ScopeDataclassBase"/> with <see cref="DataclassBases"/> and
    /// the matrix's own value (no base).</summary>
    internal static readonly IReadOnlyDictionary<ScopeDataclassBase, string> ExcludedDataclassBases = new Dictionary<ScopeDataclassBase, string>
    {
        [ScopeDataclassBase.PlainWithInitParameters] = ABaseInitWithParametersIsRefused,
    };

    /// <summary>The bases with cells. Over a dataclass, <c>G</c>, the base stands first at both orders and the deriving
    /// dataclass is the declaration that moves: its <c>__init__</c> takes the base's field, which a cell prints, and its own.
    /// Any other base is declared with the dataclass and moves with it. A class with no <c>__init__</c>, an interface, an
    /// <c>@abstract</c> class and a constructed generic class add no field and no initializer, and are generated all the
    /// same (the owner's ruling 40): that their cells carry the verdicts of the cells with no base is what the rows say, and
    /// no reason's to claim. A CLR class (<c>Exception</c>) adds no field either, and under it, as under a class with an
    /// <c>__init__</c> and under a dataclass, a class that forwards to the dataclass from above it does not run.</summary>
    internal static readonly IReadOnlyDictionary<ScopeDataclassBase, InitShape> DataclassBases = new Dictionary<ScopeDataclassBase, InitShape>
    {
        [ScopeDataclassBase.Plain] = new("class G:\n    pass\n@dataclass\nclass DC(G):\n    v: int", "DC", "SPY0342", Twin: true),
        [ScopeDataclassBase.PlainWithInit] = new(BaseWithInit + "@dataclass\nclass DC(G):\n    v: int", "DC", "SPY0342", Twin: true),
        [ScopeDataclassBase.Dataclass] = new("@dataclass\nclass DC(G):\n    u: int", "DC", "SPY0342", Args: "5, 6", Params: "int, int", Twin: true, First: "@dataclass\nclass G:\n    v: int"),
        [ScopeDataclassBase.Interface] = new("interface G:\n    def g(self) -> int: ...\n@dataclass\nclass DC(G):\n    v: int\n    def g(self) -> int:\n        return self.v", "DC", "SPY0342", Twin: true),
        [ScopeDataclassBase.AbstractClass] = new("@abstract\nclass G:\n    pass\n@dataclass\nclass DC(G):\n    v: int", "DC", "SPY0342"),
        [ScopeDataclassBase.ConstructedGeneric] = new("class G[T]:\n    pass\n@dataclass\nclass DC(G[int]):\n    v: int", "DC", "SPY0342", Twin: true),
        [ScopeDataclassBase.ClrClass] = new("@dataclass\nclass DC(Exception):\n    v: int", "DC", "SPY0342", Twin: true),
    };

    /// <summary>The bases under which no cell is python's program, each with why: what <see cref="InitShape.Twin"/> says
    /// with a reason (<see cref="AxisProblems"/> holds the two together).</summary>
    internal static readonly IReadOnlyDictionary<ScopeDataclassBase, string> BasesWithNoTwin = new Dictionary<ScopeDataclassBase, string>
    {
        [ScopeDataclassBase.AbstractClass] = "the twin renderer has no row for `@abstract` on a class",
    };

    private const string DefaultedDataclass = "@dataclass\nclass DC:\n    v: int = 3";

    /// <summary>The defaulted field's two reads, total over <see cref="ScopeFieldDefault"/> with the matrix's own value. The
    /// default is 3 and a read that hands a value hands 5, so a cell prints which of the two the field took.</summary>
    internal static readonly IReadOnlyDictionary<ScopeFieldDefault, InitShape> FieldDefaults = new Dictionary<ScopeFieldDefault, InitShape>
    {
        [ScopeFieldDefault.Overridden] = new(DefaultedDataclass, "DC", "SPY0342", MisfitArgs: "5, 6", MisfitParams: "str", DerivedDefault: " = 6", Twin: true),
        [ScopeFieldDefault.Taken] = new(DefaultedDataclass, "DC", null, Args: "", Params: "", DerivedDefault: " = 6", Prints: "3", Twin: true),
    };

    /// <summary>The types a reader names in the dataclass's stead, total over <see cref="ScopeInitThrough"/> with the matrix's
    /// own value. The deriving type stands above its reader at both orders: the one forward read is the dataclass's.</summary>
    internal static readonly IReadOnlyDictionary<ScopeInitThrough, InitShape> InitThroughs = new Dictionary<ScopeInitThrough, InitShape>
    {
        [ScopeInitThrough.ForwardingClass] = new(ModuleDataclass, "Mid", "SPY0342", Above: "class Mid(DC):\n    pass", Twin: true),
        [ScopeInitThrough.DerivingDataclass] = new(ModuleDataclass, "Em", "SPY0342", Args: "5, 6", Params: "int, int", Above: "@dataclass\nclass Em(DC):\n    u: int", Twin: true),
    };

    private const string ABareNestedNameIsAnotherQuestionFirst = "from inside its enclosing type a nested dataclass's BARE name is another question before it is order's, and the owner's ruling 35 (#2423 comment 7181238911) generates the reader there under the qualified name. Handed to a function-typed slot from a method, the bare name runs with the dataclass above the method (N1 keeps a class-body name invisible by its bare name in a method) and is SPY0342 with it below; called as a constructor it is SPY0908 CS1955 at both orders, as the bare name of a nested class that writes its `__init__` is. The owner's ruling 38 (#2423 comment 7183929991) keeps the bare spelling out: whether a method sees a nested class's bare name at all is N1's question, decided in implementation. #2397 holds the construction (its comment 7182693674) and #2423 comment 7182714360 the reference";

    /// <summary>The names with no cell, total over <see cref="ScopeDataclassName"/> with <see cref="DataclassNames"/> and the
    /// matrix's own value.</summary>
    internal static readonly IReadOnlyDictionary<ScopeDataclassName, string> ExcludedDataclassNames = new Dictionary<ScopeDataclassName, string>
    {
        [ScopeDataclassName.BareSibling] = ABareNestedNameIsAnotherQuestionFirst,
    };

    /// <summary>An alias stands with the dataclass, below it. It must run, by the specification (type_aliases.md,
    /// "Transparency"), and it is no twin: the renderer's <c>A = DC</c> is not python's program
    /// (<see cref="NotPythonsProgram"/>).</summary>
    internal static readonly IReadOnlyDictionary<ScopeDataclassName, InitShape> DataclassNames = new Dictionary<ScopeDataclassName, InitShape>
    {
        [ScopeDataclassName.Alias] = new(ModuleDataclass + "\ntype A = DC", "A", "SPY0342", Type: "DC"),
    };

    /// <summary>The slots a reference is handed to, total over <see cref="ScopeReferenceSlot"/> with the matrix's own value (a
    /// parameter). Each is the constructor reference's program at that slot: a declaration of its own, read by <c>r()</c>. The
    /// reference stands in a function's body (<see cref="InitRead.WhenCalled"/>) but for a module variable and a field, whose
    /// initializer python evaluates as the module loads. A type that receives the reference (<c>W</c>) stands with the reader,
    /// above it, so that one declaration moves: the dataclass. The receiving type below its caller is another order, and
    /// not this axis's. A slot whose cells are no python program says why in <see cref="ReferenceSlotsWithNoTwin"/>.</summary>
    internal static readonly IReadOnlyDictionary<ScopeReferenceSlot, InitRead> ReferenceSlots = new Dictionary<ScopeReferenceSlot, InitRead>
    {
        [ScopeReferenceSlot.KeywordArgument] = new("def r() -> int:\n    return mk(f={N})", "def r() -> int:\n    return mk(f={N})",
            "def mk(f: ({P}) -> {T}) -> int:\n    return f({A}).v", "def mk(f: ({M}) -> {T}) -> int:\n    return 1", Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.AnnotatedLocal] = new("def r() -> int:\n    f: ({P}) -> {T} = {N}\n    return f({A}).v", "def r() -> int:\n    f: ({M}) -> {T} = {N}\n    return 1", Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.ModuleVariable] = new("g: ({P}) -> {T} = {N}\ndef r() -> int:\n    return g({A}).v", "g: ({M}) -> {T} = {N}\ndef r() -> int:\n    return 1", Shows: "r()"),
        [ScopeReferenceSlot.InstanceField] = new("class W:\n    f: ({P}) -> {T} = {N}\ndef r() -> int:\n    return W().f({A}).v", "class W:\n    f: ({M}) -> {T} = {N}\ndef r() -> int:\n    return 1", Shows: "r()"),
        [ScopeReferenceSlot.Return] = new("def give() -> ({P}) -> {T}:\n    return {N}\ndef r() -> int:\n    return give()({A}).v", "def give() -> ({M}) -> {T}:\n    return {N}\ndef r() -> int:\n    return 1", Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.ListElement] = new("def r() -> int:\n    fs: list[({P}) -> {T}] = [{N}]\n    return fs[0]({A}).v", "def r() -> int:\n    fs: list[({M}) -> {T}] = [{N}]\n    return 1", Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.TupleElement] = new("def r() -> int:\n    t: tuple[({P}) -> {T}, int] = ({N}, 1)\n    return t[0]({A}).v", "def r() -> int:\n    t: tuple[({M}) -> {T}, int] = ({N}, 1)\n    return 1", Shows: "r()", WhenCalled: true),
        // The control of a read that does not fit is the callee's own refusal there: its type parameter has nothing to be.
        [ScopeReferenceSlot.GenericParameter] = new("def r() -> int:\n    return gp({N}).v", "def r() -> int:\n    return gq({N})",
            "def gp[T](f: ({P}) -> T) -> T:\n    return f({A})", "def gq[T](f: ({M}) -> T) -> int:\n    return 1", Shows: "r()", Refusal: "SPY0237", WhenCalled: true),
        [ScopeReferenceSlot.BuiltinGenericParameter] = new("def r() -> int:\n    return list(map({N}, [{A}]))[0].v", Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.MethodParameter] = new("class W:\n    def take(self, f: ({P}) -> {T}) -> int:\n        return f({A}).v\ndef r() -> int:\n    return W().take({N})",
            "class W:\n    def take(self, f: ({M}) -> {T}) -> int:\n        return 1\ndef r() -> int:\n    return W().take({N})", Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.ConstructorParameter] = new("class W:\n    g: int = 0\n    def __init__(self, f: ({P}) -> {T}):\n        self.g = f({A}).v\ndef r() -> int:\n    return W({N}).g",
            "class W:\n    g: int = 0\n    def __init__(self, f: ({M}) -> {T}):\n        self.g = 1\ndef r() -> int:\n    return W({N}).g", Shows: "r()", WhenCalled: true),
        // No twin: the twin renderer has no row for an alias of a function type, which python spells with `Callable`.
        [ScopeReferenceSlot.AliasTypedLocal] = new("type F = ({P}) -> {T}\ndef r() -> int:\n    f: F = {N}\n    return f({A}).v", "type F = ({M}) -> {T}\ndef r() -> int:\n    f: F = {N}\n    return 1", Shows: "r()",
            WhenCalled: true, Python: false),
        [ScopeReferenceSlot.DictValue] = new("def r() -> int:\n    d: dict[str, ({P}) -> {T}] = {\"k\": {N}}\n    return d[\"k\"]({A}).v", "def r() -> int:\n    d: dict[str, ({M}) -> {T}] = {\"k\": {N}}\n    return 1", Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.StaticField] = new("class W:\n    @static\n    f: ({P}) -> {T} = {N}\ndef r() -> int:\n    return W.f({A}).v", "class W:\n    @static\n    f: ({M}) -> {T} = {N}\ndef r() -> int:\n    return 1", Shows: "r()"),
        [ScopeReferenceSlot.ConditionalBranch] = new("def r() -> int:\n    f: ({P}) -> {T} = {N} if True else {N}\n    return f({A}).v", "def r() -> int:\n    f: ({M}) -> {T} = {N} if True else {N}\n    return 1", Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.LambdaReturn] = new("def r() -> int:\n    g: () -> ({P}) -> {T} = lambda: {N}\n    return g()({A}).v", "def r() -> int:\n    g: () -> ({M}) -> {T} = lambda: {N}\n    return 1", Shows: "r()", WhenCalled: true),
        // The local's first value is a function declared last, below the dataclass at both orders: the reference is the second.
        [ScopeReferenceSlot.Reassignment] = new("def r() -> int:\n    f: ({P}) -> {T} = first\n    f = {N}\n    return f({A}).v", "def r() -> int:\n    f: ({M}) -> {T} = first\n    f = {N}\n    return 1",
            "def first(v: {P}) -> {T}:\n    return {N}(v + 1)", "def first() -> {T}:\n    return {N}(1)", Shows: "r()", WhenCalled: true),
        // The nine the third review ran (ruling 40). A list's or a dict's first element is a function declared last, as the
        // re-assignment's is; a field assigned in `__init__` has no initializer to read anything.
        [ScopeReferenceSlot.Append] = new("def r() -> int:\n    fs: list[({P}) -> {T}] = []\n    fs.append({N})\n    return fs[0]({A}).v", "def r() -> int:\n    fs: list[({M}) -> {T}] = []\n    fs.append({N})\n    return 1",
            Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.ListSubscriptStore] = new("def r() -> int:\n    fs: list[({P}) -> {T}] = [first]\n    fs[0] = {N}\n    return fs[0]({A}).v", "def r() -> int:\n    fs: list[({M}) -> {T}] = [first]\n    fs[0] = {N}\n    return 1",
            "def first(v: {P}) -> {T}:\n    return {N}(v + 1)", "def first() -> {T}:\n    return {N}(1)", Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.DictSubscriptStore] = new("def r() -> int:\n    d: dict[str, ({P}) -> {T}] = {}\n    d[\"k\"] = {N}\n    return d[\"k\"]({A}).v", "def r() -> int:\n    d: dict[str, ({M}) -> {T}] = {}\n    d[\"k\"] = {N}\n    return 1",
            Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.LambdaArgument] = new("def r() -> int:\n    g: (({P}) -> {T}) -> int = lambda f: f({A}).v\n    return g({N})", "def r() -> int:\n    g: (({M}) -> {T}) -> int = lambda f: 1\n    return g({N})",
            Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.NestedDefParameter] = new("def r() -> int:\n    def inner(f: ({P}) -> {T}) -> int:\n        return f({A}).v\n    return inner({N})", "def r() -> int:\n    def inner(f: ({M}) -> {T}) -> int:\n        return 1\n    return inner({N})",
            Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.PropertyDefault] = new("class W:\n    property p: ({P}) -> {T} = {N}\ndef r() -> int:\n    return W().p({A}).v", "class W:\n    property p: ({M}) -> {T} = {N}\ndef r() -> int:\n    return 1", Shows: "r()"),
        [ScopeReferenceSlot.SelfFieldAssignment] = new("class W:\n    f: ({P}) -> {T}\n    def __init__(self):\n        self.f = {N}\ndef r() -> int:\n    return W().f({A}).v",
            "class W:\n    f: ({M}) -> {T}\n    def __init__(self):\n        self.f = {N}\ndef r() -> int:\n    return 1", Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.SetElement] = new("def r() -> int:\n    fs: set[({P}) -> {T}] = {{N}}\n    return [f({A}).v for f in fs][0]", "def r() -> int:\n    fs: set[({M}) -> {T}] = {{N}}\n    return 1", Shows: "r()", WhenCalled: true),
        [ScopeReferenceSlot.Yield] = new("def gen() -> ({P}) -> {T}:\n    yield {N}\ndef r() -> int:\n    return [f({A}).v for f in gen()][0]", "def gen() -> ({M}) -> {T}:\n    yield {N}\ndef r() -> int:\n    return 1",
            Shows: "r()", WhenCalled: true),
    };

    /// <summary>The slots at which no cell is python's program, each with why: what <see cref="InitRead.Python"/> says with a
    /// reason (<see cref="AxisProblems"/> holds the two together).</summary>
    internal static readonly IReadOnlyDictionary<ScopeReferenceSlot, string> ReferenceSlotsWithNoTwin = new Dictionary<ScopeReferenceSlot, string>
    {
        [ScopeReferenceSlot.AliasTypedLocal] = "the twin renderer has no row for an alias of a function type, which python spells with `Callable`",
    };

    /// <summary>One value of an axis the matrix holds at its first value: what the exclusions know it by
    /// (<paramref name="Value"/>: the axis's enum value, or a projected site's label), the shape its cells are written over, and
    /// the readers it has a program for.</summary>
    internal sealed record InitProjection(object Value, InitShape Shape, IReadOnlyDictionary<ScopeInitReader, InitRead> Readers)
    {
        /// <summary>The site part of a cell's key: the projected site, or the module's.</summary>
        public string Site => Value as string ?? ModuleSite;

        /// <summary>The key part that says which value the cell carries; a projected site's label is its site part.</summary>
        public object? Variant => Value is string ? null : Value;
    }

    private const string ModuleSite = "module";

    /// <summary>The projections (the owner's ruling 35): each value generated under every reader and fit it has a program for,
    /// at both orders, or excluded with its reason (<see cref="ExcludedProjections"/>). A reference slot is the constructor
    /// reference's alone.</summary>
    internal static IReadOnlyList<InitProjection> InitProjections => LazyInitProjections.Value;

    private static readonly Lazy<IReadOnlyList<InitProjection>> LazyInitProjections = new(() =>
    {
        var module = DataclassSites[ScopeKind.Module].Single(s => s.Label == ModuleSite).Shape with { Twin = true };
        return AllDataclassSites().Where(s => s.Projected).Select(s => new InitProjection(s.Label, s.Shape, InitReaders))
            .Concat(DataclassBases.OrderBy(e => e.Key).Select(e => new InitProjection(e.Key, e.Value, InitReaders)))
            .Concat(FieldDefaults.OrderBy(e => e.Key).Select(e => new InitProjection(e.Key, e.Value, InitReaders)))
            .Concat(InitThroughs.OrderBy(e => e.Key).Select(e => new InitProjection(e.Key, e.Value, InitReaders)))
            .Concat(DataclassNames.OrderBy(e => e.Key).Select(e => new InitProjection(e.Key, e.Value, InitReaders)))
            .Concat(ReferenceSlots.OrderBy(e => e.Key).Select(e => new InitProjection(e.Key, module,
                new Dictionary<ScopeInitReader, InitRead> { [ScopeInitReader.ConstructorReference] = e.Value })))
            .ToList();
    });

    /// <summary>Where a projected reader that is an expression stands: one slot of <see cref="InitReaderSlots"/>, a function's
    /// body, since the matrix holds the slot axis under the first value of every projected one.</summary>
    internal static readonly IReadOnlyList<(ReadPosition Position, Harness.Nesting Nesting)> ProjectedSlots = new[] { (ReadPosition.FunctionBody, Harness.Nesting.Direct) };

    private const string BaseWithInit = "class G:\n    def __init__(self):\n        pass\n";

    private const string TheBaseInitIsBoundPastTheDataclass = "#2508's class (its comment 7180653024; row 3 of #2423 comment 7171020757): over a base whose `__init__` takes no argument, `super().__init__` in a class deriving from the dataclass is bound to the BASE's `__init__`, past the dataclass's own, with the dataclass above the call as below it. Handed the field it is SPY0224; handed nothing it is taken, and the dataclass's constructor is then SPY0908 CS7036. Red where the dataclass is declared first, so no order is at fault";
    private const string ADerivingHostLosesTheDefault = "#2508's class (its comment 7181213880): a host deriving from a dataclass whose field has a default loses the default. Constructed with nothing, a deriving class with no `__init__` is SPY0908 CS7036 with the dataclass above it as below it, and a deriving dataclass is SPY0908 CS7036 with the dataclass above it (below, it runs). Red where the dataclass is declared first, so no order is at fault";
    private const string ATakenDefaultHandsNothing = "a read that takes the default hands nothing, which is what a defaulted `__init__` takes: the read that does not fit hands a value, and is the overridden default's cell";
    private const string AConstructedGenericIsNoReference = "a constructed generic type handed to a function-typed slot (`mk(DC[int])`) is SPY0339 with the class above the reference as below it, and for a class that writes its `__init__` as for a dataclass: red where the type is declared first, so no order is at fault and no row may hold it. The owner has ruled that the reference is to run (ruling 42, #2423 comment 7199064429), #2801 tracks it, and the value is excluded until that lands";
    private const string ADataclassOverAConstructedGeneric = "#2021's class (its comment 7181214084): a dataclass over a constructed generic dataclass keeps the base's type parameter in its generated constructor. It is SPY0908 CS0246 with the generic dataclass above it (and SPY0908 CS7036 beside CS1729 below): red where the dataclass is declared first, so no order is at fault";
    private const string ATypeNestedTwoDeepIsNoReference = "a type nested two deep handed to a function-typed slot by its qualified name (`mk(O.P.DC)`) is SPY0908 CS0119 with the type above the reference as below it, and for a class that writes its `__init__` as for a dataclass: no order is at fault, and #1941 holds the cell (its comment 7182693487). One deep (`mk(O.DC)`) is the nested site's cells";
    private const string AnAliasIsNotConstructed = "a type alias called as a constructor (`A(5)`) is SPY0908 CS0103 with the class above the call as below it, and for a class that writes its `__init__` as for a dataclass: no order is at fault, and #1925 holds the cell (its comment 7182693272)";
    private const string AnAliasIsNoBase = "a type alias in a base list (`class D(A)`) is SPY0202 with the class above the deriving one as below it, and for a class that writes its `__init__` as for a dataclass: nothing derives through an alias, so no deriving reader has a cell there (#1925 holds the cell: its comment 7182693272)";
    private const string ADataclassOverAForwardingClass = "a dataclass over a class that derives from a dataclass and is none itself gets no parameter for the inherited field: SPY0908 (CS1729 where it is constructed, CS7036 in its own constructor) with the dataclass above it as below it. Red where the dataclass is declared first, so no order is at fault, and #2021 holds the cell (its comment 7182693882)";
    private const string MapChecksNoElement = "`map` checks no element against what the reference takes: handed a `str` for the field (`map(DC, [\"a\"])`) it is SPY0908 CS1503 with the class above the call, and for a class that writes its `__init__` as for a dataclass. A read that does not fit has no refusing control there (#2800's class)";

    /// <summary>The projected value × reader × fit triples with no cell, each with its reason: total, per projection, over the
    /// readers it has a program for and the fits <see cref="ExcludedInitFits"/> leaves, with the triples a cell carries
    /// (<see cref="AxisProblems"/>). A triple that is red wherever the dataclass stands is not the order's cell.</summary>
    internal static readonly IReadOnlyDictionary<(object Value, ScopeInitReader Reader, ScopeInitFit Fit), string> ExcludedProjections = new Dictionary<(object, ScopeInitReader, ScopeInitFit), string>
    {
        [(ScopeDataclassBase.PlainWithInit, ScopeInitReader.SuperInit, ScopeInitFit.Fits)] = TheBaseInitIsBoundPastTheDataclass,
        [(ScopeDataclassBase.PlainWithInit, ScopeInitReader.SuperInit, ScopeInitFit.Misfits)] = TheBaseInitIsBoundPastTheDataclass,
        [(ScopeFieldDefault.Taken, ScopeInitReader.Forwarding, ScopeInitFit.Fits)] = ADerivingHostLosesTheDefault,
        [(ScopeFieldDefault.Taken, ScopeInitReader.InheritedFields, ScopeInitFit.Fits)] = ADerivingHostLosesTheDefault,
        [(ScopeFieldDefault.Taken, ScopeInitReader.ConstructorReference, ScopeInitFit.Misfits)] = ATakenDefaultHandsNothing,
        [(ScopeFieldDefault.Taken, ScopeInitReader.SuperInit, ScopeInitFit.Misfits)] = ATakenDefaultHandsNothing,
        [("generic", ScopeInitReader.ConstructorReference, ScopeInitFit.Fits)] = AConstructedGenericIsNoReference,
        [("generic", ScopeInitReader.ConstructorReference, ScopeInitFit.Misfits)] = AConstructedGenericIsNoReference,
        [("generic", ScopeInitReader.InheritedFields, ScopeInitFit.Fits)] = ADataclassOverAConstructedGeneric,
        [("deep", ScopeInitReader.ConstructorReference, ScopeInitFit.Fits)] = ATypeNestedTwoDeepIsNoReference,
        [("deep", ScopeInitReader.ConstructorReference, ScopeInitFit.Misfits)] = ATypeNestedTwoDeepIsNoReference,
        [(ScopeDataclassName.Alias, ScopeInitReader.Construction, ScopeInitFit.Fits)] = AnAliasIsNotConstructed,
        [(ScopeDataclassName.Alias, ScopeInitReader.SuperInit, ScopeInitFit.Fits)] = AnAliasIsNoBase,
        [(ScopeDataclassName.Alias, ScopeInitReader.SuperInit, ScopeInitFit.Misfits)] = AnAliasIsNoBase,
        [(ScopeDataclassName.Alias, ScopeInitReader.Forwarding, ScopeInitFit.Fits)] = AnAliasIsNoBase,
        [(ScopeDataclassName.Alias, ScopeInitReader.InheritedFields, ScopeInitFit.Fits)] = AnAliasIsNoBase,
        [(ScopeInitThrough.ForwardingClass, ScopeInitReader.InheritedFields, ScopeInitFit.Fits)] = ADataclassOverAForwardingClass,
        [(ScopeReferenceSlot.BuiltinGenericParameter, ScopeInitReader.ConstructorReference, ScopeInitFit.Misfits)] = MapChecksNoElement,
    };

    // --- the placements: where the reader stands against the dataclass --- //

    /// <summary>The nested site's label, which a reader inside the enclosing type shares: its dataclass is the nested one.</summary>
    private const string EnclosedSite = "nested";

    /// <summary>The nested dataclass as a member of the reader's own type <c>H</c>, and the qualified name a reader there
    /// spells. A constructor reference that does not fit is the slot's own mismatch, as at the nested site.</summary>
    internal static readonly InitShape EnclosedShape = new(ModuleDataclass, "H.DC", "SPY0220");

    /// <summary>Where an expression reader stands inside the enclosing type: the two bodies and the two initializers a class has
    /// per instance and per type, each one of <see cref="InitReaderSlots"/> whose host is a plain class. The matrix holds the
    /// other slots from outside the type.</summary>
    internal static readonly IReadOnlyList<(ReadPosition Position, Harness.Nesting Nesting)> EnclosedSlots = new[]
    {
        (ReadPosition.MethodBody, Harness.Nesting.Direct),
        (ReadPosition.StaticMethodBody, Harness.Nesting.Direct),
        (ReadPosition.InstanceFieldInitializer, Harness.Nesting.Direct),
        (ReadPosition.StaticInitializer, Harness.Nesting.Direct),
    };

    private const string OtherDataclassSite = ModuleSite;

    /// <summary>The module's dataclass, read from a method of another dataclass.</summary>
    internal static readonly InitShape OtherDataclassShape = new(ModuleDataclass, "DC", "SPY0342");

    internal static readonly (ReadPosition Position, Harness.Nesting Nesting) OtherDataclassSlot = (ReadPosition.MethodBody, Harness.Nesting.Direct);

    /// <summary>The orders of a reader in another dataclass: that dataclass below the one it reads, above it, and above it in a
    /// ring.</summary>
    internal static readonly Harness.Order[] OtherDataclassOrders = { Harness.Order.Earlier, Harness.Order.Later, Harness.Order.Cycle };

    /// <summary>What the dataclass reads back of <c>Rd</c> in a ring, per reader: the same kind of read, one that fits, with a
    /// helper of its own.</summary>
    internal static readonly IReadOnlyDictionary<ScopeInitReader, (string Read, string Helper)> RingReads = new Dictionary<ScopeInitReader, (string, string)>
    {
        [ScopeInitReader.Construction] = ("shr(Rd(5))", "def shr(d: Rd) -> int:\n    return d.w"),
        [ScopeInitReader.ConstructorReference] = ("mkr(Rd)", "def mkr(f: (int) -> Rd) -> int:\n    return f(5).w"),
    };

    /// <summary>What the dataclass derives from where its reader stands in another dataclass, total over the bases a cell is
    /// generated for that are dataclasses, with none: under a dataclass base the ring's reader is a method of that base (the
    /// second review of PR #2798, finding 1), and its control is the same read from a third dataclass below the two.</summary>
    internal static readonly ScopeDataclassBase[] OtherDataclassRings = { ScopeDataclassBase.None, ScopeDataclassBase.Dataclass };

    private const string AReaderInsideTheDataclass = "a reader in the dataclass's own body is no second declaration: there is no order of two to vary, and a method that hands its own class to a function-typed slot runs";

    /// <summary>The placements with no cell, total over <see cref="ScopeInitPlacement"/> with the placements a cell carries.</summary>
    internal static readonly IReadOnlyDictionary<ScopeInitPlacement, string> ExcludedInitPlacements = new Dictionary<ScopeInitPlacement, string>
    {
        [ScopeInitPlacement.Itself] = AReaderInsideTheDataclass,
    };

    /// <summary>The placement × reader pairs with no cell: a reader that is a declaration stands in no method.</summary>
    internal static readonly IReadOnlyDictionary<(ScopeInitPlacement Placement, ScopeInitReader Reader), string> ExcludedPlacementReaders = new Dictionary<(ScopeInitPlacement, ScopeInitReader), string>
    {
        [(ScopeInitPlacement.OtherDataclass, ScopeInitReader.SuperInit)] = AClassInAFunctionDeclaresNoName,
        [(ScopeInitPlacement.OtherDataclass, ScopeInitReader.Forwarding)] = AClassInAFunctionDeclaresNoName,
        [(ScopeInitPlacement.OtherDataclass, ScopeInitReader.InheritedFields)] = AClassInAFunctionDeclaresNoName,
    };

    /// <summary>Where a reader that is an expression stands: READ from the typed-read set's readers, as the slots at which the
    /// P77a pool runs a bare read of a module-level function declared above its reader. A dataclass's name is such a name (at
    /// module level, taken by a call or a function-typed slot), so the axis is the image of <see cref="ReadPosition"/> and
    /// <c>Nesting</c> under the pool's own rosters, and a slot the pool gains is a cell here with no edit.</summary>
    internal static IReadOnlyList<(ReadPosition Position, Harness.Nesting Nesting)> InitReaderSlots
        => TypedReaders.Where(r => r is { Kind: BindingKind.ModuleDef, Spelling: Harness.Spelling.Bare, Inside: false }).Select(r => (r.Position, r.Nesting)).Distinct().ToList();

    /// <summary>The slots at which python reads a name when <c>main</c> calls the reader, and not as the module loads: a
    /// function's or a method's body and what stands in one, and a lambda's body wherever the lambda is stored. Total over
    /// <see cref="InitReaderSlots"/> with <see cref="SlotsWithNoTwin"/>.</summary>
    internal static readonly IReadOnlySet<(ReadPosition Position, Harness.Nesting Nesting)> CalledSlots = new HashSet<(ReadPosition, Harness.Nesting)>
    {
        (ReadPosition.MethodBody, Harness.Nesting.Direct),
        (ReadPosition.StaticMethodBody, Harness.Nesting.Direct),
        (ReadPosition.InitBody, Harness.Nesting.Direct),
        (ReadPosition.AccessorGetBody, Harness.Nesting.Direct),
        (ReadPosition.AccessorSetBody, Harness.Nesting.Direct),
        (ReadPosition.NestedClassMethodBody, Harness.Nesting.Direct),
        (ReadPosition.NestedDefInMethod, Harness.Nesting.Direct),
        (ReadPosition.FunctionBody, Harness.Nesting.Direct),
        (ReadPosition.NestedDefInFunction, Harness.Nesting.Direct),
        (ReadPosition.GeneratorBody, Harness.Nesting.Direct),
        (ReadPosition.LambdaStored, Harness.Nesting.Direct),
        (ReadPosition.LambdaImmediate, Harness.Nesting.Direct),
        (ReadPosition.LambdaPassed, Harness.Nesting.Direct),
        (ReadPosition.ComprehensionIterable, Harness.Nesting.Direct),
        (ReadPosition.ComprehensionElement, Harness.Nesting.Direct),
        (ReadPosition.ModuleInitializer, Harness.Nesting.Lambda),
    };

    private const string ReadAsTheModuleLoads = "python evaluates the initializer as the module or the type body loads, where the helper the reader hands its value to, which stands last, is not bound yet: a NameError wherever the dataclass stands";
    private const string PythonHasNoModuleProperty = "python has no module-level property, and the twin renderer has no row for an accessor without `self`";

    /// <summary>The slots at which no cell is python's program, each with why.</summary>
    internal static readonly IReadOnlyDictionary<(ReadPosition Position, Harness.Nesting Nesting), string> SlotsWithNoTwin = new Dictionary<(ReadPosition, Harness.Nesting), string>
    {
        [(ReadPosition.StaticInitializer, Harness.Nesting.Direct)] = ReadAsTheModuleLoads,
        [(ReadPosition.InstanceFieldInitializer, Harness.Nesting.Direct)] = ReadAsTheModuleLoads,
        [(ReadPosition.ConstInitializer, Harness.Nesting.Direct)] = ReadAsTheModuleLoads,
        [(ReadPosition.StaticPropertyDefault, Harness.Nesting.Direct)] = ReadAsTheModuleLoads,
        [(ReadPosition.InstancePropertyDefault, Harness.Nesting.Direct)] = ReadAsTheModuleLoads,
        [(ReadPosition.ModuleInitializer, Harness.Nesting.Direct)] = ReadAsTheModuleLoads,
        [(ReadPosition.ModuleInitializer, Harness.Nesting.ComprehensionIterable)] = ReadAsTheModuleLoads,
        [(ReadPosition.ModuleInitializer, Harness.Nesting.ComprehensionElement)] = ReadAsTheModuleLoads,
        [(ReadPosition.ModulePropertyInitializer, Harness.Nesting.Direct)] = ReadAsTheModuleLoads,
        [(ReadPosition.ModuleAccessorBody, Harness.Nesting.Direct)] = PythonHasNoModuleProperty,
    };

    // --- written-init: reader × fit × order, and the argument a read that fits hands --- //

    private const string WrittenClass = "class DC:\n    v: int = 0\n    def __init__(self, v: int):\n        self.v = v";
    private const string ListTakingClass = "class DC:\n    v: int = 0\n    def __init__(self, xs: list[int]):\n        self.v = len(xs) + 5";
    private const string DictTakingClass = "class DC:\n    v: int = 0\n    def __init__(self, d: dict[str, int]):\n        self.v = len(d) + 5";
    private const string GenericWrittenClass = "class DC[T]:\n    v: T\n    def __init__(self, v: T):\n        self.v = v";
    private const string StructWrittenClass = "struct DC:\n    v: int = 0\n    def __init__(self, v: int):\n        self.v = v";
    private const string NestedWrittenClass = "class O:\n    class DC:\n        v: int = 0\n        def __init__(self, v: int):\n            self.v = v";
    private const string ForwardingClass = "class D(DC):\n    pass\n";

    /// <summary>The class a cell reads, per what a read that fits hands its <c>__init__</c>: total over
    /// <see cref="ScopeInitArgument"/>. Every cell that runs prints 5: the argument, or (the empty list and the empty dict)
    /// what the <c>__init__</c> makes of it. A read that does not fit hands a <c>str</c>, and offers to take one.</summary>
    internal static readonly IReadOnlyDictionary<ScopeInitArgument, InitShape> WrittenInits = new Dictionary<ScopeInitArgument, InitShape>
    {
        [ScopeInitArgument.Typed] = new(WrittenClass, "DC", "SPY0342", MisfitArgs: "\"a\"", MisfitParams: "str", Twin: true),
        [ScopeInitArgument.EmptyList] = new(ListTakingClass, "DC", "SPY0342", Args: "[]", Params: "list[int]", MisfitArgs: "\"a\"", MisfitParams: "str", Twin: true),
        // The construction names the class bare, so the argument alone says what `T` is; a base list and a type position spell it constructed.
        [ScopeInitArgument.TypeParameter] = new(GenericWrittenClass, "DC", "SPY0342", Type: "DC[int]", MisfitArgs: "\"a\"", MisfitParams: "str", Twin: true,
            Base: "DC[int]"),
        [ScopeInitArgument.EmptyDict] = new(DictTakingClass, "DC", "SPY0342", Args: "{}", Params: "dict[str, int]", MisfitArgs: "\"a\"", MisfitParams: "str", Twin: true),
    };

    /// <summary>The readers of a written <c>__init__</c>, total over <see cref="ScopeInitReader"/> with
    /// <see cref="ExcludedWrittenInitReaders"/>. A construction reads the field where it stands: nothing between the argument
    /// and the parameter gives the argument a type. The control of a misfit of type is the checker's own mismatch (measured
    /// @ 28f10cd51), but for a reference, whose control is the slot's. A construction that does not fit spells the class as a
    /// type position does (<c>{T}</c>): over a generic class the bare name would take the value as its type argument and
    /// fit, so the misfit names the argument (<c>DC[int]("a")</c>); over every other shape the two spellings are one.</summary>
    internal static readonly IReadOnlyDictionary<ScopeInitReader, InitRead> WrittenInitReaders = new Dictionary<ScopeInitReader, InitRead>
    {
        [ScopeInitReader.Construction] = new("{N}({A}).v", "{T}({X}).v", Refusal: "SPY0220"),
        [ScopeInitReader.ConstructorReference] = new("mk({N})", "mk({N})", "def mk(f: ({P}) -> {T}) -> int:\n    return f({A}).v", "def mk(f: ({M}) -> {T}) -> int:\n    return 1"),
        [ScopeInitReader.SuperInit] = new("class D({B}):\n    def __init__(self):\n        super().__init__({A})", "class D({B}):\n    def __init__(self):\n        super().__init__({X})",
            Shows: "D().v", Refusal: "SPY0220"),
        [ScopeInitReader.Forwarding] = new("class D({B}):\n    pass", Shows: "D({A}).v"),
    };

    private const string NoSelfInitInAClass = "`self.__init__` called from a method is SPY0350 in any class: there is no such reader to stand above the class";

    internal static readonly IReadOnlyDictionary<ScopeInitReader, string> ExcludedWrittenInitReaders = new Dictionary<ScopeInitReader, string>
    {
        [ScopeInitReader.SelfInit] = NoSelfInitInAClass,
        // The reader is a dataclass over a class whose `__init__` takes an argument: the base shape ruling 33 refuses.
        [ScopeInitReader.InheritedFields] = ABaseInitWithParametersIsRefused,
    };

    private const string AForwardedConstructionIsUnchecked = "a class that derives from one with a written `__init__` and declares none is constructed unchecked: handed a `str` for the `int` (`D(\"a\")`) it is SPY0908 CS1503 with the class above the deriving one as below it, where the class itself is SPY0220. So it is whatever the parameter's type is (a list, a dict, a constructed generic class's `T`), over a nested class, and for the other ways of not fitting: handed nothing it is SPY0908 CS7036, and a keyword the `__init__` does not take SPY0908 CS1739, at both orders. The misfit has no refusing control, so no order is at fault, and #2021 holds the cell (its comment 7185651052)";
    private const string AForwardedArgumentHasNoParameterType = "an empty list or an empty dict handed through a class that derives from the one with the `__init__` and declares none (`D([])`, `D({})`) is SPY0227 with the class above the deriving one as below it: the argument gets no type from the inherited parameter at either order, so no order is at fault, and #2021 holds the cell (its comment 7185651052)";

    /// <summary>The argument × reader × fit triples with no cell, total over the three with the triples a cell carries
    /// (<see cref="AxisProblems"/>).</summary>
    internal static readonly IReadOnlyDictionary<(ScopeInitArgument Argument, ScopeInitReader Reader, ScopeInitFit Fit), string> ExcludedWrittenInits
        = new Dictionary<(ScopeInitArgument, ScopeInitReader, ScopeInitFit), string>
        {
            [(ScopeInitArgument.Typed, ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = AForwardedConstructionIsUnchecked,
            [(ScopeInitArgument.EmptyList, ScopeInitReader.Forwarding, ScopeInitFit.Fits)] = AForwardedArgumentHasNoParameterType,
            [(ScopeInitArgument.EmptyList, ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = AForwardedConstructionIsUnchecked,
            [(ScopeInitArgument.TypeParameter, ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = AForwardedConstructionIsUnchecked,
            [(ScopeInitArgument.EmptyDict, ScopeInitReader.Forwarding, ScopeInitFit.Fits)] = AForwardedArgumentHasNoParameterType,
            [(ScopeInitArgument.EmptyDict, ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = AForwardedConstructionIsUnchecked,
        };

    // --- written-init: what else varies alone from the base cell (ruling 40) --- //

    /// <summary>One value of <see cref="ScopeInitVariation"/>: the class its cells read, where a reader that is an expression
    /// stands, the orders, and the code a reader's refusal carries where it is not the base cell's.</summary>
    internal sealed record InitVariation(InitShape Shape, (ReadPosition Position, Harness.Nesting Nesting) Slot, Harness.Order[] Orders, IReadOnlyDictionary<ScopeInitReader, string>? Refusals = null);

    private static readonly Harness.Order[] TwoOrders = { Harness.Order.Earlier, Harness.Order.Later };
    private static readonly (ReadPosition Position, Harness.Nesting Nesting) FunctionSlot = (ReadPosition.FunctionBody, Harness.Nesting.Direct);

    private static InitShape WrittenShape(string declaration, string name = "DC", string? referenceRefusal = "SPY0342", string misfitArgs = "\"a\"", string misfitParams = "str")
        => new(declaration, name, referenceRefusal, MisfitArgs: misfitArgs, MisfitParams: misfitParams, Twin: true);

    /// <summary>The variations, total over <see cref="ScopeInitVariation"/> with the star's own value (none). Each is the base
    /// cell (<see cref="ScopeInitArgument.Typed"/>) with one thing changed. A nested class is read by its qualified name, and
    /// a reference to it that does not fit is the slot's own mismatch, as the nested dataclass's is. A misfit of arity hands
    /// nothing and offers to take nothing; an unknown keyword is <c>w</c>. The ring's reader is a method of another class that
    /// writes its <c>__init__</c>, <c>Rd</c>, which the class reads back from a method of its own at <c>Order.Cycle</c>.</summary>
    internal static readonly IReadOnlyDictionary<ScopeInitVariation, InitVariation> WrittenVariations = new Dictionary<ScopeInitVariation, InitVariation>
    {
        [ScopeInitVariation.StructHost] = new(WrittenShape(StructWrittenClass), FunctionSlot, TwoOrders),
        [ScopeInitVariation.NestedClass] = new(WrittenShape(NestedWrittenClass, "O.DC", "SPY0220"), FunctionSlot, TwoOrders),
        [ScopeInitVariation.MethodReader] = new(WrittenShape(WrittenClass), (ReadPosition.MethodBody, Harness.Nesting.Direct), TwoOrders),
        [ScopeInitVariation.ModuleInitializerReader] = new(WrittenShape(WrittenClass), (ReadPosition.ModuleInitializer, Harness.Nesting.Direct), TwoOrders),
        [ScopeInitVariation.Ring] = new(WrittenShape(WrittenClass), (ReadPosition.MethodBody, Harness.Nesting.Direct), new[] { Harness.Order.Earlier, Harness.Order.Later, Harness.Order.Cycle }),
        [ScopeInitVariation.MisfitOfArity] = new(WrittenShape(WrittenClass, misfitArgs: "", misfitParams: ""), FunctionSlot, TwoOrders,
            new Dictionary<ScopeInitReader, string> { [ScopeInitReader.Construction] = "SPY0224", [ScopeInitReader.SuperInit] = "SPY0224" }),
        [ScopeInitVariation.UnknownKeyword] = new(WrittenShape(WrittenClass, misfitArgs: "w=5", misfitParams: ""), FunctionSlot, TwoOrders,
            new Dictionary<ScopeInitReader, string> { [ScopeInitReader.Construction] = "SPY0234", [ScopeInitReader.SuperInit] = "SPY0234" }),
    };

    /// <summary>The other class of the ring, and what the class reads back of it at <c>Order.Cycle</c>, per reader: the same
    /// kind of read, one that fits, with a helper of its own.</summary>
    private const string RingClass = "class Rd:\n    w: int = 0\n    def __init__(self, w: int):\n        self.w = w";

    internal static readonly IReadOnlyDictionary<ScopeInitReader, (string Read, string? Helper)> WrittenRingReads = new Dictionary<ScopeInitReader, (string, string?)>
    {
        [ScopeInitReader.Construction] = ("Rd(5).w", null),
        [ScopeInitReader.ConstructorReference] = ("mkr(Rd)", "def mkr(f: (int) -> Rd) -> int:\n    return f(5).w"),
    };

    private const string AStructIsNoBase = "a struct is no base: a class or a struct that derives from one is SPY0281, with the struct above it as below it. Nothing forwards to its `__init__` or calls it through `super()`";
    private const string ADeclarationIsNoInitializer = "a deriving class is a declaration and no expression: it stands in no initializer, and at module level it is the base cell's reader";
    private const string AWayOfNotFitting = "the variation is a way of not fitting: the read that fits is the base cell's";
    private const string AReferenceHandsNoKeyword = "a function type names no parameter, so a slot has no keyword to hand a reference under: the misfit a reference has is the slot's own, of type or of arity";

    /// <summary>The variation × reader × fit triples with no cell, total per variation over the readers of a written
    /// <c>__init__</c> and the two fits with the triples a cell carries (<see cref="AxisProblems"/>).</summary>
    internal static readonly IReadOnlyDictionary<(ScopeInitVariation Variation, ScopeInitReader Reader, ScopeInitFit Fit), string> ExcludedWrittenVariations
        = new Dictionary<(ScopeInitVariation, ScopeInitReader, ScopeInitFit), string>
        {
            [(ScopeInitVariation.StructHost, ScopeInitReader.SuperInit, ScopeInitFit.Fits)] = AStructIsNoBase,
            [(ScopeInitVariation.StructHost, ScopeInitReader.SuperInit, ScopeInitFit.Misfits)] = AStructIsNoBase,
            [(ScopeInitVariation.StructHost, ScopeInitReader.Forwarding, ScopeInitFit.Fits)] = AStructIsNoBase,
            [(ScopeInitVariation.StructHost, ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = AStructIsNoBase,
            [(ScopeInitVariation.NestedClass, ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = AForwardedConstructionIsUnchecked,
            [(ScopeInitVariation.MethodReader, ScopeInitReader.SuperInit, ScopeInitFit.Fits)] = AClassInAFunctionDeclaresNoName,
            [(ScopeInitVariation.MethodReader, ScopeInitReader.SuperInit, ScopeInitFit.Misfits)] = AClassInAFunctionDeclaresNoName,
            [(ScopeInitVariation.MethodReader, ScopeInitReader.Forwarding, ScopeInitFit.Fits)] = AClassInAFunctionDeclaresNoName,
            [(ScopeInitVariation.MethodReader, ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = AClassInAFunctionDeclaresNoName,
            [(ScopeInitVariation.ModuleInitializerReader, ScopeInitReader.SuperInit, ScopeInitFit.Fits)] = ADeclarationIsNoInitializer,
            [(ScopeInitVariation.ModuleInitializerReader, ScopeInitReader.SuperInit, ScopeInitFit.Misfits)] = ADeclarationIsNoInitializer,
            [(ScopeInitVariation.ModuleInitializerReader, ScopeInitReader.Forwarding, ScopeInitFit.Fits)] = ADeclarationIsNoInitializer,
            [(ScopeInitVariation.ModuleInitializerReader, ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = ADeclarationIsNoInitializer,
            [(ScopeInitVariation.Ring, ScopeInitReader.SuperInit, ScopeInitFit.Fits)] = AClassInAFunctionDeclaresNoName,
            [(ScopeInitVariation.Ring, ScopeInitReader.SuperInit, ScopeInitFit.Misfits)] = AClassInAFunctionDeclaresNoName,
            [(ScopeInitVariation.Ring, ScopeInitReader.Forwarding, ScopeInitFit.Fits)] = AClassInAFunctionDeclaresNoName,
            [(ScopeInitVariation.Ring, ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = AClassInAFunctionDeclaresNoName,
            [(ScopeInitVariation.MisfitOfArity, ScopeInitReader.Construction, ScopeInitFit.Fits)] = AWayOfNotFitting,
            [(ScopeInitVariation.MisfitOfArity, ScopeInitReader.ConstructorReference, ScopeInitFit.Fits)] = AWayOfNotFitting,
            [(ScopeInitVariation.MisfitOfArity, ScopeInitReader.SuperInit, ScopeInitFit.Fits)] = AWayOfNotFitting,
            [(ScopeInitVariation.MisfitOfArity, ScopeInitReader.Forwarding, ScopeInitFit.Fits)] = AWayOfNotFitting,
            [(ScopeInitVariation.MisfitOfArity, ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = AForwardedConstructionIsUnchecked,
            [(ScopeInitVariation.UnknownKeyword, ScopeInitReader.Construction, ScopeInitFit.Fits)] = AWayOfNotFitting,
            [(ScopeInitVariation.UnknownKeyword, ScopeInitReader.ConstructorReference, ScopeInitFit.Fits)] = AWayOfNotFitting,
            [(ScopeInitVariation.UnknownKeyword, ScopeInitReader.ConstructorReference, ScopeInitFit.Misfits)] = AReferenceHandsNoKeyword,
            [(ScopeInitVariation.UnknownKeyword, ScopeInitReader.SuperInit, ScopeInitFit.Fits)] = AWayOfNotFitting,
            [(ScopeInitVariation.UnknownKeyword, ScopeInitReader.Forwarding, ScopeInitFit.Fits)] = AWayOfNotFitting,
            [(ScopeInitVariation.UnknownKeyword, ScopeInitReader.Forwarding, ScopeInitFit.Misfits)] = AForwardedConstructionIsUnchecked,
        };

    // --- dataclass-member: member × read × order --- //

    /// <summary>A read of a generated member over the module's dataclass: the reader, what <c>main</c> prints, what that is
    /// when the cell runs, and the code its control carries when it does not (so the read does not fit).
    /// <paramref name="WhenCalled"/> is <see cref="InitRead"/>'s; <paramref name="NoTwin"/> is why python has no program for
    /// a read that runs, or null; <paramref name="After"/> is a declaration the reader names that stands last at both
    /// orders, below the dataclass.</summary>
    internal sealed record MemberRead(string Reader, string Shows, string? Prints = null, string? Refusal = null, bool WhenCalled = false, string? NoTwin = null, string? After = null);

    // `__eq__` and `__hash__` are redefined together: no cell rests on whether an inherited half satisfies the pairing rule.
    private const string RedefinesEqAndHash = "class D(DC):\n    def __eq__(self, other: object) -> bool:\n        return True\n    def __hash__(self) -> int:\n        return 5";
    private const string EqThroughSuper = "class D(DC):\n    def __eq__(self, other: object) -> bool:\n        return super().__eq__(other)\n    def __hash__(self) -> int:\n        return 5";
    private const string HashThroughSuper = "class D(DC):\n    def __eq__(self, other: object) -> bool:\n        return True\n    def __hash__(self) -> int:\n        return super().__hash__() - super().__hash__() + 5";
    // What both a class named `DC` and one named `D` end their text with: the generated text spells the DECLARING class
    // (`DC(v=5)`) where python spells the instance's (`D(v=5)`), which is #2804's and no order's.
    private const string ReprThroughSuper = "class D(DC):\n    @override\n    def __repr__(self) -> str:\n        return super().__repr__().split(\"(\")[1]";
    private const string AgainstAnObject = "def r() -> bool:\n    o: object = DC(5)\n    return DC(5) == o";
    // Another dunder method of a deriving class reads the generated member by name: `self.__dunder__()` and
    // `super().__dunder__()`, the two calls dunder_invocation_rules.md allows inside a dunder method. Each prints what the
    // generated text and python's agree on (the `__str__` cell of `__repr__` as the `super()` cell does: after the name).
    private const string NeThroughSelfEq = "class D(DC):\n    def __ne__(self, other: object) -> bool:\n        return not self.__eq__(other)";
    private const string NeThroughSuperEq = "class D(DC):\n    def __ne__(self, other: object) -> bool:\n        return not super().__eq__(other)";
    private const string StrThroughSelfHash = "class D(DC):\n    def __str__(self) -> str:\n        return str(self.__hash__() - self.__hash__())";
    private const string StrThroughSuperHash = "class D(DC):\n    def __str__(self) -> str:\n        return str(super().__hash__() - super().__hash__())";
    private const string StrThroughSelfRepr = "class D(DC):\n    def __str__(self) -> str:\n        return self.__repr__().split(\"(\")[1]";
    private const string StrThroughSuperRepr = "class D(DC):\n    def __str__(self) -> str:\n        return super().__repr__().split(\"(\")[1]";
    private const string RedefinesRepr = "class D(DC):\n    def __repr__(self) -> str:\n        return \"D\"";

    private static string WithOverride(string redefinition) => redefinition.Replace("\n    def ", "\n    @override\n    def ", StringComparison.Ordinal);

    private const string PythonsDataclassIsUnhashable = "python's dataclass sets `__hash__` to None unless it is frozen: `hash()` of one, and `self.__hash__()` or `super().__hash__()` in a class deriving from one, are a TypeError";

    /// <summary>The reads, total over the generated members that are no <c>__init__</c> × <see cref="ScopeMemberRead"/> with
    /// <see cref="ExcludedMemberReads"/>. The specification lets a dunder be called by name inside any dunder method, as
    /// <c>self.__other_dunder__()</c> and as <c>super().__dunder__()</c> (dunder_invocation_rules.md, its summary table). A
    /// read through <c>super()</c> in the member's own redefinition is one cell; the same member read from ANOTHER dunder
    /// method of the deriving class is two (the owner's ruling 40): <c>__ne__</c> reads <c>__eq__</c>, and <c>__str__</c>
    /// reads <c>__hash__</c> and <c>__repr__</c>. <c>__eq__</c> and <c>__hash__</c> are redefined without <c>@override</c>,
    /// which the specification makes implicit for them and python's own spelling; <c>__repr__</c> is redefined both ways,
    /// and without <c>@override</c> its cell is red with the dataclass declared first (#2802, which this plan closes:
    /// <see cref="ControlsThePlanCloses"/>).</summary>
    internal static readonly IReadOnlyDictionary<(ScopeDataclassMember Member, ScopeMemberRead Read), MemberRead> MemberReads = new Dictionary<(ScopeDataclassMember, ScopeMemberRead), MemberRead>
    {
        [(ScopeDataclassMember.Eq, ScopeMemberRead.Used)] = new("def r() -> bool:\n    return DC(5) == DC(5)", "r()", "True", WhenCalled: true),
        [(ScopeDataclassMember.Eq, ScopeMemberRead.CalledByName)] = new("def r() -> bool:\n    return DC(5).__eq__(DC(5))", "r()", Refusal: "SPY0427"),
        [(ScopeDataclassMember.Eq, ScopeMemberRead.RedefinedWithOverride)] = new(WithOverride(RedefinesEqAndHash), "D(5) == D(6)", "True"),
        [(ScopeDataclassMember.Eq, ScopeMemberRead.RedefinedWithoutOverride)] = new(RedefinesEqAndHash, "D(5) == D(6)", "True"),
        [(ScopeDataclassMember.Eq, ScopeMemberRead.CalledThroughSuper)] = new(EqThroughSuper, "D(5) == D(5)", "True"),
        // The operand's field differs, so the read prints `False` under either reading of #2804 (python compares the classes
        // first; the generated `==` compares the fields): the cell holds that the checker types the read, not which is right.
        [(ScopeDataclassMember.Eq, ScopeMemberRead.UsedAgainstADerivedOperand)] = new("def r() -> bool:\n    return DC(5) == D(6)", "r()", "False", WhenCalled: true, After: "class D(DC):\n    pass"),
        // The operand's field differs, so the read prints `False` under either reading of #2804, as python does: against an
        // equal `object` the control itself is wrong, and that form is excluded.
        [(ScopeDataclassMember.Eq, ScopeMemberRead.UsedAgainstAnUnequalObject)] = new("def r() -> bool:\n    o: object = DC(6)\n    return DC(5) == o", "r()", "False", WhenCalled: true),
        [(ScopeDataclassMember.Eq, ScopeMemberRead.CalledOnSelfInAnotherDunder)] = new(NeThroughSelfEq, "D(5) != D(5)", "False"),
        [(ScopeDataclassMember.Eq, ScopeMemberRead.CalledThroughSuperInAnotherDunder)] = new(NeThroughSuperEq, "D(5) != D(5)", "False"),
        [(ScopeDataclassMember.Hash, ScopeMemberRead.Used)] = new("def r() -> int:\n    return hash(DC(5)) - hash(DC(5)) + 5", "r()", "5", WhenCalled: true, NoTwin: PythonsDataclassIsUnhashable),
        [(ScopeDataclassMember.Hash, ScopeMemberRead.CalledByName)] = new("def r() -> int:\n    return DC(5).__hash__()", "r()", Refusal: "SPY0427"),
        [(ScopeDataclassMember.Hash, ScopeMemberRead.RedefinedWithOverride)] = new(WithOverride(RedefinesEqAndHash), "hash(D(5))", "5"),
        [(ScopeDataclassMember.Hash, ScopeMemberRead.RedefinedWithoutOverride)] = new(RedefinesEqAndHash, "hash(D(5))", "5"),
        [(ScopeDataclassMember.Hash, ScopeMemberRead.CalledThroughSuper)] = new(HashThroughSuper, "hash(D(5))", "5", NoTwin: PythonsDataclassIsUnhashable),
        [(ScopeDataclassMember.Hash, ScopeMemberRead.CalledOnSelfInAnotherDunder)] = new(StrThroughSelfHash, "str(D(5))", "0", NoTwin: PythonsDataclassIsUnhashable),
        [(ScopeDataclassMember.Hash, ScopeMemberRead.CalledThroughSuperInAnotherDunder)] = new(StrThroughSuperHash, "str(D(5))", "0", NoTwin: PythonsDataclassIsUnhashable),
        [(ScopeDataclassMember.Repr, ScopeMemberRead.Used)] = new("def r() -> str:\n    return repr(DC(5))", "r()", "DC(v=5)", WhenCalled: true),
        [(ScopeDataclassMember.Repr, ScopeMemberRead.CalledByName)] = new("def r() -> str:\n    return DC(5).__repr__()", "r()", Refusal: "SPY0427"),
        [(ScopeDataclassMember.Repr, ScopeMemberRead.RedefinedWithOverride)] = new(WithOverride(RedefinesRepr), "repr(D(5))", "D"),
        // Must run, by the specification (dunder_methods.md) and in python: red with the dataclass declared first (#2802).
        [(ScopeDataclassMember.Repr, ScopeMemberRead.RedefinedWithoutOverride)] = new(RedefinesRepr, "repr(D(5))", "D"),
        [(ScopeDataclassMember.Repr, ScopeMemberRead.CalledThroughSuper)] = new(ReprThroughSuper, "repr(D(5))", "v=5)"),
        [(ScopeDataclassMember.Repr, ScopeMemberRead.CalledThroughSuperInAnotherDunder)] = new(StrThroughSuperRepr, "str(D(5))", "v=5)"),
    };

    private const string TheInitIsItsOwnSets = "the generated `__init__` is the dataclass-init set's: its readers, fits and shapes are that set's axes";

    /// <summary>The generated members with no cell here, total over <see cref="ScopeDataclassMember"/> with the members
    /// <see cref="MemberReads"/> reads.</summary>
    internal static readonly IReadOnlyDictionary<ScopeDataclassMember, string> ExcludedDataclassMembers = new Dictionary<ScopeDataclassMember, string>
    {
        [ScopeDataclassMember.Init] = TheInitIsItsOwnSets,
    };

    private const string AnOperandIsEqualitysAlone = "an operand is `==`'s: `hash()` and `repr()` take none";
    private const string AnObjectOperandIsComparedByReference = "#2804's class: against an operand typed `object` the generated `==` is reference equality. With equal operands (`o: object = DC(5)`, `DC(5) == o`) the read prints `False` with the dataclass above the reader, where python prints `True`, and is SPY0222 with it below. That form's control is itself wrong, so the form has no cell: a cell does what its control does, and no row may pin `False` as right. The form with an unequal operand is the cell beside it";
    private const string AStrThatReadsItsOwnReprCallsItself = "#2057's class (its comment 7188927977): `__str__` and `__repr__` are one `ToString`, so a `__str__` that calls `self.__repr__()` calls itself. Over a dataclass declared first the program compiles and dies at run time of a stack overflow, where python prints the generated text; with the dataclass below it is SPY0203. The control is itself wrong, whatever the class derives from, so the read has no cell";

    /// <summary>The member × read pairs with no cell, total with <see cref="MemberReads"/> over the members it reads ×
    /// <see cref="ScopeMemberRead"/>.</summary>
    internal static readonly IReadOnlyDictionary<(ScopeDataclassMember Member, ScopeMemberRead Read), string> ExcludedMemberReads = new Dictionary<(ScopeDataclassMember, ScopeMemberRead), string>
    {
        [(ScopeDataclassMember.Eq, ScopeMemberRead.UsedAgainstAnEqualObject)] = AnObjectOperandIsComparedByReference,
        [(ScopeDataclassMember.Repr, ScopeMemberRead.CalledOnSelfInAnotherDunder)] = AStrThatReadsItsOwnReprCallsItself,
        [(ScopeDataclassMember.Hash, ScopeMemberRead.UsedAgainstADerivedOperand)] = AnOperandIsEqualitysAlone,
        [(ScopeDataclassMember.Hash, ScopeMemberRead.UsedAgainstAnEqualObject)] = AnOperandIsEqualitysAlone,
        [(ScopeDataclassMember.Hash, ScopeMemberRead.UsedAgainstAnUnequalObject)] = AnOperandIsEqualitysAlone,
        [(ScopeDataclassMember.Repr, ScopeMemberRead.UsedAgainstADerivedOperand)] = AnOperandIsEqualitysAlone,
        [(ScopeDataclassMember.Repr, ScopeMemberRead.UsedAgainstAnEqualObject)] = AnOperandIsEqualitysAlone,
        [(ScopeDataclassMember.Repr, ScopeMemberRead.UsedAgainstAnUnequalObject)] = AnOperandIsEqualitysAlone,
    };

    /// <summary>The names <c>DataclassSynthesis.SynthesizeMembers</c> looks for among the class's own methods before it adds
    /// a member (<c>explicitMethods.Contains(DunderNames.X)</c>), read from its source: what <c>@dataclass</c> generates.</summary>
    internal static IReadOnlyList<string> SynthesizedMemberNames()
    {
        var path = Path.Combine(Sharpy.Compiler.Tests.Infrastructure.DispatchSiteScan.FindRepoRoot(), "src", "Sharpy.Compiler", "Semantic", "DataclassSynthesis.cs");
        return Regex.Matches(File.ReadAllText(path), @"explicitMethods\.Contains\(DunderNames\.(\w+)\)").Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Whether a reader's text names the type (<paramref name="names"/>) in a function's body alone: python then reads
    /// the name when the function is called, and anywhere else as the module loads. Read from the text, so that it says what
    /// <see cref="InitRead.WhenCalled"/> says without being it. A parameter's annotation is no read: the twin has none.</summary>
    internal static bool InABodyAlone(string reader, params string[] names)
    {
        var lines = reader.Split('\n');
        static int Indent(string line) => line.Length - line.TrimStart().Length;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!names.Any(n => Regex.IsMatch(lines[i], $@"(?<![\w.]){Regex.Escape(n)}(?!\w)")) || lines[i].TrimStart().StartsWith("def ", StringComparison.Ordinal))
                continue;
            bool inBody = false;
            for (int j = i - 1, indent = Indent(lines[i]); j >= 0 && indent > 0; j--)
            {
                if (Indent(lines[j]) >= indent)
                    continue;
                indent = Indent(lines[j]);
                inBody |= lines[j].TrimStart().StartsWith("def ", StringComparison.Ordinal);
            }
            if (!inBody)
                return false;
        }
        return true;
    }

    private const string DataclassField = "@dataclass\nclass DC:\n    v: int\n";
    private const string PlainField = "class DC:\n    v: int = 0\n    def __init__(self, v: int):\n        self.v = v\n";

    private static BehaviourProbe UncheckedConstruction(string reader, string construction, bool above)
        => new($"{(above ? DataclassField + reader : reader + DataclassField)}def main() -> None:\n    print({construction})\n", "SPY0908 CS7036", r => Refused(r, "SPY0908", "CS7036"));

    private static string OverABase(string baseClass, string reader, bool above, string shows)
    {
        string dataclass = baseClass + "@dataclass\nclass DC(G):\n    v: int\n";
        return $"def mk(f: (int) -> DC) -> int:\n    return f(5).v\n{(above ? dataclass + reader : reader + dataclass)}def main() -> None:\n    print({shows})\n";
    }

    private const string GenericDataclass = "@dataclass\nclass DC[T]:\n    v: T\n";
    private const string GenericHelper = "def mk(f: (int) -> DC[int]) -> int:\n    return f(5).v\n";
    private const string DeepHelper = "def mk(f: (int) -> O.P.DC) -> int:\n    return f(5).v\n";

    /// <summary>A class nested two deep, <c>O.P.DC</c>, with its decorator and body.</summary>
    private static string DeepClass(string decorator, string body)
        => $"class O:\n    class P:\n{Indent(Indent(decorator + "class DC:\n" + Indent(body)))}\n";

    /// <summary>A class <c>H</c> holding a nested <c>DC</c> and a method that reads it by its bare name, above the method or
    /// below it; <c>main</c> prints the method's value.</summary>
    private static string Enclosed(string decorator, string body, string reader, bool above)
    {
        string nested = Indent(decorator + "class DC:\n" + Indent(body));
        string member = Indent(reader);
        return $"class H:\n{(above ? nested + "\n" + member : member + "\n" + nested)}\ndef mk(f: (int) -> H.DC) -> int:\n    return f(5).v\ndef main() -> None:\n    print(H().r())\n";
    }

    private const string DerivedCallsSuper = "class D(DC):\n    def __init__(self):\n        super().__init__(5)\n";
    private const string DerivedCallsSuperWithNothing = "class D(DC):\n    def __init__(self):\n        super().__init__()\n";

    /// <summary>A program of two declarations and what <c>main</c> prints: the class (and what stands with it) above the reader,
    /// or below it.</summary>
    private static string Ordered(string dataclass, string reader, bool above, string shows, string helper = "")
        => $"{(above ? dataclass + reader : reader + dataclass)}{helper}def main() -> None:\n    print({shows})\n";

    /// <summary>The generated C# does not compile: SPY0908, under one of <paramref name="cs"/> (C# reports two errors for some
    /// programs, and which the diagnostic names first is its own business).</summary>
    private static BehaviourProbe Crashes(string source, params string[] cs)
        => new(source, $"SPY0908 {string.Join(" or ", cs)}", r => r.ErrorCodes.Contains("SPY0908") && cs.Contains(r.CsCode));

    private static BehaviourProbe Refuses(string source, string code) => new(source, code, r => Refused(r, code));

    // ------------------------------------------------------------------------------------------- //
    // lambda-default: owner × kind × annotation × position × order × outer
    // ------------------------------------------------------------------------------------------- //

    /// <summary>An owner of a parameter default: whether it is a lambda and which spelling (<c>LambdaExpression.IsArrowSyntax</c>;
    /// null for the <c>def</c>), and the expression that stands at the position, <c>{d}</c> the default. A <c>def</c> IS its
    /// position (<see cref="ReadPosition.ParameterDefault"/>), so its text is the default alone.</summary>
    internal sealed record DefaultOwner(bool? Arrow, string Text);

    /// <summary>The owners, total over <see cref="ScopeDefaultOwner"/>; the lambdas are total over <c>IsArrowSyntax</c>, and
    /// <see cref="AxisProblems"/> reads the flag back from each cell's parsed source.</summary>
    internal static readonly IReadOnlyDictionary<ScopeDefaultOwner, DefaultOwner> DefaultOwners = new Dictionary<ScopeDefaultOwner, DefaultOwner>
    {
        [ScopeDefaultOwner.Def] = new(null, "{d}"),
        [ScopeDefaultOwner.Lambda] = new(false, "(lambda p1: int = {d}: p1)()"),
        [ScopeDefaultOwner.ArrowLambda] = new(true, "((p1: int = {d}) -> p1)()"),
    };

    private const string AStoredLambdaLosesItsDefault = "stored in a field and called later (`g: auto = lambda p1: int = 5: p1`, then `H.g()`), a lambda has lost its default: the field's type is a function type, which carries none, and the call is SPY0908 CS7036 with a LITERAL default, so no cell could show what the default read";

    /// <summary>How the lambda of a cell is used, closed: called where it stands, or stored and called later. The cell prints the
    /// default's value, which only the first shows today.</summary>
    internal static readonly IReadOnlyDictionary<string, string> ExcludedLambdaUses = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["stored"] = AStoredLambdaLosesItsDefault,
    };

    /// <summary>The kinds a default reads by a bare name from its own class: READ from the typed-read set's readers, as the kinds
    /// the P77a pool runs from a <c>def</c>'s default there. A lambda's default is a constant position like a <c>def</c>'s, so a
    /// kind that is no constant has no cell that must run (its verdict there is the constant-kind set's).</summary>
    internal static IReadOnlyList<BindingKind> LambdaDefaultKinds
        => TypedReaders.Where(r => r is { Spelling: Harness.Spelling.Bare, Inside: true, Position: ReadPosition.ParameterDefault }).Select(r => r.Kind).Distinct().ToList();

    private const string ACallInAConstantOnlyPosition = "takes a compile-time constant, and a lambda called where it stands is a call: refused by its shape (SPY0401) before any name in it is read";

    private static string ALambdaDefaultInAPropertyDefault(bool isStatic)
        => $"in {(isStatic ? "a `@static`" : "an instance")} property's default, a lambda that gives a parameter a default is SPY0908 CS0103 with a LITERAL default (the emitted `__lambda_0` is declared nowhere): no cell there could run, whatever its default reads, and the defect is no name's verdict";

    /// <summary>The positions of <see cref="LambdaDefaultPositions"/>'s source that hold no lambda, each reason with its probe.</summary>
    internal static readonly IReadOnlyDictionary<ReadPosition, string> ExcludedLambdaDefaultPositions = new Dictionary<ReadPosition, string>
    {
        [ReadPosition.ParameterDefault] = ACallInAConstantOnlyPosition,
        [ReadPosition.DataclassFieldInitializer] = ACallInAConstantOnlyPosition,
        [ReadPosition.StructFieldInitializer] = ACallInAConstantOnlyPosition,
        [ReadPosition.StaticPropertyDefault] = ALambdaDefaultInAPropertyDefault(true),
        [ReadPosition.InstancePropertyDefault] = ALambdaDefaultInAPropertyDefault(false),
    };

    /// <summary>The mark of a control cell: the owner's slot at a position with a literal default.</summary>
    internal const string LiteralDefault = "literal-default";

    /// <summary>The positions of a kind's bare read from its own type with nothing between the read and the type body, as the
    /// P77a pool has them (<see cref="TypedReaders"/>): the class-level positions, since a bare read of a member runs nowhere else (N1).</summary>
    internal static IEnumerable<ReadPosition> BareReadPositions(BindingKind kind)
        => TypedReaders.Where(r => r.Kind == kind && r is { Spelling: Harness.Spelling.Bare, Inside: true, Nesting: Harness.Nesting.Direct }).Select(r => r.Position).Distinct().OrderBy(p => p);

    /// <summary>Where a lambda whose default reads a member of the kind stands: every class-level initializer the kind is read
    /// from bare, so the axis is the image of <see cref="ReadPosition"/> under the pool's own rosters.</summary>
    internal static IEnumerable<ReadPosition> LambdaDefaultPositions(BindingKind kind)
        => BareReadPositions(kind).Where(p => !ExcludedLambdaDefaultPositions.ContainsKey(p));

    /// <summary>The lambdas of a source that give a parameter a default, by the compiler's own parser: each one's <c>IsArrowSyntax</c>.</summary>
    internal static List<bool> LambdasWithADefault(string source)
    {
        static IEnumerable<Sharpy.Compiler.Parser.Ast.Node> Descendants(Sharpy.Compiler.Parser.Ast.Node node) => node.GetChildNodes().SelectMany(Descendants).Prepend(node);
        var module = new Sharpy.Compiler.Parser.Parser(new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll()).ParseModule();
        return Descendants(module).OfType<Sharpy.Compiler.Parser.Ast.LambdaExpression>().Where(l => l.Parameters.Any(p => p.DefaultValue != null)).Select(l => l.IsArrowSyntax).ToList();
    }

    // ------------------------------------------------------------------------------------------- //
    // alias-target: site × shape × target × use × order
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A site an alias is declared at: the program around <c>{alias}</c> and <c>{use}</c>, the declaration that names the
    /// alias <c>{K}</c> there, and what stands in its place when nothing does.</summary>
    internal sealed record AliasSite(string Label, string Program, string Use, string NoUse);

    /// <summary>The sites, total over <c>ScopeKind</c> with <see cref="ExcludedAliasScopes"/>: the scope the alias's binder declares
    /// it into (<c>ResolveTypeAliasDeclaration</c> at module level and in a type body, <c>RegisterScopedTypeAlias</c> in a type
    /// body, a function and a block). A type body is refined by <see cref="AliasTypeHosts"/>.</summary>
    internal static readonly IReadOnlyDictionary<ScopeKind, AliasSite[]> AliasSites = new Dictionary<ScopeKind, AliasSite[]>
    {
        [ScopeKind.Module] = new[] { new AliasSite("module", "{alias}\n{use}", "def f(k: {K}) -> int:\n    return 1", "def f(k: int) -> int:\n    return 1") },
        [ScopeKind.TypeBody] = Array.Empty<AliasSite>(),
        [ScopeKind.FunctionLike] = new[]
        {
            new AliasSite("function", "def g() -> int:\n    {alias}\n{use}\n    return 1", "    def f(k: {K}) -> int:\n        return 1", "    def f(k: int) -> int:\n        return 1"),
            new AliasSite("method", "class H:\n    def g(self) -> int:\n        {alias}\n{use}\n        return 1", "        def f(k: {K}) -> int:\n            return 1", "        def f(k: int) -> int:\n            return 1"),
        },
        [ScopeKind.Block] = new[]
        {
            new AliasSite("block", "def g() -> int:\n    if True:\n        {alias}\n{use}\n    return 1", "        def f(k: {K}) -> int:\n            return 1", "        def f(k: int) -> int:\n            return 1"),
        },
    };

    internal static readonly IReadOnlyDictionary<ScopeKind, string> ExcludedAliasScopes = new Dictionary<ScopeKind, string>
    {
        [ScopeKind.InitializerHost] = NotADeclarationScope,
        [ScopeKind.Transparent] = HoldsTypeParameters,
    };

    /// <summary>The type bodies an alias is declared in, total over <c>TypeKind</c> with <see cref="ExcludedAliasTypeHosts"/>.</summary>
    internal static readonly IReadOnlyDictionary<TypeKind, AliasSite[]> AliasTypeHosts = new Dictionary<TypeKind, AliasSite[]>
    {
        [TypeKind.Class] = new[] { new AliasSite("class", "class H:\n    {alias}\n{use}", "    def f(self, k: {K}) -> int:\n        return 1", "    def f(self, k: int) -> int:\n        return 1") },
        [TypeKind.Struct] = new[] { new AliasSite("struct", "struct H:\n    {alias}\n    v: int = 1\n{use}", "    def f(self, k: {K}) -> int:\n        return 1", "    def f(self, k: int) -> int:\n        return 1") },
        [TypeKind.Interface] = new[] { new AliasSite("interface", "interface H:\n    {alias}\n{use}", "    def f(self, k: {K}) -> int: ...", "    def f(self, k: int) -> int: ...") },
    };

    internal static readonly IReadOnlyDictionary<TypeKind, string> ExcludedAliasTypeHosts = new Dictionary<TypeKind, string>
    {
        [TypeKind.Enum] = EnumBodyHoldsCases,
        [TypeKind.Union] = UnionBodyHoldsCases,
        [TypeKind.Delegate] = DelegateHasNoBody,
    };

    /// <summary>A target's shape: where in it the type's name <c>{N}</c> sits, whether the alias is generic, what it imports, and the
    /// name of a type that exists there (<c>C0</c>, the program's own class, unless the shape qualifies the name).</summary>
    internal sealed record AliasShape(string Label, string Target, bool Generic = false, string? Import = null, string Declared = "C0");

    private static readonly AliasShape NameShape = new("name", "{N}");

    /// <summary>The shapes, total over the public properties the alias's three AST records declare
    /// (<c>TypeAlias</c>, <c>TypeAnnotation</c>, <c>FunctionType</c>) with <see cref="ExcludedAliasShapeProperties"/>: each property
    /// that can hold, wrap or qualify a type's name is a shape, keyed <c>Record.Property</c>.</summary>
    internal static readonly IReadOnlyDictionary<string, AliasShape> AliasShapes = new Dictionary<string, AliasShape>(StringComparer.Ordinal)
    {
        [$"TypeAlias.{nameof(AstTypeAlias.Type)}"] = NameShape,
        [$"TypeAlias.{nameof(AstTypeAlias.TypeParameters)}"] = new("generic-alias", "list[{N}]", Generic: true),
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.Name)}"] = NameShape,
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.NameParts)}"] = new("dotted", "pathlib.{N}", Import: "pathlib", Declared: "Path"),
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.TypeArguments)}"] = new("type-argument", "list[{N}]"),
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.IsOptional)}"] = new("optional", "{N}?"),
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.IsCSharpNullable)}"] = new("nullable", "{N} | None"),
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.ErrorType)}"] = new("result-error", "int !{N}"),
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.TupleElementNames)}"] = new("tuple-element", "tuple[a: {N}, b: int]"),
        [$"FunctionType.{nameof(AstFunctionType.ParameterTypes)}"] = new("callable-parameter", "({N}) -> int"),
        [$"FunctionType.{nameof(AstFunctionType.ReturnType)}"] = new("callable-return", "() -> {N}"),
    };

    private const string TheAliasItself = "the alias's own name and where it stands: not its target";
    private const string AnEscapedNameIsMissingAsItsPlainOne = "the escape picks the namespace a spelling is looked up in (a type of the program over a builtin of the same spelling): a missing name is missing in both, so an escaped target has its plain one's verdict (named by an annotation, ``type K0 = `nosuch` `` is SPY0202 as `type K0 = nosuch` is)";
    private const string SourceLocation = "a source location";
    private const string UnparserFact = "a syntax fact for the unparser and the structural comparer (the record's own remark): resolution does not read it";

    internal static readonly IReadOnlyDictionary<string, string> ExcludedAliasShapeProperties = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [$"TypeAlias.{nameof(AstTypeAlias.Name)}"] = TheAliasItself,
        [$"TypeAlias.{nameof(AstTypeAlias.NameLineStart)}"] = TheAliasItself,
        [$"TypeAlias.{nameof(AstTypeAlias.NameColumnStart)}"] = TheAliasItself,
        [$"TypeAlias.{nameof(AstTypeAlias.NameColumnEnd)}"] = TheAliasItself,
        [$"TypeAlias.{nameof(AstTypeAlias.IsNameBacktickEscaped)}"] = TheAliasItself,
        [$"TypeAlias.{nameof(AstTypeAlias.FunctionType)}"] = "refined by FunctionType's own properties",
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.IsNameBacktickEscaped)}"] = AnEscapedNameIsMissingAsItsPlainOne,
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.BacktickEscapedParts)}"] = UnparserFact,
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.IsResult)}"] = "computed from ErrorType, which is a shape",
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.TupleElementNamesBacktickEscaped)}"] = UnparserFact,
        [$"FunctionType.{nameof(AstFunctionType.LineStart)}"] = SourceLocation,
        [$"FunctionType.{nameof(AstFunctionType.ColumnStart)}"] = SourceLocation,
        [$"FunctionType.{nameof(AstFunctionType.LineEnd)}"] = SourceLocation,
        [$"FunctionType.{nameof(AstFunctionType.ColumnEnd)}"] = SourceLocation,
        [$"FunctionType.{nameof(AstFunctionType.Span)}"] = SourceLocation,
    };

    private static IEnumerable<string> DeclaredProperties(Type record)
        => record.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(p => $"{record.Name}.{p.Name}");

    // ------------------------------------------------------------------------------------------- //
    // The axes against their enumerations
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The projected value a <c>dataclass-init</c> cell carries: the part of its key that is a value of a projected
    /// axis, or the label of a projected site; null for a cell of the matrix or of a placement.</summary>
    internal static object? ProjectionOf(CellKey key)
    {
        foreach (var part in key.Parts)
        {
            if (part is ScopeDataclassBase or ScopeFieldDefault or ScopeReferenceSlot or ScopeInitThrough or ScopeDataclassName)
                return part;
        }
        return AllDataclassSites().Where(s => s.Projected).Select(s => s.Label).FirstOrDefault(key.Marks.Contains);
    }

    /// <summary>Each axis against its enumeration: a value is generated or carries its reason, never both, never neither; a
    /// generated value has a cell; and an axis read from the pool or a census is the image it says it is.</summary>
    internal static List<string> AxisProblems(IReadOnlyList<Case> pool)
    {
        var problems = new List<string>();

        void Partition<T>(string axis, IEnumerable<T> universe, IEnumerable<T> generated, IEnumerable<T> excluded) where T : notnull
        {
            var g = generated.ToHashSet();
            var e = excluded.ToHashSet();
            var u = universe.ToHashSet();
            foreach (var value in u.OrderBy(v => v.ToString(), StringComparer.Ordinal))
            {
                if (g.Contains(value) == e.Contains(value))
                    problems.Add($"{axis}: {value} is {(g.Contains(value) ? "generated and excluded" : "neither generated nor excluded")}");
            }
            foreach (var value in g.Concat(e).Where(v => !u.Contains(v)).Distinct())
                problems.Add($"{axis}: {value} is no value of the enumeration");
        }

        IEnumerable<Case> Route(string route) => pool.Where(c => c.Cell.Key!.Route == route);

        void Carried<T>(string route, IEnumerable<T> owed, Func<Case, IEnumerable<T>> read) where T : notnull
        {
            var carried = Route(route).SelectMany(read).ToHashSet();
            foreach (var value in owed.Where(v => !carried.Contains(v)))
                problems.Add($"{route}: no cell carries {value}");
        }

        IEnumerable<T> Part<T>(Case c) where T : struct, Enum => c.Cell.Key!.Part<T>() is { } v ? new[] { v } : Array.Empty<T>();
        IEnumerable<string> Marks(Case c) => c.Cell.Key!.Marks;

        // no-initializer
        Partition("no-initializer × kind", Enum.GetValues<BindingKind>(), Declarations.Keys, ExcludedDeclarations.Keys);
        Partition("no-initializer × host", Enum.GetValues<TypeKind>(), DeclarationHosts.Keys, ExcludedDeclarationHosts.Keys);
        Partition("no-initializer × declared type", typeof(SemanticType).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(SemanticType)) && !t.IsAbstract),
            ValueTypes.Keys, ExcludedValueTypes.Keys);
        Carried("no-initializer", Declarations.Keys, c => new[] { c.Cell.Target });
        Carried("no-initializer", Enum.GetValues<ScopeInitializer>(), Part<ScopeInitializer>);
        // The host is read from its typed part and the declared type from its label: two axes, so a pool that loses every cell of
        // one host, or of one declared type, is red. Then the product: every kind a type declares, in every host, with every type.
        Carried("no-initializer", DeclarationHosts.Values.SelectMany(h => h), c => HostOf(c.Cell) is { } host ? new[] { host } : Array.Empty<DeclarationHost>());
        var typeLabels = ValueTypes.Values.SelectMany(v => v).Select(v => v.Label).ToList();
        Carried("no-initializer", typeLabels, Marks);
        foreach (var misplaced in DeclarationHosts.Where(h => h.Value.Any(host => host.Kind != h.Key)).Select(h => h.Key))
            problems.Add($"no-initializer × host: a host listed under {misplaced} carries another kind");
        var declared = Route("no-initializer").Select(c => (c.Cell.Target, Host: HostOf(c.Cell), Marks: Marks(c).ToHashSet(StringComparer.Ordinal))).ToList();
        foreach (var kind in Declarations.Where(d => d.Value.InType).Select(d => d.Key))
        {
            foreach (var host in DeclarationHosts.Values.SelectMany(h => h))
            {
                foreach (var label in typeLabels.Where(l => !declared.Any(d => d.Target == kind && d.Host == host && d.Marks.Contains(l))))
                    problems.Add($"no-initializer: no cell declares {kind} of type {label} in a {host}");
            }
        }

        // typed-read
        Partition("typed-read × kind", Enum.GetValues<BindingKind>(), TypedReads.Keys, ExcludedTypedReads.Keys);
        Partition("typed-read × spelling", Enum.GetValues<Harness.Spelling>(), Route("typed-read").SelectMany(Part<Harness.Spelling>), ExcludedTypedSpellings.Keys);
        Partition("typed-read × reader position", Enum.GetValues<ReadPosition>(), Route("typed-read").Select(c => c.Cell.Position!.Value), ExcludedTypedPositions.Keys);
        Carried("typed-read", TypedReads.Keys, c => new[] { c.Cell.Target });
        Carried("typed-read", Enum.GetValues<ScopeAnnotation>(), Part<ScopeAnnotation>);
        foreach (var unplaced in UnplacedTypedReaders.Take(10))
            problems.Add($"typed-read: the 8951 pool spells a read this set has no reader host for: {unplaced}");
        foreach (var placement in TypedThirdPlacements.Keys.Where(p => !ExcludedTypedPlacements.ContainsKey(p)))
            problems.Add($"typed-read × reader placement: {placement} is neither generated nor excluded, and the 8951 pool spells {TypedThirdPlacements[placement]} must-run reads from it");
        // The other direction: an exclusion says the pool reads from the placement, so one the pool no longer spells holds nothing.
        foreach (var stale in ExcludedTypedPlacements.Keys.Where(p => !TypedThirdPlacements.ContainsKey(p)))
            problems.Add($"typed-read × reader placement: {stale} is excluded as a placement the 8951 pool reads from, and that pool spells no must-run read from it");
        foreach (var label in TypedLabelledReads.Keys.Where(l => !ExcludedTypedReceivers.ContainsKey(l)))
            problems.Add($"typed-read × receiver: the label '{label}' is neither generated nor excluded, and the 8951 pool spells {TypedLabelledReads[label]} must-run reads under it");
        foreach (var stale in ExcludedTypedReceivers.Keys.Where(l => !TypedLabelledReads.ContainsKey(l)))
            problems.Add($"typed-read × receiver: the label '{stale}' is excluded as one the 8951 pool reads under, and that pool spells no must-run read under it");
        foreach (var (route, excluded) in ExcludedOrders)
            Partition($"{route} × order", Enum.GetValues<Harness.Order>(), Route(route).SelectMany(Part<Harness.Order>), excluded.Keys);
        foreach (var later in Route("typed-read").Where(c => c.Cell.Key!.Part<Harness.Order>() == Harness.Order.Later))
        {
            if (!pool.Any(c => c.Cell.Id == EarlierTwinId(later.Cell)))
                problems.Add($"typed-read: {later.Cell.Id} has no twin at the earlier order, which is what says whether its red is the order's");
        }

        // callee
        Partition("callee × binder", Enum.GetValues<LocalTarget>(), CalleeBinders.Keys, ExcludedCalleeBinders.Keys);
        Partition("callee × outer", Enum.GetValues<BindingKind>(), CalleeOuters.Keys, ExcludedCalleeOuters.Keys);
        Carried("callee", CalleeBinders.Keys, c => c.Cell.Construct is { } t ? new[] { t } : Array.Empty<LocalTarget>());
        Carried("callee", CalleeOuters.Keys, c => new[] { c.Cell.Outer });
        Carried("callee", new[] { BindingKind.Parameter }, c => new[] { c.Cell.Target });

        // constant-kind: the readers, their hosts and their tables
        var readers = ClassifierReaders.Value;
        problems.AddRange(readers.Problems.Select(p => $"constant-kind × reader: {p}"));
        Partition("constant-kind × reader", readers.Sites.Keys, ConstantReaders.Keys, ExcludedConstantReaders.Keys);
        foreach (var hostless in ConstantReaders.Where(r => r.Value.Length == 0))
            problems.Add($"constant-kind × reader: {hostless.Key} has no host");
        var tables = ConstantReaders.Keys.Where(readers.Sites.ContainsKey).Select(k => readers.Sites[k]).Distinct().ToList();
        Partition("constant-kind × table", Enum.GetValues<AdmissionTable>(), tables, ExcludedAdmissionTables.Keys);
        Partition("constant-kind × const site", Enum.GetValues<ScopeKind>(), ConstSites.Keys, ExcludedConstScopes.Keys);
        Partition("constant-kind × const type body", Enum.GetValues<TypeKind>(), ConstTypeHosts.Keys, ExcludedConstTypeHosts.Keys);
        foreach (var empty in ConstSites.Where(e => e.Key != ScopeKind.TypeBody && e.Value.Length == 0).Select(e => e.Key.ToString())
            .Concat(ConstTypeHosts.Where(e => e.Value.Length == 0).Select(e => e.Key.ToString())))
            problems.Add($"constant-kind × const site: {empty} is listed with no host");
        var hostLabels = AllConstantHosts().Select(h => h.Host.Label).ToList();
        foreach (var twice in hostLabels.GroupBy(l => l, StringComparer.Ordinal).Where(g => g.Count() > 1))
            problems.Add($"constant-kind × host: {twice.Key} is the label of {twice.Count()} hosts");
        Carried("constant-kind", tables, Part<AdmissionTable>);
        Carried("constant-kind", hostLabels, Marks);
        Carried("constant-kind", Enum.GetValues<ScopeConstantKind>(), Part<ScopeConstantKind>);

        // constant-kind: the member kinds, each against what the compiler records for the member
        var census = StdlibModuleMembers.Value;
        Partition("constant-kind × module member collection", typeof(ModuleOverloads).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name),
            ModuleMemberShapes.Keys.Concat(ExcludedModuleMemberShapes.Keys).Select(k => k.Split('/')[0]).Distinct(), ExcludedModuleCollections.Keys);
        Partition("constant-kind × module member", census.Values.Distinct().Concat(UnproducedModuleMemberShapes), ModuleMemberShapes.Keys, ExcludedModuleMemberShapes.Keys);
        foreach (var shape in UnproducedModuleMemberShapes.Where(s => census.Values.Contains(s)))
            problems.Add($"constant-kind × module member: {census.First(v => v.Value == shape).Key} is {shape}, which is listed as a shape no member has");
        foreach (var kind in ModuleMemberShapes.Values.Where(k => !ModuleMembers.ContainsKey(k)))
            problems.Add($"constant-kind × module member: {kind} has no member to read");
        foreach (var kind in ClrTypeMemberShapes.Values.Where(k => !TypeMembers.ContainsKey(k)))
            problems.Add($"constant-kind × type member: {kind} has no member to read");
        Partition("constant-kind × type member",
            typeof(ClrMemberResolution).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Where(t => t.IsSubclassOf(typeof(ClrMemberResolution)))
                .SelectMany(t => RefinedClrTypeMemberShapes.GetValueOrDefault(t.Name, new[] { t.Name })),
            ClrTypeMemberShapes.Keys, ExcludedClrTypeMemberShapes.Keys);
        Partition("constant-kind × enum access", Enum.GetValues<EnumMemberAccessKind>(), EnumReadKinds.Values.Where(v => v.Access != null).Select(v => v.Access!.Value), Array.Empty<EnumMemberAccessKind>());
        foreach (var intShape in new[] { true, false }.Where(isInt => !EnumReadKinds.Values.Contains((isInt, (EnumMemberAccessKind?)null))))
            problems.Add($"constant-kind × enum access: the member of {(intShape ? "an int" : "a string")} enum has no kind");
        Partition("constant-kind × kind", Enum.GetValues<ScopeConstantKind>(),
            ModuleMemberShapes.Values.Concat(ClrTypeMemberShapes.Values).Concat(EnumReadKinds.Keys).Append(ScopeConstantKind.OwnMember), ChainKinds);
        var ownKinds = TypedReads.Keys.Append(BindingKind.Builtin).Append(BindingKind.ModuleEnum).Append(BindingKind.ImportedName).Distinct().ToList();
        Partition("constant-kind × own name", Enum.GetValues<BindingKind>(), ownKinds, ExcludedOwnReads.Keys);
        Carried("constant-kind", ownKinds, c => new[] { c.Cell.Target });
        Carried("constant-kind", Enum.GetValues<ScopeAnnotation>(), Part<ScopeAnnotation>);
        Partition("constant-kind × read form", Enum.GetValues<Harness.Form>(), Route("constant-kind").SelectMany(Part<Harness.Form>), ExcludedConstantForms.Keys);

        // constant-kind: the spelling of the read and the placement of its reader, per where the member lives
        var constantCells = Route("constant-kind").ToList();
        IEnumerable<Case> Own() => constantCells.Where(c => c.Cell.Key!.Part<ScopeConstantKind>() == ScopeConstantKind.OwnMember);
        Partition("constant-kind × own spelling", Enum.GetValues<Harness.Spelling>(), Own().SelectMany(Part<Harness.Spelling>), ExcludedOwnSpellings.Keys);
        Partition("constant-kind × own placement", Enum.GetValues<DeclaringRelation>(), Own().SelectMany(Part<DeclaringRelation>), ExcludedOwnPlacements.Keys);
        foreach (var (_, host) in AllConstantHosts())
        {
            if ((host.Inside == null) == (host.NoInside == null))
                problems.Add($"constant-kind × host: {host.Label} {(host.Inside == null ? "has no slot in the member's own type and no reason" : "has such a slot and a reason against it")}");
            if (host.InMethod && host.Inside == null)
                problems.Add($"constant-kind × host: {host.Label} says its slot sits in a method and names none");
            // Every spelling and placement the typed-read set has a reader for, of every kind: a cell at this host, or the reason there is none.
            foreach (var (kind, t) in TypedReads)
            {
                string slot = t.Member.EndsWith("()", StringComparison.Ordinal) ? "() -> int" : "int";
                foreach (var (spelling, inside) in OwnSpellings(kind))
                {
                    bool carried = Own().Any(c => c.Cell.Target == kind && Marks(c).Contains(host.Label) && c.Cell.Key!.Part<Harness.Spelling>() == spelling
                        && (c.Cell.Key.Part<DeclaringRelation>() == DeclaringRelation.Self) == inside);
                    bool skipped = (OwnSpellingSkips(host, spelling, inside) ?? OwnKindSkips(host, kind, inside) ?? HostSkips(host, slot)) != null;
                    if (carried == skipped)
                        problems.Add($"constant-kind × own spelling: {kind} read {spelling} {(inside ? "in its own type" : "from outside its type")} at {host.Label} is {(carried ? "generated and excluded" : "neither generated nor excluded")}");
                }
            }
        }
        foreach (var kind in TypedReads.Keys.Where(k => OwnSpellings(k).Count == 0))
            problems.Add($"constant-kind × own spelling: the typed-read set has no reader of {kind}, so no spelling is owed for it");
        // A bare sibling read has the class-body rule's two axes: both orders, with and without a module binding of the name.
        foreach (var sibling in Own().Where(c => Marks(c).Contains(BareSibling)).GroupBy(c => string.Join("/", c.Cell.Key!.Parts.Where(p => p is not Harness.Order && !(p is string text && text.StartsWith("outer=", StringComparison.Ordinal))))))
        {
            var seen = sibling.Select(c => (c.Cell.Key!.Part<Harness.Order>(), c.Cell.Outer != BindingKind.None)).ToHashSet();
            foreach (var order in new Harness.Order?[] { Harness.Order.Earlier, Harness.Order.Later })
            {
                foreach (var bound in new[] { false, true }.Where(b => !seen.Contains((order, b))))
                    problems.Add($"constant-kind × sibling: {sibling.Key} has no cell at {order} {(bound ? "under" : "without")} a module binding of the name");
            }
        }
        Partition("constant-kind × import form", Enum.GetValues<ImportBindingForm>(), ImportSpellings.Select(i => i.Form), Array.Empty<ImportBindingForm>());
        Partition("constant-kind × import kind", Enum.GetValues<ImportKind>(), ImportSpellings.Select(i => i.Kind), ExcludedImportKinds.Keys);
        Partition("constant-kind × star import", new[] { false, true }, ImportSpellings.Where(i => i.Form == ImportBindingForm.FromImport).Select(i => i.Star), Array.Empty<bool>());
        Partition("constant-kind × enum spelling", Enum.GetValues<Harness.Spelling>(), EnumSpellings.Keys, ExcludedEnumSpellings.Keys);
        // Every member is read under every spelling it has: a spelling with no cell for one member is a hand list again.
        IEnumerable<string> Owed(ScopeConstantKind kind)
            => ModuleMembers.ContainsKey(kind) ? ImportSpellings.Select(i => i.Label)
                : TypeMembers.ContainsKey(kind) ? ImportSpellings.Select(i => i.Label).Append(TypeAliasSpelling)
                : EnumReadKinds.ContainsKey(kind) ? EnumSpellings.Where(v => EnumReadSkips(v.Key, kind) == null).Select(v => v.Value.Label)
                : Array.Empty<string>();
        foreach (var kind in EnumReadKinds.Keys)
        {
            foreach (var (spelling, named) in EnumSpellings.Where(s => EnumReadSkips(s.Key, kind) != null))
            {
                if (constantCells.Any(c => c.Cell.Key!.Part<ScopeConstantKind>() == kind && Marks(c).Contains(named.Label)))
                    problems.Add($"constant-kind × enum spelling: {kind} under {spelling} is generated and excluded");
            }
        }
        // A type the primitive catalog names is read by that name too: per member, the spelling or the reason the name is no receiver.
        bool ReadByItsBuiltinName(Type? owner) => owner != null && PrimitiveCatalog.GetByClrType(owner) != null && !ExcludedBuiltinReceivers.ContainsKey(owner);
        var typeMembers = TypeMembers.Values.SelectMany(v => v).ToList();
        Partition("constant-kind × builtin name", typeMembers.Where(m => PrimitiveCatalog.GetByClrType(m.Owner) != null).Select(m => $"{m.TypeName}.{m.Member}"),
            typeMembers.Where(m => m.BuiltinName != null).Select(m => $"{m.TypeName}.{m.Member}"), typeMembers.Where(m => ExcludedBuiltinReceivers.ContainsKey(m.Owner)).Select(m => $"{m.TypeName}.{m.Member}"));
        foreach (var m in typeMembers.Where(m => m.BuiltinName != null && PrimitiveCatalog.GetByName(m.BuiltinName)?.ClrType != m.Owner))
            problems.Add($"constant-kind × builtin name: `{m.BuiltinName}` is no name the primitive catalog gives {m.Owner.Name}, which {m.TypeName}.{m.Member} is read through");
        foreach (var (kind, expressions) in ConstantExpressions)
        {
            foreach (var member in expressions.GroupBy(e => e.Label, StringComparer.Ordinal))
            {
                var owed = ReadByItsBuiltinName(member.First().Owner) ? Owed(kind).Append(BuiltinNameSpelling) : Owed(kind);
                foreach (var spelling in owed.Where(o => !constantCells.Any(c => c.Cell.Key!.Part<ScopeConstantKind>() == kind && Marks(c).Contains(member.Key) && Marks(c).Contains(o))))
                    problems.Add($"constant-kind × spelling: no cell reads {kind} {member.Key} under {spelling}");
            }
        }
        // And at every host: a cell, or the reason the host's slot cannot take the expression's type.
        var marked = constantCells.Select(c => (Kind: c.Cell.Key!.Part<ScopeConstantKind>(), Marks: Marks(c).ToHashSet(StringComparer.Ordinal))).ToList();
        foreach (var (_, host) in AllConstantHosts())
        {
            foreach (var (kind, expressions) in ConstantExpressions)
            {
                foreach (var e in expressions)
                {
                    bool carried = marked.Any(m => m.Kind == kind && m.Marks.Contains(host.Label) && m.Marks.Contains(e.Label) && (e.Spelling == null || m.Marks.Contains(e.Spelling)));
                    if (carried == (HostSkips(host, e.Type) != null))
                        problems.Add($"constant-kind × host: {kind} {e.Label}{(e.Spelling == null ? "" : " under " + e.Spelling)} at {host.Label} is {(carried ? "generated and excluded" : "neither generated nor excluded")}");
                }
            }
        }
        if (!constantCells.Any(c => Marks(c).Contains(BuiltinNameSpelling)))
            problems.Add($"constant-kind × spelling: no cell reads a referenced type by its builtin name");
        foreach (var (shape, (owner, member)) in ExcludedShapeMembers)
        {
            if (ClrTypeMemberShape(owner, member) is var answered && answered != shape)
                problems.Add($"constant-kind × type member: `{owner.Name}.{member}` is what the exclusion of {shape} reads, and the checker's resolver answers {answered} for it");
            if (!ExcludedClrTypeMemberShapes.TryGetValue(shape, out var reason) || !BehaviourProbes.ContainsKey(reason))
                problems.Add($"constant-kind × type member: {shape} names a member and has no probed exclusion");
        }
        // A shape excluded because no member has it: the census of the namespace every program reads its types from holds it at 0.
        var censused = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in typeof(object).Assembly.GetExportedTypes().Where(t => t.Namespace == nameof(System) && !t.IsGenericTypeDefinition && !t.IsNested))
        {
            foreach (var value in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => NameMangler.ToSharpyName(m.Name, ReverseNameContext.Property))
                .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => NameMangler.ToSharpyName(m.Name, ReverseNameContext.Property))).Distinct())
            {
                string shape = ClrTypeMemberShape(type, value);
                censused.Add(shape);
                if (shape == DeclinedValueShape)
                    problems.Add($"constant-kind × type member: `{type.Name}.{value}` is {DeclinedValueShape}, which is excluded as a shape no member has");
            }
        }
        // The census's own control: it sees the static values the generated kinds are the image of, so its 0 is a count.
        foreach (var unseen in new[] { LiteralFieldShape, DecimalConstantShape, EnumFieldShape, StaticFieldShape, nameof(ClrMemberResolution.Property) }.Where(v => !censused.Contains(v)))
            problems.Add($"constant-kind × type member: the census of `System` sees no static value of shape {unseen}, so it says nothing of the shape it holds at 0");
        // A referenced MODULE's field is a constant by discovery's `IsConst`, which is `FieldInfo.IsLiteral` alone: a `const decimal`
        // there would be C#'s constant and discovery's static. No module declares one today (the census above is this scan's control:
        // it finds `Decimal`'s); when one does, the shape is owed its kind (ruling 17).
        foreach (var field in new[] { SharpyStdlibReference.Assembly, Sharpy.TestInfrastructure.SharpyCoreReference.Assembly }.SelectMany(a => a.GetExportedTypes())
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)).Where(IsDecimalConstant))
            problems.Add($"constant-kind × module member: `{field.DeclaringType!.Name}.{field.Name}` is a `const decimal` of a referenced assembly's own type, which discovery records as no constant: the shape is owed its kind");
        foreach (var (kind, expressions) in ConstantExpressions)
        {
            foreach (var e in expressions)
            {
                if (e.ModuleValue != null)
                {
                    if (!census.TryGetValue(e.ModuleValue, out var recorded))
                        problems.Add($"constant-kind: discovery records no member {e.ModuleValue}");
                    else if (!ModuleMemberShapes.TryGetValue(recorded, out var image) || image != kind)
                        problems.Add($"constant-kind: {e.Label} is listed under {kind}, and discovery records {e.ModuleValue} as {recorded}");
                }
                else if (e.Owner != null)
                {
                    var shape = ClrTypeMemberShape(e.Owner, e.Member!);
                    if (!ClrTypeMemberShapes.TryGetValue(shape, out var image) || image != kind)
                        problems.Add($"constant-kind: {e.Label} is listed under {kind}, and the checker's resolver answers {shape} for {e.Owner.Name}.{e.Member}");
                }
                else if (EnumReadKinds.TryGetValue(kind, out var read))
                {
                    string suffix = read.Access switch
                    {
                        null => ".RED",
                        EnumMemberAccessKind.IntValue or EnumMemberAccessKind.StringValue => ".RED.value",
                        _ => ".RED.name",
                    };
                    // The enum's declaration opens the prelude; a spelling may declare an alias of it after.
                    if (e.Prelude?.Split("\ntype ", 2)[0] != (read.IntShape ? IntEnum : StringEnum) || !e.Text.EndsWith(suffix, StringComparison.Ordinal))
                        problems.Add($"constant-kind: {e.Text} is listed under {kind}, which is `{suffix}` of {(read.IntShape ? "an int" : "a string")} enum");
                }
                else if (!ChainKinds.Contains(kind) || e.Import != null || e.Prelude != null)
                    problems.Add($"constant-kind: {e.Label} is listed under {kind} and names no member the kind can be checked against");
            }
        }
        foreach (var kind in Enum.GetValues<ScopeConstantKind>().Where(k => k != ScopeConstantKind.OwnMember && !ConstantExpressions.ContainsKey(k)))
            problems.Add($"constant-kind: {kind} has no expression");

        // init-overload
        Carried("init-overload", Enum.GetValues<ScopeOverloadCaller>(), Part<ScopeOverloadCaller>);

        // dataclass-init
        var initCells = Route(DataclassInitRoute).Select(c => (Case: c, Reader: c.Cell.Key!.Part<ScopeInitReader>()!.Value, Fit: c.Cell.Key.Part<ScopeInitFit>()!.Value, Order: c.Cell.Key.Part<Harness.Order>()!.Value,
            Slot: c.Cell.Position is { } position ? ((ReadPosition Position, Harness.Nesting Nesting)?)(position, c.Cell.Key.Part<Harness.Nesting>() ?? Harness.Nesting.Direct) : null, Marks: Marks(c).ToHashSet(StringComparer.Ordinal),
            Placement: c.Cell.Key.Part<ScopeInitPlacement>() ?? ScopeInitPlacement.Apart, Projection: ProjectionOf(c.Cell.Key))).ToList();
        var earlierAndLater = new[] { Harness.Order.Earlier, Harness.Order.Later };
        string Where((ReadPosition Position, Harness.Nesting Nesting)? slot) => slot is { } placed ? $"at {placed.Position}/{placed.Nesting}" : "as a declaration";
        // One cell at each slot owed and none elsewhere, at each order owed.
        void Product(string what, IEnumerable<(ReadPosition Position, Harness.Nesting Nesting)?> cells, IReadOnlyList<(ReadPosition Position, Harness.Nesting Nesting)?> owed, string route = DataclassInitRoute)
        {
            var at = cells.ToList();
            foreach (var slot in owed.Where(o => at.Count(a => Nullable.Equals(a, o)) != 1))
                problems.Add($"{route}: {what} has {at.Count(a => Nullable.Equals(a, slot))} cells {Where(slot)}, and one is owed");
            foreach (var stray in at.Where(a => !owed.Contains(a)).Distinct())
                problems.Add($"{route}: {what} has a cell {Where(stray)}, where the reader does not stand");
        }
        // A twin is python running the program with the same meaning: a cell that is one where python has no such program, or
        // that runs and is none where python has it, is a problem. `none` is why python has no program for the cell, or null.
        void Twin(Case c, string? none)
        {
            if (c.Twin && none != null)
                problems.Add($"{c.Cell.Key!.Route}: {c.Cell.Id} is a twin, and python has no such program: {none}");
            else if (!c.Twin && none == null && c.Cell.Key!.Part<ScopeInitFit>() == ScopeInitFit.Fits)
                problems.Add($"{c.Cell.Key!.Route}: {c.Cell.Id} runs and is no twin, where python has the shape and the read and binds the type's name before the read runs");
        }
        const string ADeclarationIsReadAsTheModuleLoads = "its reader is a declaration python evaluates as the module loads, above the type it reads: a NameError";
        List<(ReadPosition Position, Harness.Nesting Nesting)?> SlotsOf(InitRead read, IReadOnlyList<(ReadPosition Position, Harness.Nesting Nesting)> expression)
            => read.Shows == null ? expression.Select(s => ((ReadPosition Position, Harness.Nesting Nesting)?)s).ToList() : new List<(ReadPosition Position, Harness.Nesting Nesting)?> { null };

        Partition("dataclass-init × reader", Enum.GetValues<ScopeInitReader>(), InitReaders.Keys, ExcludedInitReaders.Keys);
        Carried("dataclass-init", InitReaders.Keys, Part<ScopeInitReader>);
        var matrix = initCells.Where(c => c.Placement == ScopeInitPlacement.Apart && c.Projection == null).ToList();
        Partition("dataclass-init × reader × fit", InitReaders.Keys.SelectMany(r => Enum.GetValues<ScopeInitFit>().Select(f => (Reader: r, Fit: f))), matrix.Select(c => (c.Reader, c.Fit)), ExcludedInitFits.Keys);
        Partition("dataclass-init × site", Enum.GetValues<ScopeKind>(), DataclassSites.Keys, ExcludedDataclassScopes.Keys);
        Partition("dataclass-init × host", Enum.GetValues<TypeKind>(), DataclassHosts, ExcludedDataclassHosts.Keys);
        Partition("dataclass-init × option", typeof(DataclassOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name), Array.Empty<string>(), ExcludedDataclassOptions.Keys);
        foreach (var empty in DataclassSites.Where(e => !e.Value.Any(site => !site.Projected)).Select(e => e.Key))
            problems.Add($"dataclass-init × site: {empty} is listed with no site of the matrix");
        if (InitReaderSlots.Count == 0)
            problems.Add("dataclass-init × slot: the typed-read set has no reader of a module function, so a reader that is an expression stands nowhere");
        // The matrix: every pair that has cells, at every site that is not projected, at both orders; an expression at every slot, a declaration at none.
        foreach (var pair in matrix.Select(c => (c.Reader, c.Fit)).Distinct())
        {
            foreach (var site in AllDataclassSites().Where(s => !s.Projected))
            {
                foreach (var order in earlierAndLater)
                {
                    Product($"{pair.Reader} {pair.Fit} at the {site.Label} site, {order},",
                        matrix.Where(c => c.Reader == pair.Reader && c.Fit == pair.Fit && c.Order == order && c.Marks.Contains(site.Label)).Select(c => c.Slot), SlotsOf(InitReaders[pair.Reader], InitReaderSlots));
                }
            }
        }
        foreach (var stray in matrix.Where(c => c.Order is not (Harness.Order.Earlier or Harness.Order.Later)))
            problems.Add($"dataclass-init: {stray.Case.Cell.Id} is the matrix's and stands at {stray.Order}");

        // The projected axes: each enumeration is the matrix's own value, the projected ones and the ones excluded whole.
        Partition("dataclass-init × base", Enum.GetValues<ScopeDataclassBase>(), DataclassBases.Keys.Append(ScopeDataclassBase.None), ExcludedDataclassBases.Keys);
        Partition("dataclass-init × field default", Enum.GetValues<ScopeFieldDefault>(), FieldDefaults.Keys.Append(ScopeFieldDefault.None), Array.Empty<ScopeFieldDefault>());
        Partition("dataclass-init × reference slot", Enum.GetValues<ScopeReferenceSlot>(), ReferenceSlots.Keys.Append(ScopeReferenceSlot.Parameter), Array.Empty<ScopeReferenceSlot>());
        Partition("dataclass-init × through", Enum.GetValues<ScopeInitThrough>(), InitThroughs.Keys.Append(ScopeInitThrough.Itself), Array.Empty<ScopeInitThrough>());
        Partition("dataclass-init × name", Enum.GetValues<ScopeDataclassName>(), DataclassNames.Keys.Append(ScopeDataclassName.Declared), ExcludedDataclassNames.Keys);
        // A projection: every reader it has a program for, under every fit the matrix leaves, is a cell or carries its reason;
        // and each one that is a cell stands at both orders, an expression at the projected slot.
        foreach (var slot in ProjectedSlots.Concat(EnclosedSlots).Append(OtherDataclassSlot).Where(s => !InitReaderSlots.Contains(s)))
            problems.Add($"dataclass-init × slot: {slot.Position}/{slot.Nesting} is no slot of the matrix, which is what holds the slot axis");
        foreach (var slot in EnclosedSlots.Where(s => ModulePlacedPositions.Contains(s.Position) || Harness.HostHeader(s.Position) != "class H:"))
            problems.Add($"dataclass-init × slot: {slot.Position}/{slot.Nesting} is no member of a plain class, which is what the enclosing type is");
        foreach (var p in InitProjections)
        {
            var cells = initCells.Where(c => c.Placement == ScopeInitPlacement.Apart && Equals(c.Projection, p.Value)).ToList();
            if (cells.Count == 0)
                problems.Add($"dataclass-init × {p.Value}: the value is listed as projected and no cell carries it");
            Partition($"dataclass-init × {p.Value} × reader × fit",
                p.Readers.Keys.SelectMany(r => Enum.GetValues<ScopeInitFit>().Select(f => (Reader: r, Fit: f))).Where(pair => !ExcludedInitFits.ContainsKey(pair)),
                cells.Select(c => (c.Reader, c.Fit)), ExcludedProjections.Keys.Where(k => Equals(k.Value, p.Value)).Select(k => (k.Reader, k.Fit)));
            foreach (var pair in cells.Select(c => (c.Reader, c.Fit)).Distinct())
            {
                foreach (var order in earlierAndLater)
                    Product($"{pair.Reader} {pair.Fit} under {p.Value}, {order},", cells.Where(c => c.Reader == pair.Reader && c.Fit == pair.Fit && c.Order == order).Select(c => c.Slot), SlotsOf(p.Readers[pair.Reader], ProjectedSlots));
            }
            foreach (var stray in cells.Where(c => c.Order is not (Harness.Order.Earlier or Harness.Order.Later) || !c.Marks.Contains(p.Site)))
                problems.Add($"dataclass-init: {stray.Case.Cell.Id} is projected under {p.Value}, at the {p.Site} site and the two orders, and stands elsewhere");
        }
        foreach (var orphan in ExcludedProjections.Keys.Where(k => !InitProjections.Any(p => Equals(p.Value, k.Value))))
            problems.Add($"dataclass-init: an exclusion names {orphan.Value}, which is no projected value");

        // The placements: where the reader stands, each reader a cell there or excluded with its reason.
        Partition("dataclass-init × placement", Enum.GetValues<ScopeInitPlacement>(), initCells.Select(c => c.Placement), ExcludedInitPlacements.Keys);
        var placed = initCells.Where(c => c.Placement != ScopeInitPlacement.Apart).ToList();
        Partition("dataclass-init × placement × reader", placed.Select(c => c.Placement).Distinct().SelectMany(p => InitReaders.Keys.Select(r => (Placement: p, Reader: r))),
            placed.Select(c => (c.Placement, c.Reader)), ExcludedPlacementReaders.Keys);
        foreach (var group in placed.GroupBy(c => (c.Placement, c.Reader)))
        {
            bool other = group.Key.Placement == ScopeInitPlacement.OtherDataclass;
            var orders = other ? OtherDataclassOrders : earlierAndLater;
            var owed = other
                ? new List<(ReadPosition Position, Harness.Nesting Nesting)?> { OtherDataclassSlot }
                : SlotsOf(InitReaders[group.Key.Reader], EnclosedSlots);
            // A reader in another dataclass stands under each ring: the dataclass with no base, and the one over a dataclass.
            var rings = other ? OtherDataclassRings.Select(b => b == ScopeDataclassBase.None ? null : (object?)b).ToArray() : new object?[] { null };
            foreach (var ring in rings)
            {
                foreach (var fit in Enum.GetValues<ScopeInitFit>().Where(f => !ExcludedInitFits.ContainsKey((group.Key.Reader, f))))
                {
                    foreach (var order in orders)
                    {
                        Product($"{group.Key.Reader} {fit} placed {group.Key.Placement}{(ring == null ? "" : $" over {ring}")}, {order},",
                            group.Where(c => Equals(c.Projection, ring) && c.Fit == fit && c.Order == order).Select(c => c.Slot), owed);
                    }
                }
            }
            foreach (var stray in group.Where(c => !orders.Contains(c.Order) || !rings.Contains(c.Projection)))
                problems.Add($"dataclass-init: {stray.Case.Cell.Id} is placed {group.Key.Placement} and stands at {stray.Order}{(stray.Projection == null ? "" : $" over {stray.Projection}")}");
        }
        // A base that stands first is a dataclass a reader can be a method of: each has its ring, and a ring has such a base.
        var standingFirst = DataclassBases.Where(e => e.Value.First != null).Select(e => e.Key).Append(ScopeDataclassBase.None).ToHashSet();
        foreach (var ring in standingFirst.Where(b => !OtherDataclassRings.Contains(b)))
            problems.Add($"dataclass-init × ring: the dataclass over {ring} has no cell whose reader is a method of that base, the ring no order cures");
        foreach (var ring in OtherDataclassRings.Where(b => !standingFirst.Contains(b)))
            problems.Add($"dataclass-init × ring: {ring} is no base that stands first, which is what a ring's reader is a method of");
        // A ring is a reader in another dataclass that the dataclass reads back: the one order no other placement has.
        foreach (var ring in initCells.Where(c => c.Order == Harness.Order.Cycle && c.Placement != ScopeInitPlacement.OtherDataclass))
            problems.Add($"dataclass-init: {ring.Case.Cell.Id} stands in a ring and is no reader in another dataclass");
        // Python at each slot of the matrix: it reads the name when `main` calls the reader, or the slot says why no cell there is its program.
        Partition("dataclass-init × python at the slot", InitReaderSlots, CalledSlots, SlotsWithNoTwin.Keys);
        foreach (var c in initCells)
        {
            string? none = NotPythonsProgram(c.Case)
                ?? (c.Placement == ScopeInitPlacement.Apart && c.Projection is ScopeReferenceSlot handed && ReferenceSlotsWithNoTwin.TryGetValue(handed, out var noSlot) ? noSlot
                : c.Placement == ScopeInitPlacement.Apart && c.Projection is ScopeDataclassBase over && BasesWithNoTwin.TryGetValue(over, out var noBase) ? noBase
                : c.Order != Harness.Order.Earlier && c.Projection is ScopeInitThrough ? "the type its reader names derives from the dataclass and stands above it: python evaluates that base list as the module loads, a NameError"
                : c.Slot is { } at ? SlotsWithNoTwin.GetValueOrDefault(at)
                : c.Placement == ScopeInitPlacement.EnclosingType ? "its reader is a sibling class that names `H.DC` in the body of `H`, which python has not bound yet"
                : c.Order == Harness.Order.Earlier || InABodyAlone((c.Projection is ScopeReferenceSlot slot ? ReferenceSlots[slot] : InitReaders[c.Reader]).Fits, "{N}") ? null
                : ADeclarationIsReadAsTheModuleLoads);
            Twin(c.Case, none);
        }
        // The two tables of reasons say what the shapes' own flags say, and name no value that has cells python runs.
        foreach (var slot in Enum.GetValues<ScopeReferenceSlot>().Where(v => ReferenceSlots.TryGetValue(v, out var read) && read.Python == ReferenceSlotsWithNoTwin.ContainsKey(v)))
            problems.Add($"dataclass-init × reference slot: {slot} is {(ReferenceSlots[slot].Python ? "python's" : "not python's")} by its read, and the table of slots with no twin says otherwise");
        foreach (var over in Enum.GetValues<ScopeDataclassBase>().Where(v => DataclassBases.TryGetValue(v, out var shape) && shape.Twin == BasesWithNoTwin.ContainsKey(v)))
            problems.Add($"dataclass-init × base: {over} is {(DataclassBases[over].Twin ? "python's" : "not python's")} by its shape, and the table of bases with no twin says otherwise");

        // written-init: the base cell is reader × fit × order; an argument shape is varied alone, and so is each variation.
        var written = Route(WrittenInitRoute).Select(c => (Case: c, Argument: c.Cell.Key!.Part<ScopeInitArgument>()!.Value, Reader: c.Cell.Key.Part<ScopeInitReader>()!.Value, Fit: c.Cell.Key.Part<ScopeInitFit>()!.Value,
            Order: c.Cell.Key.Part<Harness.Order>()!.Value,
            Slot: c.Cell.Position is { } position ? ((ReadPosition Position, Harness.Nesting Nesting)?)(position, c.Cell.Key.Part<Harness.Nesting>() ?? Harness.Nesting.Direct) : null,
            Variation: c.Cell.Key.Part<ScopeInitVariation>() ?? ScopeInitVariation.None)).ToList();
        var star = written.Where(c => c.Variation == ScopeInitVariation.None).ToList();
        Partition("written-init × argument", Enum.GetValues<ScopeInitArgument>(), WrittenInits.Keys, Array.Empty<ScopeInitArgument>());
        Partition("written-init × reader", Enum.GetValues<ScopeInitReader>(), WrittenInitReaders.Keys, ExcludedWrittenInitReaders.Keys);
        Partition("written-init × argument × reader × fit",
            WrittenInits.Keys.SelectMany(a => WrittenInitReaders.Keys.SelectMany(r => Enum.GetValues<ScopeInitFit>().Select(f => (Argument: a, Reader: r, Fit: f)))),
            star.Select(c => (c.Argument, c.Reader, c.Fit)), ExcludedWrittenInits.Keys);
        foreach (var triple in star.Select(c => (c.Argument, c.Reader, c.Fit)).Distinct())
        {
            foreach (var order in earlierAndLater)
            {
                Product($"{triple.Reader} {triple.Fit} handed {triple.Argument}, {order},", star.Where(c => (c.Argument, c.Reader, c.Fit) == triple && c.Order == order).Select(c => c.Slot),
                    SlotsOf(WrittenInitReaders[triple.Reader], ProjectedSlots), WrittenInitRoute);
            }
        }
        foreach (var stray in star.Where(c => !earlierAndLater.Contains(c.Order)))
            problems.Add($"written-init: {stray.Case.Cell.Id} stands at {stray.Order}, and the star has the two orders");
        // A variation: every reader under each fit is a cell or carries its reason, and each one that is a cell stands at
        // each of the variation's orders, an expression at the variation's slot, over the base cell's argument.
        Partition("written-init × variation", Enum.GetValues<ScopeInitVariation>(), WrittenVariations.Keys.Append(ScopeInitVariation.None), Array.Empty<ScopeInitVariation>());
        foreach (var (variation, v) in WrittenVariations.OrderBy(e => e.Key))
        {
            var cells = written.Where(c => c.Variation == variation).ToList();
            Partition($"written-init × {variation} × reader × fit", WrittenInitReaders.Keys.SelectMany(r => Enum.GetValues<ScopeInitFit>().Select(f => (Reader: r, Fit: f))),
                cells.Select(c => (c.Reader, c.Fit)), ExcludedWrittenVariations.Keys.Where(k => k.Variation == variation).Select(k => (k.Reader, k.Fit)));
            foreach (var pair in cells.Select(c => (c.Reader, c.Fit)).Distinct())
            {
                foreach (var order in v.Orders)
                {
                    Product($"{pair.Reader} {pair.Fit} under {variation}, {order},", cells.Where(c => (c.Reader, c.Fit) == pair && c.Order == order).Select(c => c.Slot),
                        SlotsOf(WrittenInitReaders[pair.Reader], new[] { v.Slot }), WrittenInitRoute);
                }
            }
            foreach (var stray in cells.Where(c => !v.Orders.Contains(c.Order) || c.Argument != ScopeInitArgument.Typed))
                problems.Add($"written-init: {stray.Case.Cell.Id} is {variation}'s, which varies the base cell alone at [{string.Join(", ", v.Orders)}], and stands elsewhere");
            if (!InitReaderSlots.Contains(v.Slot))
                problems.Add($"written-init × {variation}: {v.Slot.Position}/{v.Slot.Nesting} is no slot of the dataclass-init matrix, which is what holds the slot axis");
            foreach (var code in (v.Refusals ?? new Dictionary<ScopeInitReader, string>()).Where(e => WrittenInitReaders[e.Key].Refusal == e.Value))
                problems.Add($"written-init × {variation}: {code.Key} is given the base cell's own code, {code.Value}, as another");
        }
        foreach (var orphan in ExcludedWrittenVariations.Keys.Where(k => !WrittenVariations.ContainsKey(k.Variation)))
            problems.Add($"written-init: an exclusion names {orphan.Variation}, which is no variation with cells");
        foreach (var ring in written.Where(c => c.Order == Harness.Order.Cycle && c.Variation != ScopeInitVariation.Ring))
            problems.Add($"written-init: {ring.Case.Cell.Id} stands in a ring and is no ring's cell");
        foreach (var ring in written.Where(c => c.Variation == ScopeInitVariation.Ring && c.Order == Harness.Order.Cycle && !c.Case.Cell.Source.Contains(WrittenRingReads[c.Reader].Read, StringComparison.Ordinal)))
            problems.Add($"written-init × ring: {ring.Case.Cell.Id} stands in a ring, and the class does not read the reader's class back");
        foreach (var c in written)
        {
            // A reader that is an expression is python's where `main` calls its slot, at both orders. In a module
            // initializer python reads it as the module loads: with the class above it, and no helper declared last to
            // hand the value to, that is python's program too; a ring's reader is a method's body.
            var read = WrittenInitReaders[c.Reader];
            string? none = c.Slot is { } at
                ? SlotsWithNoTwin.ContainsKey(at)
                    ? c.Order == Harness.Order.Earlier && read.FitsHelper == null ? null
                        : c.Order == Harness.Order.Earlier ? SlotsWithNoTwin[at] : "python evaluates the initializer as the module loads, above the class it reads: a NameError"
                    : null
                : c.Order == Harness.Order.Earlier || InABodyAlone(read.Fits, "{N}", "{B}") ? null : ADeclarationIsReadAsTheModuleLoads;
            Twin(c.Case, none);
        }

        // dataclass-member: what the synthesis adds, each member under each read at both orders.
        var members = Route(DataclassMemberRoute).Select(c => (Case: c, Member: c.Cell.Key!.Part<ScopeDataclassMember>()!.Value, Read: c.Cell.Key.Part<ScopeMemberRead>()!.Value, Order: c.Cell.Key.Part<Harness.Order>()!.Value)).ToList();
        Partition("dataclass-member × member the synthesis adds", SynthesizedMemberNames(), Enum.GetNames<ScopeDataclassMember>(), Array.Empty<string>());
        Partition("dataclass-member × member", Enum.GetValues<ScopeDataclassMember>(), MemberReads.Keys.Select(k => k.Member), ExcludedDataclassMembers.Keys);
        Partition("dataclass-member × member × read",
            MemberReads.Keys.Select(k => k.Member).Distinct().SelectMany(m => Enum.GetValues<ScopeMemberRead>().Select(r => (Member: m, Read: r))), members.Select(c => (c.Member, c.Read)), ExcludedMemberReads.Keys);
        foreach (var pair in MemberReads.Keys)
        {
            foreach (var order in earlierAndLater.Where(o => members.Count(c => (c.Member, c.Read) == pair && c.Order == o) != 1))
                problems.Add($"dataclass-member: {pair.Member} {pair.Read} has {members.Count(c => (c.Member, c.Read) == pair && c.Order == order)} cells at {order}, and one is owed");
        }
        foreach (var stray in members.Where(c => !earlierAndLater.Contains(c.Order)))
            problems.Add($"dataclass-member: {stray.Case.Cell.Id} stands at {stray.Order}, and the set has the two orders");
        // A reader that redefines one half of the equality contract redefines the other: whether an inherited half satisfies
        // the pairing rule (dunder_methods.md; #2803 notes the reading) is not this set's to assume.
        foreach (var e in MemberReads.Where(e => e.Value.Reader.Contains("def __eq__", StringComparison.Ordinal) != e.Value.Reader.Contains("def __hash__", StringComparison.Ordinal)))
            problems.Add($"dataclass-member: {e.Key.Member} {e.Key.Read} redefines one of `__eq__` and `__hash__` alone, and the set rests on no reading of the pairing rule");
        foreach (var c in members)
        {
            var read = MemberReads[(c.Member, c.Read)];
            Twin(c.Case, read.NoTwin ?? (c.Order == Harness.Order.Earlier || InABodyAlone(read.Reader, "DC") ? null : ADeclarationIsReadAsTheModuleLoads));
        }

        // The three sets on the order-free rule. A read that does not fit names the one code its control carries, and a read
        // that fits names none.
        var orderFree = pool.Where(c => OrderFreeRoutes.Contains(c.Cell.Key!.Route ?? "")).ToList();
        foreach (var c in orderFree.Where(c => (c.Cell.Key!.Part<ScopeInitFit>() == ScopeInitFit.Misfits) != (c.Cell.SeedRefusalCodes is { Count: 1 })))
            problems.Add($"{c.Cell.Key!.Route}: {c.Cell.Id} {(c.Cell.Key.Part<ScopeInitFit>() == ScopeInitFit.Misfits ? "does not fit and names no one code its refusal carries" : "fits and names a refusal")}");
        // A cell at another order than the first has its control, the same read with the type declared first: what says
        // whether its red is the order's.
        var orderFreeIds = orderFree.Select(c => c.Cell.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var c in orderFree.Where(c => InitControlId(c.Cell) is { } control && !orderFreeIds.Contains(control)))
            problems.Add($"{c.Cell.Key!.Route}: {c.Cell.Id} has no control with the type declared first, which is what says whether its red is the order's");

        // lambda-default
        var lambdaDefaults = Route("lambda-default").ToList();
        Carried("lambda-default", Enum.GetValues<ScopeDefaultOwner>(), Part<ScopeDefaultOwner>);
        Partition("lambda-default × owner", Enum.GetValues<ScopeDefaultOwner>(), DefaultOwners.Keys, Array.Empty<ScopeDefaultOwner>());
        Partition("lambda-default × lambda spelling", new[] { false, true }, DefaultOwners.Values.Where(o => o.Arrow != null).Select(o => o.Arrow!.Value), Array.Empty<bool>());
        if (LambdaDefaultKinds.Count == 0)
            problems.Add("lambda-default × kind: the 8951 pool runs no bare read of a member from a `def`'s default in its own class, so the set has no kind and no control");
        Carried("lambda-default", LambdaDefaultKinds, c => new[] { c.Cell.Target });
        Carried("lambda-default", Enum.GetValues<ScopeAnnotation>(), Part<ScopeAnnotation>);
        foreach (var kind in LambdaDefaultKinds)
        {
            Partition($"lambda-default × position of {kind}", BareReadPositions(kind),
                lambdaDefaults.Where(c => c.Cell.Target == kind && c.Cell.Key!.Part<ScopeDefaultOwner>() != ScopeDefaultOwner.Def).Select(c => c.Cell.Position!.Value), ExcludedLambdaDefaultPositions.Keys);
        }
        foreach (var c in lambdaDefaults)
        {
            // What the cell's program holds, by the parser: a `def`'s cell no lambda, a lambda's cell the one lambda of its spelling.
            var owner = c.Cell.Key!.Part<ScopeDefaultOwner>()!.Value;
            var lambdas = LambdasWithADefault(c.Cell.Source);
            var owed = DefaultOwners[owner].Arrow is { } arrow ? new[] { arrow } : Array.Empty<bool>();
            if (!lambdas.SequenceEqual(owed))
                problems.Add($"lambda-default × owner: {c.Cell.Id} is {owner}'s, and the parser finds [{string.Join(", ", lambdas.Select(a => a ? "an arrow lambda" : "a `lambda`"))}] with a parameter default in its program");
        }
        // A read's cell stands beside its control: the same owner at the same position with a literal default.
        foreach (var slot in lambdaDefaults.Where(c => !Marks(c).Contains(LiteralDefault)).Select(c => (Owner: c.Cell.Key!.Part<ScopeDefaultOwner>()!.Value, Position: c.Cell.Position!.Value)).Distinct())
        {
            if (!lambdaDefaults.Any(c => Marks(c).Contains(LiteralDefault) && c.Cell.Key!.Part<ScopeDefaultOwner>() == slot.Owner && c.Cell.Position == slot.Position))
                problems.Add($"lambda-default × control: {slot.Owner} at {slot.Position} has no cell with a literal default, which is what says the slot itself runs");
        }
        // The class-body rule's two axes, as a bare sibling read has them: both orders, with and without a module binding of the name.
        foreach (var sibling in lambdaDefaults.Where(c => !Marks(c).Contains(LiteralDefault)).GroupBy(c => string.Join("/", c.Cell.Key!.Parts.Where(p => p is not Harness.Order && !(p is string text && text.StartsWith("outer=", StringComparison.Ordinal))))))
        {
            var seen = sibling.Select(c => (c.Cell.Key!.Part<Harness.Order>(), c.Cell.Outer != BindingKind.None)).ToHashSet();
            foreach (var order in new Harness.Order?[] { Harness.Order.Earlier, Harness.Order.Later })
            {
                foreach (var bound in new[] { false, true }.Where(b => !seen.Contains((order, b))))
                    problems.Add($"lambda-default × sibling: {sibling.Key} has no cell at {order} {(bound ? "under" : "without")} a module binding of the name");
            }
        }

        // alias-target
        Partition("alias-target × site", Enum.GetValues<ScopeKind>(), AliasSites.Keys, ExcludedAliasScopes.Keys);
        Partition("alias-target × type body", Enum.GetValues<TypeKind>(), AliasTypeHosts.Keys, ExcludedAliasTypeHosts.Keys);
        Partition("alias-target × shape", new[] { typeof(AstTypeAlias), typeof(AstTypeAnnotation), typeof(AstFunctionType) }.SelectMany(DeclaredProperties),
            AliasShapes.Keys, ExcludedAliasShapeProperties.Keys);
        Carried("alias-target", AllAliasSites().Select(s => s.Label).Concat(AliasShapes.Values.Select(s => s.Label)), Marks);
        Carried("alias-target", Enum.GetValues<ScopeAliasTarget>(), Part<ScopeAliasTarget>);
        Carried("alias-target", Enum.GetValues<ScopeAliasUse>(), Part<ScopeAliasUse>);
        Partition("alias-target × target × use",
            Enum.GetValues<ScopeAliasTarget>().SelectMany(t => Enum.GetValues<ScopeAliasUse>().Select(u => (Target: t, Use: u))),
            Route("alias-target").Select(c => (Target: c.Cell.Key!.Part<ScopeAliasTarget>()!.Value, Use: c.Cell.Key.Part<ScopeAliasUse>()!.Value)), ExcludedAliasUses.Keys);
        // A shape's name is the alias itself in some cell, or the shape says why it cannot be.
        var selfReaching = Route("alias-target").Where(c => c.Cell.Key!.Part<ScopeAliasTarget>() == ScopeAliasTarget.Itself).SelectMany(Marks).ToHashSet(StringComparer.Ordinal);
        Partition("alias-target × self-reaching shape", AliasShapes.Values.Select(v => v.Label).Distinct(), AliasShapes.Values.Select(v => v.Label).Where(selfReaching.Contains), ExcludedSelfReachingShapes.Keys);
        // The two binders of an alias, by the 8951 pool's census of binder sites: a third is a site this axis has not placed.
        var binders = NameResolutionRosters.BinderSites.Keys.Where(k => k.Contains("TypeAliasSymbol.CreateFrom", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        if (!binders.SequenceEqual(AliasBinderSites.OrderBy(k => k, StringComparer.Ordinal)))
            problems.Add($"alias-target × site: the binder census holds [{string.Join("; ", binders)}], and the sites were placed for [{string.Join("; ", AliasBinderSites)}]");

        // A probe has its exclusion, and an exclusion that rests on the compiler has its probe. What rests on the compiler is read
        // from the reason's own text: one that names a diagnostic code or cites an issue says what a program does today.
        var reasons = ExcludedDeclarations.Values.Concat(ExcludedDeclarationHosts.Values).Concat(ExcludedValueTypes.Values).Concat(ExcludedTypedReads.Values)
            .Concat(ExcludedTypedSpellings.Values).Concat(ExcludedTypedPositions.Values).Concat(ExcludedTypedPlacements.Values).Concat(ExcludedTypedReceivers.Values).Concat(ExcludedCalleeBinders.Values).Concat(ExcludedCalleeOuters.Values)
            .Concat(ExcludedAdmissionTables.Values).Concat(ExcludedModuleMemberShapes.Values).Concat(ExcludedModuleCollections.Values).Concat(ExcludedAliasScopes.Values).Concat(ExcludedAliasTypeHosts.Values)
            .Concat(ExcludedOwnSpellings.Values).Concat(ExcludedOwnPlacements.Values).Concat(ExcludedEnumSpellings.Values).Concat(ExcludedImportKinds.Values)
            .Concat(ExcludedBuiltinReceivers.Values).Concat(ExcludedLambdaUses.Values).Concat(ExcludedLambdaDefaultPositions.Values)
            .Concat(ExcludedInitReaders.Values).Concat(ExcludedInitFits.Values).Concat(ExcludedDataclassScopes.Values).Concat(ExcludedDataclassHosts.Values).Concat(ExcludedDataclassOptions.Values).Concat(ExcludedDataclassBases.Values)
            .Concat(ExcludedProjections.Values).Concat(ExcludedDataclassNames.Values).Concat(ExcludedInitPlacements.Values).Concat(ExcludedPlacementReaders.Values)
            .Concat(ExcludedWrittenInitReaders.Values).Concat(ExcludedWrittenInits.Values).Concat(ExcludedWrittenVariations.Values).Concat(ExcludedDataclassMembers.Values).Concat(ExcludedMemberReads.Values)
            .Concat(AllConstantHosts().SelectMany(h => Enum.GetValues<Harness.Spelling>().Select(sp => OwnSpellingSkips(h.Host, sp, inside: true))).Where(r => r != null).Select(r => r!))
            .Concat(ExcludedAliasShapeProperties.Values).Concat(ExcludedOrders.Values.SelectMany(o => o.Values))
            .Concat(ExcludedConstScopes.Values).Concat(ExcludedConstTypeHosts.Values).Concat(ExcludedConstantReaders.Values).Concat(ExcludedClrTypeMemberShapes.Values)
            .Concat(ExcludedOwnReads.Values).Concat(ExcludedConstantForms.Values).Concat(ExcludedAliasUses.Values).Concat(ExcludedSelfReachingShapes.Values)
            .Concat(AllConstantHosts().SelectMany(h => new[] { "int", "() -> int", "m0.T" }.Select(t => HostSkips(h.Host, t))).Where(r => r != null).Select(r => r!))
            .Concat(AllConstantHosts().SelectMany(h => TypedReads.Keys.Select(k => OwnKindSkips(h.Host, k, inside: true))).Where(r => r != null).Select(r => r!))
            .Concat(EnumReadKinds.Keys.SelectMany(k => Enum.GetValues<Harness.Spelling>().Select(sp => EnumReadSkips(sp, k))).Where(r => r != null).Select(r => r!))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var orphan in BehaviourProbes.Keys.Concat(CrashProbes.Keys).Where(k => !reasons.Contains(k)))
            problems.Add($"a probe guards the reason '{orphan}', and no exclusion gives it");
        foreach (var unprobed in reasons.Where(r => NamesWhatTheCompilerDoes.IsMatch(r) && !BehaviourProbes.ContainsKey(r) && !CrashProbes.ContainsKey(r)).OrderBy(r => r, StringComparer.Ordinal))
            problems.Add($"the exclusion '{unprobed}' names a diagnostic code or an issue, so it rests on what the compiler does today, and no probe holds it");
        foreach (var twice in BehaviourProbes.Keys.Where(CrashProbes.ContainsKey))
            problems.Add($"the reason '{twice}' has a probe in this process and one in a child process: it rests on one behaviour");
        foreach (var blank in reasons.Where(string.IsNullOrWhiteSpace))
            problems.Add($"an exclusion gives no reason: '{blank}'");
        return problems;
    }

    /// <summary>What marks a reason as resting on the compiler: a diagnostic code (Sharpy's or C#'s) or an issue's number.</summary>
    private static readonly Regex NamesWhatTheCompilerDoes = new(@"\b(?:SPY|CS)\d{4}\b|#\d{3,}");

    /// <summary>The binder sites the alias sites were placed for (<see cref="AliasSites"/>'s summary).</summary>
    internal static readonly string[] AliasBinderSites =
    {
        "NameResolver.Members.cs:ResolveTypeAliasDeclaration(TypeAliasSymbol.CreateFrom(typeAlias, _currentFilePath))",
        "TypeChecker.Utilities.cs:RegisterScopedTypeAlias(TypeAliasSymbol.CreateFrom(typeAlias, _currentFilePath))",
    };

    internal static IEnumerable<AliasSite> AllAliasSites()
        => AliasSites.OrderBy(e => e.Key).SelectMany(e => e.Key == ScopeKind.TypeBody ? AliasTypeHosts.OrderBy(h => h.Key).SelectMany(h => h.Value) : e.Value);

    [Fact]
    public void Axes_EveryValueIsGeneratedOrExcluded()
    {
        var problems = AxisProblems(GeneratedCases());
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The falsifier of an exclusion that rests on the compiler DYING: the child process still dies of it, and still
    /// compiles the control. When the crash stops, the value is owed its cells.</summary>
    [Fact]
    public void Exclusions_RestingOnACompilerCrash_GoRedWhenItStops()
    {
        var problems = new List<string>();
        foreach (var (reason, probe) in CrashProbes.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            var control = CompileInChildProcess(probe.Control);
            if (!control.Compiled)
                problems.Add($"'{reason}': the child process does not compile the control, so it says nothing about the crash: {control}");
            var run = CompileInChildProcess(probe.Source);
            if (!probe.Holds(run))
                problems.Add($"'{reason}' rests on {probe.Expected}, and the child process now gives {run}: generate the value, or give the exclusion its new reason");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The falsifier of every exclusion that rests on compiler behaviour: its probe still does what the reason says.</summary>
    [Fact]
    public void Exclusions_RestingOnTheCompiler_GoRedWhenItChanges()
    {
        var problems = new List<string>();
        foreach (var (reason, probes) in BehaviourProbes.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            foreach (var probe in probes.All)
            {
                var run = RunSharpy(probe.Source, probe.Stdlib, SharpyExecTimeoutMs);
                if (!probe.Holds(run))
                {
                    problems.Add($"'{reason}' rests on {probe.Expected}, and the probe now gives [{string.Join(",", run.ErrorCodes.Distinct())}{(run.CsCode != null ? " " + run.CsCode : "")}]"
                        + $"{(run.ErrorCodes.Count == 0 ? $" stdout '{Harness.Normalize(run.Stdout)}'" : "")}: generate the value, or give the exclusion its new reason");
                }
            }
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
