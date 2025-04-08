using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {
        [Fact]
        public void List_Native_Iteration()
        {
            // If
            List<int> l = [1, 3, 5, 7];
            var expected = l.ToList();

            // When
            DotNetList<int> actual = [];

            foreach (var elem in l)
            {
                actual.Add(elem);
            }

            // Then
            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Iterator_Iteration()
        {
            // If
            List<int> l = [1, 3, 5, 7];
            var expected = l.ToList();
            var it = Iter(l);

            // When
            DotNetList<int> actual = [];

            foreach (var elem in it)
            {
                actual.Add(elem);
            }

            // Then
            actual.Should().Equal(expected);
        }
    }
}
