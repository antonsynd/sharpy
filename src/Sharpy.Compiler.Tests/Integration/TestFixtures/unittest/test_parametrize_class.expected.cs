#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

public static partial class TestParametrizeClass
{
    public class CalculatorTests
    {
        [Xunit.TheoryAttribute]
        [Xunit.InlineDataAttribute(1, 2, 3)]
        [Xunit.InlineDataAttribute(10, 20, 30)]
        public void TestAdd(int a, int b, int expected)
#line 3 "test_parametrize_class.spy"
        {
#line (4, 9) - (4, 34) 12 "test_parametrize_class.spy"
            Xunit.Assert.Equal(expected, a + b);
#line hidden
        }

        [Xunit.TheoryAttribute]
        [Xunit.InlineDataAttribute(2, 3, 6)]
        [Xunit.InlineDataAttribute(4, 5, 20)]
        public void TestMultiply(int a, int b, int expected)
#line 7 "test_parametrize_class.spy"
        {
#line (8, 9) - (8, 34) 12 "test_parametrize_class.spy"
            Xunit.Assert.Equal(expected, a * b);
#line hidden
        }
    }

    public static void Main()
    {
#line (11, 5) - (11, 16) 8 "test_parametrize_class.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}
#line default
