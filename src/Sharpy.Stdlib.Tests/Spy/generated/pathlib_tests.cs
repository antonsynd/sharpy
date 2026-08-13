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
using os = global::Sharpy.OsModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Pathlib.PathlibTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Pathlib
    {
        [global::Sharpy.SharpyModule("pathlib.pathlib_tests")]
        public static partial class PathlibTests
        {
            public static bool Contains(Sharpy.List<string> items, string value)
            {
#line (26, 5) - (29, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                foreach (var __loopVar_0 in items)
#line hidden
                {
                    var item = __loopVar_0;
#line (27, 9) - (29, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    if (item == value)
#line hidden
                    {
#line (28, 13) - (28, 25) 24 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                        return true;
#line hidden
                    }
                }

#line (29, 5) - (29, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                return false;
#line hidden
            }
        }
    }

    public static partial class Pathlib
    {
        public partial class PathlibTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestConstructorStoresPath()
            {
#line (36, 5) - (36, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/some/path");
#line (37, 5) - (37, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("/some/path", global::Sharpy.Builtins.Str(p));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestConstructorJoinsSegments()
            {
#line (42, 5) - (42, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("a", "b");
#line (43, 5) - (43, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                bool hasB = global::Sharpy.Builtins.Str(p).Contains("b");
#line (44, 5) - (44, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(hasB);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDivisionOperatorJoinsString()
            {
#line (51, 5) - (51, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/root") / "child";
#line (52, 5) - (52, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                bool hasChild = global::Sharpy.Builtins.Str(p).Contains("child");
#line (53, 5) - (53, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(hasChild);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDivisionOperatorJoinsPath()
            {
#line (58, 5) - (58, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/root") / new global::Sharpy.Path("child");
#line (59, 5) - (59, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                bool hasChild = global::Sharpy.Builtins.Str(p).Contains("child");
#line (60, 5) - (60, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(hasChild);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNameReturnsFinalComponent()
            {
#line (67, 5) - (67, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/some/path/file.txt");
#line (68, 5) - (68, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("file.txt", p.Name);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStemReturnsNameWithoutExtension()
            {
#line (73, 5) - (73, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/some/path/file.txt");
#line (74, 5) - (74, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("file", p.Stem);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSuffixReturnsExtension()
            {
#line (79, 5) - (79, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/some/path/file.txt");
#line (80, 5) - (80, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(".txt", p.Suffix);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSuffixEmptyWhenNoExtension()
            {
#line (85, 5) - (85, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/some/path/file");
#line (86, 5) - (86, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("", p.Suffix);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSuffixesReturnsAll()
            {
#line (91, 5) - (91, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("archive.tar.gz");
#line (92, 5) - (92, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var suffixes = p.Suffixes;
#line (93, 5) - (93, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(suffixes));
#line (94, 5) - (94, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(".tar", suffixes[0]);
#line (95, 5) - (95, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(".gz", suffixes[1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestParentReturnsParentPath()
            {
#line (100, 5) - (100, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/some/path/file.txt");
#line (101, 5) - (101, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                bool hasPath = global::Sharpy.Builtins.Str(p.Parent).Contains("path");
#line (102, 5) - (102, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(hasPath);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRootReturnsPathRoot()
            {
#line (107, 5) - (107, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/some/path");
#line (108, 5) - (108, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("/", p.Root);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsAbsoluteTrueForAbsolutePaths()
            {
#line (113, 5) - (113, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Path("/absolute").IsAbsolute);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsAbsoluteFalseForRelativePaths()
            {
#line (118, 5) - (118, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path("relative").IsAbsolute);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExistsTrueForExistingFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (125, 5) - (125, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var path = tmpPath + "/exists.txt";
#line (126, 5) - (128, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (127, 9) - (127, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("data");
#line hidden
                }

#line (128, 5) - (128, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Path(path).Exists());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExistsFalseForNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (133, 5) - (133, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path(tmpPath + "/nope.txt").Exists());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsFileTrueForFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (138, 5) - (138, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var path = tmpPath + "/file.txt";
#line (139, 5) - (141, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (140, 9) - (140, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("data");
#line hidden
                }

#line (141, 5) - (141, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Path(path).IsFile());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsDirTrueForDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (146, 5) - (146, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Path(tmpPath).IsDir());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReadTextAndWriteTextRoundTrip()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (153, 5) - (153, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path(tmpPath + "/roundtrip.txt");
#line (154, 5) - (154, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                p.WriteText("hello world");
#line (155, 5) - (155, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("hello world", p.ReadText());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMkdirCreatesDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (162, 5) - (162, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path(tmpPath + "/newdir");
#line (163, 5) - (163, 14) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                p.Mkdir();
#line (164, 5) - (164, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(p.IsDir());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMkdirParentsCreatesNestedDirectories()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (169, 5) - (169, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path(tmpPath + "/a/b/c");
#line (170, 5) - (170, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                p.Mkdir(true);
#line (171, 5) - (171, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(p.IsDir());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRmdirRemovesEmptyDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (176, 5) - (176, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path(tmpPath + "/tormdir");
#line (177, 5) - (177, 14) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                p.Mkdir();
#line (178, 5) - (178, 14) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                p.Rmdir();
#line (179, 5) - (179, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(p.IsDir());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIterdirListsEntries()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (184, 5) - (186, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(tmpPath + "/a.txt", "w"))
#line hidden
                {
#line (185, 9) - (185, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("");
#line hidden
                }

#line (186, 5) - (188, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(tmpPath + "/b.txt", "w"))
#line hidden
                {
#line (187, 9) - (187, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fb.Write("");
#line hidden
                }

#line (188, 5) - (188, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Sharpy.List<string> entries = new Sharpy.List<string>()
#line hidden
                {
                };
#line (189, 5) - (191, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                foreach (var __loopVar_1 in new global::Sharpy.Path(tmpPath).Iterdir())
#line hidden
                {
                    var entry = __loopVar_1;
#line (190, 9) - (190, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    entries.Append(entry.Name);
#line hidden
                }

#line (191, 5) - (191, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(Contains(entries, "a.txt"));
#line (192, 5) - (192, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(Contains(entries, "b.txt"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGlobMatchesPattern()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (197, 5) - (199, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(tmpPath + "/test1.txt", "w"))
#line hidden
                {
#line (198, 9) - (198, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("");
#line hidden
                }

#line (199, 5) - (201, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(tmpPath + "/test2.txt", "w"))
#line hidden
                {
#line (200, 9) - (200, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fb.Write("");
#line hidden
                }

#line (201, 5) - (203, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fc = global::Sharpy.Builtins.Open(tmpPath + "/other.md", "w"))
#line hidden
                {
#line (202, 9) - (202, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fc.Write("");
#line hidden
                }

#line (203, 5) - (203, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Sharpy.List<string> matches = new Sharpy.List<string>()
#line hidden
                {
                };
#line (204, 5) - (206, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                foreach (var __loopVar_2 in new global::Sharpy.Path(tmpPath).Glob("*.txt"))
#line hidden
                {
                    var entry = __loopVar_2;
#line (205, 9) - (205, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    matches.Append(entry.Name);
#line hidden
                }

#line (206, 5) - (206, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(matches));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestUnlinkDeletesFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (213, 5) - (213, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var path = tmpPath + "/todelete.txt";
#line (214, 5) - (216, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (215, 9) - (215, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("data");
#line hidden
                }

#line (216, 5) - (216, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                new global::Sharpy.Path(path).Unlink();
#line (217, 5) - (217, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path(path).Exists());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestUnlinkMissingOkDoesNotThrow()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (222, 5) - (222, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path(tmpPath + "/nonexistent.txt");
#line (223, 5) - (223, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                p.Unlink(true);
#line (224, 5) - (224, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(p.Exists());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestUnlinkThrowsOnNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (229, 5) - (235, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (230, 9) - (230, 54) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    new global::Sharpy.Path(tmpPath + "/nope.txt").Unlink();
#line hidden
                }
                catch (FileNotFoundError)
                {
                    __raised_3 = true;
                }

                if (!__raised_3)
                    throw new global::Sharpy.AssertionError("Expected FileNotFoundError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestResolveReturnsAbsolutePath()
            {
#line (237, 5) - (237, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("relative").Resolve();
#line (238, 5) - (238, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(p.IsAbsolute);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWithNameChangesName()
            {
#line (243, 5) - (243, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/some/path/file.txt").WithName("other.md");
#line (244, 5) - (244, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("other.md", p.Name);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWithStemChangesStem()
            {
#line (249, 5) - (249, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/some/path/file.txt").WithStem("other");
#line (250, 5) - (250, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("other.txt", p.Name);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWithSuffixChangesSuffix()
            {
#line (255, 5) - (255, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/some/path/file.txt").WithSuffix(".md");
#line (256, 5) - (256, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("file.md", p.Name);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRenameRenamesFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (263, 5) - (263, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var src = tmpPath + "/rename_src.txt";
#line (264, 5) - (266, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
#line hidden
                {
#line (265, 9) - (265, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("content");
#line hidden
                }

#line (266, 5) - (266, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var dst = tmpPath + "/rename_dst.txt";
#line (267, 5) - (267, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var result = new global::Sharpy.Path(src).Rename(dst);
#line (268, 5) - (268, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path(src).Exists());
#line (269, 5) - (269, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Path(dst).Exists());
#line (270, 5) - (270, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(dst, global::Sharpy.Builtins.Str(result));
#line (271, 5) - (271, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("content", new global::Sharpy.Path(dst).ReadText());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRenameThrowsOnNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (276, 5) - (282, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                bool __raised_4 = false;
#line hidden
                try
                {
#line (277, 9) - (277, 82) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    new global::Sharpy.Path(tmpPath + "/nonexistent.txt").Rename(tmpPath + "/dst.txt");
#line hidden
                }
                catch (FileNotFoundError)
                {
                    __raised_4 = true;
                }

                if (!__raised_4)
                    throw new global::Sharpy.AssertionError("Expected FileNotFoundError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestReplaceReplacesExistingTarget()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (284, 5) - (284, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var src = tmpPath + "/replace_src.txt";
#line (285, 5) - (285, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var dst = tmpPath + "/replace_dst.txt";
#line (286, 5) - (288, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
#line hidden
                {
#line (287, 9) - (287, 32) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("new content");
#line hidden
                }

#line (288, 5) - (290, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(dst, "w"))
#line hidden
                {
#line (289, 9) - (289, 32) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fb.Write("old content");
#line hidden
                }

#line (290, 5) - (290, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var result = new global::Sharpy.Path(src).Replace(dst);
#line (291, 5) - (291, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path(src).Exists());
#line (292, 5) - (292, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Path(dst).Exists());
#line (293, 5) - (293, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(dst, global::Sharpy.Builtins.Str(result));
#line (294, 5) - (294, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("new content", new global::Sharpy.Path(dst).ReadText());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReplaceWorksWhenTargetDoesNotExist()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (299, 5) - (299, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var src = tmpPath + "/replace_src2.txt";
#line (300, 5) - (300, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var dst = tmpPath + "/replace_dst2.txt";
#line (301, 5) - (303, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
#line hidden
                {
#line (302, 9) - (302, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("data");
#line hidden
                }

#line (303, 5) - (303, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var result = new global::Sharpy.Path(src).Replace(dst);
#line (304, 5) - (304, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path(src).Exists());
#line (305, 5) - (305, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Path(dst).Exists());
#line (306, 5) - (306, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(dst, global::Sharpy.Builtins.Str(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRelativeToComputesRelativePath()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (313, 5) - (313, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var child = new global::Sharpy.Path(tmpPath + "/a/b");
#line (314, 5) - (314, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var relative = child.RelativeTo(tmpPath);
#line (315, 5) - (315, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("a/b", global::Sharpy.Builtins.Str(relative));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRelativeToThrowsWhenNotRelative()
            {
#line (320, 5) - (324, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                bool __raised_5 = false;
#line hidden
                try
                {
#line (321, 9) - (321, 73) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    new global::Sharpy.Path("/completely/different").RelativeTo("/other/base");
#line hidden
                }
                catch (ValueError)
                {
                    __raised_5 = true;
                }

                if (!__raised_5)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestRelativeToSamePathReturnsDot()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (326, 5) - (326, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path(tmpPath);
#line (327, 5) - (327, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var result = p.RelativeTo(tmpPath);
#line (328, 5) - (328, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(".", global::Sharpy.Builtins.Str(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPartsReturnsComponents()
            {
#line (335, 5) - (335, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/usr/local/bin");
#line (336, 5) - (336, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var parts = p.Parts;
#line (337, 5) - (337, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("/", parts[0]);
#line (338, 5) - (338, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("usr", parts[1]);
#line (339, 5) - (339, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("local", parts[2]);
#line (340, 5) - (340, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("bin", parts[3]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPartsRelativePath()
            {
#line (345, 5) - (345, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("a/b/c");
#line (346, 5) - (346, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var parts = p.Parts;
#line (347, 5) - (347, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(parts));
#line (348, 5) - (348, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("a", parts[0]);
#line (349, 5) - (349, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("b", parts[1]);
#line (350, 5) - (350, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("c", parts[2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestAnchorAbsolutePath()
            {
#line (357, 5) - (357, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/usr/local");
#line (358, 5) - (358, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("/", p.Anchor);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestAnchorRelativePathIsEmpty()
            {
#line (363, 5) - (363, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("relative/path");
#line (364, 5) - (364, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("", p.Anchor);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEqualsSamePaths()
            {
#line (371, 5) - (371, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Path("/a/b"), new global::Sharpy.Path("/a/b"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEqualsDifferentPaths()
            {
#line (376, 5) - (376, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.NotEqual(new global::Sharpy.Path("/a/c"), new global::Sharpy.Path("/a/b"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetHashCodeSameForEqualPaths()
            {
#line (381, 5) - (381, 69) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Builtins.Hash(new global::Sharpy.Path("/a/b")), global::Sharpy.Builtins.Hash(new global::Sharpy.Path("/a/b")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCwdReturnsCurrentDirectory()
            {
#line (388, 5) - (388, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var cwd = global::Sharpy.Path.Cwd();
#line (389, 5) - (389, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(cwd.IsAbsolute);
#line (390, 5) - (390, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(cwd.IsDir());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHomeReturnsHomeDirectory()
            {
#line (395, 5) - (395, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var home = global::Sharpy.Path.Home();
#line (396, 5) - (396, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(home.IsAbsolute);
#line (397, 5) - (397, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(home.IsDir());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTouchCreatesNewFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (404, 5) - (404, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var path = tmpPath + "/touched.txt";
#line (405, 5) - (405, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                new global::Sharpy.Path(path).Touch();
#line (406, 5) - (406, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Path(path).Exists());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTouchExistingFileUpdatesTimestamp()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (411, 5) - (411, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var path = tmpPath + "/touch_existing.txt";
#line (412, 5) - (414, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (413, 9) - (413, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("data");
#line hidden
                }

#line (414, 5) - (414, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                double before = os.Stat(path).StMtime;
#line (415, 5) - (415, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                new global::Sharpy.Path(path).Touch();
#line (416, 5) - (416, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                double after = os.Stat(path).StMtime;
#line (417, 5) - (417, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(after >= before);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTouchExistOkFalseThrowsOnExisting()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (422, 5) - (422, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var path = tmpPath + "/touch_exists.txt";
#line (423, 5) - (425, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (424, 9) - (424, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("data");
#line hidden
                }

#line (425, 5) - (431, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                bool __raised_6 = false;
#line hidden
                try
                {
#line (426, 9) - (426, 40) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    new global::Sharpy.Path(path).Touch(false);
#line hidden
                }
                catch (FileExistsError)
                {
                    __raised_6 = true;
                }

                if (!__raised_6)
                    throw new global::Sharpy.AssertionError("Expected FileExistsError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestStatReturnsFileInfo()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (433, 5) - (433, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var path = tmpPath + "/stat_file.txt";
#line (434, 5) - (436, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (435, 9) - (435, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("hello");
#line hidden
                }

#line (436, 5) - (436, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var stat = new global::Sharpy.Path(path).Stat();
#line (437, 5) - (437, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(5, stat.StSize);
#line (438, 5) - (438, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(stat.StMtime > 0);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStatReturnsDirectoryInfo()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (443, 5) - (443, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var stat = new global::Sharpy.Path(tmpPath).Stat();
#line (444, 5) - (444, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(0, stat.StSize);
#line (445, 5) - (445, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(stat.StMtime > 0);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStatThrowsOnNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (450, 5) - (456, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                bool __raised_7 = false;
#line hidden
                try
                {
#line (451, 9) - (451, 55) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    new global::Sharpy.Path(tmpPath + "/nonexistent").Stat();
#line hidden
                }
                catch (FileNotFoundError)
                {
                    __raised_7 = true;
                }

                if (!__raised_7)
                    throw new global::Sharpy.AssertionError("Expected FileNotFoundError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestIsSymlinkFalseForRegularFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (458, 5) - (458, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var path = tmpPath + "/regular.txt";
#line (459, 5) - (461, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (460, 9) - (460, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("data");
#line hidden
                }

#line (461, 5) - (461, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path(path).IsSymlink());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsSymlinkFalseForNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (466, 5) - (466, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path(tmpPath + "/nope").IsSymlink());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRglobFindsFilesRecursively()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (473, 5) - (473, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var subDir = tmpPath + "/rglob_sub";
#line (474, 5) - (474, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                os.Makedirs(subDir, existOk: true);
#line (475, 5) - (477, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(tmpPath + "/top.txt", "w"))
#line hidden
                {
#line (476, 9) - (476, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fa.Write("");
#line hidden
                }

#line (477, 5) - (479, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(subDir + "/nested.txt", "w"))
#line hidden
                {
#line (478, 9) - (478, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    fb.Write("");
#line hidden
                }

#line (479, 5) - (479, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Sharpy.List<string> matches = new Sharpy.List<string>()
#line hidden
                {
                };
#line (480, 5) - (482, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                foreach (var __loopVar_8 in new global::Sharpy.Path(tmpPath).Rglob("*.txt"))
#line hidden
                {
                    var p = __loopVar_8;
#line (481, 9) - (481, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                    matches.Append(p.Name);
#line hidden
                }

#line (482, 5) - (482, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(Contains(matches, "top.txt"));
#line (483, 5) - (483, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(Contains(matches, "nested.txt"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMatchMatchesName()
            {
#line (490, 5) - (490, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Path("/some/path/file.txt").Match("*.txt"));
#line (491, 5) - (491, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path("/some/path/file.txt").Match("*.md"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMatchExactName()
            {
#line (496, 5) - (496, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Path("/some/path/file.txt").Match("file.txt"));
#line (497, 5) - (497, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path("/some/path/file.txt").Match("other.txt"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExpanduserExpandsTilde()
            {
#line (504, 5) - (504, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var expanded = new global::Sharpy.Path("~").Expanduser();
#line (505, 5) - (505, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(expanded.IsAbsolute);
#line (506, 5) - (506, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Builtins.Str(global::Sharpy.Path.Home()), global::Sharpy.Builtins.Str(expanded));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExpanduserExpandsTildeSlash()
            {
#line (511, 5) - (511, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var expanded = new global::Sharpy.Path("~/docs").Expanduser();
#line (512, 5) - (512, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(expanded.IsAbsolute);
#line (513, 5) - (513, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                bool hasDocs = global::Sharpy.Builtins.Str(expanded).Contains("docs");
#line (514, 5) - (514, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.True(hasDocs);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExpanduserNoTildeReturnsOriginal()
            {
#line (519, 5) - (519, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var p = new global::Sharpy.Path("/absolute/path");
#line (520, 5) - (520, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                var result = p.Expanduser();
#line (521, 5) - (521, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_tests.spy"
                Xunit.Assert.Equal("/absolute/path", global::Sharpy.Builtins.Str(result));
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
