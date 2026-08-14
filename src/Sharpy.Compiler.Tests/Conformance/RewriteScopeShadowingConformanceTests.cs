using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Conformance sweep for the contract <b>an emitter identifier rewrite describes ONE binding and
/// must not cross a binder that re-binds the name.</b>
///
/// <para>Sharpy lets an accessor NAME its incoming value; C# does not — a property setter and an
/// event accessor both receive an implicit <c>value</c>, and a property observer's parameter maps
/// onto <c>value</c> or onto the captured old-value local. Nothing declares the Sharpy spelling, so
/// the emitter carries a name→slot MAPPING (<c>_accessorParamRewrite</c>) while it generates the
/// accessor body. A lambda parameter, a nested <c>def</c> parameter or a comprehension target
/// spelled the same way is a DIFFERENT binding, and rewriting it produces a silently wrong value
/// with no diagnostic — the worst class in the tracker (#1146).</para>
///
/// <para>That is exactly what #1500 was: a lambda parameter shadowing a setter's value parameter
/// printed 300 where CPython printed 106. The rewrite matched by spelling, before symbol
/// resolution, and no binder suspended it. One cell was fixed; this sweep pins the class.</para>
///
/// <para><b>Matrix</b> — <c>mechanism × binder</c>:</para>
/// <list type="bullet">
///   <item><description><b>mechanism</b> — every source shape that opens an accessor-parameter
///     rewrite, one per emitter site rather than one per concept: <c>combinedFunctionStyle</c>
///     (<c>GenerateCombinedFunctionStyleProperty</c>), <c>mixedAutoCustom</c>
///     (<c>GenerateMixedAutoCustomProperty</c>), <c>moduleLevelFunctionStyle</c>
///     (<c>GenerateFunctionStyleProperty</c>), <c>interfaceDefault</c>
///     (<c>GenerateInterfacePropertyFromDef</c>), <c>eventAddAccessor</c>
///     (<c>RoslynEmitter.ClassMembers.Events.cs</c>), and the two observer arms
///     <c>observerBeforeSet</c>/<c>observerAfterSet</c> (<c>GenerateObserverBody</c>) — the
///     after_set arm being the one whose target is NOT <c>value</c>. The mechanisms were
///     enumerated by grepping <c>RoslynEmitter*</c> for every <c>AccessorParamRewrite(</c> call
///     site; a new site with no cell here is the gap this sweep exists to make visible.</description></item>
///   <item><description><b>binder</b> — <c>lambdaParam</c>, <c>nestedDefParam</c>,
///     <c>comprehensionTarget</c>: the three constructs that re-bind a name inside an expression or
///     statement body. Only the first was probed when #1500 was filed; the other two were found
///     broken by writing this sweep (measured: comprehension target printed 1100 where CPython
///     printed 110).</description></item>
/// </list>
///
/// <para><b>Every cell is DISCRIMINATING by construction.</b> Each program computes its answer from
/// a literal (3) that the shadowing binder doubles, while the accessor's incoming value is 100 — so
/// honouring the binder prints 6 and leaking the rewrite prints 200. The harness asserts
/// <c>Expected != Leaked</c> BEFORE asserting which one the program printed, and fails the cell if
/// they coincide. This is not hypothetical caution: #1500's first probe applied the lambda to the
/// setter's own value, so both lowerings agreed and the bug read as absent.</para>
///
/// <para>The <c>eventAddAccessor</c> mechanism is the one exception to the value probe, recorded
/// honestly in its cell: the accessor's value is a delegate, so a leak puts an <c>EventHandler</c>
/// where an <c>int</c> is required and the leak is type-visible rather than value-visible. Its
/// leaked outcome is a compile failure, which still differs from 6.</para>
///
/// <para><b>Mutation test</b> (performed 2026-08-13, per the round's guard-integrity rule): with
/// <c>SuspendAccessorParamRewriteIfShadowed</c> reverted to a no-op — i.e. the rewrite restored to
/// its pre-#1500 unscoped form — this sweep failed with <b>21 of 21</b> cells leaking, naming each
/// mechanism×binder pair and printing 200 where 6 was expected. The guard was then restored and the
/// sweep returned to 21/21 green. A sweep that cannot go red for the thing it guards is not a guard
/// (docs/design/gap-discovery-contracts.md).</para>
///
/// <para><b>What it found on its first run,</b> beyond the #1500 cell it was written for: the
/// comprehension-target and nested-def binders leaked on every property mechanism (unprobed when
/// #1500 was filed), and <c>eventAddAccessor::nestedDefParam</c> did not compile at all — a nested
/// <c>def</c> inside an event accessor was never registered as a symbol, because
/// <c>CheckEvent</c> alone among the accessor paths left <c>_currentFunctionReturnType</c> null and
/// that is the gate <c>CheckFunction</c> uses to recognise a nested function. Fixed in the same
/// change; the cell is what made it visible.</para>
///
/// <para><b>Ratchet.</b> <c>Conformance/rewrite-shadowing-allowlist.txt</c>: a leaking cell whose
/// key is not listed fails the suite, every listed key cites the issue that will drain it, and a
/// listed cell that has started printing the right answer ALSO fails — deleting the line is part of
/// landing the fix. The list ships empty, and an entry means a program compiles and prints the
/// wrong number, so the bar for adding one is a filed wrong-code issue.</para>
/// </summary>
public class RewriteScopeShadowingConformanceTests : IntegrationTestBase
{
    public RewriteScopeShadowingConformanceTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// The name every mechanism gives its accessor value parameter AND every binder re-binds. The
    /// shadowing IS the collision, so the two must be spelled the same on purpose.
    /// </summary>
    private const string SharedName = "v";

