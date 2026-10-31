using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Tests.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// The import-binding verdicts on the PROJECT route (#2359, R-II): an import binds a module-level
/// name through <c>ImportResolver.BindImportName</c>, the one seam. The
/// declaration-position generator (<c>TypeDefinitionOwnSymbolMatrixTests.ImportBinding_HasOneVerdictPerCollider</c>)
/// pins the matrix on one file; this class pins the multi-file twin — an import of a PROJECT module
/// (<c>lib.spy</c>, bound through <c>ResolveImportSymbol</c>'s Phase 3 originals), the dotted-root merge
/// (<c>import os</c> + <c>import os.path</c>, both orders), the module-variable cell (c18), the star import a
/// module variable re-declares, the refused import that draws no SPY0484, the idempotent re-import that warns once, and the
/// plain-vs-alias split of a builtin TYPE name (a plain <c>from lib import double</c> is warned and
/// runs; <c>from lib import helper as double</c> is refused). P21g (#2359 R-IZ, #2371) adds the project twins of
/// K1 (two from-imports of ONE project module binding different symbols under one name: SPY0204 at the second),
/// K3 in both orders (an explicit import of another source under a name the star of a project module exports:
/// SPY0204 at the EXPLICIT import's ImportAlias), the star × star control (two libs exporting <c>shared</c>: SPY0204
/// at the second star, pinned as measured), the star × module-property cell (the local property prints <c>5</c>,
/// not lib's <c>9</c>) and its <c>--incremental</c> twin (the warm rebuild, lib.spy served from the cache). P21i
/// (#2385, #2386) adds the star × exported KIND × read POSITION cells from a project module (<c>consts.spy</c>: variable,
/// const, def, class, alias, property, the taken-over variable; module body, def, method — the single-file generator's
/// <c>StarReadPositions</c>), their explicit/qualified controls, and the warm twin of the star × var × def cell; and the
/// module-member KIND × CONSUMER cells (<c>lib.spy</c>: var, const, def, auto / getter / split property × from-import,
/// qualified, alias-qualified, star — the property kind's four cells are the #2131 cell re-homed on #2386), the package
/// <c>__init__</c> re-export of a module property, and the warm twin of the property's from-import; and the import ×
/// import over PROVENANCE cells (#2389, R-JK): one declaration reached through two module spellings — a package
/// <c>__init__</c>, a plain module, a star, two stars, two hops, an aliasing re-exporter — × spelling × order, each as a
/// verdict cell and a use cell, the different-symbol control (R12b), the single-import controls, and four warm twins.
///
/// <para><b>Ratchet.</b> Every cell is judged into failures (exact errors and shadowing warnings in main.spy, the
/// run's stdout); an SPY09xx is hard. The cells ratchet over the declaration-position generator's allowlist
/// (<c>Semantic/declaration-position-allowlist.txt</c>, ids <c>project × &lt;id&gt;</c>): a listed cell that passes
/// is STALE, an unlisted failing cell is red, and the generator's id universe includes <see cref="CellIds"/>.</para>
/// </summary>
public class ImportBindingProjectRouteTests
{
    private readonly ITestOutputHelper _output;

    public ImportBindingProjectRouteTests(ITestOutputHelper output) => _output = output;

    private const string Lib =
        "class Config:\n"
        + "    pass\n"
        + "\n"
        + "def helper() -> int:\n"
        + "    return 1\n"
        + "\n"
        + "def double(x: int) -> int:\n"
        + "    return x * 2\n"
        + "\n"
        + "level: int = 9\n"
        + "\n"
        + "shared: int = 1\n";

    /// <summary>A second project module exporting <c>shared</c> (the star × star control's other source).</summary>
    private const string LibB = "shared: int = 2\n";

    /// <summary>The star × module-property program (#2371 cell 3): lib's <c>level</c> is 9, the local property's 5.</summary>
    private const string StarPropertyMain =
        "from lib import *\nproperty level: int = 5\n\ndef main() -> None:\n    print(level)\n";

    /// <summary>The id of the <c>--incremental</c> twin of the star × module-property cell.</summary>
    private const string IncrementalStarPropertyId = "star × property (incremental)";

    /// <summary>Every ratcheted id of this class, as the shared allowlist spells it.</summary>
    internal static IEnumerable<string> CellIds()
        => Cells().Select(c => (string)c[0]!).Append(IncrementalStarPropertyId)
            .Concat(StarKindCells().Select(c => c.Id)).Concat(IncrementalStarVarDefIds)
            .Concat(ModuleMemberCells().Select(c => c.Id)).Append(IncrementalModulePropertyId)
            .Concat(ImportIdentityCells().Select(c => c.Id)).Concat(IncrementalImportIdentityIds)
            .Concat(TakeoverProvenanceCells().Select(c => c.Id))
            .Select(id => $"project × {id}");

    private const string MainOk = "\ndef main() -> None:\n    print(\"ok\")\n";

