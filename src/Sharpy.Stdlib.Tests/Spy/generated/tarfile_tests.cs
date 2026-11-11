// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

#pragma warning disable xUnit2009, xUnit2017

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using tarfile = global::Sharpy.TarfileModule;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Tarfile.TarfileTests
{
    [global::Sharpy.SharpyModule("tarfile.tarfile_tests")]
    public static partial class TarfileTestsModule
    {
    }

    public partial class TarfileTestsModuleTests : global::System.IDisposable
    {
        private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
        [Xunit.FactAttribute]
        public void TestCreateAndReadUncompressedTar()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (25, 5) - (25, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/test.tar";
#line (26, 5) - (26, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/hello.txt";
#line (27, 5) - (28, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (28, 9) - (28, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("Hello, World!");
#line hidden
            }

#line (29, 5) - (30, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:"))
#line hidden
            {
#line (30, 9) - (30, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"hello.txt");
#line hidden
            }

#line (31, 5) - (34, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tr = tarfile.Open(archive, "r:"))
#line hidden
            {
#line (32, 9) - (32, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var names = tr.Getnames();
#line (33, 9) - (33, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                bool hasHello = names.Contains("hello.txt");
#line (34, 9) - (34, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(hasHello);
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestCreateAndReadGzipTar()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (39, 5) - (39, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/test.tar.gz";
#line (40, 5) - (40, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/data.txt";
#line (41, 5) - (42, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (42, 9) - (42, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("Compressed content");
#line hidden
            }

#line (43, 5) - (44, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:gz"))
#line hidden
            {
#line (44, 9) - (44, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"data.txt");
#line hidden
            }

#line (45, 5) - (48, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tr = tarfile.Open(archive, "r:gz"))
#line hidden
            {
#line (46, 9) - (46, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var names = tr.Getnames();
#line (47, 9) - (47, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                bool hasData = names.Contains("data.txt");
#line (48, 9) - (48, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(hasData);
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestAutoDetectGzipTar()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (53, 5) - (53, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/auto.tar.gz";
#line (54, 5) - (54, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/auto.txt";
#line (55, 5) - (56, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (56, 9) - (56, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("auto");
#line hidden
            }

#line (57, 5) - (58, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:gz"))
#line hidden
            {
#line (58, 9) - (58, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"auto.txt");
#line hidden
            }

#line (59, 5) - (62, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tr = tarfile.Open(archive, "r"))
#line hidden
            {
#line (60, 9) - (60, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var names = tr.Getnames();
#line (61, 9) - (61, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                bool hasAuto = names.Contains("auto.txt");
#line (62, 9) - (62, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(hasAuto);
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestExtractallExtractsFiles()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (69, 5) - (69, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/extract.tar";
#line (70, 5) - (70, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/source.txt";
#line (71, 5) - (71, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var outDir = tmpPath + "/output";
#line (72, 5) - (73, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (73, 9) - (73, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("Extract me!");
#line hidden
            }

#line (74, 5) - (75, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:"))
#line hidden
            {
#line (75, 9) - (75, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"source.txt");
#line hidden
            }

#line (76, 5) - (77, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tr = tarfile.Open(archive, "r:"))
#line hidden
            {
#line (77, 9) - (77, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tr.Extractall((string?)outDir);
#line hidden
            }

#line (78, 5) - (78, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Isfile(outDir + "/source.txt"));
#line (79, 5) - (79, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var content = "";
#line (80, 5) - (81, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fr = global::Sharpy.Builtins.Open(outDir + "/source.txt", "r"))
#line hidden
            {
#line (81, 9) - (81, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                content = fr.Read();
#line hidden
            }

#line (82, 5) - (82, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.Equal("Extract me!", content);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestExtractfileReturnsContent()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (87, 5) - (87, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/file.tar";
#line (88, 5) - (88, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/content.txt";
#line (89, 5) - (90, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (90, 9) - (90, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("File content");
#line hidden
            }

#line (91, 5) - (92, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:"))
#line hidden
            {
#line (92, 9) - (92, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"content.txt");
#line hidden
            }

#line (93, 5) - (97, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tr = tarfile.Open(archive, "r:"))
#line hidden
            {
#line (94, 9) - (94, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var data = tr.Extractfile("content.txt");
#line (95, 9) - (95, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.NotNull(data);
#line (96, 9) - (97, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                if (data is not null)
#line hidden
                {
#line (97, 13) - (97, 59) 20 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Equal("File content", data.Value.Decode("utf-8"));
#line hidden
                }
            }
        }

        [Xunit.FactAttribute]
        public void TestGetmembersReturnsTarInfoObjects()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (104, 5) - (104, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/members.tar";
#line (105, 5) - (105, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/info.txt";
#line (106, 5) - (107, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (107, 9) - (107, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("twelve chars");
#line hidden
            }

#line (108, 5) - (109, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:"))
#line hidden
            {
#line (109, 9) - (109, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"info.txt");
#line hidden
            }

#line (110, 5) - (116, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tr = tarfile.Open(archive, "r:"))
#line hidden
            {
#line (111, 9) - (111, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var members = tr.Getmembers();
#line (112, 9) - (112, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(members));
#line (113, 9) - (113, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal("info.txt", members[0].Name);
#line (114, 9) - (114, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(members[0].Isfile());
#line (115, 9) - (115, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.False(members[0].Isdir());
#line (116, 9) - (116, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(12, members[0].Size);
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestGetmemberExistingMemberReturnsInfo()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (121, 5) - (121, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/member.tar";
#line (122, 5) - (122, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/a.txt";
#line (123, 5) - (124, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (124, 9) - (124, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("aaa");
#line hidden
            }

#line (125, 5) - (126, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:"))
#line hidden
            {
#line (126, 9) - (126, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"a.txt");
#line hidden
            }

#line (127, 5) - (129, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tr = tarfile.Open(archive, "r:"))
#line hidden
            {
#line (128, 9) - (128, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var info = tr.Getmember("a.txt");
#line (129, 9) - (129, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal("a.txt", info.Name);
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestGetmemberNonExistentThrowsKeyError()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (134, 5) - (134, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/miss.tar";
#line (135, 5) - (135, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/a.txt";
#line (136, 5) - (137, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (137, 9) - (137, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("aaa");
#line hidden
            }

#line (138, 5) - (139, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:"))
#line hidden
            {
#line (139, 9) - (139, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"a.txt");
#line hidden
            }

#line (140, 5) - (142, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tr = tarfile.Open(archive, "r:"))
#line hidden
            {
#line (141, 9) - (142, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (142, 13) - (142, 40) 20 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tr.Getmember("nonexistent");
#line hidden
                }
                catch (KeyError)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected KeyError to be raised, but no exception was raised");
            }
        }

        [Xunit.FactAttribute]
        public void TestAddWithArcnameUsesArchiveName()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (147, 5) - (147, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/arcname.tar";
#line (148, 5) - (148, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/original.txt";
#line (149, 5) - (150, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (150, 9) - (150, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("renamed");
#line hidden
            }

#line (151, 5) - (152, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:"))
#line hidden
            {
#line (152, 9) - (152, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"renamed.txt");
#line hidden
            }

#line (153, 5) - (156, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tr = tarfile.Open(archive, "r:"))
#line hidden
            {
#line (154, 9) - (154, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var names = tr.Getnames();
#line (155, 9) - (155, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                bool hasRenamed = names.Contains("renamed.txt");
#line (156, 9) - (156, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(hasRenamed);
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestIsTarfileValidTarReturnsTrue()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (163, 5) - (163, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/valid.tar";
#line (164, 5) - (164, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/x.txt";
#line (165, 5) - (166, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (166, 9) - (166, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("x");
#line hidden
            }

#line (167, 5) - (168, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:"))
#line hidden
            {
#line (168, 9) - (168, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"x.txt");
#line hidden
            }

#line (169, 5) - (169, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.True(tarfile.IsTarfile(archive));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsTarfileNonTarReturnsFalse()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (174, 5) - (174, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/notatar.txt";
#line (175, 5) - (176, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (176, 9) - (176, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("This is not a tar file");
#line hidden
            }

#line (177, 5) - (177, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.False(tarfile.IsTarfile(fp));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsTarfileNonExistentThrowsFileNotFoundError()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (183, 5) - (184, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (184, 9) - (184, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tarfile.IsTarfile(tmpPath + "/nonexistent.tar");
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
        public void TestIsTarfileGzipTarReturnsTrue()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (189, 5) - (189, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/valid.tar.gz";
#line (190, 5) - (190, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/y.txt";
#line (191, 5) - (192, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (192, 9) - (192, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("y");
#line hidden
            }

#line (193, 5) - (194, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:gz"))
#line hidden
            {
#line (194, 9) - (194, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"y.txt");
#line hidden
            }

#line (195, 5) - (195, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.True(tarfile.IsTarfile(archive));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestOpenInvalidModeThrowsValueError()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (202, 5) - (203, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (203, 9) - (203, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tarfile.Open(tmpPath + "/bad.tar", "x:");
#line hidden
            }
            catch (ValueError)
            {
                __raised_2 = true;
            }

            if (!__raised_2)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestOpenBz2ModeThrowsCompressionError()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (208, 5) - (209, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            bool __raised_3 = false;
#line hidden
            try
            {
#line (209, 9) - (209, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tarfile.Open(tmpPath + "/bad.tar.bz2", "r:bz2");
#line hidden
            }
            catch (global::Sharpy.CompressionError)
            {
                __raised_3 = true;
            }

            if (!__raised_3)
                throw new global::Sharpy.AssertionError("Expected CompressionError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestOpenXzModeThrowsCompressionError()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (214, 5) - (215, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            bool __raised_4 = false;
#line hidden
            try
            {
#line (215, 9) - (215, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tarfile.Open(tmpPath + "/bad.tar.xz", "w:xz");
#line hidden
            }
            catch (global::Sharpy.CompressionError)
            {
                __raised_4 = true;
            }

            if (!__raised_4)
                throw new global::Sharpy.AssertionError("Expected CompressionError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestOpenNonExistentFileThrowsFileNotFoundError()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (220, 5) - (221, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            bool __raised_5 = false;
#line hidden
            try
            {
#line (221, 9) - (221, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tarfile.Open(tmpPath + "/nonexistent.tar", "r:");
#line hidden
            }
            catch (FileNotFoundError)
            {
                __raised_5 = true;
            }

            if (!__raised_5)
                throw new global::Sharpy.AssertionError("Expected FileNotFoundError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestClosePreventsFurtherOperations()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (228, 5) - (228, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/closed.tar";
#line (229, 5) - (229, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var fp = tmpPath + "/c.txt";
#line (230, 5) - (231, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
#line hidden
            {
#line (231, 9) - (231, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("c");
#line hidden
            }

#line (232, 5) - (233, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:"))
#line hidden
            {
#line (233, 9) - (233, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(fp, (string?)"c.txt");
#line hidden
            }

#line (234, 5) - (234, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var readTar = tarfile.Open(archive, "r:");
#line (235, 5) - (235, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            readTar.Close();
#line (236, 5) - (237, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            bool __raised_6 = false;
#line hidden
            try
            {
#line (237, 9) - (237, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                readTar.Getnames();
#line hidden
            }
            catch (ValueError)
            {
                __raised_6 = true;
            }

            if (!__raised_6)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestModuleConstants()
        {
#line (244, 5) - (244, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.TarfileModule.REGTYPE);
#line (245, 5) - (245, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.Equal(5, global::Sharpy.TarfileModule.DIRTYPE);
#line (246, 5) - (246, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.TarfileModule.SYMTYPE);
#line (247, 5) - (247, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.TarfileModule.LNKTYPE);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestErrorHierarchy()
        {
#line (252, 5) - (252, 68) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.IsAssignableFrom<global::Sharpy.TarError>((object?)new global::Sharpy.ReadError("test"));
#line (253, 5) - (253, 75) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.IsAssignableFrom<global::Sharpy.TarError>((object?)new global::Sharpy.CompressionError("test"));
#line (254, 5) - (254, 71) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.IsAssignableFrom<global::Sharpy.TarError>((object?)new global::Sharpy.ExtractError("test"));
#line (255, 5) - (255, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.IsAssignableFrom<global::System.Exception>((object?)new global::Sharpy.TarError("test"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMultipleFilesGetNames()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (262, 5) - (262, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var archive = tmpPath + "/multi.tar";
#line (263, 5) - (264, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fa = global::Sharpy.Builtins.Open(tmpPath + "/a.txt", "w"))
#line hidden
            {
#line (264, 9) - (264, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fa.Write("aaa");
#line hidden
            }

#line (265, 5) - (266, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var fb = global::Sharpy.Builtins.Open(tmpPath + "/b.txt", "w"))
#line hidden
            {
#line (266, 9) - (266, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                fb.Write("bbb");
#line hidden
            }

#line (267, 5) - (269, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tw = tarfile.Open(archive, "w:"))
#line hidden
            {
#line (268, 9) - (268, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(tmpPath + "/a.txt", (string?)"a.txt");
#line (269, 9) - (269, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                tw.Add(tmpPath + "/b.txt", (string?)"b.txt");
#line hidden
            }

#line (270, 5) - (276, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            using (var tr = tarfile.Open(archive, "r:"))
#line hidden
            {
#line (271, 9) - (271, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var names = tr.Getnames();
#line (272, 9) - (272, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(names));
#line (273, 9) - (273, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                bool hasA = names.Contains("a.txt");
#line (274, 9) - (274, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                bool hasB = names.Contains("b.txt");
#line (275, 9) - (275, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(hasA);
#line (276, 9) - (276, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(hasB);
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestTarinfoDefaultProperties()
        {
#line (283, 5) - (283, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var info = new global::Sharpy.TarInfo();
#line (284, 5) - (284, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.Equal("", info.Name);
#line (285, 5) - (285, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.Equal(0, info.Size);
#line (286, 5) - (286, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.Equal("", info.Linkname);
#line (287, 5) - (287, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.Equal("", info.Uname);
#line (288, 5) - (288, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.Equal("", info.Gname);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTarinfoTypeChecks()
        {
#line (292, 5) - (292, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var info = new global::Sharpy.TarInfo();
#line (293, 5) - (293, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            info.Type = global::Sharpy.TarfileModule.REGTYPE;
#line (294, 5) - (294, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.True(info.Isfile());
#line (295, 5) - (295, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.False(info.Isdir());
#line (297, 5) - (297, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            info.Type = global::Sharpy.TarfileModule.DIRTYPE;
#line (298, 5) - (298, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.True(info.Isdir());
#line (299, 5) - (299, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.False(info.Isfile());
#line (301, 5) - (301, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            info.Type = global::Sharpy.TarfileModule.SYMTYPE;
#line (302, 5) - (302, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.True(info.Issym());
#line (304, 5) - (304, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            info.Type = global::Sharpy.TarfileModule.LNKTYPE;
#line (305, 5) - (305, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.True(info.Islnk());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTarinfoTostring()
        {
#line (309, 5) - (309, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            var info = new global::Sharpy.TarInfo("test.txt");
#line (310, 5) - (310, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
            Xunit.Assert.Equal("<TarInfo 'test.txt'>", global::Sharpy.Builtins.Str(info));
#line hidden
        }

        public void Dispose()
        {
            _tmpPathFixture.Dispose();
        }
    }
}
#line default
