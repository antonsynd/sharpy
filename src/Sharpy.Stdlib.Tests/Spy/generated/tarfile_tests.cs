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
#line (27, 5) - (27, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/test.tar";
#line (28, 5) - (28, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/hello.txt";
#line (29, 5) - (31, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (30, 9) - (30, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("Hello, World!");
                }

#line (31, 5) - (33, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (32, 9) - (32, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "hello.txt");
                }

#line (33, 5) - (39, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (34, 9) - (34, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var names = tr.Getnames();
#line (35, 9) - (35, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasHello = names.Contains("hello.txt");
#line (36, 9) - (36, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasHello);
                }
            }

            [Xunit.FactAttribute]
            public void TestCreateAndReadGzipTar()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (41, 5) - (41, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/test.tar.gz";
#line (42, 5) - (42, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/data.txt";
#line (43, 5) - (45, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (44, 9) - (44, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("Compressed content");
                }

#line (45, 5) - (47, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:gz"))
                {
#line (46, 9) - (46, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "data.txt");
                }

#line (47, 5) - (53, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:gz"))
                {
#line (48, 9) - (48, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var names = tr.Getnames();
#line (49, 9) - (49, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasData = names.Contains("data.txt");
#line (50, 9) - (50, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasData);
                }
            }

            [Xunit.FactAttribute]
            public void TestAutoDetectGzipTar()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (55, 5) - (55, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/auto.tar.gz";
#line (56, 5) - (56, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/auto.txt";
#line (57, 5) - (59, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (58, 9) - (58, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("auto");
                }

#line (59, 5) - (61, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:gz"))
                {
#line (60, 9) - (60, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "auto.txt");
                }

#line (61, 5) - (69, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r"))
                {
#line (62, 9) - (62, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var names = tr.Getnames();
#line (63, 9) - (63, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasAuto = names.Contains("auto.txt");
#line (64, 9) - (64, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasAuto);
                }
            }

            [Xunit.FactAttribute]
            public void TestExtractallExtractsFiles()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (71, 5) - (71, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/extract.tar";
#line (72, 5) - (72, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/source.txt";
#line (73, 5) - (73, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var outDir = tmpPath + "/output";
#line (74, 5) - (76, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (75, 9) - (75, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("Extract me!");
                }

#line (76, 5) - (78, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (77, 9) - (77, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "source.txt");
                }

#line (78, 5) - (80, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (79, 9) - (79, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tr.Extractall(outDir);
                }

#line (80, 5) - (80, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(Isfile(outDir + "/source.txt"));
#line (81, 5) - (81, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var content = "";
#line (82, 5) - (84, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fr = global::Sharpy.Builtins.Open(outDir + "/source.txt", "r"))
                {
#line (83, 9) - (83, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    content = fr.Read();
                }

#line (84, 5) - (84, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal("Extract me!", content);
            }

            [Xunit.FactAttribute]
            public void TestExtractfileReturnsContent()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (89, 5) - (89, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/file.tar";
#line (90, 5) - (90, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/content.txt";
#line (91, 5) - (93, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (92, 9) - (92, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("File content");
                }

#line (93, 5) - (95, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (94, 9) - (94, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "content.txt");
                }

#line (95, 5) - (104, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (96, 9) - (96, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var data = tr.Extractfile("content.txt");
#line (97, 9) - (97, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.NotNull(data);
#line (98, 9) - (104, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    if (data != null)
                    {
#line (99, 13) - (99, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                        Xunit.Assert.Equal("File content", data.Value.Decode("utf-8"));
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestGetmembersReturnsTarInfoObjects()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (106, 5) - (106, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/members.tar";
#line (107, 5) - (107, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/info.txt";
#line (108, 5) - (110, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (109, 9) - (109, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("twelve chars");
                }

#line (110, 5) - (112, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (111, 9) - (111, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "info.txt");
                }

