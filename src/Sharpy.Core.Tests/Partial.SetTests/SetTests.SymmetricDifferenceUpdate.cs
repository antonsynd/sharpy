using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_SymmetricDifferenceUpdate_KeepsOnlyNonSharedElements()
    {
        // Given
        Set<int> s = [1, 2, 3];
        Set<int> other = [2, 3, 4];

        // When
        s.SymmetricDifferenceUpdate(other);

        // Then
        s.Should().HaveCount(2);
        s.ToHashSet().Should().BeEquivalentTo(new HashSet<int> { 1, 4 });
    }

    [Fact]
    public void Set_SymmetricDifferenceUpdate_NoOverlap_CombinesAll()
    {
        // Given
        Set<int> s = [1, 2];
        Set<int> other = [3, 4];

        // When
        s.SymmetricDifferenceUpdate(other);

        // Then
        s.Should().HaveCount(4);
        s.ToHashSet().Should().BeEquivalentTo(new HashSet<int> { 1, 2, 3, 4 });
    }

    [Fact]
    public void Set_SymmetricDifferenceUpdate_Identical_BecomesEmpty()
    {
        // Given
        Set<int> s = [1, 2, 3];
        Set<int> other = [1, 2, 3];

        // When
        s.SymmetricDifferenceUpdate(other);

        // Then
        s.Should().HaveCount(0);
    }

    [Fact]
    public void Set_SymmetricDifferenceUpdate_NullOther_ThrowsTypeError()
    {
        Set<int> s = [1, 2, 3];

        FluentActions.Invoking(() => s.SymmetricDifferenceUpdate(null!))
            .Should().Throw<TypeError>();
    }
}
