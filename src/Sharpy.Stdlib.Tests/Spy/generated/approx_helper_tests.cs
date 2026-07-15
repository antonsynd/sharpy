// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Unittest.ApproxHelperTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Unittest
    {
        [global::Sharpy.SharpyModule("unittest.approx_helper_tests")]
        public static partial class ApproxHelperTests
        {
            internal static void _AssertClose(double actual, double expected)
            {
#line (13, 5) - (13, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/unittest/approx_helper_tests.spy"
                if (!(global::System.Math.Abs((double)(actual) - (double)(expected)) <= (double)(1e-9d)))
                {
                    throw new global::Sharpy.AssertionError();
                }
            }
        }
    }

    public static partial class Unittest
    {
        public partial class ApproxHelperTestsTests
        {
            [Xunit.FactAttribute]
            public void TestApproxPassesInNonTestHelper()
            {
#line (18, 5) - (18, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/unittest/approx_helper_tests.spy"
                _AssertClose(0.1d + 0.2d, 0.3d);
#line (19, 5) - (19, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/unittest/approx_helper_tests.spy"
                _AssertClose(1.0d, 1.0d);
            }

            [Xunit.FactAttribute]
            public void TestApproxFailureInHelperRaisesAssertionError()
            {
#line (24, 5) - (24, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/unittest/approx_helper_tests.spy"
                bool raised = false;
#line (25, 5) - (29, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/unittest/approx_helper_tests.spy"
                try
                {
#line (26, 9) - (26, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/unittest/approx_helper_tests.spy"
                    _AssertClose(1.0d, 2.0d);
                }
                catch (AssertionError)
                {
#line (28, 9) - (28, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/unittest/approx_helper_tests.spy"
                    raised = true;
                }

#line (29, 5) - (29, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/unittest/approx_helper_tests.spy"
                Xunit.Assert.True(raised);
            }
        }
    }
}
