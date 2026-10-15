using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The two code-read candidates for #2250 cell 1 ("two in-process compilations of one source gave
/// 5 vs 1 diagnostics at 8 threads"), probed deterministically (P54 Phase 1 task 2). The violated
/// contract: a compile, and every query on its result, is a function of its inputs alone — the
/// same whichever other compile ran first or runs beside it.
///
/// <para><b>Probe 1 — the TPA namespace gate.</b> <c>ModuleRegistry._loadedRuntimeNamespaces</c> is
/// a process-wide check-then-act gate with no completion barrier: the first caller's
/// <c>TryAdd</c> wins, every later caller returns at once, and so scans the process for the
/// namespace's types while the winner may not yet have loaded the trusted-platform assemblies that
/// hold them. The probe holds the winner inside the gate through the internal
/// <c>beforeTpaLoad</c> parameter (a parameter, not a settable static hook, so no
/// StaticStateConformance row) while seven fresh registries ask for the same namespace.</para>
///
/// <para><b>Probe 2 — the name-keyed origin cache.</b> <c>ClrTypeHelper._originNameDefinitionCache</c>
/// is a static name → <c>Type</c> cache: whatever the first asker could see is the answer for
/// every later compile in the process, including a null.</para>
///
/// <para><b>Two gate measurements.</b> The MECHANISM cell holds the gate for a fresh
/// <c>system.p54tpagateprobe&lt;guid&gt;</c> key that no other test can have used and that loads
/// nothing, and asks whether the askers got PAST the gate while the winner was inside it — measured
/// in every host. The CONSEQUENCE cells compare type counts, which needs a namespace whose
/// assemblies are not yet loaded, and the gate is single-shot per process. A bare
/// <c>from system import …</c> (62 test files) enters the gate with <c>System</c>, whose prefix
/// <c>System.</c> loads EVERY System.* trusted-platform file, so no <c>system.*</c> namespace is
/// first-use in a shared test host. <c>microsoft.win32</c> is outside every system prefix and named
/// by no test (the dynamic mapping branch needs one loaded public type in the namespace, which a
/// test-only marker assembly provides) — but a co-run of the Conformance namespace still found
/// <c>Microsoft.Win32.Registry</c> loaded by an earlier test, so where it is already loaded the count
/// cells print NOT MEASURED (they never pass by default); run the class alone to measure them.</para>
///
/// <para><b>KnownRed convention.</b> A deterministic red cell is a <see cref="KnownRed"/> row: the
/// test asserts the defect REPRODUCES there (so the Phase 2 fix reddens it as "stale — drain this
/// row") and asserts parity in every other cell; each probe has a green positive-control cell.</para>
/// </summary>
[Trait("Category", "Conformance")]
public class TpaNamespaceGateTests
{
    private readonly ITestOutputHelper _output;

    public TpaNamespaceGateTests(ITestOutputHelper output) => _output = output;

    private const string MechanismCell = "tpa-gate.passes-before-completion";
    private const string GateWinnerCell = "tpa-gate.gate-winner";
    private const string ConcurrentFirstUseCell = "tpa-gate.concurrent-first-use";
    private const string AfterCompletionCell = "tpa-gate.after-completion";
    private const string ReferenceSeenCell = "origin-cache.reference-seen-by-unreferencing-compile";
    private const string NullPinnedCell = "origin-cache.unresolved-name-pinned-null";
    private const string FrameworkNameCell = "origin-cache.framework-name";

    private static readonly string[] AllCells =
    {
        MechanismCell, GateWinnerCell, ConcurrentFirstUseCell, AfterCompletionCell,
        ReferenceSeenCell, NullPinnedCell, FrameworkNameCell,
    };