#line (112, 5) - (121, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (113, 9) - (113, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var members = tr.Getmembers();
#line (114, 9) - (114, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(members));
#line (115, 9) - (115, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Equal("info.txt", members[0].Name);
#line (116, 9) - (116, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(members[0].Isfile());
#line (117, 9) - (117, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.False(members[0].Isdir());
#line (118, 9) - (118, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Equal(12, members[0].Size);
                }
            }

            [Xunit.FactAttribute]
            public void TestGetmemberExistingMemberReturnsInfo()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (123, 5) - (123, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/member.tar";
#line (124, 5) - (124, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/a.txt";
#line (125, 5) - (127, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (126, 9) - (126, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("aaa");
                }

#line (127, 5) - (129, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (128, 9) - (128, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "a.txt");
                }

#line (129, 5) - (134, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (130, 9) - (130, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var info = tr.Getmember("a.txt");
#line (131, 9) - (131, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Equal("a.txt", info.Name);
                }
            }

            [Xunit.FactAttribute]
            public void TestGetmemberNonExistentThrowsKeyError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (136, 5) - (136, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/miss.tar";
#line (137, 5) - (137, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/a.txt";
#line (138, 5) - (140, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (139, 9) - (139, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("aaa");
                }

#line (140, 5) - (142, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (141, 9) - (141, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "a.txt");
                }

#line (142, 5) - (147, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (143, 9) - (147, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Throws<KeyError>((global::System.Action)(() =>
                    {
#line (144, 13) - (144, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                        tr.Getmember("nonexistent");
                    }));
                }
            }

            [Xunit.FactAttribute]
            public void TestAddWithArcnameUsesArchiveName()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (149, 5) - (149, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/arcname.tar";
#line (150, 5) - (150, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/original.txt";
#line (151, 5) - (153, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (152, 9) - (152, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("renamed");
                }

#line (153, 5) - (155, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (154, 9) - (154, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "renamed.txt");
                }

#line (155, 5) - (163, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (156, 9) - (156, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var names = tr.Getnames();
#line (157, 9) - (157, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasRenamed = names.Contains("renamed.txt");
#line (158, 9) - (158, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasRenamed);
                }
            }

            [Xunit.FactAttribute]
            public void TestIsTarfileValidTarReturnsTrue()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (165, 5) - (165, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/valid.tar";
#line (166, 5) - (166, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/x.txt";
#line (167, 5) - (169, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (168, 9) - (168, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("x");
                }

#line (169, 5) - (171, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (170, 9) - (170, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "x.txt");
                }

#line (171, 5) - (171, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(tarfile.IsTarfile(archive));
            }

            [Xunit.FactAttribute]
            public void TestIsTarfileNonTarReturnsFalse()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (176, 5) - (176, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/notatar.txt";
#line (177, 5) - (179, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (178, 9) - (178, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("This is not a tar file");
                }

#line (179, 5) - (179, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.False(tarfile.IsTarfile(fp));
            }

            [Xunit.FactAttribute]
            public void TestIsTarfileNonExistentReturnsFalse()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (184, 5) - (184, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.False(tarfile.IsTarfile(tmpPath + "/nonexistent.tar"));
            }

            [Xunit.FactAttribute]
            public void TestIsTarfileGzipTarReturnsTrue()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (189, 5) - (189, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/valid.tar.gz";
#line (190, 5) - (190, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/y.txt";
#line (191, 5) - (193, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (192, 9) - (192, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("y");
                }

#line (193, 5) - (195, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:gz"))
                {
#line (194, 9) - (194, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "y.txt");
                }

