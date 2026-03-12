using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests type hierarchy functionality: prepare, supertypes, and subtypes.
/// </summary>
public class TypeHierarchyTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;

    public TypeHierarchyTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
    }

    [Fact]
    public async Task Prepare_Class_ResolvesTypeSymbol()
    {
        var source = "class Animal:\n    def __init__(self):\n        pass\ndef main():\n    a = Animal()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("Animal");
        symbol.Should().NotBeNull();
        symbol.Should().BeOfType<TypeSymbol>();
        symbol!.DeclarationSpan.Should().NotBeNull();
    }

    [Fact]
    public async Task Prepare_Interface_ResolvesTypeSymbol()
    {
        var source = "interface Drawable:\n    def draw(self) -> None:\n        ...\nclass Impl(Drawable):\n    def draw(self) -> None:\n        pass\n    def __init__(self):\n        pass\ndef main():\n    d = Impl()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("Drawable");
        symbol.Should().NotBeNull();
        symbol.Should().BeOfType<TypeSymbol>();

        var ts = (TypeSymbol)symbol!;
        ts.TypeKind.Should().Be(TypeKind.Interface);
    }

    [Fact]
    public async Task TypeHierarchyIndex_Build_RecordsBaseClass()
    {
        var source = "class Animal:\n    def __init__(self):\n        pass\nclass Dog(Animal):\n    def __init__(self):\n        super().__init__()\ndef main():\n    d = Dog()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        var index = TypeHierarchyIndex.Build(analysis.SymbolTable!);

        var animal = analysis.SymbolTable!.Lookup("Animal") as TypeSymbol;
        var dog = analysis.SymbolTable!.Lookup("Dog") as TypeSymbol;
        animal.Should().NotBeNull();
        dog.Should().NotBeNull();

        // Dog is a subtype of Animal
        var subtypes = index.GetDirectSubtypes(animal!);
        subtypes.Should().Contain(dog!);

        // Animal is a supertype of Dog
        var supertypes = index.GetDirectSupertypes(dog!);
        supertypes.Should().Contain(animal);
    }

    [Fact]
    public async Task TypeHierarchyIndex_Build_RecordsInterface()
    {
        var source = "interface Drawable:\n    def draw(self) -> None:\n        ...\nclass Circle(Drawable):\n    def draw(self) -> None:\n        pass\n    def __init__(self):\n        pass\ndef main():\n    c = Circle()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        var index = TypeHierarchyIndex.Build(analysis.SymbolTable!);

        var drawable = analysis.SymbolTable!.Lookup("Drawable") as TypeSymbol;
        var circle = analysis.SymbolTable!.Lookup("Circle") as TypeSymbol;
        drawable.Should().NotBeNull();
        circle.Should().NotBeNull();

        // Circle implements Drawable
        var subtypes = index.GetDirectSubtypes(drawable!);
        subtypes.Should().Contain(circle!);
    }

    [Fact]
    public async Task TypeHierarchyIndex_GetDirectSubtypes_ReturnsEmptyForLeafType()
    {
        var source = "class Leaf:\n    def __init__(self):\n        pass\ndef main():\n    x = Leaf()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        var index = TypeHierarchyIndex.Build(analysis.SymbolTable!);

        var leaf = analysis.SymbolTable!.Lookup("Leaf") as TypeSymbol;
        leaf.Should().NotBeNull();

        index.GetDirectSubtypes(leaf!).Should().BeEmpty();
    }

    [Fact]
    public async Task TypeHierarchyIndex_GetDirectSupertypes_ReturnsEmptyForRootType()
    {
        var source = "class Root:\n    def __init__(self):\n        pass\ndef main():\n    x = Root()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        var index = TypeHierarchyIndex.Build(analysis.SymbolTable!);

        var root = analysis.SymbolTable!.Lookup("Root") as TypeSymbol;
        root.Should().NotBeNull();

        index.GetDirectSupertypes(root!).Should().BeEmpty();
    }

    [Fact]
    public async Task TypeHierarchyIndex_MultipleSubtypes()
    {
        var source = "class Animal:\n    def __init__(self):\n        pass\nclass Dog(Animal):\n    def __init__(self):\n        super().__init__()\nclass Cat(Animal):\n    def __init__(self):\n        super().__init__()\ndef main():\n    d = Dog()\n    c = Cat()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        var index = TypeHierarchyIndex.Build(analysis.SymbolTable!);

        var animal = analysis.SymbolTable!.Lookup("Animal") as TypeSymbol;
        animal.Should().NotBeNull();

        var subtypes = index.GetDirectSubtypes(animal!);
        subtypes.Should().HaveCount(2);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
