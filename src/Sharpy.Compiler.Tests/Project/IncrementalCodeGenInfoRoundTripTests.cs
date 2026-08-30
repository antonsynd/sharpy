using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// #1633 Phase 5.2 Task 3: the incremental cache round-trips <c>CodeGenInfo</c> through the
/// <see cref="SemanticBinding"/>, not through the <see cref="Symbol"/>.
///
/// <para><b>What was unguarded.</b> <c>SymbolSerializer.Deserialize</c> writes six
/// <c>binding.SetCodeGenInfo(...)</c> calls — one per symbol kind — and that is the only path by
/// which a cache-served file's CodeGenInfo reaches the store code generation reads. Deleting all
/// six leaves 727 tests green: every test caller passes <c>binding: null</c>, so only production
/// (<c>IncrementalCompilationCache</c>) exercises the parameter at all, and the existing
/// cold/warm differential specimen carries no fact whose loss changes the emitted C#.</para>
///
/// <para><b>Why the third build is the cell.</b> On a warm build of unchanged sources every file
/// is served from the cache, generated C# included, so a comparison of build 1 against build 2
/// would compare cached text with itself. Touching <c>main.spy</c> splits the project: main is
/// re-emitted while lib is restored, so main's C# is generated from lib's DESERIALIZED symbols.
/// The specimen is chosen so that this actually shows: <c>make_box</c> and <c>max_items</c> are
/// emitted as <c>MakeBox</c> and <c>MaxItems</c> — the <c>CodeGenInfo.CSharpName</c> of a restored
/// symbol — and lib's own emission carries a synthesized <c>ISized</c> (from <c>__len__</c>), an
/// <c>override string ToString()</c> (a CLR-base override), and a base-call constructor.</para>
/// </summary>
public class IncrementalCodeGenInfoRoundTripTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public IncrementalCodeGenInfoRoundTripTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_cgi_roundtrip_{Guid.NewGuid():N}");
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

    /// <summary>
    /// Every declaration here exists because its emission is decided by a CodeGenInfo fact:
    /// <c>max_items</c>/<c>make_box</c> mangle to <c>MaxItems</c>/<c>MakeBox</c> (CSharpName, and
    /// the only two facts VISIBLE FROM ANOTHER FILE, hence the ones the third build measures),
    /// <c>__len__</c> synthesizes <c>ISized</c> plus a <c>Count</c> property (SynthesizedInterfaces
    /// + ClrMethodName), <c>__str__</c> on an <c>Exception</c> subclass becomes
    /// <c>override string ToString()</c> (OverridesClrBaseMember + ClrMethodName), and
    /// <c>__init__</c> calling <c>super().__init__</c> becomes a base-call constructor.
    /// </summary>
    private const string LibSource = @"max_items: int = 10


class Box:
    items: list[int]

    def __init__(self, items: list[int]) -> None:
        self.items = items

    def __len__(self) -> int:
        return len(self.items)


class BoxError(Exception):
    def __init__(self, message: str) -> None:
        super().__init__(message)

    def __str__(self) -> str:
        return ""box error""


def make_box() -> Box:
    return Box([1, 2, 3])
";

    private const string MainSource = @"from lib import Box, BoxError, max_items, make_box


def main() -> None:
    b: Box = make_box()
    print(len(b))
    print(max_items)
    e: BoxError = BoxError(""boom"")
    print(str(e))
