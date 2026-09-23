using System.Collections.Generic;
using System.Linq;
using Sharpy.Stdlib.Tests.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// The stdlib mapping × protocol conformance matrix (#1933). Every <c>collections</c> mapping must
/// expose the SAME protocol surface a builtin <c>dict</c> does, and — this is the class contract —
/// the checker's answer for each protocol must be DISCOVERED from the CLR type by reflection
/// (<c>ProtocolMembership.HasClrProtocol</c>), never asserted by a name-keyed rule. Each cell is an
/// executing Sharpy program whose expected stdout is a python3 3.12.13-verified literal (the oracle
/// comment sits on the cell). The mappings are {dict (control), defaultdict, OrderedDict, ChainMap,
/// Counter}; the protocols are {len, in, for-k-in, list(x), keys/values/items length+order,
/// reversed(x), == same type, == dict (both operand orders), bool empty/non-empty, construct from
/// str, truth position (<c>if m:</c> and the ternary), <c>str()</c>/<c>print</c>, <c>repr()</c>/<c>f"{}"</c>}.
/// Six cross-stdlib equality pairs (Design Decision 5) are asserted separately.
///
/// <para>
/// <c>str</c> and <c>repr</c> (#1968) are the <c>__str__</c>/<c>__repr__</c> columns, spelled
/// <c>ToString()</c> in the dunder table. Without an override every mapping printed its KEY LIST
/// (<c>['a', 'b', 'c']</c>) because <c>SequenceFormatting</c> renders any enumerable that does not
/// render itself. Deviation: <c>defaultdict</c> prints <c>defaultdict(&lt;factory&gt;, {...})</c>
/// where CPython prints the factory's repr (<c>&lt;class 'int'&gt;</c>) — owner ruling R-BN, documented
/// on <c>DefaultDict.ToString</c>.
/// </para>
///
/// <para>
/// <c>bool</c> and <c>truth</c> are two protocols on purpose: <c>bool(x)</c> is a conversion that
/// goes through the builtin's own overload set, while <c>if x:</c> / <c>1 if x else 0</c> are
/// truth-testing positions the checker classifies itself (<c>ClassifyTruthiness</c>). The
/// two used to give different answers for the same receiver — <c>bool(od)</c> ran while
/// <c>if od:</c> was SPY0220 — because the truth classifier kept a name list that spelled
/// <c>defaultdict</c> but not its siblings (the #1933 meta-class, plan-6ca898 verify).
/// </para>
///
/// <para>
/// Every mapping (except dict) is constructed to the SAME contents — {"a":1,"b":2,"c":3} in key
/// order a,b,c — so a single set of oracle literals covers the whole column and any per-type
/// divergence is a real defect, not a fixture accident. Counter reaches the same shape from
/// ["a","b","b","c","c","c"].
/// </para>
///
/// <para>
/// Deviation: <c>reversed(ChainMap)</c> raises <c>TypeError</c> in CPython (a ChainMap is not
/// reversible), but Sharpy admits it through <c>Builtins.Reversed&lt;T&gt;(IEnumerable&lt;T&gt;)</c>
/// (the checker's <c>InferReversedElementType</c> accepts any iterable before consulting
/// <c>IReverseEnumerable</c>). The cell asserts Sharpy's reversed key order and cites
/// <c>docs/deviations.yaml:chainmap-reversed-admitted</c>.
/// </para>
/// </summary>
public class StdlibMappingProtocolMatrixTests : StdlibIntegrationTestBase
{
    public StdlibMappingProtocolMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    // ── Totality anchors: literal rosters (NOT derived from any enum), so an omitted mapping or
    //    protocol fails the count check rather than passing vacuously.
    private static readonly string[] Mappings = { "dict", "defaultdict", "OrderedDict", "ChainMap", "Counter" };

    private static readonly string[] Protocols =
    {
        "len", "in", "for", "list", "views", "reversed", "eq_same", "eq_dict", "bool", "from_str", "truth",
        "str", "repr",
    };

    /// <summary>One executing cell: a full Sharpy program and its python3-verified stdout.</summary>
    private sealed class Cell
    {
        public string Id { get; }
        public string Mapping { get; }
        public string Protocol { get; }
        public string Program { get; }
        public string Expected { get; }

