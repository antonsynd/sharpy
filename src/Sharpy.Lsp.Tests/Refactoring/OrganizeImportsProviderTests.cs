using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Refactoring;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Refactoring;

public class OrganizeImportsProviderTests
{
    private readonly CompilerApi _api = new();
    private static readonly DocumentUri TestUri = DocumentUri.From("file:///test.spy");

    private async Task<IReadOnlyList<CodeAction>> GetActionsAsync(
        ICodeActionProvider provider,
        string source,
        LspRange? range = null,
        Container<Diagnostic>? diagnostics = null)
    {
        var analysis = _api.Analyze(source, CancellationToken.None);
        var context = new CodeActionProviderContext(
            TestUri,
            range ?? new LspRange(new Position(0, 0), new Position(0, 0)),
            diagnostics ?? new Container<Diagnostic>(),
            analysis,
            source,
            _api);
        return await provider.GetCodeActionsAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task OrganizeImports_SortsAlphabetically()
    {
        var source = "import sys\nimport math\n\ndef main():\n    print(math.floor(1.5))\n    print(sys.argv)";
        var provider = new OrganizeImportsProvider();

        var actions = await GetActionsAsync(provider, source);

        actions.Should().ContainSingle();
        var action = actions[0];
        action.Title.Should().Be("Organize Imports");
        action.Kind.Should().Be(CodeActionKind.SourceOrganizeImports);

        // Verify the edit contains sorted imports (math before sys alphabetically)
        var edits = action.Edit!.Changes![TestUri].ToList();
        edits.Should().ContainSingle();
        var newText = edits[0].NewText;
        var mathIndex = newText.IndexOf("import math", StringComparison.Ordinal);
        var sysIndex = newText.IndexOf("import sys", StringComparison.Ordinal);
        mathIndex.Should().BeLessThan(sysIndex, "math should come before sys alphabetically");
    }

    [Fact]
    public async Task OrganizeImports_GroupsStdlibBeforeProject()
    {
        var source = "from mymodule import foo\nimport math\n\ndef main():\n    print(math.floor(1.5))\n    foo()";
        var provider = new OrganizeImportsProvider();

        var actions = await GetActionsAsync(provider, source);

        actions.Should().ContainSingle();
        var edits = actions[0].Edit!.Changes![TestUri].ToList();
        var newText = edits[0].NewText;

        // Stdlib (math) should come before project imports (mymodule)
        var mathIndex = newText.IndexOf("import math", StringComparison.Ordinal);
        var mymoduleIndex = newText.IndexOf("from mymodule", StringComparison.Ordinal);
        mathIndex.Should().BeLessThan(mymoduleIndex,
            "stdlib imports should be grouped before project imports");
    }

    [Fact]
    public async Task OrganizeImports_NoImports_ReturnsNoAction()
    {
        var source = "def main():\n    print('hello')";
        var provider = new OrganizeImportsProvider();

        var actions = await GetActionsAsync(provider, source);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task OrganizeImports_AlreadyOrganized_StillOffersAction()
    {
        var source = "import math\nimport sys\n\ndef main():\n    print(math.floor(1.5))\n    print(sys.argv)";
        var provider = new OrganizeImportsProvider();

        var actions = await GetActionsAsync(provider, source);

        // The action is always offered when imports exist, even if already sorted
        actions.Should().ContainSingle();
        actions[0].Title.Should().Be("Organize Imports");
    }

    [Fact]
    public async Task OrganizeImports_NullSource_ReturnsNoAction()
    {
        var provider = new OrganizeImportsProvider();
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
    public async Task OrganizeImports_RemovesUnusedImports()
    {
        // 'sys' is imported but never used; 'math' is used
        var source = "import sys\nimport math\n\ndef main():\n    print(math.floor(1.5))";
        var provider = new OrganizeImportsProvider();

        // Create a diagnostic for unused import SPY0452
        var diagnostics = new Container<Diagnostic>(new Diagnostic
        {
            Range = new LspRange(new Position(0, 0), new Position(0, 10)),
            Code = "SPY0452",
            Message = "Unused import 'sys'",
            Severity = DiagnosticSeverity.Warning
        });

        var analysis = _api.Analyze(source, CancellationToken.None);
        var context = new CodeActionProviderContext(
            TestUri,
            new LspRange(new Position(0, 0), new Position(0, 0)),
            diagnostics,
            analysis,
            source,
            _api);
        var actions = await provider.GetCodeActionsAsync(context, CancellationToken.None);

        actions.Should().ContainSingle();
        var edits = actions[0].Edit!.Changes![TestUri].ToList();
        var newText = edits[0].NewText;

        // The unused import 'sys' should be removed
        newText.Should().NotContain("import sys");
        // The used import 'math' should be kept
        newText.Should().Contain("import math");
    }
}
