using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
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

    private const string NonIntegerConstLibSource = @"enum Color:
    RED = 1
    GREEN = 2


const RATE: float = 4.0
const NAME: str = ""ab""
const ON: bool = True
const PRIMARY: Color = Color.RED
";

    private const string NonIntegerConstMainInitialSource = @"from lib import Color, RATE, NAME, ON, PRIMARY


def main() -> None:
    print(RATE)
    print(NAME)
    print(ON)
    print(PRIMARY)
";

    private const string NonIntegerConstMainConsumingSource = @"from lib import Color, RATE, NAME, ON, PRIMARY


def show(r: float = RATE, n: str = NAME, o: bool = ON) -> None:
    print(r)
    print(n)
    print(o)


def main() -> None:
    show()
    c: Color = Color.RED
    match c:
        case PRIMARY:
            print(""primary"")
        case _:
            print(""other"")
";

    /// <summary>
    /// The const-eligibility fact making the round trip for the kinds <c>ConstantValue</c> cannot
    /// carry (#1791): float, str, bool and enum.
    ///
    /// <para>An integer const travels as its folded value, which is why
    /// <see cref="AfterAWarmRestore_AConstReferenceIsStillAConstantExpression"/> passed before this
    /// fact existed. Every other const-eligible type has no folded value, so the importing module
    /// can only know the answer if the EXPORTING module's analysis result travelled with the symbol.
    /// Before the fix the fallback was "an integer with a folded value", so these four were refused
    /// SPY0401 as defaults and SPY0605 as a pattern head — on a cold build as well as a warm one.</para>
    ///
    /// <para>The warm arm's SUCCESS is the discriminating observation, and the consuming edit is
    /// what makes it one: a plain <c>print</c> of a <c>static readonly</c> prints the same text, so
    /// only a constant POSITION can tell the two apart. Dropping the serialized bool turns this
    /// build red while the cold arm stays green.</para>
    /// </summary>
    [Fact]
    public void AfterAWarmRestore_ANonIntegerConstIsStillCompileTime()
    {
        // --- Warm arm: a succeeding build writes the cache; the edit then makes main CONSUME the
        //     imported consts at constant positions. lib.spy is untouched throughout.
        var warmLib = Write("ncwarm", "lib.spy", NonIntegerConstLibSource);
        var warmMain = Write("ncwarm", "main.spy", NonIntegerConstMainInitialSource);
        var warmConfig = Config("ncwarm", warmLib, warmMain);

        Build(warmConfig).Success.Should().BeTrue(
            "the cache is only written by a build that succeeds — a failing first build leaves "
            + "nothing to restore and this cell would measure the cold path twice");

        File.WriteAllText(warmMain, NonIntegerConstMainConsumingSource);
        var warm = Build(warmConfig);

        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy" },
            "lib.spy must be the file served from cache, or the compile-time-constant fact never "
            + "makes the round trip this test is about");

        warm.Success.Should().BeTrue(
            "a float/str/bool/enum const restored from the cache must still be admitted at a "
            + "constant position — the fact rides the symbol because a project build gives every "
            + "file its own SemanticBinding (#1791). Diagnostics:\n" + Diagnostics(warm));

        // --- Cold arm: the SAME final sources, in a directory that has never been built.
        var coldLib = Write("nccold", "lib.spy", NonIntegerConstLibSource);
        var coldMain = Write("nccold", "main.spy", NonIntegerConstMainConsumingSource);
        var cold = Build(Config("nccold", coldLib, coldMain));

        Skipped(cold).Should().BeEmpty("the cold arm must have no cache to skip from");
        cold.Success.Should().BeTrue(
            "the cold reading of the same source must compile. Diagnostics:\n" + Diagnostics(cold));

        Diagnostics(warm).Should().Be(Diagnostics(cold),
            "warm ≡ cold for the const-eligibility fact (#1791, the #1553 contract)");

        var warmGenerated = Generated(warm).ToDictionary(
            kv => kv.Key, kv => kv.Value.Replace("ncwarm", "AREA"), StringComparer.Ordinal);
        var coldGenerated = Generated(cold).ToDictionary(
            kv => kv.Key, kv => kv.Value.Replace("nccold", "AREA"), StringComparer.Ordinal);
        warmGenerated.Should().BeEquivalentTo(coldGenerated,
            "the const/static-readonly decision must not depend on whether lib's symbols were "
            + "compiled or restored (#1791)");

        // Non-vacuity: all four kinds must actually emit as C# consts in the specimen, or the
        // comparison could agree on a static-readonly regression in both arms.
        var emitted = string.Concat(warmGenerated.Values);
        emitted.Should().Contain("const double RATE = 4",
            "the cached lib's float const must still emit as a C# const (#1791)");
        emitted.Should().Contain("const string NAME = \"ab\"",
            "the cached lib's str const must still emit as a C# const (#1791)");
        emitted.Should().Contain("const bool ON = true",
            "the cached lib's bool const must still emit as a C# const (#1791)");
        emitted.Should().Contain(
            "const global::ColdWarm.Lib.Color PRIMARY = global::ColdWarm.Lib.Color.RED",
            "the cached lib's enum const must still emit as a C# const — the same-file enum type is "
            + "global::-qualified under universal qualification, but it stays a `const`, not a "
            + "static-readonly (#1782, #1683)");
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

    // -----------------------------------------------------------------------
    // LiteralString and Self cells (#1766, #1751)
    // -----------------------------------------------------------------------

    private const string LiteralStringLibSource = @"def tag(s: LiteralString) -> str:
    return s.upper()

