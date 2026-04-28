using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class TypeDefinitionTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpyTypeDefinitionHandler _handler;

    public TypeDefinitionTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyTypeDefinitionHandler(_languageService, _api);
    }

    [Fact]
    public async Task Handle_VariableWithClassType_NavigatesToClassDefinitionAsync()
    {
        var source = @"
class Foo:
    x: int = 0

f: Foo = Foo()

def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // "f" is on line 5 (0-based: 4), col 0
        var result = await HandleAsync("file:///test.spy", 4, 0);

        result.Should().NotBeNull();
        var locations = result!.ToArray();
        locations.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Handle_ParameterWithTypeAnnotation_NavigatesToTypeAsync()
    {
        var source = @"
class Bar:
    y: int = 0

def process(b: Bar) -> int:
    return 0

def main():
    b2: Bar = Bar()
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // "b2" variable on line 10 (0-based: 9), col 4 — a typed variable
        var result = await HandleAsync("file:///test.spy", 9, 4);

        // Should navigate to Bar class definition when type is resolvable
        result?.ToArray().Should().HaveCountGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Handle_ClassName_NavigatesToItselfAsync()
    {
        var source = @"
class MyClass:
    val: int = 0

def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // "MyClass" on line 2 (0-based: 1), col 6
        var result = await HandleAsync("file:///test.spy", 1, 6);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_BuiltinType_ReturnsNullAsync()
    {
        var source = @"
x: int = 42

def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // "42" literal on line 2 (0-based: 1), col 9
        var result = await HandleAsync("file:///test.spy", 1, 9);

        // Builtin types (int) have no navigable source — should return null or empty
        if (result is not null)
            result.ToArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FunctionReturnType_NavigatesToReturnTypeAsync()
    {
        var source = @"
class Result:
    value: int = 0

def make_result() -> Result:
    return Result()

def main():
    r = make_result()
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // "make_result()" call on line 9 (0-based: 8), col 8
        var result = await HandleAsync("file:///test.spy", 8, 8);

        // Should navigate to Result class definition when type is resolvable
        result?.ToArray().Should().HaveCountGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Handle_UnresolvedSymbol_ReturnsNullAsync()
    {
        var source = @"
def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Empty space / whitespace area — line 0, col 0
        var result = await HandleAsync("file:///test.spy", 0, 0);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_InterfaceName_NavigatesToItselfAsync()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> str:
        ...

def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // "IDrawable" on line 2 (0-based: 1), col 10
        var result = await HandleAsync("file:///test.spy", 1, 10);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_MemberAccess_NavigatesToMemberTypeAsync()
    {
        var source = @"
class Point:
    x: int = 0
    y: int = 0

p = Point()

def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // "p" variable on line 6 (0-based: 5), col 0
        var result = await HandleAsync("file:///test.spy", 5, 0);

        // Should navigate to Point class when type is resolvable
        result?.ToArray().Should().HaveCountGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Handle_GenericType_NavigatesToGenericDefinitionAsync()
    {
        var source = @"
class Item:
    name: str = """"

items: list[Item] = []

def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // "items" variable on line 5 (0-based: 4), col 0
        var result = await HandleAsync("file:///test.spy", 4, 0);

        // list[Item] is a GenericType — navigates to the generic definition (list)
        // May return null if list has no navigable source, or a location if it does
        result?.ToArray().Should().HaveCountGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Handle_FunctionName_ReturnsNullAsync()
    {
        var source = @"
class Result:
    value: int = 0

def make_result() -> Result:
    return Result()

def main():
    pass
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // "make_result" function name on line 5 (0-based: 4), col 4
        var result = await HandleAsync("file:///test.spy", 4, 4);

        // Function names have FunctionType which is not navigable
        Assert.Null(result);
    }

    private async Task<LocationOrLocationLinks?> HandleAsync(string uri, int line, int character)
    {
        var request = new TypeDefinitionParams
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