    /// <summary>What the accessor is handed. Deliberately not 3, so a leak cannot agree.</summary>
    private const string IncomingValue = "100";

    /// <summary>What every cell prints when the binder's own binding is honoured (3 * 2).</summary>
    private const string ExpectedOutput = "6";

    /// <summary>What every value-probing cell prints when the rewrite leaks in (100 * 2).</summary>
    private const string LeakedOutput = "200";

    private const string CompileFailed = "<compile failed>";

    // --- Binders ---------------------------------------------------------------------------------

    /// <summary>
    /// A construct that re-binds <see cref="SharedName"/> and leaves the doubled literal in a local
    /// named <c>probe</c>. Rendered at the mechanism's body indentation.
    /// </summary>
    private sealed record Binder(string Name, IReadOnlyList<string> Lines);

    private static readonly Binder[] Binders =
    {
        new("lambdaParam", new[]
        {
            $"f: (int) -> int = lambda {SharedName}: {SharedName} * 2",
            "probe: int = f(3)",
        }),
        new("nestedDefParam", new[]
        {
            $"def dbl({SharedName}: int) -> int:",
            $"    return {SharedName} * 2",
            "",
            "probe: int = dbl(3)",
        }),
        new("comprehensionTarget", new[]
        {
            $"doubled: list[int] = [{SharedName} * 2 for {SharedName} in [3]]",
            "probe: int = doubled[0]",
        }),
    };

    // --- Mechanisms ------------------------------------------------------------------------------

    /// <summary>
    /// One emitter site that opens an accessor-parameter rewrite, as a program with a
    /// <c>{BODY}</c> hole at <see cref="Indent"/> spaces which the binder fills.
    /// </summary>
    private sealed record Mechanism(
        string Name,
        string EmitterSite,
        string Template,
        int Indent,
        string LeakedOutcome,
        string[] Features);