class Config:
    label: LiteralString

    def __init__(self, label: LiteralString) -> None:
        self.label = label

    def get_label(self) -> LiteralString:
        return self.label
";

    private const string LiteralStringMainSource = @"from lib import tag, Config

def main() -> None:
    c = Config(""hello"")
    print(tag(""world""))
    print(c.get_label())
";

    /// <summary>
    /// A <c>LiteralString</c> signature, return type and field must survive the serializer
    /// round trip: cold == warm output. Without the <c>Register&lt;LiteralStringType&gt;</c>
    /// added in eac345708, the second build ICEd SPY0909 (#1751).
    /// </summary>
    [Fact]
    public void WarmBuild_LiteralStringSignature_IsObservationallyIdenticalToCold()
    {
        var lib = Write("litstr", "lib.spy", LiteralStringLibSource);
        var main = Write("litstr", "main.spy", LiteralStringMainSource);
        var config = Config("litstr", lib, main);

        var cold = Build(config);
        cold.Success.Should().BeTrue(
            "the LiteralString specimen must compile cold. Diagnostics:\n" + Diagnostics(cold));
        var coldGenerated = Generated(cold);
        var coldDiagnostics = Diagnostics(cold);

        var warm = Build(config);
        warm.Success.Should().BeTrue(
            "a warm build of the LiteralString specimen must succeed — SPY0909 here means "
            + "the serializer has no arm for LiteralStringType (#1751). Diagnostics:\n"
            + Diagnostics(warm));

        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy", "main.spy" },
            "both files must come from the cache for this to measure the serializer");

        Diagnostics(warm).Should().Be(coldDiagnostics,
            "warm must report what cold reported");

        Generated(warm).Should().BeEquivalentTo(coldGenerated,
            "warm must emit what cold emitted — a dropped LiteralString fact changes the C#");
    }

    private const string SelfLibSource = @"class Builder:
    value: int

    def __init__(self, value: int) -> None:
        self.value = value

    def clone(self) -> Self:
        return Builder(self.value)
";

    private const string SelfMainSource = @"from lib import Builder

def main() -> None:
    b = Builder(42)
    c = b.clone()
    print(c.value)
";

    /// <summary>
    /// A <c>-> Self</c> return type must survive the serializer round trip: cold == warm output.
    /// Without the <c>Register&lt;SelfType&gt;</c>, the second build ICEd SPY0909.
    /// </summary>
    [Fact]
    public void WarmBuild_SelfReturnType_IsObservationallyIdenticalToCold()
    {
        var lib = Write("selfrt", "lib.spy", SelfLibSource);
        var main = Write("selfrt", "main.spy", SelfMainSource);
        var config = Config("selfrt", lib, main);

        var cold = Build(config);
        cold.Success.Should().BeTrue(
            "the Self specimen must compile cold. Diagnostics:\n" + Diagnostics(cold));
        var coldGenerated = Generated(cold);
        var coldDiagnostics = Diagnostics(cold);

        var warm = Build(config);
        warm.Success.Should().BeTrue(
            "a warm build of the Self specimen must succeed — SPY0909 here means "
            + "the serializer has no arm for SelfType (#1751). Diagnostics:\n"
            + Diagnostics(warm));

        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy", "main.spy" },
            "both files must come from the cache for this to measure the serializer");

        Diagnostics(warm).Should().Be(coldDiagnostics,
            "warm must report what cold reported");

        Generated(warm).Should().BeEquivalentTo(coldGenerated,
            "warm must emit what cold emitted — a dropped Self fact changes the C#");
    }

    private const string TemplateLibSource = @"def exclaim(tp: Template) -> Template:
    return tp + t""!""
";

    private const string TemplateMainSource = @"from lib import exclaim

def main() -> None:
    x = 1
    r = exclaim(t""a{x}"")
    print(repr(r))
";

    private const string TemplateMainEditedSource = @"from lib import exclaim

