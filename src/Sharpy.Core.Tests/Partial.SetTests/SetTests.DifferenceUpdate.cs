using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_DifferenceUpdate_RemovesCommonElements()
    {
        // Given
        Set<int> s = [1, 2, 3, 4, 5];
        Set<int> other = [2, 4];

        // When
        s.DifferenceUpdate(other);

        // Then
        s.Should().HaveCount(3);
        s.ToHashSet().Should().BeEquivalentTo(new HashSet<int> { 1, 3, 5 });
    }

    [Fact]
    public void Set_DifferenceUpdate_NoOverlap_NoChange()
    {
        // Given
        Set<int> s = [1, 2, 3];
        Set<int> other = [4, 5, 6];

        // When
        s.DifferenceUpdate(other);

        // Then
        s.Should().HaveCount(3);
    }

    [Fact]
    public void Set_DifferenceUpdate_AllRemoved_BecomesEmpty()
    {
        // Given
        Set<int> s = [1, 2, 3];
        Set<int> other = [1, 2, 3];

        // When
        s.DifferenceUpdate(other);

        // Then
        s.Should().HaveCount(0);
    }

    [Fact]
    public void Set_DifferenceUpdate_NullOther_ThrowsTypeError()
    {
        Set<int> s = [1, 2, 3];

        FluentActions.Invoking(() => s.DifferenceUpdate(null!))
            .Should().Throw<TypeError>();
    }
}
