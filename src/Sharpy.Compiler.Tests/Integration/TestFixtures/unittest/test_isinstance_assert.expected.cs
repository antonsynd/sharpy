#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static TestIsinstanceAssert;

public static partial class TestIsinstanceAssert
{
    public static void Main()
    {
#line (22, 5) - (22, 16) 1 "test_isinstance_assert.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class TestIsinstanceAssertTests
{
    [Xunit.FactAttribute]
    public void TestIsinstanceSingle()
    {
#line (3, 5) - (3, 20) 1 "test_isinstance_assert.spy"
        object x = 42;
#line (4, 5) - (4, 31) 1 "test_isinstance_assert.spy"
        Xunit.Assert.IsAssignableFrom<int>(x);
    }

    [Xunit.FactAttribute]
    public void TestIsinstanceNegated()
    {
#line (8, 5) - (8, 25) 1 "test_isinstance_assert.spy"
        object x = "hello";
#line (9, 5) - (9, 35) 1 "test_isinstance_assert.spy"
        Xunit.Assert.False(x is int);
    }

    [Xunit.FactAttribute]
    public void TestIsinstanceTuple()
    {
#line (13, 5) - (13, 20) 1 "test_isinstance_assert.spy"
        object x = 42;
#line (14, 5) - (14, 38) 1 "test_isinstance_assert.spy"
        Xunit.Assert.True(x is int || x is string);
    }

    [Xunit.FactAttribute]
    public void TestIsinstanceNegatedTuple()
    {
#line (18, 5) - (18, 22) 1 "test_isinstance_assert.spy"
        object x = 3.14d;
#line (19, 5) - (19, 42) 1 "test_isinstance_assert.spy"
        Xunit.Assert.False(x is int || x is string);
    }
}
