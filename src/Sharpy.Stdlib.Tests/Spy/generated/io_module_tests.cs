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
using static Sharpy.Stdlib.Tests.Spy.IO.IoModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class IO
    {
        [global::Sharpy.SharpyModule("io.io_module_tests")]
        public static partial class IoModuleTests
        {
        }
    }

    public static partial class IO
    {
        public partial class IoModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestWriteReturnsLengthWritten()
            {
#line (16, 5) - (16, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (17, 5) - (17, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal(5, sio.Write("hello"));
            }

            [Xunit.FactAttribute]
            public void TestWriteReadCycle()
            {
#line (22, 5) - (22, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (23, 5) - (23, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Write("hello world");
#line (24, 5) - (24, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Seek(0);
#line (25, 5) - (25, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("hello world", sio.Read());
            }

            [Xunit.FactAttribute]
            public void TestReadWithCount()
            {
#line (30, 5) - (30, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (31, 5) - (31, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Write("hello world");
#line (32, 5) - (32, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Seek(0);
#line (33, 5) - (33, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("hello", sio.Read(5));
#line (34, 5) - (34, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal(" ", sio.Read(1));
#line (35, 5) - (35, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("world", sio.Read());
            }

            [Xunit.FactAttribute]
            public void TestReadlineReadsUntilNewline()
            {
#line (40, 5) - (40, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO("line1\nline2\nline3");
#line (41, 5) - (41, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("line1\n", sio.Readline());
#line (42, 5) - (42, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("line2\n", sio.Readline());
#line (43, 5) - (43, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("line3", sio.Readline());
            }

            [Xunit.FactAttribute]
            public void TestReadlineAtEndReturnsEmpty()
            {
#line (48, 5) - (48, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (49, 5) - (49, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Read();
#line (50, 5) - (50, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("", sio.Readline());
            }

            [Xunit.FactAttribute]
            public void TestSeekSetsPosition()
            {
#line (55, 5) - (55, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (56, 5) - (56, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal(2, sio.Seek(2));
#line (57, 5) - (57, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("l", sio.Read(1));
            }

            [Xunit.FactAttribute]
            public void TestTellReturnsCurrentPosition()
            {
#line (62, 5) - (62, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (63, 5) - (63, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal(0, sio.Tell());
#line (64, 5) - (64, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Write("hello");
#line (65, 5) - (65, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal(5, sio.Tell());
            }

            [Xunit.FactAttribute]
            public void TestGetvalueReturnsEntireContent()
            {
#line (70, 5) - (70, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (71, 5) - (71, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Write("hello");
#line (72, 5) - (72, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Write(" world");
#line (73, 5) - (73, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("hello world", sio.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestTruncateAtCurrentPosition()
            {
#line (78, 5) - (78, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO("hello world");
#line (79, 5) - (79, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Seek(5);
#line (80, 5) - (80, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Truncate();
#line (81, 5) - (81, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("hello", sio.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestTruncateAtSpecificSize()
            {
#line (86, 5) - (86, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO("hello world");
#line (87, 5) - (87, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Truncate(3);
#line (88, 5) - (88, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("hel", sio.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestClosePreventsOperations()
            {
#line (93, 5) - (93, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (94, 5) - (94, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Close();
#line (95, 5) - (99, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (96, 9) - (96, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                    sio.Read();
                }));
            }

            [Xunit.FactAttribute]
            public void TestDisposeClosesStream()
            {
#line (102, 5) - (102, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (103, 5) - (103, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Close();
#line (104, 5) - (108, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (105, 9) - (105, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                    sio.Write("x");
                }));
            }

            [Xunit.FactAttribute]
            public void TestInitialContentIsReadable()
            {
#line (110, 5) - (110, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO("initial");
#line (111, 5) - (111, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("initial", sio.Read());
            }

            [Xunit.FactAttribute]
            public void TestWriteOverwritesAtPosition()
            {
#line (116, 5) - (116, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (117, 5) - (117, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Seek(0);
#line (118, 5) - (118, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                sio.Write("HE");
#line (119, 5) - (119, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Equal("HEllo", sio.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestSeekNegativeThrows()
            {
#line (124, 5) - (124, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (125, 5) - (127, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (126, 9) - (126, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_module_tests.spy"
                    sio.Seek(-1);
                }));
            }
        }
    }
}
