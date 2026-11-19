using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// plan-775c50: go-to-definition on a <c>@static</c> field reads the binding the checker's one
/// static-value resolver recorded, so every spelling of the receiver lands on the field the access
/// binds — its own class, a subclass, a nested type and a nested subclass, a subclass of a generic
/// base, a constructed generic and its alias, and a struct — and a subclass that redeclares the name
/// lands on its own member, not the base's field.
/// </summary>
public class StaticValueSpellingNavigationTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _service;

    // The 1-based line/column of each field's NAME token is recorded beside it.
    private const string Declarations =
        "class C:\n" +                 // 1
        "    @static\n" +              // 2
        "    s: int = 1\n" +           // 3  → (3, 5)
        "class D(C):\n" +              // 4
        "    pass\n" +                 // 5
        "class Outer:\n" +             // 6
        "    class C:\n" +             // 7
        "        @static\n" +          // 8
        "        s: int = 2\n" +       // 9  → (9, 9)
        "    class D(C):\n" +          // 10
        "        pass\n" +             // 11
        "class G[T]:\n" +              // 12
        "    @static\n" +              // 13
        "    s: int = 3\n" +           // 14 → (14, 5)
        "class GD(G[int]):\n" +        // 15
        "    pass\n" +                 // 16
        "type GA = G[int]\n" +         // 17
        "struct St:\n" +               // 18
        "    @static\n" +              // 19
        "    s: int = 4\n" +           // 20 → (20, 5)
        "class P(C):\n" +              // 21
        "    @static\n" +              // 22
        "    property s: int = 5\n" +  // 23
        "class Q(C):\n" +              // 24
        "    @static\n" +              // 25
        "    def s() -> int:\n" +      // 26 → (26, 9)
        "        return 6\n";          // 27

    public StaticValueSpellingNavigationTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _service = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
    }

    [Theory]
    [InlineData("C.s", 3, 5)]
    [InlineData("D.s", 3, 5)]
    [InlineData("Outer.C.s", 9, 9)]
    [InlineData("Outer.D.s", 9, 9)]
    [InlineData("GD.s", 14, 5)]
    [InlineData("G[int].s", 14, 5)]
    [InlineData("GA.s", 14, 5)]
    [InlineData("St.s", 20, 5)]
    public async Task Definition_OnEverySpelling_LandsOnTheBoundField(string spelling, int line, int column)
    {
        var location = await DefinitionAsync(spelling);

        location.Should().NotBeNull($"[{spelling}] the access binds a field");
        location!.Range.Start.Line.Should().Be(line - 1, spelling);
        location.Range.Start.Character.Should().Be(column - 1, spelling);
    }

    /// <summary>The negative: a subclass's static method of the name hides the base's field (MRO by
    /// name), so the definition is Q's method, never C's field; <c>D.s</c>, which binds C's field, is
    /// the positive control (the theory above). The property twin (<c>P.s</c>) is not asserted: the
    /// definition of P's static property is null (findings ledger).</summary>
    [Fact]
    public async Task Definition_ThroughARedeclaringSubclass_IsTheSubclassesMember()
    {
        var location = await DefinitionAsync("Q.s()");

        location.Should().NotBeNull("Q.s names Q's static method");
        location!.Range.Start.Line.Should().Be(25, "Q.s is Q's method, not C's field");
        location.Range.Start.Character.Should().Be(8);
    }

    private async Task<Location?> DefinitionAsync(string spelling)
    {
        var source = Declarations + $"def main() -> None:\n    print({spelling})\n";
        var uri = $"file:///static_value_{Guid.NewGuid():N}.spy";
        _workspace.OpenDocument(uri, source, 1);

        var lines = source.Split('\n');
        var useLine = lines.Length - 2;
        var character = lines[useLine].LastIndexOf(".s", StringComparison.Ordinal) + 1;
        var result = await new SharpyDefinitionHandler(_service, _api).Handle(
            new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Position = new Position(useLine, character),
            },
            CancellationToken.None);
        return result?.Select(l => l.Location).FirstOrDefault(l => l != null);
    }

    public void Dispose()
    {
        _service.Dispose();
        _workspace.Dispose();
    }
}
