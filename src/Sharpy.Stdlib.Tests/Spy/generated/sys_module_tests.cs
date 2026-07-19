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
#line (21, 5) - (21, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Stdout);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStderrIsNotNone()
            {
#line (26, 5) - (26, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Stderr);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStdinIsNotNone()
            {
#line (31, 5) - (31, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Stdin);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArgvIsNotNone()
            {
#line (38, 5) - (38, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Argv);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArgvHasAtLeastProgramName()
            {
#line (43, 5) - (43, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(sys.Argv) > 0);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestVersionContainsSharpy()
            {
#line (50, 5) - (50, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.Contains("Sharpy", sys.Version);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestVersionIsNotEmpty()
            {
#line (55, 5) - (55, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(sys.Version.Length > 0);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPlatformIsRecognizedValue()
            {
#line (62, 5) - (62, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                string p = sys.Platform;
#line (63, 5) - (63, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(p.Length > 0);
#line (64, 5) - (64, 83) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                bool valid = p == "win32" || p == "linux" || p == "darwin" || p == "unknown";
#line (65, 5) - (65, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(valid);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutableIsNotNone()
            {
#line (72, 5) - (72, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Executable);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPathIsNotNone()
            {
#line (79, 5) - (79, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.NotNull(sys.Path);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPathIsNotEmpty()
            {
#line (84, 5) - (84, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(sys.Path) > 0);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMaxsizeIsIntMaxValue()
            {
#line (91, 5) - (91, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.Equal(2147483647, sys.Maxsize);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetsizeofValueTypeReturnsPositiveSize()
            {
#line (98, 5) - (98, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.True(sys.Getsizeof(42) > 0);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetsizeofReferenceTypeReturnsMinusOne()
            {
#line (103, 5) - (103, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
                Xunit.Assert.Equal(-1, sys.Getsizeof("a reference type"));
#line hidden
            }
        }
    }
}
#line default
