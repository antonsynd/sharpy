using System.Linq;
using System.Text;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// The block kinds of the #1560 matrix and one way to wrap a statement body in each of them.
/// Shared by <see cref="BlockScopeRedeclarationMatrixTests"/> (executing cells) and
/// <c>TargetBindingRecordingTests</c> (recorded facts), so both range over the same rows.
/// </summary>
internal static class BlockKinds
{
    /// <summary>A context manager for the <c>with</c> kind; harmless in every other program.</summary>
    public const string Prelude =
        "class Resource:\n" +
        "    name: str\n" +
        "    def __init__(self, name: str):\n" +
        "        self.name = name\n" +
        "    def __enter__(self) -> Self:\n" +
        "        return self\n" +
        "    def __exit__(self) -> None:\n" +
        "        pass\n\n";

    /// <summary>Kinds that take a statement body (the lambda-parameter and comprehension-target kinds do not).</summary>
    public static readonly string[] BodyKinds =
    {
        "if", "elif", "else", "while", "while-else", "for", "for-else",
        "try", "except", "try-else", "finally", "with", "match-arm", "defer", "nested-def",
    };

    /// <summary>A <c>defer</c> block runs at scope exit, after everything that follows it.</summary>
    public static bool RunsAtExit(string kind) => kind == "defer";

    public static FeatureFlags FeaturesFor(string kind)
        => kind == "defer" ? FeatureFlags.None.Enable("defer") : FeatureFlags.None;

    /// <summary>
    /// Wraps <paramref name="body"/> (lines at zero indentation, may contain its own nesting) in
    /// block kind <paramref name="kind"/>; <paramref name="i"/> makes the wrapper's own names
    /// unique so two wrappers can be siblings. The result is indented for a function body.
    /// </summary>
    public static string Wrap(string kind, int i, string body)
    {
        var (prefix, depth, suffix) = kind switch
        {
            "if" => (new[] { "if True:" }, 1, System.Array.Empty<string>()),
            "elif" => (new[] { "if False:", "    pass", "elif True:" }, 1, System.Array.Empty<string>()),
            "else" => (new[] { "if False:", "    pass", "else:" }, 1, System.Array.Empty<string>()),
            "while" => (new[] { $"d{i} = False", $"while not d{i}:" }, 1, new[] { $"    d{i} = True" }),
            "while-else" => (new[] { $"d{i} = False", $"while d{i}:", "    pass", "else:" }, 1, System.Array.Empty<string>()),
            "for" => (new[] { $"for i{i} in range(1):" }, 1, System.Array.Empty<string>()),
            "for-else" => (new[] { $"for i{i} in range(0):", "    pass", "else:" }, 1, System.Array.Empty<string>()),
            "try" => (new[] { "try:" }, 1, new[] { "except Exception:", "    pass" }),
            "except" => (new[] { "try:", $"    raise ValueError(\"e{i}\")", "except ValueError:" }, 1, System.Array.Empty<string>()),
            "try-else" => (new[] { "try:", "    pass", "except Exception:", "    pass", "else:" }, 1, System.Array.Empty<string>()),
            "finally" => (new[] { "try:", "    pass", "finally:" }, 1, System.Array.Empty<string>()),
            "with" => (new[] { $"with Resource(\"r{i}\") as r{i}:" }, 1, System.Array.Empty<string>()),
            "match-arm" => (new[] { $"match {i}:", $"    case {i}:" }, 2, new[] { "    case _:", "        pass" }),
            "defer" => (new[] { "defer:" }, 1, System.Array.Empty<string>()),
            "nested-def" => (new[] { $"def inner{i}() -> None:" }, 1, new[] { $"inner{i}()" }),
            "function-body" => (System.Array.Empty<string>(), 0, System.Array.Empty<string>()),
            _ => throw new System.ArgumentException($"unknown block kind '{kind}'", nameof(kind)),
        };

        var sb = new StringBuilder();
        foreach (var line in prefix)
            sb.Append("    ").Append(line).Append('\n');
        var pad = new string(' ', 4 * (depth + 1));
        foreach (var line in body.Split('\n'))
            sb.Append(pad).Append(line).Append('\n');
        foreach (var line in suffix)
            sb.Append("    ").Append(line).Append('\n');
        return sb.ToString();
    }

