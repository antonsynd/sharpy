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
using tarfile = global::Sharpy.TarfileModule;
using static global::Sharpy.OsPathModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Tarfile.TarfileTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Tarfile
    {
        [global::Sharpy.SharpyModule("tarfile.tarfile_tests")]
        public static partial class TarfileTests
        {
        }
    }

    public static partial class Tarfile
    {
        public partial class TarfileTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestCreateAndReadUncompressedTar()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (25, 5) - (25, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/test.tar";
#line (26, 5) - (26, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/hello.txt";
#line (27, 5) - (29, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (28, 9) - (28, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("Hello, World!");
                }

#line (29, 5) - (31, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (30, 9) - (30, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "hello.txt");
                }

#line (31, 5) - (37, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (32, 9) - (32, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var names = tr.Getnames();
#line (33, 9) - (33, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasHello = names.Contains("hello.txt");
#line (34, 9) - (34, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasHello);
                }
            }

            [Xunit.FactAttribute]
            public void TestCreateAndReadGzipTar()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (39, 5) - (39, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/test.tar.gz";
#line (40, 5) - (40, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/data.txt";
#line (41, 5) - (43, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (42, 9) - (42, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("Compressed content");
                }

#line (43, 5) - (45, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:gz"))
                {
#line (44, 9) - (44, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "data.txt");
                }

#line (45, 5) - (51, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:gz"))
                {
#line (46, 9) - (46, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var names = tr.Getnames();
#line (47, 9) - (47, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasData = names.Contains("data.txt");
#line (48, 9) - (48, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasData);
                }
            }

            [Xunit.FactAttribute]
            public void TestAutoDetectGzipTar()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (53, 5) - (53, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/auto.tar.gz";
#line (54, 5) - (54, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/auto.txt";
#line (55, 5) - (57, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (56, 9) - (56, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("auto");
                }

#line (57, 5) - (59, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:gz"))
                {
#line (58, 9) - (58, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "auto.txt");
                }

#line (59, 5) - (67, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r"))
                {
#line (60, 9) - (60, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var names = tr.Getnames();
#line (61, 9) - (61, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasAuto = names.Contains("auto.txt");
#line (62, 9) - (62, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasAuto);
                }
            }

            [Xunit.FactAttribute]
            public void TestExtractallExtractsFiles()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (69, 5) - (69, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/extract.tar";
#line (70, 5) - (70, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/source.txt";
#line (71, 5) - (71, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var outDir = tmpPath + "/output";
#line (72, 5) - (74, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (73, 9) - (73, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("Extract me!");
                }

#line (74, 5) - (76, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (75, 9) - (75, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "source.txt");
                }

#line (76, 5) - (78, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (77, 9) - (77, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tr.Extractall(outDir);
                }

#line (78, 5) - (78, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(Isfile(outDir + "/source.txt"));
#line (79, 5) - (79, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var content = "";
#line (80, 5) - (82, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fr = global::Sharpy.Builtins.Open(outDir + "/source.txt", "r"))
                {
#line (81, 9) - (81, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    content = fr.Read();
                }

#line (82, 5) - (82, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal("Extract me!", content);
            }

            [Xunit.FactAttribute]
            public void TestExtractfileReturnsContent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (87, 5) - (87, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/file.tar";
#line (88, 5) - (88, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/content.txt";
#line (89, 5) - (91, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (90, 9) - (90, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("File content");
                }

#line (91, 5) - (93, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (92, 9) - (92, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "content.txt");
                }

#line (93, 5) - (102, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (94, 9) - (94, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var data = tr.Extractfile("content.txt");
#line (95, 9) - (95, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.NotNull(data);
#line (96, 9) - (102, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    if (data != null)
                    {
#line (97, 13) - (97, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                        Xunit.Assert.Equal("File content", data.Value.Decode("utf-8"));
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestGetmembersReturnsTarInfoObjects()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (104, 5) - (104, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/members.tar";
#line (105, 5) - (105, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/info.txt";
#line (106, 5) - (108, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (107, 9) - (107, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("twelve chars");
                }

#line (108, 5) - (110, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (109, 9) - (109, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "info.txt");
                }

