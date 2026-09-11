using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The standing matrix for KEYWORD BINDING FAILURES (#1810, Decision 6(b)): <b>a keyword argument
/// no candidate can bind is refused by its own code at its own span, on every route and whatever
/// the callee's shape</b> — SPY0234 for a name nothing declares, SPY0370 for one naming a
/// positional-only slot, SPY0235 for one naming a slot already filled positionally.
///
/// <para>Axes: failure kind (3) × callee kind (9) × spelling (2) = 54 cells, of which the 9
/// <c>Duplicate × KeywordOnly</c> cells are rostered undeclarable (a duplicate needs a positional
/// argument to duplicate, so the spelling does not exist) — 45 live. Every live cell asserts three
/// things: the CODE, the SPAN (the keyword's own line and column, computed from the generated
/// source, not copied from a run), and that no SPY0908 appears. The third is the one that makes
/// this a matrix about a class rather than about eight programs: every cell here used to be able to
/// fail by leaking a CS number instead of answering.</para>
///
/// <para>Two defect families this pins. An overloaded callee used to collapse ALL THREE kinds into
/// SPY0354 at the whole call, because the per-candidate binding recorded only that the candidate
/// was inapplicable and not WHY or WHERE — so <c>g(z=1)</c> over an overload set said "no matching
/// overload" while the identical single-candidate call said "unknown keyword argument 'z'" at the
/// keyword. And <c>super().__init__</c>/<c>self.__init__</c> checked ONLY the unknown-name arm
/// through a second, hand-rolled implementation, so <c>super().__init__(1, x=2)</c> reached Roslyn
/// as CS1744 behind SPY0908 pointing inside the base initializer's BODY, while the identical direct
/// construction <c>Base(1, x=2)</c> reported SPY0235 at the keyword.</para>
///
/// <para>Why the expected code depends on the failure KIND alone, never on the callee kind or the
/// spelling: that is the contract. A callee-specific expectation would be a matrix that had already
/// conceded the defect.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class OverloadBindingFailureMatrixTests : IntegrationTestBase
{
    private const int KindCount = 3;
    private const int HostCount = 9;
    private const int SpellingCount = 2;
    private const int CellCount = KindCount * HostCount * SpellingCount;

    /// <summary>
    /// <c>Duplicate × KeywordOnly</c> on every host: a keyword duplicates a POSITIONAL argument, so
    /// an all-keyword spelling of it does not exist. Rostered, not silently absent.
    /// </summary>
    private const int UndeclarableCellCount = HostCount;

    public OverloadBindingFailureMatrixTests(ITestOutputHelper output) : base(output) { }

    // ── the three failure kinds ──────────────────────────────────────────────────────────
    //
    // Each kind supplies the parameter list its failure needs and the two call spellings. Every
    // shape takes THREE parameters and every call passes THREE arguments, so arity never decides
    // first and the cell measures the binding rule it is named for.

    public enum Kind
    {
        /// <summary>A keyword no candidate declares → SPY0234.</summary>
        Unknown,

        /// <summary>A keyword naming a positional-only slot → SPY0370.</summary>
        PositionalOnly,

        /// <summary>A keyword naming a slot already filled positionally → SPY0235.</summary>
        Duplicate,
    }

    public enum Spelling
    {
        /// <summary>Every argument written as a keyword.</summary>
        KeywordOnly,

        /// <summary>A leading positional argument, the rest written as keywords.</summary>
        Mixed,
    }

    private static string ExpectedCode(Kind kind) => kind switch
    {
        Kind.Unknown => DiagnosticCodes.Semantic.UnknownKeywordArgument,
        Kind.PositionalOnly => DiagnosticCodes.Semantic.PositionalOnlyPassedByKeyword,
        Kind.Duplicate => DiagnosticCodes.Semantic.DuplicateArgument,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// The parameter list (self-free, three slots) the kind's failure needs: the positional-only
    /// marker for <see cref="Kind.PositionalOnly"/>, three plain slots otherwise. Three slots and
    /// three arguments on every host, so the arity check never fires first and each cell measures
    /// the binding rule it is named for.
    /// </summary>
    private static string Params(Kind kind, string tx, string? trest = null)
    {
        var rest = trest ?? tx;
        var slash = kind == Kind.PositionalOnly ? ", /" : string.Empty;
        return $"x: {tx}{slash}, y: {rest}, z: {rest}";
    }

    /// <summary>The name of the keyword the cell expects to be refused.</summary>
    private static string FailingKeyword(Kind kind) => kind == Kind.Unknown ? "zzz" : "x";

    /// <summary>
    /// The argument list, three arguments, in the given spelling.
    ///
    /// <para><see cref="Kind.PositionalOnly"/> + <see cref="Spelling.Mixed"/> is deliberately the
    /// case where BOTH rules are violated: `x` is positional-only AND already filled by the leading
    /// positional argument. python3 names the positional-only violation for it — `def g(x, /, y, z)`
    /// called `g(1, y=1, x=1)` raises "got some positional-only arguments passed as keyword
    /// arguments: 'x'", not "got multiple values for argument 'x'" — so that is the code every
    /// route must give. Measured at f84701e04, the single-candidate routes did and the overloaded
    /// ones said SPY0235: the two implementations of "why can't this keyword bind" tried the arms
    /// in opposite orders. This cell is what discriminates them.</para>
    /// </summary>
    private static string Args(Kind kind, Spelling spelling) => (kind, spelling) switch
    {
        (Kind.Unknown, Spelling.KeywordOnly) => "x=1, y=1, zzz=1",
        (Kind.Unknown, Spelling.Mixed) => "1, y=1, zzz=1",
        (Kind.PositionalOnly, Spelling.KeywordOnly) => "x=1, y=1, z=1",
        (Kind.PositionalOnly, Spelling.Mixed) => "1, y=1, x=1",
        (Kind.Duplicate, Spelling.Mixed) => "1, x=1, z=1",
        _ => throw new ArgumentOutOfRangeException(nameof(spelling)),
    };

    private static bool IsDeclarable(Kind kind, Spelling spelling)
        => !(kind == Kind.Duplicate && spelling == Spelling.KeywordOnly);

    // ── the eight hosts ──────────────────────────────────────────────────────────────────

    public enum Host
    {
        Function,
        OverloadedFunction,
        GenericOverloadedFunction,
        Method,
        OverloadedMethod,
        Constructor,
        OverloadedConstructor,
        SuperInit,
        SelfInit,
    }

    /// <summary>
    /// The program for one cell. The call is on the single line tagged <c>#CALL</c>, which is how
    /// the span assertion locates it without hard-coding a line number that would drift with the
    /// prelude.
    /// </summary>
    private static string Program(Host host, Kind kind, Spelling spelling)
    {
        var p = Params(kind, "int");
        var ps = Params(kind, "str");
        var args = Args(kind, spelling);

        return host switch
        {
            Host.Function =>
                $"def g({p}) -> None:\n    print(\"A\")\n\n"
                + $"def main() -> None:\n    g({args})  #CALL\n",

            Host.OverloadedFunction =>
                $"def g({p}) -> None:\n    print(\"A\")\n\n"
                + $"def g({ps}) -> None:\n    print(\"B\")\n\n"
                + $"def main() -> None:\n    g({args})  #CALL\n",

            // The generic pair differs in the DECLARED shape of the first slot, which is also
            // the slot every failure kind targets — so the cell exercises the binding rule with
            // per-candidate inference in play.
            Host.GenericOverloadedFunction =>
                $"def g[T]({Params(kind, "T")}) -> None:\n    print(\"A\")\n\n"
                + $"def g[T]({Params(kind, "list[T]", "T")}) -> None:\n    print(\"B\")\n\n"
                + $"def main() -> None:\n    g({args})  #CALL\n",

            Host.Method =>
                $"class C:\n    def __init__(self) -> None:\n        pass\n\n"
                + $"    def m(self, {p}) -> None:\n        print(\"A\")\n\n"
                + $"def main() -> None:\n    c: C = C()\n    c.m({args})  #CALL\n",

            Host.OverloadedMethod =>
                $"class C:\n    def __init__(self) -> None:\n        pass\n\n"
                + $"    def m(self, {p}) -> None:\n        print(\"A\")\n\n"
                + $"    def m(self, {ps}) -> None:\n        print(\"B\")\n\n"
                + $"def main() -> None:\n    c: C = C()\n    c.m({args})  #CALL\n",

            Host.Constructor =>
                $"class C:\n    def __init__(self, {p}) -> None:\n        print(\"A\")\n\n"
                + $"def main() -> None:\n    c: C = C({args})  #CALL\n",

            Host.OverloadedConstructor =>
                $"class C:\n    def __init__(self, {p}) -> None:\n        print(\"A\")\n\n"
                + $"    def __init__(self, {ps}) -> None:\n        print(\"B\")\n\n"
                + $"def main() -> None:\n    c: C = C({args})  #CALL\n",

            Host.SuperInit =>
                $"class Base:\n    def __init__(self, {p}) -> None:\n        print(\"A\")\n\n"
                + $"class D(Base):\n    def __init__(self) -> None:\n"
                + $"        super().__init__({args})  #CALL\n\n"
                + $"def main() -> None:\n    d: D = D()\n",

            // The DELEGATING constructor is itself a candidate of `self.__init__`, so it carries
            // the same parameter shape as the one it delegates to. A parameterless delegating
            // ctor would make the candidates disagree about WHY the keyword cannot bind (unknown
            // name on one, positional-only on the other), and a disagreement is SPY0354's
            // business, not this matrix's.
            Host.SelfInit =>
                $"class C:\n    def __init__(self, {p}) -> None:\n        print(\"A\")\n\n"
                + $"    def __init__(self, {ps}) -> None:\n"
                + $"        self.__init__({args})  #CALL\n\n"
                + $"def main() -> None:\n    c: C = C(\"a\", \"a\", \"a\")\n",

            _ => throw new ArgumentOutOfRangeException(nameof(host)),
        };
    }

    public static TheoryData<Host, Kind, Spelling> Cells()
    {
        var data = new TheoryData<Host, Kind, Spelling>();
        foreach (Host h in Enum.GetValues<Host>())
        {
            foreach (Kind k in Enum.GetValues<Kind>())
            {
                foreach (Spelling s in Enum.GetValues<Spelling>())
                {
                    if (IsDeclarable(k, s))
                        data.Add(h, k, s);
                }
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void BindingFailure_IsTheArgumentsOwnCode_AtItsOwnSpan(Host host, Kind kind, Spelling spelling)
    {
        var source = Program(host, kind, spelling);
        var (line, column) = KeywordPosition(source, FailingKeyword(kind));

        var result = CompileAndExecute(source);
        var seen = string.Join(" | ",
            result.RawDiagnostics.Select(d => $"{d.Code}@{d.Line}:{d.Column} {d.Message}"))
            + " || " + string.Join(" | ", result.CompilationErrors);

        result.Success.Should().BeFalse($"the call cannot bind '{FailingKeyword(kind)}': {seen}");

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"a binding failure is answered by name, never leaked to Roslyn: {seen}");

        result.RawDiagnostics.Should().Contain(
            d => d.Code == ExpectedCode(kind) && d.Line == line && d.Column == column,
            $"expected {ExpectedCode(kind)} at {line}:{column} (the '{FailingKeyword(kind)}' "
            + $"keyword's own span) for {host}/{kind}/{spelling}, got: {seen}\n--- source ---\n{source}");
    }

    /// <summary>
    /// The 1-based line and column of <paramref name="keyword"/> on the source's single
    /// <c>#CALL</c> line — the span a cell expects, derived from the program rather than copied
    /// out of a run, so a wrong span cannot be normalized into the expectation.
    /// </summary>
    private static (int Line, int Column) KeywordPosition(string source, string keyword)
    {
        var lines = source.Split('\n');
        var index = Array.FindIndex(lines, l => l.Contains("#CALL", StringComparison.Ordinal));
        Assert.True(index >= 0, "every cell's program tags its call line with #CALL");
        var column = lines[index].IndexOf(keyword + "=", StringComparison.Ordinal);
        Assert.True(column >= 0, $"the #CALL line must pass '{keyword}=' : {lines[index]}");
        return (index + 1, column + 1);
    }

    // ── the axes are anchored ────────────────────────────────────────────────────────────

    /// <summary>
    /// The cell count is anchored to LITERALS, and the roster of excluded cells is justified
    /// one by one. A count derived from the same enums the generator walks would agree with any
    /// generator, including one that dropped an axis.
    /// </summary>
    [Fact]
    public void Axes_AreAnchored_AndTheExcludedCellsAreJustified()
    {
        Enum.GetValues<Kind>().Length.Should().Be(KindCount);
        Enum.GetValues<Spelling>().Length.Should().Be(SpellingCount);
        Enum.GetValues<Host>().Length.Should().Be(HostCount);

        CellCount.Should().Be(54, "3 kinds x 9 hosts x 2 spellings, as the class remark states");
        UndeclarableCellCount.Should().Be(9, "one Duplicate x KeywordOnly cell per host");
        Cells().Count.Should().Be(45, "54 cells less the 9 rostered undeclarable ones");

        foreach (Host h in Enum.GetValues<Host>())
        {
            IsDeclarable(Kind.Duplicate, Spelling.KeywordOnly).Should().BeFalse(
                "a duplicate keyword duplicates a POSITIONAL argument; there is no all-keyword "
                + $"spelling of it on {h} or anywhere else");
        }
    }
}
