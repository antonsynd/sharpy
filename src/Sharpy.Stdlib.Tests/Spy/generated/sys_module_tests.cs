// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using sys = global::Sharpy.Sys;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Sys.SysModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Sys
    {
        [global::Sharpy.SharpyModule("sys.sys_module_tests")]
        public static partial class SysModuleTests
        {
        }
    }

    public static partial class Sys
    {
        public partial class SysModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestStdoutIsNotNone()
            {
#line (18, 5) - (18, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Stdout);
            }

            [Xunit.FactAttribute]
            public void TestStderrIsNotNone()
            {
#line (23, 5) - (23, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Stderr);
            }

            [Xunit.FactAttribute]
            public void TestStdinIsNotNone()
            {
#line (28, 5) - (28, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Stdin);
            }

            [Xunit.FactAttribute]
            public void TestArgvIsNotNone()
            {
#line (35, 5) - (35, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Argv);
            }

            [Xunit.FactAttribute]
            public void TestArgvHasAtLeastProgramName()
            {
#line (40, 5) - (40, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(sys.Argv) > 0);
            }

            [Xunit.FactAttribute]
            public void TestVersionContainsSharpy()
            {
#line (47, 5) - (47, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.Contains("Sharpy", sys.Version);
            }

            [Xunit.FactAttribute]
            public void TestVersionIsNotEmpty()
            {
#line (52, 5) - (52, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(sys.Version.Length > 0);
            }

            [Xunit.FactAttribute]
            public void TestPlatformIsRecognizedValue()
            {
#line (59, 5) - (59, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                string p = sys.Platform;
#line (60, 5) - (60, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(p.Length > 0);
#line (61, 5) - (61, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                bool valid = p == "win32" || p == "linux" || p == "darwin" || p == "unknown";
#line (62, 5) - (62, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(valid);
            }

            [Xunit.FactAttribute]
            public void TestExecutableIsNotNone()
            {
#line (69, 5) - (69, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Executable);
            }

            [Xunit.FactAttribute]
            public void TestPathIsNotNone()
            {
#line (76, 5) - (76, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Path);
            }

            [Xunit.FactAttribute]
            public void TestPathIsNotEmpty()
            {
#line (81, 5) - (81, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(sys.Path) > 0);
            }

            [Xunit.FactAttribute]
            public void TestMaxsizeIsIntMaxValue()
            {
#line (88, 5) - (88, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.Equal(2147483647, sys.Maxsize);
            }

            [Xunit.FactAttribute]
            public void TestGetsizeofValueTypeReturnsPositiveSize()
            {
#line (95, 5) - (95, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(sys.Getsizeof(42) > 0);
            }

            [Xunit.FactAttribute]
            public void TestGetsizeofReferenceTypeReturnsMinusOne()
            {
#line (100, 5) - (100, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.Equal(-1, sys.Getsizeof("a reference type"));
            }
        }
    }
}