    /// <summary>A whole program: the prelude, <c>def main()</c>, then the given function-body text.</summary>
    public static string Program(string mainBody)
        => Program("", mainBody);

    /// <summary>A whole program: the prelude, module-level declarations, <c>def main()</c>, then the function-body text.</summary>
    public static string Program(string modulePrefix, string mainBody)
        => Prelude + modulePrefix + "def main() -> None:\n" + mainBody;

    // ================================================================
    // The binding-law matrix (#1974, P21a): store kind × spelling × predecessor
    // ================================================================

    /// <summary>
    /// The store kinds of the binding-law matrix: every <see cref="BodyKinds"/> body plus the
    /// function body itself (<c>function-body</c> — <see cref="Wrap"/> adds no wrapper).
    /// </summary>
    public static readonly string[] StoreKinds = BodyKinds.Append("function-body").ToArray();

    /// <summary>
    /// One spelling of a statement store of a name. <see cref="WritesThrough"/> IS the spelling's
    /// law: <c>true</c> — the store writes through to the nearest existing binding of the name and
    /// declares only when none exists (bare <c>x = e</c>, Stage 1 and Stage 2 alike); <c>false</c> —
    /// the store always introduces a fresh binding (every <c>let</c> form, #1974; and, in Stage 1,
    /// the annotated <c>x: T = e</c>, which shadows today). <see cref="Store"/> spells the store of
    /// <c>name</c> with the int <c>value</c>; the tuple and star forms also bind a fresh <c>y</c> /
    /// <c>rest</c>, which no predecessor ever names.
    /// </summary>
    public sealed record Spelling(string Id, bool WritesThrough, bool IsLet, System.Func<string, string, string> Store);

    /// <summary>
    /// The spelling axis. <c>let-tuple</c> (literal RHS) reaches the inline binder of
    /// <c>CheckTupleUnpackingElements</c>; <c>let-star</c> reaches <c>BindAssignmentUnpackingIdentifier</c>
    /// — the two tuple binder sites besides the identifier arm, so a seam that gates only one arm
    /// leaves the other spelling red. <c>bare-tuple</c> and <c>bare-star</c> are the write-through
    /// controls on those two binders: each must write through to every existing binding and be
    /// refused SPY0225 on reaching a const (lead ruling, P21a: in contract — the star binder had no
    /// const refusal and crashed instead).
    /// </summary>
    public static readonly Spelling[] Spellings =
    {
        new("bare", WritesThrough: true, IsLet: false, (n, v) => $"{n} = {v}"),
        new("annotated", WritesThrough: false, IsLet: false, (n, v) => $"{n}: int = {v}"),
        new("let", WritesThrough: false, IsLet: true, (n, v) => $"let {n} = {v}"),
        new("let-annotated", WritesThrough: false, IsLet: true, (n, v) => $"let {n}: int = {v}"),
        new("let-tuple", WritesThrough: false, IsLet: true, (n, v) => $"let {n}, y = {v}, 3"),
        new("let-star", WritesThrough: false, IsLet: true, (n, v) => $"let {n}, *rest = [{v}, 3]"),
        new("bare-tuple", WritesThrough: true, IsLet: false, (n, v) => $"{n}, y = {v}, 3"),
        new("bare-star", WritesThrough: true, IsLet: false, (n, v) => $"{n}, *rest = [{v}, 3]"),
    };

    public enum PredecessorScope { None, SameScope, EnclosingBlock, EnclosingFunction, Module }

    /// <summary>Where an existing binding of the stored name lives before the store, and whether it is <c>const</c>.</summary>
    public sealed record Predecessor(string Id, PredecessorScope Scope, bool IsConst);

    public static readonly Predecessor[] Predecessors =
    {
        new("none", PredecessorScope.None, IsConst: false),
        new("same-scope", PredecessorScope.SameScope, IsConst: false),
        new("enclosing-block", PredecessorScope.EnclosingBlock, IsConst: false),
        new("enclosing-function", PredecessorScope.EnclosingFunction, IsConst: false),
        new("module-variable", PredecessorScope.Module, IsConst: false),
        new("module-const", PredecessorScope.Module, IsConst: true),
        new("same-scope-const", PredecessorScope.SameScope, IsConst: true),
        new("enclosing-function-const", PredecessorScope.EnclosingFunction, IsConst: true),
    };

