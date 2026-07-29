using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Unit tests for <see cref="ModuleExports"/>, the type that owns a module's value-position
/// exports and its types-only view as one unit (#1145 item 1).
///
/// <para>
/// The contract under test is that the types view is <b>derived</b>, never maintained by hand:
/// exporting a <see cref="TypeSymbol"/> files it in both views, exporting a value leaves the types
/// view alone (so <c>sqlite3.Row</c>'s field cannot shadow the <c>Row</c> type in annotation
/// position — #1092), and a merge moves both views under one first-import-wins rule (#1135).
/// </para>
/// </summary>
public class ModuleExportsTests
{
    private static TypeSymbol MakeType(string name) =>
        new() { Name = name, Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

    private static VariableSymbol MakeVariable(string name) =>
        new() { Name = name, Kind = SymbolKind.Variable };

    private static ModuleSymbol MakeModule(string name) =>
        new() { Name = name, Kind = SymbolKind.Module };

    [Fact]
    public void Add_TypeSymbol_FilesItInBothViews()
    {
        var exports = new ModuleExports();
        var row = MakeType("Row");

        exports.Add("Row", row);

        exports["Row"].Should().BeSameAs(row);
        exports.TryGetType("Row", out var type).Should().BeTrue();
        type.Should().BeSameAs(row);
    }

    [Fact]
    public void Add_ValueSymbol_LeavesTypesViewEmpty()
    {
        var exports = new ModuleExports();

        exports.Add("row_factory", MakeVariable("row_factory"));

        exports.ContainsKey("row_factory").Should().BeTrue();
        exports.Types.Should().BeEmpty();
    }

    [Fact]
    public void Add_ValueOverAType_ShadowsTheValueViewOnly()
    {
        // The sqlite3.Row shape: the field is exported after the type under the same name.
        var exports = new ModuleExports();
        var rowType = MakeType("Row");
        var rowField = MakeVariable("Row");

        exports.Add("Row", rowType);
        exports.Add("Row", rowField);

        exports["Row"].Should().BeSameAs(rowField, "the value view takes the last export");
        exports.TryGetType("Row", out var type).Should().BeTrue(
            "annotation position must still resolve the type (#1092)");
        type.Should().BeSameAs(rowType);
    }

    [Fact]
    public void Add_TypeOverAType_ReplacesBothViews()
    {
        var exports = new ModuleExports();
        var first = MakeType("Row");
        var second = MakeType("Row");

        exports.Add("Row", first);
        exports.Add("Row", second);

        exports["Row"].Should().BeSameAs(second);
        exports.TryGetType("Row", out var type).Should().BeTrue();
        type.Should().BeSameAs(second);
    }

    [Fact]
    public void TypesView_CannotBeMutatedThroughACast()
    {
        var exports = new ModuleExports();

        var writable = (IDictionary<string, TypeSymbol>)exports.Types;

        writable.Invoking(d => d.Add("Row", MakeType("Row")))
            .Should().Throw<NotSupportedException>(
                "the types view is a read-only wrapper — the only way in is Add/MergeFrom on the unit");
    }

    [Fact]
    public void CopyConstructor_CopiesBothViews_AndIsIndependent()
    {
        var source = new ModuleExports();
        var rowType = MakeType("Row");
        source.Add("Row", rowType);
        source.Add("Row", MakeVariable("Row"));
        source.Add("connect", MakeVariable("connect"));

        var copy = new ModuleExports(source);

        copy.Count.Should().Be(2);
        copy.TryGetType("Row", out var copiedType).Should().BeTrue();
        copiedType.Should().BeSameAs(rowType, "exported symbols are shared, only the lookups are copied");

        copy.Add("extra", MakeVariable("extra"));
        source.ContainsKey("extra").Should().BeFalse();
    }

    [Fact]
    public void MergeFrom_FirstImportWins_KeepsTheValue_ButGainsTheMirroredType()
    {
        // #1135's regression shape: the target already exports a value named "Row"; the source
        // contributes the Row type. The value must survive, the type must become resolvable.
        var target = new ModuleExports();
        var existingValue = MakeVariable("Row");
        target.Add("Row", existingValue);

        var source = new ModuleExports();
        var rowType = MakeType("Row");
        source.Add("Row", rowType);

        target.MergeFrom(source, firstImportWins: true);

        target["Row"].Should().BeSameAs(existingValue);
        target.TryGetType("Row", out var type).Should().BeTrue();
        type.Should().BeSameAs(rowType);
    }

    [Fact]
    public void MergeFrom_DisjointKeys_MergesBothViewsTogether()
    {
        var target = new ModuleExports();
        var existingValue = MakeVariable("existing");
        var existingType = MakeType("Existing");
        target.Add("existing", existingValue);
        target.Add("Existing", existingType);

        var source = new ModuleExports();
        var newValue = MakeVariable("added");
        var newType = MakeType("Added");
        source.Add("added", newValue);
        source.Add("Added", newType);

        target.MergeFrom(source, firstImportWins: true);

        target["existing"].Should().BeSameAs(existingValue);
        target["added"].Should().BeSameAs(newValue);
        target.Types.Should().HaveCount(2);
        target.Types["Existing"].Should().BeSameAs(existingType);
        target.Types["Added"].Should().BeSameAs(newType);
    }

    [Fact]
    public void MergeFrom_TypeKeyAlreadyPresent_KeepsTheFirstImport()
    {
        var target = new ModuleExports();
        var targetType = MakeType("Row");
        target.Add("Row", targetType);

        var source = new ModuleExports();
        source.Add("Row", MakeType("Row"));

        target.MergeFrom(source, firstImportWins: true);

        target.Types["Row"].Should().BeSameAs(targetType);
        target["Row"].Should().BeSameAs(targetType, "both views keep the first import atomically");
    }

    [Fact]
    public void MergeFrom_NestedModules_MergesRecursively_CarryingNestedTypes()
    {
        // "import lib.math" seen twice: the roots collide, so the nested modules must merge
        // instead of one replacing the other — and the nested types must ride along.
        var targetNested = MakeModule("math");
        var target = new ModuleExports();
        target.Add("math", targetNested);

        var sourceNested = MakeModule("math");
        var vectorType = MakeType("Vector");
        sourceNested.Exports.Add("Vector", vectorType);
        var source = new ModuleExports();
        source.Add("math", sourceNested);

        target.MergeFrom(source, firstImportWins: true);

        target["math"].Should().BeSameAs(targetNested, "recursion merges into the existing module");
        targetNested.Exports.TryGetType("Vector", out var nestedType).Should().BeTrue();
        nestedType.Should().BeSameAs(vectorType);
    }

    [Fact]
    public void MergeFrom_LastImportWins_ReplacesBothViews()
    {
        var target = new ModuleExports();
        target.Add("Row", MakeType("Row"));

        var source = new ModuleExports();
        var sourceType = MakeType("Row");
        source.Add("Row", sourceType);

        target.MergeFrom(source, firstImportWins: false);

        target["Row"].Should().BeSameAs(sourceType);
        target.Types["Row"].Should().BeSameAs(sourceType);
    }

    [Fact]
    public void Enumeration_IsStable_AndMatchesTheValueView()
    {
        var exports = new ModuleExports();
        exports.Add("alpha", MakeVariable("alpha"));
        exports.Add("Beta", MakeType("Beta"));
        exports.Add("gamma", MakeVariable("gamma"));

        var first = exports.Select(kvp => kvp.Key).ToList();
        var second = exports.Select(kvp => kvp.Key).ToList();

        second.Should().Equal(first);
        first.Should().BeEquivalentTo(new[] { "alpha", "Beta", "gamma" });
        exports.Keys.Should().BeEquivalentTo(first);
        exports.Count.Should().Be(3);
        exports.Values.Should().HaveCount(3);
    }

    [Fact]
    public void ContainsType_TracksTheTypesViewOnly()
    {
        var exports = new ModuleExports();
        exports.Add("connect", MakeVariable("connect"));
        exports.Add("Row", MakeType("Row"));

        exports.ContainsType("Row").Should().BeTrue();
        exports.ContainsType("connect").Should().BeFalse();
        exports.ContainsKey("connect").Should().BeTrue();
        exports.TryGetType("connect", out _).Should().BeFalse();
    }
}
