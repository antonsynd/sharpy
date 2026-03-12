using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Refactoring;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Refactoring;

public class ExtractVariableProviderTests
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
    public async Task ExtractVariable_ExpressionSelected_ReturnsAction()
    {
        // Source: print(1 + 2) where we select "1 + 2"
        var source = "def main():\n    print(1 + 2)";
        var provider = new ExtractVariableProvider();

        // Select the "1 + 2" expression
        // In the source, "1 + 2" is at line 1 (0-based), characters 10-15
        var range = new LspRange(new Position(1, 10), new Position(1, 15));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().NotBeEmpty();
        var action = actions[0];
        action.Kind.Should().Be(CodeActionKind.RefactorExtract);
        action.Title.Should().Contain("Extract variable");
        action.Edit.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractVariable_NoSelection_ReturnsNoAction()
    {
        var source = "def main():\n    print(1 + 2)";
        var provider = new ExtractVariableProvider();

        // Zero-width cursor (no selection)
        var range = new LspRange(new Position(1, 10), new Position(1, 10));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractVariable_SelectionNotExpression_ReturnsNoAction()
    {
        var source = "def main():\n    print(1 + 2)";
        var provider = new ExtractVariableProvider();

        // Select something that won't match an expression (e.g., the def keyword)
        var range = new LspRange(new Position(0, 0), new Position(0, 3));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractVariable_TrivialIdentifier_ReturnsNoAction()
    {
        var source = "def main():\n    x: int = 42\n    print(x)";
        var provider = new ExtractVariableProvider();

        // Select just the identifier 'x' — trivial, not worth extracting
        var range = new LspRange(new Position(2, 10), new Position(2, 11));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractVariable_NullSource_ReturnsNoAction()
    {
        var provider = new ExtractVariableProvider();
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
    public async Task ExtractVariable_StringLiteral_ReturnsAction()
    {
        // String literals are not considered trivial (unlike identifiers/booleans)
        var source = "def main():\n    print(\"hello world\")";
        var provider = new ExtractVariableProvider();

        // Select the string literal
        var range = new LspRange(new Position(1, 10), new Position(1, 23));
        var actions = await GetActionsAsync(provider, source, range);

        // String literals are valid expressions for extraction
        actions.Should().NotBeEmpty();
    }
}