def main() -> None:
    x = 2
    b: bool = exclaim(t""a{x}"")
";

    /// <summary>
    /// A <c>Template</c> parameter/return type is the registry symbol's CLR-backed
    /// <c>UserDefinedType</c> (#1996; the retired <c>TemplateType</c> record had its own
    /// <c>"template"</c> codec tag): a warm build restores <c>lib</c>'s signature through the
    /// <c>"user"</c> channel and must report and emit exactly what the cold build did. The
    /// both-files-cached half replays cached C# and diagnostics, so it cannot see the decode; the
    /// consumer RECOMPILED against the cached signature can: <c>b: bool = exclaim(…)</c> must say
    /// SPY0220 <c>'Template'</c> exactly as a cold build of the same final layout does (a lost decode
    /// is silent Unknown → SPY0908). Member access on the decoded type is a separate, pre-existing
    /// gap shared with <c>bytes</c> — the registry symbol was not restored (#2027; covered by
    /// <see cref="AfterAWarmRestore_ACachedUserDefinedTypeTypesAsCold"/>).
    /// </summary>
    [Fact]
    public void WarmBuild_TemplateSignature_IsObservationallyIdenticalToCold()
    {
        var lib = Write("template", "lib.spy", TemplateLibSource);
        var main = Write("template", "main.spy", TemplateMainSource);
        var config = Config("template", lib, main);

        var cold = Build(config);
        cold.Success.Should().BeTrue("the Template specimen must compile cold. Diagnostics:\n" + Diagnostics(cold));
        var coldGenerated = Generated(cold);
        var coldDiagnostics = Diagnostics(cold);

        var warm = Build(config);
        warm.Success.Should().BeTrue("warm build. Diagnostics:\n" + Diagnostics(warm));
        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy", "main.spy" },
            "both files must come from the cache for this to measure the serializer");
        Diagnostics(warm).Should().Be(coldDiagnostics, "warm must report what cold reported");
        Generated(warm).Should().BeEquivalentTo(coldGenerated, "warm must emit what cold emitted");

        File.WriteAllText(main, TemplateMainEditedSource);
        var edited = Build(config);
        Skipped(edited).Should().BeEquivalentTo(new[] { "lib.spy" },
            "lib must be served from the cache while main recompiles against it");

        var freshLib = Write("template-cold", "lib.spy", TemplateLibSource);
        var freshMain = Write("template-cold", "main.spy", TemplateMainEditedSource);
        var coldEdited = Build(Config("template-cold", freshLib, freshMain));
        Skipped(coldEdited).Should().BeEmpty("the control build is cold");
        Diagnostics(coldEdited).Should().Contain("SPY0220").And.Contain("'Template'",
            "the cold consumer types the call as Template");

        Diagnostics(edited).Should().Be(Diagnostics(coldEdited),
            "a consumer compiled against the cached Template signature must type it as cold does");
    }

    /// <summary>
    /// One kind of user-defined type a cached signature can carry (#2027): how <c>lib.spy</c> declares
    /// and builds a value of it, how <c>main.spy</c> builds one locally (for the parameter position),
    /// and the consumer uses — a member, an operator (null: the kind has none), iteration, and the
    /// <c>b: bool =</c> probe member (typed ≠ bool cold, so a typed member is SPY0220 and an untyped
    /// one is silent).
    /// </summary>
    private sealed record UdtKind(
        string Type, string Decl, string Value, string MainImport, string Local,
        string Member, string? Operator, bool Iterable, string Probe,
        string Prelude = "", string? StatementUse = null);

    private static readonly Dictionary<string, UdtKind> UdtKinds = new()
    {
        ["bytes"] = new("bytes", "", "b\"xy\"", "", "b\"xy\"", "{v}.hex()", "{v} + {v}", true, "{v}.hex()"),
        ["slice"] = new("slice", "", "slice(1, 3)", "", "slice(1, 3)", "{v}.stop", "{v} == {v}", false, "{v}.stop"),
        ["complex"] = new("complex", "", "complex(1.0, 2.0)", "", "complex(1.0, 2.0)", "{v}.real", "{v} + {v}", false, "{v}.real"),
        // The operator row is the plan's warm SPY0222 false refusal `repr(f() + t"!")`.
        ["template"] = new("Template", "", "t\"a\"", "", "t\"a\"", "{v}.strings", "repr({v} + t\"!\")", true, "{v}.strings"),
        // A builtin exception exposes no member surface (every System.Exception member is refused),
        // so its uses are the upcast and `raise` — both refused warm (SPY0220/SPY0242) while cold built.
        ["valueerror"] = new("ValueError", UpcastDef, "ValueError(\"boom\")", "", "ValueError(\"boom\")",
            "upcast({v})", null, false, "upcast({v})", Prelude: UpcastDef,
            StatementUse: "try:\n        raise {v}\n    except ValueError as caught:\n        print(str(caught))"),
        // The operator row is the plan's warm SPY0222 false refusal `f() + f()` with `__add__`.
        ["userclass"] = new("P",
            "class P:\n    x: int\n\n    def __init__(self, x: int) -> None:\n        self.x = x\n\n"
            + "    def __add__(self, other: P) -> P:\n        return P(self.x + other.x)\n\n"
            + "    def __iter__(self) -> int:\n        yield self.x\n\n\n",
            "P(2)", "from lib import P\n", "P(2)", "{v}.x", "({v} + {v}).x", true, "{v}.x"),
        ["userstruct"] = new("S",
            "struct S:\n    x: int\n\n    def __init__(self, x: int) -> None:\n        self.x = x\n\n"
            + "    def __add__(self, other: S) -> S:\n        return S(self.x + other.x)\n\n\n",
            "S(3)", "from lib import S\n", "S(3)", "{v}.x", "({v} + {v}).x", false, "{v}.x"),
        ["userenum"] = new("Color", "enum Color:\n    RED = 1\n    GREEN = 2\n\n\n",
            "Color.GREEN", "from lib import Color\n", "Color.GREEN", "{v}.value", "{v} == {v}", false, "{v}.value"),
        // A tagged union (#2071): its cases were not on the wire, so a cache-restored union had none —
        // the case pattern and the case construction were SPY0202 warm. The member is a union method.
        ["userunion"] = new("Shape",
            "union Shape:\n    case Circle(r: int)\n    case Square(s: int)\n\n"
            + "    def area(self) -> int:\n        match self:\n            case Shape.Circle(r):\n                return r * r\n"
            + "            case Shape.Square(s):\n                return s * s\n\n\n",
            "Shape.Circle(2)", "", "Shape.Circle(2)", "{v}.area()", null, false, "{v}.area()",
            Prelude: "from lib import Shape\n\n\n",
            StatementUse: "match {v}:\n        case Shape.Circle(r):\n            print(r)\n        case Shape.Square(s):\n"
                + "            print(s)\n    print(Shape.Square(3).area())"),
        // Beyond the issue's list: a stdlib-module type and a .NET-namespace type lose their symbol the
        // same way (measured warm SPY0908 / cold SPY0220; `date == date` warm SPY0402).
        ["moduledate"] = new("date", "from datetime import date\n\n\n", "date(2020, 1, 2)",
            "from datetime import date\n", "date(2020, 1, 2)", "{v}.year", "{v} == {v}", false, "{v}.year"),
        ["clrstringbuilder"] = new("StringBuilder", "from system.text import StringBuilder\n\n\n",
            "StringBuilder(\"ab\")", "from system.text import StringBuilder\n", "StringBuilder(\"ab\")",
            "{v}.Length", null, false, "{v}.Length"),
        // The #1325 control: an escape-declared `bytes` is the USER's class — a file origin that must
        // never relink to the registry `bytes` (whose `.x` does not exist).
        ["escapedbytes"] = new("`bytes`",
            "class `bytes`:\n    x: int\n\n    def __init__(self) -> None:\n        self.x = 5\n\n\n",
            "`bytes`()", "from lib import `bytes`\n", "`bytes`()", "{v}.x", null, false, "{v}.x"),
    };

    private const string UpcastDef = "def upcast(e: Exception) -> str:\n    return str(e)\n\n\n";

    private static readonly string[] UdtPositions = { "return", "parameter", "field", "property", "lambda" };

    public static IEnumerable<object[]> UdtKindTimesPosition()
        => from kind in UdtKinds.Keys from position in UdtPositions select new object[] { kind, position };

    private static string UdtLib(UdtKind k, string position) => k.Decl + position switch
    {
        "return" => $"def make() -> {k.Type}:\n    return {k.Value}\n",
        "parameter" => $"def take(v: {k.Type}) -> None:\n    print({k.Member.Replace("{v}", "v")})\n",
        "field" => $"class Box:\n    item: {k.Type}\n\n    def __init__(self) -> None:\n        self.item = {k.Value}\n",
        "property" => $"class Box:\n    property get item: {k.Type}\n\n    def __init__(self) -> None:\n        self.item = {k.Value}\n",
        "lambda" => $"def apply(f: ({k.Type}) -> None) -> None:\n    f({k.Value})\n",
        _ => throw new ArgumentOutOfRangeException(nameof(position)),
    };

    /// <summary>
    /// The three consumer programs for a cell: the initial one (imports the entry point, uses nothing,
    /// so the cache is written by a build that succeeds), the USES program (every applicable use,
    /// must build and run), and the PROBE program (must be refused exactly as cold refuses it). The
    /// parameter position's consumer use is the call itself: a valid argument runs, `take(1)` is the
    /// probe.
    /// </summary>
    private static (string Initial, string Uses, string Probe) UdtMains(UdtKind k, string position)
    {
        var entry = position switch
        {
            "return" => "make",
            "parameter" => "take",
            "field" or "property" => "Box",
            _ => "apply",
        };
        var head = (position == "parameter" ? k.MainImport : "") + $"from lib import {entry}\n\n\n" + k.Prelude;
        string M(string template) => template.Replace("{v}", "v");
        var initial = head + "def main() -> None:\n    print(0)\n";
        switch (position)
        {
            case "parameter":
                return (initial,
                    head + $"def main() -> None:\n    take({k.Local})\n",
                    head + "def main() -> None:\n    take(1)\n");
            case "lambda":
                {
                    var uses = new List<string> { $"    apply(lambda v: print({M(k.Member)}))" };
                    if (k.Operator != null)
                        uses.Add($"    apply(lambda v: print({M(k.Operator)}))");
                    if (k.Iterable)
                        uses.Add("    apply(lambda v: print(list(v)))");
                    return (initial,
                        head + "def main() -> None:\n" + string.Join("\n", uses) + "\n",
                        head + "def expect_bool(b: bool) -> None:\n    print(b)\n\n\n"
                            + $"def main() -> None:\n    apply(lambda v: expect_bool({M(k.Probe)}))\n");
                }
            default:
                {
                    var access = position == "return" ? "make()" : "Box().item";
                    var uses = new List<string> { $"    v = {access}", $"    print({M(k.Member)})" };
                    if (k.Operator != null)
                        uses.Add($"    print({M(k.Operator)})");
                    if (k.Iterable)
                        uses.AddRange(new[] { "    for e in v:", "        print(e)" });
                    if (k.StatementUse != null)
                        uses.Add("    " + M(k.StatementUse));
                    return (initial,
                        head + "def main() -> None:\n" + string.Join("\n", uses) + "\n",
                        head + $"def main() -> None:\n    v = {access}\n    b: bool = {M(k.Probe)}\n    print(b)\n");
                }
        }
    }

    /// <summary>
    /// A consumer recompiled against a cache-served signature types every user-defined type in it
    /// exactly as a cold build does (#2027): UDT kind × cached position × consumer use. The
    /// <c>"user"</c> codec used to write the bare name, so a cached type came back symbol-less —
    /// member access, operators and iteration went Unknown (SPY0908) or were refused (SPY0222, the
    /// two false refusals of valid programs) where the cold build typed them.
    ///
    /// <para>Warm = <c>lib.spy</c> served from the cache while <c>main.spy</c> recompiles after a
    /// CONTENT edit (staleness is content-hash; <c>touch</c> would re-serve both) — asserted, it is
    /// the positive control that the decode was exercised. Cold = the same final sources in a
    /// directory never built. Compared: the diagnostic multisets (both programs) and the stdout of the
    /// uses program; the cold probe must be refused (positive control that the probe is typed).</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(UdtKindTimesPosition))]
    public void AfterAWarmRestore_ACachedUserDefinedTypeTypesAsCold(string kind, string position)
    {
        var k = UdtKinds[kind];
        var lib = UdtLib(k, position);
        var (initial, uses, probe) = UdtMains(k, position);

        using var warm = new ProjectCompilationHelper().WithIncremental().WithStdlibModules();
        warm.AddSourceFile("lib.spy", lib).AddSourceFile("main.spy", initial);
        var first = warm.Compile();
        first.Success.Should().BeTrue($"the {kind}/{position} specimen must build cold first. Diagnostics:\n{Diagnostics(first)}");

        warm.UpdateSourceFile("main.spy", uses);
        var warmUses = warm.CompileAndExecute();
        var warmUsesBuild = warm.LastCompilationResult!;
        Skipped(warmUsesBuild).Should().BeEquivalentTo(new[] { "lib.spy" },
            "lib must be served from the cache while main recompiles against it");

        warm.UpdateSourceFile("main.spy", probe);
        var warmProbe = warm.Compile();
        Skipped(warmProbe).Should().BeEquivalentTo(new[] { "lib.spy" },
            "lib must still be served from the cache for the probe build");

        using var coldU = new ProjectCompilationHelper().WithIncremental().WithStdlibModules();
        coldU.AddSourceFile("lib.spy", lib).AddSourceFile("main.spy", uses);
        var coldUses = coldU.CompileAndExecute();
        var coldUsesBuild = coldU.LastCompilationResult!;
        Skipped(coldUsesBuild).Should().BeEmpty("the control build is cold");
        coldUses.Success.Should().BeTrue($"the cold uses program must build and run. Diagnostics:\n{Diagnostics(coldUsesBuild)}");

        using var coldP = new ProjectCompilationHelper().WithIncremental().WithStdlibModules();
        coldP.AddSourceFile("lib.spy", lib).AddSourceFile("main.spy", probe);
        var coldProbe = coldP.Compile();
        coldProbe.Diagnostics.GetErrors().Should().NotBeEmpty(
            "the cold probe must be refused — else it is not typed and proves nothing");

        Diagnostics(warmUsesBuild).Should().Be(Diagnostics(coldUsesBuild),
            $"{kind}/{position}: a consumer compiled against the cached signature must report what cold reports");
        warmUses.Success.Should().BeTrue($"{kind}/{position}: the warm uses program must build and run");
        warmUses.StandardOutput.Should().Be(coldUses.StandardOutput, $"{kind}/{position}: warm must print what cold prints");
        Diagnostics(warmProbe).Should().Be(Diagnostics(coldProbe),
            $"{kind}/{position}: the probe must be typed warm exactly as cold types it");
    }

    private const string StructAccessLibSource = @"struct Meter:
    _reading: int

    def __init__(self, reading: int) -> None:
        self._reading = reading

    def _scaled(self) -> int:
        return self._reading * 10

    def show(self) -> None:
        print(self._scaled())
