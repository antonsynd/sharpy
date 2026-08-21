using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// #1360 close-condition: mid-edit completion works from a current partial analysis without
/// ever having had a prior good analysis. Covers both stdlib-module (math) and user-type receivers.
/// </summary>
public class MidEditCompletionTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _service;
    private readonly SharpyCompletionHandler _handler;

    private const string Uri = "file:///mid-edit/main.spy";

    public MidEditCompletionTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _service = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyCompletionHandler(_service, _api);
    }

    [Fact]
    public async Task MultiMemberClass_FreshBuffer_OffersAllMembers()
    {
        const string source =
            "class Stats:\n"
            + "    count: int = 0\n"
            + "    total: float = 0.0\n"
            + "    def average(self) -> float:\n"
            + "        return self.total / self.count\n"
            + "    def reset(self) -> None:\n"
            + "        self.count = 0\n"
            + "\n"
            + "def main() -> None:\n"
            + "    s: Stats = Stats()\n"
            + "    print(s.)\n";

        // 0-based line 10, char 12 — immediately after the dot in `    print(s.)`
        const int line = 10;
        const int character = 12;

        _workspace.OpenDocument(Uri, source, 1);

        var result = await CompleteAsync(line, character, trigger: ".");

        result.Should().NotBeNull();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain("count", "the receiver's field");
        labels.Should().Contain("total", "the receiver's second field");
        labels.Should().Contain("average", "the receiver's method");
        labels.Should().Contain("reset", "the receiver's second method");
        labels.Should().NotContain("main",
            "module-level functions are not reachable through 's.' — "
            + "this is member completion, not scope dump");
    }

    /// <summary>
    /// The reference list <c>Sharpy.Lsp.Program</c> computes at startup, mirrored from
    /// <see cref="StdlibModuleMembersTests"/>. A missing DLL fails loudly rather than silently
    /// measuring the no-reference configuration, where <c>import math</c> cannot resolve at all.
    /// </summary>
    private static string[] ServerDefaultReferences()
    {
        var baseDir = AppContext.BaseDirectory;
        var corePath = System.IO.Path.Combine(baseDir, "Sharpy.Core.dll");
        Assert.True(File.Exists(corePath), $"Sharpy.Core.dll not found next to the test assembly: {corePath}");

        var stdlibPath = System.IO.Path.Combine(baseDir, "Sharpy.Stdlib.dll");
        Assert.True(File.Exists(stdlibPath), $"Sharpy.Stdlib.dll not found next to the test assembly: {stdlibPath}");

        return new[] { corePath, stdlibPath };
    }

    private const string MathMidEditSource =
        "import math\n"
        + "\n"
        + "def main() -> None:\n"
        + "    result: int = 0\n"
        + "    print(math.)\n";

    [Fact]
    public async Task StdlibModuleDot_FreshBuffer_OffersModuleMembers()
    {
        var api = new CompilerApi(null, ServerDefaultReferences());
        using var workspace = new SharpyWorkspace(api, NullLogger<SharpyWorkspace>.Instance);
        using var service = new LanguageService(workspace, api, NullLogger<LanguageService>.Instance);
        var handler = new SharpyCompletionHandler(service, api);

        // 0-based line 4, char 15 — immediately after the dot in `    print(math.)`
        workspace.OpenDocument(Uri, MathMidEditSource, 1);

        var result = await CompleteAsync(handler, 4, 15, trigger: ".");

        result.Should().NotBeNull();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain("pi", "module-level constants reach Exports with Sharpy spelling (#1540)");
        labels.Should().Contain("e", "the second math constant");
        labels.Should().Contain("floor", "the module's functions are offered too");
        labels.Should().NotContain("result",
            "a local in the enclosing function is not reachable through 'math.' — "
            + "this is member completion, not scope dump");
        labels.Should().NotContain("main",
            "module-level functions in the importing file are not reachable through 'math.'");
        labels.Count.Should().Be(43,
            "the completion list IS the export list — the same 43 StdlibModuleMembersTests pins "
            + "for a healthy buffer must survive a mid-edit partial analysis");
    }

    [Fact]
    public async Task StdlibModuleReceiver_FreshBrokenBuffer_HoverReturnsModuleInfo()
    {
        var api = new CompilerApi(null, ServerDefaultReferences());
        using var workspace = new SharpyWorkspace(api, NullLogger<SharpyWorkspace>.Instance);
        using var service = new LanguageService(workspace, api, NullLogger<LanguageService>.Instance);
        var hoverService = new HoverService(api);

        workspace.OpenDocument(Uri, MathMidEditSource, 1);
        var analysis = await service.GetAnalysisAsync(Uri, CancellationToken.None);
        analysis.Should().NotBeNull("the broken buffer still yields a partial analysis");

        // 1-based line 5, col 12 — inside `math` on the mid-edit `    print(math.)` line.
        var markdown = hoverService.GetHoverMarkdown(analysis!, 5, 12);

        markdown.Should().NotBeNull(
            "hover on the module receiver works from the mid-edit partial analysis");
        markdown.Should().Contain("math").And.Contain("module",
            "hover renders module info, not a generic identifier");
    }

    [Fact]
    public async Task UserClassDot_FreshBuffer_OffersClassMembers()
    {
        const string source =
            "class Greeter:\n"
            + "    name: str = \"world\"\n"
            + "    def greet(self) -> str:\n"
            + "        return \"hello \" + self.name\n"
            + "\n"
            + "def main() -> None:\n"
            + "    g: Greeter = Greeter()\n"
            + "    print(g.)\n";

        // 0-based line 7, char 12 — immediately after the dot
        const int line = 7;
        const int character = 12;

        _workspace.OpenDocument(Uri, source, 1);

        var result = await CompleteAsync(line, character, trigger: ".");

        result.Should().NotBeNull();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain("name", "the receiver's field");
        labels.Should().Contain("greet", "the receiver's method");
        labels.Should().NotContain("main",
            "module-level functions are not reachable through 'g.'");
    }

    private Task<CompletionList> CompleteAsync(int line, int character, string? trigger)
        => CompleteAsync(_handler, line, character, trigger);

    private static async Task<CompletionList> CompleteAsync(
        SharpyCompletionHandler handler, int line, int character, string? trigger)
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
        return await handler.Handle(request, CancellationToken.None);
    }

    public void Dispose()
    {
        _service.Dispose();
        _workspace.Dispose();
    }
}
