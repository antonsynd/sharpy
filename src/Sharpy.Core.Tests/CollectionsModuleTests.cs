using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class CollectionsModule_Tests
{
    // --- Deque ---

    [Fact]
    public void Deque_AppendAndPop_WorksLikeStack()
    {
        var deque = new Sharpy.Deque<int>();
        deque.Append(1);
        deque.Append(2);
        deque.Append(3);

        deque.Pop().Should().Be(3);
        deque.Pop().Should().Be(2);
        deque.Pop().Should().Be(1);
    }

    [Fact]
    public void Deque_AppendleftAndPopleft_WorksLikeQueue()
    {
        var deque = new Sharpy.Deque<int>();
        deque.Appendleft(1);
        deque.Appendleft(2);
        deque.Appendleft(3);

        deque.Popleft().Should().Be(3);
        deque.Popleft().Should().Be(2);
        deque.Popleft().Should().Be(1);
    }

    [Fact]
    public void Deque_AppendAndPopleft_WorksAsFIFO()
    {
        var deque = new Sharpy.Deque<int>();
        deque.Append(1);
        deque.Append(2);
        deque.Append(3);

        deque.Popleft().Should().Be(1);
        deque.Popleft().Should().Be(2);
        deque.Popleft().Should().Be(3);
    }

    [Fact]
    public void Deque_PopEmpty_ThrowsIndexError()
    {
        var deque = new Sharpy.Deque<int>();

        FluentActions.Invoking(() => deque.Pop())
            .Should().Throw<Sharpy.IndexError>();
    }

    [Fact]
    public void Deque_PopleftEmpty_ThrowsIndexError()
    {
        var deque = new Sharpy.Deque<int>();

        FluentActions.Invoking(() => deque.Popleft())
            .Should().Throw<Sharpy.IndexError>();
    }

    [Fact]
    public void Deque_Count_ReflectsSize()
    {
        var deque = new Sharpy.Deque<int>();

        deque.Count.Should().Be(0);

        deque.Append(1);
        deque.Append(2);
        deque.Count.Should().Be(2);

        deque.Pop();
        deque.Count.Should().Be(1);
    }

    [Fact]
    public void Deque_ConstructorWithIterable_InitializesFromSequence()
    {
        var deque = new Sharpy.Deque<int>(new[] { 1, 2, 3 });

        deque.Count.Should().Be(3);
        deque.Popleft().Should().Be(1);
    }

    [Fact]
    public void Deque_Clear_RemovesAllElements()
    {
        var deque = new Sharpy.Deque<int>(new[] { 1, 2, 3 });

        deque.Clear();

        deque.Count.Should().Be(0);
    }

    [Fact]
    public void Deque_Extend_AddsFromRight()
    {
        var deque = new Sharpy.Deque<int>(new[] { 1 });

        deque.Extend(new[] { 2, 3, 4 });

        deque.Count.Should().Be(4);
        deque.Pop().Should().Be(4);
    }

    [Fact]
    public void Deque_Extendleft_AddsFromLeft()
    {
        var deque = new Sharpy.Deque<int>(new[] { 4 });

        deque.Extendleft(new[] { 1, 2, 3 });

        deque.Count.Should().Be(4);
        // Extendleft reverses order: 3 is added first (leftmost), then 2, then 1
        deque.Popleft().Should().Be(3);
    }

    [Fact]
    public void Deque_Enumeration_IteratesInOrder()
    {
        var deque = new Sharpy.Deque<int>(new[] { 10, 20, 30 });

        var items = new List<int>();
        foreach (var item in deque)
        {
            items.Add(item);
        }

        items.Should().Equal(10, 20, 30);
    }

    // --- Counter ---

    [Fact]
    public void Counter_CountsOccurrences()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "b", "a", "c", "a", "b" });

        counter["a"].Should().Be(3);
        counter["b"].Should().Be(2);
        counter["c"].Should().Be(1);
    }

    [Fact]
    public void Counter_MissingKey_ReturnsZero()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a" });

        counter["nonexistent"].Should().Be(0);
    }

    [Fact]
    public void Counter_MostCommon_ReturnsOrderedByCount()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "b", "a", "c", "a", "b" });

        var mostCommon = counter.MostCommon(2);

        mostCommon.Should().HaveCount(2);
        mostCommon[0].Item1.Should().Be("a");
        mostCommon[0].Item2.Should().Be(3);
        mostCommon[1].Item1.Should().Be("b");
        mostCommon[1].Item2.Should().Be(2);
    }

    [Fact]
    public void Counter_MostCommon_NoLimit_ReturnsAll()
    {
        var counter = new Sharpy.Counter<string>(new[] { "x", "y", "x" });

        var all = counter.MostCommon();

        all.Should().HaveCount(2);
    }

    [Fact]
    public void Counter_Elements_RepeatsEachElement()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "b", "a" });

        var elements = counter.Elements().ToList();

        elements.Should().Contain("a");
        elements.Should().Contain("b");
        elements.Count(e => e == "a").Should().Be(2);
        elements.Count(e => e == "b").Should().Be(1);
    }

    [Fact]
    public void Counter_Update_AddsCounts()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a" });

        counter.Update(new[] { "a", "b" });

        counter["a"].Should().Be(2);
        counter["b"].Should().Be(1);
    }

    [Fact]
    public void Counter_Indexer_Set_OverridesCount()
    {
        var counter = new Sharpy.Counter<string>();

        counter["x"] = 10;

        counter["x"].Should().Be(10);
    }

    [Fact]
    public void Counter_EmptyConstructor_StartsEmpty()
    {
        var counter = new Sharpy.Counter<int>();

        counter[42].Should().Be(0);
    }

    [Fact]
    public void Counter_Subtract_Iterable_SubtractsCounts()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "a", "b" });

        counter.Subtract(new[] { "a" });

        counter["a"].Should().Be(1);
        counter["b"].Should().Be(1);
    }

    [Fact]
    public void Counter_Subtract_Counter_SubtractsCounts()
    {
        var c1 = new Sharpy.Counter<string>(new[] { "a", "a", "b" });
        var c2 = new Sharpy.Counter<string>(new[] { "a", "b", "b" });

        c1.Subtract(c2);

        c1["a"].Should().Be(1);
        c1["b"].Should().Be(-1);
    }

    [Fact]
    public void Counter_Subtract_MissingKeys_GoesNegative()
    {
        var counter = new Sharpy.Counter<string>();

        counter.Subtract(new[] { "a" });

        counter["a"].Should().Be(-1);
    }

    [Fact]
    public void Counter_Copy_ReturnsIndependentCopy()
    {
        var original = new Sharpy.Counter<string>(new[] { "a", "a", "b" });

        var copy = original.Copy();
        copy["a"] = 0;

        original["a"].Should().Be(2);
        copy["a"].Should().Be(0);
    }

    [Fact]
    public void Counter_Total_ReturnsSumOfCounts()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "a", "b" });

        counter.Total().Should().Be(3);
    }

    [Fact]
    public void Counter_Total_EmptyCounter_ReturnsZero()
    {
        var counter = new Sharpy.Counter<string>();

        counter.Total().Should().Be(0);
    }

    [Fact]
    public void Counter_Clear_RemovesAllElements()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "b" });

        counter.Clear();

        counter["a"].Should().Be(0);
        counter.Total().Should().Be(0);
    }

    [Fact]
    public void Counter_OperatorAdd_CombinesCounts()
    {
        var c1 = new Sharpy.Counter<string>(new[] { "a", "a", "b" });
        var c2 = new Sharpy.Counter<string>(new[] { "b", "c", "c" });

        var result = c1 + c2;

        result["a"].Should().Be(2);
        result["b"].Should().Be(2);
        result["c"].Should().Be(2);
    }

    [Fact]
    public void Counter_OperatorSubtract_DropsZeroAndNegative()
    {
        var c1 = new Sharpy.Counter<string>(new[] { "a", "a", "b" });
        var c2 = new Sharpy.Counter<string>(new[] { "a", "b" });

        var result = c1 - c2;

        result["a"].Should().Be(1);
        result.ContainsKey("b").Should().BeFalse();
    }

    [Fact]
    public void Counter_OperatorOr_TakesMaxCounts()
    {
        var c1 = new Sharpy.Counter<string>(new[] { "a", "a", "b" });
        var c2 = new Sharpy.Counter<string>(new[] { "b", "c", "c" });

        var result = c1 | c2;

        result["a"].Should().Be(2);
        result["b"].Should().Be(1);
        result["c"].Should().Be(2);
    }

    [Fact]
    public void Counter_OperatorAnd_TakesMinCounts()
    {
        var c1 = new Sharpy.Counter<string>(new[] { "a", "a", "b" });
        var c2 = new Sharpy.Counter<string>(new[] { "a", "b", "c" });

        var result = c1 & c2;

        result["a"].Should().Be(1);
        result["b"].Should().Be(1);
        result.ContainsKey("c").Should().BeFalse();
    }

    // --- DefaultDict ---

    [Fact]
    public void DefaultDict_MissingKey_ReturnsDefault()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);

        dd["new_key"].Should().Be(0);
    }

    [Fact]
    public void DefaultDict_MissingKey_CreatesEntry()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);

        _ = dd["key"];

        dd.ContainsKey("key").Should().BeTrue();
    }

    [Fact]
    public void DefaultDict_SetAndGet_WorksNormally()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);

        dd["x"] = 42;

        dd["x"].Should().Be(42);
    }

    [Fact]
    public void DefaultDict_ListFactory_AccumulatesValues()
    {
        var dd = new Sharpy.DefaultDict<string, List<int>>(() => new List<int>());

        dd["items"].Add(1);
        dd["items"].Add(2);

        dd["items"].Should().Equal(1, 2);
    }

    [Fact]
    public void DefaultDict_NullFactory_ThrowsTypeError()
    {
        FluentActions.Invoking(() => new Sharpy.DefaultDict<string, int>(null!))
            .Should().Throw<Sharpy.TypeError>();
    }

    [Fact]
    public void DefaultDict_Get_WithDefault_DoesNotCreateEntry()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);

        dd.Get("absent", 99).Should().Be(99);
        dd.ContainsKey("absent").Should().BeFalse();
    }

    [Fact]
    public void DefaultDict_Keys_ReturnsAllKeys()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["a"] = 1;
        dd["b"] = 2;

        dd.Keys.Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void DefaultDict_Values_ReturnsAllValues()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["a"] = 1;
        dd["b"] = 2;

        dd.Values.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void DefaultDict_DefaultFactory_ReturnsFactory()
    {
        Func<int> factory = () => 42;
        var dd = new Sharpy.DefaultDict<string, int>(factory);

        dd.DefaultFactory.Should().BeSameAs(factory);
    }

    [Fact]
    public void DefaultDict_Copy_PreservesFactoryAndItems()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["a"] = 1;
        dd["b"] = 2;

        var copy = dd.Copy();

        copy["a"].Should().Be(1);
        copy["b"].Should().Be(2);
        copy["c"].Should().Be(0); // Uses factory
        dd.ContainsKey("c").Should().BeFalse(); // Original unaffected
    }

    [Fact]
    public void DefaultDict_Clear_RemovesAllItems()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["a"] = 1;
        dd["b"] = 2;

        dd.Clear();

        dd.Count.Should().Be(0);
        dd.ContainsKey("a").Should().BeFalse();
    }

    [Fact]
    public void DefaultDict_Pop_ExistingKey_ReturnsAndRemoves()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["a"] = 1;

        var value = dd.Pop("a");

        value.Should().Be(1);
        dd.ContainsKey("a").Should().BeFalse();
    }

    [Fact]
    public void DefaultDict_Pop_MissingKey_ThrowsKeyError()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);

        FluentActions.Invoking(() => dd.Pop("missing"))
            .Should().Throw<Sharpy.KeyError>();
    }

    [Fact]
    public void DefaultDict_Pop_WithDefault_ReturnDefault()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);

        dd.Pop("missing", 99).Should().Be(99);
    }

    [Fact]
    public void DefaultDict_Items_ReturnsKeyValueTuples()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["x"] = 10;
        dd["y"] = 20;

        var items = dd.Items();

        items.Should().HaveCount(2);
        items.Should().Contain(("x", 10));
        items.Should().Contain(("y", 20));
    }

    [Fact]
    public void DefaultDict_Update_MergesFromDictionary()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["a"] = 1;

        var other = new Dictionary<string, int> { ["a"] = 99, ["b"] = 2 };
        dd.Update(other);

        dd["a"].Should().Be(99);
        dd["b"].Should().Be(2);
    }

    [Fact]
    public void DefaultDict_SetDefault_ExistingKey_ReturnsExisting()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["a"] = 42;

        dd.SetDefault("a", 99).Should().Be(42);
        dd["a"].Should().Be(42);
    }

    [Fact]
    public void DefaultDict_SetDefault_MissingKey_InsertsAndReturns()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);

        dd.SetDefault("a", 5).Should().Be(5);
        dd["a"].Should().Be(5);
    }

    [Fact]
    public void DefaultDict_Count_ReflectsSize()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);

        dd.Count.Should().Be(0);

        dd["a"] = 1;
        dd["b"] = 2;
        dd.Count.Should().Be(2);
    }

    // --- Module ---

    [Fact]
    public void CollectionsModule_ExposesExpectedTypes()
    {
        Sharpy.Collections.DequeType.Should().Be(typeof(Sharpy.Deque<>));
        Sharpy.Collections.CounterType.Should().Be(typeof(Sharpy.Counter<>));
        Sharpy.Collections.DefaultDictType.Should().Be(typeof(Sharpy.DefaultDict<,>));
    }
}
