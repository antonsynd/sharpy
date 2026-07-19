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
using static global::Sharpy.OsPathModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Os.OsPathTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Os
    {
        [global::Sharpy.SharpyModule("os.os_path_tests")]
        public static partial class OsPathTests
        {
        }
    }

    public static partial class Os
    {
        public partial class OsPathTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestJoinTwoParts()
            {
#line (15, 5) - (15, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("/a/b", Join("/a", "b"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestJoinMultipleParts()
            {
#line (20, 5) - (20, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("a/b/c", Join("a", "b", "c"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExistsTrueForFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (25, 5) - (25, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                var path = tmpPath + "/f.txt";
#line (26, 5) - (28, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (27, 9) - (27, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                    fa.Write("hello");
#line hidden
                }

#line (28, 5) - (28, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.True(Exists(path));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExistsTrueForDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (33, 5) - (33, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.True(Exists(tmpPath));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExistsFalseForNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (38, 5) - (38, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.False(Exists(tmpPath + "/nonexistent_path"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsfileReturnsTrueForFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (43, 5) - (43, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                var path = tmpPath + "/f.txt";
#line (44, 5) - (46, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (45, 9) - (45, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                    fa.Write("hello");
#line hidden
                }

#line (46, 5) - (46, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.True(Isfile(path));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsfileReturnsFalseForDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (51, 5) - (51, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.False(Isfile(tmpPath));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsdirReturnsTrueForDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (56, 5) - (56, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.True(Isdir(tmpPath));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsdirReturnsFalseForFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (61, 5) - (61, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                var path = tmpPath + "/f.txt";
#line (62, 5) - (64, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (63, 9) - (63, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                    fa.Write("hello");
#line hidden
                }

#line (64, 5) - (64, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.False(Isdir(path));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsabsReturnsTrueForAbsolute()
            {
#line (69, 5) - (69, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.True(Isabs("/usr/local"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsabsReturnsFalseForRelative()
            {
#line (74, 5) - (74, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.False(Isabs("a/b/c"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBasenameReturnsFilename()
            {
#line (79, 5) - (79, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("c.txt", Basename("/a/b/c.txt"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDirnameReturnsDirectory()
            {
#line (84, 5) - (84, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("/a/b", Dirname("/a/b/c.txt"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplitReturnsHeadAndTail()
            {
#line (89, 5) - (89, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                var (head, tail) = Split("/a/b/c.txt");
#line (90, 5) - (90, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("/a/b", head);
#line (91, 5) - (91, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("c.txt", tail);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplitextReturnsRootAndExt()
            {
#line (96, 5) - (96, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                var (root, ext) = Splitext("/a/b/c.tar.gz");
#line (97, 5) - (97, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("/a/b/c.tar", root);
#line (98, 5) - (98, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal(".gz", ext);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplitextNoExtension()
            {
#line (103, 5) - (103, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                var (root, ext) = Splitext("/a/b/c");
#line (104, 5) - (104, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("/a/b/c", root);
#line (105, 5) - (105, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("", ext);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestAbspathReturnsFullPath()
            {
#line (110, 5) - (110, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                var result = Abspath(".");
#line (111, 5) - (111, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.True(Isabs(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNormpathCollapsesDotdot()
            {
#line (116, 5) - (116, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("a/c", Normpath("a/b/../c"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNormpathRemovesDots()
            {
#line (121, 5) - (121, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("a/b", Normpath("a/./b"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNormpathEmptyReturnsDot()
            {
#line (126, 5) - (126, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal(".", Normpath(""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetsizeReturnsFileSize()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (131, 5) - (131, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                var path = tmpPath + "/f.txt";
#line (132, 5) - (134, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (133, 9) - (133, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                    fa.Write("hello");
#line hidden
                }

#line (134, 5) - (134, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal(5, Getsize(path));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetsizeNonexistentThrows()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (139, 5) - (143, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
#line hidden
                {
#line (140, 9) - (140, 43) 20 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                    Getsize(tmpPath + "/nonexistent");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestExpanduserExpandsTilde()
            {
#line (145, 5) - (145, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                var result = Expanduser("~");
#line (146, 5) - (146, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.NotEqual("~", result);
#line (147, 5) - (147, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.True(Isabs(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExpanduserExpandsTildeSlash()
            {
#line (152, 5) - (152, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                var result = Expanduser("~/foo");
#line (153, 5) - (153, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.EndsWith("foo", result);
#line (154, 5) - (154, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith(result, "~"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExpanduserNoTildeUnchanged()
            {
#line (159, 5) - (159, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                Xunit.Assert.Equal("/a/b", Expanduser("/a/b"));
#line hidden
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
#line default
