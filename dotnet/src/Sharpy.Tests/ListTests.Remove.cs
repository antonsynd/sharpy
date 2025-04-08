using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public partial class List_Tests
    {
        [Fact]
        public void List_Remove_Empty()
        {
            // If
            var l = new List<int>();

            // When/then
            FluentActions.Invoking(() => l.Remove(3)).Should().Throw<ValueError>();
        }

        [Fact]
        public void List_Remove_Not_Present()
        {
            // If
            List<int> l = [1, 5, 7];

            // When/then
            FluentActions.Invoking(() => l.Remove(3)).Should().Throw<ValueError>();
        }

        [Fact]
        public void List_Remove_Present_Once()
        {
            // If
            List<int> l = [1, 3, 5, 7];

            // When
            l.Remove(3);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [1, 5, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Remove_Present_Once_Object_Equal()
        {
            // If
            List<IntWrapper> l = [1, 3, 5, 7];

            // When
            var second_elem = l[1];
            l.Remove(second_elem);

            // Then
            var actual = l.ToList();
            DotNetList<IntWrapper> expected = [1, 5, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Remove_Present_Once_Object_Not_Equal()
        {
            // If
            List<IntWrapper> l = [1, 3, 5, 7];

            IntWrapper i = 4;

            // When/then
            FluentActions.Invoking(() => l.Remove(i)).Should().Throw<ValueError>();
        }

        [Fact]
        public void List_Remove_Present_Once_Object_Not_Same()
        {
            // If
            List<IntIdentityWrapper> l = [1, 3, 5, 7];

            IntIdentityWrapper i = 3;

            // When/then
            FluentActions.Invoking(() => l.Remove(i)).Should().Throw<ValueError>();
        }

        [Fact]
        public void List_Remove_Present_More_Than_Once()
        {
            // If
            List<int> l = [1, 3, 5, 7, 3];

            // When
            l.Remove(3);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [1, 5, 7, 3];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Remove_Present_More_Than_Once_Object_Same()
        {
            // If
            List<IntWrapper> l = [1, 3, 5, 7, 3];

            // When
            var second_elem = l[1];
            l.Remove(second_elem);

            // Then
            var actual = l.ToList();
            DotNetList<IntWrapper> expected = [1, 5, 7, 3];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Remove_Present_More_Than_Once_Object_Equality_Last()
        {
            // If
            List<IntWrapper> l = [1, 3, 5, 7, 3];

            // When
            var last_elem = l[-1];
            l.Remove(last_elem);

            // Then
            var actual = l.ToList();
            DotNetList<IntWrapper> expected = [1, 5, 7, 3];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Remove_Present_More_Than_Once_Object_Identity_Last()
        {
            // If
            List<IntIdentityWrapper> source = [1, 3, 5, 7, 3];
            List<IntIdentityWrapper> l = source.Copy();

            // When
            var last_elem = l[-1];
            l.Remove(last_elem);

            // Then
            var actual = l.ToList();
            DotNetList<IntIdentityWrapper> expected = [.. source[0, -1]];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Remove_Present_More_Than_Once_Object_Identity_Not_Same()
        {
            // If
            List<IntIdentityWrapper> l = [1, 3, 5, 7, 3];

            // When/then
            FluentActions.Invoking(() => l.Remove(new IntIdentityWrapper(3))).Should().Throw<ValueError>();
        }

        [Fact]
        public void List_Remove_At_End()
        {
            // If
            List<int> l = [1, 5, 7, 3];

            // When
            l.Remove(3);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [1, 5, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Remove_At_End_Object_Same()
        {
            // If
            List<IntWrapper> l = [1, 5, 7, 3];

            // When
            var last_elem = l[-1];
            l.Remove(last_elem);

            // Then
            var actual = l.ToList();
            DotNetList<IntWrapper> expected = [1, 5, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Remove_At_End_Object_Equality_Not_Same()
        {
            // If
            List<IntWrapper> l = [1, 5, 7, 3];

            // When
            IntWrapper i = 3;

            l.Remove(i);

            // Then
            var actual = l.ToList();
            DotNetList<IntWrapper> expected = [1, 5, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Remove_At_End_Object_Identity_Not_Same()
        {
            // If
            List<IntIdentityWrapper> l = [1, 5, 7, 3];

            IntIdentityWrapper i = 3;

            // When/then
            FluentActions.Invoking(() => l.Remove(i)).Should().Throw<ValueError>();
        }
    }
}
