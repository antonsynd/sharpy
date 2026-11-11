using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Sharpy.TestInfrastructure.Oracles;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>G1 axis 3 (plan-638e8b C2): the path state, a closed enum read with <c>Enum.GetValues</c>.</summary>
internal enum PathState
{
    Missing,
    MissingParent,
    ParentIsFile,
    File,
    EmptyDir,
    NonEmptyDir,
}

/// <summary>One cell: a probe over one state per placeholder (the FULL product, no held-valid value).</summary>
internal sealed record Cell(string Id, Probe Probe, PathState[] States)
{
    public string Key => $"{Probe.Id}[{string.Join(",", States)}]";
}

/// <summary>One cell's outcome on each side: <c>ok</c> | <c>OS &lt;name&gt;</c> | <c>X &lt;class&gt;</c>.</summary>
internal sealed record CellOutcome(Cell Cell, string? Python, string? Sharpy);

/// <summary>
/// The G1 differential (plan-638e8b A1): one Sharpy program and one python program per module,
/// generated from (probe row × the product of <see cref="PathState"/> over its placeholders); the
/// harness builds each cell's state on disk, a fresh tree per side so neither side's mutations reach
/// the other; each cell prints its outcome class.
/// </summary>
internal static class PathStateDifferential
{
    /// <summary>The bytes of every regular file the harness creates (valid XML, so a parser's verdict is about the path).</summary>
    public const string FileContent = "<a/>\n";

    public static IReadOnlyList<Cell> Cells(IEnumerable<Probe> probes)
    {
        var cells = new System.Collections.Generic.List<Cell>();
        var states = Enum.GetValues<PathState>();
        foreach (var probe in probes)
        {
            IEnumerable<PathState[]> product = new[] { Array.Empty<PathState>() };
            for (var i = 0; i < probe.Placeholders.Length; i++)
                product = product.SelectMany(prefix => states.Select(s => prefix.Append(s).ToArray()));
            foreach (var combination in product)
                cells.Add(new Cell($"c{cells.Count}", probe, combination));
        }
        return cells;
    }

