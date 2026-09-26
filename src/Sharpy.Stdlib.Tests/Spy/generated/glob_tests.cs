// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using os = global::Sharpy.OsModule.OsModuleModule;
using glob = global::Sharpy.GlobModule;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Glob.GlobTests
{
    [global::Sharpy.SharpyModule("glob.glob_tests")]
    public static partial class GlobTestsModule
    {
        /// <summary>
        /// Create the known directory structure used by the glob tests.
        /// </summary>
        public static void BuildTree(string @base)
        {
#line (18, 5) - (18, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            os.Makedirs(@base + "/sub/deep", existOk: true);
#line (19, 5) - (20, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(@base + "/a.txt", "w"))
#line hidden
            {
#line (20, 9) - (20, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                fa.Write("a");
#line hidden
            }

#line (21, 5) - (22, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            using (var fb = global::Sharpy.Builtins.Open(@base + "/b.txt", "w"))
#line hidden
            {
#line (22, 9) - (22, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                fb.Write("b");
#line hidden
            }

#line (23, 5) - (24, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            using (var fc = global::Sharpy.Builtins.Open(@base + "/c.py", "w"))
#line hidden
            {
#line (24, 9) - (24, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                fc.Write("c");
#line hidden
            }

#line (25, 5) - (26, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            using (var fd = global::Sharpy.Builtins.Open(@base + "/data.csv", "w"))
#line hidden
            {
#line (26, 9) - (26, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                fd.Write("d");
#line hidden
            }

#line (27, 5) - (28, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            using (var fsd = global::Sharpy.Builtins.Open(@base + "/sub/d.txt", "w"))
#line hidden
            {
#line (28, 9) - (28, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                fsd.Write("d");
#line hidden
            }

#line (29, 5) - (30, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            using (var fse = global::Sharpy.Builtins.Open(@base + "/sub/e.py", "w"))
#line hidden
            {
#line (30, 9) - (30, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                fse.Write("e");
#line hidden
            }

#line (31, 5) - (32, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            using (var fsf = global::Sharpy.Builtins.Open(@base + "/sub/deep/f.txt", "w"))
#line hidden
            {
#line (32, 9) - (32, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                fsf.Write("f");
#line hidden
            }
        }

        /// <summary>
        /// Return True if any element of items ends with suffix.
        /// </summary>
        public static bool EndsWithAny(Sharpy.List<string> items, string suffix)
        {
#line (37, 5) - (39, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            foreach (var __loopVar_0 in items)
#line hidden
            {
                var item = __loopVar_0;
#line (38, 9) - (39, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                if (global::Sharpy.StringExtensions.Endswith(item, suffix))
#line hidden
                {
#line (39, 13) - (39, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                    return true;
#line hidden
                }
            }

#line (40, 5) - (40, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            return false;
#line hidden
        }
    }

    public partial class GlobTestsModuleTests : global::System.IDisposable
    {
        private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
        [Xunit.FactAttribute]
        public void TestGlobMatchesTxtFiles()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (47, 5) - (47, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (48, 5) - (48, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> results = glob.Glob(tmpPath + "/*.txt");
#line (49, 5) - (49, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(results));
#line (50, 5) - (50, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.EndsWithAny(results, "a.txt"));
#line (51, 5) - (51, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.EndsWithAny(results, "b.txt"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGlobMatchesPyFiles()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (56, 5) - (56, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (57, 5) - (57, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> results = glob.Glob(tmpPath + "/*.py");
#line (58, 5) - (58, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(results));
#line (59, 5) - (59, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.EndsWithAny(results, "c.py"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGlobRecursiveDoubleStarTxt()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (64, 5) - (64, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (66, 5) - (66, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> results = glob.Glob(tmpPath + "/**/*.txt");
#line (67, 5) - (67, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(results));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGlobQuestionMarkWildcard()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (72, 5) - (72, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (74, 5) - (74, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> results = glob.Glob(tmpPath + "/?.txt");
#line (75, 5) - (75, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(results));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGlobNoMatchesReturnsEmptyList()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (80, 5) - (80, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (81, 5) - (81, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> results = glob.Glob(tmpPath + "/*.xyz");
#line (82, 5) - (82, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(results));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGlobResultsAreSorted()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (87, 5) - (87, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (88, 5) - (88, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> results = glob.Glob(tmpPath + "/*.*");
#line (89, 5) - (89, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> expected = global::Sharpy.Builtins.Sorted<string>(results);
#line (90, 5) - (90, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.Builtins.Len(expected), global::Sharpy.Builtins.Len(results));
#line (91, 5) - (91, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            int i = 0;
#line (92, 5) - (94, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            while (i < global::Sharpy.Builtins.Len(results))
#line hidden
            {
#line (93, 9) - (93, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                Xunit.Assert.Equal(expected[i], results[i]);
#line (94, 9) - (94, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                i = i + 1;
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestGlobNonExistentDirectoryReturnsEmpty()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (99, 5) - (99, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (100, 5) - (100, 69) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> results = glob.Glob(tmpPath + "/nonexistent/*.txt");
#line (101, 5) - (101, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(results));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGlobRecursiveDoubleStarMatchesPyFiles()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (106, 5) - (106, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (108, 5) - (108, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> results = glob.Glob(tmpPath + "/**/*.py");
#line (109, 5) - (109, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(results));
#line (110, 5) - (110, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.EndsWithAny(results, "c.py"));
#line (111, 5) - (111, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.EndsWithAny(results, "e.py"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGlobCharacterClassMatchesRange()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (116, 5) - (116, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (118, 5) - (118, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> results = glob.Glob(tmpPath + "/[ab].txt");
#line (119, 5) - (119, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(results));
#line (120, 5) - (120, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.EndsWithAny(results, "a.txt"));
#line (121, 5) - (121, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.EndsWithAny(results, "b.txt"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGlobLiteralPathNoWildcardReturnsMatch()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (126, 5) - (126, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (128, 5) - (128, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> results = glob.Glob(tmpPath + "/a.txt");
#line (129, 5) - (129, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(results));
#line (130, 5) - (130, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.EndsWith("a.txt", results.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGlobEmptyPatternReturnsEmpty()
        {
#line (135, 5) - (135, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Sharpy.List<string> results = glob.Glob("");
#line (136, 5) - (136, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(results));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIglobReturnsLazyEnumerable()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (143, 5) - (143, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (145, 5) - (145, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            int count = 0;
#line (146, 5) - (147, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            foreach (var __loopVar_1 in glob.Iglob(tmpPath + "/*.txt"))
#line hidden
            {
                var path = __loopVar_1;
#line (147, 9) - (147, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                count = count + 1;
#line hidden
            }

#line (148, 5) - (148, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(2, count);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIglobIsLazilyEvaluated()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (162, 5) - (162, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Glob.GlobTests.GlobTestsModule.BuildTree(tmpPath);
#line (163, 5) - (163, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            var lazy = glob.Iglob(tmpPath + "/*.lazytest");
#line (164, 5) - (165, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            using (var f = global::Sharpy.Builtins.Open(tmpPath + "/created_after_call.lazytest", "w"))
#line hidden
            {
#line (165, 9) - (165, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                f.Write("x");
#line hidden
            }

#line (166, 5) - (166, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            int count = 0;
#line (167, 5) - (168, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            foreach (var __loopVar_2 in lazy)
#line hidden
            {
                var path = __loopVar_2;
#line (168, 9) - (168, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
                count = count + 1;
#line hidden
            }

#line (169, 5) - (169, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal(1, count);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEscapeEscapesAsterisk()
        {
#line (176, 5) - (176, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal("file[*].txt", glob.Escape("file*.txt"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEscapeEscapesQuestionMark()
        {
#line (181, 5) - (181, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal("file[?].txt", glob.Escape("file?.txt"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEscapeEscapesBracket()
        {
#line (186, 5) - (186, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal("file[[]1].txt", glob.Escape("file[1].txt"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEscapeLeavesNormalCharsUnchanged()
        {
#line (191, 5) - (191, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal("normal.txt", glob.Escape("normal.txt"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEscapeEmptyStringReturnsEmpty()
        {
#line (196, 5) - (196, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/glob/glob_tests.spy"
            Xunit.Assert.Equal("", glob.Escape(""));
#line hidden
        }

        public void Dispose()
        {
            _tmpPathFixture.Dispose();
        }
    }
}
#line default
