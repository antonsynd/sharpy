using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CsCheck;
using Sharpy;
using Xunit;

namespace Sharpy.Core.Tests.Properties;

/// <summary>
/// The contract of #2440: a <c>dict</c>'s entries enumerate in insertion order on every route. A re-store
/// keeps a key's position, a remove followed by a reinsert moves it to the end, and the None key is an
/// ordinary key with its own position. Random write sequences run against a Sharpy dict and an ordered
/// reference model, and every read route is compared after every step. The write routes and the read
/// routes are the two axes of the class; the literal cells below pin the model itself to python3.
/// </summary>
[Trait("Category", "Property")]
public class DictInsertionOrderPropertyTests
{
    /// <summary>The ordered reference: a plain list of pairs, O(n) everywhere, nothing to go wrong.</summary>
    private sealed class OrderedModel<K>
    {
        public System.Collections.Generic.List<KeyValuePair<K, int>> Pairs { get; private set; } = new();

        private int Find(K key) => Pairs.FindIndex(p => EqualityComparer<K>.Default.Equals(p.Key, key));

        public bool Contains(K key) => Find(key) >= 0;

        public int Value(K key) => Pairs[Find(key)].Value;

        public void Set(K key, int value)
        {
            int i = Find(key);
            if (i >= 0)
            {
                Pairs[i] = new KeyValuePair<K, int>(key, value);
            }
            else
            {
                Pairs.Add(new KeyValuePair<K, int>(key, value));
            }
        }

        public bool Remove(K key)
        {
            int i = Find(key);
            if (i < 0)
            {
                return false;
            }

            Pairs.RemoveAt(i);
            return true;
        }

        public KeyValuePair<K, int> PopAt(int i)
        {
            var pair = Pairs[i];
            Pairs.RemoveAt(i);
            return pair;
        }

        public void Clear() => Pairs.Clear();
    }

    private static void Same<T>(string route, IEnumerable<T> actual, IEnumerable<T> expected, Func<string> context)
    {
        var a = actual.ToList();
        var e = expected.ToList();
        if (!a.SequenceEqual(e))
        {
            throw new Exception(
                $"{route}: [{string.Join(", ", a)}] but the model has [{string.Join(", ", e)}] after {context()}");
        }
    }

