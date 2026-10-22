using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// Every stdlib module global that is WRITTEN after its initializer is a literal roster row naming
/// its CPython model and its synchronization (#2250, P54 Design Decision 5).
///
/// <para>CPython's module globals are process-wide and every call that reads or writes one is safe
/// from several threads (the GIL makes each C-level call atomic). In Sharpy a module global is a
/// <c>static</c> field, so one written after its initializer is shared state that several threads
/// can race on. The roster is decided by a census, not by memory: a Roslyn scan over
/// <c>src/Sharpy.Stdlib/**/*.cs</c> (the generated spy C# included; <c>bin</c>, <c>obj</c> and the
/// two directories the project file removes from compilation, <c>spy/</c> and <c>modules/</c>,
/// excluded), bound with a semantic model built from the Stdlib's own references.</para>
///
/// <para><b>The predicate (syntactic, bound).</b> A static, non-const field declared in the scanned
/// sources is <i>written</i> when it is the target of a simple, compound or <c>??=</c> assignment
/// (tuple-deconstruction targets included), of <c>++</c>/<c>--</c>, of an element assignment
/// (<c>F[k] = v</c>), or is passed by <c>ref</c>/<c>out</c> to a callee that may write it — anywhere
/// outside the field's own initializer — or is the receiver of an invocation whose member name is
/// in the literal <see cref="MutatorRoster"/> (<c>F.Add(...)</c>, <c>F?.Clear()</c>,
/// <c>F[k].Add(...)</c>). The target must BIND to the field symbol, so a local or an instance field
/// that shares the name is not a write. The ~60 spy "constants" (<c>math.pi</c>,
/// <c>socket.AF_INET</c>, …) are emitted as <c>public static</c> non-readonly fields and are
/// excluded by this predicate — never written after their initializer — not by a list
/// (<see cref="NegativeControl_ASpyConstantIsScannedAndNotWritten"/>).</para>
///
/// <para>A written static without a row is red (a new process-wide global needs its CPython model
/// and synchronization named); a row the census no longer finds is red (stale — drain it). The
/// totality is anchored to the literal <see cref="ExpectedWrittenGlobals"/>, counted at landing
/// @ 486c9a88a (the plan's six plus two the census found) plus the two threading globals #2261 part 2
/// added, not derived from the scan. Each row is exercised at 1/2/8 threads by
/// <see cref="StdlibModuleGlobalConcurrencyTests"/>.</para>
/// </summary>
public class StdlibModuleGlobalRosterTests
{
    /// <summary>One written module global: its field, declaring file, CPython model, current synchronization, and the issue whose Phase 3 cures it (null: already safe).</summary>
    internal sealed record GlobalRow(string Field, string File, string CPythonModel, string Synchronization, string? CuredBy);

    private const string ProcessWideUnderTheGil =
        "process-wide module global, thread-safe under the GIL";

