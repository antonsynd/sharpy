using System.Text.RegularExpressions;
using Sharpy.BlastRadius.Probes;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Shared;

namespace Sharpy.BlastRadius;

/// <summary>One program of the corpus: a <c>.spy</c> file, or a code block of a specification page.</summary>
internal sealed record CorpusItem(string Area, string Label, string Source, string? Path, int LineOffset, string? SkipReason);

/// <summary>What happened to one corpus item.</summary>
internal sealed record ItemOutcome(CorpusItem Item, string? Skipped, IReadOnlyList<ProbeSite> Sites);

/// <summary>A named part of the corpus and where it lives, relative to the repository root.</summary>
internal sealed record CorpusArea(string Name, string RelativePath);

internal static class Corpus
{
    /// <summary>The standard corpus: every Sharpy program the repository holds.</summary>
    public static IReadOnlyList<CorpusArea> DefaultAreas { get; } = new CorpusArea[]
    {
        new("fixtures", "src/Sharpy.Compiler.Tests/Integration/TestFixtures"),
        new("conformance", "src/Sharpy.Compiler.Tests/Conformance"),
        new("stdlib", "src/Sharpy.Stdlib/spy"),
        new("stdlib-tests", "src/Sharpy.Stdlib.Tests"),
        new("benchmarks", "src/Sharpy.Compiler.Benchmarks/Corpus"),
        new("spec", "docs/language_specification"),
    };

    public const string SkipExpectedError = "a fixture that is expected to fail";
    public const string SkipFragment = "a spec block marked as a fragment or an error example";

    private static readonly Regex FenceOpen = new(@"^```(python|spy|sharpy)\s*$", RegexOptions.Compiled);
    private static readonly Regex FenceClose = new(@"^```\s*$", RegexOptions.Compiled);
    private static readonly Regex NotAProgram = new(@"<!--\s*spec-sweep:\s*(fragment|error)\b", RegexOptions.Compiled);

    /// <summary>
    /// Every item under <paramref name="root"/>, in ordinal path order: each <c>.spy</c> file, and
    /// each fenced Sharpy block of each <c>.md</c> file.
    /// </summary>
    public static IEnumerable<CorpusItem> Enumerate(string area, string root, string labelBase)
    {
        if (File.Exists(root))
        {
            foreach (var item in FromFile(area, root, labelBase))
                yield return item;
            yield break;
        }

        if (!Directory.Exists(root))
            yield break;

        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".spy", StringComparison.Ordinal) || f.EndsWith(".md", StringComparison.Ordinal))
            .Where(f => !InBuildOutput(f))
            .OrderBy(f => f, StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (var item in FromFile(area, file, labelBase))
                yield return item;
        }
    }

    private static bool InBuildOutput(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Contains("bin") || parts.Contains("obj");
    }

    private static IEnumerable<CorpusItem> FromFile(string area, string file, string labelBase)
    {
        var label = Path.GetRelativePath(labelBase, file).Replace('\\', '/');
        if (file.EndsWith(".md", StringComparison.Ordinal))
        {
            foreach (var block in SpecBlocks(area, file, label))
                yield return block;
            yield break;
        }

        yield return new CorpusItem(area, label, File.ReadAllText(file), Path.GetFullPath(file), 0,
            ExpectedToFail(file) ? SkipExpectedError : null);
    }

    /// <summary>
    /// A fixture with an <c>.error</c> or <c>.skip</c> sidecar is not a program that runs today,
    /// whatever analysis makes of it: a multi-file fixture carries the sidecar beside <c>main.spy</c>.
    /// </summary>
    private static bool ExpectedToFail(string file)
    {
        if (File.Exists(Path.ChangeExtension(file, ".error")) || File.Exists(Path.ChangeExtension(file, ".skip")))
            return true;
        var directory = Path.GetDirectoryName(file)!;
        return File.Exists(Path.Combine(directory, "main.spy"))
            && (File.Exists(Path.Combine(directory, "main.error")) || File.Exists(Path.Combine(directory, "main.skip")));
    }

    private static IEnumerable<CorpusItem> SpecBlocks(string area, string file, string label)
    {
        var lines = File.ReadAllLines(file);
        for (var i = 0; i < lines.Length; i++)
        {
            if (!FenceOpen.IsMatch(lines[i]))
                continue;
            var fenceLine = i + 1;
            var marked = i > 0 && NotAProgram.IsMatch(lines[i - 1]);
            var body = new List<string>();
            for (i++; i < lines.Length && !FenceClose.IsMatch(lines[i]); i++)
                body.Add(lines[i]);
            yield return new CorpusItem(area, $"{label}:{fenceLine}", string.Join("\n", body) + "\n",
                Path: null, LineOffset: fenceLine, marked ? SkipFragment : null);
        }
    }
}

