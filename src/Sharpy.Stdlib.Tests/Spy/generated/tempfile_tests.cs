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
using tempfile = global::Sharpy.TempfileModule;
using static global::Sharpy.OsPathModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Tempfile.TempfileTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Tempfile
    {
        [global::Sharpy.SharpyModule("tempfile.tempfile_tests")]
        public static partial class TempfileTests
        {
        }
    }

    public static partial class Tempfile
    {
        public partial class TempfileTestsTests
        {
            [Xunit.FactAttribute]
            public void TestGettempdirReturnsNonEmptyPath()
            {
#line (23, 5) - (23, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                string result = tempfile.Gettempdir();
#line (24, 5) - (24, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.True(result.Length > 0);
            }

            [Xunit.FactAttribute]
            public void TestGettempdirReturnsValidDirectory()
            {
#line (29, 5) - (29, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                string result = tempfile.Gettempdir();
#line (30, 5) - (30, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.True(Isdir(result));
            }

            [Xunit.FactAttribute]
            public void TestGettempdirDoesNotEndWithSeparator()
            {
#line (35, 5) - (35, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                string result = tempfile.Gettempdir();
#line (36, 5) - (36, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.False(result.Endswith("/"));
            }

            [Xunit.FactAttribute]
            public void TestMkdtempCreatesDirectory()
            {
#line (43, 5) - (43, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                string d = tempfile.Mkdtemp();
#line (44, 5) - (44, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.True(Isdir(d));
#line (45, 5) - (45, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                os.Rmdir(d);
            }

            [Xunit.FactAttribute]
            public void TestMkdtempUsesDefaultPrefix()
            {
#line (50, 5) - (50, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                string d = tempfile.Mkdtemp();
#line (51, 5) - (51, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.StartsWith("tmp", Basename(d));
#line (52, 5) - (52, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                os.Rmdir(d);
            }

            [Xunit.FactAttribute]
            public void TestMkdtempUsesCustomPrefix()
            {
#line (57, 5) - (57, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                string d = tempfile.Mkdtemp("myapp_");
#line (58, 5) - (58, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.StartsWith("myapp_", Basename(d));
#line (59, 5) - (59, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                os.Rmdir(d);
            }

            [Xunit.FactAttribute]
            public void TestMkdtempCreatesUniqueDirectories()
            {
#line (64, 5) - (64, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                string dir1 = tempfile.Mkdtemp();
#line (65, 5) - (65, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                string dir2 = tempfile.Mkdtemp();
#line (66, 5) - (66, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.NotEqual(dir2, dir1);
#line (67, 5) - (67, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                os.Rmdir(dir1);
#line (68, 5) - (68, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                os.Rmdir(dir2);
            }

            [Xunit.FactAttribute]
            public void TestMkstempCreatesFile()
            {
#line (75, 5) - (75, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                var (fd, path) = tempfile.Mkstemp();
#line (76, 5) - (76, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.True(Isfile(path));
#line (77, 5) - (77, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.Equal(0, fd);
#line (78, 5) - (78, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                os.Remove(path);
            }

            [Xunit.FactAttribute]
            public void TestMkstempUsesDefaultPrefix()
            {
#line (83, 5) - (83, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                var (_fd, path) = tempfile.Mkstemp();
#line (84, 5) - (84, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.StartsWith("tmp", Basename(path));
#line (85, 5) - (85, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                os.Remove(path);
            }

            [Xunit.FactAttribute]
            public void TestMkstempUsesCustomPrefixAndSuffix()
            {
#line (90, 5) - (90, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                var (_fd, path) = tempfile.Mkstemp("data_", ".csv");
#line (91, 5) - (91, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                string name = Basename(path);
#line (92, 5) - (92, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.StartsWith("data_", name);
#line (93, 5) - (93, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.EndsWith(".csv", name);
#line (94, 5) - (94, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                os.Remove(path);
            }

            [Xunit.FactAttribute]
            public void TestMkstempCreatesUniqueFiles()
            {
#line (99, 5) - (99, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                var (_fd1, path1) = tempfile.Mkstemp();
#line (100, 5) - (100, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                var (_fd2, path2) = tempfile.Mkstemp();
#line (101, 5) - (101, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                Xunit.Assert.NotEqual(path2, path1);
#line (102, 5) - (102, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                os.Remove(path1);
#line (103, 5) - (103, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_tests.spy"
                os.Remove(path2);
            }
        }
    }
}
