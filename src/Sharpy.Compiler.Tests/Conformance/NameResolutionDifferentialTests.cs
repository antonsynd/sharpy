using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Differential;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>What the python twin of a cell is predicted to do (D2): computed from the axes, never read back from python.</summary>
public enum PyKind
{
    Ran,
    NameError,
    UnboundLocalError,
    OtherError,
    SyntaxError,
    TimedOut,
}

/// <summary>A python outcome. <see cref="Value"/> is the stdout for <see cref="PyKind.Ran"/>, the name for the two name
/// errors and the exception type for <see cref="PyKind.OtherError"/>.</summary>
internal sealed record PyOutcome(PyKind Kind, string Value)
{
    public static PyOutcome Ran(object stdout) => new(PyKind.Ran, Convert.ToString(stdout, CultureInfo.InvariantCulture)!);
    public static PyOutcome Name(string name) => new(PyKind.NameError, name);
    public static PyOutcome Unbound(string name) => new(PyKind.UnboundLocalError, name);
    public static PyOutcome Other(string type) => new(PyKind.OtherError, type);
    public override string ToString() => $"{Kind}({Value})";
}

/// <summary>The Sharpy tag of a cell (D2's table). Set by <c>Expect</c>, never per cell.</summary>
internal enum SharpyTag
{
    MustRun,
    MustRefuse,
    MustRefuseOrThrow,
    MayRefuse,
    Unruled,
}

internal enum Family
{
    Read,
    Store,
    TypeMember,
    Local,
    Seed,
}

/// <summary>The Sharpy outcome classes of D2.</summary>
internal enum SharpyKind
{
    Ran,
    Refused,
    Ice,
    RuntimeThrow,
    TimedOut,
    Harness,
}

/// <summary>The verdict classes of D2. Red = FalseRefusal, SilentWrong, RuntimeThrow, Ice when not allowlisted.</summary>
internal enum VerdictClass
{
    Match,
    RefuseOk,
    Unruled,
    FalseRefusal,
    SilentWrong,
    RuntimeThrow,
    Ice,
    GeneratorBug,
    Harness,
}

/// <summary>One generated cell: one Sharpy program, its twin (rendered from the source) and both predictions.</summary>
internal sealed record NameCell
{
    public required string Id { get; init; }
    public required Family Family { get; init; }
    public required string Source { get; init; }
    public required SharpyTag Tag { get; init; }

    /// <summary>The stdout a <see cref="SharpyTag.MustRun"/> cell must print.</summary>
    public string? Model { get; init; }
    public required PyOutcome Python { get; init; }

    /// <summary>The rule the tag cites (N1…N7, S1…S3, R-Y, R-KG, …).</summary>
    public required string Rule { get; init; }

    /// <summary>A <c>docs/deviations.yaml</c> row id when the python prediction differs from the model by design.</summary>
    public string? Deviation { get; init; }

    // Axes the closed-set assertions and the roster-fed assertions read.
    public BindingKind Target { get; init; } = BindingKind.None;
    public BindingKind Outer { get; init; } = BindingKind.None;
    public ReadPosition? Position { get; init; }
    public StoreForm? Form { get; init; }
    public LocalTarget? Construct { get; init; }
    public string? Shape { get; init; }

    /// <summary>P77b axis 8: the assignment target shape of an attribute STORE (a <c>FirstInvalidAssignmentTarget</c> arm name;
    /// null: the bare <c>Identifier</c> forms of the STORE family) and its receiver.</summary>
    public string? TargetShape { get; init; }
    public AttributeReceiver? Receiver { get; init; }

    /// <summary>P77b axis 7: the READ class cell's host type kind when it is not the position's own (null: the position's host,
    /// <c>HostHeader</c>'s class / <c>@dataclass</c> / struct initializer host).</summary>
    public Sharpy.Compiler.Semantic.TypeKind? Host { get; init; }

    /// <summary>P77b axis 10: where the type-level member a READ names is declared, relative to the reading type (null: a cell
    /// with no declaring type, or one the relation axis does not tag).</summary>
    public DeclaringRelation? Relation { get; init; }

    /// <summary>P77b axis 12: the <c>global</c> / <c>nonlocal</c> declaration shape and keyword of a STORE cell.</summary>
    public DeclarationSite? Declaration { get; init; }

    /// <summary>P77b axis 4: when a LOCAL read happens relative to its binding (null: a family with no LOCAL read timing).</summary>
    public ReadTiming? Timing { get; init; }

    /// <summary>P77b axis 3: what the cell observes. Every cell observes a value; an order cell also observes initializer order.</summary>
    public Observation Observed { get; init; } = Observation.Value;

    /// <summary>P77b axis 2: the N3 cycle's endpoint pair, route and endpoint kinds.</summary>
    public CycleRoute? Cycle { get; init; }

    /// <summary>P77b axis 1: the comprehension kind, clause and slot the cell's name sits in.</summary>
    public ComprehensionSite? Comprehension { get; init; }

    /// <summary>Set by the type-position cells: which value of roster 6 this cell feeds (D3.c).</summary>
    public TypePosition? TypeAt { get; init; }

    /// <summary>P77b axis 11: how a type-position cell spells the type (Bare: <c>Later</c>; Alias: <c>type A = Later</c>).</summary>
    public NameResolutionDifferentialTests.Spelling? TypeSpelling { get; init; }

    /// <summary>P77b axis 11: the value a <c>T !E</c> function returns (null: not a result-error cell).</summary>
    public ResultValueForm? ResultForm { get; init; }

    /// <summary>Set by SEED cells: the error codes the refusal may carry, from the seed table (a seed's rule names none).</summary>
    public IReadOnlySet<string>? SeedRefusalCodes { get; init; }

    /// <summary>The Sharpy source the twin renders from when python has no spelling of the construct (a module accessor).</summary>
    public string? TwinSource { get; init; }

    /// <summary>P77b D2.9: the source imports a Sharpy.Stdlib module, so <c>RunSharpy</c> references the monolith for this
    /// cell alone. Set by the generator on the cells it builds over the <c>ImportedName</c> outer, never read off the source.</summary>
    public bool NeedsStdlib { get; init; }

    /// <summary>Set by SEED cells: the table's issue cite for a red.</summary>
    public string? SeedCite { get; init; }

    /// <summary>The inventory's python verdict, compared in the runner's normal form instead of <see cref="Python"/>.</summary>
    public string? SeedPythonVerdict { get; init; }
}

public partial class NameResolutionDifferentialTests
{
    private const int SharpyExecTimeoutMs = 12_000;
    private const int PythonBatchTimeoutMs = 3_600_000;
    private const string AllowlistFileName = "name-resolution-differential-allowlist.txt";
    private const string StrideEnv = "NAME_RES_DIFF_LIMIT";

