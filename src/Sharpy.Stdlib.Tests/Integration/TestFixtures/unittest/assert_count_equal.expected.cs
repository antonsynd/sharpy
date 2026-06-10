#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;
using static AssertCountEqual;

public static partial class AssertCountEqual
{
    public static void Main()
    {
#line (18, 5) - (18, 16) 1 "assert_count_equal.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class AssertCountEqualTests
{
    [Xunit.FactAttribute]
    public void TestSameOrderIndependent()
    {
#line (5, 5) - (5, 45) 1 "assert_count_equal.spy"
        Xunit.Assert.Equal(global::Sharpy.Builtins.Sorted(new Sharpy.List<int>() { 1, 2, 3 }), global::Sharpy.Builtins.Sorted(new Sharpy.List<int>() { 3, 1, 2 }));
    }

    [Xunit.FactAttribute]
    public void TestRespectsMultiplicity()
    {
#line (9, 5) - (9, 45) 1 "assert_count_equal.spy"
        Xunit.Assert.Equal(global::Sharpy.Builtins.Sorted(new Sharpy.List<int>() { 2, 1, 2 }), global::Sharpy.Builtins.Sorted(new Sharpy.List<int>() { 1, 2, 2 }));
    }

    [Xunit.FactAttribute]
    public void TestEmpty()
    {
#line (13, 5) - (13, 23) 1 "assert_count_equal.spy"
        Sharpy.List<int> a = new Sharpy.List<int>()
        {
        };
#line (14, 5) - (14, 23) 1 "assert_count_equal.spy"
        Sharpy.List<int> b = new Sharpy.List<int>()
        {
        };
#line (15, 5) - (15, 29) 1 "assert_count_equal.spy"
        Xunit.Assert.Equal(global::Sharpy.Builtins.Sorted(b), global::Sharpy.Builtins.Sorted(a));
    }
}
