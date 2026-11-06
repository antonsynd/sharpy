using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The binding-kind roster (P77a D3.a): ONE enum used as the READ target axis and as the outer-binding axis of
/// every family, so a kind cannot exist as a target and be missing as an outer value. Its values are the image of
/// the compiler's enumerations under the map in <c>NameResolutionDifferentialTests.Rosters.cs</c>.
/// </summary>
internal enum BindingKind
{
    // module-level
    ModuleVar,
    ModuleLet,
    ModuleLiteralConst,
    ModuleComputedConst,
    ModuleProperty,
    ModuleDef,
    ModuleClass,
    ModuleStruct,
    ModuleEnum,
    ModuleUnion,
    ModuleInterface,
    ModuleTypeAlias,

    // type-level
    StaticField,
    StaticProperty,
    StaticMethod,
    ClassLiteralConst,
    ClassComputedConst,
    NestedType,

    // instance
    InstanceField,
    InstanceProperty,
    InstanceMethod,
    Constructor,

    // position-local
    EnclosingLocal,
    Parameter,
    Inner,
    LocalVar,
    LocalLet,
    LocalLiteralConst,
    LocalComputedConst,
    LocalDef,
    LambdaTypedLocal,

    // the named outers: a binding of the global scope (SymbolTable.PopulateBuiltins) and a name a single-file
    // `from <stdlib module> import <name>` binds (ImportResolver.BindImportName, P77b D2.9); and no binding at all
    Builtin,
    ImportedName,
    None,
}

/// <summary>
/// Every position the READ family reads from (D3.c): the image of the four position rosters. A roster source
/// value maps to one or more of these or to an <c>ExcludedPositions</c> entry.
/// </summary>
internal enum ReadPosition
{
    // (1) class-body positions: ClassBodyReadPosition × ClassBodyReadSlot, with the host only where it routes
    StaticInitializer,
    InstanceFieldInitializer,
    DataclassFieldInitializer,
    DataclassInitFieldInitializer,
    StructFieldInitializer,
    StructInitFieldInitializer,
    ConstInitializer,
    StaticPropertyDefault,
    InstancePropertyDefault,
    ParameterDefault,

    // (2) body positions: the FunctionLike families of ClassifyScope × FunctionSymbol flags × owner × name class
    MethodBody,
    StaticMethodBody,
    InitBody,
    AccessorGetBody,
    AccessorSetBody,
    NestedClassMethodBody,
    NestedDefInMethod,
    FunctionBody,
    NestedDefInFunction,
    GeneratorBody,
    LambdaStored,
    LambdaImmediate,
    LambdaPassed,

    // the Block nestings that ride a host position
    ComprehensionIterable,
    ComprehensionElement,

    // (3) module positions: the Statement subclasses against ModuleLevelValidator's split
    ModuleInitializer,
    ModulePropertyInitializer,
    ModuleAccessorBody,
    EnumMemberValue,
    TypeAliasTarget,
    BaseList,
}

/// <summary>A store form of the STORE family (D3.e): the image of the binder-site census plus the one no-site form.</summary>
internal enum StoreForm
{
    Assign,
    Augmented,
    Walrus,
    OutLet,
    LetOrVar,
}

/// <summary>A construct target of the LOCAL family (D3.e): the LOCAL-target class of the binder-site census.</summary>
internal enum LocalTarget
{
    For,
    ExceptAs,
    WithAs,
    Comprehension,
    MatchCapture,
    AsPatternCapture,
    OutLet,
}

/// <summary>The class of a <c>Pattern</c> subclass in TYPEMEMBER's pattern sub-axis (D3.e, roster 7).</summary>
internal enum PatternClass
{
    /// <summary>Names a type or member: a generated cell per declaring kind that admits it.</summary>
    NamesTypeOrMember,

    /// <summary>The inner pattern carries the name: one cell wrapping the <c>TypePattern</c> cell.</summary>
    Composite,

    /// <summary>Carries no name: excluded with that reason.</summary>
    CarriesNoName,
}

