using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {
        [Fact]
        public void List_Pop_Empty()
        {
            // If
            var l = new List<int>();

            // When/then
            FluentActions.Invoking(() => l.Pop()).Should().Throw<IndexError>();
        }

        [Fact]
        public void List_Pop_Last()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When
            l.Pop();

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [1, 3, 5];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Pop_Front()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When
            l.Pop(0);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [3, 5, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Pop_Middle()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When
            l.Pop(1);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [1, 5, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Pop_Negative()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When
            l.Pop(-2);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [1, 3, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Pop_Out_Of_Bounds_Left()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When/then
            FluentActions.Invoking(() => l.Pop(-100)).Should().Throw<IndexError>();
        }

        [Fact]
        public void List_Pop_Out_Of_Bounds_Right()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When/then
            FluentActions.Invoking(() => l.Pop(100)).Should().Throw<IndexError>();
        }
    }
}
