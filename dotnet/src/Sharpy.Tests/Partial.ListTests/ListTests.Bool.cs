using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {
        [Fact]
        public void List_Bool_Empty()
        {
            // If
            var l = new List<int>();

            // When/then
            Bool(l).Should().BeFalse();
        }

        [Fact]
        public void List_Bool_Non_Empty()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When/then
            Bool(l).Should().BeTrue();
        }
    }
}