";

    private const string StructAccessMainSource = @"from lib import Meter


def main() -> None:
    Meter(4).show()
";

    private const string StructAccessMainEditedSource = @"from lib import Meter


def main() -> None:
    Meter(5).show()
";

    /// <summary>
    /// A struct's <c>_x</c> is classified <c>private</c> with the host axis (#1937), and
    /// <c>Symbol.AccessLevel</c> round-trips through the symbol cache: a warm build must emit and
    /// report exactly what the cold one did, and a consumer recompiled against the CACHED struct
    /// must still build (no CS0666 — the emitted <c>lib</c> C# is served from the cache and says
    /// <c>private</c>).
    /// </summary>
    [Fact]
    public void WarmBuild_StructUnderscoreMember_IsObservationallyIdenticalToCold()
    {
        var lib = Write("structaccess", "lib.spy", StructAccessLibSource);
        var main = Write("structaccess", "main.spy", StructAccessMainSource);
        var config = Config("structaccess", lib, main);

        var cold = Build(config);
        cold.Success.Should().BeTrue(
            "a struct `_x` must compile cold (CS0666 before #1937). Diagnostics:\n" + Diagnostics(cold));
        var coldGenerated = Generated(cold);
        var coldDiagnostics = Diagnostics(cold);
        var readingField = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(coldGenerated["lib.cs"])
            .GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>()
            .Single(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "_Reading"));
        readingField.Modifiers.Select(m => Microsoft.CodeAnalysis.CSharp.CSharpExtensions.Kind(m))
            .Should().Contain(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PrivateKeyword,
                "the cold build's struct field is private — the fact the warm build must reproduce");

        var warm = Build(config);
        warm.Success.Should().BeTrue("warm build. Diagnostics:\n" + Diagnostics(warm));
        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy", "main.spy" },
            "both files must come from the cache for this to measure the serializer");
        Diagnostics(warm).Should().Be(coldDiagnostics, "warm must report what cold reported");
        Generated(warm).Should().BeEquivalentTo(coldGenerated, "warm must emit what cold emitted");

        File.WriteAllText(main, StructAccessMainEditedSource);
        var edited = Build(config);
        edited.Success.Should().BeTrue(
            "main recompiled against the cached struct. Diagnostics:\n" + Diagnostics(edited));
        Skipped(edited).Should().BeEquivalentTo(new[] { "lib.spy" },
            "lib must be served from the cache while main recompiles against it");
    }

    /// <summary>
    /// SPY0526 (#1932) is a path-only project-phase refusal, so it must be reported identically by a
    /// warm build whose other files come from the cache and by a cold build of the same layout: the
    /// first build (main.spy + lib/core.spy) succeeds and writes the cache; adding lib/lib.spy makes
    /// the wrapper `Lib` hold module class `Lib`; the warm rebuild (main/core skipped) and a cold
    /// build of the final layout in a fresh area report the same diagnostics.
    /// </summary>
    [Fact]
    public void PackageModuleCollision_IsReportedIdenticallyWarmAndCold()
    {
        const string main = "from lib.core import g\n\ndef main() -> None:\n    print(g())\n";
        const string core = "def g() -> int:\n    return 1\n";
        const string lib = "def f() -> int:\n    return 7\n";

        var warmMain = Write("pkgwarm", "main.spy", main);
        var warmCore = Write(Path.Combine("pkgwarm", "lib"), "core.spy", core);
        var first = Build(Config("pkgwarm", warmMain, warmCore));
        first.Success.Should().BeTrue("the collision-free layout builds. Diagnostics:\n" + Diagnostics(first));

        var warmLib = Write(Path.Combine("pkgwarm", "lib"), "lib.spy", lib);
        var warm = Build(Config("pkgwarm", warmMain, warmCore, warmLib));
        warm.Success.Should().BeFalse("lib/lib.spy nests module class Lib in wrapper Lib");
        Skipped(warm).Should().Contain(new[] { "main.spy", "core.spy" },
            "the unchanged files come from the cache — the positive control that this is a WARM build");

        var coldMain = Write("pkgcold", "main.spy", main);
        var coldCore = Write(Path.Combine("pkgcold", "lib"), "core.spy", core);
        var coldLib = Write(Path.Combine("pkgcold", "lib"), "lib.spy", lib);
        var cold = Build(Config("pkgcold", coldMain, coldCore, coldLib));
        cold.Success.Should().BeFalse();

        Diagnostics(cold).Should().Contain("SPY0526@lib.spy:1:1");
        Diagnostics(warm).Should().Be(Diagnostics(cold), "warm must report what cold reports");
    }

    /// <summary>
    /// Refusal 2 of SPY0526 (#1948) reads the package's __init__ BODY, which a warm build serves
    /// from the cache: the check must re-read it so warm reports what cold reports. First build:
    /// main + pkg/__init__.spy (`sub: int = 1`) + pkg/lib.spy; adding pkg/sub/y.spy makes `sub`
    /// shadow the new subpackage — the warm rebuild (with __init__ skipped) and a cold build of the
    /// final layout report the same refusal at the declaration.
    /// </summary>
    [Fact]
    public void InitShadowsSubpackage_IsReportedIdenticallyWarmAndCold()
    {
        const string main = "from pkg.lib import f\n\ndef main() -> None:\n    print(f())\n";
        // No warnings in any cached file: a refused build replays a cached file's warnings warm but
        // stops before analysis cold, so a warning would diverge for a reason this cell does not test
        // (#2084).
        const string init = "version: int = 0\nsub: int = 1\n";
        const string lib = "def f() -> int:\n    return 7\n";
        const string y = "def g() -> int:\n    return 2\n";

        var warmMain = Write("initwarm", "main.spy", main);
        var warmInit = Write(Path.Combine("initwarm", "pkg"), "__init__.spy", init);
        var warmLib = Write(Path.Combine("initwarm", "pkg"), "lib.spy", lib);
        var first = Build(Config("initwarm", warmMain, warmInit, warmLib));
        first.Success.Should().BeTrue("the shadow-free layout builds. Diagnostics:\n" + Diagnostics(first));

        var warmY = Write(Path.Combine("initwarm", "pkg", "sub"), "y.spy", y);
        var warm = Build(Config("initwarm", warmMain, warmInit, warmLib, warmY));
        warm.Success.Should().BeFalse("`sub` in __init__ shadows the new subpackage pkg/sub");
        Skipped(warm).Should().Contain("__init__.spy",
            "the __init__ whose body the check reads comes from the cache — the positive control that this is a WARM build");

        var coldMain = Write("initcold", "main.spy", main);
        var coldInit = Write(Path.Combine("initcold", "pkg"), "__init__.spy", init);
        var coldLib = Write(Path.Combine("initcold", "pkg"), "lib.spy", lib);
        var coldY = Write(Path.Combine("initcold", "pkg", "sub"), "y.spy", y);
        var cold = Build(Config("initcold", coldMain, coldInit, coldLib, coldY));
        cold.Success.Should().BeFalse();

        Diagnostics(cold).Should().Contain("SPY0526@__init__.spy:2:1");
        Diagnostics(warm).Should().Be(Diagnostics(cold), "warm must report what cold reports");
    }

    private const string NestedAliasLibSource = @"class Box:
    type Ids = list[int]

    tag: int

    def __init__(self, tag: int) -> None:
        self.tag = tag
