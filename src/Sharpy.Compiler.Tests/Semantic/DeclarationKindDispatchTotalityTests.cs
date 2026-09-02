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
///   - Nested-type extractors: {ClassDef, StructDef, InterfaceDef, EnumDef}
///   - Type-name refusal: above + {UnionDef, DelegateDef, TypeAlias}
///   - Base-carrying kinds: {ClassDef, StructDef, InterfaceDef}
///   - Generator-attributed: {ClassDef, FunctionDef, StructDef}
///   - Module-level classification: the full ResolveDeclaration set + imports
/// </summary>
public class DeclarationKindDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public DeclarationKindDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    // --- Nested-type universe (the 4 kinds that can appear as nested type declarations) ---
    private static readonly HashSet<string> NestedTypeUniverse = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
    };

    // --- Type-name universe (kinds that introduce a named type, subject to shadowing checks) ---
    private static readonly HashSet<string> TypeNameUniverse = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(TypeAlias),
    };

    // --- Base-carrying kinds (kinds with a base-class/interface list) ---
    private static readonly HashSet<string> BaseCarryingKinds = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
    };

    // --- Generator-attributed kinds (kinds that carry source-generator decorators) ---
    private static readonly HashSet<string> GeneratorAttributedKinds = new()
    {
        nameof(ClassDef),
        nameof(FunctionDef),
        nameof(StructDef),
    };

    // --- Module-level classification (IsModuleLevelStatement arms) ---
    private static readonly HashSet<string> ModuleLevelKinds = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(PropertyDef),
        nameof(TypeAlias),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(VariableDeclaration),
    };

    // --- Declaration-name kinds (GetDeclarationName arms) ---
    private static readonly HashSet<string> DeclarationNameKinds = new()
    {
        nameof(ClassDef),
        nameof(FunctionDef),
        nameof(StructDef),
    };

    // ═══════════════════════════════════════════════════════════════════════
    // ExtractNestedTypes — ModuleLoader
    // Reason for subset: only the 4 type-declaring kinds that can nest inside a class/struct/interface body.
    // UnionDef/DelegateDef/TypeAlias/EventDef are not extracted as nested types today.
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
