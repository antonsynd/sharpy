using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The CLR call-route matrix — route (5) x argument form (2) x mismatch (8) — and its controls
/// (#1753, #1798, #1705).
///
/// <para><b>Contract.</b> Every route that binds a Sharpy call to a reflected .NET member decides
/// applicability AND betterness by ONE formula that agrees with C# §12.6.4
/// (<c>TypeChecker.Expressions.Access.Calls.Clr.cs</c>). A route cannot have its own answer, and a
/// route cannot be missing an answer: that is what this matrix measures. Before the seam the
/// instance route had applicability with no betterness (so every overload set with an
/// <c>object</c> sibling was SPY0601 — including three in the stdlib's own sources), the static
/// route carried three tie-breaks the instance route never saw, the constructor route ran two
/// deciders in sequence, and the indexer route asked nothing at all.</para>
///
/// <para><b>Axes.</b> Route: {instance, static, constructor, indexer, extension} x argument form:
/// {positional, keyword} x mismatch: {exact match, widening (int into double), string-versus-object
/// betterness, None into a non-nullable formal, wrong type, unmappable formal, params tail, wrong
/// arity}. Cells that cannot exist are in <see cref="NotApplicable"/> with a reason, and
/// live + rostered == 5 x 2 x 8 is asserted, so a route or a mismatch cannot quietly go missing.
/// The axis sizes are anchored to literals, never derived from the arrays, so deleting a route from
/// the roster fails the totality pin instead of shrinking the matrix.</para>
///
/// <para><b>Every cell EXECUTES.</b> A cell asserts either the program's stdout or the SPY code of
/// its refusal — never just "compiles". SPY0908 (the generated C# failed to compile) is the shape
/// this seam exists to remove, so a cell that expects a refusal also asserts that the refusal is
/// NOT SPY0908: a diagnostic Roslyn produced is not a diagnostic the checker produced.</para>
///
/// <para><b>Mutation record (contract §2).</b> Recorded in the commit body. Reverting the betterness
/// helper (<c>IsBetterClrBinding</c> to <c>true</c>, i.e. no order at all) turns the
/// string-versus-object and widening cells red; restoring the pre-fix
/// <c>DeclaresImplicitConversion</c> (any <c>op_Implicit</c>, not only inbound ones) turns every
/// <c>string</c>-formal cell red.</para>
/// </summary>
public class ClrCallRouteMatrixTests : IntegrationTestBase
{
    public ClrCallRouteMatrixTests(ITestOutputHelper output) : base(output) { }

    // -- Axis sizes, anchored to literals -----------------------------------------
    private const int RouteCount = 5;
    private const int ArgumentFormCount = 2;
    private const int MismatchCount = 8;

    private static readonly string[] Routes =
    {
        "instance", "static", "constructor", "indexer", "extension"
    };

    private static readonly string[] ArgumentForms = { "positional", "keyword" };

    private static readonly string[] Mismatches =
    {
        "exact", "widening", "string_beats_object", "none_into_nonnullable",
        "wrong_type", "unmappable_formal", "params_tail", "wrong_arity"
    };