    /// <summary>
    /// Cells red at base, each asserted to REPRODUCE. Drain a row when its cure lands (the probe
    /// then fails the row as stale).
    /// </summary>
    private static readonly Dictionary<string, string> KnownRed = new(StringComparer.Ordinal)
    {
        [MechanismCell] =
            "#2250 — the TPA gate has no completion barrier: 7/7 registries returned from the gate "
            + "while its winner was still inside it, deterministic, reproduced 3/3 class-alone "
            + "test-host runs @ 486c9a88a; "
            + "drains in Phase 2 task 1 (the gate becomes a Lazy)",
        [ConcurrentFirstUseCell] =
            "#2250 — the consequence: 7/7 registries that asked while the winner held the gate "
            + "returned 1 type (the marker) against the solo 9, deterministic, reproduced 5/5 "
            + "class-alone test-host runs @ 486c9a88a; NOT MEASURED where Microsoft.Win32.Registry "
            + "is already loaded (the Conformance co-run); drains in Phase 2 task 1",
        [ReferenceSeenCell] =
            "#2250 — a compile with no reference resolves a name to the Type another compile's "
            + "reference context loaded (8/8 threads), deterministic, reproduced 9/9 test-host runs @ 486c9a88a; "
            + "drains in Phase 2 task 1 (name-keyed caches move behind the per-registry visibility)",
        [NullPinnedCell] =
            "#2250 — a null cached by a compile that could not see the name is the answer for a later "
            + "compile whose reference holds it (8/8 threads), deterministic, reproduced 7/7 test-host runs @ 486c9a88a; "
            + "drains in Phase 2 task 1",
    };

    private const string GateModule = "microsoft.win32";
    private const string GateNamespace = "Microsoft.Win32";
    private const string GateAssembly = "Microsoft.Win32.Registry";
    private const int ConcurrentAskers = 7;
    private static readonly TimeSpan HoldTimeout = TimeSpan.FromSeconds(5);
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
        var gateField = typeof(ModuleRegistry).GetField(
            "_loadedRuntimeNamespaces", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.True(gateField != null,
            "ModuleRegistry._loadedRuntimeNamespaces is gone — the gate was rewritten; rewrite this probe against the new barrier");
        var gate = (ConcurrentDictionary<string, byte>)gateField!.GetValue(null)!;

        // ---- Mechanism cell, measurable in ANY host: a gate key no other test can have used. ----
        // `system.*` maps without an existence check, and no trusted-platform file starts with the
        // fresh namespace, so the winner loads nothing — what is measured is only whether an asker
        // can get PAST the gate while the winner is still inside it.
        var freshModule = "system.p54tpagateprobe" + Guid.NewGuid().ToString("N");
        var freshNamespace = new ModuleRegistry().GetNetNamespace(freshModule);
        Assert.True(freshNamespace != null && !gate.ContainsKey(freshNamespace),
            $"instrument: '{freshModule}' must map to a namespace the gate has never seen");
        var fresh = HoldGateAndAsk(freshModule, freshNamespace!);
        _output.WriteLine($"{MechanismCell}: winner={fresh.Winner} askers=[{string.Join(",", fresh.Askers)}] "
            + $"askersFinishedWhileGateHeld={fresh.AskersFinishedWhileHeld}");
        AssertCell(MechanismCell, diverged: fresh.AskersFinishedWhileHeld,
            $"all {ConcurrentAskers} askers returned from the gate for '{freshNamespace}' while the winner was "
            + $"still inside it: {fresh.AskersFinishedWhileHeld}");

        // ---- Consequence cells: the counts, measurable only where the namespace is first-use. ----
        var tpa = ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (gate.ContainsKey(GateNamespace)
            || IsLoaded(GateAssembly)
            || !tpa.Any(p => Path.GetFileNameWithoutExtension(p) == GateAssembly))
        {
            // Not a pass of the count cells: they are NOT MEASURED in this host, and the output
            // says so. The mechanism cell above was measured and asserted either way.
            _output.WriteLine(
                $"{ConcurrentFirstUseCell}, {GateWinnerCell}, {AfterCompletionCell}: NOT MEASURED in this host — "
                + $"'{GateNamespace}' seen by the gate: {gate.ContainsKey(GateNamespace)}; '{GateAssembly}' "
                + $"already loaded: {IsLoaded(GateAssembly)} (an earlier test in this process loaded it). "
                + "Run this class alone to measure them.");
            return;
        }

        // The dynamic mapping branch needs one loaded public type in the namespace.
        LoadIntoOwnContext("P54-TpaGateProbe",
            "namespace Microsoft.Win32 { public sealed class P54TpaGateProbeMarker { } }");
        var held = HoldGateAndAsk(GateModule, GateNamespace);

        // ---- Oracle: the solo count, taken once loading completed. ----
        Assert.True(IsLoaded(GateAssembly),
            $"INCONCLUSIVE: the gate winner did not load '{GateAssembly}', so the namespace never grew");
        var solo = new ModuleRegistry().GetNamespaceTypes(GateModule).Count;
        Assert.True(solo > 1,
            $"INCONCLUSIVE: solo count {solo} — '{GateAssembly}' contributed no type beyond the marker");

        var after = AskOnThreads(() => new ModuleRegistry().GetNamespaceTypes(GateModule).Count);

        var divergentAskers = held.Askers.Count(c => c != solo);
        _output.WriteLine($"{ConcurrentFirstUseCell}: solo={solo} winner={held.Winner} askers=[{string.Join(",", held.Askers)}] "
            + $"askersFinishedWhileGateHeld={held.AskersFinishedWhileHeld} divergent={divergentAskers}/{ConcurrentAskers} "
            + $"after=[{string.Join(",", after)}]");

        AssertCell(GateWinnerCell, diverged: held.Winner != solo,
            $"the gate winner's count {held.Winner} vs solo {solo}");
        AssertCell(ConcurrentFirstUseCell,
            diverged: held.AskersFinishedWhileHeld && divergentAskers > 0,
            $"{divergentAskers}/{ConcurrentAskers} askers that passed the held gate counted "
            + $"[{string.Join(",", held.Askers)}] vs solo {solo} (finished while held: {held.AskersFinishedWhileHeld})");
        AssertCell(AfterCompletionCell, diverged: after.Any(c => c != solo),
            $"after completion [{string.Join(",", after)}] vs solo {solo}");
    }

