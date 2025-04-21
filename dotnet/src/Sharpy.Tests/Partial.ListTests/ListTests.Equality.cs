using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Native_Equality_Same_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var listRef = l;

        // When/then
        (l == listRef).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Equality_Same_Object_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        Object o = l;

        // When/then
        (l == o).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Equality_Same_Object_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        Object o = l;

        // When/then
        (o == l).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Equality_Same_DotNet_Object_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        object o = l;

        // When/then
        (l == o).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Equality_Same_DotNet_Object_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        object o = l;

        // When/then
        (o == l).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Equality_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        (l == null).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Equality_Null_Object_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        Object o = l;

        // When/then
        (o == null).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Equality_Null_DotNet_Object_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        object o = l;

        // When/then
        (o == null).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Equality_Null_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        ((List<int>)null == l).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Equality_Null_Object_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        Object o = l;

        // When/then
        ((Object)null == o).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Equality_Null_DotNet_Object_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        object o = l;

        // When/then
        (null == o).Should().BeFalse();
    }

    [Fact]
    public void List_Equality_Same_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var listRef = l;

        // When/then
        listRef.Equals(l).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_Same_Object_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        Object listRef = l;

        // When/then
        listRef.Equals(l).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_Same_Object_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        Object listRef = l;

        // When/then
        l.Equals(listRef).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_Same_DotNet_Object_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        object listRef = l;

        // When/then
        listRef.Should().Be(l);
    }

    [Fact]
    public void List_Equality_Same_DotNet_Object_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        object listRef = l;

        // When/then
        l.Equals(listRef).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        l.Equals((List<int>)null).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Inequality_Same_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var listRef = l;

        // When/then
        (l != listRef).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Inequality_Same_Object_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        Object o = l;

        // When/then
        (l != o).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Inequality_Same_Object_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        Object o = l;

        // When/then
        (o != l).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Inequality_Same_Object_DotNet_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        object o = l;

        // When/then
        (l != o).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Inequality_Same_Object_DotNet_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        object o = l;

        // When/then
        (o != l).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Inequality_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        (l != null).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Inequality_Null_Object_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        Object o = l;

        // When/then
        (o != null).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Inequality_Null_DotNet_Object_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        object o = l;

        // When/then
        (o != null).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Inequality_Null_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        ((List<int>)null != l).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Inequality_Null_Object_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        Object o = l;

        // When/then
        ((Object)null != o).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Inequality_Null_DotNet_Object_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        object o = l;

        // When/then
        (null != o).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_And_Inequality_Different_Object_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> m = [1, 3, 5, 7, 9];
        Object o = m;

        // When/then
        l.Equals(o).Should().BeFalse();

        // When
        m.Pop();

        // Then
        l.Equals(o).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_And_Inequality_Different_Object_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> m = [1, 3, 5, 7, 9];
        Object o = m;

        // When/then
        o.Equals(l).Should().BeFalse();

        // When
        m.Pop();

        // Then
        o.Equals(l).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_And_Inequality_Different_DotNet_Object_Cast()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> m = [1, 3, 5, 7, 9];
        object o = m;

        // When/then
        l.Equals(o).Should().BeFalse();

        // When
        m.Pop();

        // Then
        l.Equals(o).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_And_Inequality_Different_DotNet_Object_Cast_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> m = [1, 3, 5, 7, 9];
        object o = m;

        // When/then
        o.Equals(l).Should().BeFalse();

        // When
        m.Pop();

        // Then
        o.Equals(l).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Equality_And_Inequality_Different_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> m = [1, 3, 5, 7, 9];

        // When/then
        (l == m).Should().BeFalse();

        // When
        m.Pop();

        // Then
        (l == m).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Equality_Different_Type()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<double> m = [1.0, 3.0, 5.0, 7.0];

        // When/then
        (l == m).Should().BeFalse();
    }

    [Fact]
    public void List_Equality_Different_Type()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<double> m = [1.0, 3.0, 5.0, 7.0];

        // When/then
        l.Equals(m).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Equality_Different_Type_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        (l == (List<double>)null).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Equality_Different_Type_Null_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        ((List<double>)null == l).Should().BeFalse();
    }

    [Fact]
    public void List_Equality_Different_Type_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<double> m = [1.0, 3.0, 5.0, 7.0];

        // When/then
        l.Equals((List<double>)null).Should().BeFalse();
    }

    [Fact]
    public void List_Native_Inequality_Different_Type_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        (l != (List<double>)null).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Inequality_Different_Type_Null_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        ((List<double>)null != l).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_Dunder_Same_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var listRef = l;

        // When/then
        l.__Eq__(listRef).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_Dunder_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        l.__Eq__(null).Should().BeFalse();
    }

    [Fact]
    public void List_Inequality_Dunder_Same_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var listRef = l;

        // When/then
        l.__Ne__(listRef).Should().BeFalse();
    }

    [Fact]
    public void List_Inequality_Dunder_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        l.__Ne__(null).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_And_Inequality_Dunder_Different_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> m = [1, 3, 5, 7, 9];

        // When/then
        l.__Eq__(m).Should().BeFalse();

        // When
        m.Pop();

        // Then
        l.__Eq__(m).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_Dunder_Different_Type()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<double> m = [1.0, 3.0, 5.0, 7.0];

        // When/then
        l.__Eq__(m).Should().BeFalse();
    }

    [Fact]
    public void List_Inequality_Dunder_Different_Type()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<double> m = [1.0, 3.0, 5.0, 7.0];

        // When/then
        l.__Ne__(m).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_Dunder_Different_Type_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        l.__Ne__((List<double>)null).Should().BeTrue();
    }
}