    /// <summary>
    /// Cells that cannot exist, each with the reason. A cell is listed here ONLY when the spelling
    /// is impossible, never because it is inconvenient to write.
    /// </summary>
    private static readonly Dictionary<string, string> NotApplicable = new()
    {
        // An indexer has no parameter names to write: `d[key: 1]` is not a subscript in any syntax.
        ["indexer.keyword.exact"] = "a subscript has no keyword form",
        ["indexer.keyword.widening"] = "a subscript has no keyword form",
        ["indexer.keyword.string_beats_object"] = "a subscript has no keyword form",
        ["indexer.keyword.none_into_nonnullable"] = "a subscript has no keyword form",
        ["indexer.keyword.wrong_type"] = "a subscript has no keyword form",
        ["indexer.keyword.unmappable_formal"] = "a subscript has no keyword form",
        ["indexer.keyword.params_tail"] = "a subscript has no keyword form",
        ["indexer.keyword.wrong_arity"] = "a subscript has no keyword form",
        // A subscript takes exactly one key, so there is no params tail and no arity to get wrong
        // beyond it; and no BCL indexer has an `object`/unmappable single-key sibling to choose
        // between.
        ["indexer.positional.string_beats_object"] = "no BCL indexer has an object-keyed sibling",
        ["indexer.positional.unmappable_formal"] = "no BCL single-key indexer takes an unmappable type",
        ["indexer.positional.params_tail"] = "a subscript takes exactly one key",
        ["indexer.positional.wrong_arity"] = "a subscript takes exactly one key",
        // The extension resolver pairs formals with argument SHAPES by position and requires the
        // counts to match exactly, so a keyword form and an arity mismatch never reach it; and
        // Enumerable has no params-tail or object-versus-string overload pair on a list receiver.
        ["extension.keyword.exact"] = "the extension resolver binds by position, not by name",
        ["extension.keyword.widening"] = "the extension resolver binds by position, not by name",
        ["extension.keyword.string_beats_object"] = "the extension resolver binds by position, not by name",
        ["extension.keyword.none_into_nonnullable"] = "the extension resolver binds by position, not by name",
        ["extension.keyword.wrong_type"] = "the extension resolver binds by position, not by name",
        ["extension.keyword.unmappable_formal"] = "the extension resolver binds by position, not by name",
        ["extension.keyword.params_tail"] = "the extension resolver binds by position, not by name",
        ["extension.keyword.wrong_arity"] = "the extension resolver binds by position, not by name",
        ["extension.positional.string_beats_object"] = "no Enumerable overload pair on a list receiver differs only by object/string",
        ["extension.positional.params_tail"] = "no Enumerable overload on a list receiver has a params tail",
        // A constructor's own name is the type's, so there is no `object`-versus-`string`
        // constructor pair on the types Sharpy can spell; and a params-tail constructor keyword form
        // has no BCL instance to write it against.
        ["constructor.keyword.params_tail"] = "no BCL params-tail constructor has named parameters Sharpy can write",
        ["constructor.keyword.string_beats_object"] = "no BCL constructor pair differs only by object/string",
        ["constructor.positional.string_beats_object"] = "no BCL constructor pair differs only by object/string",
        ["constructor.keyword.unmappable_formal"] = "no BCL constructor takes an unmappable formal at a named parameter Sharpy can write",
        // A static/instance method's keyword form cannot exercise a params tail: a params parameter
        // binds positionally by definition, and naming it would pass the ARRAY, which is the normal
        // form (covered by the positional cell).
        ["static.keyword.params_tail"] = "a params tail named by keyword is the normal form, not the expanded one",
        ["instance.keyword.params_tail"] = "a params tail named by keyword is the normal form, not the expanded one",
        ["instance.keyword.unmappable_formal"] = "StringBuilder's IFormatProvider overload pairs it with a ref parameter, unwritable by keyword",
        ["static.keyword.unmappable_formal"] = "no BCL static takes an unmappable formal at a named parameter Sharpy can write",
        ["static.keyword.widening"] = "Math.max's named parameters are same-typed, so a keyword widening cell is the exact cell",
        ["constructor.keyword.widening"] = "Vector2's named parameters are same-typed, so a keyword widening cell is the exact cell",
    };

    // -- The cells -----------------------------------------------------------------

