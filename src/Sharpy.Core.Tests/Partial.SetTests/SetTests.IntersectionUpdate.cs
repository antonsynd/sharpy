using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_IntersectionUpdate_KeepsCommonElements()
    {
        // Given
        Set<int> s = [1, 2, 3, 4, 5];
        Set<int> other = [2, 3, 4, 6];

        // When
        s.IntersectionUpdate(other);

        // Then
        s.Should().HaveCount(3);
        s.ToHashSet().Should().BeEquivalentTo(new HashSet<int> { 2, 3, 4 });
    }

    [Fact]
    public void Set_IntersectionUpdate_NoOverlap_BecomesEmpty()
    {
        // Given
        Set<int> s = [1, 2, 3];
        Set<int> other = [4, 5, 6];

        // When
        s.IntersectionUpdate(other);

        // Then
        s.Should().HaveCount(0);
    }

    [Fact]
    public void Set_IntersectionUpdate_WithSameSet_NoChange()
    {
        // Given
        Set<int> s = [1, 2, 3];
        Set<int> other = [1, 2, 3];

        // When
        s.IntersectionUpdate(other);

        // Then
        s.Should().HaveCount(3);
    }

    [Fact]
    public void Set_IntersectionUpdate_NullOther_ThrowsTypeError()
    {
        Set<int> s = [1, 2, 3];

        FluentActions.Invoking(() => s.IntersectionUpdate(null!))
            .Should().Throw<TypeError>();
    }
}
