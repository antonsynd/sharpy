using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Why a stdlib <c>ModuleSymbol</c>'s <c>Exports</c> is empty in an LSP analysis context (#1360).
/// </summary>
/// <remarks>
/// <para>
/// Two disjoint hypotheses, and the fixes have nothing in common, so the plan for Batch H forbids
/// writing either one before this measures which is true:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>H1</b> — the module never LOADS in the analyzing context. <c>TryResolveNetModule</c> returns
/// null at its <c>!IsModuleLoaded(moduleName)</c> early return, leaving an error-recovery
/// <c>ModuleSymbol</c> with empty Exports. The fix is reference wiring, not materialization.
/// </description></item>
/// <item><description>
/// <b>H2</b> — the module loads but Exports is still empty, i.e. the materialization in
/// <c>TryResolveNetModule</c>/<c>BuildExportsFor</c> does not reach the symbol the handlers read.
/// </description></item>
/// </list>
/// <para>
/// The discriminator is the reference set. The server adds <c>Sharpy.Stdlib.dll</c> at startup
/// (<c>Program.cs</c>); a bare <c>new CompilerApi()</c> does not. Measuring BOTH separates a wiring
/// defect from a materialization defect: if the with-stdlib context populates Exports and the bare
/// one does not, the members are demonstrably there and only the reference was missing (H1).
/// </para>
/// </remarks>
public class StdlibModuleMembersTests
{
    private readonly ITestOutputHelper _output;

    public StdlibModuleMembersTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Reproduces the reference list <c>Sharpy.Lsp.Program</c> computes at startup. A missing DLL
    /// fails loudly: silently measuring the no-reference configuration under the with-stdlib label
    /// would make the whole comparison a lie.
    /// </summary>
    private static string[] ServerDefaultReferences()
    {
        var baseDir = AppContext.BaseDirectory;
        var corePath = IOPath.Combine(baseDir, "Sharpy.Core.dll");
        Assert.True(File.Exists(corePath), $"Sharpy.Core.dll not found next to the test assembly: {corePath}");

        var stdlibPath = IOPath.Combine(baseDir, "Sharpy.Stdlib.dll");
        Assert.True(File.Exists(stdlibPath), $"Sharpy.Stdlib.dll not found next to the test assembly: {stdlibPath}");

        return new[] { corePath, stdlibPath };
    }

    private sealed record Observation(
        int ExportCount, bool IsErrorRecovery, bool HasSpy0300, string DiagnosticCodes);

    private static async Task<Observation> ObserveAsync(CompilerApi api, string label)
    {
        using var workspace = new SharpyWorkspace(api, NullLogger<SharpyWorkspace>.Instance);
        var uri = $"file:///probe_{label}.spy";
        const string source = "import math\n\n\ndef main() -> None:\n    x: float = math.floor(1.5)\n    print(x)\n";

        workspace.OpenDocument(uri, source, 1);
        var analysis = await workspace.GetAnalysisAsync(uri, CancellationToken.None);

        Assert.NotNull(analysis);

        var mathSymbol = analysis!.SymbolTable?.Lookup("math") as ModuleSymbol;
        var codes = analysis.Diagnostics.Select(d => d.Code ?? "<none>").OrderBy(c => c, StringComparer.Ordinal);

        return new Observation(
            mathSymbol?.Exports.Count ?? -1,
            mathSymbol?.IsErrorRecovery ?? false,
            analysis.Diagnostics.Any(d => d.Code == "SPY0300"),
            string.Join(",", codes));
    }

    [Fact]
    public async Task Measure_WhyStdlibModuleExportsAreEmpty_InLspAnalysisContexts()
    {
        var withStdlib = await ObserveAsync(new CompilerApi(null, ServerDefaultReferences()), "server");
        var bare = await ObserveAsync(new CompilerApi(), "bare");

        _output.WriteLine("=== #1360 H1-vs-H2 probe: `import math` ===");
        _output.WriteLine($"server-style (Sharpy.Stdlib.dll referenced, as Program.cs does):");
        _output.WriteLine($"    ModuleSymbol found  : {withStdlib.ExportCount >= 0}");
        _output.WriteLine($"    Exports.Count       : {withStdlib.ExportCount}");
        _output.WriteLine($"    IsErrorRecovery     : {withStdlib.IsErrorRecovery}");
        _output.WriteLine($"    SPY0300 present     : {withStdlib.HasSpy0300}");
        _output.WriteLine($"    diagnostics         : [{withStdlib.DiagnosticCodes}]");
        _output.WriteLine($"bare `new CompilerApi()` (no stdlib reference):");
        _output.WriteLine($"    ModuleSymbol found  : {bare.ExportCount >= 0}");
        _output.WriteLine($"    Exports.Count       : {bare.ExportCount}");
        _output.WriteLine($"    IsErrorRecovery     : {bare.IsErrorRecovery}");
        _output.WriteLine($"    SPY0300 present     : {bare.HasSpy0300}");
        _output.WriteLine($"    diagnostics         : [{bare.DiagnosticCodes}]");

        _output.WriteLine(
            withStdlib.ExportCount == bare.ExportCount
                ? "VERDICT: contexts AGREE — the reference set is not the discriminator."
                : "VERDICT: contexts DIFFER — the reference set is the discriminator (H1).");

        // H1, pinned. The materialization is not the defect: give the analysis the reference the
        // server gives it and the members are there.
        withStdlib.ExportCount.Should().BeGreaterThan(0,
            "the eager materialization populates Exports whenever the module actually loads");
        withStdlib.HasSpy0300.Should().BeFalse("the module resolved");

        // H2 refuted: without the reference the module does not load at all, and says so. The empty
        // Exports is a CONSEQUENCE of that, not a materialization gap — and it is not silent.
        bare.ExportCount.Should().Be(0);
        bare.HasSpy0300.Should().BeTrue(
            "an unresolvable import is reported, so the empty-Exports state is diagnosable rather "
            + "than a silent blank");
    }

