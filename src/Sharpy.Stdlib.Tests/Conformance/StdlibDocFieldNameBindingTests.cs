using System.Text;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Stdlib.Tests.Integration;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;
using SList = System.Collections.Generic.List<string>;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// #2264: every FIELD a generated <c>docs/stdlib/*.md</c> page names — a row of a module-level
/// <c>## Constants</c> or <c>## Properties</c> table (written <c>module.NAME</c>) or of a type's
/// <c>### Constants</c> table (written <c>Type.NAME</c>) — compiles and binds a member of the
/// documented type. One program per module page binds every row to a local annotated with the row's
/// Type column (<c>v3: int = subprocess.PIPE</c>), with every type the page has a section for
/// imported; a row whose name is not a member is SPY0203, and a row whose name is a DIFFERENT member
/// is SPY0220 (the documented <c>logging.debug</c> was the logging FUNCTION, not the level).
/// <para>Before #2264 the generator snake-cased every constant (<c>pascal_to_snake</c>) while the
/// compiler keeps a CONSTANT_CASE field as written (<c>NameMangler.ToSharpyName(…, Field)</c>):
/// measured @ a08d35fcf, 21 module Constants rows were SPY0203 (<c>calendar.monday</c>,
/// <c>http.http_port</c>, <c>subprocess.pipe</c>, <c>tarfile.regtype</c>,
/// <c>threading.timeout_max</c>, <c>uuid.namespace_dns</c>, …), the five <c>logging</c> levels bound
/// the functions, and 22 zipfile/zlib module properties (<c>zlib.z_best_speed</c>) were the same
/// rule on a static property. <c>sharpy_field_name</c> in <c>build_tools/generate_stdlib_docs.py</c>
/// mirrors the compiler's rule; this test is what keeps that copy honest.</para>
/// <para>#2272, the generator's OTHER mechanism — ownership: it rendered every un-annotated public class's
/// members at module level although the compiler binds module members on the <c>[SharpyModule]</c>
/// class only (<c>numpy.start</c> was <c>SliceSpec.start</c>, <c>os.join</c> was <c>os.path.join</c>,
/// <c>numpy.fft</c> was <c>numpy.fft.fft</c>; 50 rows measured @ 729bf1e7d). The function-heading twin
/// (<see cref="EveryDocumentedModuleFunction_BindsAMemberOfItsModule"/>) binds every documented
/// <c>module.f</c> / <c>module.sub.f</c> heading as a value. A submodule section (<c>## numpy.fft</c>)
/// is imported as <c>import numpy.fft</c>; a type section the compiler does not export (marked
/// <c>*Not importable by name.*</c>) is never imported. Not covered: type-section method headings and
/// type-level Properties tables (static and instance members are not told apart on the page).</para>
/// </summary>
public class StdlibDocFieldNameBindingTests : StdlibIntegrationTestBase
{
    public StdlibDocFieldNameBindingTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>Documented field spelling → tracking issue. Each drains when the row binds.</summary>
    private static readonly Dictionary<string, string> KnownUnbound = new();

    /// <summary>Documented function spelling → tracking issue. Each drains when the heading binds.</summary>
    private static readonly Dictionary<string, string> KnownUnboundFunctions = new();

    /// <summary>The semantic pass stops reporting at this many errors (<c>FileCompilationPipeline</c>'s
    /// default); a page program that reaches it could hide a row's SPY0203 behind it.</summary>
    private const int SemanticErrorCap = 100;

    /// <summary>One documented field: the spelling a reader copies, its Type column, its owner.
    /// <paramref name="Module"/> is the section's module — <c>numpy.fft</c> for a submodule section's row.</summary>
    internal sealed record FieldRow(string Module, string? Owner, string Name, string Type)
    {
        public string Spelling => Owner == null ? $"{Module}.{Name}" : $"{Module}.{Owner}.{Name}";
        public string Reference => Owner == null ? $"{Module}.{Name}" : $"{Owner}.{Name}";
    }

