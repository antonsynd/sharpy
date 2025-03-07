using Xunit;
using FluentAssertions;

using static Sharpy.Builtins;

namespace Sharpy.Tests
{
    file class DotNetList<T> : System.Collections.Generic.List<T>;

    public class List_Tests
    {
        [Fact]
        public void List_No_Args_Constructor()
        {
            // If/when
            var l = new List<int>();

            // Then
            Len(l).Should().Be(0);
        }

        [Fact]
        public void List_Empty_Initializer_List()
        {
            // If/when
            List<int> l = [];

            // Then
            Len(l).Should().Be(0);

            var actual = l.ToList<int>();
            actual.Count.Should().Be(0);
        }

        [Fact]
        public void List_Initializer_List()
        {
            // If/when
            List<int> l = [1, 3, 5, 7];

            // Then
            Len(l).Should().Be(4);

            var actual = l.ToList<int>();
            DotNetList<int> expected = [1, 3, 5, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Empty_Iterable_Constructor()
        {
            // If/when
            List<int> source = [];
            var l = new List<int>(Iter(source));

            // Then
            Len(l).Should().Be(0);

            var actual = l.ToList<int>();
            actual.Count.Should().Be(0);
        }

        [Fact]
        public void List_Iterable_Constructor()
        {
            // If/when
            List<int> source = [ 1, 3, 5, 7 ];
            var l = new List<int>(Iter(source));

            // Then
            Len(l).Should().Be(4);

            var actual = l.ToList<int>();
            DotNetList<int> expected = [1, 3, 5, 7];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Append_One_Element()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l.Append(9);

            // Then
            Len(l).Should().Be(5);

            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 3, 5, 7, 9 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Contains_Empty()
        {
            // If
            var l = new List<int>();

            // When/then
            l.Contains(1).Should().BeFalse();
        }

        [Fact]
        public void List_Contains_Not_Actually_In()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            l.Contains(4).Should().BeFalse();
        }

        [Fact]
        public void List_Contains_Actually_In()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            l.Contains(5).Should().BeTrue();
        }

        [Fact]
        public void List_Clear_Empty()
        {
            // If
            var l = new List<int>();

            // When
            l.Clear();

            // Then
            Len(l).Should().Be(0);
        }

        [Fact]
        public void List_Clear_Non_Empty()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l.Clear();

            // Then
            Len(l).Should().Be(0);
        }

        [Fact]
        public void List_Copy_Empty()
        {
            // If
            var l = new List<int>();

            // When
            var copy = l.Copy();
            copy.Append(5);

            // Then
            l.Should().NotEqual(copy);
            Len(l).Should().NotBe(Len(copy));
        }

        [Fact]
        public void List_Copy_Non_Empty()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            var copy = l.Copy();
            copy.Append(9);

            // Then
            var actual_l_items = l.ToList();
            DotNetList<int> expected_l_items = [ 1, 3, 5, 7 ];
            actual_l_items.Should().Equal(expected_l_items);

            var actual_copy_items = copy.ToList();
            DotNetList<int> expected_copy_items = [ 1, 3, 5, 7, 9 ];
            actual_copy_items.Should().Equal(expected_copy_items);
        }

        [Fact]
        public void List_Extend_Empty_And_Empty_Other()
        {
            // If
            var l = new List<int>();
            var other = new List<int>();

            // When
            l.Extend(other);

            // Then
            Len(l).Should().Be(0);
        }

