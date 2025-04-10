using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {
        [Fact]
        public void List_Index_Empty()
        {
            // If
            var l = new List<int>();

            // When/then
            FluentActions.Invoking(() => l.Index(5)).Should().Throw<ValueError>();
        }

        [Fact]
        public void List_Index_Non_Empty()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When/then
            l.Index(5).Should().Be(2);
        }

        [Fact]
        public void List_Index_Non_Empty_Object_Equal()
        {
            // If
            List<IntWrapper> l = [1, 3, 5, 7];

            IntWrapper i = 5;

            // When/then
            l.Index(i).Should().Be(2);
        }

        [Fact]
        public void List_Index_Non_Empty_Object_Not_Equal()
        {
            // If
            List<IntWrapper> l = [1, 3, 5, 7];

            IntWrapper i = 4;

            // When/then
            FluentActions.Invoking(() => l.Index(i)).Should().Throw<ValueError>();
        }

        [Fact]
        public void List_Index_Non_Empty_Object_Same()
        {
            // If
            List<IntIdentityWrapper> l = [1, 3, 5, 7];

            var i = l[2];

            // When/then
            l.Index(i).Should().Be(2);
        }

        [Fact]
        public void List_Index_Non_Empty_Object_Not_Same()
        {
            // If
            List<IntIdentityWrapper> l = [1, 3, 5, 7];

            IntIdentityWrapper i = 5;

            // When/then
            FluentActions.Invoking(() => l.Index(i)).Should().Throw<ValueError>();
        }
    }
}
