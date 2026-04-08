using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Str_Empty()
    {
        // If
        var s = new Set<int>();

        // When/then
        ((string)Str(s)).Should().Be("{}");
    }

    [Fact]
    public void Set_Str_Not_Empty()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When/then
        ((string)Str(s)).Should().Be("{1, 3, 5, 7}");
    }
}