    /// <summary>The law: the store writes through iff its spelling does AND a binding exists to write to.</summary>
    public static bool StoreWritesThrough(Spelling spelling, Predecessor predecessor)
        => spelling.WritesThrough && predecessor.Scope != PredecessorScope.None;

    /// <summary>
    /// The law's refusal (SPY0225): a store is refused when it would reach a <c>const</c> — by
    /// writing through to it, or by re-declaring it in the SAME scope (an introducing spelling
    /// shadows an OUTER const, as the annotated form does today).
    /// </summary>
    public static bool StoreRefused(Spelling spelling, Predecessor predecessor)
        => predecessor.IsConst
           && (predecessor.Scope == PredecessorScope.SameScope || StoreWritesThrough(spelling, predecessor));

    /// <summary>
    /// A generated binding-law program. <see cref="ExpectedOutput"/> (a run) or
    /// <see cref="ExpectedCode"/> (a refusal) is COMPUTED from <see cref="StoreWritesThrough"/> /
    /// <see cref="StoreRefused"/>, never tabled by hand.
    /// </summary>
    public sealed record BindingLawCell(
        string Kind, Spelling Spelling, Predecessor Predecessor, string Source, string? ExpectedOutput, string? ExpectedCode)
    {
        /// <summary>The cell's name inside its kind, the ratchet key's second half.</summary>
        public string Cell => $"{Spelling.Id}/{Predecessor.Id}";
    }

    /// <summary>
    /// Why a (kind, predecessor) pair has no cell, or null. The function body is the outermost
    /// function scope: it has no enclosing block, and its enclosing-function predecessor IS the
    /// same-scope cell.
    /// </summary>
    public static string? BindingLawNotApplicable(string kind, Predecessor predecessor)
        => kind == "function-body" && predecessor.Scope is PredecessorScope.EnclosingBlock or PredecessorScope.EnclosingFunction
            ? "the function body has no enclosing block, and its enclosing function is its own scope (the same-scope cell)"
            : null;

    /// <summary>
    /// Builds the program for one binding-law cell. The store <c>x = 2</c> (in the cell's
    /// spelling) sits in a <paramref name="kind"/> body and is followed by <c>print(x)</c>; a
    /// predecessor <c>x = 1</c> is planted at the predecessor's scope, and the program then reads
    /// the PREDECESSOR binding so that write-through (prints 2) and introduce (prints 1) differ:
    /// <list type="bullet">
    /// <item>same scope — a lambda planted beside the predecessor captures it (<c>print(get())</c>
    /// after the store: C# captures the variable, so only a write-through reaches it);</item>
    /// <item>enclosing block / enclosing function — <c>print(x)</c> after the block;</item>
    /// <item>module — <c>print(outer_x())</c> after the block, a module function reading the module binding.</item>
    /// </list>
    /// A <c>defer</c> body runs at the exit of its enclosing block, so for that kind the outer read
    /// is itself a <c>defer</c> registered BEFORE the store's (LIFO: it runs after), which keeps
    /// the printed order "inner, then outer" for every kind.
    /// </summary>
    public static BindingLawCell BindingLaw(string kind, Spelling spelling, Predecessor predecessor)
    {
        var scope = predecessor.Scope;
        var plant = (predecessor.IsConst ? "const " : "") + "x: int = 1";

        var inner = new System.Collections.Generic.List<string>();
        if (scope == PredecessorScope.SameScope)
        {
            inner.Add(plant);
            if (!predecessor.IsConst)
                inner.Add("get: () -> int = lambda: x");
        }

        inner.Add(spelling.Store("x", "2"));
        inner.Add("print(x)");
        if (scope == PredecessorScope.SameScope && !predecessor.IsConst)
            inner.Add("print(get())");

        var outerRead = scope switch
        {
            PredecessorScope.EnclosingBlock or PredecessorScope.EnclosingFunction => "print(x)",
            PredecessorScope.Module => "print(outer_x())",
            _ => null,
        };

        var level = new StringBuilder();
        if (scope is PredecessorScope.EnclosingBlock or PredecessorScope.EnclosingFunction)
            level.Append("    ").Append(plant).Append('\n');
        if (outerRead != null && RunsAtExit(kind))
            level.Append("    defer:\n        ").Append(outerRead).Append('\n');
        level.Append(Wrap(kind, 1, string.Join("\n", inner)));
        if (outerRead != null && !RunsAtExit(kind))
            level.Append("    ").Append(outerRead).Append('\n');

        var mainBody = level.ToString();
        if (scope == PredecessorScope.EnclosingBlock)
            mainBody = "    if True:\n" + string.Concat(mainBody.Split('\n').Select(l => l.Length == 0 ? "" : "    " + l + "\n"));

        var modulePrefix = scope == PredecessorScope.Module
            ? plant + "\n\ndef outer_x() -> int:\n    return x\n\n"
            : "";

        string? expectedOutput = null;
        string? expectedCode = null;
        if (StoreRefused(spelling, predecessor))
        {
            expectedCode = "SPY0225";
        }
        else
        {
            var lines = new System.Collections.Generic.List<string> { "2" };
            if (scope != PredecessorScope.None)
                lines.Add(StoreWritesThrough(spelling, predecessor) ? "2" : "1");
            expectedOutput = string.Join("\n", lines);
        }

        return new BindingLawCell(kind, spelling, predecessor, Program(modulePrefix, mainBody), expectedOutput, expectedCode);
    }

