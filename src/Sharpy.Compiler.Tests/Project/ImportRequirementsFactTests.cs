using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// plan-775c50 D5 (P77 T5, lead's ruling (a)): every <c>import</c> and <c>from … import</c> statement
/// records the Sharpy modules it makes Python run, in Python's order, each once, with its layout and
/// <c>HasSweep</c> (<see cref="SemanticInfo.GetImportRequirements"/>).
///
/// <para><b>Python's order</b> (CPython 3.15.2, measured on a package tree whose modules print when run):
/// <c>import a.b.c</c> → a, a.b, a.b.c; <c>import a.b as x</c> → a, a.b; <c>import a.b.c, a.b.d</c> →
/// a, a.b, a.b.c, a.b.d; <c>import a.b.c, a.b</c> → a, a.b, a.b.c; <c>from a import b</c> → a, a.b;
/// <c>from a.b import c</c> → a, a.b, a.b.c; <c>from a.b import y</c> (an attribute) → a, a.b;
/// <c>from a.b import *</c> → a, a.b; <c>from a import x</c> → a; <c>import n.m.k</c> and
/// <c>from n.m import k</c> with namespace packages n, n.m → n.m.k only.</para>
///
/// <para><b>Generator.</b> <see cref="Forms"/> × <see cref="Depths"/> (the module a.b.c's chain cut at
/// depth 1–3: <c>m</c>, <c>a.b</c>, <c>a.b.c</c>) × <see cref="NetPlacements"/> (a .NET module named in
/// the same statement, or not), each triple a cell or an exclusion with its reason, plus the rows no
/// triple spells (<see cref="ExtraCells"/>: a .NET-only statement, namespace-only ancestors, a repeated
/// ancestor, an import from inside the package, a package importing its own submodule). Each cell is
/// one file holding one statement; its expected list is module names, read as the layout each module
/// records of itself, and a literal <c>HasSweep</c>.</para>
/// <para><b>Controls.</b> A non-import statement in every cell file has no record (negative), beside
/// the import that has one; the submodule-by-name cells draw SPY0301 today (a from-import names no
/// submodule in Sharpy: findings ledger) and are checked in their own project, with the diagnostic as
/// their positive control. <b>Views:</b> a cold build's project-merged and per-file SemanticInfo, and
/// a warm build's merged one, where every package module is served from the cache.</para>
/// </summary>
public class ImportRequirementsFactTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public ImportRequirementsFactTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_import_requirements_{Guid.NewGuid():N}");
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

    // ------------------------------------------------------------------------------------------- //
    // The tree
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The package tree: module name → (file, source, HasSweep). A module with a Guarded value has
    /// a sweep; m and a.b.d have none. n and n.m are namespace-only (no <c>__init__.spy</c>).</summary>
    internal static readonly IReadOnlyDictionary<string, (string File, string Source, bool HasSweep)> Tree =
        new Dictionary<string, (string, string, bool)>
        {
            ["m"] = ("m.spy", "def fm() -> int:\n    return 1\n", false),
            ["a"] = ("a/__init__.spy", "x: int = 1\n", true),
            ["a.b"] = ("a/b/__init__.spy", "y: int = 2\n", true),
            ["a.b.c"] = ("a/b/c.spy", "cv: int = 3\n\ndef fc() -> int:\n    return 3\n", true),
            ["a.b.d"] = ("a/b/d.spy", "def fd() -> int:\n    return 4\n", false),
            ["n.m.k"] = ("n/m/k.spy", "kv: int = 4\n\ndef fk() -> int:\n    return 4\n", true),
            ["p.q"] = ("p/q.spy", "qv: int = 5\n", true),
        };

    // ------------------------------------------------------------------------------------------- //
    // The generator
    // ------------------------------------------------------------------------------------------- //

    public enum Form
    {
        Import,
        ImportAs,

        /// <summary><c>from t import name</c>, a name of t that is no submodule.</summary>
        FromName,

        /// <summary><c>from t import *</c>: the module, no submodule.</summary>
        FromStar,

        /// <summary><c>from parent(t) import name</c>, a name of the ancestor that is no submodule.</summary>
        FromAncestor,

        /// <summary><c>from parent(t) import last(t)</c>: a submodule imported by name.</summary>
        FromSubmodule,
    }

    public enum NetPlacement
    {
        None,

        /// <summary>A .NET module named first in the same statement.</summary>
        InStatement,
    }

    internal static readonly Form[] Forms = Enum.GetValues<Form>();
    internal static readonly int[] Depths = { 1, 2, 3 };
    internal static readonly NetPlacement[] NetPlacements = Enum.GetValues<NetPlacement>();

    private static string Target(int depth) => depth switch { 1 => "m", 2 => "a.b", 3 => "a.b.c", _ => throw new ArgumentOutOfRangeException(nameof(depth)) };

    /// <summary>A name each module exports that is no submodule.</summary>
    private static string NameIn(string module) => module switch { "m" => "fm", "a" => "x", "a.b" => "y", "a.b.c" => "fc", _ => throw new ArgumentOutOfRangeException(nameof(module)) };

    private static string Parent(string module) => module[..module.LastIndexOf('.')];

    /// <summary>The modules of a dotted name's chain that have a Sharpy body, outermost first.</summary>
    private static IEnumerable<string> Chain(string module)
    {
        var parts = module.Split('.');
        for (var depth = 1; depth <= parts.Length; depth++)
        {
            var name = string.Join(".", parts.Take(depth));
            if (Tree.ContainsKey(name))
                yield return name;
        }
    }

    internal static string? Excluded(Form form, int depth, NetPlacement net)
    {
        if (depth == 1 && form is Form.FromAncestor or Form.FromSubmodule)
            return "a top-level module has no package to import a name from";
        if (net == NetPlacement.InStatement && form is not (Form.Import or Form.ImportAs))
            return "a from-import names one module: its .NET spelling is the NetFrom row";
        return null;
    }

    internal sealed record Cell(string Id, string File, string Statement, IReadOnlyList<string> Expected, bool DrawsSpy0301 = false);

    internal static IReadOnlyList<Cell> GeneratedCells()
    {
        var cells = new List<Cell>();
        foreach (var form in Forms)
        {
            foreach (var depth in Depths)
            {
                foreach (var net in NetPlacements)
                {
                    if (Excluded(form, depth, net) != null)
                        continue;
                    var t = Target(depth);
                    var netAlias = net == NetPlacement.InStatement ? (form == Form.ImportAs ? "system.io as sio, " : "system.io, ") : "";
                    var (statement, expected, refused) = form switch
                    {
                        Form.Import => ($"import {netAlias}{t}", Chain(t), false),
                        Form.ImportAs => ($"import {netAlias}{t} as z", Chain(t), false),
                        Form.FromName => ($"from {t} import {NameIn(t)}", Chain(t), false),
                        Form.FromStar => ($"from {t} import *", Chain(t), false),
                        Form.FromAncestor => ($"from {Parent(t)} import {NameIn(Parent(t))}", Chain(Parent(t)), false),
                        Form.FromSubmodule => ($"from {Parent(t)} import {t[(t.LastIndexOf('.') + 1)..]}", Chain(t), true),
                        _ => throw new ArgumentOutOfRangeException(nameof(form)),
                    };
                    var id = $"{form}/d{depth}/{net}";
                    cells.Add(new Cell(id, $"c_{form}_{depth}_{net}.spy".ToLowerInvariant(), statement, expected.ToList(), refused));
                }
            }
        }
        return cells;
    }

    /// <summary>The rows no (form, depth, placement) triple spells.</summary>
    internal static readonly Cell[] ExtraCells =
    {
        new("NetOnly", "x_net_only.spy", "import system.io", Array.Empty<string>()),
        new("NetFrom", "x_net_from.spy", "from System.IO import StringWriter", Array.Empty<string>()),
        new("NamespaceOnly", "x_ns_only.spy", "import n.m.k", new[] { "n.m.k" }),
        new("NamespaceOnlyAs", "x_ns_only_as.spy", "import n.m.k as nmk", new[] { "n.m.k" }),
        new("NamespaceOnlyFrom", "x_ns_only_from.spy", "from n.m.k import fk", new[] { "n.m.k" }),
        new("TwoChains", "x_two_chains.spy", "import a.b.c, a.b.d", new[] { "a", "a.b", "a.b.c", "a.b.d" }),
        new("RepeatedAncestor", "x_repeated.spy", "import a.b.c, a.b", new[] { "a", "a.b", "a.b.c" }),
        new("TwoModules", "x_two_modules.spy", "import m, a.b", new[] { "m", "a", "a.b" }),
        new("FromInsidePackage", "a/b/e.spy", "import a.b.c", new[] { "a", "a.b", "a.b.c" }),
        new("OwnSubmodule", "p/__init__.spy", "import p.q", new[] { "p.q" }),
    };

    internal static IReadOnlyList<Cell> AllCells() => GeneratedCells().Concat(ExtraCells).ToList();

    // ------------------------------------------------------------------------------------------- //
    // Harness
    // ------------------------------------------------------------------------------------------- //

    private static string CellSource(Cell cell, string touch) =>
        $"# {touch}\n{cell.Statement}\n\ndef cell_probe() -> int:\n    return 0\n";

    private string Write(string dir, string relative, string content)
    {
        var path = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private (string Dir, ProjectConfig Config) WriteProject(string name, IEnumerable<Cell> cells, string touch,
        IReadOnlyList<string>? references = null)
    {
        var dir = Path.Combine(_tempDir, name);
        var files = Tree.Values.Select(m => Write(dir, m.File, m.Source)).ToList();
        foreach (var cell in cells)
            files.Add(Write(dir, cell.File, CellSource(cell, touch)));
        files.Add(Write(dir, "main.spy", "def main() -> None:\n    pass\n"));
        return (dir, new ProjectConfig
        {
            ProjectFilePath = Path.Combine(dir, "reqs.spyproj"),
            ProjectDirectory = dir,
            RootNamespace = "Reqs",
            SourceFiles = files,
            Configuration = "Debug",
            References = references?.ToList() ?? new List<string>(),
        });
    }

    private static ProjectCompilationResult Build(ProjectConfig config)
        => new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance).CompileProject(config);

    /// <summary>A build on <c>sharpyc project</c>'s reference route (the stdlib and Core from
    /// <see cref="DefaultReferences"/>, beside the project's own references), incremental.</summary>
    private static ProjectCompilationResult BuildWithReferences(ProjectConfig config)
    {
        var defaults = DefaultReferences.Resolve();
        DefaultReferences.ApplyTo(config, defaults);
        var options = CompilerOptionsFactory.ForProject(config, defaultReferences: defaults, incremental: true);
        return new Compiler(options, NullLogger.Instance).CompileProject(config);
    }

    private static string Diagnostics(ProjectCompilationResult result)
        => string.Join("\n", result.Diagnostics.GetAll().Where(d => d.IsError).Select(d => $"{d.Code} {d.FilePath}:{d.Line}: {d.Message}"));

    private static CompilationUnit Unit(ProjectModel model, string dir, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar)));
        return model.Units.Values.Single(u => Path.GetFullPath(u.FilePath) == path);
    }

    /// <summary>Each tree module's own layout, as its declaring file records it on its Module root.</summary>
    private static Dictionary<string, string> OwnLayouts(ProjectModel model, string dir)
        => Tree.ToDictionary(kv => kv.Key, kv =>
        {
            var unit = Unit(model, dir, kv.Value.File);
            var layout = model.SemanticInfo!.GetModuleLayout(unit.Ast!) ?? throw new InvalidOperationException($"{kv.Key}: no own layout");
            return $"{string.Join(".", layout.NamespaceSegments)}/{layout.MembersClassName}";
        });

    /// <summary>A layout as the cells spell it; a by-reference module's (D10) leads with its CLR members class,
    /// and any namespace segment it carried would show after an <c>@</c> (it carries none).</summary>
    private static string Render(ModuleLayout layout) => layout.ClrMembersClass is { } clr
        ? $"{ClrPrefix}{clr}{(layout.NamespaceSegments.Count == 0 ? "" : "@" + string.Join(".", layout.NamespaceSegments))}/{layout.MembersClassName} sweep={layout.HasSweep}"
        : $"{string.Join(".", layout.NamespaceSegments)}/{layout.MembersClassName} sweep={layout.HasSweep}";

    private const string ClrPrefix = "clr:";

    /// <summary>A cell's expected list: a tree module's name reads its own layout; a <c>clr:</c> entry is a
    /// by-reference module's literal rendering.</summary>
    private static string Expected(Cell cell, IReadOnlyDictionary<string, string> own)
        => string.Join(" | ", cell.Expected.Select(m => m.StartsWith(ClrPrefix, StringComparison.Ordinal) ? m : $"{own[m]} sweep={Tree[m].HasSweep}"));

    private static IEnumerable<string> CellFailures(string view, ProjectModel model, string dir, IEnumerable<Cell> cells,
        IReadOnlyDictionary<string, string> own, Func<CompilationUnit, SemanticInfo?> infoOf)
    {
        foreach (var cell in cells)
        {
            var unit = Unit(model, dir, cell.File);
            var info = infoOf(unit);
            if (unit.Ast == null || info == null)
            {
                yield return $"{view} {cell.Id}: the cell file has no AST or SemanticInfo (was it served?)";
                continue;
            }
            var statements = unit.Ast.Body.Select(s => s.UnwrapDecorated()).ToList();
            var import = statements.Single(s => s is ImportStatement or FromImportStatement);
            var recorded = info.GetImportRequirements(import);
            var actual = recorded == null ? "<no record>" : string.Join(" | ", recorded.Select(Render));
            var expected = Expected(cell, own);
            if (actual != expected)
                yield return $"{view} {cell.Id} `{cell.Statement}`: expected [{expected}], got [{actual}]";

            // Negative control: the non-import statement beside it records nothing.
            var probe = statements.OfType<FunctionDef>().Single();
            if (info.GetImportRequirements(probe) != null)
                yield return $"{view} {cell.Id}: the non-import statement has a record";
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // Facts
    // ------------------------------------------------------------------------------------------- //

    [Fact]
    public void EveryImportStatement_RecordsTheModulesPythonRuns_ColdMergedPerFileAndWarm()
    {
        var cells = AllCells().Where(c => !c.DrawsSpy0301).ToList();
        var (dir, config) = WriteProject("run", cells, "build 1");

        var cold = Build(config);
        cold.Success.Should().BeTrue("the specimen must compile. Diagnostics:\n" + Diagnostics(cold));
        var coldModel = cold.ProjectModel!;
        var own = OwnLayouts(coldModel, dir);

        var failures = new List<string>();
        failures.AddRange(CellFailures("cold-merged", coldModel, dir, cells, own, _ => coldModel.SemanticInfo));
        failures.AddRange(CellFailures("cold-file", coldModel, dir, cells, own, u => u.FileSemanticInfo));
        coldModel.Units.Values.Select(u => u.FileSemanticInfo).Should().NotContain(
            i => ReferenceEquals(i, coldModel.SemanticInfo), "the per-file view is not the merged one");

        // Warm: every cell file changes, so every importer is checked again while each tree module is served
        // from the cache, and its layout and HasSweep reach the importer through the imported facts.
        foreach (var cell in cells)
            Write(dir, cell.File, CellSource(cell, "build 2"));
        var warm = Build(config);
        warm.Success.Should().BeTrue("the warm build must compile. Diagnostics:\n" + Diagnostics(warm));
        var served = warm.Metrics?.SkippedFiles.Select(Path.GetFullPath).ToHashSet() ?? new HashSet<string>();
        var treeFiles = Tree.Values.Select(m => Path.GetFullPath(Path.Combine(dir, m.File.Replace('/', Path.DirectorySeparatorChar))))
            .Where(f => !cells.Any(c => Path.GetFullPath(Path.Combine(dir, c.File.Replace('/', Path.DirectorySeparatorChar))) == f));
        served.Should().Contain(treeFiles, "the positive control of the warm view: every tree module is served");
        failures.AddRange(CellFailures("warm-merged", warm.ProjectModel!, dir, cells, own, _ => warm.ProjectModel!.SemanticInfo));

        _output.WriteLine($"{cells.Count} cells x 3 views");
        failures.Should().BeEmpty("each import statement records the Sharpy modules Python runs, in order, once");
    }

    [Fact]
    public void SubmoduleByName_IsRequired_WhereSharpyRefusesTheImportToday()
    {
        var cells = AllCells().Where(c => c.DrawsSpy0301).ToList();
        cells.Should().NotBeEmpty();
        var (dir, config) = WriteProject("refused", cells, "build 1");

        var result = new Compiler(new CompilerOptions(), NullLogger.Instance).CompileProject(config);
        var errors = result.Diagnostics.GetAll().Where(d => d.IsError).ToList();
        errors.Select(d => d.Code).Distinct().Should().Equal(new[] { "SPY0301" },
            "positive control: a from-import names no submodule in Sharpy today (findings ledger)");

        var model = result.ProjectModel!;
        var own = OwnLayouts(model, dir);
        CellFailures("refused-merged", model, dir, cells, own, _ => model.SemanticInfo)
            .Should().BeEmpty("python runs the submodule a from-import names, after its package");
    }

    // ------------------------------------------------------------------------------------------- //
    // By reference (S4): a Sharpy module another assembly holds
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The regenerated stdlib's entries, literal (each is the <c>[SharpyLoadEntry(hasSweep)]</c> its
    /// <c>__Load</c> carries in <c>src/Sharpy.Stdlib</c>): os and os.path are two modules, two entries.</summary>
    private const string MathRef = ClrPrefix + "Sharpy.MathModule.MathModuleModule/MathModuleModule sweep=True";
    private const string OsRef = ClrPrefix + "Sharpy.OsModule.OsModuleModule/OsModuleModule sweep=True";
    private const string OsPathRef = ClrPrefix + "Sharpy.OsPathModule.OsPathModuleModule/OsPathModuleModule sweep=False";

    /// <summary>The referenced Sharpy library's modules: <c>libsw</c> has a Guarded value (a sweep),
    /// <c>libplain</c> none. Its members classes are spelled by the library build's own layout rule.</summary>
    private static readonly (string File, string Source)[] LibraryModules =
    {
        ("libsw.spy", "lv: int = 5\n\ndef lsw() -> int:\n    return lv\n"),
        ("libplain.spy", "def lf() -> int:\n    return 1\n"),
    };

    /// <summary>By-reference rows (Python's order as the tree cells measure it: a package before its
    /// submodule, statement order between names, each module once). A module whose class declares no
    /// entry (a hand-written C# <c>[SharpyModule]</c>, a .NET namespace) adds nothing: its statement's
    /// record is the empty list, as <c>NetOnly</c>'s is.</summary>
    internal static Cell[] ByReferenceCells(string libSw, string libPlain) => new Cell[]
    {
        new("RefImport", "r_import.spy", "import math", new[] { MathRef }),
        new("RefImportAs", "r_import_as.spy", "import math as mm", new[] { MathRef }),
        new("RefFromName", "r_from_name.spy", "from math import pi", new[] { MathRef }),
        new("RefFromStar", "r_from_star.spy", "from math import *", new[] { MathRef }),
        new("RefRepeated", "r_repeated.spy", "import math, math as mm", new[] { MathRef }),
        new("RefPackage", "r_package.spy", "import os.path", new[] { OsRef, OsPathRef }),
        new("RefFromPackageModule", "r_from_pkg.spy", "from os.path import join", new[] { OsRef, OsPathRef }),
        new("RefThenSource", "r_then_source.spy", "import math, a.b", new[] { MathRef, "a", "a.b" }),
        new("SourceThenRef", "r_source_then.spy", "import a.b, math", new[] { "a", "a.b", MathRef }),
        new("RefSourceRef", "r_src_ref.spy", "import os.path, m, math", new[] { OsRef, OsPathRef, "m", MathRef }),
        new("LibSweep", "r_lib_sw.spy", "import libsw", new[] { libSw }),
        new("LibPlainFrom", "r_lib_plain.spy", "from libplain import lf", new[] { libPlain }),
        new("LibThenSource", "r_lib_then.spy", "import libsw, a.b.c", new[] { libSw, "a", "a.b", "a.b.c" }),
        new("NoEntry", "r_no_entry.spy", "import handmod", Array.Empty<string>()),
        new("NoEntryFrom", "r_no_entry_from.spy", "from handmod import hf", Array.Empty<string>()),
        new("NoEntryThenRef", "r_no_entry_ref.spy", "import handmod, math", new[] { MathRef }),
    };

    /// <summary>A C# assembly declaring <c>[SharpyModule("handmod")]</c> by hand, with no load entry.</summary>
    private string BuildHandWrittenModuleAssembly()
    {
        var name = $"HandMod_{Guid.NewGuid():N}";
        var path = Path.Combine(_tempDir, name + ".dll");
        var compilation = CSharpCompilation.Create(
            name,
            new[] { CSharpSyntaxTree.ParseText(
                "namespace HandWritten { [global::Sharpy.SharpyModule(\"handmod\")] public static class HandModule { public static int Hf() => 7; } }") },
            IntegrationTestBase.GetSharedReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(path);
        emit.Success.Should().BeTrue(string.Join("\n", emit.Diagnostics));
        return path;
    }

    [Fact]
    public void ByReferenceModules_AreRequiredInPythonsOrder_ColdMergedPerFileAndWarm()
    {
        // The Sharpy library, built by the compiler under test: its modules' load entries are its own.
        using var lib = new ProjectCompilationHelper(_output);
        lib.WithRootNamespace("ReqLib").WithOutputType("library");
        lib.Options.AssemblyName = $"ReqLib_{Guid.NewGuid():N}";
        foreach (var (file, source) in LibraryModules)
            lib.AddSourceFile(file, source);
        lib.CreateProjectFile();
        var libResult = lib.Compile();
        lib.AssertCompilationSucceeded(libResult);
        var libModel = libResult.ProjectModel!;
        string LibRef(string file, bool hasSweep)
        {
            var unit = libModel.Units.Values.Single(u => Path.GetFileName(u.FilePath) == file);
            var layout = libModel.SemanticInfo!.GetModuleLayout(unit.Ast!)!;
            var fullName = layout.MembersClassFullName("ReqLib");
            return $"{ClrPrefix}{fullName}/{layout.MembersClassName} sweep={hasSweep}";
        }

        var cells = ByReferenceCells(LibRef("libsw.spy", true), LibRef("libplain.spy", false));
        var references = new[] { libResult.OutputAssemblyPath!, BuildHandWrittenModuleAssembly() };
        var (dir, config) = WriteProject("byref", cells, "build 1", references);

        var cold = BuildWithReferences(config);
        cold.Success.Should().BeTrue("the specimen must compile (positive control: every by-reference import resolves). Diagnostics:\n" + Diagnostics(cold));
        var coldModel = cold.ProjectModel!;
        var own = OwnLayouts(coldModel, dir);

        var failures = new List<string>();
        failures.AddRange(CellFailures("cold-merged", coldModel, dir, cells, own, _ => coldModel.SemanticInfo));
        failures.AddRange(CellFailures("cold-file", coldModel, dir, cells, own, u => u.FileSemanticInfo));

        foreach (var cell in cells)
            Write(dir, cell.File, CellSource(cell, "build 2"));
        var warm = BuildWithReferences(config);
        warm.Success.Should().BeTrue("the warm build must compile. Diagnostics:\n" + Diagnostics(warm));
        var served = warm.Metrics?.SkippedFiles.Select(Path.GetFullPath).ToHashSet() ?? new HashSet<string>();
        served.Should().Contain(Path.GetFullPath(Path.Combine(dir, Tree["a"].File.Replace('/', Path.DirectorySeparatorChar))),
            "the positive control of the warm view: a source module of the mixed chains is served");
        failures.AddRange(CellFailures("warm-merged", warm.ProjectModel!, dir, cells, own, _ => warm.ProjectModel!.SemanticInfo));

        _output.WriteLine($"{cells.Length} by-reference cells x 3 views");
        failures.Should().BeEmpty("a by-reference Sharpy module is required in Python's order, by its discovered load entry");
    }

    [Fact]
    public void Generator_IsTotal_OverFormsDepthsAndPlacements()
    {
        var generated = GeneratedCells();
        (Forms.Length * Depths.Length * NetPlacements.Length).Should().Be(generated.Count
            + (from f in Forms from d in Depths from n in NetPlacements where Excluded(f, d, n) != null select 1).Count());
        Forms.Should().OnlyContain(f => generated.Any(c => c.Id.StartsWith(f + "/", StringComparison.Ordinal)), "each form has a cell");
        Depths.Should().OnlyContain(d => generated.Any(c => c.Id.Contains($"/d{d}/", StringComparison.Ordinal)), "each depth has a cell");
        AllCells().Select(c => c.File).Should().OnlyHaveUniqueItems();
        AllCells().SelectMany(c => c.Expected).Distinct().Should().BeEquivalentTo(Tree.Keys, "every tree module is required by some cell");
    }
}