    /// <summary>
    /// Thread A wins the gate for <paramref name="module"/> and is held inside it (the
    /// <c>beforeTpaLoad</c> seam) until seven fresh registries, released together by a
    /// <see cref="Barrier"/>, have each asked for the namespace's types — or until
    /// <see cref="HoldTimeout"/>, which is what a gate with a completion barrier makes happen,
    /// since its askers wait for A.
    /// </summary>
    private static (int Winner, int[] Askers, bool AskersFinishedWhileHeld) HoldGateAndAsk(
        string module, string expectedNamespace)
    {
        var counts = new int[1 + ConcurrentAskers];
        var errors = new ConcurrentQueue<Exception>();
        var aInside = new ManualResetEventSlim(false);
        var askersDone = new CountdownEvent(ConcurrentAskers);
        var askersFinishedWhileHeld = false;
        string? heldNamespace = null;

        var winner = new Thread(() =>
        {
            try
            {
                counts[0] = new ModuleRegistry().GetNamespaceTypes(module, beforeTpaLoad: ns =>
                {
                    heldNamespace = ns;
                    aInside.Set();
                    askersFinishedWhileHeld = askersDone.Wait(HoldTimeout);
                }).Count;
            }
            catch (Exception ex) { errors.Enqueue(ex); }
            finally { aInside.Set(); }
        });
        winner.Start();
        Assert.True(aInside.Wait(JoinTimeout), "thread A never reached the gate");
        Assert.True(heldNamespace == expectedNamespace,
            $"thread A did not win the gate for '{expectedNamespace}' (held: '{heldNamespace ?? "<none>"}') — "
            + "the probe measures nothing");

        var start = new Barrier(ConcurrentAskers);
        var askers = Enumerable.Range(1, ConcurrentAskers).Select(i => new Thread(() =>
        {
            try
            {
                start.SignalAndWait();
                counts[i] = new ModuleRegistry().GetNamespaceTypes(module).Count;
            }
            catch (Exception ex) { errors.Enqueue(ex); }
            finally { askersDone.Signal(); }
        })).ToList();
        askers.ForEach(t => t.Start());
        foreach (var t in askers)
            Assert.True(t.Join(JoinTimeout), "an asker thread did not finish");
        Assert.True(winner.Join(JoinTimeout), "thread A did not finish");
        Assert.Empty(errors);
        return (counts[0], counts.Skip(1).ToArray(), askersFinishedWhileHeld);
    }

