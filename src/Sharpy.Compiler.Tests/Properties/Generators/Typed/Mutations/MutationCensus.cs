using System.Collections.Concurrent;
using System.Text;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// The drawn-code table (#2324, P63b Phase 1 Task 4): for every (lane, base) row of
/// <see cref="MutationLanes.Census"/>, <see cref="PropertyIterations.Override"/> well-typed samples of the
/// base (through <see cref="SemanticFilter.WellTypedProgram"/> with a sink), the lane's break applied,
/// the result analysed through <see cref="PropertyAnalysis.Codes"/>, and the codes bucketed. It is a
/// REPORT, not a guard: it decides nothing and asserts only that it ran. A lane is wired in
/// <c>ErrorInjector.Mutations.cs</c> with the code its row draws at ≥ 99 %; two codes is a bad injection
/// or a cascade cell; CLEAN is a by-design candidate or a missing refusal; a throw is an ICE cell. Runs
/// only when <see cref="PropertyIterations.Variable"/> is set (skipped in the suite and the gate); set
/// <c>SHARPY_CENSUS_OUT</c> to a file path to keep the table (appended row by row) and <c>SHARPY_CENSUS_FILTER</c> to run a chunk.
/// </summary>
[Trait("Category", "Property")]
[Trait("Category", "Census")]
[Collection("PropertySerial")]
public class MutationCensus
{
    private readonly ITestOutputHelper _output;

    public MutationCensus(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableFact]
    public void DrawnCodes_PerLaneAndBase()
    {
        Skip.IfNot(PropertyIterations.Override is not null, $"set {PropertyIterations.Variable} to run the mutation census");
        var iter = PropertyIterations.Override!.Value;
        // SHARPY_CENSUS_FILTER: '|'-separated substrings of "Lane @ Base" (a chunk of the matrix — a whole
        // 600-sample census outruns the dotnet wrapper's silence watchdog, so it runs in chunks).
        var filters = (Environment.GetEnvironmentVariable("SHARPY_CENSUS_FILTER") ?? "")
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool Selected(string key) => filters.Length == 0 || filters.Any(f => key.Contains(f, StringComparison.Ordinal));
        var path = Environment.GetEnvironmentVariable("SHARPY_CENSUS_OUT");
        var report = new StringBuilder();
        void Emit(string text)
        {
            report.AppendLine(text);
            if (path is { Length: > 0 })
                File.AppendAllText(path, text + "\n"); // each row lands as it completes
        }
        Emit($"# Mutation census — {iter} samples per row{(filters.Length > 0 ? $" (filter: {string.Join(" | ", filters)})" : "")}\n");
        int rows = 0;

        foreach (var row in MutationLanes.Census)
        {
            if (!Selected($"{row.Lane} @ {row.Base}"))
                continue;
            rows++;
            Emit(Measure(row.Lane, row.Base, row.Generator(), row.Mutate, iter));
        }

        if (Selected("WrongTypedLibraryConstant @ ImportingModule"))
        {
            rows++;
            Emit(MeasureProject(iter));
        }

        _output.WriteLine(report.ToString());
        Assert.True(rows > 0, "the census measured no row");
    }

    private static string Measure(string lane, string @base, Gen<Module> generator, Func<Module, Module?> mutate, int iter)
    {
        var sink = new RejectionSink($"census {lane} @ {@base}");
        var buckets = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var firstSource = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var verdicts = new ConcurrentDictionary<string, PropertyAnalysis.Drawn>(StringComparer.Ordinal);
        var distinct = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        int drawn = 0;
        int applicable = 0;

        SemanticFilter.WellTypedProgram(generator, sink).Sample(module =>
        {
            Interlocked.Increment(ref drawn);
            string key;
            string source;
            try
            {
                if (mutate(module) is not { } mutated)
                    return;
                source = Sharpy.Compiler.Pretty.Unparser.Unparse(mutated);
                key = source == Sharpy.Compiler.Pretty.Unparser.Unparse(AstRewriter.Normalize(module))
                    ? "UNCHANGED SOURCE — the mutation never reached the judged text (inert)"
                    : verdicts.GetOrAdd(source, PropertyAnalysis.Codes).Bucket;
            }
            catch (Exception e)
            {
                source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                key = $"LANE THREW {e.GetType().Name}: {e.Message}";
            }
            Interlocked.Increment(ref applicable);
            distinct.TryAdd(source, 0);
            buckets.AddOrUpdate(key, 1, (_, n) => n + 1);
            firstSource.TryAdd(key, source);
        }, iter: iter);

        return Format(lane, @base, drawn, applicable, distinct.Count, buckets, firstSource, sink.Summary);
    }

    private static string MeasureProject(int iter)
    {
        var sink = new RejectionSink("census WrongTypedLibraryConstant @ ImportingModule");
        var buckets = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var firstSource = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var distinct = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        int drawn = 0;
        int applicable = 0;
        var project = SemanticFilter.CompilableProject(GenImports.ImportingModule(TypeEnv.Default, fuel: 1),
            p => new[] { ("main.spy", p.MainSource), ("lib.spy", p.LibSource) }, sink);

        project.Sample(sample =>
        {
            Interlocked.Increment(ref drawn);
            if (MutationLanes.WrongTypedLibraryConstant(sample) is not { } mutated)
                return;
            var files = new[] { ("main.spy", mutated.MainSource), ("lib.spy", mutated.LibSource) };
            var source = string.Join("\n", files.Select(f => $"# --- {f.Item1}\n{f.Item2}"));
            var key = PropertyAnalysis.ProjectCodes(files).Bucket;
            Interlocked.Increment(ref applicable);
            distinct.TryAdd(source, 0);
            buckets.AddOrUpdate(key, 1, (_, n) => n + 1);
            firstSource.TryAdd(key, source);
        }, iter: iter);

        return Format("WrongTypedLibraryConstant", "ImportingModule", drawn, applicable, distinct.Count, buckets, firstSource, sink.Summary);
    }

    private static string Format(string lane, string @base, int drawn, int applicable, int distinct,
        IReadOnlyDictionary<string, int> buckets, IReadOnlyDictionary<string, string> firstSource, string sink)
    {
        var text = new StringBuilder();
        var ordered = buckets.OrderByDescending(b => b.Value).ThenBy(b => b.Key, StringComparer.Ordinal).ToList();
        text.AppendLine($"## {lane} @ {@base}: applicable {applicable}/{drawn}, {distinct} distinct mutated source(s)");
        foreach (var (bucket, count) in ordered)
            text.AppendLine($"- {count,4} × {bucket}");
        text.AppendLine($"- sink: {sink}");
        foreach (var (bucket, _) in ordered.Skip(1).Concat(ordered.Take(1)))
        {
            text.AppendLine($"<details><summary>first source: {bucket}</summary>");
            text.AppendLine();
            text.AppendLine("```");
            text.AppendLine(firstSource[bucket].TrimEnd());
            text.AppendLine("```");
            text.AppendLine("</details>");
        }
        return text.ToString();
    }
}