    /// <summary>Every applicable binding-law cell, in kind × spelling × predecessor order.</summary>
    public static System.Collections.Generic.IEnumerable<BindingLawCell> BindingLawCells()
    {
        foreach (var kind in StoreKinds)
            foreach (var spelling in Spellings)
                foreach (var predecessor in Predecessors)
                    if (BindingLawNotApplicable(kind, predecessor) == null)
                        yield return BindingLaw(kind, spelling, predecessor);
    }

    /// <summary>The cell with id <c>kind/spelling/predecessor</c> (theory rows carry ids, not records).</summary>
    public static BindingLawCell BindingLawCellById(string id)
    {
        var parts = id.Split('/');
        return BindingLaw(
            parts[0],
            Spellings.Single(s => s.Id == parts[1]),
            Predecessors.Single(p => p.Id == parts[2]));
    }

    /// <summary>
    /// Ratcheted known-red cells (verification-contract §1: an allowlist entry cites an issue and is
    /// deleted when fixed). The #1560 cells have been empty since the 2026-08-27 round: <c>for-else</c>/<c>while-else</c>
    /// bodies are type-checked (#1659) and their UseBeforeAssign cells flipped to
    /// <c>DefinitelyRunBeforeSibling</c> once DA-proved bare locals gained a definite initializer
    /// (#1656); the <c>defer</c> entry drained when the CFG builder gained a scope-exit model for
    /// defer bodies (#1657, f2d5270b7).
    /// <para>
    /// The binding-law entries (#1974, P21a) were red-first and drained when the introduce seam
    /// (<c>TypeChecker.StatementStorePredecessor</c>) landed: every <c>let</c> spelling is fresh over
    /// every predecessor, and every write-through store that reaches a <c>const</c> — the star
    /// binder's included — is refused SPY0225.
    /// </para>
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<(string Kind, string Cell), string> KnownRed = new()
    {
    };

    /// <summary>
    /// Runs a cell's assertion. A cell not in <see cref="KnownRed"/> must pass. A known-red cell
    /// must still FAIL its assertion: the moment it passes, this fails loudly so the entry (and
    /// the issue) are drained — never a silent skip.
    /// </summary>
    public static void Cell(string kind, string cell, System.Action assertion)
    {
        // A "*" kind entry holds the cell red in every kind (a class of cells red for one reason).
        if (!KnownRed.TryGetValue((kind, cell), out var issue) && !KnownRed.TryGetValue(("*", cell), out issue))
        {
            assertion();
            return;
        }

        try
        {
            assertion();
        }
        catch (Xunit.Sdk.XunitException)
        {
            return; // still red, as the issue records
        }

        Xunit.Assert.Fail($"[{kind}/{cell}] now passes — {issue} is fixed for this cell: delete its KnownRed entry.");
    }
}
