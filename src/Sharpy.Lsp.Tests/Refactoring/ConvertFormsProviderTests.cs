using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Refactoring;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Refactoring;

public class ConvertFormsProviderTests
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
    public async Task AddTypeAnnotation_AssignmentWithoutDeclaration_ReturnsNoAction()
    {
        // In Sharpy, "x = 42" is parsed as Assignment, not VariableDeclaration.
        // VariableDeclaration always has a type annotation (e.g., "x: int = 42").
        // So "Add type annotation" is never offered for plain assignments.
        var source = "def main():\n    x = 42\n    print(x)";
        var provider = new ConvertFormsProvider();

        // Cursor on the assignment (0-based line 1, character 4)
        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var addTypeAction = actions.FirstOrDefault(a => a.Title.Contains("Add type annotation"));
        addTypeAction.Should().BeNull();
    }

    [Fact]
    public async Task RemoveTypeAnnotation_VariableWithType_ReturnsAction()
    {
        // Variable with explicit type annotation
        var source = "def main():\n    x: int = 42\n    print(x)";
        var provider = new ConvertFormsProvider();

        // Cursor on the variable declaration
        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var removeTypeAction = actions.FirstOrDefault(a => a.Title.Contains("Remove type annotation"));
        removeTypeAction.Should().NotBeNull();
        removeTypeAction!.Kind.Should().Be(CodeActionKind.Refactor);
    }

    [Fact]
    public async Task WrapInTryExcept_StatementsSelected_ReturnsAction()
    {
        var source = "def main():\n    x: int = 42\n    print(x)";
        var provider = new ConvertFormsProvider();

        // Select both statements
        var range = new LspRange(new Position(1, 0), new Position(2, 12));
        var actions = await GetActionsAsync(provider, source, range);

        var wrapAction = actions.FirstOrDefault(a => a.Title.Contains("Wrap in try/except"));
        wrapAction.Should().NotBeNull();
        wrapAction!.Kind.Should().Be(CodeActionKind.Refactor);
    }

    [Fact]
    public async Task WrapInTryExcept_NoSelection_ReturnsNoWrapAction()
    {
        var source = "def main():\n    x: int = 42\n    print(x)";
        var provider = new ConvertFormsProvider();

        // Zero-width cursor
        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var wrapAction = actions.FirstOrDefault(a => a.Title.Contains("Wrap in try/except"));
        wrapAction.Should().BeNull();
    }

    [Fact]
    public async Task AddTypeAnnotation_VariableAlreadyHasType_ReturnsNoAddAction()
    {
        var source = "def main():\n    x: int = 42\n    print(x)";
        var provider = new ConvertFormsProvider();

        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        // Should NOT offer "Add type annotation" because it already has one
        var addTypeAction = actions.FirstOrDefault(a => a.Title.Contains("Add type annotation"));
        addTypeAction.Should().BeNull();
    }

    [Fact]
    public async Task RemoveTypeAnnotation_VariableWithoutType_ReturnsNoRemoveAction()
    {
        var source = "def main():\n    x = 42\n    print(x)";
        var provider = new ConvertFormsProvider();

        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        // Should NOT offer "Remove type annotation" because there's none
        var removeTypeAction = actions.FirstOrDefault(a => a.Title.Contains("Remove type annotation"));
        removeTypeAction.Should().BeNull();
    }

    [Fact]
    public async Task NullAnalysis_ReturnsNoAction()
    {
        var provider = new ConvertFormsProvider();
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
}
