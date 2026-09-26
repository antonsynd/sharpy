#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace TestIsinstanceAssert
{
    public static partial class TestIsinstanceAssertModule
    {
        public static void Main()
        {
#line (24, 5) - (24, 16) 12 "test_isinstance_assert.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }

    public partial class TestIsinstanceAssertModuleTests
    {
        [Xunit.FactAttribute]
        public void TestIsinstanceSingle()
        {
#line (3, 5) - (3, 20) 12 "test_isinstance_assert.spy"
            object x = 42;
#line (4, 5) - (4, 31) 12 "test_isinstance_assert.spy"
            Xunit.Assert.IsAssignableFrom<int>((object?)x);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsinstanceNegated()
        {
#line (8, 5) - (8, 25) 12 "test_isinstance_assert.spy"
            object x = "hello";
#line (9, 5) - (9, 35) 12 "test_isinstance_assert.spy"
            Xunit.Assert.False((object?)x is int);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsinstanceTupleStructural()
        {
#line (14, 5) - (14, 31) 12 "test_isinstance_assert.spy"
            object x = (42, "hello");
#line (15, 5) - (15, 38) 12 "test_isinstance_assert.spy"
            Xunit.Assert.IsAssignableFrom<global::System.ValueTuple<int, string>>(x);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsinstanceNegatedTupleStructural()
        {
#line (20, 5) - (20, 22) 12 "test_isinstance_assert.spy"
            object x = 3.14d;
#line (21, 5) - (21, 42) 12 "test_isinstance_assert.spy"
            Xunit.Assert.False(x is global::System.ValueTuple<int, string>);
#line hidden
        }
    }
}
#line default
