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
/// not lib's <c>9</c>) and its <c>--incremental</c> twin (the warm rebuild, lib.spy served from the cache).
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
        => Cells().Select(c => (string)c[0]!).Append(IncrementalStarPropertyId).Select(id => $"project × {id}");

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

    private void Judge(string id, List<string> failures, IReadOnlyDictionary<string, string> allowlist, List<string> problems)
    {
        if (failures.Count > 0)
            _output.WriteLine($"    red: {string.Join("; ", failures)}");
        TypeDefinitionOwnSymbolMatrixTests.Ratchet(id, failures, allowlist, problems, new List<string>());
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
