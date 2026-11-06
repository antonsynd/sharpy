using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Infrastructure;
using Sharpy.Compiler.Tests.Semantic;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParameterModifier = Sharpy.Compiler.Parser.Ast.ParameterModifier;
using ParserNs = Sharpy.Compiler.Parser;
using ScopeKind = Sharpy.Compiler.Semantic.SymbolTable.ScopeKind;
using SymbolKind = Sharpy.Compiler.Semantic.SymbolKind;
using TypeKind = Sharpy.Compiler.Semantic.TypeKind;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>A type position of the READ family (D3.c roster 6): the image of the <c>TypeAnnotation</c> census.</summary>
internal enum TypePosition
{
    VariableAnnotation,
    ReturnAnnotation,
    ParameterAnnotation,
    ExceptClause,
    AliasTarget,
    PropertyAnnotation,
    AccessorReturn,
    UnionCaseField,
    LambdaReturn,
    CallableReturn,
    CallableParameter,
    OutInlineType,
    Coercion,
    IsInstance,
    TryExceptionType,
    ResultError,
    GenericArgument,
    BaseList,
    PatternType,
}

/// <summary>A construct whose body hosts a <c>let</c> in the LOCAL family (D3.e roster 8).</summary>
internal enum BlockConstruct
{
    IfThen,
    Elif,
    IfElse,
    WhileBody,
    WhileElse,
    ForBody,
    ForElse,
    Try,
    Except,
    TryElse,
    Finally,
    With,
    MatchCase,
}

/// <summary>A comprehension family of roster 8 (P77b axis 1): the image of the five comprehension <c>BlockFamilies</c> names
/// and of the AST's comprehension nodes, both through <see cref="NameResolutionRosters.ComprehensionKinds"/>.</summary>
internal enum ComprehensionKind
{
    List,
    Set,
    Dict,
    DictSpread,
    Generator,
}

/// <summary>The clause a name sits in (P77b axis 1): the image of the <c>ComprehensionClause</c> subclasses, a <c>for</c> clause
/// standing as the first one (whose iterable python evaluates in the enclosing scope) and as a second one (evaluated in the
/// comprehension's own scope).</summary>
internal enum ComprehensionClauseShape
{
    FirstFor,
    SecondFor,
    If,
}

/// <summary>A slot a name sits in (P77b axis 1, deferred finding 1): the image of the Expression-typed properties of the
/// comprehension nodes and their clauses, through <see cref="NameResolutionRosters.ComprehensionSlots"/>.</summary>
internal enum ComprehensionSlot
{
    Element,
    Key,
    Value,
    Spread,
    Iterable,
    Condition,
    Target,
}

/// <summary>Where a comprehension cell sits (P77b axis 1): its kind, the clause and the slot of the name.</summary>
internal readonly record struct ComprehensionSite(ComprehensionKind Kind, ComprehensionClauseShape Clause, ComprehensionSlot Slot);

/// <summary>The receiver of an attribute STORE (P77b axis 8): N7's spelling list restricted to stores {<c>self.</c>, <c>H.</c>,
/// alias <c>A.</c>} and #2435's {derived <c>D.</c>, instance local <c>h0.</c>} — a closed LITERAL set named closed: a
/// <c>MemberAccess</c> receiver has no compiler enumeration.</summary>
internal enum AttributeReceiver
{
    SelfDot,
    Type,
    Alias,
    Derived,
    InstanceLocal,
}

/// <summary>When a LOCAL read happens relative to the binding it reads (P77b axis 4): a closed LITERAL set named closed —
/// control-flow timing has no compiler enumeration. The two same-block values are LocalCells' below / above arms.</summary>
internal enum ReadTiming
{
    SameBlockBelow,
    SameBlockAbove,
    ClosureBeforeConstruct,
    BackEdge,
    FailedGuard,
}

/// <summary>What a program observes (P77b axis 3): a closed LITERAL set named closed — an observation has no compiler
/// enumeration. Every cell observes a value; an order cell also observes when its initializers run.</summary>
internal enum Observation
{
    Value,
    Order,
}

/// <summary>The endpoint pair of an N3 cycle (P77b axis 2): which level each of the two initializers is at, the first declared
/// first. The type level is a <c>@static</c> member; the ordered pair is the route python's load order reads.</summary>
internal enum CyclePair
{
    ModuleModule,
    ModuleType,
    TypeModule,
    TypeType,
}

/// <summary>How the first endpoint's initializer reaches the second (P77b axis 2): a closed LITERAL set {direct, through a
/// <c>def</c>} named closed (deferred finding 3) — a read either sits in the initializer or in a function it calls (p07).</summary>
internal enum CycleThrough
{
    Direct,
    ThroughDef,
}

/// <summary>An N3 cycle cell's coordinates (P77b axis 2).</summary>
internal readonly record struct CycleRoute(
    CyclePair Pair, NameResolutionDifferentialTests.Spelling Spelling, CycleThrough Through, BindingKind First, BindingKind Second);

/// <summary>The scope a STORE targets (tenth pass finding 4): the image of <see cref="ScopeKind"/>.</summary>
internal enum StoreTargetScope
{
    SameBlock,
    EnclosingBlock,
    EnclosingFunction,
    Module,
}

/// <summary>
/// Where in its body a STORE sits (tenth pass finding 4). Named closed with the reason on
/// <see cref="NameResolutionRosters.StoreNestingReason"/>: one representative of the Block families.
/// </summary>
internal enum StoreNesting
{
    Top,
    InsideIf,
}

/// <summary>
/// How a <c>function:</c> scope's name decides its position (D3.c roster 4): the name class only an owner
/// <see cref="ScopeKind.TypeBody"/> reads.
/// </summary>
internal enum FunctionNameClass
{
    Ordinary,
    Init,
}

/// <summary>
/// A roster source value's image: the vocabulary values it realises, or the table that refines it, or the
/// excluded axis with its reason. Exactly one of the three, which <see cref="Roster.ShapeProblems{TKey,TImage}"/>
/// asserts, so a source is mapped exactly one way.
/// </summary>
internal sealed record RosterEntry<TImage>(
    IReadOnlyList<TImage> Image,
    string? RefinedBy,
    ExcludedAxis? Excluded,
    string? Reason);

internal static class Roster
{
    internal static RosterEntry<T> To<T>(params T[] image) => new(image, null, null, null);

    internal static RosterEntry<T> Refined<T>(string by) => new(Array.Empty<T>(), by, null, null);

    internal static RosterEntry<T> Out<T>(ExcludedAxis axis, string reason) => new(Array.Empty<T>(), null, axis, reason);

    /// <summary>A map with no default arm: a duplicate source is a static-initializer failure naming it.</summary>
    internal static IReadOnlyDictionary<TKey, TValue> Map<TKey, TValue>(params (TKey Key, TValue Value)[] rows)
        where TKey : notnull
    {
        var map = new Dictionary<TKey, TValue>();
        foreach (var (key, value) in rows)
        {
            if (!map.TryAdd(key, value))
                throw new InvalidOperationException($"roster source mapped twice: {key}");
        }

        return map;
    }

    internal static IEnumerable<string> ShapeProblems<TKey, TImage>(
        IReadOnlyDictionary<TKey, RosterEntry<TImage>> map) where TKey : notnull
    {
        foreach (var (key, entry) in map)
        {
            int ways = (entry.Image.Count > 0 ? 1 : 0)
                + (entry.RefinedBy != null ? 1 : 0)
                + (entry.Excluded != null ? 1 : 0);
            if (ways != 1)
                yield return $"{key}: mapped {ways} ways (image, refined-by, excluded: exactly one)";
            if (entry.Excluded != null && string.IsNullOrWhiteSpace(entry.Reason))
                yield return $"{key}: excluded with no reason";
        }
    }

    internal static IEnumerable<T> ImageOf<TKey, T>(IReadOnlyDictionary<TKey, RosterEntry<T>> map) where TKey : notnull
        => map.Values.SelectMany(e => e.Image);

    /// <summary>Both directions of totality: a source with no entry, and an entry with no source.</summary>
    internal static IEnumerable<string> TotalityProblems<TKey>(
        string roster, IEnumerable<TKey> universe, IEnumerable<TKey> keys) where TKey : notnull
    {
        var source = universe.ToHashSet();
        var mapped = keys.ToHashSet();
        foreach (var unmapped in source.Except(mapped).Select(k => k.ToString()).OrderBy(k => k, StringComparer.Ordinal))
            yield return $"{roster}: source value with no map entry: {unmapped}";
        foreach (var phantom in mapped.Except(source).Select(k => k.ToString()).OrderBy(k => k, StringComparer.Ordinal))
            yield return $"{roster}: map entry with no source value: {phantom}";
    }
}

/// <summary>A binder site's image (D3.e): a STORE form, a LOCAL construct target, the kinds it declares, or an excluded axis.</summary>
internal sealed record BinderImage(
    StoreForm? Form,
    LocalTarget? Target,
    IReadOnlyList<BindingKind> Declares,
    ExcludedAxis? Excluded,
    string? Reason)
{
    internal static BinderImage Store(StoreForm form, params BindingKind[] declares) => new(form, null, declares, null, null);

    internal static BinderImage Local(LocalTarget target, params BindingKind[] declares) => new(null, target, declares, null, null);

    internal static BinderImage StoreAndLocal(StoreForm form, LocalTarget target, params BindingKind[] declares)
        => new(form, target, declares, null, null);

    internal static BinderImage Declaring(params BindingKind[] declares) => new(null, null, declares, null, null);

    internal static BinderImage Out(ExcludedAxis axis, string reason) => new(null, null, Array.Empty<BindingKind>(), axis, reason);
}

/// <summary>A Block family's cell in the LOCAL family (roster 8): a <c>let</c> in its body, or a comprehension target.</summary>
internal readonly record struct ConstructCell(BlockConstruct? LetInBody, LocalTarget? Target);

/// <summary>
/// A <see cref="BindingKind"/>'s membership of a product axis (tenth pass findings 1, 3): generated at every body,
/// generated only where a class encloses the body, or left out with its reason. Every kind has one of the three, so
/// the axis cannot be narrower than the roster by omission.
/// </summary>
internal sealed record AxisMembership(bool Generated, bool ClassEnclosedBodiesOnly, ExcludedAxis? Excluded, string? Reason)
{
    internal static readonly AxisMembership Everywhere = new(true, false, null, null);

    internal static readonly AxisMembership ClassEnclosed = new(true, true, null, null);

    internal static AxisMembership Out(ExcludedAxis axis, string reason) => new(false, false, axis, reason);

    internal static AxisMembership Pruned(string reason) => new(false, false, ExcludedAxis.PrunedRepresentative, reason);
}

/// <summary>
/// The eight rosters of P77a D3 as data: each compiler enumeration under an explicit map with no default arm. The
/// generator asserts its axes against these; the <c>*Roster_*</c> facts assert these against the compiler's
/// enumerations by reflection and Roslyn, without the generator.
/// </summary>
internal static class NameResolutionRosters
{
    // ---------------------------------------------------------------------------------------------------------
    // Measured literals, @ f1418767d by the facts below. A change is a change to the compiler's inventory:
    // re-measure, map the new value, and update the literal in the same commit.
    // ---------------------------------------------------------------------------------------------------------

    /// <summary><c>VariableSymbol</c> bools with the <c>Symbol</c> base's: 9 + 3.</summary>
    internal const int VariableSymbolBoolCensus = 12;

    /// <summary><c>FunctionSymbol</c>'s own bools (<c>BindingFlags.DeclaredOnly</c>, finding 8): the base's three are
    /// D3.a's flags, already mapped on <see cref="VariableFlags"/>, and no function reads them as a position.</summary>
    internal const int FunctionSymbolDeclaredBoolCensus = 10;

    /// <summary>Binder sites whose receiver type is <c>SymbolTable</c> or <c>Scope</c> (finding 6): the plan's 67 on the
    /// four <c>SymbolTable</c> receivers, plus <c>_globalScope</c> ×3, <c>merged</c> and <c>CurrentScope</c> ×2.</summary>
    internal const int BinderSiteCensus = 73;

    internal const int TypeAnnotationScalarCensus = 22;

    /// <summary><c>IEnumerable&lt;TypeAnnotation&gt;</c>-typed AST properties (finding 5).</summary>
    internal const int TypeAnnotationCollectionCensus = 6;

    internal const int PatternSubclassCensus = 15;

    internal const int StatementSubclassCensus = 30;

    internal const int BlockFamilyCensus = 20;

    internal const int FunctionLikeFamilyCensus = 7;

    // ---------------------------------------------------------------------------------------------------------
    // Roster 1: binding kind (D3.a)
    // ---------------------------------------------------------------------------------------------------------

    internal static readonly ImmutableArray<BindingKind> ModuleLevelKinds = ImmutableArray.Create(
        BindingKind.ModuleVar, BindingKind.ModuleLet, BindingKind.ModuleLiteralConst, BindingKind.ModuleComputedConst,
        BindingKind.ModuleProperty, BindingKind.ModuleDef, BindingKind.ModuleClass, BindingKind.ModuleStruct,
        BindingKind.ModuleEnum, BindingKind.ModuleUnion, BindingKind.ModuleInterface, BindingKind.ModuleTypeAlias);

    internal static readonly ImmutableArray<BindingKind> TypeLevelKinds = ImmutableArray.Create(
        BindingKind.StaticField, BindingKind.StaticProperty, BindingKind.StaticMethod,
        BindingKind.ClassLiteralConst, BindingKind.ClassComputedConst, BindingKind.NestedType);

    internal static readonly ImmutableArray<BindingKind> InstanceKinds = ImmutableArray.Create(
        BindingKind.InstanceField, BindingKind.InstanceProperty, BindingKind.InstanceMethod, BindingKind.Constructor);

    internal static readonly ImmutableArray<BindingKind> PositionLocalKinds = ImmutableArray.Create(
        BindingKind.EnclosingLocal, BindingKind.Parameter, BindingKind.Inner, BindingKind.LocalVar, BindingKind.LocalLet,
        BindingKind.LocalLiteralConst, BindingKind.LocalComputedConst, BindingKind.LocalDef, BindingKind.LambdaTypedLocal);

    /// <summary>The one value that is the image of no symbol (finding 6: a builtin IS a symbol, bound by
    /// <c>SymbolTable.PopulateBuiltins</c>).</summary>
    internal static readonly ImmutableArray<BindingKind> NonSymbolKinds = ImmutableArray.Create(BindingKind.None);

    /// <summary>
    /// The defining scope decides the module / type / local split (D3.a, finding 7): all six <see cref="ScopeKind"/>
    /// values, no default arm. <c>InitializerHost</c> is where a comprehension / lambda / walrus local inside an
    /// initializer expression binds; <c>Transparent</c> holds only type parameters.
    /// </summary>
    internal static readonly IReadOnlyDictionary<ScopeKind, RosterEntry<BindingKind>> ScopeSplit = Roster.Map(
        (ScopeKind.Module, Roster.To(ModuleLevelKinds.Add(BindingKind.Builtin).Add(BindingKind.ImportedName).ToArray())),
        (ScopeKind.TypeBody, Roster.To(TypeLevelKinds.AddRange(InstanceKinds).ToArray())),
        (ScopeKind.FunctionLike, Roster.To(PositionLocalKinds.ToArray())),
        (ScopeKind.Block, Roster.To(
            BindingKind.EnclosingLocal, BindingKind.Inner, BindingKind.LocalVar, BindingKind.LocalLet,
            BindingKind.LocalLiteralConst, BindingKind.LocalComputedConst, BindingKind.LocalDef, BindingKind.LambdaTypedLocal)),
        (ScopeKind.InitializerHost, Roster.To(BindingKind.Inner)),
        (ScopeKind.Transparent, Roster.Out<BindingKind>(ExcludedAxis.TypeParametersAndDelegates,
            "a type-parameter scope declares no variable and no C# body (SymbolTable.ScopeKind.Transparent)")));

    internal static readonly IReadOnlyDictionary<SymbolKind, RosterEntry<BindingKind>> SymbolKinds = Roster.Map(
        (SymbolKind.Variable, Roster.Refined<BindingKind>(nameof(VariableCube))),
        (SymbolKind.Parameter, Roster.Refined<BindingKind>(nameof(ParameterModifiers))),
        (SymbolKind.Function, Roster.Refined<BindingKind>(nameof(FunctionCube))),
        (SymbolKind.Type, Roster.Refined<BindingKind>(nameof(TypeKinds))),
        (SymbolKind.Module, Roster.Out<BindingKind>(ExcludedAxis.ProjectRoute, "a module name needs a project route (i)")),
        (SymbolKind.Property, Roster.Out<BindingKind>(ExcludedAxis.DeadEnumValue,
            "no producer: a class property is the non-Symbol PropertySymbol, a module property is VariableSymbol.IsModuleProperty")),
        (SymbolKind.Event, Roster.Out<BindingKind>(ExcludedAxis.DeadEnumValue,
            "no producer: an event is an EventDef record on its owner")),
        (SymbolKind.TypeAlias, Roster.To(BindingKind.ModuleTypeAlias)),
        (SymbolKind.TypeParameter, Roster.Out<BindingKind>(ExcludedAxis.TypeParametersAndDelegates,
            "a T in class G[T] cannot be read or stored as a value (x)")));

    internal static readonly IReadOnlyDictionary<TypeKind, RosterEntry<BindingKind>> TypeKinds = Roster.Map(
        (TypeKind.Class, Roster.To(BindingKind.ModuleClass, BindingKind.NestedType)),
        (TypeKind.Struct, Roster.To(BindingKind.ModuleStruct)),
        (TypeKind.Interface, Roster.To(BindingKind.ModuleInterface)),
        (TypeKind.Enum, Roster.To(BindingKind.ModuleEnum)),
        (TypeKind.Union, Roster.To(BindingKind.ModuleUnion)),
        (TypeKind.Delegate, Roster.Out<BindingKind>(ExcludedAxis.TypeParametersAndDelegates,
            "no Sharpy declaration: () -> int is the annotation, whose binding is LambdaTypedLocal (x)")));

    internal static readonly IReadOnlyDictionary<TypeBodyMemberKind, RosterEntry<BindingKind>> MemberKinds = Roster.Map(
        (TypeBodyMemberKind.Field, Roster.To(
            BindingKind.StaticField, BindingKind.InstanceField, BindingKind.ClassLiteralConst, BindingKind.ClassComputedConst)),
        (TypeBodyMemberKind.Property, Roster.To(BindingKind.StaticProperty, BindingKind.InstanceProperty)),
        (TypeBodyMemberKind.Event, Roster.Out<BindingKind>(ExcludedAxis.EventsSplitPropertiesTupleStarStores, "events are #2400 (iii)")),
        // `__init__` is a Method member of its type (TypeBodyMembership.DescribeMemberOf reads it from TypeSymbol.Methods):
        // P77b D3.1's round trip read it there.
        (TypeBodyMemberKind.Method, Roster.To(BindingKind.StaticMethod, BindingKind.InstanceMethod, BindingKind.Constructor)));

    internal static readonly IReadOnlyDictionary<ParameterModifier, RosterEntry<BindingKind>> ParameterModifiers = Roster.Map(
        (ParameterModifier.None, Roster.To(BindingKind.Parameter)),
        (ParameterModifier.Ref, Roster.Out<BindingKind>(ExcludedAxis.RefInParameterModifiers, "#2154 / #2156's class (xv)")),
        (ParameterModifier.Out, Roster.Refined<BindingKind>(CheckModifiedArgumentSite)),
        (ParameterModifier.In, Roster.Out<BindingKind>(ExcludedAxis.RefInParameterModifiers, "#2154 / #2156's class (xv)")));

    /// <summary>Every <c>bool</c> of <c>VariableSymbol</c>, the base's included (D3.a): by reflection against this map.</summary>
    internal static readonly IReadOnlyDictionary<string, RosterEntry<BindingKind>> VariableFlags = Roster.Map(
        ("IsParameter", Roster.To(BindingKind.Parameter)),
        ("IsConstant", Roster.To(
            BindingKind.ModuleLiteralConst, BindingKind.ModuleComputedConst, BindingKind.ClassLiteralConst,
            BindingKind.ClassComputedConst, BindingKind.LocalLiteralConst, BindingKind.LocalComputedConst)),
        ("IsStatic", Roster.To(BindingKind.StaticField)),
        ("IsCompileTimeConstant", Roster.To(
            BindingKind.ModuleLiteralConst, BindingKind.ClassLiteralConst, BindingKind.LocalLiteralConst)),
        ("IsModuleProperty", Roster.To(BindingKind.ModuleProperty)),
        ("IsFinal", Roster.Out<BindingKind>(ExcludedAxis.NotAKind,
            "reads as its field kind; the @final store refusal is a kind rule (fidelity row 2), so it is no STORE outer")),
        ("HasDefaultValue", Roster.Out<BindingKind>(ExcludedAxis.DeclarationWithoutInitializer,
            "a declaration with no initializer reads the CLR default at every order (xii)")),
        ("HasPropertyGetter", Roster.Out<BindingKind>(ExcludedAxis.NotAKind,
            "ModuleProperty's STORE writability: a getter-only property is a must-refuse by kind, left out of the STORE outer axis")),
        ("HasPropertySetter", Roster.Out<BindingKind>(ExcludedAxis.NotAKind,
            "ModuleProperty's STORE writability: the StoreOuter ModuleProperty is the setter-bearing one; READ is unaffected")),
        ("IsNameBacktickEscaped", Roster.Out<BindingKind>(ExcludedAxis.NotAKind,
            "a lexer spelling fact; the generator never backtick-escapes")),
        ("IsReExport", Roster.Out<BindingKind>(ExcludedAxis.ProjectRoute, "a re-export needs a project route (i)")),
        ("IsErrorRecovery", Roster.Out<BindingKind>(ExcludedAxis.NotAKind,
            "a recovery symbol from BindRefusedCapture models no program binding")));

    /// <summary>The flag axis of <see cref="VariableCube"/>: the flags that decide a kind, and no flag.</summary>
    internal static readonly ImmutableArray<string> VariableCubeFlags =
        ImmutableArray.Create(NoFlag, "IsConstant", "IsStatic", "IsModuleProperty", "IsParameter");

    internal const string NoFlag = "NoFlag";

    internal static string CubeKey(SymbolKind kind, ScopeKind scope, string flag) => $"SymbolKind.{kind} × {scope} × {flag}";

    private const string NotInThisScope = "no binder of this scope sets the flag (D3.e's census)";