    [Fact]
    public void OriginNameCache_CrossCompile_IsNotPoisoned()
    {
        // Cell: compile A's reference context holds a type; a compile with no such reference asks
        // the same stored name. Its inputs cannot name the type, so the answer must be null.
        var nsA = "P54OriginProbeA" + Guid.NewGuid().ToString("N");
        var nameA = nsA + ".Greeter";
        var asmA = LoadIntoOwnContext("P54-compile-A",
            $"namespace {nsA} {{ public sealed class Greeter {{ public int Alpha => 1; }} }}");
        var typeA = ClrTypeHelper.ResolveClrTypeByStoredName(nameA);
        Assert.True(typeA != null && typeA.Assembly == asmA,
            "instrument: compile A must resolve its own reference's type, or the cell measures nothing");
        var seenByB = AskOnThreads(() => ClrTypeHelper.ResolveClrTypeByStoredName(nameA));
        var leaked = seenByB.Count(t => t != null && ReferenceEquals(t, typeA));
        _output.WriteLine($"{ReferenceSeenCell}: {leaked}/{seenByB.Length} unreferencing asks returned compile A's Type");
        AssertCell(ReferenceSeenCell, diverged: leaked > 0,
            $"{leaked}/{seenByB.Length} asks from a compile with no reference returned '{nameA}' from compile A's context");

        // Cell: a compile that cannot see a name asks first (null is cached); then a compile whose
        // reference HOLDS the name asks. Its inputs name the type, so the answer must be the type.
        var nsB = "P54OriginProbeB" + Guid.NewGuid().ToString("N");
        var nameB = nsB + ".Greeter";
        Assert.Null(ClrTypeHelper.ResolveClrTypeByStoredName(nameB));
        var asmB = LoadIntoOwnContext("P54-compile-B",
            $"namespace {nsB} {{ public sealed class Greeter {{ public int Beta => 2; }} }}");
        var truth = asmB.GetType(nameB);
        Assert.True(truth != null, "instrument: the referenced assembly must hold the type");
        var seenByReferencing = AskOnThreads(() => ClrTypeHelper.ResolveClrTypeByStoredName(nameB));
        var pinned = seenByReferencing.Count(t => t == null);
        _output.WriteLine($"{NullPinnedCell}: {pinned}/{seenByReferencing.Length} referencing asks returned the pinned null");
        AssertCell(NullPinnedCell, diverged: pinned > 0,
            $"{pinned}/{seenByReferencing.Length} asks from the compile whose reference holds '{nameB}' returned null");

        // Positive control: a framework name is a pure function of its key — every ask agrees.
        var framework = AskOnThreads(() => ClrTypeHelper.ResolveClrTypeByStoredName("System.Collections.Generic.List`1"));
        AssertCell(FrameworkNameCell, diverged: framework.Any(t => t != typeof(List<>)),
            "a framework generic definition resolved to something other than typeof(List<>)");
    }

    private void AssertCell(string cell, bool diverged, string observed)
    {
        if (KnownRed.TryGetValue(cell, out var row))
        {
            Assert.True(diverged,
                $"STALE KnownRed row '{cell}' — the defect no longer reproduces ({observed}); drain the row. Row: {row}");
        }
        else
        {
            Assert.False(diverged, $"cell '{cell}' diverged: {observed}");
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
