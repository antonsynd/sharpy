#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static TestMarkMultiple;

public static partial class TestMarkMultiple
{
    public static void Main()
    {
#line (15, 5) - (15, 16) 8 "test_mark_multiple.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}

public partial class TestMarkMultipleTests
{
    [Xunit.FactAttribute]
    [Xunit.TraitAttribute("Category", "slow")]
    [Xunit.TraitAttribute("Category", "network")]
    public void TestMultiMarked()
    {
#line (5, 5) - (5, 18) 8 "test_mark_multiple.spy"
        int x = 100;
#line (6, 5) - (6, 21) 8 "test_mark_multiple.spy"
        Xunit.Assert.Equal(100, x);
#line hidden
    }

    [Xunit.TheoryAttribute]
    [Xunit.InlineDataAttribute(1, 1)]
    [Xunit.InlineDataAttribute(2, 4)]
    [Xunit.TraitAttribute("Category", "parametrized")]
    public void TestParametrizedMarked(int value, int expected)
    {
#line (11, 5) - (11, 33) 8 "test_mark_multiple.spy"
        int result = value * value;
#line (12, 5) - (12, 31) 8 "test_mark_multiple.spy"
        Xunit.Assert.Equal(expected, result);
#line hidden
    }
}
#line default
