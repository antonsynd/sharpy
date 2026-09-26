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
using tempfile = global::Sharpy.TempfileModule.TempfileModuleModule;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Tempfile.TempfileCompleteTests
{
    [global::Sharpy.SharpyModule("tempfile.tempfile_complete_tests")]
    public static partial class TempfileCompleteTestsModule
    {
    }

    public partial class TempfileCompleteTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestGettempdirPathExists()
        {
#line (23, 5) - (23, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            string d = tempfile.Gettempdir();
#line (24, 5) - (24, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isdir(d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMkdtempPathIsInsideTempDir()
        {
#line (31, 5) - (31, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            string tempDir = tempfile.Gettempdir();
#line (32, 5) - (32, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            string d = tempfile.Mkdtemp();
#line (33, 5) - (33, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.StartsWith(tempDir, d);
#line (34, 5) - (34, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            os.Rmdir(d);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMkdtempWithSuffixPrefixAppearsInName()
        {
#line (40, 5) - (40, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            string d = tempfile.Mkdtemp("sharpy_test_");
#line (41, 5) - (41, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.StartsWith("sharpy_test_", global::Sharpy.OsPathModule.OsPathModuleModule.Basename(d));
#line (42, 5) - (42, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            os.Rmdir(d);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMkdtempCreatedDirectoryIsDeletable()
        {
#line (47, 5) - (47, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            string d = tempfile.Mkdtemp();
#line (48, 5) - (48, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isdir(d));
#line (49, 5) - (49, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            os.Rmdir(d);
#line (50, 5) - (50, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.False(global::Sharpy.OsPathModule.OsPathModuleModule.Isdir(d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMkdtempCreatedDirectoryIsEmpty()
        {
#line (55, 5) - (55, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            string d = tempfile.Mkdtemp();
#line (57, 5) - (57, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(os.Listdir(d)));
#line (58, 5) - (58, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            os.Rmdir(d);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMkstempPathIsInsideTempDir()
        {
#line (65, 5) - (65, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            string tempDir = tempfile.Gettempdir();
#line (66, 5) - (66, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            var (_fd, path) = tempfile.Mkstemp();
#line (67, 5) - (67, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.StartsWith(tempDir, path);
#line (68, 5) - (68, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            os.Remove(path);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMkstempDefaultNoSuffix()
        {
#line (73, 5) - (73, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            var (_fd, path) = tempfile.Mkstemp();
#line (75, 5) - (75, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            var (_root, ext) = global::Sharpy.OsPathModule.OsPathModuleModule.Splitext(path);
#line (76, 5) - (76, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.Equal("", ext);
#line (77, 5) - (77, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            os.Remove(path);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMkstempWithSuffixSuffixAppearsAtEnd()
        {
#line (82, 5) - (82, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            var (_fd, path) = tempfile.Mkstemp(suffix: ".txt");
#line (83, 5) - (83, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.EndsWith(".txt", global::Sharpy.OsPathModule.OsPathModuleModule.Basename(path));
#line (84, 5) - (84, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            os.Remove(path);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMkstempCreatedFileIsWritable()
        {
#line (89, 5) - (89, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            var (_fd, path) = tempfile.Mkstemp();
#line (91, 5) - (92, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            using (var f = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
            {
#line (92, 9) - (92, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
                f.Write("test content");
#line hidden
            }

#line (93, 5) - (93, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            string content = "";
#line (94, 5) - (95, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            using (var g = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
            {
#line (95, 9) - (95, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
                content = g.Read();
#line hidden
            }

#line (96, 5) - (96, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.Equal("test content", content);
#line (97, 5) - (97, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            os.Remove(path);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMkstempCreatedFileIsDeletable()
        {
#line (102, 5) - (102, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            var (_fd, path) = tempfile.Mkstemp();
#line (103, 5) - (103, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isfile(path));
#line (104, 5) - (104, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            os.Remove(path);
#line (105, 5) - (105, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.False(global::Sharpy.OsPathModule.OsPathModuleModule.Isfile(path));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMkstempReturnsZeroAsFd()
        {
#line (111, 5) - (111, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            var (fd, path) = tempfile.Mkstemp();
#line (112, 5) - (112, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            Xunit.Assert.Equal(0, fd);
#line (113, 5) - (113, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/tempfile/tempfile_complete_tests.spy"
            os.Remove(path);
#line hidden
        }
    }
}
#line default
