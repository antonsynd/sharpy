using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// #2234 (R-EQ) in the editor: a <c>.spyproj</c> that does not list <c>lib.spy</c> publishes SPY0313
/// on the import that resolves to it, AND the import still binds <c>ModuleLoader</c>'s extraction, so
/// hover and go-to-definition keep working (<see cref="DefinitionOutsideSourceSetTests"/>, #1440/#1441).
/// </summary>
/// <remarks>
/// Cells: the import form {<c>from lib import build</c>, <c>import lib</c>} × the request {hover,
/// definition}, each on the use of <c>build</c> (definition through <c>import lib</c> — the
/// module-qualified <c>lib.build</c> — navigates since #2242's member-access resolver; the full
/// spelling matrix is <see cref="DefinitionModuleQualifiedTests"/>). 2139c65da refused the import by binding an
/// error-recovery symbol, which left both requests with nothing to answer; the refusal now reports
/// and binding continues (the error still stops code generation).
/// </remarks>
public class ImportOutsideSourceSetEditorTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _service;
    private readonly string _tempDir;

    private const string Library =
        "class Widget:\n" +
        "    label: str\n" +
        "\n" +
        "    def __init__(self, label: str) -> None:\n" +
        "        self.label = label\n" +
        "\n" +
        "\n" +
        "def build(label: str) -> Widget:\n" +   // line 8 → `build` at column 5
        "    return Widget(label)\n";

    private const int BuildLine = 8;
    private const int BuildColumn = 5;

    public ImportOutsideSourceSetEditorTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _service = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _tempDir = IOPath.Combine(IOPath.GetTempPath(), $"sharpy_ls_unlisted_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>The program and the 0-based position of the use of <c>build</c> on line 5.</summary>
    private static (string Source, int Character) Program(string form) => form == "from"
        ? ("from lib import build\n\n\ndef main() -> None:\n    print(build(\"x\").label)\n", 10)
        : ("import lib\n\n\ndef main() -> None:\n    print(lib.build(\"x\").label)\n", 14);

    [Theory]
    [InlineData("from")]
    [InlineData("plain")]
    public async Task Definition_ThroughAnUnlistedImport_LandsInTheDefiningFile(string form)
    {
        var character = await LoadAsync(form);

        var result = await new SharpyDefinitionHandler(_service, _api).Handle(
            new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier(MainUri),
                Position = new Position(4, character)
            },
            CancellationToken.None);

        result.Should().NotBeNull($"[{form}] the import binds lib.spy's extraction");
        var location = result!.Single().Location;
        location!.Uri.GetFileSystemPath().Should().Be(LibPath, $"[{form}]");
        location.Range.Start.Line.Should().Be(BuildLine - 1, $"[{form}]");
        location.Range.Start.Character.Should().Be(BuildColumn - 1, $"[{form}]");
        AssertTheImportIsRefused(form);
    }

    [Theory]
    [InlineData("from")]
    [InlineData("plain")]
    public async Task Hover_ThroughAnUnlistedImport_ShowsTheExtractedSignature(string form)
    {
        var character = await LoadAsync(form);

        var hover = await new SharpyHoverHandler(_service, new HoverService(_api)).Handle(
            new HoverParams
            {
                TextDocument = new TextDocumentIdentifier(MainUri),
                Position = new Position(4, character)
            },
            CancellationToken.None);

        hover.Should().NotBeNull($"[{form}] the import binds lib.spy's extraction");
        var text = hover!.Contents.MarkupContent?.Value ?? string.Empty;
        text.Should().Contain("build", $"[{form}]");
        text.Should().Contain("Widget", $"[{form}] the extracted return type, not an error-recovery '<?>'");
        AssertTheImportIsRefused(form);
    }

    /// <summary>
    /// #2242 (P53 Phase 2): the per-file result the editor's handlers and publisher read for
    /// main.spy carries the build's SPY0313, once, and is not a success. Until #2242 that result was
    /// the unit's own bag, which import resolution never writes, so it held nothing while
    /// <c>sharpyc project</c> printed the refusal.
    /// </summary>
    [Theory]
    [InlineData("from")]
    [InlineData("plain")]
    public async Task PerFileResult_CarriesTheImportRefusal(string form)
    {
        await LoadAsync(form);

        var result = await _service.GetAnalysisAsync(MainUri);

        result.Should().NotBeNull($"[{form}] main.spy is a project file");
        var errors = result!.Diagnostics.Where(d => d.IsError).ToList();
        var refusal = errors.Should().ContainSingle($"[{form}] the build's one error on main.spy").Subject;
        refusal.Code.Should().Be("SPY0313", $"[{form}]");
        refusal.Line.Should().Be(1, $"[{form}] on the import");
        IOPath.GetFileName(refusal.FilePath).Should().Be("main.spy", $"[{form}]");
        result.Success.Should().BeFalse($"[{form}] a file whose import is refused is not a success");
    }

    /// <summary>
    /// Exactly one error, SPY0313 on main.spy's import, in the project analysis the editor indexes.
    /// </summary>
    /// <remarks>
    /// Asserted on <see cref="LanguageService.ProjectAnalysis"/> — the build's bag; the per-file
    /// result carries the same refusal since #2242 (<see cref="PerFileResult_CarriesTheImportRefusal"/>).
    /// </remarks>
    private void AssertTheImportIsRefused(string form)
    {
        var analysis = _service.ProjectAnalysis;
        analysis.Should().NotBeNull($"[{form}]");
        var errors = analysis!.Diagnostics.GetErrors().ToList();
        var refusal = errors.Should().ContainSingle($"[{form}] one refusal, no cascade").Subject;
        refusal.Code.Should().Be("SPY0313", $"[{form}]");
        refusal.Line.Should().Be(1, $"[{form}] on the import");
        IOPath.GetFileName(refusal.FilePath).Should().Be("main.spy", $"[{form}]");
        refusal.Message.Should().Contain($"'{LibPath}'", $"[{form}]");
    }

    private async Task<int> LoadAsync(string form)
    {
        var (source, character) = Program(form);
        File.WriteAllText(IOPath.Combine(_tempDir, "test.spyproj"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Project>\n" +
            "    <PropertyGroup>\n" +
            "        <RootNamespace>Test</RootNamespace>\n" +
            "        <OutputType>exe</OutputType>\n" +
            "    </PropertyGroup>\n" +
            "    <ItemGroup>\n" +
            "        <SpyFile Include=\"main.spy\" />\n" +
            "    </ItemGroup>\n" +
            "</Project>\n");
        File.WriteAllText(IOPath.Combine(_tempDir, "main.spy"), source);
        File.WriteAllText(LibPath, Library);
        await _service.InitializeProjectAsync(_tempDir);
        _service.HasProject.Should().BeTrue("a .spyproj governs the workspace");
        return character;
    }

    private string LibPath => IOPath.Combine(_tempDir, "lib.spy");
    private string MainUri => new Uri(IOPath.Combine(_tempDir, "main.spy")).ToString();

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
