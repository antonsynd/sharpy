using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The two code-read candidates for #2250 cell 1, probed deterministically (P54 Phase 1 task 2) and
/// rewritten against the seam that replaced them (Phase 2 task 1). The violated contract: a compile,
/// and every query on its result, is a function of its inputs alone — the same whichever other
/// compile ran first or runs beside it.
///
/// <para><b>Probe 1 — first use of a framework namespace.</b> Before Phase 2,
/// <c>ModuleRegistry._loadedRuntimeNamespaces</c> was a process-wide check-then-act gate with no
/// completion barrier: the first caller's <c>TryAdd</c> won, every later caller returned at once and
/// scanned the process while the winner was still loading the namespace's trusted-platform assemblies
/// (7/7 held askers passed the gate, 7/7 counted 1 type against the solo 9 @ 486c9a88a). Now every
/// CLR-name question answers from the compilation's <see cref="AssemblyVisibility"/>, whose framework
/// set is a process-constant index built once behind a <see cref="Lazy{T}"/>: there is no per-namespace
/// load left to race. The probe asks for one framework namespace from eight fresh registries released
/// together — the namespace's first use when the class runs alone — and compares every answer with a
/// METADATA census of the framework's trusted-platform files (read with
/// <see cref="System.Reflection.Metadata"/>, loading nothing), so a half-built answer is red in any
/// host, not only where the namespace happens to be first-use.</para>
///
/// <para><b>Probe 2 — a reference's types are its compilation's alone.</b> Before Phase 2,
/// <c>ClrTypeHelper._originNameDefinitionCache</c> was a static name → <c>Type</c> cache over an
/// AppDomain scan: a compile with no reference resolved a name to the type ANOTHER compile's reference
/// context loaded (8/8), and a null cached by a compile that could not see the name was the answer for
/// a later compile whose reference held it (8/8). Now the name resolves against the asking
/// compilation's visibility, whose caches are per instance and replaced when its own set grows.</para>
///
/// <para><b>KnownRed convention.</b> A deterministic red cell is a <see cref="KnownRed"/> row: the
/// test asserts the defect REPRODUCES there (so a fix reddens it as "stale — drain this row") and
/// asserts parity in every other cell; each probe has a green positive-control cell. Every row
/// drained in Phase 2 task 1; the cells stay as the class's guard.</para>
/// </summary>
[Trait("Category", "Conformance")]
public class TpaNamespaceGateTests
{
    private readonly ITestOutputHelper _output;

    public TpaNamespaceGateTests(ITestOutputHelper output) => _output = output;

    private const string MechanismCell = "tpa-gate.passes-before-completion";
    private const string SoloCell = "tpa-gate.solo-equals-census";
    private const string ConcurrentFirstUseCell = "tpa-gate.concurrent-first-use";
    private const string AfterCompletionCell = "tpa-gate.after-completion";
    private const string ReferenceSeenCell = "origin-cache.reference-seen-by-unreferencing-compile";
    private const string NullPinnedCell = "origin-cache.unresolved-name-pinned-null";
    private const string FrameworkNameCell = "origin-cache.framework-name";

    private static readonly string[] AllCells =
    {
        MechanismCell, SoloCell, ConcurrentFirstUseCell, AfterCompletionCell,
        ReferenceSeenCell, NullPinnedCell, FrameworkNameCell,
    };

    /// <summary>
    /// Cells red at base, each asserted to REPRODUCE. Drain a row when its cure lands (the probe
    /// then fails the row as stale). Empty since P54 Phase 2 task 1 (#2250).
    /// </summary>
    private static readonly Dictionary<string, string> KnownRed = new(StringComparer.Ordinal)
    {
    };

    private const string GateModule = "microsoft.win32";
    private const string GateNamespace = "Microsoft.Win32";
    private const string GateAssembly = "Microsoft.Win32.Registry";
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public void KnownRed_RowsNameRealCellsAndCiteTheIssue()
    {
        foreach (var (cell, reason) in KnownRed)
        {
            Assert.Contains(cell, AllCells);
            Assert.Contains("#2250", reason, StringComparison.Ordinal);
        }
        Assert.True(AllCells.Count(c => !KnownRed.ContainsKey(c)) >= 2,
            "each probe needs a green positive-control cell; the oracle is not 'always red'");
    }