    /// <summary>The literal roster. Each row's field key is "&lt;containing type&gt;.&lt;field&gt;".</summary>
    internal static readonly GlobalRow[] Roster =
    {
        // random_module.spy:5 → generated Random/Random.cs:18; seed() swaps the instance.
        // CPython 3.12 (verified): random.__doc__ "...and is, therefore, threadsafe."
        new("Sharpy.RandomModule.RandomModuleModule._Random", "src/Sharpy.Stdlib/Random/Random.cs",
            ProcessWideUnderTheGil + " (random.__doc__: \"threadsafe\")",
            "one module lock (random_module.spy `_lock`, a threading.Lock) around every read and write", "#2250"),
        // socket_module.spy → generated Socket/SocketModule.cs:38; setdefaulttimeout() writes it.
        new("Sharpy.SocketModule.SocketModuleModule._DefaultTimeout", "src/Sharpy.Stdlib/Socket/SocketModule.cs",
            ProcessWideUnderTheGil + " (one pointer write)",
            "one module lock (socket_module.spy `_lock`, a threading.Lock) around every read and write", "#2250"),
        // Calendar/CalendarConstants.cs:37; setfirstweekday() writes it. An int write is atomic.
        new("Sharpy.CalendarModule._moduleFirstweekday", "src/Sharpy.Stdlib/Calendar/CalendarConstants.cs",
            ProcessWideUnderTheGil,
            "none — an int, atomic by the CLR memory model", null),
        // Xml/XmlFunctions.cs:185; register_namespace() writes it; nothing in production reads it @ 486c9a88a.
        new("Sharpy.Xml._registeredNamespaces", "src/Sharpy.Stdlib/Xml/XmlFunctions.cs",
            ProcessWideUnderTheGil + " (ElementTree._namespace_map, a dict)",
            "lock (Xml.RegisteredNamespacesLock) around every read and write", "#2250"),
        // Logging/LoggingModule.cs:27; getLogger() adds to it.
        new("Sharpy.Logging._loggers", "src/Sharpy.Stdlib/Logging/LoggingModule.cs",
            ProcessWideUnderTheGil + " (Manager.loggerDict)",
            "ConcurrentDictionary", null),
        // Numpy/NumpyRandom.cs:22; seed() writes it.
        new("Sharpy.NumpyRandom._rng", "src/Sharpy.Stdlib/Numpy/NumpyRandom.cs",
            "process-wide global RandomState (numpy 2.5.1, verified: a seed on one thread is seen by the next draw on another)",
            "one process-wide System.Random; lock (NumpyRandom.RngLock) around every read and write", "#2250"),

        // ---- Found by this census beyond the plan's six (findings-ledger rows, P54 Phase 1) ----

        // Http/HTTPStatus.cs:20; written by the PRIVATE instance constructor (:31), which only the
        // type's own 62 `public static readonly HTTPStatus X = new HTTPStatus(...)` initializers call,
        // so every write runs inside the CLR type initializer — complete before any reader.
        // CPython 3.12 (verified): HTTPStatus is an IntEnum, len(_value2member_map_) == 62.
        new("Sharpy.HTTPStatus._valueMap", "src/Sharpy.Stdlib/Http/HTTPStatus.cs",
            "process-wide class attribute built once at class creation (IntEnum _value2member_map_)",
            "the CLR type initializer — written only during HTTPStatus's static initialization", null),
        // TypedLoadContract.cs:36; FieldDescriptorsOf (:226) GetOrAdds per target Type (json/yaml typed doors).
        new("Sharpy.TypedLoadContract.FieldCache", "src/Sharpy.Stdlib/TypedLoadContract.cs",
            "no CPython counterpart — an implementation cache whose value is a function of its key (the Type)",
            "ConcurrentDictionary", null),

        // ---- Added by #2261 part 2 (threading's process-wide state) ----

        // Threading/ThreadRegistry.cs; every unnamed Thread() and every dummy current_thread() draws a number.
        // CPython 3.12 (verified): threading._counter = itertools.count(1), shared by Thread-N and Dummy-N.
        new("Sharpy.ThreadRegistry._counter", "src/Sharpy.Stdlib/Threading/ThreadRegistry.cs",
            ProcessWideUnderTheGil + " (threading._counter, an itertools.count)",
            "Interlocked.Increment — the only write", null),
        // Threading/ThreadRegistry.cs; start() adds, the thread's own body removes, current_thread() adds a dummy.
        // CPython 3.12 (verified): threading._active / _limbo, dicts read and written under _active_limbo_lock.
        new("Sharpy.ThreadRegistry._active", "src/Sharpy.Stdlib/Threading/ThreadRegistry.cs",
            "process-wide module global (threading._active and _limbo, under _active_limbo_lock)",
            "lock (ThreadRegistry.ActiveLock) around every read and write", null),
    };

    /// <summary>
    /// Written globals @ 486c9a88a, counted at landing — the totality anchor: the plan's six
    /// (P54 cells 6–11) plus the two this census found (HTTPStatus._valueMap, TypedLoadContract.FieldCache)
    /// — plus the two #2261 part 2 added (ThreadRegistry._counter, ThreadRegistry._active).
    /// </summary>
    private const int ExpectedWrittenGlobals = 10;

    /// <summary>
    /// Invoked member names that mutate their receiver — literal: the .NET collection mutators and
    /// the Sharpy collection mutators (Python names in their C# spelling).
    /// </summary>
    internal static readonly HashSet<string> MutatorRoster = new(StringComparer.Ordinal)
    {
        "Add", "AddRange", "Remove", "RemoveAt", "RemoveAll", "RemoveRange", "Clear",
        "TryAdd", "TryRemove", "TryUpdate", "AddOrUpdate", "GetOrAdd",
        "Enqueue", "Dequeue", "TryDequeue", "Push", "Pop", "TryPop",
        "Insert", "InsertRange", "Set", "SetValue", "SetAll", "Sort", "Reverse",
        "UnionWith", "ExceptWith", "IntersectWith", "SymmetricExceptWith",
        "Append", "Extend", "Update", "Setdefault", "Discard", "Popitem",
    };