    /// <summary>Builds every cell's state under <paramref name="root"/> and returns each cell's statement for the side.</summary>
    public static Dictionary<string, string> Materialize(string root, IEnumerable<Cell> cells, bool python)
    {
        var statements = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cell in cells)
        {
            var dir = SysPath.Combine(root, cell.Id);
            Directory.CreateDirectory(dir);
            var template = python ? cell.Probe.Python : cell.Probe.Sharpy;
            if (template.Contains("@aux", StringComparison.Ordinal))
            {
                var aux = SysPath.Combine(dir, "aux");
                Directory.CreateDirectory(aux);
                WriteArchives(aux);
                template = template.Replace("@auxtar", Literal(SysPath.Combine(aux, "a.tar")), StringComparison.Ordinal)
                    .Replace("@auxzip", Literal(SysPath.Combine(aux, "a.zip")), StringComparison.Ordinal)
                    .Replace("@auxout", Literal(SysPath.Combine(aux, "out.bin")), StringComparison.Ordinal);
            }
            for (var i = cell.States.Length - 1; i >= 0; i--)
                template = template.Replace("@" + i, Literal(Build(dir, i, cell.States[i])), StringComparison.Ordinal);
            statements[cell.Id] = template;
        }
        return statements;
    }

    /// <summary>The path of placeholder <paramref name="i"/> in <paramref name="state"/>, created on disk.</summary>
    public static string Build(string cellDir, int i, PathState state)
    {
        var p = SysPath.Combine(cellDir, "p" + i);
        switch (state)
        {
            case PathState.Missing:
                return p;
            case PathState.MissingParent:
                return SysPath.Combine(cellDir, "missing" + i, "p" + i);
            case PathState.ParentIsFile:
                var parent = SysPath.Combine(cellDir, "file" + i);
                File.WriteAllText(parent, FileContent);
                return SysPath.Combine(parent, "p" + i);
            case PathState.File:
                File.WriteAllText(p, FileContent);
                return p;
            case PathState.EmptyDir:
                Directory.CreateDirectory(p);
                return p;
            case PathState.NonEmptyDir:
                Directory.CreateDirectory(p);
                File.WriteAllText(SysPath.Combine(p, "child.txt"), FileContent);
                return p;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "a PathState with no on-disk construction");
        }
    }

    private static void WriteArchives(string aux)
    {
        var member = SysPath.Combine(aux, "m.txt");
        File.WriteAllText(member, FileContent);
        using (var tar = File.Create(SysPath.Combine(aux, "a.tar")))
        using (var writer = new TarWriter(tar, TarEntryFormat.Pax))
            writer.WriteEntry(member, "m.txt");
        using (var zip = ZipFile.Open(SysPath.Combine(aux, "a.zip"), ZipArchiveMode.Create))
            zip.CreateEntryFromFile(member, "m.txt");
        File.Delete(member);
    }

    /// <summary>A double-quoted string literal both languages read the same way.</summary>
    public static string Literal(string s) => "\"" + s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    public static string SharpyProgram(string module, IReadOnlyList<Cell> cells, IReadOnlyDictionary<string, string> statements)
    {
        var sb = new StringBuilder();
        sb.Append("# GENERATED by PathStateDifferential (plan-638e8b G1) — module ").Append(module).Append('\n');
        sb.Append(ProbeRoster.Headers[module].Sharpy).Append("\n\n");
        foreach (var cell in cells)
        {
            sb.Append("def ").Append(cell.Id).Append("() -> None:\n");
            sb.Append("    try:\n");
            sb.Append("        ").Append(statements[cell.Id].Replace("\n", "\n        ", StringComparison.Ordinal)).Append('\n');
            sb.Append("        print(\"").Append(cell.Id).Append(" ok\")\n");
            sb.Append("    except OSError as e:\n");
            sb.Append("        print(\"").Append(cell.Id).Append(" OS \" + type(e).__name__)\n");
            sb.Append("    except Exception as e:\n");
            sb.Append("        print(\"").Append(cell.Id).Append(" X \" + str(type(e)))\n\n");
        }
        sb.Append("def main():\n");
        foreach (var cell in cells)
            sb.Append("    ").Append(cell.Id).Append("()\n");
        return sb.ToString();
    }

    public static string PythonProgram(string module, IReadOnlyList<Cell> cells, IReadOnlyDictionary<string, string> statements)
    {
        var sb = new StringBuilder();
        sb.Append("# GENERATED by PathStateDifferential (plan-638e8b G1) — module ").Append(module).Append('\n');
        sb.Append("import warnings\nwarnings.simplefilter('ignore')\n");
        sb.Append(ProbeRoster.Headers[module].Python).Append("\n\n");
        sb.Append("""
            def _report(cid, f):
                try:
                    f()
                    print(cid, "ok", flush=True)
                except OSError as e:
                    print(cid, "OS", type(e).__name__, flush=True)
                except Exception as e:
                    t = type(e)
                    name = t.__qualname__ if t.__module__ == "builtins" else t.__module__ + "." + t.__qualname__
                    malformed = isinstance(e, (SyntaxError, NameError, AttributeError, ImportError, TypeError))
                    print(cid, "X", name + (" MALFORMED" if malformed else ""), flush=True)


            """);
        foreach (var cell in cells)
        {
            sb.Append("def ").Append(cell.Id).Append("():\n");
            sb.Append("    ").Append(statements[cell.Id].Replace("\n", "\n    ", StringComparison.Ordinal)).Append("\n\n");
            sb.Append("_report(\"").Append(cell.Id).Append("\", ").Append(cell.Id).Append(")\n\n");
        }
        return sb.ToString();
    }

    /// <summary>Parses <c>&lt;id&gt; &lt;outcome&gt;</c> lines; a repeated id is kept as the first.</summary>
    public static Dictionary<string, string> ParseOutcomes(IEnumerable<string> lines)
    {
        var outcomes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            if (space <= 0 || line[0] != 'c')
                continue;
            outcomes.TryAdd(line[..space], line[(space + 1)..].TrimEnd());
        }
        return outcomes;
    }

    /// <summary>The python twin of one module, run once by the located oracle.</summary>
    public static IReadOnlyList<string> RunPython(PythonInterpreter python, string scriptPath)
        => python.RunBatch(scriptPath, Array.Empty<string>(), timeoutMilliseconds: 300_000);
}