    [Fact]
    public void TpaNamespaceGate_ConcurrentFirstUse_IsComplete()
    {
        var failures = new List<string>();

        // ---- Oracle: the namespace's public top-level types across the framework's trusted-platform
        //      files, read as metadata — nothing is loaded, so the oracle cannot be order-dependent. ----
        var census = FrameworkMetadataCensus(GateNamespace);
        Assert.True(census.Count > 1,
            $"INCONCLUSIVE: the framework census of '{GateNamespace}' found {census.Count} public types — "
            + $"'{GateAssembly}' is not a trusted-platform file in this host, so the cell measures nothing");
        var preloaded = IsLoaded(GateAssembly);

        // ---- Eight fresh registries released together: the namespace's first use when the class runs
        //      alone (the shape the old gate lost: a second asker read a half-loaded process). ----
        var concurrent = AskOnThreads(() => NamespaceAnswer(new ModuleRegistry()));
        var solo = NamespaceAnswer(new ModuleRegistry());
        var after = AskOnThreads(() => NamespaceAnswer(new ModuleRegistry()));

        var incomplete = concurrent.Count(a => !a.SetEquals(census));
        var (frameworkAssemblies, frameworkTypes, frameworkMillis) = AssemblyVisibility.FrameworkIndexStatistics;
        _output.WriteLine($"framework index: {frameworkAssemblies} assemblies, {frameworkTypes} visible types, "
            + $"built in {frameworkMillis} ms (first use in this process)");
        _output.WriteLine($"census={census.Count} ('{GateAssembly}' loaded before the probe: {preloaded}) "
            + $"concurrent=[{string.Join(",", concurrent.Select(a => a.Count))}] solo={solo.Count} "
            + $"after=[{string.Join(",", after.Select(a => a.Count))}] incomplete={incomplete}/{concurrent.Length}");

        AssertCell(failures, MechanismCell, diverged: incomplete > 0,
            $"{incomplete}/{concurrent.Length} concurrent first-use answers differ from the framework census "
            + $"({census.Count} types); first missing: {concurrent.Select(a => census.Except(a).FirstOrDefault()).FirstOrDefault(m => m != null) ?? "<none>"}");
        AssertCell(failures, ConcurrentFirstUseCell, diverged: concurrent.Any(a => !a.SetEquals(solo)),
            $"concurrent answers [{string.Join(",", concurrent.Select(a => a.Count))}] vs solo {solo.Count}");
        AssertCell(failures, SoloCell, diverged: !solo.SetEquals(census),
            $"solo {solo.Count} vs census {census.Count}");
        AssertCell(failures, AfterCompletionCell, diverged: after.Any(a => !a.SetEquals(solo)),
            $"after [{string.Join(",", after.Select(a => a.Count))}] vs solo {solo.Count}");

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// The CLR full names a registry answers for <see cref="GateModule"/>: every exported type symbol,
    /// with an arity group (#1613) expanded to all its members.
    /// </summary>
    private static HashSet<string> NamespaceAnswer(ModuleRegistry registry)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in registry.GetNamespaceTypes(GateModule))
        {
            IEnumerable<TypeSymbol> members = symbol.ClrArityGroup is { } group ? group.Values : new[] { symbol };
            foreach (var member in members)
            {
                if (member.ClrType?.FullName is { } fullName)
                    names.Add(fullName);
            }
        }
        return names;
    }

    /// <summary>
    /// The public top-level types of <paramref name="namespaceName"/> declared by the framework's
    /// trusted-platform files (System, System.*, Microsoft.*, netstandard, mscorlib), read from
    /// metadata without loading any assembly.
    /// </summary>
    private static HashSet<string> FrameworkMetadataCensus(string namespaceName)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var tpa = ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in tpa)
        {
            var simpleName = Path.GetFileNameWithoutExtension(path);
            if (!AssemblyVisibility.IsFrameworkFileName(simpleName))
                continue;

            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
                continue;
            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.TypeDefinitions)
            {
                var definition = reader.GetTypeDefinition(handle);
                if ((definition.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public)
                    continue;
                if (reader.GetString(definition.Namespace) != namespaceName)
                    continue;
                names.Add(namespaceName + "." + reader.GetString(definition.Name));
            }
        }
        return names;
    }

    [Fact]
    public void OriginNameCache_CrossCompile_IsNotPoisoned()
    {
        var failures = new List<string>();

        // Cell: compile A's reference context holds a type; a compile with no such reference asks
        // the same stored name. Its inputs cannot name the type, so the answer must be null.
        var nsA = "P54OriginProbeA" + Guid.NewGuid().ToString("N");
        var nameA = nsA + ".Greeter";
        var asmA = LoadIntoOwnContext("P54-compile-A",
            $"namespace {nsA} {{ public sealed class Greeter {{ public int Alpha => 1; }} }}");
        var compileA = new AssemblyVisibility();
        compileA.Add(asmA);
        var typeA = compileA.ResolveType(nameA);
        Assert.True(typeA != null && typeA.Assembly == asmA,
            "instrument: compile A must resolve its own reference's type, or the cell measures nothing");
        var seenByB = AskOnThreads(() => new ModuleRegistry().Visibility.ResolveType(nameA));
        var leaked = seenByB.Count(t => t != null && ReferenceEquals(t, typeA));
        _output.WriteLine($"{ReferenceSeenCell}: {leaked}/{seenByB.Length} unreferencing asks returned compile A's Type");
        AssertCell(failures, ReferenceSeenCell, diverged: leaked > 0,
            $"{leaked}/{seenByB.Length} asks from a compile with no reference returned '{nameA}' from compile A's context");

        // Cell: a compile that cannot see a name asks first (a null is cached); then a compile whose
        // reference HOLDS the name asks — and so does the first compile once it gains the reference.
        // Their inputs name the type, so the answer must be the type.
        var nsB = "P54OriginProbeB" + Guid.NewGuid().ToString("N");
        var nameB = nsB + ".Greeter";
        var growing = new AssemblyVisibility();
        Assert.Null(growing.ResolveType(nameB));
        var asmB = LoadIntoOwnContext("P54-compile-B",
            $"namespace {nsB} {{ public sealed class Greeter {{ public int Beta => 2; }} }}");
        var truth = asmB.GetType(nameB);
        Assert.True(truth != null, "instrument: the referenced assembly must hold the type");
        growing.Add(asmB);
        var seenByReferencing = AskOnThreads(() =>
        {
            var fresh = new AssemblyVisibility();
            fresh.Add(asmB);
            return fresh.ResolveType(nameB);
        }).Append(growing.ResolveType(nameB)).ToArray();
        var pinned = seenByReferencing.Count(t => t != truth);
        _output.WriteLine($"{NullPinnedCell}: {pinned}/{seenByReferencing.Length} referencing asks did not return the type");
        AssertCell(failures, NullPinnedCell, diverged: pinned > 0,
            $"{pinned}/{seenByReferencing.Length} asks from a compile whose reference holds '{nameB}' did not return it");

        // Positive control: a framework name is a pure function of its key — every ask agrees.
        var framework = AskOnThreads(() => new AssemblyVisibility().ResolveType("System.Collections.Generic.List`1"));
        AssertCell(failures, FrameworkNameCell, diverged: framework.Any(t => t != typeof(List<>)),
            "a framework generic definition resolved to something other than typeof(List<>)");

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// Records a cell's verdict: a KnownRed row must still reproduce (else it is STALE — drain it),
    /// every other cell must not diverge. Collected, so one run reports every cell.
    /// </summary>
    private static void AssertCell(List<string> failures, string cell, bool diverged, string observed)
    {
        if (KnownRed.TryGetValue(cell, out var row))
        {
            if (!diverged)
                failures.Add($"STALE KnownRed row '{cell}' — the defect no longer reproduces ({observed}); drain the row. Row: {row}");
        }
        else if (diverged)
        {
            failures.Add($"cell '{cell}' diverged: {observed}");
        }
    }

    private static T[] AskOnThreads<T>(Func<T> ask)
    {
        var results = new T[8];
        var errors = new ConcurrentQueue<Exception>();
        var start = new Barrier(results.Length);
        var threads = Enumerable.Range(0, results.Length).Select(i => new Thread(() =>
        {
            try
            {
                start.SignalAndWait();
                results[i] = ask();
            }
            catch (Exception ex) { errors.Enqueue(ex); }
        })).ToList();
        threads.ForEach(t => t.Start());
        foreach (var t in threads)
            Assert.True(t.Join(JoinTimeout), "an asking thread did not finish");
        Assert.Empty(errors);
        return results;
    }

    private static bool IsLoaded(string simpleName)
        => AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == simpleName);

    /// <summary>
    /// Builds a test-only assembly in memory and loads it into its own (non-default) load context —
    /// the shape a compile's reference takes once each registry owns its context (Design Decision 2).
    /// </summary>
    private static Assembly LoadIntoOwnContext(string contextName, string source)
    {
        var assemblyName = contextName + "-" + Guid.NewGuid().ToString("N");
        var runtime = ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Single(p => Path.GetFileNameWithoutExtension(p) == "System.Runtime");
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source) },
            new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(runtime),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Assert.True(emitted.Success, "the probe assembly must build: "
            + string.Join("; ", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        stream.Position = 0;
        return new AssemblyLoadContext(contextName).LoadFromStream(stream);
    }
}
