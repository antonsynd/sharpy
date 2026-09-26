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

namespace Sharpy.Stdlib.Tests.Spy.Sys
{
    [global::Sharpy.SharpyModule("sys.sys_module_tests")]
    public static partial class SysModuleTests
    {
    }

    public partial class SysModuleTestsTests
    {
        [Xunit.FactAttribute]
        public void TestStdoutIsNotNone()
        {
#line (28, 5) - (28, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.NotNull(global::Sharpy.Sys.Stdout);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestStderrIsNotNone()
        {
#line (33, 5) - (33, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.NotNull(global::Sharpy.Sys.Stderr);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestStdinIsNotNone()
        {
#line (38, 5) - (38, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.NotNull(global::Sharpy.Sys.Stdin);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArgvIsNotNone()
        {
#line (45, 5) - (45, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.NotNull(global::Sharpy.Sys.Argv);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArgvHasAtLeastProgramName()
        {
#line (50, 5) - (50, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Builtins.Len(global::Sharpy.Sys.Argv) > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArgvMutatingCopyDoesNotAffectSource()
        {
#line (57, 5) - (57, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            var copy = global::Sharpy.Sys.Argv;
#line (58, 5) - (58, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            var before = global::Sharpy.Builtins.Len(copy);
#line (59, 5) - (59, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            copy.Append("injected");
#line (60, 5) - (60, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.Equal(before, global::Sharpy.Builtins.Len(global::Sharpy.Sys.Argv));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestVersionContainsSharpy()
        {
#line (67, 5) - (67, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.Contains("Sharpy", global::Sharpy.Sys.Version);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestVersionIsNotEmpty()
        {
#line (72, 5) - (72, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Sys.Version.Length > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestPlatformIsRecognizedValue()
        {
#line (79, 5) - (79, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            string p = global::Sharpy.Sys.Platform;
#line (80, 5) - (80, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.True(p.Length > 0);
#line (81, 5) - (81, 83) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            bool valid = p == "win32" || p == "linux" || p == "darwin" || p == "unknown";
#line (82, 5) - (82, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.True(valid);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestExecutableIsNotNone()
        {
#line (89, 5) - (89, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.NotNull(global::Sharpy.Sys.Executable);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestPathIsNotNone()
        {
#line (96, 5) - (96, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.NotNull(global::Sharpy.Sys.Path);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestPathIsNotEmpty()
        {
#line (101, 5) - (101, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Builtins.Len(global::Sharpy.Sys.Path) > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestPathMutatingCopyDoesNotAffectSource()
        {
#line (107, 5) - (107, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            var copy = global::Sharpy.Sys.Path;
#line (108, 5) - (108, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            var before = global::Sharpy.Builtins.Len(copy);
#line (109, 5) - (109, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            copy.Append("/injected");
#line (110, 5) - (110, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.Equal(before, global::Sharpy.Builtins.Len(global::Sharpy.Sys.Path));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMaxsizeIsIntMaxValue()
        {
#line (117, 5) - (117, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.Equal(2147483647, global::Sharpy.Sys.Maxsize);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetsizeofValueTypeReturnsPositiveSize()
        {
#line (124, 5) - (124, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.True(sys.Getsizeof(42) > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetsizeofReferenceTypeReturnsMinusOne()
        {
#line (129, 5) - (129, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/sys/sys_module_tests.spy"
            Xunit.Assert.Equal(-1, sys.Getsizeof("a reference type"));
#line hidden
        }
    }
}
#line default
