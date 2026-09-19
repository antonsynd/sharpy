using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The protocol-receiver matrix (#1792, #1808): <b>payload × wrapper × protocol route</b>.
///
/// <para><b>Contract under test:</b> a receiver's WRAPPER decides only whether the receiver may be
/// dereferenced, never HOW. So for every payload kind and every protocol route, the bare <c>T</c>
/// and the loose <c>T | None</c> produce the same program (the spec's "protocol operations and
/// member access work directly on the underlying type",
/// <c>tagged_unions_optional.md</c>), and the strict <c>T?</c> is refused by name with SPY0326 at
/// every one of them.</para>
///
/// <para><b>Why the assertion compares the two wrappers instead of pinning strings:</b> the defect
/// class is DIVERGENCE between the wrapper spellings, and every cell that pinned a literal expected
/// output could be satisfied by two routes that agree with the pin and disagree with each other.
/// Comparing the loose cell against the bare cell's measured output makes the contract itself the
/// assertion: a route that unwraps at one wrapper and not the other cannot pass, whatever the
/// values are.</para>
///
/// <para><b>The struct payloads are the discriminating column.</b> <c>bytes</c> and a tuple emit as
/// C# value types, so a loose wrapper over them is a <c>Nullable&lt;T&gt;</c> with none of the
/// payload's members. Before the receiver-unwrap fact existed, those cells were CS0021 / CS1061 /
/// CS1503 / CS1579 / CS1929 behind SPY0908 while the reference payloads passed — the whole class
/// lived in one column of this matrix.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ProtocolReceiverMatrixTests : IntegrationTestBase
{
    public ProtocolReceiverMatrixTests(ITestOutputHelper output) : base(output) { }

    // ───────────────────────────── axes ─────────────────────────────

    /// <param name="Id">Cell id fragment.</param>
    /// <param name="Type">The payload's Sharpy type spelling.</param>
    /// <param name="Value">An expression of that type.</param>
    /// <param name="IsStruct">Whether the payload emits as a C# value type (the unwrap column).</param>
    /// <param name="Needle">A value the payload contains, for the membership route.</param>
    /// <param name="Key">The index/subscript expression body.</param>
    /// <param name="Member">A member access or method call on the receiver.</param>
    /// <param name="HasCall">Whether the payload declares <c>__call__</c> — only <c>userclass</c>
    /// does; the other five are refused even BARE at the <c>call</c> route (not callable at all),
    /// which is a language rule unrelated to the Optional wrapper and N/A's that cell (#1855).</param>
    private sealed record Payload(
        string Id, string Type, string Value, bool IsStruct,
        string Needle, string Key, string Member, bool HasCall = false);

    private static readonly Payload[] Payloads =
    {
        new("str", "str", "\"ab\"", false, "\"a\"", "0", "v.upper()"),
        new("list", "list[int]", "[1, 2]", false, "1", "0", "v.count(1)"),
        new("dict", "dict[str, int]", "{\"a\": 1}", false, "\"a\"", "\"a\"", "v.get(\"a\")"),
        new("bytes", "bytes", "b\"ab\"", true, "97", "0", "v.decode()"),
        new("tuple", "tuple[int, int]", "(1, 2)", true, "1", "0", "v.item1"),
        new("userclass", "Bag", "Bag()", false, "2", "0", "v.tag()", HasCall: true),
    };

    private sealed record Wrapper(string Id, string Suffix);

    private static readonly Wrapper[] Wrappers =
    {
        new("bare", ""),
        new("loose", " | None"),
        new("strict", "?"),
    };

    /// <param name="Id">Cell id fragment.</param>
    /// <param name="Body">The statement the route is written as.</param>
    /// <param name="StrictRefusal">
    /// The code the STRICT <c>T?</c> wrapper is refused with at this route, or <c>null</c> when the
    /// strict wrapper is not refused at all. Per route rather than one code for the matrix, because
    /// TRUTHINESS is genuinely not a refusal at all — <c>if o:</c> on an <c>Optional</c> tests
    /// is-some by design (<c>OptionalIsSome</c>) — while every OTHER route says SPY0326
    /// (<c>OptionalRequiresNarrowing</c>), including <c>member</c> and <c>call</c> (#1855: both used
    /// to say something else — SPY0229 "has no member", SPY0230 "not callable" — for the identical
    /// strict-Optional mistake; one vocabulary now covers every route in this matrix).
    /// </param>
    private sealed record Route(string Id, Func<Payload, string> Body, string? StrictRefusal);

    private static readonly Route[] Routes =
    {
        new("len", _ => "print(len(v))", DiagnosticCodes.Semantic.OptionalRequiresNarrowing),
        new("in", p => $"print({p.Needle} in v)", DiagnosticCodes.Semantic.OptionalRequiresNarrowing),
        new("iterate", _ => "for x in v:\n        print(x)",
            DiagnosticCodes.Semantic.OptionalRequiresNarrowing),
        new("comprehension", _ => "print([x for x in v])",
            DiagnosticCodes.Semantic.OptionalRequiresNarrowing),
        new("index", p => $"print(v[{p.Key}])", DiagnosticCodes.Semantic.OptionalRequiresNarrowing),
        new("slice", _ => "print(v[0:1])", DiagnosticCodes.Semantic.OptionalRequiresNarrowing),
        new("truthiness", _ => "if v:\n        print(\"t\")", null),
        new("member", p => $"print({p.Member})", DiagnosticCodes.Semantic.OptionalRequiresNarrowing),
        new("call", _ => "print(v(1))", DiagnosticCodes.Semantic.OptionalRequiresNarrowing),
    };

    private const string BagDeclaration = @"class Bag:
    items: list[int]

    def __init__(self) -> None:
        self.items = [1, 2, 3]

    def __len__(self) -> int:
        return len(self.items)

    def __iter__(self) -> Iterator[int]:
        return iter(self.items)

    def __contains__(self, item: int) -> bool:
        return item in self.items

    def __getitem__(self, index: int) -> int:
        return self.items[index]

    def __call__(self, x: int) -> int:
        return x

    def tag(self) -> str:
        return ""bag""

";

    // ───────────────────────────── N/A ─────────────────────────────

    private sealed record NaCell(string Payload, string Route, string Reason);

    /// <summary>
    /// Why a payload × route cell is not part of the contract. Every reason names a LANGUAGE rule or
    /// an OPEN issue — never "not implemented yet", which is how a matrix launders a defect into an
    /// exclusion.
    /// </summary>
    private static string? NaReason(Payload payload, Route route)
    {
        if (route.Id == "slice" && payload.Id == "dict")
            return "a mapping is not sliceable in Python either (TypeError) — refused by design";

        if (route.Id == "slice" && payload.Id == "userclass")
            return "__getitem__ over an int does not make a user class sliceable — no slice protocol";

        if (route.Id == "call" && !payload.HasCall)
            return $"{payload.Id} has no __call__ — refused even BARE, unrelated to the Optional "
                + "wrapper this matrix is about";

        return null;
    }

    private static NaCell[] BuildNaCells()
    {
        var cells = new List<NaCell>();
        foreach (var payload in Payloads)
            foreach (var route in Routes)
            {
                var reason = NaReason(payload, route);
                if (reason != null)
                    cells.Add(new NaCell(payload.Id, route.Id, reason));
            }

        return cells.ToArray();
    }

    // ───────────────────────────── program generation ─────────────────────────────

    private static string Program(Payload payload, Wrapper wrapper, Route route)
    {
        var declaration = payload.Id == "userclass" ? BagDeclaration : "";
        var value = wrapper.Id == "strict" ? $"Some({payload.Value})" : payload.Value;
        return declaration
            + "def main() -> None:\n"
            + $"    v: {payload.Type}{wrapper.Suffix} = {value}\n"
            + $"    {route.Body(payload)}\n";
    }

    public static IEnumerable<object[]> Cells =>
        from payload in Payloads
        from route in Routes
        where NaReason(payload, route) == null
        select new object[] { payload.Id, route.Id };

    // ───────────────────────────── runner ─────────────────────────────

    [Theory]
    [MemberData(nameof(Cells))]
    public void LooseWrapperDispatchesLikeBareAndStrictIsRefusedByName(string payloadId, string routeId)
    {
        var payload = Payloads.Single(p => p.Id == payloadId);
        var route = Routes.Single(r => r.Id == routeId);

        // tuple×truthiness (#1861, R-AU) is the ONE cell in this matrix where the BARE receiver,
        // not the wrapped ones, is what's refused: a fixed-arity tuple has no falsy case to test at
        // all (its truthiness is a compile-time constant, not a runtime question). Both wrappers
        // test their OWN truthiness (null-check / is-some), never the tuple's, so both still RUN —
        // the divergence question this matrix asks (does the wrapper decide only WHETHER the
        // receiver may be dereferenced, never HOW) is answered the same way here as everywhere
        // else, just with bare on the refused side instead of the wrapped sides.
        if (payloadId == "tuple" && routeId == "truthiness")
        {
            var tupleBareSource = Program(payload, Wrappers[0], route);
            var tupleBare = CompileAndExecute(tupleBareSource);
            tupleBare.Success.Should().BeFalse(
                "a fixed-arity tuple has no falsy case to test\n" + tupleBareSource);
            tupleBare.RawDiagnostics.Should().Contain(
                d => d.Message.Contains("is not truth-testable", StringComparison.Ordinal),
                "the refusal names the fact; got: "
                + string.Join(" | ", tupleBare.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));

            var tupleLooseSource = Program(payload, Wrappers[1], route);
            var tupleLoose = CompileAndExecute(tupleLooseSource);
            tupleLoose.Success.Should().BeTrue(
                "tuple[...] | None tests its OWN null-check truthiness, never the tuple's: "
                + string.Join("; ", tupleLoose.CompilationErrors) + "\n" + tupleLooseSource);

            var tupleStrictSource = Program(payload, Wrappers[2], route);
            var tupleStrict = CompileAndExecute(tupleStrictSource);
            tupleStrict.Success.Should().BeTrue(
                "tuple[...]? tests its OWN is-some truthiness, never the tuple's: "
                + string.Join("; ", tupleStrict.CompilationErrors) + "\n" + tupleStrictSource);
            return;
        }

        // call×userclass (#1918, sibling defect found adding this route for #1855): unlike EVERY
        // other route in this matrix, the loose `T | None` wrapper does NOT dispatch `__call__` like
        // bare `T` — TryResolveCallableObject only handles UserDefinedType/GenericType calleeType, so
        // a NullableType-wrapped receiver falls through to "not callable" (SPY0230) instead of being
        // unwrapped first. Out of #1855's own scope (that issue is the STRICT family's vocabulary,
        // not making the LOOSE wrapper callable at all) — filed, not fixed here. Kept LIVE rather
        // than silently N/A'd: this positively asserts the CURRENT (broken) behavior, so a fix lands
        // as a RED here demanding the cell be corrected, not a silent pass that never prompts removal.
        if (payloadId == "userclass" && routeId == "call")
        {
            var callBareSource = Program(payload, Wrappers[0], route);
            var callBare = CompileAndExecute(callBareSource);
            callBare.Success.Should().BeTrue(
                "the BARE control must run before the wrapper cells mean anything: "
                + string.Join("; ", callBare.CompilationErrors) + "\n" + callBareSource);

            var callLooseSource = Program(payload, Wrappers[1], route);
            var callLoose = CompileAndExecute(callLooseSource);
            callLoose.Success.Should().BeFalse(
                "#1918: loose Bag | None does NOT yet dispatch __call__ like bare Bag — remove this "
                + "branch and restore the normal loose-must-run assertion once #1918 is fixed\n" + callLooseSource);
            callLoose.RawDiagnostics.Should().Contain(
                d => d.Code == DiagnosticCodes.Semantic.NotCallable,
                "#1918's CURRENT symptom is SPY0230 \"not callable\"; got: "
                + string.Join(" | ", callLoose.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));

            var callStrictSource = Program(payload, Wrappers[2], route);
            var callStrict = CompileAndExecute(callStrictSource);
            callStrict.Success.Should().BeFalse(
                $"strict `{payload.Type}?` must be refused at call, not dereferenced\n" + callStrictSource);
            callStrict.RawDiagnostics.Should().ContainSingle(
                d => d.Code == route.StrictRefusal,
                "the strict family is refused BY NAME (#1855), exactly once, at call; got: "
                + string.Join(" | ", callStrict.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
            return;
        }

        var bareSource = Program(payload, Wrappers[0], route);
        var bare = CompileAndExecute(bareSource);
        bare.Success.Should().BeTrue(
            $"the BARE control {payloadId}×{routeId} must run before the wrapper cells mean anything "
            + "(a red control makes the comparison vacuous): "
            + string.Join("; ", bare.CompilationErrors) + "\n" + bareSource);

        var looseSource = Program(payload, Wrappers[1], route);
        var loose = CompileAndExecute(looseSource);
        loose.Success.Should().BeTrue(
            $"loose `{payload.Type} | None` must dispatch like `{payload.Type}` at {routeId}: "
            + string.Join("; ", loose.CompilationErrors) + "\n" + looseSource);

        Normalize(loose.StandardOutput).Should().Equal(Normalize(bare.StandardOutput),
            $"the wrapper decides WHETHER the receiver may be dereferenced, never HOW — "
            + $"{payloadId}×{routeId} must print what the bare receiver prints\n" + looseSource);

        var strictSource = Program(payload, Wrappers[2], route);
        var strict = CompileAndExecute(strictSource);

        if (route.StrictRefusal == null)
        {
            strict.Success.Should().BeTrue(
                $"`if o:` on a strict Optional tests is-some by design, so {routeId} is not a "
                + "refusal: " + string.Join("; ", strict.CompilationErrors) + "\n" + strictSource);
            return;
        }

        strict.Success.Should().BeFalse(
            $"strict `{payload.Type}?` must be refused at {routeId}, not dereferenced\n" + strictSource);
        // #1855: exactly ONE diagnostic per strict access, not just "contains the right code" — g07
        // used to double-report (SPY0229 here + SPY0203 from the extension-method steer) for the
        // IDENTICAL member access, and a mere Contain would not have caught that regression.
        strict.RawDiagnostics.Should().ContainSingle(
            d => d.Code == route.StrictRefusal,
            $"the strict family is refused BY NAME ({route.StrictRefusal}), exactly once, at {routeId}; got "
            + string.Join(" | ", strict.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}"))
            + "\n" + strictSource);
    }

    private static List<string> Normalize(string output)
        => output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && l != "=== Running Program ===")
            .ToList();

    // ──────── #1860: IEnumerable[int]/Iterator[int] receivers at the `in` route ────────

    /// <summary>
    /// The registry-vs-CLR-shape widening (#1860, R-AN): a receiver typed <c>IEnumerable[int]</c> or
    /// <c>Iterator[int]</c> answers <c>in</c> — LINQ's <c>Enumerable.Contains</c> is exactly the
    /// emitted call (<c>ProtocolMembershipTests</c> pins the registry-table delta this exercises at
    /// the unit level; this is the end-to-end wrapper-family check the same style as the main matrix
    /// above uses).
    ///
    /// <para>Not folded into the <c>Payloads</c> array above: these two payloads answer ONLY <c>in</c>
    /// among the matrix's eight routes (no <c>__len__</c>/<c>__getitem__</c>/index of their own —
    /// they are bare producer types, not user protocol classes), so crossing them with all eight
    /// routes would be seven N/A rows per payload for a reason that has nothing to do with #1860. A
    /// dedicated theory, in the SAME wrapper-comparison style (bare vs loose must agree, strict
    /// refused by name) as <see cref="LooseWrapperDispatchesLikeBareAndStrictIsRefusedByName"/>.</para>
    /// </summary>
    [Theory]
    [InlineData("IEnumerable[int]", "[1, 2, 3]")]
    [InlineData("Iterator[int]", "iter([1, 2, 3])")]
    public void GenericIEnumerableReceiver_AnswersInRoute_LikeEveryOtherPayload(string type, string value)
    {
        string Program(string wrapperSuffix, string v)
            => "def main() -> None:\n"
                + $"    v: {type}{wrapperSuffix} = {v}\n"
                + "    print(2 in v)\n";

        var bareSource = Program("", value);
        var bare = CompileAndExecute(bareSource);
        bare.Success.Should().BeTrue(
            $"the BARE control {type}×in must run before the wrapper cells mean anything: "
            + string.Join("; ", bare.CompilationErrors) + "\n" + bareSource);

        var looseSource = Program(" | None", value);
        var loose = CompileAndExecute(looseSource);
        loose.Success.Should().BeTrue(
            $"loose `{type} | None` must dispatch like `{type}` at in: "
            + string.Join("; ", loose.CompilationErrors) + "\n" + looseSource);
        Normalize(loose.StandardOutput).Should().Equal(Normalize(bare.StandardOutput),
            $"{type}×in must print what the bare receiver prints\n" + looseSource);

        var strictSource = Program("?", $"Some({value})");
        var strict = CompileAndExecute(strictSource);
        strict.Success.Should().BeFalse(
            $"strict `{type}?` must be refused at in, not dereferenced\n" + strictSource);
        strict.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.OptionalRequiresNarrowing,
            $"the strict family is refused BY NAME at in; got "
            + string.Join(" | ", strict.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}"))
            + "\n" + strictSource);
    }

    /// <summary>
    /// d13: BEFORE #1860's fix, a wrong-typed needle against these two receivers reported the
    /// mismatch TWICE — <c>ClassifyMembership</c> (TypeChecker, SPY0222, needle vs. element type) and
    /// <c>ProtocolValidator.ValidateMembership</c> (SPY0320, "missing '__contains__'") both fired,
    /// because <c>ResolveMembershipElementType</c> resolved an element fine (via the general iterable
    /// arm, independent of <c>ProtocolMembership</c>) while the registry wrongly denied
    /// <c>__contains__</c> presence. No SEPARATE code change was needed to collapse it: once
    /// <c>Has</c> answers true for these receivers, the presence check has nothing left to report.
    /// </summary>
    [Theory]
    [InlineData("IEnumerable[int]", "[1, 2, 3]")]
    [InlineData("Iterator[int]", "iter([1, 2, 3])")]
    public void WrongNeedleAgainstGenericIEnumerableReceiver_ReportsOnlyOneDiagnostic(string type, string value)
    {
        var source = "def main() -> None:\n"
            + $"    v: {type} = {value}\n"
            + "    print(\"x\" in v)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"a str needle against {type}'s int element must be refused\n{source}");
        result.RawDiagnostics.Should().ContainSingle(
            d => d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation,
            $"exactly one SPY0222, naming the needle/element mismatch; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")) + "\n" + source);
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Semantic.ProtocolMissingMethod,
            $"the presence check (SPY0320) must not ALSO fire — {type} answers __contains__; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")) + "\n" + source);
    }

    // ───────────────────────────── totality pin ─────────────────────────────

    /// <summary>
    /// The axes are pinned to LITERALS, not to <c>Payloads.Length</c> — an identity computed from the
    /// arrays that build both sides cannot notice a deleted axis member (the vacuous-pin shape this
    /// round found in two sibling matrices). Deleting a payload, a wrapper or a route reddens here.
    /// </summary>
    [Fact]
    public void MatrixIsTotalOverItsAxes()
    {
        Payloads.Select(p => p.Id).Should().BeEquivalentTo(
            new[] { "str", "list", "dict", "bytes", "tuple", "userclass" },
            "the payload axis is the reference/struct split plus a user protocol class");

        Wrappers.Select(w => w.Id).Should().BeEquivalentTo(
            new[] { "bare", "loose", "strict" },
            "the wrapper axis is exactly the three families");

        Routes.Select(r => r.Id).Should().BeEquivalentTo(
            new[] { "len", "in", "iterate", "comprehension", "index", "slice", "truthiness", "member", "call" },
            "the route axis is every protocol route a receiver reaches");

        Payloads.Should().Contain(p => p.IsStruct,
            "the struct column is the discriminating one — without it the matrix cannot see #1792");

        // Corrected from the plan's own estimate (9): eight of the NINE routes refuse the strict
        // wrapper (every route except truthiness); adding exactly ONE new route (call) to the prior
        // seven non-null (len/in/iterate/comprehension/index/slice/member) makes eight, not nine.
        Routes.Where(r => r.StrictRefusal != null).Should().HaveCount(8,
            "eight of the nine routes refuse the strict wrapper; truthiness is the exception and "
            + "says so explicitly rather than by omission");

        var naCells = BuildNaCells();
        naCells.Should().OnlyContain(n => n.Reason.Length >= 20,
            "every N/A cell states why (>= 20 chars)");

        var live = Cells.Count();
        (live + naCells.Length).Should().Be(54,
            $"6 payloads x 9 routes = 54 cells; live ({live}) + N/A ({naCells.Length})");

        naCells.Should().HaveCount(7,
            "dict slicing and user-class slicing (a language rule each), plus FIVE payloads with no "
            + "__call__ at the new call route (str/list/dict/bytes/tuple — not callable at all, "
            + "refused even BARE, unrelated to the Optional wrapper this matrix is about); only "
            + "userclass is live at call");
    }
}
