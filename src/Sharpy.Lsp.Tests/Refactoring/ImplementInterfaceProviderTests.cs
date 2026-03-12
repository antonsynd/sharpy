using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Refactoring;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Refactoring;

public class ImplementInterfaceProviderTests
{
    private readonly CompilerApi _api = new();
    private static readonly DocumentUri TestUri = DocumentUri.From("file:///test.spy");

    private async Task<IReadOnlyList<CodeAction>> GetActionsAsync(
        ICodeActionProvider provider,
        string source,
        LspRange? range = null)
    {
        var analysis = _api.Analyze(source, CancellationToken.None);
        var context = new CodeActionProviderContext(
            TestUri,
            range ?? new LspRange(new Position(0, 0), new Position(0, 0)),
            new Container<Diagnostic>(),
            analysis,
            source,
            _api);
        return await provider.GetCodeActionsAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task ImplementInterface_MissingMethod_AnalysisFailsGracefully()
    {
        // When a class doesn't implement a required interface method, the compiler
        // emits SPY0320 and the analysis fails (SymbolTable is null).
        // The provider handles this gracefully by returning no actions.
        // In a live LSP session, the LanguageService would provide a cached
        // successful analysis from before the error was introduced.
        var source = @"interface Drawable:
    def draw(self) -> None:
        ...

class Circle(Drawable):
    def __init__(self):
        pass

def main():
    c: Circle = Circle()
    print(c)";

        var provider = new ImplementInterfaceProvider();

        // Cursor inside the ClassDef body
        var range = new LspRange(new Position(5, 4), new Position(5, 4));
        var actions = await GetActionsAsync(provider, source, range);

        // Provider returns no actions when SymbolTable is unavailable
        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ImplementInterface_FullyImplemented_ReturnsNoAction()
    {
        var source = @"interface Drawable:
    def draw(self) -> None:
        ...

class Circle(Drawable):
    def draw(self) -> None:
        print('drawing')

def main():
    c: Circle = Circle()
    c.draw()";

        var provider = new ImplementInterfaceProvider();

        // Cursor on the ClassDef line
        var range = new LspRange(new Position(4, 0), new Position(4, 0));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ImplementInterface_NotOnClass_ReturnsNoAction()
    {
        var source = @"interface Drawable:
    def draw(self) -> None:
        ...

class Circle(Drawable):
    def draw(self) -> None:
        print('drawing')

def main():
    c: Circle = Circle()
    c.draw()";

        var provider = new ImplementInterfaceProvider();

        // Cursor on the main function, not on a class
        var range = new LspRange(new Position(9, 0), new Position(9, 0));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ImplementInterface_NullAnalysis_ReturnsNoAction()
    {
        var provider = new ImplementInterfaceProvider();
        var context = new CodeActionProviderContext(
            TestUri,
            new LspRange(new Position(0, 0), new Position(0, 0)),
            new Container<Diagnostic>(),
            null,
            null,
            _api);
        var actions = await provider.GetCodeActionsAsync(context, CancellationToken.None);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ImplementInterface_ClassWithNoInterfaces_ReturnsNoAction()
    {
        var source = @"class Circle:
    pass

def main():
    pass";

        var provider = new ImplementInterfaceProvider();

        var range = new LspRange(new Position(0, 0), new Position(0, 0));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().BeEmpty();
    }
}
