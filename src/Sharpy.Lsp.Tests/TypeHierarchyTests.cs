using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests type hierarchy functionality: prepare, supertypes, subtypes,
/// including handler-level integration tests.
/// </summary>
public class TypeHierarchyTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharplyTypeHierarchyPrepareHandler _prepareHandler;
    private readonly SharplyTypeHierarchySupertypesHandler _supertypesHandler;
    private readonly SharplyTypeHierarchySubtypesHandler _subtypesHandler;

    public TypeHierarchyTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _prepareHandler = new SharplyTypeHierarchyPrepareHandler(_languageService, _api);
        _supertypesHandler = new SharplyTypeHierarchySupertypesHandler(_languageService);
        _subtypesHandler = new SharplyTypeHierarchySubtypesHandler(_languageService);
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

    [Fact]
    public async Task PrepareHandler_OnClassDef_ReturnsItemAsync()
    {
        var source = "class Animal:\n    def __init__(self):\n        pass\ndef main():\n    a = Animal()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Cursor on "Animal" class definition (line 1, col 7 → 0-based: line 0, col 6)
        var result = await _prepareHandler.Handle(
            new TypeHierarchyPrepareParams
            {
                TextDocument = new TextDocumentIdentifier("file:///test.spy"),
                Position = new Position(0, 6)
            },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.First().Name.Should().Be("Animal");
        result!.First().Kind.Should().Be(OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind.Class);
    }

    [Fact]
    public async Task SupertypesHandler_ClassWithBase_ReturnsBaseAsync()
    {
        var source = "class Animal:\n    def __init__(self):\n        pass\nclass Dog(Animal):\n    def __init__(self):\n        super().__init__()\ndef main():\n    d = Dog()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Prepare item for Dog
        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        var dogSymbol = analysis!.SymbolTable?.Lookup("Dog") as TypeSymbol;
        dogSymbol.Should().NotBeNull();
        var dogItem = TypeHierarchyHelper.CreateItem(dogSymbol!, "file:///test.spy");
        dogItem.Should().NotBeNull();

        var result = await _supertypesHandler.Handle(
            new TypeHierarchySupertypesParams { Item = dogItem! },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Should().Contain(item => item.Name == "Animal");
    }

    [Fact]
    public async Task SupertypesHandler_RootClass_ReturnsNullAsync()
    {
        var source = "class Root:\n    def __init__(self):\n        pass\ndef main():\n    r = Root()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        var rootSymbol = analysis!.SymbolTable?.Lookup("Root") as TypeSymbol;
        rootSymbol.Should().NotBeNull();
        var rootItem = TypeHierarchyHelper.CreateItem(rootSymbol!, "file:///test.spy");
        rootItem.Should().NotBeNull();

        var result = await _supertypesHandler.Handle(
            new TypeHierarchySupertypesParams { Item = rootItem! },
            CancellationToken.None);

        result.Should().BeNull("root class has no supertypes");
    }

    [Fact]
    public async Task SubtypesHandler_ClassWithChildren_ReturnsSubtypesAsync()
    {
        var source = "class Animal:\n    def __init__(self):\n        pass\nclass Dog(Animal):\n    def __init__(self):\n        super().__init__()\nclass Cat(Animal):\n    def __init__(self):\n        super().__init__()\ndef main():\n    d = Dog()\n    c = Cat()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        var animalSymbol = analysis!.SymbolTable?.Lookup("Animal") as TypeSymbol;
        animalSymbol.Should().NotBeNull();
        var animalItem = TypeHierarchyHelper.CreateItem(animalSymbol!, "file:///test.spy");
        animalItem.Should().NotBeNull();

        var result = await _subtypesHandler.Handle(
            new TypeHierarchySubtypesParams { Item = animalItem! },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Should().HaveCount(2, "Dog and Cat are subtypes of Animal");
    }

    [Fact]
    public async Task SubtypesHandler_LeafClass_ReturnsNullAsync()
    {
        var source = "class Leaf:\n    def __init__(self):\n        pass\ndef main():\n    x = Leaf()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        var leafSymbol = analysis!.SymbolTable?.Lookup("Leaf") as TypeSymbol;
        leafSymbol.Should().NotBeNull();
        var leafItem = TypeHierarchyHelper.CreateItem(leafSymbol!, "file:///test.spy");
        leafItem.Should().NotBeNull();

        var result = await _subtypesHandler.Handle(
            new TypeHierarchySubtypesParams { Item = leafItem! },
            CancellationToken.None);

        result.Should().BeNull("leaf class has no subtypes");
    }

    [Fact]
    public async Task SubtypesHandler_Interface_ReturnsImplementorsAsync()
    {
        var source = "interface Drawable:\n    def draw(self) -> None:\n        ...\nclass Circle(Drawable):\n    def draw(self) -> None:\n        pass\n    def __init__(self):\n        pass\ndef main():\n    c = Circle()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        var drawableSymbol = analysis!.SymbolTable?.Lookup("Drawable") as TypeSymbol;
        drawableSymbol.Should().NotBeNull();
        var drawableItem = TypeHierarchyHelper.CreateItem(drawableSymbol!, "file:///test.spy");
        drawableItem.Should().NotBeNull();

        var result = await _subtypesHandler.Handle(
            new TypeHierarchySubtypesParams { Item = drawableItem! },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Should().Contain(item => item.Name == "Circle");
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
