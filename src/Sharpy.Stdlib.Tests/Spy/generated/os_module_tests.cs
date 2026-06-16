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
using static global::Sharpy.OsPathModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Os.OsModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Os
    {
        [global::Sharpy.SharpyModule("os.os_module_tests")]
        public static partial class OsModuleTests
        {
            public static bool Contains(Sharpy.List<string> items, string value)
            {
#line (23, 5) - (26, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                foreach (var __loopVar_0 in items)
                {
                    var item = __loopVar_0;
#line (24, 9) - (26, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    if (item == value)
                    {
#line (25, 13) - (25, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                        return true;
                    }
                }

#line (26, 5) - (26, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                return false;
            }
        }
    }

    public static partial class Os
    {
        public partial class OsModuleTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestSepIsNotEmpty()
            {
#line (33, 5) - (33, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(os.Sep.Length > 0);
            }

            [Xunit.FactAttribute]
            public void TestLinesepIsNotEmpty()
            {
#line (38, 5) - (38, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(os.Linesep.Length > 0);
            }

            [Xunit.FactAttribute]
            public void TestNameIsPosixOrNt()
            {
#line (43, 5) - (43, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(os.Name == "posix" || os.Name == "nt");
            }

            [Xunit.FactAttribute]
            public void TestRemoveDeletesFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (50, 5) - (50, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/removeme.txt";
#line (51, 5) - (53, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (52, 9) - (52, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    fa.Write("data");
                }

#line (53, 5) - (53, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Remove(path);
#line (54, 5) - (54, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.False(Exists(path));
            }

            [Xunit.FactAttribute]
            public void TestRemoveThrowsOnNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (59, 5) - (63, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
                {
#line (60, 9) - (60, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    os.Remove(tmpPath + "/nope.txt");
                }));
            }

            [Xunit.FactAttribute]
            public void TestRenameRenamesFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (65, 5) - (65, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var src = tmpPath + "/old.txt";
#line (66, 5) - (68, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(src, "w"))
                {
#line (67, 9) - (67, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    fa.Write("data");
                }

#line (68, 5) - (68, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var dst = tmpPath + "/new.txt";
#line (69, 5) - (69, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Rename(src, dst);
#line (70, 5) - (70, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.False(Exists(src));
#line (71, 5) - (71, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(Exists(dst));
            }

            [Xunit.FactAttribute]
            public void TestRenameThrowsOnNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (76, 5) - (82, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
                {
#line (77, 9) - (77, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    os.Rename(tmpPath + "/nope.txt", tmpPath + "/also_nope.txt");
                }));
            }

            [Xunit.FactAttribute]
            public void TestMkdirCreatesDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (84, 5) - (84, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/newdir";
#line (85, 5) - (85, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Mkdir(path);
#line (86, 5) - (86, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(Isdir(path));
            }

            [Xunit.FactAttribute]
            public void TestMkdirThrowsIfExists()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (91, 5) - (91, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/existdir";
#line (92, 5) - (92, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Mkdir(path);
#line (93, 5) - (97, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Throws<FileExistsError>((global::System.Action)(() =>
                {
#line (94, 9) - (94, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    os.Mkdir(path);
                }));
            }

            [Xunit.FactAttribute]
            public void TestMakedirsCreatesNestedDirectories()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (99, 5) - (99, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/a/b/c";
#line (100, 5) - (100, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Makedirs(path);
#line (101, 5) - (101, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(Isdir(path));
            }

            [Xunit.FactAttribute]
            public void TestMakedirsExistOkDoesNotThrow()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (106, 5) - (106, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/existing";
#line (107, 5) - (107, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Makedirs(path);
#line (108, 5) - (108, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Makedirs(path, existOk: true);
#line (109, 5) - (109, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(Isdir(path));
            }

            [Xunit.FactAttribute]
            public void TestMakedirsNotExistOkThrows()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (114, 5) - (114, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/existing2";
#line (115, 5) - (115, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Makedirs(path);
#line (116, 5) - (120, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Throws<FileExistsError>((global::System.Action)(() =>
                {
#line (117, 9) - (117, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    os.Makedirs(path, existOk: false);
                }));
            }

            [Xunit.FactAttribute]
            public void TestRmdirRemovesEmptyDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (122, 5) - (122, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/emptydir";
#line (123, 5) - (123, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Mkdir(path);
#line (124, 5) - (124, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Rmdir(path);
#line (125, 5) - (125, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.False(Isdir(path));
            }

            [Xunit.FactAttribute]
            public void TestRmdirThrowsOnNonEmpty()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (130, 5) - (130, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/notempty";
#line (131, 5) - (131, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Mkdir(path);
#line (132, 5) - (134, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path + "/file.txt", "w"))
                {
#line (133, 9) - (133, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    fa.Write("data");
                }

#line (134, 5) - (138, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Throws<IOError>((global::System.Action)(() =>
                {
#line (135, 9) - (135, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    os.Rmdir(path);
                }));
            }

            [Xunit.FactAttribute]
            public void TestRmdirThrowsOnNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (140, 5) - (144, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
                {
#line (141, 9) - (141, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    os.Rmdir(tmpPath + "/nope");
                }));
            }

            [Xunit.FactAttribute]
            public void TestListdirReturnsEntries()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (146, 5) - (146, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var d = tmpPath + "/listdir";
#line (147, 5) - (147, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Mkdir(d);
#line (148, 5) - (150, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(d + "/a.txt", "w"))
                {
#line (149, 9) - (149, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    fa.Write("");
                }

#line (150, 5) - (152, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(d + "/b.txt", "w"))
                {
#line (151, 9) - (151, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    fb.Write("");
                }

#line (152, 5) - (152, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Mkdir(d + "/subdir");
#line (153, 5) - (153, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var entries = os.Listdir(d);
#line (154, 5) - (154, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(Contains(entries, "a.txt"));
#line (155, 5) - (155, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(Contains(entries, "b.txt"));
#line (156, 5) - (156, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(Contains(entries, "subdir"));
            }

            [Xunit.FactAttribute]
            public void TestListdirThrowsOnNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (161, 5) - (165, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
                {
#line (162, 9) - (162, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    os.Listdir(tmpPath + "/nope");
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetcwdReturnsNonEmptyString()
            {
#line (167, 5) - (167, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var cwd = os.Getcwd();
#line (168, 5) - (168, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(cwd.Length > 0);
            }

            [Xunit.FactAttribute]
            public void TestChdirChangesDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (173, 5) - (173, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var original = os.Getcwd();
#line (174, 5) - (174, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Chdir(tmpPath);
#line (175, 5) - (175, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var cwd = os.Getcwd();
#line (176, 5) - (176, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var dirName = Basename(tmpPath);
#line (177, 5) - (177, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                bool found = cwd.Contains(dirName);
#line (179, 5) - (179, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Chdir(original);
#line (180, 5) - (180, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(found);
            }

            [Xunit.FactAttribute]
            public void TestChdirThrowsOnNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (185, 5) - (191, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
                {
#line (186, 9) - (186, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    os.Chdir(tmpPath + "/nope");
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetenvReturnsNullForMissing()
            {
#line (193, 5) - (193, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Null(os.Getenv("SHARPY_TEST_NONEXISTENT_VAR_ABC123"));
            }

            [Xunit.FactAttribute]
            public void TestGetenvWithDefaultReturnsDefault()
            {
#line (198, 5) - (198, 86) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Equal("fallback", os.Getenv("SHARPY_TEST_NONEXISTENT_VAR_ABC123", "fallback"));
            }

            [Xunit.FactAttribute]
            public void TestPutenvAndGetenvRoundTrip()
            {
#line (203, 5) - (203, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var key = "SHARPY_TEST_ROUNDTRIP_KEY_ABC";
#line (204, 5) - (204, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Putenv(key, "testvalue");
#line (205, 5) - (205, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Equal("testvalue", os.Getenv(key));
            }

            [Xunit.FactAttribute]
            public void TestEnvironReturnsDictWithEntries()
            {
#line (210, 5) - (210, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(os.Environ) > 0);
            }

            [Xunit.FactAttribute]
            public void TestPathExistsTrueForFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (217, 5) - (217, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/exists_file.txt";
#line (218, 5) - (220, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (219, 9) - (219, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    fa.Write("data");
                }

#line (220, 5) - (220, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(os.PathExists(path));
            }

            [Xunit.FactAttribute]
            public void TestPathExistsTrueForDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (225, 5) - (225, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/exists_dir";
#line (226, 5) - (226, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Mkdir(path);
#line (227, 5) - (227, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(os.PathExists(path));
            }

            [Xunit.FactAttribute]
            public void TestPathExistsFalseForNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (232, 5) - (232, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.False(os.PathExists(tmpPath + "/nonexistent_path"));
            }

            [Xunit.FactAttribute]
            public void TestStatReturnsFileSize()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (239, 5) - (239, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/stat_file.txt";
#line (240, 5) - (242, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (241, 9) - (241, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    fa.Write("hello");
                }

#line (242, 5) - (242, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var result = os.Stat(path);
#line (243, 5) - (243, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(result.StSize > 0);
            }

            [Xunit.FactAttribute]
            public void TestStatReturnsTimestamps()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (248, 5) - (248, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/stat_time.txt";
#line (249, 5) - (251, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (250, 9) - (250, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    fa.Write("data");
                }

#line (251, 5) - (251, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var result = os.Stat(path);
#line (253, 5) - (253, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(result.StMtime > 1577836800);
#line (254, 5) - (254, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(result.StCtime > 1577836800);
#line (255, 5) - (255, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(result.StAtime > 1577836800);
            }

            [Xunit.FactAttribute]
            public void TestStatWorksForDirectories()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (260, 5) - (260, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var path = tmpPath + "/stat_dir";
#line (261, 5) - (261, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Mkdir(path);
#line (262, 5) - (262, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var result = os.Stat(path);
#line (263, 5) - (263, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Equal(0, result.StSize);
#line (264, 5) - (264, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.True(result.StMtime > 1577836800);
            }

            [Xunit.FactAttribute]
            public void TestStatThrowsOnNonexistent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (269, 5) - (275, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
                {
#line (270, 9) - (270, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    os.Stat(tmpPath + "/nonexistent_stat");
                }));
            }

            [Xunit.FactAttribute]
            public void TestWalkTraversesDirectoryTree()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (277, 5) - (277, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var root = tmpPath + "/walktest";
#line (278, 5) - (278, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Mkdir(root);
#line (279, 5) - (281, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(root + "/file1.txt", "w"))
                {
#line (280, 9) - (280, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    fa.Write("");
                }

#line (281, 5) - (281, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                var sub = root + "/sub";
#line (282, 5) - (282, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                os.Mkdir(sub);
#line (283, 5) - (285, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(sub + "/file2.txt", "w"))
                {
#line (284, 9) - (284, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    fb.Write("");
                }

#line (285, 5) - (285, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Sharpy.List<string> results = new Sharpy.List<string>()
                {
                };
#line (286, 5) - (288, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                foreach (var (dirpath, dirnames, filenames) in os.Walk(root))
                {
#line (287, 9) - (287, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    results.Append(dirpath);
                }

#line (288, 5) - (288, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(results));
#line (289, 5) - (289, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Equal(root, results[0]);
#line (290, 5) - (290, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Equal(sub, results[1]);
            }

            [Xunit.FactAttribute]
            public void TestWalkNonexistentPathYieldsNothing()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (295, 5) - (295, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                int count = 0;
#line (296, 5) - (298, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                foreach (var (dirpath, dirnames, filenames) in os.Walk(tmpPath + "/nonexistent"))
                {
#line (297, 9) - (297, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                    count = count + 1;
                }

#line (298, 5) - (298, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_tests.spy"
                Xunit.Assert.Equal(0, count);
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