    private readonly ITestOutputHelper _output;

    public StdlibModuleGlobalRosterTests(ITestOutputHelper output) => _output = output;

    private static readonly Lazy<Census> StdlibCensus = new(() => Census.Run(StdlibCompilation()));

    [Fact]
    public void TheSemanticModelBinds_TheStdlibCompilationHasNoErrors()
    {
        var census = StdlibCensus.Value;

        // A degraded model binds nothing and the census passes vacuously — so the compilation must
        // be the Stdlib's own, error-free (references + preprocessor symbols of the net10.0 build).
        Assert.True(census.Files > 200, $"the scan must read the Stdlib sources; read {census.Files} file(s)");
        Assert.True(census.Errors.Count == 0,
            $"{census.Errors.Count} compilation error(s) — the semantic model is degraded:\n  "
            + string.Join("\n  ", census.Errors.Take(20)));
        Assert.True(census.UnboundTargets.Count == 0,
            "assignment targets / mutator receivers that bind to no symbol:\n  " + string.Join("\n  ", census.UnboundTargets.Take(20)));
    }

    [Fact]
    public void EveryWrittenStatic_IsARosterRow_AndEveryRowIsWritten()
    {
        var census = StdlibCensus.Value;
        foreach (var (field, writes) in census.Written.OrderBy(w => w.Key, StringComparer.Ordinal))
            _output.WriteLine($"{field} ({census.Declared[field]}): " + string.Join("; ", writes));

        var rostered = Roster.Select(r => r.Field).ToHashSet(StringComparer.Ordinal);
        var unlisted = census.Written.Keys.Where(f => !rostered.Contains(f)).OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => $"  unlisted {f} declared {census.Declared[f]}, written at {string.Join("; ", census.Written[f])}");
        var stale = Roster.Where(r => !census.Written.ContainsKey(r.Field))
            .Select(r => $"  stale row {r.Field} — the census no longer finds a write (drain the row)");
        var problems = unlisted.Concat(stale).ToList();

