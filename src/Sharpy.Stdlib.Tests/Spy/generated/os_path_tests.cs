// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Os.OsPathTests
{
    [global::Sharpy.SharpyModule("os.os_path_tests")]
    public static partial class OsPathTestsModule
    {
    }

    public partial class OsPathTestsModuleTests : global::System.IDisposable
    {
        private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
        [Xunit.FactAttribute]
        public void TestJoinTwoParts()
        {
#line (15, 5) - (15, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("/a/b", global::Sharpy.OsPathModule.OsPathModuleModule.Join("/a", "b"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestJoinMultipleParts()
        {
#line (20, 5) - (20, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("a/b/c", global::Sharpy.OsPathModule.OsPathModuleModule.Join("a", "b", "c"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestExistsTrueForFile()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (25, 5) - (25, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            var path = tmpPath + "/f.txt";
#line (26, 5) - (27, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
            {
#line (27, 9) - (27, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                fa.Write("hello");
#line hidden
            }

#line (28, 5) - (28, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Exists(path));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestExistsTrueForDirectory()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (33, 5) - (33, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Exists(tmpPath));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestExistsFalseForNonexistent()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (38, 5) - (38, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.False(global::Sharpy.OsPathModule.OsPathModuleModule.Exists(tmpPath + "/nonexistent_path"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsfileReturnsTrueForFile()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (43, 5) - (43, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            var path = tmpPath + "/f.txt";
#line (44, 5) - (45, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
            {
#line (45, 9) - (45, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                fa.Write("hello");
#line hidden
            }

#line (46, 5) - (46, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isfile(path));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsfileReturnsFalseForDirectory()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (51, 5) - (51, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.False(global::Sharpy.OsPathModule.OsPathModuleModule.Isfile(tmpPath));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsdirReturnsTrueForDirectory()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (56, 5) - (56, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isdir(tmpPath));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsdirReturnsFalseForFile()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (61, 5) - (61, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            var path = tmpPath + "/f.txt";
#line (62, 5) - (63, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
            {
#line (63, 9) - (63, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                fa.Write("hello");
#line hidden
            }

#line (64, 5) - (64, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.False(global::Sharpy.OsPathModule.OsPathModuleModule.Isdir(path));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsabsReturnsTrueForAbsolute()
        {
#line (69, 5) - (69, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isabs("/usr/local"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsabsReturnsFalseForRelative()
        {
#line (74, 5) - (74, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.False(global::Sharpy.OsPathModule.OsPathModuleModule.Isabs("a/b/c"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBasenameReturnsFilename()
        {
#line (79, 5) - (79, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("c.txt", global::Sharpy.OsPathModule.OsPathModuleModule.Basename("/a/b/c.txt"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDirnameReturnsDirectory()
        {
#line (84, 5) - (84, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("/a/b", global::Sharpy.OsPathModule.OsPathModuleModule.Dirname("/a/b/c.txt"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitReturnsHeadAndTail()
        {
#line (89, 5) - (89, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            var (head, tail) = global::Sharpy.OsPathModule.OsPathModuleModule.Split("/a/b/c.txt");
#line (90, 5) - (90, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("/a/b", head);
#line (91, 5) - (91, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("c.txt", tail);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitextReturnsRootAndExt()
        {
#line (96, 5) - (96, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            var (root, ext) = global::Sharpy.OsPathModule.OsPathModuleModule.Splitext("/a/b/c.tar.gz");
#line (97, 5) - (97, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("/a/b/c.tar", root);
#line (98, 5) - (98, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal(".gz", ext);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitextNoExtension()
        {
#line (103, 5) - (103, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            var (root, ext) = global::Sharpy.OsPathModule.OsPathModuleModule.Splitext("/a/b/c");
#line (104, 5) - (104, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("/a/b/c", root);
#line (105, 5) - (105, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("", ext);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAbspathReturnsFullPath()
        {
#line (110, 5) - (110, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            var result = global::Sharpy.OsPathModule.OsPathModuleModule.Abspath(".");
#line (111, 5) - (111, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isabs(result));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNormpathCollapsesDotdot()
        {
#line (116, 5) - (116, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("a/c", global::Sharpy.OsPathModule.OsPathModuleModule.Normpath("a/b/../c"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNormpathRemovesDots()
        {
#line (121, 5) - (121, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("a/b", global::Sharpy.OsPathModule.OsPathModuleModule.Normpath("a/./b"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNormpathEmptyReturnsDot()
        {
#line (126, 5) - (126, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal(".", global::Sharpy.OsPathModule.OsPathModuleModule.Normpath(""));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetsizeReturnsFileSize()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (131, 5) - (131, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            var path = tmpPath + "/f.txt";
#line (132, 5) - (133, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
            {
#line (133, 9) - (133, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                fa.Write("hello");
#line hidden
            }

#line (134, 5) - (134, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal(5, global::Sharpy.OsPathModule.OsPathModuleModule.Getsize(path));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetsizeNonexistentThrows()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (139, 5) - (140, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (140, 9) - (140, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
                global::Sharpy.OsPathModule.OsPathModuleModule.Getsize(tmpPath + "/nonexistent");
#line hidden
            }
            catch (FileNotFoundError)
            {
                __raised_0 = true;
            }

            if (!__raised_0)
                throw new global::Sharpy.AssertionError("Expected FileNotFoundError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestExpanduserExpandsTilde()
        {
#line (145, 5) - (145, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            var result = global::Sharpy.OsPathModule.OsPathModuleModule.Expanduser("~");
#line (146, 5) - (146, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.NotEqual("~", result);
#line (147, 5) - (147, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isabs(result));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestExpanduserExpandsTildeSlash()
        {
#line (152, 5) - (152, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            var result = global::Sharpy.OsPathModule.OsPathModuleModule.Expanduser("~/foo");
#line (153, 5) - (153, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.EndsWith("foo", result);
#line (154, 5) - (154, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith(result, "~"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestExpanduserNoTildeUnchanged()
        {
#line (159, 5) - (159, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_tests.spy"
            Xunit.Assert.Equal("/a/b", global::Sharpy.OsPathModule.OsPathModuleModule.Expanduser("/a/b"));
#line hidden
        }

        public void Dispose()
        {
            _tmpPathFixture.Dispose();
        }
    }
}
#line default