    /// <summary>
    /// Every live cell: its key, its program, and what it must produce. A stdout expectation means
    /// the program runs and prints that; a SPY code means the CHECKER refuses it by name.
    /// </summary>
    public static TheoryData<string, string, string> Cells()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var (key, source, expectation) in CellRoster())
            data.Add(key, source, expectation);
        return data;
    }

    private static IEnumerable<(string Key, string Source, string Expectation)> CellRoster()
    {
        // ---- instance.positional -------------------------------------------------
        yield return ("instance.positional.exact", """
            from System.Text import StringBuilder

            def main() -> None:
                sb: StringBuilder = StringBuilder()
                sb.append("x")
                print(sb.to_string())
            """, "out:x");
        yield return ("instance.positional.widening", """
            from System.Collections.Generic import List

            def main() -> None:
                xs: List[float] = List[float]()
                xs.add(2)
                print(xs[0])
            """, "out:2.0");
        yield return ("instance.positional.string_beats_object", """
            from System.Text import StringBuilder

            def main() -> None:
                sb: StringBuilder = StringBuilder()
                sb.append("x")
                sb.insert(0, "y")
                print(sb.to_string())
            """, "out:yx");
        yield return ("instance.positional.none_into_nonnullable", """
            from System.IO import DirectoryInfo

            def main() -> None:
                d: DirectoryInfo = DirectoryInfo(".")
                d.create_subdirectory(None)
            """, "SPY0229");
        yield return ("instance.positional.wrong_type", """
            from System.Collections.Generic import List

            def main() -> None:
                xs: List[int] = List[int]()
                xs.add("no")
            """, "SPY0220");
        yield return ("instance.positional.unmappable_formal", """
            from System.Text import StringBuilder

            def main() -> None:
                sb: StringBuilder = StringBuilder()
                sb.append_line("ok", 42)
            """, "SPY0220");
        yield return ("instance.positional.params_tail", """
            from System.Text import StringBuilder

            def main() -> None:
                sb: StringBuilder = StringBuilder()
                sb.append_format("{0}-{1}", 1, 2)
                print(sb.to_string())
            """, "out:1-2");
        yield return ("instance.positional.wrong_arity", """
            from System.Text import StringBuilder

            def main() -> None:
                sb: StringBuilder = StringBuilder()
                sb.to_string(1, 2, 3, 4)
            """, "SPY0224");

        // ---- instance.keyword ----------------------------------------------------
        yield return ("instance.keyword.exact", """
            from System.Text import StringBuilder

            def main() -> None:
                sb: StringBuilder = StringBuilder()
                sb.append(value="x")
                print(sb.to_string())
            """, "out:x");
        yield return ("instance.keyword.widening", """
            from System.Collections.Generic import List

            def main() -> None:
                xs: List[float] = List[float]()
                xs.add(item=2)
                print(xs[0])
            """, "out:2.0");
        yield return ("instance.keyword.string_beats_object", """
            from System.IO import StringWriter

            def main() -> None:
                w: StringWriter = StringWriter()
                w.write(value="w")
                print(w.to_string())
            """, "out:w");
        yield return ("instance.keyword.none_into_nonnullable", """
            from System.IO import DirectoryInfo

            def main() -> None:
                d: DirectoryInfo = DirectoryInfo(".")
                d.create_subdirectory(path=None)
            """, "SPY0229");
        yield return ("instance.keyword.wrong_type", """
            from System.Collections.Generic import List

            def main() -> None:
                xs: List[int] = List[int]()
                xs.add(item="no")
            """, "SPY0220");
        yield return ("instance.keyword.wrong_arity", """
            from System.Text import StringBuilder

            def main() -> None:
                sb: StringBuilder = StringBuilder()
                sb.append(nosuch="x")
            """, "SPY0234");

        // ---- static.positional ---------------------------------------------------
        yield return ("static.positional.exact", """
            from System.IO import Path

            def main() -> None:
                print(Path.combine("a", "b"))
            """, "out:a/b");
        yield return ("static.positional.widening", """
            from System import Math

            def main() -> None:
                print(Math.sqrt(4))
            """, "out:2.0");
        yield return ("static.positional.string_beats_object", """
            from System import Console

            def main() -> None:
                Console.write_line("hello")
            """, "out:hello");
        yield return ("static.positional.none_into_nonnullable", """
            from System import Environment

            def main() -> None:
                print(Environment.expand_environment_variables(None))
            """, "SPY0229");
        yield return ("static.positional.wrong_type", """
            from System.IO import Path

            def main() -> None:
                print(Path.combine("a", 1))
            """, "SPY0220");
        yield return ("static.positional.unmappable_formal", """
            from System import Convert

            def main() -> None:
                print(Convert.to_string(42, "not a format provider"))
            """, "SPY0220");
        yield return ("static.positional.params_tail", """
            from System.IO import Path

            def main() -> None:
                print(Path.combine("a", "b", "c", 4, "e"))
            """, "SPY0220");
        yield return ("static.positional.wrong_arity", """
            from System import Math

            def main() -> None:
                print(Math.max(1, 2, 3))
            """, "SPY0224");

        // ---- static.keyword ------------------------------------------------------
        yield return ("static.keyword.exact", """
            from System import Math

            def main() -> None:
                print(Math.max(val1=1, val2=2))
            """, "out:2");
        yield return ("static.keyword.string_beats_object", """
            from System import Console

            def main() -> None:
                Console.write_line(value="hello")
            """, "out:hello");
        yield return ("static.keyword.none_into_nonnullable", """
            from System import Environment

            def main() -> None:
                print(Environment.get_environment_variable(variable=None))
            """, "SPY0229");
        yield return ("static.keyword.wrong_type", """
            from System import Math

            def main() -> None:
                print(Math.max(val1="a", val2=1))
            """, "SPY0220");
        yield return ("static.keyword.wrong_arity", """
            from System import Math

            def main() -> None:
                print(Math.max(x=1, y=2))
            """, "SPY0234");

        // ---- constructor.positional ----------------------------------------------
        yield return ("constructor.positional.exact", """
            from System import DateTime

            def main() -> None:
                d: DateTime = DateTime(2020, 1, 2)
                print(d.year)
            """, "out:2020");
        yield return ("constructor.positional.widening", """
            from System.Numerics import Vector2

            def main() -> None:
                v: Vector2 = Vector2(1, 2)
                print(v.x)
            """, "out:1.0");
        yield return ("constructor.positional.none_into_nonnullable", """
            from System import Uri

            def main() -> None:
                u: Uri = Uri(None)
                print(u.host)
            """, "SPY0229");
        yield return ("constructor.positional.wrong_type", """
            from System import DateTime

            def main() -> None:
                d: DateTime = DateTime(2020, 1, "x")
                print(d.year)
            """, "SPY0220");
        yield return ("constructor.positional.unmappable_formal", """
            from System.Text import StringBuilder
            from System.Globalization import CultureInfo

            def main() -> None:
                c: CultureInfo = CultureInfo(1, "not a bool")
                print(c.name)
            """, "SPY0220");
        yield return ("constructor.positional.params_tail", """
            from System import AggregateException, InvalidOperationException

            def main() -> None:
                e: AggregateException = AggregateException(
                    InvalidOperationException("a"), InvalidOperationException("b"))
                print(e.inner_exceptions.count)
            """, "out:2");
        yield return ("constructor.positional.wrong_arity", """
            from System.Numerics import Vector2

            def main() -> None:
                v: Vector2 = Vector2(1.0, 2.0, 3.0)
                print(v.x)
            """, "SPY0224");

        // ---- constructor.keyword -------------------------------------------------
        yield return ("constructor.keyword.exact", """
            from System.Numerics import Vector2

            def main() -> None:
                v: Vector2 = Vector2(x=1.0, y=2.0)
                print(v.x)
            """, "out:1.0");
        yield return ("constructor.keyword.none_into_nonnullable", """
            from System import Uri

            def main() -> None:
                u: Uri = Uri(uri_string=None)
                print(u.host)
            """, "SPY0229");
        yield return ("constructor.keyword.wrong_type", """
            from System.Numerics import Vector2

            def main() -> None:
                v: Vector2 = Vector2(x="a", y="b")
                print(v.x)
            """, "SPY0220");
        yield return ("constructor.keyword.wrong_arity", """
            from System.Numerics import Vector2

            def main() -> None:
                v: Vector2 = Vector2(nosuch=1.0)
                print(v.x)
            """, "SPY0234");

        // ---- indexer.positional --------------------------------------------------
        yield return ("indexer.positional.exact", """
            from System.Collections.Generic import Dictionary

            def main() -> None:
                d: Dictionary[int, str] = Dictionary[int, str]()
                d[1] = "a"
                print(d[1])
            """, "out:a");
        yield return ("indexer.positional.widening", """
            from System.Collections.Generic import Dictionary

            def main() -> None:
                d: Dictionary[float, str] = Dictionary[float, str]()
                d[1] = "a"
                print(d[1])
            """, "out:a");
        // A `None` key at a VALUE-TYPE formal is refused by C#'s own null-literal rule. A key at a
        // substituted `notnull` type parameter (Dictionary[str, int]) is NOT: .NET declares nothing
        // there and C# only warns, which is the same by-design acceptance Dictionary.add(None, v)
        // carries (#1705).
        yield return ("indexer.positional.none_into_nonnullable", """
            from System.Text import StringBuilder

            def main() -> None:
                sb: StringBuilder = StringBuilder()
                sb.append("x")
                print(sb[None])
            """, "SPY0220");
        yield return ("indexer.positional.wrong_type", """
            from System.Collections.Generic import Dictionary

            def main() -> None:
                d: Dictionary[int, str] = Dictionary[int, str]()
                d[1] = "a"
                print(d["wrong"])
            """, "SPY0220");

        // ---- extension.positional ------------------------------------------------
        yield return ("extension.positional.exact", """
            from System.Linq import Enumerable

            def main() -> None:
                xs: list[int] = [1, 2, 3]
                print(xs.first_or_default(7))
            """, "out:1");
        yield return ("extension.positional.widening", """
            from System.Linq import Enumerable

            def main() -> None:
                xs: list[float] = [1.5, 2.5]
                print(xs.first_or_default(3))
            """, "out:1.5");
        // C#'s own null-literal rule decides this one: `None` is applicable to a reference-type
        // formal and to nothing else, so an `int32`/`Index` formal refuses it by name. A `None` at a
        // REFERENCE-typed extension formal is accepted here and selected correctly (String beats the
        // Func sibling), but the emitter does not cast it, so Roslyn still finds CS0121 — a CodeGen
        // concern outside this seam, reported to the round rather than encoded here.
        yield return ("extension.positional.none_into_nonnullable", """
            from System.Linq import Enumerable

            def main() -> None:
                xs: list[int] = [1]
                print(xs.element_at_or_default(None))
            """, "SPY0220");
        yield return ("extension.positional.wrong_type", """
            from System.Linq import Enumerable

            def main() -> None:
                xs: list[int] = [1, 2, 3]
                print(xs.first_or_default("a"))
            """, "SPY0220");
        yield return ("extension.positional.unmappable_formal", """
            from System.Linq import Enumerable

            def main() -> None:
                xs: list[int] = [1, 2, 3]
                print(xs.where("not a predicate").count())
            """, "SPY0220");
        yield return ("extension.positional.wrong_arity", """
            from System.Linq import Enumerable

            def main() -> None:
                xs: list[int] = [1, 2, 3]
                print(xs.first_or_default(1, 2, 3, 4))
            """, "SPY0224");
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Cell_ProducesItsExpectation(string key, string source, string expectation)
    {
        var result = CompileAndExecute(source);

        if (expectation.StartsWith("out:", StringComparison.Ordinal))
        {
            var expected = expectation.Substring("out:".Length);
            result.CompilationErrors.Should().BeEmpty(
                $"cell {key} must compile; it reported: {string.Join(" | ", result.CompilationErrors)}");
            result.Success.Should().BeTrue($"cell {key} must run");
            result.StandardOutput.Trim().Should().Be(expected, $"cell {key}");
            return;
        }

        var reported = result.CompilationErrors.Concat(
            result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")).ToList();
        reported.Should().NotBeEmpty($"cell {key} must be refused");
        reported.Should().Contain(
            message => message.Contains(expectation, StringComparison.Ordinal),
            $"cell {key} must be refused with {expectation}; it reported: {string.Join(" | ", reported)}");

        // SPY0908 is the shape this seam exists to remove: a diagnostic Roslyn produced is not a
        // diagnostic the checker produced (docs/design/spy0908-policy.md).
        reported.Should().NotContain(
            message => message.Contains("SPY0908", StringComparison.Ordinal),
            $"cell {key} must be refused by the checker, not by the C# compiler");
    }

    // -- Extension precedence: not a product cell -----------------------------------
    //
    // These are NOT route x form x mismatch cells, in the same way AnnotationReferenceMatrixTests'
    // alias-body position is not a kind x position cell: the fact is about which SURFACE owns a call,
    // not about how one surface decides an argument. Forcing them into the product would add nine
    // N/A rows whose only reason is "this axis value is about extension precedence", which is another
    // way of saying they do not belong to it.
    //
    // The contract: an EMPTY instance candidate surface is not a proof of absence. It is also what a
    // Sharpy-verb mapping produces (`xs.index(20)` binds `List<int>.IndexOf`, #1571) and what the
    // member seam's own conservatism produces when a PROPERTY shares the name — "the call may be
    // invoking a delegate stored there, which the method surface does not describe". The extension
    // probe has to carry that conservatism with it, or it converts an "I cannot see it" into a
    // refusal. `recv.count(0)` on a `List[int]` is the case that caught it: `count` is the `Count`
    // PROPERTY, the probe read the call as an extension call, found
    // `Enumerable.Count(source, Func<T, bool>)`, and refused the `int` argument with
    // "expects '(int32) -> bool'" — a program the base compiler accepts.

    public static TheoryData<string, string, string> ExtensionPrecedenceCells()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var (key, source, expectation) in ExtensionPrecedenceRoster())
            data.Add(key, source, expectation);
        return data;
    }

    private static IEnumerable<(string Key, string Source, string Expectation)> ExtensionPrecedenceRoster()
    {
        // The regression, asserted as an ABSENCE because the C# outcome is another branch's: a
        // value-vs-delegate overload pair reached through a receiver whose same-named PROPERTY
        // suppressed the instance surface, called with a literal argument. What this seam owes the
        // program is that it does not REFUSE it; whether the emitted call then binds depends on which
        // `List` the annotation resolves to, which is the Sharpy-namespace resolution item, not this
        // one. The absence is paired with the positive control below, so it cannot pass vacuously.
        yield return ("property_shares_the_name.value_argument", """
            def _use(recv: List[int]) -> None:
                _x = recv.count(0)

            def main() -> None:
                print("ok")
            """, "not-refused:(int32) -> bool");

        // The same shape through the Sharpy-verb mapping (#1571): `index` binds IndexOf, while
        // .NET 9's `Enumerable.Index` takes no argument beyond its receiver.
        yield return ("verb_mapping_shares_the_name.value_argument", """
            from System.Collections.Generic import List

            def main() -> None:
                xs: List[int] = List[int]()
                xs.add(10)
                xs.add(20)
                print(xs.index(20))
            """, "out:1");

        // The positive control: where NO instance member and NO property answers the name, the
        // extension surface IS the binding and its argument mistype is refused by name. Without this,
        // the two cells above could pass by the probe never refusing anything.
        yield return ("no_instance_member.argument_mistype_still_refused", """
            from System.Linq import Enumerable

            def main() -> None:
                xs: list[int] = [1, 2, 3]
                print(xs.first_or_default("a"))
            """, "SPY0220");
    }

    [Theory]
    [MemberData(nameof(ExtensionPrecedenceCells))]
    public void ExtensionSurface_DoesNotClaimACallTheInstanceSurfaceOnlyFailedToSee(
        string key, string source, string expectation)
    {
        if (!expectation.StartsWith("not-refused:", StringComparison.Ordinal))
        {
            Cell_ProducesItsExpectation(key, source, expectation);
            return;
        }

        var formal = expectation.Substring("not-refused:".Length);
        var result = CompileAndExecute(source);
        var reported = result.CompilationErrors.Concat(
            result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")).ToList();

        reported.Should().NotContain(
            message => message.Contains("SPY0220", StringComparison.Ordinal)
                       && message.Contains(formal, StringComparison.Ordinal),
            $"cell {key}: the extension surface must not claim a call the instance surface only "
            + $"failed to SEE, so no argument of it is refused against '{formal}'");
    }

    /// <summary>
    /// The totality pin: every cell of route x argument form x mismatch is either LIVE above or
    /// rostered in <see cref="NotApplicable"/> with a reason. The axis sizes are literals, so
    /// deleting a route from <see cref="Routes"/> fails here instead of shrinking the matrix
    /// silently (a count derived from the same array it measures is vacuous).
    /// </summary>
    [Fact]
    public void Matrix_IsTotalOverRouteTimesFormTimesMismatch()
    {
        Routes.Should().HaveCount(RouteCount);
        ArgumentForms.Should().HaveCount(ArgumentFormCount);
        Mismatches.Should().HaveCount(MismatchCount);

        var live = CellRoster().Select(cell => cell.Key).ToList();
        live.Should().OnlyHaveUniqueItems("a cell key names exactly one cell");

        var missing = new List<string>();
        foreach (var route in Routes)
        {
            foreach (var form in ArgumentForms)
            {
                foreach (var mismatch in Mismatches)
                {
                    var key = $"{route}.{form}.{mismatch}";
                    if (!live.Contains(key) && !NotApplicable.ContainsKey(key))
                        missing.Add(key);
                }
            }
        }

        missing.Should().BeEmpty(
            "every route x form x mismatch cell is live or rostered N/A with a reason");
        (live.Count + NotApplicable.Count).Should().Be(
            RouteCount * ArgumentFormCount * MismatchCount,
            "live and N/A cells partition the matrix — an overlap means a cell is both");
    }

    /// <summary>
    /// Every N/A reason is a sentence about why the SPELLING cannot exist, not a placeholder. A
    /// reason that greps to nothing is how a matrix quietly stops covering a route.
    /// </summary>
    [Fact]
    public void NotApplicable_ReasonsAreStated()
    {
        foreach (var (key, reason) in NotApplicable)
        {
            reason.Should().NotBeNullOrWhiteSpace($"cell {key}");
            reason.Length.Should().BeGreaterThan(20, $"cell {key} needs a reason, not a placeholder");
            key.Split('.').Should().HaveCount(3, "an N/A key is route.form.mismatch");
            Routes.Should().Contain(key.Split('.')[0], $"cell {key} names a rostered route");
            ArgumentForms.Should().Contain(key.Split('.')[1], $"cell {key} names a rostered form");
            Mismatches.Should().Contain(key.Split('.')[2], $"cell {key} names a rostered mismatch");
        }
    }
}
