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
using static Sharpy.Stdlib.Tests.Spy.Logging.LoggingModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Logging
    {
        [global::Sharpy.SharpyModule("logging.logging_module_tests")]
        public static partial class LoggingModuleTests
        {
        }
    }

    public static partial class Logging
    {
        public partial class LoggingModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestConstantsHaveCorrectValues()
            {
#line (19, 5) - (19, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                Xunit.Assert.Equal(10, logging.DEBUG);
#line (20, 5) - (20, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                Xunit.Assert.Equal(20, logging.INFO);
#line (21, 5) - (21, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                Xunit.Assert.Equal(30, logging.WARNING);
#line (22, 5) - (22, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                Xunit.Assert.Equal(40, logging.ERROR);
#line (23, 5) - (23, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                Xunit.Assert.Equal(50, logging.CRITICAL);
            }
        }
    }
}
