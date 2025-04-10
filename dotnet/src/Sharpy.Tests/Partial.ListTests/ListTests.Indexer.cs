using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {
        [Fact]
        public void List_Slice_Operator()
        {
            // If
            List<int> l = [1, 3, 5, 7, 9];

            // When
            var res = l[1, 5, 2];

            // Then
            var actual = res.ToList();
            DotNetList<int> expected = [3, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Slice_Operator_Object()
        {
            // If
            List<IntWrapper> l = [1, 3, 5, 7, 9];

            // When
            var res = l[1, 5, 2];

            // Then
            var actual = res.ToList();
            DotNetList<IntWrapper> expected = [3, 7];

            actual.Should().Equal(expected);
        }
    }
}
