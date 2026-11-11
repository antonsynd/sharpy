using System;
using System.IO;
using System.Linq;
using Sharpy.TestInfrastructure;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// What <see cref="PathErrors.Rename"/> does to the two final ENTRIES — the symbolic-link and
/// cross-file-system cells of the axes G1 excludes (plan-638e8b C2). The pair door moves and replaces
/// entries, so it must read them as <c>lstat(2)</c> does and must never remove a destination before a
/// move that can refuse: at e77bf59e it followed a link to a directory (replacing the link, or moving a
/// link onto a directory python refuses) and deleted an empty destination, or replaced a file, before
/// a cross-device move (the verify-implementation audit of P33). Each row is python3 3.13.18's verdict
/// on Linux, uid 0 (run 2026-11-11), and asserts the exact entries of both trees afterwards.
/// </summary>
public sealed class PathErrorsRenameEntryTests : IDisposable
{
    private const string OtherFileSystem = "/dev/shm";

    private readonly string _root;
    private readonly string? _other;

    public PathErrorsRenameEntryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "sharpy-rename-entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "e"));
        Directory.CreateDirectory(Path.Combine(_root, "e2"));
        File.WriteAllText(Path.Combine(_root, "f"), "F");
        File.WriteAllText(Path.Combine(_root, "g"), "G");
        Directory.CreateSymbolicLink(Path.Combine(_root, "ld"), Path.Combine(_root, "e"));
        File.CreateSymbolicLink(Path.Combine(_root, "lf"), Path.Combine(_root, "f"));

        if (Directory.Exists(OtherFileSystem))
        {
            _other = Path.Combine(OtherFileSystem, "sharpy-rename-entry-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_other, "xd"));
            File.WriteAllText(Path.Combine(_other, "xd", "in"), "I");
            File.WriteAllText(Path.Combine(_other, "xf"), "X");
        }
    }

    public void Dispose()
    {
        foreach (var dir in new[] { _root, _other })
        {
            try
            {
                if (dir != null)
                {
                    Directory.Delete(dir, true);
                }
            }
            catch (IOException)
            {
                // best-effort cleanup of a temp directory
            }
        }
    }

    /// <summary>
    /// (src, dst, python's outcome, the root's entries after, the other file system's entries after).
    /// <c>X:</c> names an entry on <see cref="OtherFileSystem"/>; <c>@</c> marks a symbolic link.
    /// </summary>
    public static TheoryData<string, string, string, string, string> Rows() => new()
    {
        // A directory onto a link to a directory: the link is a non-directory (ENOTDIR).
        { "e", "ld", "NotADirectoryError", "e e2 f g ld@ lf@", "xd xd/in xf" },
        // A link to a directory onto a directory: the link is a non-directory (EISDIR).
        { "ld", "e2", "IsADirectoryError", "e e2 f g ld@ lf@", "xd xd/in xf" },
        // The link itself moves; its target stays.
        { "ld", "new", "ok", "e e2 f g lf@ new@", "xd xd/in xf" },
        // A file replaces a link to a file — the link, not the file it points at.
        { "f", "lf", "ok", "e e2 g ld@ lf", "xd xd/in xf" },
        // A link replaces a file.
        { "lf", "g", "ok", "e e2 f g@ ld@", "xd xd/in xf" },
        // A file replaces a file (the link to the old name dangles).
        { "f", "g", "ok", "e e2 g ld@ lf@", "xd xd/in xf" },
        // Across file systems rename(2) refuses (EXDEV) and both entries stay.
        { "X:xd", "e2", "OSError", "e e2 f g ld@ lf@", "xd xd/in xf" },
        { "X:xf", "g", "OSError", "e e2 f g ld@ lf@", "xd xd/in xf" },
        { "X:xf", "new", "OSError", "e e2 f g ld@ lf@", "xd xd/in xf" },
    };