    /// <summary>D3's budget: the pool's size, with the 13-minute dev wall as its other half. P77b D1 (R-KP): 8,500 held the
    /// restored READ outers and builtins; the lead's budget ruling 2026-11-06 (plan checklist) generates the twelve axes whole and
    /// rounds the measured pool up to the next 500 (8,516 at axis 8 → 9,000), the dev wall staying ≤ 13 min; the wall, not this
    /// count, is the binding limit.</summary>
    private const int CellBudget = 9000;

    /// <summary>Edit-loop only: a JSON file of Sharpy arm results keyed by source hash. A run that reads it prints the
    /// CACHED banner and is never a gate: a cached verdict cannot see a compiler change.</summary>
    private const string CacheEnv = "NAME_RES_DIFF_SHARPY_CACHE";

    /// <summary>D2's ICE set: any of these in <c>RawDiagnostics</c> is an internal failure, never a refusal.</summary>
    internal static readonly HashSet<string> IceCodes = new(StringComparer.Ordinal)
    {
        "SPY0904", "SPY0906", "SPY0907", "SPY0908", "SPY0909", "SPY0510",
    };

    /// <summary>Infrastructure failures: the harness could not judge the program. SPY0901 is cancellation-only and
    /// SPY0912 is formatter-only; neither can surface under <c>CompileAndExecute</c>.</summary>
    private static readonly HashSet<string> InfrastructureCodes = new(StringComparer.Ordinal)
    {
        "SPY0902", "SPY0903", "SPY0910", "SPY0911", "SPY0913",
    };

    /// <summary>Summary rows that never classify: SPY0905 sits beside real errors, SPY0900 is the REPL's.</summary>
    private static readonly HashSet<string> IgnoredCodes = new(StringComparer.Ordinal) { "SPY0905", "SPY0900" };

    private static readonly Regex NameErrorLine = new(@"^NameError: name '(\w+)' is not defined", RegexOptions.CultureInvariant);
    private static readonly Regex FreeVariableLine = new(@"^NameError: cannot access free variable '(\w+)'", RegexOptions.CultureInvariant);
    private static readonly Regex UnboundLine = new(@"^UnboundLocalError: cannot access local variable '(\w+)'", RegexOptions.CultureInvariant);
    private static readonly Regex ExceptionTypeLine = new(@"^([A-Za-z_][\w.]*)(?::|$)", RegexOptions.CultureInvariant);
    private static readonly Regex IssueCite = new(@"#\d+", RegexOptions.CultureInvariant);

    /// <summary>What the Sharpy arm observed, the only input of <see cref="ClassifySharpy"/>. It is plain data so an edit
    /// loop can cache it (<see cref="CacheEnv"/>); the classifier still runs over it on every run.</summary>
    internal sealed record SharpyRun(
        bool Success,
        int? ExitCode,
        bool ThrewInHarness,
        bool TimedOut,
        string Stdout,
        string Stderr,
        IReadOnlyList<string> ErrorCodes,
        IReadOnlyList<string> AllCodes,
        string? CsCode);

    /// <summary>D2's Sharpy outcome: reads codes AND the exit path. A run-time throw is read positively from
    /// <c>ExitCode</c> and any other combination is Harness, never a verdict.</summary>
    internal static SharpyKind ClassifySharpy(SharpyRun run)
    {
        if (run.TimedOut)
            return SharpyKind.TimedOut;
        if (run.ThrewInHarness || run.AllCodes.Any(IceCodes.Contains))
            return SharpyKind.Ice;
        if (run.AllCodes.Any(InfrastructureCodes.Contains))
            return SharpyKind.Harness;
        if (run.ErrorCodes.Any(c => !IgnoredCodes.Contains(c)))
            return SharpyKind.Refused;
        if (run.ExitCode == 0 && run.Success)
            return SharpyKind.Ran;
        if (run.ExitCode is int code && code != 0)
            return SharpyKind.RuntimeThrow;
        return SharpyKind.Harness;
    }

    /// <summary>D2's python outcome: kind and name from the LAST stderr line, prefix-anchored because python 3.12
    /// appends a <c>Did you mean</c> suggestion to the same line.</summary>
    internal static PyOutcome ClassifyPython(PyResult result)
    {
        if (result.TimedOut)
            return new PyOutcome(PyKind.TimedOut, "");
        if (result.SyntaxError)
            return new PyOutcome(PyKind.SyntaxError, LastLine(result.Stderr));
        if (result.Ok)
            return PyOutcome.Ran(Normalize(result.Stdout));

        var last = LastLine(result.Stderr);
        var match = NameErrorLine.Match(last);
        if (match.Success)
            return PyOutcome.Name(match.Groups[1].Value);
        match = FreeVariableLine.Match(last);
        if (match.Success)
            return PyOutcome.Name(match.Groups[1].Value);
        match = UnboundLine.Match(last);
        if (match.Success)
            return PyOutcome.Unbound(match.Groups[1].Value);
        match = ExceptionTypeLine.Match(last);
        return PyOutcome.Other(match.Success ? match.Groups[1].Value : last);
    }

    private static string LastLine(string text)
        => text.Split('\n').Select(l => l.TrimEnd()).LastOrDefault(l => l.Length > 0) ?? "";

    internal static string Normalize(string stdout)
        => string.Join("\n", stdout.Replace("\r\n", "\n").TrimEnd().Split('\n').Select(l => l.TrimEnd()));

    /// <summary>D2's table: tag × Sharpy outcome → verdict class, the python control already passed.</summary>
    internal static VerdictClass Judge(NameCell cell, SharpyKind kind, string stdout)
    {
        if (kind is SharpyKind.TimedOut or SharpyKind.Harness)
            return VerdictClass.Harness;
        if (kind == SharpyKind.Ice)
            return VerdictClass.Ice;

        switch (cell.Tag)
        {
            case SharpyTag.Unruled:
                // The question is what the program should mean, and no answer to it is an uncaught CLR exception.
                return kind == SharpyKind.RuntimeThrow ? VerdictClass.RuntimeThrow : VerdictClass.Unruled;
            case SharpyTag.MustRun:
                return kind switch
                {
                    SharpyKind.Ran => Normalize(stdout) == cell.Model ? VerdictClass.Match : VerdictClass.SilentWrong,
                    SharpyKind.Refused => VerdictClass.FalseRefusal,
                    _ => VerdictClass.RuntimeThrow,
                };
            case SharpyTag.MustRefuse:
                return kind switch
                {
                    SharpyKind.Refused => VerdictClass.Match,
                    SharpyKind.Ran => VerdictClass.SilentWrong,
                    _ => VerdictClass.RuntimeThrow,
                };
            case SharpyTag.MustRefuseOrThrow:
                return kind == SharpyKind.Ran ? VerdictClass.SilentWrong : VerdictClass.Match;
            case SharpyTag.MayRefuse:
                return kind switch
                {
                    SharpyKind.Refused => VerdictClass.RefuseOk,
                    SharpyKind.Ran => Normalize(stdout) == cell.Model ? VerdictClass.Match : VerdictClass.SilentWrong,
                    _ => VerdictClass.RuntimeThrow,
                };
            default:
                throw new InvalidOperationException($"Unhandled tag {cell.Tag}");
        }
    }

