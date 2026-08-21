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

    def poke(self) -> None:
        scratch: int = 99
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

    private static ProjectCompilationResult Build(ProjectConfig config, bool warningsAsErrors = false)
        => new Compiler(
                new CompilerOptions { Incremental = true, WarningsAsErrors = warningsAsErrors },
                NullLogger.Instance)
            .CompileProject(config);

    /// <summary>
    /// Diagnostics as a stable, order-independent projection including Severity and Span (#1553) —
    /// the two members a naive replay gets wrong.
    /// </summary>
    private static string Diagnostics(ProjectCompilationResult result)
        => string.Join("\n", result.Diagnostics.GetAll()
            .Select(d => $"{d.Severity}:{d.Code}@{Path.GetFileName(d.FilePath ?? "")}:{d.Line}:{d.Column}"
                + $"[{d.Span?.Start.ToString() ?? "-"},{d.Span?.Length.ToString() ?? "-"}] {d.Message}")
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

        // Non-vacuity for the #1553 class: the cache-served file must CARRY a diagnostic, or every
        // diagnostics equality below compares empty strings and a warm build that dropped all
        // cached diagnostics would still pass (absence assertions pass vacuously).
        coldDiagnostics.Should().Contain("Warning",
            "lib.spy's unused local (SPY0451) must produce a warning in the cache-served file — "
            + "that warning is the payload whose replay this test exists to measure (#1553). "
            + "(The plan wanted a @must_use ignore too; a discarded call to a NON-overloaded "
            + "@must_use method records no call target and draws no SPY0480 — the #1537 seam "
            + "gap — so the unused local carries the cell instead.)");

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

    private const string ConstLibSource = @"const LIMIT: int = 200
const BIG: int = 100 + 100
";

    private const string ConstMainInitialSource = @"from lib import LIMIT, BIG


def main() -> None:
    print(LIMIT)
    print(BIG)
";

    private const string ConstMainSmallWidthSource = @"from lib import LIMIT, BIG


def main() -> None:
    b: uint8 = LIMIT
    c: uint8 = BIG
    print(b)
    print(c)
";

    /// <summary>
    /// #1460's ConstantValue making the serializer round trip, measured as the warm ≡ cold
    /// property (#1553's contract applied to the new fact): a const restored from the cache must
    /// still be a constant expression at a §10.2.11 site.
    ///
    /// <para>The warm arm's SUCCESS is the discriminating observation: <c>b: uint8 = LIMIT</c> is
    /// admitted ONLY because the decoded <see cref="Sharpy.Compiler.Semantic.VariableSymbol"/>
    /// still carries <c>ConstantValue</c> — the non-const spelling of the same binding is refused
    /// SPY0220 (pinned by <c>const_ref_nonconst_uint8_refused</c>), so a serializer or import seam
    /// that dropped the fact fails exactly this build, loudly. <c>BIG</c> adds the
    /// folded-expression shape (<c>100 + 100</c>), whose emitter half is the Design-Decision-1
    /// alignment cell (<c>const_ref_expr_init_uint8</c>) compiled from a CACHED symbol here.</para>
    /// </summary>
    [Fact]
    public void AfterAWarmRestore_AConstReferenceIsStillAConstantExpression()
    {
        // --- Warm arm: a succeeding build writes the cache; the edit then makes main CONSUME the
        //     imported consts at small-width sites. lib.spy is untouched throughout, so LIMIT and
        //     BIG come back through the serializer, not fresh analysis.
        var warmLib = Write("constwarm", "lib.spy", ConstLibSource);
        var warmMain = Write("constwarm", "main.spy", ConstMainInitialSource);
        var warmConfig = Config("constwarm", warmLib, warmMain);

        Build(warmConfig).Success.Should().BeTrue(
            "the cache is only written by a build that succeeds — a failing first build leaves "
            + "nothing to restore and this cell would measure the cold path twice");

        File.WriteAllText(warmMain, ConstMainSmallWidthSource);
        var warm = Build(warmConfig);

        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy" },
            "lib.spy must be the file served from cache, or ConstantValue never makes the round "
            + "trip this test is about");

        warm.Success.Should().BeTrue(
            "b: uint8 = LIMIT compiles only through the decoded symbol's ConstantValue — a "
            + "serializer that dropped the fact surfaces here as SPY0220 on a build that reused "
            + "a cache (#1460). Diagnostics:\n" + Diagnostics(warm));

        // --- Cold arm: the SAME final sources, in a directory that has never been built.
        var coldLib = Write("constcold", "lib.spy", ConstLibSource);
        var coldMain = Write("constcold", "main.spy", ConstMainSmallWidthSource);
        var cold = Build(Config("constcold", coldLib, coldMain));

        Skipped(cold).Should().BeEmpty("the cold arm must have no cache to skip from");
        cold.Success.Should().BeTrue(
            "the cold reading of the same source must compile. Diagnostics:\n" + Diagnostics(cold));

        Diagnostics(warm).Should().Be(Diagnostics(cold),
            "a const's ConstantValue must read back exactly as it was recorded — warm ≡ cold "
            + "for the new fact (#1460, the #1553 contract)");

        // The two arms live in different directories, so the emitted #line directives differ by
        // exactly the area name — normalize it away and the rest must match byte for byte.
        var warmGenerated = Generated(warm).ToDictionary(
            kv => kv.Key, kv => kv.Value.Replace("constwarm", "AREA"), StringComparer.Ordinal);
        var coldGenerated = Generated(cold).ToDictionary(
            kv => kv.Key, kv => kv.Value.Replace("constcold", "AREA"), StringComparer.Ordinal);
        warmGenerated.Should().BeEquivalentTo(coldGenerated,
            "the emitted C# — including the const/readonly decision the materialized "
            + "IsCompileTimeConstant drives — must not depend on whether lib's symbols were "
            + "compiled or restored (#1460)");

        // Non-vacuity: the emission the comparison protects must actually be the C# const form —
        // otherwise the cell could agree on a static-readonly regression in both arms.
        string.Concat(warmGenerated.Values).Should().Contain("const int LIMIT = 200",
            "the cached lib's LIMIT must still emit as a C# const, not static readonly (#1460)");
    }

    /// <summary>
    /// Design Decision 10 (#1553): policy is configuration, not cache content. The cache stores
    /// the policy-free per-unit diagnostics; warnings-as-errors is applied by the project bag AT
    /// REPLAY TIME — so flipping the flag between the cold build and a warm no-edit rebuild must
    /// escalate the REPLAYED warnings and fail the warm build.
    /// </summary>
    [Fact]
    public void WarnAsErrorPolicy_AppliesAtReplayTime_NotAtCacheWriteTime()
    {
        var lib = Write("flip", "lib.spy", LibSource);
        var main = Write("flip", "main.spy", MainSource);
        var config = Config("flip", lib, main);

        // Cold, WITHOUT the flag: succeeds, warnings recorded, cache written.
        var cold = Build(config);
        cold.Success.Should().BeTrue(
            "the cold arm must succeed so a cache exists to replay from. Diagnostics:\n"
            + Diagnostics(cold));
        Diagnostics(cold).Should().Contain("Warning",
            "the specimen must carry a warning, or the escalation below has nothing to escalate");

        // Warm, WITH the flag, nothing edited: both files replay from cache; the replayed
        // warnings meet the CURRENT policy and escalate.
        var warm = Build(config, warningsAsErrors: true);

        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy", "main.spy" },
            "both files are unchanged, so every diagnostic in the warm build came through the "
            + "replay path — otherwise this cell measures fresh analysis, not the cache");

        warm.Success.Should().BeFalse(
            "-warnaserror on the warm build must fail it on the REPLAYED warnings. A cache that "
            + "baked the cold build's policy into its content would sail through — policy is "
            + "configuration, not cache content (#1553)");

        Diagnostics(warm).Should().Contain("Error",
            "the replayed warning must surface escalated to an error under the current policy");
    }

    /// <summary>
    /// Mutation-tests the instrument itself (the batch's Testing Strategy names this exact
    /// mutation): hand the warm build a cache whose stored diagnostics were stripped, and the
    /// cold/warm comparison must DISAGREE. If warm still equalled cold after the payload was
    /// removed, every diagnostics assertion in this class would be decoration.
    /// </summary>
    [Fact]
    public void TheInstrument_DetectsADoctoredCacheEntryMissingItsDiagnostics()
    {
        var lib = Write("doctor", "lib.spy", LibSource);
        var main = Write("doctor", "main.spy", MainSource);
        var config = Config("doctor", lib, main);

        var cold = Build(config);
        cold.Success.Should().BeTrue("the specimen must compile. Diagnostics:\n" + Diagnostics(cold));
        var coldDiagnostics = Diagnostics(cold);
        coldDiagnostics.Should().Contain("Warning", "with no stored payload there is nothing to strip");

        var symbolCachePath = Path.Combine(_tempDir, "doctor", "obj", "Debug", ".sharpy-symbols");
        File.Exists(symbolCachePath).Should().BeTrue(
            "the cold build must have written the symbol cache this test doctors");

        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(symbolCachePath))!;
        StripDiagnostics(root);
        File.WriteAllText(symbolCachePath, root.ToJsonString());

        var warm = Build(config);

        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy", "main.spy" },
            "the doctored cache is schema-valid and must still be ACCEPTED — a rejected cache "
            + "would recompile everything and this cell would measure nothing");

        Diagnostics(warm).Should().NotBe(coldDiagnostics,
            "stripping the cached diagnostics must surface as a cold/warm divergence — the "
            + "sweep and the differential above rely on this comparison actually firing");
    }

    private static void StripDiagnostics(System.Text.Json.Nodes.JsonNode? node)
    {
        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject obj:
                obj.Remove("Diagnostics");
                foreach (var kv in obj.ToList())
                    StripDiagnostics(kv.Value);
                break;
            case System.Text.Json.Nodes.JsonArray arr:
                foreach (var item in arr)
                    StripDiagnostics(item);
                break;
        }
    }

    /// <summary>
    /// The headline divergence, other direction: with -warnaserror from the start, the cold build
    /// fails — and a failing build writes NO cache (save-only-on-success), so the warm no-edit
    /// rebuild recompiles everything and must fail identically. The empty skip set is the proof
    /// the no-cache-on-failure guard held; without it the second build would "pass" by replaying
    /// a cache that should never have existed.
    /// </summary>
    [Fact]
    public void WarnAsErrorColdFailure_RepeatsIdenticallyOnRebuild_BecauseNoCacheWasWritten()
    {
        var lib = Write("werror", "lib.spy", LibSource);
        var main = Write("werror", "main.spy", MainSource);
        var config = Config("werror", lib, main);

        var cold = Build(config, warningsAsErrors: true);
        cold.Success.Should().BeFalse(
            "the specimen's warning must escalate and fail the cold build under -warnaserror");

        var rebuild = Build(config, warningsAsErrors: true);
        rebuild.Success.Should().BeFalse("identical source under the same policy must fail again");
        Skipped(rebuild).Should().BeEmpty(
            "a failing build writes no cache (ProjectCompiler.CodeGen saves only on success), so "
            + "the rebuild has nothing to skip from — if files were skipped here, a failing "
            + "build's cache leaked");
        Diagnostics(rebuild).Should().Be(Diagnostics(cold),
            "two cache-less builds of identical source under identical policy must report "
            + "identically");
    }
}
