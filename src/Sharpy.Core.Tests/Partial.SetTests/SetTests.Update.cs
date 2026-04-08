using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Update_AddsElementsFromOther()
    {
        // Given
        Set<int> s = [1, 2, 3];
        Set<int> other = [3, 4, 5];

        // When
        s.Update(other);

        // Then
        s.Should().HaveCount(5);
        s.ToHashSet().Should().BeEquivalentTo(new HashSet<int> { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void Set_Update_WithEmptySet_NoChange()
    {
        // Given
        Set<int> s = [1, 2, 3];
        Set<int> other = [];

        // When
        s.Update(other);

        // Then
        s.Should().HaveCount(3);
    }

    [Fact]
    public void Set_Update_EmptySetWithNonEmpty_AddsAll()
    {
        // Given
        Set<int> s = [];
        Set<int> other = [1, 2, 3];

        // When
        s.Update(other);

        // Then
        s.Should().HaveCount(3);
        s.ToHashSet().Should().BeEquivalentTo(new HashSet<int> { 1, 2, 3 });
    }

    [Fact]
    public void Set_Update_DoesNotModifyOther()
    {
        // Given
        Set<int> s = [1, 2];
        Set<int> other = [3, 4];

        // When
        s.Update(other);

        // Then
        other.Should().HaveCount(2);
    }

    [Fact]
    public void Set_Update_NullOther_ThrowsTypeError()
    {
        // Given
        Set<int> s = [1, 2, 3];

        // When/Then
        FluentActions.Invoking(() => s.Update(null!))
            .Should().Throw<TypeError>();
    }
}