    /// <summary>The python control (D2): a python outcome the cell did not predict is a generator bug, never allowlistable.</summary>
    private static string? PythonControlFailure(NameCell cell, PyOutcome observed)
    {
        // A seed's prediction is its recorded verdict, and TwinRenderer_SeedPythonVerdicts_Reproduce holds the twin to that record.
        if (observed != cell.Python)
            return $"predicted {cell.Python}, python {observed}";

        // D2: a must-run program either runs in python or fails only on declaration order. A tag cannot call anything else must-run.
        if (cell.Tag == SharpyTag.MustRun && observed.Kind is not (PyKind.Ran or PyKind.NameError))
            return $"tagged must-run and python raises {observed}";

        // Acceptance 3: where python runs a must-run program, python and the model agree. Only a named deviation may differ.
        if (cell.Tag is SharpyTag.MustRun or SharpyTag.MayRefuse && observed.Kind == PyKind.Ran
            && observed.Value != cell.Model && cell.Deviation == null)
            return $"model '{cell.Model}' differs from python's '{observed.Value}' and the cell names no deviation";
        return null;
    }

    /// <summary>A rule that names a code. A must-refuse cell refused under another code is the right direction for the wrong
    /// reason, so it is read here: a code drifting (SPY0290 to SPY0200) stays visible, which the verdict class cannot show.</summary>
    private static readonly Regex RuleNamesCode = new(@"^(?:kind:)?(SPY\d{4})$", RegexOptions.CultureInvariant);

    /// <summary>The refusals that fire before the rule's own code today, with their counts. Anything else is red.</summary>
    private static readonly IReadOnlyDictionary<(string Rule, string Seen), int> EarlierRefusals = new Dictionary<(string, string), int>
    {
        // `global x` over a const: SPY0134 refuses the declaration until #2424 reinstates it, then SPY0225 is reached.
        [("kind:SPY0225", "SPY0134")] = 6,
        // A later member in a constant-only initializer: the name is not visible yet, so SPY0401 is not reached.
        [("SPY0401", "SPY0200")] = 9,
        // P77b D1's restored ModuleEnum outer: `x.RED.value` on an int member is an undefined member before SPY0401 is reached.
        [("SPY0401", "SPY0203")] = 12,
        // `x += 1` on a method's name in a method body: the undefined local is reported, so SPY0606 is not reached.
        [("SPY0606", "SPY0200")] = 4,
        // P77b axis 8 (R-LH): an instance method stored through a type (`H.x = lambda: 9`, `A.`, `D.`) is refused as an
        // instance member read through a type before the method store's own refusal is reached: refused by direction.
        [("kind:SPY0606", "SPY0290")] = 3,
        // P77b R-LJ: a module binding spelled like an imported name is SPY0204 at its own declaration, before N2's module
        // refusal of the read above it is reached (p23): refused by direction, pinned until N2's own diagnostic lands (#2423).
        [("N2-module", "SPY0204")] = 3,
        // P77b axis 12: the parser refuses every `global` / `nonlocal` line (SPY0134) until #2424 implements N6, so each N6
        // declaration shape is refused by direction before its own refusal is reached.
        [("N6-declaration", "SPY0134")] = 80,
        // P77b D6 R-LI (D21): the lambda-typed local called above its binding under a non-callable outer binds the outer and is
        // refused as a call on it (SPY0230), before #2425's "declared later" is reached: refused by direction.
        [("N5-declared-later", "SPY0230")] = 12,
        // P77b D6 R-KY (D11): a store to a def / type-kind name, refused today by its neighbours until #2424's own code lands —
        // `global f` by the parser (SPY0134), `f += 1` as a type error on the def / type read (SPY0222 / SPY0342 / SPY0346).
        [("N6-type-kind", "SPY0134")] = 21,
        [("N6-type-kind", "SPY0222")] = 2,
        [("N6-type-kind", "SPY0342")] = 6,
        [("N6-type-kind", "SPY0346")] = 6,
    };