    /// <param name="TypeSections">Importable type sections (<c>from module import Type</c>).</param>
    /// <param name="Submodules">Submodule sections (<c>import numpy.fft</c>).</param>
    /// <param name="Functions">Distinct documented function spellings (<c>os.getcwd</c>, <c>os.path.join</c>).</param>
    internal sealed record DocPage(
        string Module,
        IReadOnlyList<string> TypeSections,
        IReadOnlyList<FieldRow> Rows,
        IReadOnlyList<string> Submodules,
        IReadOnlyList<string> Functions);

    private static readonly Regex Heading2 = new(@"^## (.+)$");
    private static readonly Regex FunctionHeading = new(@"^### `([A-Za-z_][\w.]*)\(");
    private static readonly Regex UnescapedPipe = new(@"(?<!\\)\|");
    private static readonly HashSet<string> SectionWords = new() { "Constants", "Properties", "Functions", "Methods" };

    /// <summary>The generator's first sentence under a type section the compiler does not export
    /// (<c>NOT_IMPORTABLE_NOTE</c> in <c>build_tools/generate_stdlib_docs.py</c>).</summary>
    private const string NotImportableNote = "*Not importable by name.*";

    /// <summary>Parses a generated page; null when it documents a Core type rather than a module.</summary>
    internal static DocPage? ParsePage(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (!lines[0].StartsWith("# ", StringComparison.Ordinal))
            return null;
        var module = lines[0].Substring(2).Trim();
        if (!lines.Contains($"import {module}"))
            return null;

        var typeSections = new SList();
        var submodules = new SList();
        var functions = new SList();
        var rows = new System.Collections.Generic.List<FieldRow>();
        string? owner = null;
        string? submodule = null;
        string? table = null;
        var inFunctions = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var h2 = Heading2.Match(line);
            if (h2.Success)
            {
                var title = h2.Groups[1].Value.Trim('`');
                submodule = title.StartsWith(module + ".", StringComparison.Ordinal) ? title : null;
                owner = SectionWords.Contains(title) || submodule != null ? null : title;
                if (submodule != null)
                    submodules.Add(submodule);
                else if (owner != null && !NextTextLine(lines, i).StartsWith(NotImportableNote, StringComparison.Ordinal))
                    typeSections.Add(owner);
                inFunctions = title == "Functions" || submodule != null;
                table = owner == null && submodule == null && (title == "Constants" || title == "Properties") ? "module" : null;
                continue;
            }
            if (line == "### Constants" && (owner != null || submodule != null))
            {
                table = submodule != null ? "submodule" : "type";
                continue;
            }
            var fn = FunctionHeading.Match(line);
            if (fn.Success && inFunctions)
            {
                var spelling = fn.Groups[1].Value;
                var prefix = submodule ?? module;
                if (spelling.StartsWith(prefix + ".", StringComparison.Ordinal) && !functions.Contains(spelling))
                    functions.Add(spelling);
            }
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                table = null;
                continue;
            }
            if (table != null && line.StartsWith("| `", StringComparison.Ordinal))
            {
                var cells = UnescapedPipe.Split(line.Trim().Trim('|'))
                    .Select(c => c.Trim().Replace("\\|", "|"))
                    .ToArray();
                rows.Add(new FieldRow(
                    table == "submodule" ? submodule! : module,
                    table == "type" ? owner : null,
                    cells[0].Trim('`'),
                    cells[1].Trim('`')));
            }
        }
        return new DocPage(module, typeSections, rows, submodules, functions);
    }

    private static string NextTextLine(string[] lines, int index)
    {
        for (var j = index + 1; j < lines.Length; j++)
            if (lines[j].Trim().Length > 0)
                return lines[j];
        return "";
    }

    private static void EmitImports(DocPage page, Action<string> emit)
    {
        emit($"import {page.Module}");
        foreach (var sub in page.Submodules.Distinct())
            emit($"import {sub}");
        foreach (var type in page.TypeSections.Distinct())
            emit($"from {page.Module} import {type}");
    }

    /// <summary>The program binding every row of <paramref name="page"/>, and the source line of each row.</summary>
    internal static (string Source, Dictionary<int, FieldRow> RowByLine) BindingProgram(DocPage page)
    {
        var sb = new StringBuilder();
        var line = 0;
        void Emit(string text)
        {
            sb.Append(text).Append('\n');
            line++;
        }

        EmitImports(page, Emit);
        Emit("");
        Emit("def main() -> None:");
        var rowByLine = new Dictionary<int, FieldRow>();
        for (var k = 0; k < page.Rows.Count; k++)
        {
            rowByLine[line + 1] = page.Rows[k];
            Emit($"    v{k}: {page.Rows[k].Type} = {page.Rows[k].Reference}");
        }
        Emit("    print(\"bound\")");
        return (sb.ToString(), rowByLine);
    }

    /// <summary>Compiles and runs one page's program: spelling → the first error on its row (absent when it binds).</summary>
    private Dictionary<string, string> Unbound(DocPage page, SList pageErrors)
    {
        var (source, rowByLine) = BindingProgram(page);
        var result = CompileAndExecute(source, fileName: $"doc_{page.Module}.spy", executionTimeoutMs: 60_000);
        var unbound = new Dictionary<string, string>();
        foreach (var d in result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error))
        {
            if (d.Line is int l && rowByLine.TryGetValue(l, out var row))
                unbound.TryAdd(row.Spelling, $"{d.Code}: {d.Message}");
            else
                pageErrors.Add($"{page.Module} line {d.Line}: {d.Code}: {d.Message}");
        }
        if (unbound.Count == 0 && pageErrors.Count == 0)
        {
            if (!result.Success)
                pageErrors.Add($"{page.Module}: {string.Join("; ", result.CompilationErrors)} {result.StandardError}");
            else if (result.StandardOutput.Trim() != "bound")
                pageErrors.Add($"{page.Module}: printed '{result.StandardOutput.Trim()}'");
        }
        return unbound;
    }

    /// <summary>The program binding every documented function of <paramref name="page"/> as a value, and
    /// the source line of each spelling.</summary>
    internal static (string Source, Dictionary<int, string> SpellingByLine) FunctionProgram(DocPage page)
    {
        var sb = new StringBuilder();
        var line = 0;
        void Emit(string text)
        {
            sb.Append(text).Append('\n');
            line++;
        }

        EmitImports(page, Emit);
        Emit("");
        Emit("def main() -> None:");
        var spellingByLine = new Dictionary<int, string>();
        for (var k = 0; k < page.Functions.Count; k++)
        {
            spellingByLine[line + 1] = page.Functions[k];
            Emit($"    f{k} = {page.Functions[k]}");
        }
        Emit("    print(\"bound\")");
        return (sb.ToString(), spellingByLine);
    }

    /// <summary>Compiles one page's function program: spelling → its SPY0203 (absent when it binds). A
    /// function bound as a value is either accepted or SPY0336 (an overload set needs a target type to
    /// pick one — the name IS a function member). Code generation runs only when the semantic pass
    /// reported nothing, so a code-generation or infrastructure error (SPY05xx, SPY09xx) proves every
    /// name on the page bound; it is written to <paramref name="emitFailures"/>, not judged here — it
    /// is the function-as-value lowering's defect, not the page's. Any other error is a page error.</summary>
    private Dictionary<string, string> UnboundFunctions(DocPage page, SList pageErrors, SList? emitFailures = null)
    {
        var (source, spellingByLine) = FunctionProgram(page);
        var result = CompileAndExecute(source, fileName: $"doc_fn_{page.Module}.spy", executionTimeoutMs: 60_000);
        var errors = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).ToList();
        if (errors.Count >= SemanticErrorCap)
            pageErrors.Add($"{page.Module}: {errors.Count} errors reach the semantic cap; a row's SPY0203 could be hidden");
        var unbound = new Dictionary<string, string>();
        foreach (var d in errors)
        {
            if (IsAfterSemanticAnalysis(d.Code))
            {
                var at = d.Line is int el && spellingByLine.TryGetValue(el, out var s) ? s : $"{page.Module} (no row)";
                emitFailures?.Add($"{at}: {d.Code}: {d.Message}");
                continue;
            }
            if (d.Line is int l && spellingByLine.TryGetValue(l, out var spelling))
            {
                if (d.Code == "SPY0203")
                    unbound.TryAdd(spelling, $"{d.Code}: {d.Message}");
                else if (d.Code != "SPY0336")
                    pageErrors.Add($"{spelling}: {d.Code}: {d.Message}");
            }
            else
            {
                pageErrors.Add($"{page.Module} line {d.Line}: {d.Code}: {d.Message}");
            }
        }
        if (errors.Count == 0 && !(result.Success && result.StandardOutput.Trim() == "bound"))
            pageErrors.Add($"{page.Module}: {string.Join("; ", result.CompilationErrors)} {result.StandardError} printed '{result.StandardOutput.Trim()}'");
        return unbound;
    }

    /// <summary>SPY0500–SPY0599 (code generation) and SPY0900–SPY0999 (infrastructure): reported only
    /// after the semantic pass accepted the program.</summary>
    private static bool IsAfterSemanticAnalysis(string code) =>
        code.Length == 7 && int.TryParse(code.AsSpan(3), out var n) && (n is >= 500 and <= 599 || n is >= 900 and <= 999);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(IOPath.GetDirectoryName(typeof(StdlibDocFieldNameBindingTests).Assembly.Location)!);
        while (dir != null && !File.Exists(IOPath.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static System.Collections.Generic.List<DocPage> AllModulePages() =>
        Directory.GetFiles(IOPath.Combine(RepoRoot(), "docs", "stdlib"), "*.md")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => ParsePage(File.ReadAllText(f)))
            .Where(p => p != null)
            .Select(p => p!)
            .ToList();

    private static System.Collections.Generic.List<DocPage> ModulePages() =>
        AllModulePages().Where(p => p.Rows.Count > 0).ToList();

    [Fact]
    public void EveryDocumentedField_BindsAMemberOfItsDocumentedType()
    {
        var pages = ModulePages();
        var pageErrors = new SList();
        var unbound = new Dictionary<string, string>();
        foreach (var page in pages)
            foreach (var (spelling, error) in Unbound(page, pageErrors))
                unbound[spelling] = error;

        var documented = pages.SelectMany(p => p.Rows).Select(r => r.Spelling).ToHashSet();
        var newlyUnbound = unbound.Keys.Where(s => !KnownUnbound.ContainsKey(s)).OrderBy(s => s, StringComparer.Ordinal)
            .Select(s => $"{s} — {unbound[s]}").ToList();
        var stale = KnownUnbound.Keys.Where(s => !unbound.ContainsKey(s)).OrderBy(s => s, StringComparer.Ordinal)
            .Select(s => documented.Contains(s) ? $"{s} now binds — delete its row ({KnownUnbound[s]})" : $"{s} is no longer documented — delete its row")
            .ToList();

        Assert.True(pageErrors.Count == 0, "page programs failed outside a row:\n" + string.Join("\n", pageErrors));
        Assert.True(newlyUnbound.Count == 0, "documented fields that do not bind a member of their documented type:\n" + string.Join("\n", newlyUnbound));
        Assert.True(stale.Count == 0, "stale allowlist rows:\n" + string.Join("\n", stale));
    }

    /// <summary>The parser sees the tables at all: spellings anchored to literals, one per table kind
    /// and per rule branch (kept CONSTANT_CASE, snake-cased, type collision, module property, type constant).</summary>
    [Fact]
    public void ThePagesDocument_TheAnchoredFields()
    {
        var documented = ModulePages().SelectMany(p => p.Rows).Select(r => r.Spelling).ToHashSet();
        string[] anchors =
        {
            "subprocess.PIPE", "threading.TIMEOUT_MAX", "logging.DEBUG", "calendar.MONDAY", "calendar.day_name",
            "sqlite3.Row", "zlib.Z_BEST_SPEED", "zipfile.ZIP_DEFLATED", "http.HTTPStatus.CONTINUE", "datetime.timezone.utc",
        };
        var missing = anchors.Where(a => !documented.Contains(a)).ToArray();
        Assert.True(missing.Length == 0, "anchored fields not documented: " + string.Join(", ", missing));
    }

    /// <summary>Positive control: the row check refuses a name that is no member and a name that is a
    /// different member, and accepts the right one — on a synthetic page, through the same code.</summary>
    [Fact]
    public void TheRowCheck_RefusesAMissingAndAWrongMember()
    {
        const string page = "# logging\n\n```python\nimport logging\n```\n\n## Constants\n\n| Name | Type | Description |\n|------|------|-------------|\n"
            + "| `DEBUG` | `int` |  |\n| `debug` | `int` |  |\n| `nonexistent_level` | `int` |  |\n";
        var parsed = ParsePage(page);
        Assert.NotNull(parsed);
        var pageErrors = new SList();
        var unbound = Unbound(parsed!, pageErrors);

        Assert.Empty(pageErrors);
        Assert.Equal(new[] { "logging.debug", "logging.nonexistent_level" }, unbound.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.StartsWith("SPY0220", unbound["logging.debug"]);
        Assert.StartsWith("SPY0203", unbound["logging.nonexistent_level"]);
    }

    [Fact]
    public void EveryDocumentedModuleFunction_BindsAMemberOfItsModule()
    {
        var pages = AllModulePages().Where(p => p.Functions.Count > 0).ToList();
        var pageErrors = new SList();
        var emitFailures = new SList();
        var unbound = new Dictionary<string, string>();
        foreach (var page in pages)
            foreach (var (spelling, error) in UnboundFunctions(page, pageErrors, emitFailures))
                unbound[spelling] = error;
        Output.WriteLine($"DOC-FN-CENSUS pages={pages.Count} functions={pages.Sum(p => p.Functions.Count)} unbound={unbound.Count} emit-failures={emitFailures.Count}");
        foreach (var failure in emitFailures.Distinct())
            Output.WriteLine($"DOC-FN-EMIT {failure}");

        var documented = pages.SelectMany(p => p.Functions).ToHashSet();
        var newlyUnbound = unbound.Keys.Where(s => !KnownUnboundFunctions.ContainsKey(s)).OrderBy(s => s, StringComparer.Ordinal)
            .Select(s => $"{s} — {unbound[s]}").ToList();
        var stale = KnownUnboundFunctions.Keys.Where(s => !unbound.ContainsKey(s)).OrderBy(s => s, StringComparer.Ordinal)
            .Select(s => documented.Contains(s) ? $"{s} now binds — delete its row ({KnownUnboundFunctions[s]})" : $"{s} is no longer documented — delete its row")
            .ToList();

        Assert.True(pages.Count > 40, $"only {pages.Count} pages document module functions");
        Assert.True(pageErrors.Count == 0, "function programs failed outside a SPY0203 row:\n" + string.Join("\n", pageErrors));
        Assert.True(newlyUnbound.Count == 0, "documented functions that do not bind a member of their module:\n" + string.Join("\n", newlyUnbound));
        Assert.True(stale.Count == 0, "stale allowlist rows:\n" + string.Join("\n", stale));
    }

    /// <summary>The parser sees the headings at all, each section kind, and none of the spellings the
    /// generator wrote before #2272 (anchored to literals, not to the generator's own output).</summary>
    [Fact]
    public void ThePagesDocument_TheAnchoredFunctions()
    {
        var documented = AllModulePages().SelectMany(p => p.Functions).ToHashSet();
        string[] anchors =
        {
            "os.getcwd", "os.path.join", "os.path.exists", "numpy.fft.fft", "numpy.linalg.det", "numpy.random.seed",
            "unittest.captured_output", "json.dumps",
        };
        string[] retired = { "os.join", "os.exists", "numpy.fft", "numpy.det", "numpy.seed", "numpy.at", "unittest.getvalue", "functools.cache_info" };
        Assert.Equal(Array.Empty<string>(), anchors.Where(a => !documented.Contains(a)).ToArray());
        Assert.Equal(Array.Empty<string>(), retired.Where(documented.Contains).ToArray());
    }

    /// <summary>Positive control: the function check refuses a name that is no member and the
    /// pre-#2272 spelling of a submodule function, and accepts a plain function, an overload set and a
    /// submodule function — on a synthetic page, through the same code.</summary>
    [Fact]
    public void TheFunctionCheck_RefusesAMissingFunctionAndTheFlattenedSubmoduleSpelling()
    {
        const string page = "# os\n\n```python\nimport os\n```\n\n## Functions\n\n"
            + "### `os.getcwd() -> str`\n\n### `os.getenv(key: str) -> str | None`\n\n### `os.getenv(key: str, default: str) -> str`\n\n"
            + "### `os.join(a: str, b: str) -> str`\n\n### `os.nonexistent_fn() -> None`\n\n"
            + "## os.path\n\n```python\nimport os.path\n```\n\n### `os.path.join(a: str, b: str) -> str`\n\n### `os.path.exists(path: str) -> bool`\n\n"
            + "## StatResult\n\n### `os.stat_like() -> int`\n";
        var parsed = ParsePage(page);
        Assert.NotNull(parsed);
        Assert.Equal(new[] { "os.getcwd", "os.getenv", "os.join", "os.nonexistent_fn", "os.path.join", "os.path.exists" }, parsed!.Functions);
        Assert.Equal(new[] { "os.path" }, parsed.Submodules);
        var pageErrors = new SList();
        var unbound = UnboundFunctions(parsed, pageErrors);

        Assert.Empty(pageErrors);
        Assert.Equal(new[] { "os.join", "os.nonexistent_fn" }, unbound.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(unbound.Values, v => Assert.StartsWith("SPY0203", v));
    }

    /// <summary>A type section the compiler does not export is not imported; a submodule section is
    /// imported as a module and its Constants rows are written <c>module.sub.NAME</c>.</summary>
    [Fact]
    public void ThePageParser_ImportsSubmodules_AndNeverAnUnexportedType()
    {
        const string page = "# unittest\n\n```python\nimport unittest\n```\n\n"
            + "## CapturedOutput\n\n*Not importable by name.* A value of it is returned by `unittest.captured_output()`.\n\n"
            + "## TestCase\n\nA test case.\n\n"
            + "## unittest.mock\n\n```python\nimport unittest.mock\n```\n\n### Constants\n\n| Name | Type | Description |\n|------|------|-------------|\n| `DEFAULT` | `int` |  |\n";
        var parsed = ParsePage(page);
        Assert.NotNull(parsed);
        Assert.Equal(new[] { "TestCase" }, parsed!.TypeSections);
        Assert.Equal(new[] { "unittest.mock" }, parsed.Submodules);
        Assert.Equal(new[] { "unittest.mock.DEFAULT" }, parsed.Rows.Select(r => r.Spelling));
        var (source, _) = BindingProgram(parsed);
        Assert.Contains("import unittest.mock\n", source);
        Assert.Contains("from unittest import TestCase\n", source);
        Assert.DoesNotContain("CapturedOutput", source);
        Assert.Contains("= unittest.mock.DEFAULT\n", source);
    }
}
