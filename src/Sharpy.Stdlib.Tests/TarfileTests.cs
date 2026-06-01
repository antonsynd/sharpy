using System;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class TarfileTests : IDisposable
{
    private readonly string _tempDir;

    public TarfileTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy_tarfile_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, true); }
        catch { /* best effort */ }
    }

    private string Sub(string name) => System.IO.Path.Combine(_tempDir, name);

    [Fact]
    public void CreateAndRead_UncompressedTar()
    {
        string archivePath = Sub("test.tar");
        string filePath = Sub("hello.txt");
        File.WriteAllText(filePath, "Hello, World!");

        using (var tar = TarfileModule.Open(archivePath, "w:"))
        {
            tar.Add(filePath, arcname: "hello.txt");
        }

        using (var tar = TarfileModule.Open(archivePath, "r:"))
        {
            var names = tar.Getnames();
            names.Should().Contain("hello.txt");
        }
    }

    [Fact]
    public void CreateAndRead_GzipTar()
    {
        string archivePath = Sub("test.tar.gz");
        string filePath = Sub("data.txt");
        File.WriteAllText(filePath, "Compressed content");

        using (var tar = TarfileModule.Open(archivePath, "w:gz"))
        {
            tar.Add(filePath, arcname: "data.txt");
        }

        using (var tar = TarfileModule.Open(archivePath, "r:gz"))
        {
            tar.Getnames().Should().Contain("data.txt");
        }
    }

    [Fact]
    public void AutoDetect_GzipTar()
    {
        string archivePath = Sub("auto.tar.gz");
        string filePath = Sub("auto.txt");
        File.WriteAllText(filePath, "auto");

        using (var tar = TarfileModule.Open(archivePath, "w:gz"))
        {
            tar.Add(filePath, arcname: "auto.txt");
        }

        using (var tar = TarfileModule.Open(archivePath, "r"))
        {
            tar.Getnames().Should().Contain("auto.txt");
        }
    }

    [Fact]
    public void Extractall_ExtractsFiles()
    {
        string archivePath = Sub("extract.tar");
        string filePath = Sub("source.txt");
        string extractDir = Sub("output");
        File.WriteAllText(filePath, "Extract me!");

        using (var tar = TarfileModule.Open(archivePath, "w:"))
        {
            tar.Add(filePath, arcname: "source.txt");
        }

        using (var tar = TarfileModule.Open(archivePath, "r:"))
        {
            tar.Extractall(extractDir);
        }

        File.Exists(System.IO.Path.Combine(extractDir, "source.txt")).Should().BeTrue();
        File.ReadAllText(System.IO.Path.Combine(extractDir, "source.txt")).Should().Be("Extract me!");
    }

    [Fact]
    public void Extractfile_ReturnsContent()
    {
        string archivePath = Sub("file.tar");
        string filePath = Sub("content.txt");
        File.WriteAllText(filePath, "File content");

        using (var tar = TarfileModule.Open(archivePath, "w:"))
        {
            tar.Add(filePath, arcname: "content.txt");
        }

        using (var tar = TarfileModule.Open(archivePath, "r:"))
        {
            Bytes? data = tar.Extractfile("content.txt");
            data.Should().NotBeNull();
            System.Text.Encoding.UTF8.GetString(data!.Value.ToArray()).Should().Be("File content");
        }
    }

    [Fact]
    public void Getmembers_ReturnsTarInfoObjects()
    {
        string archivePath = Sub("members.tar");
        string filePath = Sub("info.txt");
        File.WriteAllText(filePath, "twelve chars");

        using (var tar = TarfileModule.Open(archivePath, "w:"))
        {
            tar.Add(filePath, arcname: "info.txt");
        }

        using (var tar = TarfileModule.Open(archivePath, "r:"))
        {
            var members = tar.Getmembers();
            members.Should().HaveCount(1);
            members[0].Name.Should().Be("info.txt");
            members[0].Isfile().Should().BeTrue();
            members[0].Isdir().Should().BeFalse();
            members[0].Size.Should().Be(12);
        }
    }

    [Fact]
    public void Getmember_ExistingMember_ReturnsInfo()
    {
        string archivePath = Sub("member.tar");
        string filePath = Sub("a.txt");
        File.WriteAllText(filePath, "aaa");

        using (var tar = TarfileModule.Open(archivePath, "w:"))
        {
            tar.Add(filePath, arcname: "a.txt");
        }

        using (var tar = TarfileModule.Open(archivePath, "r:"))
        {
            var info = tar.Getmember("a.txt");
            info.Name.Should().Be("a.txt");
        }
    }

    [Fact]
    public void Getmember_NonExistent_ThrowsKeyError()
    {
        string archivePath = Sub("miss.tar");
        string filePath = Sub("a.txt");
        File.WriteAllText(filePath, "aaa");

        using (var tar = TarfileModule.Open(archivePath, "w:"))
        {
            tar.Add(filePath, arcname: "a.txt");
        }

        using (var tar = TarfileModule.Open(archivePath, "r:"))
        {
            Action act = () => tar.Getmember("nonexistent");
            act.Should().Throw<KeyError>();
        }
    }

    [Fact]
    public void Add_WithArcname_UsesArchiveName()
    {
        string archivePath = Sub("arcname.tar");
        string filePath = Sub("original.txt");
        File.WriteAllText(filePath, "renamed");

        using (var tar = TarfileModule.Open(archivePath, "w:"))
        {
            tar.Add(filePath, arcname: "renamed.txt");
        }

        using (var tar = TarfileModule.Open(archivePath, "r:"))
        {
            tar.Getnames().Should().Contain("renamed.txt");
        }
    }

    [Fact]
    public void IsTarfile_ValidTar_ReturnsTrue()
    {
        string archivePath = Sub("valid.tar");
        string filePath = Sub("x.txt");
        File.WriteAllText(filePath, "x");

        using (var tar = TarfileModule.Open(archivePath, "w:"))
        {
            tar.Add(filePath, arcname: "x.txt");
        }

        TarfileModule.IsTarfile(archivePath).Should().BeTrue();
    }

    [Fact]
    public void IsTarfile_NonTar_ReturnsFalse()
    {
        string filePath = Sub("notatar.txt");
        File.WriteAllText(filePath, "This is not a tar file");
        TarfileModule.IsTarfile(filePath).Should().BeFalse();
    }

    [Fact]
    public void IsTarfile_NonExistent_ReturnsFalse()
    {
        TarfileModule.IsTarfile(Sub("nonexistent.tar")).Should().BeFalse();
    }

    [Fact]
    public void IsTarfile_GzipTar_ReturnsTrue()
    {
        string archivePath = Sub("valid.tar.gz");
        string filePath = Sub("y.txt");
        File.WriteAllText(filePath, "y");

        using (var tar = TarfileModule.Open(archivePath, "w:gz"))
        {
            tar.Add(filePath, arcname: "y.txt");
        }

        TarfileModule.IsTarfile(archivePath).Should().BeTrue();
    }

    [Fact]
    public void Open_InvalidMode_ThrowsValueError()
    {
        Action act = () => TarfileModule.Open(Sub("bad.tar"), "x:");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Open_Bz2Mode_ThrowsCompressionError()
    {
        Action act = () => TarfileModule.Open(Sub("bad.tar.bz2"), "r:bz2");
        act.Should().Throw<CompressionError>();
    }

    [Fact]
    public void Open_XzMode_ThrowsCompressionError()
    {
        Action act = () => TarfileModule.Open(Sub("bad.tar.xz"), "w:xz");
        act.Should().Throw<CompressionError>();
    }

    [Fact]
    public void Close_PreventsFurtherOperations()
    {
        string archivePath = Sub("closed.tar");
        string filePath = Sub("c.txt");
        File.WriteAllText(filePath, "c");

        using (var tar = TarfileModule.Open(archivePath, "w:"))
        {
            tar.Add(filePath, arcname: "c.txt");
        }

        var readTar = TarfileModule.Open(archivePath, "r:");
        readTar.Close();

        Action act = () => readTar.Getnames();
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void TarInfo_DefaultProperties()
    {
        var info = new TarInfo();
        info.Name.Should().Be("");
        info.Size.Should().Be(0);
        info.Linkname.Should().Be("");
        info.Uname.Should().Be("");
        info.Gname.Should().Be("");
    }

    [Fact]
    public void TarInfo_TypeChecks()
    {
        var info = new TarInfo { Type = TarfileModule.REGTYPE };
        info.Isfile().Should().BeTrue();
        info.Isdir().Should().BeFalse();

        info.Type = TarfileModule.DIRTYPE;
        info.Isdir().Should().BeTrue();
        info.Isfile().Should().BeFalse();

        info.Type = TarfileModule.SYMTYPE;
        info.Issym().Should().BeTrue();

        info.Type = TarfileModule.LNKTYPE;
        info.Islnk().Should().BeTrue();
    }

    [Fact]
    public void TarInfo_ToString()
    {
        var info = new TarInfo { Name = "test.txt" };
        info.ToString().Should().Be("<TarInfo 'test.txt'>");
    }

    [Fact]
    public void ModuleConstants()
    {
        TarfileModule.REGTYPE.Should().Be(0);
        TarfileModule.DIRTYPE.Should().Be(5);
        TarfileModule.SYMTYPE.Should().Be(2);
        TarfileModule.LNKTYPE.Should().Be(1);
    }

    [Fact]
    public void ErrorHierarchy()
    {
        new TarError("test").Should().BeAssignableTo<Exception>();
        new ReadError("test").Should().BeAssignableTo<TarError>();
        new CompressionError("test").Should().BeAssignableTo<TarError>();
        new ExtractError("test").Should().BeAssignableTo<TarError>();
    }

    [Fact]
    public void Open_NonExistentFile_ThrowsFileNotFoundError()
    {
        Action act = () => TarfileModule.Open(Sub("nonexistent.tar"), "r:");
        act.Should().Throw<FileNotFoundError>();
    }

    [Fact]
    public void MultipleFiles_GetNames()
    {
        string archivePath = Sub("multi.tar");
        File.WriteAllText(Sub("a.txt"), "aaa");
        File.WriteAllText(Sub("b.txt"), "bbb");

        using (var tar = TarfileModule.Open(archivePath, "w:"))
        {
            tar.Add(Sub("a.txt"), arcname: "a.txt");
            tar.Add(Sub("b.txt"), arcname: "b.txt");
        }

        using (var tar = TarfileModule.Open(archivePath, "r:"))
        {
            var names = tar.Getnames();
            names.Should().HaveCount(2);
            names.Should().Contain("a.txt");
            names.Should().Contain("b.txt");
        }
    }
}
