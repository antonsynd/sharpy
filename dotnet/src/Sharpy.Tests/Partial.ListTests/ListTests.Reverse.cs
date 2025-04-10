using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {
        [Fact]
        public void List_Reverse_Empty()
        {
            // If
            var l = new List<int>();

            // When
            l.Reverse();

            // Then
            Len(l).Should().Be(0);
        }

        [Fact]
        public void List_Reverse_Non_Empty()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When
            l.Reverse();

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [7, 5, 3, 1];

            actual.Should().Equal(expected);
        }
    }
}
