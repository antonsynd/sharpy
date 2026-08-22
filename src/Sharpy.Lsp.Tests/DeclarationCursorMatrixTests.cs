using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Declaration-cursor matrix: verifies that <see cref="SharpyReferencesHandler"/> and
/// <see cref="SharpyDocumentHighlightHandler"/> resolve symbols when the cursor sits on a
/// declaration node, not just on an Identifier reference (#1539).
/// </summary>
public class DeclarationCursorMatrixTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpyReferencesHandler _refsHandler;
    private readonly SharpyDocumentHighlightHandler _highlightHandler;
    private readonly SharpyRenameHandler _renameHandler;

    public DeclarationCursorMatrixTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _refsHandler = new SharpyReferencesHandler(_workspace, _languageService, _api);
        _highlightHandler = new SharpyDocumentHighlightHandler(_languageService, _api);
        _renameHandler = new SharpyRenameHandler(
            _workspace, _languageService, _api, NullLogger<SharpyRenameHandler>.Instance);
    }

    private void OpenDocument(string source, string uri = "file:///test.spy")
    {
        _workspace.OpenDocument(uri, source, 1);
    }

    private async Task<LocationContainer?> GetReferencesAsync(string uri, int line, int col)
    {
        var request = new ReferenceParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Position = new Position(line, col),
            Context = new ReferenceContext { IncludeDeclaration = true }
        };
        return await _refsHandler.Handle(request, CancellationToken.None);
    }

    private async Task<DocumentHighlightContainer?> GetHighlightsAsync(string uri, int line, int col)
    {
        var request = new DocumentHighlightParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Position = new Position(line, col)
        };
        return await _highlightHandler.Handle(request, CancellationToken.None);
    }

    // ── Parameter name ──────────────────────────────────────────────────────

    [Fact]
    public async Task Parameter_References_ResolvesFromDeclaration()
    {
        // Line 0: "def greet(name: str) -> str:"  'name' at col 10
        // Line 1: "    return name"               'name' at col 11
        var source = "def greet(name: str) -> str:\n    return name\n";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 0, 10);

        refs.Should().NotBeNull("a cursor on a parameter declaration should resolve");
        refs!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Parameter_Highlight_ResolvesFromDeclaration()
    {
        var source = "def greet(name: str) -> str:\n    return name\n";
        OpenDocument(source);

        var highlights = await GetHighlightsAsync("file:///test.spy", 0, 10);

        highlights.Should().NotBeNull("a cursor on a parameter declaration should resolve");
        highlights!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    // ── def function name ───────────────────────────────────────────────────

    [Fact]
    public async Task FunctionDef_References_ResolvesFromDeclaration()
    {
        // Line 0: "def foo() -> int:"    'foo' at col 4
        // Line 2: "def main():"
        // Line 3: "    foo()"            'foo' at col 4
        var source = "def foo() -> int:\n    return 1\ndef main():\n    foo()";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 0, 4);

        refs.Should().NotBeNull("a cursor on a function definition name should resolve");
        refs!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task FunctionDef_Highlight_ResolvesFromDeclaration()
    {
        var source = "def foo() -> int:\n    return 1\ndef main():\n    foo()";
        OpenDocument(source);

        var highlights = await GetHighlightsAsync("file:///test.spy", 0, 4);

        highlights.Should().NotBeNull("a cursor on a function definition name should resolve");
        highlights!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    // ── class name ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ClassDef_References_ResolvesFromDeclaration()
    {
        // Line 0: "class Foo:"        'Foo' at col 6
        // Line 3: "    f = Foo()"     'Foo' at col 8
        var source = "class Foo:\n    def __init__(self):\n        pass\ndef main():\n    f = Foo()";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 0, 6);

        refs.Should().NotBeNull("a cursor on a class definition name should resolve");
        refs!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ClassDef_Highlight_ResolvesFromDeclaration()
    {
        var source = "class Foo:\n    def __init__(self):\n        pass\ndef main():\n    f = Foo()";
        OpenDocument(source);

        var highlights = await GetHighlightsAsync("file:///test.spy", 0, 6);

        highlights.Should().NotBeNull("a cursor on a class definition name should resolve");
        highlights!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    // ── VariableDeclaration name ────────────────────────────────────────────

    [Fact]
    public async Task VariableDeclaration_References_ResolvesFromDeclaration()
    {
        // Line 1: "    count: int = 5"   'count' at col 4
        // Line 2: "    print(count)"     'count' at col 10
        var source = "def main():\n    count: int = 5\n    print(count)";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 1, 4);

        refs.Should().NotBeNull("a cursor on a variable declaration should resolve");
        refs!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task VariableDeclaration_Highlight_ResolvesFromDeclaration()
    {
        var source = "def main():\n    count: int = 5\n    print(count)";
        OpenDocument(source);

        var highlights = await GetHighlightsAsync("file:///test.spy", 1, 4);

        highlights.Should().NotBeNull("a cursor on a variable declaration should resolve");
        highlights!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    // ── with ... as name ────────────────────────────────────────────────────

    [Fact]
    public async Task WithAsName_References_ResolvesFromDeclaration()
    {
        // Line 1: "    with open(\"f.txt\") as handle:"   'handle' at col 26
        // Line 2: "        print(handle)"                 'handle' at col 14
        var source = "def main():\n    with open(\"f.txt\") as handle:\n        print(handle)\n";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 1, 26);

        refs.Should().NotBeNull("a cursor on a with-as name should resolve");
        refs!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task WithAsName_Highlight_ResolvesFromDeclaration()
    {
        var source = "def main():\n    with open(\"f.txt\") as handle:\n        print(handle)\n";
        OpenDocument(source);

        var highlights = await GetHighlightsAsync("file:///test.spy", 1, 26);

        highlights.Should().NotBeNull("a cursor on a with-as name should resolve");
        highlights!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    // ── except ... as name ──────────────────────────────────────────────────

    [Fact]
    public async Task ExceptAsName_References_ResolvesFromDeclaration()
    {
        // Line 3: "    except Exception as err:"   'err' at col 24
        // Line 4: "        print(err)"             'err' at col 14
        var source = "def main():\n    try:\n        print(1)\n    except Exception as err:\n        print(err)\n";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 3, 24);

        refs.Should().NotBeNull("a cursor on an except-as name should resolve");
        refs!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ExceptAsName_Highlight_ResolvesFromDeclaration()
    {
        var source = "def main():\n    try:\n        print(1)\n    except Exception as err:\n        print(err)\n";
        OpenDocument(source);

        var highlights = await GetHighlightsAsync("file:///test.spy", 3, 24);

        highlights.Should().NotBeNull("a cursor on an except-as name should resolve");
        highlights!.Count().Should().BeGreaterThanOrEqualTo(1);
    }

    // ── TypeAlias name ──────────────────────────────────────────────────────

    [Fact]
    public async Task TypeAlias_References_ResolvesFromDeclaration()
    {
        // Line 0: "type UserId = int"   'UserId' at col 5
        // Line 2: "    x: UserId = 1"   'UserId' at col 7
        var source = "type UserId = int\ndef main():\n    x: UserId = 1\n    print(x)";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 0, 5);
        var highlights = await GetHighlightsAsync("file:///test.spy", 0, 5);

        var refsResolved = refs is not null;
        var highlightsResolved = highlights is not null;
        (refsResolved || highlightsResolved).Should().BeTrue(
            "at least one handler should resolve a TypeAlias declaration cursor");
    }

    // ── PropertyDef name ────────────────────────────────────────────────────

    [Fact]
    public async Task PropertyDef_CursorOnDeclaration_RoutesToResolver()
    {
        // PropertyDef is a class member — FindSymbolByDeclaration scans module scope
        // and reference collections, which do not contain class members. The resolver
        // arm exists so that a future node-keyed map can answer for it; today both
        // handlers correctly return null (no resolution) rather than crashing.
        // Line 1: "    property x: int = 0"   'x' at col 13
        var source = "class Foo:\n    property x: int = 0\ndef main():\n    f = Foo()\n    print(f.x)";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 1, 13);
        var highlights = await GetHighlightsAsync("file:///test.spy", 1, 13);

        // Both return null because FindSymbolByDeclaration cannot see class members.
        // This documents the current state; resolution will improve when a node-keyed
        // map is added for PropertyDef.
        (refs is null && highlights is null).Should().BeTrue(
            "class-member PropertyDef is not in the collections FindSymbolByDeclaration scans");
    }

    // ── Negative: cursor on type annotation ─────────────────────────────────

    [Fact]
    public async Task TypeAnnotation_DoesNotResolveAsParameterName()
    {
        // Line 0: "def f(target: int) -> int:"   'int' annotation at col 14
        var source = "def f(target: int) -> int:\n    return target * 2\n";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 0, 14);
        var highlights = await GetHighlightsAsync("file:///test.spy", 0, 14);

        if (refs is not null)
        {
            refs.Should().NotContain(loc =>
                loc.Range.Start.Line == 0 && loc.Range.Start.Character == 6,
                "the cursor is on the type annotation, not the parameter name");
        }

        if (highlights is not null)
        {
            highlights.Should().NotContain(h =>
                h.Range.Start.Line == 0 && h.Range.Start.Character == 6,
                "the cursor is on the type annotation, not the parameter name");
        }
    }

    // ── Unreferenced binding still resolves ─────────────────────────────────

    [Fact]
    public async Task UnreferencedLocalDeclaration_StillResolves()
    {
        // Line 1: "    const LIMIT: int = 10"   'LIMIT' at col 10
        var source = "def main():\n    const LIMIT: int = 10\n    print(1)\n";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 1, 10);
        refs.Should().NotBeNull(
            "an unreferenced binding resolves via node-keyed maps, not the reference scan");

        var highlights = await GetHighlightsAsync("file:///test.spy", 1, 10);
        highlights.Should().NotBeNull(
            "an unreferenced binding resolves via node-keyed maps, not the reference scan");
    }

    private async Task<WorkspaceEdit?> RenameAsync(string uri, int line, int col, string newName)
    {
        var request = new RenameParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Position = new Position(line, col),
            NewName = newName
        };
        return await _renameHandler.Handle(request, CancellationToken.None);
    }

    // ── Rename parity ───────────────────────────────────────────────────────

    [Fact]
    public async Task Parameter_Rename_EditsExactlyTheReferencesBindingSet()
    {
        // Rename and references resolve through the same DeclarationCursorResolver, so a
        // declaration cursor must yield the same binding set in both handlers.
        var source = "def greet(name: str) -> str:\n    return name\n";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 0, 10);
        var edit = await RenameAsync("file:///test.spy", 0, 10, "title");

        refs.Should().NotBeNull("references resolves the parameter declaration cursor");
        edit.Should().NotBeNull("rename resolves the same declaration cursor references does");

        var editRanges = edit!.Changes!["file:///test.spy"].Select(e => e.Range).ToList();
        var refRanges = refs!.Select(l => l.Range).ToList();
        editRanges.Should().BeEquivalentTo(refRanges,
            "rename must rewrite exactly the binding set the references handler reports");
    }

    // ── Rebound locals expand through the binding chain (#1602, the #1359 class) ─────
    // A plain `x = ...` rebinding defines a fresh symbol chained to its predecessor. The
    // emitter assigns to ONE C# local across the chain, so references/highlight reporting a
    // fragment is silently wrong the same way fragment-rename was (#1359). The corpus below
    // mirrors RenameHandlerTests' #1359 repro exactly: four cursors, one variable.

    private const string ReboundLocalSource =
        "def main() -> None:\n    count: int = 0\n    count = count + 1\n    print(count)\n";

    private static readonly (int Line, int Col)[] ReboundLocalOccurrences =
        { (1, 4), (2, 4), (2, 12), (3, 10) };

    /// <summary>Every cursor in a rebound spelling reports the whole chain, identically.</summary>
    [Theory]
    [InlineData(1, 4)]   // the declaration
    [InlineData(2, 4)]   // the rebinding's target
    [InlineData(2, 12)]  // the read on the rebinding's right-hand side
    [InlineData(3, 10)]  // a use after the rebinding
    public async Task ReboundLocal_References_ReportEveryOccurrence_FromAnyCursor(int line, int col)
    {
        OpenDocument(ReboundLocalSource);

        var refs = await GetReferencesAsync("file:///test.spy", line, col);

        refs.Should().NotBeNull();
        refs!.Select(l => (l.Range.Start.Line, l.Range.Start.Character))
            .Should().BeEquivalentTo(ReboundLocalOccurrences,
                "all four occurrences are the same variable — the emitter assigns to one C# local, "
                + "so a fragment is a wrong answer (#1602)");
    }

    /// <summary>
    /// Highlight walks the same chain: every occurrence lights up, each binding's declaration
    /// as Write and the reads as Read.
    /// </summary>
    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 4)]
    [InlineData(2, 12)]
    [InlineData(3, 10)]
    public async Task ReboundLocal_Highlight_ReportsEveryOccurrence_FromAnyCursor(int line, int col)
    {
        OpenDocument(ReboundLocalSource);

        var highlights = await GetHighlightsAsync("file:///test.spy", line, col);

        highlights.Should().NotBeNull();
        highlights!.Select(h => (h.Range.Start.Line, h.Range.Start.Character))
            .Should().BeEquivalentTo(ReboundLocalOccurrences,
                "highlight must light the whole variable, not a binding fragment (#1602)");
        highlights!.Where(h => h.Kind == DocumentHighlightKind.Write)
            .Select(h => (h.Range.Start.Line, h.Range.Start.Character))
            .Should().BeEquivalentTo(new[] { (1, 4), (2, 4) },
                "both chain links' declarations are writes");
    }

    /// <summary>
    /// Negative control: the same spelling bound in a DIFFERENT function is a different
    /// variable and must not be dragged along (mirrors the #1359 rename control).
    /// </summary>
    [Fact]
    public async Task ReboundLocal_References_LeaveTheSameSpellingInAnotherFunctionAlone()
    {
        var source =
            "def first() -> None:\n"
            + "    count: int = 0\n"
            + "    count = count + 1\n"
            + "\n"
            + "\n"
            + "def second() -> None:\n"
            + "    count: int = 9\n"
            + "    print(count)\n";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 1, 4);

        refs.Should().NotBeNull();
        refs!.Select(l => l.Range.Start.Line).Should().OnlyContain(l => l == 1 || l == 2,
            "second()'s 'count' is a different variable that happens to share a spelling");
    }

    /// <summary>
    /// Negative control: a typed REDECLARATION is a different variable and must not chain —
    /// the emitter versions those (<c>x</c>, <c>x_1</c>). The boundary that keeps the chain honest.
    /// </summary>
    [Fact]
    public async Task Redeclaration_References_DoNotChainToTheOtherBinding()
    {
        var source =
            "def main() -> None:\n"
            + "    value: int = 1\n"
            + "    print(value)\n"
            + "    value: str = \"a\"\n"
            + "    print(value)\n";
        OpenDocument(source);

        var refs = await GetReferencesAsync("file:///test.spy", 1, 4);

        refs.Should().NotBeNull();
        refs!.Select(l => l.Range.Start.Line).Should().NotContain(3,
            "the second declaration binds a different variable");
        refs!.Select(l => l.Range.Start.Line).Should().NotContain(4,
            "reads of the second declaration belong to the other variable");
    }

    /// <summary>
    /// The rebound-local parity guard: rename edits exactly the set references reports. Before
    /// the #1602 fix references reported a one-binding fragment while rename walked the chain —
    /// exactly the shape this fact refuses. Mutation-verified: bypassing BindingSet in
    /// ReferencesHandler ALONE turns this red (one-handler drift); collapsing the shared
    /// BindingSet degrades both handlers together, which the occurrence theories above catch
    /// instead — the two guards are complementary, not redundant.
    /// </summary>
    [Fact]
    public async Task ReboundLocal_Rename_EditsExactlyTheReferencesBindingSet()
    {
        OpenDocument(ReboundLocalSource);

        var refs = await GetReferencesAsync("file:///test.spy", 1, 4);
        var edit = await RenameAsync("file:///test.spy", 1, 4, "total");

        refs.Should().NotBeNull("references resolves the rebound-local declaration cursor");
        edit.Should().NotBeNull("rename resolves the same declaration cursor references does");

        var editRanges = edit!.Changes!["file:///test.spy"].Select(e => e.Range).ToList();
        var refRanges = refs!.Select(l => l.Range).ToList();
        editRanges.Should().BeEquivalentTo(refRanges,
            "rename must rewrite exactly the binding set the references handler reports (#1602)");
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
