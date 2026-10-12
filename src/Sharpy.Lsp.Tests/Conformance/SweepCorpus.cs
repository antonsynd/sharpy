using Sharpy.Compiler;
using Sharpy.Compiler.Text;
using Sharpy.TestInfrastructure.Integration;
// The enclosing `Sharpy` namespace exposes Core's Pythonic collections, which shadow System's; SCG-qualify.
using SCG = System.Collections.Generic;
using CompilerNullLogger = Sharpy.Compiler.Logging.NullLogger;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// The corpus the LSP conformance sweeps iterate (lifted from <c>FormattingRouteParitySweepTests</c>, P47): every
/// single-file fixture under <see cref="FixtureRoots.CompilerTests"/> (<c>.skip</c> fixtures included) that lexes
/// (trivia preserved) and parses clean, keyed by its test name, plus the stable stem sampler both sweeps stride by.
/// Built once per process and shared.
/// </summary>
internal static class SweepCorpus
{
    internal sealed record Fixture(string Name, string Source);

    internal sealed record CorpusCensus(int SingleFile, IReadOnlyList<string> Unparseable, SCG.IReadOnlyDictionary<string, Fixture> Corpus);

    internal static readonly Lazy<CorpusCensus> Corpus = new(Build);

    private static CorpusCensus Build()
    {
        var single = FixtureDiscoveryHelper.DiscoverFixtures(FixtureRoots.CompilerTests.Path, includeSkipped: true)
            .Where(f => !f.IsMultiFile)
            .ToArray();
        var corpus = new SortedDictionary<string, Fixture>(StringComparer.Ordinal);
        var unparseable = new SCG.List<string>();
        foreach (var fixture in single)
        {
            var source = File.ReadAllText(fixture.SpyFilePath);
            if (!Parses(source))
                unparseable.Add(fixture.TestName);
            else
                corpus[fixture.TestName] = new Fixture(fixture.TestName, source);
        }

        return new CorpusCensus(single.Length, unparseable, corpus);
    }

    /// <summary>Whether <paramref name="text"/> lexes (trivia preserved) and parses with no error.</summary>
    internal static bool Parses(string text)
    {
        var lex = FileCompilationPipeline.Lex(new SourceText(text, "<sweep-corpus>"), CompilerNullLogger.Instance, preserveTrivia: true);
        return !lex.HasErrors && FileCompilationPipeline.Parse(lex.Tokens, CompilerNullLogger.Instance) is { HasErrors: false, Module: not null };
    }

    /// <summary>Process-independent bucket in <c>[0, stride)</c>: FNV-1a of the name (the <c>FrontEndParityTests</c> precedent).</summary>
    internal static int StableBucket(string text, int stride)
    {
        if (stride <= 1)
            return 0;
        uint hash = 2166136261;
        foreach (var ch in text)
        {
            hash ^= ch;
            hash *= 16777619;
        }

        return (int)(hash % (uint)stride);
    }
}
