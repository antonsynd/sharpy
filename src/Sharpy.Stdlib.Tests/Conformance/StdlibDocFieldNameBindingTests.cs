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
/// <para>Allowlisted rows are the generator's OTHER mechanism, ownership: an un-annotated public
/// class's members render at module level although the compiler's module surface is the
/// <c>[SharpyModule]</c> class only (<c>numpy.start</c> is <c>SliceSpec.start</c>). Rows drain on fix;
/// a stale row fails. Not covered: function and method headings (the Method rule) and type-level
/// Properties tables (static and instance members are not told apart on the page).</para>
/// </summary>
public class StdlibDocFieldNameBindingTests : StdlibIntegrationTestBase
{
    public StdlibDocFieldNameBindingTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>Documented spelling → tracking issue. Each drains when the row binds.</summary>
    private static readonly Dictionary<string, string> KnownUnbound = new()
    {
        // Instance and static members of the un-annotated SliceSpec rendered as module members.
        ["numpy.start"] = "#2264",
        ["numpy.stop"] = "#2264",
        ["numpy.step"] = "#2264",
        ["numpy.is_squeeze"] = "#2264",
        ["numpy.all"] = "#2264",
        // TmpPathFixture.Value (an un-annotated fixture class) rendered as a module member.
        ["unittest.value"] = "#2264",
    };

    /// <summary>One documented field: the spelling a reader copies, its Type column, its owner.</summary>
    internal sealed record FieldRow(string Module, string? Owner, string Name, string Type)
    {
        public string Spelling => Owner == null ? $"{Module}.{Name}" : $"{Module}.{Owner}.{Name}";
        public string Reference => Owner == null ? $"{Module}.{Name}" : $"{Owner}.{Name}";
    }

    internal sealed record DocPage(string Module, IReadOnlyList<string> TypeSections, IReadOnlyList<FieldRow> Rows);

    private static readonly Regex Heading2 = new(@"^## (.+)$");
    private static readonly Regex UnescapedPipe = new(@"(?<!\\)\|");
    private static readonly HashSet<string> SectionWords = new() { "Constants", "Properties", "Functions", "Methods" };

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
        var rows = new System.Collections.Generic.List<FieldRow>();
        string? owner = null;
        string? table = null;
        foreach (var line in lines)
        {
            var h2 = Heading2.Match(line);
            if (h2.Success)
            {
                var title = h2.Groups[1].Value.Trim('`');
                owner = SectionWords.Contains(title) ? null : title;
                if (owner != null)
                    typeSections.Add(owner);
                table = owner == null && (title == "Constants" || title == "Properties") ? "module" : null;
                continue;
            }
            if (line == "### Constants" && owner != null)
            {
                table = "type";
                continue;
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
                rows.Add(new FieldRow(module, table == "type" ? owner : null, cells[0].Trim('`'), cells[1].Trim('`')));
            }
        }
        return new DocPage(module, typeSections, rows);
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

        Emit($"import {page.Module}");
        foreach (var type in page.TypeSections.Distinct())
            Emit($"from {page.Module} import {type}");
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

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(IOPath.GetDirectoryName(typeof(StdlibDocFieldNameBindingTests).Assembly.Location)!);
        while (dir != null && !File.Exists(IOPath.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static System.Collections.Generic.List<DocPage> ModulePages() =>
        Directory.GetFiles(IOPath.Combine(RepoRoot(), "docs", "stdlib"), "*.md")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => ParsePage(File.ReadAllText(f)))
            .Where(p => p != null && p.Rows.Count > 0)
            .Select(p => p!)
            .ToList();

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
}