    /// <summary>Every read route, against the model.</summary>
    private static void CheckAllRoutes<K>(Dict<K, int> d, OrderedModel<K> model, K[] universe, Func<string> context)
        where K : notnull
    {
        var keys = model.Pairs.Select(p => p.Key).ToList();
        var values = model.Pairs.Select(p => p.Value).ToList();
        var items = model.Pairs.Select(p => (p.Key, p.Value)).ToList();

        var viaForeach = new System.Collections.Generic.List<K>();
        foreach (var key in d)
        {
            viaForeach.Add(key);
        }

        Same("foreach (KeyEnumerator)", viaForeach, keys, context);
        Same("keys() view", d.Keys(), keys, context);
        Same("values() view", d.Values(), values, context);
        Same("items() view", d.Items(), items, context);

        var pairs = (IEnumerable<KeyValuePair<K, int>>)d;
        Same("IEnumerable<KeyValuePair>", pairs.Select(p => (p.Key, p.Value)), items, context);

        var readOnly = (IReadOnlyDictionary<K, int>)d;
        Same("IReadOnlyDictionary.Keys", readOnly.Keys, keys, context);
        Same("IReadOnlyDictionary.Values", readOnly.Values, values, context);

        var generic = (IDictionary<K, int>)d;
        Same("IDictionary<K,V>.Keys", generic.Keys, keys, context);
        Same("IDictionary<K,V>.Values", generic.Values, values, context);

        var copied = new KeyValuePair<K, int>[d.Count];
        generic.CopyTo(copied, 0);
        Same("ICollection<KeyValuePair>.CopyTo", copied.Select(p => (p.Key, p.Value)), items, context);

        var untyped = (IDict)d;
        Same("IDict.Items", untyped.Items().Select(t => ((K)t.Item1!, (int)t.Item2!)), items, context);
        Same("IDict.Keys", untyped.Keys().Select(k => (K)k!), keys, context);
        Same("IDict.Values", untyped.Values().Select(v => (int)v!), values, context);

        var nonGeneric = (IDictionary)d;
        var viaEntries = new System.Collections.Generic.List<(K, int)>();
        var entries = nonGeneric.GetEnumerator();
        while (entries.MoveNext())
        {
            viaEntries.Add(((K)entries.Key, (int)entries.Value!));
        }

        Same("IDictionaryEnumerator", viaEntries, items, context);
        Same("IDictionary.Keys", nonGeneric.Keys.Cast<K>(), keys, context);
        Same("IDictionary.Values", nonGeneric.Values.Cast<int>(), values, context);

        var entryArray = new DictionaryEntry[d.Count];
        ((ICollection)d).CopyTo(entryArray, 0);
        Same("ICollection.CopyTo", entryArray.Select(e => ((K)e.Key, (int)e.Value!)), items, context);

        Same("copy()", d.Copy().Items(), items, context);
        Same("new Dict(mapping)", new Dict<K, int>((IReadOnlyDictionary<K, int>)d).Items(), items, context);
        Same("deepcopy", ((Dict<K, int>)CopyModule.Deepcopy(d)).Items(), items, context);

        string repr = "{" + string.Join(", ", model.Pairs.Select(p => $"{Builtins.Repr(p.Key)}: {Builtins.Repr(p.Value)}")) + "}";
        Same("repr", new[] { d.ToString() }, new[] { repr }, context);

        if (d.Count != model.Pairs.Count)
        {
            throw new Exception($"Count: {d.Count} but the model has {model.Pairs.Count} after {context()}");
        }

        // A moved slot must still be reachable by its key: the index has to follow every compaction.
        foreach (var key in universe)
        {
            bool present = model.Contains(key);
            if (d.Contains(key) != present || d.ContainsKey(key) != present)
            {
                throw new Exception($"Contains({key}): expected {present} after {context()}");
            }

            int expected = present ? model.Value(key) : -1;
            if (d.Get(key, -1) != expected)
            {
                throw new Exception($"Get({key}): {d.Get(key, -1)} but expected {expected} after {context()}");
            }

            if (d.TryGetValue(key, out int found) != present || (present && (found != expected || d[key] != expected)))
            {
                throw new Exception($"lookup({key}): disagrees with {expected} after {context()}");
            }
        }
    }