";

    private const string NestedAliasMainInitialSource = @"from lib import Box


def main() -> None:
    b = Box(7)
    print(b.tag)
";

    private const string NestedAliasMainConsumingSource = @"from lib import Box


def main() -> None:
    b = Box(7)
    xs: Box.Ids = [1, 2, 3]
    print(b.tag)
    print(len(xs))
";

    /// <summary>
    /// A NESTED type alias (<c>type Ids = list[int]</c> inside <c>class Box</c>) must survive the
    /// warm-restore path: a dependent file that RESOLVES <c>Box.Ids</c> must compile identically
    /// whether the alias-defining file was compiled fresh or restored from the cache (#1729,
    /// plan-d35e69 Phase 1).
    ///
    /// <para><see cref="Sharpy.Compiler.Semantic.TypeSymbol"/>.<c>NestedTypeAliases</c> is classified
    /// <c>CacheStatus.Dropped</c> — the serializer does not carry it; it is expected to be
    /// reconstructed from lib's AST when a dependent module is re-extracted on a rebuild. That
    /// warm-restore safety was ASSUMED but UNTESTED: the existing ColdWarm <c>lib</c> specimen has no
    /// nested <c>type X = ...</c>, so this path was never exercised. If the cached <c>Box</c> comes
    /// back without its nested alias and nothing re-extracts it, <c>main</c>'s warm rebuild resolves
    /// <c>Box.Ids</c> to nothing → a warm-only SPY0202, while the cold arm stays green — which is the
    /// exact divergence <see cref="Diagnostics"/> below is written to catch.</para>
    ///
    /// <para>The warm arm's SUCCESS is the discriminating observation, and the consuming edit is what
    /// makes it one: the mismatch cannot be present on the FIRST build (a build that failed would
    /// write no cache and this cell would measure the cold path twice — the empty-vs-{lib.spy} skip
    /// controls catch exactly that). So <c>main</c> starts NOT using the alias, the cache is written,
    /// then the edit introduces <c>Box.Ids</c> and forces <c>main</c> to recompile against a CACHED
    /// <c>Box</c>.</para>
    /// </summary>
    [Fact]
    public void AfterAWarmRestore_ANestedTypeAliasStillResolves()
    {
        // --- Warm arm: a succeeding build writes the cache; the edit then makes main RESOLVE the
        //     nested alias Box.Ids. lib.spy is untouched throughout, so Box comes back through the
        //     serializer (whose NestedTypeAliases is Dropped), not fresh analysis.
        var warmLib = Write("nalwarm", "lib.spy", NestedAliasLibSource);
        var warmMain = Write("nalwarm", "main.spy", NestedAliasMainInitialSource);
        var warmConfig = Config("nalwarm", warmLib, warmMain);

        Build(warmConfig).Success.Should().BeTrue(
            "the cache is only written by a build that succeeds — a failing first build leaves "
            + "nothing to restore and this cell would measure the cold path twice");

        File.WriteAllText(warmMain, NestedAliasMainConsumingSource);
        var warm = Build(warmConfig);

        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy" },
            "lib.spy must be the file served from cache, or the nested alias never makes the round "
            + "trip this test is about — main.spy recompiles and resolves Box.Ids against the "
            + "DECODED Box symbol");

        warm.Success.Should().BeTrue(
            "Box.Ids must resolve through the cache-restored Box — a warm-only SPY0202 here means "
            + "the Dropped NestedTypeAliases fact was neither serialized nor re-extracted from lib's "
            + "AST on the dependent rebuild (#1729). Diagnostics:\n" + Diagnostics(warm));

        // --- Cold arm: the SAME final sources, in a directory that has never been built.
        var coldLib = Write("nalcold", "lib.spy", NestedAliasLibSource);
        var coldMain = Write("nalcold", "main.spy", NestedAliasMainConsumingSource);
        var cold = Build(Config("nalcold", coldLib, coldMain));

        Skipped(cold).Should().BeEmpty("the cold arm must have no cache to skip from");
        cold.Success.Should().BeTrue(
            "the cold reading of the same source must compile. Diagnostics:\n" + Diagnostics(cold));

        Diagnostics(warm).Should().Be(Diagnostics(cold),
            "a nested alias must resolve identically whether the defining file was compiled or "
            + "restored — warm ≡ cold (#1729, the #1553 contract)");

        var warmGenerated = Generated(warm).ToDictionary(
            kv => kv.Key, kv => kv.Value.Replace("nalwarm", "AREA"), StringComparer.Ordinal);
        var coldGenerated = Generated(cold).ToDictionary(
            kv => kv.Key, kv => kv.Value.Replace("nalcold", "AREA"), StringComparer.Ordinal);
        warmGenerated.Should().BeEquivalentTo(coldGenerated,
            "the C# main emits for Box.Ids must not depend on whether Box's symbol was compiled or "
            + "restored (#1729)");

        // Non-vacuity: the cold build must actually RESOLVE Box.Ids to its target list[int] — if it
        // did not, main would be SPY0202 and the warm≡cold comparison would agree on a broken cold
        // arm. The resolved alias emits the target type verbatim.
        string.Concat(coldGenerated.Values).Should().Contain("Sharpy.List<int> xs",
            "the cold build must resolve the nested alias Box.Ids to its target list[int] "
            + "(emitted as Sharpy.List<int>) — otherwise the alias was never exercised and the "
            + "cold/warm comparison discriminates nothing");
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