#line (195, 5) - (195, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(tarfile.IsTarfile(archive));
            }

            [Xunit.FactAttribute]
            public void TestOpenInvalidModeThrowsValueError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (202, 5) - (206, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (203, 9) - (203, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tarfile.Open(tmpPath + "/bad.tar", "x:");
                }));
            }

            [Xunit.FactAttribute]
            public void TestOpenBz2ModeThrowsCompressionError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (208, 5) - (212, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.CompressionError>((global::System.Action)(() =>
                {
#line (209, 9) - (209, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tarfile.Open(tmpPath + "/bad.tar.bz2", "r:bz2");
                }));
            }

            [Xunit.FactAttribute]
            public void TestOpenXzModeThrowsCompressionError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (214, 5) - (218, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.CompressionError>((global::System.Action)(() =>
                {
#line (215, 9) - (215, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tarfile.Open(tmpPath + "/bad.tar.xz", "w:xz");
                }));
            }

            [Xunit.FactAttribute]
            public void TestOpenNonExistentFileThrowsFileNotFoundError()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (220, 5) - (226, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
                {
#line (221, 9) - (221, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tarfile.Open(tmpPath + "/nonexistent.tar", "r:");
                }));
            }

            [Xunit.FactAttribute]
            public void TestClosePreventsFurtherOperations()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (228, 5) - (228, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/closed.tar";
#line (229, 5) - (229, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var fp = tmpPath + "/c.txt";
#line (230, 5) - (232, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(fp, "w"))
                {
#line (231, 9) - (231, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("c");
                }

#line (232, 5) - (234, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (233, 9) - (233, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(fp, "c.txt");
                }

#line (234, 5) - (234, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var readTar = tarfile.Open(archive, "r:");
#line (235, 5) - (235, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                readTar.Close();
#line (236, 5) - (242, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (237, 9) - (237, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    readTar.Getnames();
                }));
            }

            [Xunit.FactAttribute]
            public void TestModuleConstants()
            {
#line (244, 5) - (244, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(0, tarfile.REGTYPE);
#line (245, 5) - (245, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(5, tarfile.DIRTYPE);
#line (246, 5) - (246, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(2, tarfile.SYMTYPE);
#line (247, 5) - (247, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.Equal(1, tarfile.LNKTYPE);
            }

            [Xunit.FactAttribute]
            public void TestErrorHierarchy()
            {
#line (252, 5) - (252, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(new global::Sharpy.ReadError("test") is global::Sharpy.TarError);
#line (253, 5) - (253, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(new global::Sharpy.CompressionError("test") is global::Sharpy.TarError);
#line (254, 5) - (254, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.True(new global::Sharpy.ExtractError("test") is global::Sharpy.TarError);
#line (255, 5) - (255, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                Xunit.Assert.IsAssignableFrom<Exception>(new global::Sharpy.TarError("test"));
            }

            [Xunit.FactAttribute]
            public void TestMultipleFilesGetNames()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (262, 5) - (262, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                var archive = tmpPath + "/multi.tar";
#line (263, 5) - (265, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fa = global::Sharpy.Builtins.Open(tmpPath + "/a.txt", "w"))
                {
#line (264, 9) - (264, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fa.Write("aaa");
                }

#line (265, 5) - (267, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var fb = global::Sharpy.Builtins.Open(tmpPath + "/b.txt", "w"))
                {
#line (266, 9) - (266, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    fb.Write("bbb");
                }

#line (267, 5) - (270, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tw = tarfile.Open(archive, "w:"))
                {
#line (268, 9) - (268, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(tmpPath + "/a.txt", "a.txt");
#line (269, 9) - (269, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    tw.Add(tmpPath + "/b.txt", "b.txt");
                }

#line (270, 5) - (277, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                using (var tr = tarfile.Open(archive, "r:"))
                {
#line (271, 9) - (271, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    var names = tr.Getnames();
#line (272, 9) - (272, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(names));
#line (273, 9) - (273, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasA = names.Contains("a.txt");
#line (274, 9) - (274, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    bool hasB = names.Contains("b.txt");
#line (275, 9) - (275, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasA);
#line (276, 9) - (276, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/tarfile/tarfile_tests.spy"
                    Xunit.Assert.True(hasB);
                }
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