    private static readonly Mechanism[] Mechanisms =
    {
        new("combinedFunctionStyle", "GenerateCombinedFunctionStyleProperty", $@"
class Box:
    total: int

    def __init__(self) -> None:
        self.total = 0

    property set slot(self, {SharedName}: int):
{{BODY}}
        self.total = probe

    property get slot(self) -> int:
        return self.total


def main() -> None:
    b: Box = Box()
    b.slot = {IncomingValue}
    print(b.slot)
", 8, LeakedOutput, Array.Empty<string>()),

        new("mixedAutoCustom", "GenerateMixedAutoCustomProperty", $@"
class Account:
    property balance: int

    property set balance(self, {SharedName}: int):
{{BODY}}
        self._balance = probe

    def __init__(self):
        self._balance = 0


def main() -> None:
    a: Account = Account()
    a.balance = {IncomingValue}
    print(a.balance)
", 8, LeakedOutput, Array.Empty<string>()),

        new("moduleLevelFunctionStyle", "GenerateFunctionStyleProperty", $@"
_backing: int = 0


property set level({SharedName}: int):
{{BODY}}
    _backing = probe


def read_level() -> int:
    return _backing


def main() -> None:
    level = {IncomingValue}
    print(read_level())
", 4, LeakedOutput, Array.Empty<string>()),

        new("interfaceDefault", "GenerateInterfacePropertyFromDef", $@"
interface IVolume:
    property set volume(self, {SharedName}: int):
{{BODY}}
        print(probe)


class Speaker(IVolume):
    pass


def main() -> None:
    s: IVolume = Speaker()
    s.volume = {IncomingValue}
", 8, LeakedOutput, Array.Empty<string>()),

        // The delegate-typed exception documented in the class remarks: a leak substitutes the
        // delegate into an int position, so it does not compile rather than printing 200 (measured
        // pre-fix: SPY0908 / CS0019 "Operator '*' cannot be applied to ClickHandler and int").
        // The delegate is user-declared rather than the builtin EventHandler because a
        // function-style event annotated EventHandler is refused outright by SPY0373 (#1512) —
        // an unrelated defect that would have made this cell measure that instead.
        new("eventAddAccessor", "RoslynEmitter.ClassMembers.Events.cs", $@"
delegate ClickHandler(sender: object) -> None


class Button:
    seen: int
    _handler: ClickHandler

    def __init__(self) -> None:
        self.seen = 0

    event add on_click(self, {SharedName}: ClickHandler):
{{BODY}}
        self.seen = probe
        self._handler = {SharedName}

    event remove on_click(self, {SharedName}: ClickHandler):
        self.seen = 0


def on_click_handler(sender: object) -> None:
    pass


def main() -> None:
    b: Button = Button()
    b.on_click += on_click_handler
    print(b.seen)
", 8, CompileFailed, Array.Empty<string>()),

        new("observerBeforeSet", "GenerateObserverBody (before_set → value)", $@"
class Character:
    total: int

    property health: int = 100
        before_set({SharedName}):
{{BODY}}
            self.total = probe

    def __init__(self):
        self.total = 0


def main() -> None:
    c: Character = Character()
    c.health = {IncomingValue}
    print(c.total)
", 12, LeakedOutput, new[] { "property_observers" }),

        new("observerAfterSet", "GenerateObserverBody (after_set → captured old-value local)", $@"
class Character:
    total: int

    property health: int = 100
        after_set({SharedName}):
{{BODY}}
            self.total = probe

    def __init__(self):
        self.total = 0


def main() -> None:
    c: Character = Character()
    c.health = {IncomingValue}
    print(c.total)
", 12, LeakedOutput, new[] { "property_observers" }),
    };

    // --- The sweep -------------------------------------------------------------------------------

    [Fact]
    public void EmitterRewrites_DoNotCrossShadowingBinders()
    {
        var stopwatch = Stopwatch.StartNew();
        var allowlist = LoadAllowlist();
        var results = new List<CellResult>();

        foreach (var mechanism in Mechanisms)
        {
            foreach (var binder in Binders)
            {
                results.Add(Measure(mechanism, binder));
            }
        }

        stopwatch.Stop();

        var leaking = results.Where(r => r.Verdict != "ok").ToList();
        var offenders = leaking.Where(r => !allowlist.Contains(r.Key)).ToList();
        var stale = results
            .Where(r => r.Verdict == "ok" && allowlist.Contains(r.Key))
            .Select(r => r.Key)
            .ToList();

        WriteReport(new
        {
            harness = "rewrite-scope-shadowing",
            contract = "an emitter identifier rewrite describes ONE binding and must not cross a "
                + "binder that re-binds the name (#1500; #1146 wrong-code class)",
            scopeNotes = new[]
            {
                "Mechanisms were enumerated from every AccessorParamRewrite( call site in RoslynEmitter*.",
                "Every cell is discriminating: the binder doubles the literal 3 while the accessor's "
                + "incoming value is 100, so honouring the binder prints 6 and leaking prints 200. "
                + "The harness fails a cell whose two candidate outcomes coincide.",
                "eventAddAccessor's value is a delegate, so its leak is a compile failure rather than "
                + "a wrong number — recorded as its leaked outcome rather than papered over.",
            },
            elapsedSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
            totals = new
            {
                cells = results.Count,
                ok = results.Count - leaking.Count,
                notOk = leaking.Count,
                allowlistSize = allowlist.Count,
                nonAllowlisted = offenders.Count,
                staleAllowlistEntries = stale.Count,
            },
            cells = results.Select(r => r.ToReport(allowlist)),
        });

        foreach (var r in results)
            Output.WriteLine($"{(r.Verdict == "ok" ? "  ok" : r.Verdict.ToUpperInvariant())}  {r.Key}");
        Output.WriteLine(
            $"cells: {results.Count}  not-ok: {leaking.Count}  allowlist: {allowlist.Count}  "
            + $"elapsed: {stopwatch.Elapsed.TotalSeconds:F1}s");

        stale.Should().BeEmpty(
            "an allowlist entry is deleted in the change that fixes its bug, or the ratchet stays "
            + "disarmed on a cell that has started working (docs/design/gap-discovery-contracts.md). "
            + "Delete these lines from Conformance/rewrite-shadowing-allowlist.txt:\n  "
            + string.Join("\n  ", stale));

        offenders.Should().BeEmpty(
            "a name re-bound by a lambda parameter, a nested def parameter or a comprehension target "
            + "is a different binding, and an accessor's name→slot mapping must stop at that boundary. "
            + "A cell that leaks compiles and prints the wrong number with no diagnostic (#1500). Fix "
            + "the emitter, or file an issue and add the printed cell key to "
            + "Conformance/rewrite-shadowing-allowlist.txt citing it.\n"
            + "Cells:\n"
            + string.Join("\n", offenders.Select(o => o.Describe()))
            + "\nFull report: .claude/tmp/rewrite-shadowing-conformance-report.json");
    }

    // --- Measurement -----------------------------------------------------------------------------

    private CellResult Measure(Mechanism mechanism, Binder binder)
    {
        var key = $"{mechanism.Name}::{binder.Name}";

        // The discrimination gate, asserted BEFORE the program runs: a cell whose correct and
        // leaked outcomes coincide measures nothing, and #1500's first probe was exactly that.
        mechanism.LeakedOutcome.Should().NotBe(ExpectedOutput,
            "cell '{0}' must be able to tell the two lowerings apart", key);

        var source = mechanism.Template.Replace("{BODY}", Indent(binder.Lines, mechanism.Indent));
        var features = mechanism.Features.Length == 0
            ? FeatureFlags.None
            : FeatureFlags.None.Enable(mechanism.Features);

        var result = CompileAndExecute(source, fileName: "rewrite_shadowing.spy", features: features);

        var actual = result.Success
            ? result.StandardOutput.Trim()
            : CompileFailed + " " + string.Join("; ", result.CompilationErrors);

        var verdict = actual == ExpectedOutput
            ? "ok"
            : actual.StartsWith(mechanism.LeakedOutcome, StringComparison.Ordinal)
                ? "leaked"
                : "broken";

        return new CellResult(key, mechanism, binder, source, actual, verdict);
    }

    /// <summary>
    /// Renders a binder's lines at <paramref name="spaces"/> indentation. Blank lines stay blank —
    /// trailing whitespace on an empty line is what the whitespace formatter would strip anyway,
    /// and an indented blank line inside a suite is meaningless to the lexer.
    /// </summary>
    private static string Indent(IReadOnlyList<string> lines, int spaces)
    {
        var pad = new string(' ', spaces);
        return string.Join("\n", lines.Select(l => l.Length == 0 ? string.Empty : pad + l));
    }

    private sealed record CellResult(
        string Key,
        Mechanism Mechanism,
        Binder Binder,
        string Source,
        string Actual,
        string Verdict)
    {
        public string Describe()
            => $"  {Key}  [{Mechanism.EmitterSite}]\n"
            + $"    expected: {ExpectedOutput}   leaked-outcome: {Mechanism.LeakedOutcome}   "
            + $"actual: {Actual}\n"
            + $"    program:\n{Source}";

        public object ToReport(Allowlist allowlist) => new
        {
            key = Key,
            mechanism = Mechanism.Name,
            emitterSite = Mechanism.EmitterSite,
            binder = Binder.Name,
            expected = ExpectedOutput,
            leakedOutcome = Mechanism.LeakedOutcome,
            actual = Actual,
            verdict = Verdict,
            allowlisted = allowlist.Contains(Key),
            program = Source,
        };
    }

    // --- allowlist + report I/O ------------------------------------------------------------------

    private static readonly Lazy<string?> AllowlistPath = new(FindAllowlistPath);

    private static Allowlist LoadAllowlist()
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var path = AllowlistPath.Value;
        if (path == null || !File.Exists(path))
        {
            throw new InvalidOperationException(
                "Conformance/rewrite-shadowing-allowlist.txt is missing. Its presence is what arms "
                + "the ratchet; deleting it would silently disarm the sweep rather than relax it.");
        }

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw;
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
                line = line.Substring(0, hash);
            line = line.Trim();
            if (line.Length > 0)
                exact.Add(line);
        }

        return new Allowlist(exact);
    }

    private static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
            if (Directory.Exists(dir))
                return Path.Combine(dir, "rewrite-shadowing-allowlist.txt");
            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }

    private void WriteReport(object report)
    {
        var reportDir = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(RewriteScopeShadowingConformanceTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", ".claude", "tmp"));
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "rewrite-shadowing-conformance-report.json");
        File.WriteAllText(reportPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Output.WriteLine($"Report written to: {reportPath}");
    }

    /// <summary>The reviewed conformance allowlist: exact <c>mechanism::binder</c> keys.</summary>
    internal sealed class Allowlist
    {
        private readonly HashSet<string> _exact;
        public Allowlist(HashSet<string> exact) => _exact = exact;
        public int Count => _exact.Count;
        public bool Contains(string key) => _exact.Contains(key);
    }
}
