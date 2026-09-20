// CS8714: `Dict<K,V>` and `ChainMap<K,V>` both declare `where K : notnull`, which #1818 (R-AO)
// made advisory rather than load-bearing — the null-key slot sits beside the `Dictionary` and is
// selected by the runtime value, not by the constraint. `Sharpy.Core.Tests` suppresses the same
// warning project-wide for the same reason; here one file needs it, so it is scoped to the file.
#pragma warning disable CS8714

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// #1818 (R-AO): <c>ChainMap&lt;K,V&gt;</c> composes <c>Sharpy.Dict&lt;K,V&gt;</c> on every path —
/// the indexer, <c>ContainsKey</c>, <c>Get</c>, <c>Keys()</c>, <c>Count</c>, <c>Pop</c> and
/// <c>Clear</c> all delegate to the underlying dicts — so it inherits the null-key slot rather
/// than needing an arm of its own. These cells are the ADEQUACY check on that inheritance: they
/// fail if any ChainMap seam reaches around the Dict wrapper to a raw collection.
///
/// Each cell carries CPython 3.12's answer for the same program, verified with python3 before
/// these were written.
///
/// One deviation is deliberate and called out on <see cref="NullKey_Keys_YieldsNullOnce"/>:
/// Sharpy's ChainMap enumerates its maps in FORWARD order while CPython merges them reversed, so
/// the relative order of keys drawn from DIFFERENT maps differs. That is independent of null keys
/// (measured: a ChainMap with no None anywhere shows the same difference), so these cells assert
/// the null key's presence, its deduplication and its first-map-wins precedence, not the
/// cross-map order.
/// </summary>
public class ChainMapNullKeyTests
{
    /// <summary>CPython: a = {None: 1, "x": 2}; b = {None: 99, "y": 3}; ChainMap(a, b)</summary>
    private static ChainMap<string?, int> TwoMaps()
    {
        var a = new Dict<string?, int>();
        a[null!] = 1;
        a["x"] = 2;

        var b = new Dict<string?, int>();
        b[null!] = 99;
        b["y"] = 3;

        return new ChainMap<string?, int>(a, b);
    }

    [Fact]
    public void NullKey_Indexer_FirstMapWins()
    {
        // CPython: cm[None] -> 1 (not 99); cm["x"] -> 2; cm["y"] -> 3
        var cm = TwoMaps();

        Assert.Equal(1, cm[null!]);
        Assert.Equal(2, cm["x"]);
        Assert.Equal(3, cm["y"]);
    }

    [Fact]
    public void NullKey_Indexer_LooksThroughToLaterMap()
    {
        // The null key present ONLY in a later map must still be found. This is the cell that
        // fails if the chain walk short-circuits on the first map's miss.
        // CPython: ChainMap({"x": 2}, {None: 99})[None] -> 99
        var first = new Dict<string?, int>();
        first["x"] = 2;

        var second = new Dict<string?, int>();
        second[null!] = 99;

        var cm = new ChainMap<string?, int>(first, second);

        Assert.Equal(99, cm[null!]);
        Assert.True(cm.ContainsKey(null!));
    }

    [Fact]
    public void NullKey_ContainsKey()
    {
        // CPython: None in cm -> True; "x" in cm -> True
        var cm = TwoMaps();

        Assert.True(cm.ContainsKey(null!));
        Assert.True(cm.Contains(null!));
        Assert.True(cm.ContainsKey("x"));
    }

    [Fact]
    public void NullKey_ContainsKey_Absent()
    {
        // CPython: None in ChainMap({"x": 2}) -> False. Absence reports false, it does not throw.
        var only = new Dict<string?, int>();
        only["x"] = 2;

        Assert.False(new ChainMap<string?, int>(only).ContainsKey(null!));
    }

    [Fact]
    public void NullKey_Indexer_Absent_ThrowsKeyError()
    {
        // CPython raises KeyError(None); Sharpy renders the missing key as "None".
        var only = new Dict<string?, int>();
        only["x"] = 2;
        var cm = new ChainMap<string?, int>(only);

        Assert.Throws<KeyError>(() => cm[null!]);
    }

    [Fact]
    public void NullKey_Get_PresentAndAbsent()
    {
        // CPython: cm.get(None, -1) -> 1 (present, first map wins); absent -> -1
        Assert.Equal(1, TwoMaps().Get(null!, -1));

        var only = new Dict<string?, int>();
        only["x"] = 2;
        Assert.Equal(-1, new ChainMap<string?, int>(only).Get(null!, -1));
    }

    [Fact]
    public void NullKey_Keys_YieldsNullOnce()
    {
        // CPython: list(cm.keys()) -> [None, 'y', 'x'] — three unique keys, the null key appearing
        // exactly ONCE even though both maps carry it (the HashSet<K> dedup takes a null element).
        //
        // Sharpy yields [None, 'x', 'y']: the cross-map order differs for the reason given on the
        // class. The null key is drawn from the first map in both, so it leads in both.
        var keys = TwoMaps().Keys().ToList();

        Assert.Equal(3, keys.Count);
        Assert.Equal(1, keys.Count(k => k is null));
        Assert.Contains("x", keys);
        Assert.Contains("y", keys);
        Assert.Null(keys[0]);
    }

