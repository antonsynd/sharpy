using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// The benchmark corpus compiles — enforced in the default suite, so PR CI notices (#1337).
///
/// <para>
/// <c>CorpusGuard</c> already asserts this at benchmark setup time, which is where it belongs: a
/// benchmark whose input fails to compile times an early return under a label claiming a full
/// pipeline (#1224). But those assertions only run when a benchmark runs, and CI runs 5 of the 18
/// benchmark rows — a corpus member used only by an excluded row could rot for months with nothing
/// to say so, which is the shape of #1140 all over again.
/// </para>
///
/// <para>
/// So this is a deliberate small duplication of <c>CorpusGuard.AssertCompiles</c>. The alternative
/// — a project reference from the test project to <c>Sharpy.Compiler.Benchmarks</c> — would drag
/// BenchmarkDotNet into every test run to share four lines. The corpus is discovered by glob, not
/// listed, so a new corpus file is covered the moment it lands and no list can drift out of sync.
/// </para>
///
/// <para>
/// Scope: compilability of the BDN corpus. Widening CI to run more benchmark rows is the other
/// half of #1337 and is a CI-minutes decision, recorded in <c>benchmarks/BASELINE.md</c> as
/// deliberately not taken here.
/// </para>
/// </summary>
public class BenchmarkCorpusCompilesTests
{
    private readonly ITestOutputHelper _output;

    public BenchmarkCorpusCompilesTests(ITestOutputHelper output) => _output = output;

    public static TheoryData<string> CorpusFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in EnumerateCorpus())
            data.Add(Path.GetFileName(file));
        return data;
    }

    [Fact]
    public void BenchmarkCorpus_IsDiscoverable()
    {
        var corpus = EnumerateCorpus();

        // Without this the theory below would pass by having no cases — the exact way a guard
        // stops guarding without failing.
        corpus.Should().NotBeEmpty(
            $"the benchmark corpus at {CorpusDirectory()} is what the #1224 setup guards read");
    }

    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public void BenchmarkCorpusMember_Compiles(string fileName)
    {
        var path = Path.Combine(CorpusDirectory(), fileName);
        var source = File.ReadAllText(path);

        // Same configuration the benchmark methods use: `new Compiler()`, no extra references.
        var result = new Compiler().Compile(source, fileName);

        var errors = result.Diagnostics.GetErrors()
            .Select(d => $"{d.Code} {d.Message}")
            .ToList();
        foreach (var error in errors)
            _output.WriteLine(error);

        result.Success.Should().BeTrue(
            $"benchmark input '{fileName}' must compile — timing it otherwise measures an early "
            + "return rather than a compilation (#1224). Errors: " + string.Join(" | ", errors));
    }

    private static IReadOnlyList<string> EnumerateCorpus()
    {
        var dir = CorpusDirectory();
        return Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.spy").OrderBy(f => f, StringComparer.Ordinal).ToList()
            : Array.Empty<string>();
    }

    private static string CorpusDirectory() => Path.GetFullPath(Path.Combine(
        FixtureRoots.RepositoryRoot, "src", "Sharpy.Compiler.Benchmarks", "Corpus"));
}
