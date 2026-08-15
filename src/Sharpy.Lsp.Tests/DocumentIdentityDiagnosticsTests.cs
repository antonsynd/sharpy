using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// #1433: two diagnostic classes exist only because a file has a NAME, and the LSP used to analyze
/// every buffer as the nameless <c>"&lt;source&gt;"</c> — so neither was producible in the editor.
/// The user saw no squiggle, then the build refused the file.
/// </summary>
/// <remarks>
/// <para>
/// The two classes are <see cref="DiagnosticCodes.CodeGen.FunctionModuleClassCollision"/>
/// (SPY0523 — a module-level <c>def foo</c> in <c>foo.spy</c> collides with the module class
/// derived from the filename) and <see cref="DiagnosticCodes.Semantic.CircularImport"/> (SPY0302 —
/// a module that imports itself, which is only recognizable as SELF-import if the module knows its
/// own name; nameless it reported the unrelated SPY0300 module-not-found).
/// </para>
/// <para>
/// The fix threads the document path in as the file's NAME while keeping the #1087 SYMBOL-identity
/// contract — the entry file's symbols still come back with a null path so handlers fall back to
/// the request URI. <see cref="EntryFileSymbols_KeepNullDeclaringPath_SoHandlersFallBackToTheUri"/>
/// is the cell that keeps those two axes from being re-conflated: it is the half a naive switch to
/// the path-carrying <c>Analyze</c> overload would have broken.
/// </para>
/// </remarks>
public class DocumentIdentityDiagnosticsTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "sharpy_lsp_identity_" + Guid.NewGuid().ToString("N"));

    public DocumentIdentityDiagnosticsTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        _workspace.Dispose();
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public async Task FunctionNamedAfterItsFile_ReportsSPY0523()
    {
        const string source = """
            def collide_target() -> None:
                print("this should fail")

            def main():
                collide_target()
            """;

        var codes = await AnalyzeAsync("collide_target.spy", source);

        codes.Should().Contain(DiagnosticCodes.CodeGen.FunctionModuleClassCollision,
            "the module class is derived from the FILE name, so the collision is only visible "
            + "to an analysis that knows what the file is called (#1433)");
    }

    [Fact]
    public async Task FunctionNamedAfterADifferentFile_IsSilent()
    {
        // The falsifiable arm: the same source in a differently-named file must NOT report. Without
        // this, the cell above would pass on any change that reported SPY0523 unconditionally.
        const string source = """
            def collide_target() -> None:
                print("this is fine")

            def main():
                collide_target()
            """;

        var codes = await AnalyzeAsync("something_else.spy", source);

        codes.Should().NotContain(DiagnosticCodes.CodeGen.FunctionModuleClassCollision,
            "no collision exists when the function's name differs from the file's");
    }

    [Fact]
    public async Task ModuleImportingItself_ReportsSPY0302_NotModuleNotFound()
    {
        const string source = """
            import ring_self


            def main():
                pass
            """;

        var codes = await AnalyzeAsync("ring_self.spy", source);

        codes.Should().Contain(DiagnosticCodes.Semantic.CircularImport,
            "a module can only recognize an import of ITSELF if it knows its own name (#1433)");
        codes.Should().NotContain(DiagnosticCodes.Semantic.ModuleNotFound,
            "the nameless baseline mistook the self-import for a missing module");
    }

    [Fact]
    public async Task UntitledBuffer_StillAnalyzes_UnderThePathlessContract()
    {
        // An untitled buffer genuinely has no name; it must keep working, not throw or regress.
        _workspace.OpenDocument("untitled:Untitled-1", "def main():\n    x: int = 1\n    print(x)", 1);
        var analysis = await _workspace.GetAnalysisAsync("untitled:Untitled-1", CancellationToken.None);

        analysis.Should().NotBeNull();
        analysis!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task EntryFileSymbols_KeepNullDeclaringPath_SoHandlersFallBackToTheUri()
    {
        // The #1087 contract, and the reason this fix is an axis SPLIT rather than a flag flip:
        // naming the file must not give the entry file's symbols a path identity, or every handler
        // that falls back to the request URI (rename, highlight, type hierarchy) starts comparing
        // against a synthetic path instead.
        const string source = """
            def helper() -> int:
                return 1


            def main():
                print(helper())
            """;

        var path = Path.Combine(_dir, "symbol_identity.spy");
        File.WriteAllText(path, source);
        var uri = new Uri(path).ToString();
        _workspace.OpenDocument(uri, source, 1);

        var analysis = await _workspace.GetAnalysisAsync(uri, CancellationToken.None);

        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();
        var helper = analysis.SymbolTable!.Lookup("helper");
        helper.Should().NotBeNull("the entry file's own symbols must be reachable");
        helper!.DeclaringFilePath.Should().BeNull(
            "the entry file's symbols carry no path so handlers fall back to the request URI (#1087)");
    }

    // ── harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens <paramref name="fileName"/> on disk under this test's own directory (so the analysis
    /// sees a real, uniquely-named file) and returns the diagnostic codes it produces.
    /// </summary>
    private async Task<IReadOnlyList<string>> AnalyzeAsync(string fileName, string source)
    {
        var path = Path.Combine(_dir, fileName);
        File.WriteAllText(path, source);
        var uri = new Uri(path).ToString();

        _workspace.OpenDocument(uri, source, 1);
        var analysis = await _workspace.GetAnalysisAsync(uri, CancellationToken.None);

        analysis.Should().NotBeNull($"the document {fileName} must analyze");
        return analysis!.Diagnostics
            .Where(d => !string.IsNullOrEmpty(d.Code))
            .Select(d => d.Code!)
            .ToList();
    }
}
