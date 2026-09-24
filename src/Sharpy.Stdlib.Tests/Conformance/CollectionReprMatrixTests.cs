using System.Collections.Generic;
using System.Linq;
using Sharpy.Stdlib.Tests.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// The collection repr matrix (#1995) — the executing column of <see cref="CollectionReprSweepTests"/>.
/// Every cell is a Sharpy program whose expected stdout is a python3 3.12.13-verified literal:
/// types {set, frozenset (control), deque, dict_keys, dict_values, dict_items, and the three
/// <c>defaultdict</c> views} × {non-empty, empty} × routes {<c>str()</c>, <c>print</c>,
/// <c>repr()</c>, f-string, nested in a <c>list[object]</c>}, plus a <c>live</c> route for the six
/// views (the view is taken, the owner mutated, the view printed — a copy would print the old
/// contents).
///
/// <para>
/// <c>str</c> and <c>repr</c> are both <c>ToString()</c> in the dunder table (there is no separate
/// <c>__repr__</c> spelling for Core/Stdlib collections), so for these types the two routes agree —
/// as they do in CPython. Before #1995 an empty set printed <c>{}</c>, a deque printed a bare list
/// and every view printed its elements as a list: the three types had no <c>ToString</c> of their own
/// and fell through to <c>SequenceFormatting.TryFormatPlainClrSequence</c>.
/// </para>
///
/// <para>
/// Every non-empty instance holds the same contents — elements 1, 2 or the mapping
/// {"a": 1, "b": 2} — so one oracle literal per type covers the whole row.
/// </para>
/// </summary>
public class CollectionReprMatrixTests : StdlibIntegrationTestBase
{
    public CollectionReprMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    // ── Totality anchors: literal rosters (NOT derived from the cells), so an omitted type or route
    //    fails the count check rather than passing vacuously.
    private static readonly string[] Types =
    {
        "set", "frozenset", "deque", "dict_keys", "dict_values", "dict_items", "dd_keys", "dd_values", "dd_items",
    };

    private static readonly string[] Routes = { "str", "print", "repr", "fstring", "nested", "live" };

    /// <summary>One executing cell: a full Sharpy program and its python3-verified stdout.</summary>
    private sealed class Cell
    {
        public string Id { get; }
        public string Program { get; }
        public string Expected { get; }

        public Cell(string type, string route, string body, string expected)
        {
            Id = type + "/" + route;
            Program = "import collections\n\ndef main() -> None:\n" + body;
            Expected = expected;
        }
    }

    private const string DictSetup =
        "    d: dict[str, int] = {\"a\": 1, \"b\": 2}\n" +
        "    ed: dict[str, int] = {}\n";

    private const string DefaultDictSetup =
        "    d: collections.DefaultDict[str, int] = collections.DefaultDict[str, int](lambda: 0)\n" +
        "    d[\"a\"] = 1\n    d[\"b\"] = 2\n" +
        "    ed: collections.DefaultDict[str, int] = collections.DefaultDict[str, int](lambda: 0)\n";

