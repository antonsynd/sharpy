using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Lsp.Handlers;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests for code action generation by SharplyCodeActionHandler.
/// Verifies quick fixes for unused variables, unused imports, and naming conventions.
/// </summary>
public class CodeActionTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;
    private readonly SharplyCodeActionHandler _handler;
    private static readonly DocumentUri TestUri = DocumentUri.From("file:///test.spy");

    public CodeActionTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
        _handler = new SharplyCodeActionHandler(_workspace);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private async Task<CommandOrCodeActionContainer?> GetCodeActionsAsync(
        string documentText,
        params Diagnostic[] diagnostics)
    {
        _workspace.OpenDocument(TestUri.ToString(), documentText, 1);

        var request = new CodeActionParams
        {
            TextDocument = new TextDocumentIdentifier(TestUri),
            Range = new LspRange(new Position(0, 0), new Position(0, 0)),
            Context = new CodeActionContext
            {
                Diagnostics = new Container<Diagnostic>(diagnostics)
            }
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    private static Diagnostic MakeDiagnostic(
        string code,
        string message,
        int line,
        int startCol,
        int endCol,
        JToken? data = null)
    {
        return new Diagnostic
        {
            Code = new DiagnosticCode(code),
            Message = message,
            Range = new LspRange(
                new Position(line, startCol),
                new Position(line, endCol)),
            Severity = DiagnosticSeverity.Warning,
            Source = "sharpy",
            Data = data
        };
    }

    #region Unused variable (SPY0451)

    [Fact]
    public async Task UnusedVariable_CreatesPrefixUnderscoreAction()
    {
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.UnusedVariable,
            "Variable 'x' is assigned but never used",
            line: 1, startCol: 4, endCol: 5);

        var result = await GetCodeActionsAsync("def main():\n    x: int = 1\n    print(1)", diag);

        result.Should().NotBeNull();
        var actions = result!.ToList();
        actions.Should().ContainSingle();

        var action = actions[0].CodeAction;
        action.Should().NotBeNull();
        action!.Title.Should().Be("Prefix with '_' to mark as intentionally unused");
        action.Kind.Should().Be(CodeActionKind.QuickFix);

        // The edit should insert "_" at the start of the variable name
        var changes = action.Edit!.Changes!;
        changes.Should().ContainKey(TestUri);
        var edits = changes[TestUri].ToList();
        edits.Should().ContainSingle();
        edits[0].NewText.Should().Be("_");
        // Insert at the start position (zero-width range)
        edits[0].Range.Start.Should().Be(edits[0].Range.End);
    }

    #endregion

    #region Unused import (SPY0452)

    [Fact]
    public async Task UnusedImport_CreatesRemoveImportAction()
    {
        var source = "import math\ndef main():\n    print(1)";
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.UnusedImport,
            "Import 'math' is unused",
            line: 0, startCol: 0, endCol: 11);

        var result = await GetCodeActionsAsync(source, diag);

        result.Should().NotBeNull();
        var actions = result!.ToList();
        actions.Should().ContainSingle();

        var action = actions[0].CodeAction;
        action.Should().NotBeNull();
        action!.Title.Should().Be("Remove unused import");
        action.Kind.Should().Be(CodeActionKind.QuickFix);

        // The edit should delete the entire line
        var edits = action.Edit!.Changes![TestUri].ToList();
        edits.Should().ContainSingle();
        edits[0].NewText.Should().BeEmpty();
        edits[0].Range.Start.Line.Should().Be(0);
        edits[0].Range.Start.Character.Should().Be(0);
        edits[0].Range.End.Line.Should().Be(1);
        edits[0].Range.End.Character.Should().Be(0);
    }

    [Fact]
    public async Task UnusedImport_WithoutDocumentText_UsesOriginalRange()
    {
        // Don't open the document so doc text is null
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.UnusedImport,
            "Import 'math' is unused",
            line: 0, startCol: 0, endCol: 11);

        // Open with empty document to avoid null workspace issues
        // Then close to simulate no doc available
        _workspace.OpenDocument(TestUri.ToString(), "import math", 1);
        _workspace.CloseDocument(TestUri.ToString());

        var request = new CodeActionParams
        {
            TextDocument = new TextDocumentIdentifier(TestUri),
            Range = new LspRange(new Position(0, 0), new Position(0, 0)),
            Context = new CodeActionContext
            {
                Diagnostics = new Container<Diagnostic>(diag)
            }
        };

        var result = await _handler.Handle(request, CancellationToken.None);

        result.Should().NotBeNull();
        var action = result!.First().CodeAction;
        action.Should().NotBeNull();

        // Without document text, falls back to the diagnostic range
        var edits = action!.Edit!.Changes![TestUri].ToList();
        edits[0].Range.Start.Line.Should().Be(0);
        edits[0].Range.Start.Character.Should().Be(0);
    }

    #endregion

    #region Naming convention (SPY0453)

    [Fact]
    public async Task NamingConvention_WithStructuredData_UsesDataField()
    {
        var data = JObject.FromObject(new { suggestedName = "my_variable" });
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.NamingConventionWarning,
            "Variable 'myVariable' should use snake_case; consider 'my_variable'",
            line: 0, startCol: 0, endCol: 10,
            data: data);

        var result = await GetCodeActionsAsync("myVariable: int = 1\ndef main():\n    print(myVariable)", diag);

        result.Should().NotBeNull();
        var actions = result!.ToList();
        actions.Should().ContainSingle();

        var action = actions[0].CodeAction;
        action.Should().NotBeNull();
        action!.Title.Should().Be("Rename to 'my_variable'");
        action.Kind.Should().Be(CodeActionKind.QuickFix);

        var edits = action.Edit!.Changes![TestUri].ToList();
        edits.Should().ContainSingle();
        edits[0].NewText.Should().Be("my_variable");
    }

    [Fact]
    public async Task NamingConvention_NoStructuredData_ReturnsNoAction()
    {
        // No structured data — no suggestion can be extracted
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.NamingConventionWarning,
            "Variable 'myVariable' should use snake_case",
            line: 0, startCol: 0, endCol: 10);

        var result = await GetCodeActionsAsync("myVariable: int = 1\ndef main():\n    print(myVariable)", diag);

        result.Should().NotBeNull();
        result!.Should().BeEmpty("no structured data means no suggestion available");
    }

    [Fact]
    public async Task NamingConvention_NoSuggestedName_ReturnsNoAction()
    {
        // Message without a parseable suggestion
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.NamingConventionWarning,
            "Variable 'x' has an unusual name",
            line: 0, startCol: 0, endCol: 1);

        var result = await GetCodeActionsAsync("x: int = 1\ndef main():\n    print(x)", diag);

        result.Should().NotBeNull();
        var actions = result!.ToList();
        actions.Should().BeEmpty("no suggestion could be extracted");
    }

    [Fact]
    public async Task NamingConvention_StructuredDataUsedForSuggestion()
    {
        // Structured data carries the suggestion
        var data = JObject.FromObject(new { suggestedName = "correct_name" });
        var diag = MakeDiagnostic(
            DiagnosticCodes.Validation.NamingConventionWarning,
            "Variable 'wrongName' has naming issues",
            line: 0, startCol: 0, endCol: 10,
            data: data);

        var result = await GetCodeActionsAsync("wrongName: int = 1\ndef main():\n    print(wrongName)", diag);

        result.Should().NotBeNull();
        var action = result!.First().CodeAction;
        action.Should().NotBeNull();
        action!.Title.Should().Be("Rename to 'correct_name'");
    }

    #endregion

    #region General behavior

    [Fact]
    public async Task NoDiagnostics_ReturnsEmptyActions()
    {
        var result = await GetCodeActionsAsync("x: int = 1\ndef main():\n    print(x)");

        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownDiagnosticCode_IsIgnored()
    {
        var diag = MakeDiagnostic(
            "SPY9999",
            "Some unknown diagnostic",
            line: 0, startCol: 0, endCol: 5);

        var result = await GetCodeActionsAsync("x: int = 1\ndef main():\n    print(x)", diag);

        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    [Fact]
    public async Task DiagnosticWithoutCode_IsIgnored()
    {
        var diag = new Diagnostic
        {
            Message = "Some message",
            Range = new LspRange(new Position(0, 0), new Position(0, 5)),
            Severity = DiagnosticSeverity.Warning,
            Source = "sharpy"
        };

        var result = await GetCodeActionsAsync("x: int = 1\ndef main():\n    print(x)", diag);

        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleDiagnostics_ProducesMultipleActions()
    {
        var diag1 = MakeDiagnostic(
            DiagnosticCodes.Validation.UnusedVariable,
            "Variable 'x' is unused",
            line: 1, startCol: 4, endCol: 5);
        var diag2 = MakeDiagnostic(
            DiagnosticCodes.Validation.UnusedImport,
            "Import 'math' is unused",
            line: 0, startCol: 0, endCol: 11);

        var result = await GetCodeActionsAsync(
            "import math\ndef main():\n    x: int = 1\n    print(1)",
            diag1, diag2);

        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
    }

    [Fact]
    public async Task CodeAction_Resolve_ReturnsAsIs()
    {
        var action = new CodeAction
        {
            Title = "Test action",
            Kind = CodeActionKind.QuickFix
        };

        var result = await _handler.Handle(action, CancellationToken.None);
        result.Should().BeSameAs(action);
    }

    #endregion
}