    /// <summary><c>SymbolKind.Variable</c> × defining <see cref="ScopeKind"/> × deciding flag: the whole product, so a
    /// flag alone never decides a kind. <c>let</c> / <c>var</c> is the AST's <c>IsLet</c>, not a symbol flag.</summary>
    internal static readonly IReadOnlyDictionary<string, RosterEntry<BindingKind>> VariableCube = Roster.Map(
        (CubeKey(SymbolKind.Variable, ScopeKind.Module, NoFlag), Roster.To(BindingKind.ModuleVar, BindingKind.ModuleLet)),
        (CubeKey(SymbolKind.Variable, ScopeKind.Module, "IsConstant"), Roster.To(BindingKind.ModuleLiteralConst, BindingKind.ModuleComputedConst)),
        (CubeKey(SymbolKind.Variable, ScopeKind.Module, "IsStatic"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind,
            "only a module property sets it at Module (NameResolver.Members.cs ResolveModulePropertyDeclaration), and IsModuleProperty decides that kind")),
        (CubeKey(SymbolKind.Variable, ScopeKind.Module, "IsModuleProperty"), Roster.To(BindingKind.ModuleProperty)),
        (CubeKey(SymbolKind.Variable, ScopeKind.Module, "IsParameter"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),

        (CubeKey(SymbolKind.Variable, ScopeKind.TypeBody, NoFlag), Roster.To(BindingKind.InstanceField)),
        (CubeKey(SymbolKind.Variable, ScopeKind.TypeBody, "IsConstant"), Roster.To(BindingKind.ClassLiteralConst, BindingKind.ClassComputedConst)),
        (CubeKey(SymbolKind.Variable, ScopeKind.TypeBody, "IsStatic"), Roster.To(BindingKind.StaticField)),
        (CubeKey(SymbolKind.Variable, ScopeKind.TypeBody, "IsModuleProperty"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.TypeBody, "IsParameter"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),

        (CubeKey(SymbolKind.Variable, ScopeKind.FunctionLike, NoFlag), Roster.To(BindingKind.LocalVar, BindingKind.LocalLet, BindingKind.EnclosingLocal, BindingKind.LambdaTypedLocal)),
        (CubeKey(SymbolKind.Variable, ScopeKind.FunctionLike, "IsConstant"), Roster.To(BindingKind.LocalLiteralConst, BindingKind.LocalComputedConst)),
        (CubeKey(SymbolKind.Variable, ScopeKind.FunctionLike, "IsStatic"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.FunctionLike, "IsModuleProperty"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.FunctionLike, "IsParameter"), Roster.To(BindingKind.Parameter, BindingKind.Inner)),

        (CubeKey(SymbolKind.Variable, ScopeKind.Block, NoFlag), Roster.To(BindingKind.LocalVar, BindingKind.LocalLet, BindingKind.EnclosingLocal, BindingKind.Inner)),
        (CubeKey(SymbolKind.Variable, ScopeKind.Block, "IsConstant"), Roster.To(BindingKind.LocalLiteralConst, BindingKind.LocalComputedConst)),
        (CubeKey(SymbolKind.Variable, ScopeKind.Block, "IsStatic"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.Block, "IsModuleProperty"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.Block, "IsParameter"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),

        (CubeKey(SymbolKind.Variable, ScopeKind.InitializerHost, NoFlag), Roster.To(BindingKind.Inner)),
        (CubeKey(SymbolKind.Variable, ScopeKind.InitializerHost, "IsConstant"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.InitializerHost, "IsStatic"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.InitializerHost, "IsModuleProperty"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.InitializerHost, "IsParameter"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NotInThisScope)),

        (CubeKey(SymbolKind.Variable, ScopeKind.Transparent, NoFlag), Roster.Out<BindingKind>(ExcludedAxis.TypeParametersAndDelegates, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.Transparent, "IsConstant"), Roster.Out<BindingKind>(ExcludedAxis.TypeParametersAndDelegates, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.Transparent, "IsStatic"), Roster.Out<BindingKind>(ExcludedAxis.TypeParametersAndDelegates, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.Transparent, "IsModuleProperty"), Roster.Out<BindingKind>(ExcludedAxis.TypeParametersAndDelegates, NotInThisScope)),
        (CubeKey(SymbolKind.Variable, ScopeKind.Transparent, "IsParameter"), Roster.Out<BindingKind>(ExcludedAxis.TypeParametersAndDelegates, NotInThisScope)));

    /// <summary>The function axis of <see cref="FunctionCube"/>: an ordinary def, a <c>@static</c> one, and <c>__init__</c>.</summary>
    internal static readonly ImmutableArray<string> FunctionCubeClasses = ImmutableArray.Create(NoFlag, "IsStatic", "__init__");

    private const string NoDefStatementHere = "an expression position holds no def statement";

    private const string MemberOnly = "@static and the __init__ name class mean something only on a type's member; here they are the ordinary def's kind";

    /// <summary><c>SymbolKind.Function</c> × owner <see cref="ScopeKind"/> × function class. A lambda-typed local is NOT here: its
    /// binding is a <c>VariableSymbol</c> (P77b D3.1's round trip), so <see cref="VariableCube"/> maps it; its READ is the call,
    /// which is <c>FormOf</c>'s, not a symbol roster's.</summary>
    internal static readonly IReadOnlyDictionary<string, RosterEntry<BindingKind>> FunctionCube = Roster.Map(
        (CubeKey(SymbolKind.Function, ScopeKind.Module, NoFlag), Roster.To(BindingKind.ModuleDef)),
        (CubeKey(SymbolKind.Function, ScopeKind.Module, "IsStatic"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, MemberOnly)),
        (CubeKey(SymbolKind.Function, ScopeKind.Module, "__init__"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, MemberOnly)),
        (CubeKey(SymbolKind.Function, ScopeKind.TypeBody, NoFlag), Roster.To(BindingKind.InstanceMethod)),
        (CubeKey(SymbolKind.Function, ScopeKind.TypeBody, "IsStatic"), Roster.To(BindingKind.StaticMethod)),
        (CubeKey(SymbolKind.Function, ScopeKind.TypeBody, "__init__"), Roster.To(BindingKind.Constructor)),
        (CubeKey(SymbolKind.Function, ScopeKind.FunctionLike, NoFlag), Roster.To(BindingKind.LocalDef)),
        (CubeKey(SymbolKind.Function, ScopeKind.FunctionLike, "IsStatic"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, MemberOnly)),
        (CubeKey(SymbolKind.Function, ScopeKind.FunctionLike, "__init__"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, MemberOnly)),
        (CubeKey(SymbolKind.Function, ScopeKind.Block, NoFlag), Roster.To(BindingKind.LocalDef)),
        (CubeKey(SymbolKind.Function, ScopeKind.Block, "IsStatic"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, MemberOnly)),
        (CubeKey(SymbolKind.Function, ScopeKind.Block, "__init__"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, MemberOnly)),
        (CubeKey(SymbolKind.Function, ScopeKind.InitializerHost, NoFlag), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NoDefStatementHere)),
        (CubeKey(SymbolKind.Function, ScopeKind.InitializerHost, "IsStatic"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NoDefStatementHere)),
        (CubeKey(SymbolKind.Function, ScopeKind.InitializerHost, "__init__"), Roster.Out<BindingKind>(ExcludedAxis.NotAKind, NoDefStatementHere)),
        (CubeKey(SymbolKind.Function, ScopeKind.Transparent, NoFlag), Roster.Out<BindingKind>(ExcludedAxis.TypeParametersAndDelegates, NotInThisScope)),
        (CubeKey(SymbolKind.Function, ScopeKind.Transparent, "IsStatic"), Roster.Out<BindingKind>(ExcludedAxis.TypeParametersAndDelegates, NotInThisScope)),
        (CubeKey(SymbolKind.Function, ScopeKind.Transparent, "__init__"), Roster.Out<BindingKind>(ExcludedAxis.TypeParametersAndDelegates, NotInThisScope)));

    // ---------------------------------------------------------------------------------------------------------
    // Roster 2: binder sites (D3.e STORE), keyed <file>:<enclosing method>(<argument text>)[#n]. #n numbers the
    // sites with one text in one method, in source order: the two CheckWith(varSymbol) sites bind different things.
    // ---------------------------------------------------------------------------------------------------------

    internal const string CheckModifiedArgumentSite = "TypeChecker.Expressions.cs:CheckModifiedArgument(newSymbol)";

    private const string TypeParameter = "a type parameter is never a value binding (x)";

    private const string ProjectRouteBinder = "a project phase or a module-qualified spelling binds this: a single-file generator cannot enumerate it (i)";

    /// <summary>The binder of every import spelling (<c>ImportResolver.BindImportName</c>), the source of <see cref="BindingKind.ImportedName"/>.</summary>
    internal const string ImportBinderSite = "ImportResolver.Symbols.cs:BindImportName(symbol)";

    internal static readonly IReadOnlyDictionary<string, BinderImage> BinderSites = Roster.Map(
        // STORE forms
        ("TypeChecker.Statements.cs:CheckAssignment(newSymbol)", BinderImage.Store(StoreForm.Assign, BindingKind.LocalVar, BindingKind.LocalLet, BindingKind.LambdaTypedLocal)),
        ("TypeChecker.Statements.cs:CheckVariableDeclaration(newSymbol)", BinderImage.Store(StoreForm.LetOrVar, BindingKind.ModuleVar, BindingKind.ModuleLet, BindingKind.LocalVar, BindingKind.LocalLet, BindingKind.LambdaTypedLocal)),
        ("TypeChecker.Expressions.cs:CheckWalrusExpression(newSymbol)", BinderImage.Store(StoreForm.Walrus, BindingKind.LocalVar, BindingKind.Inner)),
        (CheckModifiedArgumentSite, BinderImage.StoreAndLocal(StoreForm.OutLet, LocalTarget.OutLet, BindingKind.LocalLet)),

        // LOCAL construct targets
        ("TypeChecker.Statements.cs:CheckFor(loopVarSymbol)", BinderImage.Local(LocalTarget.For)),
        ("TypeChecker.Statements.cs:CheckTry(varSymbol)", BinderImage.Local(LocalTarget.ExceptAs)),
        ("TypeChecker.Statements.cs:CheckWith(varSymbol)#2", BinderImage.Local(LocalTarget.WithAs)),
        ("TypeChecker.Expressions.Literals.cs:CheckComprehensionForClause(loopVarSymbol)", BinderImage.Local(LocalTarget.Comprehension, BindingKind.Inner)),
        ("TypeChecker.Statements.Patterns.cs:CheckPattern(newSymbol)", BinderImage.Local(LocalTarget.MatchCapture)),
        ("TypeChecker.Statements.Patterns.cs:BindAsPatternCapture(newSymbol)", BinderImage.Local(LocalTarget.AsPatternCapture)),

        // Declaring kinds
        ("NameResolver.Declarations.cs:ResolveClassDeclaration(typeSymbol)", BinderImage.Declaring(BindingKind.ModuleClass)),
        ("NameResolver.Declarations.cs:ResolveStructDeclaration(typeSymbol)", BinderImage.Declaring(BindingKind.ModuleStruct)),
        ("NameResolver.Declarations.cs:ResolveInterfaceDeclaration(typeSymbol)", BinderImage.Declaring(BindingKind.ModuleInterface)),
        ("NameResolver.Declarations.cs:ResolveEnumDeclaration(typeSymbol)", BinderImage.Declaring(BindingKind.ModuleEnum)),
        ("NameResolver.Declarations.cs:ResolveUnionDeclaration(unionSymbol)", BinderImage.Declaring(BindingKind.ModuleUnion)),
        ("NameResolver.Members.cs:ResolveFunctionDeclaration(funcSymbol)", BinderImage.Declaring(BindingKind.ModuleDef)),
        ("NameResolver.Members.cs:ResolveFunctionDeclaration(funcSymbol)#2", BinderImage.Declaring(BindingKind.ModuleDef)),
        ("NameResolver.Members.cs:TryDeclareMember(member)", BinderImage.Declaring(
            BindingKind.StaticField, BindingKind.StaticMethod, BindingKind.InstanceField, BindingKind.InstanceMethod, BindingKind.Constructor)),
        ("NameResolver.Members.cs:ResolveConstantDeclaration(varSymbol)", BinderImage.Declaring(BindingKind.ModuleLiteralConst, BindingKind.ModuleComputedConst)),
        ("TypeChecker.Statements.cs:CheckVariableDeclaration(constSymbol)", BinderImage.Declaring(BindingKind.LocalLiteralConst, BindingKind.LocalComputedConst)),
        ("TypeChecker.Statements.cs:DefineTypeBodyConstFields(field)", BinderImage.Declaring(BindingKind.ClassLiteralConst, BindingKind.ClassComputedConst)),
        ("NameResolver.Members.cs:ResolveModulePropertyDeclaration(propSymbol)", BinderImage.Declaring(BindingKind.ModuleProperty)),
        ("NameResolver.Members.cs:ResolveTypeAliasDeclaration(TypeAliasSymbol.CreateFrom(typeAlias, _currentFilePath))", BinderImage.Declaring(BindingKind.ModuleTypeAlias)),
        ("TypeChecker.Definitions.cs:CheckFunction(nestedFuncSymbol)", BinderImage.Declaring(BindingKind.LocalDef)),
        ("TypeChecker.Definitions.cs:CheckClass(nestedType)", BinderImage.Declaring(BindingKind.NestedType)),
        ("TypeChecker.Definitions.cs:CheckStruct(nestedType)", BinderImage.Declaring(BindingKind.NestedType)),
        ("TypeChecker.Definitions.cs:CheckInterface(nestedType)", BinderImage.Declaring(BindingKind.NestedType)),
        ("TypeChecker.Definitions.cs:RegisterFunctionParameters(paramSymbol)", BinderImage.Declaring(BindingKind.Parameter)),
        ("TypeChecker.Expressions.Access.Lambdas.cs:CheckLambda(paramSymbol)", BinderImage.Declaring(BindingKind.Inner)),
        ("TypeChecker.Definitions.cs:CheckModuleProperty(paramSymbol)", BinderImage.Declaring(BindingKind.Parameter)),
        ("TypeChecker.Definitions.cs:CheckClassProperty(paramSymbol)", BinderImage.Declaring(BindingKind.Parameter)),

        // The global scope (finding 6): a builtin is a symbol, and these are its binders.
        ("SymbolTable.cs:PopulateBuiltins(typeSymbol)", BinderImage.Declaring(BindingKind.Builtin)),
        ("SymbolTable.cs:PopulateBuiltins(name, typeSymbol)", BinderImage.Declaring(BindingKind.Builtin)),
        ("SymbolTable.cs:PopulateBuiltins(funcSymbol)", BinderImage.Declaring(BindingKind.Builtin)),

        // The import seam (P77b axis 9, D2.9): every import spelling binds here; the single-file `from <stdlib module> import
        // <name>` is the ImportedName outer. The module-qualified and other-user-module spellings stay out (i).
        (ImportBinderSite, BinderImage.Declaring(BindingKind.ImportedName)),

        // Excluded: the census's own subject
        ("SymbolTable.cs:Define(symbol)", BinderImage.Out(ExcludedAxis.CompilerInternal, "the SymbolTable.Define wrapper over Scope.Define: every other row is its caller")),
        ("SymbolTable.cs:TryDefine(symbol)", BinderImage.Out(ExcludedAxis.CompilerInternal, "the SymbolTable.TryDefine wrapper over Scope.Define: every other row is its caller")),

        // Excluded: type parameters (16)
        ("NameResolver.Declarations.cs:ResolveClassDeclaration(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("NameResolver.Declarations.cs:ResolveStructDeclaration(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("NameResolver.Declarations.cs:ResolveInterfaceDeclaration(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("NameResolver.Declarations.cs:ResolveDelegateDeclaration(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("NameResolver.Declarations.cs:ResolveUnionDeclaration(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("TypeResolver.cs:ResolveInTypeParameterScope(new TypeParameterSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("TypeResolver.cs:ExpandGenericTypeAlias(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("GenericTypeInferenceService.cs:PushConstraintScope(new TypeParameterSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("TypeChecker.Definitions.cs:ResolveFunctionSignatureInto(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("TypeChecker.Definitions.cs:CheckFunction(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("TypeChecker.Definitions.cs:CheckClass(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("TypeChecker.Definitions.cs:CheckStruct(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("TypeChecker.Definitions.cs:CheckInterface(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("TypeChecker.Definitions.cs:CheckInterface(typeParamSymbol)#2", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("TypeChecker.Definitions.cs:CheckUnion(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),
        ("TypeChecker.Definitions.cs:CheckDelegate(typeParamSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, TypeParameter)),

        // Excluded: each with its axis
        ("NameResolver.Declarations.cs:ResolveDelegateDeclaration(typeSymbol)", BinderImage.Out(ExcludedAxis.TypeParametersAndDelegates, "the delegate kind has no value spelling (x)")),
        ("TypeChecker.Definitions.cs:CheckEvent(paramSymbol)", BinderImage.Out(ExcludedAxis.EventsSplitPropertiesTupleStarStores, "an event accessor's value parameter (iii)")),
        ("TypeChecker.Definitions.cs:CheckPropertyObservers(selfSymbol)", BinderImage.Out(ExcludedAxis.PropertyObservers, "experimental, behind property_observers (iv)")),
        ("TypeChecker.Definitions.cs:CheckPropertyObservers(paramSymbol)", BinderImage.Out(ExcludedAxis.PropertyObservers, "experimental, behind property_observers (iv)")),
        ("TypeChecker.Definitions.cs:ResolvePropertyTypes(backingField)", BinderImage.Out(ExcludedAxis.CompilerInternal, "a synthesized backing field: no user spelling")),
        ("TypeChecker.Utilities.cs:RegisterScopedTypeAlias(TypeAliasSymbol.CreateFrom(typeAlias, _currentFilePath))", BinderImage.Out(ExcludedAxis.FunctionScopedTypeAlias, "a TYPE-name binding inside a body (xi)")),
        ("TypeChecker.Statements.cs:CheckWith(varSymbol)", BinderImage.Out(ExcludedAxis.AssertRaisesCapture, "the assert_raises capture, defined in the ENCLOSING scope (xiv)")),
        ("TypeChecker.Statements.Patterns.cs:BindRefusedCapture(recovery)", BinderImage.Out(ExcludedAxis.NotAKind, "error recovery: IsErrorRecovery models no program binding")),
        ("TypeChecker.Statements.Patterns.cs:CheckTupleUnpackingElements(newSymbol)", BinderImage.Out(ExcludedAxis.EventsSplitPropertiesTupleStarStores, "a tuple store (iii)")),
        ("TypeChecker.Statements.Unpacking.cs:DefineUnpackingSymbol(symbol)", BinderImage.Out(ExcludedAxis.EventsSplitPropertiesTupleStarStores, "a tuple / star store (iii)")),
        ("InheritanceResolver.cs:ResolveTransitiveBaseTypes(found)", BinderImage.Out(ExcludedAxis.ProjectRoute, ProjectRouteBinder)),
        ("InheritanceResolver.cs:ResolveTransitiveBaseTypes(found)#2", BinderImage.Out(ExcludedAxis.ProjectRoute, ProjectRouteBinder)),
        ("TypeChecker.Expressions.cs:TryResolveInlineClrNamespace(moduleSymbol)", BinderImage.Out(ExcludedAxis.ProjectRoute, ProjectRouteBinder)),
        ("ProjectCompiler.Initialization.cs:RestoreCachedSymbols(symbol)", BinderImage.Out(ExcludedAxis.ProjectRoute, ProjectRouteBinder)),
        ("ProjectCompiler.Initialization.cs:RestoreCachedSymbols(symbol)#2", BinderImage.Out(ExcludedAxis.ProjectRoute, ProjectRouteBinder)),
        ("ProjectCompiler.Initialization.cs:RestoreCachedSymbols(symbol)#3", BinderImage.Out(ExcludedAxis.ProjectRoute, ProjectRouteBinder)),
        ("ProjectCompiler.Phases.cs:CollectTypeDeclarations(symbol)", BinderImage.Out(ExcludedAxis.ProjectRoute, ProjectRouteBinder)),
        ("SymbolTable.cs:MergeFrom(symbol)", BinderImage.Out(ExcludedAxis.ProjectRoute, "the per-file to project merge (i)")),
        ("RoslynEmitter.ModuleClass.cs:GenerateModuleMembers(enumSymbol)", BinderImage.Out(ExcludedAxis.CompilerInternal, "the emitter's own table re-registering a declared symbol: not a semantic binder")),
        ("RoslynEmitter.ModuleClass.cs:GenerateModuleMembers(unionSymbol)", BinderImage.Out(ExcludedAxis.CompilerInternal, "the emitter's own table re-registering a declared symbol: not a semantic binder")));

    /// <summary>The store forms with NO binder site. <c>+=</c> never creates a binding: SPY0600 / SPY0200 on an undeclared name.</summary>
    internal static readonly ImmutableArray<StoreForm> NoSiteStoreForms = ImmutableArray.Create(StoreForm.Augmented);

    /// <summary>
    /// The spellings <c>CheckModifiedArgument(newSymbol)</c> binds, one site with four spellings. <c>out let</c> always
    /// introduces (R-BM, #1974), which is the <see cref="StoreForm.OutLet"/> a let by another spelling; <c>out v: T</c> and
    /// <c>out v: auto</c> rebind an existing variable as a walrus does (the checker's own comment), so they are the
    /// <c>out</c> store of excluded axis (iii) and NOT that form. Asserted against the parser's <c>IsLet</c>.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, RosterEntry<StoreForm>> OutSpellings = Roster.Map(
        ("out let v", Roster.To(StoreForm.OutLet)),
        ("out let v: int", Roster.To(StoreForm.OutLet)),
        ("out v: int", Roster.Out<StoreForm>(ExcludedAxis.EventsSplitPropertiesTupleStarStores,
            "rebinds an existing variable: an out store of (iii), #2154 / #2156 / P21d")),
        ("out v: auto", Roster.Out<StoreForm>(ExcludedAxis.EventsSplitPropertiesTupleStarStores,
            "rebinds an existing variable: an out store of (iii), #2154 / #2156 / P21d")));

    // ---------------------------------------------------------------------------------------------------------
    // Roster 3: class-body positions (D3.c (1))
    // ---------------------------------------------------------------------------------------------------------

    internal static string ClassBodyKey(string position, string slot) => $"{position} × {slot}";

    /// <summary>The pairs the dispatch builds. Every other pair of the 5 × 6 product is on <see cref="UnbuiltClassBodyPairs"/>.</summary>
    internal static readonly IReadOnlyDictionary<string, RosterEntry<ReadPosition>> ClassBodyPairs = Roster.Map(
        (ClassBodyKey("MethodBody", "Body"), Roster.To(
            ReadPosition.MethodBody, ReadPosition.StaticMethodBody, ReadPosition.InitBody, ReadPosition.AccessorGetBody,
            ReadPosition.AccessorSetBody, ReadPosition.NestedClassMethodBody, ReadPosition.NestedDefInMethod)),
        (ClassBodyKey("StaticInitializer", "Field"), Roster.To(ReadPosition.StaticInitializer)),
        (ClassBodyKey("InstanceInitializer", "Field"), Roster.To(
            ReadPosition.InstanceFieldInitializer, ReadPosition.DataclassInitFieldInitializer, ReadPosition.StructInitFieldInitializer)),
        (ClassBodyKey("ParameterDefault", "SynthesizedConstructorField"), Roster.To(
            ReadPosition.DataclassFieldInitializer, ReadPosition.StructFieldInitializer)),
        (ClassBodyKey("ConstInitializer", "Const"), Roster.To(ReadPosition.ConstInitializer)),
        (ClassBodyKey("StaticInitializer", "Property"), Roster.To(ReadPosition.StaticPropertyDefault)),
        (ClassBodyKey("InstanceInitializer", "Property"), Roster.To(ReadPosition.InstancePropertyDefault)),
        (ClassBodyKey("ParameterDefault", "Parameter"), Roster.To(ReadPosition.ParameterDefault)));

    /// <summary>The rest of the product, each a pair no <c>ClassBodyReader</c> construction names.</summary>
    internal static readonly ImmutableArray<string> UnbuiltClassBodyPairs = ImmutableArray.Create(
        ClassBodyKey("MethodBody", "Field"),
        ClassBodyKey("MethodBody", "Property"),
        ClassBodyKey("MethodBody", "Const"),
        ClassBodyKey("MethodBody", "Parameter"),
        ClassBodyKey("MethodBody", "SynthesizedConstructorField"),
        ClassBodyKey("InstanceInitializer", "Body"),
        ClassBodyKey("InstanceInitializer", "Const"),
        ClassBodyKey("InstanceInitializer", "Parameter"),
        ClassBodyKey("InstanceInitializer", "SynthesizedConstructorField"),
        ClassBodyKey("StaticInitializer", "Body"),
        ClassBodyKey("StaticInitializer", "Const"),
        ClassBodyKey("StaticInitializer", "Parameter"),
        ClassBodyKey("StaticInitializer", "SynthesizedConstructorField"),
        ClassBodyKey("ConstInitializer", "Body"),
        ClassBodyKey("ConstInitializer", "Field"),
        ClassBodyKey("ConstInitializer", "Property"),
        ClassBodyKey("ConstInitializer", "Parameter"),
        ClassBodyKey("ConstInitializer", "SynthesizedConstructorField"),
        ClassBodyKey("ParameterDefault", "Body"),
        ClassBodyKey("ParameterDefault", "Field"),
        ClassBodyKey("ParameterDefault", "Property"),
        ClassBodyKey("ParameterDefault", "Const"));

    // ---------------------------------------------------------------------------------------------------------
    // Roster 4: body positions (D3.c (2))
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>The FunctionLike families of <c>ClassifyScope</c>, through <c>ScopeNameFamilies()</c>.</summary>
    internal static readonly IReadOnlyDictionary<string, RosterEntry<ReadPosition>> FunctionLikeFamilies = Roster.Map(
        ("lambda", Roster.To(ReadPosition.LambdaStored, ReadPosition.LambdaImmediate, ReadPosition.LambdaPassed)),
        ("function:", Roster.Refined<ReadPosition>($"{nameof(FunctionFlags)} × {nameof(FunctionOwners)}")),
        ("property:", Roster.To(ReadPosition.AccessorGetBody, ReadPosition.AccessorSetBody, ReadPosition.ModuleAccessorBody)),
        ("pre-pass:", Roster.Out<ReadPosition>(ExcludedAxis.CompilerInternal, "the checker's signature pre-pass: no user position")),
        ("observer:", Roster.Out<ReadPosition>(ExcludedAxis.PropertyObservers, "experimental, behind property_observers (iv)")),
        ("event:", Roster.Out<ReadPosition>(ExcludedAxis.EventsSplitPropertiesTupleStarStores, "an event accessor body (iii)")),
        ("interface-method:", Roster.Out<ReadPosition>(ExcludedAxis.InterfaceUnionEnumGenericHosts, "an interface default method's host (ii)")));

    /// <summary><c>FunctionSymbol</c>'s own bools with <c>BindingFlags.DeclaredOnly</c> (finding 8).</summary>
    internal static readonly IReadOnlyDictionary<string, RosterEntry<ReadPosition>> FunctionFlags = Roster.Map(
        ("IsStatic", Roster.To(ReadPosition.StaticMethodBody)),
        ("IsGenerator", Roster.To(ReadPosition.GeneratorBody)),
        ("IsAsync", Roster.Out<ReadPosition>(ExcludedAxis.AsyncBodies, "the same function: family; the twin needs an await chain (xiii)")),
        ("IsAbstract", Roster.Out<ReadPosition>(ExcludedAxis.NotAKind, "an abstract method has no body to read from")),
        ("IsVirtual", Roster.Out<ReadPosition>(ExcludedAxis.NotAKind, "dispatch, not scope: the inherited read is the declaring-type base axis")),
        ("IsOverride", Roster.Out<ReadPosition>(ExcludedAxis.NotAKind, "dispatch, not scope: the inherited read is the declaring-type base axis")),
        ("IsGeneric", Roster.Out<ReadPosition>(ExcludedAxis.TypeParametersAndDelegates, "a generic body is the same family; its type parameters are (x)")),
        ("IsMustUse", Roster.Out<ReadPosition>(ExcludedAxis.NotAKind, "a decorator fact that leaves the scope family unchanged")),
        ("IsCached", Roster.Out<ReadPosition>(ExcludedAxis.NotAKind, "a decorator fact: a @cached body is a function body")),
        ("IsFormatTemplateReceiver", Roster.Out<ReadPosition>(ExcludedAxis.NotAKind, "a CLR-discovered fact: no Sharpy body carries it")));

    internal static string OwnerKey(ScopeKind owner, FunctionNameClass nameClass) => $"function: × owner {owner} × {nameClass}";

    private const string NameClassIsAMemberFact = "the __init__ name class is a member fact: outside a type body it is the ordinary row's position";

    /// <summary><c>function:</c> × owner <see cref="ScopeKind"/> × name class. A nested type's method is the TypeBody owner.</summary>
    internal static readonly IReadOnlyDictionary<string, RosterEntry<ReadPosition>> FunctionOwners = Roster.Map(
        (OwnerKey(ScopeKind.Module, FunctionNameClass.Ordinary), Roster.To(ReadPosition.FunctionBody)),
        (OwnerKey(ScopeKind.Module, FunctionNameClass.Init), Roster.Out<ReadPosition>(ExcludedAxis.NotAKind, NameClassIsAMemberFact)),
        (OwnerKey(ScopeKind.TypeBody, FunctionNameClass.Ordinary), Roster.To(ReadPosition.MethodBody, ReadPosition.NestedClassMethodBody)),
        (OwnerKey(ScopeKind.TypeBody, FunctionNameClass.Init), Roster.To(ReadPosition.InitBody)),
        (OwnerKey(ScopeKind.FunctionLike, FunctionNameClass.Ordinary), Roster.To(ReadPosition.NestedDefInFunction, ReadPosition.NestedDefInMethod)),
        (OwnerKey(ScopeKind.FunctionLike, FunctionNameClass.Init), Roster.Out<ReadPosition>(ExcludedAxis.NotAKind, NameClassIsAMemberFact)),
        (OwnerKey(ScopeKind.Block, FunctionNameClass.Ordinary), Roster.Out<ReadPosition>(ExcludedAxis.PrunedRepresentative,
            "a def inside a block is transparent to the walk: the FunctionLike owner's nested def stays")),
        (OwnerKey(ScopeKind.Block, FunctionNameClass.Init), Roster.Out<ReadPosition>(ExcludedAxis.NotAKind, NameClassIsAMemberFact)),
        (OwnerKey(ScopeKind.InitializerHost, FunctionNameClass.Ordinary), Roster.Out<ReadPosition>(ExcludedAxis.NotAKind, NoDefStatementHere)),
        (OwnerKey(ScopeKind.InitializerHost, FunctionNameClass.Init), Roster.Out<ReadPosition>(ExcludedAxis.NotAKind, NoDefStatementHere)),
        (OwnerKey(ScopeKind.Transparent, FunctionNameClass.Ordinary), Roster.Out<ReadPosition>(ExcludedAxis.TypeParametersAndDelegates, NotInThisScope)),
        (OwnerKey(ScopeKind.Transparent, FunctionNameClass.Init), Roster.Out<ReadPosition>(ExcludedAxis.TypeParametersAndDelegates, NotInThisScope)));

    /// <summary>The ride-along nestings: a comprehension family's two read positions, on its host position.</summary>
    internal static readonly ImmutableArray<ReadPosition> ComprehensionNestings =
        ImmutableArray.Create(ReadPosition.ComprehensionIterable, ReadPosition.ComprehensionElement);

    // ---------------------------------------------------------------------------------------------------------
    // Roster 5: module positions (D3.c (3))
    // ---------------------------------------------------------------------------------------------------------

    private const string Executable = "refused by kind at every order: ModuleLevelValidator files it SPY0340 (fidelity row 2)";

    private static readonly ReadPosition[] CommonClassBody =
    {
        ReadPosition.BaseList, ReadPosition.StaticInitializer, ReadPosition.ConstInitializer,
        ReadPosition.StaticPropertyDefault, ReadPosition.InstancePropertyDefault, ReadPosition.ParameterDefault,
        ReadPosition.MethodBody,
    };

    /// <summary>Every <c>Statement</c> subclass against <c>ModuleLevelValidator</c>'s split, which a fact parses. A
    /// <see cref="ExcludedAxis.RefusedByKind"/> entry is on the validator's executable side, and only those are.</summary>
    internal static readonly IReadOnlyDictionary<string, RosterEntry<ReadPosition>> Statements = Roster.Map(
        (nameof(VariableDeclaration), Roster.To(ReadPosition.ModuleInitializer)),
        (nameof(PropertyDef), Roster.To(ReadPosition.ModulePropertyInitializer, ReadPosition.ModuleAccessorBody)),
        (nameof(FunctionDef), Roster.To(ReadPosition.ParameterDefault, ReadPosition.FunctionBody)),
        (nameof(ClassDef), Roster.To(CommonClassBody.Concat(new[]
        {
            ReadPosition.InstanceFieldInitializer, ReadPosition.DataclassFieldInitializer, ReadPosition.DataclassInitFieldInitializer,
        }).ToArray())),
        (nameof(StructDef), Roster.To(CommonClassBody.Concat(new[]
        {
            ReadPosition.StructFieldInitializer, ReadPosition.StructInitFieldInitializer,
        }).ToArray())),
        (nameof(InterfaceDef), Roster.To(ReadPosition.BaseList)),
        (nameof(EnumDef), Roster.To(ReadPosition.EnumMemberValue)),
        (nameof(UnionDef), Roster.Refined<ReadPosition>($"{nameof(TypeAnnotationProperties)}: UnionCaseField.Type")),
        (nameof(TypeAlias), Roster.To(ReadPosition.TypeAliasTarget)),
        (nameof(DelegateDef), Roster.Out<ReadPosition>(ExcludedAxis.TypeParametersAndDelegates, "(x)")),
        (nameof(EventDef), Roster.Out<ReadPosition>(ExcludedAxis.EventsSplitPropertiesTupleStarStores, "(iii): a module event is SPY0708 by kind")),
        (nameof(ImportStatement), Roster.Out<ReadPosition>(ExcludedAxis.ProjectRoute, "(i)")),
        (nameof(FromImportStatement), Roster.Out<ReadPosition>(ExcludedAxis.ProjectRoute, "(i)")),
        (nameof(DecoratedStatement), Roster.Refined<ReadPosition>("transparent: ModuleLevelValidator classifies UnwrapDecorated()'s inner statement")),
        (nameof(Assignment), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(ExpressionStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(IfStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(WhileStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(ForStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(TryStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(WithStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(AssertStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(ReturnStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(BreakStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(ContinueStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(RaiseStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(PassStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(MatchStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(YieldStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)),
        (nameof(DeferStatement), Roster.Out<ReadPosition>(ExcludedAxis.RefusedByKind, Executable)));

    // ---------------------------------------------------------------------------------------------------------
    // Roster 6: type positions (D3.c (4), finding 5): scalar ∪ IEnumerable<TypeAnnotation>-typed AST properties.
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A dead AST property, measured by the renderer agent @ f1418767d: <c>case v: Later</c> is SPY0102 because the parser never
    /// sets <c>BindingPattern.Type</c> (neither of its two constructions does, <c>Parser.Statements.cs</c>). The class stays
    /// <see cref="PatternClass.NamesTypeOrMember"/> since it owns the property, but there is no typed-capture cell: the generator
    /// reaches a later type's name in a capture through <c>case Later() as v:</c>. A producer appearing is red.
    /// </summary>
    internal const string TypedCaptureNote =
        "the typed capture has no parser producer (case v: Later is SPY0102): no generated cell, reach a later type's name through case Later() as v:";

    internal static readonly IReadOnlyDictionary<string, RosterEntry<TypePosition>> TypeAnnotationProperties = Roster.Map(
        ("VariableDeclaration.Type", Roster.To(TypePosition.VariableAnnotation)),
        ("FunctionDef.ReturnType", Roster.To(TypePosition.ReturnAnnotation)),
        ("Parameter.Type", Roster.To(TypePosition.ParameterAnnotation)),
        ("ExceptHandler.ExceptionType", Roster.To(TypePosition.ExceptClause)),
        ("TypeAlias.Type", Roster.To(TypePosition.AliasTarget)),
        ("PropertyDef.Type", Roster.To(TypePosition.PropertyAnnotation)),
        ("PropertyDef.ReturnType", Roster.To(TypePosition.AccessorReturn)),
        ("UnionCaseField.Type", Roster.To(TypePosition.UnionCaseField)),
        ("LambdaExpression.ReturnType", Roster.To(TypePosition.LambdaReturn)),
        ("FunctionType.ReturnType", Roster.To(TypePosition.CallableReturn)),
        ("FunctionType.ParameterTypes", Roster.To(TypePosition.CallableParameter)),
        ("ModifiedArgument.InlineType", Roster.To(TypePosition.OutInlineType)),
        ("TypeCoercion.TargetType", Roster.To(TypePosition.Coercion)),
        ("TypeCheck.CheckType", Roster.To(TypePosition.IsInstance)),
        ("TryExpression.ExceptionTypes", Roster.To(TypePosition.TryExceptionType)),
        ("TypeAnnotation.ErrorType", Roster.To(TypePosition.ResultError)),
        ("TypeAnnotation.TypeArguments", Roster.To(TypePosition.GenericArgument)),
        ("ClassDef.BaseClasses", Roster.To(TypePosition.BaseList)),
        ("StructDef.BaseClasses", Roster.To(TypePosition.BaseList)),
        ("InterfaceDef.BaseInterfaces", Roster.To(TypePosition.BaseList)),
        ("BindingPattern.Type", Roster.Out<TypePosition>(ExcludedAxis.DeadEnumValue, TypedCaptureNote)),
        ("TypePattern.Type", Roster.To(TypePosition.PatternType)),
        ("PropertyPattern.Type", Roster.To(TypePosition.PatternType)),
        ("PositionalPattern.Type", Roster.To(TypePosition.PatternType)),
        ("TypeParameterDef.DefaultType", Roster.Out<TypePosition>(ExcludedAxis.TypeParametersAndDelegates, "(x)")),
        ("TypeConstraint.Type", Roster.Out<TypePosition>(ExcludedAxis.TypeParametersAndDelegates, "(x)")),
        ("DelegateDef.ReturnType", Roster.Out<TypePosition>(ExcludedAxis.TypeParametersAndDelegates, "(x)")),
        ("EventDef.Type", Roster.Out<TypePosition>(ExcludedAxis.EventsSplitPropertiesTupleStarStores, "(iii)")));

    // ---------------------------------------------------------------------------------------------------------
    // Roster 7: patterns (D3.e TYPEMEMBER)
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>The pattern classes that do not read as a generated cell at face value. Only the untyped <c>case v:</c> is
    /// constructible as a <c>BindingPattern</c>, and that is LOCAL's <c>match</c> capture.</summary>
    internal static readonly IReadOnlyDictionary<string, string> PatternNotes = Roster.Map(
        (nameof(BindingPattern), TypedCaptureNote),
        (nameof(AndPattern), "python has no `&` pattern, so a program spelling one has no twin for the control to judge; OrPattern, GuardPattern, AsPattern, TuplePattern and ListPattern keep the composite arm"));

    internal static readonly IReadOnlyDictionary<string, PatternClass> Patterns = Roster.Map(
        (nameof(TypePattern), PatternClass.NamesTypeOrMember),
        (nameof(BindingPattern), PatternClass.NamesTypeOrMember),
        (nameof(MemberAccessPattern), PatternClass.NamesTypeOrMember),
        (nameof(PositionalPattern), PatternClass.NamesTypeOrMember),
        (nameof(PropertyPattern), PatternClass.NamesTypeOrMember),
        (nameof(OrPattern), PatternClass.Composite),
        (nameof(AndPattern), PatternClass.Composite),
        (nameof(GuardPattern), PatternClass.Composite),
        (nameof(AsPattern), PatternClass.Composite),
        (nameof(TuplePattern), PatternClass.Composite),
        (nameof(ListPattern), PatternClass.Composite),
        (nameof(WildcardPattern), PatternClass.CarriesNoName),
        (nameof(LiteralPattern), PatternClass.CarriesNoName),
        (nameof(RelationalPattern), PatternClass.CarriesNoName),
        (nameof(StarPattern), PatternClass.CarriesNoName));

    // ---------------------------------------------------------------------------------------------------------
    // Roster 8: block constructs (D3.e LOCAL): ClassifyScope's Block arms through ScopeNameFamilies(), the ONE source
    // ---------------------------------------------------------------------------------------------------------

    // match-arm is a match EXPRESSION's arm: MatchArm's body is an Expression, so no let sits in it (D3.e names a let
    // here; it cannot be written). Its one binder is the pattern capture CheckPattern(newSymbol) defines in that scope.
    private static RosterEntry<ConstructCell> LetIn(BlockConstruct construct) => Roster.To(new ConstructCell(construct, null));

    private static RosterEntry<ConstructCell> ComprehensionTarget() => Roster.To(new ConstructCell(null, LocalTarget.Comprehension));

    internal static readonly IReadOnlyDictionary<string, RosterEntry<ConstructCell>> BlockFamilies = Roster.Map(
        ("if-then", LetIn(BlockConstruct.IfThen)),
        ("elif", LetIn(BlockConstruct.Elif)),
        ("if-else", LetIn(BlockConstruct.IfElse)),
        ("while-body", LetIn(BlockConstruct.WhileBody)),
        ("while-else", LetIn(BlockConstruct.WhileElse)),
        ("for-body", LetIn(BlockConstruct.ForBody)),
        ("for-else", LetIn(BlockConstruct.ForElse)),
        ("try", LetIn(BlockConstruct.Try)),
        ("except", LetIn(BlockConstruct.Except)),
        ("try-else", LetIn(BlockConstruct.TryElse)),
        ("finally", LetIn(BlockConstruct.Finally)),
        ("with", LetIn(BlockConstruct.With)),
        ("match-case", LetIn(BlockConstruct.MatchCase)),
        ("match-arm", Roster.To(new ConstructCell(null, LocalTarget.MatchCapture))),
        ("list-comprehension", ComprehensionTarget()),
        ("set-comprehension", ComprehensionTarget()),
        ("dict-comprehension", ComprehensionTarget()),
        ("dict-spread-comprehension", ComprehensionTarget()),
        ("generator-expression", ComprehensionTarget()),
        ("defer", Roster.Out<ConstructCell>(ExcludedAxis.GatedConstruct, "behind the defer feature; the harness deploys no flags")));

    // ---------------------------------------------------------------------------------------------------------
    // P77b axis 1: comprehension kind × clause × slot
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>A comprehension AST node's roster-8 family and its kind.</summary>
    internal sealed record ComprehensionKindRow(string Family, ComprehensionKind Kind);

    /// <summary>The comprehension kinds (P77b axis 1): every <c>Expression</c> subclass named <c>*Comprehension</c> or
    /// <c>GeneratorExpression</c>, mapped to its <see cref="BlockFamilies"/> name and kind. The fact asserts it total over both
    /// sources, both directions, so neither the AST nor <c>ClassifyScope</c> can gain a comprehension the axis does not see.</summary>
    internal static readonly IReadOnlyDictionary<string, ComprehensionKindRow> ComprehensionKinds = Roster.Map(
        (nameof(ListComprehension), new ComprehensionKindRow("list-comprehension", ComprehensionKind.List)),
        (nameof(SetComprehension), new ComprehensionKindRow("set-comprehension", ComprehensionKind.Set)),
        (nameof(DictComprehension), new ComprehensionKindRow("dict-comprehension", ComprehensionKind.Dict)),
        (nameof(DictSpreadComprehension), new ComprehensionKindRow("dict-spread-comprehension", ComprehensionKind.DictSpread)),
        (nameof(GeneratorExpression), new ComprehensionKindRow("generator-expression", ComprehensionKind.Generator)));

    /// <summary>The clauses (P77b axis 1): every <c>ComprehensionClause</c> subclass, and every <c>bool</c> property of one keyed
    /// <c>Subclass.Property</c> (the <see cref="VariableFlags"/> shape: a flag is a row or an <c>Out</c>).</summary>
    internal static readonly IReadOnlyDictionary<string, RosterEntry<ComprehensionClauseShape>> ComprehensionClauses = Roster.Map(
        (nameof(ForClause), Roster.To(ComprehensionClauseShape.FirstFor, ComprehensionClauseShape.SecondFor)),
        (nameof(IfClause), Roster.To(ComprehensionClauseShape.If)),
        ($"{nameof(ForClause)}.{nameof(ForClause.IsAsync)}", Roster.Out<ComprehensionClauseShape>(ExcludedAxis.AsyncBodies,
            "an `async for` clause (Parser.Statements.cs sets it) needs an await chain in the twin, which is no name axis (xiii)")));

    /// <summary>The slots (P77b deferred finding 1): every Expression-typed property of the five comprehension nodes and of the
    /// clauses, keyed <c>Type.Property</c>. <c>ForClause.Target</c> is the LOCAL-target row: the name it binds is read after
    /// the comprehension; every other slot holds a read.</summary>
    internal static readonly IReadOnlyDictionary<string, RosterEntry<ComprehensionSlot>> ComprehensionSlots = Roster.Map(
        ($"{nameof(ListComprehension)}.{nameof(ListComprehension.Element)}", Roster.To(ComprehensionSlot.Element)),
        ($"{nameof(SetComprehension)}.{nameof(SetComprehension.Element)}", Roster.To(ComprehensionSlot.Element)),
        ($"{nameof(DictComprehension)}.{nameof(DictComprehension.Key)}", Roster.To(ComprehensionSlot.Key)),
        ($"{nameof(DictComprehension)}.{nameof(DictComprehension.Value)}", Roster.To(ComprehensionSlot.Value)),
        ($"{nameof(DictSpreadComprehension)}.{nameof(DictSpreadComprehension.Spread)}", Roster.To(ComprehensionSlot.Spread)),
        ($"{nameof(GeneratorExpression)}.{nameof(GeneratorExpression.Element)}", Roster.To(ComprehensionSlot.Element)),
        ($"{nameof(ForClause)}.{nameof(ForClause.Target)}", Roster.To(ComprehensionSlot.Target)),
        ($"{nameof(ForClause)}.{nameof(ForClause.Iterator)}", Roster.To(ComprehensionSlot.Iterable)),
        ($"{nameof(IfClause)}.{nameof(IfClause.Condition)}", Roster.To(ComprehensionSlot.Condition)));

    /// <summary>The hosts a comprehension READ cell sits at (P77b axis 1): a function body, where every slot reads at the call,
    /// and a <c>@static</c> initializer, where it reads at load.</summary>
    internal static readonly ImmutableArray<ReadPosition> ComprehensionHosts =
        ImmutableArray.Create(ReadPosition.FunctionBody, ReadPosition.StaticInitializer);

    private const string WalrusIterableSyntaxError =
        "python refuses it at compile time (3.13.18: SyntaxError 'assignment expression cannot be used in a comprehension iterable expression', the first for's and a later one's alike): no twin runs (deferred finding 2)";

    /// <summary>The walrus STORE's slot axis (P77b axis 1, deferred finding 2): a slot that holds an expression the walrus can
    /// sit in is generated, the rest out with the reason.</summary>
    internal static readonly IReadOnlyDictionary<ComprehensionSlot, AxisMembership> ComprehensionWalrusSlots = Roster.Map(
        (ComprehensionSlot.Element, AxisMembership.Everywhere),
        (ComprehensionSlot.Key, AxisMembership.Everywhere),
        (ComprehensionSlot.Value, AxisMembership.Everywhere),
        (ComprehensionSlot.Condition, AxisMembership.Everywhere),
        (ComprehensionSlot.Spread, AxisMembership.Out(ExcludedAxis.NotAKind,
            "python 3.13 has no dict-spread comprehension: the twin is PEP 798's expansion, where the spread is a nested for's iterable and python refuses a walrus there (SyntaxError)")),
        (ComprehensionSlot.Iterable, AxisMembership.Out(ExcludedAxis.NotAKind, WalrusIterableSyntaxError)),
        (ComprehensionSlot.Target, AxisMembership.Out(ExcludedAxis.NotAKind,
            "the target is the clause's binder: the parser takes a name there, never an assignment expression")));

    /// <summary>The walrus STORE's host axis (P77b axis 1, deferred finding 2): total over <see cref="ComprehensionHosts"/>.</summary>
    internal static readonly IReadOnlyDictionary<ReadPosition, AxisMembership> ComprehensionWalrusHosts = Roster.Map(
        (ReadPosition.FunctionBody, AxisMembership.Everywhere),
        (ReadPosition.StaticInitializer, AxisMembership.Out(ExcludedAxis.NotAKind,
            "python refuses it at compile time (3.13.18: SyntaxError 'assignment expression within a comprehension cannot be used in a class body'): no twin runs (deferred finding 2)")));

    // ---------------------------------------------------------------------------------------------------------
    // P77b axis 2: N3 cycle × route
    // ---------------------------------------------------------------------------------------------------------

    private const string NoInitializer = "no initializer: the kind's declaration reads nothing at load, so it closes no cycle";

    private const string LiteralReadsNoName = "a literal initializer reads no name: it cannot close a cycle";

    /// <summary>The cycle's endpoints (P77b deferred finding 3): total over roster 1's module-level, type-level and instance
    /// groups. A value kind with an initializer of its own is an endpoint; every other kind is out with the reason.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, AxisMembership> CycleEndpoints = Roster.Map(
        (BindingKind.ModuleVar, AxisMembership.Everywhere),
        (BindingKind.ModuleLet, AxisMembership.Out(ExcludedAxis.RefusedByKind, "SPY0340 at its own declaration")),
        (BindingKind.ModuleLiteralConst, AxisMembership.Out(ExcludedAxis.NotAKind, LiteralReadsNoName)),
        (BindingKind.ModuleComputedConst, AxisMembership.Everywhere),
        (BindingKind.ModuleProperty, AxisMembership.Everywhere),
        (BindingKind.ModuleDef, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializer)),
        (BindingKind.ModuleClass, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializer)),
        (BindingKind.ModuleStruct, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializer)),
        (BindingKind.ModuleEnum, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializer)),
        (BindingKind.ModuleUnion, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializer)),
        (BindingKind.ModuleInterface, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializer)),
        (BindingKind.ModuleTypeAlias, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializer)),
        (BindingKind.StaticField, AxisMembership.Everywhere),
        (BindingKind.StaticProperty, AxisMembership.Everywhere),
        (BindingKind.StaticMethod, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializer)),
        (BindingKind.ClassLiteralConst, AxisMembership.Out(ExcludedAxis.NotAKind, LiteralReadsNoName)),
        (BindingKind.ClassComputedConst, AxisMembership.Everywhere),
        (BindingKind.NestedType, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializer)),
        (BindingKind.InstanceField, AxisMembership.Out(ExcludedAxis.NotAKind,
            "construction-time, not load order: an instance initializer runs at each construction, after every load-time initializer")),
        (BindingKind.InstanceProperty, AxisMembership.Out(ExcludedAxis.NotAKind,
            "construction-time, not load order: an instance initializer runs at each construction, after every load-time initializer")),
        (BindingKind.InstanceMethod, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializer)),
        (BindingKind.Constructor, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializer)));

    /// <summary>The universe <see cref="CycleEndpoints"/> is total over.</summary>
    internal static IEnumerable<BindingKind> CycleEndpointUniverse() => ModuleLevelKinds.Concat(TypeLevelKinds).Concat(InstanceKinds);

    private const string NoSelfAtLoad = "no `self` at a load-time initializer: a module or @static endpoint is read before any instance exists";

    private const string NoCallableEndpoint =
        "a value endpoint has no callable to call: the read through a helper def is CycleThrough.ThroughDef, on every spelling";

    private const string NoBareTypeMember =
        "a @static member is not visible bare outside its class body: the read is N1-methods' refusal, never a cycle";

    private static RosterEntry<CycleThrough> BothThroughs() => Roster.To(CycleThrough.Direct, CycleThrough.ThroughDef);

    /// <summary>The cycle's routes (P77b axis 2): total over <see cref="NameResolutionDifferentialTests.Spelling"/> × <see cref="CyclePair"/>.
    /// The spelling is the read of a TYPE endpoint (a module endpoint is read bare); every (spelling, pair) is generated over
    /// <see cref="CycleThrough"/> or out with its reason.</summary>
    internal static readonly IReadOnlyDictionary<NameResolutionDifferentialTests.Spelling, IReadOnlyDictionary<CyclePair, RosterEntry<CycleThrough>>> CycleRoutes = Roster.Map(
        (NameResolutionDifferentialTests.Spelling.Bare, Roster.Map(
            (CyclePair.ModuleModule, BothThroughs()),
            (CyclePair.ModuleType, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, NoBareTypeMember)),
            (CyclePair.TypeModule, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, NoBareTypeMember)),
            (CyclePair.TypeType, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, NoBareTypeMember)))),
        (NameResolutionDifferentialTests.Spelling.Qualified, Roster.Map(
            (CyclePair.ModuleModule, Roster.Out<CycleThrough>(ExcludedAxis.ProjectRoute,
                "a module binding has no qualifier in a single file: the module-qualified spelling needs a project route (i)")),
            (CyclePair.ModuleType, BothThroughs()),
            (CyclePair.TypeModule, BothThroughs()),
            (CyclePair.TypeType, BothThroughs()))),
        (NameResolutionDifferentialTests.Spelling.Alias, Roster.Map(
            (CyclePair.ModuleModule, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, "a type alias names a type, never a module binding")),
            (CyclePair.ModuleType, BothThroughs()),
            (CyclePair.TypeModule, BothThroughs()),
            (CyclePair.TypeType, BothThroughs()))),
        (NameResolutionDifferentialTests.Spelling.Call, Roster.Map(
            (CyclePair.ModuleModule, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, NoCallableEndpoint)),
            (CyclePair.ModuleType, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, NoCallableEndpoint)),
            (CyclePair.TypeModule, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, NoCallableEndpoint)),
            (CyclePair.TypeType, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, NoCallableEndpoint)))),
        (NameResolutionDifferentialTests.Spelling.SelfDot, Roster.Map(
            (CyclePair.ModuleModule, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, NoSelfAtLoad)),
            (CyclePair.ModuleType, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, NoSelfAtLoad)),
            (CyclePair.TypeModule, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, NoSelfAtLoad)),
            (CyclePair.TypeType, Roster.Out<CycleThrough>(ExcludedAxis.NotAKind, NoSelfAtLoad)))));

    /// <summary>The endpoint kinds a pair joins, from <see cref="CycleEndpoints"/>' generated values: each level's kinds paired
    /// by index with a rotation, so every endpoint kind sits on every pair it can and the product stays linear.</summary>
    internal static IEnumerable<(BindingKind First, BindingKind Second)> CyclePairKinds(CyclePair pair)
    {
        var generated = CycleEndpoints.Where(e => e.Value.Generated).Select(e => e.Key).OrderBy(k => k).ToList();
        var module = generated.Where(k => ModuleLevelKinds.Contains(k)).ToList();
        var type = generated.Where(k => TypeLevelKinds.Contains(k)).ToList();
        var (left, right, rotate) = pair switch
        {
            CyclePair.ModuleModule => (module, module, 1),
            CyclePair.ModuleType => (module, type, 0),
            CyclePair.TypeModule => (type, module, 0),
            CyclePair.TypeType => (type, type, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(pair)),
        };
        if (left.Count == 0 || right.Count == 0)
            yield break;
        for (int i = 0; i < Math.Max(left.Count, right.Count); i++)
            yield return (left[i % left.Count], right[(i + rotate) % right.Count]);
    }

    /// <summary>Every cycle cell the axis owes: each generated (spelling, pair, through) × the pair's endpoint kinds.</summary>
    internal static IEnumerable<CycleRoute> CycleRouteCells() =>
        from spelling in CycleRoutes
        from pair in spelling.Value
        from through in pair.Value.Image
        from kinds in CyclePairKinds(pair.Key)
        select new CycleRoute(pair.Key, spelling.Key, through, kinds.First, kinds.Second);

    // ---------------------------------------------------------------------------------------------------------
    // P77b axis 3: observation {value, order}
    // ---------------------------------------------------------------------------------------------------------

    private const string NoInitializerToObserve = "no initializer to observe: the declaration runs no expression the helper could sit in";

    private const string LocalOrderIsTheBody = "a local's initializer runs where its statement sits, in the body's own order: no load or construction order to observe";

    /// <summary>The order observation's kinds (P77b axis 3): total over <see cref="BindingKind"/>. A kind whose declaration has an
    /// initializer that runs at load (module, <c>@static</c>) or at construction (instance) and can call a helper is generated;
    /// every other kind is out with the reason.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, AxisMembership> OrderObservationKinds = Roster.Map(
        (BindingKind.ModuleVar, AxisMembership.Everywhere),
        (BindingKind.ModuleLet, AxisMembership.Out(ExcludedAxis.RefusedByKind, "SPY0340 at its own declaration")),
        (BindingKind.ModuleLiteralConst, AxisMembership.Out(ExcludedAxis.NotAKind, "a literal initializer calls no helper")),
        (BindingKind.ModuleComputedConst, AxisMembership.Everywhere),
        (BindingKind.ModuleProperty, AxisMembership.Everywhere),
        (BindingKind.ModuleDef, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.ModuleClass, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.ModuleStruct, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.ModuleEnum, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.ModuleUnion, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.ModuleInterface, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.ModuleTypeAlias, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.StaticField, AxisMembership.Everywhere),
        (BindingKind.StaticProperty, AxisMembership.Everywhere),
        (BindingKind.StaticMethod, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.ClassLiteralConst, AxisMembership.Out(ExcludedAxis.NotAKind, "a literal initializer calls no helper")),
        (BindingKind.ClassComputedConst, AxisMembership.Everywhere),
        (BindingKind.NestedType, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.InstanceField, AxisMembership.Everywhere),
        (BindingKind.InstanceProperty, AxisMembership.Everywhere),
        (BindingKind.InstanceMethod, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.Constructor, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.EnclosingLocal, AxisMembership.Out(ExcludedAxis.NotAKind, LocalOrderIsTheBody)),
        (BindingKind.Parameter, AxisMembership.Out(ExcludedAxis.NotAKind, "bound by the call's argument, not an initializer of the program's own")),
        (BindingKind.Inner, AxisMembership.Out(ExcludedAxis.NotAKind, "bound by its expression's iteration or call, not an initializer")),
        (BindingKind.LocalVar, AxisMembership.Out(ExcludedAxis.NotAKind, LocalOrderIsTheBody)),
        (BindingKind.LocalLet, AxisMembership.Out(ExcludedAxis.NotAKind, LocalOrderIsTheBody)),
        (BindingKind.LocalLiteralConst, AxisMembership.Out(ExcludedAxis.NotAKind, "a literal initializer calls no helper")),
        (BindingKind.LocalComputedConst, AxisMembership.Out(ExcludedAxis.NotAKind, LocalOrderIsTheBody)),
        (BindingKind.LocalDef, AxisMembership.Out(ExcludedAxis.NotAKind, NoInitializerToObserve)),
        (BindingKind.LambdaTypedLocal, AxisMembership.Out(ExcludedAxis.NotAKind, LocalOrderIsTheBody)),
        (BindingKind.Builtin, AxisMembership.Out(ExcludedAxis.NotAKind, "bound outside the program: no initializer in it")),
        (BindingKind.ImportedName, AxisMembership.Out(ExcludedAxis.NotAKind, "bound by the imported module: no initializer in this program")),
        (BindingKind.None, AxisMembership.Out(ExcludedAxis.NotAKind, "no binding")));

    // ---------------------------------------------------------------------------------------------------------
    // P77b axis 4: LOCAL read timing
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>The read timings (P77b axis 4): a closed LITERAL set named closed — control-flow timing has no compiler
    /// enumeration — each value with the roster of the constructs it crosses. The fed check requires a cell per value.</summary>
    internal static readonly IReadOnlyDictionary<ReadTiming, string> ReadTimings = Roster.Map(
        (ReadTiming.SameBlockBelow, "LocalCells' below arm: every LocalKinds binder × outer × body"),
        (ReadTiming.SameBlockAbove, "LocalCells' above arm: every LocalKinds binder × outer × body"),
        (ReadTiming.ClosureBeforeConstruct, "a closure taken before the construct, over every LocalTarget (the binder census's LOCAL-target class)"),
        (ReadTiming.BackEdge, "a read at the next iteration, over roster 8's loop-body families (BackEdgeLoopFamilies)"),
        (ReadTiming.FailedGuard, "a capture under a failed guard, roster 7's GuardPattern over the pattern captures (FailedGuardCaptures)"));

    /// <summary>The loop families a back-edge read crosses: roster 8's Block families whose body is a loop's.</summary>
    internal static readonly ImmutableArray<string> BackEdgeLoopFamilies = ImmutableArray.Create("for-body", "while-body");

    /// <summary>The construct targets a failed guard binds: the LOCAL targets the binder census's pattern-checker sites bind.</summary>
    internal static readonly ImmutableArray<LocalTarget> FailedGuardCaptures = ImmutableArray.Create(LocalTarget.MatchCapture, LocalTarget.AsPatternCapture);

    // ---------------------------------------------------------------------------------------------------------
    // P77b axis 5: let-in binder × outer
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>The let-in product's outer axis (P77b axis 5): the image of <see cref="StoreTargetScopes"/>, each scope a store can
    /// target mapped to the binding of that scope a block's binder shadows. The binder's own block is out, and the no-binding
    /// outer (<see cref="BindingKind.None"/>) is <see cref="LetInNoOuter"/>. A callable binder reads its outer through the call
    /// form, so its module outer is spelled as a <c>def</c> (<see cref="BindingKind.ModuleDef"/>) and its function-scope outers
    /// as a <c>def</c> of the same kind: the read after the block is never a type error, whichever binding it reaches.</summary>
    internal static readonly IReadOnlyDictionary<StoreTargetScope, RosterEntry<BindingKind>> LetInOuters = Roster.Map(
        (StoreTargetScope.SameBlock, Roster.Out<BindingKind>(ExcludedAxis.NotAKind,
            "the binder's own block: a binding there is a same-block re-declaration (#1974's same-scope question), not an outer the block's binder shadows")),
        (StoreTargetScope.EnclosingBlock, Roster.To(BindingKind.LocalVar)),
        (StoreTargetScope.EnclosingFunction, Roster.To(BindingKind.EnclosingLocal)),
        (StoreTargetScope.Module, Roster.To(BindingKind.ModuleVar)));

    /// <summary>The let-in product's no-binding outer (P77b axis 5): the binder is the only binding of its name.</summary>
    internal const BindingKind LetInNoOuter = BindingKind.None;

    /// <summary>The outers the let-in product crosses with EVERY block family (P77b axis 5): no binding and the module scope. The
    /// function-scope outers ride the <see cref="StoreNestingFamily"/> family alone (<see cref="LetInOneFamilyReason"/>).</summary>
    internal static readonly ImmutableArray<BindingKind> LetInEveryFamilyOuters = ImmutableArray.Create(BindingKind.None, BindingKind.ModuleVar);

    /// <summary>Why the function-scope outers ride one block family (P77b axis 5): the plan's named prune.</summary>
    internal const string LetInOneFamilyReason = "one block family stands for the walk — " + StoreNestingReason;

    // ---------------------------------------------------------------------------------------------------------
    // P77b axis 6: closure over a loop-BODY binding (R-LG)
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>The binders axis 6 closes over (P77b axis 6, deferred finding 4): total over <see cref="LocalKinds"/>' generated
    /// binders, each a statement in the loop body binding <c>y</c> from the loop counter, or out with its reason.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, AxisMembership> LoopBodyClosureBinders = Roster.Map(
        (BindingKind.LocalVar, AxisMembership.Everywhere),
        (BindingKind.LocalLet, AxisMembership.Everywhere),
        // `const y: int = i`: a computed const binds a value per iteration, python's twin `y: int = i`.
        (BindingKind.LocalComputedConst, AxisMembership.Everywhere),
        (BindingKind.LocalLiteralConst, AxisMembership.Out(ExcludedAxis.NotAKind,
            "a literal const binds the same value at every iteration, so a fresh and a shared binding print alike: no cell can observe the axis (the counter is not a literal, so `const y: int = i` is LocalComputedConst's row)")),
        (BindingKind.LocalDef, AxisMembership.Everywhere),
        (BindingKind.LambdaTypedLocal, AxisMembership.Everywhere));

    /// <summary>The enclosure of axis 6's binder inside the loop body that is no block of its own: the loop body itself.</summary>
    internal const string LoopBodyDirectEnclosure = "direct";

    /// <summary>The block families axis 6's binder may sit in inside the loop body (P77b axis 6): roster 8's statement-bodied
    /// families (the let-in ones), the two
    /// R-LG names generated, the loop families the loop axis itself, the others the plan's named prune.</summary>
    internal static readonly IReadOnlyDictionary<string, AxisMembership> LoopBodyEnclosures = Roster.Map(
        ("if-then", AxisMembership.Pruned(LoopBodyEnclosureReason)),
        ("elif", AxisMembership.Pruned(LoopBodyEnclosureReason)),
        ("if-else", AxisMembership.Pruned(LoopBodyEnclosureReason)),
        ("while-body", AxisMembership.Out(ExcludedAxis.NotAKind, LoopBodyLoopFamilyReason)),
        ("while-else", AxisMembership.Pruned(LoopBodyEnclosureReason)),
        ("for-body", AxisMembership.Out(ExcludedAxis.NotAKind, LoopBodyLoopFamilyReason)),
        ("for-else", AxisMembership.Pruned(LoopBodyEnclosureReason)),
        ("try", AxisMembership.Pruned(LoopBodyEnclosureReason)),
        ("except", AxisMembership.Everywhere),
        ("try-else", AxisMembership.Pruned(LoopBodyEnclosureReason)),
        ("finally", AxisMembership.Pruned(LoopBodyEnclosureReason)),
        ("with", AxisMembership.Everywhere),
        ("match-case", AxisMembership.Pruned(LoopBodyEnclosureReason)));

    internal const string LoopBodyEnclosureReason = "same walk; R-LG names with/except";

    // ---------------------------------------------------------------------------------------------------------
    // P77b axis 7: the host (R-KT)
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>The READ family's host axis (P77b axis 7): the image of <see cref="TypeKind"/>. The class host (with its
    /// <c>@dataclass</c> spelling at the dataclass initializer positions) is every class cell's; the struct host rides every
    /// <c>ClassBodies</c> position; the other kinds are out with their reasons.</summary>
    internal static readonly IReadOnlyDictionary<TypeKind, AxisMembership> HostKinds = Roster.Map(
        (TypeKind.Class, AxisMembership.Everywhere),
        (TypeKind.Struct, AxisMembership.Everywhere),
        (TypeKind.Interface, AxisMembership.Out(ExcludedAxis.InterfaceUnionEnumGenericHosts, "P78's interface host matrix")),
        (TypeKind.Enum, AxisMembership.Out(ExcludedAxis.InterfaceUnionEnumGenericHosts, "P78's enum host matrix")),
        (TypeKind.Union, AxisMembership.Out(ExcludedAxis.InterfaceUnionEnumGenericHosts, "P78's union host matrix")),
        (TypeKind.Delegate, AxisMembership.Out(ExcludedAxis.TypeParametersAndDelegates, "no Sharpy declaration hosts a body")));

    /// <summary>The targets the struct host reads (P77b axis 7): the instance and <c>@static</c> field and method.</summary>
    internal static readonly ImmutableArray<BindingKind> StructHostTargets = ImmutableArray.Create(
        BindingKind.InstanceField, BindingKind.StaticField, BindingKind.InstanceMethod, BindingKind.StaticMethod);

    /// <summary>The body positions whose statement axis 7 also reads through a stored lambda (#2434's <c>st2_init_lambda</c> /
    /// <c>st2_accessor_get_lambda</c>).</summary>
    internal static readonly ImmutableArray<ReadPosition> StructLambdaNestings = ImmutableArray.Create(ReadPosition.InitBody, ReadPosition.AccessorGetBody);

    // ---------------------------------------------------------------------------------------------------------
    // P77b axis 8: the attribute STORE target (R-KU, R-LH)
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>The method whose <c>case</c> arms are the compiler's one authority for an assignment target (P77b axis 8):
    /// <c>IsValidAssignmentTarget</c> is defined as its null, and <c>CheckAssignment</c> routes every store through it.</summary>
    internal const string AssignmentTargetAuthority = "FirstInvalidAssignmentTarget";

    /// <summary>The assignment target shapes (P77b axis 8): one row per <c>case</c> arm of
    /// <see cref="AssignmentTargetAuthority"/>'s switch, read by a Roslyn scan; the <c>default:</c> arm is SPY0225 for every
    /// other shape, so the scanned arms are the universe.</summary>
    internal static readonly IReadOnlyDictionary<string, AxisMembership> AssignmentTargetShapes = Roster.Map(
        ("Identifier", AxisMembership.Everywhere),
        ("MemberAccess", AxisMembership.Everywhere),
        ("IndexAccess", AxisMembership.Out(ExcludedAxis.NotAKind, "the receiver is a read and the element is a value, no name is stored")),
        ("TupleLiteral", AxisMembership.Out(ExcludedAxis.EventsSplitPropertiesTupleStarStores, "a tuple store (iii): #2154 / #2156 / P21d")),
        ("StarExpression", AxisMembership.Out(ExcludedAxis.EventsSplitPropertiesTupleStarStores, "a starred store (iii): #2154 / #2156 / P21d")));

    /// <summary>The members an attribute store writes (P77b axis 8): roster 1's type-level and instance kinds.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, AxisMembership> AttributeStoreMembers = Roster.Map(
        (BindingKind.StaticField, AxisMembership.Everywhere),
        (BindingKind.StaticProperty, AxisMembership.Everywhere),
        (BindingKind.StaticMethod, AxisMembership.Everywhere),
        (BindingKind.ClassLiteralConst, AxisMembership.Everywhere),
        (BindingKind.ClassComputedConst, AxisMembership.Everywhere),
        (BindingKind.NestedType, AxisMembership.Out(ExcludedAxis.NotAKind,
            "a nested type is a type: `H.I = ...` stores no value of the member's own type, so no store is type-compatible with it")),
        (BindingKind.InstanceField, AxisMembership.Everywhere),
        (BindingKind.InstanceProperty, AxisMembership.Everywhere),
        (BindingKind.InstanceMethod, AxisMembership.Everywhere),
        (BindingKind.Constructor, AxisMembership.Out(ExcludedAxis.NotAKind,
            "the constructor has no member name a store spells (it is `__init__`, called through the type), so no attribute store writes it")));

    /// <summary>The store forms an attribute target takes (P77b axis 8). The name-binding forms are refused by the parser on
    /// a member access (probes @ bc7d3ab: walrus SPY0143, <c>let</c> SPY0107, <c>out let</c> SPY0104; python's walrus is
    /// "cannot use assignment expressions with attribute").</summary>
    internal static readonly IReadOnlyDictionary<StoreForm, AxisMembership> AttributeStoreForms = Roster.Map(
        (StoreForm.Assign, AxisMembership.Everywhere),
        (StoreForm.Augmented, AxisMembership.Everywhere),
        (StoreForm.Walrus, AxisMembership.Out(ExcludedAxis.NotAKind, "the parser binds a name, never a member (SPY0143)")),
        (StoreForm.OutLet, AxisMembership.Out(ExcludedAxis.NotAKind, "the parser binds a name, never a member (SPY0104)")),
        (StoreForm.LetOrVar, AxisMembership.Out(ExcludedAxis.NotAKind, "the parser binds a name, never a member (SPY0107)")));

    /// <summary>The members axis 8 crosses with both forms and the four bodies (the plan's 16 + 8 product).</summary>
    internal static readonly ImmutableArray<BindingKind> AttributeStoreFormMembers = ImmutableArray.Create(BindingKind.InstanceField, BindingKind.StaticField);

    /// <summary>The struct-host targets whose <c>self.</c> read lowers through <c>this</c> (P77b axis 7): the instance members. A
    /// <c>@static</c> member read through <c>self.</c> is the type's member (<c>H.x</c>), so a closure over it captures no
    /// <c>this</c>, C# refuses nothing (measured: <c>self.x</c> on a <c>@static</c> field in a struct lambda prints 2), and R-KT
    /// would be wider than CS1673: it takes the class arm.</summary>
    internal static readonly ImmutableArray<BindingKind> StructSelfCaptureTargets =
        StructHostTargets.Where(k => InstanceKinds.Contains(k)).ToImmutableArray();

    /// <summary>R-KT's closure set (P77b axis 7, #2434): the struct positions where <c>self.</c> is captured by a C# closure, a
    /// lambda or a local function, which C# refuses (CS1673). The comprehension and generator positions are lowered without a
    /// <c>this</c> capture and run: they take the class arm. Asserted equal to what <c>PlaceInClass</c> renders through a
    /// lambda or a nested <c>def</c>.</summary>
    internal static readonly ImmutableHashSet<(ReadPosition Position, NameResolutionDifferentialTests.Nesting Nesting)> StructSelfCaptureClosures =
        ImmutableHashSet.Create(
            (ReadPosition.LambdaStored, NameResolutionDifferentialTests.Nesting.Direct),
            (ReadPosition.LambdaImmediate, NameResolutionDifferentialTests.Nesting.Direct),
            (ReadPosition.LambdaPassed, NameResolutionDifferentialTests.Nesting.Direct),
            (ReadPosition.NestedDefInMethod, NameResolutionDifferentialTests.Nesting.Direct),
            (ReadPosition.InitBody, NameResolutionDifferentialTests.Nesting.Lambda),
            (ReadPosition.AccessorGetBody, NameResolutionDifferentialTests.Nesting.Lambda));

    internal const string LoopBodyLoopFamilyReason =
        "the loop axis itself (BackEdgeLoopFamilies): the binder directly in the loop body is the `direct` enclosure";

    /// <summary>The slots of a kind's own node, from <see cref="ComprehensionSlots"/>: the element side of the comprehension.</summary>
    internal static IEnumerable<ComprehensionSlot> KindSlots(ComprehensionKind kind)
    {
        // A kind with no row has no slots: RosterFedFailures names it, rather than a static initializer throwing.
        var node = ComprehensionKinds.FirstOrDefault(e => e.Value.Kind == kind).Key;
        if (node == null)
            return Enumerable.Empty<ComprehensionSlot>();
        return ComprehensionSlots.Where(e => e.Key.StartsWith(node + ".", StringComparison.Ordinal)).SelectMany(e => e.Value.Image);
    }

    /// <summary>The slots of a clause shape, from <see cref="ComprehensionClauses"/> and <see cref="ComprehensionSlots"/>.</summary>
    internal static IEnumerable<ComprehensionSlot> ClauseSlots(ComprehensionClauseShape shape)
    {
        var clause = ComprehensionClauses.FirstOrDefault(e => !e.Key.Contains('.') && e.Value.Image.Contains(shape)).Key;
        if (clause == null)
            return Enumerable.Empty<ComprehensionSlot>();
        return ComprehensionSlots.Where(e => e.Key.StartsWith(clause + ".", StringComparison.Ordinal)).SelectMany(e => e.Value.Image);
    }

    /// <summary>Every (kind, clause, slot) the axis owes a cell: a kind's own slots ride its first <c>for</c>; a clause's slots
    /// ride every kind.</summary>
    internal static IEnumerable<ComprehensionSite> ComprehensionSites() =>
        from kind in Enum.GetValues<ComprehensionKind>()
        from site in KindSlots(kind).Select(s => new ComprehensionSite(kind, ComprehensionClauseShape.FirstFor, s))
            .Concat(ComprehensionClauses.Where(e => !e.Key.Contains('.')).SelectMany(e => e.Value.Image)
                .SelectMany(shape => ClauseSlots(shape).Select(s => new ComprehensionSite(kind, shape, s))))
        select site;

    // ---------------------------------------------------------------------------------------------------------
    // Findings 1–4: the STORE / LOCAL axes, each total over its source
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>The image of roster 4 (finding 2): the positions the body axes of STORE and LOCAL are read from.</summary>
    internal static readonly ImmutableArray<ReadPosition> BodyPositions = ImmutableArray.Create(
        ReadPosition.FunctionBody, ReadPosition.NestedDefInFunction, ReadPosition.GeneratorBody,
        ReadPosition.MethodBody, ReadPosition.StaticMethodBody, ReadPosition.InitBody,
        ReadPosition.AccessorGetBody, ReadPosition.AccessorSetBody, ReadPosition.ModuleAccessorBody,
        ReadPosition.NestedClassMethodBody, ReadPosition.NestedDefInMethod,
        ReadPosition.LambdaStored, ReadPosition.LambdaImmediate, ReadPosition.LambdaPassed);

    /// <summary>The bodies a class encloses: the only ones where a type-level outer means anything.</summary>
    internal static readonly ImmutableArray<ReadPosition> ClassEnclosedBodies = ImmutableArray.Create(
        ReadPosition.MethodBody, ReadPosition.StaticMethodBody, ReadPosition.InitBody,
        ReadPosition.AccessorGetBody, ReadPosition.AccessorSetBody,
        ReadPosition.NestedClassMethodBody, ReadPosition.NestedDefInMethod);

    private static readonly StoreForm[] AllStoreForms =
        { StoreForm.Assign, StoreForm.Augmented, StoreForm.Walrus, StoreForm.OutLet, StoreForm.LetOrVar };

    private const string LambdaShapePruned = "the store binds in the same lambda scope however the lambda is used: LambdaStored stays";

    /// <summary>STORE's body axis (finding 2): which forms each roster-4 position hosts. A lambda's body is one expression, so
    /// it hosts the walrus alone; the plan's hand list named five of these fourteen.</summary>
    internal static readonly IReadOnlyDictionary<ReadPosition, RosterEntry<StoreForm>> StoreBodies = Roster.Map(
        (ReadPosition.FunctionBody, Roster.To(AllStoreForms)),
        (ReadPosition.NestedDefInFunction, Roster.To(AllStoreForms)),
        (ReadPosition.GeneratorBody, Roster.To(AllStoreForms)),
        (ReadPosition.MethodBody, Roster.To(AllStoreForms)),
        (ReadPosition.StaticMethodBody, Roster.To(AllStoreForms)),
        (ReadPosition.InitBody, Roster.To(AllStoreForms)),
        (ReadPosition.AccessorGetBody, Roster.To(AllStoreForms)),
        (ReadPosition.AccessorSetBody, Roster.To(AllStoreForms)),
        (ReadPosition.ModuleAccessorBody, Roster.To(AllStoreForms)),
        (ReadPosition.NestedClassMethodBody, Roster.To(AllStoreForms)),
        (ReadPosition.NestedDefInMethod, Roster.To(AllStoreForms)),
        (ReadPosition.LambdaStored, Roster.To(StoreForm.Walrus)),
        (ReadPosition.LambdaImmediate, Roster.Out<StoreForm>(ExcludedAxis.PrunedRepresentative, LambdaShapePruned)),
        (ReadPosition.LambdaPassed, Roster.Out<StoreForm>(ExcludedAxis.PrunedRepresentative, LambdaShapePruned)));

    private static readonly LocalTarget[] AllLocalTargets =
    {
        LocalTarget.For, LocalTarget.ExceptAs, LocalTarget.WithAs, LocalTarget.Comprehension,
        LocalTarget.MatchCapture, LocalTarget.AsPatternCapture, LocalTarget.OutLet,
    };

    /// <summary>LOCAL's body axis (finding 2): which construct targets each roster-4 position hosts. The statement-bearing
    /// positions, those with a <see cref="LocalTarget.For"/>, also host a <c>let</c> inside a construct body.</summary>
    internal static readonly IReadOnlyDictionary<ReadPosition, RosterEntry<LocalTarget>> LocalBodies = Roster.Map(
        (ReadPosition.FunctionBody, Roster.To(AllLocalTargets)),
        (ReadPosition.NestedDefInFunction, Roster.To(AllLocalTargets)),
        (ReadPosition.GeneratorBody, Roster.To(AllLocalTargets)),
        (ReadPosition.MethodBody, Roster.To(AllLocalTargets)),
        (ReadPosition.StaticMethodBody, Roster.To(AllLocalTargets)),
        (ReadPosition.InitBody, Roster.To(AllLocalTargets)),
        (ReadPosition.AccessorGetBody, Roster.To(AllLocalTargets)),
        (ReadPosition.AccessorSetBody, Roster.To(AllLocalTargets)),
        (ReadPosition.ModuleAccessorBody, Roster.To(AllLocalTargets)),
        (ReadPosition.NestedClassMethodBody, Roster.To(AllLocalTargets)),
        (ReadPosition.NestedDefInMethod, Roster.To(AllLocalTargets)),
        (ReadPosition.LambdaStored, Roster.To(LocalTarget.Comprehension)),
        (ReadPosition.LambdaImmediate, Roster.Out<LocalTarget>(ExcludedAxis.PrunedRepresentative, LambdaShapePruned)),
        (ReadPosition.LambdaPassed, Roster.Out<LocalTarget>(ExcludedAxis.PrunedRepresentative, LambdaShapePruned)));

    private const string SameFunctionIsTargetScope = "a binding of the same function is the target-scope axis, not an outer: EnclosingLocal keeps the arm that crosses a boundary";

    private const string NoSelfOrBareSpelling = "TYPEMEMBER's member axis only: never a bare-name binding";

    /// <summary>STORE's outer axis (finding 1): module-level ∪ type-level and instance kinds ∪ {EnclosingLocal, Builtin, None},
    /// replacing the plan's <c>ClassMember</c>, which is no <see cref="BindingKind"/>.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, AxisMembership> StoreOuter = Roster.Map(
        (BindingKind.ModuleVar, AxisMembership.Everywhere),
        (BindingKind.ModuleLet, AxisMembership.Pruned("refused at its own declaration (SPY0340), so the program never reaches the store: as READ's outer ModuleLet")),
        (BindingKind.ModuleLiteralConst, AxisMembership.Everywhere),
        (BindingKind.ModuleComputedConst, AxisMembership.Everywhere),
        (BindingKind.ModuleProperty, AxisMembership.Everywhere),
        (BindingKind.ModuleDef, AxisMembership.Everywhere),
        (BindingKind.ModuleClass, AxisMembership.Everywhere),
        (BindingKind.ModuleStruct, AxisMembership.Everywhere),
        (BindingKind.ModuleEnum, AxisMembership.Everywhere),
        (BindingKind.ModuleUnion, AxisMembership.Everywhere),
        (BindingKind.ModuleInterface, AxisMembership.Everywhere),
        (BindingKind.ModuleTypeAlias, AxisMembership.Everywhere),
        (BindingKind.StaticField, AxisMembership.ClassEnclosed),
        (BindingKind.StaticProperty, AxisMembership.ClassEnclosed),
        (BindingKind.StaticMethod, AxisMembership.ClassEnclosed),
        (BindingKind.ClassLiteralConst, AxisMembership.ClassEnclosed),
        (BindingKind.ClassComputedConst, AxisMembership.ClassEnclosed),
        (BindingKind.InstanceField, AxisMembership.ClassEnclosed),
        (BindingKind.InstanceProperty, AxisMembership.ClassEnclosed),
        (BindingKind.InstanceMethod, AxisMembership.ClassEnclosed),
        (BindingKind.EnclosingLocal, AxisMembership.Everywhere),
        (BindingKind.Builtin, AxisMembership.Everywhere),
        (BindingKind.ImportedName, AxisMembership.Everywhere),
        (BindingKind.None, AxisMembership.Everywhere),
        (BindingKind.NestedType, AxisMembership.Pruned("a type's bare name binds through the walk (Scope.ResolveName binds types); non-writable by meaning, ModuleClass keeps that arm")),
        (BindingKind.Constructor, AxisMembership.Out(ExcludedAxis.NotAKind, NoSelfOrBareSpelling)),
        (BindingKind.Parameter, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.Inner, AxisMembership.Pruned("a lambda parameter or comprehension variable: no statement store sits inside one, and a walrus there is the form's own position")),
        (BindingKind.LocalVar, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.LocalLet, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.LocalLiteralConst, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.LocalComputedConst, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.LocalDef, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.LambdaTypedLocal, AxisMembership.Pruned(SameFunctionIsTargetScope)));

    private const string AsModuleVar = "a value binding of the module: the read above N5's binding shares ModuleVar's arm (N4), and ModuleVar stays";

    private const string AsModuleClass = "shares the F2 arm with ModuleClass, the one type kind that stays (D3.e's pruning order)";

    private const string OneClassAnswer = "R-Y gives one answer for every class member at a method body: StaticField (value) and StaticMethod (callable, S3) stay";

    /// <summary>LOCAL's outer axis (finding 3): the whole roster, with D3.e's pruning order naming every value it leaves out,
    /// and <see cref="ExcludedAxis.PrunedRepresentative"/> on each.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, AxisMembership> LocalOuter = Roster.Map(
        (BindingKind.None, AxisMembership.Everywhere),
        (BindingKind.ModuleVar, AxisMembership.Everywhere),
        (BindingKind.ModuleDef, AxisMembership.Everywhere),
        (BindingKind.ModuleClass, AxisMembership.Everywhere),
        (BindingKind.ModuleProperty, AxisMembership.Everywhere),
        (BindingKind.ModuleLiteralConst, AxisMembership.Everywhere),
        (BindingKind.EnclosingLocal, AxisMembership.Everywhere),
        (BindingKind.Builtin, AxisMembership.Everywhere),
        (BindingKind.ImportedName, AxisMembership.Everywhere),
        (BindingKind.StaticField, AxisMembership.ClassEnclosed),
        (BindingKind.StaticMethod, AxisMembership.ClassEnclosed),
        (BindingKind.ModuleLet, AxisMembership.Pruned(AsModuleVar)),
        (BindingKind.ModuleComputedConst, AxisMembership.Pruned(AsModuleVar)),
        (BindingKind.ModuleStruct, AxisMembership.Pruned(AsModuleClass)),
        (BindingKind.ModuleEnum, AxisMembership.Pruned(AsModuleClass)),
        (BindingKind.ModuleUnion, AxisMembership.Pruned(AsModuleClass)),
        (BindingKind.ModuleInterface, AxisMembership.Pruned(AsModuleClass)),
        (BindingKind.ModuleTypeAlias, AxisMembership.Pruned(AsModuleClass)),
        (BindingKind.StaticProperty, AxisMembership.Pruned(OneClassAnswer)),
        (BindingKind.ClassLiteralConst, AxisMembership.Pruned(OneClassAnswer)),
        (BindingKind.ClassComputedConst, AxisMembership.Pruned(OneClassAnswer)),
        (BindingKind.NestedType, AxisMembership.Pruned(OneClassAnswer)),
        (BindingKind.InstanceField, AxisMembership.Pruned(OneClassAnswer)),
        (BindingKind.InstanceProperty, AxisMembership.Pruned(OneClassAnswer)),
        (BindingKind.InstanceMethod, AxisMembership.Pruned(OneClassAnswer)),
        (BindingKind.Constructor, AxisMembership.Out(ExcludedAxis.NotAKind, NoSelfOrBareSpelling)),
        (BindingKind.Parameter, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.Inner, AxisMembership.Pruned("binds only inside its lambda or comprehension: nothing of LOCAL's sits there but the same comprehension target")),
        (BindingKind.LocalVar, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.LocalLet, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.LocalLiteralConst, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.LocalComputedConst, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.LocalDef, AxisMembership.Pruned(SameFunctionIsTargetScope)),
        (BindingKind.LambdaTypedLocal, AxisMembership.Pruned(SameFunctionIsTargetScope)));