        public Cell(string mapping, string protocol, string body, string expected)
        {
            Mapping = mapping;
            Protocol = protocol;
            Id = mapping + "/" + protocol;
            Program = "import collections\n\ndef main() -> None:\n" + body;
            Expected = expected;
        }
    }

    // Construction snippets binding `m` to contents {"a":1,"b":2,"c":3} in key order a,b,c.
    private static string Construct(string mapping) => mapping switch
    {
        "dict" => "    m: dict[str, int] = {\"a\": 1, \"b\": 2, \"c\": 3}\n",
        "defaultdict" =>
            "    m: collections.DefaultDict[str, int] = collections.DefaultDict[str, int](lambda: 0)\n" +
            "    m[\"a\"] = 1\n    m[\"b\"] = 2\n    m[\"c\"] = 3\n",
        "OrderedDict" =>
            "    m: collections.OrderedDict[str, int] = collections.OrderedDict[str, int]([(\"a\", 1), (\"b\", 2), (\"c\", 3)])\n",
        "ChainMap" =>
            "    m: collections.ChainMap[str, int] = collections.ChainMap[str, int]({\"a\": 1, \"b\": 2, \"c\": 3})\n",
        "Counter" =>
            "    m: collections.Counter[str] = collections.Counter[str]([\"a\", \"b\", \"b\", \"c\", \"c\", \"c\"])\n",
        _ => throw new System.ArgumentOutOfRangeException(nameof(mapping), mapping, null),
    };

    // A second instance equal to `m`, bound to `m2` (same contents, key order a,b,c).
    private static string ConstructSecond(string mapping) => mapping switch
    {
        "dict" => "    m2: dict[str, int] = {\"a\": 1, \"b\": 2, \"c\": 3}\n",
        "defaultdict" =>
            "    m2: collections.DefaultDict[str, int] = collections.DefaultDict[str, int](lambda: 0)\n" +
            "    m2[\"a\"] = 1\n    m2[\"b\"] = 2\n    m2[\"c\"] = 3\n",
        "ChainMap" =>
            "    m2: collections.ChainMap[str, int] = collections.ChainMap[str, int]({\"a\": 1, \"b\": 2, \"c\": 3})\n",
        "Counter" =>
            "    m2: collections.Counter[str] = collections.Counter[str]([\"a\", \"b\", \"b\", \"c\", \"c\", \"c\"])\n",
        _ => throw new System.ArgumentOutOfRangeException(nameof(mapping), mapping, null),
    };

    private static string Empty(string mapping) => mapping switch
    {
        "dict" => "    e: dict[str, int] = {}\n",
        "defaultdict" => "    e: collections.DefaultDict[str, int] = collections.DefaultDict[str, int](lambda: 0)\n",
        "OrderedDict" => "    e: collections.OrderedDict[str, int] = collections.OrderedDict[str, int]()\n",
        "ChainMap" => "    e: collections.ChainMap[str, int] = collections.ChainMap[str, int]()\n",
        "Counter" => "    e: collections.Counter[str] = collections.Counter[str]()\n",
        _ => throw new System.ArgumentOutOfRangeException(nameof(mapping), mapping, null),
    };

    // The python3 3.12.13 repr of `m` ({"a":1,"b":2,"c":3}) and of the empty `e`, per mapping.
    // defaultdict: `<factory>` replaces CPython's `<class 'int'>` (R-BN deviation, #1968).
    private static (string full, string empty) ReprOf(string mapping) => mapping switch
    {
        "dict" => ("{'a': 1, 'b': 2, 'c': 3}", "{}"),
        "defaultdict" => ("defaultdict(<factory>, {'a': 1, 'b': 2, 'c': 3})", "defaultdict(<factory>, {})"),
        "OrderedDict" => ("OrderedDict({'a': 1, 'b': 2, 'c': 3})", "OrderedDict()"),
        "ChainMap" => ("ChainMap({'a': 1, 'b': 2, 'c': 3})", "ChainMap({})"),
        // Most-common order: counts c=3, b=2, a=1.
        "Counter" => ("Counter({'c': 3, 'b': 2, 'a': 1})", "Counter()"),
        _ => throw new System.ArgumentOutOfRangeException(nameof(mapping), mapping, null),
    };

