using Xunit;
using FluentAssertions;
using System.Linq;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for interoperability between Sharpy iterators/iterables and C# enumerators/enumerables.
/// </summary>
public class IteratorInteropTests
{
    #region C# IEnumerable to Sharpy Iterator Tests

    [Fact]
    public void Iter_CSharpEnumerable_ReturnsIterator()
    {
        // Given
        IEnumerable<int> csharpEnumerable = new[] { 1, 2, 3, 4, 5 };

        // When
        var iterator = Iter(csharpEnumerable);

        // Then
        iterator.Should().NotBeNull();
        iterator.Should().BeAssignableTo<Iterator<int>>();
    }

    [Fact]
    public void Iter_CSharpEnumerable_IteratesCorrectly()
    {
        // Given
        IEnumerable<int> csharpEnumerable = new[] { 1, 2, 3, 4, 5 };
        var iterator = Iter(csharpEnumerable);

        // When/Then
        Next(iterator).Should().Be(1);
        Next(iterator).Should().Be(2);
        Next(iterator).Should().Be(3);
        Next(iterator).Should().Be(4);
        Next(iterator).Should().Be(5);

        // Should throw StopIteration when exhausted
        Assert.Throws<StopIteration>(() => Next(iterator));
    }

    [Fact]
    public void Iter_CSharpList_ReturnsIterator()
    {
        // Given
        var csharpList = new DotNetList<string> { "hello", "world" };

        // When
        var iterator = Iter(csharpList);

        // Then
        Next(iterator).Should().Be("hello");
        Next(iterator).Should().Be("world");
        Assert.Throws<StopIteration>(() => Next(iterator));
    }

    [Fact]
    public void Iter_CSharpLinqQuery_ReturnsIterator()
    {
        // Given
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var evens = numbers.Where(n => n % 2 == 0);

        // When
        var iterator = Iter(evens);

        // Then
        Next(iterator).Should().Be(2);
        Next(iterator).Should().Be(4);
        Assert.Throws<StopIteration>(() => Next(iterator));
    }

    [Fact]
    public void Iter_EmptyCSharpEnumerable_ReturnsEmptyIterator()
    {
        // Given
        IEnumerable<int> empty = Enumerable.Empty<int>();

        // When
        var iterator = Iter(empty);

        // Then
        Assert.Throws<StopIteration>(() => Next(iterator));
    }

    [Fact]
    public void Iter_SharpyIterable_ReturnsSameIterator()
    {
        // Given - Sharpy List is IIterable
        List<int> sharpyList = [1, 2, 3];

        // When - Iter should recognize it's already IIterable
        var iterator = Iter(sharpyList);

        // Then - Should work correctly
        Next(iterator).Should().Be(1);
        Next(iterator).Should().Be(2);
        Next(iterator).Should().Be(3);
        Assert.Throws<StopIteration>(() => Next(iterator));
    }

    #endregion

    #region ToIterator Extension Method Tests

    [Fact]
    public void ToIterator_CSharpEnumerable_ReturnsIterator()
    {
        // Given
        var csharpArray = new[] { "a", "b", "c" };

        // When
        var iterator = csharpArray.ToIterator();

        // Then
        iterator.Should().BeAssignableTo<Iterator<string>>();
        Next(iterator).Should().Be("a");
        Next(iterator).Should().Be("b");
        Next(iterator).Should().Be("c");
        Assert.Throws<StopIteration>(() => Next(iterator));
    }

    [Fact]
    public void ToIterator_EmptyEnumerable_ReturnsEmptyIterator()
    {
        // Given
        var empty = Enumerable.Empty<double>();

        // When
        var iterator = empty.ToIterator();

        // Then
        Assert.Throws<StopIteration>(() => Next(iterator));
    }

    #endregion

    #region AsIterable Extension Method Tests

    [Fact]
    public void AsIterable_CSharpEnumerable_ReturnsIterable()
    {
        // Given
        var csharpArray = new[] { 1, 2, 3 };

        // When
        var iterable = csharpArray.AsIterable();

        // Then
        iterable.Should().BeAssignableTo<Collections.Interfaces.IIterable<int>>();
    }

