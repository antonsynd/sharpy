using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Seam-level tests for <see cref="ProjectCompiler.MergeModuleExports"/>, which combines nested
/// module structures when the same root module is imported more than once.
///
/// Regression guard for #1135: the merge must carry the types-only
/// <see cref="ModuleSymbol.ExportedTypes"/> mirror as well as <see cref="ModuleSymbol.Exports"/>.
/// Dropping the mirror breaks annotation-position type resolution, which consults
/// <see cref="ModuleSymbolExtensions.TryGetExportedType"/> BEFORE <c>Exports</c> (#1092), so a
/// type whose name is also taken by a value-position export (e.g. <c>sqlite3.Row</c>) would fail
/// to resolve after a merge.
/// </summary>
public class ProjectCompilerMergeModuleExportsTests
{
    private static TypeSymbol MakeType(string name) =>
        new() { Name = name, Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

    private static VariableSymbol MakeVariable(string name) =>
        new() { Name = name, Kind = SymbolKind.Variable };

    private static ModuleSymbol MakeModule(string name) =>
        new() { Name = name, Kind = SymbolKind.Module };

    [Fact]
    public void Merge_CollisionShape_KeepsExportsFirstImportWins_ButMirrorsExportedTypes()
    {
        // Mirrors the sqlite3.Row shape: a value-position export ("row_factory"-style) already
        // occupies the "Row" key in Exports, while the source module contributes both the Row
        // TypeSymbol (as a value export) AND its types-only mirror entry.
        var existingValue = MakeVariable("Row");
        var target = MakeModule("sqlite3");
        target.Exports["Row"] = existingValue;

        var rowType = MakeType("Row");
        var source = MakeModule("sqlite3");
        source.Exports["Row"] = rowType;
        source.ExportedTypes["Row"] = rowType;

        ProjectCompiler.MergeModuleExports(target, source);

        // Exports keeps first-import-wins: the value symbol is not overwritten by the type.
        Assert.Same(existingValue, target.Exports["Row"]);

        // The types-only mirror is merged, so annotation-position resolution still finds the type.
        Assert.Same(rowType, target.ExportedTypes["Row"]);
        Assert.True(target.TryGetExportedType("Row", out var resolved));
        Assert.Same(rowType, resolved);
    }

    [Fact]
    public void Merge_DisjointKeys_MergesIntoBothDictionaries()
    {
        var target = MakeModule("pkg");
        var existingValue = MakeVariable("existing");
        var existingType = MakeType("Existing");
        target.Exports["existing"] = existingValue;
        target.ExportedTypes["Existing"] = existingType;

        var newValue = MakeVariable("added");
        var newType = MakeType("Added");
        var source = MakeModule("pkg");
        source.Exports["added"] = newValue;
        source.ExportedTypes["Added"] = newType;

        ProjectCompiler.MergeModuleExports(target, source);

        Assert.Same(existingValue, target.Exports["existing"]);
        Assert.Same(newValue, target.Exports["added"]);
        Assert.Same(existingType, target.ExportedTypes["Existing"]);
        Assert.Same(newType, target.ExportedTypes["Added"]);
    }

    [Fact]
    public void Merge_ExportedTypeKeyAlreadyPresent_IsNotOverwritten()
    {
        var target = MakeModule("pkg");
        var targetType = MakeType("Row");
        target.ExportedTypes["Row"] = targetType;

        var sourceType = MakeType("Row");
        var source = MakeModule("pkg");
        source.ExportedTypes["Row"] = sourceType;

        ProjectCompiler.MergeModuleExports(target, source);

        // First-import-wins for the mirror too: the pre-existing type is retained.
        Assert.Same(targetType, target.ExportedTypes["Row"]);
    }

    [Fact]
    public void Merge_NestedModuleRecursion_CarriesNestedExportedTypes()
    {
        // Root "lib" already has a nested "math" module (no exports yet). The source contributes
        // the same nested path, whose "math" module carries an ExportedTypes entry. The recursive
        // Exports branch must merge into the existing nested module, carrying its mirror through.
        var target = MakeModule("lib");
        var targetNested = MakeModule("math");
        target.Exports["math"] = targetNested;

        var sourceNested = MakeModule("math");
        var vectorType = MakeType("Vector");
        sourceNested.ExportedTypes["Vector"] = vectorType;
        var source = MakeModule("lib");
        source.Exports["math"] = sourceNested;

        ProjectCompiler.MergeModuleExports(target, source);

        // The existing nested module is preserved (recursion merges into it, not replaces it)...
        Assert.Same(targetNested, target.Exports["math"]);
        // ...and its types-only mirror gained the nested Vector type via recursion.
        var mergedNested = Assert.IsType<ModuleSymbol>(target.Exports["math"]);
        Assert.Same(vectorType, mergedNested.ExportedTypes["Vector"]);
        Assert.True(mergedNested.TryGetExportedType("Vector", out var resolved));
        Assert.Same(vectorType, resolved);
    }
}
