using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.TestInfrastructure.Formatting;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Registry;
using Xunit;
using Xunit.Abstractions;
using SAst = Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// #1633: a compilation writes only state it owns. The master <see cref="BuiltinRegistry"/> (and
/// every symbol cache reachable from it) is shared by every analysis a <see cref="CompilerApi"/>
/// performs, so a <see cref="Symbol"/> reachable from it is never a materialization target.
///
/// <para><b>Why this is a snapshot and not an assertion about one field.</b> The per-compilation
/// registry clone was retired with the fix (it was the thing that made the write survivable), so
/// the master IS what every analysis sees — including the FIRST one, which was always handed the
/// master uncloned. The live Symbol writes are still there by design (
/// <c>SemanticBinding.MaterializeInheritance</c> writes <c>BaseType</c>/<c>BaseTypeRef</c>/
/// <c>Interfaces</c>, <c>MaterializeVariableTypes</c> writes <c>VariableSymbol.Type</c>), and the
/// contract is that none of them ever lands on a master symbol. A guard naming one field would
/// pass while a sibling field poisoned the registry, so this enumerates by reflection: every
/// property of every <see cref="Symbol"/>-derived type reachable from the analysis cache, before
/// and after each entry point.</para>
///
/// <para><b>Instrument controls.</b> An "assert nothing changed" guard passes vacuously if it
/// enumerates nothing, so <see cref="Snapshot_DetectsAMutationOfAMasterSymbol"/> is a positive
/// control that mutates a master symbol and requires the comparison to name it, and every
/// comparison asserts a floor on the number of symbols and properties actually compared.</para>
/// </summary>
public class MasterRegistryImmutabilityTests
{
    private readonly ITestOutputHelper _output;

    public MasterRegistryImmutabilityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Corpus

    /// <summary>
    /// Shapes that reach the materialization paths that write to symbols. The builtin-shadowing
    /// program is #1633's own poisoner shape; the builtin-inheriting class drives
    /// <c>MaterializeInheritance</c> with a MASTER symbol as the base type; the dunder class
    /// drives protocol-interface synthesis; the CLR import drives the discovery/bridge caches
    /// that vend symbols (#1672 H1.4).
    /// </summary>
    private static readonly (string Name, string Source)[] Corpus =
    {
        ("plain", """
            def main() -> None:
                a: int = len("hello")
                b: str = str(42)
                print(a)
                print(b)
            """),

        ("builtin-shadow", """
            def twice(x: int) -> int:
                return x * 2

            def main() -> None:
                len = twice
                print(len(21))
            """),

        ("inherits-builtin", """
            class NotFound(Exception):
                def __init__(self, msg: str):
                    super().__init__(msg)

            def main() -> None:
                try:
                    raise NotFound("nf")
                except NotFound as e:
                    print("caught")
            """),

        ("dunder-protocol", """
            class Box:
                items: list[int]

                def __init__(self) -> None:
                    self.items = [1, 2, 3]

                def __len__(self) -> int:
                    return len(self.items)

            def main() -> None:
                b: Box = Box()
                print(len(b))
            """),

        ("clr-import", """
            from System.IO import StringWriter

            def main() -> None:
                w: StringWriter = StringWriter()
                w.Write("x")
                print(w.ToString())
            """),
    };

    #endregion

    #region The guard

    [Fact]
    public void MasterSymbols_Unchanged_AcrossAnalyzeAndCompile_IncludingFirstUse()
    {
        var api = new CompilerApi();

        // The master is built lazily and the FIRST analysis is handed it directly, so the
        // baseline must be taken before any analysis runs — priming builds the same cache entry
        // the path-less Analyze overload would build, without analyzing anything.
        var master = api.PrimeAnalysisContextForTests();
        Assert.NotNull(master);

        var snapshot = new RegistrySnapshot(api);
        _output.WriteLine(snapshot.Describe("baseline"));
        snapshot.AssertNotVacuous();

        foreach (var (name, source) in Corpus)
        {
            RunQuietly($"Analyze({name})", () => api.Analyze(source, CompilerOptionsFactory.ForLibraryAnalysis()));
            AssertUnchanged(api, master, snapshot, $"Analyze({name})");

            RunQuietly($"AnalyzeDocument({name})", () => api.AnalyzeDocument(
                source, $"{name}.spy", CompilerOptionsFactory.ForLibraryAnalysis(), null, default));
            AssertUnchanged(api, master, snapshot, $"AnalyzeDocument({name})");

            RunQuietly($"Compile({name})", () => api.Compile(source));
            AssertUnchanged(api, master, snapshot, $"Compile({name})");
        }
    }

