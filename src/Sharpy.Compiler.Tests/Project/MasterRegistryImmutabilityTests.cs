using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using Xunit.Abstractions;

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
                foreach (var element in Enumerate(enumerable))
                    foreach (var child in Unwrap(element))
                        yield return (child, $"{path}[]");
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
