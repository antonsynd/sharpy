using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {
        [Fact]
        public void List_Clear_Empty()
        {
            // If
            var l = new List<int>();

            // When
            l.Clear();

            // Then
            Len(l).Should().Be(0);
        }

        [Fact]
        public void List_Clear_Non_Empty()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When
            l.Clear();

            // Then
            Len(l).Should().Be(0);
        }
    }
}
