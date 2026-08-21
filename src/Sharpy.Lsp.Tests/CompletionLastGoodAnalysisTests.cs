using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// #1360 half 2: completion in a buffer that does not parse. A trailing dot is the single most
/// common transient syntax error there is, and an editor sends <c>textDocument/completion</c>
/// precisely at that instant — so "no analysis ⇒ no completion" meant member completion fired only
/// on text that already compiles, which is when it is least needed.
/// </summary>
/// <remarks>
/// Measured at the time of writing: analysing <c>print(c.)</c> yields SPY0101 with <c>Ast</c>,
/// <c>SymbolTable</c> and <c>SemanticQuery</c> all null, so there is no partial tree to recover
/// from — the answer has to come from an earlier analysis or not at all.
/// </remarks>
public class CompletionLastGoodAnalysisTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _service;
    private readonly SharpyCompletionHandler _handler;

    private const string Uri = "file:///last-good/main.spy";

    // Line 5 (1-based) is the completion line in every fixture below.
    private const string Valid =
        "class Counter:\n"
        + "    total: int = 0\n"
        + "    def bump(self) -> int:\n"
        + "        return self.total\n"
        + "\n"
        + "def main() -> None:\n"
        + "    c: Counter = Counter()\n"
        + "    print(c.total)\n";

    // The same document the instant the user types the dot: `print(c.` with nothing after it.
    private const string MidEdit =
        "class Counter:\n"
        + "    total: int = 0\n"
        + "    def bump(self) -> int:\n"
        + "        return self.total\n"
        + "\n"
        + "def main() -> None:\n"
        + "    c: Counter = Counter()\n"
        + "    print(c.\n";

    // 0-based line 7, character 12 — immediately after the '.' of `print(c.`
    private const int DotLine = 7;
    private const int DotChar = 12;

    public CompletionLastGoodAnalysisTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _service = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyCompletionHandler(_service, _api);
    }

    [Fact]
    public async Task TrailingDot_AfterAGoodAnalysis_OffersTheReceiversMembers()
    {
        // The issue's repro. Establish a good analysis, then edit to the mid-typing state.
        _workspace.OpenDocument(Uri, Valid, 1);
        (await _service.GetAnalysisAsync(Uri)).Should().NotBeNull();

        _workspace.UpdateDocument(Uri, MidEdit, 2);

        var result = await CompleteAsync(DotLine, DotChar, trigger: ".");

        result.Should().NotBeNull();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain("total", "the receiver's field is reachable through that dot");
        labels.Should().Contain("bump", "and so is its method");
        labels.Should().NotContain("main",
            "a module-level function is not reachable through 'c.' — answering from the last good "
            + "analysis must not degrade into the scope dump");
    }

    [Fact]
    public async Task LexerError_WithNoPriorGoodAnalysis_ReturnsEmptyGracefully()
    {
        // A buffer with a lexer error produces no AST at all — no partial tree to recover from.
        // The contract is an empty list, not an exception and not a guess. (#1360: trailing-dot
        // buffers now produce a partial AST, so this tests the genuine empty case.)
        const string lexerBroken =
            "class Counter:\n"
            + "    total: int = 0\n"
            + "\n"
            + "def main() -> None:\n"
            + "    c: Counter = Counter()\n"
            + "    s: str = \"unterminated\n";

        _workspace.OpenDocument(Uri, lexerBroken, 1);
        (await _service.GetAnalysisAsync(Uri))!.SymbolTable.Should().BeNull(
            "precondition: a lexer error produces no AST and no symbol table");

        var result = await CompleteAsync(DotLine, DotChar, trigger: ".");

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task TrailingDot_WhenTextBeforeTheDotChanged_DeclinesRatherThanGuessing()
    {
        // The drift check, and it has to be built carefully to mean anything. The receiver is
        // replaced by a DIFFERENT single character, so the dot stays in the same column: the stale
        // tree therefore still has a Counter-typed `c` at exactly the coordinates being queried,
        // and a handler that skipped the drift check would answer with Counter's members —
        // confidently, and for a receiver the user has replaced.
        //
        // An earlier version of this test moved the dot instead. It passed with the drift check
        // disabled, because the shifted coordinates landed inside `c.total` (an int), which offers
        // no members anyway — empty for an accidental reason rather than the intended one.
        _workspace.OpenDocument(Uri, Valid, 1);
        (await _service.GetAnalysisAsync(Uri)).Should().NotBeNull();

        var receiverReplaced = Valid.Replace("    print(c.total)\n", "    print(q.\n");
        _workspace.UpdateDocument(Uri, receiverReplaced, 2);

        var result = await CompleteAsync(DotLine, DotChar, trigger: ".");

        result.Items.Should().BeEmpty(
            "the text before the dot is not the text the stale tree was built from, so its "
            + "coordinates name a different expression");
    }

    [Fact]
    public async Task NonMemberRequest_WithLexerError_StillReturnsEmpty()
    {
        // Deliberate scope limit: when the buffer produces no analysis at all (lexer error,
        // not trailing-dot), scope completion has nothing to answer from. A trailing-dot buffer
        // now produces a partial analysis (#1360) and legitimately returns scope completions.
        const string lexerBroken =
            "class Counter:\n"
            + "    total: int = 0\n"
            + "\n"
            + "def main() -> None:\n"
            + "    c: Counter = Counter()\n"
            + "    s: str = \"unterminated\n";

        _workspace.OpenDocument(Uri, Valid, 1);
        (await _service.GetAnalysisAsync(Uri)).Should().NotBeNull();
        _workspace.UpdateDocument(Uri, lexerBroken, 2);

        var result = await CompleteAsync(DotLine, DotChar, trigger: null);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAnalysisAsync_NeverFallsBackToTheLastGoodSnapshot()
    {
        // The hard constraint. GetAnalysisAsync is what DiagnosticPublisher publishes from and what
        // FrontEndParityTests compares against the CompilerApi.Analyze baseline (umbrella #1144).
        // If the last-good snapshot leaked into it, the editor would show diagnostics for text the
        // user has already fixed, and the parity sweep would break by construction.
        // Mutation check: make GetAnalysisAsync return the snapshot when the current analysis fails
        // and this test fails on both assertions.
        _workspace.OpenDocument(Uri, Valid, 1);
        (await _service.GetAnalysisAsync(Uri))!.Success.Should().BeTrue();

        _workspace.UpdateDocument(Uri, MidEdit, 2);

        var current = await _service.GetAnalysisAsync(Uri);
        current.Should().NotBeNull();
        current!.Success.Should().BeFalse("the CURRENT text does not parse");
        current.Diagnostics.Select(d => d.Code).Should().Contain(DiagnosticCodes.Parser.ExpectedIdentifier,
            "the trailing dot's own diagnostic, not one recovered from the earlier text");
        // #1360: the partial-AST pipeline gives a non-null SymbolTable even for parse-errored
        // buffers, so LSP can serve member completion from the current analysis.
        current.SymbolTable.Should().NotBeNull(
            "the partial analysis resolves the receiver's type even though the buffer has errors");

        // ...while the separate accessor does have something to offer.
        _service.GetLastGoodAnalysis(Uri).Should().NotBeNull(
            "the snapshot exists — it is simply not served through the diagnostics path");
    }

    [Fact]
    public async Task InvalidateAnalysis_DropsTheLastGoodSnapshot()
    {
        // An options change makes every earlier analysis stale in the sense that matters: it was
        // produced under different features. Serving it afterwards would reintroduce exactly the
        // staleness InvalidateAnalysis exists to remove.
        //
        // Driven through DocumentState directly rather than through SetConfiguredFeatures: that
        // path also arms a 300 ms debounce timer whose analysis would repopulate the snapshot, so
        // asserting "null" after it would be a race — the #1375 failure mode, which this batch is
        // fixing rather than joining. The wiring from a configuration change to this call is
        // already pinned by LspWorkspaceFeatureGatingTests.
        _workspace.OpenDocument(Uri, Valid, 1);
        (await _service.GetAnalysisAsync(Uri)).Should().NotBeNull();

        var document = _workspace.GetDocument(Uri);
        document.Should().NotBeNull();
        document!.LastGoodAnalysis.Should().NotBeNull("precondition");

        // The generation argument is what RebuildOptions would stamp (#1375); any advance works
        // here, since this test is about the snapshot being dropped, not about which options won.
        document.InvalidateAnalysis(optionsGeneration: 1);

        document.LastGoodAnalysis.Should().BeNull();
    }

    [Fact]
    public async Task AnEdit_KeepsTheLastGoodSnapshot()
    {
        // The complement of the test above, and the property the whole feature rests on: an edit
        // must NOT drop the snapshot, or the fallback would be empty exactly when it is needed.
        _workspace.OpenDocument(Uri, Valid, 1);
        (await _service.GetAnalysisAsync(Uri)).Should().NotBeNull();

        _workspace.UpdateDocument(Uri, MidEdit, 2);

        _service.GetLastGoodAnalysis(Uri).Should().NotBeNull();
    }

    private async Task<CompletionList> CompleteAsync(int line, int character, string? trigger)
    {
        var request = new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier(Uri),
            Position = new Position(line, character),
            Context = trigger == null
                ? new CompletionContext { TriggerKind = CompletionTriggerKind.Invoked }
                : new CompletionContext
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = trigger
                }
        };
        return await _handler.Handle(request, CancellationToken.None);
    }

    public void Dispose()
    {
        _service.Dispose();
        _workspace.Dispose();
    }
}