";

    #region Harness

    private string Write(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private ProjectConfig Config(params string[] sourceFiles) => new()
    {
        ProjectFilePath = Path.Combine(_tempDir, "roundtrip.spyproj"),
        ProjectDirectory = _tempDir,
        RootNamespace = "RoundTrip",
        SourceFiles = sourceFiles.ToList(),
        Configuration = "Debug",
    };

    private static ProjectCompilationResult Build(ProjectConfig config)
        => new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance)
            .CompileProject(config);

    private static string Diagnostics(ProjectCompilationResult result)
        => string.Join("\n", result.Diagnostics.GetAll().Select(d => $"{d.Severity}:{d.Code} {d.Message}"));

    /// <summary>
    /// The files served from the cache instead of recompiled. This is the POSITIVE CONTROL for
    /// every assertion below: if nothing was skipped then nothing came through
    /// <c>SymbolSerializer.Deserialize</c>, and the comparisons would be measuring the cold path
    /// against itself.
    /// </summary>
    private static IReadOnlyList<string> Skipped(ProjectCompilationResult result)
        => result.Metrics?.SkippedFiles.Select(Path.GetFileName).OfType<string>().ToList()
            ?? new List<string>();

    private static IReadOnlyDictionary<string, string> Generated(ProjectCompilationResult result)
        => result.GeneratedCSharpFiles
            .ToDictionary(kv => Path.GetFileName(kv.Key), kv => kv.Value, StringComparer.Ordinal);

    /// <summary>
    /// What the binding knows about every symbol declared in <paramref name="fileLeaf"/>, as a
    /// sorted, comparable projection. A symbol whose CodeGenInfo did not survive the round trip
    /// shows up as <c>&lt;NO CodeGenInfo&gt;</c>, and a symbol whose name decoded differently shows
    /// up as a changed row — the cold build is the oracle, so nothing here is a hand-written
    /// expectation that could be wrong in the same direction as the code.
    /// </summary>
    private IReadOnlyList<string> BindingFacts(ProjectCompilationResult result, string fileLeaf)
    {
        var model = result.ProjectModel;
        model.Should().NotBeNull("the compile result must carry the project model");

        var binding = model!.SemanticBinding;
        var rows = new List<string>();

        // Exactly the twelve fields SerializeCodeGenInfo writes to the wire — the serializer's
        // actual contract. The five it does NOT carry are asserted separately below, because
        // folding them in here would make every row differ for a reason that is by design.
        void Add(string label, Symbol symbol)
        {
            var info = binding.GetCodeGenInfo(symbol);
            rows.Add(info == null
                ? $"{label} -> <NO CodeGenInfo>"
                : $"{label} -> {info.CSharpName} original={info.OriginalName} v={info.Version}"
                  + $" moduleLevel={info.IsModuleLevel} constant={info.IsConstant}"
                  + $" clrMethod={info.ClrMethodName ?? "-"}"
                  + $" strips={info.StripsOverrideKeyword} implementsIface={info.ImplementsInterfaceMethod}"
                  + $" importKind={info.ImportKind} stringEnum={info.IsStringEnum}"
                  + $" execOrder={info.HasExecutionOrderIssues}"
                  + $" originalImport={info.OriginalImportName ?? "-"}");
        }

        foreach (var symbol in SymbolsDeclaredIn(model, fileLeaf))
        {
            switch (symbol)
            {
                case TypeSymbol type:
                    Add($"type {type.Name}", type);
                    foreach (var method in type.Methods.OrderBy(m => m.Name, StringComparer.Ordinal))
                        Add($"method {type.Name}.{method.Name}", method);
                    foreach (var field in type.Fields.OrderBy(f => f.Name, StringComparer.Ordinal))
                        Add($"field {type.Name}.{field.Name}", field);
                    for (var i = 0; i < type.Constructors.Count; i++)
                        Add($"ctor {type.Name}#{i}", type.Constructors[i]);
                    break;
                case FunctionSymbol function:
                    Add($"function {function.Name}", function);
                    break;
                case VariableSymbol variable:
                    Add($"variable {variable.Name}", variable);
                    break;
            }
        }

        // A SET of facts: multiplicity is a property of how many scope entries reach a symbol,
        // which is not this test's subject, while the presence and content of each fact is.
        return rows.Distinct(StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The symbols another file can reference: the project-wide module-scope table, filtered to
    /// the ones this file declares. That set IS the round trip's subject — it is what a
    /// recompiled dependent is emitted against — and taking it from one source keeps the
    /// projection stable (the per-unit lists and the global table hold DIFFERENT VariableSymbol
    /// instances for the same module-level name, so unioning them made the cold projection carry
    /// a duplicate row that no warm build could reproduce).
    /// </summary>
    private static IReadOnlyList<Symbol> SymbolsDeclaredIn(ProjectModel model, string fileLeaf)
    {
        var symbols = new List<Symbol>();
        if (model.GlobalSymbols is not { } table)
            return symbols;

        // The same symbol INSTANCE is reachable under more than one module-scope entry (an import
        // alias and the declaration itself), so identity-dedupe before projecting: otherwise the
        // cold projection carries every row twice and no warm build can match its multiplicity.
        var seen = new HashSet<Symbol>(ReferenceEqualityComparer.Instance);

        foreach (var symbol in table.GetAllModuleScopeSymbols())
        {
            if (!seen.Add(symbol))
                continue;

            var declaring = symbol switch
            {
                TypeSymbol type => type.DefiningFilePath ?? type.DeclaringFilePath,
                _ => symbol.DeclaringFilePath
            };

            if (declaring != null && Path.GetFileName(declaring) == fileLeaf)
                symbols.Add(symbol);
        }

        return symbols.OrderBy(s => $"{s.GetType().Name}:{s.Name}", StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The <see cref="CodeGenInfo"/> of the named module-scope type, or null when it has none.
    /// </summary>
    private static CodeGenInfo? TypeInfo(ProjectCompilationResult result, string fileLeaf, string typeName)
    {
        var model = result.ProjectModel!;
        var type = SymbolsDeclaredIn(model, fileLeaf).OfType<TypeSymbol>()
            .FirstOrDefault(t => t.Name == typeName);
        return type == null ? null : model.SemanticBinding.GetCodeGenInfo(type);
    }

    /// <summary>
    /// Every restored symbol answers <c>GetCodeGenInfo</c>, its facts are ones the cold build
    /// computed for that same symbol, and no symbol was lost.
    ///
    /// <para>Containment rather than set equality, deliberately: the cold module-scope table holds
    /// TWO <c>VariableSymbol</c> instances for a module-level name (one flagged
    /// <c>IsModuleLevel</c>, one not) and a restore produces one. That multiplicity is a property
    /// of how the cold symbol graph is populated, not of the round trip, and demanding warm
    /// reproduce it would make this test fail for a reason it is not about. What it does demand
    /// is that no NAME disappears and that every fact a restored symbol carries is a fact the cold
    /// build actually computed — a decoded <c>Max_items</c>, or a <c>&lt;NO CodeGenInfo&gt;</c>,
    /// matches no cold row.</para>
    /// </summary>
    private static void AssertRoundTripped(
        IReadOnlyList<string> coldFacts, IReadOnlyList<string> actualFacts, string what)
    {
        actualFacts.Should().NotContain(row => row.Contains("<NO CodeGenInfo>"),
            $"every symbol {what} restored must answer GetCodeGenInfo non-null — the six "
            + "binding.SetCodeGenInfo calls in SymbolSerializer.Deserialize are the only path by "
            + "which a cache-served symbol's CodeGenInfo reaches the store code generation reads "
            + "(#1633)");

        static string Label(string row) => row.Split(" -> ")[0];

        actualFacts.Select(Label).Distinct().Should().BeEquivalentTo(
            coldFacts.Select(Label).Distinct(),
            $"{what} must restore every symbol the cold build declared — a symbol that vanished "
            + "cannot have a wrong fact, and would make the fact comparison vacuous");

        foreach (var row in actualFacts)
        {
            coldFacts.Should().Contain(row,
                $"{what} must carry the CodeGenInfo the cold build computed — same C# name, same "
                + $"CLR method name, same import/constant/version facts. Cold rows for that "
                + $"symbol: {string.Join(" | ", coldFacts.Where(c => Label(c) == Label(row)))}");
        }
    }

    #endregion

    [Fact]
    public void WarmRestore_CodeGenInfoReachesTheBinding_AndTheEmittedCSharpIsIdentical()
    {
        var lib = Write("lib.spy", LibSource);
        var main = Write("main.spy", MainSource);
        var config = Config(lib, main);

        // --- Build 1: cold. Nothing cached; every fact is computed, not decoded. ---
        var cold = Build(config);
        cold.Success.Should().BeTrue(
            "the specimen must compile, or every comparison below is vacuous. Diagnostics:\n"
            + Diagnostics(cold));

        var coldGenerated = Generated(cold);
        var coldFacts = BindingFacts(cold, "lib.spy");
        _output.WriteLine("cold binding facts for lib.spy:\n  " + string.Join("\n  ", coldFacts));

        // Non-vacuity of the binding projection: an empty (or CodeGenInfo-less) cold projection
        // would make the warm comparison an equality of two empty lists.
        coldFacts.Should().HaveCountGreaterThan(5,
            "the cold build must know CodeGenInfo for lib's symbols, or there is nothing whose "
            + "round trip this test could measure");
        coldFacts.Should().NotContain(row => row.Contains("<NO CodeGenInfo>"),
            "on a cold build every declared symbol is computed, so none may be missing CodeGenInfo");

        // Non-vacuity of the C# comparison: main's emission must actually SPELL facts that live on
        // lib's symbols, or a lost CodeGenInfo could not change main's C# either way.
        coldGenerated.Should().ContainKey("main.cs");
        coldGenerated["main.cs"].Should().Contain("MakeBox",
            "main calls `make_box`, whose C# name comes from the CodeGenInfo of a symbol that the "
            + "third build reads back out of the cache");
        coldGenerated["main.cs"].Should().Contain("MaxItems",
            "main reads `max_items`, a module-level variable whose C# name is a CodeGenInfo fact");
        coldGenerated.Should().ContainKey("lib.cs");
        coldGenerated["lib.cs"].Should().Contain("ISized",
            "`__len__` synthesizes ISized — a CodeGenInfo fact that must survive serialization");
        coldGenerated["lib.cs"].Should().Contain("override string ToString",
            "`__str__` on an Exception subclass is a CLR-base override — also a CodeGenInfo fact");

        // --- Build 2: warm, nothing edited. Both files come from the cache. ---
        var warm = Build(config);
        warm.Success.Should().BeTrue(
            "a warm build of unchanged sources must succeed. Diagnostics:\n" + Diagnostics(warm));

        Skipped(warm).Should().BeEquivalentTo(new[] { "lib.spy", "main.spy" },
            "both files are unchanged, so both must be served from the cache — otherwise the "
            + "restore path under test never ran");

        var warmFacts = BindingFacts(warm, "lib.spy");
        _output.WriteLine("warm binding facts for lib.spy:\n  " + string.Join("\n  ", warmFacts));

        AssertRoundTripped(coldFacts, warmFacts, "the warm build");

        // The five fields SerializeCodeGenInfo does NOT write (OverridesClrBaseMember,
        // IsCompileTimeConstant, ForwardingConstructors, SelfInterfaceBridges,
        // SynthesizedInterfaces) come back at their defaults. Stated as an assertion rather than
        // a comment: it is safe TODAY only because a cache-served file's C# is served from the
        // cache too — it is never re-emitted from restored symbols — and the day either half of
        // that changes, this row goes red and the wire format has to grow (drain on fix). The
        // cold arm is the positive control: without it, "restored is 0" would also pass if the
        // fact never existed.
        TypeInfo(cold, "lib.spy", "Box")!.SynthesizedInterfaces.Should().HaveCount(1,
            "`__len__` makes the cold build synthesize ISized on Box — the fact whose absence "
            + "after a round trip the next assertion documents");
        TypeInfo(warm, "lib.spy", "Box")!.SynthesizedInterfaces.Should().BeNullOrEmpty(
            "SynthesizedInterfaces is one of the five CodeGenInfo fields the wire format does not "
            + "carry (SymbolSerializer.SerializeCodeGenInfo writes twelve). A restored Box "
            + "therefore knows nothing about ISized — which no emission consults, because lib.spy "
            + "is served from the cache rather than re-emitted");
        TypeInfo(warm, "lib.spy", "Box")!.CSharpName.Should().Be("Box",
            "the carried half of the record must still be there — this is not a null CodeGenInfo");

        Generated(warm).Should().BeEquivalentTo(coldGenerated,
            "a warm build must emit what the cold build emitted");

        // --- Build 3: main edited inertly; lib is restored while main is re-emitted. ---
        //
        // This is the cell: main's C# is now GENERATED from lib's deserialized symbols, so a
        // CodeGenInfo that did not reach the binding shows up as a different name in main.cs
        // (or as a CS error behind SPY0908). A trailing comment changes no emitted code, so any
        // difference here is the restore, not the edit.
        File.WriteAllText(main, MainSource + "\n# touched\n");

        var afterTouch = Build(config);

        Skipped(afterTouch).Should().BeEquivalentTo(new[] { "lib.spy" },
            "the edit must split the project: main.spy recompiles and lib.spy is restored. If lib "
            + "were recompiled too, main would be reading FRESH symbols and this cell would say "
            + "nothing about the round trip");

        afterTouch.Success.Should().BeTrue(
            "main is re-emitted against lib's DESERIALIZED symbols; a CodeGenInfo that never "
            + "reached the binding surfaces here as an unresolved C# name behind SPY0908. "
            + "Diagnostics:\n" + Diagnostics(afterTouch));

        var touchedFacts = BindingFacts(afterTouch, "lib.spy");
        _output.WriteLine("after-touch binding facts for lib.spy:\n  " + string.Join("\n  ", touchedFacts));

        AssertRoundTripped(coldFacts, touchedFacts, "the build that re-emits main against a restored lib");

        Generated(afterTouch)["main.cs"].Should().Be(coldGenerated["main.cs"],
            "main's emitted C# must be byte-identical to the cold build's: it names `MakeBox` and "
            + "`MaxItems`, which are CodeGenInfo facts of symbols that came out of the cache");
        Generated(afterTouch).Should().BeEquivalentTo(coldGenerated,
            "and the project's emission as a whole is unchanged by an inert edit");
    }
}
