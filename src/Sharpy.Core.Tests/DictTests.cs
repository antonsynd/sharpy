using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Dict_Tests
{
    [Fact]
    public void Constructor_CreatesEmptyDict()
    {
        // Act
        var dict = new Dict<Str, int>();

        // Assert
        dict.Count.Should().Be(0);
    }

    [Fact]
    public void Indexer_Get_ReturnsValue()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["key"] = 42;

        // Act
        var value = dict["key"];

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void Indexer_Get_ThrowsKeyErrorForMissingKey()
    {
        // Arrange
        var dict = new Dict<Str, int>();

        // Act & Assert
        dict.Invoking(d => { var _ = d["missing"]; })
            .Should().Throw<KeyError>();
    }

    [Fact]
    public void Indexer_Set_AddsNewKey()
    {
        // Arrange
        var dict = new Dict<Str, int>();

        // Act
        dict["new"] = 100;

        // Assert
        dict["new"].Should().Be(100);
        dict.Count.Should().Be(1);
    }

    [Fact]
    public void Indexer_Set_UpdatesExistingKey()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["key"] = 1;

        // Act
        dict["key"] = 2;

        // Assert
        dict["key"].Should().Be(2);
        dict.Count.Should().Be(1);
    }

    [Fact]
    public void Get_ReturnsValueForExistingKey()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["key"] = 42;

        // Act
        var value = dict.Get("key");

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void Get_ReturnsNullForMissingKey()
    {
        // Arrange
        var dict = new Dict<Str, int?>();

        // Act
        var value = dict.Get("missing");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void Get_ReturnsDefaultForMissingKey()
    {
        // Arrange
        var dict = new Dict<Str, int>();

        // Act
        var value = dict.Get("missing", 99);

        // Assert
        value.Should().Be(99);
    }

    [Fact]
    public void Contains_ReturnsTrueForExistingKey()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["key"] = 42;

        // Act
        var result = dict.__Contains__("key");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Contains_ReturnsFalseForMissingKey()
    {
        // Arrange
        var dict = new Dict<Str, int>();

        // Act
        var result = dict.__Contains__("missing");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        dict["c"] = 3;

        // Act
        dict.Clear();

        // Assert
        dict.Count.Should().Be(0);
    }

    [Fact]
    public void Pop_ReturnsAndRemovesValue()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["key"] = 42;

        // Act
        var value = dict.Pop("key");

        // Assert
        value.Should().Be(42);
        dict.Count.Should().Be(0);
        dict.__Contains__("key").Should().BeFalse();
    }

    [Fact]
    public void Pop_ThrowsKeyErrorForMissingKey()
    {
        // Arrange
        var dict = new Dict<Str, int>();

        // Act & Assert
        dict.Invoking(d => d.Pop("missing"))
            .Should().Throw<KeyError>();
    }

    [Fact]
    public void Pop_ReturnsDefaultForMissingKey()
    {
        // Arrange
        var dict = new Dict<Str, int>();

        // Act
        var value = dict.Pop("missing", 99);

        // Assert
        value.Should().Be(99);
    }

    [Fact]
    public void SetDefault_ReturnsExistingValue()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["key"] = 42;

        // Act
        var value = dict.SetDefault("key", 99);

        // Assert
        value.Should().Be(42);
        dict["key"].Should().Be(42);
    }

    [Fact]
    public void SetDefault_SetsAndReturnsDefaultForMissingKey()
    {
        // Arrange
        var dict = new Dict<Str, int>();

        // Act
        var value = dict.SetDefault("key", 99);

        // Assert
        value.Should().Be(99);
        dict["key"].Should().Be(99);
    }

    [Fact]
    public void Update_MergesAnotherDict()
    {
        // Arrange
        var dict1 = new Dict<Str, int>();
        dict1["a"] = 1;
        dict1["b"] = 2;

        var dict2 = new Dict<Str, int>();
        dict2["b"] = 20;
        dict2["c"] = 30;

        // Act
        dict1.Update(dict2);

        // Assert
        dict1["a"].Should().Be(1);
        dict1["b"].Should().Be(20); // Overwritten
        dict1["c"].Should().Be(30);
        dict1.Count.Should().Be(3);
    }

    [Fact]
    public void Or_MergesDicts()
    {
        // Arrange
        var dict1 = new Dict<Str, int>();
        dict1["a"] = 1;
        dict1["b"] = 2;

        var dict2 = new Dict<Str, int>();
        dict2["b"] = 20;
        dict2["c"] = 30;

        // Act
        var result = dict1 | dict2;

        // Assert
        result["a"].Should().Be(1);
        result["b"].Should().Be(20); // From dict2
        result["c"].Should().Be(30);
        result.Count.Should().Be(3);

        // Original dicts should be unchanged
        dict1.Count.Should().Be(2);
        dict2.Count.Should().Be(2);
    }

    [Fact]
    public void Copy_CreatesShallowCopy()
    {
        // Arrange
        var original = new Dict<Str, int>();
        original["a"] = 1;
        original["b"] = 2;

        // Act
        var copy = original.Copy();

        // Assert
        copy.Count.Should().Be(2);
        copy["a"].Should().Be(1);
        copy["b"].Should().Be(2);

        // Modifying copy should not affect original
        copy["c"] = 3;
        original.Count.Should().Be(2);
        original.__Contains__("c").Should().BeFalse();
    }

    [Fact]
    public void Equality_ComparesKeyValuePairs()
    {
        // Arrange
        var dict1 = new Dict<Str, int>();
        dict1["a"] = 1;
        dict1["b"] = 2;

        var dict2 = new Dict<Str, int>();
        dict2["a"] = 1;
        dict2["b"] = 2;

        var dict3 = new Dict<Str, int>();
        dict3["a"] = 1;
        dict3["b"] = 99; // Different value

        // Act & Assert
        (dict1 == dict2).Should().BeTrue();
        (dict1 != dict3).Should().BeTrue();
    }

    [Fact]
    public void Iteration_YieldsKeys()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        dict["c"] = 3;

        // Act
        var keys = new List<Str>();
        foreach (var key in dict)
        {
            keys.Add(key);
        }

        // Assert
        keys.Should().HaveCount(3);
        keys.Should().Contain(new Str("a"));
        keys.Should().Contain(new Str("b"));
        keys.Should().Contain(new Str("c"));
    }

    [Fact]
    public void PopItem_RemovesAndReturnsFirstItem()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        dict["c"] = 3;

        // Act
        var (key, value) = dict.PopItem();

        // Assert
        key.Should().Be("a"); // First inserted (last=false is default)
        value.Should().Be(1);
        dict.Count.Should().Be(2);
        dict.__Contains__("a").Should().BeFalse();
    }

    [Fact]
    public void PopItem_ThrowsInvalidOperationOnEmptyDict()
    {
        // Arrange
        var dict = new Dict<Str, int>();

        // Act & Assert
        dict.Invoking(d => d.PopItem())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*no elements*");
    }

    [Fact]
    public void MixedOperations_MaintainCorrectState()
    {
        // Arrange
        var dict = new Dict<Str, int>();

        // Act & Assert - Progressive operations
        dict["a"] = 1;
        dict.Count.Should().Be(1);

        dict["b"] = 2;
        dict["c"] = 3;
        dict.Count.Should().Be(3);

        dict.Pop("b");
        dict.Count.Should().Be(2);
        dict.__Contains__("b").Should().BeFalse();

        dict["a"] = 100; // Update
        dict["a"].Should().Be(100);
        dict.Count.Should().Be(2);

        dict.Clear();
        dict.Count.Should().Be(0);
    }

    [Fact]
    public void Indexer_Get_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var dict = new Dict<Str?, int>();

        // Act & Assert
        dict.Invoking(d => { var _ = d[null!]; })
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Indexer_Set_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var dict = new Dict<Str?, int>();

        // Act & Assert
        dict.Invoking(d => d[null!] = 42)
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Update_WithNullMapping_ThrowsArgumentNullException()
    {
        // Arrange
        var dict = new Dict<Str, int>();

        // Act & Assert
        dict.Invoking(d => d.Update((Dict<Str, int>)null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Contains_WithNullKey_ThrowsException()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;

        // Act & Assert
        // Note: With K : notnull constraint, null keys throw NullReferenceException
        // when the key type's GetHashCode is called by the underlying Dictionary
        dict.Invoking(d => d.__Contains__(null!))
            .Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void Or_WithEmptyDicts_ReturnsEmptyDict()
    {
        // Arrange
        var dict1 = new Dict<Str, int>();
        var dict2 = new Dict<Str, int>();

        // Act
        var result = dict1 | dict2;

        // Assert
        result.Count.Should().Be(0);
    }

    [Fact]
    public void Copy_ModifyingOriginal_DoesNotAffectCopy()
    {
        // Arrange
        var original = new Dict<Str, int>();
        original["a"] = 1;
        var copy = original.Copy();

        // Act
        original["a"] = 999;
        original["b"] = 2;

        // Assert
        copy["a"].Should().Be(1); // Copy unchanged
        copy.__Contains__("b").Should().BeFalse();
    }

    [Fact]
    public void SetDefault_WithNullDefault_SetsNull()
    {
        // Arrange
        var dict = new Dict<Str, int?>();

        // Act
        var value = dict.SetDefault("key", null);

        // Assert
        value.Should().BeNull();
        dict["key"].Should().BeNull();
    }

    [Fact]
    public void Equality_WithDifferentSizes_ReturnsFalse()
    {
        // Arrange
        var dict1 = new Dict<Str, int>();
        dict1["a"] = 1;

        var dict2 = new Dict<Str, int>();
        dict2["a"] = 1;
        dict2["b"] = 2;

        // Act & Assert
        (dict1 == dict2).Should().BeFalse();
        (dict1 != dict2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithNull_ReturnsFalse()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;

        // Act & Assert
        (dict == null).Should().BeFalse();
        (dict != null).Should().BeTrue();
    }
}
