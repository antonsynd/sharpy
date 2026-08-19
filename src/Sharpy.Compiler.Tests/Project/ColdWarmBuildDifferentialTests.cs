using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// The batch's class-killer, stated as one cell: a warm <c>--incremental</c> build must be
/// OBSERVATIONALLY IDENTICAL to a cold one.
///
/// <para>Every bug in this area (#1474, #1444, and the #1309 family before them) has the same
/// shape — the second build knows something different from the first, and nothing says so. Unit
/// tests over the serializer catch a fact at a time; this catches the property. The observable is
/// the pair (diagnostics, generated C#), which is the materialization of every symbol fact
/// codegen and validation actually read: a fact that is dropped or mis-decoded either changes the
/// emitted C# or changes what is reported, or it was not a fact anyone consulted.</para>
///
/// <para>The specimen deliberately carries one instance of each thing the two issues broke:
/// <c>long</c> (the #1474 alias whose encoded name <c>int64</c> had no decoder arm), a
/// <c>@must_use</c> method, a <c>@final</c> field, and a <c>@dataclass</c> (four of #1444's ten
/// dropped facts), spread across two files so the third build can recompile one and serve the
/// other from cache.</para>
/// </summary>
public class ColdWarmBuildDifferentialTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public ColdWarmBuildDifferentialTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_coldwarm_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        GC.SuppressFinalize(this);
    }

    private const string LibSource = @"@dataclass
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int) -> None:
        self.x = x
        self.y = y


class Counter:
    @final
    limit: long

    def __init__(self, limit: long) -> None:
        self.limit = limit

    @must_use
    def next_value(self) -> long:
        return self.limit
";

    private const string MainSource = @"from lib import Counter, Point


def main() -> None:
    c = Counter(10)
    total: long = c.next_value()
    p = Point(1, 2)
    print(total)
    print(p.x)
";

    private string Write(string area, string name, string content)
    {
        var dir = Path.Combine(_tempDir, area);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private ProjectConfig Config(string area, params string[] sourceFiles)
    {
        var dir = Path.Combine(_tempDir, area);
        return new ProjectConfig
        {
            ProjectFilePath = Path.Combine(dir, "test.spyproj"),
            ProjectDirectory = dir,
            RootNamespace = "ColdWarm",
            SourceFiles = sourceFiles.ToList(),
            Configuration = "Debug",
        };
    }

    private static ProjectCompilationResult Build(ProjectConfig config)
        => new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance)
            .CompileProject(config);

    /// <summary>Diagnostics as a stable, order-independent projection including Severity (#1553).</summary>
    private static string Diagnostics(ProjectCompilationResult result)
        => string.Join("\n", result.Diagnostics.GetAll()
            .Select(d => $"{d.Severity}:{d.Code}@{Path.GetFileName(d.FilePath ?? "")}:{d.Line}:{d.Column} {d.Message}")
            .OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>
    /// The files this build served from the cache instead of recompiling. This is the POSITIVE
    /// CONTROL for every assertion below: if a build skipped nothing, then nothing was restored
    /// through SymbolSerializer, and a cold==warm comparison would pass by measuring the cold path
    /// twice. Asserted explicitly rather than assumed.
    /// </summary>
    private static IReadOnlyList<string> Skipped(ProjectCompilationResult result)
        => result.Metrics?.SkippedFiles.Select(Path.GetFileName).OfType<string>().ToList()
            ?? new List<string>();

    /// <summary>Generated C# keyed by FILE NAME — the two builds use the same directory, but
    /// keying on the leaf keeps the comparison about content rather than path plumbing.</summary>
    private static IReadOnlyDictionary<string, string> Generated(ProjectCompilationResult result)
        => result.GeneratedCSharpFiles
            .ToDictionary(kv => Path.GetFileName(kv.Key), kv => kv.Value, StringComparer.Ordinal);

    [Fact]
    public void WarmBuild_IsObservationallyIdenticalToCold()
    {
        var lib = Write("same", "lib.spy", LibSource);
        var main = Write("same", "main.spy", MainSource);
        var config = Config("same", lib, main);

        // --- Build 1: cold. No cache on disk. ---
        var cold = Build(config);
        cold.Success.Should().BeTrue(
            "the specimen must compile, or every comparison below is vacuous. Diagnostics:\n"
            + Diagnostics(cold));

        var coldGenerated = Generated(cold);
        var coldDiagnostics = Diagnostics(cold);

        // Non-vacuity: the fixture has to actually drive the channel #1474 broke. If `long` never
        // reaches the emitted C#, this test would pass while measuring nothing.
        string.Concat(coldGenerated.Values).Should().Contain("long",
            "the specimen exists to exercise the int64/long channel — a cold build that never emits "
            + "`long` cannot tell a warm build's decode failure from agreement");

        // --- Build 2: warm. Same sources, cache now on disk, nothing edited. ---
        var warm = Build(config);
        warm.Success.Should().BeTrue("a warm build of unchanged sources must succeed. Diagnostics:\n"
            + Diagnostics(warm));

        Diagnostics(warm).Should().Be(coldDiagnostics,
            "a warm build must report exactly what the cold build reported. A dropped @must_use or "
            + "@deprecated fact shows up here as a diagnostic the second build no longer emits "
            + "(#1444)");

        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy", "main.spy" },
            "both files are unchanged, so both must come from the cache. Without this the "
            + "comparison above could pass by recompiling everything twice — measuring the cold "
            + "path against itself and calling it agreement");

        Generated(warm).Should().BeEquivalentTo(coldGenerated,
            "a warm build must EMIT what the cold build emitted. This is the property the batch "
            + "exists to establish: cold == warm, observationally (#1474, #1444)");

        // --- Build 3: one file edited, the other served from cache. ---
        //
        // The edit is semantically inert (a trailing comment), so the correct generated C# is
        // UNCHANGED — which makes any difference here a cache fault rather than an effect of the
        // edit. main.spy recompiles; lib.spy is unchanged and restored from the cache, so main's
        // view of `Counter.next_value() -> long` now comes from a DECODED symbol. That is #1474's
        // second-build repro end to end: before the fix the decoder had no arm for the encoded name
        // `int64` and handed back UnknownType, silently.
        File.WriteAllText(main, MainSource + "\n# touched\n");

        var afterTouch = Build(config);

        Skipped(afterTouch).Should().BeEquivalentTo(new[] { "lib.spy" },
            "the edit must split the project: main.spy recompiles and lib.spy is restored from the "
            + "cache. If lib.spy were recompiled too, main would be reading FRESH symbols and this "
            + "cell would say nothing about the decoder");

        afterTouch.Success.Should().BeTrue(
            "the third build reads lib's symbols back out of the cache; a builtin that decoded to "
            + "UnknownType surfaces here. Diagnostics:\n" + Diagnostics(afterTouch));

        Diagnostics(afterTouch).Should().Be(coldDiagnostics,
            "an inert edit must not change what is reported about the OTHER file, whose symbols "
            + "came from the cache");

        Generated(afterTouch).Should().BeEquivalentTo(coldGenerated,
            "a trailing comment changes no emitted code. A difference here is the cached half of "
            + "the project decoding differently from the way it was encoded (#1474)");
    }

    /// <summary>
    /// A downstream file that MISUSES the cached type, so the compiler has to say out loud what it
    /// thinks that type is.
    /// </summary>
    private const string MismatchMainSource = @"from lib import Counter


