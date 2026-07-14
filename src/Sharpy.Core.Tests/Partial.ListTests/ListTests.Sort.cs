using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Sort_Empty()
    {
        // If
        List<int> l = [];

        // When
        l.Sort();

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Sort()
    {
        // If
        List<int> l = [7, 3, 1, 1, 5];

        // When
        l.Sort();

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Sort_Reverse()
    {
        // If
        List<int> l = [7, 3, 1, 1, 5];

        // When
        l.Sort(true);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [7, 5, 3, 1, 1];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Sort_Reverse_Empty()
    {
        // If
        List<int> l = [];

        // When
        l.Sort(true);

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Sort_With_Key()
    {
        // If
        List<int> l = [7, 3, 1, 1, 5];

        // This effectively inverts the sort
        var key = (int i) =>
        {
            return 1.0 / i;
        };

        // When
        l.Sort(key);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [7, 5, 3, 1, 1];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Sort_Empty_With_Key()
    {
        // If
        List<int> l = [];

        // This effectively inverts the sort
        var key = (int i) =>
        {
            return 1.0 / i;
        };

        // When
        l.Sort(key);

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Sort_With_Key_And_Reverse()
    {
        // If
        List<int> l = [7, 3, 1, 1, 5];

        // This effectively inverts the sort, but the reverse reverses it again
        var key = (int i) =>
        {
            return 1.0 / i;
        };

        // When
        l.Sort(key, true);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Sort_Empty_With_Key_And_Reverse()
    {
        // If
        List<int> l = [];

        // This effectively inverts the sort
        var key = (int i) =>
        {
            return 1.0 / i;
        };

        // When
        l.Sort(key, true);

        // Then
        Len(l).Should().Be(0);
    }

    /// <summary>
    /// Reference-type element that compares only on <see cref="Key"/> but is
    /// distinguishable by <see cref="Tag"/>, so that sort stability is
    /// observable.
    /// </summary>
    private sealed class KeyedItem : System.IComparable<KeyedItem>
    {
        public KeyedItem(int key, string tag)
        {
            Key = key;
            Tag = tag;
        }

        public int Key { get; }

        public string Tag { get; }

        public int CompareTo(KeyedItem? other)
        {
            return Key.CompareTo(other!.Key);
        }
    }

    private static string TagOrder(List<KeyedItem> list)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var item in list.ToList())
        {
            if (sb.Length > 0)
            {
                sb.Append(',');
            }

            sb.Append(item.Tag);
        }

        return sb.ToString();
    }

    [Fact]
    public void List_Sort_Is_Stable_For_Reference_Types()
    {
        // If — two elements share each key; distinct Tags record insertion order.
        List<KeyedItem> l =
        [
            new KeyedItem(1, "a"),
            new KeyedItem(0, "b"),
            new KeyedItem(1, "c"),
            new KeyedItem(0, "d"),
            new KeyedItem(1, "e"),
        ];

        // When
        l.Sort();

        // Then — keys ascending; within equal keys, original order preserved.
        // Matches Python: sorted(..., key=lambda t: t[0]) -> b,d,a,c,e
        TagOrder(l).Should().Be("b,d,a,c,e");
    }

    [Fact]
    public void List_Sort_Reverse_Is_Stable_And_Preserves_Original_Order_Among_Equals()
    {
        // If
        List<KeyedItem> l =
        [
            new KeyedItem(1, "a"),
            new KeyedItem(0, "b"),
            new KeyedItem(1, "c"),
            new KeyedItem(0, "d"),
            new KeyedItem(1, "e"),
        ];

        // When
        l.Sort(true);

        // Then — keys descending, but equal keys keep their ORIGINAL order
        // (this is Python's reverse=True semantics, NOT sort-then-reverse).
        // Matches Python: sorted(..., key=..., reverse=True) -> a,c,e,b,d
        TagOrder(l).Should().Be("a,c,e,b,d");
    }

    [Fact]
    public void List_Sort_Reference_Type_Strings()
    {
        // If — strings are a reference type, so they take the stable path.
        List<string> l = ["banana", "apple", "cherry", "apple"];

        // When
        l.Sort();

        // Then
        var actual = l.ToList();
        DotNetList<string> expected = ["apple", "apple", "banana", "cherry"];
        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Sort_Throws_When_Comparing_Against_None()
    {
        // If — a nullable reference element list containing null.
        List<string?> l = ["b", null, "a"];

        // When / Then — matches Python: comparing None to a value raises.
        var act = () => l.Sort();
        act.Should().Throw<TypeError>();
    }

    [Fact]
    public void List_Sort_With_Key_Is_Stable_For_Value_Types()
    {
        // #1066 — the key projection makes equal-key ordering observable even for
        // value-type elements, so keyed sort must be stable unconditionally.
        // Key = i / 10, so 11/12/13 share key 1 and 21/22 share key 2.
        List<int> l = [11, 21, 12, 22, 13];

        // When
        l.Sort((int i) => i / 10);

        // Then — matches Python sorted(..., key=lambda i: i // 10)
        var actual = l.ToList();
        DotNetList<int> expected = [11, 12, 13, 21, 22];
        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Sort_With_Key_Reverse_Is_Stable_For_Value_Types()
    {
        // #1066 — reverse=True keeps equal-key elements in ORIGINAL order (not
        // sort-then-reverse), observable via value-type elements grouped by key.
        List<int> l = [11, 21, 12, 22, 13];

        // When
        l.Sort((int i) => i / 10, true);

        // Then — matches Python sorted(..., key=..., reverse=True)
        var actual = l.ToList();
        DotNetList<int> expected = [21, 22, 11, 12, 13];
        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Sort_With_Key_Is_Stable_For_Reference_Types()
    {
        // #1066 — equal-key elements keep insertion order under a key projection.
        List<KeyedItem> l =
        [
            new KeyedItem(1, "a"),
            new KeyedItem(0, "b"),
            new KeyedItem(1, "c"),
            new KeyedItem(0, "d"),
            new KeyedItem(1, "e"),
        ];

        // When
        l.Sort((KeyedItem item) => item.Key);

        // Then — keys ascending; within equal keys, original order preserved.
        TagOrder(l).Should().Be("b,d,a,c,e");
    }

    [Fact]
    public void List_Sort_With_Key_Reverse_Is_Stable_For_Reference_Types()
    {
        // #1066 — reverse keeps equal-key elements in original order.
        List<KeyedItem> l =
        [
            new KeyedItem(1, "a"),
            new KeyedItem(0, "b"),
            new KeyedItem(1, "c"),
            new KeyedItem(0, "d"),
            new KeyedItem(1, "e"),
        ];

        // When
        l.Sort((KeyedItem item) => item.Key, true);

        // Then — keys descending, equal keys keep ORIGINAL order.
        TagOrder(l).Should().Be("a,c,e,b,d");
    }
}