    // ---------------------------------------------------------------------------------------------------------
    // TYPEMEMBER's declaring kind × member (P77b D5)
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>TYPEMEMBER's declaring-kind axis: the image of <see cref="TypeKind"/> (a class host is spelled two ways,
    /// plain and <c>@dataclass</c>), the delegate out as <see cref="TypeKinds"/> has it.</summary>
    internal static readonly IReadOnlyDictionary<TypeKind, RosterEntry<string>> TypeMemberHosts = Roster.Map(
        (TypeKind.Class, Roster.To("class", "dataclass")),
        (TypeKind.Struct, Roster.To("struct")),
        (TypeKind.Interface, Roster.To("interface")),
        (TypeKind.Enum, Roster.To("enum")),
        (TypeKind.Union, Roster.To("union")),
        (TypeKind.Delegate, Roster.Out<string>(ExcludedAxis.TypeParametersAndDelegates,
            "no Sharpy declaration: () -> int is the annotation, whose binding is LambdaTypedLocal (x)")));

    /// <summary>TYPEMEMBER's member axis: the image of <see cref="TypeBodyMemberKind"/>, the event out as
    /// <see cref="MemberKinds"/> has it; <see cref="TypeMemberExtraMembers"/> adds the members no body-member kind names.</summary>
    internal static readonly IReadOnlyDictionary<TypeBodyMemberKind, RosterEntry<string>> TypeMemberMembers = Roster.Map(
        (TypeBodyMemberKind.Field, Roster.To("field")),
        (TypeBodyMemberKind.Property, Roster.To("property")),
        (TypeBodyMemberKind.Event, Roster.Out<string>(ExcludedAxis.EventsSplitPropertiesTupleStarStores, "events are #2400 (iii)")),
        (TypeBodyMemberKind.Method, Roster.To("method")));

