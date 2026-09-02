using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class FoldingRangeTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpyFoldingRangeHandler _handler;

    public FoldingRangeTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyFoldingRangeHandler(_languageService);
    }

    private async Task<Container<FoldingRange>?> GetFoldingRangesAsync(string source)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new FoldingRangeRequestParam
        {
            TextDocument = new TextDocumentIdentifier(uri)
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task FunctionDef_ProducesFoldingRangeAsync()
    {
        var source = "def foo():\n    x: int = 1\n    return x";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        ranges.Should().ContainSingle();
        var range = ranges!.Single();
        range.StartLine.Should().Be(0); // 0-based
        range.EndLine.Should().Be(2);
        range.Kind.Should().Be(FoldingRangeKind.Region);
    }

    [Fact]
    public async Task ClassDef_ProducesFoldingRangeAsync()
    {
        var source = "class Foo:\n    x: int = 1\n    def bar(self):\n        pass";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        // Class + method inside
        ranges!.Count().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task IfStatement_ProducesFoldingRangeAsync()
    {
        var source = "def foo():\n    if True:\n        x: int = 1\n        y: int = 2";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        // Function + if statement
        ranges!.Count().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task SingleLineStatements_NoFoldingRangesAsync()
    {
        var source = "x: int = 1";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        ranges.Should().BeEmpty();
    }

    [Fact]
    public async Task NestedBlocks_ProduceMultipleRangesAsync()
    {
        var source = "class Outer:\n    class Inner:\n        def method(self):\n            pass";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        // Outer class + Inner class + method
        ranges!.Count().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task NullAst_ReturnsNullAsync()
    {
        // Non-existent document
        var request = new FoldingRangeRequestParam
        {
            TextDocument = new TextDocumentIdentifier("file:///nonexistent.spy")
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        result.Should().BeNull();
    }

    // ── Dispatch-totality probes (plan-950124 Phase 2 — FoldingRangeDispatchTotalityTests) ──

    [Fact]
    public async Task MultiLineAssignment_YieldsNoRange_WhileSiblingIfDoes()
    {
        // A simple statement spelled over several lines is the client's bracket folding; the
        // sibling `if` suite on the same input is the server's.
        var source = "def f() -> None:\n    xs = [\n        1,\n        2,\n    ]\n    if True:\n        pass\n        pass";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        ranges!.Should().NotContain(r => r.StartLine == 1, "a multi-line assignment produces no server folding range");
        ranges.Should().Contain(r => r.StartLine == 5 && r.EndLine == 7, "positive control: the sibling if-suite folds");
    }

    // Misses found by the probe, fixed by delegating arms (no range → range).

    [Fact]
    public async Task DeferBlock_ProducesFoldingRange_WithTheSameExtentRuleAsAnIfSuite()
    {
        // Differential on one input: the defer block (L1-L3) and an if-suite (L5-L7) in the same
        // position, each followed by a statement. The arm delegates to the node extents like
        // every other suite arm, so both ranges must obey the same rule.
        var source = "def f() -> None:\n    defer:\n        print(1)\n        print(2)\n    print(3)\n    if True:\n        print(4)\n        print(5)\n    print(6)";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        var deferRange = ranges!.Should().ContainSingle(r => r.StartLine == 1, "the defer block is a suite").Which;
        var ifRange = ranges.Should().ContainSingle(r => r.StartLine == 5, "control: the if-suite").Which;
        (deferRange.EndLine - deferRange.StartLine).Should().Be(ifRange.EndLine - ifRange.StartLine,
            "a defer block and an if-suite of the same shape fold to the same extent");
    }

    /// <summary>
    /// Pins the measured extent defect of #1736: a suite followed by a statement records its
    /// LineEnd from the Dedent token, i.e. the NEXT statement's line, so its folding range ends
    /// one line past its body (an EOF-terminated suite ends correctly, which is why every other
    /// folding test — all EOF-terminated — never saw it). Drain on fix: when #1736 lands this
    /// reads 7 and the cell is deleted.
    /// </summary>
    [Fact]
    public async Task KnownExtentDefect_SuiteFollowedByStatement_EndsOneLinePastItsBody()
    {
        var source = "def f() -> None:\n    defer:\n        print(1)\n        print(2)\n    print(3)\n    if True:\n        print(4)\n        print(5)\n    print(6)";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        var ifRange = ranges!.Should().ContainSingle(r => r.StartLine == 5).Which;
        ifRange.EndLine.Should().Be(8, "#1736: the if-suite L5-L7 is followed by `print(6)` on L8 and the parser records the Dedent line");
    }

    [Fact]
    public async Task UnionDef_ProducesFoldingRangeWithItsMethods()
    {
        var source = "union Shape:\n    case Circle(r: float)\n    def describe(self) -> str:\n        return \"s\"";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        ranges!.Should().Contain(r => r.StartLine == 0 && r.EndLine == 3, "the union body folds like a class");
        ranges.Should().Contain(r => r.StartLine == 2 && r.EndLine == 3, "the union's method folds like a class member");
    }

    [Fact]
    public async Task FunctionStyleEvent_ProducesFoldingRange()
    {
        var source = "class Box:\n    event add on_click(self, handler: Cb):\n        print(1)\n        print(2)";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        ranges!.Should().Contain(r => r.StartLine == 1 && r.EndLine == 3,
            "a function-style event accessor is a suite like a function-style property");
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
