#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;

public static partial class AssertAlmostEqualDelta
{
    public static void Main()
    {
#line (12, 5) - (12, 16) 1 "assert_almost_equal_delta.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class AssertAlmostEqualDeltaTests
{
    [Xunit.FactAttribute]
    public void TestWithinDelta()
    {
#line (5, 5) - (5, 53) 1 "assert_almost_equal_delta.spy"
        Xunit.Assert.True(System.Math.Abs(0.1d + 0.2d - 0.3d) <= 0.001d);
    }

    [Xunit.FactAttribute]
    public void TestSmallDelta()
    {
#line (9, 5) - (9, 50) 1 "assert_almost_equal_delta.spy"
        Xunit.Assert.True(System.Math.Abs(1.0d - 1.0001d) <= 0.001d);
    }
}
