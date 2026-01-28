using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Multiplication_Operator_Negative()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l * -1;

        // Then
        Len(product).Should().Be(0);
    }

    [Fact]
    public void List_Multiplication_Operator_Zero()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l * 0;

        // Then
        Len(product).Should().Be(0);
    }

    [Fact]
    public void List_Multiplication_Operator_One()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l * 1;

        // Then
        var actual = product.ToList();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Multiplication_Operator_More_Than_One()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l * 3;

        // Then
        var actual = product.ToList();
        DotNetList<int> expected = [1, 3, 5, 7, 1, 3, 5, 7, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Right_Multiplication_Operator_Negative()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = -1 * l;

        // Then
        Len(product).Should().Be(0);
    }

    [Fact]
    public void List_Right_Multiplication_Operator_Zero()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = 0 * l;

        // Then
        Len(product).Should().Be(0);
    }

    [Fact]
    public void List_Right_Multiplication_Operator_One()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = 1 * l;

        // Then
        var actual = product.ToList();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Right_Multiplication_Operator_More_Than_One()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = 3 * l;

        // Then
        var actual = product.ToList();
        DotNetList<int> expected = [1, 3, 5, 7, 1, 3, 5, 7, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Multiplication_Assignment_Operator_Negative()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        l *= -1;

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Multiplication_Assignment_Operator_Zero()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        l *= 0;

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Multiplication_Assignment_Operator_One()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        l *= 1;

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Multiplication_Assignment_Operator_More_Than_One()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        l *= 3;

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 3, 5, 7, 1, 3, 5, 7, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Multiplication_Operator_Negative_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l.__Mul__(-1);

        // Then
        Len(product).Should().Be(0);
    }

    [Fact]
    public void List_Multiplication_Operator_Zero_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l.__Mul__(0);

        // Then
        Len(product).Should().Be(0);
    }

    [Fact]
    public void List_Multiplication_Operator_One_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l.__Mul__(1);

        // Then
        var actual = product.ToList();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Multiplication_Operator_More_Than_One_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l.__Mul__(3);

        // Then
        var actual = product.ToList();
        DotNetList<int> expected = [1, 3, 5, 7, 1, 3, 5, 7, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Inplace_Multiplication_Operator_Negative_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        l.__IMul__(-1);

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Inplace_Multiplication_Operator_Zero_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        l.__IMul__(0);

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Inplace_Multiplication_Operator_One_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        l.__IMul__(1);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Inplace_Multiplication_Operator_More_Than_One_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        l.__IMul__(3);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 3, 5, 7, 1, 3, 5, 7, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Right_Multiplication_Operator_Negative_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l.__RMul__(-1);

        // Then
        Len(product).Should().Be(0);
    }

    [Fact]
    public void List_Right_Multiplication_Operator_Zero_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l.__RMul__(0);

        // Then
        Len(product).Should().Be(0);
    }

    [Fact]
    public void List_Right_Multiplication_Operator_One_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l.__RMul__(1);

        // Then
        var actual = product.ToList();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Right_Multiplication_Operator_More_Than_One_Dunder()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var product = l.__RMul__(3);

        // Then
        var actual = product.ToList();
        DotNetList<int> expected = [1, 3, 5, 7, 1, 3, 5, 7, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }
}
