using Sharpy;
using Xunit;
using Fluent_Assertions;

namespace Sharpy.Tests
{
    public class List_Tests
    {
        [Fact]
        public void List_Should_Be_Empty_On_Initialization()
        {
            // If
            var l = new List<int>();

            // When
            uint length = l.Len();

            // Then
            length.Should().Be(0);
        }

        [Fact]
        public void List_Empty_Constructor()
        {
            // If/when
            var l = new List<int>();

            // Then
            EXPECT_EQ(Len(l), 0);
        }

        [Fact]
        public void List_Variadic_Constructor()
        {
            // If/when
            const auto l = new List<int>(1, 3, 5, 7);

            // Then
            ASSERT_EQ(Len(l), 4);

            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Initializer_List_Constructor()
        {
            // If/when
            const List<int> l = { 1, 3, 5, 7 };

            // Then
            ASSERT_EQ(Len(l), 4);

            const auto actual = as_vector<int>(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Iterable_Constructor()
        {
            // If/when
            const List<int> source = { 1, 3, 5, 7 };
            const List<int> l = Iter(source);

            // Then
            ASSERT_EQ(Len(l), 4);

            const auto actual = as_vector<int>(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Append_One_Element()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l.Append(9);

            // Then
            ASSERT_EQ(Len(l), 5);

            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7, 9 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Append_Variadic_Elements()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l.Append(9, 11, 13);

            // Then
            ASSERT_EQ(Len(l), 7);

            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7, 9, 11, 13 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Contains_Empty()
        {
            // If
            const List<int> l;

            // When/then
            EXPECT_FALSE(Contains(l, 1));
        }

        [Fact]
        public void List_Contains_Not_Actually_In()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_FALSE(Contains(l, 4));
        }

        [Fact]
        public void List_Contains_Actually_In()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_TRUE(Contains(l, 5));
        }

        [Fact]
        public void List_Clear_Empty()
        {
            // If
            List<int> l;

            // When
            l.Clear();

            // Then
            EXPECT_EQ(Len(l), 0);
        }

        [Fact]
        public void List_Clear_Non_Empty()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l.Clear();

            // Then
            EXPECT_EQ(Len(l), 0);
        }

        [Fact]
        public void List_Copy_Empty()
        {
            // If
            const List<int> l;

            // When
            auto copy = l.Copy();
            copy.Append(5);

            // Then
            EXPECT_NE(l, copy);
            EXPECT_NE(Len(l), Len(copy));
        }

        [Fact]
        public void List_Copy_Non_Empty()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When
            auto copy = l.Copy();
            copy.Append(9);

            // Then
            const auto actual_l_items = as_vector(l);
            const System.Collections.Generic.List<int> expected_l_items = { 1, 3, 5, 7 };
            EXPECT_EQ(actual_l_items, expected_l_items);

            const auto actual_copy_items = as_vector<int>(copy);
            const System.Collections.Generic.List<int> expected_copy_items = { 1, 3, 5, 7, 9 };
            EXPECT_EQ(actual_copy_items, expected_copy_items);
        }

        [Fact]
        public void List_Extend_Empty_And_Empty_Other()
        {
            // If
            List<int> l;
            const List<int> other;

            // When
            l.Extend(other);

            // Then
            EXPECT_EQ(Len(l), 0);
        }

        [Fact]
        public void List_Extend_Empty_And_Non_Empty_Other()
        {
            // If
            List<int> l;
            const List<int> other = { 1, 3, 5, 7 };

            // When
            l.Extend(other);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Extend_Non_Empty_And_Non_Empty_Other()
        {
            // If
            List<int> l = { 9, 11, 13 };
            const List<int> other = { 1, 3, 5, 7 };

            // When
            l.Extend(other);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 9, 11, 13, 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Addition_Assignment_Operator()
        {
            // If
            List<int> l = { 9, 11, 13 };
            const List<int> other = { 1, 3, 5, 7 };

            // When
            l += other;

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 9, 11, 13, 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Addition_Operator()
        {
            // If
            const List<int> l = { 9, 11, 13 };
            const List<int> other = { 1, 3, 5, 7 };

            // When
            const auto sum = l + other;

            // Then
            const auto actual = as_vector(sum);
            const System.Collections.Generic.List<int> expected = { 9, 11, 13, 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Multiplication_Operator_Negative()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When
            const auto product = l * -1;

            // Then
            EXPECT_EQ(Len(product), 0);
        }

        [Fact]
        public void List_Multiplication_Operator_Zero()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When
            const auto product = l * 0;

            // Then
            EXPECT_EQ(Len(product), 0);
        }

        [Fact]
        public void List_Multiplication_Operator_One()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When
            const auto product = l * 1;

            // Then
            const auto actual = as_vector(product);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Multiplication_Operator_More_Than_One()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When
            const auto product = l * 3;

            // Then
            const auto actual = as_vector(product);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7, 1, 3, 5, 7, 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Multiplication_Assignment_Operator_Negative()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l *= -1;

            // Then
            EXPECT_EQ(Len(l), 0);
        }

        [Fact]
        public void List_Multiplication_Assignment_Operator_Zero()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l *= 0;

            // Then
            EXPECT_EQ(Len(l), 0);
        }

        [Fact]
        public void List_Multiplication_Assignment_Operator_One()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l *= 1;

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Multiplication_Assignment_Operator_More_Than_One()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l *= 3;

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7, 1, 3, 5, 7, 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Get_By_Positive_Index()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_EQ(l[0], 1);
            EXPECT_EQ(l[1], 3);
            EXPECT_EQ(l[2], 5);
            EXPECT_EQ(l[3], 7);
        }

        [Fact]
        public void List_Get_By_Negative_Index()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_EQ(l[-1], 7);
            EXPECT_EQ(l[-2], 5);
            EXPECT_EQ(l[-3], 3);
            EXPECT_EQ(l[-4], 1);
        }