    [Fact]
    public void AsIterable_CanBeIteratedMultipleTimes()
    {
        // Given
        var csharpArray = new[] { 1, 2, 3 };
        var iterable = csharpArray.AsIterable();

        // When/Then - First iteration
        var iter1 = iterable.__Iter__();
        Next(iter1).Should().Be(1);
        Next(iter1).Should().Be(2);
        Next(iter1).Should().Be(3);
        Assert.Throws<StopIteration>(() => Next(iter1));

        // When/Then - Second iteration
        var iter2 = iterable.__Iter__();
        Next(iter2).Should().Be(1);
        Next(iter2).Should().Be(2);
        Next(iter2).Should().Be(3);
        Assert.Throws<StopIteration>(() => Next(iter2));
    }

    #endregion

    #region Sharpy Iterables in C# foreach Tests

    [Fact]
    public void SharpyList_CanBeUsedInCSharpForeach()
    {
        // Given
        List<int> sharpyList = [10, 20, 30, 40];
        var result = new DotNetList<int>();

        // When
        foreach (var item in sharpyList)
        {
            result.Add(item);
        }

        // Then
        result.Should().Equal(10, 20, 30, 40);
    }

    [Fact]
    public void SharpySet_CanBeUsedInCSharpForeach()
    {
        // Given
        Set<string> sharpySet = new(["apple", "banana", "cherry"]);
        var result = new HashSet<string>();

        // When
        foreach (var item in sharpySet)
        {
            result.Add(item);
        }

        // Then
        result.Should().BeEquivalentTo(new[] { "apple", "banana", "cherry" });
    }

    [Fact]
    public void SharpyDict_CanBeUsedInCSharpForeach()
    {
        // Given
        Dict<string, int> sharpyDict = new()
        {
            ["one"] = 1,
            ["two"] = 2,
            ["three"] = 3
        };
        var result = new DotNetList<string>();

        // When - foreach on dict iterates over keys
        foreach (var key in sharpyDict)
        {
            result.Add(key);
        }

        // Then
        result.Should().BeEquivalentTo(new[] { "one", "two", "three" });
    }

    [Fact]
    public void SharpyIterator_CanBeUsedInCSharpForeach()
    {
        // Given
        List<int> sharpyList = [1, 2, 3, 4, 5];
        var iterator = Iter(sharpyList);
        var result = new DotNetList<int>();

        // When
        foreach (var item in iterator)
        {
            result.Add(item);
        }

        // Then
        result.Should().Equal(1, 2, 3, 4, 5);
    }

    #endregion

    #region LINQ Extensions on Sharpy Iterables Tests

    [Fact]
    public void SharpyIterable_Select_Works()
    {
        // Given
        List<int> sharpyList = [1, 2, 3, 4, 5];

        // When
        var result = sharpyList.Select(x => x * 2).ToList();

        // Then
        result.Should().Equal(2, 4, 6, 8, 10);
    }

    [Fact]
    public void SharpyIterable_Where_Works()
    {
        // Given
        List<int> sharpyList = [1, 2, 3, 4, 5, 6];

        // When
        var result = sharpyList.Where(x => x % 2 == 0).ToList();

        // Then
        result.Should().Equal(2, 4, 6);
    }

    [Fact]
    public void SharpyIterable_SelectAndWhere_Chained()
    {
        // Given
        List<int> sharpyList = [1, 2, 3, 4, 5];

        // When
        var result = sharpyList
            .Where(x => x > 2)
            .Select(x => x * 10)
            .ToList();

        // Then
        result.Should().Equal(30, 40, 50);
    }

    [Fact]
    public void SharpyIterable_First_Works()
    {
        // Given
        List<string> sharpyList = ["apple", "banana", "cherry"];

        // When
        var result = sharpyList.First();

        // Then
        result.Should().Be("apple");
    }

    [Fact]
    public void SharpyIterable_First_WithPredicate_Works()
    {
        // Given
        List<int> sharpyList = [1, 2, 3, 4, 5];

        // When
        var result = sharpyList.First(x => x > 3);

        // Then
        result.Should().Be(4);
    }

    [Fact]
    public void SharpyIterable_Last_Works()
    {
        // Given
        List<string> sharpyList = ["first", "middle", "last"];

        // When
        var result = sharpyList.Last();

        // Then
        result.Should().Be("last");
    }

