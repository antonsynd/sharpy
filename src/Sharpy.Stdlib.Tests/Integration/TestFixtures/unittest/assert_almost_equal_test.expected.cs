#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;
using static AssertAlmostEqualTest;

public static partial class AssertAlmostEqualTest
{
    public static void Main()
    {
#line (12, 5) - (12, 16) 1 "assert_almost_equal_test.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class AssertAlmostEqualTestTests
{
    [Xunit.FactAttribute]
    public void TestAlmostEqualDefault()
    {
#line (5, 5) - (5, 45) 1 "assert_almost_equal_test.spy"
        Xunit.Assert.Equal(3.14159265d, 3.14159d, 7);
    }

    [Xunit.FactAttribute]
    public void TestAlmostEqualPlaces()
    {
#line (9, 5) - (9, 50) 1 "assert_almost_equal_test.spy"
        Xunit.Assert.Equal(0.3d, 0.1d + 0.2d, 3);
    }
}