        [Fact]
        public void List_Extend_Empty_And_Non_Empty_Other()
        {
            // If
            var l = new List<int>();
            List<int> other = [ 1, 3, 5, 7 ];

            // When
            l.Extend(other);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 3, 5, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Extend_Non_Empty_And_Non_Empty_Other()
        {
            // If
            List<int> l = [ 9, 11, 13 ];
            List<int> other = [ 1, 3, 5, 7 ];

            // When
            l.Extend(other);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 9, 11, 13, 1, 3, 5, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Addition_Assignment_Operator()
        {
            // If
            List<int> l = [ 9, 11, 13 ];
            List<int> other = [ 1, 3, 5, 7 ];

            // When
            l += other;

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 9, 11, 13, 1, 3, 5, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Addition_Operator()
        {
            // If
            List<int> l = [ 9, 11, 13 ];
            List<int> other = [ 1, 3, 5, 7 ];

            // When
            var sum = l + other;

            // Then
            var actual = sum.ToList();
            DotNetList<int> expected = [ 9, 11, 13, 1, 3, 5, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Multiplication_Operator_Negative()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            var product = l * -1;

            // Then
            Len(product).Should().Be(0);
        }

        [Fact]
        public void List_Multiplication_Operator_Zero()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            var product = l * 0;

            // Then
            Len(product).Should().Be(0);
        }

        [Fact]
        public void List_Multiplication_Operator_One()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            var product = l * 1;

            // Then
            var actual = product.ToList();
            DotNetList<int> expected = [ 1, 3, 5, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Multiplication_Operator_More_Than_One()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            var product = l * 3;

            // Then
            var actual = product.ToList();
            DotNetList<int> expected = [ 1, 3, 5, 7, 1, 3, 5, 7, 1, 3, 5, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Multiplication_Assignment_Operator_Negative()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l *= -1;

            // Then
            Len(l).Should().Be(0);
        }

        [Fact]
        public void List_Multiplication_Assignment_Operator_Zero()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l *= 0;

            // Then
            Len(l).Should().Be(0);
        }

        [Fact]
        public void List_Multiplication_Assignment_Operator_One()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l *= 1;

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 3, 5, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Multiplication_Assignment_Operator_More_Than_One()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l *= 3;

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 3, 5, 7, 1, 3, 5, 7, 1, 3, 5, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Get_By_Positive_Index()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            l[0].Should().Be(1);
            l[1].Should().Be(3);
            l[2].Should().Be(5);
            l[3].Should().Be(7);
        }

        [Fact]
        public void List_Get_By_Negative_Index()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            l[-1].Should().Be(7);
            l[-2].Should().Be(5);
            l[-3].Should().Be(3);
            l[-4].Should().Be(1);
        }

        [Fact]
        public void List_Get_By_Out_Of_Bounds()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            FluentActions.Invoking(() => { var _ = l[-5]; }).Should().Throw<IndexError>();
            FluentActions.Invoking(() => { var _ = l[4]; }).Should().Throw<IndexError>();
        }

        [Fact]
        public void List_Set_By_Positive_Index()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l[2] = 6;