#line (110, 5) - (119, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (111, 9) - (111, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var members = tr.Getmembers();
#line (112, 9) - (112, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(members));
#line (113, 9) - (113, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Equal("info.txt", members[0].Name);
#line (114, 9) - (114, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(members[0].Isfile());
#line (115, 9) - (115, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.False(members[0].Isdir());
#line (116, 9) - (116, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Equal(12, members[0].Size);
                }
            }

            [Xunit.FactAttribute]
            public void TestGetmemberExistingMemberReturnsInfo()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (121, 5) - (121, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/member.tar";
#line (122, 5) - (122, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/a.txt";
#line (123, 5) - (125, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (124, 9) - (124, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("aaa");
                }

#line (125, 5) - (127, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (126, 9) - (126, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "a.txt");
                }

#line (127, 5) - (132, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (128, 9) - (128, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var info = tr.Getmember("a.txt");
#line (129, 9) - (129, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Equal("a.txt", info.Name);
                }
            }

            [Xunit.FactAttribute]
            public void TestGetmemberNonExistentThrowsKeyError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (134, 5) - (134, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/miss.tar";
#line (135, 5) - (135, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/a.txt";
#line (136, 5) - (138, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (137, 9) - (137, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("aaa");
                }

#line (138, 5) - (140, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (139, 9) - (139, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "a.txt");
                }

#line (140, 5) - (145, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (141, 9) - (145, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Throws<KeyError>((global::System.Action)(() =>
                    {
#line (142, 13) - (142, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                        tr.Getmember("nonexistent");
                    }));
                }
            }

            [Xunit.FactAttribute]
            public void TestAddWithArcnameUsesArchiveName()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (147, 5) - (147, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/arcname.tar";
#line (148, 5) - (148, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/original.txt";
#line (149, 5) - (151, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (150, 9) - (150, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("renamed");
                }

#line (151, 5) - (153, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (152, 9) - (152, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "renamed.txt");
                }

#line (153, 5) - (161, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (154, 9) - (154, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var names = tr.Getnames();
#line (155, 9) - (155, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasRenamed = names.Contains("renamed.txt");
#line (156, 9) - (156, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasRenamed);
                }
            }

            [Xunit.FactAttribute]
            public void TestIsTarfileValidTarReturnsTrue()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (163, 5) - (163, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/valid.tar";
#line (164, 5) - (164, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/x.txt";
#line (165, 5) - (167, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (166, 9) - (166, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("x");
                }

#line (167, 5) - (169, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (168, 9) - (168, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "x.txt");
                }

#line (169, 5) - (169, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(tarfile.IsTarfile(archive));
            }

            [Xunit.FactAttribute]
            public void TestIsTarfileNonTarReturnsFalse()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (174, 5) - (174, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/notatar.txt";
#line (175, 5) - (177, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (176, 9) - (176, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("This is not a tar file");
                }

#line (177, 5) - (177, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.False(tarfile.IsTarfile(fp));
            }

            [Xunit.FactAttribute]
            public void TestIsTarfileNonExistentReturnsFalse()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (182, 5) - (182, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.False(tarfile.IsTarfile(tmpPath + "/nonexistent.tar"));
            }

            [Xunit.FactAttribute]
            public void TestIsTarfileGzipTarReturnsTrue()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (187, 5) - (187, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/valid.tar.gz";
#line (188, 5) - (188, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/y.txt";
#line (189, 5) - (191, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (190, 9) - (190, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("y");
                }

#line (191, 5) - (193, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:gz"))
                {
#line (192, 9) - (192, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "y.txt");
                }

#line (193, 5) - (193, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(tarfile.IsTarfile(archive));
            }

            [Xunit.FactAttribute]
            public void TestOpenInvalidModeThrowsValueError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (200, 5) - (204, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (201, 9) - (201, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tarfile.Open(tmpPath + "/bad.tar", "x:");
                }));
            }

            [Xunit.FactAttribute]
            public void TestOpenBz2ModeThrowsCompressionError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (206, 5) - (210, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.CompressionError>((global::System.Action)(() =>
                {
#line (207, 9) - (207, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tarfile.Open(tmpPath + "/bad.tar.bz2", "r:bz2");
                }));
            }

            [Xunit.FactAttribute]
            public void TestOpenXzModeThrowsCompressionError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (212, 5) - (216, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.CompressionError>((global::System.Action)(() =>
                {
#line (213, 9) - (213, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tarfile.Open(tmpPath + "/bad.tar.xz", "w:xz");
                }));
            }

            [Xunit.FactAttribute]
            public void TestOpenNonExistentFileThrowsFileNotFoundError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (218, 5) - (224, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
                {
#line (219, 9) - (219, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tarfile.Open(tmpPath + "/nonexistent.tar", "r:");
                }));
            }

            [Xunit.FactAttribute]
            public void TestClosePreventsFurtherOperations()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (226, 5) - (226, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/closed.tar";
#line (227, 5) - (227, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/c.txt";
#line (228, 5) - (230, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (229, 9) - (229, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("c");
                }