    private static IReadOnlyList<Cell> BuildCells()
    {
        var cells = new System.Collections.Generic.List<Cell>();

        foreach (var mapping in Mappings)
        {
            string c = Construct(mapping);

            // len — python3: len({"a":1,"b":2,"c":3}) == 3
            cells.Add(new Cell(mapping, "len", c + "    print(len(m))", "3"));

            // in — python3: "a" in m, "z" in m -> True False
            cells.Add(new Cell(mapping, "in", c + "    print(\"a\" in m, \"z\" in m)", "True False"));

            // for k in m — python3: keys in order -> a,b,c
            cells.Add(new Cell(mapping, "for",
                c + "    parts: list[str] = []\n    for k in m:\n        parts.append(k)\n    print(\",\".join(parts))",
                "a,b,c"));

            // list(m) — python3: list(m) yields keys -> a,b,c
            cells.Add(new Cell(mapping, "list",
                c + "    ks: list[str] = list(m)\n    print(\",\".join(ks))",
                "a,b,c"));

            // keys()/values()/items() length + order — python3: 3 3 3 / a,b,c / 1,2,3 / a=1,b=2,c=3
            cells.Add(new Cell(mapping, "views",
                c +
                "    print(len(m.keys()), len(m.values()), len(m.items()))\n" +
                "    kp: list[str] = []\n    for k in m.keys():\n        kp.append(k)\n    print(\",\".join(kp))\n" +
                "    vp: list[str] = []\n    for v in m.values():\n        vp.append(str(v))\n    print(\",\".join(vp))\n" +
                "    ip: list[str] = []\n    for k, v in m.items():\n        ip.append(k + \"=\" + str(v))\n    print(\",\".join(ip))",
                "3 3 3\na,b,c\n1,2,3\na=1,b=2,c=3"));

            // reversed(m) — python3: reversed keys -> c,b,a (ChainMap: deviation, see class doc)
            cells.Add(new Cell(mapping, "reversed",
                c + "    rp: list[str] = []\n    for k in reversed(m):\n        rp.append(k)\n    print(\",\".join(rp))",
                "c,b,a"));

            // == same type — python3: equal contents -> True (OrderedDict also order-sensitive)
            if (mapping == "OrderedDict")
            {
                cells.Add(new Cell(mapping, "eq_same",
                    c +
                    "    m2: collections.OrderedDict[str, int] = collections.OrderedDict[str, int]([(\"a\", 1), (\"b\", 2), (\"c\", 3)])\n" +
                    "    rev: collections.OrderedDict[str, int] = collections.OrderedDict[str, int]([(\"c\", 3), (\"b\", 2), (\"a\", 1)])\n" +
                    "    print(m == m2, m == rev)",
                    "True False"));
            }
            else
            {
                cells.Add(new Cell(mapping, "eq_same",
                    c + ConstructSecond(mapping) + "    print(m == m2)",
                    "True"));
            }

            // == dict, both operand orders — python3: True True
            cells.Add(new Cell(mapping, "eq_dict",
                c + "    print(m == {\"a\": 1, \"b\": 2, \"c\": 3}, {\"a\": 1, \"b\": 2, \"c\": 3} == m)",
                "True True"));

            // bool empty/non-empty — python3: False True
            cells.Add(new Cell(mapping, "bool",
                c + Empty(mapping) + "    print(bool(e), bool(m))",
                "False True"));

            // truth position: `if x:` statement AND the ternary, on the empty and the non-empty
            // instance — python3 3.12.13: e-empty / m-full / empty full. Discovered from the CLR
            // type's ISized surface, never from a name (#1933 sibling).
            cells.Add(new Cell(mapping, "truth",
                c + Empty(mapping) +
                "    if e:\n        print(\"e-full\")\n    else:\n        print(\"e-empty\")\n" +
                "    if m:\n        print(\"m-full\")\n    else:\n        print(\"m-empty\")\n" +
                "    print(\"full\" if e else \"empty\", \"full\" if m else \"empty\")",
                "e-empty\nm-full\nempty full"));

            // str: str(m), print(m), str(e), print(e) — python3 3.12.13 (see ReprOf).
            var (full, empty) = ReprOf(mapping);
            cells.Add(new Cell(mapping, "str",
                c + Empty(mapping) +
                "    print(str(m))\n    print(m)\n    print(str(e))\n    print(e)",
                full + "\n" + full + "\n" + empty + "\n" + empty));

            // repr: repr(m), f"{m}", repr(e), f"{e}" — python3 3.12.13 (see ReprOf), plus one
            // shape cell per mapping whose repr has a second axis.
            string reprBody = c + Empty(mapping) +
                "    print(repr(m))\n    print(f\"{m}\")\n    print(repr(e))\n    print(f\"{e}\")";
            string reprExpected = full + "\n" + full + "\n" + empty + "\n" + empty;
            if (mapping == "ChainMap")
            {
                // Two maps: one dict repr per map, in maps order —
                // python3: ChainMap({'a': 1, 'b': 2, 'c': 3}, {'z': 9}).
                reprBody += "\n    two: collections.ChainMap[str, int] = collections.ChainMap[str, int]({\"a\": 1, \"b\": 2, \"c\": 3}, {\"z\": 9})\n    print(repr(two))";
                reprExpected += "\nChainMap({'a': 1, 'b': 2, 'c': 3}, {'z': 9})";
            }
            else if (mapping == "Counter")
            {
                // Ties keep first-seen order (CPython's stable most_common sort), NOT key order (#1979) —
                // python3: Counter(["c", "b", "a", "b"]) -> Counter({'b': 2, 'c': 1, 'a': 1}).
                reprBody += "\n    tie: collections.Counter[str] = collections.Counter[str]([\"c\", \"b\", \"a\", \"b\"])\n    print(repr(tie))";
                reprExpected += "\nCounter({'b': 2, 'c': 1, 'a': 1})";
            }
            cells.Add(new Cell(mapping, "repr", reprBody, reprExpected));
        }

        // construct from str — Counter only; python3: Counter("abca") -> a:2,b:1,c:1.
        cells.Add(new Cell("Counter", "from_str",
            "    cs: collections.Counter[str] = collections.Counter[str](\"abca\")\n" +
            "    csp: list[str] = []\n    for k, v in cs.items():\n        csp.append(k + \"=\" + str(v))\n    print(\",\".join(csp))",
            "a=2,b=1,c=1"));

        return cells;
    }

