using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Refactoring;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Refactoring;

public class InlineProviderTests
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
            source);
        return await provider.GetCodeActionsAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task InlineVariable_LiteralInitializer_ReturnsAction()
    {
        var source = "def main():\n    x: int = 42\n    print(x)";
        var provider = new InlineProvider();

        // Cursor on the variable declaration (0-based line 1, char 4)
        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var inlineAction = actions.FirstOrDefault(a =>
            a.Title.Contains("Inline variable") && a.Title.Contains("x"));
        inlineAction.Should().NotBeNull();
        inlineAction!.Kind.Should().Be(CodeActionKind.RefactorInline);
        inlineAction.Edit.Should().NotBeNull();
    }

    [Fact]
    public async Task InlineVariable_NoDeclaration_ReturnsNoAction()
    {
        var source = "def main():\n    print(42)";
        var provider = new InlineProvider();

        // Cursor on print statement, no variable declaration
        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var inlineVarAction = actions.FirstOrDefault(a => a.Title.Contains("Inline variable"));
        inlineVarAction.Should().BeNull();
    }

    [Fact]
    public async Task InlineVariable_NullSource_ReturnsNoAction()
    {
        var provider = new InlineProvider();
        var context = new CodeActionProviderContext(
            TestUri,
            new LspRange(new Position(0, 0), new Position(0, 0)),
            new Container<Diagnostic>(),
            null,
            null);
        var actions = await provider.GetCodeActionsAsync(context, CancellationToken.None);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task InlineVariable_FunctionCallInitializer_ReturnsNoAction()
    {
        // Function call initializer has side effects, so should not be offered
        var source = "def get_value() -> int:\n    return 42\n\ndef main():\n    x: int = get_value()\n    print(x)";
        var provider = new InlineProvider();

        // Cursor on the x variable declaration (0-based line 4, char 4)
        var range = new LspRange(new Position(4, 4), new Position(4, 4));
        var actions = await GetActionsAsync(provider, source, range);

        // Should not offer inline because get_value() may have side effects
        var inlineVarAction = actions.FirstOrDefault(a => a.Title.Contains("Inline variable"));
        inlineVarAction.Should().BeNull();
    }

    [Fact]
    public async Task InlineVariable_ReassignedVariable_ReturnsNoAction()
    {
        // Variable is reassigned, so should not be inlined
        var source = "def main():\n    x: int = 1\n    x = 2\n    print(x)";
        var provider = new InlineProvider();

        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var inlineVarAction = actions.FirstOrDefault(a => a.Title.Contains("Inline variable"));
        inlineVarAction.Should().BeNull();
    }

    [Fact]
    public async Task InlineVariable_ArithmeticInitializer_ReturnsAction()
    {
        // Arithmetic expression initializer is side-effect free, so should offer inline
        var source = "def main():\n    x: int = 2 + 3\n    print(x)";
        var provider = new InlineProvider();

        // Cursor on the variable declaration
        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var inlineAction = actions.FirstOrDefault(a =>
            a.Title.Contains("Inline variable") && a.Title.Contains("x"));
        inlineAction.Should().NotBeNull();
        inlineAction!.Kind.Should().Be(CodeActionKind.RefactorInline);
        inlineAction.Edit.Should().NotBeNull();
    }

    [Fact]
    public async Task InlineFunction_SingleExpressionBody_ReturnsAction()
    {
        var source = "def double(n: int) -> int:\n    return n * 2\n\ndef main():\n    print(double(5))";
        var provider = new InlineProvider();

        // Cursor on the function call double(5) - line 4, inside the print() call
        // The FunctionCall for double(5) starts at column 11 (0-based: 10) on line 5 (0-based: 4)
        var range = new LspRange(new Position(4, 10), new Position(4, 10));
        var actions = await GetActionsAsync(provider, source, range);

        var inlineAction = actions.FirstOrDefault(a =>
            a.Title.Contains("Inline function") && a.Title.Contains("double"));
        inlineAction.Should().NotBeNull();
        inlineAction!.Kind.Should().Be(CodeActionKind.RefactorInline);
        inlineAction.Edit.Should().NotBeNull();
    }

    [Fact]
    public async Task InlineFunction_MultipleCallSites_ReturnsNoAction()
    {
        // Function called twice — should not be offered for inlining
        var source = "def double(n: int) -> int:\n    return n * 2\n\ndef main():\n    print(double(5))\n    print(double(10))";
        var provider = new InlineProvider();

        // Cursor on first call to double
        var range = new LspRange(new Position(4, 10), new Position(4, 10));
        var actions = await GetActionsAsync(provider, source, range);

        var inlineAction = actions.FirstOrDefault(a => a.Title.Contains("Inline function"));
        inlineAction.Should().BeNull();
    }

    [Fact]
    public async Task InlineFunction_RecursiveFunction_ReturnsNoAction()
    {
        // Recursive function — should not be offered for inlining
        var source = "def factorial(n: int) -> int:\n    if n <= 1:\n        return 1\n    return n * factorial(n - 1)\n\ndef main():\n    print(factorial(5))";
        var provider = new InlineProvider();

        // Cursor on the call to factorial in main
        var range = new LspRange(new Position(6, 10), new Position(6, 10));
        var actions = await GetActionsAsync(provider, source, range);

        var inlineAction = actions.FirstOrDefault(a => a.Title.Contains("Inline function"));
        inlineAction.Should().BeNull();
    }

    [Fact]
    public async Task InlineFunction_ParameterSubstitution_ReturnsCorrectEdit()
    {
        // Inline a single-expression function with a parameter
        var source = "def add_one(n: int) -> int:\n    return n + 1\n\ndef main():\n    print(add_one(5))";
        var provider = new InlineProvider();

        // Cursor on the function call add_one(5)
        var range = new LspRange(new Position(4, 10), new Position(4, 10));
        var actions = await GetActionsAsync(provider, source, range);

        var inlineAction = actions.FirstOrDefault(a =>
            a.Title.Contains("Inline function") && a.Title.Contains("add_one"));
        inlineAction.Should().NotBeNull();
        inlineAction!.Kind.Should().Be(CodeActionKind.RefactorInline);
        inlineAction.Edit.Should().NotBeNull();

        // Verify the edit substitutes the parameter 'n' with the argument '5'
        var edits = inlineAction.Edit!.Changes![TestUri].ToList();
        edits.Should().NotBeEmpty();
        var replacementEdit = edits.FirstOrDefault(e => e.NewText.Contains("5"));
        replacementEdit.Should().NotBeNull();
    }
}
