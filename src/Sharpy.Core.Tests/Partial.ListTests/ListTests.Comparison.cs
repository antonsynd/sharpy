namespace Sharpy.Core.Tests;

using Xunit;

public partial class ListTests
{
    public class Comparison
    {
        [Fact]
        public void LessThan_EmptyLists_ReturnsFalse()
        {
            var list1 = new List<int>();
            var list2 = new List<int>();

            Assert.False(list1 < list2);
        }

        [Fact]
        public void LessThan_EmptyVsNonEmpty_ReturnsTrue()
        {
            var empty = new List<int>();
            var nonEmpty = new List<int> { 1 };

            Assert.True(empty < nonEmpty);
        }

        [Fact]
        public void LessThan_NonEmptyVsEmpty_ReturnsFalse()
        {
            var nonEmpty = new List<int> { 1 };
            var empty = new List<int>();

            Assert.False(nonEmpty < empty);
        }

        [Fact]
        public void LessThan_LexicographicallyLess_ReturnsTrue()
        {
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2, 4 };

            Assert.True(list1 < list2);
        }

        [Fact]
        public void LessThan_LexicographicallyGreater_ReturnsFalse()
        {
            var list1 = new List<int> { 1, 2, 4 };
            var list2 = new List<int> { 1, 2, 3 };

            Assert.False(list1 < list2);
        }

        [Fact]
        public void LessThan_PrefixOfOther_ReturnsTrue()
        {
            var list1 = new List<int> { 1, 2 };
            var list2 = new List<int> { 1, 2, 3 };

            Assert.True(list1 < list2);
        }

        [Fact]
        public void LessThan_OtherIsPrefixOfThis_ReturnsFalse()
        {
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2 };

            Assert.False(list1 < list2);
        }

        [Fact]
        public void LessThan_EqualLists_ReturnsFalse()
        {
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2, 3 };

            Assert.False(list1 < list2);
        }

        [Fact]
        public void LessThan_NullRight_ThrowsTypeError()
        {
            var list = new List<int> { 1, 2, 3 };

            Assert.Throws<TypeError>(() => list < null);
        }

        [Fact]
        public void LessThan_NullLeft_ThrowsTypeError()
        {
            var list = new List<int> { 1, 2, 3 };

            Assert.Throws<TypeError>(() => (List<int>)null < list);
        }

        [Fact]
        public void LessThanOrEqual_EmptyLists_ReturnsTrue()
        {
            var list1 = new List<int>();
            var list2 = new List<int>();

            Assert.True(list1 <= list2);
        }

        [Fact]
        public void LessThanOrEqual_EmptyVsNonEmpty_ReturnsTrue()
        {
            var empty = new List<int>();
            var nonEmpty = new List<int> { 1 };

            Assert.True(empty <= nonEmpty);
        }

        [Fact]
        public void LessThanOrEqual_LexicographicallyLess_ReturnsTrue()
        {
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2, 4 };

            Assert.True(list1 <= list2);
        }

        [Fact]
        public void LessThanOrEqual_EqualLists_ReturnsTrue()
        {
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2, 3 };

            Assert.True(list1 <= list2);
        }

        [Fact]
        public void LessThanOrEqual_LexicographicallyGreater_ReturnsFalse()
        {
            var list1 = new List<int> { 1, 2, 4 };
            var list2 = new List<int> { 1, 2, 3 };

            Assert.False(list1 <= list2);
        }

        [Fact]
        public void GreaterThan_EmptyLists_ReturnsFalse()
        {
            var list1 = new List<int>();
            var list2 = new List<int>();

            Assert.False(list1 > list2);
        }

        [Fact]
        public void GreaterThan_NonEmptyVsEmpty_ReturnsTrue()
        {
            var nonEmpty = new List<int> { 1 };
            var empty = new List<int>();

            Assert.True(nonEmpty > empty);
        }

        [Fact]
        public void GreaterThan_EmptyVsNonEmpty_ReturnsFalse()
        {
            var empty = new List<int>();
            var nonEmpty = new List<int> { 1 };

            Assert.False(empty > nonEmpty);
        }

        [Fact]
        public void GreaterThan_LexicographicallyGreater_ReturnsTrue()
        {
            var list1 = new List<int> { 1, 2, 4 };
            var list2 = new List<int> { 1, 2, 3 };

            Assert.True(list1 > list2);
        }

        [Fact]
        public void GreaterThan_LexicographicallyLess_ReturnsFalse()
        {
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2, 4 };

            Assert.False(list1 > list2);
        }

        [Fact]
        public void GreaterThan_OtherIsPrefixOfThis_ReturnsTrue()
        {
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2 };

            Assert.True(list1 > list2);
        }

        [Fact]
        public void GreaterThan_PrefixOfOther_ReturnsFalse()
        {
            var list1 = new List<int> { 1, 2 };
            var list2 = new List<int> { 1, 2, 3 };

            Assert.False(list1 > list2);
        }

        [Fact]
        public void GreaterThan_EqualLists_ReturnsFalse()
        {
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2, 3 };

            Assert.False(list1 > list2);
        }

        [Fact]
        public void GreaterThanOrEqual_EmptyLists_ReturnsTrue()
        {
            var list1 = new List<int>();
            var list2 = new List<int>();

            Assert.True(list1 >= list2);
        }

        [Fact]
        public void GreaterThanOrEqual_NonEmptyVsEmpty_ReturnsTrue()
        {
            var nonEmpty = new List<int> { 1 };
            var empty = new List<int>();

            Assert.True(nonEmpty >= empty);
        }

        [Fact]
        public void GreaterThanOrEqual_LexicographicallyGreater_ReturnsTrue()
        {
            var list1 = new List<int> { 1, 2, 4 };
            var list2 = new List<int> { 1, 2, 3 };

            Assert.True(list1 >= list2);
        }

        [Fact]
        public void GreaterThanOrEqual_EqualLists_ReturnsTrue()
        {
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2, 3 };

            Assert.True(list1 >= list2);
        }

        [Fact]
        public void GreaterThanOrEqual_LexicographicallyLess_ReturnsFalse()
        {
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2, 4 };

            Assert.False(list1 >= list2);
        }

        [Fact]
        public void Comparison_WithStrings_WorksCorrectly()
        {
            var list1 = new List<string> { "apple", "banana" };
            var list2 = new List<string> { "apple", "cherry" };

            Assert.True(list1 < list2);
            Assert.True(list1 <= list2);
            Assert.False(list1 > list2);
            Assert.False(list1 >= list2);
        }

        [Fact]
        public void Comparison_MixedSizes_WorksCorrectly()
        {
            var list1 = new List<int> { 1, 2, 3, 4, 5 };
            var list2 = new List<int> { 1, 2, 3 };
            var list3 = new List<int> { 1, 2, 3, 4, 5, 6 };

            Assert.False(list1 < list2);  // [1,2,3,4,5] is not < [1,2,3]
            Assert.True(list1 > list2);   // [1,2,3,4,5] is > [1,2,3]
            Assert.True(list1 < list3);   // [1,2,3,4,5] is < [1,2,3,4,5,6]
            Assert.False(list1 > list3);  // [1,2,3,4,5] is not > [1,2,3,4,5,6]
        }

        [Fact]
        public void Comparison_SingleElementDifference_WorksCorrectly()
        {
            var list1 = new List<int> { 5 };
            var list2 = new List<int> { 10 };

            Assert.True(list1 < list2);
            Assert.False(list1 > list2);
            Assert.True(list1 <= list2);
            Assert.False(list1 >= list2);
        }
    }
}
