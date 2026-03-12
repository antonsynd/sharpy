using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests for go-to-implementation functionality.
/// Verifies that the implementation handler correctly finds implementing classes,
/// derived classes, and method overrides.
/// </summary>
public class ImplementationTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharplyImplementationHandler _handler;

    public ImplementationTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharplyImplementationHandler(_languageService, _api);
    }

    [Fact]
    public async Task Handle_OnInterface_ReturnsImplementingClassesAsync()
    {
        var source = @"
interface IAnimal:
    def speak(self) -> str:
        ...

class Dog(IAnimal):
    def speak(self) -> str:
        return ""woof""

class Cat(IAnimal):
    def speak(self) -> str:
        return ""meow""

def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Go-to-implementation on "IAnimal" (line 2, col 11 → 0-based: line 1, col 10)
        var result = await HandleAsync("file:///test.spy", 1, 10);

        result.Should().NotBeNull();
        var locations = result!.ToArray();
        locations.Should().HaveCountGreaterThanOrEqualTo(2,
            "Dog and Cat implement IAnimal");
    }

    [Fact]
    public async Task Handle_OnAbstractClass_ReturnsDerivedClassesAsync()
    {
        var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        ...

class Circle(Shape):
    radius: float
    def __init__(self, radius: float):
        self.radius = radius
    @override
    def area(self) -> float:
        return 3.14 * self.radius * self.radius

def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Go-to-implementation on "Shape" (line 3, col 7 → 0-based: line 2, col 6)
        var result = await HandleAsync("file:///test.spy", 2, 6);

        result.Should().NotBeNull();
        var locations = result!.ToArray();
        locations.Should().HaveCountGreaterThanOrEqualTo(1,
            "Circle derives from Shape");
    }

    [Fact]
    public async Task Handle_OnAbstractMethod_ReturnsOverridesAsync()
    {
        var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        ...

class Circle(Shape):
    radius: float
    def __init__(self, radius: float):
        self.radius = radius
    @override
    def area(self) -> float:
        return 3.14 * self.radius * self.radius

def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Go-to-implementation on "area" in Shape (line 5, col 9 → 0-based: line 4, col 8)
        var result = await HandleAsync("file:///test.spy", 4, 8);

        result.Should().NotBeNull();
        var locations = result!.ToArray();
        locations.Should().HaveCountGreaterThanOrEqualTo(1,
            "Circle.area overrides Shape.area");
    }

    [Fact]
    public async Task Handle_OnVirtualMethod_ReturnsOverridesAsync()
    {
        var source = @"
class Base:
    @virtual
    def greet(self) -> str:
        return ""hello""

class Derived(Base):
    @override
    def greet(self) -> str:
        return ""hi""

def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Go-to-implementation on "greet" in Base (line 4, col 9 → 0-based: line 3, col 8)
        var result = await HandleAsync("file:///test.spy", 3, 8);

        result.Should().NotBeNull();
        var locations = result!.ToArray();
        locations.Should().HaveCountGreaterThanOrEqualTo(1,
            "Derived.greet overrides Base.greet");
    }

    [Fact]
    public async Task Handle_OnConcreteClass_ReturnsDefinitionAsync()
    {
        var source = @"
class Point:
    x: int
    y: int
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main():
    p = Point(1, 2)
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Go-to-implementation on "Point" (line 2, col 7 → 0-based: line 1, col 6)
        var result = await HandleAsync("file:///test.spy", 1, 6);

        result.Should().NotBeNull();
        var locations = result!.ToArray();
        locations.Should().HaveCount(1,
            "concrete class returns its own definition");
    }

    [Fact]
    public async Task Handle_OnNonSymbol_ReturnsNullAsync()
    {
        var source = "def main():\n    x: int = 42\n    print(x)";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Click on a numeric literal — not a symbol
        var result = await HandleAsync("file:///test.spy", 1, 15);

        result.Should().BeNull();
    }

    private async Task<LocationOrLocationLinks?> HandleAsync(string uri, int line, int character)
    {
        var request = new ImplementationParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Position = new Position(line, character)
        };
        return await _handler.Handle(request, CancellationToken.None);
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
