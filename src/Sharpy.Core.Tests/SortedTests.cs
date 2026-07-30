using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Sorted_Tests
{
    [Fact]
    public void Sorted_EmptyList_ReturnsEmptyList()
    {
        // Given
        var list = new List<int>();

        // When
        var result = Sorted(list);

        // Then
        result.Should().NotBeNull();
        Len(result).Should().Be(0);
    }

    [Fact]
    public void Sorted_ListOfIntegers_ReturnsSortedList()
    {
        // Given
        List<int> list = [5, 2, 8, 1, 9, 3];

        // When
        var result = Sorted(list);

        // Then
        Len(result).Should().Be(6);
        result[0].Should().Be(1);
        result[1].Should().Be(2);
        result[2].Should().Be(3);
        result[3].Should().Be(5);
        result[4].Should().Be(8);
        result[5].Should().Be(9);
    }

    [Fact]
    public void Sorted_WithReverseTrue_ReturnsSortedDescending()
    {
        // Given
        List<int> list = [5, 2, 8, 1, 9, 3];

        // When
        var result = Sorted(list, reverse: true);

        // Then
        Len(result).Should().Be(6);
        result[0].Should().Be(9);
        result[1].Should().Be(8);
        result[2].Should().Be(5);
        result[3].Should().Be(3);
        result[4].Should().Be(2);
        result[5].Should().Be(1);
    }

    [Fact]
    public void Sorted_WithKeyFunction_SortsByKey()
    {
        // Given
        List<string> list = ["apple", "pie", "a", "longer"];

        // When
        var result = Sorted(list, key: s => s.Length);

        // Then
        Len(result).Should().Be(4);
        result[0].Should().Be("a");
        result[1].Should().Be("pie");
        result[2].Should().Be("apple");
        result[3].Should().Be("longer");
    }

    [Fact]
    public void Sorted_NullIterable_ThrowsTypeError()
    {
        // When/Then
        FluentActions.Invoking(() => Sorted<int>(null!))
            .Should().Throw<TypeError>();
    }

    // #1191 — SortedImpl used an unstable introsort (List.Sort(comparer)), which #1066 had
    // already fixed on the list.sort() path. Introsort only misbehaves above its 16-element
    // insertion-sort cutoff, so every stability case here keeps at least 20 elements.
    private static List<string> TwentyTiedWords() =>
    [
        "bb", "aa", "cc", "dd", "ee", "ff", "gg", "hh", "ii", "jj",
        "kk", "ll", "mm", "nn", "oo", "pp", "qq", "rr", "ss", "tt",
    ];

    [Fact]
    public void Sorted_WithKey_AllTies_PreservesInputOrder()
    {
        // Every word has length 2, so the key ties across all 20 elements.
        // Python: sorted(words, key=len) == words
        var words = TwentyTiedWords();

        var result = Sorted(words, key: s => s.Length);

        result.ToList().Should().Equal(words.ToList());
    }

    [Fact]
    public void Sorted_WithKeyAndReverse_AllTies_PreservesInputOrder()
    {
        // Python's reverse=True keeps equal elements in their ORIGINAL order, so a
        // sort-then-Reverse() would be wrong even over a stable sort.
        // Python: sorted(words, key=len, reverse=True) == words
        var words = TwentyTiedWords();

        var result = Sorted(words, key: s => s.Length, reverse: true);

        result.ToList().Should().Equal(words.ToList());
    }

    // Key = i / 10, so 11..19 and 10 share key 1, and 21..29 and 20 share key 2.
    private static List<int> TwentyKeyedInts() =>
    [
        11, 21, 12, 22, 13, 23, 14, 24, 15, 25,
        16, 26, 17, 27, 18, 28, 19, 29, 10, 20,
    ];

    [Fact]
    public void Sorted_WithKey_TiedGroups_KeepsInsertionOrderWithinGroup()
    {
        // Python: sorted(xs, key=lambda i: i // 10)
        var result = Sorted(TwentyKeyedInts(), key: i => i / 10);

        DotNetList<int> expected =
        [
            11, 12, 13, 14, 15, 16, 17, 18, 19, 10,
            21, 22, 23, 24, 25, 26, 27, 28, 29, 20,
        ];
        result.ToList().Should().Equal(expected);
    }

    [Fact]
    public void Sorted_WithKeyAndReverse_TiedGroups_KeepsInsertionOrderWithinGroup()
    {
        // Python: sorted(xs, key=lambda i: i // 10, reverse=True) — groups descend,
        // members keep their original order.
        var result = Sorted(TwentyKeyedInts(), key: i => i / 10, reverse: true);

        DotNetList<int> expected =
        [
            21, 22, 23, 24, 25, 26, 27, 28, 29, 20,
            11, 12, 13, 14, 15, 16, 17, 18, 19, 10,
        ];
        result.ToList().Should().Equal(expected);
    }

    [Fact]
    public void Sorted_AgreesWithListSort_ForKeyedSort()
    {
        // The two surfaces disagreed before #1191: list.sort was stable, sorted was not.
        var viaSorted = Sorted(TwentyKeyedInts(), key: i => i / 10);

        var viaListSort = TwentyKeyedInts();
        viaListSort.Sort((int i) => i / 10);

        viaSorted.ToList().Should().Equal(viaListSort.ToList());
    }

    [Fact]
    public void Sorted_AgreesWithListSort_ForKeyedReverseSort()
    {
        var viaSorted = Sorted(TwentyKeyedInts(), key: i => i / 10, reverse: true);

        var viaListSort = TwentyKeyedInts();
        viaListSort.Sort((int i) => i / 10, true);

        viaSorted.ToList().Should().Equal(viaListSort.ToList());
    }

    [Fact]
    public void Sorted_Keyless_TwentyReferenceElements_SortsCorrectly()
    {
        // Above introsort's 16-element cutoff, so this also exercises the merge path.
        // Python: sorted(words)
        var result = Sorted(TwentyTiedWords());

        DotNetList<string> expected =
        [
            "aa", "bb", "cc", "dd", "ee", "ff", "gg", "hh", "ii", "jj",
            "kk", "ll", "mm", "nn", "oo", "pp", "qq", "rr", "ss", "tt",
        ];
        result.ToList().Should().Equal(expected);
    }

    [Fact]
    public void Sorted_DoesNotMutateInput()
    {
        var words = TwentyKeyedInts();
        var before = words.ToList();

        Sorted(words, key: i => i / 10);

        words.ToList().Should().Equal(before);
    }

    [Fact]
    public void Sorted_SingleElement_ReturnsCopy()
    {
        List<int> list = [7];

        var result = Sorted(list);

        Len(result).Should().Be(1);
        result[0].Should().Be(7);
    }
}
