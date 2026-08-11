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
/// Go-to-definition through a MODULE-QUALIFIED reference (#1366/#1407). The plain in-memory
/// <see cref="DefinitionTests"/> cannot cover this: a qualified reference needs a real sibling
/// file on disk, so these drive a <c>.spyproj</c> workspace.
/// </summary>
/// <remarks>
/// <para>
/// The seam being guarded is symbol IDENTITY on the import path. A <c>ModuleSymbol</c>'s exports
/// used to be <see cref="ModuleLoader"/> re-extractions of the imported file's declarations rather
/// than the compilation's own symbols, and an extraction sets <c>DefiningModule</c> but never
/// <c>DeclaringFilePath</c> — the field <see cref="SymbolLocationHelper.GetSymbolLocation"/> needs
/// to answer with a range in another file at all.
/// </para>
/// <para>
/// Measured by disabling the seam and re-running these two: go-to-definition on
/// <c>lib.Child().describe()</c> returns <b>null</b> — the editor offers no destination whatever
/// for a member reached through a module qualifier, while the same member reached through
/// <c>from lib import Child</c> navigates. Both halves are asserted here because the location
/// assertion alone could regress into "answers something" without answering the right file.
/// </para>
/// <para>
/// This is why the fix needed an LSP test and not only a compile one: compilation reads the base
/// chain, the editor reads the file path, and the two failure modes are invisible to each other.
/// </para>
/// </remarks>
public class DefinitionCrossModuleTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _service;
    private readonly string _tempDir;

    private const string Library =
        "class Base:\n" +
        "    def greet(self) -> str:\n" +
        "        return \"hello\"\n" +
        "\n" +
        "\n" +
        "class Child(Base):\n" +
        "    def describe(self) -> str:\n" +
        "        return \"Child\"\n";

    // `describe` is declared on line 7 of lib.spy, at column 9 (1-based) — "    def " is 8 chars.
    private const int DescribeLine = 7;
    private const int DescribeColumn = 9;

    private const string Main =
        "import lib\n" +
        "\n" +
        "\n" +
        "def main() -> None:\n" +
        "    print(lib.Child().describe())\n";

    // 0-based LSP position of `describe` in main.spy line 5: "    print(lib.Child().describe())".
    private static readonly Position DescribeUsePosition = new(4, 22);

    public DefinitionCrossModuleTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _service = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _tempDir = IOPath.Combine(IOPath.GetTempPath(), $"sharpy_ls_def_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task Definition_MemberThroughQualifiedReference_ResolvesIntoTheImportedFile()
    {
        WriteProject();
        await _service.InitializeProjectAsync(_tempDir);

        var handler = new SharpyDefinitionHandler(_service, _api);
        var result = await handler.Handle(
            new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier(MainUri),
                Position = DescribeUsePosition
            },
            CancellationToken.None);

        result.Should().NotBeNull(
            "`lib.Child().describe()` names a declaration the workspace has — with the export "
            + "pointing at an extraction copy this answers null, so the editor offers no "
            + "destination at all through a module qualifier");
        var location = result!.Single().Location;
        location.Should().NotBeNull();

        location!.Uri.GetFileSystemPath().Should().Be(LibPath,
            "the definition of a member reached through a module-qualified reference lives in the "
            + "imported file — answering with the requesting document is the null-DeclaringFilePath "
            + "fallback firing on an Exports copy");
        location.Range.Start.Line.Should().Be(DescribeLine - 1);
        location.Range.Start.Character.Should().Be(DescribeColumn - 1);
    }

    [Fact]
    public async Task QualifiedExport_IsTheCompilationsOwnSymbol_NotAnExtractionCopy()
    {
        // The identity statement the location assertion above depends on, made directly: the
        // symbol a qualified spelling resolves to must be the SAME OBJECT the module scope holds,
        // which is what carries DeclaringFilePath and the materialised base chain.
        WriteProject();
        await _service.InitializeProjectAsync(_tempDir);

        var analysis = _service.ProjectAnalysis;
        analysis.Should().NotBeNull();
        var symbols = analysis!.ProjectModel.GlobalSymbols;
        symbols.Should().NotBeNull();

        symbols!.EnterModuleScope("main");
        try
        {
            var libModule = symbols.Lookup("lib").Should().BeOfType<ModuleSymbol>().Subject;
            libModule.Exports.TryGetType("Child", out var exported).Should().BeTrue();

            var own = symbols.GetModuleScope("lib")?.Lookup("Child", searchParent: false);
            own.Should().NotBeNull("lib's own module scope declares Child");

            exported.Should().BeSameAs(own,
                "an in-source-set module exports the compilation's own symbols; a separate object "
                + "here is the copy whose BaseType nothing ever materialises (#1366, #1407, #1410)");
            exported.BaseType.Should().NotBeNull("the own symbol has its base chain resolved");
            exported.DeclaringFilePath.Should().NotBeNull(
                "the own symbol knows which file declares it; an extraction copy does not");
        }
        finally
        {
            symbols.ExitScope();
        }
    }

    private string LibPath => IOPath.Combine(_tempDir, "lib.spy");
    private string MainUri => new Uri(IOPath.Combine(_tempDir, "main.spy")).ToString();

    private void WriteProject()
    {
        var projectContent =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Project>\n" +
            "    <PropertyGroup>\n" +
            "        <RootNamespace>Test</RootNamespace>\n" +
            "        <OutputType>exe</OutputType>\n" +
            "    </PropertyGroup>\n" +
            "    <ItemGroup>\n" +
            "        <SpyFile Include=\"main.spy\" />\n" +
            "        <SpyFile Include=\"lib.spy\" />\n" +
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
