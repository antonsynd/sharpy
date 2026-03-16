using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class CopyModuleTests
{
    [Fact]
    public void Copy_Null_ReturnsNull()
    {
        object result = CopyModule.Copy(null!);
        result.Should().BeNull();
    }

    [Fact]
    public void Copy_ValueType_ReturnsSameValue()
    {
        object result = CopyModule.Copy(42);
        result.Should().Be(42);
    }

    [Fact]
    public void Copy_String_ReturnsSameReference()
    {
        string original = "hello";
        object result = CopyModule.Copy(original);
        result.Should().BeSameAs(original);
    }

    [Fact]
    public void Copy_List_ReturnsNewListWithSameElements()
    {
        var original = new Sharpy.List<int> { 1, 2, 3 };
        var copy = (Sharpy.List<int>)CopyModule.Copy(original);

        copy.Should().NotBeSameAs(original);
        ((ICollection<int>)copy).Count.Should().Be(3);
        copy[0].Should().Be(1);
        copy[1].Should().Be(2);
        copy[2].Should().Be(3);
    }

    [Fact]
    public void Copy_List_IsShallow()
    {
        var inner = new Sharpy.List<int> { 10, 20 };
        var original = new Sharpy.List<Sharpy.List<int>> { inner };
        var copy = (Sharpy.List<Sharpy.List<int>>)CopyModule.Copy(original);

        copy.Should().NotBeSameAs(original);
        // Inner list should be the same reference (shallow copy)
        copy[0].Should().BeSameAs(inner);
    }

    [Fact]
    public void Copy_Dict_ReturnsNewDictWithSameEntries()
    {
        var original = new Sharpy.Dict<string, int>();
        original.Add("a", 1);
        original.Add("b", 2);

        var copy = (Sharpy.Dict<string, int>)CopyModule.Copy(original);

        copy.Should().NotBeSameAs(original);
        ((ICollection<KeyValuePair<string, int>>)copy).Count.Should().Be(2);
        copy["a"].Should().Be(1);
        copy["b"].Should().Be(2);
    }

    [Fact]
    public void Copy_Set_ReturnsNewSetWithSameElements()
    {
        var original = new Sharpy.Set<int> { 1, 2, 3 };
        var copy = (Sharpy.Set<int>)CopyModule.Copy(original);

        copy.Should().NotBeSameAs(original);
        ((ICollection<int>)copy).Count.Should().Be(3);
        copy.Contains(1).Should().BeTrue();
        copy.Contains(2).Should().BeTrue();
        copy.Contains(3).Should().BeTrue();
    }

    [Fact]
    public void Deepcopy_Null_ReturnsNull()
    {
        object result = CopyModule.Deepcopy(null!);
        result.Should().BeNull();
    }

    [Fact]
    public void Deepcopy_ValueType_ReturnsSameValue()
    {
        object result = CopyModule.Deepcopy(42);
        result.Should().Be(42);
    }

    [Fact]
    public void Deepcopy_NestedList_CreatesIndependentCopy()
    {
        var inner = new Sharpy.List<int> { 10, 20 };
        var original = new Sharpy.List<Sharpy.List<int>> { inner };

        var copy = (Sharpy.List<Sharpy.List<int>>)CopyModule.Deepcopy(original);

        copy.Should().NotBeSameAs(original);
        // Inner list should be a different reference (deep copy)
        copy[0].Should().NotBeSameAs(inner);
        // But have same values
        copy[0][0].Should().Be(10);
        copy[0][1].Should().Be(20);

        // Modifying the copy should not affect the original
        copy[0].Append(30);
        ((ICollection<int>)inner).Count.Should().Be(2);
    }

    [Fact]
    public void Deepcopy_Dict_CreatesIndependentCopy()
    {
        var innerList = new Sharpy.List<int> { 1, 2 };
        var original = new Sharpy.Dict<string, Sharpy.List<int>>();
        original.Add("key", innerList);

        var copy = (Sharpy.Dict<string, Sharpy.List<int>>)CopyModule.Deepcopy(original);

        copy.Should().NotBeSameAs(original);
        copy["key"].Should().NotBeSameAs(innerList);
        copy["key"][0].Should().Be(1);
        copy["key"][1].Should().Be(2);
    }

    [Fact]
    public void Deepcopy_Set_CreatesNewSet()
    {
        var original = new Sharpy.Set<int> { 1, 2, 3 };
        var copy = (Sharpy.Set<int>)CopyModule.Deepcopy(original);

        copy.Should().NotBeSameAs(original);
        ((ICollection<int>)copy).Count.Should().Be(3);
    }

    [Fact]
    public void Deepcopy_CircularReference_HandledGracefully()
    {
        var list = new Sharpy.List<object>();
        list.Append(list); // circular reference

        var copy = (Sharpy.List<object>)CopyModule.Deepcopy(list);

        copy.Should().NotBeSameAs(list);
        // The inner element should reference the copy, not the original
        copy[0].Should().BeSameAs(copy);
    }

    [Fact]
    public void Copy_List_ModifyingCopyDoesNotAffectOriginal()
    {
        var original = new Sharpy.List<int> { 1, 2, 3 };
        var copy = (Sharpy.List<int>)CopyModule.Copy(original);

        copy.Append(4);
        ((ICollection<int>)original).Count.Should().Be(3);
    }
}
