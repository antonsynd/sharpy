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