        [Fact]
        public void List_Get_By_Out_Of_Bounds()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_THROW(l[-5], Index_Error);
            EXPECT_THROW(l[4], Index_Error);
        }

        [Fact]
        public void List_Set_By_Positive_Index()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l[2] = 6;

            // Then
            EXPECT_EQ(l[0], 1);
            EXPECT_EQ(l[1], 3);
            EXPECT_EQ(l[2], 6);
            EXPECT_EQ(l[3], 7);
        }

        [Fact]
        public void List_Set_By_Negative_Index()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l[-3] = 4;

            // Then
            EXPECT_EQ(l[-1], 7);
            EXPECT_EQ(l[-2], 5);
            EXPECT_EQ(l[-3], 4);
            EXPECT_EQ(l[-4], 1);
        }

        [Fact]
        public void List_Set_By_Out_Of_Bounds()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_THROW({ l[-5] = 9; }, Index_Error);
            EXPECT_THROW({ l[4] = 11; }, Index_Error);
        }

        [Fact]
        public void List_Len_Zero()
        {
            // If
            const List<int> l;

            // When/then
            EXPECT_EQ(Len(l), 0);
        }

        [Fact]
        public void List_Len_Non_Zero()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_EQ(Len(l), 4);
        }

        [Fact]
        public void List_Min_Empty()
        {
            // If
            const List<int> l;

            // When/then
            EXPECT_THROW(Min(l), Value_Error);
        }

        [Fact]
        public void List_Min_Non_Empty()
        {
            // If
            const List<int> l = { 5, 7, 3, 1 };

            // When/then
            EXPECT_EQ(Min(l), 1);
        }

        [Fact]
        public void List_Max_Empty()
        {
            // If
            const List<int> l;

            // When/then
            EXPECT_THROW(Max(l), Value_Error);
        }

        [Fact]
        public void List_Max_Non_Empty()
        {
            // If
            const List<int> l = { 5, 7, 3, 1 };

            // When/then
            EXPECT_EQ(Max(l), 7);
        }

        [Fact]
        public void List_Count_Empty()
        {
            // If
            const List<int> l;

            // When/then
            EXPECT_EQ(l.Count(1), 0);
        }

        [Fact]
        public void List_Count_Zero()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_EQ(l.Count(9), 0);
        }

        [Fact]
        public void List_Count_Non_Zero()
        {
            // If
            const List<int> l = { 1, 3, 5, 1, 7 };

            // When/then
            EXPECT_EQ(l.Count(1), 2);
        }

        [Fact]
        public void List_Slice_Zero_Step()
        {
            // If
            const List<int> l = { 1, 3, 5, 1, 7 };

            // When/then
            EXPECT_THROW(l.Slice(0, 0, 0), Value_Error);
        }

        [Fact]
        public void List_Slice_Negative_Step()
        {
            // If
            const List<int> l = { 1, 3, 5, 1, 7 };

            // When
            const auto actual = l.Slice(0, 1, -1);

            // Then
            EXPECT_EQ(Len(actual), 0);
        }

        [Fact]
        public void List_Slice_Same_Start_And_End()
        {
            // If
            const List<int> l = { 1, 3, 5, 1, 7 };

            // When
            const auto actual = l.Slice(1, 1);

            // Then
            EXPECT_EQ(Len(actual), 0);
        }

        [Fact]
        public void List_Slice_Single_Step()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When
            const auto res = l.Slice(1, 3);

            // Then
            const auto actual = as_vector(res);
            const System.Collections.Generic.List<int> expected = { 3, 5 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Slice_Not_Single_Step_Not_Enough()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When
            const auto res = l.Slice(1, 3, 4);

            // Then
            const auto actual = as_vector(res);
            const System.Collections.Generic.List<int> expected = { 3 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Slice_Not_Single_Step_Enough()
        {
            // If
            const List<int> l = { 1, 3, 5, 7, 9 };

            // When
            const auto res = l.Slice(1, 5, 2);

            // Then
            const auto actual = as_vector(res);
            const System.Collections.Generic.List<int> expected = { 3, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Slice_Out_Of_Bounds_Left()
        {
            // If
            const List<int> l = { 1, 3, 5, 7, 9 };

            // When
            const auto res = l.Slice(-9, 4, 2);

            // Then
            const auto actual = as_vector(res);
            const System.Collections.Generic.List<int> expected = { 1, 5 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Slice_Out_Of_Bounds_Right()
        {
            // If
            const List<int> l = { 1, 3, 5, 7, 9 };

            // When
            const auto res = l.Slice(0, 9, 2);

            // Then
            const auto actual = as_vector(res);
            const System.Collections.Generic.List<int> expected = { 1, 5, 9 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Slice_No_Args_Is_Copy()
        {
            // If
            const List<int> l = { 1, 3, 5, 7, 9 };

            // When
            const auto res = l.Slice();

            // Then
            const auto actual = as_vector(res);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7, 9 };

            EXPECT_EQ(actual, expected);
        }

#if __cplusplus >= 202302L
[Fact]
public void List_Slice_Operator() {
  // If
  const List<int> l = {1, 3, 5, 7, 9};

  // When
  const auto res = l[1, 5, 2];

  // Then
  const auto actual = as_vector(res);
  const System.Collections.Generic.List<int> expected = {3, 7};

  EXPECT_EQ(actual, expected);
}

[Fact]
public void List_Slice_Operator_Object() {
  // If
  const List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
                              Int_Wrapper(7), Int_Wrapper(9)};

  // When
  const auto res = l[1, 5, 2];

  // Then
  const auto actual = as_vector<Int_Wrapper>(res);
  const System.Collections.Generic.List<int> expected = {3, 7};

  EXPECT_EQ(actual, expected);
}

[Fact]
public void List_Slice_Operator_With_No_Args_Is_Copy() {
  // If
  const List<int> l = {1, 3, 5, 7, 9};

  // When
  const auto res = l[];

  // Then
  const auto actual = as_vector(res);
  const System.Collections.Generic.List<int> expected = {1, 3, 5, 7, 9};

  EXPECT_EQ(actual, expected);
}

[Fact]
public void List_Slice_Operator_With_No_Args_Is_Copy_Object() {
  // If
  const List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
                              Int_Wrapper(7), Int_Wrapper(9)};

  // When
  const auto res = l[];

  // Then
  const auto actual = as_vector<Int_Wrapper>(res);
  const System.Collections.Generic.List<int> expected = {1, 3, 5, 7, 9};

  EXPECT_EQ(actual, expected);
}
#endif  // __cplusplus >= 202302L

        [Fact]
        public void List_Reverse_Empty()
        {
            // If
            List<int> l;

            // When
            l.Reverse();

            // Then
            EXPECT_EQ(Len(l), 0);
        }

        [Fact]
        public void List_Reverse_Non_Empty()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l.Reverse();

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 7, 5, 3, 1 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Reversed_Empty()
        {
            // If
            List<int> l;

            // When
            auto reversed = Reverse(l);
            const List<int> reversed_list(reversed);

            // Then
            EXPECT_EQ(Len(reversed_list), 0);
        }

        [Fact]
        public void List_Reversed_Non_Empty()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            auto reversed = Reverse(l);
            const List<int> reversed_list(reversed);

            // Then
            const auto actual = as_vector(reversed_list);
            const System.Collections.Generic.List<int> expected = { 7, 5, 3, 1 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Bool_Empty()
        {
            // If
            const List<int> l;

            // When/then
            EXPECT_FALSE(Bool(l));
        }

        [Fact]
        public void List_Bool_Non_Empty()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_TRUE(Bool(l));
        }

        [Fact]
        public void List_Index_Empty()
        {
            // If
            const List<int> l;

            // When/then
            EXPECT_THROW(l.Index(5), Value_Error);
        }

        [Fact]
        public void List_Index_Non_Empty()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_EQ(l.Index(5), 2);
        }

        [Fact]
        public void List_Index_Non_Empty_Object_Equal()
        {
            // If
            const List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
                              Int_Wrapper(7)};

            const Int_Wrapper i{ 5}
            ;

            ASSERT_NE(&i, &l[2]);

            // When/then
            EXPECT_EQ(l.Index(i), 2);
        }

        [Fact]
        public void List_Index_Non_Empty_Object_Not_Equal()
        {
            // If
            const List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
                              Int_Wrapper(7)};

            const Int_Wrapper i{ 4}
            ;

            // When/then
            EXPECT_THROW(l.Index(i), Value_Error);
        }

        [Fact]
        public void List_Index_Non_Empty_Object_Same()
        {
            // If
            const List<Int_Identity_Wrapper> l = {
      Int_Identity_Wrapper(1), Int_Identity_Wrapper(3), Int_Identity_Wrapper(5),
      Int_Identity_Wrapper(7)};

            const auto i = l[2];

            ASSERT_EQ(&i, &l[2]);

            // When/then
            EXPECT_EQ(l.Index(i), 2);
        }

        [Fact]
        public void List_Index_Non_Empty_Object_Not_Same()
        {
            // If
            const List<Int_Identity_Wrapper> l = {
      Int_Identity_Wrapper(1), Int_Identity_Wrapper(3), Int_Identity_Wrapper(5),
      Int_Identity_Wrapper(7)};

            const Int_Identity_Wrapper i{ 5}
            ;

            ASSERT_NE(&i, &l[2]);

            // When/then
            EXPECT_THROW(l.Index(i), Value_Error);
        }

        [Fact]
        public void List_Remove_Empty()
        {
            // If
            List<int> l;

            // When/then
            EXPECT_THROW(l.Remove(3), Value_Error);
        }

        [Fact]
        public void List_Remove_Not_Present()
        {
            // If
            List<int> l = { 1, 5, 7 };

            // When/then
            EXPECT_THROW(l.Remove(3), Value_Error);
        }

        [Fact]
        public void List_Remove_Present_Once()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l.Remove(3);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Remove_Present_Once_Object_Equal()
        {
            // If
            List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
                        Int_Wrapper(7)};

            // When
            const auto second_elem = l[1];
            l.Remove(second_elem);

            // Then
            const auto actual = as_vector<Int_Wrapper>(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Remove_Present_Once_Object_Not_Equal()
        {
            // If
            List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
                        Int_Wrapper(7)};

            const Int_Wrapper i{ 4}
            ;

            // When/then
            EXPECT_THROW(l.Remove(i), Value_Error);
        }

        [Fact]
        public void List_Remove_Present_Once_Object_Not_Same()
        {
            // If
            List<Int_Identity_Wrapper> l = {Int_Identity_Wrapper(1), Int_Identity_Wrapper(3),
                                Int_Identity_Wrapper(5), Int_Identity_Wrapper(7)};

            const Int_Identity_Wrapper i{ 3}
            ;

            ASSERT_NE(&i, &l[1]);

            // When/then
            EXPECT_THROW(l.Remove(Int_Identity_Wrapper(3)), Value_Error);
        }

        [Fact]
        public void List_Remove_Present_More_Than_Once()
        {
            // If
            List<int> l = { 1, 3, 5, 7, 3 };

            // When
            l.Remove(3);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 7, 3 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Remove_Present_More_Than_Once_Object_Same()
        {
            // If
            List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
                        Int_Wrapper(7), Int_Wrapper(3)};

            // When
            const auto second_elem = l[1];
            l.Remove(second_elem);

            // Then
            const auto actual = as_vector<Int_Wrapper>(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 7, 3 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Remove_Present_More_Than_Once_Object_Equality_Last()
        {
            // If
            List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(3), Int_Wrapper(5),
                        Int_Wrapper(7), Int_Wrapper(3)};

            // When
            const auto last_elem = l[-1];
            l.Remove(last_elem);

            // Then
            const auto actual = as_vector<Int_Wrapper>(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 7, 3 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Remove_Present_More_Than_Once_Object_Identity_Last()
        {
            // If
            List<Int_Identity_Wrapper> l = {Int_Identity_Wrapper(1), Int_Identity_Wrapper(3),
                                Int_Identity_Wrapper(5), Int_Identity_Wrapper(7),
                                Int_Identity_Wrapper(3)};

            // When
            const auto last_elem = l[-1];

            ASSERT_NE(&last_elem, &l[1]);

            l.Remove(last_elem);

            // Then
            const auto actual = as_vector<Int_Identity_Wrapper>(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Remove_Present_More_Than_Once_Object_Identity_Not_Same()
        {
            // If
            List<Int_Identity_Wrapper> l = {Int_Identity_Wrapper(1), Int_Identity_Wrapper(3),
                                Int_Identity_Wrapper(5), Int_Identity_Wrapper(7),
                                Int_Identity_Wrapper(3)};

            // When
            EXPECT_THROW(l.Remove(Int_Identity_Wrapper(3)), Value_Error);
        }

        [Fact]
        public void List_Remove_At_End()
        {
            // If
            List<int> l = { 1, 5, 7, 3 };

            // When
            l.Remove(3);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Remove_At_End_Object_Same()
        {
            // If
            List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(5), Int_Wrapper(7),
                        Int_Wrapper(3)};

            // When
            const auto last_elem = l[-1];
            l.Remove(last_elem);

            // Then
            const auto actual = as_vector<Int_Wrapper>(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Remove_At_End_Object_Equality_Not_Same()
        {
            // If
            List<Int_Wrapper> l = {Int_Wrapper(1), Int_Wrapper(5), Int_Wrapper(7),
                        Int_Wrapper(3)};

            // When
            const auto i = Int_Wrapper(3);

            ASSERT_NE(&i, &l[-1]);

            l.Remove(i);

            // Then
            const auto actual = as_vector<Int_Wrapper>(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Remove_At_End_Object_Identity_Not_Same()
        {
            // If
            List<Int_Identity_Wrapper> l = {Int_Identity_Wrapper(1), Int_Identity_Wrapper(5),
                                Int_Identity_Wrapper(7), Int_Identity_Wrapper(3)};

            const Int_Identity_Wrapper i{ 3}
            ;

            ASSERT_NE(&i, &l[-1]);

            // When/then
            EXPECT_THROW(l.Remove(i), Value_Error);
        }

        [Fact]
        public void List_Delete_Slice_Zero_Step()
        {
            // If
            List<int> l = { 1, 3, 5, 1, 7 };

            // When/then
            EXPECT_THROW(l.Delete_Slice(0, 0, 0), Value_Error);
        }

        [Fact]
        public void List_Delete_Slice_Negative_Step()
        {
            // If
            List<int> l = { 1, 3, 5, 1, 7 };

            // When
            l.Delete_Slice(0, 1, -1);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 1, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Delete_Slice_Same_Start_And_End()
        {
            // If
            List<int> l = { 1, 3, 5, 1, 7 };

            // When
            l.Delete_Slice(1, 1);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 1, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Delete_Slice_Single_Step()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l.Delete_Slice(1, 3);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Delete_Slice_Not_Single_Step_Not_Enough()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l.Delete_Slice(1, 3, 4);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Delete_Slice_Not_Single_Step_Enough()
        {
            // If
            List<int> l = { 1, 3, 5, 7, 9 };

            // When
            l.Delete_Slice(1, 5, 2);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 9 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Delete_Slice_Out_Of_Bounds_Left()
        {
            // If
            List<int> l = { 1, 3, 5, 7, 9 };

            // When
            l.Delete_Slice(-9, 4, 2);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 3, 7, 9 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Delete_Slice_Out_Of_Bounds_Right()
        {
            // If
            List<int> l = { 1, 3, 5, 7, 9 };

            // When
            l.Delete_Slice(0, 9, 2);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 3, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Delete_Slice_No_Args_Is_Clear()
        {
            // If
            List<int> l = { 1, 3, 5, 7, 9 };

            // When
            l.Delete_Slice();

            // Then
            EXPECT_EQ(Len(l), 0);
        }

        [Fact]
        public void List_Replace_Slice_Zero_Step()
        {
            // If
            List<int> l = { 1, 3, 5, 1, 7 };
            const List<int> other = { 2, 4, 6 };

            // When/then
            EXPECT_THROW(l.Replace_Slice(other, 0, 0, 0), Value_Error);
        }

        [Fact]
        public void List_Replace_Slice_Negative_Step()
        {
            // If
            List<int> l = { 1, 3, 5, 1, 7 };
            const List<int> other = { 2, 4, 6 };

            // When
            l.Replace_Slice(other, 0, 1, -1);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 1, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Replace_Slice_Same_Start_And_End()
        {
            // If
            List<int> l = { 1, 3, 5, 1, 7 };
            const List<int> other = { 2, 4, 6 };

            // When
            l.Replace_Slice(other, 1, 1);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 1, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Replace_Slice_Single_Step_More_New_Elems()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };
            const List<int> other = { 2, 4, 6 };

            // When
            l.Replace_Slice(other, 1, 3);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 2, 4, 6, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Replace_Slice_Single_Step_Less_New_Elems()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };
            const List<int> other = { 2 };

            // When
            l.Replace_Slice(other, 1, 3);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 2, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Replace_Slice_Single_Step_Same_New_Elems()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };
            const List<int> other = { 2, 4 };

            // When
            l.Replace_Slice(other, 1, 3);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 2, 4, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Replace_Slice_Not_Single_Step_Not_Same_Num_Elems()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };
            const List<int> other = { 2, 4, 6 };

            // When/then
            EXPECT_THROW(l.Replace_Slice(other, 1, 3, 4), Value_Error);
        }

        [Fact]
        public void List_Replace_Slice_Not_Single_Step_Same_Num_Elems()
        {
            // If
            List<int> l = { 1, 3, 5, 7, 9 };
            const List<int> other = { 2, 4 };

            // When
            l.Replace_Slice(other, 1, 4, 2);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 2, 5, 4, 9 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Replace_Slice_No_Args_Is_Complete_Replacement()
        {
            // If
            List<int> l = { 1, 3, 5, 7, 9 };
            const List<int> other = { 2, 4, 6 };

            // When
            l.Replace_Slice(other);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 2, 4, 6 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Insert_Into_Empty()
        {
            // If
            List<int> l;

            // When
            l.Insert(0, 5);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 5 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty()
        {
            // If
            List<int> l = { 1, 3, 7 };

            // When
            l.Insert(1, 5);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 3, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty_Beyond_Left_Bound()
        {
            // If
            List<int> l = { 1, 3, 7 };

            // When
            l.Insert(-100, 5);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 5, 1, 3, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty_Beyond_Right_Bound()
        {
            // If
            List<int> l = { 1, 3, 7 };

            // When
            l.Insert(100, 5);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 7, 5 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty_At_Left_Bound()
        {
            // If
            List<int> l = { 1, 3, 7 };

            // When
            l.Insert(0, 5);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 5, 1, 3, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty_Before_Right_Bound()
        {
            // If
            List<int> l = { 1, 3, 7 };

            // When
            l.Insert(-1, 5);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Insert_Into_Non_Empty_At_Right_Bound()
        {
            // If
            List<int> l = { 1, 3, 7 };

            // When
            l.Insert(3, 5);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 7, 5 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Pop_Empty()
        {
            // If
            List<int> l;

            // When/then
            EXPECT_THROW(l.Pop(), Index_Error);
        }

        [Fact]
        public void List_Pop_Last()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l.Pop();

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 5 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Pop_Front()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l.Pop(0);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Pop_Middle()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l.Pop(1);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Pop_Negative()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When
            l.Pop(-2);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 3, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Pop_Out_Of_Bounds_Left()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_THROW(l.Pop(-100), Index_Error);
        }

        [Fact]
        public void List_Pop_Out_Of_Bounds_Right()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_THROW(l.Pop(100), Index_Error);
        }

        [Fact]
        public void List_Native_Iteration()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };
            const auto expected = as_vector(l);

            // When
            System.Collections.Generic.List<int> actual;

            for (const auto elem : l) {
                actual.emplace_back(elem);
            }

            // Then
            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Iterator_Iteration()
        {
            // If
            List<int> l = { 1, 3, 5, 7 };
            const auto expected = as_vector(l);
            const auto it = Iter(l);

            // When
            System.Collections.Generic.List<int> actual;

            for (const auto elem : it) {
                actual.emplace_back(elem);
            }

            // Then
            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Equality_Same_Object()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };
            const auto copy = l;
            ASSERT_NE(&l, &copy);

            // When/then
            EXPECT_EQ(l, copy);
        }

        [Fact]
        public void List_Native_Equality_Same_Object()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };
            const auto copy = l;
            ASSERT_NE(&l, &copy);

            // When/then
            EXPECT_EQ(l, copy);
        }

        [Fact]
        public void List_Native_In_Equality_Same_Object()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };
            const auto copy = l;
            ASSERT_NE(&l, &copy);

            // When/then
            EXPECT_FALSE(l != copy);
        }

        [Fact]
        public void List_Equality_Different_Object()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };
            List<int> m = { 1, 3, 5, 7, 9 };
            ASSERT_NE(&l, &m);

            // When/then
            EXPECT_NE(l, m);

            // When
            m.Pop();

            // Then
            EXPECT_EQ(l, m);
        }

        [Fact]
        public void List_Native_Equality_And_Inequality_Different_Object()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };
            List<int> m = { 1, 3, 5, 7, 9 };

            // When/then
            EXPECT_NE(l, m);

            // When
            m.Pop();

            // Then
            EXPECT_EQ(l, m);
        }

        [Fact]
        public void List_Native_Equality_Different_Type()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };
            const List<float> m = { 1.0, 3.0, 5.0, 7.0 };

            // When/then
            EXPECT_NE(l, m);
        }

        [Fact]
        public void List_As_Str_Empty()
        {
            // If
            const List<int> l;

            // When/then
            EXPECT_EQ(Str(l), "[]");
        }

        [Fact]
        public void List_As_Str_Not_Empty()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_EQ(Str(l), "[1, 3, 5, 7]");
        }

        [Fact]
        public void List_Repr_Empty()
        {
            // If
            const List<int> l;

            // When/then
            EXPECT_EQ(Repr(l), "[]");
        }

        [Fact]
        public void List_Repr_Not_Empty()
        {
            // If
            const List<int> l = { 1, 3, 5, 7 };

            // When/then
            EXPECT_EQ(Repr(l), "[1, 3, 5, 7]");
        }

        [Fact]
        public void List_Sort()
        {
            // If
            List<int> l = { 7, 3, 1, 1, 5 };

            // When
            l.Sort();

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Sort_Reverse()
        {
            // If
            List<int> l = { 7, 3, 1, 1, 5 };

            // When
            l.Sort(true);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 7, 5, 3, 1, 1 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Sort_With_Key()
        {
            // If
            List<int> l = { 7, 3, 1, 1, 5 };

            // This effectively inverts the sort
            const auto key = [](const int i) -> float {
                return 1.0 / static_cast<float>(i);
            }
            ;

            // When
            l.Sort(key);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 7, 5, 3, 1, 1 };

            EXPECT_EQ(actual, expected);
        }

        [Fact]
        public void List_Sort_With_Key_And_Reverse()
        {
            // If
            List<int> l = { 7, 3, 1, 1, 5 };

            // This effectively inverts the sort, but the reverse reverses it again
            const auto key = [](const int i) -> float {
                return 1.0 / static_cast<float>(i);
            }
            ;

            // When
            l.Sort(key, true);

            // Then
            const auto actual = as_vector(l);
            const System.Collections.Generic.List<int> expected = { 1, 1, 3, 5, 7 };

            EXPECT_EQ(actual, expected);
        }
    }
}