    [Fact]
    public void MasterSymbols_Unchanged_AcrossAnalyzeProject()
    {
        using var helper = new Helpers.ProjectCompilationHelper(_output)
            .WithRootNamespace("MasterRegistryProbe");
        foreach (var (name, source) in Corpus)
            helper.AddSourceFile($"{name}.spy", source);
        helper.CreateProjectFile();
        var config = ProjectFileParser.Load(
            Path.Combine(helper.ProjectDirectory, "MasterRegistryProbe.spyproj"));

        var api = new CompilerApi();
        var master = api.PrimeAnalysisContextForTests(config);
        Assert.NotNull(master);

        var snapshot = new RegistrySnapshot(api);
        _output.WriteLine(snapshot.Describe("baseline"));
        snapshot.AssertNotVacuous();

        for (int i = 0; i < 3; i++)
        {
            RunQuietly($"AnalyzeProject #{i + 1}", () => api.AnalyzeProject(config));
            AssertUnchanged(api, master, snapshot, $"AnalyzeProject #{i + 1}");
        }
    }

    /// <summary>
    /// Positive control for the instrument (verification-contract §3): the same comparison that
    /// reports "nothing changed" above must REPORT a change that is really there, naming the
    /// symbol and the property. Without this, an enumeration that silently reached nothing —
    /// or a digest that is constant — would make every assertion above pass vacuously.
    /// </summary>
    [Fact]
    public void Snapshot_DetectsAMutationOfAMasterSymbol()
    {
        var api = new CompilerApi();
        var master = api.PrimeAnalysisContextForTests();
        var snapshot = new RegistrySnapshot(api);
        snapshot.AssertNotVacuous();

        var len = master.GetFunction("len");
        Assert.NotNull(len);
        var original = len!.Documentation;
        try
        {
            len.Documentation = "poisoned by the positive control";

            var mutations = snapshot.FindMutations(api);
            Assert.NotEmpty(mutations);
            Assert.Contains(mutations, m =>
                m.SymbolPath.EndsWith(":len", StringComparison.Ordinal)
                && m.Property == nameof(Symbol.Documentation)
                && m.After.Contains("poisoned by the positive control", StringComparison.Ordinal));
            _output.WriteLine(Format(mutations));
        }
        finally
        {
            len.Documentation = original;
        }

        // ...and the same comparison is clean once the mutation is undone, so the control is
        // detecting the mutation rather than a snapshot that never matches itself.
        Assert.Empty(snapshot.FindMutations(api));
    }

    /// <summary>
    /// The two smoke tests the guard replaced, kept as positive controls that the corpus still
    /// COMPILES through one reused <see cref="CompilerApi"/> — a guard that only proves nothing
    /// was written would also pass if every analysis failed outright.
    /// </summary>
    [Fact]
    public void SequentialReuse_NoClone_Succeeds()
    {
        var api = new CompilerApi();

        for (int i = 0; i < 4; i++)
        {
            var source = $"def main() -> None:\n    v{i}: int = len([{i}])\n    print(v{i})\n";
            var result = api.Analyze(source);
            Assert.True(result.Success,
                $"Analysis {i + 1} failed: {string.Join("; ", result.Diagnostics.Select(d => d.Message))}");
        }

        _output.WriteLine("4 sequential analyses through one CompilerApi: no exception");
    }

    [Fact]
    public void SequentialReuse_MixedPaths_Succeeds()
    {
        var api = new CompilerApi();

        var analyzeSource = "def main() -> None:\n    a: int = len([1])\n    print(a)\n";
        var analyzeResult = api.Analyze(analyzeSource);
        Assert.True(analyzeResult.Success,
            $"Analyze failed: {string.Join("; ", analyzeResult.Diagnostics.Select(d => d.Message))}");

        var compileSource = "def main() -> None:\n    b: int = len([2])\n    print(b)\n";
        var compileResult = api.Compile(compileSource);
        Assert.True(compileResult.Success,
            $"Compile after analyze failed: {string.Join("; ", compileResult.Diagnostics.Select(d => d.Message))}");

        var analyzeSource2 = "def main() -> None:\n    c: str = str(42)\n    print(c)\n";
        var analyzeResult2 = api.Analyze(analyzeSource2);
        Assert.True(analyzeResult2.Success,
            $"Second analyze failed: {string.Join("; ", analyzeResult2.Diagnostics.Select(d => d.Message))}");

        _output.WriteLine("Analyze → Compile → Analyze through one CompilerApi: no exception");
    }

    #endregion

    #region Dictionary key sets across the handler-query / applied-text request sequence (P54, #2250)

    /// <summary>
    /// The P47 stems (#2250 cells 2–3, issuecomment-6120931669) whose Extract-method output moved with what the same
    /// <see cref="CompilerApi"/> had served before; each is run as its <c>identity</c> and lone-<c>\r</c> twin.
    /// </summary>
    private static readonly string[] P47Stems =
    {
        "fstrings/format_iformattable_class_1988",
        "fstrings/format_no_dunder_format_refused_1988",
        "deferred_overload_annotated_pin_1589",
    };