        Assert.True(problems.Count == 0,
            "the stdlib's written module globals differ from the roster:\n" + string.Join("\n", problems)
            + "\nA written static is a process-wide global: add a row naming its CPython model and its "
            + "synchronization (and a StdlibModuleGlobalConcurrencyTests exerciser), or make it init-once.");
    }

    [Fact]
    public void Totality_IsTheLiteralCount()
    {
        Assert.Equal(ExpectedWrittenGlobals, Roster.Length);
        Assert.Equal(ExpectedWrittenGlobals, StdlibCensus.Value.Written.Count);
    }

    [Fact]
    public void EveryRow_NamesItsModelSynchronizationAndDeclaringFile()
    {
        var census = StdlibCensus.Value;
        foreach (var row in Roster)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.CPythonModel), row.Field);
            Assert.False(string.IsNullOrWhiteSpace(row.Synchronization), row.Field);
            Assert.True(census.Declared.TryGetValue(row.Field, out var declared), $"{row.Field} is not declared in the scanned sources");
            Assert.StartsWith(row.File + ":", declared, StringComparison.Ordinal);
        }

        // The rows Phase 3 of #2250 cures — written out, so a row cannot silently drop its citation.
        Assert.Equal(
            new[] { "Sharpy.NumpyRandom._rng", "Sharpy.RandomModule.RandomModuleModule._Random", "Sharpy.SocketModule.SocketModuleModule._DefaultTimeout", "Sharpy.Xml._registeredNamespaces" },
            Roster.Where(r => r.CuredBy == "#2250").Select(r => r.Field).OrderBy(f => f, StringComparer.Ordinal));
    }

    /// <summary>Positive control: the generated random module's seed() assigns <c>_Random = new …</c>.</summary>
    [Fact]
    public void PositiveControl_GeneratedRandomSeedIsAWrite()
    {
        var census = StdlibCensus.Value;
        Assert.True(census.Written.TryGetValue("Sharpy.RandomModule.RandomModuleModule._Random", out var writes),
            "the census must find random.seed()'s write to _Random");
        Assert.Contains(writes, w => w.File == "src/Sharpy.Stdlib/Random/Random.cs" && w.Member == "Seed" && w.Kind == "SimpleAssignmentExpression");
    }

    /// <summary>
    /// Negative control: a spy constant is a static NON-readonly field (as every spy module-level
    /// binding is), it is scanned, and it is not written — excluded by the predicate, not a list.
    /// </summary>
    [Fact]
    public void NegativeControl_ASpyConstantIsScannedAndNotWritten()
    {
        var census = StdlibCensus.Value;
        foreach (var constant in new[] { "Sharpy.MathModule.MathModuleModule.Pi", "Sharpy.SocketModule.SocketModuleModule.AF_INET" })
        {
            Assert.True(census.Declared.ContainsKey(constant), $"{constant} must be scanned as a static field");
            Assert.True(census.NonReadonly.Contains(constant), $"{constant} must be a non-readonly static (the spy emission) for this control to mean anything");
            Assert.False(census.Written.ContainsKey(constant), $"{constant} is init-once and must not be flagged");
        }
    }

    public static IEnumerable<object[]> WriteSnippets() => new[]
    {
        new object[] { "simple", "F = 1;" },
        new object[] { "qualified", "C.F = 1;" },
        new object[] { "compound", "F += 1;" },
        new object[] { "coalesce", "S ??= \"x\";" },
        new object[] { "increment", "F++;" },
        new object[] { "prefix-decrement", "--F;" },
        new object[] { "deconstruction", "(F, local) = (1, 2);" },
        new object[] { "element", "D[\"k\"] = 1;" },
        new object[] { "ref-argument", "System.Threading.Interlocked.Increment(ref F);" },
        new object[] { "out-argument", "D.TryGetValue(\"k\", out F);" },
        new object[] { "mutator", "D.Add(\"k\", 1);" },
        new object[] { "conditional-mutator", "L?.Clear();" },
        new object[] { "nested-mutator", "N[\"k\"].Add(1);" },
    };

    /// <summary>Every arm of the predicate is a write — an arm whose matcher broke would otherwise pass as "no writes".</summary>
    [Theory]
    [MemberData(nameof(WriteSnippets))]
    public void Synthetic_EveryPredicateArmIsAWrite(string arm, string statement)
    {
        var census = Census.Run(SyntheticCompilation($"void M() {{ int local = 0; {statement} }}"));

        Assert.True(census.Errors.Count == 0, arm + ": " + string.Join("; ", census.Errors));
        Assert.True(census.Written.Count == 1, $"{arm}: expected one written static, found [{string.Join(", ", census.Written.Keys)}]");
    }

    [Fact]
    public void Synthetic_InitializersReadsLocalsAndInstanceFieldsAreNotWrites()
    {
        var census = Census.Run(SyntheticCompilation("""
            int I;
            static readonly System.Collections.Generic.Dictionary<string, int> Init = new() { ["a"] = 1 };
            void M(C other)
            {
                int F = 0;
                F = 2;
                I = 3;
                other.I = 4;
                var d = D["k"];
                var n = D.Count;
                D.ContainsKey("k");
                System.Threading.Volatile.Read(ref S);
                L?.Contains(1);
                var copy = D.ToString();
            }
            """));

        Assert.True(census.Errors.Count == 0, string.Join("; ", census.Errors));
        Assert.True(census.Declared.Count >= 5, "control: the synthetic statics are scanned");
        Assert.True(census.Written.Count == 0, "not writes: " + string.Join(", ", census.Written.Select(w => $"{w.Key} at {string.Join("; ", w.Value)}")));
    }

    private static CSharpCompilation SyntheticCompilation(string members)
    {
        var source = $$"""
            namespace N
            {
                public class C
                {
                    public static int F;
                    public static string? S;
                    public static System.Collections.Generic.Dictionary<string, int> D = new();
                    public static System.Collections.Generic.List<int>? L = new();
                    public static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>> N = new();
                    {{members}}
                }
            }
            """;
        return CSharpCompilation.Create(
            "Synthetic",
            new[] { CSharpSyntaxTree.ParseText(source, ParseOptions, path: "src/Sharpy.Stdlib/Synthetic.cs") },
            FrameworkReferences(),
            CompilationOptions);
    }

    // ---- The Stdlib compilation ----

    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.Preview,
        preprocessorSymbols: new[] { "NET", "NET10_0", "NET10_0_OR_GREATER", "NETCOREAPP", "RELEASE" });

    private static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.DynamicallyLinkedLibrary,
        nullableContextOptions: NullableContextOptions.Enable,
        allowUnsafe: true);

    private static CSharpCompilation StdlibCompilation()
    {
        var root = SysPath.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Stdlib");
        var excluded = new[] { "bin", "obj", "spy", "modules" }
            .Select(d => SysPath.Combine(root, d) + SysPath.DirectorySeparatorChar)
            .ToArray();
        var trees = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !excluded.Any(x => f.StartsWith(x, StringComparison.Ordinal)))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => CSharpSyntaxTree.ParseText(
                File.ReadAllText(f), ParseOptions,
                path: SysPath.GetRelativePath(FixtureRoots.RepositoryRoot, f).Replace('\\', '/')))
            .ToList();

        // The Stdlib's own direct references (Sharpy.Core, MathNet.Numerics, Microsoft.Data.Sqlite,
        // Tomlyn, YamlDotNet, …) as the test host loaded them, plus the shared framework.
        var references = FrameworkReferences().ToList();
        var frameworkDir = RuntimeEnvironment.GetRuntimeDirectory();
        foreach (var name in typeof(Sharpy.NumpyRandom).Assembly.GetReferencedAssemblies())
        {
            var location = Assembly.Load(name).Location;
            if (!location.StartsWith(frameworkDir, StringComparison.Ordinal))
                references.Add(MetadataReference.CreateFromFile(location));
        }

        return CSharpCompilation.Create("Sharpy.Stdlib", trees, references, CompilationOptions);
    }

    private static IEnumerable<MetadataReference> FrameworkReferences()
        => Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
            .Where(IsManagedAssembly)
            .Select(f => MetadataReference.CreateFromFile(f));

    private static bool IsManagedAssembly(string path)
    {
        try
        {
            AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    // ---- The census ----

    internal sealed record WriteSite(string File, int Line, string Member, string Kind)
    {
        public override string ToString() => $"{File}:{Line} {Member} ({Kind})";
    }

    internal sealed class Census
    {
        public int Files { get; private init; }
        public System.Collections.Generic.List<string> Errors { get; } = new();
        public System.Collections.Generic.List<string> UnboundTargets { get; } = new();

        /// <summary>Every static non-const field declared in the scanned sources → "file:line".</summary>
        public Dictionary<string, string> Declared { get; } = new(StringComparer.Ordinal);

        /// <summary>The declared statics that are not readonly.</summary>
        public HashSet<string> NonReadonly { get; } = new(StringComparer.Ordinal);

        /// <summary>The written statics → their write sites.</summary>
        public Dictionary<string, System.Collections.Generic.List<WriteSite>> Written { get; } = new(StringComparer.Ordinal);

        public static Census Run(CSharpCompilation compilation)
        {
            var census = new Census { Files = compilation.SyntaxTrees.Count() };
            census.Errors.AddRange(compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));

            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();

                foreach (var declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                             .Where(v => v.Parent?.Parent is FieldDeclarationSyntax))
                {
                    if (model.GetDeclaredSymbol(declarator) is IFieldSymbol { IsStatic: true, IsConst: false } field)
                    {
                        census.Declared[Key(field)] = $"{tree.FilePath}:{Line(declarator)}";
                        if (!field.IsReadOnly)
                            census.NonReadonly.Add(Key(field));
                    }
                }

                foreach (var node in root.DescendantNodes())
                {
                    switch (node)
                    {
                        case AssignmentExpressionSyntax assignment:
                            foreach (var target in Targets(assignment.Left))
                                census.Record(model, target, assignment, node.Kind().ToString());
                            break;
                        case PrefixUnaryExpressionSyntax prefix
                            when prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression):
                            census.Record(model, prefix.Operand, prefix, node.Kind().ToString());
                            break;
                        case PostfixUnaryExpressionSyntax postfix
                            when postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression):
                            census.Record(model, postfix.Operand, postfix, node.Kind().ToString());
                            break;
                        case ArgumentSyntax argument
                            when argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword):
                            // Volatile.Read(ref F) takes a ref and only reads: a literal read-only callee list.
                            if (!IsReadOnlyRefCallee(model, argument))
                                census.Record(model, argument.Expression, argument, argument.RefKindKeyword.Text + "-argument");
                            break;
                        case InvocationExpressionSyntax invocation:
                            var receiver = MutatorReceiver(invocation);
                            if (receiver != null)
                                census.Record(model, receiver, invocation, "mutator ." + MemberName(invocation));
                            break;
                    }
                }
            }

            return census;
        }

        private static readonly HashSet<string> ReadOnlyRefCallees = new(StringComparer.Ordinal)
        {
            "System.Threading.Volatile.Read", "System.Threading.Interlocked.Read",
        };

        private static bool IsReadOnlyRefCallee(SemanticModel model, ArgumentSyntax argument)
            => argument.Parent?.Parent is InvocationExpressionSyntax invocation
               && model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
               && ReadOnlyRefCallees.Contains(method.ContainingType.ToDisplayString() + "." + method.Name);

        private static IEnumerable<ExpressionSyntax> Targets(ExpressionSyntax left) => left switch
        {
            TupleExpressionSyntax tuple => tuple.Arguments.SelectMany(a => Targets(a.Expression)),
            DeclarationExpressionSyntax => Array.Empty<ExpressionSyntax>(),
            _ => new[] { left },
        };

        private static string MemberName(InvocationExpressionSyntax invocation) => invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.Text,
            _ => "",
        };

        /// <summary>The receiver of <c>R.Mutator(...)</c> or <c>R?.Mutator(...)</c> for a rostered mutator name.</summary>
        private static ExpressionSyntax? MutatorReceiver(InvocationExpressionSyntax invocation)
        {
            if (!MutatorRoster.Contains(MemberName(invocation)))
                return null;
            return invocation.Expression switch
            {
                MemberAccessExpressionSyntax access => access.Expression,
                MemberBindingExpressionSyntax => invocation.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression,
                _ => null,
            };
        }

        private void Record(SemanticModel model, ExpressionSyntax target, SyntaxNode site, string kind)
        {
            var expression = target;
            if (expression is ElementAccessExpressionSyntax element)
            {
                expression = element.Expression;
                kind = "element " + kind;
            }

            // Only a name or a member access can bind to a field; a call result, a cast or an
            // object-initializer slot ([k] = v inside new() { ... }) cannot.
            if (expression is not (IdentifierNameSyntax or MemberAccessExpressionSyntax))
                return;

            var symbol = model.GetSymbolInfo(expression).Symbol;
            if (symbol == null)
            {
                if (expression is IdentifierNameSyntax { Identifier.Text: "_" })
                    return; // discard
                UnboundTargets.Add($"{site.SyntaxTree.FilePath}:{Line(site)} {expression}");
                return;
            }

            if (symbol is not IFieldSymbol { IsStatic: true, IsConst: false } field
                || !field.Locations.Any(l => l.IsInSource))
                return;

            // Outside the field's own initializer.
            var ownDeclarator = site.Ancestors().OfType<VariableDeclaratorSyntax>()
                .FirstOrDefault(v => v.Parent?.Parent is FieldDeclarationSyntax);
            if (ownDeclarator != null && SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(ownDeclarator), field))
                return;

            var member = site.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault() switch
            {
                MethodDeclarationSyntax m => m.Identifier.Text,
                ConstructorDeclarationSyntax c => (c.Modifiers.Any(SyntaxKind.StaticKeyword) ? "static " : "") + c.Identifier.Text,
                PropertyDeclarationSyntax p => p.Identifier.Text,
                IndexerDeclarationSyntax => "this[]",
                FieldDeclarationSyntax f => "initializer of " + string.Join(",", f.Declaration.Variables.Select(v => v.Identifier.Text)),
                var other => other?.Kind().ToString() ?? "?",
            };

            var key = Key(field);
            if (!Written.TryGetValue(key, out var sites))
                Written[key] = sites = new System.Collections.Generic.List<WriteSite>();
            sites.Add(new WriteSite(site.SyntaxTree.FilePath, Line(site), member, kind));
        }

        private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        private static string Key(IFieldSymbol field) => field.ContainingType.ToDisplayString() + "." + field.Name;
    }
}
