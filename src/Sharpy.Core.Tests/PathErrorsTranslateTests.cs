using System;
using System.IO;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// The arms of <see cref="PathErrors"/>' <i>translate</i> door that no G1 cell can reach
/// (plan-638e8b A1, last bullet; #2476). G1 (<c>Sharpy.Stdlib.Tests</c>
/// <c>Conformance/OSErrorPathState</c>) runs every check, translate and pair door against python3
/// over the six path states, so the state arms of the table are proved there, against the live
/// oracle; what it cannot see is decided here, on a path whose state the table leaves alone (an
/// existing file read, a missing file written — python3 3.13.18, Linux, uid 0: <c>open(file)</c>
/// and <c>open(missing, "w")</c> both succeed):
/// <list type="bullet">
/// <item>an access refusal — permission bits do not refuse root, so no container run reaches it — is
/// <see cref="PermissionError"/>;</item>
/// <item>an <see cref="OSError"/> is returned unchanged, so an outer catch never re-classifies what an
/// inner one already did (D1 predicate 1);</item>
/// <item>any other CLR I/O failure is exactly plain <see cref="OSError"/>, never a subclass and never
/// the CLR exception itself;</item>
/// <item>a CLR "not found" report the state no longer shows (a race) is
/// <see cref="FileNotFoundError"/>;</item>
/// <item>an exception that is not an I/O failure passes through unchanged.</item>
/// </list>
/// </summary>
public sealed class PathErrorsTranslateTests : IDisposable
{
    private readonly string _root;

    public PathErrorsTranslateTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "sharpy-patherrors-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string ExistingFile()
    {
        var path = Path.Combine(_root, "f.txt");
        File.WriteAllText(path, "x");
        return path;
    }

    private string MissingFile() => Path.Combine(_root, "missing.txt");

    /// <summary>Each translate door with a path its own requirement leaves alone (the table answers Ok).</summary>
    public static TheoryData<string> Doors() => new() { "read", "write", "exclusive", "remove-file", "create-dir", "any" };

    private Exception TranslateOnUntouchedState(string door, Exception error) => door switch
    {
        "read" => PathErrors.TranslateNeedsFileToRead(error, ExistingFile()),
        "write" => PathErrors.TranslateNeedsFileToWrite(error, MissingFile()),
        "exclusive" => PathErrors.TranslateCreatesExclusively(error, MissingFile()),
        "remove-file" => PathErrors.TranslateRemovesFile(error, ExistingFile()),
        "create-dir" => PathErrors.TranslateCreatesDir(error, MissingFile()),
        "any" => PathErrors.TranslateNeedsAny(error, ExistingFile()),
        _ => throw new ArgumentOutOfRangeException(nameof(door), door, null),
    };

    [Theory]
    [MemberData(nameof(Doors))]
    public void AccessRefusal_IsPermissionError(string door)
    {
        var refusal = new UnauthorizedAccessException("Access to the path is denied.");
        var translated = TranslateOnUntouchedState(door, refusal);
        Assert.IsType<PermissionError>(translated);
        Assert.Same(refusal, translated.InnerException);
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public void BareIOException_IsExactlyPlainOSError(string door)
    {
        var failure = new IOException("disk on fire");
        var translated = TranslateOnUntouchedState(door, failure);
        Assert.Equal(typeof(OSError), translated.GetType());
        Assert.NotSame(failure, translated);
        Assert.Same(failure, translated.InnerException);
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public void ClrNotFoundReport_TheStateNoLongerShows_IsFileNotFoundError(string door)
    {
        var report = new DirectoryNotFoundException("Could not find a part of the path.");
        var translated = TranslateOnUntouchedState(door, report);
        Assert.IsType<FileNotFoundError>(translated);
        Assert.Same(report, translated.InnerException);
    }

    /// <summary>
    /// Idempotence (D1 predicate 1), on a path the table WOULD classify (reading a missing path is
    /// <see cref="FileNotFoundError"/>), so a missing guard shows as a new instance.
    /// </summary>
    [Fact]
    public void OSErrorInstance_IsReturnedUnchanged()
    {
        foreach (var already in new OSError[] { new OSError("plain"), new PermissionError("denied"), new IsADirectoryError("dir") })
        {
            Assert.Same(already, PathErrors.TranslateNeedsFileToRead(already, MissingFile()));
        }
    }

    /// <summary>Not an I/O failure: returned unchanged, even on a path the table would classify.</summary>
    [Fact]
    public void NonIoException_PassesThroughUnchanged()
    {
        var bug = new ArgumentException("not an I/O failure");
        Assert.Same(bug, PathErrors.TranslateNeedsFileToRead(bug, MissingFile()));
    }
}
