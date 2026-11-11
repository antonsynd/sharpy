using FluentAssertions;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Shared;

/// <summary>
/// The incremental cache's file identity (<see cref="FileContentIdentity"/>): content, not size and
/// write time; memoised per instance and never across instances; unreadable by structure, not text.
/// </summary>
public class FileContentIdentityTests : IDisposable
{
    private readonly string _tempDir;

    public FileContentIdentityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_fileidentity_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        GC.SuppressFinalize(this);
    }

    /// <summary>Replaces <paramref name="path"/> by <paramref name="bytes"/> of the same size, keeping its write time.</summary>
    private static void ReplaceKeepingSizeAndTime(string path, byte[] bytes)
    {
        var time = File.GetLastWriteTimeUtc(path);
        new FileInfo(path).Length.Should().Be(bytes.Length, "precondition: same size");
        var staged = path + ".staged";
        File.WriteAllBytes(staged, bytes);
        File.SetLastWriteTimeUtc(staged, time);
        File.Move(staged, path, overwrite: true);
    }

    [Fact]
    public void AnInstance_AnswersOnce_ANewInstance_SeesTheNewBytes()
    {
        var path = Path.Combine(_tempDir, "a.bin");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        var first = new FileContentIdentity();
        var before = first.Of(path);
        var disk = FileContentIdentity.DiskIdentity(path);

        ReplaceKeepingSizeAndTime(path, new byte[] { 3, 2, 1 });

        FileContentIdentity.DiskIdentity(path).Should().Be(disk, "control: disk identity cannot see the change");
        first.Of(path).Should().Be(before, "one instance answers one identity per file");
        new FileContentIdentity().Of(path).Should().NotBe(before, "a new instance sees the new bytes");
        before.Should().StartWith(disk).And.Contain(":sha256=");
    }

    /// <summary>
    /// Unreadable is a property of an identity's suffix, not of text anywhere in a key: a readable file
    /// whose path contains the marker's text is not unreadable (before, a substring match made such a
    /// project never up to date). Positive control: a marked identity inside a multi-line key is.
    /// </summary>
    [SkippableFact]
    public void Unreadable_IsJudgedByEachIdentitysSuffix_NotByAPathThatContainsTheMarker()
    {
        Skip.If(OperatingSystem.IsWindows(), "':' cannot appear in a Windows file name");
        var directory = Path.Combine(_tempDir, "odd" + FileContentIdentity.UnreadableMarker + "dir");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Lib.dll");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });

        var identity = FileContentIdentity.Compute(path);
        identity.Should().Contain(FileContentIdentity.UnreadableMarker, "precondition: the path holds the marker's text");
        FileContentIdentity.IsUnreadable("reference=" + identity + "\nruntime=10.0.0").Should().BeFalse();

        var marked = FileContentIdentity.DiskIdentity(path) + FileContentIdentity.UnreadableMarker;
        FileContentIdentity.IsUnreadable("runtime=10.0.0\nreference=" + marked + "\nfeatures=").Should().BeTrue(
            "control: an identity that ends with the marker is unreadable wherever it sits in the key");
    }
}