    /// <summary>(id, main.spy, exact errors in main.spy as code@line:col, exact SPY0483/SPY0484 warnings, stdout of an error-free cell).</summary>
    public static IEnumerable<object?[]> Cells()
    {
        yield return Cell("class × from-import-as", "class Foo:\n    pass\nfrom lib import helper as Foo\n" + MainOk,
            new[] { "SPY0204@3:17" });
        yield return Cell("def × import-as", "import lib as h\n\ndef h() -> int:\n    return 1\n" + MainOk,
            new[] { "SPY0204@1:8" });
        yield return Cell("builtin-type × import-as", "import lib as str\n" + MainOk, new[] { "SPY0212@1:8" });
        yield return Cell("builtin-type × from-import-as", "from lib import helper as double\n" + MainOk,
            new[] { "SPY0212@1:17" });
        yield return Cell("builtin-type × from-import (plain, warned)",
            "from lib import double\n\ndef main() -> None:\n    print(double(5))\n",
            errors: new string[0], warnings: new[] { "SPY0484@1:17" }, stdout: "10\n");
        yield return Cell("other-module × import-as", "import lib as mm\nimport math as mm\n" + MainOk,
            new[] { "SPY0204@2:8" });
        yield return Cell("var × import-as (c18)", "K: int = 1\nimport math as K\n" + MainOk, new[] { "SPY0204@1:1" });
        yield return Cell("var × from-import-as", "from math import pi as K\nK: int = 1\n" + MainOk,
            new[] { "SPY0204@2:1" });
        // A star import keeps its rule: a module variable re-declares the star-imported name (as at the base),
        // for a project module's variable and a Stdlib one alike.
        yield return Cell("var × star (project module)", "from lib import *\nlevel: int = 5\n\ndef main() -> None:\n    print(level)\n",
            errors: new string[0], stdout: "5\n");
        yield return Cell("var × star (stdlib module)", "from math import *\npi: float = 1.5\n\ndef main() -> None:\n    print(pi)\n",
            errors: new string[0], stdout: "1.5\n");
        // A refused import draws its SPY0204 alone (no SPY0484 claiming it rebinds the name); the def keeps its SPY0483.
        yield return Cell("def print × import-as print (refused, no SPY0484)",
            "import lib as print\n\ndef print(x: int) -> None:\n    pass\n\ndef main() -> None:\n    pass\n",
            new[] { "SPY0204@1:8" }, warnings: new[] { "SPY0483@3:5" });
        // An idempotent re-import binds nothing new and warns once.
        yield return Cell("from-import-as print twice (one SPY0484)",
            "from lib import helper as print\nfrom lib import helper as print\n\ndef main() -> None:\n    pass\n",
            errors: new string[0], warnings: new[] { "SPY0484@1:17" }, stdout: "");
        yield return Cell("fresh", "from lib import helper\n\ndef main() -> None:\n    print(helper())\n",
            errors: new string[0], stdout: "1\n");
        yield return Cell("same from-import twice",
            "from lib import helper\nfrom lib import helper\n\ndef main() -> None:\n    print(helper())\n",
            errors: new string[0], stdout: "1\n");
        yield return Cell("os root × root-first",
            "import os\nimport os.path\n\ndef main() -> None:\n    print(len(os.path.join(\"a\", \"b\")))\n",
            errors: new string[0], stdout: "3\n");
        yield return Cell("os root × dotted-first",
            "import os.path\nimport os\n\ndef main() -> None:\n    print(len(os.path.join(\"a\", \"b\")))\n",
            errors: new string[0], stdout: "3\n");
        // K1 (#2359): two from-imports of ONE project module binding different symbols under one name collide.
        yield return Cell("same module other symbol × from-import-as (K1)",
            "from lib import helper as v\nfrom lib import double as v\n" + MainOk, new[] { "SPY0204@2:17" });
        // K3 (R-IZ): an explicit import of another source under a star-exported name is refused at the explicit
        // import, in both orders.
        yield return Cell("star × import-as (K3, explicit first)", "import os as helper\nfrom lib import *\n" + MainOk,
            new[] { "SPY0204@1:8" });
        yield return Cell("star × import-as (K3, star first)", "from lib import *\nimport os as helper\n" + MainOk,
            new[] { "SPY0204@2:8" });
        yield return Cell("star × from-import-as (K3, explicit first)",
            "from os.path import join as helper\nfrom lib import *\n" + MainOk, new[] { "SPY0204@1:21" });
        yield return Cell("star × from-import-as (K3, star first)",
            "from lib import *\nfrom os.path import join as helper\n" + MainOk, new[] { "SPY0204@2:21" });
        // Star × star (control, D7): two sources exporting one name — SPY0204 at the second star, pinned as measured.
        yield return Cell("star × star (two libs, control)", "from lib import *\nfrom libb import *\n" + MainOk,
            new[] { "SPY0204@2:1" });
        // #2371: a star import never displaces a module property of the importing file.
        yield return Cell("star × property (project module)", StarPropertyMain, errors: new string[0], stdout: "5\n");
    }

    private static object?[] Cell(string id, string main, string[] errors, string[]? warnings = null,
        string? stdout = null)
        => new object?[] { id, main, errors, warnings ?? new string[0], stdout };

    [Theory]
    [MemberData(nameof(Cells))]
    public void ImportBinding_VerdictHoldsOnTheProjectRoute(string id, string main, string[] errors,
        string[] warnings, string? stdout)
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        using var helper = NewHelper("ImportBindingRoute", main);

        var result = helper.Compile();
        var inMain = result.Diagnostics.GetAll()
            .Where(d => d.FilePath != null && Path.GetFileName(d.FilePath) == "main.spy").ToList();
        var actualErrors = inMain.Where(d => d.IsError).Select(d => $"{d.Code}@{d.Line}:{d.Column}")
            .OrderBy(s => s, StringComparer.Ordinal).ToList();
        var actualWarnings = inMain.Where(d => !d.IsError && d.Code is "SPY0483" or "SPY0484")
            .Select(d => $"{d.Code}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
        var everyMessage = string.Join(" | ", result.Diagnostics.GetAll().Select(d => $"{d.Code} {Path.GetFileName(d.FilePath)}:{d.Line}:{d.Column} {d.Message}"));
        _output.WriteLine($"{id}: [{string.Join(" ", actualErrors)}] {everyMessage}");

        // Hard: no internal error on the project route.
        if (result.Diagnostics.GetErrors().Any(d => d.Code != null && d.Code.StartsWith("SPY09", StringComparison.Ordinal)))
            problems.Add($"{id}: an internal error — {everyMessage}");

        var failures = new List<string>();
        var expectedErrors = errors.OrderBy(s => s, StringComparer.Ordinal).ToList();
        if (!actualErrors.SequenceEqual(expectedErrors, StringComparer.Ordinal))
            failures.Add($"errors in main.spy are [{string.Join(" ", actualErrors)}], expected [{string.Join(" ", expectedErrors)}]");
        var expectedWarnings = warnings.OrderBy(s => s, StringComparer.Ordinal).ToList();
        if (!actualWarnings.SequenceEqual(expectedWarnings, StringComparer.Ordinal))
            failures.Add($"shadowing warnings in main.spy are [{string.Join(" ", actualWarnings)}], expected [{string.Join(" ", expectedWarnings)}]");

        if (errors.Length == 0)
        {
            var run = helper.CompileAndExecute();
            if (!run.Success || run.StandardOutput != stdout)
                failures.Add($"run: success={run.Success} stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}], expected [{stdout?.ReplaceLineEndings("\\n")}] "
                    + string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" "));
        }

