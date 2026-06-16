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
using static Sharpy.Stdlib.Tests.Spy.Shutil.ShutilAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Shutil
    {
        [global::Sharpy.SharpyModule("shutil.shutil_additional_tests")]
        public static partial class ShutilAdditionalTests
        {
        }
    }

    public static partial class Shutil
    {
        public partial class ShutilAdditionalTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestCopyOverwritesExistingDestination()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (21, 5) - (21, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var src = tmpPath + "/src.txt";
#line (22, 5) - (24, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
                {
#line (23, 9) - (23, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    fa.Write("new content");
                }

#line (24, 5) - (24, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var dst = tmpPath + "/dst.txt";
#line (25, 5) - (27, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(dst, "w"))
                {
#line (26, 9) - (26, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    fb.Write("old content");
                }

#line (27, 5) - (27, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                shutil.Copy(src, dst);
#line (28, 5) - (28, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var content = "";
#line (29, 5) - (31, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fc = global::Sharpy.Builtins.Open(dst, "r"))
                {
#line (30, 9) - (30, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    content = fc.Read();
                }

#line (31, 5) - (31, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.Equal("new content", content);
            }

            [Xunit.FactAttribute]
            public void TestCopyContentMatchesSource()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (36, 5) - (36, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var text = "hello from source";
#line (37, 5) - (37, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var src = tmpPath + "/content_src.txt";
#line (38, 5) - (40, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
                {
#line (39, 9) - (39, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    fa.Write(text);
                }

#line (40, 5) - (40, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var dst = tmpPath + "/content_dst.txt";
#line (41, 5) - (41, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                shutil.Copy(src, dst);
#line (42, 5) - (42, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var content = "";
#line (43, 5) - (45, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(dst, "r"))
                {
#line (44, 9) - (44, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    content = fb.Read();
                }

#line (45, 5) - (45, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.Equal(text, content);
            }

            [Xunit.FactAttribute]
            public void TestCopy2ToDirectoryCopiesIntoDir()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (52, 5) - (52, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var src = tmpPath + "/src2.txt";
#line (53, 5) - (55, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
                {
#line (54, 9) - (54, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    fa.Write("data");
                }

#line (55, 5) - (55, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var destdir = tmpPath + "/copy2dir";
#line (56, 5) - (56, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                os.Makedirs(destdir, existOk: true);
#line (57, 5) - (57, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var result = shutil.Copy2(src, destdir);
#line (58, 5) - (58, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.True(Isfile(result));
#line (59, 5) - (59, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var content = "";
#line (60, 5) - (62, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(result, "r"))
                {
#line (61, 9) - (61, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    content = fb.Read();
                }

#line (62, 5) - (62, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.Equal("data", content);
            }

            [Xunit.FactAttribute]
            public void TestCopy2NonexistentSourceThrowsOsError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (67, 5) - (73, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.Throws<OSError>((global::System.Action)(() =>
                {
#line (68, 9) - (68, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    shutil.Copy2(tmpPath + "/nope.txt", tmpPath + "/dst.txt");
                }));
            }

            [Xunit.FactAttribute]
            public void TestCopytreeDeeplyNestedCopiesAllLevels()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (75, 5) - (75, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var src = tmpPath + "/deep_src";
#line (76, 5) - (76, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var l1 = src + "/l1";
#line (77, 5) - (77, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var l2 = l1 + "/l2";
#line (78, 5) - (78, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                os.Makedirs(l2, existOk: true);
#line (79, 5) - (81, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src + "/root.txt", "w"))
                {
#line (80, 9) - (80, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    fa.Write("root");
                }

#line (81, 5) - (83, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(l1 + "/mid.txt", "w"))
                {
#line (82, 9) - (82, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    fb.Write("mid");
                }

#line (83, 5) - (85, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fc = global::Sharpy.Builtins.Open(l2 + "/deep.txt", "w"))
                {
#line (84, 9) - (84, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    fc.Write("deep");
                }

#line (85, 5) - (85, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var dst = tmpPath + "/deep_dst";
#line (86, 5) - (86, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                shutil.Copytree(src, dst);
#line (87, 5) - (87, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.True(Isfile(dst + "/root.txt"));
#line (88, 5) - (88, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.True(Isfile(dst + "/l1/mid.txt"));
#line (89, 5) - (89, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.True(Isfile(dst + "/l1/l2/deep.txt"));
#line (90, 5) - (90, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var content = "";
#line (91, 5) - (93, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fd = global::Sharpy.Builtins.Open(dst + "/l1/l2/deep.txt", "r"))
                {
#line (92, 9) - (92, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    content = fd.Read();
                }

#line (93, 5) - (93, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.Equal("deep", content);
            }

            [Xunit.FactAttribute]
            public void TestRmtreeEmptyDirectoryRemoves()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (100, 5) - (100, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var d = tmpPath + "/rmtree_empty";
#line (101, 5) - (101, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                os.Makedirs(d, existOk: true);
#line (102, 5) - (102, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                shutil.Rmtree(d);
#line (103, 5) - (103, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.False(Isdir(d));
            }

            [Xunit.FactAttribute]
            public void TestRmtreeWithSubdirectoriesRemovesAll()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (108, 5) - (108, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var d = tmpPath + "/rmtree_subs";
#line (109, 5) - (109, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var sub = d + "/sub";
#line (110, 5) - (110, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                os.Makedirs(sub, existOk: true);
#line (111, 5) - (113, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(sub + "/f.txt", "w"))
                {
#line (112, 9) - (112, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    fa.Write("data");
                }

#line (113, 5) - (113, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                shutil.Rmtree(d);
#line (114, 5) - (114, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.False(Isdir(d));
            }

            [Xunit.FactAttribute]
            public void TestMoveFileContentPreserved()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (121, 5) - (121, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var src = tmpPath + "/move_content_src.txt";
#line (122, 5) - (124, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
                {
#line (123, 9) - (123, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    fa.Write("preserved");
                }

#line (124, 5) - (124, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var dst = tmpPath + "/move_content_dst.txt";
#line (125, 5) - (125, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                shutil.Move(src, dst);
#line (126, 5) - (126, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var content = "";
#line (127, 5) - (129, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(dst, "r"))
                {
#line (128, 9) - (128, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    content = fb.Read();
                }

#line (129, 5) - (129, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.Equal("preserved", content);
            }

            [Xunit.FactAttribute]
            public void TestMoveFileIntoDirectoryPlacesInDir()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (134, 5) - (134, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var src = tmpPath + "/move_into_src.txt";
#line (135, 5) - (137, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
                {
#line (136, 9) - (136, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    fa.Write("data");
                }

#line (137, 5) - (137, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var destdir = tmpPath + "/move_into_dir";
#line (138, 5) - (138, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                os.Makedirs(destdir, existOk: true);
#line (139, 5) - (139, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var result = shutil.Move(src, destdir);
#line (141, 5) - (141, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.False(Exists(src));
#line (143, 5) - (143, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.True(result.Length > 0);
            }

            [Xunit.FactAttribute]
            public void TestWhichFindsLsOnUnix()
            {
#line (150, 5) - (150, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var result = shutil.Which("ls");
#line (151, 5) - (151, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.NotNull(result);
#line (152, 5) - (156, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                if (result != null)
                {
#line (153, 9) - (153, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    Xunit.Assert.True(Isfile(result));
                }
            }

            [Xunit.FactAttribute]
            public void TestWhichPathWithSeparatorChecksDirectly()
            {
#line (159, 5) - (159, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var lsPath = shutil.Which("ls");
#line (160, 5) - (165, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                if (lsPath != null)
                {
#line (161, 9) - (161, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    var result = shutil.Which(lsPath);
#line (162, 9) - (162, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    Xunit.Assert.NotNull(result);
                }
            }

            [Xunit.FactAttribute]
            public void TestWhichPathWithSeparatorNonexistentReturnsNull()
            {
#line (167, 5) - (167, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.Null(shutil.Which("/nonexistent_xyz_path/binary"));
            }

            [Xunit.FactAttribute]
            public void TestDiskUsageForFileReturnsSensibleValues()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (174, 5) - (174, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var f = tmpPath + "/diskusage.txt";
#line (175, 5) - (177, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(f, "w"))
                {
#line (176, 9) - (176, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                    fa.Write("data");
                }

#line (177, 5) - (177, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var (total, used, free) = shutil.DiskUsage(f);
#line (178, 5) - (178, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.True(total > 0);
#line (179, 5) - (179, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.Equal(total, used + free);
            }

            [Xunit.FactAttribute]
            public void TestDiskUsageTotalGreaterThanUsedAndFree()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (184, 5) - (184, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                var (total, used, free) = shutil.DiskUsage(tmpPath);
#line (185, 5) - (185, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.True(total >= used);
#line (186, 5) - (186, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/shutil/shutil_additional_tests.spy"
                Xunit.Assert.True(total >= free);
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
