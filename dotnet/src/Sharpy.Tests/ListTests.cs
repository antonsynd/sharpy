using Sharpy;
using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public class ListTests
    {
        [Fact]
        public void List_Should_Be_Empty_On_Initialization()
        {
            // If
            var list = new List<int>();

            // When
            uint length = list.Len();

            // Then
            length.Should().Be(0);
        }
    }
}