    [Fact]
    public void SharpyIterable_Any_Works()
    {
        // Given
        List<int> sharpyList = [1, 2, 3, 4, 5];

        // When/Then
        sharpyList.Any().Should().BeTrue();
        sharpyList.Any(x => x > 10).Should().BeFalse();
        sharpyList.Any(x => x == 3).Should().BeTrue();
    }

    [Fact]
    public void SharpyIterable_All_Works()
    {
        // Given
        List<int> sharpyList = [2, 4, 6, 8];

        // When/Then
        sharpyList.All(x => x % 2 == 0).Should().BeTrue();
        sharpyList.All(x => x > 5).Should().BeFalse();
    }

    [Fact]
    public void SharpyIterable_Count_Works()
    {
        // Given
        List<int> sharpyList = [1, 2, 3, 4, 5];

        // When/Then
        sharpyList.Count().Should().Be(5);
        sharpyList.Count(x => x > 3).Should().Be(2);
    }

    [Fact]
    public void SharpyIterable_ToList_Works()
    {
        // Given
        Set<int> sharpySet = new([3, 1, 2]);

        // When
        var result = sharpySet.ToList();

        // Then
        result.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void SharpyIterable_ToArray_Works()
    {
        // Given
        List<string> sharpyList = ["a", "b", "c"];

        // When
        var result = sharpyList.ToArray();

        // Then
        result.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void SharpyIterable_Skip_Works()
    {
        // Given
        List<int> sharpyList = [1, 2, 3, 4, 5];

        // When
        var result = sharpyList.Skip(2).ToList();

        // Then
        result.Should().Equal(3, 4, 5);
    }

    [Fact]
    public void SharpyIterable_Take_Works()
    {
        // Given
        List<int> sharpyList = [1, 2, 3, 4, 5];

        // When
        var result = sharpyList.Take(3).ToList();

        // Then
        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void SharpyIterable_OrderBy_Works()
    {
        // Given
        List<int> sharpyList = [5, 2, 8, 1, 9, 3];

        // When
        var result = sharpyList.OrderBy(x => x).ToList();

        // Then
        result.Should().Equal(1, 2, 3, 5, 8, 9);
    }

    [Fact]
    public void SharpyIterable_OrderByDescending_Works()
    {
        // Given
        List<int> sharpyList = [5, 2, 8, 1, 9, 3];

        // When
        var result = sharpyList.OrderByDescending(x => x).ToList();

        // Then
        result.Should().Equal(9, 8, 5, 3, 2, 1);
    }

    [Fact]
    public void SharpySet_LinqWhere_Works()
    {
        // Given
        Set<int> sharpySet = new([1, 2, 3, 4, 5, 6]);

        // When
        var result = sharpySet.Where(x => x % 2 == 0).ToList();

        // Then
        result.Should().BeEquivalentTo(new[] { 2, 4, 6 });
    }

    [Fact]
    public void SharpyDict_LinqSelect_OnKeys_Works()
    {
        // Given
        Dict<string, int> sharpyDict = new()
        {
            ["one"] = 1,
            ["two"] = 2,
            ["three"] = 3
        };

        // When - Dict iterates over keys
        var result = sharpyDict.Select(key => key.ToUpper()).ToList();

        // Then
        result.Should().BeEquivalentTo(new[] { "ONE", "TWO", "THREE" });
    }

    #endregion

    #region Negative Tests

    [Fact]
    public void First_EmptyIterable_ThrowsInvalidOperationException()
    {
        // Given
        List<int> emptyList = [];

        // When/Then
        Assert.Throws<InvalidOperationException>(() => emptyList.First());
    }

    [Fact]
    public void First_WithPredicate_NoMatch_ThrowsInvalidOperationException()
    {
        // Given
        List<int> sharpyList = [1, 2, 3];

        // When/Then
        Assert.Throws<InvalidOperationException>(() => sharpyList.First(x => x > 10));
    }

    [Fact]
    public void Last_EmptyIterable_ThrowsInvalidOperationException()
    {
        // Given
        List<int> emptyList = [];

        // When/Then
        Assert.Throws<InvalidOperationException>(() => emptyList.Last());
    }

    [Fact]
    public void Iter_Null_ThrowsTypeError()
    {
        // When/Then - Sharpy throws TypeError for null iterables
        Assert.Throws<TypeError>(() => Iter<int>(null!));
    }

    [Fact]
    public void ToIterator_Null_ThrowsTypeError()
    {
        // When/Then - Sharpy throws TypeError for null enumerables
        IEnumerable<int>? nullEnumerable = null;
        Assert.Throws<TypeError>(() => nullEnumerable!.ToIterator());
    }

    [Fact]
    public void AsIterable_Null_ThrowsArgumentNullException()
    {
        // When/Then
        IEnumerable<int>? nullEnumerable = null;
        Assert.Throws<ArgumentNullException>(() => nullEnumerable!.AsIterable());
    }

    [Fact]
    public void Next_ExhaustedIterator_ThrowsStopIteration()
    {
        // Given
        var csharpArray = new[] { 1 };
        var iterator = Iter(csharpArray);

        // Exhaust the iterator
        Next(iterator);

        // When/Then
        Assert.Throws<StopIteration>(() => Next(iterator));
    }

    [Fact]
    public void CSharpEnumerator_CannotBeReused_ThrowsStopIteration()
    {
        // Given
        var csharpArray = new[] { 1, 2, 3 };
        var iterator = Iter(csharpArray);

        // Exhaust the iterator
        while (true)
        {
            try
            {
                Next(iterator);
            }
            catch (StopIteration)
            {
                break;
            }
        }

        // When/Then - Iterator is exhausted and should continue throwing
        Assert.Throws<StopIteration>(() => Next(iterator));
    }

    [Fact]
    public void Skip_NegativeCount_ReturnsAllElements()
    {
        // Given
        List<int> sharpyList = [1, 2, 3];

        // When - LINQ Skip with negative count is treated as Skip(0)
        var result = sharpyList.Skip(-1).ToList();

        // Then
        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Take_NegativeCount_ReturnsEmpty()
    {
        // Given
        List<int> sharpyList = [1, 2, 3];

        // When - LINQ Take with negative count returns empty
        var result = sharpyList.Take(-1).ToList();

        // Then
        result.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Iter_SingleElementEnumerable_Works()
    {
        // Given
        var singleElement = new[] { 42 };

        // When
        var iterator = Iter(singleElement);

        // Then
        Next(iterator).Should().Be(42);
        Assert.Throws<StopIteration>(() => Next(iterator));
    }

    [Fact]
    public void SharpyIterable_WithNullElements_WorksCorrectly()
    {
        // Given
        List<string?> sharpyList = ["a", null, "b", null, "c"];

        // When
        var result = sharpyList.Where(x => x != null).ToList();

        // Then
        result.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void ComplexLinqChain_OnSharpyIterable_Works()
    {
        // Given
        List<int> sharpyList = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        // When
        var result = sharpyList
            .Where(x => x % 2 == 0)  // [2, 4, 6, 8, 10]
            .Select(x => x * x)       // [4, 16, 36, 64, 100]
            .OrderByDescending(x => x) // [100, 64, 36, 16, 4]
            .Skip(1)                  // [64, 36, 16, 4]
            .Take(2)                  // [64, 36]
            .ToList();

        // Then
        result.Should().Equal(64, 36);
    }

    [Fact]
    public void CSharpEnumerable_ToSharpyIterator_ToLinq_Works()
    {
        // Given
        var csharpArray = new[] { 1, 2, 3, 4, 5 };

        // When - Convert to Sharpy iterator, then use as IEnumerable in LINQ
        var iterator = Iter(csharpArray);
        var result = iterator.Where(x => x > 2).ToList();

        // Then
        result.Should().Equal(3, 4, 5);
    }

    [Fact]
    public void SharpyIterable_Count_OnLargeCollection_Works()
    {
        // Given
        var largeList = Enumerable.Range(1, 10000).ToList();
        List<int> sharpyList = new(largeList);

        // When
        var count = sharpyList.Count();

        // Then
        count.Should().Be(10000);
    }

    [Fact]
    public void Skip_MoreThanAvailable_ReturnsEmpty()
    {
        // Given
        List<int> sharpyList = [1, 2, 3];

        // When
        var result = sharpyList.Skip(10).ToList();

        // Then
        result.Should().BeEmpty();
    }

    [Fact]
    public void Take_MoreThanAvailable_ReturnAll()
    {
        // Given
        List<int> sharpyList = [1, 2, 3];

        // When
        var result = sharpyList.Take(10).ToList();

        // Then
        result.Should().Equal(1, 2, 3);
    }

    #endregion
}
