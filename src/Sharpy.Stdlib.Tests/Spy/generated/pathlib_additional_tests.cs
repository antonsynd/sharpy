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
using static Sharpy.Stdlib.Tests.Spy.Pathlib.PathlibAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Pathlib
    {
        [global::Sharpy.SharpyModule("pathlib.pathlib_additional_tests")]
        public static partial class PathlibAdditionalTests
        {
        }
    }

    public static partial class Pathlib
    {
        public partial class PathlibAdditionalTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestConstructorThreeSegmentsJoinsAll()
            {
#line (26, 5) - (26, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path("a", "b", "c");
#line (27, 5) - (27, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool hasA = global::Sharpy.Builtins.Str(p).Contains("a");
#line (28, 5) - (28, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.True(hasA);
#line (29, 5) - (29, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.EndsWith("c", global::Sharpy.Builtins.Str(p));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWithNameOnRootLevelReturnsNewName()
            {
#line (36, 5) - (36, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path("/file.txt").WithName("other.txt");
#line (37, 5) - (37, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal("other.txt", p.Name);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWithSuffixEmptySuffixRemovesExtension()
            {
#line (42, 5) - (42, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path("/some/file.txt").WithSuffix("");
#line (43, 5) - (43, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal("file", p.Name);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWithSuffixChangesOnlyLastSuffix()
            {
#line (48, 5) - (48, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path("/some/archive.tar.gz").WithSuffix(".bz2");
#line (49, 5) - (49, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal("archive.tar.bz2", p.Name);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDivisionChainedOperations()
            {
#line (56, 5) - (56, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path("/root") / "a" / "b" / "c";
#line (57, 5) - (57, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool hasA = global::Sharpy.Builtins.Str(p).Contains("a");
#line (58, 5) - (58, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.True(hasA);
#line (59, 5) - (59, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.EndsWith("c", global::Sharpy.Builtins.Str(p));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDivisionPathAndPathJoinsCorrectly()
            {
#line (64, 5) - (64, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var left = new global::Sharpy.Path("/base");
#line (65, 5) - (65, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var right = new global::Sharpy.Path("child");
#line (66, 5) - (66, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var result = left / right;
#line (67, 5) - (67, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool hasBase = global::Sharpy.Builtins.Str(result).Contains("base");
#line (68, 5) - (68, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool hasChild = global::Sharpy.Builtins.Str(result).Contains("child");
#line (69, 5) - (69, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.True(hasBase);
#line (70, 5) - (70, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.True(hasChild);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEqualityOperatorEqualPathsReturnsTrue()
            {
#line (77, 5) - (77, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p1 = new global::Sharpy.Path("/a/b");
#line (78, 5) - (78, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p2 = new global::Sharpy.Path("/a/b");
#line (79, 5) - (79, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal(p2, p1);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEqualityOperatorDifferentPathsReturnsFalse()
            {
#line (84, 5) - (84, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p1 = new global::Sharpy.Path("/a/b");
#line (85, 5) - (85, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p2 = new global::Sharpy.Path("/a/c");
#line (86, 5) - (86, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.NotEqual(p2, p1);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSuffixesSingleExtensionReturnsOneItem()
            {
#line (93, 5) - (93, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path("file.txt");
#line (94, 5) - (94, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var suffixes = p.Suffixes;
#line (95, 5) - (95, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(suffixes));
#line (96, 5) - (96, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal(".txt", suffixes[0]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSuffixesNoExtensionReturnsEmptyList()
            {
#line (101, 5) - (101, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path("noextension");
#line (102, 5) - (102, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var suffixes = p.Suffixes;
#line (103, 5) - (103, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(suffixes));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestParentMultiLevelReturnsImmediateParent()
            {
#line (110, 5) - (110, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path("/a/b/c/d");
#line (111, 5) - (111, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool hasC = global::Sharpy.Builtins.Str(p.Parent).Contains("c");
#line (112, 5) - (112, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.True(hasC);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestParentOfParentGrandParent()
            {
#line (117, 5) - (117, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path("/a/b/c");
#line (118, 5) - (118, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var grandParent = p.Parent.Parent;
#line (119, 5) - (119, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool hasA = global::Sharpy.Builtins.Str(grandParent).Contains("a");
#line (120, 5) - (120, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.True(hasA);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsSymlinkForDirectoryReturnsFalse()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (127, 5) - (127, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path(tmpPath).IsSymlink());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMkdirExistOkDoesNotThrowWhenExists()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (134, 5) - (134, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path(tmpPath + "/existing_dir");
#line (135, 5) - (135, 14) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                p.Mkdir();
#line (136, 5) - (136, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                p.Mkdir(false, true);
#line (137, 5) - (137, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.True(p.IsDir());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMkdirExistOkFalseThrowsWhenExists()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (142, 5) - (142, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path(tmpPath + "/dup_dir");
#line (143, 5) - (143, 14) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                p.Mkdir();
#line (144, 5) - (148, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (145, 9) - (145, 30) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    p.Mkdir(false, false);
#line hidden
                }
                catch (FileExistsError)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected FileExistsError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestMkdirMissingParentThrowsWithoutParentsFlag()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (150, 5) - (156, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (151, 9) - (151, 58) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    new global::Sharpy.Path(tmpPath + "/missing/child").Mkdir();
#line hidden
                }
                catch (FileNotFoundError)
                {
                    __raised_1 = true;
                }

                if (!__raised_1)
                    throw new global::Sharpy.AssertionError("Expected FileNotFoundError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestWriteTextAndReadTextWithAsciiEncoding()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (158, 5) - (158, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path(tmpPath + "/ascii.txt");
#line (159, 5) - (159, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                p.WriteText("hello ascii", "ascii");
#line (160, 5) - (160, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal("hello ascii", p.ReadText("ascii"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReadTextUnknownEncodingThrowsLookupError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (165, 5) - (165, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var p = new global::Sharpy.Path(tmpPath + "/enc.txt");
#line (166, 5) - (166, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                p.WriteText("data");
#line (167, 5) - (173, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (168, 9) - (168, 44) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    p.ReadText("nonexistent-encoding");
#line hidden
                }
                catch (LookupError)
                {
                    __raised_2 = true;
                }

                if (!__raised_2)
                    throw new global::Sharpy.AssertionError("Expected LookupError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestTouchMissingParentDirThrowsFileNotFoundError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (175, 5) - (181, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (176, 9) - (176, 63) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    new global::Sharpy.Path(tmpPath + "/no_parent/file.txt").Touch();
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
            public void TestIterdirNonexistentDirThrowsFileNotFoundError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (183, 5) - (189, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool __raised_5 = false;
#line hidden
                try
                {
#line (184, 9) - (184, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    Sharpy.List<string> names = new Sharpy.List<string>()
#line hidden
                    {
                    };
#line (185, 9) - (189, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    foreach (var __loopVar_4 in new global::Sharpy.Path(tmpPath + "/no_such_dir").Iterdir())
#line hidden
                    {
                        var entry = __loopVar_4;
#line (186, 13) - (186, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                        names.Append(entry.Name);
#line hidden
                    }
                }
                catch (FileNotFoundError)
                {
                    __raised_5 = true;
                }

                if (!__raised_5)
                    throw new global::Sharpy.AssertionError("Expected FileNotFoundError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestIterdirEmptyDirYieldsNothing()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (191, 5) - (191, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var d = tmpPath + "/empty_iterdir";
#line (192, 5) - (192, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                os.Makedirs(d, existOk: true);
#line (193, 5) - (193, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Sharpy.List<string> entries = new Sharpy.List<string>()
#line hidden
                {
                };
#line (194, 5) - (196, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                foreach (var __loopVar_6 in new global::Sharpy.Path(d).Iterdir())
#line hidden
                {
                    var entry = __loopVar_6;
#line (195, 9) - (195, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    entries.Append(entry.Name);
#line hidden
                }

#line (196, 5) - (196, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(entries));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGlobNoMatchesYieldsNothing()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (203, 5) - (203, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var d = tmpPath + "/glob_empty";
#line (204, 5) - (204, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                os.Makedirs(d, existOk: true);
#line (205, 5) - (207, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(d + "/file.txt", "w"))
#line hidden
                {
#line (206, 9) - (206, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    fa.Write("");
#line hidden
                }

#line (207, 5) - (207, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Sharpy.List<string> matches = new Sharpy.List<string>()
#line hidden
                {
                };
#line (208, 5) - (210, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                foreach (var __loopVar_7 in new global::Sharpy.Path(d).Glob("*.py"))
#line hidden
                {
                    var p = __loopVar_7;
#line (209, 9) - (209, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    matches.Append(p.Name);
#line hidden
                }

#line (210, 5) - (210, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(matches));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGlobNonexistentDirThrowsFileNotFoundError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (215, 5) - (223, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                bool __raised_9 = false;
#line hidden
                try
                {
#line (216, 9) - (216, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    Sharpy.List<string> names = new Sharpy.List<string>()
#line hidden
                    {
                    };
#line (217, 9) - (223, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    foreach (var __loopVar_8 in new global::Sharpy.Path(tmpPath + "/no_such_glob_dir").Glob("*"))
#line hidden
                    {
                        var p = __loopVar_8;
#line (218, 13) - (218, 33) 24 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                        names.Append(p.Name);
#line hidden
                    }
                }
                catch (FileNotFoundError)
                {
                    __raised_9 = true;
                }

                if (!__raised_9)
                    throw new global::Sharpy.AssertionError("Expected FileNotFoundError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestRelativeToNormalizesBeforeComparing()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (225, 5) - (225, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var child = new global::Sharpy.Path(tmpPath + "/sub");
#line (226, 5) - (226, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var relative = child.RelativeTo(tmpPath);
#line (227, 5) - (227, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal("sub", global::Sharpy.Builtins.Str(relative));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMatchQuestionMarkWildcardMatchesSingleChar()
            {
#line (234, 5) - (234, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Path("/a/b/fil?.txt").Match("fil?.txt"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMatchNoMatchReturnsFalse()
            {
#line (239, 5) - (239, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.False(new global::Sharpy.Path("/a/b/file.txt").Match("*.py"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRglobEmptyDirYieldsNothing()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (246, 5) - (246, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var d = tmpPath + "/rglob_empty";
#line (247, 5) - (247, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                os.Makedirs(d, existOk: true);
#line (248, 5) - (248, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Sharpy.List<string> matches = new Sharpy.List<string>()
#line hidden
                {
                };
#line (249, 5) - (251, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                foreach (var __loopVar_10 in new global::Sharpy.Path(d).Rglob("*.txt"))
#line hidden
                {
                    var p = __loopVar_10;
#line (250, 9) - (250, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                    matches.Append(p.Name);
#line hidden
                }

#line (251, 5) - (251, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(matches));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCwdIsDirectory()
            {
#line (258, 5) - (258, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var cwd = global::Sharpy.Path.Cwd();
#line (259, 5) - (259, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.True(cwd.IsDir());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHomeIsAbsoluteDir()
            {
#line (264, 5) - (264, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                var home = global::Sharpy.Path.Home();
#line (265, 5) - (265, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.True(home.IsAbsolute);
#line (266, 5) - (266, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/pathlib/pathlib_additional_tests.spy"
                Xunit.Assert.True(home.IsDir());
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