    /// <summary>
    /// KnownRed rows (P54 Phase 1 convention): the instance key sets of <see cref="MasterRegistryKeySets"/> measured to
    /// GROW on a second, identical pass of the request sequence after a full warm-up — keyed by set path, each citing
    /// #2250 with its measured reproduction. A row asserts the growth reproduces (the Phase 2 task 3 freeze turns it
    /// red as stale: delete the row); a set that grows with no row is red.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownGrowingKeySets = new Dictionary<string, string>(StringComparer.Ordinal)
    {
    };

    /// <summary>
    /// KnownRed rows: master symbol properties the request sequence WRITES (symbol path + property, as the
    /// <see cref="RegistrySnapshot"/> names them). Deterministic — each asserts the write reproduces. This is the #2250
    /// cell-2 seam: analysing a <see cref="WarmInstanceTriggers"/> text (a builtin's name defined inside a <c>try:</c>
    /// body) rewrites the master builtin's <c>Parameters</c>, and every later document reads the user signature.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownWrittenMasterProperties = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["BuiltinRegistry._functions[][]:len.Parameters"] = "#2250 cell 2: wrap-try-shadowing-len rewrites len's parameter to int — 1/1 @ 486c9a88a; drains in Phase 2 task 3",
        ["BuiltinRegistry._functions[][]:format.Parameters"] = "#2250 cell 2: wrap-try-method-format rewrites format's parameters to (Formatter, int32) — 1/1 @ 486c9a88a; drains in Phase 2 task 3",
    };

    /// <summary>
    /// #2250 Design Decision 4: after ONE full pass of the P47 request sequence — LSP-route analysis
    /// (<see cref="CompilerApi.AnalyzeDocument"/> with <see cref="CompilerOptionsFactory.ForLsp"/>, a fresh document path
    /// per open, as the workspace does), the semantic queries a code-action/hover handler makes on that analysis
    /// (<see cref="RunHandlerQueries"/>), the analysis of the Extract-method APPLIED text, and the path-less source
    /// analysis the P47 sweep's oracle ran — the master registries' dictionary key sets and every master symbol property
    /// must be a fixed point: a second identical pass may not add a key or write a property. Between every step of the
    /// second pass the first diverging key is printed (the seam measurement for cells 2–3).
    /// </summary>
    [Fact]
    public void MasterKeySets_AreAFixedPointAfterWarmUp_AcrossAnalyzeHandlerQueriesAndAppliedTextAnalyze()
    {
        var api = new CompilerApi(null, StdlibReferencePaths());
        var master = api.PrimeAnalysisContextForTests();
        var baseline = MasterRegistryKeySets.Take(api);
        Assert.True(baseline.InstanceKeyCount >= 50,
            $"the key-set snapshot saw only {baseline.InstanceKeyCount} entries in the master's dictionaries — it is not "
            + "reading the registry and every comparison below would pass vacuously");

        // Symbol properties are never a target, warm or not (#1633): compared from the baseline, before the first pass.
        var symbols = new RegistrySnapshot(api);
        symbols.AssertNotVacuous();
        var programs = RequestSequencePrograms();
        foreach (var (label, step) in RequestSequence(api, programs, pass: 1))
            step();
        AssertSameMaster(api, master, "the warm-up pass");
        var warm = MasterRegistryKeySets.Take(api);
        _output.WriteLine($"warm-up pass grew the masters:\n{baseline.Describe(warm)}");

        var previous = warm;
        var steps = 0;
        foreach (var (label, step) in RequestSequence(api, programs, pass: 2))
        {
            step();
            var now = MasterRegistryKeySets.Take(api);
            var first = previous.InstanceDiffTo(now);
            _output.WriteLine($"{label}: {(first.Count == 0 ? "no instance key-set change" : first[0].ToString())}");
            previous = now;
            steps++;
        }

        AssertSameMaster(api, master, "the second pass");
        Assert.True(steps >= 4 * programs.Count, $"the second pass ran only {steps} steps");
        var grown = warm.InstanceDiffTo(previous);
        _output.WriteLine($"second pass, instance sets:\n{warm.Describe(previous)}");
        var mutations = symbols.FindMutations(api);
        _output.WriteLine($"both passes, master symbol properties: {mutations.Count} written, "
            + $"{symbols.AppearedSinceBaseline} symbols appeared\n{Format(mutations)}");

        var problems = new List<string>();
        var written = mutations.Select(m => $"{m.SymbolPath}.{m.Property}").ToHashSet(StringComparer.Ordinal);
        foreach (var property in written.Where(p => !KnownWrittenMasterProperties.ContainsKey(p)))
            problems.Add($"{property} — a master symbol property was written (no KnownRed row):\n{Format(mutations)}");
        foreach (var (property, row) in KnownWrittenMasterProperties)
        {
            if (!written.Contains(property))
                problems.Add($"{property} is no longer written — stale KnownRed row ({row}): delete it");
        }

        foreach (var divergence in grown)
        {
            if (!KnownGrowingKeySets.ContainsKey(divergence.Path))
                problems.Add($"{divergence} — a master dictionary grew on a pass that repeats one it has already served (no KnownRed row)");
        }

        foreach (var (path, row) in KnownGrowingKeySets)
        {
            if (grown.All(d => d.Path != path))
                problems.Add($"{path} no longer grows on the second pass — stale KnownRed row ({row}): delete it");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>
    /// Positive control for the key-set instrument: a deliberate write to one master dictionary
    /// (<c>BuiltinRegistry._interfaceSymbols</c>, made here by reflection) must be named by the comparison — and the
    /// comparison must be clean again once it is undone. A missing field is a <see cref="MissingFieldException"/> from
    /// <see cref="MasterRegistryKeySets.Take(CompilerApi)"/>, so a rename is red too.
    /// </summary>
    [Fact]
    public void KeySetSnapshot_DetectsAWriteToAMasterDictionary()
    {
        var api = new CompilerApi(null, StdlibReferencePaths());
        var master = api.PrimeAnalysisContextForTests();
        var before = MasterRegistryKeySets.Take(api);
        var field = typeof(BuiltinRegistry).GetField("_interfaceSymbols", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(BuiltinRegistry), "_interfaceSymbols");
        var table = (Dictionary<Type, TypeSymbol>)field.GetValue(master)!;
        var key = typeof(IKeySetProbe);
        Assert.False(table.ContainsKey(key));
        table[key] = new TypeSymbol { Name = "IKeySetProbe", Kind = SymbolKind.Type };
        try
        {
            var after = MasterRegistryKeySets.Take(api);
            var divergences = before.InstanceDiffTo(after);
            _output.WriteLine(before.Describe(after));
            Assert.Contains(divergences, d => d.Path == "BuiltinRegistry._interfaceSymbols"
                && d.Added.Any(k => k.StartsWith(key.FullName!, StringComparison.Ordinal)));
            Assert.StartsWith("BuiltinRegistry._interfaceSymbols", before.FirstDivergence(after), StringComparison.Ordinal);
        }
        finally
        {
            table.Remove(key);
        }

        Assert.Empty(before.InstanceDiffTo(MasterRegistryKeySets.Take(api)));
    }

    /// <summary>
    /// KnownRed rows for <see cref="WarmInstanceTriggers_DoNotRetypeABuiltinForALaterDocument"/>: the builtin names a
    /// later document reads with a different type after a <see cref="WarmInstanceTriggers"/> text was analysed on the
    /// same instance (deterministic; the row asserts the retype reproduces — the Phase 2 task 3 freeze turns it stale).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownRetypedBuiltins = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["len"] = "#2250 cell 2: (object) -> int32 becomes (int32) -> int32 after wrap-try-shadowing-len — 1/1 @ 486c9a88a; drains in Phase 2 task 3",
        ["format"] = "#2250 cell 2: (object | None, str) -> str becomes (Formatter, int32) -> str after wrap-try-method-format — 1/1 @ 486c9a88a; drains in Phase 2 task 3",
    };

    /// <summary>
    /// The compiler-level twin of #2250 cell 2: on ONE <see cref="CompilerApi"/>, a later document's builtin identifiers
    /// must have the type they have on a fresh instance after the <see cref="WarmInstanceTriggers"/> texts — a module
    /// <c>def len</c> and a method <c>format</c> declared inside a <c>try:</c> body — were analysed. The master's key sets
    /// and symbol properties across the triggers are printed (the seam). <c>print</c> is the positive control: no trigger
    /// shadows it, so it must read the same.
    /// </summary>
    [Fact]
    public void WarmInstanceTriggers_DoNotRetypeABuiltinForALaterDocument()
    {
        const string probe = "def main() -> None:\n    xs: list[int] = [1, 2]\n    n: int32 = len(xs)\n    s: str = format(1.5, \".1f\")\n    print(s)\n";
        var builtins = new[] { "len", "format", "print" };
        var cold = BuiltinIdentifierTypes(new CompilerApi(null, StdlibReferencePaths()), probe, builtins);
        Assert.Equal(builtins.Length, cold.Count);

        var api = new CompilerApi(null, StdlibReferencePaths());
        api.PrimeAnalysisContextForTests();
        var keySets = MasterRegistryKeySets.Take(api);
        var symbols = new RegistrySnapshot(api);
        symbols.AssertNotVacuous();
        Assert.Equal(cold, BuiltinIdentifierTypes(api, probe, builtins));
        foreach (var (name, _, text) in WarmInstanceTriggers.AppliedTexts)
        {
            api.Analyze(text, CancellationToken.None);
            var now = MasterRegistryKeySets.Take(api);
            _output.WriteLine($"after {name}: key sets {keySets.FirstDivergence(now)}");
            keySets = now;
        }

        var mutations = symbols.FindMutations(api);
        _output.WriteLine($"master symbol properties written across the triggers: {mutations.Count} "
            + $"({symbols.ComparedProperties} compared over {symbols.ComparedSymbols} symbols)\n{Format(mutations)}");
        var warm = BuiltinIdentifierTypes(api, probe, builtins);
        var problems = new List<string>();
        foreach (var name in builtins)
        {
            var line = $"{name}: cold {cold[name]} / warm {warm[name]}";
            _output.WriteLine(line);
            var retyped = cold[name] != warm[name];
            if (KnownRetypedBuiltins.TryGetValue(name, out var row))
            {
                if (!retyped)
                    problems.Add($"{line} — no longer retyped: stale KnownRed row ({row}), delete it");
            }
            else if (retyped)
            {
                problems.Add($"{line} — a later document's builtin was retyped by an earlier analysis (no KnownRed row)");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The effective type of each named builtin identifier in <paramref name="source"/>, analysed on <paramref name="api"/>.</summary>
    private static Dictionary<string, string> BuiltinIdentifierTypes(CompilerApi api, string source, string[] names)
    {
        var result = api.Analyze(source, CancellationToken.None);
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        var stack = new Stack<SAst.Node>();
        stack.Push(result.Ast ?? throw new InvalidOperationException("the probe did not parse"));
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is SAst.Identifier id && names.Contains(id.Name))
                types[id.Name] = result.SemanticQuery?.GetEffectiveType(id)?.GetDisplayName() ?? "<none>";
            foreach (var child in node.GetChildNodes())
            {
                if (child is not null)
                    stack.Push(child);
            }
        }

        return types;
    }

    /// <summary>A CLR interface no builtin implements: the positive control's key.</summary>
    private interface IKeySetProbe
    {
    }

    private static void AssertSameMaster(CompilerApi api, BuiltinRegistry master, string what)
        => Assert.True(ReferenceEquals(master, api.CachedBuiltinsForTests),
            $"{what} replaced the cached master registry; the key sets would describe a registry nobody uses");

    /// <summary>The Core and Stdlib assemblies beside the test assembly — the server's default reference pair.</summary>
    private static string[] StdlibReferencePaths()
    {
        var paths = new[] { "Sharpy.Core.dll", "Sharpy.Stdlib.dll" }.Select(n => Path.Combine(AppContext.BaseDirectory, n)).ToArray();
        foreach (var path in paths)
            Assert.True(File.Exists(path), $"{path} is missing beside the test assembly");
        return paths;
    }

    /// <summary>
    /// (name, source, applied) for each P47 stem twin, each <see cref="WarmInstanceTriggers"/> program (its applied
    /// text is the Wrap-in-try text measured to move the P47 stems on one instance) and each corpus program. A program
    /// with no applied text re-analyses its edited source (one trailing line break). Extract method's own applied text
    /// for the P47 stems is <c>WarmInstanceParityTests</c>' (Lsp.Tests) step — it needs the LSP handler.
    /// </summary>
    private static List<(string Name, string Source, string? Applied)> RequestSequencePrograms()
    {
        var programs = new List<(string, string, string?)>();
        foreach (var stem in P47Stems)
        {
            var source = File.ReadAllText(Path.Combine(FixtureRoots.CompilerTests.Path, stem + ".spy"));
            programs.Add((stem, source, null));
            programs.Add((stem + "#cr", FormatterTwins.Cr(source), null));
        }

        foreach (var (name, fixture, applied) in WarmInstanceTriggers.AppliedTexts)
            programs.Add((name, File.ReadAllText(Path.Combine(FixtureRoots.CompilerTests.Path, fixture + ".spy")), applied));
        foreach (var (name, source) in Corpus)
            programs.Add((name, source, null));
        return programs;
    }

    private static long s_documentId;

    /// <summary>
    /// One pass of the request sequence: per program, the LSP-route analysis, the handler queries on it, the applied
    /// text's analysis and the oracle's path-less source analysis — each a labelled step.
    /// </summary>
    private static IEnumerable<(string Label, Action Step)> RequestSequence(CompilerApi api, List<(string Name, string Source, string? Applied)> programs, int pass)
    {
        foreach (var (name, source, applied) in programs)
        {
            SemanticResult? analysis = null;
            yield return ($"pass {pass} {name} analyze", () => analysis = api.AnalyzeDocument(
                source, $"/master-registry-keysets/doc{Interlocked.Increment(ref s_documentId)}.spy",
                CompilerOptionsFactory.ForLsp(), null, CancellationToken.None));
            yield return ($"pass {pass} {name} handler queries", () => RunHandlerQueries(analysis!));
            yield return ($"pass {pass} {name} applied-text analyze", () => api.Analyze(applied ?? source + "\n", CancellationToken.None));
            yield return ($"pass {pass} {name} oracle analyze", () => api.Analyze(source, CancellationToken.None));
        }
    }

    /// <summary>
    /// The semantic queries the LSP handlers make on an analysis (hover, Extract method's parameter typing, find
    /// references, signature help): every identifier's expression/effective type, symbol and references, every
    /// call's target, routing and inferred type arguments, every annotation's type, every declaration's symbol — and
    /// each type's display name, which is what Extract method spells.
    /// </summary>
    private static void RunHandlerQueries(SemanticResult analysis)
    {
        var query = analysis.SemanticQuery;
        if (analysis.Ast is null || query is null)
            return;
        var stack = new Stack<SAst.Node>();
        stack.Push(analysis.Ast);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            switch (node)
            {
                case SAst.Identifier id:
                    _ = query.GetExpressionType(id)?.GetDisplayName();
                    _ = query.GetEffectiveType(id)?.GetDisplayName();
                    if (query.GetIdentifierSymbol(id) is { } symbol)
                        _ = query.GetReferences(symbol).Count;
                    break;
                case SAst.FunctionCall call:
                    _ = query.GetCallTarget(call)?.Name;
                    _ = query.GetCalleeRouting(call);
                    _ = query.GetInferredTypeArguments(call)?.Count;
                    _ = query.GetExpressionType(call)?.GetDisplayName();
                    break;
                case SAst.TypeAnnotation annotation:
                    _ = query.GetTypeAnnotation(annotation)?.GetDisplayName();
                    break;
                case SAst.VariableDeclaration declaration:
                    _ = query.GetDeclarationSymbol(declaration)?.Name;
                    break;
                case SAst.FunctionDef definition:
                    _ = query.GetFunctionDeclarationSymbol(definition)?.Name;
                    break;
                case SAst.Expression expression:
                    _ = query.GetEffectiveType(expression)?.GetDisplayName();
                    break;
            }

            foreach (var child in node.GetChildNodes())
            {
                if (child is not null)
                    stack.Push(child);
            }
        }
    }

    #endregion

    #region Comparison plumbing

    private void AssertUnchanged(CompilerApi api, BuiltinRegistry master, RegistrySnapshot snapshot, string entryPoint)
    {
        // If the entry point rebuilt the analysis cache, a later comparison would be comparing a
        // registry nobody uses — the guard would pass while the real master went unwatched.
        Assert.True(ReferenceEquals(master, api.CachedBuiltinsForTests),
            $"{entryPoint} replaced the cached master registry; the snapshot no longer describes "
            + "the registry in use and the comparison below would be vacuous");

        var mutations = snapshot.FindMutations(api);
        Assert.True(mutations.Count == 0,
            $"{entryPoint} mutated {mutations.Count} master symbol propert"
            + $"{(mutations.Count == 1 ? "y" : "ies")}:\n{Format(mutations)}");

        _output.WriteLine($"{entryPoint}: {snapshot.ComparedProperties} properties over "
            + $"{snapshot.ComparedSymbols} symbols unchanged");
    }

    private static string Format(IReadOnlyList<Mutation> mutations)
        => string.Join("\n", mutations.Take(25).Select(m =>
            $"  {m.SymbolPath} ({m.SymbolType}).{m.Property}: {m.Before} -> {m.After}"));

    private void RunQuietly(string label, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            // A corpus program that fails to analyze still exercised the write paths under test;
            // what it must not do is leave a mark on the master. The outcome is reported so a
            // corpus that silently stopped compiling anything is visible.
            _output.WriteLine($"{label} threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private sealed record Mutation(string SymbolPath, string SymbolType, string Property, string Before, string After);

    /// <summary>
    /// A reflection snapshot of every <see cref="Symbol"/> reachable from a
    /// <see cref="CompilerApi"/>'s analysis cache: the <see cref="BuiltinRegistry"/>, the
    /// <see cref="ModuleRegistry"/>, and — through their private fields — the discovery and
    /// CLR-bridge symbol caches they share.
    /// </summary>
    private sealed class RegistrySnapshot
    {
        private readonly Dictionary<Symbol, int> _ids = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Symbol, string> _paths = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<(int SymbolId, string Property), string> _digests = new();

        public int ComparedSymbols { get; private set; }
        public int ComparedProperties { get; private set; }

        public RegistrySnapshot(CompilerApi api)
        {
            foreach (var (symbol, path) in Walk(api))
            {
                var id = IdOf(symbol, path);
                foreach (var (property, digest) in DigestProperties(symbol))
                    _digests[(id, property)] = digest;
            }

            ComparedSymbols = _ids.Count;
            ComparedProperties = _digests.Count;
        }

        public string Describe(string label)
            => $"{label}: {_ids.Count} symbols, {_digests.Count} properties snapshotted";

        /// <summary>
        /// The enumeration must actually reach the registry. A guard that walks an empty graph
        /// reports "no mutations" forever, so the floors below fail loudly instead.
        /// </summary>
        public void AssertNotVacuous()
        {
            Assert.True(_ids.Count >= 50,
                $"the walk reached only {_ids.Count} symbols — the enumeration is not seeing the "
                + "master registry and every comparison would pass vacuously");
            Assert.True(_digests.Count >= 500,
                $"the walk snapshotted only {_digests.Count} properties — the property enumeration "
                + "is not seeing symbol state and every comparison would pass vacuously");
        }

        public IReadOnlyList<Mutation> FindMutations(CompilerApi api)
        {
            var mutations = new List<Mutation>();
            var comparedSymbols = 0;
            var comparedProperties = 0;
            var appeared = 0;

            foreach (var (symbol, path) in Walk(api))
            {
                if (!_ids.TryGetValue(symbol, out var id))
                {
                    // A symbol that did not exist at baseline: a lazily filled shared cache, not a
                    // write to an existing symbol. Counted, not failed — the cache-fill hazard is
                    // #1672 H1.4's, and failing on it here would make this guard non-deterministic.
                    appeared++;
                    continue;
                }

                comparedSymbols++;
                foreach (var (property, digest) in DigestProperties(symbol))
                {
                    if (!_digests.TryGetValue((id, property), out var before))
                        continue;

                    comparedProperties++;
                    if (!string.Equals(before, digest, StringComparison.Ordinal))
                        mutations.Add(new Mutation(path, symbol.GetType().Name, property, before, digest));
                }
            }

            ComparedSymbols = comparedSymbols;
            ComparedProperties = comparedProperties;
            AppearedSinceBaseline = appeared;
            return mutations;
        }

        public int AppearedSinceBaseline { get; private set; }

        private int IdOf(Symbol symbol, string path)
        {
            if (_ids.TryGetValue(symbol, out var existing))
                return existing;

            var id = _ids.Count;
            _ids[symbol] = id;
            _paths[symbol] = path;
            return id;
        }

        #region Reachability

        /// <summary>
        /// Every symbol reachable from the analysis cache, with a human-readable path. Walks the
        /// registries' own instance fields (public and private) so the discovery and bridge caches
        /// they hold — which vend the same symbol instances — are covered without naming them.
        /// </summary>
        private IEnumerable<(Symbol Symbol, string Path)> Walk(CompilerApi api)
        {
            var roots = new List<(object Root, string Path)>();
            if (api.CachedBuiltinsForTests is { } builtins)
                roots.Add((builtins, "BuiltinRegistry"));
            if (api.CachedModuleRegistryForTests is { } modules)
                roots.Add((modules, "ModuleRegistry"));

            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var queue = new Queue<(object Node, string Path, int Depth)>();
            foreach (var (root, path) in roots)
                queue.Enqueue((root, path, 0));

            while (queue.Count > 0)
            {
                var (node, path, depth) = queue.Dequeue();
                if (node is null || depth > MaxWalkDepth || !seen.Add(node))
                    continue;

                if (node is Symbol symbol)
                {
                    // The symbol's own name is part of its path: a failure message that says
                    // "BuiltinRegistry._functions[]" names a container, not the symbol that was
                    // written, and the whole point of the guard is to name the writer's target.
                    var symbolPath = $"{path}:{(string.IsNullOrEmpty(symbol.Name) ? "<unnamed>" : symbol.Name)}";
                    yield return (symbol, symbolPath);

                    // Symbols reach further symbols through their own properties (Methods, Fields,
                    // Constructors, Interfaces, Properties, TypeParameters, BaseType, ...).
                    foreach (var (child, childPath) in SymbolChildren(symbol, symbolPath))
                        queue.Enqueue((child, childPath, depth + 1));
                    continue;
                }

                foreach (var (child, childPath) in ContainerChildren(node, path))
                    queue.Enqueue((child, childPath, depth + 1));
            }
        }

        private const int MaxWalkDepth = 12;

        private static IEnumerable<(object Child, string Path)> SymbolChildren(Symbol symbol, string path)
        {
            foreach (var property in PropertiesOf(symbol.GetType()))
            {
                object? value;
                try
                { value = property.GetValue(symbol); }
                catch { continue; }
                if (value is null || value is string)
                    continue;

                foreach (var child in Unwrap(value))
                    yield return (child, $"{path}.{property.Name}");
            }
        }

        private static IEnumerable<(object Child, string Path)> ContainerChildren(object node, string path)
        {
            if (node is IEnumerable enumerable and not string)
            {
                // A dictionary VALUE may be null (the master's `_clrTypeCache` caches a name that resolved to no
                // type as null, #2250's key-set walk) — nothing to walk into, and Unwrap needs an object.
                foreach (var element in Enumerate(enumerable))
                {
                    if (element is null)
                        continue;
                    foreach (var child in Unwrap(element))
                        yield return (child, $"{path}[]");
                }

                yield break;
            }

            if (!IsCompilerOwned(node.GetType()))
                yield break;

            foreach (var field in FieldsOf(node.GetType()))
            {
                object? value;
                try
                { value = field.GetValue(node); }
                catch { continue; }
                if (value is null || value is string)
                    continue;

                foreach (var child in Unwrap(value))
                    yield return (child, $"{path}.{field.Name}");
            }
        }

        /// <summary>
        /// Values worth walking into: symbols, compiler-owned objects, and collections of either.
        /// A <see cref="Lazy{T}"/> is unwrapped only when it is already created — forcing it would
        /// make the guard itself populate the caches it is watching.
        /// </summary>
        private static IEnumerable<object> Unwrap(object value)
        {
            var type = value.GetType();

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Lazy<>))
            {
                var isCreated = (bool)type.GetProperty("IsValueCreated")!.GetValue(value)!;
                if (!isCreated)
                    yield break;
                var inner = type.GetProperty("Value")!.GetValue(value);
                if (inner is not null)
                    yield return inner;
                yield break;
            }

            if (value is Symbol || value is IEnumerable || IsCompilerOwned(type))
                yield return value;
        }

        private static IEnumerable<object?> Enumerate(IEnumerable enumerable)
        {
            IEnumerator enumerator;
            try
            { enumerator = enumerable.GetEnumerator(); }
            catch { yield break; }

            while (true)
            {
                object? current;
                try
                {
                    if (!enumerator.MoveNext())
                        break;
                    current = enumerator.Current;
                }
                catch { break; }

                if (current is null)
                    continue;

                // Dictionary entries surface as KeyValuePair<,>; both halves may hold symbols.
                var currentType = current.GetType();
                if (currentType.IsGenericType
                    && currentType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    yield return currentType.GetProperty("Key")!.GetValue(current);
                    yield return currentType.GetProperty("Value")!.GetValue(current);
                    continue;
                }

                yield return current;
            }
        }

        private static bool IsCompilerOwned(Type type)
            => type.Namespace?.StartsWith("Sharpy.Compiler", StringComparison.Ordinal) == true;

        #endregion

        #region Digests

        /// <summary>
        /// One digest per readable property of the symbol's RUNTIME type. Every property is
        /// digested, not just those with a settable setter: a record's <c>init</c> collection
        /// (<c>Interfaces</c>, <c>Methods</c>, <c>OperatorMethods</c>) has no setter at all and is
        /// mutated in place by <c>MaterializeInheritance</c> — the exact write this guard exists
        /// to catch.
        /// </summary>
        private IEnumerable<(string Property, string Digest)> DigestProperties(Symbol symbol)
        {
            foreach (var property in PropertiesOf(symbol.GetType()))
            {
                string digest;
                try
                { digest = Digest(property.GetValue(symbol), 0); }
                catch (Exception ex) { digest = $"<threw {ex.GetType().Name}>"; }
                yield return (property.Name, digest);
            }
        }

        private string Digest(object? value, int depth)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string s:
                    return $"\"{s}\"";
                case bool b:
                    return b ? "true" : "false";
                case Type t:
                    return $"type:{t.FullName}";
                case Symbol symbol:
                    // Identity, not content: the symbol is snapshotted as its own node, and its id
                    // is stable across passes because the id map persists.
                    return $"sym#{IdOf(symbol, "<referenced>")}";
                case Enum e:
                    return e.ToString();
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            if (depth >= MaxDigestDepth)
                return $"<{value.GetType().Name}>";

            if (value is IEnumerable enumerable)
            {
                var parts = new List<string>();
                foreach (var element in Enumerate(enumerable))
                    parts.Add(Digest(element, depth + 1));

                // Order matters for a list; for a set or dictionary it is an implementation
                // detail that a rehash can change, so those are compared as multisets.
                if (value is not IList && value is not Array)
                    parts.Sort(StringComparer.Ordinal);

                return $"[{string.Join(",", parts)}]";
            }

            var type = value.GetType();
            if (IsCompilerOwned(type))
            {
                var parts = PropertiesOf(type).Select(p =>
                {
                    string inner;
                    try
                    { inner = Digest(p.GetValue(value), depth + 1); }
                    catch (Exception ex) { inner = $"<threw {ex.GetType().Name}>"; }
                    return $"{p.Name}={inner}";
                });
                return $"{type.Name}{{{string.Join(",", parts)}}}";
            }

            return $"<{type.Name}>";
        }

        private const int MaxDigestDepth = 4;

        #endregion

        #region Member caches

        private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new();
        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new();

        private static PropertyInfo[] PropertiesOf(Type type)
        {
            lock (PropertyCache)
            {
                if (PropertyCache.TryGetValue(type, out var cached))
                    return cached;

                var properties = type
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .ToArray();
                PropertyCache[type] = properties;
                return properties;
            }
        }

        private static FieldInfo[] FieldsOf(Type type)
        {
            lock (FieldCache)
            {
                if (FieldCache.TryGetValue(type, out var cached))
                    return cached;

                var fields = new List<FieldInfo>();
                for (var current = type; current != null && IsCompilerOwned(current); current = current.BaseType)
                {
                    fields.AddRange(current.GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
                }

                var ordered = fields.OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
                FieldCache[type] = ordered;
                return ordered;
            }
        }

        #endregion
    }

    #endregion

}
