using System.Text.RegularExpressions;
using Sharpy.BlastRadius.Probes;
using Sharpy.Compiler.Project;

namespace Sharpy.BlastRadius;

/// <summary>
/// One program of the corpus: a <c>.spy</c> file, or a code block of a specification page.
/// </summary>
/// <param name="Source">The text handed to the compiler.</param>
/// <param name="Path">The file, for a <c>.spy</c> file; null for a code block.</param>
/// <param name="LineOffset">Added to a line of the block to give its line in the page.</param>
/// <param name="SkipReason">Why the item is not analysed, when it is known before analysis.</param>
/// <param name="PreludeLines">
/// How many leading lines of <paramref name="Source"/> are declarations prepended from earlier
/// blocks of the page. A site there belongs to the block that declares it.
/// </param>
internal sealed record CorpusItem(
    string Area, string Label, string Source, string? Path, int LineOffset, string? SkipReason, int PreludeLines = 0);

/// <summary>
/// What happened to one corpus item. <paramref name="Skipped"/> is the reason it gave no sites;
/// <paramref name="ErrorCode"/> is the first error of an item that does not compile.
/// </summary>
internal sealed record ItemOutcome(
    CorpusItem Item, string? Skipped, IReadOnlyList<ProbeSite> Sites, string? ErrorCode = null)
{
    public const string DoesNotCompile = "does not compile today";
}

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

    // The markers of the specification sweep (build_tools/spec_blocks.py), which owns their meaning.
    private static readonly Regex FenceOpen = new(@"^```(python|spy|sharpy)\s*$", RegexOptions.Compiled);
    private static readonly Regex FenceClose = new(@"^```\s*$", RegexOptions.Compiled);
    private static readonly Regex NotAProgram = new(@"<!--\s*spec-sweep:\s*(fragment|error)\b", RegexOptions.Compiled);
    private static readonly Regex Prelude = new(@"<!--\s*spec-sweep:\s*prelude\s*-->", RegexOptions.Compiled);

    /// <summary>
    /// Every item under <paramref name="root"/> (a directory, or one file), in ordinal path order:
    /// each <c>.spy</c> file, and each fenced Sharpy block of each <c>.md</c> file.
    /// </summary>
    public static IEnumerable<CorpusItem> Enumerate(string area, string root, string labelBase)
    {
        if (File.Exists(root))
            return FromFile(area, root, Path.GetDirectoryName(Path.GetFullPath(root))!, labelBase);

        // The compiler's own source glob: it leaves out bin, obj and crash-bundle copies (#1660).
        return SourceGlob.EnumerateSourceFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".spy", StringComparison.Ordinal) || f.EndsWith(".md", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal)
            .SelectMany(file => FromFile(area, file, root, labelBase));
    }

    private static IEnumerable<CorpusItem> FromFile(string area, string file, string root, string labelBase)
    {
        var label = Path.GetRelativePath(labelBase, file).Replace('\\', '/');
        if (file.EndsWith(".md", StringComparison.Ordinal))
            return SpecBlocks(area, file, label);

        return new[]
        {
            new CorpusItem(area, label, File.ReadAllText(file), Path.GetFullPath(file), 0,
                ExpectedToFail(file, root) ? SkipExpectedError : null),
        };
    }

    /// <summary>
    /// A fixture with an <c>.error</c> or <c>.skip</c> sidecar is not a program that runs today,
    /// whatever analysis makes of it. A multi-file fixture carries the sidecar as <c>main.error</c>
    /// or <c>main.skip</c> in its own directory, and every file below that directory is part of it.
    /// </summary>
    private static bool ExpectedToFail(string file, string root)
    {
        if (File.Exists(Path.ChangeExtension(file, ".error")) || File.Exists(Path.ChangeExtension(file, ".skip")))
            return true;

        var top = Path.GetFullPath(root);
        for (var directory = Path.GetDirectoryName(Path.GetFullPath(file));
             directory != null && directory.StartsWith(top, StringComparison.Ordinal);
             directory = Path.GetDirectoryName(directory))
        {
            if (File.Exists(Path.Combine(directory, "main.error")) || File.Exists(Path.Combine(directory, "main.skip")))
                return true;
        }

        return false;
    }

    private static IEnumerable<CorpusItem> SpecBlocks(string area, string file, string label)
    {
        var lines = File.ReadAllLines(file);
        // A block marked as a prelude declares names for every later block of the page.
        var preludes = new List<List<string>>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!FenceOpen.IsMatch(lines[i]))
                continue;
            var fenceLine = i + 1;
            var marker = i > 0 ? lines[i - 1] : "";
            var body = new List<string>();
            for (i++; i < lines.Length && !FenceClose.IsMatch(lines[i]); i++)
                body.Add(lines[i]);

            if (NotAProgram.IsMatch(marker))
            {
                yield return new CorpusItem(area, $"{label}:{fenceLine}", string.Join("\n", body) + "\n",
                    Path: null, LineOffset: fenceLine, SkipFragment);
                continue;
            }

            // The earlier preludes, a blank line, then the block: as the sweep compiles it.
            var above = preludes.SelectMany(p => p.Append("")).ToList();
            yield return new CorpusItem(area, $"{label}:{fenceLine}", string.Join("\n", above.Concat(body)) + "\n",
                Path: null, LineOffset: fenceLine, SkipReason: null, PreludeLines: above.Count);
            if (Prelude.IsMatch(marker))
                preludes.Add(body);
        }
    }
}
