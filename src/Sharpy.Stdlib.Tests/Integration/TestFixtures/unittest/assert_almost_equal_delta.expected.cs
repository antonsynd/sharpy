#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace AssertAlmostEqualDelta
{
    public static partial class AssertAlmostEqualDeltaModule
    {
        public static void Main()
        {
#line (12, 5) - (12, 16) 12 "assert_almost_equal_delta.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }

    public partial class AssertAlmostEqualDeltaModuleTests
    {
        [Xunit.FactAttribute]
        public void TestWithinDelta()
        {
#line (5, 5) - (5, 53) 12 "assert_almost_equal_delta.spy"
            Xunit.Assert.True(global::System.Math.Abs(0.1d + 0.2d - 0.3d) <= 0.001d);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSmallDelta()
        {
#line (9, 5) - (9, 50) 12 "assert_almost_equal_delta.spy"
            Xunit.Assert.True(global::System.Math.Abs(1.0d - 1.0001d) <= 0.001d);
#line hidden
        }
    }
}
#line default
