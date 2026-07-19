#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static TestIsinstanceAssertCollections;

public static partial class TestIsinstanceAssertCollections
{
    public static void Main()
    {
#line (37, 5) - (37, 16) 8 "test_isinstance_assert_collections.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}

public partial class TestIsinstanceAssertCollectionsTests
{
    [Xunit.FactAttribute]
    public void TestIsinstanceDict()
    {
#line (3, 5) - (3, 26) 8 "test_isinstance_assert_collections.spy"
        object x = new Sharpy.Dict<string, int>()
#line hidden
        {
            {
                "a",
                1
            }
        };
#line (4, 5) - (4, 32) 8 "test_isinstance_assert_collections.spy"
        Xunit.Assert.IsAssignableFrom<global::Sharpy.IDict>(x);
#line hidden
    }

    [Xunit.FactAttribute]
    public void TestIsinstanceList()
    {
#line (8, 5) - (8, 27) 8 "test_isinstance_assert_collections.spy"
        object x = new Sharpy.List<int>()
#line hidden
        {
            1,
            2,
            3
        };
#line (9, 5) - (9, 32) 8 "test_isinstance_assert_collections.spy"
        Xunit.Assert.IsAssignableFrom<global::Sharpy.IList>(x);
#line hidden
    }

    [Xunit.FactAttribute]
    public void TestIsinstanceSet()
    {
#line (13, 5) - (13, 27) 8 "test_isinstance_assert_collections.spy"
        object x = new Sharpy.Set<int>()
#line hidden
        {
            1,
            2,
            3
        };
#line (14, 5) - (14, 31) 8 "test_isinstance_assert_collections.spy"
        Xunit.Assert.IsAssignableFrom<global::Sharpy.ISet>(x);
#line hidden
    }

    [Xunit.FactAttribute]
    public void TestIsinstanceTupleCollections()
    {
#line (18, 5) - (18, 26) 8 "test_isinstance_assert_collections.spy"
        object x = new Sharpy.Dict<string, int>()
#line hidden
        {
            {
                "a",
                1
            }
        };
#line (19, 5) - (19, 40) 8 "test_isinstance_assert_collections.spy"
        Xunit.Assert.True(x is global::Sharpy.IDict || x is global::Sharpy.IList);
#line hidden
    }

    [Xunit.FactAttribute]
    public void TestIsinstanceNegatedDict()
    {
#line (23, 5) - (23, 25) 8 "test_isinstance_assert_collections.spy"
        object x = "hello";
#line (24, 5) - (24, 36) 8 "test_isinstance_assert_collections.spy"
        Xunit.Assert.False(x is global::Sharpy.IDict);
#line hidden
    }

    [Xunit.FactAttribute]
    public void TestIsinstanceNegatedTupleCollections()
    {
#line (28, 5) - (28, 25) 8 "test_isinstance_assert_collections.spy"
        object x = "hello";
#line (29, 5) - (29, 43) 8 "test_isinstance_assert_collections.spy"
        Xunit.Assert.False(x is global::Sharpy.IList || x is global::Sharpy.ISet);
#line hidden
    }

    [Xunit.FactAttribute]
    public void TestIsinstanceMixedCollectionAndPrimitive()
    {
#line (33, 5) - (33, 24) 8 "test_isinstance_assert_collections.spy"
        object x = new Sharpy.List<int>()
#line hidden
        {
            1,
            2
        };
#line (34, 5) - (34, 45) 8 "test_isinstance_assert_collections.spy"
        Xunit.Assert.True(x is global::Sharpy.IDict || x is int || x is global::Sharpy.IList);
#line hidden
    }
}
#line default
