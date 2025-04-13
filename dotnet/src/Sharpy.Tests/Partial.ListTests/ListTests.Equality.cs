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
    public void List_Equality_Same_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var copy = l;

        // When/then
        copy.Should().Equal(l);
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
}