    private static readonly IReadOnlyList<Cell> AllCells = BuildCells();

    // N/A roster: the four non-Counter mappings have no str constructor. python3: dict("ab") and the
    // sibling mappings raise (a str is not a mapping/pairs source); Counter is the sole mapping that
    // counts a str's code units. This is a roster entry with a reason, not a silent omission (#1933).
    private static readonly (string mapping, string protocol, string reason)[] NotApplicable =
    {
        ("dict", "from_str", "dict(str) is not a construction — python3 dict('ab') raises ValueError"),
        ("defaultdict", "from_str", "defaultdict has no str constructor — only Counter counts code units"),
        ("OrderedDict", "from_str", "OrderedDict has no str constructor — only Counter counts code units"),
        ("ChainMap", "from_str", "ChainMap has no str constructor — only Counter counts code units"),
    };

    public static TheoryData<string> CellIds()
    {
        var data = new TheoryData<string>();
        foreach (var cell in AllCells)
        {
            data.Add(cell.Id);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(CellIds))]
    public void MappingProtocolCell_MatchesPython(string cellId)
    {
        var cell = AllCells.Single(x => x.Id == cellId);

        var result = CompileAndExecute(cell.Program);

        Assert.True(result.Success,
            $"[{cell.Id}] did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}\n--- program ---\n{cell.Program}");
        Assert.Equal(cell.Expected, result.StandardOutput.TrimEnd('\n', '\r'));
    }

