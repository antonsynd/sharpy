extern alias SharpyRT;
extern alias SharpyStdlib;

using System.Globalization;
using System.Reflection;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sem = Sharpy.Compiler.Semantic;
using TypeKind = Sharpy.Compiler.Semantic.TypeKind;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>
/// The class guard for membership with an optional left operand (#2635): a needle of type <c>T | None</c> is
/// tested against every container kind exactly as the <c>T</c> needle is, and its <c>None</c> answers what
/// CPython answers — <c>False</c> for <c>in</c> (an equality scan finds no element equal to <c>None</c>),
/// <c>True</c> for <c>not in</c>, and <c>TypeError</c> at run time where CPython raises (a <c>str</c> or
/// <c>bytes</c> container) — owner rulings 2026-11-12. Never SPY0222, SPY0908 or a CLR exception. The literal
/// <c>None</c> into a non-optional slot is refused (SPY0222) by owner ruling A, 2026-11-12, and accepted into an
/// optional element type (the element-optionality axis).
/// <para><b>Generated, never listed.</b> The full product of seven axes, with three structural exclusions (an optional
/// element exists only where the element type is the payload, and not for a payload
/// <see cref="OptionalElementExcluded"/> names with the defect that refuses it; the literal <c>None</c> has no
/// evaluation to order, <see cref="OrderExcludedForms"/>); the count is asserted from the axis sizes, not the
/// generator:</para>
/// <list type="bullet">
/// <item><b>payload</b> — the image of <see cref="PrimitiveCatalog.GetAllPrimitives"/> (one canonical name per
/// CLR type) minus <see cref="ExcludedPrimitives"/>, each excluded with its reason, plus a value-typed
/// <c>tuple[int, int]</c> (the tuple type, outside both censuses), plus <see cref="TypeKindPayloads"/>: the image of
/// the compiler's <see cref="TypeKind"/> (every kind of user-declared type, each generated or excluded with its
/// reason; an enum by both of its representations, int-backed and string-backed), asserted both ways by
/// <see cref="AssertCompositeAxisIsTheTypeKindImage"/>, plus
/// <see cref="SlotPayloads"/>: every fixed needle slot of the container axis (a single-argument public
/// <c>Contains</c> parameter, read by reflection) that is not a catalog primitive — asserted both ways by
/// <see cref="AssertPayloadAxisIsTheCatalogAndTheSlots"/>, plus <see cref="ScopedPayloads"/>: a constructed generic
/// type, a type parameter and <c>Self</c>. The whole axis is closed against the compiler's type model,
/// <see cref="SemanticTypeCensus"/>: every sealed <c>SemanticType</c> record, and a record that carries a definition
/// once per kind of definition (a generic struct and a generic class; a <c>struct</c>-constrained, a
/// <c>class</c>-constrained and an unconstrained type parameter; <c>Self</c> in a struct and in a class), is generated
/// or excluded with its reason, and each payload's key is the one the checker gives it
/// (<see cref="AssertPayloadAxisIsTheSemanticTypeCensus"/>);</item>
/// <item><b>container</b> — the closed set of the spec's "Container kinds" table
/// (<c>membership_operators.md</c>), read from the spec and mapped row by row (<see cref="SpecRowMap"/>); the CLR
/// row is closed against the reflection census of public single-argument <c>Contains</c> in Sharpy.Core and
/// Sharpy.Stdlib (<see cref="ClrContainerRoster"/>, <see cref="AssertClrRowIsTheContainsCensus"/>), and its
/// "any <c>IEnumerable[T]</c>" arm by one representative, <c>deque[T]</c> (<see cref="EnumerableArm"/>); plus the
/// kinds an owner ruling decides before the spec table carries them (<see cref="RuledContainerKinds"/>, #2653: a
/// user class that only iterates, plain and generic, answers by iteration; one with only <c>__getitem__</c> and
/// <c>__len__</c> stays refused);</item>
/// <item><b>element optionality</b> — <c>T</c> or <c>T | None</c> for every container whose element type is the
/// payload (the container then also holds a <c>None</c> element); a fixed-slot container has no such spelling;</item>
/// <item><b>needle form</b> — the plain <c>T</c> (the control twin), <c>T | None</c>, and the literal
/// <c>None</c>;</item>
/// <item><b>operator</b> — the <see cref="BinaryOperator"/> members that are membership tests;</item>
/// <item><b>position</b> — the checker's two membership sites (a binary operator, a comparison-chain link) and
/// the two statement-level contexts that lower a binary operator differently (an <c>if</c> condition, a
/// comprehension filter);</item>
/// <item><b>observation</b> — the cell's value, or its operand evaluation ORDER (#2645): the needle and the
/// container (and a chain link's head operand) go through a tracing identity call, so the cell prints which was
/// evaluated first and then its value.</item>
/// </list>
/// <para><b>Excluded axis.</b> Declaration order: every container is declared above its use
/// (<see cref="DeclarationOrderExcluded"/>, #2249, with the program that must stay refused).</para>
/// <para><b>Expected map</b> (<see cref="Expect"/>). COMPARED cells print CPython 3.15's values (the
/// <c>.python-version</c> pin). HAND-EXPECTED cells — a typed needle the slot cannot hold (<see cref="SlotHolds"/>),
/// refused by SPY0222 under the deviation <c>membership-needle-type-mismatch</c> (owner ruling B for bool, float and
/// decimal into an <c>int</c> slot), the literal <c>None</c> into a non-optional slot (SPY0222, ruling A), and a
/// class with only <c>__getitem__</c> and <c>__len__</c> (SPY0320, owner ruling R-OA) — are counted apart in the
/// summary line. An order cell expects what its value twin expects. A chain link is its binary twins (#2648, owner
/// rulings 2026-11-13: "Refuse, as the twin", "Same as the twin"): the cell <c>x == x in c</c> expects what the
/// expression <c>x in c</c> expects, unless its head link <c>x == x</c> is refused as a binary expression — MEASURED
/// per payload, form and observation (<see cref="HeadTwin"/>), never listed — and then it is refused by the head's
/// codes (<see cref="Expect"/>). One program per payload is compiled; cells refused at compile time are attributed
/// by line, removed and the rest recompiled until it runs.</para>
/// <para><b>Known red.</b> <c>optional-membership-differential-allowlist.txt</c> (Conformance): one row per class
/// of cause, pinning its exact divergent cells and their observed Sharpy verdicts. A row holds one PART per fact
/// that has cells in it (<see cref="PinnedPart"/>: the row line is its first fact's, a <c>#@ Part</c> line opens
/// another's), each with one <c>#= cell -&gt; verdict</c> line per cell; the cells of the row's ONE cause
/// (<see cref="BulkCauseOf"/>) may instead be a bulk portion, a count and a digest, while that portion is whole
/// (<see cref="BulkCensus"/>; owner ruling 2026-11-13, #2645: "Accept, pin the residue"). A cell that starts or
/// stops diverging, or whose wrong answer changes, fails the test; the problems and a regenerated allowlist are
/// written under <c>.claude/tmp/</c>, and every divergence always is. The rows of the classes plan P93 drains
/// (<see cref="Cell.RowKey"/>): <c>Order/*</c> (#2645: the cells whose order alone diverges),
/// <c>ChainLink/*</c> (#2648), <c>UserIterable/*</c> (#2653).</para>
/// <para>Skips when no python3 ≥ 3.15 answers, except under <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI).</para>
/// </summary>
public class OptionalMembershipDifferentialTests : IntegrationTestBase
{
    public OptionalMembershipDifferentialTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    private const string AllowlistFileName = "optional-membership-differential-allowlist.txt";

    /// <summary>One compile reports every refused cell (the default cap is 100 semantic errors, so a payload with
    /// thousands of known-red cells took a compile round per hundred).</summary>
    protected override int MaxErrors => 1_000_000;

    /// <summary>The CLR row's stdlib containers (collections, ipaddress, argparse, email) are imported by every program.</summary>
    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths().Append(SharpyStdlibReference.Location);

    /// <summary>The primitives the payload axis leaves out, each with why its python twin cannot be spelled.</summary>
    private static readonly Dictionary<string, string> ExcludedPrimitives = new(StringComparer.Ordinal)
    {
        ["char"] = "python has no character type",
        ["object"] = "an `object` element slot admits every needle: no needle-type decision to make",
        ["void"] = "None itself, the needle form axis",
    };

    private enum PayloadKind { Integer, Float, Decimal, Bool, Str, Tuple, UserType, Slot }

    /// <summary>A payload: its Sharpy (and python) spelling, a member, a second member, a non-member, the CLR type a
    /// fixed needle slot must be to hold it, and — for a user-declared type — its declaration in each language (python
    /// has no <c>struct</c>, <c>interface</c>, <c>union</c> or <c>delegate</c>: its twin is a plain class or function
    /// with the same constructor spelling, and every member test compares the very object it stored).</summary>
    private sealed record Payload(string Name, PayloadKind Kind, string Member, string Other, string NonMember, Type? ClrType,
        string? SharpyDeclaration = null, string? PythonDeclaration = null)
    {
        /// <summary>A type-parameter payload: the clause its generic cells declare (<c>[T: struct]</c>). The payload
        /// type is spelled <c>T</c> inside them; <c>Name</c> is only the cell key.</summary>
        public string TypeParameters { get; init; } = "";

        /// <summary>A type-parameter payload: the type its generic cells are closed over.</summary>
        public string? Binding { get; init; }

        /// <summary>A <c>Self</c> payload: the declared type whose methods the cells and their driver are. The
        /// payload type is spelled <c>Self</c> in them and by this name in the helpers, which are not its methods.</summary>
        public string? SelfOf { get; init; }

        public bool IsTypeParameter => TypeParameters.Length > 0;

        /// <summary>The payload type as a cell's signature spells it.</summary>
        public string CellSpelling => IsTypeParameter ? "T" : SelfOf != null ? "Self" : Name;

        /// <summary>The payload type outside its scope: in the helpers and in <c>main</c>.</summary>
        public string OuterSpelling => Binding ?? SelfOf ?? Name;
    }

    private const string PlainClassTwin = "    def __init__(self, v):\n        self.v = v\n";

    /// <summary>
    /// The composite payloads: one entry per <see cref="TypeKind"/> (the compiler's kinds of user-declared type), each
    /// generating its payloads or naming why none is generated. <see cref="AssertCompositeAxisIsTheTypeKindImage"/>
    /// asserts the keys are exactly <c>Enum.GetValues&lt;TypeKind&gt;()</c> and that each Sharpy declaration declares
    /// its key's kind. An enum has two representations (an int-backed enum is a C# <c>enum</c>, a value type; a
    /// string-backed one is a class, enums.md), so both are generated.
    /// </summary>
    private static readonly Dictionary<TypeKind, (Payload[] Generated, string? Excluded)> TypeKindPayloads = new()
    {
        [TypeKind.Class] = (new[]
        {
            new Payload("K", PayloadKind.UserType, "K(1)", "K(2)", "K(5)", null,
                "class K:\n    v: int\n    def __init__(self, v: int):\n        self.v = v\n",
                "class K:\n" + PlainClassTwin),
        }, null),
        [TypeKind.Struct] = (new[]
        {
            new Payload("SP", PayloadKind.UserType, "SP(1)", "SP(2)", "SP(5)", null,
                "struct SP:\n    v: int\n    def __init__(self, v: int):\n        self.v = v\n",
                "class SP:\n" + PlainClassTwin),
        }, null),
        [TypeKind.Interface] = (new[]
        {
            new Payload("IFace", PayloadKind.UserType, "KI(1)", "KI(2)", "KI(5)", null,
                "interface IFace:\n    def tag(self) -> int:\n        return 0\n\n"
                + "class KI(IFace):\n    v: int\n    def __init__(self, v: int):\n        self.v = v\n",
                "class IFace:\n    def tag(self):\n        return 0\n\nclass KI(IFace):\n" + PlainClassTwin),
        }, null),
        [TypeKind.Enum] = (new[]
        {
            new Payload("Color", PayloadKind.UserType, "Color.RED", "Color.GREEN", "Color.BLUE", null,
                "enum Color:\n    RED = 1\n    GREEN = 2\n    BLUE = 5\n",
                "from enum import Enum\n\nclass Color(Enum):\n    RED = 1\n    GREEN = 2\n    BLUE = 5\n"),
            // A string-backed enum is CPython's StrEnum: a str, so a `str` slot holds it (enums.md).
            new Payload("Tone", PayloadKind.UserType, "Tone.A", "Tone.B", "Tone.Z", typeof(string),
                "enum Tone:\n    A = \"a\"\n    B = \"b\"\n    Z = \"z\"\n",
                "from enum import StrEnum\n\nclass Tone(StrEnum):\n    A = \"a\"\n    B = \"b\"\n    Z = \"z\"\n"),
        }, null),
        [TypeKind.Union] = (new[]
        {
            new Payload("U", PayloadKind.UserType, "U.A(1)", "U.A(2)", "U.A(5)", null,
                "union U:\n    case A(v: int)\n",
                "class U:\n    class A:\n        def __init__(self, v):\n            self.v = v\n"),
        }, null),
        [TypeKind.Delegate] = (new[]
        {
            new Payload("D", PayloadKind.UserType, "f1", "f2", "f5", null,
                "delegate D(v: int) -> int\n\ndef f1(v: int) -> int:\n    return v\n\ndef f2(v: int) -> int:\n    return v\n\n"
                + "def f5(v: int) -> int:\n    return v\n",
                "def f1(v):\n    return v\n\ndef f2(v):\n    return v\n\ndef f5(v):\n    return v\n"),
        }, null),
    };

    /// <summary>How many payloads <see cref="TypeKindPayloads"/> generates, as a literal: the totality count reads the
    /// map itself, so only this pins a payload against silent deletion.</summary>
    private const int CompositePayloadCount = 7;

    /// <summary>The (payload, form) pairs <see cref="OptionalElementExcluded"/> removes from the optional-element
    /// variant: all three forms of <c>T: struct</c> and the literal-None form of <c>T</c>.</summary>
    private const int OptionalElementExcludedForms = 4;

    /// <summary>The same pairs counted once per observation each form has (<see cref="ObservationsOf"/>): the
    /// literal-None form has no order observation, so 2 + 2 + 1 for <c>T: struct</c> and 1 for <c>T</c>.</summary>
    private const int OptionalElementExcludedObservedForms = 6;

    /// <summary>The declaration keyword of each <see cref="TypeKind"/>: a generated payload's Sharpy declaration must
    /// declare its key's kind.</summary>
    private static readonly Dictionary<TypeKind, string> TypeKindKeyword = new()
    {
        [TypeKind.Class] = "class ",
        [TypeKind.Struct] = "struct ",
        [TypeKind.Interface] = "interface ",
        [TypeKind.Enum] = "enum ",
        [TypeKind.Union] = "union ",
        [TypeKind.Delegate] = "delegate ",
    };

    private const string KDeclaration = "class K:\n    v: int\n    def __init__(self, v: int):\n        self.v = v\n";

    /// <summary>
    /// The payloads whose type is not a bare name of a declared type: a constructed generic type, a type parameter
    /// and <c>Self</c>, each by the kind of the definition it carries (<see cref="SemanticTypeCensus"/> says which
    /// are generated and why the others are not). A type parameter and <c>Self</c> have a spelling only inside the
    /// scope that declares them, so their cells are generic functions and methods (<see cref="ProgramFor"/>).
    /// </summary>
    private static readonly Payload[] ScopedPayloads =
    {
        new("GK[int]", PayloadKind.UserType, "GK[int](1)", "GK[int](2)", "GK[int](5)", null,
            "class GK[T]:\n    v: T\n    def __init__(self, v: T):\n        self.v = v\n",
            "class GK[T]:\n" + PlainClassTwin),
        new("GS[int]", PayloadKind.UserType, "GS[int](1)", "GS[int](2)", "GS[int](5)", null,
            "struct GS[T]:\n    v: T\n    def __init__(self, v: T):\n        self.v = v\n",
            "class GS[T]:\n" + PlainClassTwin),
        new("T", PayloadKind.UserType, "K(1)", "K(2)", "K(5)", null, KDeclaration, "class K:\n" + PlainClassTwin)
        { TypeParameters = "[T]", Binding = "K" },
        new("T: class", PayloadKind.UserType, "K(1)", "K(2)", "K(5)", null, KDeclaration, "class K:\n" + PlainClassTwin)
        { TypeParameters = "[T: class]", Binding = "K" },
        new("T: struct", PayloadKind.UserType, "1", "2", "5", null)
        { TypeParameters = "[T: struct]", Binding = "int" },
        new("Self@SK", PayloadKind.UserType, "SK(1)", "SK(2)", "SK(5)", null,
            "class SK:\n    v: int\n    def __init__(self, v: int):\n        self.v = v\n",
            "class SK:\n" + PlainClassTwin)
        { SelfOf = "SK" },
        new("Self@SS", PayloadKind.UserType, "SS(1)", "SS(2)", "SS(5)", null,
            "struct SS:\n    v: int\n    def __init__(self, v: int):\n        self.v = v\n",
            "class SS:\n" + PlainClassTwin)
        { SelfOf = "SS" },
    };

    /// <summary>
    /// The second structural exclusion: payloads generated without some optional-element cells, each with the
    /// forms it loses (the forms a defect outside the membership seam refuses before the test is reached; every
    /// other form of the variant is generated), the reason, the program that shows it and the one error code that
    /// program must still be refused with. <see cref="AssertOptionalElementExclusionsStillHold"/> compiles each:
    /// an exclusion whose program is no longer refused that way is red until it is deleted (its cells then join
    /// the product).
    /// </summary>
    private static readonly Dictionary<string, (Form[] Forms, string Why, string Program, string RefusedBy)> OptionalElementExcluded =
        new(StringComparer.Ordinal)
        {
            ["T: struct"] = (Enum.GetValues<Form>(), "`Container[T | None]` closed over a value type is `Container[T]` at the call "
                + "(TypeSubstitution drops the `| None` of a type parameter bound to a value type, #1797), so no container "
                + "with a None element reaches a `[T: struct]` cell: SPY0220 at the store, not at the membership test "
                + "(the value-ness work tracked by #2674; owner ruling H, 2026-11-12, moved it out of #2635)",
                "def c[T: struct](x: T, xs: list[T | None]) -> bool:\n    return x in xs\n\n"
                + "def main() -> None:\n    ys: list[int | None] = [1, None]\n    print(c[int](1, ys))\n",
                DiagnosticCodes.Semantic.TypeMismatch),
            ["T"] = (new[] { Form.NoneLiteral }, "the literal `None` has no emission at an unconstrained `T | None` "
                + "(CS1503 `<null>` to `T?`, SPY0908): a defect of unconstrained generics tracked by #2672, which "
                + "refuses the literal-None cells of the variant; its plain and `T | None` needle cells are generated",
                KDeclaration + "\ndef c[T](xs: list[T | None]) -> bool:\n    return None in xs\n\n"
                + "def main() -> None:\n    ys: list[K | None] = [K(1), None]\n    print(c[K](ys))\n",
                DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError),
        };

    private const string LaterDeclaredBag =
        "class IBag:\n    a: int\n    b: int\n    def __init__(self, a: int, b: int):\n        self.a = a\n"
        + "        self.b = b\n    def __contains__(self, v: int) -> bool:\n        return v == self.a or v == self.b\n";

    private const string LaterDeclaredUse = "def main():\n    x: int | None = None\n    print(x in IBag(1, 2))\n";

    /// <summary>
    /// The excluded axis "declaration order": the generator declares every container ABOVE its use
    /// (<see cref="ProgramFor"/>). With the <c>__contains__</c> class declared BELOW the test, no needle slot
    /// resolves at the check (a later-declared class's member types are not there yet), so the <c>T | None</c> needle
    /// is neither lifted nor refused and reaches Roslyn. The entry names the reason and its tracker, the program
    /// that shows it and the one code it must still be refused by; <c>Control</c> is the same program with
    /// the class declared above, which must run and print <c>ControlOutput</c> (so the refusal is the
    /// order's, not the program's). <see cref="DeclarationOrderExclusionProblem"/> compiles both: when the
    /// defect is fixed it is red until the entry is deleted and declaration order becomes an axis.
    /// </summary>
    private static readonly (string Why, string Program, string RefusedBy, string Control, string ControlOutput)
        DeclarationOrderExcluded =
        ("a `__contains__` class declared below its use resolves no needle slot at the membership check (a "
            + "later-declared class's member types are missing there, #2249), so `x: int | None; x in IBag(1, 2)` is "
            + "SPY0908 (CS1503 `int?` to `int`) before and after plan-4e2867: declaration order is not an axis here",
            LaterDeclaredUse + "\n" + LaterDeclaredBag,
            DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            LaterDeclaredBag + "\n" + LaterDeclaredUse,
            "False\n");

    /// <summary>
    /// The payload axis against the compiler's type model: one entry per census key
    /// (<see cref="SemanticTypeCensusKeys"/>, by reflection) — every sealed <c>SemanticType</c> record, and a record
    /// that carries a definition (a <c>TypeSymbol</c> it can be told to hold) once per <see cref="TypeKind"/> of that
    /// definition; a type parameter, whose symbol is only where it was declared, once per constraint clause the
    /// parser has and once unconstrained. Each entry says how many payloads the axis generates at that key, or why
    /// none. <see cref="AssertPayloadAxisIsTheSemanticTypeCensus"/> asserts the keys both ways and MEASURES each
    /// payload's key: it compiles a probe and reads the type the checker gave the <c>T | None</c> needle
    /// (<see cref="MeasuredCensusKey"/>), so an entry cannot claim a payload the compiler types otherwise. A record
    /// excluded as a whole takes one entry under its bare name.
    /// </summary>
    private static readonly Dictionary<string, (int Payloads, string? Excluded)> SemanticTypeCensus = new(StringComparer.Ordinal)
    {
        ["BuiltinType"] = (13, null),
        ["ConstructorReferenceType"] = (0, TransientCarrier),
        ["FunctionType"] = (0, "a function type is a delegate to C#, a reference type whatever #2674 decides: its declared "
            + "twin is the `D` payload (probed at the base: `((int) -> int) | None in list[(int) -> int]` prints False for None)"),
        ["GenericFunctionType"] = (0, TransientCarrier),
        ["GenericType/Class"] = (1, null),
        ["GenericType/Delegate"] = (0, ReferenceTwin + "`D` (probed at the base with `delegate GD[T](v: T) -> T`: False for None)"),
        ["GenericType/Enum"] = (0, "no spelling: an enum declares no type parameters (SPY0104)"),
        ["GenericType/Interface"] = (0, ReferenceTwin + "`IFace` (probed at the base with `interface IG[T]`: False for None)"),
        ["GenericType/Struct"] = (1, null),
        ["GenericType/Union"] = (0, ReferenceTwin + "`U` (probed at the base with `union GU[T]`: False for None)"),
        ["LiteralStringType"] = (0, "a compile-time subtype of str that emits as `string` (its record): the `str` payload "
            + "at run time (probed at the base: `LiteralString | None in list[str]` prints False for None)"),
        ["ModuleType"] = (0, "a module is not a value: no annotation spells its type"),
        ["NullableType"] = (0, "the needle form axis: `T | None` is the wrapper every payload of this axis is under"),
        ["OptionalType"] = (0, "`T?` is the tagged Optional, the other absence type: python has no twin, and its needle is "
            + "a typed SPY0222 with its own steer"),
        ["ResultType"] = (0, "python has no Result type"),
        ["SelfType/Class"] = (1, null),
        ["SelfType/Delegate"] = (0, "no spelling: a delegate declares no members (SPY0100)"),
        ["SelfType/Enum"] = (0, "no spelling: an enum declares no methods (SPY0101)"),
        ["SelfType/Interface"] = (0, "no spelling: `Self` cannot appear in a parameter of an interface member (SPY0497), "
            + "so no `Self | None` needle exists there"),
        ["SelfType/Struct"] = (1, null),
        ["SelfType/Union"] = (0, ReferenceTwin + "`U` (probed at the base with a `Self | None` parameter of a union method: False for None)"),
        ["TaskType"] = (0, "an awaitable: python's twin is a coroutine object, which is not a value two programs can compare"),
        ["TupleType"] = (1, null),
        ["TypeParameterType/ClassConstraint"] = (1, null),
        ["TypeParameterType/NewConstraint"] = (0, UnconstrainedTwin + " (probed at the base with `[T: new()]`: False for None)"),
        ["TypeParameterType/NotnullConstraint"] = (0, UnconstrainedTwin + " (probed at the base with `[T: notnull]`: False for None)"),
        ["TypeParameterType/StructConstraint"] = (1, null),
        ["TypeParameterType/TypeConstraint"] = (0, UnconstrainedTwin + " (probed at the base with `[T: IFace]`: False for None)"),
        ["TypeParameterType/Unconstrained"] = (1, null),
        ["UnionType"] = (0, "a placeholder record no annotation resolves to (only the symbol-cache reader builds one): "
            + "`union U` is a UserDefinedType/Union, measured here"),
        ["UnknownType"] = (0, "the error type: no annotation of a program that compiles resolves to it"),
        ["UnmappedClrType"] = (0, "a CLR type the bridge could not map: it displays and assigns as `object`, the excluded "
            + "`object` primitive's reason"),
        ["UserDefinedType/Class"] = (3, null),
        ["UserDefinedType/Delegate"] = (1, null),
        ["UserDefinedType/Enum"] = (2, null),
        ["UserDefinedType/Interface"] = (1, null),
        ["UserDefinedType/Struct"] = (2, null),
        ["UserDefinedType/Union"] = (1, null),
        ["VoidType"] = (0, "None itself: the needle form axis"),
    };

