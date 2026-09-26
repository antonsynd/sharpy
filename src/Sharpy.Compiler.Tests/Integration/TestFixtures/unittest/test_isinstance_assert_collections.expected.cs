#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace TestIsinstanceAssertCollections
{
    public static partial class TestIsinstanceAssertCollectionsModule
    {
        public static void Main()
        {
#line (40, 5) - (40, 16) 12 "test_isinstance_assert_collections.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }

    public partial class TestIsinstanceAssertCollectionsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestIsinstanceDict()
        {
#line (3, 5) - (3, 26) 12 "test_isinstance_assert_collections.spy"
            object x = new Sharpy.Dict<string, int>()
#line hidden
            {
                {
                    "a",
                    1
                }
            };
#line (4, 5) - (4, 42) 12 "test_isinstance_assert_collections.spy"
            Xunit.Assert.IsAssignableFrom<Sharpy.Dict<string, int>>(x);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsinstanceList()
        {
#line (8, 5) - (8, 27) 12 "test_isinstance_assert_collections.spy"
            object x = new Sharpy.List<int>()
#line hidden
            {
                1,
                2,
                3
            };
#line (9, 5) - (9, 37) 12 "test_isinstance_assert_collections.spy"
            Xunit.Assert.IsAssignableFrom<Sharpy.List<int>>(x);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsinstanceSet()
        {
#line (13, 5) - (13, 27) 12 "test_isinstance_assert_collections.spy"
            object x = new Sharpy.Set<int>()
#line hidden
            {
                1,
                2,
                3
            };
#line (14, 5) - (14, 36) 12 "test_isinstance_assert_collections.spy"
            Xunit.Assert.IsAssignableFrom<Sharpy.Set<int>>(x);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsinstanceOrOfSinglesCollections()
        {
#line (19, 5) - (19, 26) 12 "test_isinstance_assert_collections.spy"
            object x = new Sharpy.Dict<string, int>()
#line hidden
            {
                {
                    "a",
                    1
                }
            };
#line (20, 5) - (20, 70) 12 "test_isinstance_assert_collections.spy"
            Xunit.Assert.True(x is Sharpy.Dict<string, int> || x is Sharpy.List<int>);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsinstanceNegatedDict()
        {
#line (24, 5) - (24, 25) 12 "test_isinstance_assert_collections.spy"
            object x = "hello";
#line (25, 5) - (25, 46) 12 "test_isinstance_assert_collections.spy"
            Xunit.Assert.False(x is Sharpy.Dict<string, int>);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsinstanceNegatedOrOfSinglesCollections()
        {
#line (30, 5) - (30, 25) 12 "test_isinstance_assert_collections.spy"
            object x = "hello";
#line (31, 5) - (31, 73) 12 "test_isinstance_assert_collections.spy"
            Xunit.Assert.True(!(x is Sharpy.List<int>) && !(x is Sharpy.Set<int>));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsinstanceOrOfSinglesMixed()
        {
#line (36, 5) - (36, 24) 12 "test_isinstance_assert_collections.spy"
            object x = new Sharpy.List<int>()
#line hidden
            {
                1,
                2
            };
#line (37, 5) - (37, 92) 12 "test_isinstance_assert_collections.spy"
            Xunit.Assert.True(x is Sharpy.Dict<string, int> || (object?)x is int || x is Sharpy.List<int>);
#line hidden
        }
    }
}
#line default
