using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_IsReadOnly()
    {
        // If
        var s = new Set<int>();

        // When/then
        s.IsReadOnly.Should().BeFalse();
    }
}
