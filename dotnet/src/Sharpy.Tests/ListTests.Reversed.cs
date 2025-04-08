using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {
        [Fact]
        public void List_Reversed_Empty()
        {
            // If
            var l = new List<int>();

            // When
            var reversed = Reversed(l);
            var reversed_list = new List<int>(reversed);

            // Then
            Len(reversed_list).Should().Be(0);
        }

        [Fact]
        public void List_Reversed_Non_Empty()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When
            var reversed = Reversed(l);
            var reversedList = new List<int>(reversed);

            // Then
            var actual = reversedList.ToList();
            DotNetList<int> expected = [7, 5, 3, 1];

            actual.Should().Equal(expected);
        }
    }
}
