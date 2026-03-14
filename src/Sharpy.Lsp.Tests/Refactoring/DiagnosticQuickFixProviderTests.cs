using FluentAssertions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Lsp.Refactoring;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Refactoring;

public class DiagnosticQuickFixProviderTests
{
    private static readonly DocumentUri TestUri = DocumentUri.From("file:///test.spy");
    private readonly DiagnosticQuickFixProvider _provider = new();

    private static Diagnostic MakeDiagnostic(string code, LspRange range, JToken? data = null)
    {
        return new Diagnostic
        {
            Code = code,
            Range = range,
            Message = $"Test diagnostic {code}",
            Data = data
        };
    }

    private async Task<IReadOnlyList<CodeAction>> GetActionsAsync(
        Container<Diagnostic> diagnostics,
        string? sourceText = null)
    {
        var context = new CodeActionProviderContext(
            TestUri,
            new LspRange(new Position(0, 0), new Position(0, 0)),
            diagnostics,
            null,
            sourceText);
        return await _provider.GetCodeActionsAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task UnusedImport_ReturnsRemoveImportAction()
    {
        var source = "import math\n\ndef main():\n    pass";
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.UnusedImport,
            new LspRange(new Position(0, 0), new Position(0, 11)));

        var actions = await GetActionsAsync(new Container<Diagnostic>(diag), source);

        actions.Should().HaveCount(1);
        var action = actions[0];
        action.Title.Should().Be("Remove unused import");
        action.Kind.Should().Be(CodeActionKind.QuickFix);
        action.Edit.Should().NotBeNull();
        action.Edit!.Changes.Should().ContainKey(TestUri);

        var edits = action.Edit.Changes![TestUri].ToList();
        edits.Should().HaveCount(1);
        edits[0].NewText.Should().BeEmpty();
        // Should delete the entire line (line 0 to line 1)
        edits[0].Range.Start.Line.Should().Be(0);
        edits[0].Range.Start.Character.Should().Be(0);
        edits[0].Range.End.Line.Should().Be(1);
        edits[0].Range.End.Character.Should().Be(0);
    }

    [Fact]
    public async Task UnusedImport_WithoutSourceText_FallsBackToDiagnosticRange()
    {
        var diagRange = new LspRange(new Position(0, 0), new Position(0, 11));
        var diag = MakeDiagnostic(DiagnosticCodes.Validation.UnusedImport, diagRange);

        var actions = await GetActionsAsync(new Container<Diagnostic>(diag), sourceText: null);

        actions.Should().HaveCount(1);
        var edits = actions[0].Edit!.Changes![TestUri].ToList();
        edits[0].Range.Should().Be(diagRange);
    }

    [Fact]
    public async Task UnusedVariable_ReturnsPrefixUnderscoreAction()
    {
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.UnusedVariable,
            new LspRange(new Position(1, 4), new Position(1, 5)));

        var actions = await GetActionsAsync(new Container<Diagnostic>(diag));

        actions.Should().HaveCount(1);
        var action = actions[0];
        action.Title.Should().Contain("Prefix with '_'");
        action.Kind.Should().Be(CodeActionKind.QuickFix);
        action.Edit.Should().NotBeNull();

        var edits = action.Edit!.Changes![TestUri].ToList();
        edits.Should().HaveCount(1);
        edits[0].NewText.Should().Be("_");
        // Should insert at start of variable name (zero-width range)
        edits[0].Range.Start.Should().Be(new Position(1, 4));
        edits[0].Range.End.Should().Be(new Position(1, 4));
    }

    [Fact]
    public async Task NamingConvention_WithSuggestedName_ReturnsRenameAction()
    {
        var data = JObject.FromObject(new { suggestedName = "my_variable" });
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.NamingConventionWarning,
            new LspRange(new Position(1, 4), new Position(1, 14)),
            data);

        var actions = await GetActionsAsync(new Container<Diagnostic>(diag));

        actions.Should().HaveCount(1);
        var action = actions[0];
        action.Title.Should().Be("Rename to 'my_variable'");
        action.Kind.Should().Be(CodeActionKind.QuickFix);

        var edits = action.Edit!.Changes![TestUri].ToList();
        edits.Should().HaveCount(1);
        edits[0].NewText.Should().Be("my_variable");
    }

    [Fact]
    public async Task NamingConvention_WithoutSuggestedName_ReturnsNoAction()
    {
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.NamingConventionWarning,
            new LspRange(new Position(1, 4), new Position(1, 14)));

        var actions = await GetActionsAsync(new Container<Diagnostic>(diag));

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task NamingConvention_WithEmptySuggestedName_ReturnsNoAction()
    {
        var data = JObject.FromObject(new { suggestedName = "" });
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.NamingConventionWarning,
            new LspRange(new Position(1, 4), new Position(1, 14)),
            data);

        var actions = await GetActionsAsync(new Container<Diagnostic>(diag));

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task UnsupportedDiagnosticCode_ReturnsNoAction()
    {
        var diag = MakeDiagnostic(
            DiagnosticCodes.Semantic.TypeMismatch,
            new LspRange(new Position(0, 0), new Position(0, 5)));

        var actions = await GetActionsAsync(new Container<Diagnostic>(diag));

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task NoDiagnostics_ReturnsNoAction()
    {
        var actions = await GetActionsAsync(new Container<Diagnostic>());

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleDiagnostics_ReturnsActionsForEachSupported()
    {
        var diag1 = MakeDiagnostic(
            DiagnosticCodes.Validation.UnusedImport,
            new LspRange(new Position(0, 0), new Position(0, 11)));
        var diag2 = MakeDiagnostic(
            DiagnosticCodes.Validation.UnusedVariable,
            new LspRange(new Position(1, 4), new Position(1, 5)));
        var diag3 = MakeDiagnostic(
            DiagnosticCodes.Semantic.TypeMismatch,
            new LspRange(new Position(2, 0), new Position(2, 10)));

        var source = "import math\nx = 1\nprint(x)";
        var actions = await GetActionsAsync(
            new Container<Diagnostic>(diag1, diag2, diag3), source);

        // 2 supported diagnostics → 2 actions (TypeMismatch not handled)
        actions.Should().HaveCount(2);
        actions.Should().Contain(a => a.Title == "Remove unused import");
        actions.Should().Contain(a => a.Title.Contains("Prefix with '_'"));
    }

    [Fact]
    public async Task DiagnosticWithNullCode_IsSkipped()
    {
        var diag = new Diagnostic
        {
            Code = null,
            Range = new LspRange(new Position(0, 0), new Position(0, 5)),
            Message = "Some diagnostic"
        };

        var actions = await GetActionsAsync(new Container<Diagnostic>(diag));

        actions.Should().BeEmpty();
    }
}
