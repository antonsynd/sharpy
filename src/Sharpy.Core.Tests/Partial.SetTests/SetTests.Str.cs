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
        // python3: print(str(set()), repr(set())) -> set() set()  ({} is the empty dict)
        Str(s).Should().Be("set()");
    }

    [Fact]
    public void Set_Str_Not_Empty()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When/then
        Str(s).Should().Be("{1, 3, 5, 7}");
    }
}
