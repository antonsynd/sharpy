using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Family guard for dispatch sites over declaration-kind <see cref="Statement"/> subtypes.
/// The authority is <c>NameResolver.ResolveDeclaration</c> (already guarded by
/// <see cref="NameResolverDeclarationsTotalityTests"/>). Each member site's arms
/// are pinned as a stated subset of the authority's handled set, with reasons
/// for any strict subset. A new declaration kind fails all sites at once.
///
/// Sub-families:
///   - Nested-type extractors: {ClassDef, StructDef, InterfaceDef, EnumDef, UnionDef, DelegateDef, TypeAlias}
///   - Type-name refusal: the same seven kinds
///   - Base-carrying kinds: {ClassDef, StructDef, InterfaceDef}
///   - Generator-attributed: {ClassDef, FunctionDef, StructDef}
///   - Module-level classification: the full ResolveDeclaration set + imports
///   - Abstract-member scan (class body): {FunctionDef, PropertyDef, EventDef} + ClassDef recursion
/// </summary>
public class DeclarationKindDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public DeclarationKindDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    // ═══════════════════════════════════════════════════════════════════════
    // The authority's handled set, READ FROM THE PRODUCTION SWITCH: every member universe below
    // is `AuthorityKinds − {excluded kind → reason} ∪ {extra arms}` (Design Decision 4), so a
    // kind that ResolveDeclaration starts handling reaches EVERY member fact at once — each site
    // then either grows an arm or is given an exclusion with a reason. A stale exclusion (a kind
    // the authority no longer handles) fails ExcludedKinds_AreAuthorityKinds.
    // ═══════════════════════════════════════════════════════════════════════
    private const string AuthorityFile = "src/Sharpy.Compiler/Semantic/NameResolver.Declarations.cs";
    private const string AuthorityMethod = "ResolveDeclaration";

    private static readonly IReadOnlySet<string> AuthorityKinds =
        SwitchArmScan.CaseTypeNames(AuthorityFile, AuthorityMethod);

    private static HashSet<string> Derived(IReadOnlyDictionary<string, string> excludedWithReason, params string[] extraArms)
    {
        var kinds = new HashSet<string>(AuthorityKinds);
        foreach (var excluded in excludedWithReason.Keys)
            kinds.Remove(excluded);
        foreach (var extra in extraArms)
            kinds.Add(extra);
        return kinds;
    }

    private const string MemberNotType = "a member declaration, not a type declaration";

    // --- Nested-type universe (the 7 type-declaring kinds extracted as nested declarations) ---
    // Union, delegate and alias JOINED this universe when #1729 landed: nested union/delegate are
    // TypeSymbols in NestedTypes and a nested alias is a TypeAliasSymbol in NestedTypeAliases, all
    // resolved through the one NestedDeclaration classifier on both the single-file and import
    // routes. The three former #1729 exclusion rows drained here (three rows out); the base-carrying
    // roster gained matching exclusions (three rows in) because base-carrying is a strict subset.
    private static readonly Dictionary<string, string> NotNestedTypes = new()
    {
        [nameof(FunctionDef)] = MemberNotType,
        [nameof(PropertyDef)] = MemberNotType,
        [nameof(VariableDeclaration)] = MemberNotType,
    };
    private static readonly HashSet<string> NestedTypeUniverse = Derived(NotNestedTypes);

    // --- Type-name universe (kinds that introduce a named type, subject to shadowing checks) ---
    private static readonly Dictionary<string, string> NotTypeNames = new()
    {
        [nameof(FunctionDef)] = "does not introduce a type name",
        [nameof(PropertyDef)] = "does not introduce a type name",
        [nameof(VariableDeclaration)] = "does not introduce a type name",
    };
    private static readonly HashSet<string> TypeNameUniverse = Derived(NotTypeNames);

    // --- Base-carrying kinds (kinds with a base-class/interface list) ---
    private static readonly Dictionary<string, string> NotBaseCarrying = new(NotNestedTypes)
    {
        [nameof(EnumDef)] = "has no base list",
        // Union, delegate and alias are nested-type kinds (#1729) but carry no base-class/interface
        // list the #1535 refusal checks — a union declares cases, a delegate a signature, an alias a
        // target — so IntegrateGeneratedSource does not scan them. These three rows moved here when
        // the #1729 rows drained out of NotNestedTypes (Design Decision 4: three rows out, three in).
        [nameof(UnionDef)] = "has no base list",
        [nameof(DelegateDef)] = "has no base list",
        [nameof(TypeAlias)] = "has no base list",
    };
    private static readonly HashSet<string> BaseCarryingKinds = Derived(NotBaseCarrying);

    // --- Generator-attributed kinds (kinds that carry source-generator decorators) ---
    private static readonly Dictionary<string, string> NotGeneratorAttributed = new()
    {
        [nameof(InterfaceDef)] = "source-generator decorators are not recognized on this kind",
        [nameof(EnumDef)] = "source-generator decorators are not recognized on this kind",
        [nameof(UnionDef)] = "source-generator decorators are not recognized on this kind",
        [nameof(DelegateDef)] = "source-generator decorators are not recognized on this kind",
        [nameof(TypeAlias)] = "source-generator decorators are not recognized on this kind",
        [nameof(PropertyDef)] = "source-generator decorators are not recognized on this kind",
        [nameof(VariableDeclaration)] = "source-generator decorators are not recognized on this kind",
    };
    private static readonly HashSet<string> GeneratorAttributedKinds = Derived(NotGeneratorAttributed);

    // --- Module-level classification (IsModuleLevelStatement arms): every authority kind plus
    //     the import statements, which are module-level but are not declarations the resolver
    //     handles. Exposed (not private) so ModuleExportTotalityTests (#1674, #1906, P5.1) can
    //     compare it against ModuleLoader's export switches without re-deriving a second copy of
    //     "every module-level declaration kind" that could drift from this one.
    internal static readonly HashSet<string> ModuleLevelKinds = Derived(
        new Dictionary<string, string>(),
        nameof(ImportStatement),
        nameof(FromImportStatement));

    // --- Declaration-name kinds (GetDeclarationName arms) ---
    private static readonly Dictionary<string, string> NotGeneratorNamed = new()
    {
        [nameof(InterfaceDef)] = "the generator framework names only class/function/struct; default 'Unknown'",
        [nameof(EnumDef)] = "the generator framework names only class/function/struct; default 'Unknown'",
        [nameof(UnionDef)] = "the generator framework names only class/function/struct; default 'Unknown'",
        [nameof(DelegateDef)] = "the generator framework names only class/function/struct; default 'Unknown'",
        [nameof(TypeAlias)] = "the generator framework names only class/function/struct; default 'Unknown'",
        [nameof(PropertyDef)] = "the generator framework names only class/function/struct; default 'Unknown'",
        [nameof(VariableDeclaration)] = "the generator framework names only class/function/struct; default 'Unknown'",
    };
    private static readonly HashSet<string> DeclarationNameKinds = Derived(NotGeneratorNamed);

    // --- Abstract-member scan (AbstractMemberValidator.ValidateClass arms) ---
    // The three class-body member kinds on which `@abstract` is recognized (EventDef is a
    // class-member kind the authority does not resolve, so it is an extra arm), plus ClassDef for
    // the nested-class recursion (#1461).
    private static readonly Dictionary<string, string> NotAbstractScanned = new()
    {
        [nameof(StructDef)] = "nested struct body not recursed by AbstractMemberValidator",
        [nameof(InterfaceDef)] = "nested interface body not recursed; @abstract on interfaces refused by DecoratorValidator.InvalidOnInterface",
        [nameof(EnumDef)] = "nested enum body not recursed",
        [nameof(UnionDef)] = "not a class-body member the validator scans",
        [nameof(DelegateDef)] = "not a class-body member the validator scans",
        [nameof(TypeAlias)] = "not a class-body member the validator scans",
        [nameof(VariableDeclaration)] = "@abstract is not recognized on fields",
    };
    private static readonly HashSet<string> AbstractMemberScanKinds = Derived(NotAbstractScanned, nameof(EventDef));

    private static readonly (string Name, IReadOnlyDictionary<string, string> Excluded)[] AllExclusions =
    {
        (nameof(NotNestedTypes), NotNestedTypes),
        (nameof(NotTypeNames), NotTypeNames),
        (nameof(NotBaseCarrying), NotBaseCarrying),
        (nameof(NotGeneratorAttributed), NotGeneratorAttributed),
        (nameof(NotGeneratorNamed), NotGeneratorNamed),
        (nameof(NotAbstractScanned), NotAbstractScanned),
    };

    [Fact]
    public void AuthorityKinds_AreReadFromResolveDeclaration()
    {
        _output.WriteLine($"{AuthorityMethod} arms ({AuthorityKinds.Count}): {string.Join(", ", AuthorityKinds.OrderBy(k => k, StringComparer.Ordinal))}");
        Assert.NotEmpty(AuthorityKinds);
        Assert.Contains(nameof(ClassDef), AuthorityKinds);
        Assert.Contains(nameof(FunctionDef), AuthorityKinds);
    }

    [Fact]
    public void ExcludedKinds_AreAuthorityKinds()
    {
        var stale = AllExclusions
            .SelectMany(e => e.Excluded.Keys.Where(k => !AuthorityKinds.Contains(k)).Select(k => $"{e.Name}: {k}"))
            .ToList();
        Assert.True(stale.Count == 0,
            $"Exclusions naming kinds the authority no longer handles (drain them):\n  {string.Join("\n  ", stale)}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ExtractNestedTypes — ModuleLoader
    // The seven type-declaring kinds are extracted as nested declarations (#1729 landed): the six
    // that produce a TypeSymbol (class/struct/interface/enum/union/delegate) into NestedTypes, and
    // TypeAlias into the enclosing symbol's NestedTypeAliases. The switch has a case arm per kind so
    // the scan sees all seven; a new declaration kind fails this pin for every extractor at once.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExtractNestedTypes_Arms_MatchNestedTypeUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/ModuleLoader.cs",
            "ExtractNestedTypes");
        Assert.NotEmpty(arms);
        _output.WriteLine($"ExtractNestedTypes arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(NestedTypeUniverse),
            $"Arms differ from nested-type universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(NestedTypeUniverse))}\n" +
            $"  Missing: {string.Join(", ", NestedTypeUniverse.Except(arms))}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TypeDeclarationOf — NestedTypeIndex
    // Same nested-type universe as ExtractNestedTypes (mirror).
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TypeDeclarationOf_Arms_MatchNestedTypeUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/Validation/NestedTypeIndex.cs",
            "TypeDeclarationOf");
        Assert.NotEmpty(arms);
        _output.WriteLine($"TypeDeclarationOf arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(NestedTypeUniverse),
            $"Arms differ from nested-type universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(NestedTypeUniverse))}\n" +
            $"  Missing: {string.Join(", ", NestedTypeUniverse.Except(arms))}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GetAccessLevel — NameResolver
    // Same nested-type universe: only types that can be nested carry access decorators.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAccessLevel_Arms_MatchNestedTypeUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/NameResolver.Declarations.cs",
            "GetAccessLevel");
        Assert.NotEmpty(arms);
        _output.WriteLine($"GetAccessLevel arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(NestedTypeUniverse),
            $"Arms differ from nested-type universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(NestedTypeUniverse))}\n" +
            $"  Missing: {string.Join(", ", NestedTypeUniverse.Except(arms))}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RefuseBuiltinTypeNameShadowing — NameResolver
    // Type-name universe: all kinds that introduce a named type (SPY0212 check).
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void RefuseBuiltinTypeNameShadowing_Arms_MatchTypeNameUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/NameResolver.Declarations.cs",
            "RefuseBuiltinTypeNameShadowing");
        Assert.NotEmpty(arms);
        _output.WriteLine($"RefuseBuiltinTypeNameShadowing arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(TypeNameUniverse),
            $"Arms differ from type-name universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(TypeNameUniverse))}\n" +
            $"  Missing: {string.Join(", ", TypeNameUniverse.Except(arms))}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IntegrateGeneratedSource — ProjectCompiler
    // Base-carrying kinds: only kinds with a BaseClasses/BaseInterfaces property
    // are checked for the #1535 refusal. EnumDef has no base list.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void IntegrateGeneratedSource_Arms_MatchBaseCarryingKinds()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Project/ProjectCompiler.Generators.cs",
            "IntegrateGeneratedSource");
        Assert.NotEmpty(arms);
        _output.WriteLine($"IntegrateGeneratedSource arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(BaseCarryingKinds),
            $"Arms differ from base-carrying kinds.\n" +
            $"  Extra: {string.Join(", ", arms.Except(BaseCarryingKinds))}\n" +
            $"  Missing: {string.Join(", ", BaseCarryingKinds.Except(arms))}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DetectGeneratorAttributes — TypeChecker
    // Generator-attributed kinds: ClassDef, FunctionDef, StructDef.
    // Reason for subset: only these kinds are checked by the type-checker for
    // source-generator decorators; other kinds do not support generators.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void DetectGeneratorAttributes_Arms_MatchGeneratorAttributedKinds()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs",
            "DetectGeneratorAttributes");
        Assert.NotEmpty(arms);
        _output.WriteLine($"DetectGeneratorAttributes arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(GeneratorAttributedKinds),
            $"Arms differ from generator-attributed kinds.\n" +
            $"  Extra: {string.Join(", ", arms.Except(GeneratorAttributedKinds))}\n" +
            $"  Missing: {string.Join(", ", GeneratorAttributedKinds.Except(arms))}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GetDeclarationName — ProjectCompiler
    // Narrow subset: only the kinds the generator framework names today.
    // The default returns "Unknown" — conservative.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetDeclarationName_Arms_MatchDeclarationNameKinds()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Project/ProjectCompiler.Generators.cs",
            "GetDeclarationName");
        Assert.NotEmpty(arms);
        _output.WriteLine($"GetDeclarationName arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(DeclarationNameKinds),
            $"Arms differ from declaration-name kinds.\n" +
            $"  Extra: {string.Join(", ", arms.Except(DeclarationNameKinds))}\n" +
            $"  Missing: {string.Join(", ", DeclarationNameKinds.Except(arms))}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IsModuleLevelStatement — ReplSession
    // Widest set: all declaration/import kinds are module-level.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void IsModuleLevelStatement_Arms_MatchModuleLevelKinds()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Services/ReplSession.cs",
            "IsModuleLevelStatement");
        Assert.NotEmpty(arms);
        _output.WriteLine($"IsModuleLevelStatement arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(ModuleLevelKinds),
            $"Arms differ from module-level kinds.\n" +
            $"  Extra: {string.Join(", ", arms.Except(ModuleLevelKinds))}\n" +
            $"  Missing: {string.Join(", ", ModuleLevelKinds.Except(arms))}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ValidateClass — AbstractMemberValidator (verify-round finding P4.2 / D cells)
    // Stated subset: the switch walks a ClassDef body and reports `@abstract` on the three
    // member kinds that carry the decorator (FunctionDef, PropertyDef, EventDef — the
    // validator's #1307 contract: "methods, properties, events"), and recurses through
    // ClassDef so nested classes are reached (#1461). Nested StructDef / InterfaceDef /
    // EnumDef bodies are NOT recursed by this validator; DecoratorValidator.InvalidOnInterface
    // refuses `@abstract` on interfaces separately. Its intersection with the nested-type
    // universe is therefore exactly {ClassDef} — pinned below so a recursion arm added for
    // another nested kind, or the class recursion dropped, is a visible universe change.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateClass_Arms_MatchAbstractMemberScanKinds()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/Validation/AbstractMemberValidator.cs",
            "ValidateClass");
        Assert.NotEmpty(arms);
        _output.WriteLine($"ValidateClass arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(AbstractMemberScanKinds),
            $"Arms differ from abstract-member scan kinds.\n" +
            $"  Extra: {string.Join(", ", arms.Except(AbstractMemberScanKinds))}\n" +
            $"  Missing: {string.Join(", ", AbstractMemberScanKinds.Except(arms))}");
    }

    [Fact]
    public void AbstractMemberScanKinds_RecurseOnlyThroughClassDef()
    {
        var recursed = AbstractMemberScanKinds.Intersect(NestedTypeUniverse).ToList();
        Assert.True(recursed.Count == 1 && recursed[0] == nameof(ClassDef),
            $"AbstractMemberValidator recurses through exactly {{ClassDef}} of the nested-type universe; " +
            $"got {{{string.Join(", ", recursed)}}}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Cross-family consistency
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void NestedTypeUniverse_IsSubsetOf_TypeNameUniverse()
    {
        var extra = NestedTypeUniverse.Except(TypeNameUniverse).ToList();
        Assert.True(extra.Count == 0,
            $"Nested-type kinds not in type-name universe: {string.Join(", ", extra)}");
    }

    [Fact]
    public void BaseCarryingKinds_IsSubsetOf_NestedTypeUniverse()
    {
        var extra = BaseCarryingKinds.Except(NestedTypeUniverse).ToList();
        Assert.True(extra.Count == 0,
            $"Base-carrying kinds not in nested-type universe: {string.Join(", ", extra)}");
    }

    [Fact]
    public void TypeNameUniverse_IsSubsetOf_ModuleLevelKinds()
    {
        var extra = TypeNameUniverse.Except(ModuleLevelKinds).ToList();
        Assert.True(extra.Count == 0,
            $"Type-name kinds not in module-level kinds: {string.Join(", ", extra)}");
    }
}
