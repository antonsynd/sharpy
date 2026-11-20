using System.Reflection;
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
/// <item><term>constant-kind × host</term><description><c>AdmissionTable</c>, the validator's own enumeration of constant positions</description></item>
/// <item><term>constant-kind × member kind</term><description>a census of what discovery records for every value of every
/// Stdlib module (<c>FieldSignature.IsConst</c> × <c>IsGuarded</c>), which is what a checker's answer for a bound module
/// member can rest on</description></item>
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
        [TypeKind.Delegate] = "no Sharpy declaration: `() -> int` is an annotation, never a host",
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
    private const string ARecursiveAlias = "an alias that names itself, directly or through a ring, has a target that is neither missing nor a declared type: a recursive alias is unruled (owner question raised with plan-777361's first revision)";

    /// <summary>The orders with no cell, per cell set that has the axis: total over <c>Order</c> with the two every set generates.</summary>
    internal static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<Harness.Order, string>> ExcludedOrders = new Dictionary<string, IReadOnlyDictionary<Harness.Order, string>>(StringComparer.Ordinal)
    {
        ["typed-read"] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = AReadOfItself, [Harness.Order.Cycle] = AReadOfItself },
        ["init-overload"] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = ARecursion, [Harness.Order.Cycle] = ARecursion },
        ["alias-target"] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = ARecursiveAlias, [Harness.Order.Cycle] = ARecursiveAlias },
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
    // constant-kind (#2432): host × kind
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A host of a constant position: its label, the program around <c>{e}</c> typed <c>{T}</c>, the code its refusal
    /// carries, the 8951 pool's position where it has one, whether the program prints <c>1</c> and not the value, whether it
    /// takes an int only, and whether python has the same position.</summary>
    internal sealed record ConstantHost(string Label, string Program, string Refusal, ReadPosition? Position = null, bool PrintsOne = false, bool IntOnly = false, bool Twin = false);

    /// <summary>The constant positions, total over <c>AdmissionTable</c> with <see cref="ExcludedAdmissionTables"/>: every table the
    /// validator judges a position by, each with the hosts that reach it.</summary>
    internal static readonly IReadOnlyDictionary<AdmissionTable, ConstantHost[]> ConstantHosts = new Dictionary<AdmissionTable, ConstantHost[]>
    {
        [AdmissionTable.ParameterDefault] = new[]
        {
            new ConstantHost("def", $"def f(p: {{T}} = {{e}}) -> {{T}}:\n    return p\n{Main}    print(f())", "SPY0401", ReadPosition.ParameterDefault, Twin: true),
        },
        [AdmissionTable.LambdaParameterDefault] = new[]
        {
            new ConstantHost("lambda", $"{Main}    f = lambda p: {{T}} = {{e}}: p\n    print(f())", "SPY0401"),
        },
        [AdmissionTable.DecoratorArgument] = new[]
        {
            new ConstantHost("attribute", $"class H:\n    @[system.component_model.default_value({{e}})]\n    def f(self) -> int:\n        return 1\n{Main}    print(H().f())", "SPY0425", PrintsOne: true),
        },
        [AdmissionTable.ConstInitializer] = new[]
        {
            new ConstantHost("module-const", $"const K: {{T}} = {{e}}\n{Main}    print(K)", "SPY0401"),
            new ConstantHost("class-const", $"class H:\n    const K: {{T}} = {{e}}\n{Main}    print(H.K)", "SPY0401", ReadPosition.ConstInitializer),
            new ConstantHost("local-const", $"{Main}    const K: {{T}} = {{e}}\n    print(K)", "SPY0401"),
        },
        [AdmissionTable.PerInstanceFieldDefault] = new[]
        {
            new ConstantHost("dataclass", $"@dataclass\nclass H:\n    p: {{T}} = {{e}}\n{Main}    print(H().p)", "SPY0401", ReadPosition.DataclassFieldInitializer, Twin: true),
            new ConstantHost("struct", $"struct H:\n    p: {{T}} = {{e}}\n{Main}    print(H().p)", "SPY0401", ReadPosition.StructFieldInitializer, Twin: true),
        },
    };

    internal static readonly IReadOnlyDictionary<AdmissionTable, string> ExcludedAdmissionTables = new Dictionary<AdmissionTable, string>();

    /// <summary>The one constant position the validator judges by no table: an enum member's value. A table value added for it
    /// has no entry in <see cref="ConstantHosts"/>, which is red until this host moves under it.</summary>
    internal static readonly ConstantHost EnumMemberHost = new("enum-member", $"enum H:\n    RED = {{e}}\n{Main}    print(H.RED.value)", "SPY0401", ReadPosition.EnumMemberValue, IntOnly: true);

    /// <summary>The expression, its type, what it prints and what it imports.</summary>
    internal sealed record ConstantExpression(string Label, string Text, string Type, string Prints, string? Import);

    internal static readonly IReadOnlyDictionary<ScopeConstantKind, ConstantExpression[]> ConstantExpressions = new Dictionary<ScopeConstantKind, ConstantExpression[]>
    {
        [ScopeConstantKind.ClrLiteralField] = new[] { new ConstantExpression("subprocess.PIPE", "subprocess.PIPE", "int", "-1", "subprocess") },
        [ScopeConstantKind.StdlibModuleVariable] = new[]
        {
            new ConstantExpression("socket.AF_INET", "socket.AF_INET", "int", "2", "socket"),
            new ConstantExpression("math.pi", "math.pi", "float", "3.141592653589793", "math"),
        },
        [ScopeConstantKind.ClrStaticMember] = new[] { new ConstantExpression("sys.maxsize", "sys.maxsize", "int", "2147483647", "sys") },
        [ScopeConstantKind.ComparisonChain] = new[] { new ConstantExpression("ordered", "1 < 2 < 3", "bool", "True", null) },
        [ScopeConstantKind.ChainWithNonConstantLink] = new[] { new ConstantExpression("membership", "0 < 2 in [1, 2]", "bool", "True", null) },
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

    /// <summary>The kinds that are no member of a module: closed by call 45 (a chain is admitted when each link is).</summary>
    internal static readonly IReadOnlySet<ScopeConstantKind> ChainKinds = new HashSet<ScopeConstantKind> { ScopeConstantKind.ComparisonChain, ScopeConstantKind.ChainWithNonConstantLink };

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
        [ScopeKind.InitializerHost] = "the scope of an initializer EXPRESSION's own locals: a `type` statement is no expression",
        [ScopeKind.Transparent] = "pushed to hold type parameters for one resolution step: it declares nothing a program writes",
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
        [TypeKind.Delegate] = "no Sharpy declaration: `() -> int` is an annotation, never a host",
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

        // constant-kind
        Partition("constant-kind × host", Enum.GetValues<AdmissionTable>(), ConstantHosts.Keys, ExcludedAdmissionTables.Keys);
        Carried("constant-kind", ConstantHosts.Keys, Part<AdmissionTable>);
        Carried("constant-kind", ConstantHosts.Values.SelectMany(h => h).Append(EnumMemberHost).Select(h => h.Label), Marks);
        Carried("constant-kind", Enum.GetValues<ScopeConstantKind>(), Part<ScopeConstantKind>);
        var census = StdlibModuleValues.Value;
        Partition("constant-kind × member kind", census.Values.Distinct().Concat(UnproducedModuleValueShapes.Keys), ModuleValueShapes.Keys, UnproducedModuleValueShapes.Keys);
        foreach (var shape in UnproducedModuleValueShapes.Keys.Where(s => census.Values.Contains(s)))
            problems.Add($"constant-kind × member kind: {census.First(v => v.Value == shape).Key} is {shape}, which is listed as a shape no value has");
        Partition("constant-kind × kind", Enum.GetValues<ScopeConstantKind>(), ModuleValueShapes.Values, ChainKinds);
        foreach (var (kind, expressions) in ConstantExpressions)
        {
            foreach (var e in expressions)
            {
                if (ChainKinds.Contains(kind) != (e.Import == null))
                    problems.Add($"constant-kind: {e.Label} is listed under {kind}, and a member kind is read off an imported module while a chain is not");
                else if (e.Import != null && !census.ContainsKey(e.Text))
                    problems.Add($"constant-kind: discovery records no value {e.Text}");
                else if (e.Import != null && ModuleValueShapes.GetValueOrDefault(census[e.Text], kind) != kind)
                    problems.Add($"constant-kind: {e.Text} is listed under {kind}, and discovery records it (IsConst, IsGuarded) = {census[e.Text]}");
            }
        }
        foreach (var kind in Enum.GetValues<ScopeConstantKind>().Where(k => !ConstantExpressions.ContainsKey(k)))
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
        // The two binders of an alias, by the 8951 pool's census of binder sites: a third is a site this axis has not placed.
        var binders = NameResolutionRosters.BinderSites.Keys.Where(k => k.Contains("TypeAliasSymbol.CreateFrom", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        if (!binders.SequenceEqual(AliasBinderSites.OrderBy(k => k, StringComparer.Ordinal)))
            problems.Add($"alias-target × site: the binder census holds [{string.Join("; ", binders)}], and the sites were placed for [{string.Join("; ", AliasBinderSites)}]");

        // An exclusion that rests on the compiler names its probe, and a probe has its exclusion.
        var reasons = ExcludedDeclarations.Values.Concat(ExcludedDeclarationHosts.Values).Concat(ExcludedValueTypes.Values).Concat(ExcludedTypedReads.Values)
            .Concat(ExcludedTypedSpellings.Values).Concat(ExcludedTypedPositions.Values).Concat(ExcludedCalleeBinders.Values).Concat(ExcludedCalleeOuters.Values)
            .Concat(ExcludedAdmissionTables.Values).Concat(UnproducedModuleValueShapes.Values).Concat(ExcludedAliasScopes.Values).Concat(ExcludedAliasTypeHosts.Values)
            .Concat(ExcludedAliasShapeProperties.Values).Concat(ExcludedOrders.Values.SelectMany(o => o.Values)).ToHashSet(StringComparer.Ordinal);
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