    /// <summary>The members of the axis that are no <see cref="TypeBodyMemberKind"/>: the constructor, the static method (a
    /// <c>Method</c> by kind, read through the type name) and the enum / union case.</summary>
    internal static readonly ImmutableArray<string> TypeMemberExtraMembers = ImmutableArray.Create("constructor", "static-method", "case");

    internal static IEnumerable<(string Host, string Member)> TypeMemberUniverse()
        => from h in Roster.ImageOf(TypeMemberHosts)
           from m in Roster.ImageOf(TypeMemberMembers).Concat(TypeMemberExtraMembers)
           select (h, m);

    private const string NoCaseOutsideEnumOrUnion = "a case is an enum or union member: any other body refuses it at the parser (SPY0103)";

    private const string EnumBodyIsCases = "an enum body declares only its cases (enums.md: no custom members; the parser refuses SPY0101 / SPY0103)";

    private const string UnionBodyIsCasesAndMethods = "a union body declares its cases and methods only (the parser refuses SPY0104)";

    /// <summary>TYPEMEMBER's declaring kind × member pairs (P77b D5): total over <see cref="TypeMemberUniverse"/>, each pair
    /// generated or out with the reason the kind cannot declare it. <c>MemberShapes()</c> yields only generated pairs and
    /// <c>RosterFedFailures</c> requires a cell on each, so a pair dropped from the generator is red without the census.</summary>
    internal static readonly IReadOnlyDictionary<(string Host, string Member), AxisMembership> TypeMemberShapes = Roster.Map(
        (("class", "field"), AxisMembership.Everywhere),
        (("class", "property"), AxisMembership.Everywhere),
        (("class", "method"), AxisMembership.Everywhere),
        (("class", "constructor"), AxisMembership.Everywhere),
        (("class", "static-method"), AxisMembership.Everywhere),
        (("class", "case"), AxisMembership.Out(ExcludedAxis.NotAKind, NoCaseOutsideEnumOrUnion)),
        (("dataclass", "field"), AxisMembership.Everywhere),
        (("dataclass", "property"), AxisMembership.Everywhere),
        (("dataclass", "method"), AxisMembership.Everywhere),
        (("dataclass", "constructor"), AxisMembership.Everywhere),
        (("dataclass", "static-method"), AxisMembership.Everywhere),
        (("dataclass", "case"), AxisMembership.Out(ExcludedAxis.NotAKind, NoCaseOutsideEnumOrUnion)),
        (("struct", "field"), AxisMembership.Everywhere),
        (("struct", "property"), AxisMembership.Everywhere),
        (("struct", "method"), AxisMembership.Everywhere),
        (("struct", "constructor"), AxisMembership.Everywhere),
        (("struct", "static-method"), AxisMembership.Everywhere),
        (("struct", "case"), AxisMembership.Out(ExcludedAxis.NotAKind, NoCaseOutsideEnumOrUnion)),
        // An interface's field is its const (interfaces.md "Constants"); a non-const field is a property, the property row.
        (("interface", "field"), AxisMembership.Everywhere),
        (("interface", "property"), AxisMembership.Everywhere),
        (("interface", "method"), AxisMembership.Everywhere),
        (("interface", "constructor"), AxisMembership.Out(ExcludedAxis.RefusedByKind, "an interface declares no __init__ (SPY0413)")),
        (("interface", "static-method"), AxisMembership.Out(ExcludedAxis.InterfaceUnionEnumGenericHosts,
            "an interface @static method is SPY0908 CS0120 at its declaration at BOTH orders: a declaration defect of the interface host (findings ledger), not a later-declared-member verdict")),
        (("interface", "case"), AxisMembership.Out(ExcludedAxis.NotAKind, NoCaseOutsideEnumOrUnion)),
        (("enum", "field"), AxisMembership.Out(ExcludedAxis.NotAKind, EnumBodyIsCases)),
        (("enum", "property"), AxisMembership.Out(ExcludedAxis.NotAKind, EnumBodyIsCases)),
        (("enum", "method"), AxisMembership.Out(ExcludedAxis.NotAKind, EnumBodyIsCases)),
        (("enum", "constructor"), AxisMembership.Out(ExcludedAxis.NotAKind, EnumBodyIsCases)),
        (("enum", "static-method"), AxisMembership.Out(ExcludedAxis.NotAKind, EnumBodyIsCases)),
        (("enum", "case"), AxisMembership.Everywhere),
        (("union", "field"), AxisMembership.Out(ExcludedAxis.NotAKind, UnionBodyIsCasesAndMethods)),
        (("union", "property"), AxisMembership.Out(ExcludedAxis.NotAKind, UnionBodyIsCasesAndMethods)),
        (("union", "method"), AxisMembership.Everywhere),
        // A union's constructor IS its case: `Later.V(3)`, generated by TYPEMEMBER's union arm.
        (("union", "constructor"), AxisMembership.Everywhere),
        (("union", "static-method"), AxisMembership.Out(ExcludedAxis.NotAKind, UnionBodyIsCasesAndMethods)),
        (("union", "case"), AxisMembership.Out(ExcludedAxis.NotAKind,
            "a union case is read through its constructor (`Later.V(3)`): the constructor row is the case")));