#line (230, 5) - (232, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (231, 9) - (231, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "c.txt");
                }

#line (232, 5) - (232, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var readTar = tarfile.Open(archive, "r:");
#line (233, 5) - (233, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                readTar.Close();
#line (234, 5) - (240, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (235, 9) - (235, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    readTar.Getnames();
                }));
            }

            [Xunit.FactAttribute]
            public void TestModuleConstants()
            {
#line (242, 5) - (242, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(0, tarfile.REGTYPE);
#line (243, 5) - (243, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(5, tarfile.DIRTYPE);
#line (244, 5) - (244, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(2, tarfile.SYMTYPE);
#line (245, 5) - (245, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(1, tarfile.LNKTYPE);
            }

            [Xunit.FactAttribute]
            public void TestErrorHierarchy()
            {
#line (250, 5) - (250, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(new global::Sharpy.ReadError("test") is global::Sharpy.TarError);
#line (251, 5) - (251, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(new global::Sharpy.CompressionError("test") is global::Sharpy.TarError);
#line (252, 5) - (252, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(new global::Sharpy.ExtractError("test") is global::Sharpy.TarError);
#line (253, 5) - (253, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.IsAssignableFrom<Exception>(new global::Sharpy.TarError("test"));
            }

            [Xunit.FactAttribute]
            public void TestMultipleFilesGetNames()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (260, 5) - (260, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/multi.tar";
#line (261, 5) - (263, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(tmpPath + "/a.txt", "w"))
                {
#line (262, 9) - (262, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("aaa");
                }

#line (263, 5) - (265, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(tmpPath + "/b.txt", "w"))
                {
#line (264, 9) - (264, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fb.Write("bbb");
                }

#line (265, 5) - (268, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (266, 9) - (266, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(tmpPath + "/a.txt", "a.txt");
#line (267, 9) - (267, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(tmpPath + "/b.txt", "b.txt");
                }

#line (268, 5) - (279, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (269, 9) - (269, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var names = tr.Getnames();
#line (270, 9) - (270, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(names));
#line (271, 9) - (271, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasA = names.Contains("a.txt");
#line (272, 9) - (272, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasB = names.Contains("b.txt");
#line (273, 9) - (273, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasA);
#line (274, 9) - (274, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasB);
                }
            }

            [Xunit.FactAttribute]
            public void TestTarinfoDefaultProperties()
            {
#line (281, 5) - (281, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var info = new global::Sharpy.TarInfo();
#line (282, 5) - (282, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal("", info.Name);
#line (283, 5) - (283, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(0, info.Size);
#line (284, 5) - (284, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal("", info.Linkname);
#line (285, 5) - (285, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal("", info.Uname);
#line (286, 5) - (286, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal("", info.Gname);
            }

            [Xunit.FactAttribute]
            public void TestTarinfoTypeChecks()
            {
#line (290, 5) - (290, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var info = new global::Sharpy.TarInfo();
#line (291, 5) - (291, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                info.Type = tarfile.REGTYPE;
#line (292, 5) - (292, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(info.Isfile());
#line (293, 5) - (293, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.False(info.Isdir());
#line (295, 5) - (295, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                info.Type = tarfile.DIRTYPE;
#line (296, 5) - (296, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(info.Isdir());
#line (297, 5) - (297, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.False(info.Isfile());
#line (299, 5) - (299, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                info.Type = tarfile.SYMTYPE;
#line (300, 5) - (300, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(info.Issym());
#line (302, 5) - (302, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                info.Type = tarfile.LNKTYPE;
#line (303, 5) - (303, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(info.Islnk());
            }

            [Xunit.FactAttribute]
            public void TestTarinfoTostring()
            {
#line (307, 5) - (307, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var info = new global::Sharpy.TarInfo("test.txt");
#line (308, 5) - (308, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal("<TarInfo 'test.txt'>", global::Sharpy.Builtins.Str(info));
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
