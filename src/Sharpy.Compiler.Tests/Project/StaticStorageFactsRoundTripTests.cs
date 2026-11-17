using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// plan-775c50 A2 (D2): every value kind × host declaration carries its <see cref="StaticStorage"/> and
/// generic-host flag after a cold build and after a warm (incremental-cache) round trip, and every
/// importer reads its imported module's <c>HasSweep</c> (transitive, the conservative arm for a deferred
/// cycle). The cells are generated: <see cref="Kinds"/> × <see cref="Hosts"/>, each pair either a cell
/// with its expected facts or an excluded pair with its reason (<see cref="ExcludedPairs"/>), and a
/// totality check holds the product to that split.
///
/// <para>The cold build is checked against the rule (<see cref="ExpectedStorage"/>); the warm build
/// touches only <c>main.spy</c>, so <c>lib.spy</c> is served from the cache and every fact read for it
/// came through <c>SymbolSerializer</c> (M2: drop either field from the serializer → the warm cells red).</para>
/// </summary>
public class StaticStorageFactsRoundTripTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public StaticStorageFactsRoundTripTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_static_storage_{Guid.NewGuid():N}");
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
    // The generator
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The value kinds of D2, and the two with no initializer (Q4: Plain).</summary>
    public enum Kind
    {
        ModuleVariable,
        ComputedConst,
        LiteralConst,
        PropertyDefault,
        PropertyNoDefault,
        StaticField,
        NoInitializer,
    }

    /// <summary>The hosts a value is declared in: the module, a class, a struct, a generic class and
    /// struct, a class nested in a class and in a generic class (the generic-host flag's enclosing arm).</summary>
    public enum Host
    {
        Module,
        Class,
        Struct,
        GenericClass,
        GenericStruct,
        NestedInClass,
        NestedInGenericClass,
    }

    internal static readonly Kind[] Kinds = Enum.GetValues<Kind>();
    internal static readonly Host[] Hosts = Enum.GetValues<Host>();

    /// <summary>The pairs with no cell, each with its reason.</summary>
    internal static readonly IReadOnlyDictionary<(Kind, Host), string> ExcludedPairs = BuildExclusions();

    private static Dictionary<(Kind, Host), string> BuildExclusions()
    {
        var excluded = new Dictionary<(Kind, Host), string>
        {
            [(Kind.StaticField, Host.Module)] = "a module has no @static field: its value is a module variable (Kind.ModuleVariable)",
        };
        foreach (var host in Hosts.Where(h => h != Host.Module))
            excluded[(Kind.ModuleVariable, host)] = "a type's value is a @static field (Kind.StaticField); a type has no module variable";
        return excluded;
    }

    internal sealed record Cell(Kind Kind, Host Host)
    {
        public string Id => $"{Kind}@{Host}";
    }

    internal static IReadOnlyList<Cell> Cells() =>
        (from kind in Kinds from host in Hosts where !ExcludedPairs.ContainsKey((kind, host)) select new Cell(kind, host)).ToList();

    /// <summary>D2's rule: a C# <c>const</c> is Const, any other value with an initializer Guarded, one with none Plain.</summary>
    internal static StaticStorage ExpectedStorage(Kind kind) => kind switch
    {
        Kind.LiteralConst => StaticStorage.Const,
        Kind.ModuleVariable or Kind.ComputedConst or Kind.PropertyDefault or Kind.StaticField => StaticStorage.Guarded,
        Kind.PropertyNoDefault or Kind.NoInitializer => StaticStorage.Plain,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The generic-host flag: the host or a type enclosing it declares a type parameter.</summary>
    internal static bool ExpectedGenericHost(Host host) => host is Host.GenericClass or Host.GenericStruct or Host.NestedInGenericClass;

    /// <summary>The declared name of a cell's value: unique in its host, screaming case for a const.</summary>
    private static string ValueName(Kind kind) => kind switch
    {
        Kind.ModuleVariable => "mv",
        Kind.ComputedConst => "CC",
        Kind.LiteralConst => "LC",
        Kind.PropertyDefault => "pd",
        Kind.PropertyNoDefault => "pn",
        Kind.StaticField => "sf",
        Kind.NoInitializer => "ni",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>A value declaration's lines. <paramref name="inType"/>: a static value carries <c>@static</c>
    /// (a const is static by itself, a module property implicitly).</summary>
    private static IEnumerable<string> Declaration(Kind kind, bool inType)
    {
        var name = ValueName(kind);
        var lines = kind switch
        {
            Kind.ModuleVariable => new[] { $"{name}: int = 3" },
            Kind.ComputedConst => new[] { $"const {name}: int = seven()" },
            Kind.LiteralConst => new[] { $"const {name}: int = 3" },
            Kind.PropertyDefault => new[] { $"property {name}: int = 5" },
            Kind.PropertyNoDefault => new[] { $"property {name}: int" },
            Kind.StaticField => new[] { $"{name}: int = 5" },
            Kind.NoInitializer => new[] { $"{name}: int" },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var isStatic = inType && kind is Kind.PropertyDefault or Kind.PropertyNoDefault or Kind.StaticField or Kind.NoInitializer;
        return isStatic ? new[] { "@static" }.Concat(lines) : lines;
    }

    /// <summary>The type a host's values are declared in: (header lines, member indent, type path).</summary>
    private static (string[] Header, string Indent, string[] Path) HostShape(Host host) => host switch
    {
        Host.Class => (new[] { "class HClass:" }, "    ", new[] { "HClass" }),
        Host.Struct => (new[] { "struct HStruct:" }, "    ", new[] { "HStruct" }),
        Host.GenericClass => (new[] { "class HGeneric[T]:" }, "    ", new[] { "HGeneric" }),
        Host.GenericStruct => (new[] { "struct HGenericStruct[T]:" }, "    ", new[] { "HGenericStruct" }),
        Host.NestedInClass => (new[] { "class HOuter:", "    class Inner:" }, "        ", new[] { "HOuter", "Inner" }),
        Host.NestedInGenericClass => (new[] { "class HGenericOuter[T]:", "    class Inner:" }, "        ", new[] { "HGenericOuter", "Inner" }),
        _ => throw new ArgumentOutOfRangeException(nameof(host)),
    };

    /// <summary>lib.spy: every cell's declaration, generated from <see cref="Cells"/>.</summary>
    internal static string LibSource()
    {
        var lines = new List<string> { "def seven() -> int:", "    return 7", "" };
        foreach (var group in Cells().GroupBy(c => c.Host))
        {
            if (group.Key == Host.Module)
            {
                foreach (var cell in group)
                    lines.AddRange(Declaration(cell.Kind, inType: false));
            }
            else
            {
                var (header, indent, _) = HostShape(group.Key);
                lines.AddRange(header);
                foreach (var cell in group)
                    lines.AddRange(Declaration(cell.Kind, inType: true).Select(l => indent + l));
            }
            lines.Add("");
        }
        return string.Join("\n", lines) + "\n";
    }

    // HasSweep's specimen beside lib: a module of literal consts only (no sweep), and a value-less
    // module that imports lib (a sweep by transitivity).
    private const string ConstsSource = "const ONLY: int = 3\n";

    private const string MidSource = "import lib\n\ndef ping() -> int:\n    return lib.seven()\n";

    private static string MainSource(string touch) =>
        $"# {touch}\nimport lib\nimport mid\nfrom consts import ONLY\nfrom mid import ping\nfrom lib import mv as amv\n\n"
        + "def main() -> None:\n    print(lib.mv, mid.ping(), ONLY, ping(), amv)\n";

    // ------------------------------------------------------------------------------------------- //
    // Harness
    // ------------------------------------------------------------------------------------------- //

    private string Write(string dir, string name, string content)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static ProjectConfig Config(string dir, params string[] sourceFiles) => new()
    {
        ProjectFilePath = Path.Combine(dir, "storage.spyproj"),
        ProjectDirectory = dir,
        RootNamespace = "Storage",
        SourceFiles = sourceFiles.ToList(),
        Configuration = "Debug",
    };

    private static ProjectCompilationResult Build(ProjectConfig config)
        => new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance).CompileProject(config);

    private static string Diagnostics(ProjectCompilationResult result)
        => string.Join("\n", result.Diagnostics.GetAll().Select(d => $"{d.Severity}:{d.Code} {d.Message}"));

    private static IReadOnlyList<string> Skipped(ProjectCompilationResult result)
        => result.Metrics?.SkippedFiles.Select(Path.GetFileName).OfType<string>().ToList() ?? new List<string>();

    /// <summary>The module-scope symbols <paramref name="fileLeaf"/> declares, identity-deduped.</summary>
    private static IReadOnlyList<Symbol> DeclaredIn(ProjectModel model, string fileLeaf)
    {
        var seen = new HashSet<Symbol>(ReferenceEqualityComparer.Instance);
        var symbols = new List<Symbol>();
        foreach (var symbol in model.GlobalSymbols!.GetAllModuleScopeSymbols())
        {
            if (!seen.Add(symbol))
                continue;
            var declaring = symbol is TypeSymbol type ? type.DefiningFilePath ?? type.DeclaringFilePath : symbol.DeclaringFilePath;
            if (declaring != null && Path.GetFileName(declaring) == fileLeaf)
                symbols.Add(symbol);
        }
        return symbols;
    }

    /// <summary>What a build knows of one cell: "storage generic", or why it knows nothing.</summary>
    private static string ReadCell(ProjectModel model, Cell cell)
    {
        var binding = model.SemanticBinding;
        var name = ValueName(cell.Kind);
        var declared = DeclaredIn(model, "lib.spy");

        if (cell.Host == Host.Module)
        {
            var facts = declared.OfType<VariableSymbol>().Where(v => v.Name == name)
                .Select(v => binding.GetCodeGenInfo(v)).OfType<CodeGenInfo>()
                .Select(i => Render(i.StaticStorage, i.IsOnGenericHost)).Distinct().ToList();
            return facts.Count == 1 ? facts[0] : $"<{facts.Count} distinct facts: {string.Join(" | ", facts)}>";
        }

        var (_, _, path) = HostShape(cell.Host);
        var type = declared.OfType<TypeSymbol>().FirstOrDefault(t => t.Name == path[0]);
        foreach (var segment in path.Skip(1))
            type = type?.NestedTypes.FirstOrDefault(t => t.Name == segment);
        if (type == null)
            return $"<no type {string.Join(".", path)}>";

        if (cell.Kind is Kind.PropertyDefault or Kind.PropertyNoDefault)
        {
            return type.FindOwnProperty(name) is { } property
                ? Render(property.StaticStorage, property.IsOnGenericHost)
                : $"<no property {name}>";
        }

        var field = type.Fields.FirstOrDefault(f => f.Name == name);
        if (field == null)
            return $"<no field {name}>";
        return binding.GetCodeGenInfo(field) is { } info ? Render(info.StaticStorage, info.IsOnGenericHost) : "<NO CodeGenInfo>";
    }

    private static string Render(StaticStorage? storage, bool generic) => $"{storage?.ToString() ?? "null"} generic={generic}";

    private static string Expected(Cell cell) => Render(ExpectedStorage(cell.Kind), ExpectedGenericHost(cell.Host));

    private static List<string> CellFailures(ProjectModel model, string build) =>
        Cells().Select(c => (Cell: c, Actual: ReadCell(model, c)))
            .Where(r => r.Actual != Expected(r.Cell))
            .Select(r => $"{build} {r.Cell.Id}: expected {Expected(r.Cell)}, got {r.Actual}")
            .ToList();

    /// <summary>What an importer's ModuleSymbol (<c>import m</c>) carries: its CodeGenInfo's HasSweep.</summary>
    private static bool? ImportedHasSweep(ProjectModel model, string importerLeaf, string moduleName)
    {
        var unit = model.Units.Values.Single(u => Path.GetFileName(u.FilePath) == importerLeaf);
        var module = unit.ModuleScope?.Lookup(moduleName, searchParent: false) as ModuleSymbol;
        return module == null ? null : model.SemanticBinding.GetCodeGenInfo(module)?.HasSweep;
    }

    /// <summary>What an importer's from-import carries: its recorded source layout's HasSweep.</summary>
    private static bool? FromImportedHasSweep(ProjectModel model, string importerLeaf, string moduleName)
    {
        var unit = model.Units.Values.Single(u => Path.GetFileName(u.FilePath) == importerLeaf);
        var node = unit.Ast?.Body.Select(s => s.UnwrapDecorated()).OfType<FromImportStatement>()
            .FirstOrDefault(f => f.Module == moduleName);
        return node == null ? null : model.SemanticInfo?.GetModuleLayout(node)?.HasSweep;
    }

    // ------------------------------------------------------------------------------------------- //
    // The cells
    // ------------------------------------------------------------------------------------------- //

    [Fact]
    public void Generator_IsTotalOverKindTimesHost()
    {
        var product = (from kind in Kinds from host in Hosts select (kind, host)).ToList();
        var cells = Cells().Select(c => (c.Kind, c.Host)).ToHashSet();

        product.Where(p => !cells.Contains(p) && !ExcludedPairs.ContainsKey(p)).Should().BeEmpty("every pair is a cell or an excluded pair");
        product.Where(p => cells.Contains(p) && ExcludedPairs.ContainsKey(p)).Should().BeEmpty("no pair is both");
        ExcludedPairs.Keys.Should().OnlyContain(p => product.Contains(p), "an exclusion names a pair of the product");
        Kinds.Select(ExpectedStorage).Distinct().Should().BeEquivalentTo(Enum.GetValues<StaticStorage>(),
            "the kinds reach every StaticStorage value");
        Cells().Select(c => ExpectedGenericHost(c.Host)).Distinct().Should().HaveCount(2, "the hosts reach both flag values");
    }

    [Fact]
    public void EveryKindTimesHost_CarriesItsStaticStorageAndGenericHostFlag_ColdAndWarm()
    {
        var dir = Path.Combine(_tempDir, "cells");
        var lib = Write(dir, "lib.spy", LibSource());
        var consts = Write(dir, "consts.spy", ConstsSource);
        var mid = Write(dir, "mid.spy", MidSource);
        var main = Write(dir, "main.spy", MainSource("build 1"));
        var config = Config(dir, lib, consts, mid, main);
        _output.WriteLine(LibSource());

        // Build 1: cold. Every fact is computed.
        var cold = Build(config);
        cold.Success.Should().BeTrue("the specimen must compile. Diagnostics:\n" + Diagnostics(cold));
        var coldModel = cold.ProjectModel!;

        // Build 2: warm. Only main changes, so lib, consts and mid are served from the cache and their
        // facts come through SymbolSerializer and the file entry.
        Write(dir, "main.spy", MainSource("build 2"));
        var warm = Build(config);
        warm.Success.Should().BeTrue("the warm build must compile. Diagnostics:\n" + Diagnostics(warm));
        Skipped(warm).Should().Contain(new[] { "lib.spy", "consts.spy", "mid.spy" },
            "the positive control of every warm cell: a file that was not served measured the cold path again");
        var warmModel = warm.ProjectModel!;

        var failures = CellFailures(coldModel, "cold").Concat(CellFailures(warmModel, "warm")).ToList();

        // HasSweep: each module's own record, and what each importer's two carriers read from it.
        foreach (var (build, model) in new[] { ("cold", coldModel), ("warm", warmModel) })
        {
            void Expect(string what, bool? actual, bool expected)
            {
                if (actual != expected)
                    failures.Add($"{build} {what}: expected {expected}, got {actual?.ToString() ?? "null"}");
            }
            Expect("record lib (Guarded values)", model.SemanticBinding.GetModuleHasSweep(lib), true);
            Expect("record consts (literal consts only)", model.SemanticBinding.GetModuleHasSweep(consts), false);
            Expect("record mid (no value, imports lib)", model.SemanticBinding.GetModuleHasSweep(mid), true);
            Expect("main: import lib", ImportedHasSweep(model, "main.spy", "lib"), true);
            Expect("main: import mid (transitive)", ImportedHasSweep(model, "main.spy", "mid"), true);
            Expect("main: from consts import", FromImportedHasSweep(model, "main.spy", "consts"), false);
            Expect("main: from mid import (transitive)", FromImportedHasSweep(model, "main.spy", "mid"), true);

            // An aliased from-import binds a clone: its storage fact is the declaration's.
            var alias = model.Units.Values.Single(u => Path.GetFileName(u.FilePath) == "main.spy")
                .ModuleScope?.Lookup("amv", searchParent: false);
            var aliasFact = alias == null ? "<no symbol amv>"
                : model.SemanticBinding.GetCodeGenInfo(alias) is { } aliasInfo ? Render(aliasInfo.StaticStorage, aliasInfo.IsOnGenericHost) : "<NO CodeGenInfo>";
            if (aliasFact != Render(StaticStorage.Guarded, false))
                failures.Add($"{build} main: from lib import mv as amv: expected {Render(StaticStorage.Guarded, false)}, got {aliasFact}");
        }

        _output.WriteLine($"{Cells().Count} cells x 2 builds, {failures.Count} failure(s)");
        failures.Should().BeEmpty("each cell carries D2's facts on both builds:\n  " + string.Join("\n  ", failures));
    }

    /// <summary>
    /// The conservative arm (lead's ruling on T2): in a deferred import cycle the member checked first
    /// imports a peer with no record yet and reads HasSweep true, and the peer, checked second, reads
    /// the first's record — true through its import. Neither declares a value, so a false on either
    /// side means the arm did not fire. The control is the same pair without the cycle: false.
    /// </summary>
    [Fact]
    public void DeferredImportCycle_EachFromImporterReadsHasSweepTrue_AndTheAcyclicControlReadsFalse()
    {
        // A deferrable cycle imports types only (a function import across a cycle is SPY0302).
        var cycleDir = Path.Combine(_tempDir, "cycle");
        var a = Write(cycleDir, "a.spy", "from b import ClassB\n\nclass ClassA:\n    pass\n");
        var b = Write(cycleDir, "b.spy", "from a import ClassA\n\nclass ClassB:\n    pass\n");
        var main = Write(cycleDir, "main.spy", "from a import ClassA\nfrom b import ClassB\n\ndef main() -> None:\n    print(1)\n");
        var cycle = Build(Config(cycleDir, a, b, main));
        cycle.Success.Should().BeTrue("the cycle specimen must compile. Diagnostics:\n" + Diagnostics(cycle));
        var model = cycle.ProjectModel!;

        FromImportedHasSweep(model, "a.spy", "b").Should().BeTrue("a's import of its cycle peer b");
        FromImportedHasSweep(model, "b.spy", "a").Should().BeTrue("b's import of its cycle peer a");

        var controlDir = Path.Combine(_tempDir, "acyclic");
        var ca = Write(controlDir, "a.spy", "class ClassA:\n    pass\n");
        var cb = Write(controlDir, "b.spy", "from a import ClassA\n\nclass ClassB:\n    pass\n");
        var cmain = Write(controlDir, "main.spy", "from b import ClassB\n\ndef main() -> None:\n    print(1)\n");
        var control = Build(Config(controlDir, ca, cb, cmain));
        control.Success.Should().BeTrue("the control must compile. Diagnostics:\n" + Diagnostics(control));
        FromImportedHasSweep(control.ProjectModel!, "b.spy", "a").Should().BeFalse("a value-less acyclic import has no sweep");
        FromImportedHasSweep(control.ProjectModel!, "main.spy", "b").Should().BeFalse("nor does its importer");
    }
}