            // Then
            l[0].Should().Be(1);
            l[1].Should().Be(3);
            l[2].Should().Be(6);
            l[3].Should().Be(7);
        }

        [Fact]
        public void List_Set_By_Negative_Index()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l[-3] = 4;

            // Then
            l[-1].Should().Be(7);
            l[-2].Should().Be(5);
            l[-3].Should().Be(4);
            l[-4].Should().Be(1);
        }

        [Fact]
        public void List_Set_By_Out_Of_Bounds()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            FluentActions.Invoking(() => { l[-5] = 9; }).Should().Throw<IndexError>();
            FluentActions.Invoking(() => { l[4] = 11; }).Should().Throw<IndexError>();
        }

        [Fact]
        public void List_Len_Zero()
        {
            // If
            var l = new List<int>();

            // When/then
            Len(l).Should().Be(0);
        }

        [Fact]
        public void List_Len_Non_Zero()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            Len(l).Should().Be(4);
        }

        //         [Fact]
        //         public void List_Min_Empty()
        //         {
        //             // If
        //             var l = new List<int>();

        //             // When/then
        //             EXPECT_THROW(Min(l), ValueError);
        //         }

        //         [Fact]
        //         public void List_Min_Non_Empty()
        //         {
        //             // If
        //             List<int> l = [ 5, 7, 3, 1 ];

        //             // When/then
        //             Min(l).Should().Be(1);
        //         }

        //         [Fact]
        //         public void List_Max_Empty()
        //         {
        //             // If
        //             var l = new List<int>();

        //             // When/then
        //             EXPECT_THROW(Max(l), ValueError);
        //         }

        //         [Fact]
        //         public void List_Max_Non_Empty()
        //         {
        //             // If
        //             List<int> l = [ 5, 7, 3, 1 ];

        //             // When/then
        //             Max(l).Should().Be(7);
        //         }

        [Fact]
        public void List_Count_Empty()
        {
            // If
            var l = new List<int>();

            // When/then
            l.Count(1).Should().Be(0);
        }

        [Fact]
        public void List_Count_Zero()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            l.Count(9).Should().Be(0);
        }

        [Fact]
        public void List_Count_Non_Zero()
        {
            // If
            List<int> l = [ 1, 3, 5, 1, 7 ];

            // When/then
            l.Count(1).Should().Be(2);
        }

        //         [Fact]
        //         public void List_Slice_Zero_Step()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 1, 7 ];

        //             // When/then
        //             EXPECT_THROW(l.Slice(0, 0, 0), ValueError);
        //         }

        //         [Fact]
        //         public void List_Slice_Negative_Step()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 1, 7 ];

        //             // When
        //             var actual = l.Slice(0, 1, -1);

        //             // Then
        //             Len(actual).Should().Be(0);
        //         }

        //         [Fact]
        //         public void List_Slice_Same_Start_And_End()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 1, 7 ];

        //             // When
        //             var actual = l.Slice(1, 1);

        //             // Then
        //             Len(actual).Should().Be(0);
        //         }

        //         [Fact]
        //         public void List_Slice_Single_Step()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];

        //             // When
        //             var res = l.Slice(1, 3);

        //             // Then
        //             var actual = res.ToList();
        //             DotNetList<int> expected = [ 3, 5 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Slice_Not_Single_Step_Not_Enough()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];

        //             // When
        //             var res = l.Slice(1, 3, 4);

        //             // Then
        //             var actual = res.ToList();
        //             DotNetList<int> expected = [ 3 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Slice_Not_Single_Step_Enough()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7, 9 ];

        //             // When
        //             var res = l.Slice(1, 5, 2);

        //             // Then
        //             var actual = res.ToList();
        //             DotNetList<int> expected = [ 3, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Slice_Out_Of_Bounds_Left()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7, 9 ];

        //             // When
        //             var res = l.Slice(-9, 4, 2);

        //             // Then
        //             var actual = res.ToList();
        //             DotNetList<int> expected = [ 1, 5 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Slice_Out_Of_Bounds_Right()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7, 9 ];

        //             // When
        //             var res = l.Slice(0, 9, 2);

        //             // Then
        //             var actual = res.ToList();
        //             DotNetList<int> expected = [ 1, 5, 9 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Slice_No_Args_Is_Copy()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7, 9 ];

        //             // When
        //             var res = l.Slice();

        //             // Then
        //             var actual = res.ToList();
        //             DotNetList<int> expected = [ 1, 3, 5, 7, 9 ];

        //             actual.Should().Equal(expected);
        //         }

        // #if __cplusplus >= 202302L
        // [Fact]
        // public void List_Slice_Operator() {
        //   // If
        //   List<int> l = [1, 3, 5, 7, 9];

        //   // When
        //   var res = l[1, 5, 2];

        //   // Then
        //   var actual = res.ToList();
        //   DotNetList<int> expected = [3, 7];

        //   actual.Should().Equal(expected);
        // }

        // [Fact]
        // public void List_Slice_Operator_Object() {
        //   // If
        //   List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
        //                               Int_Wrapper(7), Int_Wrapper(9)};

        //   // When
        //   var res = l[1, 5, 2];

        //   // Then
        //   var actual = res.ToList();
        //   DotNetList<int> expected = [3, 7];

        //   actual.Should().Equal(expected);
        // }

        // [Fact]
        // public void List_Slice_Operator_With_No_Args_Is_Copy() {
        //   // If
        //   List<int> l = [1, 3, 5, 7, 9];

        //   // When
        //   var res = l[];

        //   // Then
        //   var actual = res.ToList();
        //   DotNetList<int> expected = [1, 3, 5, 7, 9];

        //   actual.Should().Equal(expected);
        // }

        // [Fact]
        // public void List_Slice_Operator_With_No_Args_Is_Copy_Object() {
        //   // If
        //   List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
        //                               Int_Wrapper(7), Int_Wrapper(9)};

        //   // When
        //   var res = l[];

        //   // Then
        //   var actual = res.ToList();
        //   DotNetList<int> expected = [1, 3, 5, 7, 9];

        //   actual.Should().Equal(expected);
        // }
        // #endif  // __cplusplus >= 202302L

        [Fact]
        public void List_Reverse_Empty()
        {
            // If
            var l = new List<int>();

            // When
            l.Reverse();

            // Then
            Len(l).Should().Be(0);
        }

        [Fact]
        public void List_Reverse_Non_Empty()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l.Reverse();

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 7, 5, 3, 1 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Reversed_Empty()
        {
            // If
            var l = new List<int>();

            // When
            var reversed = Reversed(l);
            var reversed_list = new List<int>(reversed);

            // Then
            Len(reversed_list).Should().Be(0);
        }

        [Fact]
        public void List_Reversed_Non_Empty()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            var reversed = Reversed(l);
            var reversedList = new List<int>(reversed);

            // Then
            var actual = reversedList.ToList();
            DotNetList<int> expected = [ 7, 5, 3, 1 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Bool_Empty()
        {
            // If
            var l = new List<int>();

            // When/then
            Bool(l).Should().BeFalse();
        }

        [Fact]
        public void List_Bool_Non_Empty()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            Bool(l).Should().BeTrue();
        }

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
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            l.Index(5).Should().Be(2);
        }

        //         [Fact]
        //         public void List_Index_Non_Empty_Object_Equal()
        //         {
        //             // If
        //             List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
        //                               Int_Wrapper(7)};

        //             Int_Wrapper i{ 5}
        //             ;

        //             ASSERT_NE(&i, &l[2]);

        //             // When/then
        //             l.Index(i).Should().Be(2);
        //         }

        //         [Fact]
        //         public void List_Index_Non_Empty_Object_Not_Equal()
        //         {
        //             // If
        //             List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
        //                               Int_Wrapper(7)};

        //             Int_Wrapper i{ 4}
        //             ;

        //             // When/then
        //             EXPECT_THROW(l.Index(i), ValueError);
        //         }

        //         [Fact]
        //         public void List_Index_Non_Empty_Object_Same()
        //         {
        //             // If
        //             List<Int_Identity_Wrapper> l = {
        //       Int_Identity_Wrapper(1), Int_Identity_Wrapper(3), Int_Identity_Wrapper(5),
        //       Int_Identity_Wrapper(7)};

        //             var i = l[2];

        //             ASSERT_EQ(&i, &l[2]);

        //             // When/then
        //             l.Index(i).Should().Be(2);
        //         }

        //         [Fact]
        //         public void List_Index_Non_Empty_Object_Not_Same()
        //         {
        //             // If
        //             List<Int_Identity_Wrapper> l = {
        //       Int_Identity_Wrapper(1), Int_Identity_Wrapper(3), Int_Identity_Wrapper(5),
        //       Int_Identity_Wrapper(7)};

        //             Int_Identity_Wrapper i{ 5}
        //             ;

        //             ASSERT_NE(&i, &l[2]);

        //             // When/then
        //             EXPECT_THROW(l.Index(i), ValueError);
        //         }

        //         [Fact]
        //         public void List_Remove_Empty()
        //         {
        //             // If
        //             var l = new List<int>();

        //             // When/then
        //             EXPECT_THROW(l.Remove(3), ValueError);
        //         }

        //         [Fact]
        //         public void List_Remove_Not_Present()
        //         {
        //             // If
        //             List<int> l = [ 1, 5, 7 ];

        //             // When/then
        //             EXPECT_THROW(l.Remove(3), ValueError);
        //         }

        [Fact]
        public void List_Remove_Present_Once()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l.Remove(3);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 5, 7 ];

            actual.Should().Equal(expected);
        }

        //         [Fact]
        //         public void List_Remove_Present_Once_Object_Equal()
        //         {
        //             // If
        //             List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
        //                         Int_Wrapper(7)};

        //             // When
        //             var second_elem = l[1];
        //             l.Remove(second_elem);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 5, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Remove_Present_Once_Object_Not_Equal()
        //         {
        //             // If
        //             List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
        //                         Int_Wrapper(7)};

        //             Int_Wrapper i{ 4}
        //             ;

        //             // When/then
        //             EXPECT_THROW(l.Remove(i), ValueError);
        //         }

        //         [Fact]
        //         public void List_Remove_Present_Once_Object_Not_Same()
        //         {
        //             // If
        //             List<Int_Identity_Wrapper> l = {Int_Identity_Wrapper(1), Int_Identity_Wrapper(3),
        //                                 Int_Identity_Wrapper(5), Int_Identity_Wrapper(7)};

        //             Int_Identity_Wrapper i{ 3}
        //             ;

        //             ASSERT_NE(&i, &l[1]);

        //             // When/then
        //             EXPECT_THROW(l.Remove(Int_Identity_Wrapper(3)), ValueError);
        //         }

        [Fact]
        public void List_Remove_Present_More_Than_Once()
        {
            // If
            List<int> l = [ 1, 3, 5, 7, 3 ];

            // When
            l.Remove(3);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 5, 7, 3 ];

            actual.Should().Equal(expected);
        }

        //         [Fact]
        //         public void List_Remove_Present_More_Than_Once_Object_Same()
        //         {
        //             // If
        //             List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
        //                         Int_Wrapper(7), Int_Wrapper(3)};

        //             // When
        //             var second_elem = l[1];
        //             l.Remove(second_elem);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 5, 7, 3 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Remove_Present_More_Than_Once_Object_Equality_Last()
        //         {
        //             // If
        //             List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
        //                         Int_Wrapper(7), Int_Wrapper(3)};

        //             // When
        //             var last_elem = l[-1];
        //             l.Remove(last_elem);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 5, 7, 3 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Remove_Present_More_Than_Once_Object_Identity_Last()
        //         {
        //             // If
        //             List<Int_Identity_Wrapper> l = {Int_Identity_Wrapper(1), Int_Identity_Wrapper(3),
        //                                 Int_Identity_Wrapper(5), Int_Identity_Wrapper(7),
        //                                 Int_Identity_Wrapper(3)};

        //             // When
        //             var last_elem = l[-1];

        //             ASSERT_NE(&last_elem, &l[1]);

        //             l.Remove(last_elem);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 3, 5, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Remove_Present_More_Than_Once_Object_Identity_Not_Same()
        //         {
        //             // If
        //             List<Int_Identity_Wrapper> l = {Int_Identity_Wrapper(1), Int_Identity_Wrapper(3),
        //                                 Int_Identity_Wrapper(5), Int_Identity_Wrapper(7),
        //                                 Int_Identity_Wrapper(3)};

        //             // When
        //             EXPECT_THROW(l.Remove(Int_Identity_Wrapper(3)), ValueError);
        //         }

        [Fact]
        public void List_Remove_At_End()
        {
            // If
            List<int> l = [ 1, 5, 7, 3 ];

            // When
            l.Remove(3);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 5, 7 ];

            actual.Should().Equal(expected);
        }

        //         [Fact]
        //         public void List_Remove_At_End_Object_Same()
        //         {
        //             // If
        //             List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(5), Int_Wrapper(7),
        //                         Int_Wrapper(3)};

        //             // When
        //             var last_elem = l[-1];
        //             l.Remove(last_elem);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 5, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Remove_At_End_Object_Equality_Not_Same()
        //         {
        //             // If
        //             List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(5), Int_Wrapper(7),
        //                         Int_Wrapper(3)};

        //             // When
        //             var i = Int_Wrapper(3);

        //             ASSERT_NE(&i, &l[-1]);

        //             l.Remove(i);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 5, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Remove_At_End_Object_Identity_Not_Same()
        //         {
        //             // If
        //             List<Int_Identity_Wrapper> l = {Int_Identity_Wrapper(1), Int_Identity_Wrapper(5),
        //                                 Int_Identity_Wrapper(7), Int_Identity_Wrapper(3)};

        //             Int_Identity_Wrapper i{ 3}
        //             ;

        //             ASSERT_NE(&i, &l[-1]);

        //             // When/then
        //             EXPECT_THROW(l.Remove(i), ValueError);
        //         }

        //         [Fact]
        //         public void List_Delete_Slice_Zero_Step()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 1, 7 ];

        //             // When/then
        //             EXPECT_THROW(l.Delete_Slice(0, 0, 0), ValueError);
        //         }

        //         [Fact]
        //         public void List_Delete_Slice_Negative_Step()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 1, 7 ];

        //             // When
        //             l.Delete_Slice(0, 1, -1);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 3, 5, 1, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Delete_Slice_Same_Start_And_End()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 1, 7 ];

        //             // When
        //             l.Delete_Slice(1, 1);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 3, 5, 1, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Delete_Slice_Single_Step()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];

        //             // When
        //             l.Delete_Slice(1, 3);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Delete_Slice_Not_Single_Step_Not_Enough()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];

        //             // When
        //             l.Delete_Slice(1, 3, 4);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 5, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Delete_Slice_Not_Single_Step_Enough()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7, 9 ];

        //             // When
        //             l.Delete_Slice(1, 5, 2);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 5, 9 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Delete_Slice_Out_Of_Bounds_Left()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7, 9 ];

        //             // When
        //             l.Delete_Slice(-9, 4, 2);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 3, 7, 9 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Delete_Slice_Out_Of_Bounds_Right()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7, 9 ];

        //             // When
        //             l.Delete_Slice(0, 9, 2);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 3, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Delete_Slice_No_Args_Is_Clear()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7, 9 ];

        //             // When
        //             l.Delete_Slice();

        //             // Then
        //             Len(l).Should().Be(0);
        //         }

        //         [Fact]
        //         public void List_Replace_Slice_Zero_Step()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 1, 7 ];
        //             List<int> other = [ 2, 4, 6 ];

        //             // When/then
        //             EXPECT_THROW(l.Replace_Slice(other, 0, 0, 0), ValueError);
        //         }

        //         [Fact]
        //         public void List_Replace_Slice_Negative_Step()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 1, 7 ];
        //             List<int> other = [ 2, 4, 6 ];

        //             // When
        //             l.Replace_Slice(other, 0, 1, -1);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 3, 5, 1, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Replace_Slice_Same_Start_And_End()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 1, 7 ];
        //             List<int> other = [ 2, 4, 6 ];

        //             // When
        //             l.Replace_Slice(other, 1, 1);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 3, 5, 1, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Replace_Slice_Single_Step_More_New_Elems()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             List<int> other = [ 2, 4, 6 ];

        //             // When
        //             l.Replace_Slice(other, 1, 3);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 2, 4, 6, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Replace_Slice_Single_Step_Less_New_Elems()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             List<int> other = [ 2 ];

        //             // When
        //             l.Replace_Slice(other, 1, 3);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 2, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Replace_Slice_Single_Step_Same_New_Elems()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             List<int> other = [ 2, 4 ];

        //             // When
        //             l.Replace_Slice(other, 1, 3);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 2, 4, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Replace_Slice_Not_Single_Step_Not_Same_Num_Elems()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             List<int> other = [ 2, 4, 6 ];

        //             // When/then
        //             EXPECT_THROW(l.Replace_Slice(other, 1, 3, 4), ValueError);
        //         }

        //         [Fact]
        //         public void List_Replace_Slice_Not_Single_Step_Same_Num_Elems()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7, 9 ];
        //             List<int> other = [ 2, 4 ];

        //             // When
        //             l.Replace_Slice(other, 1, 4, 2);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 2, 5, 4, 9 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Replace_Slice_No_Args_Is_Complete_Replacement()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7, 9 ];
        //             List<int> other = [ 2, 4, 6 ];

        //             // When
        //             l.Replace_Slice(other);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 2, 4, 6 ];

        //             actual.Should().Equal(expected);
        //         }

        [Fact]
        public void List_Insert_Into_Empty()
        {
            // If
            var l = new List<int>();

            // When
            l.Insert(0, 5);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 5 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty()
        {
            // If
            List<int> l = [ 1, 3, 7 ];

            // When
            l.Insert(1, 5);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 5, 3, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty_Beyond_Left_Bound()
        {
            // If
            List<int> l = [ 1, 3, 7 ];

            // When
            l.Insert(-100, 5);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 5, 1, 3, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty_Beyond_Right_Bound()
        {
            // If
            List<int> l = [ 1, 3, 7 ];

            // When
            l.Insert(100, 5);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 3, 7, 5 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty_At_Left_Bound()
        {
            // If
            List<int> l = [ 1, 3, 7 ];

            // When
            l.Insert(0, 5);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 5, 1, 3, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty_Before_Right_Bound()
        {
            // If
            List<int> l = [ 1, 3, 7 ];

            // When
            l.Insert(-1, 5);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 3, 5, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty_At_Right_Bound()
        {
            // If
            List<int> l = [ 1, 3, 7 ];

            // When
            l.Insert(3, 5);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 3, 7, 5 ];

            actual.Should().Equal(expected);
        }

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
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l.Pop();

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 3, 5 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Pop_Front()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l.Pop(0);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 3, 5, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Pop_Middle()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l.Pop(1);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 5, 7 ];

            actual.Should().Equal(expected);
        }

        [Fact]
        public void List_Pop_Negative()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When
            l.Pop(-2);

            // Then
            var actual = l.ToList();
            DotNetList<int> expected = [ 1, 3, 7 ];

            actual.Should().Equal(expected);
        }

        //         [Fact]
        //         public void List_Pop_Out_Of_Bounds_Left()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];

        //             // When/then
        //             EXPECT_THROW(l.Pop(-100), IndexError);
        //         }

        //         [Fact]
        //         public void List_Pop_Out_Of_Bounds_Right()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];

        //             // When/then
        //             EXPECT_THROW(l.Pop(100), IndexError);
        //         }

        //         [Fact]
        //         public void List_Native_Iteration()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             var expected = l.ToList();

        //             // When
        //             DotNetList<int> actual;

        //             for (var elem : l) {
        //                 actual.emplace_back(elem);
        //             }

        //             // Then
        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Iterator_Iteration()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             var expected = l.ToList();
        //             var it = Iter(l);

        //             // When
        //             DotNetList<int> actual;

        //             for (var elem : it) {
        //                 actual.emplace_back(elem);
        //             }

        //             // Then
        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Equality_Same_Object()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             var copy = l;
        //             ASSERT_NE(&l, &copy);

        //             // When/then
        //             l.Should().Equal(copy);
        //         }

        //         [Fact]
        //         public void List_Native_Equality_Same_Object()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             var copy = l;
        //             ASSERT_NE(&l, &copy);

        //             // When/then
        //             l.Should().Equal(copy);
        //         }

        //         [Fact]
        //         public void List_Native_In_Equality_Same_Object()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             var copy = l;
        //             ASSERT_NE(&l, &copy);

        //             // When/then
        //             l != copy.Should().BeFalse();
        //         }

        //         [Fact]
        //         public void List_Equality_Different_Object()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             List<int> m = [ 1, 3, 5, 7, 9 ];
        //             ASSERT_NE(&l, &m);

        //             // When/then
        //             l.Should().NotEqual(m);

        //             // When
        //             m.Pop();

        //             // Then
        //             l.Should().Equal(m);
        //         }

        //         [Fact]
        //         public void List_Native_Equality_And_Inequality_Different_Object()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             List<int> m = [ 1, 3, 5, 7, 9 ];

        //             // When/then
        //             l.Should().NotEqual(m);

        //             // When
        //             m.Pop();

        //             // Then
        //             l.Should().Equal(m);
        //         }

        //         [Fact]
        //         public void List_Native_Equality_Different_Type()
        //         {
        //             // If
        //             List<int> l = [ 1, 3, 5, 7 ];
        //             List<float> m = [ 1.0, 3.0, 5.0, 7.0 ];

        //             // When/then
        //             l.Should().NotEqual(m);
        //         }

        [Fact]
        public void List_Str_Empty()
        {
            // If
            var l = new List<int>();

            // When/then
            Str(l).Should().Be("[]");
        }

        [Fact]
        public void List_Str_Not_Empty()
        {
            // If
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            Str(l).Should().Be("[1, 3, 5, 7]");
        }

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
            List<int> l = [ 1, 3, 5, 7 ];

            // When/then
            Repr(l).Should().Be("[1, 3, 5, 7]");
        }

        //         [Fact]
        //         public void List_Sort()
        //         {
        //             // If
        //             List<int> l = [ 7, 3, 1, 1, 5 ];

        //             // When
        //             l.Sort();

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 1, 3, 5, 7 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Sort_Reverse()
        //         {
        //             // If
        //             List<int> l = [ 7, 3, 1, 1, 5 ];

        //             // When
        //             l.Sort(true);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 7, 5, 3, 1, 1 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Sort_With_Key()
        //         {
        //             // If
        //             List<int> l = [ 7, 3, 1, 1, 5 ];

        //             // This effectively inverts the sort
        //             var key = [](var int i) -> float {
        //                 return 1.0 / static_cast<float>(i);
        //             }
        //             ;

        //             // When
        //             l.Sort(key);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 7, 5, 3, 1, 1 ];

        //             actual.Should().Equal(expected);
        //         }

        //         [Fact]
        //         public void List_Sort_With_Key_And_Reverse()
        //         {
        //             // If
        //             List<int> l = [ 7, 3, 1, 1, 5 ];

        //             // This effectively inverts the sort, but the reverse reverses it again
        //             var key = [](var int i) -> float {
        //                 return 1.0 / static_cast<float>(i);
        //             }
        //             ;

        //             // When
        //             l.Sort(key, true);

        //             // Then
        //             var actual = l.ToList();
        //             DotNetList<int> expected = [ 1, 1, 3, 5, 7 ];

        //             actual.Should().Equal(expected);
        //         }
    }
}
