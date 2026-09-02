using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class CodeLensTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpyCodeLensHandler _handler;

    public CodeLensTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyCodeLensHandler(_languageService);
    }

    private async Task<CodeLensContainer?> GetCodeLensesAsync(string source)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier(uri)
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task Function_ShowsReferenceCountAsync()
    {
        var source = "def foo():\n    pass\n\ndef main():\n    foo()";
        var lenses = await GetCodeLensesAsync(source);

        lenses.Should().NotBeNull();
        var refLenses = lenses!.Where(l => l.Command?.Title?.Contains("reference") == true).ToList();
        refLenses.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MainFunction_ShowsRunLensAsync()
    {
        var source = "def main():\n    print(\"hello\")";
        var lenses = await GetCodeLensesAsync(source);

        lenses.Should().NotBeNull();
        var runLenses = lenses!.Where(l => l.Command?.Title == "Run").ToList();
        runLenses.Should().ContainSingle();
    }

    [Fact]
    public async Task MainFunction_ShowsReferenceAndRunLensAsync()
    {
        // main() should show both reference count and Run lens
        var source = "def main():\n    print(\"hello\")";
        var lenses = await GetCodeLensesAsync(source);

        lenses.Should().NotBeNull();
        lenses!.Count().Should().BeGreaterThanOrEqualTo(2);
        lenses.Should().Contain(l => l.Command != null && l.Command.Title == "Run");
        lenses.Should().Contain(l => l.Command != null && l.Command.Title != null && l.Command.Title.Contains("reference"));
    }

    [Fact]
    public async Task ClassDef_ShowsReferenceCountAsync()
    {
        var source = "class Foo:\n    pass\n\ndef main():\n    f: Foo = Foo()";
        var lenses = await GetCodeLensesAsync(source);

        lenses.Should().NotBeNull();
        var refLenses = lenses!.Where(l => l.Command?.Title?.Contains("reference") == true).ToList();
        refLenses.Should().NotBeEmpty();
    }

    [Fact]
    public async Task NoDocument_ReturnsNullAsync()
    {
        var request = new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier("file:///nonexistent.spy")
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        result.Should().BeNull();
    }

    // ── Dispatch-totality probes (plan-950124 Phase 2 — CodeLensDocumentLinkDispatchTotalityTests) ──

    [Fact]
    public async Task ModuleVariable_YieldsNoLens_WhileSiblingFunctionDoes()
    {
        var lenses = await GetCodeLensesAsync("x: int = 1\n\ndef main():\n    print(x)");

        lenses.Should().NotBeNull();
        lenses!.Should().NotContain(l => l.Range.Start.Line == 0, "the lens scope is type and function declarations");
        lenses.Should().Contain(l => l.Range.Start.Line == 2 && l.Command != null && l.Command.Title.Contains("reference"),
            "positive control: the sibling function on the same input carries a reference lens");
    }

    // Misses found by the probe, fixed by delegating arms (no lens → lens).

    [Fact]
    public async Task EnumAndUnion_ShowReferenceCounts()
    {
        // Identifier uses (`Color.RED`, `Shape.Circle`) are recorded references, so both count 1.
        var source = "enum Color:\n    RED = 1\n\nunion Shape:\n    case Circle(r: float)\n\ndef main():\n    c: Color = Color.RED\n    s: Shape = Shape.Circle(1.0)\n    print(c, s)";
        var lenses = await GetCodeLensesAsync(source);

        lenses.Should().NotBeNull();
        foreach (var (line, kind) in new[] { (0, "enum"), (3, "union") })
        {
            lenses!.Should().Contain(l => l.Range.Start.Line == line && l.Command != null
                    && l.Command.Title == "1 reference",
                $"the {kind} declaration on line {line} counts its identifier use like a class");
        }
    }

    [Fact]
    public async Task DelegateAndAlias_ShowReferenceLenses_CountingLikeAClass()
    {
        // Differential on one input: a class, a delegate and an alias, each used ONLY in a type
        // annotation. The lens must exist for all three, and the delegate/alias count must equal
        // the class count — the count itself is the semantic layer's (identifier uses only).
        var source = "class C:\n    pass\n\ndelegate Cb(v: int) -> None\n\ntype Names = list[str]\n\ndef use(c: C, cb: Cb, ns: Names) -> None:\n    pass\n\ndef main():\n    pass";
        var lenses = await GetCodeLensesAsync(source);

        lenses.Should().NotBeNull();
        string TitleAt(int line) => lenses!.Should().ContainSingle(l => l.Range.Start.Line == line
                && l.Command != null && l.Command.Title.Contains("reference"),
            $"the declaration on line {line} carries a reference lens").Which.Command!.Title!;
        var classTitle = TitleAt(0);
        TitleAt(3).Should().Be(classTitle, "a delegate counts exactly like a class");
        TitleAt(5).Should().Be(classTitle, "a type alias counts exactly like a class");
        // Pins #1737 (measured): annotation-only uses are not recorded references, for a class as
        // for anything else. Drain on fix: when #1737 lands this reads "1 reference".
        classTitle.Should().Be("0 references", "#1737: type-annotation uses are not recorded as symbol references");
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
