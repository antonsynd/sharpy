using System.Collections.Concurrent;
using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Helpers;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// The generated lane's filter (#2288, P63 Phase 3): passes on the samples the compiler accepts and
/// DISCARDS the rest — but never silently. Every overload takes a <see cref="RejectionSink"/> that
/// records each discarded sample (its source and error codes, or the exception), so the property
/// that reads the filtered generator asserts the sink's <see cref="RejectionSink.Unexplained"/> is
/// empty: a refused generated program is a red, not a skipped sample. There is no overload without a
/// sink — a kept sink-less overload is the inert path (verification-contract.md §2).
/// </summary>
internal static class SemanticFilter
{
    // Each filter forces a full GC after every sample, lexically inside its own body:
    // ProcessGlobalStateConformanceTests rows each filter as a Helper, so every caller must run alone
    // in a serial collection (#2254, #2262).

    /// <summary>Samples whose unparsed source <c>Analyze</c>s clean.</summary>
    public static Gen<Module> WellTypedProgram(Gen<Module> source, RejectionSink sink) =>
        source.Where(module =>
        {
            try
            {
                return AdmitWellTyped(module, sink);
            }
            finally
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
            }
        });

    /// <summary>
    /// Samples whose <paramref name="program"/> <c>Analyze</c>s clean, where a sample the caller will not
    /// read as a program — <paramref name="program"/> returns null — passes WITHOUT analysis and without
    /// reaching <paramref name="sink"/> (#2324, R-IX). An injection lane draws its base and decides
    /// applicability on the raw AST first, so a lane that applies to a tenth of its base's samples pays a
    /// tenth of the analyses instead of all of them; the samples it judges are the same draws. The forced
    /// GC follows each analysis, as in the other filters.
    /// </summary>
    public static Gen<T> WellTypedProgram<T>(Gen<T> source, Func<T, Module?> program, RejectionSink sink) =>
        source.Where(sample =>
        {
            if (program(sample) is not { } module)
                return true;
            try
            {
                return AdmitWellTyped(module, sink);
            }
            finally
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
            }
        });

    /// <summary>
    /// The admission rule of <see cref="WellTypedProgram"/> for ONE sample, without the forced GC — for a
    /// lane that collects its own programs in a loop (the differential sweep's generated arm, which runs
    /// in a parallel class): true when the unparsed <paramref name="module"/> <c>Analyze</c>s clean,
    /// otherwise the sample is recorded on <paramref name="sink"/> exactly as the filter records it.
    /// </summary>
    public static bool AdmitWellTyped(Module module, RejectionSink sink) =>
        Admit(sink, () => Sharpy.Compiler.Pretty.Unparser.Unparse(module), module, AnalyseSingleFile);

    /// <summary>Samples whose unparsed source <c>Compile</c>s clean (front end and C# emit).</summary>
    public static Gen<Module> CompilableProgram(Gen<Module> source, RejectionSink sink) =>
        source.Where(module =>
        {
            try
            {
                return Admit(sink, () => Sharpy.Compiler.Pretty.Unparser.Unparse(module), module, unparsed =>
                {
                    var result = new Sharpy.Compiler.Compiler().Compile(unparsed, "filter_test.spy");
                    return (result.Success, result.Diagnostics.GetAll().ToList());
                });
            }
            finally
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
            }
        });

    /// <summary>A source-text generator's samples that <c>Analyze</c> clean. No AST origin, so no
    /// parse pin can explain a rejection.</summary>
    public static Gen<string> WellTypedSource(Gen<string> source, RejectionSink sink) =>
        source.Where(text =>
        {
            try
            {
                return Admit(sink, () => text, origin: null, AnalyseSingleFile);
            }
            finally
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
            }
        });

    /// <summary>A multi-file generator's samples whose files compile clean as one project (root
    /// namespace <c>Test</c>, <see cref="ProjectCompilationHelper"/> — the import lanes' route). No
    /// AST origin, so no parse pin can explain a rejection.</summary>
    public static Gen<T> CompilableProject<T>(
        Gen<T> source, Func<T, IReadOnlyList<(string Path, string Content)>> files, RejectionSink sink) =>
        source.Where(sample =>
        {
            try
            {
                var projectFiles = files(sample);
                return Admit(sink, () => string.Join("\n", projectFiles.Select(f => $"# --- {f.Path}\n{f.Content}")),
                    origin: null, _ => PropertyAnalysis.AnalyseProject(projectFiles));
            }
            finally
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
            }
        });

    private static (bool Success, List<CompilerDiagnostic> All) AnalyseSingleFile(string source) =>
        PropertyAnalysis.AnalyseSingleFile(source);

    /// <summary>The one admission rule: render the sample, run the compiler, and either accept it or
    /// record why not. A throw anywhere (render or compile) is a rejection with its exception, never
    /// explained by a pin.</summary>
    private static bool Admit(
        RejectionSink sink,
        Func<string> render,
        Node? origin,
        Func<string, (bool Success, List<CompilerDiagnostic> All)> run)
    {
        string source = "<sample did not render>";
        try
        {
            source = render();
            var (success, all) = run(source);
            var errors = all.Where(d => d.IsError)
                .Select(d => d.Code ?? "(no code)")
                .Distinct()
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();
            if (success && errors.Count == 0)
            {
                sink.Accept();
                return true;
            }

            var pin = origin is null ? null : KnownGeneratorPins.Explain(origin, source);
            sink.Reject(new RejectionSink.Rejection(source, errors, null, pin));
        }
        catch (Exception e)
        {
            sink.Reject(new RejectionSink.Rejection(source, Array.Empty<string>(), e, null));
        }
        ThrowIfEverySampleIsRefused(sink);
        return false;
    }

    /// <summary>
    /// A generator whose every sample is refused makes CsCheck's <c>Where</c> give up after
    /// <see cref="Check.WhereLimit"/> rejected draws with "Failing Where max count" — a red that names no
    /// code and no source (#2324, P63b Decision 13). One draw before that, with no sample accepted yet,
    /// the filter throws <see cref="RejectionBudgetExceeded"/> carrying the sink's report instead: the code
    /// buckets and the first refused sources.
    /// </summary>
    private static void ThrowIfEverySampleIsRefused(RejectionSink sink)
    {
        if (sink.Accepted == 0 && sink.Rejections.Count >= Check.WhereLimit - 1)
            throw new RejectionBudgetExceeded(sink.Report());
    }
}