/// <summary>Analyses corpus items with the compiler's own front end and runs one probe over each.</summary>
internal sealed class ProbeRunner
{
    private readonly CompilerApi _api = new(logger: null, DefaultReferences.Resolve());

    // Every experimental feature on: a fixture that needs one is still a program that runs today.
    private readonly CompilerOptions _options = CompilerOptionsFactory.ForLsp(
        features: FeatureFlags.None.Enable(FeatureFlags.KnownFeatures.Keys));

    private readonly IRuleProbe _probe;

    public ProbeRunner(IRuleProbe probe) => _probe = probe;

    /// <summary>
    /// The source with each top-level statement the compiler called executable blanked out where it
    /// stood and repeated, indented, in a <c>main()</c> appended at the end; null when the compiler
    /// named none.
    /// </summary>
    private string? HoistLooseStatements(string source, SemanticResult failed)
    {
        var flagged = failed.Diagnostics
            .Where(d => d.IsError && d.Code == DiagnosticCodes.Semantic.ModuleLevelExecutableStatement && d.Line != null)
            .Select(d => d.Line!.Value).ToHashSet();
        if (flagged.Count == 0 || failed.Diagnostics.Any(d => d.IsError && d.Code != DiagnosticCodes.Semantic.ModuleLevelExecutableStatement))
            return null;
        if (_api.Parse(source).Ast is not { } module)
            return null;

        var lines = source.Split('\n');
        var main = new List<string>();
        foreach (var statement in module.Body.Where(s => flagged.Contains(s.LineStart)))
        {
            for (var line = statement.LineStart; line <= statement.LineEnd && line <= lines.Length; line++)
            {
                main.Add("    " + lines[line - 1]);
                lines[line - 1] = "";
            }
        }

        return main.Count == 0
            ? null
            : string.Join("\n", lines).TrimEnd('\n') + "\n\ndef main() -> None:\n" + string.Join("\n", main) + "\n";
    }

    public ItemOutcome Run(CorpusItem item)
    {
        if (item.SkipReason != null)
            return new ItemOutcome(item, item.SkipReason, Array.Empty<ProbeSite>());

        SemanticResult result;
        try
        {
            result = item.Path != null
                ? _api.Analyze(item.Source, item.Path, _options)
                : _api.Analyze(item.Source, _options);
        }
        catch (Exception e)
        {
            return new ItemOutcome(item, $"analysis threw {e.GetType().Name}", Array.Empty<ProbeSite>());
        }

        // A specification block is often a few declarations followed by loose statements, which a
        // module cannot hold (SPY0340). Read it as the specification sweep does: the statements go
        // into a main() below, and every declaration keeps its line.
        if (!result.Success && item.Path == null && HoistLooseStatements(item.Source, result) is { } hoisted)
        {
            try
            {
                result = _api.Analyze(hoisted, _options);
            }
            catch (Exception e)
            {
                return new ItemOutcome(item, $"analysis threw {e.GetType().Name}", Array.Empty<ProbeSite>());
            }
        }

        if (!result.Success || result.Ast == null || result.SemanticInfo == null || result.SymbolTable == null)
        {
            var code = result.Diagnostics.FirstOrDefault(d => d.IsError)?.Code ?? "no code";
            return new ItemOutcome(item, $"does not compile today ({code})", Array.Empty<ProbeSite>());
        }

        var sites = _probe.Probe(new ProbeInput(result.Ast, result.SemanticInfo, result.SymbolTable))
            .Select(s => s with { Line = s.Line + item.LineOffset })
            .OrderBy(s => s.Line).ThenBy(s => s.Subject, StringComparer.Ordinal)
            .ToList();
        return new ItemOutcome(item, null, sites);
    }
}
