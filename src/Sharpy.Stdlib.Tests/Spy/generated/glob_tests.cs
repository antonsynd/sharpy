// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using os = global::Sharpy.OsModule;
using glob = global::Sharpy.GlobModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Glob.GlobTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Glob
    {
        [global::Sharpy.SharpyModule("glob.glob_tests")]
        public static partial class GlobTests
        {
            /// <summary>
            /// Create the known directory structure used by the glob tests.
            /// </summary>
            public static void BuildTree(string @base)
            {
#line (18, 5) - (18, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                os.Makedirs(@base + "/sub/deep", existOk: true);
#line (19, 5) - (21, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(@base + "/a.txt", "w"))
                {
#line (20, 9) - (20, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    fa.Write("a");
                }

#line (21, 5) - (23, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(@base + "/b.txt", "w"))
                {
#line (22, 9) - (22, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    fb.Write("b");
                }

#line (23, 5) - (25, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                using (var fc = global::Sharpy.Builtins.Open(@base + "/c.py", "w"))
                {
#line (24, 9) - (24, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    fc.Write("c");
                }

#line (25, 5) - (27, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                using (var fd = global::Sharpy.Builtins.Open(@base + "/data.csv", "w"))
                {
#line (26, 9) - (26, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    fd.Write("d");
                }

#line (27, 5) - (29, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                using (var fsd = global::Sharpy.Builtins.Open(@base + "/sub/d.txt", "w"))
                {
#line (28, 9) - (28, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    fsd.Write("d");
                }

#line (29, 5) - (31, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                using (var fse = global::Sharpy.Builtins.Open(@base + "/sub/e.py", "w"))
                {
#line (30, 9) - (30, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    fse.Write("e");
                }

#line (31, 5) - (35, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                using (var fsf = global::Sharpy.Builtins.Open(@base + "/sub/deep/f.txt", "w"))
                {
#line (32, 9) - (32, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    fsf.Write("f");
                }
            }

            /// <summary>
            /// Return True if any element of items ends with suffix.
            /// </summary>
            public static bool EndsWithAny(Sharpy.List<string> items, string suffix)
            {
#line (37, 5) - (40, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                foreach (var __loopVar_0 in items)
                {
                    var item = __loopVar_0;
#line (38, 9) - (40, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    if (item.Endswith(suffix))
                    {
#line (39, 13) - (39, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                        return true;
                    }
                }

#line (40, 5) - (40, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                return false;
            }
        }
    }

    public static partial class Glob
    {
        public partial class GlobTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestGlobMatchesTxtFiles()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (47, 5) - (47, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (48, 5) - (48, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> results = glob.Glob(tmpPath + "/*.txt");
#line (49, 5) - (49, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(results));
#line (50, 5) - (50, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.True(EndsWithAny(results, "a.txt"));
#line (51, 5) - (51, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.True(EndsWithAny(results, "b.txt"));
            }

            [Xunit.FactAttribute]
            public void TestGlobMatchesPyFiles()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (56, 5) - (56, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (57, 5) - (57, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> results = glob.Glob(tmpPath + "/*.py");
#line (58, 5) - (58, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(results));
#line (59, 5) - (59, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.True(EndsWithAny(results, "c.py"));
            }

            [Xunit.FactAttribute]
            public void TestGlobRecursiveDoubleStarTxt()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (64, 5) - (64, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (66, 5) - (66, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> results = glob.Glob(tmpPath + "/**/*.txt");
#line (67, 5) - (67, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(results));
            }

            [Xunit.FactAttribute]
            public void TestGlobQuestionMarkWildcard()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (72, 5) - (72, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (74, 5) - (74, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> results = glob.Glob(tmpPath + "/?.txt");
#line (75, 5) - (75, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(results));
            }

            [Xunit.FactAttribute]
            public void TestGlobNoMatchesReturnsEmptyList()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (80, 5) - (80, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (81, 5) - (81, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> results = glob.Glob(tmpPath + "/*.xyz");
#line (82, 5) - (82, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(results));
            }

            [Xunit.FactAttribute]
            public void TestGlobResultsAreSorted()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (87, 5) - (87, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (88, 5) - (88, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> results = glob.Glob(tmpPath + "/*.*");
#line (89, 5) - (89, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> expected = global::Sharpy.Builtins.Sorted<string>(results);
#line (90, 5) - (90, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Builtins.Len(expected), global::Sharpy.Builtins.Len(results));
#line (91, 5) - (91, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                int i = 0;
#line (92, 5) - (97, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                while (i < global::Sharpy.Builtins.Len(results))
                {
#line (93, 9) - (93, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    Xunit.Assert.Equal(expected[i], results[i]);
#line (94, 9) - (94, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestGlobNonExistentDirectoryReturnsEmpty()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (99, 5) - (99, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (100, 5) - (100, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> results = glob.Glob(tmpPath + "/nonexistent/*.txt");
#line (101, 5) - (101, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(results));
            }

            [Xunit.FactAttribute]
            public void TestGlobRecursiveDoubleStarMatchesPyFiles()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (106, 5) - (106, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (108, 5) - (108, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> results = glob.Glob(tmpPath + "/**/*.py");
#line (109, 5) - (109, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(results));
#line (110, 5) - (110, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.True(EndsWithAny(results, "c.py"));
#line (111, 5) - (111, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.True(EndsWithAny(results, "e.py"));
            }

            [Xunit.FactAttribute]
            public void TestGlobCharacterClassMatchesRange()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (116, 5) - (116, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (118, 5) - (118, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> results = glob.Glob(tmpPath + "/[ab].txt");
#line (119, 5) - (119, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(results));
#line (120, 5) - (120, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.True(EndsWithAny(results, "a.txt"));
#line (121, 5) - (121, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.True(EndsWithAny(results, "b.txt"));
            }

            [Xunit.FactAttribute]
            public void TestGlobLiteralPathNoWildcardReturnsMatch()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (126, 5) - (126, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (128, 5) - (128, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> results = glob.Glob(tmpPath + "/a.txt");
#line (129, 5) - (129, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(results));
#line (130, 5) - (130, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.EndsWith("a.txt", results[0]);
            }

            [Xunit.FactAttribute]
            public void TestGlobEmptyPatternReturnsEmpty()
            {
#line (135, 5) - (135, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Sharpy.List<string> results = glob.Glob("");
#line (136, 5) - (136, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(results));
            }

            [Xunit.FactAttribute]
            public void TestIglobReturnsLazyEnumerable()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (143, 5) - (143, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (145, 5) - (145, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                int count = 0;
#line (146, 5) - (148, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                foreach (var __loopVar_1 in glob.Iglob(tmpPath + "/*.txt"))
                {
                    var path = __loopVar_1;
#line (147, 9) - (147, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    count = count + 1;
                }

#line (148, 5) - (148, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(2, count);
            }

            [Xunit.FactAttribute]
            public void TestIglobIsLazilyEvaluated()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (153, 5) - (153, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                BuildTree(tmpPath);
#line (156, 5) - (156, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                var lazy = glob.Iglob(tmpPath + "/*.lazytest");
#line (157, 5) - (159, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                using (var f = global::Sharpy.Builtins.Open(tmpPath + "/created_after_call.lazytest", "w"))
                {
#line (158, 9) - (158, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    f.Write("x");
                }

#line (159, 5) - (159, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                int count = 0;
#line (160, 5) - (162, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                foreach (var __loopVar_2 in lazy)
                {
                    var path = __loopVar_2;
#line (161, 9) - (161, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    count = count + 1;
                }

#line (162, 5) - (162, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(1, count);
            }

            [Xunit.FactAttribute]
            public void TestEscapeEscapesAsterisk()
            {
#line (169, 5) - (169, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal("file[*].txt", glob.Escape("file*.txt"));
            }

            [Xunit.FactAttribute]
            public void TestEscapeEscapesQuestionMark()
            {
#line (174, 5) - (174, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal("file[?].txt", glob.Escape("file?.txt"));
            }

            [Xunit.FactAttribute]
            public void TestEscapeEscapesBracket()
            {
#line (179, 5) - (179, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal("file[[]1].txt", glob.Escape("file[1].txt"));
            }

            [Xunit.FactAttribute]
            public void TestEscapeLeavesNormalCharsUnchanged()
            {
#line (184, 5) - (184, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal("normal.txt", glob.Escape("normal.txt"));
            }

            [Xunit.FactAttribute]
            public void TestEscapeEmptyStringReturnsEmpty()
            {
#line (189, 5) - (189, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal("", glob.Escape(""));
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
