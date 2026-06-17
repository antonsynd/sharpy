// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using logging = global::Sharpy.Logging;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Logging.LoggingCompleteTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Logging
    {
        [global::Sharpy.SharpyModule("logging.logging_complete_tests")]
        public static partial class LoggingCompleteTests
        {
        }
    }

    public static partial class Logging
    {
        public partial class LoggingCompleteTestsTests
        {
            [Xunit.FactAttribute]
            public void TestDebugLessThanInfo()
            {
#line (21, 5) - (21, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                Xunit.Assert.True(logging.DEBUG < logging.INFO);
            }

            [Xunit.FactAttribute]
            public void TestInfoLessThanWarning()
            {
#line (26, 5) - (26, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                Xunit.Assert.True(logging.INFO < logging.WARNING);
            }

            [Xunit.FactAttribute]
            public void TestWarningLessThanError()
            {
#line (31, 5) - (31, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                Xunit.Assert.True(logging.WARNING < logging.ERROR);
            }

            [Xunit.FactAttribute]
            public void TestErrorLessThanCritical()
            {
#line (36, 5) - (36, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                Xunit.Assert.True(logging.ERROR < logging.CRITICAL);
            }
        }
    }
}