    [SkippableTheory]
    [MemberData(nameof(Rows))]
    public void Rename_ReadsTheFinalEntriesAndNeverRemovesBeforeAMoveThatCanRefuse(
        string src, string dst, string python, string rootAfter, string otherAfter)
    {
        var crossDevice = src.StartsWith("X:", StringComparison.Ordinal);
        if (crossDevice)
        {
            TestPreconditions.RequireOrSkip(_other != null && IsAnotherFileSystem(_other),
                OtherFileSystem + " is absent or on the temp directory's file system: no cross-device pair to build");
        }

        var source = crossDevice
            ? Path.Combine(_other!, src.Substring(2))
            : Path.Combine(_root, src);

        string outcome;
        try
        {
            PathErrors.Rename(source, Path.Combine(_root, dst));
            outcome = "ok";
        }
        catch (Exception e)
        {
            outcome = e.GetType().Name;
        }

        Assert.Equal(python, outcome);
        Assert.Equal(rootAfter, Entries(_root));
        if (_other != null)
        {
            Assert.Equal(otherAfter, Entries(_other));
        }
        // The file a link pointed at is never what was replaced.
        if (File.Exists(Path.Combine(_root, "f")) && !IsLink(Path.Combine(_root, "f")))
        {
            Assert.Equal("F", File.ReadAllText(Path.Combine(_root, "f")));
        }
    }

    /// <summary>
    /// Two hard links to one file: <c>rename(2)</c> succeeds and changes nothing, so python keeps both
    /// names (python3 3.13.18). A door that moved the destination aside and deleted it would drop one.
    /// </summary>
    [SkippableFact]
    public void Rename_OfAHardLinkOntoAnotherLinkOfTheSameFile_KeepsBothNames()
    {
        var f = Path.Combine(_root, "f");
        var h = Path.Combine(_root, "h");
        TestPreconditions.RequireOrSkip(TryHardLink(f, h), "no `ln` to make a hard link on this host");

        PathErrors.Rename(f, h);

        Assert.Equal("e e2 f g h ld@ lf@", Entries(_root));
        Assert.Equal("F", File.ReadAllText(f));
        Assert.Equal("F", File.ReadAllText(h));
    }

    /// <summary>Positive control for the replace the rows above guard: the new content is at the destination.</summary>
    [Fact]
    public void Rename_OfAFileOntoAFile_ReplacesItsContent()
    {
        PathErrors.Rename(Path.Combine(_root, "f"), Path.Combine(_root, "g"));

        Assert.Equal("F", File.ReadAllText(Path.Combine(_root, "g")));
        Assert.False(File.Exists(Path.Combine(_root, "f")));
    }

    private static bool TryHardLink(string target, string link)
    {
        try
        {
            using var ln = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ln", $"\"{target}\" \"{link}\"") { UseShellExecute = false });
            ln!.WaitForExit();
            return ln.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>The entries under <paramref name="dir"/>, links not followed, relative and sorted.</summary>
    private static string Entries(string dir)
    {
        var names = new System.Collections.Generic.List<string>();
        void Walk(string at)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(at))
            {
                var relative = Path.GetRelativePath(dir, entry).Replace(Path.DirectorySeparatorChar, '/');
                if (IsLink(entry))
                {
                    names.Add(relative + "@");
                    continue;
                }
                names.Add(relative);
                if (Directory.Exists(entry))
                {
                    Walk(entry);
                }
            }
        }
        Walk(dir);
        return string.Join(" ", names.OrderBy(n => n, StringComparer.Ordinal));
    }

    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    /// <summary>True when a rename(2) from <paramref name="other"/> into the temp directory refuses as cross-device.</summary>
    private bool IsAnotherFileSystem(string other)
    {
        var probe = Path.Combine(other, "probe");
        var target = Path.Combine(_root, "probe");
        File.WriteAllText(probe, "p");
        try
        {
            Directory.Move(probe, target);
            File.Delete(target);
            return false;
        }
        catch (IOException)
        {
            File.Delete(probe);
            return true;
        }
    }
}
