using System;
using System.IO;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// The RELATION between <see cref="PathErrors.Rename"/>'s two paths — the axis G1 excludes, because
/// its state product always gives two distinct paths (plan-638e8b C2, excluded axes). The pair door
/// PERFORMS the rename and removes an empty destination directory first (D1 predicate 3), so a
/// destination that IS the source, or lies inside it, must be decided before that delete: at
/// 3ff077b1 <c>os.rename(d, d)</c> deleted <c>d</c> and <c>os.rename(ne, ne/sub)</c> deleted
/// <c>sub</c> (the refuting audit of P33). Each row is python3 3.13.18's verdict on Linux, uid 0
/// (run 2026-11-11), and every row also asserts that no entry the program created was removed.
/// </summary>
public sealed class PathErrorsRenameRelationTests : IDisposable
{
    private readonly string _root;

    public PathErrorsRenameRelationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "sharpy-rename-relation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "d"));
        Directory.CreateDirectory(Path.Combine(_root, "ne", "sub"));
        File.WriteAllText(Path.Combine(_root, "ne", "file"), "x");
        File.WriteAllText(Path.Combine(_root, "f"), "x");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
            // best-effort cleanup of a temp directory
        }
    }

    /// <summary>(src, dst, python's outcome class or "ok"), paths relative to the fixture root.</summary>
    public static TheoryData<string, string, string> Rows() => new()
    {
        { "d", "d", "ok" },                       // an empty directory onto itself
        { "d", "./d", "ok" },                     // the same entry, another spelling
        { "d", "d/", "ok" },                      // trailing separator
        { "ne", "ne", "ok" },                     // a non-empty directory onto itself
        { "f", "f", "ok" },                       // a file onto itself
        { "ne", "ne/sub", "OSError" },            // a directory onto an empty directory inside it (errno 22)
        { "ne", "ne/file", "OSError" },           // onto a file inside it (errno 22, not NotADirectoryError)
        { "ne", "ne/x", "OSError" },              // onto a missing entry inside it (errno 22)
        { "d", "d/x", "OSError" },                // an empty directory into itself (errno 22)
        { "ne", "ne/x/y", "FileNotFoundError" },  // the destination's parent resolves first (errno 2)
        { "ne", "ne/file/y", "NotADirectoryError" }, // a parent that is a file resolves first (errno 20)
        { "ne/sub", "ne", "OSError" },            // onto a non-empty ancestor (errno 39, ENOTEMPTY)
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Rename_DecidesTheRelationBeforeAnyDelete(string src, string dst, string python)
    {
        var before = Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories);

        string outcome;
        try
        {
            PathErrors.Rename(Path.Combine(_root, src), Path.Combine(_root, dst));
            outcome = "ok";
        }
        catch (Exception e)
        {
            outcome = e.GetType().Name;
        }

        Assert.Equal(python, outcome);
        // Every row is a refusal or a same-entry no-op: either way the tree is exactly as it was.
        var after = Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories);
        Assert.Equal(before, after);
    }

    /// <summary>Positive control: the empty-destination replace the guard sits in front of still happens.</summary>
    [Fact]
    public void Rename_OfADirectoryOntoAnEmptyDirectory_StillReplacesIt()
    {
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);

        PathErrors.Rename(Path.Combine(_root, "ne"), target);

        Assert.False(Directory.Exists(Path.Combine(_root, "ne")));
        Assert.True(File.Exists(Path.Combine(target, "file")));
        Assert.True(Directory.Exists(Path.Combine(target, "sub")));
    }
}