def main() -> None:
    c = Counter(10)
    wrong: str = c.next_value()
    print(wrong)
";

    /// <summary>
    /// The narrow #1474 repro, made DISCRIMINATING.
    ///
    /// <para>The obvious probe — assert the warm build still emits <c>long</c> — is vacuous, and
    /// measuring it that way is how this nearly shipped as a green test over a broken decoder. Two
    /// reasons it says nothing: an annotated local takes its emitted type from the ANNOTATION, not
    /// from the inferred right-hand side, and an inferred local emits as <c>var</c>, so codegen
    /// renders the cached return type in neither case. Verified by mutation: with the pre-#1474
    /// decoder restored, a codegen-shaped assertion stayed green.</para>
    ///
    /// <para>So the probe makes the compiler NAME the type instead. Assigning a <c>long</c> to a
    /// <c>str</c> produces "Cannot assign type 'int64' to variable of type 'str'" — the cached fact
    /// quoted verbatim. If the decoder loses <c>int64</c>, that text changes or the diagnostic
    /// disappears.</para>
    ///
    /// <para>The mismatch cannot be present on the FIRST build: a failing build writes no cache, so
    /// there would be nothing to restore and the cell would measure the cold path twice (the skip
    /// control below catches exactly that, and did). The mismatch is therefore introduced by the
    /// EDIT, and the reference reading is the same final source compiled in a cache-free
    /// directory.</para>
    /// </summary>
    [Fact]
    public void AfterAWarmRestore_TheCompilerStillNamesTheCachedTypeInt64()
    {
        // --- The warm arm: a succeeding build writes the cache, then the edit introduces the
        //     mismatch. lib.spy is untouched throughout, so its symbols come back through the
        //     serializer.
        var warmLib = Write("warm", "lib.spy", LibSource);
        var warmMain = Write("warm", "main.spy", MainSource);
        var warmConfig = Config("warm", warmLib, warmMain);

        Build(warmConfig).Success.Should().BeTrue(
            "the cache is only written by a build that succeeds — a failing first build leaves "
            + "nothing to restore and this cell would measure the cold path twice");

        File.WriteAllText(warmMain, MismatchMainSource);
        var warm = Build(warmConfig);

        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy" },
            "lib.spy must be the file served from cache, or `long` never makes the round trip this "
            + "test is about");

        // --- The cold arm: the SAME final sources, in a directory that has never been built.
        var coldLib = Write("cold", "lib.spy", LibSource);
        var coldMain = Write("cold", "main.spy", MismatchMainSource);
        var cold = Build(Config("cold", coldLib, coldMain));

        Skipped(cold).Should().BeEmpty("the cold arm must have no cache to skip from");

        var coldDiagnostics = Diagnostics(cold);
        coldDiagnostics.Should().Contain("int64",
            "the cold build must name the type in its message, or the comparison below has nothing "
            + "to discriminate with");

        _output.WriteLine("cold: " + coldDiagnostics);
        _output.WriteLine("warm: " + Diagnostics(warm));

        Diagnostics(warm).Should().Be(coldDiagnostics,
            "the same source must produce the same report whether the imported half was compiled "
            + "or restored. When the decoder had no arm for the encoded name `int64`, the cached "
            + "return type came back UnknownType and this message changed — silently, and only on "
            + "a build that reused a cache (#1474)");
    }
}
