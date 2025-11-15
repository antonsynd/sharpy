using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_IsReadOnly()
    {
        // If
        var l = new List<int>();

        // When/then
        l.IsReadOnly.Should().BeFalse();
    }
}
