using System.Reflection;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
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
/// Stdlib), a referenced type's member (the subclasses of <c>ClrMemberResolution</c>, a field refined by
/// <c>FieldInfo.IsLiteral</c>), the program's own enum (<c>EnumMemberAccessKind</c>), and any other name of the program
/// (<see cref="BindingKind"/>)</description></item>
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
    /// only while the probe holds: when the behaviour changes the probe is red, and the value is generated or re-reasoned.</summary>
    internal sealed record BehaviourProbe(string Source, string Expected, Func<Harness.SharpyRun, bool> Holds, bool Stdlib = false);

    private static bool Refused(Harness.SharpyRun run, string code, string? cs = null)
        => run.ErrorCodes.Contains(code) && (cs == null || run.CsCode == cs);

    private const string TailMain = "def main() -> None:\n    print(1)\n";

    private const string ModuleLetRefused = "a module `let` is refused at every order (SPY0340, P21b)";
    private const string MatchSubjectIsNoValue = "`match one:` / `case x:` over a function value is SPY0908 CS8119 with no outer binding of the name: no callee verdict to compare";
    private const string InterfaceStaticIsNoStatic = "an interface's `@static` value member is SPY0908 CS0120 WITH its initializer (#2383's class): the host has no running control";
    private const string EnumBodyHoldsCases = "an enum body holds `NAME = value` cases and nothing else (SPY0101 / SPY0103)";
    private const string UnionBodyHoldsCases = "a union body holds cases and methods and nothing else (SPY0104)";

    internal static readonly IReadOnlyDictionary<string, BehaviourProbe> BehaviourProbes = new Dictionary<string, BehaviourProbe>(StringComparer.Ordinal)
    {
        [ModuleLetRefused] = new("let x = 5\n" + TailMain, "SPY0340", r => Refused(r, "SPY0340")),
        [MatchSubjectIsNoValue] = new("def one() -> int:\n    return 1\ndef main() -> None:\n    match one:\n        case x:\n            print(x())\n",
            "SPY0908 CS8119", r => Refused(r, "SPY0908", "CS8119")),
        [InterfaceStaticIsNoStatic] = new("interface H:\n    @static\n    x: int = 5\ndef main() -> None:\n    print(H.x)\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120")),
        [EnumBodyHoldsCases] = new("enum H:\n    type K0 = int\n    RED = 1\n" + TailMain, "SPY0103", r => Refused(r, "SPY0103") || Refused(r, "SPY0101")),
        [UnionBodyHoldsCases] = new("union H:\n    type K0 = int\n    case A()\n" + TailMain, "SPY0104", r => Refused(r, "SPY0104")),
        [NeedsItsValue] = new("const x: int\ndef main() -> None:\n    let y: int\n    print(1)\n", "SPY0104", r => r.ErrorCodes.Count(c => c == "SPY0104") == 2),
        [DelegateHasNoBody] = new("delegate D(x: int) -> int:\n    type K0 = int\n" + TailMain, "SPY0100", r => Refused(r, "SPY0100")),
        [EnumBodyHoldsNoConst] = new("enum H:\n    const K: int = 1\n    RED = 1\n" + TailMain, "SPY0101", r => Refused(r, "SPY0101")),
        [UnionBodyHoldsNoConst] = new("union H:\n    const K: int = 1\n    case A()\n" + TailMain, "SPY0104", r => Refused(r, "SPY0104")),
        [LambdaTakesNoCallableSlot] = new("def five() -> int:\n    return 5\ndef main() -> None:\n    f = lambda p: () -> int = five: p\n    print(1)\n", "SPY0103", r => Refused(r, "SPY0103")),
        [AnIntEnum] = new("enum H:\n    RED = 1.5\n" + TailMain, "SPY0253", r => Refused(r, "SPY0253")),
        [NamesAType] = new("class G:\n    class x:\n        v: int = 5\ndef f(p: int = G.x) -> int:\n    return p\n" + TailMain, "SPY0220", r => Refused(r, "SPY0220")),
        [NoSuchMember] = new("def f(p: int = int.nosuch) -> int:\n    return p\n" + TailMain, "SPY0203", r => Refused(r, "SPY0203")),
        [ACallByShape] = new("def five() -> int:\n    return 5\ndef f(p: int = five()) -> int:\n    return p\n" + TailMain, "SPY0401", r => Refused(r, "SPY0401")),
        [ALocalInAConstantPosition] = new("def main() -> None:\n    y: int = 5\n    f = lambda p: int = y: p\n    print(f())\n", "SPY0401", r => Refused(r, "SPY0401")),
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

    /// <summary>The hosts of a member declaration, total over <c>TypeKind</c> with <see cref="ExcludedDeclarationHosts"/>: the
    /// keyword that opens the host's body.</summary>
    internal static readonly IReadOnlyDictionary<TypeKind, string[]> DeclarationHosts = new Dictionary<TypeKind, string[]>
    {
        [TypeKind.Class] = new[] { "class" },
        [TypeKind.Struct] = new[] { "struct" },
    };

    internal static readonly IReadOnlyDictionary<TypeKind, string> ExcludedDeclarationHosts = new Dictionary<TypeKind, string>
    {
        [TypeKind.Interface] = InterfaceStaticIsNoStatic,
        [TypeKind.Enum] = EnumBodyHoldsCases,
        [TypeKind.Union] = UnionBodyHoldsCases,
        [TypeKind.Delegate] = DelegateHasNoBody,
    };

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
    /// typed-read cell with no edit here. The pool places such a reader in the member's own type or outside every type; a third
    /// placement has no host here and is <see cref="UnplacedTypedReaders"/>, which the axes fact holds empty. One reader the pool
    /// does not spell is added: an instance member read through its type's name is read off a constructed instance
    /// (<c>H().x</c>, <c>A().x</c>), from wherever the pool reads a <c>@static</c> field through that name from outside its type.
    /// </summary>
    private static readonly Lazy<(IReadOnlyList<TypedReader> Readers, IReadOnlyList<string> Unplaced)> DerivedTypedReaders = new(() =>
    {
        var readers = new HashSet<TypedReader>();
        var unplaced = new List<string>();
        foreach (var c in Harness.GeneratedCells())
        {
            if (c.Family != Family.Read || c.Tag != SharpyTag.MustRun || c.Outer != BindingKind.None || c.Position is not { } position
                || c.Key is not { Route: null } key || key.Part<Harness.Spelling>() is not { } spelling
                || key.Part<Harness.Order>() != Harness.Order.Earlier || !TypedReads.ContainsKey(c.Target)
                || ExcludedTypedSpellings.ContainsKey(spelling) || ExcludedTypedPositions.ContainsKey(position))
                continue;
            if (c.Relation is not (null or DeclaringRelation.Self))
                unplaced.Add($"{c.Id} (relation {c.Relation})");
            else
                readers.Add(new TypedReader(c.Target, spelling, position, key.Part<Harness.Nesting>() ?? Harness.Nesting.Direct, c.Relation == DeclaringRelation.Self));
        }
        var instances = TypedReads.Where(t => t.Value.Level == MemberLevel.Instance).Select(t => t.Key).ToList();
        foreach (var r in readers.Where(r => r is { Kind: BindingKind.StaticField, Inside: false, Spelling: Harness.Spelling.Qualified or Harness.Spelling.Alias }).ToList())
        {
            foreach (var instance in instances)
                readers.Add(r with { Kind = instance });
        }
        return (readers.OrderBy(r => r.Kind).ThenBy(r => r.Spelling).ThenBy(r => r.Position).ThenBy(r => r.Nesting).ThenBy(r => r.Inside).ToList(), unplaced);
    });

    internal static IReadOnlyList<TypedReader> TypedReaders => DerivedTypedReaders.Value.Readers;

    /// <summary>The pool's spelled reads whose reader sits neither in the member's type nor outside every type.</summary>
    internal static IReadOnlyList<string> UnplacedTypedReaders => DerivedTypedReaders.Value.Unplaced;

    private const string AReadOfItself = "a declaration that reads itself, directly or through a ring, is N3-cycle's: refused or thrown whatever its slot's type (the 8951 pool's Self and Cycle cells)";
    private const string ARecursion = "a constructor that calls itself is a recursion, not an order of two declarations";
    private const string OverflowsTheCompiler = "#2769: named by an annotation, an alias whose target reaches the alias overflows the compiler's stack (`TypeResolver.ExpandTypeAlias`), and a process that dies leaves no verdict a cell can read (so no probe either: it would take the test host with it). The class is unruled and out of P78 (owner, #2423 comment 7042723494 item 9), so only the UNUSED alias has cells, and they keep running";

    /// <summary>The orders with no cell, per cell set that has the axis: total over <c>Order</c> with the two every set generates.</summary>
    internal static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<Harness.Order, string>> ExcludedOrders = new Dictionary<string, IReadOnlyDictionary<Harness.Order, string>>(StringComparer.Ordinal)
    {
        ["typed-read"] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = AReadOfItself, [Harness.Order.Cycle] = AReadOfItself },
        ["init-overload"] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = ARecursion, [Harness.Order.Cycle] = ARecursion },
        ["alias-target"] = new Dictionary<Harness.Order, string>(),
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

    internal static readonly IReadOnlyDictionary<LocalTarget, string> ExcludedCalleeBinders = new Dictionary<LocalTarget, string>
    {
        [LocalTarget.ExceptAs] = "binds an exception, which no call calls",
        [LocalTarget.MatchCapture] = MatchSubjectIsNoValue,
        [LocalTarget.AsPatternCapture] = MatchSubjectIsNoValue,
        [LocalTarget.OutLet] = "binds an out argument: a function-typed out parameter is #2154's class",
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
    /// it takes an int only, whether its slot can be given a function type, and whether python has the same position.</summary>
    internal sealed record ConstantHost(string Label, string Program, string Refusal, ReadPosition? Position = null, string? Imports = null, bool PrintsOne = false,
        bool IntOnly = false, bool NoCallableSlot = false, bool Twin = false);

    /// <summary><c>main</c> for a host whose slot is read by <paramref name="read"/>, after <paramref name="before"/>.</summary>
    private static string ShowIn(string read, string before = "") => $"{Main}{before}    v: {{T}} = {read}\n    print({{s}})";

    private const string AttributeTarget = "    def f(self) -> int:\n        return 1\n" + Main + "    print(H().f())";

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
        [ScopeKind.Module] = new[] { new ConstantHost("module-const", "const K: {T} = {e}\n" + ShowIn("K"), "SPY0401") },
        [ScopeKind.TypeBody] = Array.Empty<ConstantHost>(),
        [ScopeKind.FunctionLike] = new[] { new ConstantHost("local-const", ShowIn("K", "    const K: {T} = {e}\n"), "SPY0401") },
        [ScopeKind.Block] = new[] { new ConstantHost("block-const", $"{Main}    if True:\n        const K: {{T}} = {{e}}\n        v: {{T}} = K\n        print({{s}})", "SPY0401") },
    };

    internal static readonly IReadOnlyDictionary<ScopeKind, string> ExcludedConstScopes = new Dictionary<ScopeKind, string>
    {
        [ScopeKind.InitializerHost] = NotADeclarationScope,
        [ScopeKind.Transparent] = HoldsTypeParameters,
    };

    /// <summary>The type bodies a <c>const</c> is declared in, total over <c>TypeKind</c> with <see cref="ExcludedConstTypeHosts"/>.</summary>
    internal static readonly IReadOnlyDictionary<TypeKind, ConstantHost[]> ConstTypeHosts = new Dictionary<TypeKind, ConstantHost[]>
    {
        [TypeKind.Class] = new[] { new ConstantHost("class-const", "class H:\n    const K: {T} = {e}\n" + ShowIn("H.K"), "SPY0401", ReadPosition.ConstInitializer) },
        [TypeKind.Struct] = new[] { new ConstantHost("struct-const", "struct H:\n    const K: {T} = {e}\n    w: int = 1\n" + ShowIn("H.K"), "SPY0401") },
        [TypeKind.Interface] = new[] { new ConstantHost("interface-const", "interface H:\n    const K: {T} = {e}\n    def m(self) -> int: ...\n" + ShowIn("H.K"), "SPY0401") },
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
            new ConstantHost("def", "def f(p: {T} = {e}) -> {T}:\n    return p\n" + ShowIn("f()"), "SPY0401", ReadPosition.ParameterDefault, Twin: true),
        },
        ["ConstantPositionValidator.cs:ValidateLambdaDefaults"] = new[]
        {
            new ConstantHost("lambda", ShowIn("f()", "    f = lambda p: {T} = {e}: p\n"), "SPY0401", NoCallableSlot: true),
        },
        // The two loops of one method: an attribute's positional arguments, then its named ones.
        ["ConstantPositionValidator.cs:ValidateDecoratorConstantArguments"] = new[]
        {
            new ConstantHost("attribute", "class H:\n    @[system.component_model.default_value({e})]\n" + AttributeTarget, "SPY0425", PrintsOne: true),
        },
        ["ConstantPositionValidator.cs:ValidateDecoratorConstantArguments#2"] = new[]
        {
            new ConstantHost("attribute-keyword", "class Mark(Attribute):\n    w: object = 0\nclass H:\n    @[Mark(w={e})]\n" + AttributeTarget, "SPY0425",
                Imports: "from system import Attribute", PrintsOne: true),
        },
        ["ConstantPositionValidator.cs:ValidateDataclassFieldDefaults"] = new[]
        {
            new ConstantHost("dataclass", "@dataclass\nclass H:\n    p: {T} = {e}\n" + ShowIn("H().p"), "SPY0401", ReadPosition.DataclassFieldInitializer, Twin: true),
        },
        ["ConstantPositionValidator.cs:ValidateStructFieldDefaults"] = new[]
        {
            new ConstantHost("struct", "struct H:\n    p: {T} = {e}\n" + ShowIn("H().p"), "SPY0401", ReadPosition.StructFieldInitializer, Twin: true),
        },
        ["ConstEligibility.cs:Compute"] = AllConstSites().ToArray(),
        // A struct's defaulted auto-property (#1938): a constant default becomes the synthesized constructor's parameter default,
        // and any other default keeps its initializer, so every expression must run here (ruling 8).
        ["CodeGenInfoComputer.cs:MarkStructConstructorProperty"] = new[]
        {
            new ConstantHost("struct-auto-property", "struct H:\n    property p: {T} = {e}\n" + ShowIn("H().p"), "SPY0401"),
        },
    };

    internal static readonly IReadOnlyDictionary<string, string> ExcludedConstantReaders = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ConstEligibility.cs:IsExportedConstCompileTime"] = OutsideOneFile,
    };

    internal static readonly IReadOnlyDictionary<AdmissionTable, string> ExcludedAdmissionTables = new Dictionary<AdmissionTable, string>();

    /// <summary>The one constant position no classifier call judges: an enum member's value. A table value added for it has no
    /// reader here, which is red until this host moves under it.</summary>
    internal static readonly ConstantHost EnumMemberHost = new("enum-member", $"enum H:\n    RED = {{e}}\n{Main}    print(H.RED.value)", "SPY0401", ReadPosition.EnumMemberValue, IntOnly: true);

    /// <summary>Every host with the table its reader passes; the enum member's value last, with none.</summary>
    internal static IEnumerable<(AdmissionTable? Table, ConstantHost Host)> AllConstantHosts()
    {
        var census = ClassifierReaders.Value.Sites;
        return ConstantReaders.Where(r => census.ContainsKey(r.Key))
            .SelectMany(r => r.Value.Select(h => (Table: (AdmissionTable?)census[r.Key], Host: h)))
            .OrderBy(h => h.Table).ThenBy(h => h.Host.Label, StringComparer.Ordinal)
            .Append((Table: null, Host: EnumMemberHost));
    }

    private const string LambdaTakesNoCallableSlot = "a lambda parameter's annotation cannot be a function type and take a default: `lambda p: () -> int = five: p` is SPY0103";
    private const string AnIntEnum = "the host is an int enum, whose member's value is an int: a `str` value declares a string enum, another host, and any other type is SPY0253";

    /// <summary>Why a host has no cell for a slot of <paramref name="type"/>, or null when it has one.</summary>
    internal static string? HostSkips(ConstantHost host, string type)
        => host.IntOnly && type != "int" ? AnIntEnum
            : host.NoCallableSlot && type.Contains("->", StringComparison.Ordinal) ? LambdaTakesNoCallableSlot
            : null;

    /// <summary>The expression: its label, text, type and what it prints; the import line and the declarations it needs; how
    /// <c>main</c> shows a value of its type (<c>{0}</c> the read); the codes its own kind's refusal carries beside the host's;
    /// the binding kind a bare name of it has; the value of a referenced module it names (<c>module.name</c>, what
    /// <see cref="StdlibModuleValues"/> is keyed by) or the member of a referenced type; and its read form where it has one.</summary>
    internal sealed record ConstantExpression(string Label, string Text, string Type, string Prints, string? Import = null, string? Prelude = null, string Show = "{0}",
        string[]? Refusals = null, BindingKind Target = BindingKind.None, string? ModuleValue = null, Type? Owner = null, string? Member = null, Harness.Form? Form = null)
    {
        public bool Stdlib => ModuleValue != null;
    }

    private static ConstantExpression ModuleValue(string module, string name, string type, string prints)
        => new($"{module}.{name}", $"{module}.{name}", type, prints, Import: $"import {module}", ModuleValue: $"{module}.{name}");

    /// <summary>The same value named bare through <c>from module import name</c>: a spelling, which changes no verdict (N7).</summary>
    private static ConstantExpression ImportedValue(string module, string name, string type, string prints)
        => new($"from-{module}-import-{name}", name, type, prints, Import: $"from {module} import {name}", Target: BindingKind.ImportedName, ModuleValue: $"{module}.{name}");

    private static ConstantExpression TypeMember(string text, string type, string prints, Type owner, string member, string? import = null, string show = "{0}", string[]? refusals = null, Harness.Form? form = null)
        => new(text, text, type, prints, Import: import, Show: show, Refusals: refusals, Owner: owner, Member: member, Form: form);

    private const string IntEnum = "enum E0:\n    RED = 5";
    private const string StringEnum = "enum E0:\n    RED = \"r\"";

    internal static readonly IReadOnlyDictionary<ScopeConstantKind, ConstantExpression[]> ConstantExpressions = new Dictionary<ScopeConstantKind, ConstantExpression[]>
    {
        [ScopeConstantKind.ClrLiteralField] = new[] { ModuleValue("subprocess", "PIPE", "int", "-1"), ImportedValue("subprocess", "PIPE", "int", "-1") },
        [ScopeConstantKind.StdlibModuleVariable] = new[]
        {
            ModuleValue("socket", "AF_INET", "int", "2"),
            ModuleValue("math", "pi", "float", "3.141592653589793"),
            ImportedValue("math", "pi", "float", "3.141592653589793"),
        },
        [ScopeConstantKind.ClrStaticMember] = new[] { ModuleValue("sys", "maxsize", "int", "2147483647"), ImportedValue("sys", "maxsize", "int", "2147483647") },
        // A literal field through each receiver a type has: a builtin's own name, an imported name, a module-qualified name.
        [ScopeConstantKind.ClrTypeLiteralField] = new[]
        {
            TypeMember("int.max_value", "int", "2147483647", typeof(int), "max_value"),
            TypeMember("Int32.max_value", "int", "2147483647", typeof(int), "max_value", "from system import Int32"),
            TypeMember("system.Int32.max_value", "int", "2147483647", typeof(int), "max_value", "import system"),
            TypeMember("Math.PI", "float", "3.141592653589793", typeof(Math), "PI", "from system import Math"),
        },
        [ScopeConstantKind.ClrTypeEnumMember] = new[] { TypeMember("DayOfWeek.Monday", "DayOfWeek", "DayOfWeek.Monday", typeof(DayOfWeek), "Monday", "from system import DayOfWeek") },
        [ScopeConstantKind.ClrTypeStaticField] = new[] { TypeMember("BitConverter.is_little_endian", "bool", "True", typeof(BitConverter), "is_little_endian", "from system import BitConverter") },
        [ScopeConstantKind.ClrTypeStaticProperty] = new[] { TypeMember("Array.max_length", "int", "2147483591", typeof(Array), "max_length", "from system import Array") },
        [ScopeConstantKind.ClrTypeMethod] = new[] { TypeMember("Math.sqrt", "(float) -> float", "5.0", typeof(Math), "sqrt", "from system import Math", "{0}(25.0)", form: Harness.Form.Value) },
        [ScopeConstantKind.ClrTypeMethodGroup] = new[]
        {
            TypeMember("Math.abs", "(int) -> int", "5", typeof(Math), "abs", "from system import Math", "{0}(-5)", new[] { "SPY0336" }, Harness.Form.Value),
        },
        [ScopeConstantKind.IntEnumMember] = new[] { new ConstantExpression("member", "E0.RED", "E0", "5", Prelude: IntEnum, Show: "{0}.value", Target: BindingKind.ModuleEnum) },
        [ScopeConstantKind.IntEnumValue] = new[] { new ConstantExpression("value", "E0.RED.value", "int", "5", Prelude: IntEnum, Target: BindingKind.ModuleEnum, Form: Harness.Form.EnumValue) },
        [ScopeConstantKind.IntEnumName] = new[] { new ConstantExpression("name", "E0.RED.name", "str", "RED", Prelude: IntEnum, Target: BindingKind.ModuleEnum) },
        [ScopeConstantKind.StringEnumMember] = new[] { new ConstantExpression("member", "E0.RED", "E0", "r", Prelude: StringEnum, Show: "{0}.value", Target: BindingKind.ModuleEnum) },
        [ScopeConstantKind.StringEnumValue] = new[] { new ConstantExpression("value", "E0.RED.value", "str", "r", Prelude: StringEnum, Target: BindingKind.ModuleEnum, Form: Harness.Form.EnumValue) },
        [ScopeConstantKind.StringEnumName] = new[] { new ConstantExpression("name", "E0.RED.name", "str", "RED", Prelude: StringEnum, Target: BindingKind.ModuleEnum) },
        [ScopeConstantKind.ComparisonChain] = new[] { new ConstantExpression("ordered", "1 < 2 < 3", "bool", "True") },
        [ScopeConstantKind.ChainWithNonConstantLink] = new[] { new ConstantExpression("membership", "0 < 2 in [1, 2]", "bool", "True") },
    };

    /// <summary>What discovery records for a value of a referenced module, and the kind that is its image: the two facts of
    /// <c>FieldSignature</c> a constant position's answer can read.</summary>
    internal static readonly IReadOnlyDictionary<(bool IsConst, bool IsGuarded), ScopeConstantKind> ModuleValueShapes = new Dictionary<(bool, bool), ScopeConstantKind>
    {
        [(true, false)] = ScopeConstantKind.ClrLiteralField,
        [(false, true)] = ScopeConstantKind.StdlibModuleVariable,
        [(false, false)] = ScopeConstantKind.ClrStaticMember,
    };

    /// <summary>The shapes no value of the Stdlib has: the census holds each at 0.</summary>
    internal static readonly IReadOnlyDictionary<(bool IsConst, bool IsGuarded), string> UnproducedModuleValueShapes = new Dictionary<(bool, bool), string>
    {
        [(true, true)] = "a literal has no storage to guard",
    };

    /// <summary>Every value of every module of the Stdlib, as discovery records it.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, (bool IsConst, bool IsGuarded)>> StdlibModuleValues = new(() =>
    {
        var index = new OverloadIndexBuilder().BuildFromAssembly(SharpyStdlibReference.Assembly);
        var values = new Dictionary<string, (bool, bool)>(StringComparer.Ordinal);
        foreach (var (module, overloads) in index.Modules)
        {
            foreach (var (name, field) in overloads.Fields)
                values[$"{module}.{name}"] = (field.IsConst, field.IsGuarded);
        }
        return values;
    });

    private const string LiteralFieldShape = "Field/literal";
    private const string EnumFieldShape = "Field/enum";
    private const string StaticFieldShape = "Field/static";

    /// <summary>
    /// What the checker's resolver of a referenced type's member (<c>ClrMemberTypeResolver</c>) answers for a member read through
    /// the type, and the kind that is its image: the subclasses of <c>ClrMemberResolution</c>, by name, with a field refined by
    /// the two facts reflection has and discovery does not (<c>FieldInfo.IsLiteral</c>; a literal of an enum type is the enum's
    /// member). Total over those subclasses with <see cref="ExcludedClrTypeMemberShapes"/>.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, ScopeConstantKind> ClrTypeMemberShapes = new Dictionary<string, ScopeConstantKind>(StringComparer.Ordinal)
    {
        [LiteralFieldShape] = ScopeConstantKind.ClrTypeLiteralField,
        [EnumFieldShape] = ScopeConstantKind.ClrTypeEnumMember,
        [StaticFieldShape] = ScopeConstantKind.ClrTypeStaticField,
        [nameof(ClrMemberResolution.Property)] = ScopeConstantKind.ClrTypeStaticProperty,
        [nameof(ClrMemberResolution.Method)] = ScopeConstantKind.ClrTypeMethod,
        [nameof(ClrMemberResolution.MethodGroup)] = ScopeConstantKind.ClrTypeMethodGroup,
    };

    private const string UncheckedAtAnAttribute = "; at an attribute argument, which the checker does not visit today, it is one more cell of the class the instance-member and method-group cells there hold red, with the same cure";
    private const string NamesAType = "names a type: handed to a slot of another type it is the checker's own mismatch (SPY0220) at every position the checker checks" + UncheckedAtAnAttribute;
    private const string NoSuchMember = "no member of the name: the checker refuses the read itself (SPY0203) and binds nothing" + UncheckedAtAnAttribute;
    private const string TheCheckerDeclines = "the resolver declines to describe the member, so the checker binds nothing: a member nothing bound is outside the bound-member kinds by definition, and plan-777361's D8 leaves it to the shape rule";

    internal static readonly IReadOnlyDictionary<string, string> ExcludedClrTypeMemberShapes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(ClrMemberResolution.NestedType)] = NamesAType,
        [nameof(ClrMemberResolution.AbsentResult)] = NoSuchMember,
        [nameof(ClrMemberResolution.InconclusiveResult)] = TheCheckerDeclines,
    };

    /// <summary>The shape of <paramref name="member"/> read through <paramref name="owner"/>, as the checker's resolver answers it.</summary>
    internal static string ClrTypeMemberShape(Type owner, string member)
    {
        var resolution = new ClrMemberTypeResolver(new ClrTypeBridge()).Resolve(owner, member, ClrReceiverKind.StaticType);
        if (resolution is not ClrMemberResolution.Field field)
            return resolution.GetType().Name;
        var info = owner.GetField(field.ClrName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        return info is not { IsLiteral: true } ? StaticFieldShape : owner.IsEnum ? EnumFieldShape : LiteralFieldShape;
    }

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

    /// <summary>The kinds that are no member: closed by call 45 (a chain is admitted when each link is).</summary>
    internal static readonly IReadOnlySet<ScopeConstantKind> ChainKinds = new HashSet<ScopeConstantKind> { ScopeConstantKind.ComparisonChain, ScopeConstantKind.ChainWithNonConstantLink };

    /// <summary>A read of a name the program declares: the kind, the spelling of its declaration's type, through which type the
    /// member is named (<c>Base</c>: a subclass), its read form where it is a callable, the declarations, the read, the slot's
    /// type, how <c>main</c> shows the value, and the codes the kind's own refusal carries.</summary>
    internal sealed record OwnRead(BindingKind Kind, ScopeAnnotation? Annotation, DeclaringRelation? Relation, Harness.Form? Form, string? Prelude, string Text, string Type, string Show, string[]? Refusals);

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

    /// <summary>
    /// The program's own names as a constant position is handed them: every kind of the typed-read set (<see cref="TypedReads"/>:
    /// each kind with an int-valued declaration of <c>x</c>), under each spelling of its declaration's type, named bare at module
    /// level and through its type's name or a subclass's otherwise, a callable read UNCALLED (called, it is
    /// <see cref="ACallByShape"/>); and a builtin function, which is declared by no program. An instance member named through a
    /// type is the kind refusal SPY0290 whatever the position.
    /// </summary>
    internal static readonly IReadOnlyList<OwnRead> OwnReads = BuildOwnReads().ToList();

    private static IEnumerable<OwnRead> BuildOwnReads()
    {
        foreach (var (kind, t) in TypedReads.OrderBy(e => e.Key))
        {
            bool callable = t.Member.EndsWith("()", StringComparison.Ordinal);
            string name = callable ? t.Member[..^2] : t.Member;
            foreach (var annotation in Enum.GetValues<ScopeAnnotation>())
            {
                var declaration = annotation == ScopeAnnotation.Annotated ? t.Annotated : t.Inferred;
                if (declaration == null)
                    continue;
                foreach (var relation in t.Level == MemberLevel.Module ? new DeclaringRelation?[] { null } : new DeclaringRelation?[] { null, DeclaringRelation.Base })
                {
                    var prelude = new List<string>();
                    if (t.NeedsFive)
                        prelude.Add(Five);
                    prelude.Add(t.Level == MemberLevel.Module ? declaration : "class G:\n" + Indent(declaration));
                    if (relation == DeclaringRelation.Base)
                        prelude.Add("class D(G):\n    pass");
                    string receiver = t.Level == MemberLevel.Module ? "" : relation == DeclaringRelation.Base ? "D." : "G.";
                    yield return new OwnRead(kind, annotation, relation, callable ? Harness.Form.Value : null, string.Join("\n", prelude), receiver + name,
                        callable ? "() -> int" : "int", callable ? "{0}()" : "{0}", t.Level == MemberLevel.Instance ? new[] { "SPY0290" } : null);
                }
            }
        }
        yield return new OwnRead(BindingKind.Builtin, null, null, Harness.Form.Value, null, "abs", "(int) -> int", "{0}(-5)", null);
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
        [$"TypeAnnotation.{nameof(AstTypeAnnotation.IsNameBacktickEscaped)}"] = "the escape picks the namespace a spelling is looked up in: a missing name is missing in both, and the escaped lookup is #1325's",
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
        Carried("no-initializer", DeclarationHosts.Values.SelectMany(h => h).Concat(ValueTypes.Values.SelectMany(v => v).Select(v => v.Label)), Marks);

        // typed-read
        Partition("typed-read × kind", Enum.GetValues<BindingKind>(), TypedReads.Keys, ExcludedTypedReads.Keys);
        Partition("typed-read × spelling", Enum.GetValues<Harness.Spelling>(), Route("typed-read").SelectMany(Part<Harness.Spelling>), ExcludedTypedSpellings.Keys);
        Partition("typed-read × reader position", Enum.GetValues<ReadPosition>(), Route("typed-read").Select(c => c.Cell.Position!.Value), ExcludedTypedPositions.Keys);
        Carried("typed-read", TypedReads.Keys, c => new[] { c.Cell.Target });
        Carried("typed-read", Enum.GetValues<ScopeAnnotation>(), Part<ScopeAnnotation>);
        foreach (var unplaced in UnplacedTypedReaders.Take(10))
            problems.Add($"typed-read: the 8951 pool spells a read this set has no reader host for: {unplaced}");
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
        var census = StdlibModuleValues.Value;
        Partition("constant-kind × module value", census.Values.Distinct().Concat(UnproducedModuleValueShapes.Keys), ModuleValueShapes.Keys, UnproducedModuleValueShapes.Keys);
        foreach (var shape in UnproducedModuleValueShapes.Keys.Where(s => census.Values.Contains(s)))
            problems.Add($"constant-kind × module value: {census.First(v => v.Value == shape).Key} is {shape}, which is listed as a shape no value has");
        Partition("constant-kind × type member",
            typeof(ClrMemberResolution).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Where(t => t.IsSubclassOf(typeof(ClrMemberResolution)))
                .SelectMany(t => t.Name == nameof(ClrMemberResolution.Field) ? new[] { LiteralFieldShape, EnumFieldShape, StaticFieldShape } : new[] { t.Name }),
            ClrTypeMemberShapes.Keys, ExcludedClrTypeMemberShapes.Keys);
        Partition("constant-kind × enum access", Enum.GetValues<EnumMemberAccessKind>(), EnumReadKinds.Values.Where(v => v.Access != null).Select(v => v.Access!.Value), Array.Empty<EnumMemberAccessKind>());
        foreach (var intShape in new[] { true, false }.Where(isInt => !EnumReadKinds.Values.Contains((isInt, (EnumMemberAccessKind?)null))))
            problems.Add($"constant-kind × enum access: the member of {(intShape ? "an int" : "a string")} enum has no kind");
        Partition("constant-kind × kind", Enum.GetValues<ScopeConstantKind>(),
            ModuleValueShapes.Values.Concat(ClrTypeMemberShapes.Values).Concat(EnumReadKinds.Keys).Append(ScopeConstantKind.OwnMember), ChainKinds);
        var ownKinds = OwnReads.Select(r => r.Kind).Append(BindingKind.ModuleEnum).Append(BindingKind.ImportedName).Distinct().ToList();
        Partition("constant-kind × own name", Enum.GetValues<BindingKind>(), ownKinds, ExcludedOwnReads.Keys);
        Carried("constant-kind", ownKinds, c => new[] { c.Cell.Target });
        Carried("constant-kind", Enum.GetValues<ScopeAnnotation>(), Part<ScopeAnnotation>);
        Carried("constant-kind", new[] { DeclaringRelation.Base }, Part<DeclaringRelation>);
        Partition("constant-kind × read form", Enum.GetValues<Harness.Form>(), Route("constant-kind").SelectMany(Part<Harness.Form>), ExcludedConstantForms.Keys);
        foreach (var (kind, expressions) in ConstantExpressions)
        {
            foreach (var e in expressions)
            {
                if (e.ModuleValue != null)
                {
                    if (!census.TryGetValue(e.ModuleValue, out var recorded))
                        problems.Add($"constant-kind: discovery records no value {e.ModuleValue}");
                    else if (!ModuleValueShapes.TryGetValue(recorded, out var image) || image != kind)
                        problems.Add($"constant-kind: {e.Label} is listed under {kind}, and discovery records {e.ModuleValue} (IsConst, IsGuarded) = {recorded}");
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
                    if (e.Prelude != (read.IntShape ? IntEnum : StringEnum) || !e.Text.EndsWith(suffix, StringComparison.Ordinal))
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

        // An exclusion that rests on the compiler names its probe, and a probe has its exclusion.
        var reasons = ExcludedDeclarations.Values.Concat(ExcludedDeclarationHosts.Values).Concat(ExcludedValueTypes.Values).Concat(ExcludedTypedReads.Values)
            .Concat(ExcludedTypedSpellings.Values).Concat(ExcludedTypedPositions.Values).Concat(ExcludedCalleeBinders.Values).Concat(ExcludedCalleeOuters.Values)
            .Concat(ExcludedAdmissionTables.Values).Concat(UnproducedModuleValueShapes.Values).Concat(ExcludedAliasScopes.Values).Concat(ExcludedAliasTypeHosts.Values)
            .Concat(ExcludedAliasShapeProperties.Values).Concat(ExcludedOrders.Values.SelectMany(o => o.Values))
            .Concat(ExcludedConstScopes.Values).Concat(ExcludedConstTypeHosts.Values).Concat(ExcludedConstantReaders.Values).Concat(ExcludedClrTypeMemberShapes.Values)
            .Concat(ExcludedOwnReads.Values).Concat(ExcludedConstantForms.Values).Concat(ExcludedAliasUses.Values).Concat(ExcludedSelfReachingShapes.Values)
            .Concat(AllConstantHosts().SelectMany(h => new[] { "int", "() -> int" }.Select(t => HostSkips(h.Host, t))).Where(r => r != null).Select(r => r!))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var orphan in BehaviourProbes.Keys.Where(k => !reasons.Contains(k)))
            problems.Add($"a probe guards the reason '{orphan}', and no exclusion gives it");
        foreach (var blank in reasons.Where(string.IsNullOrWhiteSpace))
            problems.Add($"an exclusion gives no reason: '{blank}'");
        return problems;
    }

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

    /// <summary>The falsifier of every exclusion that rests on compiler behaviour: its probe still does what the reason says.</summary>
    [Fact]
    public void Exclusions_RestingOnTheCompiler_GoRedWhenItChanges()
    {
        var problems = new List<string>();
        foreach (var (reason, probe) in BehaviourProbes.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            var run = RunSharpy(probe.Source, probe.Stdlib, SharpyExecTimeoutMs);
            if (!probe.Holds(run))
            {
                problems.Add($"'{reason}' rests on {probe.Expected}, and the probe now gives [{string.Join(",", run.ErrorCodes.Distinct())}{(run.CsCode != null ? " " + run.CsCode : "")}]"
                    + $"{(run.ErrorCodes.Count == 0 ? $" stdout '{Harness.Normalize(run.Stdout)}'" : "")}: generate the value, or give the exclusion its new reason");
            }
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
