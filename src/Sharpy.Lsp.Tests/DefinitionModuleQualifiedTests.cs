using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// #2242 (plan-6ce1a5 Phase 4): every spelling of an imported name navigates to its declaration in
/// the defining module's file, whether that module is a compilation unit or an extraction.
/// </summary>
/// <remarks>
/// <para>
/// Two matrices over one library:
/// </para>
/// <list type="bullet">
///   <item>request {definition, implementation, hover} × MODULE-QUALIFIED spelling {<c>lib.build(…)</c>,
///     <c>f = lib.build</c> (a value, no call to fall back to), <c>lib.Widget(…)</c> (a type),
///     <c>import lib as l</c> → <c>l.build</c>, <c>import pkg.lib</c> → <c>pkg.lib.build</c>,
///     <c>lib.Box[int](5)</c>, <c>lib.ident[int](3)</c>, <c>lib.Handle("42")</c> (a type alias)} ×
///     target {listed unit, unlisted extraction}. Before #2242 definition and implementation answered
///     nothing for every one of these: their private member-access arms read a
///     <c>UserDefinedType</c> receiver only, and a module receiver is a <c>ModuleType</c>. They now
///     read the checker's recorded export binding through <see cref="MemberAccessSymbolResolver"/>.</item>
///   <item>request × POSITION of the bare <c>from lib import X</c> spelling {plain call, generic call
///     <c>Box[int](5)</c>, type argument <c>ident[Widget](…)</c>, type-alias call
///     <c>Handle("42")</c>} × target. The plain call is the positive control (it navigated before
///     #2242). The generic call and the type argument bound the identifier without recording its
///     symbol; the alias recorded it, but an alias symbol carried no declaration span or file, so
///     no location could be made from it — in the same file or another.</item>
/// </list>
/// <para>
/// Definition and implementation land on the declaration's NAME token in lib.spy (the exact
/// line/column, <c>Symbol.EffectiveNameLine/Column</c>); hover shows the declaration.
/// <see cref="SameFileControl_DefinitionAtAPosition_LandsOnTheDeclaration"/> is the single-file
/// control for the position axis: the same spellings with the name declared in the same file.
/// </para>
/// </remarks>
public class DefinitionModuleQualifiedTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _service;
    private readonly string _tempDir;

    // lib.spy — the 1-based line/column of each declaration's NAME token is recorded beside it.
    private const string Library =
        "class Widget:\n" +                               // 1  → Widget at column 7
        "    label: str\n" +                              // 2
        "\n" +                                            // 3
        "    def __init__(self, label: str) -> None:\n" + // 4
        "        self.label = label\n" +                  // 5
        "\n" +                                            // 6
        "\n" +                                            // 7
        "def build(label: str) -> Widget:\n" +            // 8  → build at column 5
        "    return Widget(label)\n" +                    // 9
        "\n" +                                            // 10
        "\n" +                                            // 11
        "class Box[T]:\n" +                               // 12 → Box at column 7
        "    value: T\n" +                                // 13
        "\n" +                                            // 14
        "    def __init__(self, value: T) -> None:\n" +   // 15
        "        self.value = value\n" +                  // 16
        "\n" +                                            // 17
        "\n" +                                            // 18
        "def ident[T](v: T) -> T:\n" +                    // 19 → ident at column 5
        "    return v\n" +                                // 20
        "\n" +                                            // 21
        "\n" +                                            // 22
        "type Handle = int\n";                            // 23 → Handle at column 6

    private const int LibraryLineCount = 23;

    private static readonly IReadOnlyDictionary<string, (int Line, int Column)> Declarations =
        new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            ["Widget"] = (1, 7),
            ["build"] = (8, 5),
            ["Box"] = (12, 7),
            ["ident"] = (19, 5),
            ["Handle"] = (23, 6),
        };

    /// <summary>A program, the name the cursor sits on (its first use after the imports), and
    /// the text hover must show beside that name.</summary>
    private sealed record Spelling(string Main, string Name, string HoverAlso, bool Nested = false);

    private static readonly IReadOnlyDictionary<string, Spelling> MemberSpellings =
        new Dictionary<string, Spelling>(StringComparer.Ordinal)
        {
            ["call"] = new("import lib\n\n\ndef main() -> None:\n    print(lib.build(\"x\").label)\n",
                "build", "Widget"),
            ["value"] = new("import lib\n\n\ndef main() -> None:\n    f = lib.build\n    print(f(\"x\").label)\n",
                "build", "Widget"),
            ["type"] = new("import lib\n\n\ndef main() -> None:\n    w = lib.Widget(\"y\")\n    print(w.label)\n",
                "Widget", "class"),
            ["aliased-import"] = new("import lib as l\n\n\ndef main() -> None:\n    print(l.build(\"x\").label)\n",
                "build", "Widget"),
            ["nested-module"] = new("import pkg.lib\n\n\ndef main() -> None:\n    print(pkg.lib.build(\"x\").label)\n",
                "build", "Widget", Nested: true),
            ["generic-type"] = new("import lib\n\n\ndef main() -> None:\n    b = lib.Box[int](5)\n    print(b.value)\n",
                "Box", "class"),
            ["generic-function"] = new("import lib\n\n\ndef main() -> None:\n    print(lib.ident[int](3))\n",
                "ident", "T"),
            ["type-alias-call"] = new("import lib\n\n\ndef main() -> None:\n    v = lib.Handle(\"42\")\n    print(v)\n",
                "Handle", "type alias"),
        };

    private static readonly IReadOnlyDictionary<string, Spelling> FromPositions =
        new Dictionary<string, Spelling>(StringComparer.Ordinal)
        {
            ["plain-call"] = new("from lib import build\n\n\ndef main() -> None:\n    print(build(\"x\").label)\n",
                "build", "Widget"),
            ["generic-call"] = new("from lib import Box\n\n\ndef main() -> None:\n    b = Box[int](5)\n    print(b.value)\n",
                "Box", "class"),
            ["type-argument"] = new("from lib import Widget, ident\n\n\ndef main() -> None:\n    w = ident[Widget](Widget(\"z\"))\n    print(w.label)\n",
                "Widget", "class"),
            ["type-alias-call"] = new("from lib import Handle\n\n\ndef main() -> None:\n    v = Handle(\"42\")\n    print(v)\n",
                "Handle", "type alias"),
        };

    private static readonly string[] Requests = { "definition", "implementation", "hover" };
    private static readonly string[] Targets = { "listed", "unlisted" };

    public DefinitionModuleQualifiedTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _service = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _tempDir = IOPath.Combine(IOPath.GetTempPath(), $"sharpy_ls_modqual_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public static TheoryData<string, string, string> MemberMatrix()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var request in Requests)
            foreach (var spelling in MemberSpellings.Keys)
                foreach (var target in Targets)
                    data.Add(request, spelling, target);
        return data;
    }

    public static TheoryData<string, string, string> FromPositionMatrix()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var request in Requests)
            foreach (var position in FromPositions.Keys)
                foreach (var target in Targets)
                    data.Add(request, position, target);
        return data;
    }

    public static TheoryData<string> SameFilePositions()
    {
        var data = new TheoryData<string>();
        foreach (var position in FromPositions.Keys)
            data.Add(position);
        return data;
    }

    [Theory]
    [MemberData(nameof(MemberMatrix))]
    public Task ModuleQualifiedMember_Navigates(string request, string spelling, string target)
        => AssertNavigatesAsync(request, MemberSpellings[spelling], target, $"[{request} · m.name {spelling} · {target}]");

    [Theory]
    [MemberData(nameof(FromPositionMatrix))]
    public Task FromImportedName_NavigatesAtEveryPosition(string request, string position, string target)
        => AssertNavigatesAsync(request, FromPositions[position], target, $"[{request} · from {position} · {target}]");

    /// <summary>
    /// The single-file control for the position axis: the same spellings with the name declared in
    /// the same (unsaved, project-less) document. A miss here is not an import gap — it is the
    /// identifier binding itself recording no symbol.
    /// </summary>
    [Theory]
    [MemberData(nameof(SameFilePositions))]
    public async Task SameFileControl_DefinitionAtAPosition_LandsOnTheDeclaration(string position)
    {
        var spelling = FromPositions[position];
        var body = spelling.Main[(spelling.Main.IndexOf('\n') + 1)..];
        var source = Library + "\n\n" + body;
        const string uri = "file:///same_file_control.spy";
        _workspace.OpenDocument(uri, source, 1);

        var (line, character) = Cursor(source, spelling.Name, fromLine: LibraryLineCount);
        var result = await new SharpyDefinitionHandler(_service, _api).Handle(
            new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Position = new Position(line, character),
            },
            CancellationToken.None);

        var what = $"[same-file {position}]";
        result.Should().NotBeNull($"{what} the name binds a declaration in the same file");
        var location = result!.Select(l => l.Location).First(l => l != null)!;
        var (declLine, declColumn) = Declarations[spelling.Name];
        location.Range.Start.Line.Should().Be(declLine - 1, what);
        location.Range.Start.Character.Should().Be(declColumn - 1, what);
    }

    /// <summary>
    /// Axis change beyond the matrix: the same type name declared in two modules. Each qualified
    /// spelling must land in ITS module's file — a resolution that re-looks-up the bare name
    /// answers whichever module scope it meets first.
    /// </summary>
    [Theory]
    [InlineData("definition", "a")]
    [InlineData("definition", "b")]
    [InlineData("implementation", "a")]
    [InlineData("implementation", "b")]
    public async Task SameNameInTwoModules_EachQualifiedSpellingLandsInItsOwnModule(string request, string module)
    {
        const string declaration = "class Point:\n    x: int\n\n    def __init__(self, x: int) -> None:\n        self.x = x\n";
        File.WriteAllText(IOPath.Combine(_tempDir, "a.spy"), declaration);
        File.WriteAllText(IOPath.Combine(_tempDir, "b.spy"), "\n\n" + declaration); // Point at (3,7)
        const string main =
            "import a\nimport b\n\n\ndef main() -> None:\n    p = a.Point(1)\n    q = b.Point(2)\n    print(p.x + q.x)\n";
        File.WriteAllText(IOPath.Combine(_tempDir, "main.spy"), main);
        File.WriteAllText(IOPath.Combine(_tempDir, "test.spyproj"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Project>\n    <PropertyGroup>\n" +
            "        <RootNamespace>Test</RootNamespace>\n        <OutputType>exe</OutputType>\n" +
            "    </PropertyGroup>\n    <ItemGroup>\n" +
            "        <SpyFile Include=\"main.spy\" />\n        <SpyFile Include=\"a.spy\" />\n" +
            "        <SpyFile Include=\"b.spy\" />\n    </ItemGroup>\n</Project>\n");
        await _service.InitializeProjectAsync(_tempDir);
        _service.ProjectAnalysis!.Diagnostics.GetErrors().Should().BeEmpty("the arrangement compiles");

        var line = module == "a" ? 5 : 6;
        var document = new TextDocumentIdentifier(MainUri);
        var position = new Position(line, main.Split('\n')[line].IndexOf("Point", StringComparison.Ordinal));
        LocationOrLocationLinks? result = request == "definition"
            ? await new SharpyDefinitionHandler(_service, _api).Handle(
                new DefinitionParams { TextDocument = document, Position = position }, CancellationToken.None)
            : await new SharpyImplementationHandler(_service, _api).Handle(
                new ImplementationParams { TextDocument = document, Position = position }, CancellationToken.None);

        var what = $"[{request} · {module}.Point]";
        result.Should().NotBeNull(what);
        var location = result!.Select(l => l.Location).First(l => l != null)!;
        location.Uri.GetFileSystemPath().Should().Be(IOPath.Combine(_tempDir, $"{module}.spy"), what);
        location.Range.Start.Line.Should().Be(module == "a" ? 0 : 2, what);
        location.Range.Start.Character.Should().Be(6, what);
    }

    private async Task AssertNavigatesAsync(string request, Spelling spelling, string target, string what)
    {
        var libPath = WriteProject(spelling, listed: target == "listed");
        await _service.InitializeProjectAsync(_tempDir);
        AssertArrangement(spelling, target, what);

        var (line, character) = Cursor(spelling.Main, spelling.Name, fromLine: 1);
        var document = new TextDocumentIdentifier(MainUri);
        var position = new Position(line, character);

        if (request == "hover")
        {
            var hover = await new SharpyHoverHandler(_service, new HoverService(_api)).Handle(
                new HoverParams { TextDocument = document, Position = position },
                CancellationToken.None);
            hover.Should().NotBeNull($"{what} the spelling binds a declaration");
            var text = hover!.Contents.MarkupContent?.Value ?? string.Empty;
            text.Should().Contain(spelling.Name, what);
            text.Should().Contain(spelling.HoverAlso, $"{what} the declaration, not an error-recovery '<?>'");
            text.Should().NotContain("<?>", what);
            return;
        }

        LocationOrLocationLinks? result = request == "definition"
            ? await new SharpyDefinitionHandler(_service, _api).Handle(
                new DefinitionParams { TextDocument = document, Position = position }, CancellationToken.None)
            : await new SharpyImplementationHandler(_service, _api).Handle(
                new ImplementationParams { TextDocument = document, Position = position }, CancellationToken.None);

        result.Should().NotBeNull($"{what} the spelling binds a declaration in lib.spy");
        var location = result!.Select(l => l.Location).First(l => l != null)!;
        location.Uri.GetFileSystemPath().Should().Be(libPath,
            $"{what} the declaration lives in the module's own file");
        var (declLine, declColumn) = Declarations[spelling.Name];
        location.Range.Start.Line.Should().Be(declLine - 1, what);
        location.Range.Start.Character.Should().Be(declColumn - 1, what);
    }

    /// <summary>
    /// The arrangement's own control: a listed lib.spy is a compilation unit, an unlisted one is not —
    /// otherwise the two targets would measure the same thing.
    /// </summary>
    private void AssertArrangement(Spelling spelling, string target, string what)
    {
        var analysis = _service.ProjectAnalysis;
        analysis.Should().NotBeNull(what);
        var units = analysis!.ProjectModel.Units.Values
            .Select(u => IOPath.GetFileName(u.FilePath))
            .ToList();
        if (target == "listed")
            units.Should().Contain("lib.spy", $"{what} lib.spy is listed, so it is a compilation unit");
        else
            units.Should().NotContain("lib.spy", $"{what} lib.spy is unlisted, so the import binds its extraction");
        analysis.Diagnostics.GetErrors()
            .Where(d => d.Code != "SPY0313")
            .Should().BeEmpty($"{what} the program is well-formed apart from the unlisted-module refusal");
    }

    /// <summary>
    /// The 0-based position of the first whole-word occurrence of <paramref name="name"/> at or after
    /// 0-based line <paramref name="fromLine"/>.
    /// </summary>
    private static (int Line, int Character) Cursor(string source, string name, int fromLine)
    {
        var lines = source.Split('\n');
        for (var i = fromLine; i < lines.Length; i++)
        {
            var at = 0;
            while ((at = lines[i].IndexOf(name, at, StringComparison.Ordinal)) >= 0)
            {
                var before = at == 0 ? ' ' : lines[i][at - 1];
                var afterIndex = at + name.Length;
                var after = afterIndex < lines[i].Length ? lines[i][afterIndex] : ' ';
                if (!char.IsLetterOrDigit(before) && before != '_'
                    && !char.IsLetterOrDigit(after) && after != '_')
                {
                    return (i, at);
                }
                at = afterIndex;
            }
        }
        throw new InvalidOperationException($"'{name}' does not occur after line {fromLine}");
    }

    private string MainUri => new Uri(IOPath.Combine(_tempDir, "main.spy")).ToString();

    /// <summary>Writes the project and returns lib.spy's path.</summary>
    private string WriteProject(Spelling spelling, bool listed)
    {
        var libRelative = spelling.Nested ? "pkg/lib.spy" : "lib.spy";
        var libPath = IOPath.Combine(_tempDir, libRelative);
        Directory.CreateDirectory(IOPath.GetDirectoryName(libPath)!);
        File.WriteAllText(libPath, Library);
        if (spelling.Nested)
            File.WriteAllText(IOPath.Combine(_tempDir, "pkg", "__init__.spy"), string.Empty);
        File.WriteAllText(IOPath.Combine(_tempDir, "main.spy"), spelling.Main);

        var items = "        <SpyFile Include=\"main.spy\" />\n";
        if (listed)
        {
            if (spelling.Nested)
                items += "        <SpyFile Include=\"pkg/__init__.spy\" />\n";
            items += $"        <SpyFile Include=\"{libRelative}\" />\n";
        }

        File.WriteAllText(IOPath.Combine(_tempDir, "test.spyproj"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Project>\n" +
            "    <PropertyGroup>\n" +
            "        <RootNamespace>Test</RootNamespace>\n" +
            "        <OutputType>exe</OutputType>\n" +
            "    </PropertyGroup>\n" +
            "    <ItemGroup>\n" +
            items +
            "    </ItemGroup>\n" +
            "</Project>\n");
        return libPath;
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