/// <summary>Every sample the filter drew was refused: the message is the sink's report (#2324).</summary>
internal sealed class RejectionBudgetExceeded(string report)
    : Exception("every sample of a filtered generator was refused — " + report);

/// <summary>
/// What <see cref="SemanticFilter"/> discarded (#2288): one <see cref="Rejection"/> per refused or
/// throwing sample, beside the count of every sample drawn. Concurrent-safe — CsCheck samples on
/// several threads. A rejection is EXPLAINED only by a pin in <see cref="KnownGeneratorPins"/>; the
/// property asserts <c>Assert.True(sink.Unexplained.Count == 0, sink.Report())</c> and prints
/// <see cref="Summary"/>.
/// </summary>
internal sealed class RejectionSink
{
    /// <summary>A discarded sample: its source, its distinct error codes (empty for a throw or a
    /// failure without a code), the exception if one was thrown, and the pin that explains it.</summary>
    public sealed record Rejection(
        string Source,
        IReadOnlyList<string> Codes,
        Exception? Exception,
        KnownGeneratorPins.ParsePin? Pin)
    {
        public string Describe() =>
            Exception is { } e
                ? $"threw {e.GetType().Name}: {e.Message}"
                : Codes.Count == 0 ? "refused without an error code" : string.Join(", ", Codes);
    }

    private readonly ConcurrentQueue<Rejection> _rejections = new();
    private int _drawn;

    public RejectionSink(string label)
    {
        Label = label;
    }

    public string Label { get; }

    /// <summary>Every sample the filter judged, accepted or rejected.</summary>
    public int Drawn => Volatile.Read(ref _drawn);

    public IReadOnlyList<Rejection> Rejections => _rejections.ToArray();

    /// <summary>Rejections no declared pin explains — the property's red.</summary>
    public IReadOnlyList<Rejection> Unexplained => Rejections.Where(r => r.Pin is null).ToList();

    /// <summary>Every sample the filter admitted.</summary>
    public int Accepted => Volatile.Read(ref _accepted);

    private int _accepted;

    internal void Accept()
    {
        Interlocked.Increment(ref _accepted);
        Interlocked.Increment(ref _drawn);
    }

    internal void Reject(Rejection rejection)
    {
        _rejections.Enqueue(rejection);
        Interlocked.Increment(ref _drawn);
    }

    /// <summary><c>label: rejections k/N (pinned p [#N ×p], unexplained u)</c>.</summary>
    public string Summary
    {
        get
        {
            var all = Rejections;
            var pinned = all.Where(r => r.Pin is not null)
                .GroupBy(r => r.Pin!.Pin.Issue)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key} ×{g.Count()}")
                .ToList();
            var pinnedCount = all.Count(r => r.Pin is not null);
            return $"{Label}: rejections {all.Count}/{Drawn} (pinned {pinnedCount}"
                + (pinned.Count > 0 ? $" [{string.Join(", ", pinned)}]" : "")
                + $", unexplained {all.Count - pinnedCount})";
        }
    }

    /// <summary><see cref="Summary"/>, the code buckets of the unexplained rejections, and the first
    /// <paramref name="first"/> unexplained sources.</summary>
    public string Report(int first = 3)
    {
        var unexplained = Unexplained;
        var lines = new List<string> { Summary };
        if (unexplained.Count == 0)
            return Summary;

        lines.Add("unexplained by bucket: " + string.Join("; ", unexplained
            .GroupBy(r => r.Describe())
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} ×{g.Count()}")));
        lines.Add("a generated program the compiler refuses is a generator defect (fix the generator) or a "
            + "compiler cell (pin it in KnownGeneratorPins with its tracker) — never a skipped sample");
        foreach (var r in unexplained.Take(first))
        {
            lines.Add($"--- [{r.Describe()}] ---");
            lines.Add(r.Source);
            if (r.Exception is { } e)
                lines.Add(e.ToString());
        }
        return string.Join("\n", lines);
    }
}
