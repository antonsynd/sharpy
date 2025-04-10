using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {
        [Fact]
        public void List_Append_One_Element()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When
            l.Append(9);

            // Then
            Len(l).Should().Be(5);

            var actual = l.ToList();
            DotNetList<int> expected = [1, 3, 5, 7, 9];

            actual.Should().Equal(expected);
        }
    }
}
