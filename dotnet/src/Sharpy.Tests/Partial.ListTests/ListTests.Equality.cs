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
        var copy = l;

        // When/then
        (l == copy).Should().BeTrue();
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
    public void List_Native_Equality_Null_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        ((List<int>)null == l).Should().BeFalse();
    }

    [Fact]
    public void List_Equality_Same_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var copy = l;

        // When/then
        copy.Should().Equal(l);
    }

    // NOTE: This test is no-op because FluentAssertions does not allow
    // a null argument to NotEqual()
    [Fact]
    public void List_Equality_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        // l.Should().NotEqual((List<int>)null);
    }

    [Fact]
    public void List_Native_Inequality_Same_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var copy = l;

        // When/then
        (l != copy).Should().BeFalse();
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
    public void List_Native_Inequality_Null_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        ((List<int>)null != l).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_And_Inequality_Different_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> m = [1, 3, 5, 7, 9];

        // When/then
        l.Should().NotEqual(m);

        // When
        m.Pop();

        // Then
        l.Should().Equal(m);
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
        var copy = l;

        // When/then
        l.__Eq__(copy).Should().BeTrue();
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
        var copy = l;

        // When/then
        l.__Ne__(copy).Should().BeFalse();
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
