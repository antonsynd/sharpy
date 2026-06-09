#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;

public static partial class CustomAssertions
{
    public static void Main()
    {
#line (38, 5) - (38, 16) 1 "custom_assertions.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class CustomAssertionsTests
{
    [Xunit.FactAttribute]
    public void TestAssertTrue()
    {
#line (5, 5) - (5, 24) 1 "custom_assertions.spy"
        Xunit.Assert.True(1 == 1);
    }

    [Xunit.FactAttribute]
    public void TestAssertFalse()
    {
#line (9, 5) - (9, 25) 1 "custom_assertions.spy"
        Xunit.Assert.False(1 == 2);
    }

    [Xunit.FactAttribute]
    public void TestAssertIsNone()
    {
#line (13, 5) - (13, 25) 1 "custom_assertions.spy"
        Xunit.Assert.Null(null);
    }

    [Xunit.FactAttribute]
    public void TestAssertIsNotNone()
    {
#line (17, 5) - (17, 32) 1 "custom_assertions.spy"
        Xunit.Assert.NotNull("hello");
    }

    [Xunit.FactAttribute]
    public void TestAssertGreater()
    {
#line (21, 5) - (21, 26) 1 "custom_assertions.spy"
        Xunit.Assert.True(10 > 5, "Expected first argument > second argument");
    }

    [Xunit.FactAttribute]
    public void TestAssertLess()
    {
#line (25, 5) - (25, 22) 1 "custom_assertions.spy"
        Xunit.Assert.True(3 < 7, "Expected first argument < second argument");
    }

    [Xunit.FactAttribute]
    public void TestAssertIn()
    {
#line (29, 5) - (29, 34) 1 "custom_assertions.spy"
        Sharpy.List<int> items = new Sharpy.List<int>()
        {
            1,
            2,
            3
        };
#line (30, 5) - (30, 24) 1 "custom_assertions.spy"
        Xunit.Assert.Contains(2, items);
    }

    [Xunit.FactAttribute]
    public void TestAssertNotIn()
    {
#line (34, 5) - (34, 34) 1 "custom_assertions.spy"
        Sharpy.List<int> items = new Sharpy.List<int>()
        {
            1,
            2,
            3
        };
#line (35, 5) - (35, 29) 1 "custom_assertions.spy"
        Xunit.Assert.DoesNotContain(99, items);
    }
}
