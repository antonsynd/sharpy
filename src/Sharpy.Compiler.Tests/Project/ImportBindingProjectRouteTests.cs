using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// The import-binding verdicts on the PROJECT route (#2359, R-II): an import binds a module-level
/// name through <c>ImportResolver.BindImportName</c>, the one seam both compile routes share. The
/// declaration-position generator (<c>TypeDefinitionOwnSymbolMatrixTests.ImportBinding_HasOneVerdictPerCollider</c>)
/// pins the matrix on one file; this class pins the multi-file twin — an import of a PROJECT module
/// (<c>lib.spy</c>, bound through <c>ResolveImportSymbol</c>'s Phase 3 originals), the dotted-root merge
/// (<c>import os</c> + <c>import os.path</c>, both orders), the module-variable cell (c18), the star import a
/// module variable re-declares, and the
/// plain-vs-alias split of a builtin TYPE name (a plain <c>from lib import double</c> is warned and
/// runs; <c>from lib import helper as double</c> is refused).
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
        + "level: int = 9\n";

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
    }

    private static object?[] Cell(string id, string main, string[] errors, string[]? warnings = null,
        string? stdout = null)
        => new object?[] { id, main, errors, warnings ?? new string[0], stdout };

    [Theory]
    [MemberData(nameof(Cells))]
    public void ImportBinding_VerdictHoldsOnTheProjectRoute(string id, string main, string[] errors,
        string[] warnings, string? stdout)
    {
        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("ImportBindingRoute")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        helper.AddSourceFile("lib.spy", Lib);
        helper.AddSourceFile("main.spy", main, isEntryPoint: true);

        var result = helper.Compile();
        var inMain = result.Diagnostics.GetAll()
            .Where(d => d.FilePath != null && Path.GetFileName(d.FilePath) == "main.spy").ToList();
        var actualErrors = inMain.Where(d => d.IsError).Select(d => $"{d.Code}@{d.Line}:{d.Column}")
            .OrderBy(s => s, StringComparer.Ordinal).ToList();
        var actualWarnings = inMain.Where(d => !d.IsError && d.Code is "SPY0483" or "SPY0484")
            .Select(d => $"{d.Code}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
        var everyMessage = string.Join("\n", result.Diagnostics.GetAll().Select(d => $"{d.Code} {d.FilePath}:{d.Line}:{d.Column} {d.Message}"));

        actualErrors.Should().Equal(errors.OrderBy(s => s, StringComparer.Ordinal), $"{id}: errors in main.spy\n{everyMessage}");
        actualWarnings.Should().Equal(warnings.OrderBy(s => s, StringComparer.Ordinal), $"{id}: shadowing warnings in main.spy\n{everyMessage}");
        result.Diagnostics.GetErrors().Should().NotContain(d => d.Code != null && d.Code.StartsWith("SPY09"), id);

        if (errors.Length == 0)
        {
            var run = helper.CompileAndExecute();
            run.Success.Should().BeTrue($"{id}: {string.Join("\n", run.CompilationErrors)}");
            run.StandardOutput.Should().Be(stdout, id);
        }
    }
}