    /// <summary>The error codes a refusal may carry, per rule. A refusal is only agreement when it is THIS rule's refusal: a
    /// program refused for an unrelated reason would otherwise read Match and hide the cell. Today N2, N3 and N6 are refused
    /// by the neighbouring checks named here, because the rule's own diagnostic lands with #2423, #2422 and #2424.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> AdmittedRefusals = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        // SPY0618 is P75's forward refusal, which the principle retires: 1 comprehension cell reaches it before the name check.
        ["N1-methods"] = new[] { "SPY0200", "SPY0618" },
        // The outer binding is read and the read is ill-typed. SPY0401 is the default position's own refusal beside it. SPY0320
        // is SPY0230's neighbour for the ImportedName read (P77b axis 9): `len(digits)` on the int member has no `__len__`.
        ["N1-type-error"] = new[] { "SPY0200", "SPY0203", "SPY0230", "SPY0320", "SPY0401" },
        // SPY0203 is SPY0230's neighbour for the EnumValue form (P77b D1's restored ModuleEnum outer): `x.RED.value` on the
        // later int member the read wrongly binds is an undefined member, as `x(...)` on it is not callable. SPY0320 is the
        // same for the ImportedName read (P77b axis 9): `len(digits)` on the later int member has no `__len__`.
        ["N2"] = new[] { "SPY0203", "SPY0230", "SPY0278", "SPY0320", "SPY0401", "SPY0618" },
        // P77b R-LJ: SPY0204 is the module binding re-declaring an imported name (pinned in EarlierRefusals); SPY0230 is the
        // later binding the read wrongly binds, called or measured as the wrong kind.
        ["N2-module"] = new[] { "SPY0204", "SPY0230" },
        // SPY0340 is the module `let` declaration refused beside the cycle.
        ["N3-cycle"] = new[] { "SPY0200", "SPY0278", "SPY0340", "SPY0618" },
        ["N5"] = new[] { "SPY0200", "SPY0230", "SPY0600" },
        // P77b axis 4 (R-KX / R-LC): with no outer binding, a read above (a back edge, a closure taken before `out let`) its own
        // binding is an undefined name at the read: SPY0200 is the name verdict itself, no type is involved. P77b D6 R-LI: SPY0230
        // is the lambda-typed local called above its binding under a non-callable outer — the read binds the outer (refused by
        // direction, pinned in EarlierRefusals until #2425's "declared later" lands).
        ["N5-declared-later"] = new[] { "SPY0200", "SPY0230" },
        // P77b D6 R-LE (D17): the lambda-typed local reading itself stays refused; the steer is message-only, never compared.
        ["N5-self"] = new[] { "SPY0200" },
        // SPY0222: `abs += 1` under a builtin is refused as a type error before N6's own refusal exists (#2424). SPY0336 and
        // SPY0342 are the same read of the builtin for an overloaded one (`max`) and a type name (`int`), P77b D1's restore.
        ["N6"] = new[] { "SPY0200", "SPY0222", "SPY0336", "SPY0342" },
        // P77b axis 12: SPY0134 is the parser's refusal of the declaration line itself, pinned in EarlierRefusals until #2424.
        ["N6-declaration"] = new[] { "SPY0134" },
        // P77b D6 R-KY (D11): the store's own code lands with #2424. Today `global f` is the parser's SPY0134, `f += 1` the type
        // errors SPY0222 / SPY0342 / SPY0346 on the def or type read: each pinned in EarlierRefusals as a refusal by direction.
        ["N6-type-kind"] = new[] { "SPY0134", "SPY0222", "SPY0342", "SPY0346" },
        ["R-KG"] = new[] { "SPY0200" },
        ["R1(b)"] = new[] { "SPY0200" },
        ["S3"] = new[] { "SPY0200", "SPY0230" },
        ["SPY0401"] = new[] { "SPY0200", "SPY0203", "SPY0230", "SPY0320", "SPY0401" },
        ["SPY0606"] = new[] { "SPY0200", "SPY0606" },
        // P77b axis 5: a block's `def` over a `def` of the same function in an enclosing block (function_definition.md).
        ["kind:SPY0204"] = new[] { "SPY0204" },
        // P77b axis 8 (R-LH): SPY0290 is the instance method stored through a type, pinned in EarlierRefusals.
        ["kind:SPY0606"] = new[] { "SPY0290", "SPY0606" },
        ["kind:SPY0225"] = new[] { "SPY0134", "SPY0225" },
        ["kind:SPY0290"] = new[] { "SPY0290", "SPY0401" },
        ["kind:SPY0340"] = new[] { "SPY0200", "SPY0340", "SPY0401" },
        ["kind:enum-member-constant"] = new[] { "SPY0200" },
        ["kind:instance-at-initializer"] = new[] { "SPY0200", "SPY0230", "SPY0401" },
        // P77b axis 11: SPY0260 refuses the bare `return v` from `T !E` (the spec requires Ok): the refusal IS the verdict.
        ["kind:result-wrap"] = new[] { "SPY0260" },
    };

    /// <summary>Is every error code of this refusal one its rule admits? A rule with no entry admits nothing.</summary>
    internal static string? RefusalReasonFailure(NameCell cell, SharpyRun run)
    {
        var admitted = cell.SeedRefusalCodes
            ?? (AdmittedRefusals.TryGetValue(cell.Rule, out var codes) ? codes.ToHashSet(StringComparer.Ordinal) : null);
        if (admitted == null)
            return $"RefusalReason: {cell.Id}: rule {cell.Rule} has no admitted codes";
        var foreign = run.ErrorCodes.Where(c => !IgnoredCodes.Contains(c) && !admitted.Contains(c)).Distinct().ToList();
        return foreign.Count == 0 ? null : $"RefusalReason: {cell.Id}: rule {cell.Rule} does not admit {string.Join(",", foreign)}";
    }

    private static IEnumerable<string> RefusalCodeFailures(IReadOnlyList<CellResult> results, bool partial)
    {
        var seen = new Dictionary<(string Rule, string Seen), int>();
        foreach (var r in results.Where(r => r.Kind == SharpyKind.Refused && r.Cell.Tag is SharpyTag.MustRefuse or SharpyTag.MustRefuseOrThrow or SharpyTag.MayRefuse))
        {
            if (RefusalReasonFailure(r.Cell, r.Run) is string reason)
                yield return reason;
        }
        foreach (var r in results.Where(r => r.Kind == SharpyKind.Refused))
        {
            var named = RuleNamesCode.Match(r.Cell.Rule);
            var first = r.Run.ErrorCodes.First(c => !IgnoredCodes.Contains(c));
            if (!named.Success)
            {
                // A rule that names no code is held to its admitted codes above; an earlier refusal pinned for it is counted.
                if (EarlierRefusals.ContainsKey((r.Cell.Rule, first)))
                    seen[(r.Cell.Rule, first)] = seen.GetValueOrDefault((r.Cell.Rule, first)) + 1;
                continue;
            }
            if (r.Run.ErrorCodes.Contains(named.Groups[1].Value))
                continue;
            if (!EarlierRefusals.ContainsKey((r.Cell.Rule, first)))
                yield return $"RefusalCode: {r.Cell.Id} names {r.Cell.Rule} and was refused with {first}";
            seen[(r.Cell.Rule, first)] = seen.GetValueOrDefault((r.Cell.Rule, first)) + 1;
        }
        if (partial)
            yield break;
        foreach (var (key, count) in EarlierRefusals.Where(e => seen.GetValueOrDefault(e.Key) != e.Value))
            yield return $"RefusalCode: {key.Rule} refused with {key.Seen} is pinned at {count} cells and measured {seen.GetValueOrDefault(key)}";
    }

    /// <summary>A row of the allowlist: the class it pins, the issue that drains it and its lane.</summary>
    internal sealed record AllowRow(VerdictClass Class, string Cite);

    /// <summary>D4's loader, in the shape of <c>TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist</c>: a row names a
    /// generated cell, names a red class, cites an issue, appears once. No globs.</summary>
    internal static Dictionary<string, AllowRow> LoadAllowlist(IReadOnlySet<string> universe, out List<string> problems)
    {
        problems = new List<string>();
        var path = PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Conformance", AllowlistFileName)
            ?? throw new InvalidOperationException($"Conformance/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");
        var rows = new Dictionary<string, AllowRow>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var head = split < 0 ? line : line[..split].Trim();
            var cite = split < 0 ? "" : line[(split + 2)..].Trim();
            var space = head.LastIndexOf(' ');
            if (space <= 0 || !Enum.TryParse<VerdictClass>(head[(space + 1)..], out var cls)
                || cls is not (VerdictClass.Ice or VerdictClass.FalseRefusal or VerdictClass.SilentWrong or VerdictClass.RuntimeThrow))
            {
                problems.Add($"'{line}' must be '<cell-id> <Ice|FalseRefusal|SilentWrong|RuntimeThrow> # #<issue> <lane>'");
                continue;
            }
            var id = head[..space].Trim();
            if (!IssueCite.IsMatch(cite))
                problems.Add($"'{line}' must cite an issue (#N)");
            if (!universe.Contains(id))
                problems.Add($"'{id}' names no generated cell");
            if (!rows.TryAdd(id, new AllowRow(cls, cite)))
                problems.Add($"'{id}' is listed twice");
        }
        return rows;
    }

    /// <summary>The row a red cell would carry (D4): the IMPLEMENTING issue of its class, whose fix turns it green.</summary>
    internal static string ProposedCite(NameCell cell, VerdictClass verdict, SharpyRun run)
    {
        if (cell.SeedCite != null)
            return $"{cell.SeedCite} {Lane(cell, verdict)}";
        return $"{ImplementingIssue(cell, verdict, run)} {Lane(cell, verdict)}";
    }

    private static string Lane(NameCell cell, VerdictClass verdict) => verdict switch
    {
        VerdictClass.Ice => "ice",
        VerdictClass.FalseRefusal => "false-refusal",
        VerdictClass.RuntimeThrow => "runtime-throw",
        VerdictClass.SilentWrong when cell.Tag == SharpyTag.MustRefuse => "missing-refusal",
        _ => "silent-wrong",
    };

    private static string ImplementingIssue(NameCell cell, VerdictClass verdict, SharpyRun run)
    {
        switch (verdict)
        {
            case VerdictClass.Ice:
                // P77b axis 8 (lead ruling 2026-11-06): a member store the checker lets through to Roslyn (CS0131 on a const,
                // CS0176 on a `@static` member through `self.`) is #2435's class — keyed on the attribute-store cell, never on
                // the CS code, so #2147's CS0176 READ receivers keep their route.
                if (cell.TargetShape == "MemberAccess")
                    return "#2435";
                if (run.AllCodes.Contains("SPY0909"))
                    return "#2398";
                return run.CsCode switch
                {
                    "CS0176" => "#2147",
                    // P77b axis 7 (R-KT): a struct `self` captured by a C# lambda or local function reaches Roslyn unrefused (lead
                    // ruling 2026-11-06: #2434's class).
                    // A struct `self.s()` on a `@static` method at a closure: #2147's `this.S()` lowering (the class host's same
                    // cells are CS0176 rows on #2147) seen through the struct closure (lead ruling 2026-11-06).
                    "CS1673" when cell.Target == BindingKind.StaticMethod => "#2147",
                    "CS1673" => "#2434",
                    // P77b axis 10 (p08): a nested type reading its container's other nested type bare is #2397's nested-type
                    // read. Keyed on the sibling relation, never on the NestedType target: the 154 own-body NestedType CS1955
                    // rows keep their #2423 route (lead ruling 2026-11-06).
                    "CS1955" when cell.Relation == DeclaringRelation.Sibling => "#2397",
                    // Checked first: `x().v` through `type x = T` is CS0103 in a plain main, with no store, lambda or order in it.
                    "CS0103" when IsAliasConstructorCell(cell) => "#1925",
                    // P77b axis 11 (lead ruling 2026-11-06): a type position spelled through `type A = Later` that the emitter
                    // writes as a value (`isinstance(o, A)`) is #1925's alias-position class.
                    "CS0103" when cell.TypeSpelling == Spelling.Alias => "#1925",
                    "CS0103" when cell.Family == Family.Store => "#2151",
                    "CS0103" when cell.Source.Contains("lambda", StringComparison.Ordinal) => "#2420",
                    // The checker and the emitter disagree on which binding a call means (#2150's route); N1 makes them agree.
                    // P77b axis 11 (p16, lead ruling 2026-11-06): a bare `return v` from `T !E` with `E` declared later reaches
                    // Roslyn unwrapped; the earlier order is refused SPY0260 (#2249: the result wrap, the later-declared type).
                    "CS0029" when cell.ResultForm == ResultValueForm.Bare => "#2249",
                    "CS1955" or "CS8917" or "CS1729" or "CS0029" => "#2423",
                    // A position that needs a constant was handed something else and nothing refused it.
                    "CS0133" or "CS1736" => "#2432",
                    "CS0149" or "CS0266" or "CS1061" when cell.Target == BindingKind.NestedType => "#2397",
                    "CS0149" or "CS0266" when cell.Target == BindingKind.StaticProperty => "#2417",
                    // A later binding reaches the emitter untyped; N1's order-free resolution types it.
                    "CS0149" or "CS0266" or "CS1503" => "#2423",
                    // A builtin's overload set reached the emitter for a later module binding of its name (p15, `max(1, 2)`
                    // above `max: int = 7`, P77b D1's restored builtin): N1's order-free resolution binds the module name.
                    "CS1501" => "#2423",
                    // `x.RED.value` the checker bound to the module enum outer while C# binds the class member (P77b D1's
                    // restored ModuleEnum outer): the checker and the emitter disagree on the binding, and N1 makes them agree.
                    "CS1061" when cell.Outer == BindingKind.ModuleEnum => "#2423",
                    // An imported name read bare in a method beside a same-named member (P77b axis 9): the checker binds the import
                    // and the emitter qualifies it as the module class's member. Routed with the N1 class's checker / emitter
                    // disagreement; the H2 findings-ledger line asks the lead to confirm the class.
                    "CS0117" when cell.Outer == BindingKind.ImportedName => "#2423",
                    // No default arm: an ICE nobody has triaged is red until a route names the check that cures it.
                    _ => $"UNROUTED({run.CsCode})",
                };
            case VerdictClass.FalseRefusal:
                if (cell.Family == Family.TypeMember)
                    return IsAliasConstructorCell(cell) ? "#1925" : "#2249";
                if (cell.Family == Family.Store)
                    return run.AllCodes.Contains("SPY0134") ? "#2424"
                        : run.AllCodes.Contains("SPY0600") ? "#2423"
                        : $"UNROUTED({string.Join(",", run.ErrorCodes)})";
                // P77b axis 11 (p17, lead ruling 2026-11-06): a type position spelled through an alias and refused (`class C(A)`)
                // is #1925's alias-position class.
                if (cell.TypeSpelling == Spelling.Alias)
                    return "#1925";
                return cell.Position == ReadPosition.ModuleInitializer && cell.Rule.StartsWith("N3", StringComparison.Ordinal)
                    ? "#2422"
                    : "#2423";
            case VerdictClass.RuntimeThrow:
                return "#2422";
            case VerdictClass.SilentWrong:
                if (cell.Rule == "N3-cycle" && cell.Id.Contains("/Alias/", StringComparison.Ordinal))
                    return "#2412";
                if (cell.Family == Family.TypeMember)
                    return "#2249";
                // P77b axis 8 (lead ruling 2026-11-06): a member store that runs where it must be refused (R-KU's `@static`
                // member through `self.` / `D.` / `h0.`, R-LH's method) is #2435's: "a store that cannot run is refused".
                // P77b axis 8 (lead ruling 2026-11-06): an augmented `self.x op= v` at the top of `__init__` is emitted as
                // `this.X = v` — a lowering defect, not a store to refuse.
                if (cell.TargetShape == "MemberAccess" && cell.Rule == "N7" && cell.Position == ReadPosition.InitBody && cell.Form == StoreForm.Augmented)
                    return "#2439";
                if (cell.TargetShape == "MemberAccess" && cell.Rule is "R-KU" or "kind:SPY0606")
                    return "#2435";
                if (cell.Family == Family.Store)
                    return "#2424";
                if (cell.Family == Family.Local)
                    // N5-declared-later is #2425's (R-KX): a read above the local's binding that reads the outer instead.
                    return cell.Rule.StartsWith("S2", StringComparison.Ordinal) || cell.Rule == "N5-declared-later" ? "#2425" : "#2423";
                // Only a read with no other binding can print a storage default; values-ready cannot change which binding is read.
                return cell.Tag == SharpyTag.MustRefuse || cell.Outer != BindingKind.None ? "#2423" : "#2422";
            default:
                throw new InvalidOperationException($"{verdict} is not a red class");
        }
    }

    private static bool IsAliasConstructorCell(NameCell cell)
        => cell.Target == BindingKind.ModuleTypeAlias || cell.Outer == BindingKind.ModuleTypeAlias
            || (cell.Family == Family.TypeMember && cell.Id.Contains("/alias/", StringComparison.Ordinal));

    /// <summary>P77b D2.9: set by <see cref="RunSharpy"/> for the one compile it wraps and read back by the override below, in
    /// the shape of <c>DifferentialExecutionTests._currentProgramNeedsStdlib</c>. The Sharpy arm is a sequential loop over
    /// one test instance, so a field is the whole mechanism; every cell that does not ask keeps the Core-only reference.</summary>
    private bool _currentCellNeedsStdlib;

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => _currentCellNeedsStdlib ? new[] { SharpyStdlibReference.Location } : Enumerable.Empty<string>();

    private SharpyRun RunSharpy(string source, int executionTimeoutMs = SharpyExecTimeoutMs, bool needsStdlib = false)
    {
        bool wasMuted = MuteCompilePath;
        MuteCompilePath = true;
        _currentCellNeedsStdlib = needsStdlib;
        ExecutionResult r;
        try
        {
            r = CompileAndExecute(source, "name_resolution.spy", executionTimeoutMs: executionTimeoutMs);
        }
        finally
        {
            MuteCompilePath = wasMuted;
            _currentCellNeedsStdlib = false;
        }
        var diagnostics = r.RawDiagnostics;
        var all = diagnostics.Select(d => d.Code).Where(c => c != null).Select(c => c!).ToList();
        var errors = diagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
            .Select(d => d.Code).Where(c => c != null).Select(c => c!).ToList();
        var cs = diagnostics.Where(d => d.Code == "SPY0908")
            .Select(d => Regex.Match(d.Message, @"\bCS\d{4}\b"))
            .Where(m => m.Success).Select(m => m.Value).FirstOrDefault();
        return new SharpyRun(r.Success, r.ExitCode, r.Exception != null, r.TimedOut,
            r.StandardOutput, r.StandardError, errors, all, cs);
    }

    /// <summary>D4.3: a Sharpy run that times out is re-run ONCE at twice the timeout before <see cref="ClassifySharpy"/> sees
    /// it, so one slow cell on a loaded machine is not a red; a second timeout stays a timeout (Harness, red). The run is a
    /// parameter so the retry is testable without a real hang.</summary>
    internal static SharpyRun RunWithRetry(Func<string, int, SharpyRun> run, string source, int timeoutMs, out bool retried)
    {
        var first = run(source, timeoutMs);
        retried = first.TimedOut;
        return first.TimedOut ? run(source, 2 * timeoutMs) : first;
    }

    private static string SourceKey(string source)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));

    [SkippableFact]
    public void NameResolutionDifferential_Sweep()
    {
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 12));
        var wall = Stopwatch.StartNew();

        var pool = GeneratedCells();
        var failures = new List<string>();

        // The generator's own totality: cell ids are unique, and every exemption tag is in its closed set.
        foreach (var dup in pool.GroupBy(c => c.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).Take(20))
            failures.Add($"duplicate cell id {dup.Key}");
        failures.AddRange(ClosedSetFailures(pool));
        failures.AddRange(CensusFailures(pool));
        failures.AddRange(RosterFedFailures(pool));
        if (pool.Count > CellBudget)
            failures.Add($"the pool is {pool.Count} cells and the budget is {CellBudget}: prune in D3.e's order");

        int limit = int.TryParse(Environment.GetEnvironmentVariable(StrideEnv), out var parsed) ? parsed : 0;
        bool partial = limit > 0 && limit < pool.Count;
        var cells = partial
            ? pool.Where((_, i) => i % Math.Max(1, pool.Count / limit) == 0).ToList()
            : pool;

        // --- Python arm: one batch. A twin the renderer cannot produce is a generator bug, never a skip. ---
        var requests = new List<PyRequest>(cells.Count);
        var twinFailures = new Dictionary<int, string>();
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].Family == Family.Seed)
                continue; // the seed's python verdicts are TwinRenderer_SeedPythonVerdicts_Reproduce's control
            try
            {
                requests.Add(new PyRequest(i, TwinRenderer.Render(cells[i].TwinSource ?? cells[i].Source)));
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
            {
                twinFailures[i] = $"renderer: {ex.Message}";
            }
        }
        var pythonById = oracle.RunBatch(requests, PythonBatchTimeoutMs);

        // --- Sharpy arm: production compile + execute, sequential, `run` and never `emit`. ---
        var cachePath = Environment.GetEnvironmentVariable(CacheEnv);
        var cache = LoadCache(cachePath);
        int cached = 0;
        int retried = 0;
        var timing = new ArmTiming();
        var runs = new SharpyRun[cells.Count];
        for (int i = 0; i < cells.Count; i++)
        {
            var key = SourceKey(cells[i].Source);
            if (cachePath != null && cache.TryGetValue(key, out var hit))
            {
                runs[i] = hit;
                cached++;
            }
            else
            {
                var needsStdlib = cells[i].NeedsStdlib;
                var started = Stopwatch.GetTimestamp();
                runs[i] = RunWithRetry((src, timeout) => RunSharpy(src, timeout, needsStdlib), cells[i].Source, SharpyExecTimeoutMs, out var again);
                timing.Add(needsStdlib, Stopwatch.GetElapsedTime(started).TotalSeconds);
                if (again)
                    retried++;
                if (cachePath != null)
                    cache[key] = runs[i];
            }
        }
        if (cachePath != null)
            File.WriteAllText(cachePath, JsonSerializer.Serialize(cache));

        // --- Judge. ---
        var results = new List<CellResult>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            PyOutcome py = cell.Family == Family.Seed
                ? cell.Python
                : pythonById.TryGetValue(i, out var verdict)
                    ? ClassifyPython(verdict)
                    : new PyOutcome(PyKind.SyntaxError, twinFailures.GetValueOrDefault(i, "no verdict from the runner"));
            var run = runs[i];
            var kind = ClassifySharpy(run);
            var control = twinFailures.TryGetValue(i, out var twin) ? twin : PythonControlFailure(cell, py);
            var cls = control != null ? VerdictClass.GeneratorBug : Judge(cell, kind, run.Stdout);
            results.Add(new CellResult(cell, run, kind, py, cls, control));
        }

        // --- Ratchet. ---
        var universe = pool.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var rows = LoadAllowlist(universe, out var loaderProblems);
        failures.AddRange(loaderProblems);

        var offenders = new List<CellResult>();
        var stale = new List<string>();
        var moved = new List<string>();
        var ineffective = new List<string>();
        var generatorBugs = results.Where(r => r.Class == VerdictClass.GeneratorBug).ToList();
        var harness = results.Where(r => r.Class == VerdictClass.Harness).ToList();
        foreach (var r in results)
        {
            var isRed = r.Class is VerdictClass.Ice or VerdictClass.FalseRefusal or VerdictClass.SilentWrong or VerdictClass.RuntimeThrow;
            rows.TryGetValue(r.Cell.Id, out var row);
            if (r.Class == VerdictClass.GeneratorBug && row != null)
                ineffective.Add($"{r.Cell.Id}: row names a GeneratorBug cell — not allowlistable");
            if (isRed && row == null)
                offenders.Add(r);
            else if (isRed && row!.Class != r.Class)
                moved.Add($"{r.Cell.Id}: listed as {row.Class}, now {r.Class} — update or delete the row");
            else if (!isRed && row != null && r.Class is not (VerdictClass.GeneratorBug or VerdictClass.Harness))
                stale.Add($"{r.Cell.Id}: listed as {row.Class}, now {r.Class} — STALE: delete the row");

            // D4: a row cites the issue whose fix turns it green, and that route is the code's. A hand-edited cite cannot hide here.
            if (isRed && row != null && row.Class == r.Class && row.Cite != ProposedCite(r.Cell, r.Class, r.Run))
                failures.Add($"{r.Cell.Id}: the row cites '{row.Cite}' and the route says '{ProposedCite(r.Cell, r.Class, r.Run)}'");
        }
        failures.AddRange(RefusalCodeFailures(results, partial));

        wall.Stop();
        var report = WriteReport(pool, results, rows, offenders, stale, moved, ineffective, partial, cached, retried, timing, wall.Elapsed);
        WriteProposedAllowlist(results);

        Output.WriteLine(report);
        if (partial)
            Output.WriteLine($"*** PARTIAL RUN {StrideEnv}={limit}: the allowlist's guarantee is scoped to these cells. ***");
        if (cachePath != null)
            Output.WriteLine($"*** CACHED RUN {CacheEnv}: {cached}/{cells.Count} Sharpy results reused; never a gate. ***");

        foreach (var g in generatorBugs.Take(40))
            failures.Add($"GeneratorBug {g.Cell.Id}: {g.Control}");
        if (generatorBugs.Count > 40)
            failures.Add($"… {generatorBugs.Count - 40} more GeneratorBug cells");
        foreach (var h in harness.Take(20))
            failures.Add($"Harness {h.Cell.Id}: Sharpy {h.Kind}, exit {h.Run.ExitCode}, codes [{string.Join(",", h.Run.AllCodes)}]");
        foreach (var o in offenders.Take(40))
            failures.Add($"{o.Class} {o.Cell.Id} ({o.Cell.Rule}): Sharpy {Describe(o)}; tag {o.Cell.Tag}, model '{o.Cell.Model}'; python {o.Python}");
        if (offenders.Count > 40)
            failures.Add($"… {offenders.Count - 40} more non-allowlisted red cells");
        failures.AddRange(stale);
        failures.AddRange(moved);
        failures.AddRange(ineffective);
        if (failures.Count > 0)
            Assert.Fail($"{failures.Count} failures. {report}\n{string.Join("\n", failures)}");
    }

    /// <summary>A seed cell's python column is its verdict from the seed table (the renderer facts' control), not a batch run.</summary>
    private static string PythonText(CellResult r) => r.Cell.SeedPythonVerdict ?? r.Python.ToString();

    private static string Describe(CellResult r)
        => r.Kind == SharpyKind.Ran
            ? $"Ran '{Normalize(r.Run.Stdout)}'"
            : $"{r.Kind} [{string.Join(",", r.Run.AllCodes.Distinct())}{(r.Run.CsCode != null ? " " + r.Run.CsCode : "")}]";

    internal sealed record CellResult(
        NameCell Cell, SharpyRun Run, SharpyKind Kind, PyOutcome Python, VerdictClass Class, string? Control);

    private static Dictionary<string, SharpyRun> LoadCache(string? path)
    {
        if (path == null || !File.Exists(path))
            return new Dictionary<string, SharpyRun>(StringComparer.Ordinal);
        var loaded = JsonSerializer.Deserialize<Dictionary<string, CachedRun>>(File.ReadAllText(path))!;
        return loaded.ToDictionary(kv => kv.Key, kv => kv.Value.ToRun(), StringComparer.Ordinal);
    }

    private sealed record CachedRun(
        bool Success, int? ExitCode, bool ThrewInHarness, bool TimedOut, string Stdout, string Stderr,
        List<string> ErrorCodes, List<string> AllCodes, string? CsCode)
    {
        public SharpyRun ToRun() => new(Success, ExitCode, ThrewInHarness, TimedOut, Stdout, Stderr, ErrorCodes, AllCodes, CsCode);
    }

    private string WriteReport(
        IReadOnlyList<NameCell> pool,
        IReadOnlyList<CellResult> results,
        IReadOnlyDictionary<string, AllowRow> rows,
        IReadOnlyList<CellResult> offenders,
        IReadOnlyList<string> stale,
        IReadOnlyList<string> moved,
        IReadOnlyList<string> ineffective,
        bool partial,
        int cached,
        int retried,
        ArmTiming timing,
        TimeSpan wall)
    {
        var perFamily = results.GroupBy(r => r.Cell.Family).OrderBy(g => g.Key).ToDictionary(
            g => g.Key.ToString(),
            g => g.GroupBy(r => r.Class).OrderBy(c => c.Key).ToDictionary(c => c.Key.ToString(), c => c.Count()));
        int pythonAgreed = results.Count(r => r.Control == null);
        var report = new
        {
            commit = CurrentCommit(),
            pool = pool.Count,
            run = results.Count,
            coveragePercent = pool.Count == 0 ? 0 : 100.0 * results.Count / pool.Count,
            partial,
            cachedSharpyResults = cached,
            perFamily,
            perFamilyTargetOuter = results.GroupBy(r => $"{r.Cell.Family}/{r.Cell.Target}/{r.Cell.Outer}")
                .OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()),
            pythonControlAgreed = pythonAgreed,
            pythonControlCells = results.Count,
            generatorBugs = results.Count(r => r.Class == VerdictClass.GeneratorBug),
            harness = results.Count(r => r.Class == VerdictClass.Harness),
            retried,
            allowlistSize = rows.Count,
            nonAllowlistedRed = offenders.Count,
            staleRows = stale,
            movedRows = moved,
            ineffectiveRows = ineffective,
            excludedAxes = Enum.GetNames<ExcludedAxis>(),
            rosters = RosterReport(results),
            unruledCells = results.Where(r => r.Cell.Tag == SharpyTag.Unruled).Select(r => new
            {
                id = r.Cell.Id,
                shape = r.Cell.Shape,
                sharpy = Describe(r),
                python = PythonText(r),
            }).ToList(),
            secondsPerCell = results.Count == 0 ? 0 : wall.TotalSeconds / results.Count,
            wallSeconds = wall.TotalSeconds,
            // P77b D2.9 "Reach": the Sharpy arm's compile + execute seconds per cell, the Stdlib-referencing axis-9 cells beside
            // the Core-only rest (uncached cells only).
            sharpyArm = new
            {
                coreOnlyCells = timing.CoreCells,
                coreOnlySecondsPerCell = timing.CoreSecondsPerCell,
                importedOuterCells = timing.StdlibCells,
                importedOuterSecondsPerCell = timing.StdlibSecondsPerCell,
            },
        };

        var dir = ReportDirectory();
        File.WriteAllText(
            Path.Combine(dir, "name-resolution-differential-report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        using (var writer = new StreamWriter(Path.Combine(dir, "name-resolution-differential-cells.jsonl")))
        {
            foreach (var r in results)
            {
                writer.WriteLine(JsonSerializer.Serialize(new
                {
                    id = r.Cell.Id,
                    family = r.Cell.Family.ToString(),
                    verdict = r.Class.ToString(),
                    tag = r.Cell.Tag.ToString(),
                    rule = r.Cell.Rule,
                    model = r.Cell.Model,
                    predicted = r.Cell.SeedPythonVerdict ?? r.Cell.Python.ToString(),
                    python = PythonText(r),
                    control = r.Control,
                    sharpy = Describe(r),
                    cs = r.Run.CsCode,
                    source = r.Cell.Source,
                }));
            }
        }

        var counts = string.Join("; ", perFamily.Select(f => $"{f.Key}: " + string.Join(" ", f.Value.Select(c => $"{c.Key}={c.Value}"))));
        return $"name-resolution-differential: pool {pool.Count}, run {results.Count}, python agreed {pythonAgreed}/{results.Count}, "
            + $"allowlist {rows.Count}, non-allowlisted {offenders.Count}, stale {stale.Count}, moved {moved.Count}, retried {retried}, "
            + $"wall {wall.TotalSeconds:F1}s ({(results.Count == 0 ? 0 : wall.TotalSeconds / results.Count):F3}s/cell; Sharpy arm "
            + $"{timing.CoreSecondsPerCell:F3}s/cell over {timing.CoreCells} Core-only cells, {timing.StdlibSecondsPerCell:F3}s/cell over "
            + $"{timing.StdlibCells} ImportedName cells). {counts}";
    }

    /// <summary>The Sharpy arm's seconds, split by whether the cell referenced the Stdlib monolith (P77b D2.9's "Reach").</summary>
    private sealed class ArmTiming
    {
        public int CoreCells { get; private set; }
        public int StdlibCells { get; private set; }
        private double _coreSeconds;
        private double _stdlibSeconds;

        public void Add(bool stdlib, double seconds)
        {
            if (stdlib)
            {
                StdlibCells++;
                _stdlibSeconds += seconds;
            }
            else
            {
                CoreCells++;
                _coreSeconds += seconds;
            }
        }

        public double CoreSecondsPerCell => CoreCells == 0 ? 0 : _coreSeconds / CoreCells;
        public double StdlibSecondsPerCell => StdlibCells == 0 ? 0 : _stdlibSeconds / StdlibCells;
    }

    private static void WriteProposedAllowlist(IReadOnlyList<CellResult> results)
    {
        var lines = results
            .Where(r => r.Class is VerdictClass.Ice or VerdictClass.FalseRefusal or VerdictClass.SilentWrong or VerdictClass.RuntimeThrow)
            .OrderBy(r => r.Cell.Id, StringComparer.Ordinal)
            .Select(r => $"{r.Cell.Id} {r.Class} # {ProposedCite(r.Cell, r.Class, r.Run)}");
        File.WriteAllLines(Path.Combine(ReportDirectory(), "name-resolution-differential-proposed-allowlist.txt"), lines);
    }

    /// <summary>The report's sha, so its counts read `@ sha`. A tree with no git says so; the sweep's verdict does not depend on it.</summary>
    private static string CurrentCommit()
    {
        try
        {
            using var git = Process.Start(new ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = ReportDirectory(),
            })!;
            var sha = git.StandardOutput.ReadToEnd().Trim();
            git.WaitForExit();
            return git.ExitCode == 0 ? sha : "unknown";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "unknown";
        }
    }

    private static string ReportDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;
        var tmp = Path.Combine(dir?.FullName ?? Path.GetTempPath(), ".claude", "tmp");
        Directory.CreateDirectory(tmp);
        return tmp;
    }
}