    /// <summary>The universe of READ's bare outer axis: the module-level group, the two named outers (the builtin and the
    /// imported name, P77b axis 9) and no binding (P77b D1). The totality fact requires a row for each.</summary>
    internal static readonly ImmutableArray<BindingKind> ReadOuterUniverse =
        ImmutableArray.Create(BindingKind.None).AddRange(ModuleLevelKinds).Add(BindingKind.Builtin).Add(BindingKind.ImportedName);

    /// <summary>READ's bare outer axis (P77b D1, R-KP): replaces P77a's hand array <c>ModuleOuters</c> and its budget
    /// prunes. Every value of <see cref="ReadOuterUniverse"/> is generated or out with its reason; the generator
    /// (<c>ReadClassCells</c>' bare outers) and <c>RosterFedFailures</c> read it, and the report lists the exclusions
    /// under <c>rosters.excluded.readOuter</c>. The Builtin and ImportedName rows are generated by their own name arms.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, AxisMembership> ReadOuter = Roster.Map(
        (BindingKind.None, AxisMembership.Everywhere),
        (BindingKind.ModuleVar, AxisMembership.Everywhere),
        (BindingKind.ModuleLet, AxisMembership.Out(ExcludedAxis.RefusedByKind, "SPY0340 at its own declaration")),
        (BindingKind.ModuleLiteralConst, AxisMembership.Everywhere),
        (BindingKind.ModuleComputedConst, AxisMembership.Everywhere),
        (BindingKind.ModuleProperty, AxisMembership.Everywhere),
        (BindingKind.ModuleDef, AxisMembership.Everywhere),
        (BindingKind.ModuleClass, AxisMembership.Everywhere),
        (BindingKind.ModuleStruct, AxisMembership.Everywhere),
        (BindingKind.ModuleEnum, AxisMembership.Everywhere),
        (BindingKind.ModuleUnion, AxisMembership.Out(ExcludedAxis.NotAKind, "no expression read")),
        (BindingKind.ModuleInterface, AxisMembership.Out(ExcludedAxis.NotAKind, "no expression read")),
        (BindingKind.ModuleTypeAlias, AxisMembership.Everywhere),
        (BindingKind.Builtin, AxisMembership.Everywhere),
        (BindingKind.ImportedName, AxisMembership.Everywhere));

    /// <summary>LOCAL's binder axis (P77b deferred finding 5): the image of <see cref="PositionLocalKinds"/>, the
    /// position-local kinds a declaration statement in the body binds. The three a statement cannot bind are out with
    /// their reasons, so the binder axis cannot be narrower than the roster by omission.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, AxisMembership> LocalKinds = Roster.Map(
        (BindingKind.EnclosingLocal, AxisMembership.Out(ExcludedAxis.NotAKind,
            "the enclosing function's binding: LOCAL's outer axis (LocalOuter), never a binder of the body the read sits in")),
        (BindingKind.Parameter, AxisMembership.Out(ExcludedAxis.NotAKind,
            "bound by the signature, not a statement of the body: there is no read above it, and READ's innermost arm reads it")),
        (BindingKind.Inner, AxisMembership.Out(ExcludedAxis.NotAKind,
            "a lambda parameter or comprehension variable: bound by its expression, whose target is LOCAL's Comprehension construct")),
        (BindingKind.LocalVar, AxisMembership.Everywhere),
        (BindingKind.LocalLet, AxisMembership.Everywhere),
        (BindingKind.LocalLiteralConst, AxisMembership.Everywhere),
        (BindingKind.LocalComputedConst, AxisMembership.Everywhere),
        (BindingKind.LocalDef, AxisMembership.Everywhere),
        (BindingKind.LambdaTypedLocal, AxisMembership.Everywhere));

    /// <summary>STORE's target scope (finding 4): the image of <see cref="ScopeKind"/>, the other three with their reasons.</summary>
    internal static readonly IReadOnlyDictionary<ScopeKind, RosterEntry<StoreTargetScope>> StoreTargetScopes = Roster.Map(
        (ScopeKind.Block, Roster.To(StoreTargetScope.SameBlock, StoreTargetScope.EnclosingBlock)),
        (ScopeKind.FunctionLike, Roster.To(StoreTargetScope.EnclosingFunction)),
        (ScopeKind.Module, Roster.To(StoreTargetScope.Module)),
        (ScopeKind.TypeBody, Roster.Out<StoreTargetScope>(ExcludedAxis.NotAKind,
            "a type body is the STORE outer axis's type-level kinds (finding 1): a bare store never targets it")),
        (ScopeKind.Transparent, Roster.Out<StoreTargetScope>(ExcludedAxis.TypeParametersAndDelegates, "declares no variable")),
        (ScopeKind.InitializerHost, Roster.Out<StoreTargetScope>(ExcludedAxis.NotAKind,
            "an initializer is an expression: only a walrus binds there, as the Inner kind, and it is not a function boundary")));

    /// <summary>The Block family <see cref="StoreNesting.InsideIf"/> stands for.</summary>
    internal const string StoreNestingFamily = "if-then";

    /// <summary>
    /// Why <see cref="StoreNesting"/> is {top, inside if} and not the image of roster 8. Verified by reading the route:
    /// <c>CheckAssignment</c> reads no scope name, <c>StatementStorePredecessor</c> calls
    /// <c>SymbolTable.Lookup(name, searchParents: true)</c>, and <c>Scope.ResolveName</c> branches only on the
    /// function-like and class-like predicates, so every Block family is walked alike.
    /// </summary>
    internal const string StoreNestingReason =
        "one representative of the Block families, since every Block family writes through identically at base";
}

public partial class NameResolutionDifferentialTests
{
    private const string SyntheticBinderFile = "SyntheticBinders.cs";

    private static readonly string[] BinderMethodNames = { "Define", "DefineAs", "TryDefine" };

    private static readonly string[] BinderReceiverTypes =
    {
        "Sharpy.Compiler.Semantic.SymbolTable",
        "Sharpy.Compiler.Semantic.Scope",
    };

    private sealed record BinderSite(string Key, int Line, string Receiver, string ReceiverType)
    {
        /// <summary>P77b D3.1: the Roslyn type of the argument bound to the binder's <c>Symbol</c> parameter (null for a
        /// method group).</summary>
        public string? BoundArgumentType { get; init; }

        /// <summary>P77b D3.1: the Roslyn type of the enclosing checker method's first parameter, the AST node it binds for.</summary>
        public string? CheckedNodeType { get; init; }
    }

    private sealed record BinderScan(
        IReadOnlyList<BinderSite> Sites,
        IReadOnlyList<string> Rejected,
        IReadOnlyList<string> Unresolved);

    private static readonly Lazy<BinderScan> CompilerBinders = new(ScanTheCompiler);

    private static string CompilerSourceDir()
        => Path.Combine(DispatchSiteScan.FindRepoRoot(), "src", "Sharpy.Compiler");

    private static IEnumerable<string> CompilerSourceFiles()
        => Directory.GetFiles(CompilerSourceDir(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .OrderBy(f => f, StringComparer.Ordinal);

    private static SyntaxTree ParseCSharp(string source, string path)
        => CSharpSyntaxTree.ParseText(
            source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), path: path);

    private static BinderScan ScanTheCompiler()
    {
        var csproj = Path.Combine(CompilerSourceDir(), "Sharpy.Compiler.csproj");
        var trees = CompilerSourceFiles().Select(f => ParseCSharp(File.ReadAllText(f), f)).ToList();
        trees.Add(ParseCSharp(DispatchSiteScan.BuildGlobalUsingsSource(csproj), "GlobalUsings.synthesized.cs"));
        var references = DispatchSiteScan.BuildReferences(
            DispatchSiteScan.ReadAliasMap(csproj), DispatchSiteScan.GetAssemblyName(csproj));
        return ScanBinders(trees, references);
    }

    /// <summary>
    /// The ONE classifier, which the compiler census and the synthetic control both run through. Candidates by spelling
    /// (<c>Define</c>, <c>DefineAs</c>, <c>TryDefine</c> in any position, so <c>?.Define(</c>, a bare <c>Define(</c> and a method
    /// group are all seen), then the positive filter: the bound method's containing type is <c>SymbolTable</c> or
    /// <c>Scope</c>. Anything else with the spelling is listed as rejected, and a call that binds to nothing is listed as
    /// unresolved rather than silently dropped.
    /// </summary>
    private static BinderScan ScanBinders(IReadOnlyList<SyntaxTree> trees, IEnumerable<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            "BinderSiteScan",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var sites = new List<BinderSite>();
        var rejected = new List<string>();
        var unresolved = new List<string>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var tree in trees)
        {
            var file = Path.GetFileName(tree.FilePath);
            if (file == "GlobalUsings.synthesized.cs")
                continue;

            var candidates = tree.GetCompilationUnitRoot().DescendantNodes()
                .OfType<SimpleNameSyntax>()
                .Where(n => BinderMethodNames.Contains(n.Identifier.ValueText))
                .ToList();
            if (candidates.Count == 0)
                continue;

            var model = compilation.GetSemanticModel(tree);
            foreach (var name in candidates)
            {
                var line = name.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var info = model.GetSymbolInfo(name);
                var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                var invocation = InvocationOf(name);

                if (symbol == null)
                {
                    if (invocation != null)
                        unresolved.Add($"{file}:{line}: {invocation}");
                    continue;
                }

                if (symbol is not IMethodSymbol method)
                {
                    rejected.Add($"{file}:{line}: {name} is a {symbol.Kind}");
                    continue;
                }

                var receiverType = method.ContainingType.ToDisplayString();
                if (!BinderReceiverTypes.Contains(receiverType))
                {
                    rejected.Add($"{file}:{line}: {receiverType}.{name}");
                    continue;
                }

                var baseKey = $"{file}:{EnclosingMethod(name)}({ArgumentText(invocation)})";
                var ordinal = seen[baseKey] = seen.GetValueOrDefault(baseKey) + 1;
                sites.Add(new BinderSite(
                    ordinal == 1 ? baseKey : $"{baseKey}#{ordinal}", line, ReceiverText(name), receiverType)
                {
                    BoundArgumentType = BoundArgumentType(model, method, invocation),
                    CheckedNodeType = CheckedNodeType(model, name),
                });
            }
        }

        return new BinderScan(sites, rejected, unresolved);
    }

    private static InvocationExpressionSyntax? InvocationOf(SimpleNameSyntax name) => name.Parent switch
    {
        InvocationExpressionSyntax direct when direct.Expression == name => direct,
        MemberAccessExpressionSyntax member when member.Name == name
            && member.Parent is InvocationExpressionSyntax outer && outer.Expression == member => outer,
        MemberBindingExpressionSyntax binding when binding.Name == name
            && binding.Parent is InvocationExpressionSyntax bound && bound.Expression == binding => bound,
        _ => null,
    };

    private static string ReceiverText(SimpleNameSyntax name) => name.Parent switch
    {
        MemberAccessExpressionSyntax member when member.Name == name => member.Expression.ToString(),
        MemberBindingExpressionSyntax binding when binding.Parent?.Parent is ConditionalAccessExpressionSyntax access =>
            access.Expression.ToString(),
        _ => "this",
    };

