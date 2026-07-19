using System.Linq;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Covers the parse-once handoff (#1086): the import-closure discovery pass stashes its clean
/// parse results in <see cref="ProjectConfig.PreParsedUnits"/> and <c>ParseAllFiles</c> reuses
/// them instead of lexing and parsing each file a second time.
/// </summary>
public class PreParsedUnitReuseTests : IDisposable
{
    private readonly string _tempDir;

    public PreParsedUnitReuseTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_preparsed_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// Sentinel: when a <see cref="PreParsedUnit"/> is present, the parse phase must use the
    /// cached AST/source and never re-read or re-parse the on-disk file. We prove it by putting a
    /// DIFFERENT program on disk than the one cached — if the cache were silently ignored the
    /// generated C# would reflect the disk program.
    /// </summary>
    [Fact]
    public void ParseAllFiles_PreParsedUnit_UsesCachedAstNotDiskReparse()
    {
        var filePath = Path.Combine(_tempDir, "main.spy");

        // On disk: a program that, if re-parsed, would change the generated C#.
        File.WriteAllText(filePath, "def disk_marker() -> int:\n    return 1\n");

        // Cached (discovery-pass) artifacts built from a distinct source.
        const string cachedSource = "def cached_marker() -> int:\n    return 1\n";
        var preParsed = BuildPreParsed(cachedSource, filePath);

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            ProjectFilePath = filePath,
            RootNamespace = string.Empty,
            SourceFiles = new List<string> { filePath },
            PreParsedUnits = new Dictionary<string, PreParsedUnit>(StringComparer.OrdinalIgnoreCase)
            {
                [filePath] = preParsed
            }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);
        var result = compiler.Compile(config, CancellationToken.None, emitAssembly: false);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        var generated = string.Join("\n", result.GeneratedCSharpFiles.Values);
        Assert.Contains("CachedMarker", generated);
        Assert.DoesNotContain("DiskMarker", generated);
    }

    /// <summary>
    /// Clean-only rule: discovery caches only pristine parses, so an imported file that contains a
    /// syntax error is absent from <see cref="ProjectConfig.PreParsedUnits"/> and <c>ParseAllFiles</c>
    /// re-parses it — surfacing its parse diagnostics that discovery swallowed. Compiling the clean
    /// importing entry file must still report the broken import's syntax error.
    /// </summary>
    [Fact]
    public void CleanOnlyCache_ImportedFileWithSyntaxError_StillReportsParseDiagnostics()
    {
        var brokenPath = Path.Combine(_tempDir, "broken.spy");
        var mainPath = Path.Combine(_tempDir, "main.spy");

        // broken.spy has a real parser error (malformed parameter list): discovery parses it
        // best-effort, sees diagnostics, and must NOT cache it.
        File.WriteAllText(brokenPath, "def broken(:\n    pass\n");
        // main.spy is clean and imports the broken module.
        File.WriteAllText(mainPath, "import broken\n\nprint(\"ok\")\n");

        var api = new CompilerApi();
        var result = api.Compile(File.ReadAllText(mainPath), filePath: mainPath);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,
            d => d.IsError && d.Phase == CompilerPhase.Parser);
    }

    private static PreParsedUnit BuildPreParsed(string source, string filePath)
    {
        var sourceText = new Sharpy.Compiler.Text.SourceText(source, filePath);
        var lexer = new Sharpy.Compiler.Lexer.Lexer(sourceText);
        var tokens = lexer.TokenizeAll();
        Assert.False(lexer.Diagnostics.HasErrors, "test setup source must lex cleanly");

        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();
        Assert.False(parser.Diagnostics.HasErrors, "test setup source must parse cleanly");

        return new PreParsedUnit(source, tokens, module!, null);
    }
}
