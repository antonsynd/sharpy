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

namespace Sharpy.Stdlib.Tests.Spy.Os.OsPathAdditionalTests
{
    [global::Sharpy.SharpyModule("os.os_path_additional_tests")]
    public static partial class OsPathAdditionalTestsModule
    {
    }

    public partial class OsPathAdditionalTestsModuleTests : global::System.IDisposable
    {
        private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
        [Xunit.FactAttribute]
        public void TestAbspathDotReturnsAbsolute()
        {
#line (14, 5) - (14, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var result = global::Sharpy.OsPathModule.OsPathModuleModule.Abspath(".");
#line (15, 5) - (15, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.True(result.Length > 0);
#line (16, 5) - (16, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isabs(result));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAbspathRelativePathReturnsAbsolute()
        {
#line (21, 5) - (21, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var result = global::Sharpy.OsPathModule.OsPathModuleModule.Abspath("some/relative");
#line (22, 5) - (22, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isabs(result));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRealpathExistingDirReturnsAbsolute()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (29, 5) - (29, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var result = global::Sharpy.OsPathModule.OsPathModuleModule.Realpath(tmpPath);
#line (30, 5) - (30, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isabs(result));
#line (31, 5) - (31, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.True(result.Length > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRealpathDotReturnsAbsolute()
        {
#line (36, 5) - (36, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var result = global::Sharpy.OsPathModule.OsPathModuleModule.Realpath(".");
#line (37, 5) - (37, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isabs(result));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitTrailingSlashTailIsEmpty()
        {
#line (44, 5) - (44, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var (head, tail) = global::Sharpy.OsPathModule.OsPathModuleModule.Split("/a/b/");
#line (45, 5) - (45, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("", tail);
#line (46, 5) - (46, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.True(head.Length > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitRootOnlyReturnsRootAndEmpty()
        {
#line (51, 5) - (51, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var (_head, tail) = global::Sharpy.OsPathModule.OsPathModuleModule.Split("/");
#line (52, 5) - (52, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("", tail);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitSimpleFilenameHeadIsEmpty()
        {
#line (57, 5) - (57, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var (head, tail) = global::Sharpy.OsPathModule.OsPathModuleModule.Split("file.txt");
#line (58, 5) - (58, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("file.txt", tail);
#line (59, 5) - (59, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("", head);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitextSimpleExtensionReturnsCorrectParts()
        {
#line (66, 5) - (66, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var (root, ext) = global::Sharpy.OsPathModule.OsPathModuleModule.Splitext("file.txt");
#line (67, 5) - (67, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("file", root);
#line (68, 5) - (68, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal(".txt", ext);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitextHiddenFileDotnetBehavior()
        {
#line (74, 5) - (74, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var (root, ext) = global::Sharpy.OsPathModule.OsPathModuleModule.Splitext(".hidden");
#line (75, 5) - (75, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var combined = root + ext;
#line (76, 5) - (76, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal(".hidden", combined);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitextDotAtEndExtIsEmpty()
        {
#line (81, 5) - (81, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var (root, ext) = global::Sharpy.OsPathModule.OsPathModuleModule.Splitext("file.");
#line (82, 5) - (82, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var combined = root + ext;
#line (83, 5) - (83, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("file.", combined);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestJoinFourArgsJoinsCorrectly()
        {
#line (90, 5) - (90, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var result = global::Sharpy.OsPathModule.OsPathModuleModule.Join("a", "b", "c", "d");
#line (91, 5) - (91, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            bool hasA = result.Contains("a");
#line (92, 5) - (92, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.True(hasA);
#line (93, 5) - (93, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.EndsWith("d", result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestJoinTwoAbsolutePathsSecondWins()
        {
#line (99, 5) - (99, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("/b", global::Sharpy.OsPathModule.OsPathModuleModule.Join("/a", "/b"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestJoinEmptyFirstArgReturnsSecond()
        {
#line (105, 5) - (105, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("b", global::Sharpy.OsPathModule.OsPathModuleModule.Join("", "b"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNormpathAbsoluteWithDotdotCollapses()
        {
#line (112, 5) - (112, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("/a/c", global::Sharpy.OsPathModule.OsPathModuleModule.Normpath("/a/./b/../c"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNormpathDoubleSlashCollapses()
        {
#line (117, 5) - (117, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("/a/b", global::Sharpy.OsPathModule.OsPathModuleModule.Normpath("/a//b"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNormpathRelativeDotdotAtStartPreserved()
        {
#line (122, 5) - (122, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("../a/b", global::Sharpy.OsPathModule.OsPathModuleModule.Normpath("../a/b"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetsizeEmptyFileReturnsZero()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (129, 5) - (129, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var path = tmpPath + "/empty.txt";
#line (130, 5) - (131, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
            {
#line (131, 9) - (131, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
                fa.Write("");
#line hidden
            }

#line (132, 5) - (132, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.OsPathModule.OsPathModuleModule.Getsize(path));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetsizeNonEmptyFileReturnsCorrectSize()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (137, 5) - (137, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var path = tmpPath + "/sized.txt";
#line (138, 5) - (139, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
            {
#line (139, 9) - (139, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
                fa.Write("0123456789");
#line hidden
            }

#line (140, 5) - (140, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal(10, global::Sharpy.OsPathModule.OsPathModuleModule.Getsize(path));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestExpanduserTildeWithSubdirBuildsCorrectPath()
        {
#line (147, 5) - (147, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var result = global::Sharpy.OsPathModule.OsPathModuleModule.Expanduser("~/mydir/file.txt");
#line (148, 5) - (148, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith(result, "~"));
#line (149, 5) - (149, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.EndsWith("mydir/file.txt", result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDirnameTrailingSlashReturnsParent()
        {
#line (156, 5) - (156, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var result = global::Sharpy.OsPathModule.OsPathModuleModule.Dirname("/a/b/");
#line (157, 5) - (157, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.True(result.Length > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBasenameTrailingSlashReturnsEmpty()
        {
#line (162, 5) - (162, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            var result = global::Sharpy.OsPathModule.OsPathModuleModule.Basename("/a/b/");
#line (163, 5) - (163, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("", result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBasenameRootPathReturnsEmpty()
        {
#line (168, 5) - (168, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.Equal("", global::Sharpy.OsPathModule.OsPathModuleModule.Basename("/"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsabsEmptyStringReturnsFalse()
        {
#line (175, 5) - (175, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.False(global::Sharpy.OsPathModule.OsPathModuleModule.Isabs(""));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsabsSingleSlashReturnsTrue()
        {
#line (180, 5) - (180, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/os/os_path_additional_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isabs("/"));
#line hidden
        }

        public void Dispose()
        {
            _tmpPathFixture.Dispose();
        }
    }
}
#line default
