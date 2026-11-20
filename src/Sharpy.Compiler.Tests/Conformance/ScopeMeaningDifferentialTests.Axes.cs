using System.Reflection;
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
        [NamesAModuleType] = new("import pathlib\ndef f(p: int = pathlib.Path) -> int:\n    return p\n" + TailMain, "SPY0220", r => Refused(r, "SPY0220"), Stdlib: true),
        [AnInstanceMemberThroughItsType] = new("from system import DateTime\ndef main() -> None:\n    x: object = DateTime.year\n    print(1)\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120")),
        [ASiblingCaseIsNoName] = new("enum H:\n    RED = 5\n    BLUE = RED\ndef main() -> None:\n    print(H.BLUE.value)\n", "SPY0200", r => Refused(r, "SPY0200")),
        [NoSelfAtATypeBody] = new("class H:\n    const x: int = 5\n    const K: int = self.x\ndef main() -> None:\n    print(H.K)\n", "SPY0200", r => Refused(r, "SPY0200")),
        [AGenericMethodThroughItsType] = new("from system import Array\ndef main() -> None:\n    x: object = Array.resize\n    print(1)\n", "SPY0908 CS8917", r => Refused(r, "SPY0908", "CS8917")),
        [LambdaTakesOneNameForAType] = new("import system\ndef main() -> None:\n    f = lambda p: system.DayOfWeek: p\n    print(1)\n", "SPY0103", r => Refused(r, "SPY0103")),
        [AliasedIntEnumRead(EnumMemberAccessKind.IntValue)] = new(IntEnum + "\ntype A = E0\ndef main() -> None:\n    print(A.RED.value)\n", "SPY0908 CS1061", r => Refused(r, "SPY0908", "CS1061")),
        [AliasedIntEnumRead(EnumMemberAccessKind.IntName)] = new(IntEnum + "\ntype A = E0\ndef main() -> None:\n    print(A.RED.name)\n", "SPY0908 CS1061", r => Refused(r, "SPY0908", "CS1061")),
        [InterfaceStaticIsRefused("field")] = new("interface H:\n    @static\n    x: int = 5\n    def m(self) -> int: ...\ndef main() -> None:\n    print(H.x)\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120")),
        [InterfaceStaticIsRefused("property")] = new("interface H:\n    @static\n    property x: int = 5\n    def m(self) -> int: ...\ndef main() -> None:\n    print(H.x)\n", "SPY0908 CS0120", r => Refused(r, "SPY0908", "CS0120")),
        [CharNamesNoReceiver] = new("def main() -> None:\n    x: object = char.is_ascii_digit\n    print(1)\n", "SPY0200", r => Refused(r, "SPY0200")),
        [AStoredLambdaLosesItsDefault] = new("class H:\n    @static\n    g: auto = lambda p1: int = 5: p1\ndef main() -> None:\n    print(H.g())\n", "SPY0908 CS7036", r => Refused(r, "SPY0908", "CS7036")),
        [ALambdaDefaultInAPropertyDefault(false)] = new("class H:\n    property r: int = (lambda p1: int = 5: p1)()\ndef main() -> None:\n    print(H().r)\n", "SPY0908 CS0103", r => Refused(r, "SPY0908", "CS0103")),
        [ALambdaDefaultInAPropertyDefault(true)] = new("class H:\n    @static\n    property r: int = (lambda p1: int = 5: p1)()\ndef main() -> None:\n    print(H.r)\n", "SPY0908 CS0103", r => Refused(r, "SPY0908", "CS0103")),
        [ACallInAConstantOnlyPosition] = new("class H:\n    def r(self, p0: int = (lambda p1: int = 5: p1)()) -> int:\n        return p0\n" + TailMain, "SPY0401", r => Refused(r, "SPY0401")),
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
    private const string OverflowsTheCompiler = "#2769: named by an annotation, an alias whose target reaches the alias overflows the compiler's stack (`TypeResolver.ExpandTypeAlias`), and a process that dies leaves no verdict a cell can read (its probe compiles in a child process). The class is unruled and out of P78 (owner, #2423 comment 7042723494 item 9), so only the UNUSED alias has cells, and they keep running";

    /// <summary>The orders with no cell, per cell set that has the axis: total over <c>Order</c> with the two every set generates.</summary>
    internal static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<Harness.Order, string>> ExcludedOrders = new Dictionary<string, IReadOnlyDictionary<Harness.Order, string>>(StringComparer.Ordinal)
    {
        ["typed-read"] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = AReadOfItself, [Harness.Order.Cycle] = AReadOfItself },
        ["init-overload"] = new Dictionary<Harness.Order, string> { [Harness.Order.Self] = ARecursion, [Harness.Order.Cycle] = ARecursion },
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

    private const string LambdaTakesNoCallableSlot = "a lambda parameter's annotation cannot be a function type and take a default: `lambda p: () -> int = five: p` is SPY0103";
    private const string LambdaTakesOneNameForAType = "a lambda parameter's annotation is one name: a type spelled through its module (`lambda p: system.DayOfWeek: p`) is SPY0103 with no default at all, so the slot cannot be declared there and the parse error is no constant position's verdict; the same type under a from-import is generated";
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

        // An exclusion that rests on the compiler names its probe, and a probe has its exclusion.
        var reasons = ExcludedDeclarations.Values.Concat(ExcludedDeclarationHosts.Values).Concat(ExcludedValueTypes.Values).Concat(ExcludedTypedReads.Values)
            .Concat(ExcludedTypedSpellings.Values).Concat(ExcludedTypedPositions.Values).Concat(ExcludedCalleeBinders.Values).Concat(ExcludedCalleeOuters.Values)
            .Concat(ExcludedAdmissionTables.Values).Concat(ExcludedModuleMemberShapes.Values).Concat(ExcludedModuleCollections.Values).Concat(ExcludedAliasScopes.Values).Concat(ExcludedAliasTypeHosts.Values)
            .Concat(ExcludedOwnSpellings.Values).Concat(ExcludedOwnPlacements.Values).Concat(ExcludedEnumSpellings.Values).Concat(ExcludedImportKinds.Values)
            .Concat(ExcludedBuiltinReceivers.Values).Concat(ExcludedLambdaUses.Values).Concat(ExcludedLambdaDefaultPositions.Values)
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
        foreach (var twice in BehaviourProbes.Keys.Where(CrashProbes.ContainsKey))
            problems.Add($"the reason '{twice}' has a probe in this process and one in a child process: it rests on one behaviour");
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