/// <summary>
/// The reasons a roster source value maps to no generated cell: D3's excluded axes (i)–(xvi) plus the reasons the
/// roster itself names. The report lists the axes as <c>excludedAxes</c>, so the coverage statement is honest.
/// </summary>
internal enum ExcludedAxis
{
    /// <summary>(i) the module-qualified spelling (<c>string.digits</c>), reads from another user module, a later project file:
    /// needs a project route. The single-file <c>from &lt;stdlib module&gt; import &lt;name&gt;</c> is NOT out: it is
    /// <see cref="BindingKind.ImportedName"/> (P77b axis 9).</summary>
    ProjectRoute,

    /// <summary>(ii) hosts interface/union/enum/generic: P78's and P75's hand matrices.</summary>
    InterfaceUnionEnumGenericHosts,

    /// <summary>(iii) events, split properties, out/tuple/star stores: #2400, #2154/#2156/P21d.</summary>
    EventsSplitPropertiesTupleStarStores,

    /// <summary>(iv) property observers: experimental; the harness deploys no feature flags.</summary>
    PropertyObservers,

    /// <summary>(v) user decorator expressions: not a Sharpy program at any order.</summary>
    UserDecorators,

    /// <summary>(vi) @[...] attribute arguments: reachable only through a .NET attribute.</summary>
    AttributeArguments,

    /// <summary>(vii) the case None(): arm: python has no None() class pattern.</summary>
    NoneCaseArm,

    /// <summary>(viii) the super().x spelling: SPY0287 by spec, a spec restriction and not a name verdict.</summary>
    SuperSpelling,

    /// <summary>(ix) instance receivers other than self: #2147's own matrix columns.</summary>
    NonSelfInstanceReceivers,

    /// <summary>(x) type parameters and the delegate kind: never a value binding.</summary>
    TypeParametersAndDelegates,

    /// <summary>(xi) a function-scoped type alias: no rule N1–N7 names a local type binding.</summary>
    FunctionScopedTypeAlias,

    /// <summary>(xii) a declaration without an initializer: order-independent, no python twin.</summary>
    DeclarationWithoutInitializer,

    /// <summary>(xiii) async def bodies: the same function: family, and the twin needs an await chain.</summary>
    AsyncBodies,

    /// <summary>(xiv) the with assert_raises(E) as exc: capture: a unittest marker with no python twin.</summary>
    AssertRaisesCapture,

    /// <summary>(xv) ref / in parameter modifiers: #2154 / #2156's class.</summary>
    RefInParameterModifiers,

    /// <summary>(xvi) value types beyond int and str: a read before its value is ready has two observable shapes, a default that
    /// prints (int) and a null that throws (str), and every other type repeats one of them.</summary>
    ValueTypesBeyondIntAndStr,

    /// <summary>A flag or enum value that cannot change a binding's kind or a scope family; the entry states why.</summary>
    NotAKind,

    /// <summary>A dead enum value: the compiler has no producer for it; the roster asserts the count stays 0.</summary>
    DeadEnumValue,

    /// <summary>The compiler re-registering symbols for its own bookkeeping, or synthesizing them: no user spelling.</summary>
    CompilerInternal,

    /// <summary>Refused by kind at every order (fidelity row 2): the verdict is not a name verdict.</summary>
    RefusedByKind,

    /// <summary>A construct behind a feature flag: the harness deploys none.</summary>
    GatedConstruct,

    /// <summary>A D3.e pruning-order representative: this value shares its arm with one that stays.</summary>
    PrunedRepresentative,
}

/// <summary>
/// The name-resolution differential sweep (#2421, P77a): for every generated program python3 runs, Sharpy <c>run</c>
/// prints the same output or refuses at compile time, and never surfaces SPY0908/SPY0909. Design of record:
/// <c>docs/design/name-resolution.md</c>; contract and rosters: <c>docs/design/gap-discovery-contracts.md</c>.
/// </summary>
[Trait("Category", "GapDiscovery")]
public partial class NameResolutionDifferentialTests : IntegrationTestBase
{
    /// <summary>The compile path is muted per <c>RunSharpy</c> call rather than in the constructor: a fact that
    /// compiles through the base class directly still records its generated C#, which is how
    /// <c>RunSharpy_BuffersNothingInTheTestResult</c> shows the mute was lifted. The mute and the per-test output
    /// ceiling are <see cref="IntegrationTestBase"/>'s (#2438).</summary>
    public NameResolutionDifferentialTests(ITestOutputHelper output) : base(output) { }
}