    private const string TransientCarrier = "a transient checker carrier: no binding ever has this type (its record says so)";

    private const string ReferenceTwin = "a class to C# whether generic or not, so a reference type whatever #2674 decides: "
        + "the declared twin is the payload ";

    private const string UnconstrainedTwin = "the constraint says nothing about value-ness, so `T | None` is the "
        + "unconstrained `T?` annotation: the `T` payload's twin";

    /// <summary>
    /// The fixed needle slots of the container axis that are not catalog primitives, keyed by CLR full name: the
    /// Sharpy (and python) spelling, a member of every container that holds the type, a second member, and a
    /// non-member. <see cref="AssertPayloadAxisIsTheCatalogAndTheSlots"/> asserts the keys are exactly the reflected
    /// slot types the catalog does not cover.
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Member, string Other, string NonMember)> SlotPayloads =
        new(StringComparer.Ordinal)
        {
            ["Sharpy.Bytes"] = ("bytes", "bytes([1])", "bytes([2])", "bytes([5])"),
            ["Sharpy.IPv4Address"] = ("IPv4Address", "IPv4Address(\"10.0.0.1\")", "IPv4Address(\"10.0.0.2\")",
                "IPv4Address(\"11.0.0.1\")"),
            ["Sharpy.IPv6Address"] = ("IPv6Address", "IPv6Address(\"::1\")", "IPv6Address(\"::2\")", "IPv6Address(\"1::1\")"),
        };

    private enum Container
    {
        Tuple, List, Set, FrozenSet, Dict, DictKeys, DictValues, ContainsDunder, Str, Bytes, Range,
        Counter, OrderedDict, ChainMap, DefaultDict, FrozenDict, IPv4Network, IPv6Network, ArgparseNamespace, EmailMessage,
        Deque, IterDunder, GenericIterDunder, GetItemLen,
    }

    /// <summary>
    /// The spec's "Container kinds" rows (first cell, verbatim) → the containers generated for that row, or the
    /// reason none is. Read against the spec by <see cref="AssertContainerAxisIsTheSpecTable"/>.
    /// </summary>
    private static readonly (string Row, Container[] Generated, string? Excluded)[] SpecRowMap =
    {
        ("`list[T]`, `set[T]`, `frozenset[T]`, `array[T]`", new[] { Container.List, Container.Set, Container.FrozenSet },
            "array[T]: python's array.array is a typecode-keyed stdlib class with no T | None spelling"),
        ("`dict[K, V]`", new[] { Container.Dict, Container.DictKeys, Container.DictValues }, null),
        ("`str`", new[] { Container.Str }, null),
        ("`bytes`", new[] { Container.Bytes }, null),
        ("`tuple[...]`", new[] { Container.Tuple }, null),
        ("`range(...)`", new[] { Container.Range }, null),
        ("a class with `__contains__` (declared or inherited)", new[] { Container.ContainsDunder }, null),
        ("a CLR type with a public `Contains`, or any `IEnumerable[T]`", new[]
            {
                Container.Counter, Container.OrderedDict, Container.ChainMap, Container.DefaultDict, Container.FrozenDict,
                Container.IPv4Network, Container.IPv6Network, Container.ArgparseNamespace, Container.EmailMessage,
                Container.Deque,
            },
            "the Contains arm is closed against the reflection census (ClrContainerRoster); the IEnumerable[T]-without-"
            + "Contains arm is generated by one representative (EnumerableArm)"),
    };

    /// <summary>
    /// Container kinds an owner ruling decides and the spec's table does not carry yet (#2653): the ruling, the
    /// containers generated for it, whether the ruling REFUSES the kind, and its words. R-NZ (owner, 2026-11-12:
    /// "accept as CPython, record it as a ruling"): a user class with <c>__iter__</c> and no <c>__contains__</c>,
    /// plain or generic, answers <c>in</c> by iteration, the needle checked against the iterator's element type.
    /// R-OA (owner, 2026-11-12: "maybe let's say no on __getitem__ with __len__"): a class with only
    /// <c>__len__</c> and <c>__getitem__</c> stays refused (SPY0320), where CPython answers through the legacy
    /// sequence protocol. <see cref="AssertContainerAxisIsTheSpecTable"/> asserts the spec table has no
    /// <c>__iter__</c> row: when the spec gains R-NZ's row the test is red until that entry moves into
    /// <see cref="SpecRowMap"/>.
    /// </summary>
    private static readonly (string Ruling, Container[] Generated, bool Refused, string Why)[] RuledContainerKinds =
    {
        ("R-NZ", new[] { Container.IterDunder, Container.GenericIterDunder }, false,
            "a user class with `__iter__` and no `__contains__` answers `in` / `not in` by iteration (#2653); "
            + "the class is declared over the payload (IterDunder) and as a generic class constructed over it "
            + "(GenericIterDunder: a GenericType to the checker)"),
        ("R-OA", new[] { Container.GetItemLen }, true,
            "a class with only `__len__` + `__getitem__` stays refused for `in` (SPY0320): the index need not be an "
            + "integer, and an integer index need not be dense (#2653)"),
    };

    /// <summary>The containers a ruling refuses (<see cref="RuledContainerKinds"/>): every cell expects SPY0320.</summary>
    private static bool IsRefusedByRuling(Container container) =>
        RuledContainerKinds.Any(k => k.Refused && k.Generated.Contains(container));

    /// <summary>The user classes that only iterate (R-NZ): their cells have rows of their own.</summary>
    private static bool IsUserIterable(Container container) =>
        RuledContainerKinds.Any(k => !k.Refused && k.Generated.Contains(container));

    /// <summary>
    /// The spec row's "any <c>IEnumerable[T]</c>" arm: a receiver with no <c>Contains</c> of its own. Its needle is
    /// decided by the checker's generic-container arm (<c>ResolveMembershipElementType</c>: a <c>GenericType</c> →
    /// <c>InferIterableElementType</c>; <c>ProtocolMembership</c> admits it through <c>IEnumerable&lt;T&gt;</c>) and
    /// emitted as LINQ's <c>Enumerable.Contains</c>. Every such receiver Sharpy spells is a <c>GenericType</c> over its
    /// element and reaches that same arm and the same emission, so one representative stands for it: <c>deque[T]</c>
    /// (an <c>Iterator[T]</c> from <c>iter(xs)</c> was probed at the base: the same SPY0222 for a <c>T | None</c>
    /// needle, the same answers otherwise). <see cref="AssertClrRowIsTheContainsCensus"/> asserts it has no
    /// <c>Contains</c> and is an <c>IEnumerable&lt;T&gt;</c>.
    /// </summary>
    private static readonly (string ClrName, Container Generated, string Why) EnumerableArm =
        ("Sharpy.Deque`1", Container.Deque, "the IEnumerable[T]-without-Contains arm: one GenericType checker arm "
            + "(InferIterableElementType) and one LINQ emission for every such receiver");

    /// <summary>The element-typed containers, as a literal roster: the totality count reads its size, and
    /// <see cref="IsElementTyped"/> (which the generator uses) is asserted equal to it.</summary>
    private static readonly Container[] ElementTypedRoster =
    {
        Container.Tuple, Container.List, Container.Set, Container.FrozenSet, Container.Dict, Container.DictKeys,
        Container.DictValues, Container.ContainsDunder, Container.Counter, Container.OrderedDict, Container.ChainMap,
        Container.DefaultDict, Container.FrozenDict, Container.Deque, Container.IterDunder, Container.GenericIterDunder,
        Container.GetItemLen,
    };

    /// <summary>Element-typed containers exempt from the Sharpy half of the optional-element control (a compared
    /// <c>T | None</c> needle that Sharpy answers <c>True False True</c> today), each with its reason and issue.</summary>
    private static readonly Dictionary<Container, string> OptionalElementControlExcluded = new()
    {
        [Container.Counter] = "#2651: Counter[T | None] with a None element throws ArgumentNullException at construction",
        [Container.IterDunder] = "#2653: a class with `__iter__` and no `__contains__` is refused (SPY0320) until R-NZ lands",
        [Container.GenericIterDunder] = "#2653: a generic class with `__iter__` and no `__contains__` is refused (SPY0320) "
            + "until R-NZ lands",
    };

    /// <summary>
    /// The CLR row's census: every public type in Sharpy.Core and Sharpy.Stdlib with a public instance single-argument
    /// <c>Contains</c> (a generic definition by its open name) → the container that generates it, or why none does.
    /// <see cref="AssertClrRowIsTheContainsCensus"/> asserts the keys are exactly the reflected set.
    /// </summary>
    private static readonly Dictionary<string, (Container? Generated, string? Excluded)> ClrContainerRoster =
        new(StringComparer.Ordinal)
        {
            ["Sharpy.List`1"] = (Container.List, null),
            ["Sharpy.Set`1"] = (Container.Set, null),
            ["Sharpy.FrozenSet`1"] = (Container.FrozenSet, null),
            ["Sharpy.Dict`2"] = (Container.Dict, null),
            ["Sharpy.DictKeyView`2"] = (Container.DictKeys, null),
            ["Sharpy.DictValuesView`2"] = (Container.DictValues, null),
            ["Sharpy.DictItemsView`2"] = (null, "its slot is the (K, V) pair, which no payload of this axis is: a pair "
                + "payload would need its own value type per key; the pair is the dict's slot, generated by Dict"),
            ["Sharpy.RangeIterator"] = (Container.Range, null),
            ["Sharpy.IDict"] = (null, ObjectSlot),
            ["Sharpy.IList"] = (null, ObjectSlot),
            ["Sharpy.ISet"] = (null, ObjectSlot),
            ["Sharpy.Bytes"] = (Container.Bytes, null),
            ["Sharpy.FrozenDict`2"] = (Container.FrozenDict, null),
            ["Sharpy.Counter`1"] = (Container.Counter, null),
            ["Sharpy.OrderedDict`2"] = (Container.OrderedDict, null),
            ["Sharpy.ChainMap`2"] = (Container.ChainMap, null),
            ["Sharpy.DefaultDict`2"] = (Container.DefaultDict, null),
            ["Sharpy.IPv4Network"] = (Container.IPv4Network, null),
            ["Sharpy.IPv6Network"] = (Container.IPv6Network, null),
            ["Sharpy.Namespace"] = (Container.ArgparseNamespace, null),
            ["Sharpy.EmailMessage"] = (Container.EmailMessage, null),
        };

    private const string ObjectSlot = "a non-generic runtime interface whose Contains(object) slot admits every needle: no "
        + "needle-type decision to make (as for the excluded `object` primitive)";

    private static IEnumerable<Type> SharpyTypes() =>
        new[] { typeof(SharpyRT::Sharpy.Builtins).Assembly, typeof(SharpyStdlib::Sharpy.EmailMessage).Assembly }
            .Distinct()
            .SelectMany(a => a.GetExportedTypes());

    /// <summary>The public instance single-argument <c>Contains</c> parameter types of a CLR type.</summary>
    private static IReadOnlyList<Type> ContainsSlots(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Contains" && m.GetParameters().Length == 1)
            .Select(m => m.GetParameters()[0].ParameterType)
            .Distinct()
            .ToList();

    /// <summary>The CLR type a container's membership test reaches: the roster's, <c>str</c>'s, or none (a tuple
    /// projection; a user class).</summary>
    private static Type? ClrTypeOf(Container container)
    {
        if (container == Container.Str)
            return typeof(string);
        if (container is Container.Tuple or Container.ContainsDunder || IsUserClass(container))
            return null;
        var name = container == EnumerableArm.Generated
            ? EnumerableArm.ClrName
            : ClrContainerRoster.Single(r => r.Value.Generated == container).Key;
        return SharpyTypesByName.Value[name];
    }

    private static readonly Lazy<Dictionary<string, Type>> SharpyTypesByName =
        new(() => SharpyTypes().ToDictionary(t => t.FullName!, StringComparer.Ordinal));

    /// <summary>A container whose element type is the payload, so it has a <c>T | None</c> element spelling: a tuple,
    /// a user class, or a generic CLR type.</summary>
    private static bool IsElementTyped(Container container) =>
        container is Container.Tuple or Container.ContainsDunder || IsUserClass(container)
        || ClrTypeOf(container) is { IsGenericTypeDefinition: true };

    /// <summary>The ruled kinds are user classes declared over the payload: no CLR type, an element type.</summary>
    private static bool IsUserClass(Container container) => RuledContainerKinds.Any(k => k.Generated.Contains(container));

    private enum Form { Plain, Optional, NoneLiteral }

    private enum Position { Expression, IfCondition, ComprehensionFilter, ChainLink }

    /// <summary>What a cell observes: its value, or the order its operands are evaluated in and then its value
    /// (#2645: CPython evaluates the needle before the container).</summary>
    private enum Observation { Value, Order }

    /// <summary>The allowlist rows of the cells whose operand order alone diverges (the order fact's).</summary>
    private const string OrderRowPrefix = "Order/";

    /// <summary>The allowlist rows of a chain link, and of a user class that only iterates.</summary>
    private const string ChainLinkRowPrefix = "ChainLink/";
    private const string UserIterableRowPrefix = "UserIterable/";

    /// <summary>The forms the order observation leaves out, each with its reason.</summary>
    private static readonly Dictionary<Form, string> OrderExcludedForms = new()
    {
        [Form.NoneLiteral] = "a literal needle has no evaluation to order: only the container would be traced",
    };

    private static IEnumerable<Observation> ObservationsOf(Form form) =>
        Enum.GetValues<Observation>().Where(o => o == Observation.Value || !OrderExcludedForms.ContainsKey(form));

    private sealed record Cell(int Id, Payload Payload, Container Container, bool OptionalElement, Form Form,
        BinaryOperator Operator, Position Position, Observation Observation)
    {
        public string ContainerName => OptionalElement ? $"{Container}OfOptional" : Container.ToString();
        public string Key => $"{Payload.Name}/{ContainerName}/{Form}/{OperatorSpelling(Operator)}/{Position}"
            + (Observation == Observation.Value ? "" : $"/{Observation}");
        /// <summary>The allowlist row: the container × whether the needle can be None (<c>T | None</c> and the literal
        /// <c>None</c> share one row, as they share the rulings; an optional element has its own row, since a defect
        /// that only an optional element shows is a different class). Rows pin their exact cells, so the key's
        /// coarseness hides nothing.
        /// <para>The classes plan P93 drains have rows of their own, keyed by cause: a cell whose operand ORDER alone
        /// diverges (<paramref name="orderAlone"/>: it ran and gave CPython's answers, #2645); then a chain link
        /// (#2648); then a user class that only iterates (#2653). An order cell that diverges for another reason is
        /// a cell of its value twin's row, in the order fact's part of it.</para></summary>
        public string RowKey(bool orderAlone) => (orderAlone ? OrderRowPrefix
            : Position == Position.ChainLink ? ChainLinkRowPrefix
            : IsUserIterable(Container) ? UserIterableRowPrefix
            : ContainerName + "/") + (Form == Form.Plain ? "Plain" : "NoneNeedle");
    }

    private static string OperatorSpelling(BinaryOperator op) => op == BinaryOperator.In ? "in" : "not in";

    private static readonly BinaryOperator[] Operators =
        Enum.GetValues<BinaryOperator>().Where(o => o is BinaryOperator.In or BinaryOperator.NotIn).ToArray();

    /// <summary>One canonical Sharpy name per CLR primitive (aliases dropped), ordinal-sorted.</summary>
    private static IReadOnlyList<string> CanonicalPrimitives() =>
        PrimitiveCatalog.GetAllPrimitives()
            .Where(p => PrimitiveCatalog.GetByClrType(p.Info.ClrType)?.SharpyName == p.Name)
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    private static List<Payload> GeneratePayloads()
    {
        var payloads = new List<Payload>();
        foreach (var name in CanonicalPrimitives())
        {
            if (ExcludedPrimitives.ContainsKey(name))
                continue;
            var info = PrimitiveCatalog.GetByName(name)!;
            var clr = info.ClrType;
            var payload = info.Kind switch
            {
                PrimitiveCatalog.NumericKind.SignedInteger or PrimitiveCatalog.NumericKind.UnsignedInteger
                    => new Payload(name, PayloadKind.Integer, "1", "2", "5", clr),
                PrimitiveCatalog.NumericKind.FloatingPoint => new Payload(name, PayloadKind.Float, "1.5", "2.5", "5.5", clr),
                // The python twin spells it through the prelude's `from decimal import Decimal as decimal`.
                PrimitiveCatalog.NumericKind.Decimal
                    => new Payload(name, PayloadKind.Decimal, "decimal(1)", "decimal(2)", "decimal(5)", clr),
                _ when name == "bool" => new Payload(name, PayloadKind.Bool, "True", "True", "False", clr),
                _ when name == "str" => new Payload(name, PayloadKind.Str, "\"a\"", "\"b\"", "\"z\"", clr),
                // A primitive the generator has no literals for: the totality assertion names it.
                _ => null,
            };
            if (payload != null)
                payloads.Add(payload);
        }
        payloads.Add(new Payload("tuple[int, int]", PayloadKind.Tuple, "(1, 2)", "(3, 4)", "(5, 6)", typeof(ValueTuple<int, int>)));
        foreach (var kind in Enum.GetValues<TypeKind>())
            if (TypeKindPayloads.TryGetValue(kind, out var entry))
                payloads.AddRange(entry.Generated);
        payloads.AddRange(ScopedPayloads);
        var types = SharpyTypesByName.Value;
        foreach (var (clrName, slot) in SlotPayloads.OrderBy(s => s.Key, StringComparer.Ordinal))
            payloads.Add(new Payload(slot.Name, PayloadKind.Slot, slot.Member, slot.Other, slot.NonMember,
                types.GetValueOrDefault(clrName)));
        return payloads;
    }

    private static Container[] Containers() => SpecRowMap.SelectMany(m => m.Generated)
        .Concat(RuledContainerKinds.SelectMany(k => k.Generated)).OrderBy(c => c).ToArray();

    /// <summary>The full product of the axes; the two structural exclusions are an optional element on a fixed-slot
    /// container, which has no spelling, and the forms <see cref="OptionalElementExcluded"/> names for a payload. A needle
    /// the container's slot cannot hold is a CELL (its expectation is the spec's refusal), never a skip.</summary>
    private static List<Cell> GenerateCells(IReadOnlyList<Payload> payloads)
    {
        var containers = Containers();
        var cells = new List<Cell>();
        foreach (var payload in payloads)
            foreach (var container in containers)
                foreach (var optionalElement in IsElementTyped(container) ? new[] { false, true } : new[] { false })
                    foreach (var form in Enum.GetValues<Form>())
                    {
                        if (optionalElement && OptionalElementExcluded.TryGetValue(payload.Name, out var excluded)
                            && excluded.Forms.Contains(form))
                            continue;
                        foreach (var op in Operators)
                            foreach (var position in Enum.GetValues<Position>())
                                foreach (var observation in ObservationsOf(form))
                                    cells.Add(new Cell(cells.Count, payload, container, optionalElement, form, op, position,
                                        observation));
                    }
        return cells;
    }

    /// <summary>
    /// The head link of a chain-link cell as a binary expression of its own: <c>x == x</c> over the payload in the
    /// cell's needle form (<c>None is None</c> for the literal), traced as the cell traces it when the observation
    /// is the order. One per payload, form and observation, compiled in the payload's program (never called): its
    /// verdict is what the chain's head link must get (<see cref="Expect"/>), so the nine payloads whose
    /// <c>==</c> the binary form refuses, and the ones that reach Roslyn, are measured and not listed.
    /// </summary>
    private sealed record HeadTwin(int Id, Payload Payload, Form Form, Observation Observation);

    /// <summary>One head twin per payload, form and observation of the form, numbered after the cells.</summary>
    private static List<HeadTwin> GenerateHeadTwins(IReadOnlyList<Payload> payloads, int firstId)
    {
        var twins = new List<HeadTwin>();
        foreach (var payload in payloads)
            foreach (var form in Enum.GetValues<Form>())
                foreach (var observation in ObservationsOf(form))
                    twins.Add(new HeadTwin(firstId + twins.Count, payload, form, observation));
        return twins;
    }

    /// <summary>The head link of a chain-link cell, the one spelling its cell and its twin share.</summary>
    private static string HeadLink(Form form, Observation observation, int id) => form == Form.NoneLiteral
        // The literal None's spelling is `None is None` (Sharpy has no `None == None`).
        ? "None is None"
        : observation == Observation.Order ? $"tr({id}, \"h\", x) == tr({id}, \"n\", x)" : "x == x";

    /// <summary>The container expression. An optional element types <c>a</c>, <c>c</c> and <c>z</c> as
    /// <c>T | None</c> and adds <c>z</c> (always <c>None</c>) as an element, so <c>None</c> is a member.</summary>
    private static string ContainerSource(Cell cell, string bagArguments = "")
    {
        var o = cell.OptionalElement;
        var items = o ? "a, c, z" : "a, c";
        return cell.Container switch
        {
            Container.Tuple => $"({items})",
            Container.List => $"[{items}]",
            Container.Set => $"{{{items}}}",
            Container.FrozenSet => $"frozenset([{items}])",
            Container.Dict => o ? "{a: 0, c: 1, z: 2}" : "{a: 0, c: 1}",
            Container.DictKeys => o ? "{a: 0, c: 1, z: 2}.keys()" : "{a: 0, c: 1}.keys()",
            Container.DictValues => o ? "{0: a, 1: c, 2: z}.values()" : "{0: a, 1: c}.values()",
            Container.ContainsDunder => o ? $"OBag{bagArguments}(a, c, z)" : $"Bag{bagArguments}(a, c)",
            Container.IterDunder => o ? $"OIterBag{bagArguments}(a, c, z)" : $"IterBag{bagArguments}(a, c)",
            Container.GetItemLen => o ? $"OSeqBag{bagArguments}(a, c, z)" : $"SeqBag{bagArguments}(a, c)",
            Container.GenericIterDunder => $"make_GIter{(o ? "_o" : "")}({items})",
            Container.Str => "\"ab\"",
            Container.Bytes => "bytes([1, 2])",
            Container.Range => "range(1, 3)",
            // Built by a helper from typed locals: an explicit type argument list on a constructor call does not take
            // a `T | None` argument (SPY0342), and the helpers spell both variants the same way.
            Container.Counter or Container.OrderedDict or Container.ChainMap or Container.DefaultDict or Container.FrozenDict
                or Container.Deque
                => $"make_{cell.Container}{(o ? "_o" : "")}({items})",
            Container.IPv4Network => "IPv4Network(\"10.0.0.0/8\")",
            Container.IPv6Network => "IPv6Network(\"::/112\")",
            Container.ArgparseNamespace => "make_ns()",
            Container.EmailMessage => "make_msg()",
            _ => throw new InvalidOperationException(cell.Container.ToString()),
        };
    }

    private sealed record Program(string Source, Dictionary<int, int> CellByLine);

    /// <summary>A type-parameter payload's container, as its generic cell receives it: the parameter's type over
    /// the element <paramref name="e"/> (<c>T</c> or <c>T | None</c>), the expression the cell tests, and the argument
    /// the feeder builds over the binding. A fixed-slot container does not depend on <c>T</c>: no parameter.</summary>
    private static (string? Parameter, string Test, string? Argument) GenericContainer(Cell cell, string e, string bagArguments)
    {
        var o = cell.OptionalElement;
        var source = ContainerSource(cell, bagArguments);
        return cell.Container switch
        {
            Container.Tuple => (o ? $"tuple[{e}, {e}, {e}]" : $"tuple[{e}, {e}]", "xs", source),
            Container.List => ($"list[{e}]", "xs", source),
            Container.Set => ($"set[{e}]", "xs", source),
            Container.FrozenSet => ($"frozenset[{e}]", "xs", source),
            Container.Dict => ($"dict[{e}, int]", "xs", source),
            Container.DictKeys => ($"dict[{e}, int]", "xs.keys()", ContainerSource(cell with { Container = Container.Dict })),
            Container.DictValues => ($"dict[int, {e}]", "xs.values()", o ? "{0: a, 1: c, 2: z}" : "{0: a, 1: c}"),
            Container.ContainsDunder => (o ? "OBag[T]" : "Bag[T]", "xs", source),
            Container.IterDunder => (o ? "OIterBag[T]" : "IterBag[T]", "xs", source),
            Container.GetItemLen => (o ? "OSeqBag[T]" : "SeqBag[T]", "xs", source),
            Container.GenericIterDunder => ($"GIterBag[{e}]", "xs", source),
            Container.Counter => ($"Counter[{e}]", "xs", source),
            Container.OrderedDict => ($"OrderedDict[{e}, int]", "xs", source),
            Container.ChainMap => ($"ChainMap[{e}, int]", "xs", source),
            Container.DefaultDict => ($"defaultdict[{e}, int]", "xs", source),
            Container.FrozenDict => ($"frozendict[{e}, int]", "xs", source),
            Container.Deque => ($"deque[{e}]", "xs", source),
            Container.Str or Container.Bytes or Container.Range or Container.IPv4Network or Container.IPv6Network
                or Container.ArgparseNamespace or Container.EmailMessage => (null, source, null),
            _ => throw new InvalidOperationException(cell.Container.ToString()),
        };
    }

    /// <summary>One program per payload over the cells still active; every line of a cell's function and of its
    /// driver loop is attributed to it, so a refusal anywhere in either removes that cell alone.
    /// <para>A payload whose type has a spelling only inside a scope is laid out in that scope.
    /// A type parameter: the cell <c>c</c><i>N</i> is a generic function over the payload's clause that takes the
    /// needle and the container (<c>x: T | None, xs: list[T]</c>); its feeder <c>f</c><i>N</i> has the signature of
    /// a bare payload's cell over the binding, builds the container the same way and calls
    /// <c>c</c><i>N</i><c>[Binding]</c>; the driver calls the feeder. The <c>__contains__</c> classes are generic
    /// over their element, so <c>Bag[T]</c>'s slot is <c>T</c>. <c>Self</c>: the cells and the driver are methods of
    /// the payload's declaration; the helpers, which are not, spell the declared type. Python's twin is the same
    /// code without clauses and type-argument lists.</para></summary>
    private static Program ProgramFor(Payload payload, IReadOnlyList<Cell> cells, bool python = false,
        IReadOnlyList<HeadTwin>? twins = null)
    {
        var lines = new List<string>();
        var cellByLine = new Dictionary<int, int>();
        void Add(string line, int? cellId = null)
        {
            lines.Add(line);
            if (cellId is { } id)
                cellByLine[lines.Count] = id;
        }

        var p = payload.OuterSpelling;
        var t = payload.CellSpelling;
        var generic = payload.IsTypeParameter;
        var method = payload.SelfOf != null;
        var scope = method ? "    " : "";
        var self = method ? "self, " : "";
        // The __contains__ classes: over the payload type, or (a type parameter) generic over their element.
        var bagClause = generic && !python ? "[E]" : "";
        var bagElement = generic ? "E" : p;
        var bagArguments = generic && !python ? $"[{p}]" : "";

        void AddTest(string indent, Cell cell, string needle, string container)
        {
            var op = OperatorSpelling(cell.Operator);
            // The order observation: each operand goes through the tracing identity, which prints the cell and the
            // operand's side before it returns it (`n` the needle, `c` the container, `h` a chain's head operand).
            var ordered = cell.Observation == Observation.Order;
            string Traced(string side, string operand) => ordered ? $"tr({cell.Id}, \"{side}\", {operand})" : operand;
            needle = Traced("n", needle);
            container = Traced("c", container);
            var test = $"{needle} {op} {container}";
            // A located first statement: code the emitter hoists above the test (a comprehension) carries no
            // #line of its own, and a Roslyn error there maps to the last located line — this cell's, not the
            // previous function's.
            Add($"{indent}cell: int = {cell.Id}", cell.Id);
            switch (cell.Position)
            {
                case Position.Expression:
                    Add($"{indent}return {test}", cell.Id);
                    break;
                case Position.IfCondition:
                    Add($"{indent}if {test}:", cell.Id);
                    Add($"{indent}    return True", cell.Id);
                    Add($"{indent}return False", cell.Id);
                    break;
                case Position.ComprehensionFilter:
                    Add($"{indent}return len([0 for _ in [0] if {test}]) == 1", cell.Id);
                    break;
                case Position.ChainLink:
                    // The first link reads the needle twice: the head link, then the membership link.
                    Add($"{indent}return {HeadLink(cell.Form, cell.Observation, cell.Id)} {op} {container}", cell.Id);
                    break;
            }
        }

        void AddCells()
        {
            foreach (var cell in cells)
            {
                var needle = cell.Form == Form.NoneLiteral ? "None" : "x";
                // The feeder's (a bare payload's cell's) parameters: over the binding for a type parameter.
                var f = generic ? p : t;
                var elements = cell.OptionalElement ? $"a: {f} | None, c: {f} | None, z: {f} | None" : $"a: {f}, c: {f}";
                var parameters = cell.Form switch
                {
                    Form.Plain => $"x: {f}, {elements}",
                    Form.Optional => $"x: {f} | None, {elements}",
                    _ => elements,
                };
                if (!generic)
                {
                    Add($"{scope}def c{cell.Id}({self}{parameters}) -> bool:", cell.Id);
                    AddTest(scope + "    ", cell, needle, ContainerSource(cell));
                    Add("");
                    continue;
                }
                var (parameter, test, argument) = GenericContainer(cell, cell.OptionalElement ? "T | None" : "T", bagArguments);
                var received = new[]
                {
                    cell.Form == Form.Plain ? "x: T" : cell.Form == Form.Optional ? "x: T | None" : null,
                    parameter != null ? $"xs: {parameter}" : null,
                }.OfType<string>();
                var passed = new[] { cell.Form == Form.NoneLiteral ? null : "x", argument }.OfType<string>();
                Add($"def c{cell.Id}{(python ? "" : payload.TypeParameters)}({string.Join(", ", received)}) -> bool:", cell.Id);
                AddTest("    ", cell, needle, test);
                Add($"def f{cell.Id}({parameters}) -> bool:", cell.Id);
                Add($"    return c{cell.Id}{(python ? "" : $"[{p}]")}({string.Join(", ", passed)})", cell.Id);
                Add("");
            }
            // The head twins: a chain cell's head link alone, in the scope and over the needle its cell has.
            foreach (var twin in twins ?? Array.Empty<HeadTwin>())
            {
                var x = twin.Form switch
                {
                    Form.Plain => $"x: {t}",
                    Form.Optional => $"x: {t} | None",
                    _ => null,
                };
                var received = string.Join(", ", new[] { method ? "self" : null, x }.OfType<string>());
                Add($"{scope}def h{twin.Id}{(x != null ? payload.TypeParameters : "")}({received}) -> bool:", twin.Id);
                Add($"{scope}    return {HeadLink(twin.Form, twin.Observation, twin.Id)}", twin.Id);
                Add("");
            }
        }

        // The driver's body: a member, a non-member and (for a T | None needle) None through every cell. `e` is the
        // payload type as the driver's scope spells it; `callee` prefixes a cell's number.
        void AddDriverBody(string indent, string e, string callee)
        {
            Add($"{indent}plain: list[{e}] = [a, n]");
            Add($"{indent}optional: list[{e} | None] = [a, n, None]");
            foreach (var cell in cells)
            {
                var id = cell.Id.ToString(CultureInfo.InvariantCulture);
                var elements = cell.OptionalElement ? "a, c, None" : "a, c";
                var (loop, call) = cell.Form switch
                {
                    Form.Plain => ($"{indent}for v in plain:", $"{callee}{id}(v, {elements})"),
                    Form.Optional => ($"{indent}for v in optional:", $"{callee}{id}(v, {elements})"),
                    _ => ($"{indent}for v in [0]:", $"{callee}{id}({elements})"),
                };
                Add(loop, cell.Id);
                Add($"{indent}    try:", cell.Id);
                Add($"{indent}        print({id}, {call})", cell.Id);
                Add($"{indent}    except TypeError:", cell.Id);
                Add($"{indent}        print({id}, \"TypeError\")", cell.Id);
                Add($"{indent}    except AttributeError:", cell.Id);
                Add($"{indent}        print({id}, \"AttributeError\")", cell.Id);
                Add($"{indent}    except Exception:", cell.Id);
                Add($"{indent}        print({id}, \"other\")", cell.Id);
            }
        }

        void AddBags()
        {
            Add($"class Bag{bagClause}:");
            Add($"    items: list[{bagElement}]");
            Add($"    def __init__(self, a: {bagElement}, c: {bagElement}):");
            Add("        self.items = [a, c]");
            Add($"    def __contains__(self, item: {bagElement}) -> bool:");
            Add("        return item in self.items");
            Add("");
            Add($"class OBag{bagClause}:");
            Add($"    items: list[{bagElement} | None]");
            Add($"    def __init__(self, a: {bagElement} | None, c: {bagElement} | None, z: {bagElement} | None):");
            Add("        self.items = [a, c, z]");
            Add($"    def __contains__(self, item: {bagElement} | None) -> bool:");
            Add("        return item in self.items");
            Add("");
            // The ruled kinds (#2653). A class that only iterates, over the payload and over `T | None`:
            foreach (var (name, element, parameters, items) in new[]
                {
                    ("IterBag", bagElement, $"a: {bagElement}, c: {bagElement}", "[a, c]"),
                    ("OIterBag", $"{bagElement} | None",
                        $"a: {bagElement} | None, c: {bagElement} | None, z: {bagElement} | None", "[a, c, z]"),
                })
            {
                Add($"class {name}{bagClause}:");
                Add($"    items: list[{element}]");
                Add($"    def __init__(self, {parameters}):");
                Add($"        self.items = {items}");
                Add($"    def __iter__(self) -> Iterator[{element}]:");
                Add("        return iter(self.items)");
                Add("");
            }
            // The same class as a generic one (python has no type-parameter clause to drop here: one class).
            Add($"class GIterBag{(python ? "" : "[G]")}:");
            Add("    items: list[G]");
            Add("    def __init__(self, items: list[G]):");
            Add("        self.items = items");
            Add("    def __iter__(self) -> Iterator[G]:");
            Add("        return iter(self.items)");
            Add("");
            // A class with only the legacy sequence protocol (owner ruling R-OA: refused).
            foreach (var (name, element, parameters, items) in new[]
                {
                    ("SeqBag", bagElement, $"a: {bagElement}, c: {bagElement}", "[a, c]"),
                    ("OSeqBag", $"{bagElement} | None",
                        $"a: {bagElement} | None, c: {bagElement} | None, z: {bagElement} | None", "[a, c, z]"),
                })
            {
                Add($"class {name}{bagClause}:");
                Add($"    items: list[{element}]");
                Add($"    def __init__(self, {parameters}):");
                Add($"        self.items = {items}");
                Add("    def __len__(self) -> int:");
                Add("        return len(self.items)");
                Add($"    def __getitem__(self, i: int) -> {element}:");
                Add("        return self.items[i]");
                Add("");
            }
        }

        Add("from argparse import ArgumentParser, Namespace");
        Add("from collections import ChainMap, Counter, OrderedDict, defaultdict, deque");
        Add("from email import EmailMessage");
        Add("from ipaddress import IPv4Address, IPv4Network, IPv6Address, IPv6Network");
        Add("");
        // The tracing identity of the order observation: prints the cell and the operand's side, returns the operand.
        Add($"def tr{(python ? "" : "[V]")}(cell: int, side: str, v: V) -> V:");
        Add("    print(cell, side)");
        Add("    return v");
        Add("");
        // A Self payload's cells are in its declaration, so the __contains__ classes go above it: an argument checked
        // against a __contains__ class declared BELOW the test is not refused today but reaches Roslyn (#2249: a
        // later-declared class's member types are not there at the check), and declaration order is not an axis here.
        if (method)
            AddBags();
        if ((python ? payload.PythonDeclaration : payload.SharpyDeclaration) is { } declaration)
        {
            foreach (var line in declaration.TrimEnd('\n').Split('\n'))
                Add(line);
            if (method)
            {
                // A Self payload: the cells and the driver are methods of the declaration just opened.
                Add("");
                AddCells();
                Add($"    def drive(self, a: {t}, c: {t}, n: {t}) -> None:");
                AddDriverBody("        ", t, "self.c");
            }
            Add("");
        }
        if (!method)
            AddBags();
        // The CLR row's generic containers, each over `a, c` (element T) and `a, c, z` (element T | None, z = None).
        foreach (var optional in new[] { false, true })
        {
            var e = optional ? $"{p} | None" : p;
            var sig = optional ? $"a: {e}, c: {e}, z: {e}" : $"a: {e}, c: {e}";
            var suffix = optional ? "_o" : "";
            var items = optional ? "a, c, z" : "a, c";
            var dict = optional ? "{a: 0, c: 1, z: 2}" : "{a: 0, c: 1}";
            Add($"def make_Counter{suffix}({sig}) -> Counter[{e}]:");
            Add($"    xs: list[{e}] = [{items}]");
            Add($"    r: Counter[{e}] = Counter(xs)");
            Add("    return r");
            Add($"def make_Deque{suffix}({sig}) -> deque[{e}]:");
            Add($"    xs: list[{e}] = [{items}]");
            Add($"    r: deque[{e}] = deque(xs)");
            Add("    return r");
            Add($"def make_GIter{suffix}({sig}) -> GIterBag[{e}]:");
            Add($"    xs: list[{e}] = [{items}]");
            Add($"    r: GIterBag[{e}] = GIterBag(xs)");
            Add("    return r");
            Add($"def make_OrderedDict{suffix}({sig}) -> OrderedDict[{e}, int]:");
            Add($"    d: dict[{e}, int] = {dict}");
            Add($"    r: OrderedDict[{e}, int] = OrderedDict(d)");
            Add("    return r");
            Add($"def make_FrozenDict{suffix}({sig}) -> frozendict[{e}, int]:");
            Add($"    d: dict[{e}, int] = {dict}");
            Add($"    r: frozendict[{e}, int] = frozendict(d)");
            Add("    return r");
            Add($"def make_ChainMap{suffix}({sig}) -> ChainMap[{e}, int]:");
            Add($"    first: dict[{e}, int] = {{a: 0}}");
            Add($"    rest: dict[{e}, int] = {(optional ? "{c: 1, z: 2}" : "{c: 1}")}");
            Add($"    r: ChainMap[{e}, int] = ChainMap(first, rest)");
            Add("    return r");
            Add($"def make_DefaultDict{suffix}({sig}) -> defaultdict[{e}, int]:");
            Add($"    r: defaultdict[{e}, int] = defaultdict(int)");
            Add("    r[a] = 0");
            Add("    r[c] = 1");
            if (optional)
                Add("    r[z] = 2");
            Add("    return r");
            Add("");
        }
        // A Namespace holding "a" and "b"; positionals, as Sharpy's argparse takes no `--` options here.
        Add("def make_ns() -> Namespace:");
        Add("    parser = ArgumentParser()");
        Add("    parser.add_argument(\"a\")");
        Add("    parser.add_argument(\"b\")");
        Add("    return parser.parse_args([\"1\", \"2\"])");
        Add("");
        Add("def make_msg() -> EmailMessage:");
        Add("    m = EmailMessage()");
        Add("    m.add_header(\"a\", \"1\")");
        Add("    m.add_header(\"b\", \"2\")");
        Add("    return m");
        Add("");

        if (!method)
            AddCells();

        Add("def main() -> None:");
        Add($"    a: {p} = {payload.Member}");
        Add($"    c: {p} = {payload.Other}");
        Add($"    n: {p} = {payload.NonMember}");
        if (method)
            Add("    a.drive(a, c, n)");
        else
            AddDriverBody("    ", p, generic ? "f" : "c");
        Add("    print(\"done\")");
        return new Program(string.Join("\n", lines) + "\n", cellByLine);
    }

    private static Dictionary<int, List<string>> ParseLines(string stdout)
    {
        var byId = new Dictionary<int, List<string>>();
        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var space = line.IndexOf(' ');
            if (space > 0 && int.TryParse(line[..space], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                if (!byId.TryGetValue(id, out var values))
                    byId[id] = values = new List<string>();
                values.Add(line[(space + 1)..]);
            }
        }
        return byId;
    }

    /// <summary>Compile rounds per payload: checker refusals, then codegen refusals, then Roslyn's (SPY0908).</summary>
    private const int MaxRounds = 40;

    private sealed record SharpyVerdict(IReadOnlyList<string>? Values, IReadOnlySet<string>? RefusedBy);

    /// <summary>Compiles the payload's program, removing each cell a refusal is attributed to, until it runs.</summary>
    private Dictionary<int, SharpyVerdict> RunSharpy(Payload payload, IReadOnlyList<Cell> cells,
        IReadOnlyList<HeadTwin> twins, List<string> harnessFailures)
        => RunRounds(payload.Name, cells.Select(c => c.Id).Concat(twins.Select(t => t.Id)).ToList(),
            active => ProgramFor(payload, cells.Where(c => active.Contains(c.Id)).ToList(),
                twins: twins.Where(t => active.Contains(t.Id)).ToList()), "optional_membership.spy",
            harnessFailures, out _);

    /// <summary>Compiles a program over the cells still active (<paramref name="programFor"/>), removing each cell a
    /// refusal is attributed to by line, until it runs; <paramref name="ranSource"/> is the program that ran.</summary>
    private Dictionary<int, SharpyVerdict> RunRounds(string name, IReadOnlyList<int> ids,
        Func<IReadOnlySet<int>, Program> programFor, string fileName, List<string> harnessFailures, out string? ranSource)
    {
        ranSource = null;
        var verdicts = new Dictionary<int, SharpyVerdict>();
        var active = ids.ToHashSet();
        for (var round = 0; round < MaxRounds; round++)
        {
            var program = programFor(active);
            var run = CompileAndExecute(program.Source, fileName, executionTimeoutMs: 120_000);
            if (RanToCompletion(run))
            {
                var byId = ParseLines(run.StandardOutput);
                foreach (var id in ids.Where(active.Contains))
                    verdicts[id] = new SharpyVerdict(byId.TryGetValue(id, out var v) ? v : new List<string>(), null);
                if (!run.StandardOutput.Contains("done", StringComparison.Ordinal))
                    harnessFailures.Add($"{name}: the program did not reach its last line");
                ranSource = program.Source;
                return verdicts;
            }

            var refused = new Dictionary<int, HashSet<string>>();
            var unattributed = new List<string>();
            foreach (var d in run.RawDiagnostics.Where(d => d.IsError))
            {
                if (d.Line is { } line && program.CellByLine.TryGetValue(line, out var id))
                {
                    if (!refused.TryGetValue(id, out var codes))
                        refused[id] = codes = new HashSet<string>(StringComparer.Ordinal);
                    codes.Add(d.Code ?? "?");
                    if (d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError)
                        Output.WriteLine($"  {name} c{id} line {line}: {d.Message}");
                }
                else
                {
                    unattributed.Add($"line {d.Line}: [{d.Code}] {d.Message}");
                }
            }
            if (unattributed.Count > 0 || refused.Count == 0)
            {
                harnessFailures.Add($"{name}: round {round} failed with no cell to remove: "
                    + (unattributed.Count > 0 ? string.Join("; ", unattributed.Take(5)) : run.FailureSummary()));
                return verdicts;
            }
            foreach (var (id, codes) in refused)
                verdicts[id] = new SharpyVerdict(null, codes);
            active.ExceptWith(refused.Keys);
            Output.WriteLine($"{name}: round {round} removed {refused.Count} cell(s) ("
                + string.Join(",", refused.Values.SelectMany(c => c).Distinct().OrderBy(c => c, StringComparer.Ordinal)) + ")");
        }
        harnessFailures.Add($"{name}: still refusing after {MaxRounds} rounds");
        return verdicts;
    }

    /// <summary>What a cell may do: print CPython's values (<see cref="Values"/>: the cell is COMPARED against the
    /// oracle), or be refused by exactly one of <see cref="RefusedBy"/> (HAND-EXPECTED: the basis is in
    /// <see cref="Why"/>, never the oracle).</summary>
    private sealed record Expectation(bool Values, IReadOnlySet<string> RefusedBy, string Why)
    {
        public bool Compared => Values;

        /// <summary>Codes that may accompany the refusal. Empty for every expectation but a ruled refusal (and a
        /// chain link that inherits one): there an expected code must be among the codes and nothing outside
        /// <see cref="RefusedBy"/> and this set may be.</summary>
        public IReadOnlySet<string> Tolerated { get; init; } = NoCodes;

        /// <summary>Whether a refusal by <paramref name="codes"/> is the expected one: exactly one of
        /// <see cref="RefusedBy"/>, or — with <see cref="Tolerated"/> codes — one of them and otherwise only
        /// tolerated ones.</summary>
        public bool MeetsRefusal(IReadOnlySet<string> codes) => Tolerated.Count == 0 && !AnyOf
            ? codes.Count == 1 && RefusedBy.Contains(codes.Single())
            : codes.Any(RefusedBy.Contains) && codes.All(c => RefusedBy.Contains(c) || Tolerated.Contains(c));

        /// <summary>A chain link whose head link is refused: the refusal is one or more of <see cref="RefusedBy"/>
        /// (the head's measured codes, and the membership link's when that is refused too: both links are checked,
        /// and which codes one compile reports is the pipeline's), and nothing else.</summary>
        public bool AnyOf { get; init; }

        public string Describe() => (Values ? "python's values" : "")
            + (RefusedBy.Count > 0 ? "refused by " + string.Join("|", RefusedBy.OrderBy(c => c, StringComparer.Ordinal)) : "")
            + " (" + Why + ")";
    }

    private static readonly IReadOnlySet<string> NoCodes = new HashSet<string>();
    private static readonly IReadOnlySet<string> Spy0222 = new HashSet<string> { DiagnosticCodes.Semantic.InvalidBinaryOperation };
    private static readonly IReadOnlySet<string> Spy0320 = new HashSet<string> { DiagnosticCodes.Semantic.ProtocolMissingMethod };

    /// <summary>The needle-slot rule (membership_operators.md › Container kinds, as owner ruling C amends the
    /// <c>bytes</c> row): an element-typed container's slot IS the payload. A fixed-slot container's slots are its
    /// public single-argument <c>Contains</c> parameter types, read by reflection: the payload holds one when its CLR
    /// type is that type, or when it is an integer the catalog converts implicitly to it (so <c>range</c> and
    /// <c>bytes</c> take <c>int</c> and narrower, <c>bytes</c> also takes <c>bytes</c>, <c>str</c> takes <c>str</c>,
    /// an IP network its address type).</summary>
    private static bool SlotHolds(Cell cell)
    {
        if (IsElementTyped(cell.Container))
            return true;
        var payload = cell.Payload;
        if (payload.ClrType is not { } clr)
            return false;
        return ContainsSlots(ClrTypeOf(cell.Container)!).Any(slot => slot == clr
            || (payload.Kind == PayloadKind.Integer && PrimitiveCatalog.GetByClrType(slot) is { } to
                && PrimitiveCatalog.CanImplicitlyConvert(PrimitiveCatalog.GetByName(payload.Name)!, to)));
    }

    /// <summary>
    /// The case → expected map. COMPARED: a needle the slot holds, plain or <c>T | None</c>, prints CPython's values —
    /// <c>False</c> for a None needle in an equality scan (owner ruling 1, #2635), <c>TypeError</c> for a None needle
    /// in <c>str</c>/<c>bytes</c> (owner ruling 2: accepted, TypeError at run time), and whatever CPython answers for
    /// the other fixed-slot containers; so does the literal <c>None</c> into an optional element (owner ruling A).
    /// HAND-EXPECTED, by owner ruling A (2026-11-12): the literal <c>None</c> into a non-optional slot is refused by
    /// SPY0222. HAND-EXPECTED, by the deviation <c>membership-needle-type-mismatch</c> in <c>docs/deviations.yaml</c>:
    /// a typed needle the slot cannot hold (<see cref="SlotHolds"/>) is refused by SPY0222. HAND-EXPECTED, by owner
    /// ruling R-OA (2026-11-12): every cell of a class with only <c>__getitem__</c> and <c>__len__</c> is refused by
    /// SPY0320. An order cell takes its value twin's expectation. A chain link <c>x == x in c</c> takes its binary
    /// twins': the membership link's is this rule's answer for <c>x in c</c>; the head link's is MEASURED
    /// (<paramref name="heads"/>: the verdict of <see cref="HeadTwin"/> for the cell's payload, form and
    /// observation). A head that runs leaves the membership link's expectation; a head that is refused refuses the
    /// chain by its codes — an internal error included (owner rulings 2026-11-13, #2648: "Refuse, as the twin";
    /// "Same as the twin", so a chain whose head reaches Roslyn is a cell of the head's tracker, listed in the
    /// summary) — and by the membership link's when that is refused too.
    /// </summary>
    private static Expectation Expect(Cell cell, IReadOnlyDictionary<(string Payload, Form Form, Observation Observation), SharpyVerdict> heads)
    {
        if (cell.Position == Position.ChainLink)
        {
            var link = Expect(cell with { Position = Position.Expression }, heads);
            if (heads[(cell.Payload.Name, cell.Form, cell.Observation)].RefusedBy is not { } head)
                return link;
            var headCodes = string.Join(",", head.OrderBy(c => c, StringComparer.Ordinal));
            return new Expectation(false, link.Values ? head : head.Concat(link.RefusedBy).ToHashSet(StringComparer.Ordinal),
                $"a chain link takes its binary twin's verdict (owner rulings 2026-11-13, #2648): the head link is refused "
                + $"by {headCodes} as a binary expression (measured)" + (link.Values ? "" : "; the membership link: " + link.Why))
            {
                Tolerated = link.Tolerated,
                AnyOf = true,
            };
        }

        // An order cell expects what its value twin expects (its trace is part of the compared output).
        if (cell.Observation != Observation.Value)
            return Expect(cell with { Observation = Observation.Value }, heads);

        if (IsRefusedByRuling(cell.Container))
        {
            // Owner ruling R-OA, 2026-11-12: "maybe let's say no on __getitem__ with __len__". The needle check may
            // also speak (SPY0222: a generic class's element type resolves through `__getitem__`).
            return new Expectation(false, Spy0320, "a class with only __len__ + __getitem__: owner ruling R-OA (2026-11-12), "
                + "a deliberate divergence from CPython's legacy sequence protocol")
            {
                Tolerated = Spy0222,
            };
        }

        if (cell.Form == Form.NoneLiteral && !cell.OptionalElement)
        {
            // Owner ruling A, 2026-11-12: "switch to the table, refuse literal None for non-optional containers, makes
            // the most sense".
            return new Expectation(false, Spy0222, "literal None into a non-optional slot: owner ruling A (2026-11-12)");
        }

        if (cell.Form == Form.NoneLiteral)
            return new Expectation(true, NoCodes, "literal None into an optional element: owner ruling A (2026-11-12)");

        if (!SlotHolds(cell))
            return new Expectation(false, Spy0222, "by deviation membership-needle-type-mismatch, not by oracle: the slot cannot hold this type");

        return new Expectation(true, NoCodes, cell.Form == Form.Plain
            ? "plain needle"
            : "T | None needle: owner rulings 1-2 (None is no member of an equality scan; TypeError for str/bytes); CPython's answer elsewhere");
    }

    /// <summary>The python twin's names for Sharpy's types and its <c>email.EmailMessage</c> (python's lives in
    /// <c>email.message</c>).</summary>
    private const string PythonPrelude =
        "from decimal import Decimal as decimal\n"
        + "int8 = int16 = int32 = int64 = uint8 = uint16 = uint32 = uint64 = int\nfloat32 = float64 = float\n"
        + "import email, email.message\nemail.EmailMessage = email.message.EmailMessage\n";

    [SkippableFact]
    public void OptionalNeedle_EveryContainerOperatorPosition_MatchesCPython() => RunMembershipMatrix(Observation.Value);

    /// <summary>The same matrix by its order observation (#2645): every cell traces its operands, and CPython's
    /// trace (the needle before the container, a chain's head operand before both) is part of the compared output.
    /// A fact of its own so that neither half of the matrix runs for longer than CI's per-test hang limit.</summary>
    [SkippableFact]
    public void Membership_EveryContainerOperatorPosition_EvaluatesItsOperandsLeftToRight() => RunMembershipMatrix(Observation.Order);

    /// <summary>The membership matrix over the cells of one observation. The axes and the totality count are the
    /// whole product's in both facts; each fact runs, compares and pins its own half (the order half owns the
    /// <c>Order/*</c> rows).</summary>
    private void RunMembershipMatrix(Observation observation)
    {
        // The oracle minor is the repository's .python-version pin (3.15).
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 15));

        AssertContainerAxisIsTheSpecTable();
        AssertClrRowIsTheContainsCensus();
        AssertPayloadAxisIsTheCatalogAndTheSlots();
        AssertCompositeAxisIsTheTypeKindImage();
        AssertPayloadAxisIsTheSemanticTypeCensus();
        AssertOptionalElementExclusionsStillHold();
        // Measured here, asserted after the matrix: its control rests on the lift the matrix measures, so a
        // regression there must not hide the cells that say which rows it broke.
        var declarationOrderProblem = DeclarationOrderExclusionProblem();
        AssertEveryExclusionHasAReason();
        // The generator's element-typed predicate is the literal roster, container by container.
        Assert.Equal(ElementTypedRoster.OrderBy(c => c), Containers().Where(IsElementTyped).OrderBy(c => c));

        var payloads = GeneratePayloads();
        var allCells = GenerateCells(payloads);

        // Totality: the cell count is the product of the axis sizes, each from its own source (the payload counts the
        // SemanticType census pins, key by key; the spec table's containers, doubled for the literal element-typed
        // roster; the enums). Never from the generator's loops or its predicates, so a cell the generator drops (or
        // invents) is red. The census's count is itself checked against the catalog, the TypeKind map and the slots.
        var payloadCount = SemanticTypeCensus.Values.Sum(v => v.Payloads);
        Assert.Equal(payloadCount, CanonicalPrimitives().Count - ExcludedPrimitives.Count + 1
            + CompositePayloadCount + ScopedPayloads.Length + RequiredSlotPayloadTypes().Count);
        var containerCount = SpecRowMap.Sum(m => m.Generated.Length) + RuledContainerKinds.Sum(k => k.Generated.Length);
        var elementTypedCount = ElementTypedRoster.Length;
        var formCount = Enum.GetValues<Form>().Length;
        var positionCount = Enum.GetValues<Position>().Length;
        var containerVariants = containerCount + elementTypedCount;
        // A form is observed once per observation, less the (form, order) pairs OrderExcludedForms names.
        var observedFormCount = formCount * Enum.GetValues<Observation>().Length - OrderExcludedForms.Count;
        Assert.All(OrderExcludedForms, e => Assert.False(string.IsNullOrWhiteSpace(e.Value), $"{e.Key}: no reason"));
        // OptionalElementExcluded removes whole forms of a payload's optional-element variant. The number of
        // (payload, form) pairs it removes is pinned here, not read from the table the generator reads, so an entry
        // added or widened there is red until this count is raised with it.
        Assert.True(OptionalElementExcluded.Values.Sum(e => e.Forms.Distinct().Count()) == OptionalElementExcludedForms,
            $"OptionalElementExcluded removes {OptionalElementExcluded.Values.Sum(e => e.Forms.Distinct().Count())} "
            + $"(payload, form) pairs; {OptionalElementExcludedForms} are pinned");
        var excludedObservedForms = OptionalElementExcluded.Values.Sum(e => e.Forms.Distinct().Sum(f => ObservationsOf(f).Count()));
        Assert.True(excludedObservedForms == OptionalElementExcludedObservedForms,
            $"OptionalElementExcluded removes {excludedObservedForms} (payload, form, observation) triples; "
            + $"{OptionalElementExcludedObservedForms} are pinned");
        var expectedCount = (payloadCount * containerVariants * observedFormCount
            - OptionalElementExcludedObservedForms * elementTypedCount) * Operators.Length * positionCount;
        Assert.Equal(2, Operators.Length);
        Assert.Equal(payloadCount, payloads.Count);
        Assert.True(expectedCount == allCells.Count,
            $"the generator made {allCells.Count} cells; the axes give {expectedCount} ({payloadCount} payloads x "
            + $"{containerVariants} container variants ({containerCount} containers, {elementTypedCount} with an optional "
            + $"element) x {observedFormCount} observed forms ({formCount} forms, each by value and by order, less "
            + $"{OrderExcludedForms.Count} order-excluded), less {OptionalElementExcludedObservedForms} excluded (payload, "
            + $"form, observation) triples of the optional-element variant, x {Operators.Length} operators x "
            + $"{positionCount} positions): a cell was dropped or invented");
        Assert.Equal(allCells.Count, allCells.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count());

        // The head twins: one per payload and observed form, from the same axis sizes.
        var allTwins = GenerateHeadTwins(payloads, allCells.Count);
        Assert.True(allTwins.Count == payloadCount * observedFormCount,
            $"the generator made {allTwins.Count} head twins; the axes give {payloadCount * observedFormCount} "
            + $"({payloadCount} payloads x {observedFormCount} observed forms): a head twin was dropped or invented");
        Assert.All(allCells.Where(c => c.Position == Position.ChainLink), c => Assert.True(
            allTwins.Count(t => t.Payload == c.Payload && t.Form == c.Form && t.Observation == c.Observation) == 1,
            $"{c.Key}: a chain link without exactly one head twin"));

        // This fact's half of the product, and its part of the rows.
        var cells = allCells.Where(c => c.Observation == observation).ToList();
        var twins = allTwins.Where(t => t.Observation == observation).ToList();
        Assert.NotEmpty(cells);
        var part = observation == Observation.Value ? ValuePart : OrderPart;
        var report = observation == Observation.Value ? "optional-membership-differential" : "membership-order-differential";

        // CPython: one program per payload × container (the runner passes a program as one argv string, which the OS
        // caps at 128 KiB), one batch.
        var chunks = cells.GroupBy(c => (c.Payload, c.Container)).Select(g => (Name: $"{g.Key.Payload.Name}/{g.Key.Container}",
            Program: ProgramFor(g.Key.Payload, g.ToList(), python: true))).ToList();
        var py = oracle.RunBatch(chunks.Select((chunk, i) => new PyRequest(i, PythonPrelude + chunk.Program.Source + "main()\n")).ToList());
        var pythonById = new Dictionary<int, List<string>>();
        for (var i = 0; i < chunks.Count; i++)
        {
            Assert.True(py.TryGetValue(i, out var result) && result.Ok,
                $"python3 did not run the {chunks[i].Name} program: " + (result?.Stderr ?? "no verdict"));
            foreach (var (id, values) in ParseLines(result!.Stdout))
                pythonById[id] = values;
        }
        Assert.Equal(cells.Count, pythonById.Count);

        // Sharpy.
        var harnessFailures = new List<string>();
        var sharpyById = new Dictionary<int, SharpyVerdict>();
        foreach (var payload in payloads)
            foreach (var (id, verdict) in RunSharpy(payload, cells.Where(c => c.Payload == payload).ToList(),
                         twins.Where(t => t.Payload == payload).ToList(), harnessFailures))
                sharpyById[id] = verdict;
        Assert.True(harnessFailures.Count == 0, "harness: " + string.Join("\n", harnessFailures));
        Assert.Equal(cells.Count + twins.Count, sharpyById.Count);

        // The head link's verdict, measured: what the head of every chain cell of that payload and form must get.
        var heads = twins.ToDictionary(t => (t.Payload.Name, t.Form, t.Observation), t => sharpyById[t.Id]);
        var expectations = cells.ToDictionary(c => c.Id, c => Expect(c, heads));

        // The answers of a cell without its trace: an order cell that ran and gave CPython's answers diverges by
        // its operand order alone.
        static IEnumerable<string> Answers(IEnumerable<string> values) => values.Where(v => v is not ("n" or "c" or "h"));

        var divergences = new List<(Cell Cell, string Row, string Observed, string Detail)>();
        foreach (var cell in cells)
        {
            var python = pythonById[cell.Id];
            var sharpy = sharpyById[cell.Id];
            var expected = expectations[cell.Id];
            var meets = sharpy.RefusedBy is { } refusedBy
                ? expected.MeetsRefusal(refusedBy)
                : expected.Values && python.SequenceEqual(sharpy.Values!);
            if (meets)
                continue;
            var observed = sharpy.RefusedBy is { } by
                ? "refused " + string.Join(",", by.OrderBy(c => c, StringComparer.Ordinal))
                : "values " + string.Join(" ", sharpy.Values!);
            // The pinned verdict names both sides, so a changed oracle answer or expectation for a red cell is a change
            // to its pin, exactly as a changed Sharpy answer is.
            var expectedShort = expected.Values
                ? "python " + string.Join(" ", python)
                : "refused " + string.Join("|", expected.RefusedBy.OrderBy(c => c, StringComparer.Ordinal));
            var pinned = $"sharpy {observed} | expected {expectedShort}";
            var orderAlone = cell.Observation == Observation.Order && expected.Values && sharpy.Values is { } ran
                && Answers(ran).SequenceEqual(Answers(python));
            divergences.Add((cell, cell.RowKey(orderAlone), pinned,
                $"python [{string.Join(" ", python)}] sharpy [{observed}]: expected {expected.Describe()}"));
        }

        var compared = expectations.Values.Count(e => e.Compared);
        var handExpected = cells.Count - compared;
        var reportDir = ReportDirectory();
        var divergencePath = Path.Combine(reportDir, $"{report}-divergences.txt");
        File.WriteAllLines(divergencePath, divergences.OrderBy(d => d.Cell.Key, StringComparer.Ordinal)
            .Select(d => $"{d.Cell.Key}  {d.Detail}"));

        var allowlist = LoadAllowlist();
        var actualByRow = divergences.GroupBy(d => d.Row, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToDictionary(d => d.Cell.Key, d => d.Observed, StringComparer.Ordinal),
                StringComparer.Ordinal);
        var problems = PinProblems(actualByRow, allowlist, part);

        // A chain link whose head link is refused expects the head's codes. Where the head reaches Roslyn the chain
        // is to reach it too (owner ruling 2026-11-13, #2648: "Same as the twin"): never silent, those cells are
        // listed for the lead, by direction, as cells of the head's tracker.
        var chainCells = cells.Where(c => c.Position == Position.ChainLink).ToList();
        var headRefused = chainCells.Where(c => heads[(c.Payload.Name, c.Form, c.Observation)].RefusedBy != null).ToList();
        var headIce = headRefused.Where(c => heads[(c.Payload.Name, c.Form, c.Observation)].RefusedBy!
            .Contains(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError)).ToList();
        var headIcePath = Path.Combine(reportDir, $"{report}-chain-head-spy0908.txt");
        File.WriteAllLines(headIcePath, headIce.Select(c => c.Key).OrderBy(k => k, StringComparer.Ordinal));

        Output.WriteLine($"{observation}: {payloads.Count} payloads, {cells.Count} of {allCells.Count} cases: {compared} compared with CPython, {handExpected} "
            + $"hand-expected (slot mismatches by deviation membership-needle-type-mismatch; literal None by owner ruling A; "
            + "__getitem__ + __len__ classes by owner ruling R-OA; chain links whose head link is refused, by its "
            + $"measured twin: {headRefused.Count} of {chainCells.Count} chain cells, {headIce.Count} of them where the head "
            + $"reaches Roslyn: {headIcePath}); head twins refused: "
            + string.Join("; ", twins.Where(t => sharpyById[t.Id].RefusedBy != null).Select(t => $"{t.Payload.Name}/{t.Form} "
                + string.Join(",", sharpyById[t.Id].RefusedBy!.OrderBy(c => c, StringComparer.Ordinal))))
            + $"; {divergences.Count} divergent in {actualByRow.Count} rows; allowlist {allowlist.Count} rows; "
            + $"{problems.Count} pin problem line(s). Every divergence: {divergencePath}");

        // Positive controls: the oracle column reaches every answer the expected map distinguishes.
        Assert.Contains(cells, c => pythonById[c.Id].Contains("True"));
        Assert.Contains(cells, c => pythonById[c.Id].Contains("False"));
        Assert.Contains(cells, c => expectations[c.Id].Compared && c.Form == Form.Optional && pythonById[c.Id].Contains("TypeError"));
        Assert.Contains(cells, c => c.Form == Form.Plain && c.Position != Position.ChainLink && sharpyById[c.Id].Values != null);
        // The head twin distinguishes: some head link runs, and some is refused by the operator validator's code —
        // a chain cell of each kind exists, and a refused head's cell expects exactly the head's codes.
        Assert.Contains(twins, t => sharpyById[t.Id].Values != null);
        Assert.Contains(twins, t => sharpyById[t.Id].RefusedBy is { Count: 1 } by && by.Contains(DiagnosticCodes.Validation.UnsupportedOperator));
        Assert.All(headRefused, c => Assert.True(!expectations[c.Id].Values && heads[(c.Payload.Name, c.Form, c.Observation)].RefusedBy!
            .All(expectations[c.Id].RefusedBy.Contains), $"{c.Key}: its head link is refused and the cell does not expect the head's codes"));
        Assert.Contains(chainCells, c => expectations[c.Id].Values);
        // The optional column can pass: a T | None needle's cell already runs, prints False for None and agrees (by
        // value a reference payload's; by order a lifted one's, whose helper takes the needle first).
        var divergent = divergences.Select(d => d.Cell.Id).ToHashSet();
        Assert.Contains(cells, c => c.Form == Form.Optional && sharpyById[c.Id].Values is { } v
            && v.Contains("False") && v.SequenceEqual(pythonById[c.Id]) && !divergent.Contains(c.Id));
        // A ruled refusal is a refusal of something CPython answers, and Sharpy refuses it today.
        var refusedCells = cells.Where(c => IsRefusedByRuling(c.Container)).ToList();
        Assert.NotEmpty(refusedCells);
        Assert.All(refusedCells, c => Assert.False(expectations[c.Id].Compared, $"{c.Key}: a ruled refusal is compared"));
        Assert.Contains(refusedCells, c => pythonById[c.Id].Contains("True") && sharpyById[c.Id].RefusedBy is { } by
            && expectations[c.Id].MeetsRefusal(by));
        Assert.Equal(1, ElementTypedRoster.Count(IsRefusedByRuling));
        if (observation == Observation.Order)
        {
            // The order observation reaches the oracle: CPython traces the needle before the container in every
            // cell (and a chain's head operand before both).
            static bool Before(IReadOnlyList<string> trace, string first, string second) =>
                trace.Contains(first) && trace.Contains(second)
                && Enumerable.Range(0, trace.Count).Where(i => trace[i] == second).All(i => i > 0 && trace.Take(i).Contains(first));
            Assert.All(cells, c => Assert.True(Before(pythonById[c.Id], "n", "c"),
                $"{c.Key}: CPython's trace [{string.Join(" ", pythonById[c.Id])}] does not show the needle before the container"));
            Assert.All(cells.Where(c => c.Position == Position.ChainLink), c => Assert.True(Before(pythonById[c.Id], "h", "n"),
                $"{c.Key}: CPython's trace [{string.Join(" ", pythonById[c.Id])}] does not show the chain's head operand first"));
        }
        else
        {
            // The element-optionality axis discriminates, container by container: a None needle into an optional
            // element is a member (CPython `True False True` over a member, a non-member and None), so a lift that
            // answers False for every None needle is red there, and a container whose optional variant lost its None
            // element is red here. The Sharpy half (the cell runs and agrees today) holds for every element-typed
            // container but the named ones.
            var trueFalseTrue = new[] { "True", "False", "True" };
            var controlled = 0;
            foreach (var container in ElementTypedRoster)
            {
                controlled++;
                if (IsRefusedByRuling(container))
                    continue;
                var controls = cells.Where(c => c.Container == container && c.OptionalElement && c.Form == Form.Optional
                    && c.Operator == BinaryOperator.In && expectations[c.Id].Compared).ToList();
                Assert.True(controls.Any(c => pythonById[c.Id].SequenceEqual(trueFalseTrue)),
                    $"{container}OfOptional: no compared T | None needle cell where CPython answers True False True "
                    + "(the optional variant must hold a None element)");
                if (!OptionalElementControlExcluded.ContainsKey(container))
                    Assert.True(controls.Any(c => pythonById[c.Id].SequenceEqual(trueFalseTrue)
                            && sharpyById[c.Id].Values is { } v && v.SequenceEqual(trueFalseTrue)),
                        $"{container}OfOptional: no cell where Sharpy also answers True False True (exempt it in "
                        + "OptionalElementControlExcluded with a reason and an issue, or fix it)");
                else
                    Assert.False(controls.Any(c => sharpyById[c.Id].Values is { } v && v.SequenceEqual(trueFalseTrue)),
                        $"{container} is in OptionalElementControlExcluded but Sharpy now answers True False True: "
                        + "delete the exemption");
            }
            Assert.Equal(ElementTypedRoster.Length, controlled);
        }

        // Written on every run, header included, so copying it over the allowlist is a byte-identical round trip
        // when nothing moved and a reviewable diff when something did.
        var proposedPath = Path.Combine(reportDir, $"{report}-proposed-allowlist.txt");
        File.WriteAllText(proposedPath, string.Join("\n", ProposedAllowlist(actualByRow, allowlist, part)) + "\n");

        if (problems.Count > 0)
        {
            var problemPath = Path.Combine(reportDir, $"{report}-pin-problems.txt");
            File.WriteAllLines(problemPath, problems);
            Assert.Fail($"{problems.Count} pin problem line(s) against {AllowlistFileName}: every line is in {problemPath}; "
                + $"a regenerated allowlist (rows' citations kept) is in {proposedPath}. First lines:\n"
                + string.Join("\n", problems.Take(30))
                + (declarationOrderProblem != null ? "\nAlso: " + declarationOrderProblem : ""));
        }
        Assert.True(declarationOrderProblem == null, declarationOrderProblem);
    }

    // ---------------------------------------------------------------------------------------------------------
    // The operator-sequence matrix (#2645 evaluation order, #2648 chain links)
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>The allowlist rows <see cref="OperatorSequences_OverEffectfulOperands_AreTheirDesugaring"/> owns.</summary>
    private const string SequenceRowPrefix = "Sequence/";

    /// <summary>How a sequence's operands are typed: the payload; <c>T | None</c>; the tagged <c>T?</c>, on both
    /// values or on the first alone (the operand types <c>??</c> has its two lowerings for); or the payload with the
    /// literal <c>None</c> as the last operand (the None tests and their refusals).</summary>
    private enum SequenceForm { Plain, Optional, Tagged, TaggedLeft, NoneLast }

    /// <summary>
    /// The spelling of every <see cref="BinaryOperator"/>, or why the sequence matrix does not generate it. No
    /// default arm: a new operator throws here until it is spelled or excluded.
    /// </summary>
    private static (string? Spelling, string? Excluded) SpellingOf(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => ("+", null),
        BinaryOperator.Subtract => ("-", null),
        BinaryOperator.Multiply => ("*", null),
        BinaryOperator.Divide => ("/", null),
        BinaryOperator.FloorDivide => ("//", null),
        BinaryOperator.Modulo => ("%", null),
        BinaryOperator.Power => ("**", null),
        BinaryOperator.MatMul => (null, "`@` is an experimental feature, off by default (SPY0331): no ungated program spells it"),
        BinaryOperator.Equal => ("==", null),
        BinaryOperator.NotEqual => ("!=", null),
        BinaryOperator.LessThan => ("<", null),
        BinaryOperator.LessThanOrEqual => ("<=", null),
        BinaryOperator.GreaterThan => (">", null),
        BinaryOperator.GreaterThanOrEqual => (">=", null),
        BinaryOperator.And => ("and", null),
        BinaryOperator.Or => ("or", null),
        BinaryOperator.BitwiseAnd => ("&", null),
        BinaryOperator.BitwiseOr => ("|", null),
        BinaryOperator.BitwiseXor => ("^", null),
        BinaryOperator.LeftShift => ("<<", null),
        BinaryOperator.RightShift => (">>", null),
        BinaryOperator.In => ("in", null),
        BinaryOperator.NotIn => ("not in", null),
        BinaryOperator.Is => ("is", null),
        BinaryOperator.IsNot => ("is not", null),
        BinaryOperator.NullCoalesce => ("??", null),
        BinaryOperator.PipeForward => (null, "the right operand of `|>` is a callee, not a value: `x |> f` is the call `f(x)`"),
        _ => throw new InvalidOperationException($"BinaryOperator.{op} has no spelling here: spell it or exclude it with a reason"),
    };

    /// <summary>How many <see cref="BinaryOperator"/> members <see cref="SpellingOf"/> excludes, as a literal: the
    /// totality count reads the map the generator reads, so only this pins an operator against a silent exclusion.</summary>
    private const int SequenceExcludedOperators = 2;

    /// <summary>
    /// The operand types a one-operator sequence pairs the payload with, on either side, besides the payload itself:
    /// a type and a value. A closed literal set, closed against the compiler by <see cref="SequenceExcludedKinds"/>:
    /// every <c>OperatorLoweringKind</c> the checker records on a binary operator must be recorded in a generated
    /// program, so a lowering only a mixed pair reaches (a string repeated by an int on its left) names its pivot.
    /// </summary>
    private static readonly (string Type, string Value)[] Pivots =
    {
        ("int", "2"), ("int64", "2"), ("uint64", "2"), ("float", "2.5"), ("str", "\"ab\""),
        // A class whose comparison dunders take an `int` on their right: `int < Rf` is the reflected lowering.
        ("Rf", "Rf(2)"),
    };

    private const string PivotDeclarations =
        "class Rf:\n    n: int\n    def __init__(self, n: int):\n        self.n = n\n"
        + "    def __lt__(self, o: int) -> bool:\n        return self.n < o\n"
        + "    def __le__(self, o: int) -> bool:\n        return self.n <= o\n"
        + "    def __gt__(self, o: int) -> bool:\n        return self.n > o\n"
        + "    def __ge__(self, o: int) -> bool:\n        return self.n >= o\n";

    /// <summary>
    /// Payloads of the sequence matrix alone. The membership matrix leaves a type-constrained type parameter out
    /// as the unconstrained one's twin (<see cref="SemanticTypeCensus"/>, <c>TypeParameterType/TypeConstraint</c>:
    /// the constraint says nothing about a needle), but an ordering operator is decided BY the constraint (the
    /// <c>TypeParameterCompareTo</c> lowering), so the sequence matrix generates it.
    /// <para>The primitives the membership matrix leaves out (<see cref="ExcludedPrimitives"/>) are left out for a
    /// needle slot and a python twin, and a sequence has neither: <c>object</c> and <c>char</c> are generated here
    /// (<c>o == p == o</c> over <c>object</c> is #2648's own cell), and <c>void</c> has this matrix's own reason
    /// (<see cref="SequenceExcludedPrimitives"/>). The fact asserts the three sets agree.</para>
    /// </summary>
    private static readonly Payload[] SequenceOnlyPayloads =
    {
        new("T: IComparable[T]", PayloadKind.UserType, "1", "2", "5", null)
        { TypeParameters = "[T: IComparable[T]]", Binding = "int" },
        new("object", PayloadKind.UserType, "\"a\"", "\"b\"", "\"z\"", typeof(object)),
        // A `char` has no literal: it is an element of a `str` (primitive_types.md).
        new("char", PayloadKind.UserType, "\"ab\"[0]", "\"ab\"[1]", "\"z\"[0]", typeof(char)),
    };

    /// <summary>How many payloads <see cref="SequenceOnlyPayloads"/> adds, as a literal (the generator reads the array).</summary>
    private const int SequenceOnlyPayloadCount = 3;

    /// <summary>The primitives of <see cref="ExcludedPrimitives"/> the sequence matrix leaves out too, each with the
    /// sequence matrix's own reason; every other one is a <see cref="SequenceOnlyPayloads"/> entry.</summary>
    private static readonly Dictionary<string, string> SequenceExcludedPrimitives = new(StringComparer.Ordinal)
    {
        ["void"] = "a `None`-typed operand has one value, the literal `None`, which has no evaluation to order: the "
            + "NoneLast form places it as the last operand of every sequence",
    };

    /// <summary>The <see cref="BinaryOperator"/> of a chain link, by name: every <see cref="ComparisonOperator"/> has
    /// one (a link is a binary operation), and a new one fails here.</summary>
    private static BinaryOperator LinkOperator(ComparisonOperator op) => Enum.Parse<BinaryOperator>(op.ToString());

    /// <summary>A sequence: one operator between two operands, or a chain of two comparison links over three. Its
    /// cell (<see cref="CellId"/>) is the expression over traced operands; its reference (<see cref="ReferenceId"/>)
    /// is the desugaring over the same operands.</summary>
    private sealed record Sequence(int Id, Payload Payload, SequenceForm Form, BinaryOperator[] Operators, int? Pivot, bool PivotOnLeft)
    {
        public int CellId => Id * 2;
        public int ReferenceId => Id * 2 + 1;
        public string Key => $"{Payload.Name}/{Form}/{string.Join(",", Operators.Select(o => SpellingOf(o).Spelling))}"
            + (Pivot is { } p ? $"/{Pivots[p].Type} on the {(PivotOnLeft ? "left" : "right")}" : "");
        public string RowKey => SequenceRowPrefix + (Operators.Length == 1 ? "Binary" : "Chain");
    }

    /// <summary>The full product: for each payload, every form of every spelled binary operator and of every pair
    /// of chain links; and, in the plain form, every spelled binary operator with each pivot on each side.</summary>
    private static List<Sequence> GenerateSequences(IReadOnlyList<Payload> payloads)
    {
        var binary = Enum.GetValues<BinaryOperator>().Where(o => SpellingOf(o).Spelling != null).ToList();
        var links = Enum.GetValues<ComparisonOperator>().Select(LinkOperator).ToList();
        var sequences = new List<Sequence>();
        foreach (var payload in payloads)
            foreach (var form in Enum.GetValues<SequenceForm>())
            {
                foreach (var op in binary)
                {
                    sequences.Add(new Sequence(sequences.Count, payload, form, new[] { op }, null, false));
                    if (form != SequenceForm.Plain)
                        continue;
                    for (var pivot = 0; pivot < Pivots.Length; pivot++)
                        foreach (var onLeft in new[] { false, true })
                            sequences.Add(new Sequence(sequences.Count, payload, form, new[] { op }, pivot, onLeft));
                }
                foreach (var first in links)
                    foreach (var second in links)
                        sequences.Add(new Sequence(sequences.Count, payload, form, new[] { first, second }, null, false));
            }
        return sequences;
    }

    /// <summary>An operand over the payload's two values <c>a</c> and <c>c</c>, <paramref name="depth"/> lists deep:
    /// the right operand of a membership link is a list of what its left operand is.</summary>
    private static string SequenceOperand(int depth, bool alternate) => depth == 0
        ? (alternate ? "c" : "a")
        : alternate ? $"[{SequenceOperand(depth - 1, true)}]" : $"[{SequenceOperand(depth - 1, false)}, {SequenceOperand(depth - 1, true)}]";

    /// <summary>The operands of a sequence, left to right: the payload's values (a membership operator's right
    /// operand one list deeper than its left), a pivot local on one side, or the literal <c>None</c> last.</summary>
    private static string[] SequenceOperands(Sequence sequence)
    {
        if (sequence.Pivot != null)
            return sequence.PivotOnLeft ? new[] { "p", "a" } : new[] { "a", "p" };
        var operands = new List<string> { SequenceOperand(0, false) };
        var depth = 0;
        var alternate = false;
        foreach (var op in sequence.Operators)
        {
            if (op is BinaryOperator.In or BinaryOperator.NotIn)
            {
                depth++;
                alternate = false;
            }
            else
            {
                alternate = !alternate;
            }
            operands.Add(SequenceOperand(depth, alternate));
        }
        if (sequence.Form == SequenceForm.NoneLast)
            operands[^1] = "None";
        return operands.ToArray();
    }

    /// <summary>One program per payload over the functions still active. A sequence's cell computes the expression
    /// with every operand through the tracing identity; its reference is the desugaring — the left operand bound to a
    /// name first, then the operator applied to the name (a chain: both operands of the first link bound, the second
    /// link evaluated only when the first holds, comparison_chaining.md). Both print their value. A type-parameter
    /// payload's functions are generic over its clause and called over its binding; a <c>Self</c> payload's are
    /// methods of its declaration.</summary>
    private static Program SequenceProgramFor(Payload payload, IReadOnlyList<Sequence> sequences, IReadOnlySet<int> active)
    {
        var lines = new List<string>();
        var cellByLine = new Dictionary<int, int>();
        void Add(string line, int? id = null)
        {
            lines.Add(line);
            if (id is { } cell)
                cellByLine[lines.Count] = cell;
        }

        var method = payload.SelfOf != null;
        var scope = method ? "    " : "";
        var self = method ? "self, " : "";

        void AddFunction(Sequence sequence, bool reference)
        {
            var id = reference ? sequence.ReferenceId : sequence.CellId;
            if (!active.Contains(id))
                return;
            var type = payload.CellSpelling + sequence.Form switch
            {
                SequenceForm.Optional => " | None",
                SequenceForm.Tagged or SequenceForm.TaggedLeft => "?",
                _ => "",
            };
            var second = sequence.Form == SequenceForm.TaggedLeft ? payload.CellSpelling : type;
            var operands = SequenceOperands(sequence);
            var ops = sequence.Operators.Select(o => SpellingOf(o).Spelling!).ToArray();
            string Traced(int i) => operands[i] == "None" ? "None" : $"tr({id}, {i}, {operands[i]})";
            Add($"{scope}def e{id}{payload.TypeParameters}({self}a: {type}, c: {second}) -> None:", id);
            if (sequence.Pivot is { } pivot)
                Add($"{scope}    p: {Pivots[pivot].Type} = {Pivots[pivot].Value}", id);
            if (!reference)
            {
                Add($"{scope}    v = {Traced(0)}" + string.Concat(ops.Select((o, i) => $" {o} {Traced(i + 1)}")), id);
            }
            else
            {
                Add($"{scope}    t0 = {Traced(0)}", id);
                if (ops.Length == 1)
                {
                    Add($"{scope}    v = t0 {ops[0]} {Traced(1)}", id);
                }
                else
                {
                    Add($"{scope}    t1 = {Traced(1)}", id);
                    Add($"{scope}    v = t0 {ops[0]} t1", id);
                    Add($"{scope}    if v:", id);
                    Add($"{scope}        v = t1 {ops[1]} {Traced(2)}", id);
                }
            }
            Add($"{scope}    print({id}, v)", id);
            Add("");
        }

        void AddFunctions()
        {
            foreach (var sequence in sequences)
            {
                AddFunction(sequence, reference: false);
                AddFunction(sequence, reference: true);
            }
        }

        void AddDriverBody(string indent, string callee)
        {
            foreach (var sequence in sequences)
                foreach (var id in new[] { sequence.CellId, sequence.ReferenceId })
                {
                    if (!active.Contains(id))
                        continue;
                    Add($"{indent}try:", id);
                    // The tagged Optional is constructed, never converted to (SPY0604).
                    var arguments = sequence.Form switch
                    {
                        SequenceForm.Tagged => "Some(a), Some(c)",
                        SequenceForm.TaggedLeft => "Some(a), c",
                        _ => "a, c",
                    };
                    Add($"{indent}    {callee}{id}{(payload.IsTypeParameter ? $"[{payload.Binding}]" : "")}({arguments})", id);
                    Add($"{indent}except Exception:", id);
                    Add($"{indent}    print({id}, \"raised\")", id);
                }
        }

        Add("from ipaddress import IPv4Address, IPv6Address");
        Add("");
        Add("def tr[V](cell: int, operand: int, v: V) -> V:");
        Add("    print(cell, operand)");
        Add("    return v");
        Add("");
        foreach (var line in PivotDeclarations.TrimEnd('\n').Split('\n'))
            Add(line);
        Add("");
        if (payload.SharpyDeclaration is { } declaration)
        {
            foreach (var line in declaration.TrimEnd('\n').Split('\n'))
                Add(line);
            if (method)
            {
                Add("");
                AddFunctions();
                Add($"    def drive(self, a: {payload.CellSpelling}, c: {payload.CellSpelling}) -> None:");
                Add("        pass");
                AddDriverBody("        ", "self.e");
            }
            Add("");
        }
        if (!method)
            AddFunctions();
        Add("def main() -> None:");
        Add($"    a: {payload.OuterSpelling} = {payload.Member}");
        Add($"    c: {payload.OuterSpelling} = {payload.Other}");
        if (method)
            Add("    a.drive(a, c)");
        else
            AddDriverBody("    ", "e");
        Add("    print(\"done\")");
        return new Program(string.Join("\n", lines) + "\n", cellByLine);
    }

    /// <summary>
    /// The <c>OperatorLoweringKind</c> values no generated sequence records on a binary operator, each with its
    /// reason. Every other value must be recorded on a <see cref="BinaryOp"/> of a program that ran
    /// (<see cref="KindsOnBinaryOperators"/>, read by reflection from the checker's own record), and an excluded value
    /// that is recorded is red until its entry is deleted: the pivot set (<see cref="Pivots"/>) is closed against the
    /// compiler's enumeration of lowerings, not against a list of operand pairs someone thought of.
    /// </summary>
    private static readonly Dictionary<Sem.OperatorLoweringKind, string> SequenceExcludedKinds = new()
    {
        [Sem.OperatorLoweringKind.Native] = "the absent-entry convention: no record is written for it",
        [Sem.OperatorLoweringKind.TrueDivisionCastLeft] = "recorded only when the divisor is a non-zero literal: a literal "
            + "operand has no evaluation to order (the variable-divisor twin, GuardedTrueDivisionCastLeft, is recorded)",
        [Sem.OperatorLoweringKind.OptionalNoneTest] = "a None test on a `T?` subject: its other operand is the literal `None`, "
            + "and the NoneLast form types its subject as the payload (its twins ReferenceNoneTest and BoxedNoneTest are recorded)",
        [Sem.OperatorLoweringKind.EnumMemberReferenceIdentity] = "recorded on a TypeCheck node (`t is Tone.A`, a member "
            + "spelled after `is`), never on a BinaryOp: the right operand is a member name, not an evaluation",
        [Sem.OperatorLoweringKind.NegateLiteralInt] = "recorded on a unary minus",
        [Sem.OperatorLoweringKind.NegateLiteralLong] = "recorded on a unary minus",
        [Sem.OperatorLoweringKind.SuperOperatorApplication] = "recorded on the call `super().__op__(x)`, not on a BinaryOp: "
            + "its receiver is `super()`, which has no evaluation of its own to order",
        [Sem.OperatorLoweringKind.MembershipNoneIsFalse] = "a value-type `T | None` needle into a container of plain `T`: "
            + "every such cell is in the membership matrix by value and by order (form Optional, a non-optional element); "
            + "a sequence's operands share one form",
        [Sem.OperatorLoweringKind.MembershipNoneRaises] = "a `T | None` needle into `str` / `bytes`: every such cell is in "
            + "the membership matrix by value and by order; a sequence's operands share one form",
    };

    private static readonly FieldInfo OperatorLoweringsField =
        typeof(Sem.SemanticInfo).GetField("_operatorLowerings", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SemanticInfo._operatorLowerings is gone: the sequence census moves with it");

    /// <summary>The lowering kinds the checker recorded on the binary operators of a program that compiles.</summary>
    private static HashSet<Sem.OperatorLoweringKind> KindsOnBinaryOperators(string name, string source)
    {
        var options = new CompilerOptions
        {
            References = new[] { Sharpy.TestInfrastructure.SharpyCoreReference.Location, SharpyStdlibReference.Location },
            OutputType = "exe",
            WarningsAsErrors = false,
        };
        var result = new Compiler(options, NullLogger.Instance).Compile(source, "operator_sequence_census.spy");
        var errors = result.Diagnostics.GetErrors().Select(d => $"[{d.Code}] {d.Message}").ToList();
        Assert.True(errors.Count == 0 && result.SemanticInfo != null,
            $"{name}: the program that ran does not compile for the census: " + string.Join("; ", errors.Take(5)));
        var kinds = new HashSet<Sem.OperatorLoweringKind>();
        foreach (System.Collections.DictionaryEntry entry in (System.Collections.IDictionary)OperatorLoweringsField.GetValue(result.SemanticInfo)!)
            if (entry.Key is BinaryOp)
                kinds.Add(((Sem.OperatorLowering)entry.Value!).Kind);
        return kinds;
    }

    /// <summary>
    /// Operands are evaluated left to right, and a comparison chain is its links (#2645, #2648): an operator
    /// expression over effectful operands prints what its DESUGARING prints — the left operand bound to a name
    /// first, then the operator applied to it; for a chain <c>a op1 b op2 c</c>, <c>t0 = a; t1 = b; v = t0 op1 t1;
    /// if v: v = t1 op2 c</c> (comparison_chaining.md) — and is refused exactly when the desugaring is, by a code
    /// the desugaring is refused by: a link gets its binary twin's verdict. The reference is Sharpy's own answer for
    /// the desugared program, so no oracle is needed and a divergence the binary form has from CPython is not
    /// counted twice here.
    /// <para><b>Generated, never listed.</b> payload (the axis of the membership matrix) × form
    /// (<see cref="SequenceForm"/>) × sequence: every <see cref="BinaryOperator"/> <see cref="SpellingOf"/> spells,
    /// and every ordered pair of <see cref="ComparisonOperator"/> links; in the plain form each binary operator also
    /// takes each pivot type on each side (<see cref="Pivots"/>). The count is asserted from the enumerations, and
    /// the operand types are closed against <c>OperatorLoweringKind</c> (<see cref="SequenceExcludedKinds"/>).</para>
    /// <para><b>Known red.</b> The <c>Sequence/Binary</c> and <c>Sequence/Chain</c> rows of the allowlist.</para>
    /// </summary>
    [Fact]
    public void OperatorSequences_OverEffectfulOperands_AreTheirDesugaring()
    {
        var payloads = GeneratePayloads().Concat(SequenceOnlyPayloads).ToList();
        var sequences = GenerateSequences(payloads);

        // Totality: from the enumerations, never from the generator's loops.
        var operators = Enum.GetValues<BinaryOperator>();
        Assert.All(operators, o => Assert.True((SpellingOf(o).Spelling != null) != (SpellingOf(o).Excluded != null)
            && (SpellingOf(o).Spelling ?? SpellingOf(o).Excluded)!.Trim().Length > 0, $"{o}: a spelling or an exclusion reason, exactly one"));
        Assert.True(operators.Count(o => SpellingOf(o).Excluded != null) == SequenceExcludedOperators,
            $"SpellingOf excludes {operators.Count(o => SpellingOf(o).Excluded != null)} operators; {SequenceExcludedOperators} are pinned");
        var binaryCount = operators.Length - SequenceExcludedOperators;
        var linkCount = Enum.GetValues<ComparisonOperator>().Length;
        Assert.Equal(linkCount, Enum.GetValues<ComparisonOperator>().Select(LinkOperator).Distinct().Count());
        var formCount = Enum.GetValues<SequenceForm>().Length;
        var payloadCount = SemanticTypeCensus.Values.Sum(v => v.Payloads) + SequenceOnlyPayloadCount;
        Assert.Equal(payloadCount, payloads.Count);
        // The membership matrix's excluded primitives are each a payload here or excluded for this matrix's own
        // reason: never by a reason about a needle slot or a python twin.
        var sequenceOnlyPrimitives = SequenceOnlyPayloads.Select(p => p.Name).Where(ExcludedPrimitives.ContainsKey).ToList();
        Assert.True(ExcludedPrimitives.Keys.OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(
                sequenceOnlyPrimitives.Concat(SequenceExcludedPrimitives.Keys).OrderBy(n => n, StringComparer.Ordinal)),
            "every primitive the membership matrix excludes [" + string.Join(", ", ExcludedPrimitives.Keys) + "] is a "
            + "sequence-only payload [" + string.Join(", ", sequenceOnlyPrimitives) + "] or in SequenceExcludedPrimitives ["
            + string.Join(", ", SequenceExcludedPrimitives.Keys) + "] with the sequence matrix's own reason, never both");
        Assert.All(SequenceExcludedPrimitives, e => Assert.False(string.IsNullOrWhiteSpace(e.Value), $"{e.Key}: no reason"));
        Assert.All(SequenceOnlyPayloads.Where(p => ExcludedPrimitives.ContainsKey(p.Name)), p => Assert.True(
            PrimitiveCatalog.GetByName(p.Name)?.ClrType == p.ClrType, $"{p.Name}: not the catalog's primitive of that name"));
        Assert.Equal(payloads.Count, payloads.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count());
        var expectedCount = payloadCount * (formCount * (binaryCount + linkCount * linkCount) + binaryCount * Pivots.Length * 2);
        Assert.True(expectedCount == sequences.Count,
            $"the generator made {sequences.Count} sequences; the axes give {expectedCount} ({payloadCount} payloads x "
            + $"({formCount} forms x ({binaryCount} binary operators + {linkCount} x {linkCount} chain link pairs) + "
            + $"{binaryCount} binary operators x {Pivots.Length} pivots x 2 sides)): a sequence was dropped or invented");
        Assert.Equal(sequences.Count, sequences.Select(s => s.Key).Distinct(StringComparer.Ordinal).Count());

        var harnessFailures = new List<string>();
        var verdicts = new Dictionary<int, SharpyVerdict>();
        var kinds = new HashSet<Sem.OperatorLoweringKind>();
        foreach (var payload in payloads)
        {
            var own = sequences.Where(s => s.Payload == payload).ToList();
            var ids = own.SelectMany(s => new[] { s.CellId, s.ReferenceId }).ToList();
            var payloadVerdicts = RunRounds(payload.Name, ids, active => SequenceProgramFor(payload, own, active),
                "operator_sequence.spy", harnessFailures, out var ranSource);
            foreach (var (id, verdict) in payloadVerdicts)
                verdicts[id] = verdict;
            if (ranSource != null)
                kinds.UnionWith(KindsOnBinaryOperators(payload.Name, ranSource));
        }
        Assert.True(harnessFailures.Count == 0, "harness: " + string.Join("\n", harnessFailures));
        Assert.Equal(sequences.Count * 2, verdicts.Count);

        static string Describe(SharpyVerdict verdict) => verdict.RefusedBy is { } by
            ? "refused " + string.Join(",", by.OrderBy(c => c, StringComparer.Ordinal))
            : "prints " + string.Join(" ", verdict.Values!);
        var divergences = new List<(Sequence Sequence, string Observed)>();
        foreach (var sequence in sequences)
        {
            var cell = verdicts[sequence.CellId];
            var reference = verdicts[sequence.ReferenceId];
            var meets = cell.RefusedBy is { } refusedBy
                ? reference.RefusedBy is { } twin && refusedBy.Overlaps(twin)
                : reference.Values is { } values && cell.Values!.SequenceEqual(values);
            if (!meets)
                divergences.Add((sequence, $"sharpy {Describe(cell)} | desugaring {Describe(reference)}"));
        }

        var reportDir = ReportDirectory();
        var divergencePath = Path.Combine(reportDir, "operator-sequence-divergences.txt");
        File.WriteAllLines(divergencePath, divergences.OrderBy(d => d.Sequence.Key, StringComparer.Ordinal)
            .Select(d => $"{d.Sequence.Key}  {d.Observed}"));

        var allowlist = LoadAllowlist();
        var actualByRow = divergences.GroupBy(d => d.Sequence.RowKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToDictionary(d => d.Sequence.Key, d => d.Observed, StringComparer.Ordinal),
                StringComparer.Ordinal);
        var problems = PinProblems(actualByRow, allowlist, SequencePart);
        // Not a divergence of THIS contract, but never silent: a sequence both forms of which reach Roslyn
        // (SPY0908 is a net, not an error channel) is listed for the lead to file by class.
        var iceInBoth = sequences.Where(s => verdicts[s.CellId].RefusedBy is { } a && verdicts[s.ReferenceId].RefusedBy is { } b
            && a.Contains(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError)
            && b.Contains(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError)).ToList();
        var icePath = Path.Combine(reportDir, "operator-sequence-spy0908-in-both-forms.txt");
        File.WriteAllLines(icePath, iceInBoth.Select(s => s.Key).OrderBy(k => k, StringComparer.Ordinal));
        var ran = sequences.Count(s => verdicts[s.CellId].Values != null && verdicts[s.ReferenceId].Values != null);
        var refused = sequences.Count(s => verdicts[s.CellId].RefusedBy != null && verdicts[s.ReferenceId].RefusedBy != null);
        Output.WriteLine($"{payloads.Count} payloads, {sequences.Count} sequences ({sequences.Count(s => s.Operators.Length == 1)} "
            + $"binary, {sequences.Count(s => s.Operators.Length == 2)} chains): {ran} run in both forms, {refused} are refused in "
            + $"both ({iceInBoth.Count} of them by SPY0908 in both forms: {icePath}); {divergences.Count} divergent in "
            + $"{actualByRow.Count} rows; {problems.Count} pin problem line(s). "
            + $"Lowering kinds recorded: {string.Join(", ", kinds.OrderBy(k => k.ToString(), StringComparer.Ordinal))}. "
            + $"Every divergence: {divergencePath}");

        // The operand types are closed against the compiler's lowerings.
        var unreached = Enum.GetValues<Sem.OperatorLoweringKind>()
            .Where(k => !kinds.Contains(k) && !SequenceExcludedKinds.ContainsKey(k)).ToList();
        Assert.True(unreached.Count == 0,
            "no generated sequence records these lowering kinds on a binary operator: [" + string.Join(", ", unreached)
            + "]: add the pivot that reaches each, or exclude it in SequenceExcludedKinds with its reason");
        var stale = SequenceExcludedKinds.Keys.Where(kinds.Contains).ToList();
        Assert.True(stale.Count == 0, "SequenceExcludedKinds excludes kinds a sequence does record: [" + string.Join(", ", stale) + "]");
        Assert.All(SequenceExcludedKinds, e => Assert.False(string.IsNullOrWhiteSpace(e.Value), $"{e.Key}: no reason"));

        // Positive controls: the instrument sees every answer it distinguishes.
        bool Agrees(Sequence s) => verdicts[s.CellId].Values is { } v && verdicts[s.ReferenceId].Values is { } r && v.SequenceEqual(r);
        // A sequence that runs in both forms and traces its operands left to right.
        Assert.Contains(sequences, s => s.Operators.Length == 1 && Agrees(s)
            && verdicts[s.CellId].Values!.Take(2).SequenceEqual(new[] { "0", "1" }));
        // A chain that evaluates its last operand, and one that short-circuits before it.
        Assert.Contains(sequences, s => s.Operators.Length == 2 && Agrees(s) && verdicts[s.CellId].Values!.Contains("2"));
        Assert.Contains(sequences, s => s.Operators.Length == 2 && Agrees(s) && !verdicts[s.CellId].Values!.Contains("2")
            && verdicts[s.CellId].Values!.Take(2).SequenceEqual(new[] { "0", "1" }));
        // A sequence refused in both forms by the same code.
        Assert.Contains(sequences, s => verdicts[s.CellId].RefusedBy is { } a && verdicts[s.ReferenceId].RefusedBy is { } b && a.SetEquals(b));
        // Every reference that ran traced operand 0 first: the desugaring is the order the cell is held to.
        Assert.All(sequences.Where(s => verdicts[s.ReferenceId].Values is { Count: > 0 }),
            s => Assert.True(verdicts[s.ReferenceId].Values![0] is "0" or "raised", $"{s.Key}: its desugaring did not evaluate operand 0 first"));

        var proposedPath = Path.Combine(reportDir, "operator-sequence-proposed-allowlist.txt");
        File.WriteAllText(proposedPath, string.Join("\n", ProposedAllowlist(actualByRow, allowlist, SequencePart)) + "\n");
        if (problems.Count > 0)
        {
            var problemPath = Path.Combine(reportDir, "operator-sequence-pin-problems.txt");
            File.WriteAllLines(problemPath, problems);
            Assert.Fail($"{problems.Count} pin problem line(s) against {AllowlistFileName}: every line is in {problemPath}; "
                + $"a regenerated allowlist (rows' citations kept) is in {proposedPath}. First lines:\n"
                + string.Join("\n", problems.Take(30)));
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // The left-operand form matrix (#2645; owner ruling 2026-11-13, #2645 comment 6867355702: "Close the axis")
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>How the left operand's emitted type is decided: the expression's own (a call, a name of that type),
    /// or only by the slot it is checked against (a tuple of literals narrower than the slot, a conditional with a
    /// <c>None</c> arm, a lambda, a function name, the literal <c>None</c>): the form a binding of the left operand
    /// ahead of the container cannot type without the slot.</summary>
    private enum Typing { Own, BySlot }

    /// <summary>The right operand: a name (no evaluation), a traced call, or a call whose argument REBINDS the
    /// variable the left operand reads (<c>keep((a := c), xs)</c>), so that an operand with no call of its own —
    /// a bare name — still shows which side was evaluated first, by its value. A closed literal set: what the
    /// emitter can branch on in a right operand is whether it has an evaluation and whether that evaluation
    /// changes what the left operand reads.</summary>
    private enum ContainerForm { Name, Call, Rebind }

    /// <summary>
    /// A left-operand form. <see cref="Needle"/> is the operand over <c>@</c>, the traced read of the local
    /// <c>a</c> (a form that cannot hold a call, a bare name, reads <c>a</c> itself); <see cref="A"/> is the type of
    /// <c>a</c> and of <c>c</c>, the value the Rebind container assigns to it; <see cref="Slot"/> is the container's
    /// element type, which the reference declares its bound operand by; <see cref="Members"/> builds the container
    /// (it holds the operand's value over <see cref="AValue"/> and not over <see cref="CValue"/>) and
    /// <see cref="Head"/> the head operand of the chain position.
    /// </summary>
    private sealed record LeftForm(string Needle, string A, string AValue, string CValue, string Slot, string Members, string Head)
    {
        /// <summary>The form awaits: its functions are <c>async</c> and their calls awaited.</summary>
        public bool Async { get; init; }

        /// <summary>The form returns early (<c>?</c>): its functions return this type, and end by returning <c>a</c>.</summary>
        public string? Returns { get; init; }
    }

    private static LeftForm IntForm(string needle, string a = "1", string c = "5") =>
        new(needle, "int", a, c, "int", "[1, 2]", "1");

    private const string NoSlotTyping = "the form's type is its own: no slot gives it another";

    /// <summary>
    /// The left-operand forms: one entry per <c>Expression</c> record of the parser that can stand as a left
    /// operand, each by its own type and typed by its slot — a spelling, or the reason there is none.
    /// <see cref="AssertLeftFormAxisIsTheExpressionCensus"/> asserts the keys, with those of
    /// <see cref="LeftFormUnspellable"/>, are exactly the non-abstract subclasses of <c>Expression</c> (by
    /// reflection), and the fact MEASURES each spelling: the left operand of every generated cell parses to its
    /// entry's record.
    /// </summary>
    private static readonly Dictionary<Type, (LeftForm? Own, string? OwnExcluded, LeftForm? BySlot, string? BySlotExcluded)> LeftForms = new()
    {
        [typeof(IntegerLiteral)] = (IntForm("1"), null,
            new("1", "int", "1", "5", "uint64", "[1, 2]", "1"), null),
        [typeof(FloatLiteral)] = (new("1.5", "int", "1", "5", "float", "[1.5, 2.5]", "1.5"), null,
            null, "typed by a `float32` slot it reaches Roslyn in every position (LeftFormTypingRefused): no order to observe"),
        [typeof(StringLiteral)] = (new("\"a\"", "int", "1", "5", "str", "[\"a\", \"b\"]", "\"a\""), null, null, NoSlotTyping),
        [typeof(BytesLiteralExpression)] = (new("b\"a\"", "int", "1", "5", "bytes", "[b\"a\", b\"b\"]", "b\"a\""), null, null, NoSlotTyping),
        [typeof(FStringLiteral)] = (new("f\"{@}\"", "str", "\"a\"", "\"z\"", "str", "[\"a\", \"b\"]", "\"a\""), null, null, NoSlotTyping),
        [typeof(TStringLiteral)] = (new("t\"{@}\"", "str", "\"a\"", "\"z\"", "Template", "[t\"a\"]", "t\"a\""), null, null, NoSlotTyping),
        [typeof(BooleanLiteral)] = (new("True", "int", "1", "5", "bool", "[True]", "True"), null, null, NoSlotTyping),
        [typeof(NoneLiteral)] = (null, "the literal None has no type of its own",
            new("None", "int", "1", "5", "int | None", "[1, None]", "None"), null),
        [typeof(ListLiteral)] = (new("[@, 2]", "int", "1", "5", "list[int]", "[[1, 2], [3]]", "[1, 2]"), null,
            new("[@, 2]", "int", "1", "5", "list[int64]", "[[1, 2], [3]]", "[1, 2]"), null),
        [typeof(DictLiteral)] = (new("{@: 1}", "int", "1", "5", "dict[int, int]", "[{1: 1}]", "{1: 1}"), null,
            new("{@: 1}", "int", "1", "5", "dict[int64, int64]", "[{1: 1}]", "{1: 1}"), null),
        [typeof(SetLiteral)] = (new("{@, 2}", "int", "1", "5", "set[int]", "[{1, 2}]", "{1, 2}"), null,
            new("{@, 2}", "int", "1", "5", "set[int64]", "[{1, 2}]", "{1, 2}"), null),
        [typeof(TupleLiteral)] = (new("(@, 2)", "int", "1", "5", "tuple[int, int]", "[(1, 2), (3, 4)]", "(1, 2)"), null,
            new("(@, 2)", "int", "1", "5", "tuple[int, uint64]", "[(1, 2), (3, 4)]", "(1, 2)"), null),
        [typeof(ListComprehension)] = (new("[w for w in [@, 2]]", "int", "1", "5", "list[int]", "[[1, 2], [3]]", "[1, 2]"), null,
            null, "a comprehension's element type is its own element expression's"),
        [typeof(GeneratorExpression)] = (new("(w for w in [@, 2])", "int", "1", "5", "Iterator[int]", "[iter([1, 2])]", "iter([1, 2])"), null,
            null, "a comprehension's element type is its own element expression's"),
        [typeof(SetComprehension)] = (new("{w for w in [@, 2]}", "int", "1", "5", "set[int]", "[{1, 2}]", "{1, 2}"), null,
            null, "a comprehension's element type is its own element expression's"),
        [typeof(DictComprehension)] = (new("{w: 1 for w in [@]}", "int", "1", "5", "dict[int, int]", "[{1: 1}]", "{1: 1}"), null,
            null, "a comprehension's element type is its own element expression's"),
        [typeof(DictSpreadComprehension)] = (new("{**d for d in [@]}", "dict[int, int]", "{1: 1}", "{5: 5}", "dict[int, int]", "[{1: 1}]", "{1: 1}"), null,
            null, "a comprehension's element type is its own element expression's"),
        // A bare name has no call to trace: it reads `a` itself, and the Rebind container shows the order.
        [typeof(Identifier)] = (IntForm("a"), null,
            new("f1", "int", "1", "5", "(int) -> int", "[f1, f2]", "f1"), null),
        [typeof(MemberAccess)] = (new("@.v", "Box", "Box(1)", "Box(5)", "int", "[1, 2]", "1"), null, null, NoSlotTyping),
        [typeof(IndexAccess)] = (new("@[0]", "list[int]", "[1]", "[5]", "int", "[1, 2]", "1"), null, null, NoSlotTyping),
        [typeof(SliceAccess)] = (new("@[0:1]", "list[int]", "[1]", "[5]", "list[int]", "[[1], [2]]", "[1]"), null, null, NoSlotTyping),
        // A subscript with a slice dimension (one with indices alone parses to an IndexAccess over a tuple).
        [typeof(MultiAxisAccess)] = (new("@[0:1, 0]", "ndarray[float]", "array([[1.0, 2.0], [3.0, 4.0]])", "array([[5.0, 6.0], [7.0, 8.0]])",
            "ndarray[float]", "[array([1.0])]", "array([1.0])"), null, null, NoSlotTyping),
        [typeof(FunctionCall)] = (IntForm("@"), null, null, NoSlotTyping),
        [typeof(UnaryOp)] = (IntForm("-@", "-1", "-5"), null, null, NoSlotTyping),
        [typeof(QuestionMarkExpression)] = (new("@?", "int?", "Some(1)", "Some(5)", "int", "[1, 2]", "1") { Returns = "int?" }, null,
            null, NoSlotTyping),
        [typeof(BinaryOp)] = (IntForm("@ + 1", "0", "4"), null, null, NoSlotTyping),
        [typeof(ComparisonChain)] = (new("(0 < @ < 3)", "int", "1", "5", "bool", "[True]", "True"), null, null, NoSlotTyping),
        [typeof(ConditionalExpression)] = (IntForm("(@ if len(xs) > 0 else 2)"), null,
            new("(@ if len(xs) > 0 else None)", "int", "1", "5", "int | None", "[1, None]", "1"), null),
        [typeof(LambdaExpression)] = (null, "a lambda has no type of its own: its parameters are typed by its slot",
            new("(lambda w: w)", "int", "1", "5", "(int) -> int", "[f1, f2]", "f1"), null),
        [typeof(TypeCoercion)] = (new("(@ as! int64)", "int", "1", "5", "int64", "[1, 2]", "1"), null, null, NoSlotTyping),
        [typeof(TypeCheck)] = (new("(@ is Color.RED)", "Color", "Color.RED", "Color.GREEN", "bool", "[True]", "True"), null, null, NoSlotTyping),
        [typeof(Parenthesized)] = (IntForm("(@)"), null, null, NoSlotTyping),
        [typeof(WalrusExpression)] = (IntForm("(w := @)"), null, null, NoSlotTyping),
        [typeof(TryExpression)] = (new("(try @)", "int", "1", "5", "Result[int, Exception]", "[ok(1), ok(2)]", "ok(1)"), null, null, NoSlotTyping),
        [typeof(MaybeExpression)] = (new("(maybe @)", "int | None", "1", "5", "int?", "[Some(1), Some(2)]", "Some(1)"), null, null, NoSlotTyping),
        [typeof(AwaitExpression)] = (IntForm("await g(@)") with { Async = true }, null, null, NoSlotTyping),
    };

    /// <summary>How many forms <see cref="LeftForms"/> generates, as a literal: the totality count reads the map the
    /// generator reads, so only this pins a form against silent deletion.</summary>
    private const int LeftFormCount = 43;

    /// <summary>
    /// The <c>Expression</c> records that cannot stand as a left operand, each with its reason, the program that
    /// shows it and the one code that program must still be refused by (<see cref="AssertLeftFormAxisIsTheExpressionCensus"/>
    /// compiles each: an entry whose program is no longer refused that way is red until the form is generated).
    /// </summary>
    private static readonly Dictionary<Type, (string Why, string Program, string RefusedBy)> LeftFormUnspellable = new()
    {
        [typeof(ModifiedArgument)] = ("`ref` / `out` / `in` marks a call-site argument: it parses only inside an argument list",
            "def main() -> None:\n    a: int = 1\n    xs: list[int] = [1]\n    v = ref a in xs\n    print(v)\n",
            DiagnosticCodes.Parser.ExpectedEndOfStatement),
        [typeof(StarExpression)] = ("`*rest` is an unpacking target, never an operand",
            "def main() -> None:\n    a: list[int] = [1]\n    xs: list[list[int]] = [[1]]\n    v = *a in xs\n    print(v)\n",
            DiagnosticCodes.Parser.UnexpectedToken),
        [typeof(SpreadElement)] = ("`*expr` is an element of a collection literal (`[*a, 2]` is the ListLiteral form), never "
            + "an operand: the parser builds no spread outside one",
            "def main() -> None:\n    a: list[int] = [1]\n    xs: list[list[int]] = [[1]]\n    v = *a in xs\n    print(v)\n",
            DiagnosticCodes.Parser.UnexpectedToken),
        [typeof(SuperExpression)] = ("`super()` is only the receiver of a method call",
            "class B:\n    @virtual\n    def m(self, xs: list[B]) -> bool:\n        return False\n\n"
            + "class D(B):\n    @override\n    def m(self, xs: list[B]) -> bool:\n        return super() in xs\n\n"
            + "def main() -> None:\n    print(D().m([B()]))\n",
            DiagnosticCodes.Semantic.InvalidSuperUsage),
        [typeof(EllipsisLiteral)] = ("`...` is not a value: in a value position it reaches Roslyn today (CS8115, a throw "
            + "expression), as an operand and as an assigned value alike, so there is no evaluation to order",
            "def main() -> None:\n    xs: list[int | None] = [1, None]\n    print(... in xs)\n",
            DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError),
        [typeof(MatchExpression)] = ("a match expression is a statement's whole right side: its arms start their own lines, "
            + "so it cannot be an operand, even in parentheses",
            "def main() -> None:\n    a: int = 1\n    xs: list[int] = [1]\n    v = (match a:\n        case 1: 1\n        case _: 2) in xs\n    print(v)\n",
            DiagnosticCodes.Parser.ExpectedNewline),
    };

    /// <summary>
    /// A typing of a form that <see cref="LeftForms"/> leaves out because every cell of it is refused before any
    /// operand is evaluated: the reason, the program that shows it and the one code it must still be refused by
    /// (compiled by <see cref="AssertLeftFormAxisIsTheExpressionCensus"/>; red when that changes, until the
    /// typing is generated).
    /// </summary>
    private static readonly Dictionary<(Type Kind, Typing Typing), (string Why, string Program, string RefusedBy)> LeftFormTypingRefused = new()
    {
        [(typeof(FloatLiteral), Typing.BySlot)] = ("a float literal needle into a `float32` slot is accepted by the checker and "
            + "refused by C# (CS1503 `double` to `float`), where `t: float32 = 1.5` then `t in xs` runs",
            "def main() -> None:\n    xs: list[float32] = [1.5, 2.5]\n    print(1.5 in xs)\n",
            DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError),
    };

    private static IReadOnlyList<Type> ExpressionRecords() =>
        typeof(Expression).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Expression)) && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>The left-operand form axis is closed against the parser's expression model: every non-abstract
    /// subclass of <c>Expression</c> is a key of <see cref="LeftForms"/> or of <see cref="LeftFormUnspellable"/>,
    /// never both, and every key is such a record; each typing of a form has a spelling or a reason, exactly one;
    /// and every unspellable form's program is still refused by its code.</summary>
    private void AssertLeftFormAxisIsTheExpressionCensus()
    {
        var census = ExpressionRecords().Select(t => t.Name).ToList();
        var mapped = LeftForms.Keys.Concat(LeftFormUnspellable.Keys).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(census.SequenceEqual(mapped),
            "the Expression records have no left-operand form entry for [" + string.Join(", ", census.Except(mapped))
            + "] and the maps name [" + string.Join(", ", mapped.Except(census)) + "], which are not Expression records (or are "
            + "named twice): spell each form in LeftForms or exclude it in LeftFormUnspellable with its program");
        foreach (var (kind, (own, ownExcluded, bySlot, bySlotExcluded)) in LeftForms)
        {
            Assert.True((own != null) != (ownExcluded != null) && (bySlot != null) != (bySlotExcluded != null),
                $"{kind.Name}: each typing has a spelling or an exclusion reason, exactly one");
            Assert.True(own != null || bySlot != null, $"{kind.Name}: a form with no spelling belongs in LeftFormUnspellable");
            Assert.False(string.IsNullOrWhiteSpace(ownExcluded ?? bySlotExcluded ?? "-"), $"{kind.Name}: a blank exclusion reason");
        }
        Assert.True(LeftForms.Values.Sum(v => (v.Own != null ? 1 : 0) + (v.BySlot != null ? 1 : 0)) == LeftFormCount,
            $"LeftForms generates {LeftForms.Values.Sum(v => (v.Own != null ? 1 : 0) + (v.BySlot != null ? 1 : 0))} forms; "
            + $"{nameof(LeftFormCount)} says {LeftFormCount}: a form was dropped or added without the literal");
        Assert.All(LeftFormTypingRefused, e => Assert.True(LeftForms.TryGetValue(e.Key.Kind, out var entry)
            && (e.Key.Typing == Typing.Own ? entry.Own : entry.BySlot) == null,
            $"{e.Key.Kind.Name}/{e.Key.Typing}: LeftFormTypingRefused names a typing LeftForms generates, or no form"));
        foreach (var (kind, (why, program, refusedBy)) in LeftFormUnspellable.Select(e => (e.Key.Name, e.Value))
                     .Concat(LeftFormTypingRefused.Select(e => ($"{e.Key.Kind.Name}/{e.Key.Typing}", e.Value))))
        {
            Assert.False(string.IsNullOrWhiteSpace(why), $"{kind}: no reason");
            var run = CompileAndExecute(program, "left_form_unspellable.spy", executionTimeoutMs: 120_000);
            var codes = run.RawDiagnostics.Where(d => d.IsError).Select(d => d.Code ?? "?").Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
            Assert.True(!RanToCompletion(run) && codes.Count == 1 && codes[0] == refusedBy,
                $"{kind}: its exclusion says the program below is refused by {refusedBy}; it "
                + (RanToCompletion(run) ? "now runs" : "is refused by " + string.Join(",", codes))
                + ": spell the form in LeftForms (its cells then join the product)\n" + program);
        }
    }

    /// <summary>A left-operand cell: a form, by its own type or by its slot, against a container form, under a
    /// membership operator, in a position. Its cell (<see cref="CellId"/>) is the expression; its reference
    /// (<see cref="ReferenceId"/>) the desugaring, the left operand bound first to a name of the slot's type.</summary>
    private sealed record LeftCell(int Id, Type Kind, Typing Typing, LeftForm Form, ContainerForm Container, BinaryOperator Operator, Position Position)
    {
        public int CellId => Id * 2;
        public int ReferenceId => Id * 2 + 1;
        public string FormName => Kind.Name + (Typing == Typing.BySlot ? "@slot" : "");
        public string Key => $"{FormName}/{Container}/{OperatorSpelling(Operator)}/{Position}";
        public string RowKey => SequenceRowPrefix + (Position == Position.ChainLink ? "Chain" : "Binary");
    }

    private static List<LeftCell> GenerateLeftCells()
    {
        var cells = new List<LeftCell>();
        foreach (var (kind, entry) in LeftForms.OrderBy(e => e.Key.Name, StringComparer.Ordinal))
            foreach (var (typing, form) in new[] { (Typing.Own, entry.Own), (Typing.BySlot, entry.BySlot) })
            {
                if (form == null)
                    continue;
                foreach (var container in Enum.GetValues<ContainerForm>())
                    foreach (var op in Operators)
                        foreach (var position in Enum.GetValues<Position>())
                            cells.Add(new LeftCell(cells.Count, kind, typing, form, container, op, position));
            }
        return cells;
    }

    private const string LeftFormPrelude =
        "from numpy import ndarray, array\n\n"
        + "def tr[V](cell: int, operand: int, v: V) -> V:\n    print(cell, operand)\n    return v\n\n"
        // The Rebind container: its first argument is evaluated for its effect, its second is the container.
        + "def keep[V, W](v: V, xs: W) -> W:\n    return xs\n\n"
        + "enum Color:\n    RED = 1\n    GREEN = 2\n\n"
        + "class Box:\n    v: int\n    def __init__(self, v: int):\n        self.v = v\n\n"
        + "def f1(v: int) -> int:\n    return v\n\n"
        + "def f2(v: int) -> int:\n    return v + 1\n\n"
        + "async def g(v: int) -> int:\n    return v\n\n"
        + "def ok(v: int) -> Result[int, Exception]:\n    return Ok(v)\n\n";

    /// <summary>One program over the functions still active. A cell evaluates <c>needle op container</c> in its
    /// position (a chain link after a traced head operand, <c>tr(h) == needle op container</c>); its reference binds
    /// the head, then the needle to a name declared by the slot's type, then applies the operator to the name (a
    /// chain: the head link first, the membership link only when it holds). Both print their value. <c>main</c> declares each form's
    /// operands once and calls every function of the form over them.</summary>
    private static Program LeftFormProgramFor(IReadOnlyList<LeftCell> cells, IReadOnlySet<int> active)
    {
        var lines = new List<string>();
        var cellByLine = new Dictionary<int, int>();
        void Add(string line, int? id = null)
        {
            lines.Add(line);
            if (id is { } cell)
                cellByLine[lines.Count] = cell;
        }

        foreach (var line in LeftFormPrelude.TrimEnd('\n').Split('\n'))
            Add(line);
        Add("");

        void AddFunction(LeftCell cell, bool reference)
        {
            var id = reference ? cell.ReferenceId : cell.CellId;
            if (!active.Contains(id))
                return;
            var form = cell.Form;
            var op = OperatorSpelling(cell.Operator);
            var needle = form.Needle.Replace("@", $"tr({id}, 0, a)", StringComparison.Ordinal);
            var container = cell.Container switch
            {
                ContainerForm.Name => "xs",
                ContainerForm.Call => $"tr({id}, 1, xs)",
                _ => "keep((a := c), xs)",
            };
            var chain = cell.Position == Position.ChainLink;
            Add($"{(form.Async ? "async " : "")}def e{id}(pa: {form.A}, c: {form.A}, h: {form.Slot}, xs: list[{form.Slot}]) -> {form.Returns ?? "None"}:", id);
            // A local, so that the Rebind container's walrus rebinds it (one that names a parameter makes every
            // earlier read of the parameter SPY0600).
            Add($"    a: {form.A} = pa", id);
            if (!reference)
            {
                var test = $"{needle} {op} {container}";
                switch (cell.Position)
                {
                    case Position.Expression:
                        Add($"    v = {test}", id);
                        break;
                    case Position.IfCondition:
                        Add("    v = False", id);
                        Add($"    if {test}:", id);
                        Add("        v = True", id);
                        break;
                    case Position.ComprehensionFilter:
                        Add($"    v = len([0 for _ in [0] if {test}]) == 1", id);
                        break;
                    case Position.ChainLink:
                        Add($"    v = tr({id}, 9, h) == {test}", id);
                        break;
                }
            }
            else
            {
                if (chain)
                    Add($"    t9 = tr({id}, 9, h)", id);
                Add($"    t0: {form.Slot} = {needle}", id);
                if (chain)
                {
                    // The head link reads the bound operand; an operand with no evaluation (no traced read: a
                    // literal, a name, a lambda) is spelled in place there, so the link keeps the literal's typing
                    // (`h == None` is refused where `h == t0` over `t0: int | None` is not).
                    Add($"    v = t9 == {(form.Needle.Contains('@') ? "t0" : needle)}", id);
                    Add("    if v:", id);
                    Add($"        v = t0 {op} {container}", id);
                }
                else
                {
                    Add($"    v = t0 {op} {container}", id);
                }
            }
            Add($"    print({id}, v)", id);
            if (form.Returns != null)
                Add("    return a", id);
            Add("");
        }

        foreach (var cell in cells)
        {
            AddFunction(cell, reference: false);
            AddFunction(cell, reference: true);
        }

        Add("async def main() -> None:");
        foreach (var group in cells.GroupBy(c => (c.Kind, c.Typing)))
        {
            var k = group.First().Id;
            var form = group.First().Form;
            Add($"    a{k}: {form.A} = {form.AValue}");
            Add($"    c{k}: {form.A} = {form.CValue}");
            Add($"    h{k}: {form.Slot} = {form.Head}");
            Add($"    xs{k}: list[{form.Slot}] = {form.Members}");
            foreach (var cell in group)
                foreach (var id in new[] { cell.CellId, cell.ReferenceId })
                {
                    if (!active.Contains(id))
                        continue;
                    var call = $"{(form.Async ? "await " : "")}e{id}(a{k}, c{k}, h{k}, xs{k})";
                    Add("    try:", id);
                    Add(form.Returns != null ? $"        print({id}, \"returned\", {call})" : $"        {call}", id);
                    Add("    except Exception:", id);
                    Add($"        print({id}, \"raised\")", id);
                }
        }
        Add("    print(\"done\")");
        return new Program(string.Join("\n", lines) + "\n", cellByLine);
    }

    /// <summary>The left operand of the membership test of a cell's function, as the parser built it: the left
    /// operand of the first <c>in</c> / <c>not in</c> operator, or the operand before that link of a chain.</summary>
    private static Expression? ParsedLeftOperand(FunctionDef function)
    {
        foreach (var node in Descendants(function))
        {
            if (node is BinaryOp { Operator: BinaryOperator.In or BinaryOperator.NotIn } binary)
                return binary.Left;
            if (node is ComparisonChain chain)
                for (var i = 0; i < chain.Operators.Length; i++)
                    if (chain.Operators[i] is ComparisonOperator.In or ComparisonOperator.NotIn)
                        return chain.Operands[i];
        }
        return null;
    }

    /// <summary>
    /// The left operand of a membership test is evaluated before the right one whatever its FORM (#2645): for every
    /// expression record of the parser that can stand as a left operand, by its own type and typed by its slot,
    /// <c>needle op container</c> prints what its DESUGARING prints — the needle bound first to a name declared by
    /// the slot's type, then the operator applied to the name — and is refused exactly when the desugaring is, by a
    /// code the desugaring is refused by. Owner ruling 2026-11-13 (#2645 comment 6867355702, "Close the axis"): the
    /// form is generated from the compiler's own expression node kinds, each covered or excluded with its reason.
    /// <para><b>Generated, never listed.</b> form (<see cref="LeftForms"/>: the image of the <c>Expression</c>
    /// records by reflection, less <see cref="LeftFormUnspellable"/>, each by <see cref="Typing"/>) × right operand
    /// (<see cref="ContainerForm"/>) × the membership operators × <see cref="Position"/>. The count is asserted
    /// from the enumerations, and each cell's left operand is MEASURED to parse to its form's record.</para>
    /// <para><b>Known red.</b> This fact's part (<c>#@ LeftForm</c>) of the <c>Sequence/Binary</c> and
    /// <c>Sequence/Chain</c> rows: the same two causes as the operator sequences'.</para>
    /// </summary>
    [Fact]
    public void Membership_EveryLeftOperandForm_IsItsDesugaring()
    {
        AssertLeftFormAxisIsTheExpressionCensus();
        var cells = GenerateLeftCells();

        // Totality: from the enumerations and the pinned form count, never from the generator's loops.
        var expectedCount = LeftFormCount * Enum.GetValues<ContainerForm>().Length * Operators.Length * Enum.GetValues<Position>().Length;
        Assert.True(expectedCount == cells.Count,
            $"the generator made {cells.Count} left-operand cells; the axes give {expectedCount} ({LeftFormCount} forms x "
            + $"{Enum.GetValues<ContainerForm>().Length} container forms x {Operators.Length} operators x "
            + $"{Enum.GetValues<Position>().Length} positions): a cell was dropped or invented");
        Assert.Equal(cells.Count, cells.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count());

        // Every cell's left operand IS its form: the parser's record for it, parentheses aside.
        var all = cells.SelectMany(c => new[] { c.CellId, c.ReferenceId }).ToHashSet();
        var options = new CompilerOptions
        {
            References = new[] { Sharpy.TestInfrastructure.SharpyCoreReference.Location, SharpyStdlibReference.Location },
            OutputType = "exe",
            WarningsAsErrors = false,
        };
        var parsed = new Compiler(options, NullLogger.Instance).Compile(LeftFormProgramFor(cells, all).Source, "left_operand_form.spy").Module;
        Assert.True(parsed != null, "the left-operand form program does not parse");
        var functions = Descendants(parsed!).OfType<FunctionDef>().ToDictionary(f => f.Name, StringComparer.Ordinal);
        foreach (var cell in cells)
        {
            var operand = ParsedLeftOperand(functions[$"e{cell.CellId}"]);
            while (operand is Parenthesized parenthesized && cell.Kind != typeof(Parenthesized))
                operand = parenthesized.Expression;
            Assert.True(operand?.GetType() == cell.Kind,
                $"{cell.Key}: its left operand parses to {operand?.GetType().Name ?? "nothing"}, not to {cell.Kind.Name}");
        }

        var harnessFailures = new List<string>();
        var verdicts = RunRounds("left operand forms", all.OrderBy(id => id).ToList(), active => LeftFormProgramFor(cells, active),
            "left_operand_form.spy", harnessFailures, out _);
        Assert.True(harnessFailures.Count == 0, "harness: " + string.Join("\n", harnessFailures));
        Assert.Equal(cells.Count * 2, verdicts.Count);

        static string Describe(SharpyVerdict verdict) => verdict.RefusedBy is { } by
            ? "refused " + string.Join(",", by.OrderBy(c => c, StringComparer.Ordinal))
            : "prints " + string.Join(" ", verdict.Values!);
        var divergences = new List<(LeftCell Cell, string Observed)>();
        foreach (var cell in cells)
        {
            var own = verdicts[cell.CellId];
            var reference = verdicts[cell.ReferenceId];
            var meets = own.RefusedBy is { } refusedBy
                ? reference.RefusedBy is { } twin && refusedBy.Overlaps(twin)
                : reference.Values is { } values && own.Values!.SequenceEqual(values);
            if (!meets)
                divergences.Add((cell, $"sharpy {Describe(own)} | desugaring {Describe(reference)}"));
        }

        var reportDir = ReportDirectory();
        var divergencePath = Path.Combine(reportDir, "left-operand-form-divergences.txt");
        File.WriteAllLines(divergencePath, divergences.OrderBy(d => d.Cell.Key, StringComparer.Ordinal).Select(d => $"{d.Cell.Key}  {d.Observed}"));

        var allowlist = LoadAllowlist();
        var actualByRow = divergences.GroupBy(d => d.Cell.RowKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToDictionary(d => d.Cell.Key, d => d.Observed, StringComparer.Ordinal),
                StringComparer.Ordinal);
        var problems = PinProblems(actualByRow, allowlist, LeftFormPart);
        // Never silent: a cell both forms of which reach Roslyn is listed for the lead to file by class.
        var iceInBoth = cells.Where(c => verdicts[c.CellId].RefusedBy is { } a && verdicts[c.ReferenceId].RefusedBy is { } b
            && a.Contains(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError)
            && b.Contains(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError)).ToList();
        var icePath = Path.Combine(reportDir, "left-operand-form-spy0908-in-both-forms.txt");
        File.WriteAllLines(icePath, iceInBoth.Select(c => c.Key).OrderBy(k => k, StringComparer.Ordinal));
        bool Agrees(LeftCell c) => verdicts[c.CellId].Values is { } v && verdicts[c.ReferenceId].Values is { } r && v.SequenceEqual(r);
        Output.WriteLine($"{LeftFormCount} forms of {LeftForms.Count} expression records ({LeftFormUnspellable.Count} unspellable), "
            + $"{cells.Count} cells: {cells.Count(Agrees)} run in both forms and agree, "
            + $"{cells.Count(c => verdicts[c.CellId].RefusedBy != null && verdicts[c.ReferenceId].RefusedBy != null)} are refused in both "
            + $"({iceInBoth.Count} of them by SPY0908 in both forms: {icePath}); {divergences.Count} divergent in "
            + $"{actualByRow.Count} rows; {problems.Count} pin problem line(s). Every divergence: {divergencePath}");

        // Positive controls: the instrument sees every answer it distinguishes.
        // Every reference that ran evaluated its left operand before its container, and a chain's head before both.
        static bool InOrder(IReadOnlyList<string> trace, string first, string second) =>
            !trace.Contains(first) || !trace.Contains(second)
            || trace.Select((v, i) => (v, i)).First(t => t.v == first).i < trace.Select((v, i) => (v, i)).First(t => t.v == second).i;
        Assert.All(cells.Where(c => verdicts[c.ReferenceId].Values != null), c => Assert.True(
            InOrder(verdicts[c.ReferenceId].Values!, "0", "1") && InOrder(verdicts[c.ReferenceId].Values!, "9", "0")
            && InOrder(verdicts[c.ReferenceId].Values!, "9", "1"), $"{c.Key}: its desugaring did not evaluate left to right"));
        // A cell that runs in both forms, traces both operands and agrees; and one whose reference shows the
        // rebinding container did not change what the left operand had read.
        Assert.Contains(cells, c => Agrees(c) && verdicts[c.CellId].Values!.Take(2).SequenceEqual(new[] { "0", "1" }));
        Assert.Contains(cells, c => c.Kind == typeof(Identifier) && c.Typing == Typing.Own && c.Container == ContainerForm.Rebind
            && c.Operator == BinaryOperator.In && c.Position == Position.Expression
            && verdicts[c.ReferenceId].Values is { } r && r.SequenceEqual(new[] { "True" }));
        // A form typed by its slot runs in its desugaring: the reference's declared name takes it.
        Assert.Contains(cells, c => c.Typing == Typing.BySlot && c.Kind == typeof(TupleLiteral) && verdicts[c.ReferenceId].Values != null);
        // A cell refused in both forms by the same code.
        Assert.Contains(cells, c => verdicts[c.CellId].RefusedBy is { } a && verdicts[c.ReferenceId].RefusedBy is { } b && a.SetEquals(b));

        var proposedPath = Path.Combine(reportDir, "left-operand-form-proposed-allowlist.txt");
        File.WriteAllText(proposedPath, string.Join("\n", ProposedAllowlist(actualByRow, allowlist, LeftFormPart)) + "\n");
        if (problems.Count > 0)
        {
            var problemPath = Path.Combine(reportDir, "left-operand-form-pin-problems.txt");
            File.WriteAllLines(problemPath, problems);
            Assert.Fail($"{problems.Count} pin problem line(s) against {AllowlistFileName}: every line is in {problemPath}; "
                + $"a regenerated allowlist (rows' citations kept) is in {proposedPath}. First lines:\n"
                + string.Join("\n", problems.Take(30)));
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // The allowlist: rows by cause, a part per fact, a bulk portion per cause
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>The facts' parts of a row. A row's first part is on the row line (<see cref="PrimaryPartOf"/>);
    /// another fact's part of the same row follows under a <c>#@ Part</c> line.</summary>
    private const string ValuePart = "Value";
    private const string OrderPart = "Order";
    private const string SequencePart = "Sequence";
    private const string LeftFormPart = "LeftForm";

    /// <summary>The parts in the order a row lists them.</summary>
    private static readonly string[] Parts = { ValuePart, OrderPart, SequencePart, LeftFormPart };

    /// <summary>The part the row line itself carries: the fact the row's prefix names, else the value fact.</summary>
    private static string PrimaryPartOf(string row) => row.StartsWith(OrderRowPrefix, StringComparison.Ordinal) ? OrderPart
        : row.StartsWith(SequenceRowPrefix, StringComparison.Ordinal) ? SequencePart
        : ValuePart;

    /// <summary>
    /// The ONE cause the bulk portion of a row may hold, as a predicate on a cell's pinned verdict, or null for a
    /// row that has no bulk portion (owner ruling 2026-11-13, #2645 comment 6867355702: "Accept, pin the residue":
    /// a bulk row is a valid known-red row while all its cells share one cause; the cells that remain once a fix
    /// drains it are written as individual pin lines). Every cell of a row that is not of its cause is a pin line.
    /// <list type="bullet">
    /// <item><c>Order/*</c> — the operand order alone diverges: every cell of the row, by <see cref="Cell.RowKey"/>
    /// (#2645);</item>
    /// <item><c>ChainLink/*</c>, <c>Sequence/Chain</c> — code generation refuses the chain (SPY0519, #2648);</item>
    /// <item><c>UserIterable/*</c> — the protocol validator refuses the class that only iterates (SPY0320,
    /// #2653).</item>
    /// </list>
    /// </summary>
    private static Func<string, bool>? BulkCauseOf(string row)
    {
        static Func<string, bool> RefusedBy(string code) => verdict => verdict.StartsWith($"sharpy refused {code} |", StringComparison.Ordinal);
        if (row.StartsWith(OrderRowPrefix, StringComparison.Ordinal))
            return _ => true;
        if (row.StartsWith(ChainLinkRowPrefix, StringComparison.Ordinal) || row == SequenceRowPrefix + "Chain")
            return RefusedBy(DiagnosticCodes.CodeGen.UnsupportedOperator);
        if (row.StartsWith(UserIterableRowPrefix, StringComparison.Ordinal))
            return RefusedBy(DiagnosticCodes.Semantic.ProtocolMissingMethod);
        return null;
    }

    /// <summary>
    /// The bulk portions: a row's part, and the number of cells of the row's one cause at which the portion is
    /// WHOLE. A portion is written as a count and a digest only at exactly this count; at any other count (a fix
    /// drained some of it) every cell of the part is a pin line and this entry is red until it is deleted. A part
    /// with no entry here is pin lines only. The counts are measurements the facts re-take on every run.
    /// </summary>
    private static readonly Dictionary<(string Row, string Part), int> BulkCensus = new()
    {
        [("ChainLink/NoneNeedle", OrderPart)] = 2078,
        [("ChainLink/NoneNeedle", ValuePart)] = 3176,
        [("ChainLink/Plain", OrderPart)] = 2112,
        [("ChainLink/Plain", ValuePart)] = 2112,
        [("Order/NoneNeedle", OrderPart)] = 3384,
        [("Order/Plain", OrderPart)] = 5058,
        [("Sequence/Chain", SequencePart)] = 6580,
        [("UserIterable/NoneNeedle", OrderPart)] = 720,
        [("UserIterable/NoneNeedle", ValuePart)] = 1236,
        [("UserIterable/Plain", OrderPart)] = 732,
        [("UserIterable/Plain", ValuePart)] = 732,
    };

    /// <summary>How a part's divergent cells are written: the bulk portion (the cells of the row's one cause, when
    /// the census says that portion is whole) and the pin lines (every other cell; every cell when it is not).</summary>
    private static (Dictionary<string, string> Lines, Dictionary<string, string>? Bulk) Layout(
        string row, string part, IReadOnlyDictionary<string, string> cells)
    {
        if (BulkCauseOf(row) is { } cause && BulkCensus.TryGetValue((row, part), out var whole))
        {
            var bulk = cells.Where(c => cause(c.Value)).ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);
            if (bulk.Count == whole && whole > 0)
                return (cells.Where(c => !cause(c.Value)).ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal), bulk);
        }
        return (cells.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal), null);
    }

    /// <summary>The divergent cells of one fact against its part of the allowlist's rows: every part that is
    /// missing or stale, whose pinned cells or verdicts moved, or whose bulk portion is not whole.</summary>
    private static List<string> PinProblems(IReadOnlyDictionary<string, Dictionary<string, string>> actualByRow,
        IReadOnlyDictionary<string, PinnedRow> allowlist, string part)
    {
        var problems = new List<string>();
        var pinnedRows = allowlist.Where(r => r.Value.Parts.ContainsKey(part)).Select(r => r.Key);
        foreach (var row in actualByRow.Keys.Union(pinnedRows).OrderBy(r => r, StringComparer.Ordinal))
        {
            var actual = actualByRow.GetValueOrDefault(row) ?? new Dictionary<string, string>(StringComparer.Ordinal);
            var name = $"{row} [{part}]";
            if (!allowlist.TryGetValue(row, out var pinnedRow) || !pinnedRow.Parts.TryGetValue(part, out var pinned))
            {
                problems.Add($"ROW {name}: not in the allowlist ({actual.Count} divergent cells)");
                problems.AddRange(actual.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => $"  added {a.Key} -> {a.Value}"));
                continue;
            }
            if (actual.Count == 0)
                problems.Add($"ROW {name}: no cell diverges any more — delete this part of the row");
            var (lines, bulk) = Layout(row, part, actual);
            if (pinned.Bulk is { } pinnedBulk)
            {
                // A bulk portion keeps no pin lines: it is whole (its census count, every cell of the row's one
                // cause) with the pinned digest, or it is not bulk.
                if (bulk == null)
                    problems.Add($"ROW {name}: its bulk portion (bulk={pinnedBulk.Count}:{pinnedBulk.Pin}) is not whole: "
                        + $"{(BulkCauseOf(row) is { } cause ? actual.Count(a => cause(a.Value)) : 0)} cell(s) of the row's cause "
                        + $"diverge and BulkCensus says {(BulkCensus.TryGetValue((row, part), out var whole) ? whole.ToString(CultureInfo.InvariantCulture) : "nothing")}. "
                        + "A drained portion is pin lines: take the proposed allowlist and delete the census entry");
                else if (pinnedBulk.Count != bulk.Count || pinnedBulk.Pin != Digest(bulk))
                    problems.Add($"ROW {name}: bulk portion (bulk={pinnedBulk.Count}:{pinnedBulk.Pin}) does not match its "
                        + $"{bulk.Count} divergent cells (pin={Digest(bulk)}): the count is wrong or a verdict changed; the divergence report lists them");
            }
            else if (bulk != null)
            {
                problems.Add($"ROW {name}: BulkCensus says its {bulk.Count} cells of the row's cause are a bulk portion, and the "
                    + "allowlist pins none: take the proposed allowlist");
            }
            if (pinned.Count != pinned.Cells.Count || pinned.Pin != Digest(pinned.Cells))
                problems.Add($"ROW {name}: its header (cells={pinned.Count} pin={pinned.Pin}) does not match its "
                    + $"{pinned.Cells.Count} pin lines (pin={Digest(pinned.Cells)})");
            foreach (var (key, verdict) in lines.OrderBy(a => a.Key, StringComparer.Ordinal))
            {
                if (!pinned.Cells.TryGetValue(key, out var was))
                    problems.Add($"  {name}: added {key} -> {verdict} (a cell outside the pin now diverges)");
                else if (was != verdict)
                    problems.Add($"  {name}: changed {key}: {was} -> {verdict}");
            }
            foreach (var key in pinned.Cells.Keys.Where(k => !lines.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
                problems.Add(actual.ContainsKey(key)
                    ? $"  {name}: {key} is pinned as a line and is a cell of the bulk portion"
                    : $"  {name}: removed {key} (pinned {pinned.Cells[key]}; it now meets its expectation)");
        }
        // A census entry names a portion that is whole: one that is drained, or gone, is red until it is deleted.
        foreach (var ((row, _), whole) in BulkCensus.Where(e => e.Key.Part == part).OrderBy(e => e.Key.Row, StringComparer.Ordinal))
        {
            var ofCause = BulkCauseOf(row) is { } cause && actualByRow.TryGetValue(row, out var actual) ? actual.Count(a => cause(a.Value)) : -1;
            if (ofCause != whole)
                problems.Add($"BulkCensus [{row}, {part}] = {whole}: "
                    + (ofCause < 0 ? "the row has no bulk cause or no divergent cell" : $"{ofCause} cell(s) of the row's cause diverge")
                    + ". A portion that is not whole is not bulk: its cells are pin lines (the proposed allowlist) and the entry is deleted");
        }
        return problems;
    }

    /// <summary>The first 16 hex digits of SHA-256 over the sorted <c>cell -&gt; verdict</c> lines.</summary>
    private static string Digest(IReadOnlyDictionary<string, string> cells)
    {
        var text = string.Concat(cells.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key} -> {c.Value}\n"));
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    /// <summary>The allowlist's header, emitted verbatim by <see cref="ProposedAllowlist"/>.</summary>
    private static readonly string[] AllowlistHeader =
    {
        "# Membership operator differential allowlist (OptionalMembershipDifferentialTests, four facts).",
        "#",
        "# A row is '<class>/<needle or kind>  cells=N  pin=HEX  [bulk=M:HEX]  # citation': one class of cause, citing its",
        "# open issue. The '#= <cell> -> <observed verdict>' lines under it pin its EXACT divergent cells: a pinned cell",
        "# that starts passing, a cell that starts diverging, or a pinned verdict that changes fails the test, and cells=",
        "# and pin= (a SHA-256 prefix over the sorted pin lines) must match them. Each fact pins its own PART of a row:",
        "# the row line is the part of the row's first fact, and a '#@ <Part>  cells=N  pin=HEX  [bulk=M:HEX]' line opens",
        "# another fact's part of the same row (Order: the order observation; LeftForm: the left-operand forms). Pin",
        "# lines and part lines are comments, so scorecard_metrics.py counts one row per class.",
        "#",
        "# 'bulk=M:HEX' is a bulk portion: M cells that share the row's ONE cause (BulkCauseOf in the test), kept as a",
        "# count and a digest while the portion is whole (BulkCensus in the test; owner ruling 2026-11-13, #2645). A cell",
        "# of the row with any other verdict is a pin line, and once a fix drains a portion every cell that remains is.",
        "# On a failure the test writes every problem and a regenerated allowlist under .claude/tmp/; every divergence",
        "# is written there on every run. Delete a part when its last cell passes, and the row with its last part.",
    };

    /// <summary>The allowlist as it should read: this fact's part of every row from its divergent cells
    /// (<see cref="Layout"/>), every other part and every citation as committed.</summary>
    private static IEnumerable<string> ProposedAllowlist(
        IReadOnlyDictionary<string, Dictionary<string, string>> actualByRow, IReadOnlyDictionary<string, PinnedRow> allowlist,
        string part)
    {
        static string Header(Dictionary<string, string> lines, Dictionary<string, string>? bulk) =>
            $"cells={lines.Count}  pin={Digest(lines)}" + (bulk != null ? $"  bulk={bulk.Count}:{Digest(bulk)}" : "");
        foreach (var line in AllowlistHeader)
            yield return line;
        foreach (var row in actualByRow.Keys.Union(allowlist.Keys).OrderBy(r => r, StringComparer.Ordinal))
        {
            allowlist.TryGetValue(row, out var old);
            // Each part of the row: its header and its pin lines.
            var parts = new Dictionary<string, (string Header, Dictionary<string, string> Lines)>();
            foreach (var p in Parts)
            {
                if (p == part)
                {
                    if (actualByRow.TryGetValue(row, out var actual) && actual.Count > 0)
                    {
                        var (lines, bulk) = Layout(row, p, actual);
                        parts[p] = (Header(lines, bulk), lines);
                    }
                }
                else if (old != null && old.Parts.TryGetValue(p, out var kept))
                {
                    parts[p] = ($"cells={kept.Count}  pin={kept.Pin}" + (kept.Bulk is { } b ? $"  bulk={b.Count}:{b.Pin}" : ""), kept.Cells);
                }
            }
            if (parts.Count == 0)
                continue;
            var primary = PrimaryPartOf(row);
            var cite = old?.Citation ?? "# see #<open issue>";
            yield return "";
            yield return $"{row}  {(parts.TryGetValue(primary, out var first) ? first.Header : $"cells=0  pin={Digest(new Dictionary<string, string>())}")}  {cite}";
            foreach (var p in Parts.Where(parts.ContainsKey).OrderBy(p => p == primary ? 0 : 1))
            {
                if (p != primary)
                    yield return $"#@ {p}  {parts[p].Header}";
                foreach (var (key, verdict) in parts[p].Lines.OrderBy(c => c.Key, StringComparer.Ordinal))
                    yield return $"#= {key} -> {verdict}";
            }
        }
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

    /// <summary>The fixed needle-slot types of the container axis (reflected <c>Contains</c> parameters of every
    /// non-element-typed container) that the catalog does not name: the payloads <see cref="SlotPayloads"/> must
    /// spell.</summary>
    private static IReadOnlyList<Type> RequiredSlotPayloadTypes() =>
        SpecRowMap.SelectMany(m => m.Generated)
            .Where(c => !IsElementTyped(c))
            .SelectMany(c => ContainsSlots(ClrTypeOf(c)!))
            .Where(t => PrimitiveCatalog.GetByClrType(t) == null)
            .Distinct()
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

    /// <summary>Every canonical primitive is a payload or an exclusion with its reason — never neither, never both; and
    /// every fixed needle slot of the container axis is a payload's type (a catalog primitive, or a
    /// <see cref="SlotPayloads"/> entry), with no slot payload that is not such a slot.</summary>
    private static void AssertPayloadAxisIsTheCatalogAndTheSlots()
    {
        var canonical = CanonicalPrimitives();
        var payloads = GeneratePayloads();
        var generated = payloads.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var unaccounted = canonical.Where(n => !generated.Contains(n) && !ExcludedPrimitives.ContainsKey(n)).ToList();
        Assert.True(unaccounted.Count == 0,
            "primitives neither generated nor excluded (give them literals or an exclusion reason): " + string.Join(", ", unaccounted));
        var phantom = ExcludedPrimitives.Keys.Where(n => !canonical.Contains(n)).ToList();
        Assert.True(phantom.Count == 0, "excluded names that are not canonical primitives: " + string.Join(", ", phantom));
        Assert.DoesNotContain(generated, ExcludedPrimitives.ContainsKey);

        var required = RequiredSlotPayloadTypes().Select(t => t.FullName!).ToList();
        Assert.True(required.OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(SlotPayloads.Keys.OrderBy(n => n, StringComparer.Ordinal)),
            "the container axis's fixed needle slots outside the catalog are [" + string.Join(", ", required)
            + "]; SlotPayloads spells [" + string.Join(", ", SlotPayloads.Keys.OrderBy(n => n, StringComparer.Ordinal)) + "]");
        Assert.All(payloads.Where(p => p.Kind == PayloadKind.Slot), p => Assert.NotNull(p.ClrType));
        // Every catalog-typed slot is a payload or an excluded primitive.
        var catalogSlots = SpecRowMap.SelectMany(m => m.Generated).Where(c => !IsElementTyped(c))
            .SelectMany(c => ContainsSlots(ClrTypeOf(c)!)).Select(PrimitiveCatalog.GetByClrType).OfType<PrimitiveCatalog.PrimitiveInfo>()
            .Select(i => i.SharpyName).Distinct().ToList();
        Assert.All(catalogSlots, n => Assert.True(generated.Contains(n) || ExcludedPrimitives.ContainsKey(n), $"slot primitive {n}"));
    }

    /// <summary>The composite payloads are the image of <see cref="TypeKind"/>: every kind is a key of
    /// <see cref="TypeKindPayloads"/> and every key a kind; each key generates payloads or names its reason, never both;
    /// each generated Sharpy declaration declares its key's kind (by keyword) and has a python twin; payload names are
    /// distinct across the whole axis.</summary>
    private static void AssertCompositeAxisIsTheTypeKindImage()
    {
        var kinds = Enum.GetValues<TypeKind>().OrderBy(k => k).ToList();
        Assert.True(kinds.SequenceEqual(TypeKindPayloads.Keys.OrderBy(k => k)),
            "TypeKind is [" + string.Join(", ", kinds) + "]; TypeKindPayloads keys are ["
            + string.Join(", ", TypeKindPayloads.Keys.OrderBy(k => k)) + "]: generate or exclude (with a reason) each kind");
        Assert.True(kinds.SequenceEqual(TypeKindKeyword.Keys.OrderBy(k => k)), "TypeKindKeyword must name every TypeKind");
        foreach (var (kind, (generated, excluded)) in TypeKindPayloads)
        {
            Assert.True((generated.Length > 0) != (excluded != null), $"{kind}: payloads or an exclusion reason, exactly one");
            foreach (var payload in generated)
            {
                // The declared name is the payload's WHOLE name: `class KI(IFace):` does not declare `K`.
                Assert.True(payload.SharpyDeclaration is { } decl
                    && decl.Split('\n').Any(l => l.StartsWith(TypeKindKeyword[kind] + payload.Name, StringComparison.Ordinal)
                        && l[(TypeKindKeyword[kind].Length + payload.Name.Length)..] is var rest
                        && (rest.Length == 0 || rest[0] is ':' or '(')),
                    $"{kind}: payload {payload.Name}'s Sharpy declaration does not declare a {kind} named {payload.Name}");
                Assert.False(string.IsNullOrWhiteSpace(payload.PythonDeclaration), $"{kind}: payload {payload.Name} has no python twin");
            }
        }
        // The payload count below reads this map, as the generator does, so a payload deleted from an entry would
        // shrink both together: the count is pinned here as a literal, and an enum by both of its representations.
        Assert.True(TypeKindPayloads.Values.Sum(v => v.Generated.Length) == CompositePayloadCount,
            $"TypeKindPayloads generates {TypeKindPayloads.Values.Sum(v => v.Generated.Length)} payloads; "
            + $"{nameof(CompositePayloadCount)} says {CompositePayloadCount}: a payload was dropped or added without the literal");
        var enumPayloads = TypeKindPayloads[TypeKind.Enum].Generated;
        Assert.True(enumPayloads.Count(p => p.ClrType == null) == 1 && enumPayloads.Count(p => p.ClrType == typeof(string)) == 1
            && enumPayloads.Length == 2,
            "TypeKind.Enum generates exactly an int-backed enum (its own slot) and a string-backed one (a str slot holds it)");
        var names = GeneratePayloads().Select(p => p.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>The settable <c>TypeSymbol</c> property a <c>SemanticType</c> record declares: the definition it
    /// carries (<c>UserDefinedType.Symbol</c>, <c>GenericType.GenericDefinition</c>, <c>SelfType.DeclaringType</c>),
    /// or none.</summary>
    private static PropertyInfo? DefinitionOf(Type record) =>
        record.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SingleOrDefault(p => p.PropertyType == typeof(Sem.TypeSymbol) && p.SetMethod != null);

    private static IReadOnlyList<Type> SemanticTypeRecords() =>
        typeof(Sem.SemanticType).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Sem.SemanticType)) && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    private const string Unconstrained = "Unconstrained";

    /// <summary>The definition axis of a record: the <see cref="TypeKind"/> members for a record that carries a
    /// definition, the parser's constraint clauses (and none) for a type parameter, nothing for the others.</summary>
    private static IReadOnlyList<string> DefinitionAxis(Type record)
    {
        if (record == typeof(Sem.TypeParameterType))
            return typeof(ConstraintClause).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(ConstraintClause)) && !t.IsAbstract)
                .Select(t => t.Name).Append(Unconstrained).OrderBy(n => n, StringComparer.Ordinal).ToList();
        return DefinitionOf(record) != null
            ? Enum.GetValues<TypeKind>().Select(k => k.ToString()).ToList()
            : Array.Empty<string>();
    }

    /// <summary>The key of a type the checker produced: its record, and for a record with a definition axis the
    /// value on it (the definition's kind; a type parameter's constraint clauses).</summary>
    private static string CensusKey(Sem.SemanticType type)
    {
        var record = type.GetType();
        if (type is Sem.TypeParameterType parameter)
            return record.Name + "/" + (parameter.Constraints.IsDefaultOrEmpty
                ? Unconstrained
                : string.Join("+", parameter.Constraints.Select(c => c.GetType().Name).OrderBy(n => n, StringComparer.Ordinal)));
        return DefinitionOf(record) is { } definition
            ? record.Name + "/" + ((definition.GetValue(type) as Sem.TypeSymbol)?.TypeKind.ToString() ?? "no definition")
            : record.Name;
    }

    private static IEnumerable<Node> Descendants(Node node) =>
        node.GetChildNodes().SelectMany(child => Descendants(child).Prepend(child));

    /// <summary>Compiles a probe that declares the payload and reads a <c>T | None</c> parameter of it the way a cell
    /// does (a generic function for a type parameter, a method for <c>Self</c>), and returns the census key of the
    /// type the checker recorded under the <c>| None</c>.</summary>
    private static string MeasuredCensusKey(Payload payload)
    {
        var source = "from ipaddress import IPv4Address, IPv6Address\n\n"
            + (payload.SharpyDeclaration is { } declaration ? declaration.TrimEnd('\n') + "\n" : "")
            + (payload.SelfOf != null
                ? "    def census_probe(self, x: Self | None) -> bool:\n        return x is None\n"
                : $"\ndef census_probe{payload.TypeParameters}(x: {payload.CellSpelling} | None) -> bool:\n    return x is None\n")
            + "\ndef main() -> None:\n    pass\n";
        var options = new CompilerOptions
        {
            References = new[] { Sharpy.TestInfrastructure.SharpyCoreReference.Location, SharpyStdlibReference.Location },
            OutputType = "exe",
            WarningsAsErrors = false,
        };
        var result = new Compiler(options, NullLogger.Instance).Compile(source, "census_probe.spy");
        var errors = result.Diagnostics.GetErrors().Select(d => $"[{d.Code}] {d.Message}").ToList();
        Assert.True(errors.Count == 0 && result.Module != null && result.SemanticInfo != null,
            $"payload {payload.Name}: its census probe does not compile: " + string.Join("; ", errors) + "\n" + source);
        var probe = Descendants(result.Module!).OfType<FunctionDef>().Single(f => f.Name == "census_probe");
        var needle = Descendants(probe).OfType<Identifier>().Single(i => i.Name == "x");
        var type = result.SemanticInfo!.GetExpressionType(needle);
        Assert.True(type is Sem.NullableType,
            $"payload {payload.Name}: the checker typed its `{payload.CellSpelling} | None` needle as "
            + $"{type?.GetType().Name ?? "nothing"} ({type?.GetDisplayName()}), not as a NullableType");
        return CensusKey(((Sem.NullableType)type!).UnderlyingType);
    }

    /// <summary>The census keys, by reflection: a record with a definition axis once per value on it, unless
    /// <see cref="SemanticTypeCensus"/> excludes the record as a whole (its bare name, with a reason); every other
    /// record by its name.</summary>
    private static IReadOnlyList<string> SemanticTypeCensusKeys() =>
        SemanticTypeRecords().SelectMany(record =>
        {
            var axis = DefinitionAxis(record);
            return axis.Count == 0 || SemanticTypeCensus.TryGetValue(record.Name, out var whole) && whole.Excluded != null
                ? new[] { record.Name }
                : axis.Select(value => record.Name + "/" + value);
        }).OrderBy(k => k, StringComparer.Ordinal).ToList();

    /// <summary>The payload axis is closed against the compiler's type model: <see cref="SemanticTypeCensus"/>'s keys
    /// are exactly the reflected census keys; every key generates payloads or names its reason, never both; every
    /// payload's MEASURED key is a generating key; and every generating key has exactly the payloads it pins.</summary>
    private static void AssertPayloadAxisIsTheSemanticTypeCensus()
    {
        Assert.All(SemanticTypeRecords(), record => Assert.True(record.IsSealed, $"{record.Name}: a SemanticType record that is not sealed"));
        // A type parameter is a definition carrier too (its declaring type); its axis is the constraint, named above.
        Assert.NotNull(DefinitionOf(typeof(Sem.TypeParameterType)));
        var census = SemanticTypeCensusKeys();
        var mapped = SemanticTypeCensus.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(census.SequenceEqual(mapped),
            "the SemanticType census (each sealed record; a record with a definition once per kind of definition) has no "
            + "entry for [" + string.Join(", ", census.Except(mapped)) + "] and SemanticTypeCensus names ["
            + string.Join(", ", mapped.Except(census)) + "], which the census has not: generate or exclude (with a reason) each key");
        foreach (var (key, (count, excluded)) in SemanticTypeCensus)
            Assert.True((count > 0) != (excluded != null), $"{key}: payloads or an exclusion reason, exactly one");

        var payloads = GeneratePayloads();
        var measured = payloads.ToDictionary(p => p.Name, MeasuredCensusKey, StringComparer.Ordinal);
        var table = string.Join("; ", measured.OrderBy(m => m.Value, StringComparer.Ordinal).Select(m => $"{m.Key} = {m.Value}"));
        foreach (var (name, key) in measured)
            Assert.True(SemanticTypeCensus.TryGetValue(key, out var entry) && entry.Excluded == null,
                $"payload {name} is a {key} to the checker, which SemanticTypeCensus "
                + (SemanticTypeCensus.ContainsKey(key) ? "excludes" : "does not name") + $". Measured: {table}");
        foreach (var (key, (count, _)) in SemanticTypeCensus.Where(e => e.Value.Excluded == null))
        {
            var at = measured.Where(m => m.Value == key).Select(m => m.Key).OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.True(at.Count == count,
                $"{key}: SemanticTypeCensus pins {count} payload(s); the checker types [{string.Join(", ", at)}] so. Measured: {table}");
        }
        // A scoped payload states its scope: a type parameter names its binding, and is not also Self.
        Assert.All(payloads, p => Assert.True((p.IsTypeParameter == (p.Binding != null)) && !(p.IsTypeParameter && p.SelfOf != null),
            $"payload {p.Name}: an inconsistent scope"));
    }

    /// <summary>Each payload generated without optional-element variants is a payload of the axis, and the defect
    /// that excludes them is still there: its program is refused by exactly the named code. When the defect is fixed
    /// this is red until the entry is deleted, and the variants' cells join the product.</summary>
    private void AssertOptionalElementExclusionsStillHold()
    {
        var names = GeneratePayloads().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (name, (_, _, program, refusedBy)) in OptionalElementExcluded)
        {
            Assert.True(names.Contains(name), $"OptionalElementExcluded names {name}, which is not a payload");
            var run = CompileAndExecute(program, "optional_element_exclusion.spy", executionTimeoutMs: 120_000);
            var codes = run.RawDiagnostics.Where(d => d.IsError).Select(d => d.Code ?? "?").Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
            Assert.True(!RanToCompletion(run) && codes.Count == 1 && codes[0] == refusedBy,
                $"{name}: its optional-element exclusion says the program below is refused by {refusedBy}; it "
                + (RanToCompletion(run) ? "now runs" : "is refused by " + string.Join(",", codes))
                + ": delete the entry from OptionalElementExcluded (the variants' cells then join the product)\n" + program);
        }
    }

    /// <summary>The declaration-order exclusion still holds: its program (the <c>__contains__</c> class below its
    /// use) is refused by exactly the named code, and its control (the class above) runs and prints the lifted
    /// answer. Returns the problem, or null. A problem is red when the defect is fixed, until the entry is deleted
    /// and the order joins the axes.</summary>
    private string? DeclarationOrderExclusionProblem()
    {
        var (_, program, refusedBy, control, controlOutput) = DeclarationOrderExcluded;
        var controlRun = CompileAndExecute(control, "declaration_order_control.spy", executionTimeoutMs: 120_000);
        if (!RanToCompletion(controlRun) || controlRun.StandardOutput.ReplaceLineEndings("\n") != controlOutput)
            return "DeclarationOrderExcluded: its control (the class declared above its use) must run and print "
                + $"[{controlOutput.TrimEnd()}]; it "
                + (RanToCompletion(controlRun) ? $"printed [{controlRun.StandardOutput.TrimEnd()}]" : controlRun.FailureSummary())
                + "\n" + control;
        var run = CompileAndExecute(program, "declaration_order_exclusion.spy", executionTimeoutMs: 120_000);
        var codes = run.RawDiagnostics.Where(d => d.IsError).Select(d => d.Code ?? "?").Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
        if (RanToCompletion(run) || codes.Count != 1 || codes[0] != refusedBy)
            return $"DeclarationOrderExcluded says the program below is refused by {refusedBy}; it "
                + (RanToCompletion(run) ? "now runs" : "is refused by " + string.Join(",", codes))
                + ": delete the entry (declaration order then becomes an axis of the generator)\n" + program;
        return null;
    }

    /// <summary>The CLR row is the census: the roster's keys are exactly the public types of Sharpy.Core and
    /// Sharpy.Stdlib with a public instance single-argument <c>Contains</c>; each generates one container or names why
    /// not; and every container of the CLR row comes from the roster.</summary>
    private static void AssertClrRowIsTheContainsCensus()
    {
        var census = SharpyTypes().Where(t => ContainsSlots(t).Count > 0).Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        var roster = ClrContainerRoster.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(census.SequenceEqual(roster),
            "public single-argument Contains census [" + string.Join(", ", census) + "] vs ClrContainerRoster ["
            + string.Join(", ", roster) + "]: generate or exclude (with a reason) each type, and drop each phantom");
        foreach (var (name, (generated, excluded)) in ClrContainerRoster)
            Assert.True(generated.HasValue != (excluded != null), $"{name}: a container or an exclusion reason, exactly one");
        var fromRoster = ClrContainerRoster.Values.Where(v => v.Generated.HasValue).Select(v => v.Generated!.Value).ToList();
        Assert.Equal(fromRoster.Count, fromRoster.Distinct().Count());
        var clrRow = SpecRowMap.Single(m => m.Row.StartsWith("a CLR type", StringComparison.Ordinal)).Generated;
        Assert.All(clrRow, c => Assert.True(fromRoster.Contains(c) || c == EnumerableArm.Generated, $"{c}: not from the roster"));
        Assert.Contains(EnumerableArm.Generated, clrRow);
        Assert.DoesNotContain(EnumerableArm.Generated, fromRoster);
        // The representative IS of the arm it stands for: no Contains of its own, an IEnumerable<T>.
        var arm = SharpyTypesByName.Value[EnumerableArm.ClrName];
        Assert.Empty(ContainsSlots(arm));
        Assert.Contains(arm.GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    /// <summary>Every exclusion names its reason: a primitive, a census type, a spec row's unexpanded part, the
    /// enumerable arm's representative, and an exemption from the optional-element control (which also names its
    /// issue). An empty or blank reason is red.</summary>
    private static void AssertEveryExclusionHasAReason()
    {
        static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
        Assert.All(ExcludedPrimitives, e => Assert.False(Blank(e.Value), $"primitive {e.Key}: no reason"));
        Assert.All(TypeKindPayloads.Where(e => e.Value.Generated.Length == 0),
            e => Assert.False(Blank(e.Value.Excluded), $"TypeKind {e.Key}: no exclusion reason"));
        Assert.All(OptionalElementExcluded, e => Assert.True(
            !Blank(e.Value.Why) && System.Text.RegularExpressions.Regex.IsMatch(e.Value.Why, "#[0-9]+"),
            $"{e.Key}: an optional-element exclusion needs a reason and an issue"));
        Assert.True(!Blank(DeclarationOrderExcluded.Why)
            && System.Text.RegularExpressions.Regex.IsMatch(DeclarationOrderExcluded.Why, "#[0-9]+"),
            "DeclarationOrderExcluded: an excluded axis needs a reason and an issue");
        Assert.All(SemanticTypeCensus.Where(e => e.Value.Payloads == 0),
            e => Assert.False(Blank(e.Value.Excluded), $"SemanticType census key {e.Key}: no exclusion reason"));
        Assert.All(ClrContainerRoster.Where(r => r.Value.Generated == null),
            r => Assert.False(Blank(r.Value.Excluded), $"census type {r.Key}: no exclusion reason"));
        Assert.All(SpecRowMap.Where(m => m.Excluded != null || m.Generated.Length == 0),
            m => Assert.False(Blank(m.Excluded), $"spec row {m.Row}: no exclusion reason"));
        Assert.False(Blank(EnumerableArm.Why), "EnumerableArm: no reason");
        Assert.All(OptionalElementControlExcluded, e => Assert.True(
            !Blank(e.Value) && System.Text.RegularExpressions.Regex.IsMatch(e.Value, "#[0-9]+"),
            $"{e.Key}: an optional-element control exemption needs a reason and an issue"));
    }

    /// <summary>The container axis is the spec's "Container kinds" table: every row is mapped, every mapped row
    /// exists, and every <see cref="Container"/> comes from exactly one row or one owner ruling
    /// (<see cref="RuledContainerKinds"/>).</summary>
    private static void AssertContainerAxisIsTheSpecTable()
    {
        var spec = PythonInterpreter.FindRepoFile("docs", "language_specification", "membership_operators.md")
            ?? throw new FileNotFoundException("docs/language_specification/membership_operators.md");
        var rows = new List<string>();
        var inTable = false;
        foreach (var line in File.ReadAllLines(spec))
        {
            if (line.StartsWith("### Container kinds", StringComparison.Ordinal))
            {
                inTable = true;
                continue;
            }
            if (!inTable)
                continue;
            if (line.StartsWith("#", StringComparison.Ordinal))
                break;
            if (!line.StartsWith("|", StringComparison.Ordinal) || line.StartsWith("|---", StringComparison.Ordinal))
                continue;
            var first = line.Split('|')[1].Trim();
            if (first != "container")
                rows.Add(first);
        }
        Assert.True(rows.Count >= 5, $"the spec's Container kinds table was not found ({rows.Count} rows)");
        var mapped = SpecRowMap.Select(m => m.Row).ToList();
        Assert.Equal(rows.OrderBy(r => r, StringComparer.Ordinal), mapped.OrderBy(r => r, StringComparer.Ordinal));
        // A ruled kind is generated before the spec carries its row: none of them is a spec row's container, each
        // names its ruling and its reason, and the spec table has no `__iter__` row yet (when it gains R-NZ's row,
        // this is red until that entry moves into SpecRowMap).
        var ruled = RuledContainerKinds.SelectMany(k => k.Generated).ToList();
        Assert.All(RuledContainerKinds, k => Assert.True(k.Generated.Length > 0
            && System.Text.RegularExpressions.Regex.IsMatch(k.Ruling, "^R-[A-Z]+$")
            && System.Text.RegularExpressions.Regex.IsMatch(k.Why, "#[0-9]+"),
            $"{k.Ruling}: a ruled container kind needs its containers, its ruling letter and a reason with an issue"));
        Assert.True(!rows.Any(r => r.Contains("__iter__", StringComparison.Ordinal)),
            "the spec's Container kinds table now has an `__iter__` row: move R-NZ's containers from "
            + "RuledContainerKinds into SpecRowMap under that row");
        var fromRows = SpecRowMap.SelectMany(m => m.Generated).Concat(ruled).ToList();
        Assert.Equal(fromRows.Count, fromRows.Distinct().Count());
        Assert.Equal(Enum.GetValues<Container>().OrderBy(c => c), fromRows.OrderBy(c => c));
    }

    /// <summary>A fact's part of a row: the count and digest its header pins, its pinned cells (<c>#= cell -&gt;
    /// verdict</c> comment lines), and its bulk portion (a count and a digest, no lines), if it has one.</summary>
    private sealed record PinnedPart(int Count, string Pin, Dictionary<string, string> Cells, (int Count, string Pin)? Bulk);

    /// <summary>A row: its citation comment and the parts the facts pin in it (<c>scorecard_metrics.py</c> counts
    /// the row line; part lines and pin lines are comments).</summary>
    private sealed record PinnedRow(string Citation, Dictionary<string, PinnedPart> Parts);

    private static Dictionary<string, PinnedRow> LoadAllowlist()
    {
        var rows = new Dictionary<string, PinnedRow>(StringComparer.Ordinal);
        var path = PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Conformance", AllowlistFileName);
        if (path == null || !File.Exists(path))
            return rows;

        // `cells=N pin=HEX [bulk=M:HEX]`, the header of a row line or of a part line.
        static PinnedPart? ParsePart(string[] tokens, string raw)
        {
            var count = tokens.FirstOrDefault(t => t.StartsWith("cells=", StringComparison.Ordinal));
            var pin = tokens.FirstOrDefault(t => t.StartsWith("pin=", StringComparison.Ordinal));
            var bulk = tokens.FirstOrDefault(t => t.StartsWith("bulk=", StringComparison.Ordinal))?["bulk=".Length..].Split(':');
            Assert.True(count != null && pin != null && bulk is null or { Length: 2 } && !tokens.Contains("bulk"),
                $"{AllowlistFileName}: a row or part needs 'cells=N pin=HEX [bulk=M:HEX]': {raw}");
            var cells = int.Parse(count!["cells=".Length..], CultureInfo.InvariantCulture);
            // An absent part: a row line whose own fact pins nothing in the row.
            if (cells == 0 && bulk == null)
                return null;
            return new PinnedPart(cells, pin!["pin=".Length..], new Dictionary<string, string>(StringComparer.Ordinal),
                bulk != null ? (int.Parse(bulk[0], CultureInfo.InvariantCulture), bulk[1]) : null);
        }

        PinnedRow? current = null;
        PinnedPart? currentPart = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("#=", StringComparison.Ordinal))
            {
                var arrow = line.IndexOf(" -> ", StringComparison.Ordinal);
                Assert.True(currentPart != null && arrow > 2, $"{AllowlistFileName}: a pin line outside a part or without ' -> ': {raw}");
                currentPart!.Cells[line[2..arrow].Trim()] = line[(arrow + 4)..].Trim();
                continue;
            }
            if (line.StartsWith("#@", StringComparison.Ordinal))
            {
                var tokens = line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Assert.True(current != null && tokens.Length > 0 && Parts.Contains(tokens[0]) && !current.Parts.ContainsKey(tokens[0]),
                    $"{AllowlistFileName}: a part line outside a row, or naming an unknown or repeated part: {raw}");
                currentPart = ParsePart(tokens, raw);
                Assert.True(currentPart != null, $"{AllowlistFileName}: an empty part: {raw}");
                current!.Parts[tokens[0]] = currentPart!;
                continue;
            }
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            var citation = hash >= 0 ? line[hash..] : "";
            if (hash >= 0)
                line = line[..hash].Trim();
            if (line.Length == 0)
                continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(System.Text.RegularExpressions.Regex.IsMatch(citation, @"#[0-9]+"),
                $"{AllowlistFileName}: a row needs 'cells=N pin=HEX # see #NNN' (an issue number): {raw}");
            current = new PinnedRow(citation, new Dictionary<string, PinnedPart>(StringComparer.Ordinal));
            currentPart = ParsePart(parts, raw);
            if (currentPart != null)
                current.Parts[PrimaryPartOf(parts[0])] = currentPart;
            rows.Add(parts[0], current);
        }
        Assert.All(rows, r => Assert.True(r.Value.Parts.Count > 0, $"{AllowlistFileName}: row {r.Key} pins nothing"));
        return rows;
    }
}
