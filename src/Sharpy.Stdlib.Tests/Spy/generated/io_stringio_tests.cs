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
using static Sharpy.Stdlib.Tests.Spy.IO.IoStringioTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class IO
    {
        [global::Sharpy.SharpyModule("io.io_stringio_tests")]
        public static partial class IoStringioTests
        {
        }
    }

    public static partial class IO
    {
        public partial class IoStringioTestsTests
        {
            [Xunit.FactAttribute]
            public void TestEmptyConstructorGetvalueIsEmpty()
            {
#line (15, 5) - (15, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (16, 5) - (16, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("", sio.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestEmptyConstructorReadReturnsEmpty()
            {
#line (21, 5) - (21, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (22, 5) - (22, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("", sio.Read());
            }

            [Xunit.FactAttribute]
            public void TestEmptyConstructorTellIsZero()
            {
#line (27, 5) - (27, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (28, 5) - (28, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(0, sio.Tell());
            }

            [Xunit.FactAttribute]
            public void TestInitialContentTellIsAtStart()
            {
#line (33, 5) - (33, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (35, 5) - (35, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(0, sio.Tell());
            }

            [Xunit.FactAttribute]
            public void TestWriteSequentialWritesAccumulate()
            {
#line (42, 5) - (42, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (43, 5) - (43, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Write("foo");
#line (44, 5) - (44, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Write("bar");
#line (45, 5) - (45, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("foobar", sio.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestWriteReturnsCharacterCount()
            {
#line (50, 5) - (50, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (51, 5) - (51, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(11, sio.Write("hello world"));
            }

            [Xunit.FactAttribute]
            public void TestWriteEmptyStringReturnsZero()
            {
#line (56, 5) - (56, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (57, 5) - (57, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(0, sio.Write(""));
            }

            [Xunit.FactAttribute]
            public void TestSeekToZeroAllowsFullRead()
            {
#line (64, 5) - (64, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (65, 5) - (65, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Write("hello");
#line (66, 5) - (66, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Seek(0);
#line (67, 5) - (67, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("hello", sio.Read());
            }

            [Xunit.FactAttribute]
            public void TestSeekReturnsNewPosition()
            {
#line (72, 5) - (72, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello world");
#line (73, 5) - (73, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(6, sio.Seek(6));
            }

            [Xunit.FactAttribute]
            public void TestTellAfterReadReflectsPosition()
            {
#line (78, 5) - (78, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (79, 5) - (79, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Read(3);
#line (80, 5) - (80, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(3, sio.Tell());
            }

            [Xunit.FactAttribute]
            public void TestReadlineNoNewlineReadsToEnd()
            {
#line (87, 5) - (87, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("no newline here");
#line (88, 5) - (88, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("no newline here", sio.Readline());
            }

            [Xunit.FactAttribute]
            public void TestReadlineMultipleCallsAdvanceThroughContent()
            {
#line (93, 5) - (93, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("line1\nline2\nline3");
#line (94, 5) - (94, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("line1\n", sio.Readline());
#line (95, 5) - (95, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("line2\n", sio.Readline());
#line (96, 5) - (96, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("line3", sio.Readline());
#line (97, 5) - (97, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("", sio.Readline());
            }

            [Xunit.FactAttribute]
            public void TestTruncateZeroClearsContent()
            {
#line (104, 5) - (104, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello world");
#line (105, 5) - (105, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Truncate(0);
#line (106, 5) - (106, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("", sio.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestTruncateReturnsNewSize()
            {
#line (111, 5) - (111, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello world");
#line (112, 5) - (112, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(5, sio.Truncate(5));
            }

            [Xunit.FactAttribute]
            public void TestWriteAfterSeekToMiddleOverwritesChars()
            {
#line (117, 5) - (117, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello world");
#line (118, 5) - (118, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Seek(6);
#line (119, 5) - (119, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Write("earth");
#line (120, 5) - (120, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("hello earth", sio.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestGetvalueRegardlessOfPositionReturnsAll()
            {
#line (127, 5) - (127, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (128, 5) - (128, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Write("full content");
#line (130, 5) - (130, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(12, sio.Tell());
#line (132, 5) - (132, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("full content", sio.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestSeekToEndReadReturnsEmpty()
            {
#line (137, 5) - (137, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (138, 5) - (138, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Seek(5);
#line (139, 5) - (139, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("", sio.Read());
            }

            [Xunit.FactAttribute]
            public void TestReadPartialReadAdvancesPosition()
            {
#line (144, 5) - (144, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("abcdef");
#line (145, 5) - (145, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Read(2);
#line (146, 5) - (146, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(2, sio.Tell());
#line (147, 5) - (147, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("cd", sio.Read(2));
#line (148, 5) - (148, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(4, sio.Tell());
            }

            [Xunit.FactAttribute]
            public void TestCloseGetvalueThrowsValueError()
            {
#line (155, 5) - (155, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (156, 5) - (156, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Close();
#line (157, 5) - (161, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (158, 9) - (158, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                    sio.Getvalue();
                }));
            }

            [Xunit.FactAttribute]
            public void TestCloseSeekThrowsValueError()
            {
#line (163, 5) - (163, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (164, 5) - (164, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Close();
#line (165, 5) - (169, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (166, 9) - (166, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                    sio.Seek(0);
                }));
            }

            [Xunit.FactAttribute]
            public void TestCloseWriteThrowsValueError()
            {
#line (171, 5) - (171, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (172, 5) - (172, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Close();
#line (173, 5) - (177, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (174, 9) - (174, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                    sio.Write("data");
                }));
            }

            [Xunit.FactAttribute]
            public void TestCloseReadThrowsValueError()
            {
#line (179, 5) - (179, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (180, 5) - (180, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Close();
#line (181, 5) - (185, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (182, 9) - (182, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                    sio.Read();
                }));
            }

            [Xunit.FactAttribute]
            public void TestCloseTellThrowsValueError()
            {
#line (187, 5) - (187, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (188, 5) - (188, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Close();
#line (189, 5) - (193, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (190, 9) - (190, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                    sio.Tell();
                }));
            }

            [Xunit.FactAttribute]
            public void TestCloseTruncateThrowsValueError()
            {
#line (195, 5) - (195, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (196, 5) - (196, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Close();
#line (197, 5) - (203, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (198, 9) - (198, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                    sio.Truncate();
                }));
            }

            [Xunit.FactAttribute]
            public void TestSeekNegativePositionThrowsValueError()
            {
#line (205, 5) - (205, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello");
#line (206, 5) - (212, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (207, 9) - (207, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                    sio.Seek(-1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestTruncateDefaultArgUsesCurrentPosition()
            {
#line (214, 5) - (214, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO("hello world");
#line (215, 5) - (215, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Seek(5);
#line (216, 5) - (216, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(5, sio.Truncate());
#line (217, 5) - (217, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal("hello", sio.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestTellAfterWriteReflectsBufferLength()
            {
#line (224, 5) - (224, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                var sio = new global::Sharpy.StringIO();
#line (225, 5) - (225, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                sio.Write("abc");
#line (226, 5) - (226, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/io/io_stringio_tests.spy"
                Xunit.Assert.Equal(3, sio.Tell());
            }
        }
    }
}