    private static string EnclosingMethod(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case LocalFunctionStatementSyntax local:
                    return local.Identifier.ValueText;
                case MethodDeclarationSyntax method:
                    return method.Identifier.ValueText;
                case ConstructorDeclarationSyntax constructor:
                    return constructor.Identifier.ValueText;
                case PropertyDeclarationSyntax property:
                    return property.Identifier.ValueText;
            }
        }

        return "<no method>";
    }

    private static string? BoundArgumentType(SemanticModel model, IMethodSymbol binder, InvocationExpressionSyntax? invocation)
    {
        if (invocation == null)
            return null;
        var index = binder.Parameters.ToList().FindIndex(p => p.Type.Name == "Symbol");
        var arguments = invocation.ArgumentList.Arguments;
        return index < 0 || index >= arguments.Count ? null : model.GetTypeInfo(arguments[index].Expression).Type?.Name;
    }

    private static string? CheckedNodeType(SemanticModel model, SyntaxNode node)
    {
        var parameters = node.Ancestors().Select(a => a switch
        {
            LocalFunctionStatementSyntax local => local.ParameterList,
            MethodDeclarationSyntax method => method.ParameterList,
            ConstructorDeclarationSyntax constructor => constructor.ParameterList,
            _ => null,
        }).FirstOrDefault(p => p != null);
        var first = parameters?.Parameters.FirstOrDefault()?.Type;
        return first == null ? null : model.GetTypeInfo(first).Type?.Name;
    }

    private static string ArgumentText(InvocationExpressionSyntax? invocation)
    {
        if (invocation == null)
            return "<method group>";

        return string.Join(", ", invocation.ArgumentList.Arguments.Select(a =>
        {
            // `new TypeParameterSymbol { ... }` keys on its head: the initializer is not the site's identity.
            var text = a.ToString();
            var cut = text.IndexOfAny(new[] { '{', '\r', '\n' });
            return (cut < 0 ? text : text[..cut]).Trim();
        }));
    }

    /// <summary>
    /// Where a symbol's <c>Kind</c> comes from, read before writing this: only <c>Kind = SymbolKind.X</c> initializers and
    /// <c>with</c> expressions (<c>Symbol.Kind</c> is <c>init</c>; no symbol's <c>Kind</c> is assigned from a variable @ f1418767d).
    /// So a member access <c>SymbolKind.value</c> is a producer in a value-flowing position: the right of an assignment,
    /// an argument, a return, an arrow body, an initializer or a switch-expression arm, through parentheses and either branch of a
    /// conditional. A comparison or a pattern arm consumes the value and is not a producer. A syntax scan of one file's text, so a
    /// test can feed it a snippet.
    /// </summary>
    private static List<string> SymbolKindProducers(string source, string file, string valueName)
    {
        var found = new List<string>();
        foreach (var access in ParseCSharp(source, file).GetCompilationUnitRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (access.Name.Identifier.ValueText != valueName
                || RightmostName(access.Expression) != "SymbolKind")
            {
                continue;
            }

            SyntaxNode value = access;
            while (value.Parent is ParenthesizedExpressionSyntax
                   || (value.Parent is ConditionalExpressionSyntax conditional && conditional.Condition != value))
            {
                value = value.Parent;
            }

            bool producer = value.Parent switch
            {
                AssignmentExpressionSyntax assignment => assignment.Right == value,
                ArgumentSyntax or ReturnStatementSyntax or ArrowExpressionClauseSyntax or EqualsValueClauseSyntax => true,
                SwitchExpressionArmSyntax arm => arm.Expression == value,
                _ => false,
            };
            if (producer)
                found.Add($"{file}:{access.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {value.Parent}");
        }

        return found;
    }

    private static string? RightmostName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => null,
    };

    private static List<string> CompilerSymbolKindProducers(string valueName)
        => CompilerSourceFiles()
            .SelectMany(f => SymbolKindProducers(File.ReadAllText(f), Path.GetFileName(f), valueName))
            .ToList();

    private static IEnumerable<string> BoolProperties(Type type, BindingFlags flags)
        => type.GetProperties(flags).Where(p => p.PropertyType == typeof(bool)).Select(p => p.Name);

    private static IEnumerable<string> ConcreteSubclasses(Type root)
        => root.Assembly.GetTypes()
            .Where(t => root.IsAssignableFrom(t) && !t.IsAbstract && t != root)
            .Select(t => t.Name);

    private static List<string> FamiliesClassifiedAs(ScopeKind kind)
        => ScopeKindRosterTests.ScopeNameFamilies()
            .Select(f => f.Family)
            .Distinct(StringComparer.Ordinal)
            .Where(f => SymbolTable.ClassifyScope(ScopeKindRosterTests.SpecimenFor(f)) == kind)
            .ToList();

    private static void AssertNoProblems(IEnumerable<string> problems, string because)
    {
        var list = problems.ToList();
        list.Should().BeEmpty(because + "\n" + string.Join("\n", list));
    }

    // =========================================================================================================
    // Roster 1: binding kind
    // =========================================================================================================

    [Fact]
    public void BindingKindRoster_EveryEnumValue_IsMappedExactlyOneWay()
    {
        var problems = new List<string>();
        problems.AddRange(Roster.TotalityProblems("SymbolKind", Enum.GetValues<SymbolKind>(), NameResolutionRosters.SymbolKinds.Keys));
        problems.AddRange(Roster.TotalityProblems("TypeKind", Enum.GetValues<TypeKind>(), NameResolutionRosters.TypeKinds.Keys));
        problems.AddRange(Roster.TotalityProblems("TypeBodyMemberKind", Enum.GetValues<TypeBodyMemberKind>(), NameResolutionRosters.MemberKinds.Keys));
        problems.AddRange(Roster.TotalityProblems("ParameterModifier", Enum.GetValues<ParameterModifier>(), NameResolutionRosters.ParameterModifiers.Keys));
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.SymbolKinds));
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.TypeKinds));
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.MemberKinds));
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.ParameterModifiers));

        AssertNoProblems(problems, "every value of D3.a's enumerations is mapped or excluded, with no default arm");
    }

    [Fact]
    public void BindingKindRoster_VariableCube_IsTheWholeProductAndRespectsTheScopeSplit()
    {
        var universe = Enum.GetValues<ScopeKind>().SelectMany(scope => NameResolutionRosters.VariableCubeFlags
            .Select(flag => NameResolutionRosters.CubeKey(SymbolKind.Variable, scope, flag)));
        var problems = Roster.TotalityProblems("VariableCube", universe, NameResolutionRosters.VariableCube.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.VariableCube));
        problems.AddRange(ScopeSplitProblems(NameResolutionRosters.VariableCube, SymbolKind.Variable));

        AssertNoProblems(problems, "a flag alone never decides a kind: each scope × flag cell of SymbolKind.Variable is mapped or excluded");
    }

    [Fact]
    public void BindingKindRoster_FunctionCube_IsTheWholeProductAndRespectsTheScopeSplit()
    {
        var universe = Enum.GetValues<ScopeKind>().SelectMany(scope => NameResolutionRosters.FunctionCubeClasses
            .Select(cls => NameResolutionRosters.CubeKey(SymbolKind.Function, scope, cls)));
        var problems = Roster.TotalityProblems("FunctionCube", universe, NameResolutionRosters.FunctionCube.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.FunctionCube));
        problems.AddRange(ScopeSplitProblems(NameResolutionRosters.FunctionCube, SymbolKind.Function));

        AssertNoProblems(problems, "each owner scope × function class of SymbolKind.Function is mapped or excluded");
    }

    private static IEnumerable<string> ScopeSplitProblems(
        IReadOnlyDictionary<string, RosterEntry<BindingKind>> cube, SymbolKind kind)
    {
        foreach (var scope in Enum.GetValues<ScopeKind>())
        {
            if (!NameResolutionRosters.ScopeSplit.TryGetValue(scope, out var split))
                continue;
            var prefix = NameResolutionRosters.CubeKey(kind, scope, "");
            foreach (var (key, entry) in cube.Where(row => row.Key.StartsWith(prefix, StringComparison.Ordinal)))
            {
                foreach (var outside in entry.Image.Except(split.Image))
                    yield return $"{key} → {outside}, which the {scope} split does not hold";
            }
        }
    }

    [Fact]
    public void BindingKindRoster_ScopeSplit_MapsAllSixScopeKinds()
    {
        var problems = Roster.TotalityProblems("ScopeKind split", Enum.GetValues<ScopeKind>(), NameResolutionRosters.ScopeSplit.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.ScopeSplit));

        var inASplit = Roster.ImageOf(NameResolutionRosters.ScopeSplit).ToHashSet();
        problems.AddRange(Enum.GetValues<BindingKind>()
            .Except(NameResolutionRosters.NonSymbolKinds)
            .Where(k => !inASplit.Contains(k))
            .Select(k => $"BindingKind.{k} has no defining scope in the split"));

        AssertNoProblems(problems, "the module / type / local split is a map over ScopeKind with no default arm (tenth pass finding 7)");
    }

    [Fact]
    public void BindingKindRoster_VariableFlags_MatchReflection()
    {
        // GetProperties() without DeclaredOnly: the base's flags are D3.a's flags too.
        var flags = BoolProperties(typeof(VariableSymbol), BindingFlags.Public | BindingFlags.Instance).ToList();

        flags.Should().HaveCount(NameResolutionRosters.VariableSymbolBoolCensus,
            "a new VariableSymbol or Symbol flag is red until mapped, and the count is a measured literal: "
            + string.Join(", ", flags.OrderBy(f => f, StringComparer.Ordinal)));

        var problems = Roster.TotalityProblems("VariableSymbol flag", flags, NameResolutionRosters.VariableFlags.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.VariableFlags));
        problems.AddRange(NameResolutionRosters.VariableCubeFlags
            .Where(f => f != NameResolutionRosters.NoFlag && !flags.Contains(f))
            .Select(f => $"the cube's flag axis names {f}, which VariableSymbol does not declare"));

        AssertNoProblems(problems, "every VariableSymbol bool is mapped to the kinds it refines or excluded with its reason");
    }

    [Fact]
    public void BindingKindRoster_EveryKind_IsTheImageOfASource()
    {
        // Not the scope split, the kind groups or the binder census: those restate the kinds, so they could
        // not catch an orphan. Builtin's and ImportedName's source is the binder census, and each has its own fact below.
        var image = Roster.ImageOf(NameResolutionRosters.SymbolKinds)
            .Concat(Roster.ImageOf(NameResolutionRosters.TypeKinds))
            .Concat(Roster.ImageOf(NameResolutionRosters.MemberKinds))
            .Concat(Roster.ImageOf(NameResolutionRosters.ParameterModifiers))
            .Concat(Roster.ImageOf(NameResolutionRosters.VariableFlags))
            .Concat(Roster.ImageOf(NameResolutionRosters.VariableCube))
            .Concat(Roster.ImageOf(NameResolutionRosters.FunctionCube))
            .ToHashSet();

        var problems = Enum.GetValues<BindingKind>()
            .Except(NameResolutionRosters.NonSymbolKinds)
            .Where(k => k is not (BindingKind.Builtin or BindingKind.ImportedName) && !image.Contains(k))
            .Select(k => $"BindingKind.{k} is the image of no source value (an orphan the compiler cannot bind)")
            .ToList();
        problems.AddRange(NameResolutionRosters.NonSymbolKinds
            .Where(image.Contains)
            .Select(k => $"BindingKind.{k} is declared the image of no symbol but a source maps to it"));

        AssertNoProblems(problems, "every BindingKind is the image of at least one compiler enumeration value");
    }

    [Fact]
    public void BindingKindRoster_KindGroups_PartitionTheEnum()
    {
        var groups = NameResolutionRosters.ModuleLevelKinds
            .Concat(NameResolutionRosters.TypeLevelKinds)
            .Concat(NameResolutionRosters.InstanceKinds)
            .Concat(NameResolutionRosters.PositionLocalKinds)
            .Append(BindingKind.Builtin)
            .Append(BindingKind.ImportedName)
            .Append(BindingKind.None)
            .ToList();

        groups.Should().BeEquivalentTo(Enum.GetValues<BindingKind>(),
            "the generator's outer axes are built from these groups, so they hold each kind exactly once");
    }

    [Fact]
    public void BindingKindRoster_Builtin_IsTheGlobalScopeBinderAndOnlyNoneIsNoSymbol()
    {
        var builtinSites = CompilerBinders.Value.Sites
            .Where(s => s.Receiver == "_globalScope")
            .Select(s => s.Key)
            .ToList();

        builtinSites.Should().NotBeEmpty("SymbolTable.PopulateBuiltins binds every builtin through _globalScope");
        var problems = builtinSites
            .Where(key => !NameResolutionRosters.BinderSites.TryGetValue(key, out var image)
                || !image.Declares.Contains(BindingKind.Builtin))
            .Select(key => $"{key} binds a builtin but its entry does not declare BindingKind.Builtin")
            .ToList();
        problems.AddRange(NameResolutionRosters.BinderSites
            .Where(row => row.Value.Declares.Contains(BindingKind.Builtin) && !builtinSites.Contains(row.Key))
            .Select(row => $"{row.Key} declares BindingKind.Builtin but is no _globalScope site"));

        AssertNoProblems(problems, "a builtin IS a symbol, and the global scope's binder sites are its source (tenth pass finding 6)");
        NameResolutionRosters.NonSymbolKinds.Should().Equal(new[] { BindingKind.None }, "None is the one kind with no symbol");
    }

    /// <summary>P77b deferred finding 6: <see cref="BindingKindRoster_EveryKind_IsTheImageOfASource"/> excludes the binder
    /// census, so ImportedName's source is held here: the import seam is a binder site of the compiler, its entry declares
    /// ImportedName, and no other entry does.</summary>
    [Fact]
    public void BindingKindRoster_ImportedName_IsTheImportBinderSite()
    {
        var problems = new List<string>();
        if (!CompilerBinders.Value.Sites.Any(s => s.Key == NameResolutionRosters.ImportBinderSite))
            problems.Add($"{NameResolutionRosters.ImportBinderSite} is no binder site of the compiler");
        if (!NameResolutionRosters.BinderSites.TryGetValue(NameResolutionRosters.ImportBinderSite, out var image)
            || !image.Declares.Contains(BindingKind.ImportedName))
            problems.Add($"{NameResolutionRosters.ImportBinderSite} binds every import and its entry does not declare BindingKind.ImportedName");
        problems.AddRange(NameResolutionRosters.BinderSites
            .Where(row => row.Value.Declares.Contains(BindingKind.ImportedName) && row.Key != NameResolutionRosters.ImportBinderSite)
            .Select(row => $"{row.Key} declares BindingKind.ImportedName but is not the import seam"));

        AssertNoProblems(problems, "an imported name IS a symbol, and the import seam is its one binder site (P77b D2.9)");
    }

    [Fact]
    public void BindingKindRoster_DeadEnumValues_HaveNoProducer()
    {
        var dead = NameResolutionRosters.SymbolKinds
            .Where(row => row.Value.Excluded == ExcludedAxis.DeadEnumValue)
            .Select(row => row.Key)
            .ToList();
        dead.Should().BeEquivalentTo(new[] { SymbolKind.Property, SymbolKind.Event });

        // The positive control on the real tree: the same scan finds the producers of a live value, or it is vacuous.
        var live = CompilerSymbolKindProducers(nameof(SymbolKind.Variable));
        live.Should().NotBeEmpty("the producer scan must see the construction sites of a live value");
        Output.WriteLine($"SymbolKind.Variable producers: {live.Count}");

        var producers = dead.SelectMany(value => CompilerSymbolKindProducers(value.ToString())).ToList();
        AssertNoProblems(producers, "a producer of a dead SymbolKind is red until the value is mapped in SymbolKinds");
    }

    [Fact]
    public void BindingKindRoster_ProducerScan_SeesAProducerAndIgnoresAConsumer()
    {
        const string producing = """
            var a = new VariableSymbol { Kind = SymbolKind.Property };
            var b = s with { Kind = flag ? Semantic.SymbolKind.Property : SymbolKind.Variable };
            var c = Make(SymbolKind.Property);
            """;
        const string consuming = """
            if (s.Kind == SymbolKind.Property) { }
            switch (s.Kind) { case SymbolKind.Property: break; }
            var d = s.Kind switch { SymbolKind.Property => 1, _ => 2 };
            """;

        SymbolKindProducers(producing, "snippet.cs", nameof(SymbolKind.Property)).Should().HaveCount(3,
            "an object initializer, a with expression's conditional and an argument are all construction positions");
        SymbolKindProducers(consuming, "snippet.cs", nameof(SymbolKind.Property)).Should().BeEmpty(
            "a comparison, a case label and a switch-arm pattern read the value and produce nothing");
    }

    // =========================================================================================================
    // Roster 2: binder sites
    // =========================================================================================================

    [Fact]
    public void BinderSiteRoster_Census_MatchesTheMeasuredLiteral()
    {
        var scan = CompilerBinders.Value;
        foreach (var site in scan.Sites)
            Output.WriteLine($"{site.Key}  @{site.Line}  {site.ReceiverType} via {site.Receiver}");
        foreach (var rejected in scan.Rejected)
            Output.WriteLine($"rejected: {rejected}");
        Output.WriteLine($"sites {scan.Sites.Count}, rejected {scan.Rejected.Count}, unresolved {scan.Unresolved.Count}");

        scan.Unresolved.Should().BeEmpty("a call that binds to nothing is a blind spot of the positive filter");
        scan.Sites.Should().HaveCount(NameResolutionRosters.BinderSiteCensus,
            "the census is a measured literal: a new binder site is mapped and counted in the same commit\n"
            + string.Join("\n", scan.Sites.Select(s => $"{s.Key} @{s.Line} via {s.Receiver}")));
        scan.Sites.Select(s => s.Receiver).Distinct().Should().Contain(new[] { "_globalScope", "CurrentScope", "merged" },
            "the census is on receiver TYPE: the four SymbolTable receivers alone miss the Scope binders (tenth pass finding 6)");
    }

    [Fact]
    public void BinderSiteRoster_EverySite_IsMappedAndEveryEntry_HasASite()
    {
        var problems = Roster.TotalityProblems(
            "binder site", CompilerBinders.Value.Sites.Select(s => s.Key), NameResolutionRosters.BinderSites.Keys).ToList();

        foreach (var (key, image) in NameResolutionRosters.BinderSites)
        {
            bool mapped = image.Form != null || image.Target != null || image.Declares.Count > 0;
            if (mapped == (image.Excluded != null))
                problems.Add($"{key}: mapped AND excluded, or neither");
            if (image.Excluded != null && string.IsNullOrWhiteSpace(image.Reason))
                problems.Add($"{key}: excluded with no reason");
        }

        AssertNoProblems(problems, "every binder site is a STORE form, a LOCAL target, a declaring kind or an excluded axis, with no default arm");
    }

    [Fact]
    public void BinderSiteRoster_StoreForms_AreTheImageOfTheCensusExceptTheNoSiteForm()
    {
        var image = NameResolutionRosters.BinderSites.Values
            .Where(i => i.Form != null).Select(i => i.Form!.Value).ToHashSet();

        var problems = Enum.GetValues<StoreForm>()
            .Except(NameResolutionRosters.NoSiteStoreForms)
            .Where(f => !image.Contains(f))
            .Select(f => $"StoreForm.{f} is the image of no binder site")
            .ToList();
        problems.AddRange(NameResolutionRosters.NoSiteStoreForms
            .Where(image.Contains)
            .Select(f => $"StoreForm.{f} is named the no-site form but a binder site maps to it"));
        NameResolutionRosters.NoSiteStoreForms.Should().Equal(new[] { StoreForm.Augmented }, "+= is the ONE form with no binder site");

        AssertNoProblems(problems, "the form roster says explicitly which form creates no binding");
    }

    [Fact]
    public void BinderSiteRoster_PositiveFilter_RejectsAnUnrelatedDefine()
    {
        const string source = """
            namespace Sharpy.Compiler.Semantic
            {
                public class Symbol { }
                public class SymbolTable
                {
                    public void Define(Symbol s) { }
                    public bool TryDefine(Symbol s) { Define(s); return true; }
                }
                public class Scope { public void DefineAs(string n, Symbol s) { } }
            }
            namespace Unrelated
            {
                using Sharpy.Compiler.Semantic;
                public class Registry { public void Define(object o) { } }
                public class User
                {
                    public void Use(SymbolTable t, Scope s, Registry r, Symbol x)
                    {
                        t.Define(x);
                        t?.TryDefine(x);
                        s.DefineAs("a", x);
                        r.Define(x);
                        Action<Symbol> group = t.Define;
                        missing.Define(x);
                    }
                }
            }
            """;

        var scan = ScanBinders(
            new[] { ParseCSharp("global using System;\n" + source, SyntheticBinderFile) },
            DispatchSiteScan.BuildReferences(new Dictionary<string, string>(), "Sharpy.Compiler"));

        scan.Sites.Select(s => s.Key).Should().BeEquivalentTo(
            new[]
            {
                $"{SyntheticBinderFile}:TryDefine(s)",
                $"{SyntheticBinderFile}:Use(x)",
                $"{SyntheticBinderFile}:Use(x)#2",
                $"{SyntheticBinderFile}:Use(\"a\", x)",
                $"{SyntheticBinderFile}:Use(<method group>)",
            },
            "the bare call, the member call, the ?. call, the Scope call and the method group are all binders");
        scan.Rejected.Should().ContainSingle().Which.Should().Contain("Unrelated.Registry.Define",
            "a Define on another type is listed as rejected, never a site");
        scan.Unresolved.Should().ContainSingle().Which.Should().Contain("missing.Define",
            "a call that binds to nothing is reported, not dropped");
    }

    [Fact]
    public void BinderSiteRoster_OutSpellings_AreNamedAgainstTheParser()
    {
        var problems = new List<string>();
        foreach (var (spelling, entry) in NameResolutionRosters.OutSpellings)
        {
            var source = $"def g() -> None:\n    f({spelling})\n";
            var lexer = new LexerNs.Lexer(source);
            var tokens = new List<LexerNs.Token>();
            LexerNs.Token token;
            do
            {
                token = lexer.NextToken();
                tokens.Add(token);
            }
            while (token.Type != LexerNs.TokenType.Eof);

            var module = new ParserNs.Parser(tokens).ParseModule();
            var argument = module.Body.Cast<Node>().SelectMany(Descendants).OfType<ModifiedArgument>().SingleOrDefault();
            if (argument?.InlineName != "v" || argument.Modifier != ParameterModifier.Out)
            {
                problems.Add($"'{spelling}' does not parse as an inline out binding of v");
                continue;
            }

            bool isOutLetForm = entry.Image.Contains(StoreForm.OutLet);
            if (argument.IsLet != isOutLetForm)
                problems.Add($"'{spelling}': the parser says IsLet={argument.IsLet}, the roster says StoreForm.OutLet={isOutLetForm}");
        }

        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.OutSpellings));
        AssertNoProblems(problems, "StoreForm.OutLet is exactly the spellings CheckModifiedArgument treats as introducing (IsLet, R-BM)");
    }

    private static IEnumerable<Node> Descendants(Node node)
        => node.GetChildNodes().SelectMany(Descendants).Prepend(node);

    [Fact]
    public void BinderSiteRoster_StoreOuter_IsTotalOverBindingKind()
    {
        var problems = OuterAxisProblems("StoreOuter", NameResolutionRosters.StoreOuter).ToList();
        foreach (var kind in NameResolutionRosters.ModuleLevelKinds.Where(k => k != BindingKind.ModuleLet).Append(BindingKind.EnclosingLocal).Append(BindingKind.Builtin).Append(BindingKind.ImportedName).Append(BindingKind.None))
        {
            if (NameResolutionRosters.StoreOuter.TryGetValue(kind, out var member) && !(member.Generated && !member.ClassEnclosedBodiesOnly))
                problems.Add($"StoreOuter: {kind} is an outer at every body (finding 1)");
        }

        // ModuleLet is out because its declaration is refused: this reads that reason, so the exclusion cannot outlive it.
        if (NameResolutionRosters.StoreOuter[BindingKind.ModuleLet].Generated)
            problems.Add("StoreOuter: ModuleLet is generated, and a store under it is never reached");
        if (!RunSharpy("let x = 5\ndef main() -> None:\n    print(x)\n").AllCodes.Contains("SPY0340"))
            problems.Add("StoreOuter: ModuleLet's exclusion says SPY0340 refuses the declaration, and it no longer does: generate it");

        foreach (var kind in new[]
        {
            BindingKind.StaticField, BindingKind.StaticProperty, BindingKind.StaticMethod, BindingKind.ClassLiteralConst,
            BindingKind.ClassComputedConst, BindingKind.InstanceField, BindingKind.InstanceProperty, BindingKind.InstanceMethod,
        })
        {
            if (NameResolutionRosters.StoreOuter.TryGetValue(kind, out var member) && !member.ClassEnclosedBodiesOnly)
                problems.Add($"StoreOuter: {kind} replaces ClassMember, at the bodies a class encloses (finding 1)");
        }

        AssertNoProblems(problems, "STORE's outer is the image of the kind roster, never a hand list naming a value the roster lacks");
    }

    [Fact]
    public void BinderSiteRoster_StoreBodies_AreTheImageOfRosterFour()
    {
        var problems = BodyAxisProblems("StoreBodies", NameResolutionRosters.StoreBodies.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.StoreBodies));
        AssertNoProblems(problems, "STORE's body axis is roster 4's image, each position excluded by name if it cannot host the form (finding 2)");
    }

    [Fact]
    public void BinderSiteRoster_StoreTargetScope_IsTheImageOfScopeKind()
    {
        var problems = Roster.TotalityProblems("ScopeKind → StoreTargetScope", Enum.GetValues<ScopeKind>(), NameResolutionRosters.StoreTargetScopes.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.StoreTargetScopes));
        var image = Roster.ImageOf(NameResolutionRosters.StoreTargetScopes).ToHashSet();
        problems.AddRange(Enum.GetValues<StoreTargetScope>().Where(s => !image.Contains(s)).Select(s => $"StoreTargetScope.{s} is the image of no ScopeKind"));

        AssertNoProblems(problems, "the target-scope axis is derived from ScopeKind (finding 4)");
    }

    [Fact]
    public void BinderSiteRoster_StoreNesting_NamesABlockFamily()
    {
        SymbolTable.ClassifyScope(NameResolutionRosters.StoreNestingFamily).Should().Be(ScopeKind.Block,
            "StoreNesting.InsideIf stands for one Block family");
        NameResolutionRosters.BlockFamilies[NameResolutionRosters.StoreNestingFamily].Image
            .Should().ContainSingle().Which.LetInBody.Should().NotBeNull("the representative is a statement block, one a store can sit in");
        Enum.GetValues<StoreNesting>().Should().Equal(StoreNesting.Top, StoreNesting.InsideIf);
    }

    private static IEnumerable<string> OuterAxisProblems(string axis, IReadOnlyDictionary<BindingKind, AxisMembership> outer)
    {
        foreach (var problem in Roster.TotalityProblems(axis, Enum.GetValues<BindingKind>(), outer.Keys))
            yield return problem;

        foreach (var (kind, member) in outer)
        {
            if (member.Generated == (member.Excluded != null))
                yield return $"{axis}: {kind} is generated AND excluded, or neither";
            if (member.Excluded != null && string.IsNullOrWhiteSpace(member.Reason))
                yield return $"{axis}: {kind} is excluded with no reason";
        }
    }

    private static IEnumerable<string> BodyAxisProblems(string axis, IEnumerable<ReadPosition> keys)
    {
        foreach (var problem in Roster.TotalityProblems(axis, NameResolutionRosters.BodyPositions, keys))
            yield return problem;

        var rosterFourImage = Roster.ImageOf(NameResolutionRosters.FunctionLikeFamilies)
            .Concat(Roster.ImageOf(NameResolutionRosters.FunctionFlags))
            .Concat(Roster.ImageOf(NameResolutionRosters.FunctionOwners))
            .ToHashSet();
        foreach (var problem in Roster.TotalityProblems($"{axis}: BodyPositions vs roster 4", rosterFourImage, NameResolutionRosters.BodyPositions))
            yield return problem;
    }

    // =========================================================================================================
    // Rosters 3–6: read positions
    // =========================================================================================================

    [Fact]
    public void ReadPositionRoster_ClassBodyPairs_TheProductIsTotal()
    {
        var positions = Enum.GetNames(ClassBodyEnum("ClassBodyReadPosition"));
        var slots = Enum.GetNames(ClassBodyEnum("ClassBodyReadSlot"));
        var product = positions.SelectMany(p => slots.Select(s => NameResolutionRosters.ClassBodyKey(p, s))).ToList();
        product.Should().HaveCount(30, "the two private enums are 5 × 6 @ f1418767d, a measured literal");

        var mapped = NameResolutionRosters.ClassBodyPairs.Keys.Concat(NameResolutionRosters.UnbuiltClassBodyPairs).ToList();
        var problems = Roster.TotalityProblems("ClassBodyReadPosition × ClassBodyReadSlot", product, mapped).ToList();
        problems.AddRange(mapped.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => $"{g.Key} is both built and unbuilt"));
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.ClassBodyPairs));

        // "Unbuilt" is checked against the dispatch, or the list could hide a pair it builds.
        problems.AddRange(Roster.TotalityProblems(
            "pair a ClassBodyReader construction names", BuiltClassBodyPairs(), NameResolutionRosters.ClassBodyPairs.Keys));

        AssertNoProblems(problems, "every pair of the 5 × 6 product is mapped to positions or listed unbuilt, and the built list is what the dispatch builds");
    }

    private static Type ClassBodyEnum(string name)
    {
        var type = typeof(TypeChecker).GetNestedType(name, BindingFlags.NonPublic);
        type.Should().NotBeNull($"{name} is a private nested enum of TypeChecker");
        return type!;
    }

    /// <summary>Each construction whose arguments name a position and a slot: a conditional's two positions are two pairs.</summary>
    private static IEnumerable<string> BuiltClassBodyPairs()
    {
        var source = File.ReadAllText(Path.Combine(CompilerSourceDir(), "Semantic", "TypeChecker.ClassScopeRule.cs"));
        var pairs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var arguments in ParseCSharp(source, "TypeChecker.ClassScopeRule.cs").GetCompilationUnitRoot().DescendantNodes().OfType<ArgumentListSyntax>())
        {
            if (arguments.Arguments.Count < 2)
                continue;

            var named = arguments.Arguments.Take(2).Select(a => a.DescendantNodesAndSelf()
                .OfType<MemberAccessExpressionSyntax>()
                .ToList()).ToList();
            var positions = named[0].Where(m => m.Expression.ToString() == "ClassBodyReadPosition").Select(m => m.Name.ToString());
            var slots = named[1].Where(m => m.Expression.ToString() == "ClassBodyReadSlot").Select(m => m.Name.ToString()).ToList();
            foreach (var pair in positions.SelectMany(p => slots.Select(s => NameResolutionRosters.ClassBodyKey(p, s))))
                pairs.Add(pair);
        }

        return pairs;
    }

    [Fact]
    public void ReadPositionRoster_FunctionLikeFamilies_AreMapped()
    {
        var families = FamiliesClassifiedAs(ScopeKind.FunctionLike);
        families.Should().HaveCount(NameResolutionRosters.FunctionLikeFamilyCensus,
            "the census is a measured literal: " + string.Join(", ", families));

        var problems = Roster.TotalityProblems("FunctionLike family", families, NameResolutionRosters.FunctionLikeFamilies.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.FunctionLikeFamilies));
        AssertNoProblems(problems, "every FunctionLike family of ClassifyScope has a body position or an exclusion");
    }

    [Fact]
    public void ReadPositionRoster_FunctionSymbolFlags_MatchReflection()
    {
        // DeclaredOnly, tenth pass finding 8: the base's three (IsNameBacktickEscaped, IsReExport, IsErrorRecovery) are
        // symbol facts mapped on VariableFlags, and no scope family reads them. Without it the count is 13.
        var flags = BoolProperties(typeof(FunctionSymbol), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).ToList();

        flags.Should().HaveCount(NameResolutionRosters.FunctionSymbolDeclaredBoolCensus,
            "a new FunctionSymbol flag is red until mapped, and the count is a measured literal: "
            + string.Join(", ", flags.OrderBy(f => f, StringComparer.Ordinal)));

        var problems = Roster.TotalityProblems("FunctionSymbol flag", flags, NameResolutionRosters.FunctionFlags.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.FunctionFlags));
        AssertNoProblems(problems, "every FunctionSymbol bool refines the function: family or is excluded with its reason");
    }

    [Fact]
    public void ReadPositionRoster_FunctionOwners_AreTheProductOfOwnerAndNameClass()
    {
        var universe = Enum.GetValues<ScopeKind>().SelectMany(owner => Enum.GetValues<FunctionNameClass>()
            .Select(nameClass => NameResolutionRosters.OwnerKey(owner, nameClass)));
        var problems = Roster.TotalityProblems("function: owner × name class", universe, NameResolutionRosters.FunctionOwners.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.FunctionOwners));
        AssertNoProblems(problems, "the function: family's owner × name class is total, with no default arm");
    }

    [Fact]
    public void ReadPositionRoster_StatementSubclasses_MatchModuleLevelValidator()
    {
        var subclasses = ConcreteSubclasses(typeof(Statement)).ToList();
        subclasses.Should().HaveCount(NameResolutionRosters.StatementSubclassCensus,
            "the census is a measured literal: " + string.Join(", ", subclasses.OrderBy(s => s, StringComparer.Ordinal)));

        var problems = Roster.TotalityProblems("Statement subclass", subclasses, NameResolutionRosters.Statements.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.Statements));

        var split = ModuleLevelValidatorSplit();
        foreach (var (statement, entry) in NameResolutionRosters.Statements)
        {
            bool executable = split.Executable.Contains(statement) || !split.Admitted.Contains(statement);
            bool transparent = statement == nameof(DecoratedStatement);
            if (transparent)
            {
                if (split.Executable.Contains(statement) || split.Admitted.Contains(statement))
                    problems.Add($"{statement} is mapped transparent, but the validator names it");
                continue;
            }

            bool refused = entry.Excluded == ExcludedAxis.RefusedByKind;
            if (refused != executable)
                problems.Add($"{statement}: the roster says refused-by-kind={refused}, ModuleLevelValidator says executable={executable}");
        }

        foreach (var let in new[] { nameof(Assignment), nameof(VariableDeclaration) })
        {
            if (!split.Let.Contains(let))
                problems.Add($"{let} {{ IsLet }} is the ModuleLet kind's module route, but the validator's let arm does not name it");
        }

        split.Text.Should().Contain("UnwrapDecorated()", "DecoratedStatement's transparency is the validator's unwrap");

        AssertNoProblems(problems, "the module-position roster is checked against the validator's admitted / executable split, not restated");
    }

    private sealed record ValidatorSplit(HashSet<string> Admitted, HashSet<string> Executable, HashSet<string> Let, string Text);

    /// <summary>The first <c>switch</c> of <c>ModuleLevelValidator</c>, by arm: an arm that files <c>executableStatements</c> or
    /// <c>letStatements</c>, and every other arm, which admits. A label's own pattern gives the statement type; a <c>default</c> has
    /// none, so a statement in no arm is executable by the validator's default.</summary>
    private static ValidatorSplit ModuleLevelValidatorSplit()
    {
        var text = File.ReadAllText(Path.Combine(CompilerSourceDir(), "Semantic", "Validation", "ModuleLevelValidator.cs"));
        var dispatch = ParseCSharp(text, "ModuleLevelValidator.cs").GetCompilationUnitRoot()
            .DescendantNodes().OfType<SwitchStatementSyntax>().First();

        var admitted = new HashSet<string>(StringComparer.Ordinal);
        var executable = new HashSet<string>(StringComparer.Ordinal);
        var let = new HashSet<string>(StringComparer.Ordinal);

        foreach (var section in dispatch.Sections)
        {
            var body = string.Concat(section.Statements.Select(s => s.ToString()));
            var side = body.Contains("executableStatements.Add", StringComparison.Ordinal) ? executable
                : body.Contains("letStatements.Add", StringComparison.Ordinal) ? let
                : admitted;

            foreach (var label in section.Labels)
            {
                var name = label switch
                {
                    CaseSwitchLabelSyntax plain => plain.Value.ToString(),
                    CasePatternSwitchLabelSyntax { Pattern: DeclarationPatternSyntax declared } => declared.Type.ToString(),
                    CasePatternSwitchLabelSyntax { Pattern: TypePatternSyntax typed } => typed.Type.ToString(),
                    CasePatternSwitchLabelSyntax { Pattern: RecursivePatternSyntax recursive } => recursive.Type?.ToString(),
                    CasePatternSwitchLabelSyntax { Pattern: ConstantPatternSyntax constant } => constant.Expression.ToString(),
                    _ => null,
                };
                if (name != null)
                    side.Add(name);
            }
        }

        return new ValidatorSplit(admitted, executable, let, text);
    }

    [Fact]
    public void ReadPositionRoster_TypeAnnotationCensus_ScalarAndCollection()
    {
        var properties = typeof(TypeAnnotation).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(TypeAnnotation).Namespace)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .ToList();

        var scalar = properties.Where(p => typeof(TypeAnnotation).IsAssignableFrom(p.PropertyType)).Select(PropertyKey).ToList();
        var collection = properties
            .Where(p => p.PropertyType != typeof(string) && p.PropertyType.GetInterfaces()
                .Any(i => i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                    && typeof(TypeAnnotation).IsAssignableFrom(i.GetGenericArguments()[0])))
            .Select(PropertyKey)
            .ToList();

        scalar.Should().HaveCount(NameResolutionRosters.TypeAnnotationScalarCensus,
            "the scalar census is a measured literal: " + string.Join(", ", scalar.OrderBy(k => k, StringComparer.Ordinal)));
        collection.Should().HaveCount(NameResolutionRosters.TypeAnnotationCollectionCensus,
            "the collection census is a measured literal (tenth pass finding 5): " + string.Join(", ", collection.OrderBy(k => k, StringComparer.Ordinal)));

        var problems = Roster.TotalityProblems("TypeAnnotation property", scalar.Concat(collection), NameResolutionRosters.TypeAnnotationProperties.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.TypeAnnotationProperties));
        var image = Roster.ImageOf(NameResolutionRosters.TypeAnnotationProperties).ToHashSet();
        problems.AddRange(Enum.GetValues<TypePosition>().Where(p => !image.Contains(p)).Select(p => $"TypePosition.{p} is the image of no property"));

        AssertNoProblems(problems, "every AST property typed TypeAnnotation or IEnumerable<TypeAnnotation> is a type position or excluded");
    }

    private static string PropertyKey(PropertyInfo property) => $"{property.DeclaringType!.Name}.{property.Name}";

    /// <summary>Base lists and alias targets are in both rosters, which cross-check as patterns do against roster 6.</summary>
    [Fact]
    public void ReadPositionRoster_TypePositions_CrossCheckTheModulePositions()
    {
        var problems = new List<string>();

        void CrossCheck(TypePosition typePosition, ReadPosition readPosition)
        {
            var fromTypes = NameResolutionRosters.TypeAnnotationProperties
                .Where(row => row.Value.Image.Contains(typePosition))
                .Select(row => row.Key.Split('.')[0]);
            var fromStatements = NameResolutionRosters.Statements
                .Where(row => row.Value.Image.Contains(readPosition))
                .Select(row => row.Key);
            problems.AddRange(Roster.TotalityProblems($"{typePosition} ↔ ReadPosition.{readPosition}", fromTypes, fromStatements));
        }

        CrossCheck(TypePosition.BaseList, ReadPosition.BaseList);
        CrossCheck(TypePosition.AliasTarget, ReadPosition.TypeAliasTarget);

        AssertNoProblems(problems, "the statement that owns a base list or alias target owns the type position, in both rosters");
    }

    [Fact]
    public void ReadPositionRoster_EveryPosition_IsTheImageOfARoster()
    {
        var image = Roster.ImageOf(NameResolutionRosters.ClassBodyPairs)
            .Concat(Roster.ImageOf(NameResolutionRosters.FunctionLikeFamilies))
            .Concat(Roster.ImageOf(NameResolutionRosters.FunctionFlags))
            .Concat(Roster.ImageOf(NameResolutionRosters.FunctionOwners))
            .Concat(Roster.ImageOf(NameResolutionRosters.Statements))
            .ToHashSet();

        // A comprehension's two positions ride their host, so they are fed by the comprehension Block families.
        if (Roster.ImageOf(NameResolutionRosters.BlockFamilies).Any(c => c.Target == LocalTarget.Comprehension))
            image.UnionWith(NameResolutionRosters.ComprehensionNestings);

        AssertNoProblems(
            Enum.GetValues<ReadPosition>().Where(p => !image.Contains(p)).Select(p => $"ReadPosition.{p} is the image of no roster value"),
            "the position vocabulary holds nothing the rosters cannot generate");
    }

    // =========================================================================================================
    // Roster 7: patterns
    // =========================================================================================================

    [Fact]
    public void PatternKindRoster_EverySubclass_IsClassed()
    {
        var subclasses = ConcreteSubclasses(typeof(Pattern)).ToList();
        subclasses.Should().HaveCount(NameResolutionRosters.PatternSubclassCensus,
            "the census is a measured literal: " + string.Join(", ", subclasses.OrderBy(s => s, StringComparer.Ordinal)));

        AssertNoProblems(
            Roster.TotalityProblems("Pattern subclass", subclasses, NameResolutionRosters.Patterns.Keys),
            "every Pattern subclass is in one of TYPEMEMBER's three classes, with no default arm");
    }

    [Fact]
    public void PatternKindRoster_ClassesCrossCheckTheTypeAnnotationCensus()
    {
        var problems = new List<string>();
        foreach (var type in typeof(Pattern).Assembly.GetTypes().Where(t => typeof(Pattern).IsAssignableFrom(t) && !t.IsAbstract))
        {
            if (!NameResolutionRosters.Patterns.TryGetValue(type.Name, out var patternClass))
                continue;

            var owned = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var typeAnnotated = owned.Where(p => typeof(TypeAnnotation).IsAssignableFrom(p.PropertyType)).ToList();
            bool memberAccess = owned.Any(p => p.PropertyType == typeof(ImmutableArray<string>) && p.Name == nameof(MemberAccessPattern.Parts));
            bool ownsPattern = owned.Any(p => typeof(Pattern).IsAssignableFrom(p.PropertyType)
                || typeof(IEnumerable<Pattern>).IsAssignableFrom(p.PropertyType));

            if ((typeAnnotated.Count > 0 || memberAccess) && patternClass != PatternClass.NamesTypeOrMember)
                problems.Add($"{type.Name} owns a type annotation or member access but is {patternClass}");

            foreach (var property in typeAnnotated)
            {
                var key = PropertyKey(property);
                if (!NameResolutionRosters.TypeAnnotationProperties.TryGetValue(key, out var entry)
                    || (!entry.Image.Contains(TypePosition.PatternType) && entry.Excluded != ExcludedAxis.DeadEnumValue))
                {
                    problems.Add($"{key} is a pattern's type annotation but roster 6 maps it neither to TypePosition.PatternType nor as dead");
                }
            }

            if (patternClass == PatternClass.Composite && !ownsPattern)
                problems.Add($"{type.Name} is Composite but owns no inner pattern");
            if (patternClass == PatternClass.CarriesNoName && typeAnnotated.Count > 0)
                problems.Add($"{type.Name} is CarriesNoName but owns a type annotation");
        }

        AssertNoProblems(problems, "a pattern's class follows from what it owns: a type annotation or member access names a type, an inner pattern composes");
    }

    /// <summary>Dead exactly when nothing sets it: each pattern's type annotation, in both directions. A pattern with a
    /// producer that the roster calls dead loses a position; a dead one the roster maps generates a cell that is SPY0102.</summary>
    [Fact]
    public void PatternKindRoster_DeadTypeAnnotations_HaveNoProducer()
    {
        var problems = new List<string>();
        var sources = CompilerSourceFiles().Select(f => (File: Path.GetFileName(f), Text: File.ReadAllText(f))).ToList();

        foreach (var type in typeof(Pattern).Assembly.GetTypes().Where(t => typeof(Pattern).IsAssignableFrom(t) && !t.IsAbstract))
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => typeof(TypeAnnotation).IsAssignableFrom(p.PropertyType)))
            {
                var key = PropertyKey(property);
                if (!NameResolutionRosters.TypeAnnotationProperties.TryGetValue(key, out var entry))
                    continue; // the census fact names an unmapped property

                var producers = sources.SelectMany(s => AstPropertyProducers(s.Text, s.File, type.Name, property.Name)).ToList();
                Output.WriteLine($"{key}: {producers.Count} producers");
                bool dead = entry.Excluded == ExcludedAxis.DeadEnumValue;
                if (dead != (producers.Count == 0))
                    problems.Add($"{key}: the roster says dead={dead}, the compiler has {producers.Count} producers\n  " + string.Join("\n  ", producers));
            }
        }

        NameResolutionRosters.PatternNotes.Should().ContainKey(nameof(BindingPattern));
        AssertNoProblems(problems, "a dead AST property is excluded with its reason, and only a dead one");
    }

    [Fact]
    public void PatternKindRoster_ProducerScan_SeesAProducerAndIgnoresAnotherType()
    {
        const string snippet = """
            var a = new BindingPattern { Name = n, Type = t };
            var b = new Ast.BindingPattern { Type = t };
            var c = new BindingPattern { Name = n };
            var d = new TypePattern { Type = t };
            var e = new Other { Type = t };
            """;

        AstPropertyProducers(snippet, "snippet.cs", nameof(BindingPattern), nameof(BindingPattern.Type)).Should().HaveCount(2,
            "an initializer setting the property is a producer, with or without the namespace alias; the unset one and other types are not");
    }

    /// <summary>A construction of <paramref name="typeName"/> whose object initializer sets <paramref name="property"/>: how every AST
    /// pattern is built. A syntax scan of one file's text, so a test can feed it a snippet.</summary>
    private static IEnumerable<string> AstPropertyProducers(string source, string file, string typeName, string property)
    {
        if (!source.Contains(typeName, StringComparison.Ordinal))
            return Array.Empty<string>();

        return ParseCSharp(source, file).GetCompilationUnitRoot().DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => creation.Type.ToString().Split('.').Last() == typeName
                && creation.Initializer != null
                && creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>()
                    .Any(a => a.Left is IdentifierNameSyntax left && left.Identifier.ValueText == property))
            .Select(creation => $"{file}:{creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1}")
            .ToList();
    }

    // =========================================================================================================
    // Roster 8: local constructs
    // =========================================================================================================

    [Fact]
    public void LocalConstructRoster_EveryBlockFamily_IsMapped()
    {
        var families = FamiliesClassifiedAs(ScopeKind.Block);
        families.Should().HaveCount(NameResolutionRosters.BlockFamilyCensus,
            "the census is a measured literal: " + string.Join(", ", families));

        var problems = Roster.TotalityProblems("Block family", families, NameResolutionRosters.BlockFamilies.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.BlockFamilies));

        var constructs = Roster.ImageOf(NameResolutionRosters.BlockFamilies).Select(c => c.LetInBody).ToHashSet();
        problems.AddRange(Enum.GetValues<BlockConstruct>().Where(c => !constructs.Contains(c)).Select(c => $"BlockConstruct.{c} is the image of no Block family"));

        AssertNoProblems(problems, "every Block family hosts a let in its body, is a comprehension target, or is excluded");
    }

    [Fact]
    public void LocalConstructRoster_EveryLocalTarget_IsTheImageOfABinderSite()
    {
        var targets = NameResolutionRosters.BinderSites.Values.Where(i => i.Target != null).Select(i => i.Target!.Value).ToList();

        AssertNoProblems(
            Roster.TotalityProblems("LocalTarget", Enum.GetValues<LocalTarget>(), targets),
            "LOCAL's construct targets are the binder census's LOCAL-target class: both directions");
    }

    [Fact]
    public void LocalConstructRoster_OuterAxis_IsTotalOverBindingKind()
    {
        var problems = OuterAxisProblems("LocalOuter", NameResolutionRosters.LocalOuter).ToList();
        problems.AddRange(NameResolutionRosters.LocalOuter
            .Where(row => !row.Value.Generated && NameResolutionRosters.ModuleLevelKinds.Concat(NameResolutionRosters.TypeLevelKinds).Contains(row.Key)
                && row.Value.Excluded != ExcludedAxis.PrunedRepresentative)
            .Select(row => $"LocalOuter: {row.Key} is left out, and only D3.e's pruning order may leave a module or type kind out"));

        AssertNoProblems(problems, "LOCAL's outer is the whole roster, or a value names why it is pruned (finding 3)");
    }

    /// <summary>P77b D5: TYPEMEMBER's declaring kind × member pairs are a map total over the image of <see cref="TypeKind"/> ×
    /// (<see cref="TypeBodyMemberKind"/> ∪ the extra members), both directions, each pair generated or out with a reason; the
    /// host and member maps are total over their enums.</summary>
    [Fact]
    public void TypeMemberShapesRoster_IsTotalOverHostsByMembers()
    {
        var problems = Roster.TotalityProblems("TypeMemberHosts", Enum.GetValues<TypeKind>(), NameResolutionRosters.TypeMemberHosts.Keys).ToList();
        problems.AddRange(Roster.TotalityProblems("TypeMemberMembers", Enum.GetValues<TypeBodyMemberKind>(), NameResolutionRosters.TypeMemberMembers.Keys));
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.TypeMemberHosts));
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.TypeMemberMembers));
        problems.AddRange(Roster.TotalityProblems("TypeMemberShapes", NameResolutionRosters.TypeMemberUniverse(), NameResolutionRosters.TypeMemberShapes.Keys));
        foreach (var (pair, member) in NameResolutionRosters.TypeMemberShapes)
        {
            if (member.Generated == (member.Excluded != null))
                problems.Add($"TypeMemberShapes: {pair.Host}/{pair.Member} is generated AND excluded, or neither");
            if (member.Excluded != null && string.IsNullOrWhiteSpace(member.Reason))
                problems.Add($"TypeMemberShapes: {pair.Host}/{pair.Member} is excluded with no reason");
        }

        AssertNoProblems(problems, "TYPEMEMBER's pairs are the type-kind image × the member image, each left-out pair naming why");
    }

    /// <summary>P77b D1: READ's bare outer axis is a map total over the module-level group ∪ {Builtin, ImportedName, None}, both
    /// directions, each value generated or out with a reason, and ModuleLet's reason is read from the compiler.</summary>
    [Fact]
    public void ReadOuterRoster_IsTotalOverModuleLevelKindsBuiltinAndNone()
    {
        var problems = Roster.TotalityProblems("ReadOuter", NameResolutionRosters.ReadOuterUniverse, NameResolutionRosters.ReadOuter.Keys).ToList();
        foreach (var (kind, member) in NameResolutionRosters.ReadOuter)
        {
            if (member.Generated == (member.Excluded != null))
                problems.Add($"ReadOuter: {kind} is generated AND excluded, or neither");
            if (member.Excluded != null && string.IsNullOrWhiteSpace(member.Reason))
                problems.Add($"ReadOuter: {kind} is excluded with no reason");
            if (member.Excluded == ExcludedAxis.PrunedRepresentative)
                problems.Add($"ReadOuter: {kind} is pruned, and R-KP restored every pruned READ outer");
        }

        // ModuleLet is out because its declaration is refused: this reads that reason, so the exclusion cannot outlive it.
        if (!RunSharpy("let x = 5\ndef main() -> None:\n    print(x)\n").AllCodes.Contains("SPY0340"))
            problems.Add("ReadOuter: ModuleLet's exclusion says SPY0340 refuses the declaration, and it no longer does: generate it");

        AssertNoProblems(problems, "READ's outer is the module-level group's image with the builtin and no binding, each left-out kind naming why");
    }

    /// <summary>P77b D1: the builtin-class arm is out exactly for the restored builtin names a type declaration may not spell.
    /// Both directions, read from the compiler: a name in the set that SPY0212 stops refusing is red (generate it), and a
    /// name outside it that SPY0212 refuses is red (its cells would be FalseRefusal by design).</summary>
    [Fact]
    public void BuiltinClassArm_IsOutExactlyForTheBuiltinTypeNames()
    {
        var problems = new List<string>();
        var names = Builtins.Select(b => b.Name).ToHashSet(StringComparer.Ordinal);
        problems.AddRange(BuiltinTypeNames.Where(n => !names.Contains(n)).Select(n => $"BuiltinTypeNames: {n} is no builtin of the axis"));
        foreach (var name in names.OrderBy(n => n, StringComparer.Ordinal))
        {
            var refused = RunSharpy($"class {name}:\n    n0: int = 9\ndef main() -> None:\n    print(1)\n").ErrorCodes.Contains("SPY0212");
            if (refused != BuiltinTypeNames.Contains(name))
                problems.Add($"BuiltinTypeNames: `class {name}:` is {(refused ? "" : "not ")}refused with SPY0212 and the set says the opposite");
        }

        AssertNoProblems(problems, "the builtin-class arm leaves out the names SPY0212 refuses as a type declaration, and only those");
    }

    /// <summary>P77b deferred finding 5: LOCAL's binder axis is a map over roster 1's position-local group, both
    /// directions, each value generated or out with a reason. A kind added to the group with no entry is red here.</summary>
    [Fact]
    public void LocalKindsRoster_IsTotalOverPositionLocalKinds()
    {
        var problems = Roster.TotalityProblems("LocalKinds", NameResolutionRosters.PositionLocalKinds, NameResolutionRosters.LocalKinds.Keys).ToList();
        foreach (var (kind, member) in NameResolutionRosters.LocalKinds)
        {
            if (member.Generated == (member.Excluded != null))
                problems.Add($"LocalKinds: {kind} is generated AND excluded, or neither");
            if (member.Excluded != null && string.IsNullOrWhiteSpace(member.Reason))
                problems.Add($"LocalKinds: {kind} is excluded with no reason");
        }

        AssertNoProblems(problems, "LOCAL's binder axis is the position-local kinds' image, each left-out kind naming why");
    }

    [Fact]
    public void LocalConstructRoster_LocalBodies_AreTheImageOfRosterFour()
    {
        var problems = BodyAxisProblems("LocalBodies", NameResolutionRosters.LocalBodies.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.LocalBodies));
        AssertNoProblems(problems, "LOCAL's body axis is roster 4's image, each position excluded by name if it cannot host the target (finding 2)");
    }

    // =========================================================================================================
    // P77b axis 1: comprehension kind × clause × slot
    // =========================================================================================================

    private static IEnumerable<Type> ConcreteSubclassTypes(Type root)
        => root.Assembly.GetTypes().Where(t => root.IsAssignableFrom(t) && !t.IsAbstract && t != root);

    /// <summary>The kinds are the AST's comprehension nodes AND roster 8's comprehension families, both directions each, and
    /// every <see cref="ComprehensionKind"/> is the image of exactly one row.</summary>
    [Fact]
    public void ComprehensionKindsRoster_IsTotalOverTheAstNodesAndTheBlockFamilies()
    {
        var nodes = ConcreteSubclasses(typeof(Expression))
            .Where(n => n.EndsWith("Comprehension", StringComparison.Ordinal) || n == nameof(GeneratorExpression));
        var problems = Roster.TotalityProblems("ComprehensionKinds over the Expression subclasses", nodes, NameResolutionRosters.ComprehensionKinds.Keys).ToList();
        var families = NameResolutionRosters.BlockFamilies
            .Where(e => e.Value.Image.Any(c => c.Target == LocalTarget.Comprehension)).Select(e => e.Key);
        problems.AddRange(Roster.TotalityProblems("ComprehensionKinds over the comprehension BlockFamilies", families,
            NameResolutionRosters.ComprehensionKinds.Values.Select(r => r.Family)));
        problems.AddRange(Roster.TotalityProblems("ComprehensionKind", Enum.GetValues<ComprehensionKind>(),
            NameResolutionRosters.ComprehensionKinds.Values.Select(r => r.Kind)));
        problems.AddRange(NameResolutionRosters.ComprehensionKinds.Values.GroupBy(r => r.Kind).Where(g => g.Count() > 1)
            .Select(g => $"ComprehensionKind.{g.Key} is the image of {g.Count()} nodes"));

        AssertNoProblems(problems, "axis 1's kinds are the comprehension nodes of the AST and the comprehension families of ClassifyScope");
    }

    /// <summary>The clauses are the <c>ComprehensionClause</c> subclasses and every <c>bool</c> property of one (a flag is a row
    /// or an <c>Out</c>), both directions; every clause shape is the image of a subclass row.</summary>
    [Fact]
    public void ComprehensionClausesRoster_IsTotalOverTheSubclassesAndTheirFlags()
    {
        var subclasses = ConcreteSubclassTypes(typeof(ComprehensionClause)).ToList();
        var universe = subclasses.Select(t => t.Name)
            .Concat(subclasses.SelectMany(t => BoolProperties(t, BindingFlags.Public | BindingFlags.Instance).Select(p => $"{t.Name}.{p}")))
            .ToList();
        var problems = Roster.TotalityProblems("ComprehensionClauses (a subclass or a clause flag with no entry)", universe,
            NameResolutionRosters.ComprehensionClauses.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.ComprehensionClauses));
        problems.AddRange(Roster.TotalityProblems("ComprehensionClauseShape", Enum.GetValues<ComprehensionClauseShape>(),
            NameResolutionRosters.ComprehensionClauses.Where(e => !e.Key.Contains('.')).SelectMany(e => e.Value.Image)));
        problems.AddRange(NameResolutionRosters.ComprehensionClauses.Where(e => e.Key.Contains('.') && e.Value.Excluded == null)
            .Select(e => $"ComprehensionClauses: the flag {e.Key} is generated, and a flag row is an Out or the image of no clause shape"));

        AssertNoProblems(problems, "axis 1's clauses are the ComprehensionClause subclasses, each of their bool flags mapped or out with its reason");
    }

    /// <summary>P77b deferred finding 1: the slots are every Expression-typed property of the five comprehension nodes and of the
    /// clauses, by reflection, both directions; every <see cref="ComprehensionSlot"/> is an image.</summary>
    [Fact]
    public void ComprehensionSlotsRoster_IsTotalOverTheExpressionTypedProperties()
    {
        var types = ConcreteSubclassTypes(typeof(Expression)).Where(t => NameResolutionRosters.ComprehensionKinds.ContainsKey(t.Name))
            .Concat(ConcreteSubclassTypes(typeof(ComprehensionClause)))
            .ToList();
        var universe = types.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => typeof(Expression).IsAssignableFrom(p.PropertyType))
            .Select(p => $"{t.Name}.{p.Name}")).ToList();
        var problems = Roster.TotalityProblems("ComprehensionSlots (a property with no entry)", universe, NameResolutionRosters.ComprehensionSlots.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.ComprehensionSlots));
        problems.AddRange(Roster.TotalityProblems("ComprehensionSlot", Enum.GetValues<ComprehensionSlot>(), Roster.ImageOf(NameResolutionRosters.ComprehensionSlots)));

        AssertNoProblems(problems, "axis 1's slots are the Expression-typed properties of the comprehension nodes and clauses");
    }

    /// <summary>P77b deferred finding 2: the walrus STORE's slot axis is total over <see cref="ComprehensionSlot"/> and its host
    /// axis over <see cref="NameResolutionRosters.ComprehensionHosts"/>, each value generated or out with its reason.</summary>
    [Fact]
    public void ComprehensionWalrusRoster_IsTotalOverTheSlotsAndTheHosts()
    {
        var problems = Roster.TotalityProblems("ComprehensionWalrusSlots", Enum.GetValues<ComprehensionSlot>(), NameResolutionRosters.ComprehensionWalrusSlots.Keys).ToList();
        problems.AddRange(Roster.TotalityProblems("ComprehensionWalrusHosts", NameResolutionRosters.ComprehensionHosts, NameResolutionRosters.ComprehensionWalrusHosts.Keys));
        problems.AddRange(MembershipShapeProblems("ComprehensionWalrusSlots", NameResolutionRosters.ComprehensionWalrusSlots));
        problems.AddRange(MembershipShapeProblems("ComprehensionWalrusHosts", NameResolutionRosters.ComprehensionWalrusHosts));

        AssertNoProblems(problems, "a walrus in a comprehension is generated at every slot and host python admits, the others out with the reason");
    }

    // =========================================================================================================
    // P77b axis 2: N3 cycle × route
    // =========================================================================================================

    /// <summary>P77b deferred finding 3: the cycle's endpoints are a map total over roster 1's module-level, type-level and
    /// instance groups, both directions, each kind an endpoint or out with its reason; both levels keep an endpoint.</summary>
    [Fact]
    public void CycleEndpointsRoster_IsTotalOverTheModuleTypeAndInstanceGroups()
    {
        var problems = Roster.TotalityProblems("CycleEndpoints", NameResolutionRosters.CycleEndpointUniverse(), NameResolutionRosters.CycleEndpoints.Keys).ToList();
        problems.AddRange(MembershipShapeProblems("CycleEndpoints", NameResolutionRosters.CycleEndpoints));
        var generated = NameResolutionRosters.CycleEndpoints.Where(e => e.Value.Generated).Select(e => e.Key).ToList();
        if (!generated.Any(k => NameResolutionRosters.ModuleLevelKinds.Contains(k)))
            problems.Add("CycleEndpoints: no module-level endpoint, so the module pairs generate nothing");
        if (!generated.Any(k => NameResolutionRosters.TypeLevelKinds.Contains(k)))
            problems.Add("CycleEndpoints: no type-level endpoint, so the type pairs generate nothing");

        AssertNoProblems(problems, "an N3 cycle's endpoints are roster 1's value kinds with an initializer, each other kind naming why");
    }

    /// <summary>The cycle's routes are total over <see cref="Spelling"/> × <see cref="CyclePair"/>, both directions, each an image
    /// of <see cref="CycleThrough"/> or out with its reason, and every <see cref="CycleThrough"/> value is generated somewhere.</summary>
    [Fact]
    public void CycleRoutesRoster_IsTotalOverSpellingsByEndpointPairs()
    {
        var problems = new List<string>();
        foreach (var s in Enum.GetValues<Spelling>().Where(s => !NameResolutionRosters.CycleRoutes.ContainsKey(s)))
            problems.Add($"CycleRoutes: Spelling.{s} has no cycle cell and no exclusion");
        problems.AddRange(Roster.TotalityProblems("CycleRoutes (spelling)", Enum.GetValues<Spelling>(), NameResolutionRosters.CycleRoutes.Keys)
            .Where(p => p.Contains("map entry with no source value", StringComparison.Ordinal)));
        foreach (var (spelling, pairs) in NameResolutionRosters.CycleRoutes)
        {
            problems.AddRange(Roster.TotalityProblems($"CycleRoutes[{spelling}]", Enum.GetValues<CyclePair>(), pairs.Keys));
            problems.AddRange(Roster.ShapeProblems(pairs).Select(p => $"CycleRoutes[{spelling}] {p}"));
        }
        problems.AddRange(Roster.TotalityProblems("CycleThrough", Enum.GetValues<CycleThrough>(),
            NameResolutionRosters.CycleRoutes.Values.SelectMany(p => p.Values).SelectMany(e => e.Image)));

        AssertNoProblems(problems, "every (spelling, endpoint pair) of an N3 cycle is generated over the through axis or out with its reason");
    }

    // =========================================================================================================
    // P77b axis 3: observation {value, order}
    // =========================================================================================================

    /// <summary>The order observation's kinds are a map total over <see cref="BindingKind"/>, both directions, each kind generated
    /// or out with its reason; a generated kind is a module, type-level or instance kind (the ones with a load or construction
    /// order).</summary>
    [Fact]
    public void OrderObservationKindsRoster_IsTotalOverBindingKind()
    {
        var problems = Roster.TotalityProblems("OrderObservationKinds", Enum.GetValues<BindingKind>(), NameResolutionRosters.OrderObservationKinds.Keys).ToList();
        problems.AddRange(MembershipShapeProblems("OrderObservationKinds", NameResolutionRosters.OrderObservationKinds));
        var ordered = NameResolutionRosters.ModuleLevelKinds.Concat(NameResolutionRosters.TypeLevelKinds).Concat(NameResolutionRosters.InstanceKinds).ToHashSet();
        problems.AddRange(NameResolutionRosters.OrderObservationKinds.Where(e => e.Value.Generated && !ordered.Contains(e.Key))
            .Select(e => $"OrderObservationKinds: {e.Key} is generated and has no load or construction order"));

        AssertNoProblems(problems, "the order observation rides every kind whose initializer runs at load or construction, each other kind naming why");
    }

    // =========================================================================================================
    // P77b axis 4: LOCAL read timing
    // =========================================================================================================

    /// <summary>The read timings are a closed literal set: every <see cref="ReadTiming"/> has a row, both directions, and the
    /// constructs a timing crosses are roster values — the back edge's families are exactly roster 8's loop-body families, the
    /// failed guard's captures are LocalTargets a pattern binds, and roster 7 classes <c>GuardPattern</c>.</summary>
    [Fact]
    public void ReadTimingsRoster_IsTotalAndCrossesRosterValues()
    {
        var problems = Roster.TotalityProblems("ReadTimings", Enum.GetValues<ReadTiming>(), NameResolutionRosters.ReadTimings.Keys).ToList();
        var loopBodies = NameResolutionRosters.BlockFamilies
            .Where(e => e.Value.Image.Any(c => c.LetInBody is BlockConstruct.ForBody or BlockConstruct.WhileBody)).Select(e => e.Key);
        problems.AddRange(Roster.TotalityProblems("BackEdgeLoopFamilies", loopBodies, NameResolutionRosters.BackEdgeLoopFamilies));
        // The pattern binders of the binder census: the sites in the pattern checker that bind a LOCAL target.
        var captures = NameResolutionRosters.BinderSites
            .Where(e => e.Key.StartsWith("TypeChecker.Statements.Patterns.cs:", StringComparison.Ordinal) && e.Value.Target != null)
            .Select(e => e.Value.Target!.Value);
        problems.AddRange(Roster.TotalityProblems("FailedGuardCaptures", captures, NameResolutionRosters.FailedGuardCaptures));
        if (!NameResolutionRosters.Patterns.ContainsKey(nameof(GuardPattern)))
            problems.Add("ReadTimings: roster 7 has no GuardPattern, so the failed guard crosses no roster value");

        AssertNoProblems(problems, "axis 4's timings are named closed, and each crosses its constructs' rosters");
    }

    // =========================================================================================================
    // P77b axis 5: let-in binder × outer
    // =========================================================================================================

    /// <summary>The let-in product's outer axis is the image of <see cref="StoreTargetScope"/> (both directions, each scope mapped
    /// to one binding or out with its reason), its every-family outers are the no-binding outer and the module scope's image, and
    /// its function-scope family is a let-in family of roster 8.</summary>
    [Fact]
    public void LetInOutersRoster_IsTheImageOfStoreTargetScopes()
    {
        var problems = Roster.TotalityProblems("LetInOuters", Enum.GetValues<StoreTargetScope>(), NameResolutionRosters.LetInOuters.Keys).ToList();
        problems.AddRange(Roster.ShapeProblems(NameResolutionRosters.LetInOuters).Select(p => $"LetInOuters {p}"));
        problems.AddRange(NameResolutionRosters.LetInOuters.Where(e => e.Value.Image.Count > 1).Select(e => $"LetInOuters: {e.Key} maps to more than one binding"));
        var image = Roster.ImageOf(NameResolutionRosters.LetInOuters).ToHashSet();
        var storeImage = Roster.ImageOf(NameResolutionRosters.StoreTargetScopes).ToHashSet();
        problems.AddRange(NameResolutionRosters.LetInOuters.Keys.Where(k => !storeImage.Contains(k)).Select(k => $"LetInOuters: {k} is no scope a store targets"));
        var everyFamily = new[] { NameResolutionRosters.LetInNoOuter }.Concat(NameResolutionRosters.LetInOuters[StoreTargetScope.Module].Image);
        problems.AddRange(Roster.TotalityProblems("LetInEveryFamilyOuters", everyFamily, NameResolutionRosters.LetInEveryFamilyOuters));
        problems.AddRange(image.Where(k => k != BindingKind.LocalVar && !(NameResolutionRosters.LocalOuter.TryGetValue(k, out var m) && m.Generated))
            .Select(k => $"LetInOuters: {k} is no generated LocalOuter"));
        if (!NameResolutionRosters.BlockFamilies.TryGetValue(NameResolutionRosters.StoreNestingFamily, out var walk) || !walk.Image.Any(c => c.LetInBody != null))
            problems.Add($"LetInOuters: the function-scope family {NameResolutionRosters.StoreNestingFamily} is no let-in family of roster 8");

        AssertNoProblems(problems, "the let-in product's outers are the image of the scopes a store targets, each other scope naming why");
    }

    // =========================================================================================================
    // P77b axis 6: closure over a loop-BODY binding
    // =========================================================================================================

    /// <summary>Axis 6's binder roster is total over <see cref="NameResolutionRosters.LocalKinds"/>' generated binders (both
    /// directions), its enclosure roster total over roster 8's families, and the R-LG bodies {with, except} are generated.</summary>
    [Fact]
    public void LoopBodyClosureRosters_AreTotalOverLocalKindsAndBlockFamilies()
    {
        var binders = NameResolutionRosters.LocalKinds.Where(e => e.Value.Generated).Select(e => e.Key);
        var problems = Roster.TotalityProblems("LoopBodyClosureBinders", binders, NameResolutionRosters.LoopBodyClosureBinders.Keys).ToList();
        problems.AddRange(MembershipShapeProblems("LoopBodyClosureBinders", NameResolutionRosters.LoopBodyClosureBinders));
        // The families that host a statement body (a let-in construct): the comprehensions, `defer` and the match arm's pattern
        // scope hold no statement a binder could be.
        var statementFamilies = NameResolutionRosters.BlockFamilies.Where(e => e.Value.Image.Any(c => c.LetInBody != null)).Select(e => e.Key);
        problems.AddRange(Roster.TotalityProblems("LoopBodyEnclosures", statementFamilies, NameResolutionRosters.LoopBodyEnclosures.Keys));
        problems.AddRange(MembershipShapeProblems("LoopBodyEnclosures", NameResolutionRosters.LoopBodyEnclosures));
        foreach (var family in new[] { "with", "except" }.Where(f => !(NameResolutionRosters.LoopBodyEnclosures.TryGetValue(f, out var m) && m.Generated)))
            problems.Add($"LoopBodyEnclosures: R-LG's {family} body is not generated");
        foreach (var family in NameResolutionRosters.BackEdgeLoopFamilies.Where(f => NameResolutionRosters.LoopBodyEnclosures.TryGetValue(f, out var m) && m.Generated))
            problems.Add($"LoopBodyEnclosures: the loop family {family} is generated as an enclosure of its own loop body");

        AssertNoProblems(problems, "axis 6 closes over every LocalKinds binder in every R-LG body, each other binder or family naming why");
    }

    // =========================================================================================================
    // P77b axis 7: the host
    // =========================================================================================================

    /// <summary>The host axis is total over <see cref="TypeKind"/>, both directions, each kind generated or out with its reason,
    /// and the class and struct hosts are generated.</summary>
    [Fact]
    public void HostKindsRoster_IsTotalOverTypeKind()
    {
        var problems = Roster.TotalityProblems("HostKinds", Enum.GetValues<TypeKind>(), NameResolutionRosters.HostKinds.Keys).ToList();
        problems.AddRange(MembershipShapeProblems("HostKinds", NameResolutionRosters.HostKinds));
        foreach (var k in new[] { TypeKind.Class, TypeKind.Struct }.Where(k => !(NameResolutionRosters.HostKinds.TryGetValue(k, out var m) && m.Generated)))
            problems.Add($"HostKinds: the {k} host is not generated");

        AssertNoProblems(problems, "every type kind hosts the READ family's body positions or names why not");
    }

    /// <summary>The struct host's positions: every <c>ClassBodies</c> position read directly, and the lambda nestings.</summary>
    private static IEnumerable<(ReadPosition Position, Nesting Nesting)> StructHostPositions() =>
        ClassBodies.Select(p => (p, Nesting.Direct)).Concat(NameResolutionRosters.StructLambdaNestings.Select(p => (p, Nesting.Lambda)));

    /// <summary>Does a body render through a C# closure: a <c>lambda</c>, or a <c>def</c> inside a function body (a C# local
    /// function)? A nested CLASS's method is a method of its own, and a generator or a comprehension is neither.</summary>
    internal static bool RendersThroughCSharpClosure(IEnumerable<string> members)
    {
        var functions = new Stack<int>();
        foreach (var line in members)
        {
            var text = line.TrimStart();
            var indent = line.Length - text.Length;
            while (functions.Count > 0 && indent <= functions.Peek())
                functions.Pop();
            if (text.Contains("lambda", StringComparison.Ordinal))
                return true;
            if (text.StartsWith("def ", StringComparison.Ordinal) && functions.Count > 0)
                return true;
            if (text.StartsWith("def ", StringComparison.Ordinal) || text.StartsWith("property ", StringComparison.Ordinal))
                functions.Push(indent);
            else if (text.StartsWith("class ", StringComparison.Ordinal))
                functions.Clear();
        }
        return false;
    }

    /// <summary>R-KT's closure set is exactly the struct positions <c>PlaceInClass</c> renders <c>self.x</c> through a C# lambda
    /// or local function (both directions), and the comprehension and generator positions are not in it (the positive control:
    /// they take the class arm).</summary>
    [Fact]
    public void StructSelfCaptureClosures_AreThePositionsRenderedThroughACSharpClosure()
    {
        var rendered = StructHostPositions().Where(e => RendersThroughCSharpClosure(PlaceInClass(e.Position, e.Nesting, "self.x").Members)).ToList();
        var problems = Roster.TotalityProblems("StructSelfCaptureClosures", rendered, NameResolutionRosters.StructSelfCaptureClosures).ToList();
        foreach (var p in new[] { ReadPosition.GeneratorBody, ReadPosition.ComprehensionElement, ReadPosition.ComprehensionIterable, ReadPosition.MethodBody, ReadPosition.NestedClassMethodBody })
            if (RendersThroughCSharpClosure(PlaceInClass(p, Nesting.Direct, "self.x").Members))
                problems.Add($"StructSelfCaptureClosures: {p} renders through a closure, and it takes the class arm");
        if (!RendersThroughCSharpClosure(new[] { "def r(self) -> int:", "    def g() -> int:", "        return self.x", "    return g()" }))
            problems.Add("StructSelfCaptureClosures: a def inside a def is not seen as a local function");

        AssertNoProblems(problems, "R-KT refuses exactly what C# refuses: a struct self captured by a lambda or a local function");
    }

    // =========================================================================================================
    // P77b axis 8: the attribute STORE target
    // =========================================================================================================

    private static string LastTypeSegment(TypeSyntax type) => type.ToString().Split('.').Last().Trim();

    /// <summary>The <c>case</c> arms of <paramref name="method"/>'s switch, by bound type name, and whether it has a
    /// <c>default:</c> arm: a <c>case T:</c> constant label and a <c>case T x:</c> / <c>case T { }</c> pattern label alike.</summary>
    internal static (IReadOnlyList<string> Arms, bool HasDefault) ScanSwitchArms(string source, string method)
    {
        var root = ParseCSharp(source, "scan.cs").GetCompilationUnitRoot();
        var declaration = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == method);
        var arms = new List<string>();
        bool hasDefault = false;
        foreach (var label in declaration.DescendantNodes().OfType<SwitchStatementSyntax>().Single().Sections.SelectMany(s => s.Labels))
        {
            switch (label)
            {
                case DefaultSwitchLabelSyntax:
                    hasDefault = true;
                    break;
                case CaseSwitchLabelSyntax { Value: TypeSyntax type }:
                    arms.Add(LastTypeSegment(type));
                    break;
                case CasePatternSwitchLabelSyntax { Pattern: DeclarationPatternSyntax d }:
                    arms.Add(LastTypeSegment(d.Type));
                    break;
                case CasePatternSwitchLabelSyntax { Pattern: TypePatternSyntax t }:
                    arms.Add(LastTypeSegment(t.Type));
                    break;
                case CasePatternSwitchLabelSyntax { Pattern: RecursivePatternSyntax { Type: TypeSyntax r } }:
                    arms.Add(LastTypeSegment(r));
                    break;
                default:
                    arms.Add($"<unread label: {label}>");
                    break;
            }
        }
        return (arms, hasDefault);
    }

    /// <summary>The types the <c>assignment.Target is T</c> sites of <paramref name="source"/> test.</summary>
    internal static IReadOnlyList<string> ScanTargetIsTypes(string source)
    {
        var root = ParseCSharp(source, "scan.cs").GetCompilationUnitRoot();
        var types = new List<string>();
        foreach (var e in root.DescendantNodes().OfType<IsPatternExpressionSyntax>())
        {
            if (e.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Target", Expression: IdentifierNameSyntax { Identifier.ValueText: "assignment" } })
                continue;
            var pattern = e.Pattern is UnaryPatternSyntax u ? u.Pattern : e.Pattern;
            types.Add(pattern switch
            {
                DeclarationPatternSyntax d => LastTypeSegment(d.Type),
                TypePatternSyntax t => LastTypeSegment(t.Type),
                RecursivePatternSyntax { Type: TypeSyntax r } => LastTypeSegment(r),
                ConstantPatternSyntax { Expression: IdentifierNameSyntax n } => n.Identifier.ValueText,
                _ => $"<unread pattern: {pattern}>",
            });
        }
        return types;
    }

    private static string TypeCheckerSource(string file) => File.ReadAllText(Path.Combine(CompilerSourceDir(), "Semantic", file));

    /// <summary>The assignment target shapes are the scan of <see cref="NameResolutionRosters.AssignmentTargetAuthority"/>'s
    /// <c>case</c> arms, both directions, with a <c>default:</c> arm (it makes every other shape SPY0225, so the scan IS the
    /// universe). Positive controls: a synthetic copy of the switch with an extra <c>case Foo:</c> arm is seen as one more arm,
    /// and the <c>assignment.Target is T</c> list of <c>TypeChecker.Statements.cs</c> is a strict subset of the scan.</summary>
    [Fact]
    public void AssignmentTargetShapesRoster_IsTheScanOfFirstInvalidAssignmentTarget()
    {
        var utilities = TypeCheckerSource("TypeChecker.Utilities.cs");
        var (arms, hasDefault) = ScanSwitchArms(utilities, NameResolutionRosters.AssignmentTargetAuthority);
        var problems = new List<string>();
        if (!hasDefault)
            problems.Add($"{NameResolutionRosters.AssignmentTargetAuthority} has no default: arm, so its case arms are not the universe");
        problems.AddRange(arms.GroupBy(a => a).Where(g => g.Count() > 1).Select(g => $"AssignmentTargetShapes: arm {g.Key} scanned twice"));
        problems.AddRange(Roster.TotalityProblems("AssignmentTargetShapes", arms, NameResolutionRosters.AssignmentTargetShapes.Keys));
        problems.AddRange(MembershipShapeProblems("AssignmentTargetShapes", NameResolutionRosters.AssignmentTargetShapes));

        // The scan sees an arm it is shown: the same file with a sixth arm added.
        var synthetic = utilities.Replace("            case Identifier:\n            case MemberAccess:", "            case Foo:\n            case Identifier:\n            case MemberAccess:", StringComparison.Ordinal);
        if (synthetic == utilities)
            problems.Add("the synthetic control could not place its extra arm: the switch's first arms moved");
        var (syntheticArms, _) = ScanSwitchArms(synthetic, NameResolutionRosters.AssignmentTargetAuthority);
        if (syntheticArms.Count != arms.Count + 1 || !syntheticArms.Contains("Foo"))
            problems.Add($"the scan does not see a synthetic extra arm: {string.Join(", ", syntheticArms)}");

        // The partial list beside it: every `assignment.Target is T` site tests a scanned arm, and they do not name them all.
        var targetIs = ScanTargetIsTypes(TypeCheckerSource("TypeChecker.Statements.cs")).ToHashSet(StringComparer.Ordinal);
        if (targetIs.Count == 0)
            problems.Add("no assignment.Target is T site was scanned");
        problems.AddRange(targetIs.Where(t => !arms.Contains(t)).Select(t => $"assignment.Target is {t} names no scanned arm"));
        if (targetIs.SetEquals(arms))
            problems.Add("the assignment.Target is T list names every arm: it is no longer the strict subset the scan is checked against");

        AssertNoProblems(problems, "axis 8's target shapes are the compiler's one assignment-target authority, scanned");
    }

    /// <summary>Axis 8's member and form rosters: the members are total over roster 1's type-level and instance kinds, the forms
    /// over <see cref="StoreForm"/>, each generated or out with its reason.</summary>
    [Fact]
    public void AttributeStoreRosters_AreTotalOverMembersAndForms()
    {
        var problems = Roster.TotalityProblems("AttributeStoreMembers",
            NameResolutionRosters.TypeLevelKinds.Concat(NameResolutionRosters.InstanceKinds), NameResolutionRosters.AttributeStoreMembers.Keys).ToList();
        problems.AddRange(MembershipShapeProblems("AttributeStoreMembers", NameResolutionRosters.AttributeStoreMembers));
        problems.AddRange(Roster.TotalityProblems("AttributeStoreForms", Enum.GetValues<StoreForm>(), NameResolutionRosters.AttributeStoreForms.Keys));
        problems.AddRange(MembershipShapeProblems("AttributeStoreForms", NameResolutionRosters.AttributeStoreForms));
        problems.AddRange(NameResolutionRosters.AttributeStoreFormMembers.Where(k => !NameResolutionRosters.AttributeStoreMembers[k].Generated)
            .Select(k => $"AttributeStoreFormMembers: {k} is not a generated member"));

        AssertNoProblems(problems, "axis 8 stores every member kind in every form a member access admits, each other naming why");
    }

    private static IEnumerable<string> MembershipShapeProblems<TKey>(string axis, IReadOnlyDictionary<TKey, AxisMembership> map) where TKey : notnull
    {
        foreach (var (key, member) in map)
        {
            if (member.Generated == (member.Excluded != null))
                yield return $"{axis}: {key} is generated AND excluded, or neither";
            if (member.Excluded != null && string.IsNullOrWhiteSpace(member.Reason))
                yield return $"{axis}: {key} is excluded with no reason";
        }
    }
}
