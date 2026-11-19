using Harness = Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests;
using OrderMatrix = Sharpy.Compiler.Tests.Semantic.StaticInitializerOrderMatrixTests;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The read after a caught throw (plan-775c50's Testing Strategy, task M; #2457 cell 4, "N3 bullet 5, read again after a
/// caught throw"; the lazy host A4's retry needs). An initializer raises on its first run, the program catches the
/// failure, and uses the host again. D3 and D5 give one rule: a throw puts back the state the run found and the ORIGINAL
/// exception propagates; a host's failure is delivered once, to the use that required the host, a store as much as a
/// read; from then on each value is initialized by its own first read (retry per value), and a value stored before its
/// initializer ran keeps its store (T3's reading, accepted as the plan's text).
///
/// Axes: the host (<see cref="RetryHosts"/>: the location family's stored holders in their host forms, a module, a plain
/// type and a closed generic type, each type in every kind of host the static-initializer order matrix has,
/// <see cref="OrderMatrix.Hosts"/>) × who catches (<see cref="RetryCatcher"/>) × what the program does around the failure
/// (<see cref="RetryScenario"/>, one value per clause of the rule) × whether the initializer fails. The host and the
/// catcher decide where the failure can be caught (<see cref="RetrySituation"/>): a module and a plain class are swept
/// before <c>main</c> (Q3), so only an earlier initializer that requires them first, from inside the module's sweep,
/// can catch it; a closed generic type sweeps on its first use, wherever that is. Every cell has its control: the same
/// program whose initializer does not fail, which runs.
/// </summary>
public sealed partial class InitializerDependencyDifferentialTests
{
    private const string RetryPrefix = "Location/Retry/";

    /// <summary>The exception the initializer raises on its first run.</summary>
    private const string RetryFailure = "ValueError";

    /// <summary>What a store before the initializer, or the first use's store, writes.</summary>
    private const int RetryStore = 9;

    /// <summary>What the value's own initializer stores to it before it calls the failing function.</summary>
    private const int OwnStoreValue = 5;

    /// <summary>Who catches the failure: a function an earlier module value's initializer calls, or <c>main</c>. A closed
    /// literal set: the two places a program's code runs before and after the module's sweep.</summary>
    internal enum RetryCatcher
    {
        EarlierInitializer,
        Main,
    }

    /// <summary>What the program does around the failure. A closed literal set, one value per clause of D3's rule:
    /// <list type="bullet">
    /// <item><see cref="ReadAgain"/>: the failure is delivered to the read that required the host, and the next read
    /// retries the initializer.</item>
    /// <item><see cref="StoreFirst"/>: the first use is a plain store; the failure is delivered to it (the store requires
    /// the host first, <c>StaticInit.Store</c>), so nothing is stored, and the read after retries.</item>
    /// <item><see cref="ReadDependent"/>: a later value, which the failed sweep never reached, reads the failed one: each is
    /// initialized by its own first read.</item>
    /// <item><see cref="OwnStoreEveryAttempt"/>: the initializer stores its own value, then fails; the retry stores again
    /// and reads its store (the plan's W6).</item>
    /// <item><see cref="OwnStoreFailedAttemptOnly"/>: the initializer stores its own value only on the attempt that fails;
    /// the throw put back the state it found, so the retry's read meets a value never given one (W6).</item>
    /// <item><see cref="StoredBefore"/>: an earlier value of the host stores the failing value before its initializer
    /// runs; the throw puts back that stored state, and the read after returns the store.</item>
    /// </list></summary>
    internal enum RetryScenario
    {
        ReadAgain,
        StoreFirst,
        ReadDependent,
        OwnStoreEveryAttempt,
        OwnStoreFailedAttemptOnly,
        StoredBefore,
    }

    /// <summary>Where the failure lands. <see cref="Lazy"/>: the first use is outside the host's sweep and starts it, so the
    /// sweep's failure is delivered to that use. <see cref="OwnSweep"/>: the first use is a forward read inside the host's
    /// own running sweep, which initializes the value there. <see cref="BeforeMain"/>: the host sweeps before <c>main</c>
    /// and nothing catches its failure.</summary>
    internal enum RetrySituation
    {
        Lazy,
        OwnSweep,
        BeforeMain,
    }

    /// <summary>A host: a cross host (<see cref="CrossHosts"/>) and, for a type, the kind of type that declares it, one of the
    /// static-initializer order matrix's hosts (a class, a dataclass, a struct).</summary>
    internal sealed record RetryHost(CrossHost Cross, string Kind)
    {
        public bool IsModule => Cross.IsModule;

        public bool IsGeneric => Cross.IsGeneric;

        public string Key => IsModule || Kind == "class" ? Cross.Key : $"{Cross.Key}.{Kind}";

        /// <summary>The host's declaration: the module's lines, or the order matrix's header for its kind, named <c>H</c>.</summary>
        public List<string> Declare(params (string Name, string Initializer)[] values)
        {
            var lines = CrossDeclaration("H", Cross, values);
            if (IsModule)
                return lines;
            var header = OrderMatrix.Hosts.Single(h => h.Name == Kind).Header
                .Select(l => l.Replace("Host:", IsGeneric ? "H[E]:" : "H:", StringComparison.Ordinal));
            return header.Concat(lines.Skip(1)).ToList();
        }
    }

    /// <summary>The module, and each type host form in each kind of host of the order matrix.</summary>
    internal static IEnumerable<RetryHost> RetryHosts() =>
        from cross in CrossHosts()
        from kind in cross.IsModule ? new[] { "module" } : OrderMatrix.Hosts.Select(h => h.Name)
        select new RetryHost(cross, kind);

    internal static RetrySituation SituationOf(RetryHost host, RetryCatcher catcher) =>
        catcher == RetryCatcher.Main ? (host.IsGeneric ? RetrySituation.Lazy : RetrySituation.BeforeMain)
        : host.IsModule ? RetrySituation.OwnSweep
        : RetrySituation.Lazy;

    /// <summary>What a cell prints, or the exception it ends with. A cell prints <c>failed</c> where it caught the failure,
    /// then what it reads after the catch, the failing value read once more, and how many times its initializer ran.</summary>
    internal static (string? Prints, string? Raises) RetryModel(RetryScenario scenario, RetrySituation situation, bool fails)
    {
        if (!fails)
        {
            // Nothing fails: one run of the initializer. A store before it is kept until the initializer replaces it at its
            // place, so a forward read inside the module's own sweep reads the store, and a use after the sweep the result.
            var keptUntilItsPlace = situation == RetrySituation.OwnSweep;
            return scenario switch
            {
                RetryScenario.ReadAgain => ("1 1 1", null),
                RetryScenario.StoreFirst => (keptUntilItsPlace ? $"{RetryStore} 1 1" : $"{RetryStore} {RetryStore} 1", null),
                RetryScenario.ReadDependent => ("2 1 1", null),
                RetryScenario.OwnStoreEveryAttempt or RetryScenario.OwnStoreFailedAttemptOnly => ($"{OwnStoreValue + 1} {OwnStoreValue + 1} 1", null),
                RetryScenario.StoredBefore => (keptUntilItsPlace ? $"{RetryStore} 1 1" : "1 1 1", null),
                _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
            };
        }

        // The sweep runs before main, and nothing catches its failure (Q3: stop before main, with the original exception).
        if (situation == RetrySituation.BeforeMain)
            return (null, RetryFailure);
        // Inside the module's own sweep a store gives the value one, so the forward read returns it and starts no
        // initializer: the failure comes at the value's place in the sweep, which nothing catches.
        if (situation == RetrySituation.OwnSweep && scenario is RetryScenario.StoreFirst or RetryScenario.StoredBefore)
            return (null, RetryFailure);
        return scenario switch
        {
            // The retry runs the initializer a second time: it returns 2.
            RetryScenario.ReadAgain or RetryScenario.StoreFirst => ("failed\n2 2 2", null),
            RetryScenario.ReadDependent => ("failed\n3 2 2", null),
            RetryScenario.OwnStoreEveryAttempt => ($"failed\n{OwnStoreValue + 2} {OwnStoreValue + 2} 2", null),
            // The retry stores nothing and reads its own value, which the failed attempt's store no longer gives it.
            RetryScenario.OwnStoreFailedAttemptOnly => (null, CycleError),
            // The store made before the initializer ran is the state the throw put back: read, the initializer not run again.
            RetryScenario.StoredBefore => ($"failed\n{RetryStore} {RetryStore} 1", null),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
    }

    private static string RetryId(RetryHost host, RetryCatcher catcher, RetryScenario scenario, bool fails) =>
        $"{RetryPrefix}{host.Key}/{catcher}/{scenario}{(fails ? "" : "/Control")}";

    /// <summary>The program: a counter, the catcher's module value where it is an earlier initializer (in a module host,
    /// right before the failing value), the host's values (<c>z</c> storing <c>a</c> first, <c>a</c>, and <c>c</c> reading
    /// <c>a</c>, as the scenario needs), the functions after them (rule N1), and <c>main</c>. Python's twin runs top to bottom, so its functions come first, and a function that
    /// stores a module name declares it global.</summary>
    private static string RetryProgram(RetryHost host, RetryCatcher catcher, RetryScenario scenario, bool fails, bool twin)
    {
        var a = CrossName("H", host.Cross, "a");
        var c = CrossName("H", host.Cross, "c");
        var global = twin && host.IsModule ? new[] { "    global a" } : Array.Empty<string>();
        var own = scenario is RetryScenario.OwnStoreEveryAttempt or RetryScenario.OwnStoreFailedAttemptOnly;

        var values = new List<(string Name, string Initializer)>();
        if (scenario == RetryScenario.StoredBefore)
            values.Add(("z", "pre()"));
        // In a module host the catcher's value is one of the host's own: it sits right before the failing value.
        if (host.IsModule && catcher == RetryCatcher.EarlierInitializer)
            values.Add(("b", "probe()"));
        values.Add(("a", own ? "own()" : "tick()"));
        if (scenario == RetryScenario.ReadDependent)
            values.Add(("c", $"{a} + 1"));

        var functions = new List<string>
        {
            "def tick() -> int:", "    calls.append(1)", $"    if len(calls) == 1 and {(fails ? "True" : "False")}:", $"        raise {RetryFailure}(\"x\")", "    return len(calls)",
        };
        if (own)
        {
            functions.Add("def own() -> int:");
            functions.AddRange(global);
            functions.AddRange(scenario == RetryScenario.OwnStoreEveryAttempt
                ? new[] { $"    {a} = {OwnStoreValue}" }
                : new[] { "    if len(calls) == 0:", $"        {a} = {OwnStoreValue}" });
            functions.AddRange(new[] { "    n0 = tick()", $"    return {a} + n0" });
        }
        if (scenario == RetryScenario.StoredBefore)
        {
            functions.Add("def pre() -> int:");
            functions.AddRange(global);
            functions.AddRange(new[] { $"    {a} = {RetryStore}", "    return 0" });
        }

        var first = scenario == RetryScenario.StoreFirst ? $"{a} = {RetryStore}" : $"r0 = {a}";
        var after = scenario == RetryScenario.ReadDependent ? c : a;
        var attempt = new[] { "try:", $"    {first}", $"except {RetryFailure}:", "    print(\"failed\")" };
        var storesInAttempt = scenario == RetryScenario.StoreFirst ? global : Array.Empty<string>();
        string main;
        if (catcher == RetryCatcher.EarlierInitializer)
        {
            functions.Add("def probe() -> int:");
            functions.AddRange(storesInAttempt);
            functions.AddRange(attempt.Select(l => "    " + l));
            functions.Add($"    return {after}");
            main = Harness.MainBlock($"print(b, {a}, len(calls))");
        }
        else
        {
            main = Harness.MainBlock(storesInAttempt.Select(l => l.TrimStart()).Concat(attempt).Append($"print({after}, {a}, len(calls))").ToArray());
        }

        var head = new List<string> { "calls: list[int] = []" };
        if (!host.IsModule && catcher == RetryCatcher.EarlierInitializer)
            head.Add("b: int = probe()");
        head.AddRange(host.Declare(values.ToArray()));
        var body = twin ? functions.Concat(head) : head.Concat(functions);
        return WithHelpers(Harness.Join(body) + main);
    }

    /// <summary>Python's twin, where python runs the same program with the same meaning: the module host caught in
    /// <c>main</c>, whose module runs before <c>main</c> as Sharpy's sweep does, when nothing fails. A class body has no
    /// lazy sweep and cannot name its own class; a forward read is python's NameError, which N3 initializes first; and a
    /// failure there is a <c>ValueError</c>, which the twin's expectations do not hold.</summary>
    internal static PythonExpectation RetryPython(RetryHost host, RetryCatcher catcher, bool fails) =>
        host.IsModule && catcher == RetryCatcher.Main && !fails ? PythonExpectation.Prints : PythonExpectation.NoTwin;

    private static IEnumerable<(RetryHost Host, RetryCatcher Catcher, RetryScenario Scenario, bool Fails)> RetryProduct() =>
        from host in RetryHosts()
        from catcher in Enum.GetValues<RetryCatcher>()
        from scenario in Enum.GetValues<RetryScenario>()
        from fails in new[] { true, false }
        select (host, catcher, scenario, fails);

    internal static IEnumerable<Cell> RetryCells()
    {
        foreach (var (host, catcher, scenario, fails) in RetryProduct())
        {
            var (prints, raises) = RetryModel(scenario, SituationOf(host, catcher), fails);
            var python = RetryPython(host, catcher, fails);
            yield return new Cell(
                RetryId(host, catcher, scenario, fails),
                Family.Location,
                RetryProgram(host, catcher, scenario, fails, twin: false),
                python == PythonExpectation.NoTwin ? null : RetryProgram(host, catcher, scenario, fails, twin: true),
                prints ?? raises!)
            {
                Raises = raises,
                // The same program with an initializer that does not fail: it runs today and must keep running.
                IsControl = !fails,
                Python = python,
                Route = static (verdict, _, _) => ValueRoute(verdict),
            };
        }
    }

    internal static List<string> RetryTotalityFailures(IReadOnlyList<Cell> pool)
    {
        var failures = new List<string>();
        var hosts = RetryHosts().ToList();
        var product = RetryProduct().ToList();

        // The hosts are the cross family's, a module, a plain type and a closed generic type each, and each type form rides
        // every kind of host the order matrix has.
        foreach (var (isModule, isGeneric, what) in new[] { (true, false, "a module"), (false, false, "a plain class"), (false, true, "a closed generic type") })
        {
            if (!hosts.Any(h => h.IsModule == isModule && h.IsGeneric == isGeneric))
                failures.Add($"no retry host is {what}");
        }
        foreach (var cross in CrossHosts().Where(h => !h.IsModule))
            foreach (var kind in OrderMatrix.Hosts.Select(h => h.Name).Where(k => !hosts.Contains(new RetryHost(cross, k))))
                failures.Add($"the {cross.Key} type host has no retry host of kind {kind}");
        if (OrderMatrix.Hosts.Length < 3)
            failures.Add($"the order matrix has {OrderMatrix.Hosts.Length} kinds of host: it reads nothing");
        // Every situation is reached, and the lazy one under both catchers, on a generic host (A4's lazy host).
        foreach (var situation in Enum.GetValues<RetrySituation>().Where(s => product.All(p => SituationOf(p.Host, p.Catcher) != s)))
            failures.Add($"RetrySituation.{situation} has no cell");
        foreach (var catcher in Enum.GetValues<RetryCatcher>().Where(k => !hosts.Any(h => h.IsGeneric && SituationOf(h, k) == RetrySituation.Lazy)))
            failures.Add($"no generic host is lazy when RetryCatcher.{catcher} catches");
        // Each scenario prints after a caught failure somewhere, and each answer of the rule after a failure has a cell.
        foreach (var scenario in Enum.GetValues<RetryScenario>())
        {
            var outcomes = product.Where(p => p.Scenario == scenario && p.Fails).Select(p => RetryModel(scenario, SituationOf(p.Host, p.Catcher), true)).ToList();
            if (!outcomes.Any(o => o.Prints?.StartsWith("failed\n", StringComparison.Ordinal) == true) && !outcomes.Any(o => o.Raises == CycleError))
                failures.Add($"RetryScenario.{scenario} neither prints after a caught failure nor raises on the retry anywhere");
        }
        foreach (var answer in new[] { RetryFailure, CycleError })
        {
            if (!product.Any(p => p.Fails && RetryModel(p.Scenario, SituationOf(p.Host, p.Catcher), true).Raises == answer))
                failures.Add($"no retry cell ends with {answer}");
        }
        // A control differs from its failing cell: the failure is the one thing it removes.
        foreach (var (host, catcher, scenario, _) in product.Where(p => p.Fails))
        {
            if (RetryModel(scenario, SituationOf(host, catcher), true) == RetryModel(scenario, SituationOf(host, catcher), false))
                failures.Add($"{RetryId(host, catcher, scenario, true)} has the model of its control");
            if (RetryModel(scenario, SituationOf(host, catcher), false).Prints == null)
                failures.Add($"{RetryId(host, catcher, scenario, false)} is a control that does not run");
        }

        // What the axes owe, written again from the enumerations: every host, catcher, scenario, failing or not. A control
        // is the program whose initializer does not fail.
        var owed = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var cross in CrossHosts())
            foreach (var kind in cross.IsModule ? new[] { "module" } : OrderMatrix.Hosts.Select(h => h.Name).ToArray())
                foreach (var catcher in Enum.GetValues<RetryCatcher>())
                    foreach (var scenario in Enum.GetValues<RetryScenario>())
                    {
                        owed[RetryId(new RetryHost(cross, kind), catcher, scenario, true)] = false;
                        owed[RetryId(new RetryHost(cross, kind), catcher, scenario, false)] = true;
                    }
        failures.AddRange(PoolAgainstOwed("the retry axes", owed, pool.Where(c => c.Id.StartsWith(RetryPrefix, StringComparison.Ordinal))));
        return failures;
    }
}
