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
            source);
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
            null);
        var actions = await provider.GetCodeActionsAsync(context, CancellationToken.None);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ConvertIfToMatch_SimpleEqualityChecks_ReturnsAction()
    {
        var source = @"def main():
    x: int = 1
    if x == 1:
        print('one')
    elif x == 2:
        print('two')
    else:
        print('other')";

        var provider = new ConvertFormsProvider();

        // Cursor on the if statement
        var range = new LspRange(new Position(2, 4), new Position(2, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var convertAction = actions.FirstOrDefault(a => a.Title.Contains("Convert to match statement"));
        convertAction.Should().NotBeNull();
        convertAction!.Kind.Should().Be(CodeActionKind.Refactor);
        convertAction.Edit.Should().NotBeNull();
    }

    [Fact]
    public async Task ConvertMatchToIf_LiteralPatterns_ReturnsAction()
    {
        var source = @"def main():
    x: int = 1
    match x:
        case 1:
            print('one')
        case 2:
            print('two')
        case _:
            print('other')";

        var provider = new ConvertFormsProvider();

        // Cursor on the match statement
        var range = new LspRange(new Position(2, 4), new Position(2, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var convertAction = actions.FirstOrDefault(a => a.Title.Contains("Convert to if/elif/else"));
        convertAction.Should().NotBeNull();
        convertAction!.Kind.Should().Be(CodeActionKind.Refactor);
        convertAction.Edit.Should().NotBeNull();
    }

    [Fact]
    public async Task ConvertIfToMatch_NonEqualityCondition_ReturnsNoConvertAction()
    {
        var source = @"def main():
    x: int = 1
    if x > 1:
        print('big')
    elif x < 0:
        print('negative')
    else:
        print('other')";

        var provider = new ConvertFormsProvider();

        // Cursor on the if statement
        var range = new LspRange(new Position(2, 4), new Position(2, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var convertAction = actions.FirstOrDefault(a => a.Title.Contains("Convert to match statement"));
        convertAction.Should().BeNull();
    }

    [Fact]
    public async Task ConvertIfToMatch_WithoutElse_ReturnsAction()
    {
        var source = @"def main():
    x: int = 1
    if x == 1:
        print('one')
    elif x == 2:
        print('two')";

        var provider = new ConvertFormsProvider();

        // Cursor on the if statement
        var range = new LspRange(new Position(2, 4), new Position(2, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var convertAction = actions.FirstOrDefault(a => a.Title.Contains("Convert to match statement"));
        convertAction.Should().NotBeNull();
        convertAction!.Kind.Should().Be(CodeActionKind.Refactor);
        convertAction.Edit.Should().NotBeNull();

        // Verify the generated match does not contain case _: since there was no else
        var edits = convertAction.Edit!.Changes![TestUri].ToList();
        edits.Should().ContainSingle();
        var newText = edits[0].NewText;
        newText.Should().Contain("match x:");
        newText.Should().Contain("case 1:");
        newText.Should().Contain("case 2:");
        newText.Should().NotContain("case _:");
    }

    [Fact]
    public async Task ConvertIfToMatch_GreaterThanCondition_ReturnsNoConvertAction()
    {
        // Single if with greater-than condition (not equality) — should not convert
        var source = @"def main():
    x: int = 5
    if x > 5:
        print('big')
    else:
        print('small')";

        var provider = new ConvertFormsProvider();

        // Cursor on the if statement
        var range = new LspRange(new Position(2, 4), new Position(2, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var convertAction = actions.FirstOrDefault(a => a.Title.Contains("Convert to match statement"));
        convertAction.Should().BeNull();
    }

    /// <summary>
    /// Pins ConvertFormsProvider behavior for backtick-escaped variable names (#1454).
    ///
    /// Findings (measured, not assumed):
    /// - Line 365 (add-type-annotation): UNREACHABLE — Sharpy VariableDeclaration always carries
    ///   a type annotation; <c>varDecl.Type is null</c> never holds, so the reconstruction defect
    ///   at <c>ColumnStart + Name.Length</c> is dead code.
    /// - Line 412 (remove-type-annotation): MASKED — <c>nameEndOffset</c> is computed from
    ///   <c>ColumnStart</c> (statement start) instead of <c>NameColumnStart</c> (name token start),
    ///   and uses <c>Name.Length</c> (logical name, no backticks) instead of the recorded extent.
    ///   For a non-const variable both positions coincide, so the offset lands 2 chars early but
    ///   the <c>IndexOf(':')</c> search overshoots to the correct colon. For a <c>const</c>
    ///   variable, <c>ColumnStart</c> points at <c>const</c>, making the offset even more wrong,
    ///   but the forward search still finds the colon.
    ///
    /// This test pins the CURRENT correct-by-accident output. When Task 4 fixes lines 365/412
    /// to use <c>NameColumnStart</c>/<c>NameColumnEnd</c>, the edit range stays the same (the colon
    /// search was forgiving), so the test stays green — the value is as a regression guard.
    /// </summary>
    [Fact]
    public async Task RemoveTypeAnnotation_BacktickEscapedName_RemovesAnnotation()
    {
        var source = "def main():\n    `class`: int = 1\n    print(`class`)";
        var provider = new ConvertFormsProvider();

        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var removeAction = actions.FirstOrDefault(a => a.Title.Contains("Remove type annotation"));
        removeAction.Should().NotBeNull("backtick-escaped VariableDeclaration with type should offer removal");
        removeAction!.Kind.Should().Be(CodeActionKind.Refactor);
        removeAction.Edit.Should().NotBeNull();

        var edits = removeAction.Edit!.Changes![TestUri].ToList();
        edits.Should().ContainSingle();

        var edit = edits[0];
        edit.NewText.Should().BeEmpty("removal replaces the annotation span with nothing");

        // Pin the removal range (0-based LSP coordinates).
        // `: int` spans from the colon (1-based col 12 → 0-based 11) to the space before `=`.
        // The current code finds the colon correctly despite the wrong nameEndOffset because
        // IndexOf(':') searches forward past the error.
        edit.Range.Start.Line.Should().Be(1);
        edit.Range.Start.Character.Should().Be(11, "colon at 1-based column 12 → 0-based 11");
        edit.Range.End.Line.Should().Be(1);
        edit.Range.End.Character.Should().Be(16, "removal ends before the '=' sign");
    }

    [Fact]
    public async Task RemoveTypeAnnotation_ConstBacktickEscapedName_RemovesAnnotation()
    {
        var source = "def main():\n    const `class`: int = 1\n    print(`class`)";
        var provider = new ConvertFormsProvider();

        var range = new LspRange(new Position(1, 10), new Position(1, 10));
        var actions = await GetActionsAsync(provider, source, range);

        var removeAction = actions.FirstOrDefault(a => a.Title.Contains("Remove type annotation"));
        removeAction.Should().NotBeNull("const backtick-escaped VariableDeclaration with type should offer removal");

        var edits = removeAction!.Edit!.Changes![TestUri].ToList();
        edits.Should().ContainSingle();

        var edit = edits[0];
        edit.NewText.Should().BeEmpty();

        // Pin the removal range. ColumnStart points at `const` (col 5), NameColumnStart at
        // the backtick (col 11). The nameEndOffset is computed from ColumnStart (wrong) but
        // IndexOf(':') still finds the colon at the correct position.
        edit.Range.Start.Line.Should().Be(1);
        edit.Range.Start.Character.Should().Be(17, "colon at 1-based column 18 → 0-based 17");
        edit.Range.End.Line.Should().Be(1);
        edit.Range.End.Character.Should().Be(22, "removal ends before the '=' sign");
    }

    [Fact]
    public async Task AddTypeAnnotation_BacktickEscapedName_NotOffered()
    {
        // In Sharpy, VariableDeclaration always carries a type annotation, so the
        // add-type-annotation code path (ConvertFormsProvider.cs:365) is unreachable.
        // This test pins that fact — if it ever starts returning an action, the
        // reconstruction defect at ColumnStart + Name.Length must be fixed first (#1454).
        var source = "def main():\n    `class`: int = 1\n    print(`class`)";
        var provider = new ConvertFormsProvider();

        var range = new LspRange(new Position(1, 4), new Position(1, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var addAction = actions.FirstOrDefault(a => a.Title.Contains("Add type annotation"));
        addAction.Should().BeNull("VariableDeclaration with type never offers add-type-annotation");
    }

    [Fact]
    public async Task ConvertMatchToIf_SimpleTwoCaseMatch_ReturnsActionWithIfElse()
    {
        // Simple match with two literal cases and a wildcard
        var source = @"def main():
    x: int = 1
    match x:
        case 1:
            print('one')
        case _:
            print('other')";

        var provider = new ConvertFormsProvider();

        // Cursor on the match statement
        var range = new LspRange(new Position(2, 4), new Position(2, 4));
        var actions = await GetActionsAsync(provider, source, range);

        var convertAction = actions.FirstOrDefault(a => a.Title.Contains("Convert to if/elif/else"));
        convertAction.Should().NotBeNull();
        convertAction!.Kind.Should().Be(CodeActionKind.Refactor);
        convertAction.Edit.Should().NotBeNull();

        // Verify the generated if/else structure
        var edits = convertAction.Edit!.Changes![TestUri].ToList();
        edits.Should().ContainSingle();
        var newText = edits[0].NewText;
        newText.Should().Contain("if x == 1:");
        newText.Should().Contain("else:");
        newText.Should().NotContain("elif");
    }
}