    /// <summary>One write. <paramref name="d"/> is replaced by the routes that build a new dict.</summary>
    private static void Apply<K>(ref Dict<K, int> d, OrderedModel<K> model, int roll, K key, K otherKey, int value)
        where K : notnull
    {
        switch (roll)
        {
            case < 28: // d[k] = v
                d[key] = value;
                model.Set(key, value);
                break;
            case < 44: // d.pop(k)
                if (model.Contains(key))
                {
                    int popped = d.Pop(key);
                    int expected = model.Value(key);
                    model.Remove(key);
                    if (popped != expected)
                    {
                        throw new Exception($"pop({key}) returned {popped}, expected {expected}");
                    }
                }

                break;
            case < 50: // d.pop(k, default)
                if (d.Pop(key, -1) != (model.Contains(key) ? model.Value(key) : -1))
                {
                    throw new Exception($"pop({key}, -1) returned the wrong value");
                }

                model.Remove(key);
                break;
            case < 60: // d.popitem()
                if (model.Pairs.Count > 0)
                {
                    var (k, v) = d.PopItem();
                    var last = model.PopAt(model.Pairs.Count - 1);
                    if (!EqualityComparer<K>.Default.Equals(k, last.Key) || v != last.Value)
                    {
                        throw new Exception($"popitem() returned ({k}, {v}), expected ({last.Key}, {last.Value})");
                    }
                }

                break;
            case < 65: // d.popitem(last=False)
                if (model.Pairs.Count > 0)
                {
                    var (k, v) = d.PopItem(last: false);
                    var first = model.PopAt(0);
                    if (!EqualityComparer<K>.Default.Equals(k, first.Key) || v != first.Value)
                    {
                        throw new Exception($"popitem(False) returned ({k}, {v}), expected ({first.Key}, {first.Value})");
                    }
                }

                break;
            case < 70: // d.setdefault(k, v)
                {
                    int expected = model.Contains(key) ? model.Value(key) : value;
                    if (d.SetDefault(key, value) != expected)
                    {
                        throw new Exception($"setdefault({key}, {value}) returned the wrong value");
                    }

                    model.Set(key, expected);
                    break;
                }

            case < 75: // d.update(dict), with the None key anywhere in the other dict
                {
                    var other = new Dict<K, int>();
                    other[otherKey] = value;
                    other[key] = value + 1;
                    d.Update(other);
                    model.Set(otherKey, value);
                    model.Set(key, value + 1);
                    break;
                }

            case < 78: // d.update(pairs)
                d.Update(new[] { (key, value), (otherKey, value + 1) });
                model.Set(key, value);
                model.Set(otherKey, value + 1);
                break;
            case < 81: // d.update(d): a self-update changes nothing
                d.Update(d);
                break;
            case < 84: // d |= ...: Merge builds a new dict
                {
                    var other = new Dict<K, int>();
                    other[key] = value;
                    d = d | other;
                    model.Set(key, value);
                    break;
                }

            case < 87: // d = d.copy(): the copy keeps working as the dict from here on
                d = d.Copy();
                break;
            case < 90: // the object-keyed store and removal routes
                {
                    var untyped = (IDict)d;
                    untyped[key!] = value;
                    model.Set(key, value);
                    untyped.Pop(otherKey!, -1);
                    model.Remove(otherKey);
                    break;
                }

            case < 93: // the non-generic store and removal routes
                {
                    var nonGeneric = (IDictionary)d;
                    nonGeneric[key!] = value;
                    model.Set(key, value);
                    nonGeneric.Remove(otherKey!);
                    model.Remove(otherKey);
                    break;
                }

            case < 96: // the pair routes of ICollection<KeyValuePair>
                {
                    var pairs = (ICollection<KeyValuePair<K, int>>)d;
                    pairs.Add(new KeyValuePair<K, int>(key, value));
                    model.Set(key, value);
                    if (model.Contains(otherKey) && pairs.Remove(new KeyValuePair<K, int>(otherKey, model.Value(otherKey))))
                    {
                        model.Remove(otherKey);
                    }

                    break;
                }

            case < 99: // d.remove(k)
                if (model.Contains(key))
                {
                    d.Remove(key);
                    model.Remove(key);
                }

                break;
            default: // d.clear()
                d.Clear();
                model.Clear();
                break;
        }
    }

    /// <summary>
    /// Runs random write sequences. The small universes make reinsertion constant; the long sequences over a
    /// larger universe force the tombstones through compaction and growth.
    /// </summary>
    private static void RunSequences<K>(K[] universe, int iter)
        where K : notnull
    {
        Gen.Int[1, 120].SelectMany(n => Gen.Select(
                Gen.Int[0, 99].Array[n],
                Gen.Int[0, universe.Length - 1].Array[n],
                Gen.Int[0, universe.Length - 1].Array[n],
                Gen.Int[0, 1000].Array[n]))
            .Sample((rolls, keys, otherKeys, values) =>
            {
                var d = new Dict<K, int>();
                var model = new OrderedModel<K>();
                var trace = new System.Collections.Generic.List<string>();
                for (int i = 0; i < rolls.Length; i++)
                {
                    K key = universe[keys[i]];
                    K otherKey = universe[otherKeys[i]];
                    trace.Add($"roll {rolls[i]} key {key} other {otherKey} value {values[i]}");
                    Apply(ref d, model, rolls[i], key, otherKey, values[i]);
                    CheckAllRoutes(d, model, universe, () => string.Join("; ", trace));
                }
            }, iter: iter);
    }

    private static string?[] StringUniverse(int size) =>
        new string?[] { null }.Concat(Enumerable.Range(0, size).Select(i => "k" + i)).ToArray();

    private static int?[] NullableIntUniverse(int size) =>
        new int?[] { null }.Concat(Enumerable.Range(0, size).Select(i => (int?)i)).ToArray();

    [Fact]
    public void ReferenceKeys_WithNone_FollowInsertionOrder_ReinsertionHeavy() => RunSequences(StringUniverse(3), 300);

    [Fact]
    public void ReferenceKeys_WithNone_FollowInsertionOrder_ThroughCompaction() => RunSequences(StringUniverse(24), 300);