    [Fact]
    public void NullKey_Count_DeduplicatesAcrossMaps()
    {
        // CPython: len(cm) -> 3. The null key is in both maps and is counted once.
        Assert.Equal(3, TwoMaps().Count);
    }

    [Fact]
    public void NullKey_Enumeration_PairsNullWithFirstMapValue()
    {
        // CPython: dict(cm)[None] -> 1. GetEnumerator() re-reads each key through the indexer, so
        // this fails if the enumerator yields the null key but the re-read misses the slot.
        var pairs = TwoMaps().ToList();

        Assert.Equal(3, pairs.Count);
        Assert.Equal(1, pairs.Single(p => p.Key is null).Value);
    }

    [Fact]
    public void NullKey_Set_WritesToFirstMapOnly()
    {
        // CPython: cm[None] = 7 leaves a[None] == 7 and b[None] == 99 — the write shadows, it does
        // not reach through to the later map.
        var a = new Dict<string?, int>();
        a[null!] = 1;

        var b = new Dict<string?, int>();
        b[null!] = 99;

        var cm = new ChainMap<string?, int>(a, b);
        cm[null!] = 7;

        Assert.Equal(7, cm[null!]);
        Assert.Equal(7, a[null!]);
        Assert.Equal(99, b[null!]);
    }

    [Fact]
    public void NullKey_Set_InsertsIntoFirstMap_WhenOnlyInLaterMap()
    {
        // The null key exists only downstream; the write must CREATE it in the first map rather
        // than mutate the later one. CPython: first[None] == 7, second[None] == 99.
        var first = new Dict<string?, int>();

        var second = new Dict<string?, int>();
        second[null!] = 99;

        var cm = new ChainMap<string?, int>(first, second);
        cm[null!] = 7;

        Assert.True(first.ContainsKey(null!));
        Assert.Equal(7, first[null!]);
        Assert.Equal(99, second[null!]);
        Assert.Equal(7, cm[null!]);
    }

    [Fact]
    public void NullKey_Pop_RemovesFromFirstMap_ThenLaterMapShowsThrough()
    {
        // CPython: cm.pop(None) -> 1, then None is still in cm and reads 99 from the later map.
        var cm = TwoMaps();

        Assert.Equal(1, cm.Pop(null!));
        Assert.True(cm.ContainsKey(null!));
        Assert.Equal(99, cm[null!]);
    }

    [Fact]
    public void NullKey_Clear_ClearsFirstMapOnly()
    {
        // CPython: cm.clear() empties maps[0]; the later map's None survives and shows through.
        var cm = TwoMaps();
        cm.Clear();

        Assert.True(cm.ContainsKey(null!));
        Assert.Equal(99, cm[null!]);
    }

    [Fact]
    public void NullKey_NewChild_ShadowsThroughTheSlot()
    {
        // CPython: cm.new_child({None: 5})[None] -> 5, and the parent keeps its own value.
        var cm = TwoMaps();

        var overlay = new Dict<string?, int>();
        overlay[null!] = 5;

        Assert.Equal(5, cm.NewChild(overlay)[null!]);
        Assert.Equal(1, cm[null!]);
    }

    [Fact]
    public void NullKey_Parents_DropsTheFirstMapsNullEntry()
    {
        // CPython: cm.parents[None] -> 99 — the first map, and its None entry, is dropped.
        Assert.Equal(99, TwoMaps().Parents[null!]);
    }

    [Fact]
    public void StructNullKey_NullableIntBehavesTheSame()
    {
        // The struct arm: K closes to Nullable<int>, which IS null when valueless, so the same
        // `key is null` guard serves it. CPython: ChainMap({None: "z", 1: "a"})[None] -> "z".
        var a = new Dict<int?, string>();
        a[null] = "z";
        a[1] = "a";

        var cm = new ChainMap<int?, string>(a);

        Assert.Equal("z", cm[null]);
        Assert.True(cm.ContainsKey(null));
        Assert.Equal(2, cm.Count);
    }

    [Fact]
    public void NonNullKeysOnly_IsThePositiveControl()
    {
        // Positive control for the absence assertions above: a ChainMap with NO null key anywhere
        // behaves identically on every path these cells touch, and `ContainsKey(null)` is false
        // here for the ordinary reason rather than because the probe cannot see anything.
        // CPython: ChainMap({"n": 1, "x": 2}, {"n": 99, "y": 3}) -> cm["n"] 1, len 3.
        var a = new Dict<string?, int>();
        a["n"] = 1;
        a["x"] = 2;

        var b = new Dict<string?, int>();
        b["n"] = 99;
        b["y"] = 3;

        var cm = new ChainMap<string?, int>(a, b);

        Assert.Equal(1, cm["n"]);
        Assert.True(cm.ContainsKey("n"));
        Assert.False(cm.ContainsKey(null!));
        Assert.Equal(3, cm.Count);
        Assert.Equal(3, cm.Keys().ToList().Count);
    }
}
