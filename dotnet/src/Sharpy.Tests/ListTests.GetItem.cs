using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {
        [Fact]
        public void List_Slice_Zero_Step()
        {
            // If
            List<int> l = [1, 3, 5, 1, 7];

            // When/then
            FluentActions.Invoking(() => l.__GetItem__(new Slice(0, 0, 0))).Should().Throw<ValueError>();
        }

        [Fact]
        public void List_Slice_Negative_Step()
        {
            // If
            List<int> l = [1, 3, 5, 1, 7];

            // When
            var actual = l.__GetItem__(new Slice(0, 1, -1));

            // Then
            Len(actual).Should().Be(0);
        }

        [Fact]
        public void List_Slice_Same_Start_And_End()
        {
            // If
            List<int> l = [1, 3, 5, 1, 7];

            // When
            var actual = l.__GetItem__(new Slice(1, 1));

            // Then
            Len(actual).Should().Be(0);
        }

        [Fact]
        public void List_Slice_Single_Step()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When
            var res = l.__GetItem__(new Slice(1, 3));

            // Then
            var actual = res.ToList();
            DotNetList<int> expected = [3, 5];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Slice_Not_Single_Step_Not_Enough()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When
            var res = l.__GetItem__(new Slice(1, 3, 4));

            // Then
            var actual = res.ToList();
            DotNetList<int> expected = [3];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Slice_Not_Single_Step_Enough()
        {
            // If
            List<int> l = [1, 3, 5, 7, 9];

            // When
            var res = l.__GetItem__(new Slice(1, 5, 2));

            // Then
            var actual = res.ToList();
            DotNetList<int> expected = [3, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Slice_Out_Of_Bounds_Left()
        {
            // If
            List<int> l = [1, 3, 5, 7, 9];

            // When
            var res = l.__GetItem__(new Slice(-9, 4, 2));

            // Then
            var actual = res.ToList();
            DotNetList<int> expected = [1, 5];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Slice_Out_Of_Bounds_Right()
        {
            // If
            List<int> l = [1, 3, 5, 7, 9];

            // When
            var res = l.__GetItem__(new Slice(0, 9, 2));

            // Then
            var actual = res.ToList();
            DotNetList<int> expected = [1, 5, 9];

            actual.Should().Equal(expected);
        }
    }
}
