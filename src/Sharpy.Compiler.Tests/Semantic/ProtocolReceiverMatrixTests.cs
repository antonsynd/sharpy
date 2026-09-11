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
    private sealed record Payload(
        string Id, string Type, string Value, bool IsStruct,
        string Needle, string Key, string Member);

    private static readonly Payload[] Payloads =
    {
        new("str", "str", "\"ab\"", false, "\"a\"", "0", "v.upper()"),
        new("list", "list[int]", "[1, 2]", false, "1", "0", "v.count(1)"),
        new("dict", "dict[str, int]", "{\"a\": 1}", false, "\"a\"", "\"a\"", "v.get(\"a\")"),
        new("bytes", "bytes", "b\"ab\"", true, "97", "0", "v.decode()"),
        new("tuple", "tuple[int, int]", "(1, 2)", true, "1", "0", "v.item1"),
        new("userclass", "Bag", "Bag()", false, "2", "0", "v.tag()"),
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
    /// the routes genuinely differ and pretending otherwise would need an "either code" assertion
    /// that stops discriminating: the protocol routes say SPY0326 and name narrowing and unwrapping,
    /// the MEMBER route says SPY0229 with the same three remedies in its message
    /// (a divergence reported as a finding, not fixed here), and TRUTHINESS is not a refusal at all —
    /// <c>if o:</c> on an <c>Optional</c> tests is-some by design (<c>OptionalIsSome</c>).
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
        new("member", p => $"print({p.Member})", DiagnosticCodes.Semantic.NullabilityViolation),
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

        // A tuple is not truth-testable at all: `if (1, 2):` is SPY0220 at this sha AND at
        // 311252e33 (controlled), where python3 prints. The BARE cell is the defect, so the
        // comparison against it would compare two refusals and prove nothing. Cited, not silent.
        if (route.Id == "truthiness" && payload.Id == "tuple")
            return "a tuple is not truth-testable yet — the bare control is itself refused (#1861)";

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
        strict.RawDiagnostics.Should().Contain(
            d => d.Code == route.StrictRefusal,
            $"the strict family is refused BY NAME ({route.StrictRefusal}) at {routeId}; got "
            + string.Join(" | ", strict.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}"))
            + "\n" + strictSource);
    }

    private static List<string> Normalize(string output)
        => output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && l != "=== Running Program ===")
            .ToList();

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
            new[] { "len", "in", "iterate", "comprehension", "index", "slice", "truthiness", "member" },
            "the route axis is every protocol route a receiver reaches");

        Payloads.Should().Contain(p => p.IsStruct,
            "the struct column is the discriminating one — without it the matrix cannot see #1792");

        Routes.Where(r => r.StrictRefusal != null).Should().HaveCount(7,
            "seven of the eight routes refuse the strict wrapper; truthiness is the exception and "
            + "says so explicitly rather than by omission");

        var naCells = BuildNaCells();
        naCells.Should().OnlyContain(n => n.Reason.Length >= 20,
            "every N/A cell states why (>= 20 chars)");

        var live = Cells.Count();
        (live + naCells.Length).Should().Be(48,
            $"6 payloads x 8 routes = 48 cells; live ({live}) + N/A ({naCells.Length})");

        naCells.Should().HaveCount(3,
            "three cells are excluded, each naming a language rule or an OPEN issue: dict slicing, "
            + "user-class slicing, and tuple truthiness (#1861)");
    }
}
