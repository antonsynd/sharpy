using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// The damage #1233 did was not a wrong squiggle on one line — the parse threw, the AST came back
/// null, and every feature that reads the AST went dark at once on that buffer: hover, completion,
/// definition, symbols, semantic tokens. An editor buffer is exactly where the triggering shape is
/// ordinary, since a file being typed has no trailing newline until the moment the user presses
/// return.
///
/// <para>These tests drive the workspace the way the server does, and they assert on the presence of
/// analysis products rather than on the absence of an error, so a regression fails as "hover returned
/// nothing" rather than as a changed diagnostic count. Each case checks first that its source really
/// has no final newline — the defect <em>is</em> the missing byte, so a source that quietly gained
/// one would leave a green test proving nothing.</para>
/// </summary>
public class UnterminatedFinalLineAvailabilityTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;

    public UnterminatedFinalLineAvailabilityTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
    }

    private async Task<SemanticResult> OpenUnterminatedAsync(string uri, string source)
    {
        source.Should().NotEndWith("\n",
            "the buffer under test is one the user has not yet ended with a newline");

        _workspace.OpenDocument(uri, source, 1);
        var analysis = await _workspace.GetAnalysisAsync(uri);
        analysis.Should().NotBeNull();
        return analysis!;
    }

    /// <summary>
    /// The reported shape: a trailing indented <c>const</c> in a buffer with no final newline. Hover
    /// over an unrelated symbol earlier in the file has to keep working — that is the availability
    /// claim, and it is what the null AST destroyed.
    /// </summary>
    [Fact]
    public async Task TrailingConst_KeepsHoverAlive()
    {
        var analysis = await OpenUnterminatedAsync(
            "file:///unterminated_const.spy",
            "def greet(name: str) -> str:\n    return \"hi \" + name\n\n\ndef main() -> None:\n    const LIMIT = 42");

        analysis.Ast.Should().NotBeNull("a trailing const must not take the whole tree down");

        var symbol = analysis.SymbolTable?.Lookup("greet");
        symbol.Should().NotBeNull("symbols must still resolve in a buffer ending on a const");

        var formatted = SymbolFormatter.FormatSymbolWithDocs(symbol!);
        formatted.Should().Contain("def greet");
        formatted.Should().Contain("-> str");
    }

    /// <summary>
    /// Hover on the trailing statement itself, not merely on something before it: the const's own
    /// name must resolve, so the availability fix reaches the last line rather than just sparing the
    /// rest of the file.
    /// </summary>
    [Fact]
    public async Task TrailingConst_IsItselfHoverable()
    {
        var analysis = await OpenUnterminatedAsync(
            "file:///unterminated_const_self.spy",
            "def main() -> None:\n    const LIMIT: int = 42\n    print(LIMIT)\n    const LAST = 7");

        analysis.Ast.Should().NotBeNull();

        var node = _api.FindNodeAtPosition(analysis.Ast!, 3, 11);
        node.Should().BeOfType<Identifier>("the LIMIT read on line 3 must still be locatable");

        var type = analysis.SemanticQuery?.GetEffectiveType((Identifier)node!);
        type.Should().NotBeNull("a const read must still have a resolved type");
    }

    /// <summary>
    /// The same availability property for the other converted forms an editor buffer can plausibly
    /// end on. Diagnostics are asserted empty because for these sources a diagnostic could only be
    /// the SPY0102 under test.
    /// </summary>
    [Theory]
    [InlineData("raise", "def main() -> None:\n    raise ValueError(\"x\")")]
    [InlineData("assert", "def main() -> None:\n    assert True")]
    [InlineData("import", "import os")]
    [InlineData("enum member", "enum Color:\n    RED = 1")]
    [InlineData("inline stub", "class C:\n    def m(self) -> int: ...")]
    [InlineData("body-less interface method", "interface I:\n    def m(self) -> int")]
    public async Task ConvertedProduction_LeavesAUsableAst(string label, string source)
    {
        var analysis = await OpenUnterminatedAsync($"file:///unterminated_{label.Replace(' ', '_')}.spy", source);

        analysis.Ast.Should().NotBeNull($"a trailing {label} must not null the AST");
        analysis.Ast!.Body.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        _workspace.Dispose();
        GC.SuppressFinalize(this);
    }
}
