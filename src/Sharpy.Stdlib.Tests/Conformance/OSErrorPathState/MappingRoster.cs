namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// A2's two PERMANENT rosters (plan-638e8b, #2476), keyed <c>file:Type.Member</c> with the number of
/// sites the member holds and the reason. Spy sites are keyed by their generated C# file. Both are
/// compared with the scan in both directions by <see cref="MappingSpelledOnceTests"/>: an unlisted site,
/// a wrong count and a stale row are red. A row is never the cure for a path-state site — that site
/// calls a <see cref="PathErrors"/> door.
/// </summary>
internal static class MappingRoster
{
    private const string Mode = "not path state: an operation the open MODE refuses on an already-open handle (python raises io.UnsupportedOperation / plain OSError; no path is consulted)";

    /// <summary>Creations of plain Core <c>OSError</c>, each with its non-path-state reason.</summary>
    public static readonly IReadOnlyList<MappingRow> PlainOSError = new MappingRow[]
    {
        new("src/Sharpy.Core/TextFile.cs:TextFile.EnsureReadable", 1, Mode + " — read() on a file opened for writing"),
        new("src/Sharpy.Core/TextFile.cs:TextFile.EnsureWritable", 1, Mode + " — write() on a file opened for reading"),
        new("src/Sharpy.Stdlib/Gzip/GzipFile.cs:GzipFile.Read", 1, Mode + " — read() on a write-only GzipFile"),
        new("src/Sharpy.Stdlib/Gzip/GzipFile.cs:GzipFile.Write", 1, Mode + " — write() on a read-only GzipFile"),
        new("src/Sharpy.Stdlib/Zipfile/ZipFileArchive.cs:ZipFileArchive.Read", 1, Mode + " — read() on a ZipFile opened for writing (python raises ValueError there: #2559)"),
        new("src/Sharpy.Stdlib/Zipfile/ZipFileArchive.cs:ZipFileArchive.EnsureWritable", 1, Mode + " — write on a ZipFile opened for reading (python raises ValueError there: #2559)"),
        new("src/Sharpy.Stdlib/Subprocess/Popen.cs:Popen.Popen", 1,
            "not path state: a process start failure wrapped with its cause; the path-state cells of subprocess "
            + "(a missing executable, cwd=) are P56's surface (#2266), outside this contract"),
    };

    /// <summary>Catches around a door that absorb an I/O failure on purpose, each with its reason.</summary>
    public static readonly IReadOnlyList<MappingRow> Swallows = new MappingRow[]
    {
        new("src/Sharpy.Stdlib/Configparser/ConfigParser.IO.cs:ConfigParser.Read", 1,
            "python's ConfigParser.read skips a file it cannot open (`except OSError: continue`) and returns the list it read"),
        new("src/Sharpy.Stdlib/Glob/GlobModule.cs:GlobModule.IglobCore", 2,
            "python's glob answers no match for a directory it cannot list (IOException and UnauthorizedAccessException twins)"),
        new("src/Sharpy.Stdlib/Pathlib/Path.cs:Path.IsSymlink", 1,
            "python's Path.is_symlink() answers False when the path cannot be stat'ed (a missing path included)"),
        new("src/Sharpy.Stdlib/Pathlib/Path.cs:Path.Unlink", 1,
            "Path.unlink(missing_ok=True): python's `except FileNotFoundError: if not missing_ok: raise` — the caught type is "
            + "an already-classified FileNotFoundError and the filter is missing_ok"),
        new("src/Sharpy.Stdlib/Requests/Requests.cs:Requests.Send", 1,
            "a files= read failure is returned as Err(RequestException) — the Result channel ruled under #2553, no python oracle"),
        new("src/Sharpy.Stdlib/Shutil/Shutil.cs:ShutilModuleModule.Move", 1,
            "shutil.move (spy/shutil_module.spy): CPython's `except OSError:` after os.rename falls back to copy + remove "
            + "(each step a PathErrors door); the directory-into-itself arm re-raises the classified OSError"),
        new("src/Sharpy.Stdlib/Unittest/TmpPathFixture.cs:TmpPathFixture.Dispose", 2,
            "unittest's tmp_path fixture: a cleanup failure must not fail the test (IOException and UnauthorizedAccessException twins)"),
        new("src/Sharpy.Stdlib/Zipfile/ZipfileModule.cs:ZipfileModule.IsZipfile", 1,
            "python's zipfile.is_zipfile answers False when the file cannot be opened or read (`except OSError: pass`)"),
    };
}