    // ── Cross-stdlib equality (Design Decision 5): all six pairs are content-equal in python3
    //    3.12.13. Each operator is declared once (on either type — the checker unions both sides).
    public static TheoryData<string, string> CrossPairs()
    {
        var data = new TheoryData<string, string>();

        // dd == od — python3: True
        data.Add("dd_eq_od",
            "    dd: collections.DefaultDict[str, int] = collections.DefaultDict[str, int](lambda: 0)\n" +
            "    dd[\"a\"] = 1\n    dd[\"b\"] = 2\n" +
            "    od: collections.OrderedDict[str, int] = collections.OrderedDict[str, int]([(\"a\", 1), (\"b\", 2)])\n" +
            "    print(dd == od)");

        // dd == cm — python3: True
        data.Add("dd_eq_cm",
            "    dd: collections.DefaultDict[str, int] = collections.DefaultDict[str, int](lambda: 0)\n" +
            "    dd[\"a\"] = 1\n    dd[\"b\"] = 2\n" +
            "    cm: collections.ChainMap[str, int] = collections.ChainMap[str, int]({\"a\": 1, \"b\": 2})\n" +
            "    print(dd == cm)");

        // od == cm — python3: True
        data.Add("od_eq_cm",
            "    od: collections.OrderedDict[str, int] = collections.OrderedDict[str, int]([(\"a\", 1), (\"b\", 2)])\n" +
            "    cm: collections.ChainMap[str, int] = collections.ChainMap[str, int]({\"a\": 1, \"b\": 2})\n" +
            "    print(od == cm)");

        // c == dd — python3: True
        data.Add("c_eq_dd",
            "    c: collections.Counter[str] = collections.Counter[str]([\"a\", \"b\", \"b\"])\n" +
            "    dd: collections.DefaultDict[str, int] = collections.DefaultDict[str, int](lambda: 0)\n" +
            "    dd[\"a\"] = 1\n    dd[\"b\"] = 2\n" +
            "    print(c == dd)");

        // c == od — python3: True
        data.Add("c_eq_od",
            "    c: collections.Counter[str] = collections.Counter[str]([\"a\", \"b\", \"b\"])\n" +
            "    od: collections.OrderedDict[str, int] = collections.OrderedDict[str, int]([(\"a\", 1), (\"b\", 2)])\n" +
            "    print(c == od)");

        // c == cm — python3: True
        data.Add("c_eq_cm",
            "    c: collections.Counter[str] = collections.Counter[str]([\"a\", \"b\", \"b\"])\n" +
            "    cm: collections.ChainMap[str, int] = collections.ChainMap[str, int]({\"a\": 1, \"b\": 2})\n" +
            "    print(c == cm)");

        return data;
    }

    [Theory]
    [MemberData(nameof(CrossPairs))]
    public void CrossStdlibEquality_MatchesPython(string label, string body)
    {
        var program = "import collections\n\ndef main() -> None:\n" + body;

        var result = CompileAndExecute(program);

        Assert.True(result.Success,
            $"[{label}] did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}\n--- program ---\n{program}");
        Assert.Equal("True", result.StandardOutput.TrimEnd('\n', '\r'));
    }

    [Fact]
    public void Matrix_IsTotal_EveryMappingTimesEveryProtocolIsACellOrRosteredNA()
    {
        // Anchor to the literal rosters, not to the cells themselves: for every mapping × protocol
        // there must be either an executing cell or an explicit N/A entry with a reason.
        var executing = new HashSet<string>(AllCells.Select(x => x.Id));
        var na = new HashSet<string>(NotApplicable.Select(x => x.mapping + "/" + x.protocol));

        var missing = new System.Collections.Generic.List<string>();
        foreach (var mapping in Mappings)
        {
            foreach (var protocol in Protocols)
            {
                var id = mapping + "/" + protocol;
                if (!executing.Contains(id) && !na.Contains(id))
                {
                    missing.Add(id);
                }
            }
        }

        Assert.True(missing.Count == 0,
            "Matrix is not total — no cell and no N/A roster entry for: " + string.Join(", ", missing));

        // Exactly 5 mappings × 13 protocols = 65 (mapping, protocol) positions.
        Assert.Equal(65, Mappings.Length * Protocols.Length);
        // 61 executing cells (65 positions − 4 N/A from_str rows).
        Assert.Equal(61, executing.Count);
        Assert.Equal(4, na.Count);
    }
}
