using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp.Handlers;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Go-to-definition and hover for symbols imported from a module that is NOT a compilation unit —
/// importable but never compiled, so what the editor sees is <see cref="ModuleLoader"/>'s
/// extraction rather than the compilation's own symbols.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DefinitionCrossModuleTests"/> covers the in-source-set arrangement, where identity
/// sharing (#1366/#1407) makes the imported symbol BE the declaration. This is the other half, and
/// it was the half nothing measured: <c>grep DeclaringFilePath ModuleLoader.cs</c> returned nothing
/// at all, so every extracted symbol answered "which file declares you?" with null and
/// <c>SymbolLocationHelper</c> had no other file to name. Go-to-definition on an imported method,
/// field or function offered no destination whatever (#1441).
/// </para>
/// <para>
/// Mutation test for the seam: stub the <c>DeclaringFilePath = CurrentModulePath</c> stamps in
/// <c>ModuleLoader.ExtractMethodSymbol</c>/<c>ExtractFields</c>/the <c>FunctionDef</c> arm and these
/// reddens — the location comes back as the requesting document, or null.
/// </para>
/// <para>
/// The hover half guards a different seam in the same feature: <c>Documentation</c> is extracted but
/// was dropped again by the from-import re-export clone, which restated facts by hand
/// (#1440). Both are asserted here because a symbol can know its file and still hover blank.
/// </para>
/// </remarks>
public class DefinitionOutsideSourceSetTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _service;
    private readonly string _tempDir;

    // lib.spy — 1-based line/column of each specimen's NAME token is recorded beside it, because
    // that is what an LSP text edit and a go-to-definition range both read
    // (Symbol.EffectiveNameLine/Column).
    private const string Library =
        "class Widget:\n" +                              // 1
        "    \"\"\"Widget docs.\"\"\"\n" +               // 2
        "\n" +                                           // 3
        "    label: str\n" +                             // 4  → label at column 5
        "\n" +                                           // 5
        "    def __init__(self, label: str) -> None:\n" + // 6
        "        self.label = label\n" +                 // 7
        "\n" +                                           // 8
        "    def render(self) -> str:\n" +               // 9  → render at column 9
        "        return self.label\n" +                  // 10
        "\n" +                                           // 11
        "\n" +                                           // 12
        "def build(label: str) -> Widget:\n" +           // 13 → build at column 5
        "    return Widget(label)\n";                    // 14

    private const int LabelLine = 4;
    private const int LabelColumn = 5;
    private const int RenderLine = 9;
    private const int RenderColumn = 9;
    private const int BuildLine = 13;
    private const int BuildColumn = 5;

    private const string Main =
        "from lib import Widget\n" +      // 1
        "from lib import build\n" +       // 2
        "\n" +                            // 3
        "\n" +                            // 4
        "def main() -> None:\n" +         // 5
        "    w: Widget = build(\"x\")\n" + // 6  → `build` at 0-based (5, 16)
        "    print(w.render())\n" +       // 7  → `render` at 0-based (6, 12)
        "    print(w.label)\n";           // 8  → `label` at 0-based (7, 12)

    public DefinitionOutsideSourceSetTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _service = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _tempDir = IOPath.Combine(IOPath.GetTempPath(), $"sharpy_ls_outside_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Theory]
    [InlineData(5, 16, BuildLine, BuildColumn, "an imported module-level function")]
    [InlineData(6, 12, RenderLine, RenderColumn, "a method of an imported type")]
    [InlineData(7, 12, LabelLine, LabelColumn, "a field of an imported type")]
    public async Task Definition_OnAnExtractedSymbol_LandsInTheDefiningFile(
        int useLine, int useCharacter, int expectedLine, int expectedColumn, string what)
    {
        WriteProject();
        await _service.InitializeProjectAsync(_tempDir);

        var handler = new SharpyDefinitionHandler(_service, _api);
        var result = await handler.Handle(
            new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier(MainUri),
                Position = new Position(useLine, useCharacter)
            },
            CancellationToken.None);

        result.Should().NotBeNull(
            "go-to-definition on {0} must offer a destination; with no DeclaringFilePath on the "
            + "extraction there is no other file to name and the editor offers nothing (#1441)",
            what);

        var location = result!.Single().Location;
        location.Should().NotBeNull();
        location!.Uri.GetFileSystemPath().Should().Be(LibPath,
            "the declaration of {0} lives in lib.spy — answering with the requesting document is "
            + "the null-DeclaringFilePath fallback firing", what);
        location.Range.Start.Line.Should().Be(expectedLine - 1);
        location.Range.Start.Character.Should().Be(expectedColumn - 1);
    }

    [Fact]
    public async Task Hover_OnAFromImportedType_ShowsTheDeclarationsDocumentation()
    {
        WriteProject();
        await _service.InitializeProjectAsync(_tempDir);

        var analysis = _service.ProjectAnalysis;
        analysis.Should().NotBeNull();
        var symbols = analysis!.ProjectModel.GlobalSymbols;
        symbols.Should().NotBeNull();

        symbols!.EnterModuleScope("main");
        try
        {
            var widget = symbols.Lookup("Widget", searchParents: false);
            widget.Should().NotBeNull("`from lib import Widget` binds something in main's scope");

            SymbolFormatter.FormatSymbolWithDocs(widget!).Should().Contain("Widget docs.",
                "the docstring is on the declaration, the extraction carries it, and the re-export "
                + "clone must not drop it again — hover is the only place a user reads it (#1440)");
        }
        finally
        {
            symbols.ExitScope();
        }
    }

    /// <summary>
    /// The arrangement's own control. If lib.spy ever became a compilation unit the assertions above
    /// would be measuring identity sharing instead of the extraction, and would pass for the wrong
    /// reason.
    /// </summary>
    [Fact]
    public async Task Arrangement_LibIsNotACompilationUnit()
    {
        WriteProject();
        await _service.InitializeProjectAsync(_tempDir);

        var analysis = _service.ProjectAnalysis;
        analysis.Should().NotBeNull();
        analysis!.ProjectModel.Units.Values
            .Select(u => IOPath.GetFileName(u.FilePath))
            .Should().NotContain("lib.spy",
                "these tests measure ModuleLoader's extraction; a lib.spy in the source set would "
                + "make every imported symbol the declaration itself");
    }

    private string LibPath => IOPath.Combine(_tempDir, "lib.spy");
    private string MainUri => new Uri(IOPath.Combine(_tempDir, "main.spy")).ToString();

    private void WriteProject()
    {
        // lib.spy is written beside the project file but deliberately NOT listed: the project
        // directory is a module search path, so it is importable and never compiled.
        var projectContent =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Project>\n" +
            "    <PropertyGroup>\n" +
            "        <RootNamespace>Test</RootNamespace>\n" +
            "        <OutputType>exe</OutputType>\n" +
            "    </PropertyGroup>\n" +
            "    <ItemGroup>\n" +
            "        <SpyFile Include=\"main.spy\" />\n" +
            "    </ItemGroup>\n" +
            "</Project>\n";

        File.WriteAllText(IOPath.Combine(_tempDir, "test.spyproj"), projectContent);
        File.WriteAllText(IOPath.Combine(_tempDir, "main.spy"), Main);
        File.WriteAllText(LibPath, Library);
    }

    public void Dispose()
    {
        _service.Dispose();
        _workspace.Dispose();
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
