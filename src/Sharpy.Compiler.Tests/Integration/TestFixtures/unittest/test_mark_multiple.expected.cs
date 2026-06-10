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
#line (15, 5) - (15, 16) 1 "test_mark_multiple.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class TestMarkMultipleTests
{
    [Xunit.FactAttribute]
    [Xunit.TraitAttribute("Category", "slow")]
    [Xunit.TraitAttribute("Category", "network")]
    public void TestMultiMarked()
    {
#line (5, 5) - (5, 18) 1 "test_mark_multiple.spy"
        int x = 100;
#line (6, 5) - (6, 21) 1 "test_mark_multiple.spy"
        Xunit.Assert.Equal(100, x);
    }

    [Xunit.TheoryAttribute]
    [Xunit.InlineDataAttribute(1, 1)]
    [Xunit.InlineDataAttribute(2, 4)]
    [Xunit.TraitAttribute("Category", "parametrized")]
    public void TestParametrizedMarked(int value, int expected)
    {
#line (11, 5) - (11, 33) 1 "test_mark_multiple.spy"
        int result = value * value;
#line (12, 5) - (12, 31) 1 "test_mark_multiple.spy"
        Xunit.Assert.Equal(expected, result);
    }
}
