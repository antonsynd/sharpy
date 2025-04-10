using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {

        [Fact]
        public void List_Repr_Empty()
        {
            // If
            var l = new List<int>();

            // When/then
            Repr(l).Should().Be("[]");
        }

        [Fact]
        public void List_Repr_Not_Empty()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When/then
            Repr(l).Should().Be("[1, 3, 5, 7]");
        }
    }
}