    [Fact]
    public void NullableValueKeys_WithNone_FollowInsertionOrder() => RunSequences(NullableIntUniverse(10), 200);

    [Fact]
    public void ValueKeys_FollowInsertionOrder() => RunSequences(Enumerable.Range(0, 16).ToArray(), 200);

    // ------------------------------------------------------------------------------------------------
    // The model, pinned to python3. Each expectation below is python3.12's output for the same program.
    // ------------------------------------------------------------------------------------------------

    private static string?[] KeysOf(Dict<string?, int> d) => d.Keys().ToArray();

    [Fact]
    public void Python_RemoveThenInsert_AppendsAtTheEnd()
    {
        var d = new Dict<string?, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } };
        d.Pop("a");
        d["d"] = 4;
        Assert.Equal(new[] { "b", "c", "d" }, KeysOf(d));
    }

    [Fact]
    public void Python_ReinsertingARemovedKey_MovesItToTheEnd()
    {
        var d = new Dict<string?, int> { { "x", 1 }, { "y", 2 } };
        d.Pop("x");
        d["z"] = 3;
        d["x"] = 9;
        Assert.Equal(new (string?, int)[] { ("y", 2), ("z", 3), ("x", 9) }, d.Items().ToArray());
    }

    [Fact]
    public void Python_ReStore_KeepsThePosition()
    {
        var d = new Dict<string?, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } };
        d["a"] = 9;
        Assert.Equal(new (string?, int)[] { ("a", 9), ("b", 2), ("c", 3) }, d.Items().ToArray());
    }

    [Fact]
    public void Python_PopItem_IsInsertionLast_AfterRemovals()
    {
        var d = new Dict<string?, int> { { "a", 1 }, { "b", 2 }, { "c", 3 }, { "d", 4 } };
        d.Pop("d");
        d["e"] = 5;
        d.Pop("b");
        Assert.Equal(("e", 5), d.PopItem());
        Assert.Equal(new[] { "a", "c" }, KeysOf(d));

        d["b"] = 7;
        Assert.Equal(("b", 7), d.PopItem());
        Assert.Equal(new[] { "a", "c" }, KeysOf(d));
    }

    [Fact]
    public void Python_NoneKey_HoldsItsPosition_ThroughEarlierRemovals()
    {
        var d = new Dict<string?, int> { { "a", 1 }, { null!, 2 }, { "b", 3 } };
        d.Pop("a");
        Assert.Equal(new[] { null, "b" }, KeysOf(d));
        d["c"] = 1;
        Assert.Equal(new[] { null, "b", "c" }, KeysOf(d));
        d.Pop(null!);
        d[null!] = 5;
        Assert.Equal(new[] { "b", "c", null }, KeysOf(d));
        Assert.Equal((null, 5), d.PopItem());
        Assert.Equal(new[] { "b", "c" }, KeysOf(d));
    }

    [Fact]
    public void Python_Update_TakesTheOtherDictsOrder_IncludingNone()
    {
        var other = new Dict<string?, int> { { null!, 1 }, { "b", 2 } };
        var d = new Dict<string?, int> { { "a", 0 } };
        d.Update(other);
        Assert.Equal(new[] { "a", null, "b" }, KeysOf(d));
        Assert.Equal("{'a': 0, None: 1, 'b': 2}", d.ToString());
    }

    [Fact]
    public void Python_ValuesAndRepr_FollowTheReinsertion()
    {
        var d = new Dict<string?, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } };
        d.Pop("b");
        d["b"] = 1;
        Assert.Equal(new[] { 1, 3, 1 }, d.Values().ToArray());
        Assert.Equal("{'a': 1, 'c': 3, 'b': 1}", d.ToString());
    }

    [Fact]
    public void Python_CopyAndSetDefaultAndMerge_FollowTheReinsertion()
    {
        var k = new Dict<string?, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } };
        k.Pop("a");
        k["a"] = 1;
        Assert.Equal(new[] { "b", "c", "a" }, KeysOf(k.Copy()));

        var s = new Dict<string?, int> { { "a", 1 } };
        s.SetDefault("b", 2);
        s.Pop("a");
        s.SetDefault("a", 3);
        Assert.Equal(new (string?, int)[] { ("b", 2), ("a", 3) }, s.Items().ToArray());

        var a = new Dict<string?, int> { { "a", 1 }, { "b", 2 } };
        a.Pop("a");
        a["a"] = 1;
        Assert.Equal(new[] { "b", "a", "z" }, KeysOf(a | new Dict<string?, int> { { "b", 5 }, { "z", 1 } }));
    }

    // ------------------------------------------------------------------------------------------------
    // The cost half of the contract.
    // ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Draining by <c>popitem</c> is linear, from either end. An O(n) per-call scan over 200,000 entries takes
    /// on the order of a minute; the linear drain takes milliseconds, so the wide margin cannot flake.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PopItem_DrainsInLinearTime(bool last)
    {
        const int size = 200_000;
        var d = new Dict<int, int>(size);
        for (int i = 0; i < size; i++)
        {
            d[i] = i;
        }

        // Tombstones at the far end are the case a scan from that end pays for.
        for (int i = 0; i < size / 2; i++)
        {
            d.Pop(last ? size - 1 - i : i);
        }

        var clock = Stopwatch.StartNew();
        int expected = last ? (size / 2) - 1 : size / 2;
        while (d.Count > 0)
        {
            var (key, _) = d.PopItem(last);
            Assert.True(key == expected, $"popitem({last}) returned {key}, expected {expected}");
            expected += last ? -1 : 1;
        }

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"draining took {clock.Elapsed}");
    }

    /// <summary>
    /// A dict used as a queue holds a few live entries while tombstones pass through it. Compaction must keep
    /// the entries array bounded, so once warm the workload allocates nothing however long it runs. The keys
    /// are reference types: the gate runs a Debug Core, whose <c>key is null</c> boxes a value-type key.
    /// </summary>
    [Fact]
    public void RemoveAndInsert_SteadyState_DoesNotGrow()
    {
        var ring = Enumerable.Range(0, 64).Select(i => "k" + i).ToArray();
        var d = new Dict<string, int>();
        int next = 0;

        void Churn(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                d[ring[next % ring.Length]] = next;
                if (d.Count > 8)
                {
                    d.Pop(ring[(next - 8) % ring.Length]);
                }

                next++;
            }
        }

        Churn(1_000);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Churn(200_000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 4_096, $"the warm churn allocated {allocated} bytes");
        Assert.Equal(Enumerable.Range(next - 8, 8).Select(i => ring[i % ring.Length]), d.Keys());
    }

    // ------------------------------------------------------------------------------------------------
    // Enumeration during mutation: the same rules the Dictionary backing had.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void Enumeration_NewKey_Invalidates_TheOthersDoNot()
    {
        var inserted = new Dict<string?, int> { { "a", 1 }, { "b", 2 } };
        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var key in inserted)
            {
                inserted["z"] = 1;
            }
        });

        // The None key is a key: arriving during enumeration invalidates like any other.
        var insertedNone = new Dict<string?, int> { { "a", 1 } };
        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var key in insertedNone)
            {
                insertedNone[null!] = 1;
            }
        });

        var overwritten = new Dict<string?, int> { { "a", 1 }, { "b", 2 } };
        var seen = new System.Collections.Generic.List<string?>();
        foreach (var key in overwritten)
        {
            overwritten["a"] = 2;
            seen.Add(key);
        }

        Assert.Equal(new[] { "a", "b" }, seen);

        var removed = new Dict<string?, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } };
        seen.Clear();
        foreach (var key in removed)
        {
            removed.Pop("b", 0);
            seen.Add(key);
        }

        Assert.Equal(new[] { "a", "c" }, seen);

        var trimmed = new Dict<string?, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } };
        seen.Clear();
        foreach (var key in trimmed)
        {
            trimmed.Pop("c", 0);
            trimmed.Pop("b", 0);
            seen.Add(key);
        }

        Assert.Equal(new[] { "a" }, seen);

        var cleared = new Dict<string?, int> { { "a", 1 }, { "b", 2 } };
        seen.Clear();
        foreach (var key in cleared)
        {
            cleared.Clear();
            seen.Add(key);
        }

        Assert.Equal(new[] { "a" }, seen);
    }
}