        Judge($"project × {id}", failures, allowlist, problems);
    }

    /// <summary>
    /// The <c>--incremental</c> twin of the star × module-property cell (#2371; the axis the generator cannot
    /// enumerate): a cold build populates the cache, a REAL content edit to main.spy (a touch is ignored by the hash
    /// cache) makes the second build recompile main.spy alone with lib.spy's symbols served from the cache, and the warm
    /// program must print the local property's <c>5</c>. That lib.spy was cache-served is asserted hard (the instrument:
    /// a silent full rebuild would make the cell a duplicate of the cold one).
    /// </summary>
    [Fact]
    public void StarImport_ModuleProperty_HoldsOnAnIncrementalRebuild()
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        using var helper = NewHelper("ImportBindingRouteWarm", StarPropertyMain).WithIncremental();

        var cold = helper.Compile();
        cold.Diagnostics.GetErrors().Should().BeEmpty("the cold build of the star × property program compiles");
        helper.UpdateSourceFile("main.spy", StarPropertyMain + "# warm rebuild\n");
        var run = helper.CompileAndExecute();
        helper.AssertWarmBuildSkipped(helper.LastCompilationResult!, "lib.spy");
        _output.WriteLine($"{IncrementalStarPropertyId}: success={run.Success} stdout=[{run.StandardOutput.TrimEnd()}]");

        var failures = new List<string>();
        if (!run.Success || run.StandardOutput != "5\n")
            failures.Add($"warm run: success={run.Success} stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}], expected [5\\n] "
                + string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" "));
        Judge($"project × {IncrementalStarPropertyId}", failures, allowlist, problems);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // P21i (i): star × exported kind × read position, project source (#2385; the property kind is #2386's export cell).
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>The project module every star × kind cell imports: one member of every exported kind, each with a
    /// value the importing file could not print any other way (the lib's <c>9</c> vs a taken-over local's <c>1</c>).</summary>
    private const string Consts =
        "level: int = 9\n"
        + "const LIMIT: int = 3\n"
        + "def helper() -> int:\n"
        + "    return 4\n"
        + "class Box:\n"
        + "    v: int = 7\n"
        + "type Num = int\n"
        + "property prop: int = 6\n";

    /// <summary>(kind, the import line, a module-level prelude — the taken-over variable's own declaration —, the read,
    /// its annotated type, the stdout). The explicit <c>from consts import level</c> and qualified <c>consts.level</c>
    /// spellings are the controls: they print the lib's <c>9</c> at every position at the base.</summary>
    private static readonly (string Kind, string Import, string[] Prelude, string Read, string Type, string Stdout)[] StarKinds =
    {
        ("var", "from consts import *", new string[0], "level", "int", "9\n"),
        ("const", "from consts import *", new string[0], "LIMIT", "int", "3\n"),
        ("def", "from consts import *", new string[0], "helper()", "int", "4\n"),
        ("class", "from consts import *", new string[0], "Box().v", "int", "7\n"),
        ("alias", "from consts import *", new string[0], "2", "Num", "2\n"),
        ("property", "from consts import *", new string[0], "prop", "int", "6\n"),
        ("var-taken-over", "from consts import *", new[] { "level: int = 1" }, "level", "int", "1\n"),
        // The explicit import of a star-bound name merges into the star's binding, so the takeover takes it from both
        // statements: the local 1, never consts' 9 (verify-implementation R1: 9 when the explicit arm registered the
        // file's own variable under consts' container; 1 at 808d82955 and in python3). Its control: no takeover → 9.
        ("var-taken-over-beside-explicit", "from consts import *\nfrom consts import level", new[] { "level: int = 1" },
            "level", "int", "1\n"),
        ("var-explicit-beside-star (control)", "from consts import *\nfrom consts import level", new string[0], "level",
            "int", "9\n"),
        ("var-explicit (control)", "from consts import level", new string[0], "level", "int", "9\n"),
        ("var-qualified (control)", "import consts", new string[0], "consts.level", "int", "9\n"),
    };

    /// <summary>A cell of the project route judged against the contract of an error-free program: exact errors in
    /// main.spy (none), the run's stdout, and — when set — an emitted reference spelling.</summary>
    internal sealed record RouteCell(string Id, string Main, string[] Errors, string? Stdout, string? Spelling,
        (string Path, string Content)[] Libs);

    /// <summary>The star × kind × position cells (the single-file generator's <c>StarReadPositions</c>, project
    /// source): ids <c>star-kind × &lt;kind&gt; × &lt;position&gt;</c>. Expected: no diagnostic and the lib's value — the
    /// taken-over variable prints the local <c>1</c> at every position (M1).</summary>
    internal static IEnumerable<RouteCell> StarKindCells()
    {
        foreach (var kind in StarKinds)
        {
            foreach (var position in TypeDefinitionOwnSymbolMatrixTests.StarReadPositions)
            {
                yield return new RouteCell($"star-kind × {kind.Kind} × {position.Name}",
                    position.Program(kind.Import, kind.Prelude, kind.Read, kind.Type), new string[0], kind.Stdout,
                    null, new[] { ("consts.spy", Consts) });
            }
        }
    }

    public static IEnumerable<object[]> StarKindCellIds() => StarKindCells().Select(c => new object[] { c.Id });

    private static RouteCell StarKindCell(string id) => StarKindCells().Single(c => c.Id == id);

    [Theory]
    [MemberData(nameof(StarKindCellIds))]
    public void StarImport_KindByPosition_HoldsOnTheProjectRoute(string id)
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        var cell = StarKindCell(id);
        using var helper = NewHelper("ImportBindingRoute", cell.Main, cell.Libs);
        var failures = JudgeRun(cell, helper);
        Judge($"project × {cell.Id}", failures, allowlist, problems);
    }

    /// <summary>The ids of the two stages of the <c>--incremental</c> twin of the star × var × def cell: the COLD stage
    /// (the star program built into an empty cache) and the WARM stage (rebuilt with consts.spy cache-served).</summary>
    private static readonly string[] IncrementalStarVarDefIds =
    {
        "star-kind × var × def (incremental, cold stage)",
        "star-kind × var × def (incremental, warm stage)",
    };

    /// <summary>
    /// The <c>--incremental</c> twin of the star × var × def cell (#2385; the axis the generator cannot enumerate), judged
    /// at BOTH stages because they DISAGREE at the base (measured @ 04f9138b3 by hand with <c>sharpyc project</c> and by
    /// this test: cold → SPY0909 "No CodeGenInfo for local 'level'"; warm → builds and prints <c>9</c>, the read spelled
    /// <c>global::…Consts.ConstsModule.Level</c> — the cache-restored symbol carries a CodeGenInfo the cold route never
    /// computes for a star-bound name, so warm ≠ cold is a cell of the plan's contract (i), not a stale row). Stage 1
    /// builds the star program into an empty cache. Stage 2 SEEDS the cache with a valid main (a failed build writes no
    /// cache, so the seed is the explicit spelling), rewrites main.spy to the star program and rebuilds with consts.spy
    /// served from the cache — asserted hard (the instrument). The contract for both stages is the same: no diagnostic,
    /// the lib's <c>9</c>.
    /// </summary>
    [Fact]
    public void StarImport_ModuleVariableInADef_HoldsOnAnIncrementalRebuild()
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        var cell = StarKindCell("star-kind × var × def");
        var seed = StarKindCell("star-kind × var-explicit (control) × def");

        using var coldHelper = NewHelper("ImportBindingRouteWarm", cell.Main, cell.Libs).WithIncremental();
        var coldFailures = JudgeRun(cell, coldHelper);
        _output.WriteLine($"{IncrementalStarVarDefIds[0]}: {(coldFailures.Count == 0 ? "green" : string.Join("; ", coldFailures))}");

        var warmFailures = JudgeWarm(cell, seed, "ImportBindingRouteWarm", "consts.spy");
        _output.WriteLine($"{IncrementalStarVarDefIds[1]}: {(warmFailures.Count == 0 ? "green" : string.Join("; ", warmFailures))}");

        Judge($"project × {IncrementalStarVarDefIds[0]}", coldFailures, allowlist, problems);
        Judge($"project × {IncrementalStarVarDefIds[1]}", warmFailures, allowlist, problems);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // P21i (ii): module-member kind × consumer, cross-module (#2386 — the #2131 cell re-homed; var × star is #2385's).
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>The project module every kind × consumer cell imports: one member of every module-level kind, each
    /// with its own value.</summary>
    private const string KindsLib =
        "count: int = 5\n"
        + "const LIMIT: int = 3\n"
        + "def helper() -> int:\n"
        + "    return 4\n"
        + "property auto_p: int = 6\n"
        + "property get fget_p() -> int:\n"
        + "    return 7\n"
        + "property get sp: int = 8\n"
        + "property set sp: int\n";

    /// <summary>(kind, the exported name, the read expression given the qualifier — empty for a bare read —, the emitted
    /// member, the value).</summary>
    private static readonly (string Kind, string Name, string Read, string Member, string Value)[] ModuleMemberKinds =
    {
        ("var", "count", "count", "Count", "5"),
        ("const", "LIMIT", "LIMIT", "LIMIT", "3"),
        ("def", "helper", "helper()", "Helper()", "4"),
        ("property-auto", "auto_p", "auto_p", "AutoP", "6"),
        ("property-get", "fget_p", "fget_p", "FgetP", "7"),
        ("property-split", "sp", "sp", "Sp", "8"),
    };

    /// <summary>The four import spellings a module member is reached through: (consumer, the import line given the
    /// name, the qualifier the read is spelled with).</summary>
    private static readonly (string Consumer, Func<string, string> Import, string Qualifier)[] ModuleMemberConsumers =
    {
        ("from-import", n => $"from lib import {n}", ""),
        ("qualified", _ => "import lib", "lib."),
        ("alias-qualified", _ => "import lib as l", "l."),
        ("star", _ => "from lib import *", ""),
    };

    /// <summary>The kind × consumer cells, read in <c>main</c> into an annotated local: ids
    /// <c>module-member × &lt;kind&gt; × &lt;consumer&gt;</c>. Expected: no diagnostic, the lib's value, and the reference
    /// emitted against the LIB's container <c>global::ImportBindingRoute.Lib.LibModule.&lt;Member&gt;</c> (not the importing
    /// module's — a mis-qualification to the own module is what goes red). Two package cells ride along: a module
    /// property re-exported through <c>pkg/__init__.spy</c> (<c>from pkg.sub import level</c>; main <c>from pkg import
    /// level</c>), with the variable twin as its control — the re-export's container is <c>PkgModule</c>, the delegating
    /// member the <c>__init__</c> emits.</summary>
    internal static IEnumerable<RouteCell> ModuleMemberCells()
    {
        foreach (var kind in ModuleMemberKinds)
        {
            foreach (var consumer in ModuleMemberConsumers)
            {
                var main = $"{consumer.Import(kind.Name)}\n\ndef main() -> None:\n    x: int = {consumer.Qualifier}{kind.Read}\n    print(x)\n";
                yield return new RouteCell($"module-member × {kind.Kind} × {consumer.Consumer}", main, new string[0],
                    kind.Value + "\n", $"global::ImportBindingRoute.Lib.LibModule.{kind.Member}", new[] { ("lib.spy", KindsLib) });
            }
        }

        // A `_private` module property is importable exactly as its `_private` variable twin is (the plan's Testing
        // Strategy pin; measured by verify-implementation @ 481cae5ea): the explicit and qualified spellings reach it,
        // and a star excludes it (SPY0200, python3's NameError) — the export arm adds no access rule of its own.
        foreach (var (kind, name, value) in new[] { ("var-private", "_v", "5"), ("property-private", "_p", "6") })
        {
            const string privateLib = "_v: int = 5\nproperty _p: int = 6\n";
            foreach (var consumer in ModuleMemberConsumers)
            {
                var main = $"{consumer.Import(name)}\n\ndef main() -> None:\n    x: int = {consumer.Qualifier}{name}\n    print(x)\n";
                var excluded = consumer.Consumer == "star";
                yield return new RouteCell($"module-member × {kind} × {consumer.Consumer}", main,
                    excluded ? new[] { $"SPY0200@4:{"    x: int = ".Length + consumer.Qualifier.Length + 1}" } : new string[0],
                    excluded ? null : value + "\n", null, new[] { ("lib.spy", privateLib) });
            }
        }

        foreach (var (kind, declaration) in new[] { ("var", "level: int = 5\n"), ("property-auto", "property level: int = 5\n") })
        {
            yield return new RouteCell($"module-member × {kind} × pkg-init-reexport",
                "from pkg import level\n\ndef main() -> None:\n    x: int = level\n    print(x)\n", new string[0], "5\n",
                "global::ImportBindingRoute.Pkg.PkgModule.Level",
                new[] { ("pkg/__init__.spy", "from pkg.sub import level\n"), ("pkg/sub.spy", declaration) });
        }
    }

    public static IEnumerable<object[]> ModuleMemberCellIds() => ModuleMemberCells().Select(c => new object[] { c.Id });

    private static RouteCell ModuleMemberCell(string id) => ModuleMemberCells().Single(c => c.Id == id);

    [Theory]
    [MemberData(nameof(ModuleMemberCellIds))]
    public void ModuleMember_KindByConsumer_HoldsOnTheProjectRoute(string id)
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        var cell = ModuleMemberCell(id);
        using var helper = NewHelper("ImportBindingRoute", cell.Main, cell.Libs);
        var failures = JudgeRun(cell, helper);
        Judge($"project × {cell.Id}", failures, allowlist, problems);
    }

    /// <summary>The id of the <c>--incremental</c> twin of the module property's from-import cell.</summary>
    private const string IncrementalModulePropertyId = "module-member × property-auto × from-import (incremental)";

    /// <summary>The <c>--incremental</c> twin of <c>from lib import auto_p</c> (#2386; the cache round-trips
    /// <c>IsModuleProperty</c>/<c>HasPropertyGetter</c>): the seed is the variable's from-import cell, lib.spy is
    /// cache-served on the warm build, and the warm program must print the property's <c>6</c>.</summary>
    [Fact]
    public void ModuleProperty_FromImport_HoldsOnAnIncrementalRebuild()
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        var cell = ModuleMemberCell("module-member × property-auto × from-import");
        var seed = ModuleMemberCell("module-member × var × from-import");
        var failures = JudgeWarm(cell, seed, "ImportBindingRouteWarm", "lib.spy");
        Judge($"project × {IncrementalModulePropertyId}", failures, allowlist, problems);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // P21i (iii): import × import over provenance × spelling × order (#2389, R-JK; the use cells of a plain-module
    // re-export are #2116's CS0117 — P25's, pinned not cured).
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>The lib set every provenance cell compiles over: <c>core.helper</c> is the declaration; <c>lib1</c> and
    /// <c>lib2</c> re-export it by a plain from-import, <c>lib3</c> by a star, <c>lib1b</c> re-exports <c>lib1</c>'s re-export
    /// (two hops), <c>lib1a</c> re-exports it under ANOTHER name (<c>h</c>); <c>utils/__init__.spy</c> re-exports
    /// <c>utils.helpers.fmt</c> through the package route (no clone — the original symbol).</summary>
    private static readonly (string Path, string Content)[] ProvenanceLibs =
    {
        ("core.spy", "def helper() -> int:\n    return 1\n"),
        ("lib1.spy", "from core import helper\n"),
        ("lib2.spy", "from core import helper\n"),
        ("lib3.spy", "from core import *\n"),
        ("lib1b.spy", "from lib1 import helper\n"),
        ("lib1a.spy", "from core import helper as h\n"),
        ("utils/__init__.spy", "from utils.helpers import fmt\n"),
        ("utils/helpers.spy", "def fmt() -> str:\n    return \"f\"\ndef other() -> str:\n    return \"o\"\n"),
    };

    /// <summary>The file names <see cref="ProvenanceLibs"/> compiles, as the warm-build assertion names them.</summary>
    private static readonly string[] ProvenanceLibNames = ProvenanceLibs.Select(l => Path.GetFileName(l.Path)).ToArray();

    /// <summary>A provenance: the RE-EXPORT import line and the DIRECT import line, each given the spelling's bound
    /// name (null for a spelling that binds the exporter's own name), the use expression given the bound name, and the
    /// use's stdout. <c>Spellings</c> lists the spellings the provenance admits (a star has no <c>as</c>).</summary>
    private sealed record Provenance(string Name, Func<string?, string> ReExport, Func<string?, string> Direct,
        string BoundName, Func<string, string> Use, string Stdout, string[] Spellings, string[]? ImportAs = null,
        string? ImportAsUse = null);

    private static string As(string? alias) => alias == null ? "" : $" as {alias}";

    /// <summary>The provenances (P21i (iii)): the five #2389 cells, the two-hop re-export, the aliasing re-exporter
    /// (the one provenance where the declaration's name differs from the bound name), and the DIFFERENT-symbol control
    /// (R12b: two symbols under one name still collide). <c>ImportAs</c> is the attribute spelling's module pair
    /// (<c>import m as a</c> + <c>import n as b</c>: no module-level NAME collides — the control) and
    /// <c>ImportAsUse</c> its read through the re-exporter.</summary>
    private static readonly Provenance[] Provenances =
    {
        new("init-reexport", a => $"from utils import fmt{As(a)}", a => $"from utils.helpers import fmt{As(a)}", "fmt",
            n => $"{n}()", "f\n", new[] { "from", "from-as" }, new[] { "import utils as a", "import utils.helpers as b" }, "a.fmt()"),
        new("module-reexport", a => $"from lib1 import helper{As(a)}", a => $"from core import helper{As(a)}", "helper",
            n => $"{n}()", "1\n", new[] { "from", "from-as" }, new[] { "import lib1 as a", "import core as b" }, "a.helper()"),
        new("two-hop-reexport", a => $"from lib1b import helper{As(a)}", a => $"from core import helper{As(a)}", "helper",
            n => $"{n}()", "1\n", new[] { "from", "from-as" }, new[] { "import lib1b as a", "import core as b" }, "a.helper()"),
        new("aliasing-reexporter", a => $"from lib1a import h{As(a)}", a => $"from core import helper as {a ?? "h"}", "h",
            n => $"{n}()", "1\n", new[] { "from", "from-as" }, new[] { "import lib1a as a", "import core as b" }, "a.h()"),
        new("star-of-reexporter", _ => "from lib3 import *", _ => "from core import helper", "helper",
            n => $"{n}()", "1\n", new[] { "star" }),
        new("two-stars", _ => "from lib1 import *", _ => "from lib2 import *", "helper",
            n => $"{n}()", "1\n", new[] { "star" }),
        new("different-symbol (control)", a => $"from utils import fmt{As(a)}", a => $"from utils.helpers import other as {a ?? "fmt"}", "fmt",
            n => $"{n}()", "o\n", new[] { "from", "from-as" }),
    };

    private const string AliasName = "k";

    /// <summary>
    /// The provenance × spelling × order cells, each as a VERDICT cell (<c>print("ok")</c>; the contract is the
    /// diagnostic set) and a USE cell (<c>print(helper())</c> / <c>print(fmt())</c>; the contract is the value): ids
    /// <c>import-identity × &lt;provenance&gt; × &lt;spelling&gt; × &lt;order&gt; × verdict|use</c>. Expected (R-JK, D6): one
    /// declaration imported through two module spellings is ONE import — no diagnostic, the verdict prints <c>ok</c>, the
    /// use prints the declaration's value; the DIFFERENT-symbol control (R12b) is SPY0204 at the SECOND import's ImportAlias
    /// in both orders (the later binding is refused — its use cell is the verdict). The <c>import-as</c> spelling binds two
    /// MODULE names, which never collide (verdict: a control); its use reads through the re-exporter. The single-import
    /// controls: the direct package import (R12c, runs <c>f</c>) and each re-exporter imported ALONE (R11d and kin: the
    /// verdict is a control, the use is #2116's CS0117 — P25's). For <c>two-stars</c> the "re-export" is <c>lib1</c>'s star
    /// and the "direct" <c>lib2</c>'s.
    /// </summary>
    internal static IEnumerable<RouteCell> ImportIdentityCells()
    {
        RouteCell Cell(string id, string[] lines, string body, string[] errors, string? stdout)
            => new(id, string.Join("\n", lines) + "\n\ndef main() -> None:\n    " + body + "\n", errors, stdout, null, ProvenanceLibs);

        foreach (var provenance in Provenances)
        {
            var control = provenance.Name.Contains("control", StringComparison.Ordinal);
            foreach (var spelling in provenance.Spellings)
            {
                var alias = spelling == "from-as" ? AliasName : null;
                var bound = alias ?? provenance.BoundName;
                var reExport = provenance.ReExport(alias);
                var direct = provenance.Direct(alias);
                foreach (var order in new[] { "reexport-first", "direct-first" })
                {
                    var lines = order == "reexport-first" ? new[] { reExport, direct } : new[] { direct, reExport };
                    var errors = control
                        ? new[] { $"SPY0204@2:{TypeDefinitionOwnSymbolMatrixTests.ImportAliasColumn(lines[1])}" }
                        : new string[0];
                    yield return Cell($"import-identity × {provenance.Name} × {spelling} × {order} × verdict", lines,
                        "print(\"ok\")", errors, "ok\n");
                    if (!control)
                        yield return Cell($"import-identity × {provenance.Name} × {spelling} × {order} × use", lines,
                            $"print({provenance.Use(bound)})", errors, provenance.Stdout);
                }
            }

            if (provenance.ImportAs is { } modules)
            {
                foreach (var order in new[] { "reexport-first", "direct-first" })
                {
                    var lines = order == "reexport-first" ? modules : new[] { modules[1], modules[0] };
                    yield return Cell($"import-identity × {provenance.Name} × import-as × {order} × verdict", lines,
                        "print(\"ok\")", new string[0], "ok\n");
                    yield return Cell($"import-identity × {provenance.Name} × import-as × {order} × use", lines,
                        $"print({provenance.ImportAsUse})", new string[0], provenance.Stdout);
                }
            }

            // The re-exporter imported ALONE (no second import): the verdict is a control; the use through a plain-module
            // re-export is #2116's CS0117 (R11d), through the package route it runs (R12c).
            if (!control)
            {
                var alone = new[] { provenance.ReExport(null) };
                yield return Cell($"import-identity × {provenance.Name} × from × alone × verdict", alone, "print(\"ok\")",
                    new string[0], "ok\n");
                yield return Cell($"import-identity × {provenance.Name} × from × alone × use", alone,
                    $"print({provenance.Use(provenance.BoundName)})", new string[0], provenance.Stdout);
            }
        }

        // R12c: the direct package import alone — the control every __init__ cell is measured against.
        yield return Cell("import-identity × direct (control) × from × alone × verdict", new[] { "from utils import fmt" },
            "print(\"ok\")", new string[0], "ok\n");
        yield return Cell("import-identity × direct (control) × from × alone × use", new[] { "from utils import fmt" },
            "print(fmt())", new string[0], "f\n");
    }

    public static IEnumerable<object[]> ImportIdentityCellIds() => ImportIdentityCells().Select(c => new object[] { c.Id });

    private static RouteCell ImportIdentityCell(string id) => ImportIdentityCells().Single(c => c.Id == id);

    [Theory]
    [MemberData(nameof(ImportIdentityCellIds))]
    public void ImportIdentity_HoldsOverProvenance(string id)
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        var cell = ImportIdentityCell(id);
        using var helper = NewHelper("ImportBindingRoute", cell.Main, cell.Libs);
        var failures = JudgeRun(cell, helper);
        Judge($"project × {cell.Id}", failures, allowlist, problems);
    }

    /// <summary>The lib set of the takeover × provenance cells: <c>core.level</c> is the declaration (9); <c>lib1</c>
    /// re-exports it by a plain from-import, <c>lib3</c> by a star, <c>utils/__init__.spy</c> through the package route.</summary>
    private static readonly (string Path, string Content)[] TakeoverProvenanceLibs =
    {
        ("core.spy", "level: int = 9\n"),
        ("lib1.spy", "from core import level\n"),
        ("lib3.spy", "from core import *\n"),
        ("utils/__init__.spy", "from utils.helpers import level\n"),
        ("utils/helpers.spy", "level: int = 9\n"),
    };

    /// <summary>
    /// A module variable of the importing file that takes over a name a star bound AND an explicit import of the SAME
    /// declaration named (the explicit import merges into the star's binding — one identity, R-JK) × provenance
    /// {same module, star of a re-exporter + direct, package star + submodule, star + plain re-exporter, two stars}:
    /// ids <c>import-takeover × &lt;provenance&gt;</c>. Expected: no diagnostic and the file's OWN <c>2</c> (python3 2) —
    /// the takeover drops the name from every from-import's recorded binding, so it is never spelled through an
    /// import's module. Measured by verify-implementation (R1): @ 808d82955 same-module printed 2 and the others were
    /// SPY0204 (identity was the spelled module); @ 481cae5ea the explicit arm registered the file's own variable under
    /// the import's container — 9, 9, 9 and CS0117 'Lib1Module'; two stars printed 2 throughout (no explicit import).
    /// </summary>
    internal static IEnumerable<RouteCell> TakeoverProvenanceCells()
    {
        RouteCell Cell(string provenance, string first, string second)
            => new($"import-takeover × {provenance}",
                $"{first}\n{second}\nlevel: int = 2\n\ndef main() -> None:\n    print(level)\n",
                new string[0], "2\n", null, TakeoverProvenanceLibs);

        yield return Cell("same-module", "from core import *", "from core import level");
        yield return Cell("star-of-reexporter + direct", "from lib3 import *", "from core import level");
        yield return Cell("package-star + submodule", "from utils import *", "from utils.helpers import level");
        yield return Cell("star + plain-reexporter", "from core import *", "from lib1 import level");
        yield return Cell("two-stars (control)", "from lib3 import *", "from core import *");
    }

    public static IEnumerable<object[]> TakeoverProvenanceCellIds()
        => TakeoverProvenanceCells().Select(c => new object[] { c.Id });

    [Theory]
    [MemberData(nameof(TakeoverProvenanceCellIds))]
    public void ImportTakeover_KeepsTheFilesOwnVariable_OverProvenance(string id)
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        var cell = TakeoverProvenanceCells().Single(c => c.Id == id);
        using var helper = NewHelper("ImportBindingRoute", cell.Main, cell.Libs);
        var failures = JudgeRun(cell, helper);
        Judge($"project × {cell.Id}", failures, allowlist, problems);
    }

    /// <summary>The warm twins of the provenance matrix (the axis the generator cannot enumerate): R12's verdict and use,
    /// and the aliasing re-exporter's (the provenance where the declaration's name differs from the bound name — a
    /// restored symbol's own name is what a cache-served re-exporter carries), each re-export first.</summary>
    private static readonly string[] IncrementalImportIdentityIds =
    {
        "import-identity × init-reexport × from × reexport-first × verdict (incremental)",
        "import-identity × init-reexport × from × reexport-first × use (incremental)",
        "import-identity × aliasing-reexporter × from × reexport-first × verdict (incremental)",
        "import-identity × aliasing-reexporter × from × reexport-first × use (incremental)",
    };

    public static IEnumerable<object[]> IncrementalImportIdentityCellIds()
        => IncrementalImportIdentityIds.Select(id => new object[] { id });

    /// <summary>The <c>--incremental</c> twin of a provenance cell: the seed is the direct package import's use (R12c, a
    /// valid program over the same lib set), main.spy is rewritten to the cell's program, and every lib file must be
    /// cache-served on the warm build (hard).</summary>
    [Theory]
    [MemberData(nameof(IncrementalImportIdentityCellIds))]
    public void ImportIdentity_HoldsOnAnIncrementalRebuild(string warmId)
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        var cell = ImportIdentityCell(warmId.Replace(" (incremental)", "", StringComparison.Ordinal));
        var seed = ImportIdentityCell("import-identity × direct (control) × from × alone × use");
        var failures = JudgeWarm(cell, seed, "ImportBindingRouteWarm", ProvenanceLibNames);
        Judge($"project × {warmId}", failures, allowlist, problems);
    }

    /// <summary>The refusals that involve a RE-EXPORTING star (P21i D6): <c>lib3</c>'s star exports <c>core.helper</c>,
    /// and <c>other.helper</c> is a different symbol — refused (R-IZ at the explicit import in both orders; star × star at
    /// the second star). The import identity is the DEFINING module (<c>core</c>), but a diagnostic names the module the
    /// source SPELLED: (id, main, the message's required substrings).</summary>
    public static TheoryData<string, string, string[]> ReExportingStarRefusalCells() => new()
    {
        { "R-IZ × explicit-first", "from other import helper\nfrom lib3 import *",
            new[] { "also exported by 'from lib3 import *'", "write 'lib3.helper'" } },
        { "R-IZ × star-first", "from lib3 import *\nfrom other import helper",
            new[] { "also exported by 'from lib3 import *'", "write 'lib3.helper'" } },
        { "star × star", "from lib3 import *\nfrom other import *",
            new[] { "exported by both 'from lib3 import *'", "and 'from other import *'" } },
    };

    [Theory]
    [MemberData(nameof(ReExportingStarRefusalCells))]
    public void ReExportingStarRefusal_NamesTheSpelledModule(string id, string main, string[] required)
    {
        using var helper = NewHelper("ImportBindingRoute", main + "\n\ndef main() -> None:\n    print(\"ok\")\n",
            ProvenanceLibs.Append(("other.spy", "def helper() -> int:\n    return 2\n")).ToArray());
        helper.CompileAndExecute();
        var refusal = helper.LastCompilationResult!.Diagnostics.GetAll().Where(d => d.Code == "SPY0204").ToList();
        _output.WriteLine($"{id}: {string.Join(" | ", refusal.Select(d => d.Message))}");

        refusal.Should().ContainSingle($"{id}: two different symbols under one name are one refusal");
        foreach (var part in required)
            refusal[0].Message.Should().Contain(part, $"{id}: the refusal names the module the source spelled");
        refusal[0].Message.Should().NotContain("core", $"{id}: never the defining module the identity compares by");
    }

    /// <summary>The type kinds a re-exporter forwards (P21i F1): (kind, the declaration lines, the declared name).</summary>
    private static readonly (string Kind, string Declaration, string Name)[] ReExportedTypeKinds =
    {
        ("class", "class Box:\n    v: int = 8\n", "Box"),
        ("enum", "enum Color:\n    A = 1\n    B = 2\n", "Color"),
        ("interface", "interface Shape:\n    def area(self) -> int:\n        ...\n", "Shape"),
        ("struct", "struct Pt:\n    x: int\n", "Pt"),
    };

    /// <summary>
    /// The type re-export cells (P21i F1, #2389): a TYPE imported through a re-exporter AND directly is one import
    /// whichever module Phase 4 binds first — kind {class, enum, interface, struct} × the entry file sorting BEFORE
    /// (<c>aa.spy</c>) or AFTER (<c>zz.spy</c>) the declaring and re-exporting modules × re-exporter {a plain module
    /// <c>lib1.spy</c>, a package <c>__init__</c>}. At ef0047891 the type clone carried no origin chain, so the
    /// identity fell back to the spelled re-exporter whenever the entry was bound before the re-exporter: the aa cells
    /// were SPY0204 "'Box' already names 'lib1.Box'", the zz cells ran. (kind, order, re-exporter.)
    /// </summary>
    public static TheoryData<string, string, string> TypeReExportOrderCells()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var kind in ReExportedTypeKinds)
            foreach (var order in new[] { "entry-sorts-before", "entry-sorts-after" })
                foreach (var reExporter in new[] { "plain-module", "package-init" })
                    data.Add(kind.Kind, order, reExporter);
        return data;
    }

    [Theory]
    [MemberData(nameof(TypeReExportOrderCells))]
    public void TypeImportedThroughAReExporterAndDirectly_IsOneImport_InEitherBindOrder(string kind, string order, string reExporter)
    {
        var type = ReExportedTypeKinds.Single(k => k.Kind == kind);
        var entry = order == "entry-sorts-before" ? "aa.spy" : "zz.spy";
        var (declaring, reExportLine, directLine) = reExporter == "plain-module"
            ? ("core.spy", $"from lib1 import {type.Name}", $"from core import {type.Name}")
            : ("pkg/core.spy", $"from pkg import {type.Name}", $"from pkg.core import {type.Name}");
        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("TypeReExport")
            .WithOutputType("exe")
            .WithEntryPoint(entry);
        helper.AddSourceFile(declaring, type.Declaration);
        if (reExporter == "plain-module")
            helper.AddSourceFile("lib1.spy", $"from core import {type.Name}\n");
        else
            helper.AddSourceFile("pkg/__init__.spy", $"from pkg.core import {type.Name}\n");
        helper.AddSourceFile(entry, $"{reExportLine}\n{directLine}\n\ndef main() -> None:\n    print(\"ok\")\n", isEntryPoint: true);
        var run = helper.CompileAndExecute();
        var errors = helper.LastCompilationResult!.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message}").ToList();
        _output.WriteLine($"{kind} × {order} × {reExporter}: success={run.Success} stdout=[{run.StandardOutput.TrimEnd()}] {string.Join(" | ", errors)}");

        errors.Should().BeEmpty($"{kind} × {order} × {reExporter}: one declaration imported through two module spellings is one import");
        run.StandardOutput.Should().Be("ok\n");
    }

    /// <summary>The negative control (P21i F1): two DIFFERENT types under one alias still collide in either bind order —
    /// the chained identity is the defining module.symbol of each, never the alias.</summary>
    [Theory]
    [InlineData("aa.spy")]
    [InlineData("zz.spy")]
    public void TwoDifferentTypesUnderOneAlias_StillCollide(string entry)
    {
        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("TypeReExportNegative")
            .WithOutputType("exe")
            .WithEntryPoint(entry);
        helper.AddSourceFile("core.spy", "class Box:\n    v: int = 8\n\nclass Other:\n    v: int = 9\n");
        helper.AddSourceFile("lib1.spy", "from core import Box\n");
        helper.AddSourceFile(entry, "from lib1 import Box as B\nfrom core import Other as B\n\ndef main() -> None:\n    print(\"ok\")\n",
            isEntryPoint: true);
        helper.CompileAndExecute();
        var refusals = helper.LastCompilationResult!.Diagnostics.GetErrors().Where(d => d.Code == "SPY0204").ToList();
        _output.WriteLine($"{entry}: {string.Join(" | ", refusals.Select(d => $"{d.Line}:{d.Column} {d.Message}"))}");

        refusals.Should().ContainSingle($"{entry}: Box and Other are two symbols under one name");
        refusals[0].Line.Should().Be(2);
    }

    /// <summary>The layout × spelling cells (P21i L1): <c>core.spy</c> and <c>other.spy</c> declare DIFFERENT
    /// <c>helper</c>s (<c>int</c> 1 vs <c>str</c> "o"), and main reaches OTHER's through every spelling, read into an
    /// annotated <c>str</c> local. The layout axis is the source directory: flat (the project directory) or
    /// <c>src/</c> — module scopes are keyed by the project-relative module path (<c>src.other</c>), and a lookup by the
    /// SPELLED module name bound core's <c>helper</c> in the src/ layout only (SPY0220 'int32' to 'str'). (layout,
    /// spelling, main.)</summary>
    public static TheoryData<string, string, string> LayoutBySpellingCells()
    {
        var data = new TheoryData<string, string, string>();
        var spellings = new (string Spelling, string Import, string Read)[]
        {
            ("from", "from other import helper", "helper()"),
            ("from-as", "from other import helper as h", "h()"),
            ("qualified", "import other", "other.helper()"),
            ("alias-qualified", "import other as o", "o.helper()"),
            ("star", "from other import *", "helper()"),
        };
        foreach (var layout in new[] { "flat", "src" })
            foreach (var (spelling, import, read) in spellings)
                data.Add(layout, spelling, $"{import}\n\ndef main() -> None:\n    x: str = {read}\n    print(x)\n");
        return data;
    }

    [Theory]
    [MemberData(nameof(LayoutBySpellingCells))]
    public void SameNamedDeclarationOfAnotherModule_IsNotWhatTheImportBinds(string layout, string spelling, string main)
    {
        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("ImportBindingLayout")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        if (layout == "flat")
        {
            helper.WithSourceDirectory(".");
            helper.Options.SourceFilePattern = "**/*.spy";
        }
        helper.AddSourceFile("core.spy", "def helper() -> int:\n    return 1\n");
        helper.AddSourceFile("other.spy", "def helper() -> str:\n    return \"o\"\n");
        helper.AddSourceFile("main.spy", main, isEntryPoint: true);
        var run = helper.CompileAndExecute();
        var errors = helper.LastCompilationResult!.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message}").ToList();
        _output.WriteLine($"{layout} × {spelling}: success={run.Success} stdout=[{run.StandardOutput.TrimEnd()}] {string.Join(" | ", errors)}");

        errors.Should().BeEmpty($"{layout} × {spelling}: the import binds OTHER's helper (str), never core's");
        run.Success.Should().BeTrue();
        run.StandardOutput.Should().Be("o\n");
    }

    /// <summary>Compiles and runs <paramref name="helper"/>'s project and judges it against <paramref name="cell"/>: the
    /// exact errors in main.spy, the stdout, the emitted spelling (when the cell names one). An SPY09xx anywhere is a
    /// FAILURE here, not a hard problem — the project route's <c>Compile</c> runs code generation and the C# compile, so
    /// an SPY0909/SPY0908 is the cell's measured red exactly as the single-file generator's run arm treats it, and it
    /// ratchets.</summary>
    private List<string> JudgeRun(RouteCell cell, ProjectCompilationHelper helper)
    {
        var failures = new List<string>();
        var run = helper.CompileAndExecute();
        var result = helper.LastCompilationResult!;
        var all = result.Diagnostics.GetAll().ToList();
        var everyMessage = string.Join(" | ", all.Select(d => $"{d.Code} {Path.GetFileName(d.FilePath)}:{d.Line}:{d.Column} {d.Message}"));
        var inMain = all.Where(d => d.FilePath != null && Path.GetFileName(d.FilePath) == "main.spy").ToList();
        var actualErrors = inMain.Where(d => d.IsError).Select(d => $"{d.Code}@{d.Line}:{d.Column}")
            .OrderBy(s => s, StringComparer.Ordinal).ToList();
        _output.WriteLine($"{cell.Id}: success={run.Success} stdout=[{run.StandardOutput.TrimEnd()}] [{string.Join(" ", actualErrors)}] {everyMessage}");

        var internals = all.Where(d => d.Code != null && d.Code.StartsWith("SPY09", StringComparison.Ordinal)).ToList();
        if (internals.Count > 0)
            failures.Add($"an internal error: {string.Join(" | ", internals.Select(d => $"{d.Code} {d.Message}")).ReplaceLineEndings(" ")}");
        var expectedErrors = cell.Errors.OrderBy(s => s, StringComparer.Ordinal).ToList();
        if (!actualErrors.SequenceEqual(expectedErrors, StringComparer.Ordinal))
            failures.Add($"errors in main.spy are [{string.Join(" ", actualErrors)}], expected [{string.Join(" ", expectedErrors)}] — {everyMessage}");
        if (cell.Errors.Length == 0)
        {
            if (!run.Success || run.StandardOutput != cell.Stdout)
                failures.Add($"run: success={run.Success} stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}], expected [{cell.Stdout?.ReplaceLineEndings("\\n")}] "
                    + string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" "));
            if (cell.Spelling != null)
            {
                var cs = string.Concat(result.GeneratedCSharpFiles.Values);
                if (!cs.Contains(cell.Spelling, StringComparison.Ordinal))
                    failures.Add($"the emitted reference is not spelled '{cell.Spelling}'");
                if (cs.Contains("using static ", StringComparison.Ordinal))
                    failures.Add("a `using static` directive is emitted (#1683 close criterion)");
            }
        }

        return failures;
    }

    /// <summary>The warm-rebuild judge: a seed build of <paramref name="seed"/>'s main (a valid program over the same
    /// lib set) populates the cache, main.spy is rewritten to <paramref name="cell"/>'s program, and the warm build is
    /// judged by <see cref="JudgeRun"/> with the lib files asserted cache-served (hard: a silent full rebuild would make
    /// the twin a duplicate of the cold cell).</summary>
    private List<string> JudgeWarm(RouteCell cell, RouteCell seed, string rootNamespace, params string[] cacheServed)
    {
        using var helper = NewHelper(rootNamespace, seed.Main, cell.Libs).WithIncremental();
        var cold = helper.Compile();
        cold.Diagnostics.GetErrors().Should().BeEmpty($"the seed build ({seed.Id}) compiles and populates the cache");
        helper.UpdateSourceFile("main.spy", cell.Main);
        // A cell's spelling names the cold route's root namespace; the warm build compiles under its own.
        var warmCell = cell with
        {
            Spelling = cell.Spelling?.Replace("global::ImportBindingRoute.", $"global::{rootNamespace}.", StringComparison.Ordinal),
        };
        var failures = JudgeRun(warmCell, helper);
        helper.AssertWarmBuildSkipped(helper.LastCompilationResult!, cacheServed);
        return failures;
    }

    private ProjectCompilationHelper NewHelper(string rootNamespace, string main)
    {
        var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace(rootNamespace)
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        helper.AddSourceFile("lib.spy", Lib);
        if (main.Contains("libb", StringComparison.Ordinal))
            helper.AddSourceFile("libb.spy", LibB);
        helper.AddSourceFile("main.spy", main, isEntryPoint: true);
        return helper;
    }

    /// <summary>A helper over a per-cell lib file set (P21i): the cell names its modules.</summary>
    private ProjectCompilationHelper NewHelper(string rootNamespace, string main, (string Path, string Content)[] libs)
    {
        var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace(rootNamespace)
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        foreach (var (path, content) in libs)
            helper.AddSourceFile(path, content);
        helper.AddSourceFile("main.spy", main, isEntryPoint: true);
        return helper;
    }

    private void Judge(string id, List<string> failures, IReadOnlyDictionary<string, string> allowlist, List<string> problems)
    {
        if (failures.Count > 0)
            _output.WriteLine($"    red: {string.Join("; ", failures)}");
        TypeDefinitionOwnSymbolMatrixTests.Ratchet(id, failures, allowlist, problems, new List<string>());
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
