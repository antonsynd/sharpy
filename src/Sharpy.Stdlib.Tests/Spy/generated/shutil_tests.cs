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
using shutil = global::Sharpy.ShutilModule;
using static global::Sharpy.OsPathModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Shutil.ShutilTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Shutil
    {
        [global::Sharpy.SharpyModule("shutil.shutil_tests")]
        public static partial class ShutilTests
        {
        }
    }

    public static partial class Shutil
    {
        public partial class ShutilTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestCopyCopiesToFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (25, 5) - (25, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var src = tmpPath + "/src.txt";
#line (26, 5) - (28, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
                {
#line (27, 9) - (27, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    fa.Write("hello");
                }

#line (28, 5) - (28, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var dst = tmpPath + "/dst.txt";
#line (29, 5) - (29, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var result = shutil.Copy(src, dst);
#line (30, 5) - (30, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.True(Isfile(dst));
#line (31, 5) - (31, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var content = "";
#line (32, 5) - (34, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(dst, "r"))
                {
#line (33, 9) - (33, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    content = fb.Read();
                }

#line (34, 5) - (34, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Equal("hello", content);
#line (35, 5) - (35, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Equal(dst, result);
            }

            [Xunit.FactAttribute]
            public void TestCopyCopiesToDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (40, 5) - (40, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var src = tmpPath + "/src.txt";
#line (41, 5) - (43, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
                {
#line (42, 9) - (42, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    fa.Write("hello");
                }

#line (43, 5) - (43, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var destdir = tmpPath + "/destdir";
#line (44, 5) - (44, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                os.Makedirs(destdir, existOk: true);
#line (45, 5) - (45, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var result = shutil.Copy(src, destdir);
#line (46, 5) - (46, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var expected = destdir + "/src.txt";
#line (47, 5) - (47, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.True(Isfile(expected));
#line (48, 5) - (48, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Equal(expected, result);
            }

            [Xunit.FactAttribute]
            public void TestCopyThrowsOnNonexistentSource()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (53, 5) - (59, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Throws<OSError>((global::System.Action)(() =>
                {
#line (54, 9) - (54, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    shutil.Copy(tmpPath + "/nope.txt", tmpPath + "/dst.txt");
                }));
            }

            [Xunit.FactAttribute]
            public void TestCopy2PreservesTimestamps()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (61, 5) - (61, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var src = tmpPath + "/src2.txt";
#line (62, 5) - (64, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
                {
#line (63, 9) - (63, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    fa.Write("data");
                }

#line (64, 5) - (64, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                double srcMtime = os.Stat(src).StMtime;
#line (65, 5) - (65, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var result = shutil.Copy2(src, tmpPath + "/dst2.txt");
#line (66, 5) - (66, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Equal(srcMtime, os.Stat(result).StMtime);
            }

            [Xunit.FactAttribute]
            public void TestCopytreeCopiesRecursively()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (73, 5) - (73, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var srcDir = tmpPath + "/treesrc";
#line (74, 5) - (74, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                os.Makedirs(srcDir + "/sub", existOk: true);
#line (75, 5) - (77, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(srcDir + "/a.txt", "w"))
                {
#line (76, 9) - (76, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    fa.Write("a");
                }

#line (77, 5) - (79, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(srcDir + "/sub/b.txt", "w"))
                {
#line (78, 9) - (78, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    fb.Write("b");
                }

#line (79, 5) - (79, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var dstDir = tmpPath + "/treedst";
#line (80, 5) - (80, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var result = shutil.Copytree(srcDir, dstDir);
#line (81, 5) - (81, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Equal(dstDir, result);
#line (82, 5) - (82, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.True(Isfile(dstDir + "/a.txt"));
#line (83, 5) - (83, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.True(Isfile(dstDir + "/sub/b.txt"));
#line (84, 5) - (84, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var content = "";
#line (85, 5) - (87, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fc = global::Sharpy.Builtins.Open(dstDir + "/sub/b.txt", "r"))
                {
#line (86, 9) - (86, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    content = fc.Read();
                }

#line (87, 5) - (87, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Equal("b", content);
            }

            [Xunit.FactAttribute]
            public void TestCopytreeThrowsOnNonexistentSource()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (92, 5) - (98, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Throws<OSError>((global::System.Action)(() =>
                {
#line (93, 9) - (93, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    shutil.Copytree(tmpPath + "/nope", tmpPath + "/dst");
                }));
            }

            [Xunit.FactAttribute]
            public void TestRmtreeDeletesDirectoryTree()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (100, 5) - (100, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var d = tmpPath + "/rmdir";
#line (101, 5) - (101, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                os.Makedirs(d + "/sub", existOk: true);
#line (102, 5) - (104, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(d + "/file.txt", "w"))
                {
#line (103, 9) - (103, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    fa.Write("data");
                }

#line (104, 5) - (106, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(d + "/sub/inner.txt", "w"))
                {
#line (105, 9) - (105, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    fb.Write("inner");
                }

#line (106, 5) - (106, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                shutil.Rmtree(d);
#line (107, 5) - (107, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.False(Isdir(d));
            }

            [Xunit.FactAttribute]
            public void TestRmtreeThrowsOnNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (112, 5) - (118, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Throws<OSError>((global::System.Action)(() =>
                {
#line (113, 9) - (113, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    shutil.Rmtree(tmpPath + "/nope");
                }));
            }

            [Xunit.FactAttribute]
            public void TestMoveMovesFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (120, 5) - (120, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var src = tmpPath + "/movesrc.txt";
#line (121, 5) - (123, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
                {
#line (122, 9) - (122, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    fa.Write("move");
                }

#line (123, 5) - (123, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var dst = tmpPath + "/movedst.txt";
#line (124, 5) - (124, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var result = shutil.Move(src, dst);
#line (125, 5) - (125, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.False(Exists(src));
#line (126, 5) - (126, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.True(Isfile(dst));
#line (127, 5) - (127, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var content = "";
#line (128, 5) - (130, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(dst, "r"))
                {
#line (129, 9) - (129, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    content = fb.Read();
                }

#line (130, 5) - (130, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Equal("move", content);
#line (131, 5) - (131, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Equal(dst, result);
            }

            [Xunit.FactAttribute]
            public void TestMoveMovesDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (136, 5) - (136, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var srcDir = tmpPath + "/movedirsrc";
#line (137, 5) - (137, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                os.Makedirs(srcDir, existOk: true);
#line (138, 5) - (140, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(srcDir + "/f.txt", "w"))
                {
#line (139, 9) - (139, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    fa.Write("f");
                }

#line (140, 5) - (140, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var dstDir = tmpPath + "/movedirdst";
#line (141, 5) - (141, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var result = shutil.Move(srcDir, dstDir);
#line (142, 5) - (142, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.False(Isdir(srcDir));
#line (143, 5) - (143, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.True(Isdir(dstDir));
#line (144, 5) - (144, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.True(Isfile(dstDir + "/f.txt"));
#line (145, 5) - (145, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Equal(dstDir, result);
            }

            [Xunit.FactAttribute]
            public void TestMoveThrowsOnNonexistentSource()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (150, 5) - (156, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Throws<OSError>((global::System.Action)(() =>
                {
#line (151, 9) - (151, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    shutil.Move(tmpPath + "/nope.txt", tmpPath + "/dst.txt");
                }));
            }

            [Xunit.FactAttribute]
            public void TestWhichFindsDotnet()
            {
#line (158, 5) - (158, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var result = shutil.Which("dotnet");
#line (159, 5) - (159, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.NotNull(result);
#line (160, 5) - (164, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                if (result != null)
                {
#line (161, 9) - (161, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                    Xunit.Assert.True(Isfile(result));
                }
            }

            [Xunit.FactAttribute]
            public void TestWhichReturnsNullForNonexistent()
            {
#line (166, 5) - (166, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Null(shutil.Which("this_command_does_not_exist_xyz_123"));
            }

            [Xunit.FactAttribute]
            public void TestWhichReturnsNullForEmpty()
            {
#line (171, 5) - (171, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Null(shutil.Which(""));
            }

            [Xunit.FactAttribute]
            public void TestDiskUsageReturnsSensibleValues()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (178, 5) - (178, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                var (total, used, free) = shutil.DiskUsage(tmpPath);
#line (179, 5) - (179, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.True(total > 0);
#line (180, 5) - (180, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.True(used >= 0);
#line (181, 5) - (181, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.True(free >= 0);
#line (182, 5) - (182, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_tests.spy"
                Xunit.Assert.Equal(total, used + free);
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
