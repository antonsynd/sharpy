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
using static Sharpy.Stdlib.Tests.Spy.Os.OsModuleAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Os
    {
        [global::Sharpy.SharpyModule("os.os_module_additional_tests")]
        public static partial class OsModuleAdditionalTests
        {
            public static bool Contains(Sharpy.List<string> items, string value)
            {
#line (17, 5) - (20, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                foreach (var __loopVar_0 in items)
                {
                    var item = __loopVar_0;
#line (18, 9) - (20, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    if (item == value)
                    {
#line (19, 13) - (19, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                        return true;
                    }
                }

#line (20, 5) - (20, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                return false;
            }
        }
    }

    public static partial class Os
    {
        public partial class OsModuleAdditionalTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestAltsepIsStringValue()
            {
#line (27, 5) - (27, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                string s = os.Altsep;
#line (29, 5) - (29, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(s.Length >= 0);
            }

            [Xunit.FactAttribute]
            public void TestPathsepIsNotEmpty()
            {
#line (34, 5) - (34, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(os.Pathsep.Length > 0);
            }

            [Xunit.FactAttribute]
            public void TestRemoveOnDirectoryPathThrowsFileNotFoundError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (42, 5) - (42, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var d = tmpPath + "/adirtoremove";
#line (43, 5) - (43, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                os.Mkdir(d);
#line (44, 5) - (48, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
                {
#line (45, 9) - (45, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    os.Remove(d);
                }));
            }

            [Xunit.FactAttribute]
            public void TestRenameRenamesDirectory()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (50, 5) - (50, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var src = tmpPath + "/rename_src_dir";
#line (51, 5) - (51, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                os.Mkdir(src);
#line (52, 5) - (52, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var dst = tmpPath + "/rename_dst_dir";
#line (53, 5) - (53, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                os.Rename(src, dst);
#line (54, 5) - (54, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.False(Isdir(src));
#line (55, 5) - (55, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(Isdir(dst));
            }

            [Xunit.FactAttribute]
            public void TestMkdirThrowsWhenParentMissing()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (62, 5) - (66, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
                {
#line (63, 9) - (63, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    os.Mkdir(tmpPath + "/missing_parent/child");
                }));
            }

            [Xunit.FactAttribute]
            public void TestListdirEmptyDirectoryReturnsEmptyList()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (68, 5) - (68, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var d = tmpPath + "/empty_listdir_dir";
#line (69, 5) - (69, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                os.Mkdir(d);
#line (70, 5) - (70, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var entries = os.Listdir(d);
#line (71, 5) - (71, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(entries));
            }

            [Xunit.FactAttribute]
            public void TestListdirCurrentDirReturnsEntries()
            {
#line (77, 5) - (77, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var entries = os.Listdir(".");
#line (78, 5) - (78, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(entries) >= 0);
            }

            [Xunit.FactAttribute]
            public void TestWalkYieldsCorrectFilenames()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (85, 5) - (85, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var root = tmpPath + "/walk_files";
#line (86, 5) - (86, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                os.Mkdir(root);
#line (87, 5) - (89, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(root + "/alpha.txt", "w"))
                {
#line (88, 9) - (88, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    fa.Write("");
                }

#line (89, 5) - (91, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(root + "/beta.txt", "w"))
                {
#line (90, 9) - (90, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    fb.Write("");
                }

#line (91, 5) - (91, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                bool found = false;
#line (92, 5) - (97, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                foreach (var (dirpath, dirnames, filenames) in os.Walk(root))
                {
#line (93, 9) - (97, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    if (dirpath == root)
                    {
#line (94, 13) - (94, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                        found = true;
#line (95, 13) - (95, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                        Xunit.Assert.True(Contains(filenames, "alpha.txt"));
#line (96, 13) - (96, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                        Xunit.Assert.True(Contains(filenames, "beta.txt"));
                    }
                }

#line (97, 5) - (97, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(found);
            }

            [Xunit.FactAttribute]
            public void TestWalkYieldsCorrectDirnames()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (102, 5) - (102, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var root = tmpPath + "/walk_dirs";
#line (103, 5) - (103, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                os.Mkdir(root);
#line (104, 5) - (104, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                os.Mkdir(root + "/subA");
#line (105, 5) - (105, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                os.Mkdir(root + "/subB");
#line (106, 5) - (106, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                bool found = false;
#line (107, 5) - (112, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                foreach (var (dirpath, dirnames, filenames) in os.Walk(root))
                {
#line (108, 9) - (112, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    if (dirpath == root)
                    {
#line (109, 13) - (109, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                        found = true;
#line (110, 13) - (110, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                        Xunit.Assert.True(Contains(dirnames, "subA"));
#line (111, 13) - (111, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                        Xunit.Assert.True(Contains(dirnames, "subB"));
                    }
                }

#line (112, 5) - (112, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(found);
            }

            [Xunit.FactAttribute]
            public void TestWalkDeepTreeVisitsAllLevels()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (117, 5) - (117, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var root = tmpPath + "/walk_deep";
#line (118, 5) - (118, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var level1 = root + "/level1";
#line (119, 5) - (119, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var level2 = level1 + "/level2";
#line (120, 5) - (120, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                os.Makedirs(level2, existOk: true);
#line (121, 5) - (123, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(level2 + "/deep.txt", "w"))
                {
#line (122, 9) - (122, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    fa.Write("");
                }

#line (123, 5) - (123, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Sharpy.List<string> visited = new Sharpy.List<string>()
                {
                };
#line (124, 5) - (126, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                foreach (var (dirpath, dirnames, filenames) in os.Walk(root))
                {
#line (125, 9) - (125, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    visited.Append(dirpath);
                }

#line (126, 5) - (126, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(Contains(visited, root));
#line (127, 5) - (127, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(Contains(visited, level1));
#line (128, 5) - (128, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(Contains(visited, level2));
            }

            [Xunit.FactAttribute]
            public void TestEnvironContainsPathVariable()
            {
#line (135, 5) - (135, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var env = os.Environ;
#line (136, 5) - (136, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                bool hasPath = env.Contains("PATH") || env.Contains("Path");
#line (137, 5) - (137, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(hasPath);
            }

            [Xunit.FactAttribute]
            public void TestGetenvReturnsExistingVariable()
            {
#line (143, 5) - (143, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var pathVal = os.Getenv("PATH");
#line (144, 5) - (144, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.NotNull(pathVal);
#line (145, 5) - (151, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                if (pathVal != null)
                {
#line (146, 9) - (146, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    Xunit.Assert.True(pathVal.Length > 0);
                }
            }

            [Xunit.FactAttribute]
            public void TestStatModeFieldIsNonNegative()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (153, 5) - (153, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var path = tmpPath + "/stat_mode.txt";
#line (154, 5) - (156, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (155, 9) - (155, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    fa.Write("mode test");
                }

#line (156, 5) - (156, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var result = os.Stat(path);
#line (157, 5) - (157, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(result.StMode >= 0);
            }

            [Xunit.FactAttribute]
            public void TestStatEmptyFileHasZeroSize()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (162, 5) - (162, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var path = tmpPath + "/empty_stat.txt";
#line (163, 5) - (165, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (164, 9) - (164, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                    fa.Write("");
                }

#line (165, 5) - (165, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var result = os.Stat(path);
#line (166, 5) - (166, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.Equal(0, result.StSize);
            }

            [Xunit.FactAttribute]
            public void TestGetcwdIsAbsolutePath()
            {
#line (173, 5) - (173, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var cwd = os.Getcwd();
#line (174, 5) - (174, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(Isabs(cwd));
            }

            [Xunit.FactAttribute]
            public void TestMakedirsDeepNestingCreatesAll()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (181, 5) - (181, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                var deep = tmpPath + "/x/y/z/w";
#line (182, 5) - (182, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                os.Makedirs(deep);
#line (183, 5) - (183, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/os/os_module_additional_tests.spy"
                Xunit.Assert.True(Isdir(deep));
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