    // Binds `x` (non-empty) and `e` (empty) for each type.
    private static string Construct(string type) => type switch
    {
        "set" => "    x: set[int] = {1, 2}\n    e: set[int] = set()\n",
        "frozenset" => "    x: frozenset[int] = frozenset([1, 2])\n    e: frozenset[int] = frozenset[int]()\n",
        "deque" =>
            "    x: collections.Deque[int] = collections.Deque[int]([1, 2])\n" +
            "    e: collections.Deque[int] = collections.Deque[int]()\n",
        "dict_keys" => DictSetup + "    x = d.keys()\n    e = ed.keys()\n",
        "dict_values" => DictSetup + "    x = d.values()\n    e = ed.values()\n",
        "dict_items" => DictSetup + "    x = d.items()\n    e = ed.items()\n",
        "dd_keys" => DefaultDictSetup + "    x = d.keys()\n    e = ed.keys()\n",
        "dd_values" => DefaultDictSetup + "    x = d.values()\n    e = ed.values()\n",
        "dd_items" => DefaultDictSetup + "    x = d.items()\n    e = ed.items()\n",
        _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null),
    };

    // python3 3.12.13: str(x) / str(e) for each type — the defaultdict views print exactly the dict
    // views' text (CPython: defaultdict(int, {...}).keys() -> dict_keys([...])).
    private static (string full, string empty) ReprOf(string type) => type switch
    {
        "set" => ("{1, 2}", "set()"),
        "frozenset" => ("frozenset({1, 2})", "frozenset()"),
        "deque" => ("deque([1, 2])", "deque([])"),
        "dict_keys" or "dd_keys" => ("dict_keys(['a', 'b'])", "dict_keys([])"),
        "dict_values" or "dd_values" => ("dict_values([1, 2])", "dict_values([])"),
        "dict_items" or "dd_items" => ("dict_items([('a', 1), ('b', 2)])", "dict_items([])"),
        _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null),
    };

    // python3 3.12.13: the view taken before `d["c"] = 3` prints the new entry.
    private static string LiveOf(string type) => type switch
    {
        "dict_keys" or "dd_keys" => "dict_keys(['a', 'b', 'c'])",
        "dict_values" or "dd_values" => "dict_values([1, 2, 3])",
        "dict_items" or "dd_items" => "dict_items([('a', 1), ('b', 2), ('c', 3)])",
        _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null),
    };

    private static IReadOnlyList<Cell> BuildCells()
    {
        var cells = new System.Collections.Generic.List<Cell>();

        foreach (var type in Types)
        {
            string c = Construct(type);
            var (full, empty) = ReprOf(type);
            string both = full + "\n" + empty;

            cells.Add(new Cell(type, "str", c + "    print(str(x))\n    print(str(e))", both));
            cells.Add(new Cell(type, "print", c + "    print(x)\n    print(e)", both));
            cells.Add(new Cell(type, "repr", c + "    print(repr(x))\n    print(repr(e))", both));
            cells.Add(new Cell(type, "fstring", c + "    print(f\"{x}\")\n    print(f\"{e}\")", both));
            // python3: [x, e] renders each element with repr -> "[<full>, <empty>]".
            cells.Add(new Cell(type, "nested",
                c + "    xs: list[object] = [x, e]\n    print(xs)",
                "[" + full + ", " + empty + "]"));

            if (type.StartsWith("dict_", System.StringComparison.Ordinal) || type.StartsWith("dd_", System.StringComparison.Ordinal))
            {
                cells.Add(new Cell(type, "live", c + "    d[\"c\"] = 3\n    print(x)", LiveOf(type)));
            }
        }

        return cells;
    }

    private static readonly IReadOnlyList<Cell> AllCells = BuildCells();

    // N/A roster: `live` is a view property; set/frozenset/deque are the values themselves.
    private static readonly (string type, string route, string reason)[] NotApplicable =
    {
        ("set", "live", "a set is not a view onto an owner"),
        ("frozenset", "live", "a frozenset is not a view onto an owner"),
        ("deque", "live", "a deque is not a view onto an owner"),
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
    public void CollectionReprCell_MatchesPython(string cellId)
    {
        var cell = AllCells.Single(x => x.Id == cellId);

        var result = CompileAndExecute(cell.Program);

        Assert.True(result.Success,
            $"[{cell.Id}] did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}\n--- program ---\n{cell.Program}");
        Assert.Equal(cell.Expected, result.StandardOutput.TrimEnd('\n', '\r'));
    }

    [Fact]
    public void Matrix_IsTotal_EveryTypeTimesEveryRouteIsACellOrRosteredNA()
    {
        var executing = new HashSet<string>(AllCells.Select(x => x.Id));
        var na = new HashSet<string>(NotApplicable.Select(x => x.type + "/" + x.route));

        var missing = new System.Collections.Generic.List<string>();
        foreach (var type in Types)
        {
            foreach (var route in Routes)
            {
                var id = type + "/" + route;
                if (!executing.Contains(id) && !na.Contains(id))
                {
                    missing.Add(id);
                }
            }
        }

        Assert.True(missing.Count == 0,
            "Matrix is not total — no cell and no N/A roster entry for: " + string.Join(", ", missing));

        // Exactly 9 types × 6 routes = 54 positions: 51 executing cells + 3 N/A `live` rows.
        Assert.Equal(54, Types.Length * Routes.Length);
        Assert.Equal(51, executing.Count);
        Assert.Equal(3, na.Count);
    }
}
