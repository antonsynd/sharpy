using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public class Min_Tests
    {
        [Fact]
        public void Min_List_Empty()
        {
            // If
            var l = new List<int>();

            // When/then
            FluentActions.Invoking(() => Min(l)).Should().Throw<ValueError>();
        }

        [Fact]
        public void Min_List_Non_Empty()
        {
            // If
            List<int> l = [5, 7, 3, 1];

            // When/then
            Min(l).Should().Be(1);
        }

        // [Fact]
        // public void Min_List_With_Nullable()
        // {
        //     // If
        //     List<Optional<int>> l = [ 5, 7, None<int>(), 1 ];

        //     // When/then
        //     Min(l, value => value.GetValue()).Should().Be(None<int>());
        // }
    }
}
