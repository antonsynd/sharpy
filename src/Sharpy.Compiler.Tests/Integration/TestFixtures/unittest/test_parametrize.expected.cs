#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static TestParametrize;

public static partial class TestParametrize
{
    public static void Main()
    {
#line (14, 5) - (14, 16) 1 "test_parametrize.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class TestParametrizeTests
{
    [Xunit.TheoryAttribute]
    [Xunit.InlineDataAttribute(1, 2, 3)]
    [Xunit.InlineDataAttribute(4, 5, 9)]
    [Xunit.InlineDataAttribute(10, 20, 30)]
    public void TestAdd(int a, int b, int expected)
    {
#line (3, 5) - (3, 30) 1 "test_parametrize.spy"
        Xunit.Assert.Equal(expected, a + b);
    }

    [Xunit.TheoryAttribute]
    [Xunit.InlineDataAttribute("hello", 5)]
    [Xunit.InlineDataAttribute("hi", 2)]
    [Xunit.InlineDataAttribute("", 0)]
    public void TestStringLength(string s, int expected)
    {
#line (7, 5) - (7, 31) 1 "test_parametrize.spy"
        Xunit.Assert.Equal(expected, s.Length);
    }

    [Xunit.TheoryAttribute]
    [Xunit.InlineDataAttribute(true)]
    [Xunit.InlineDataAttribute(false)]
    [Xunit.InlineDataAttribute(true)]
    public void TestBool(bool flag)
    {
#line (11, 5) - (11, 42) 1 "test_parametrize.spy"
        Xunit.Assert.True(flag == true || flag == false);
    }
}
