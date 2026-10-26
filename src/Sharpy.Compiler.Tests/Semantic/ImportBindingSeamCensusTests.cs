using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Pins the ONE import-binding seam (P21g task 0, R-JC; #2359): every import spelling binds its
/// module-level name through <c>ImportResolver.BindImportName</c>, and the only route that calls it is
/// <c>ProjectCompiler</c> Phase 4 — every command (<c>run</c>, <c>emit</c>, <c>project</c>, analyze, the
/// LSP) compiles through it. The single-file import route (<c>ImportResolver</c>'s whole-module resolve
/// loop, <c>FileCompilationPipeline</c>'s import phase and its result struct, the from-import define
/// helper) had no production caller and was deleted; a second binding route would let the two
/// disagree again (W1).
/// </summary>
/// <remarks>
/// A source scan of <c>src/Sharpy.Compiler/**/*.cs</c> (the <c>EmitterBannedTokenScanTests</c> walk).
/// The call-site count is anchored to a LITERAL, so a new caller fails here and its author decides
/// whether it is a second route. The absence rows scan raw text (comments too — a comment naming the
/// deleted route is stale); their positive control is the call-site row, which must find the four
/// live calls in the same scan. The deleted names are spelled in two halves below so the repo-wide
/// grep for them stays at zero hits.
/// </remarks>
public class ImportBindingSeamCensusTests
{
    /// <summary>The live call sites, measured after the deletion (base 75c65ee29): Phases.cs :233, :293, :335, :407.</summary>
    private const int ExpectedBindImportNameCallSites = 4;

    private const string SeamFile = "Project/ProjectCompiler.Phases.cs";

    private const string CallToken = "BindImportName(";

    /// <summary>The definition, excluded by its signature (not by its file).</summary>
    private const string DefinitionSignature = "static bool BindImportName(";

    private static string FindCompilerSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", "Sharpy.Compiler");
            if (File.Exists(Path.Combine(path, "Sharpy.Compiler.csproj")))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("src/Sharpy.Compiler not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Every production source file: (path relative to src/Sharpy.Compiler with '/' separators, lines).</summary>
    private static List<(string File, string[] Lines)> ProductionSources()
    {
        var root = FindCompilerSourceDirectory();
        var sep = Path.DirectorySeparatorChar;
        return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .Select(f => (Path.GetRelativePath(root, f).Replace('\\', '/'), File.ReadAllLines(f)))
            .OrderBy(t => t.Item1, StringComparer.Ordinal)
            .ToList();
    }

    private static string StripLineComment(string line)
    {
        var idx = line.IndexOf("//", StringComparison.Ordinal);
        return idx >= 0 ? line.Substring(0, idx) : line;
    }

    /// <summary>Every <c>BindImportName(</c> call in comment-stripped code, as <c>file:line</c>.</summary>
    private static List<(string File, int Line)> BindImportNameCallSites(List<(string File, string[] Lines)> sources)
    {
        var hits = new List<(string, int)>();
        foreach (var (file, lines) in sources)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var code = StripLineComment(lines[i]);
                if (code.Contains(CallToken, StringComparison.Ordinal)
                    && !code.Contains(DefinitionSignature, StringComparison.Ordinal))
                    hits.Add((file, i + 1));
            }
        }
        return hits;
    }

    [Fact]
    public void BindImportName_IsCalledOnlyByProjectCompilerPhase4()
    {
        var sources = ProductionSources();
        sources.Count.Should().BeGreaterThan(100, "the scan must read the compiler's sources");
        sources.Should().Contain(s => s.File == SeamFile);

        var definitions = sources.SelectMany(s => s.Lines.Where(l => StripLineComment(l).Contains(DefinitionSignature, StringComparison.Ordinal)))
            .Count();
        definitions.Should().Be(1, "the seam is defined once (ImportResolver.Symbols.cs)");

        var sites = BindImportNameCallSites(sources);
        var listing = string.Join("\n", sites.Select(s => $"{s.File}:{s.Line}"));
        sites.Should().HaveCount(ExpectedBindImportNameCallSites,
            $"every import spelling binds through the one seam at its {ExpectedBindImportNameCallSites} Phase 4 call "
            + "sites (import-as, plain/dotted import, star, explicit from-import); a new caller is a second "
            + $"binding route — re-measure and re-anchor only if it is not\n{listing}");
        sites.Should().OnlyContain(s => s.File == SeamFile,
            $"BindImportName is called from ProjectCompiler Phase 4 only\n{listing}");
    }

    public static IEnumerable<object[]> DeletedRouteNames() => new[]
    {
        // The deleted whole-module resolve loop, its pipeline result struct and its from-import helper.
        new object[] { "Resolve" + "AllImports" },
        new object[] { "Import" + "ResolveResult" },
        new object[] { "TryDefine" + "FromImport" },
    };

    [Theory]
    [MemberData(nameof(DeletedRouteNames))]
    public void DeletedSingleFileImportRoute_NamesNothing(string name)
    {
        var sources = ProductionSources();
        // Positive control in the same scan: the scanner sees the live seam's calls.
        BindImportNameCallSites(sources).Should().NotBeEmpty("the scan must see the live BindImportName calls");

        var hits = sources.SelectMany(s => s.Lines.Select((l, i) => (s.File, Line: i + 1, Text: l)))
            .Where(h => h.Text.Contains(name, StringComparison.Ordinal))
            .Select(h => $"{h.File}:{h.Line}: {h.Text.Trim()}")
            .ToList();
        hits.Should().BeEmpty($"'{name}' belonged to the deleted single-file import route (R-JC)");
    }

    [Fact]
    public void ResolveImports_IsOnlyProjectCompilersOwnPhase4()
    {
        // FileCompilationPipeline's import phase is gone; ProjectCompiler's private Phase 4 method of
        // the same name is the live route, so the token may appear only in ProjectCompiler's partials
        // (its definition and its two callers) — and must appear there (the positive control).
        var sources = ProductionSources();
        var hits = sources.SelectMany(s => s.Lines.Select((l, i) => (s.File, Line: i + 1, Code: StripLineComment(l))))
            .Where(h => h.Code.Contains("ResolveImports(", StringComparison.Ordinal))
            .ToList();
        hits.Should().Contain(h => h.File == SeamFile && h.Code.Contains("bool ResolveImports(", StringComparison.Ordinal),
            "ProjectCompiler's Phase 4 method is the live import route");
        hits.Where(h => !h.File.StartsWith("Project/ProjectCompiler", StringComparison.Ordinal))
            .Select(h => $"{h.File}:{h.Line}: {h.Code.Trim()}")
            .Should().BeEmpty("no import phase outside ProjectCompiler binds imports");
    }
}
