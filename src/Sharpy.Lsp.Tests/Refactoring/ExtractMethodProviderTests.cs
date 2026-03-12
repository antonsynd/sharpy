using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Refactoring;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Refactoring;

public class ExtractMethodProviderTests
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
    public async Task ExtractMethod_StatementsSelected_ReturnsAction()
    {
        var source = "def main():\n    x: int = 1\n    y: int = 2\n    print(x + y)";
        var provider = new ExtractMethodProvider();

        // Select the first two statements (lines 1-2, 0-based)
        var range = new LspRange(new Position(1, 0), new Position(2, 14));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().NotBeEmpty();
        var action = actions[0];
        action.Title.Should().Be("Extract method");
        action.Kind.Should().Be(CodeActionKind.RefactorExtract);
        action.Edit.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractMethod_SelectionWithReturn_ReturnsNoAction()
    {
        var source = "def main() -> int:\n    x: int = 42\n    return x";
        var provider = new ExtractMethodProvider();

        // Select the return statement
        var range = new LspRange(new Position(2, 0), new Position(2, 12));
        var actions = await GetActionsAsync(provider, source, range);

        // Should not offer extract method because selection contains return
        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractMethod_NoSelection_ReturnsNoAction()
    {
        var source = "def main():\n    x: int = 1\n    print(x)";
        var provider = new ExtractMethodProvider();

        // Zero-width cursor (no selection)
        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractMethod_NullAst_ReturnsNoAction()
    {
        var provider = new ExtractMethodProvider();
        var context = new CodeActionProviderContext(
            TestUri,
            new LspRange(new Position(0, 0), new Position(0, 5)),
            new Container<Diagnostic>(),
            null,
            null,
            _api);
        var actions = await provider.GetCodeActionsAsync(context, CancellationToken.None);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractMethod_SelectionOutsideStatements_ReturnsNoAction()
    {
        var source = "def main():\n    x: int = 1\n    print(x)";
        var provider = new ExtractMethodProvider();

        // Select an area that does not contain full statements (e.g., comment area)
        var range = new LspRange(new Position(0, 0), new Position(0, 3));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractMethod_SelectionWithBreak_ReturnsNoAction()
    {
        var source = "def main():\n    for i in range(10):\n        if i == 5:\n            break\n        print(i)";
        var provider = new ExtractMethodProvider();

        // Select the if/break statement inside the loop
        var range = new LspRange(new Position(2, 0), new Position(3, 17));
        var actions = await GetActionsAsync(provider, source, range);

        // Should not offer extract method because selection contains break
        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractMethod_SelectionWithContinue_ReturnsNoAction()
    {
        var source = "def main():\n    for i in range(10):\n        if i == 5:\n            continue\n        print(i)";
        var provider = new ExtractMethodProvider();

        // Select the if/continue statement inside the loop
        var range = new LspRange(new Position(2, 0), new Position(3, 20));
        var actions = await GetActionsAsync(provider, source, range);

        // Should not offer extract method because selection contains continue
        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractMethod_InsideClassMethod_ReturnsAction()
    {
        var source = "class Calculator:\n    def compute(self) -> int:\n        x: int = 10\n        y: int = 20\n        return x + y\n\ndef main():\n    c: Calculator = Calculator()\n    print(c.compute())";
        var provider = new ExtractMethodProvider();

        // Select the two assignment statements inside the class method
        var range = new LspRange(new Position(2, 0), new Position(3, 18));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().NotBeEmpty();
        var action = actions[0];
        action.Title.Should().Be("Extract method");
        action.Kind.Should().Be(CodeActionKind.RefactorExtract);
        action.Edit.Should().NotBeNull();
    }
}