    /// <summary>
    /// The node just before the dot, resolved exactly as <c>CompletionHandler</c> does: a node's
    /// ColumnEnd is exclusive, so <c>col - 1</c> lands on the receiver, not the member access.
    /// </summary>
    private static Compiler.Parser.Ast.Expression? _apiFindReceiver(
        CompilerApi api, SemanticResult analysis, int line, int col)
        => api.FindNodeAtPosition(analysis.Ast!, line, Math.Max(1, col - 1)) as Compiler.Parser.Ast.Expression;

    /// <summary>
    /// Given H1, the follow-on question the fix depends on: with the reference present — which is
    /// what the real server has — do the two CONSUMERS actually work? Completion and hover fail for
    /// different reasons, so "Exports is populated" does not answer for either on its own.
    /// </summary>
    [Fact]
    public async Task Measure_WhatStillFails_WhenTheStdlibReferenceIsPresent()
    {
        var api = new CompilerApi(null, ServerDefaultReferences());
        using var workspace = new SharpyWorkspace(api, NullLogger<SharpyWorkspace>.Instance);
        var languageService = new LanguageService(workspace, api, NullLogger<LanguageService>.Instance);
        var hoverService = new HoverService(api);

        const string uri = "file:///consumers.spy";
        const string source = "import math\n\n\ndef main() -> None:\n    x: float = math.floor(1.5)\n    print(x)\n";
        workspace.OpenDocument(uri, source, 1);

        var completion = new SharpyCompletionHandler(languageService, api);

        // Completion just after the dot of `math.floor` on line 5 (1-based), i.e. LSP line 4.
        var dotCol = source.Split('\n')[4].IndexOf("math.", StringComparison.Ordinal) + "math.".Length;
        var completions = await completion.Handle(
            new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Position = new Position(4, dotCol),
                // The handler gates member completion on the TRIGGER CHARACTER, not on the text.
                // Omitting it measures SCOPE completion while claiming to measure member completion
                // — the instrument, not the product.
                Context = new CompletionContext
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "."
                }
            },
            CancellationToken.None);

        var labels = completions?.Items.Select(i => i.Label).ToList()
            ?? new System.Collections.Generic.List<string>();

        var analysis = await languageService.GetAnalysisAsync(uri, CancellationToken.None);
        var hoverOnMember = hoverService.GetHoverMarkdown(analysis!, 5, dotCol + 2);

        _output.WriteLine("=== #1360 consumers, WITH the stdlib reference (as the server has it) ===");
        _output.WriteLine($"completion after `math.` : {labels.Count} items");
        _output.WriteLine($"    contains 'floor'     : {labels.Contains("floor")}");
        _output.WriteLine($"    contains 'pi'        : {labels.Contains("pi")}");
        _output.WriteLine($"    first 8              : {string.Join(", ", labels.Take(8))}");
        _output.WriteLine($"hover on `math.floor`    : {(hoverOnMember ?? "<null>").Replace("\n", " | ")}");

        // The receiver's own type, which is what CompletionHandler's guard actually reads. If
        // Exports is populated on the SymbolTable's ModuleSymbol but empty here, the two are not
        // the same instance — a third mechanism, distinct from both H1 and H2.
        var tableSymbol = analysis!.SymbolTable?.Lookup("math") as ModuleSymbol;
        var receiver = _apiFindReceiver(api, analysis, 5, dotCol);
        var receiverType = receiver == null ? null : analysis.SemanticQuery!.GetEffectiveType(receiver);

        _output.WriteLine("--- the receiver the completion guard reads ---");
        _output.WriteLine($"receiver node            : {receiver?.GetType().Name ?? "<null>"}");
        _output.WriteLine($"receiver effective type  : {receiverType?.GetType().Name ?? "<null>"}");

        if (receiverType is ModuleType mt)
        {
            _output.WriteLine($"    ModuleType.Symbol.Name       : {mt.Symbol.Name}");
            _output.WriteLine($"    ModuleType.Symbol.Exports    : {mt.Symbol.Exports.Count}");
            _output.WriteLine($"    SymbolTable's Exports        : {tableSymbol?.Exports.Count ?? -1}");
            _output.WriteLine($"    SAME ModuleSymbol instance?  : {ReferenceEquals(mt.Symbol, tableSymbol)}");
            _output.WriteLine(
                mt.Symbol.Exports.Count > 0
                    ? "VERDICT: guard's precondition HOLDS — the defect is downstream of it."
                    : "VERDICT (H3): the receiver's ModuleType carries a DIFFERENT, EMPTY ModuleSymbol "
                      + "than the SymbolTable's — materialization lands on one instance, the guard reads another.");
        }
        else
        {
            _output.WriteLine(
                "VERDICT (H4): the receiver's effective type is not a ModuleType at all — the guard "
                + "can never fire, whatever Exports holds.");
        }

        // `pi` is a module-level CONSTANT, not a function. If every export is a function, constants
        // do not reach Exports and neither completion nor hover can offer them.
        if (receiverType is ModuleType mt2)
        {
            var kinds = mt2.Symbol.Exports
                .GroupBy(e => e.Value.GetType().Name)
                .Select(g => $"{g.Key}={g.Count()}");
            _output.WriteLine($"--- export kinds: {string.Join(", ", kinds)}");
            _output.WriteLine($"    'pi' among exports : {mt2.Symbol.Exports.Any(e => e.Key == "pi")}");
            _output.WriteLine($"    'e'  among exports : {mt2.Symbol.Exports.Any(e => e.Key == "e")}");
        }

        // Does `math.pi` type-check at all, independent of the LSP?
        const string constUri = "file:///consts.spy";
        workspace.OpenDocument(
            constUri, "import math\n\n\ndef main() -> None:\n    y: float = math.pi\n    print(y)\n", 1);
        var constAnalysis = await languageService.GetAnalysisAsync(constUri, CancellationToken.None);
        _output.WriteLine(
            $"--- `y: float = math.pi` diagnostics: "
            + $"[{string.Join(",", constAnalysis!.Diagnostics.Select(d => d.Code))}]");

        // Which cursor column, if any, DOES reach the exports? Separates an off-by-one in the
        // receiver lookup from a module-kind difference.
        _output.WriteLine("--- completion by cursor column (LSP 0-based) on line 4 ---");
        for (var c = 16; c <= 22; c++)
        {
            var sweep = await completion.Handle(
                new CompletionParams
                {
                    TextDocument = new TextDocumentIdentifier(uri),
                    Position = new Position(4, c),
                    Context = new CompletionContext
                    {
                        TriggerKind = CompletionTriggerKind.TriggerCharacter,
                        TriggerCharacter = "."
                    }
                },
                CancellationToken.None);

            var sweepLabels = sweep?.Items.Select(i => i.Label).ToList()
                ?? new System.Collections.Generic.List<string>();
            var receiverAt = _apiFindReceiver(api, analysis, 5, c + 1);
            var typeAt = receiverAt == null ? null : analysis.SemanticQuery!.GetEffectiveType(receiverAt);

            _output.WriteLine(
                $"  col {c,2}: {sweepLabels.Count,4} items, floor={sweepLabels.Contains("floor"),-5} "
                + $"| node@col-1={receiverAt?.GetType().Name ?? "<null>",-12} type={typeAt?.GetType().Name ?? "<null>"}");
        }

        // ── the pins ────────────────────────────────────────────────────────
        // Both consumers WORK at HEAD; #1360 half 1's remaining defect is narrower than the issue
        // describes. Nothing pinned this, so it could regress silently — hence these.
        labels.Should().Contain("floor", "`math.` offers the module's own members");
        labels.Should().NotContain("main",
            "a function in the importing file is not reachable through `math.`");
        labels.Count.Should().Be(43,
            "the completion list IS the export list — 135 would mean the scope dump");

        hoverOnMember.Should().NotBeNull("hover resolves a stdlib module member");
        hoverOnMember.Should().Contain("floor").And.Contain("float64",
            "hover renders the member's signature, not just its name");

        labels.Should().Contain("pi",
            "module-level constants reach Exports with Sharpy spelling (#1540)");
        labels.Should().NotContain("Pi",
            "PascalCase 'Pi' must not appear in Exports after #1540 re-keying");
    }
}
