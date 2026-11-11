using Sharpy;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// One probe row of Map 2: a Sharpy statement template and its python twin over the placeholders
/// <c>@0</c>, <c>@1</c> (one per path POSITION, named in <see cref="Placeholders"/>), plus the fixture
/// tokens <c>@auxtar</c> (a valid tar holding <c>m.txt</c>), <c>@auxzip</c> (a valid zip holding
/// <c>m.txt</c>) and <c>@auxout</c> (a writable path that does not exist yet), and the requirement the
/// probe's fixed non-path arguments select.
/// </summary>
internal sealed record Probe(
    string Id, string Entry, string Module, PathRequirement Requirement, string[] Placeholders, string Sharpy, string Python);

/// <summary>
/// Map 2 of G1 (plan-638e8b C2 axes 2 and 4): entry point → probes, the entry points' path positions,
/// and the exclusions in their two closed forms. Rows are keyed by the census's member key, so T3 and
/// T4 can hold them to the reach in both directions.
/// </summary>
internal static class ProbeRoster
{
    public const string Unreachable = "unreachable with a user path: ";
    public const string OutOfContract = "out of contract: #";

    private const string Open1 = "Sharpy.Builtins.Open(string)";
    private const string Open2 = "Sharpy.Builtins.Open(string, string)";
    private const string Open3 = "Sharpy.Builtins.Open(string, string, string)";
    private const string Os = "Sharpy.OsModule.OsModuleModule.";
    private const string OsPath = "Sharpy.OsPathModule.OsPathModuleModule.";
    private const string Shutil = "Sharpy.ShutilModule.ShutilModuleModule.";
    private const string PathErrors = "Sharpy.PathErrors.";

    /// <summary>
    /// D1's doors, one row per (requirement, door shape): the requirement's python operation is the
    /// twin. A <i>check</i> door raises python's class or nothing (python's operation fails or
    /// succeeds); a <i>translate</i> door, handed a CLR <c>IOException</c> the table cannot classify,
    /// returns python's class where python's operation fails and plain <c>OSError</c> where it
    /// succeeds (the twin performs the operation, then raises <c>OSError</c>).
    /// </summary>
    private static readonly (PathRequirement Requirement, string Door, string Snake, string Python)[] SinglePathDoors =
    {
        (PathRequirement.NeedsFileToRead, "NeedsFileToRead", "needs_file_to_read", "open(@0).close()"),
        (PathRequirement.NeedsFileToWrite, "NeedsFileToWrite", "needs_file_to_write", "open(@0, \"w\").close()"),
        (PathRequirement.CreatesExclusively, "CreatesExclusively", "creates_exclusively", "open(@0, \"x\").close()"),
        (PathRequirement.RemovesFile, "RemovesFile", "removes_file", "os.remove(@0)"),
        (PathRequirement.CreatesDir, "CreatesDir", "creates_dir", "os.mkdir(@0)"),
        (PathRequirement.RemovesDir, "RemovesDir", "removes_dir", "os.rmdir(@0)"),
        (PathRequirement.NeedsDir, "NeedsDir", "needs_dir", "os.listdir(@0)"),
        (PathRequirement.NeedsAny, "NeedsAny", "needs_any", "os.stat(@0)"),
    };

    /// <summary>The import header of each module's two programs (Sharpy, python).</summary>
    public static readonly IReadOnlyDictionary<string, (string Sharpy, string Python)> Headers =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["builtins"] = ("", ""),
            ["os"] = ("import os", "import os"),
            ["os.path"] = ("import os.path", "import os.path"),
            ["pathlib"] = ("from pathlib import Path", "from pathlib import Path"),
            ["shutil"] = ("import shutil", "import shutil"),
            ["glob"] = ("import glob", "import glob"),
            ["gzip"] = ("import gzip", "import gzip"),
            ["tarfile"] = ("import tarfile", "import tarfile"),
            ["zipfile"] = ("import zipfile", "import zipfile"),
            ["sqlite3"] = ("import sqlite3", "import sqlite3"),
            ["xml"] = ("import xml", "import xml.etree.ElementTree as xml"),
            ["toml"] = ("import toml", "import tomllib"),
            ["configparser"] = ("import configparser", "import configparser"),
            ["PathErrors"] = ("from sharpy import PathErrors\nfrom system.io import IOException", "import os"),
        };

    private static Probe P(string id, string entry, string module, PathRequirement requirement, string[] placeholders, string sharpy, string? python = null)
        => new(id, entry, module, requirement, placeholders, sharpy, python ?? sharpy);

    private static readonly string[] Path0 = { "path" };
    private static readonly string[] Self = { PathPositions.Receiver };

    public static readonly IReadOnlyList<Probe> Probes = new[]
    {
        // ---- PathErrors (Core, D1): the classifier's own doors against python's operations ----
        P("PathErrors.rename", PathErrors + "Rename(string, string)", "PathErrors", PathRequirement.Rename, new[] { "src", "dst" },
            "PathErrors.rename(@0, @1)", "os.rename(@0, @1)"),

        // ---- builtins (Core) ----
        P("open(r)", Open1, "builtins", PathRequirement.NeedsFileToRead, Path0, "open(@0).close()"),
        P("open(w)", Open2, "builtins", PathRequirement.NeedsFileToWrite, Path0, "open(@0, \"w\").close()"),
        P("open(a)", Open2, "builtins", PathRequirement.NeedsFileToWrite, Path0, "open(@0, \"a\").close()"),
        P("open(x)", Open2, "builtins", PathRequirement.CreatesExclusively, Path0, "open(@0, \"x\").close()"),
        P("open(r,encoding)", Open3, "builtins", PathRequirement.NeedsFileToRead, Path0, "open(@0, \"r\", encoding=\"utf-8\").close()"),

        // ---- os ----
        P("os.remove", Os + "Remove(string)", "os", PathRequirement.RemovesFile, Path0, "os.remove(@0)"),
        P("os.rename", Os + "Rename(string, string)", "os", PathRequirement.Rename, new[] { "src", "dst" }, "os.rename(@0, @1)"),
        P("os.mkdir", Os + "Mkdir(string)", "os", PathRequirement.CreatesDir, Path0, "os.mkdir(@0)"),
        P("os.makedirs", Os + "Makedirs(string, bool)", "os", PathRequirement.CreatesDir, Path0, "os.makedirs(@0)"),
        P("os.rmdir", Os + "Rmdir(string)", "os", PathRequirement.RemovesDir, Path0, "os.rmdir(@0)"),
        P("os.listdir", Os + "Listdir(string)", "os", PathRequirement.NeedsDir, Path0, "os.listdir(@0)"),
        P("os.chdir", Os + "Chdir(string)", "os", PathRequirement.NeedsDir, Path0, "os.chdir(@0)"),
        P("os.stat", Os + "Stat(string)", "os", PathRequirement.NeedsAny, Path0, "os.stat(@0)"),
        P("os.walk", Os + "Walk(string)", "os", PathRequirement.NeedsDir, new[] { "top" }, "list(os.walk(@0))"),
        P("os.path_exists", Os + "PathExists(string)", "os", PathRequirement.NeedsAny, Path0, "os.path_exists(@0)", "os.path.exists(@0)"),

        // ---- os.path ----
        P("os.path.exists", OsPath + "Exists(string)", "os.path", PathRequirement.NeedsAny, Path0, "os.path.exists(@0)"),
        P("os.path.isfile", OsPath + "Isfile(string)", "os.path", PathRequirement.NeedsAny, Path0, "os.path.isfile(@0)"),
        P("os.path.isdir", OsPath + "Isdir(string)", "os.path", PathRequirement.NeedsAny, Path0, "os.path.isdir(@0)"),
        P("os.path.getsize", OsPath + "Getsize(string)", "os.path", PathRequirement.NeedsAny, Path0, "os.path.getsize(@0)"),

        // ---- pathlib (the receiver is the path: the probe builds the instance from it) ----
        P("Path.exists", "Sharpy.Path.Exists()", "pathlib", PathRequirement.NeedsAny, Self, "Path(@0).exists()"),
        P("Path.is_dir", "Sharpy.Path.IsDir()", "pathlib", PathRequirement.NeedsAny, Self, "Path(@0).is_dir()"),
        P("Path.is_file", "Sharpy.Path.IsFile()", "pathlib", PathRequirement.NeedsAny, Self, "Path(@0).is_file()"),
        P("Path.is_symlink", "Sharpy.Path.IsSymlink()", "pathlib", PathRequirement.NeedsAny, Self, "Path(@0).is_symlink()"),
        P("Path.stat", "Sharpy.Path.Stat()", "pathlib", PathRequirement.NeedsAny, Self, "Path(@0).stat()"),
        P("Path.iterdir", "Sharpy.Path.Iterdir()", "pathlib", PathRequirement.NeedsDir, Self, "list(Path(@0).iterdir())"),
        P("Path.glob", "Sharpy.Path.Glob(string)", "pathlib", PathRequirement.NeedsDir, Self, "list(Path(@0).glob(\"*\"))"),
        P("Path.rglob", "Sharpy.Path.Rglob(string)", "pathlib", PathRequirement.NeedsDir, Self, "list(Path(@0).rglob(\"*\"))"),
        P("Path.mkdir", "Sharpy.Path.Mkdir(bool, bool)", "pathlib", PathRequirement.CreatesDir, Self, "Path(@0).mkdir()"),
        P("Path.read_bytes", "Sharpy.Path.ReadBytes()", "pathlib", PathRequirement.NeedsFileToRead, Self, "Path(@0).read_bytes()"),
        P("Path.read_text", "Sharpy.Path.ReadText(string)", "pathlib", PathRequirement.NeedsFileToRead, Self, "Path(@0).read_text()"),
        P("Path.rename", "Sharpy.Path.Rename(string)", "pathlib", PathRequirement.Rename, new[] { PathPositions.Receiver, "target" }, "Path(@0).rename(@1)"),
        P("Path.replace", "Sharpy.Path.Replace(string)", "pathlib", PathRequirement.Rename, new[] { PathPositions.Receiver, "target" }, "Path(@0).replace(@1)"),
        P("Path.rmdir", "Sharpy.Path.Rmdir()", "pathlib", PathRequirement.RemovesDir, Self, "Path(@0).rmdir()"),
        P("Path.touch", "Sharpy.Path.Touch(bool)", "pathlib", PathRequirement.NeedsFileToWrite, Self, "Path(@0).touch()"),
        P("Path.unlink", "Sharpy.Path.Unlink(bool)", "pathlib", PathRequirement.RemovesFile, Self, "Path(@0).unlink()"),
        P("Path.write_bytes", "Sharpy.Path.WriteBytes(byte[])", "pathlib", PathRequirement.NeedsFileToWrite, Self, "Path(@0).write_bytes(b\"x\")"),
        P("Path.write_text", "Sharpy.Path.WriteText(string, string)", "pathlib", PathRequirement.NeedsFileToWrite, Self, "Path(@0).write_text(\"x\")"),

        // ---- shutil ----
        P("shutil.copy", Shutil + "Copy(string, string)", "shutil", PathRequirement.NeedsFileToRead, new[] { "src", "dst" }, "shutil.copy(@0, @1)"),
        P("shutil.copy2", Shutil + "Copy2(string, string)", "shutil", PathRequirement.NeedsFileToRead, new[] { "src", "dst" }, "shutil.copy2(@0, @1)"),
        P("shutil.copytree", Shutil + "Copytree(string, string)", "shutil", PathRequirement.CreatesDir, new[] { "src", "dst" }, "shutil.copytree(@0, @1)"),
        P("shutil.move", Shutil + "Move(string, string)", "shutil", PathRequirement.Rename, new[] { "src", "dst" }, "shutil.move(@0, @1)"),
        P("shutil.rmtree", Shutil + "Rmtree(string)", "shutil", PathRequirement.RemovesDir, Path0, "shutil.rmtree(@0)"),
        P("shutil.disk_usage", Shutil + "DiskUsage(string)", "shutil", PathRequirement.NeedsAny, Path0, "shutil.disk_usage(@0)"),
        P("shutil.which", Shutil + "Which(string)", "shutil", PathRequirement.NeedsAny, new[] { "name" }, "shutil.which(@0)"),

        // ---- glob ----
        P("glob.glob", "Sharpy.GlobModule.Glob(string)", "glob", PathRequirement.NeedsDir, new[] { "pattern" }, "glob.glob(@0)"),
        P("glob.iglob", "Sharpy.GlobModule.Iglob(string)", "glob", PathRequirement.NeedsDir, new[] { "pattern" }, "list(glob.iglob(@0))"),

        // ---- gzip ----
        P("gzip.open(rb)", "Sharpy.GzipModule.Open(string, string, int)", "gzip", PathRequirement.NeedsFileToRead, new[] { "filename" }, "gzip.open(@0).read()"),
        P("gzip.open(wb)", "Sharpy.GzipModule.Open(string, string, int)", "gzip", PathRequirement.NeedsFileToWrite, new[] { "filename" }, "gzip.open(@0, \"wb\").close()"),
        P("gzip.GzipFile", "Sharpy.GzipFile.GzipFile(string, string, int, System.IO.Stream)", "gzip", PathRequirement.NeedsFileToRead, new[] { "filename" }, "gzip.GzipFile(@0).read()"),

        // ---- tarfile ----
        P("tarfile.open(r)", "Sharpy.TarfileModule.Open(string, string)", "tarfile", PathRequirement.NeedsFileToRead, new[] { "name" }, "tarfile.open(@0).close()"),
        P("tarfile.open(w)", "Sharpy.TarfileModule.Open(string, string)", "tarfile", PathRequirement.NeedsFileToWrite, new[] { "name" }, "tarfile.open(@0, \"w\").close()"),
        P("tarfile.is_tarfile", "Sharpy.TarfileModule.IsTarfile(string)", "tarfile", PathRequirement.NeedsAny, new[] { "name" }, "tarfile.is_tarfile(@0)"),
        P("TarFile.getnames", "Sharpy.TarFile.Getnames()", "tarfile", PathRequirement.NeedsFileToRead, Self, "tarfile.open(@0).getnames()"),
        P("TarFile.getmembers", "Sharpy.TarFile.Getmembers()", "tarfile", PathRequirement.NeedsFileToRead, Self, "tarfile.open(@0).getmembers()"),
        P("TarFile.getmember", "Sharpy.TarFile.Getmember(string)", "tarfile", PathRequirement.NeedsFileToRead, Self, "tarfile.open(@0).getmember(\"m.txt\")"),
        P("TarFile.extractfile", "Sharpy.TarFile.Extractfile(string)", "tarfile", PathRequirement.NeedsFileToRead, Self, "tarfile.open(@0).extractfile(\"m.txt\")"),
        P("TarFile.add", "Sharpy.TarFile.Add(string, string, bool)", "tarfile", PathRequirement.NeedsAny, new[] { "name" }, "tarfile.open(@auxout, \"w\").add(@0)"),
        P("TarFile.extract", "Sharpy.TarFile.Extract(string, string)", "tarfile", PathRequirement.NeedsDir, Path0, "tarfile.open(@auxtar).extract(\"m.txt\", @0)"),
        P("TarFile.extractall", "Sharpy.TarFile.Extractall(string, Sharpy.List<Sharpy.TarInfo>)", "tarfile", PathRequirement.NeedsDir, Path0, "tarfile.open(@auxtar).extractall(@0)"),

        // ---- zipfile ----
        P("zipfile.ZipFile(r)", "Sharpy.ZipFileArchive.ZipFileArchive(string, string, int, bool)", "zipfile", PathRequirement.NeedsFileToRead, new[] { "file" }, "zipfile.ZipFile(@0).close()"),
        P("zipfile.ZipFile(w)", "Sharpy.ZipFileArchive.ZipFileArchive(string, string, int, bool)", "zipfile", PathRequirement.NeedsFileToWrite, new[] { "file" }, "zipfile.ZipFile(@0, \"w\").close()"),
        P("zipfile.is_zipfile", "Sharpy.ZipfileModule.IsZipfile(string)", "zipfile", PathRequirement.NeedsAny, new[] { "filename" }, "zipfile.is_zipfile(@0)"),
        P("ZipFile.extract", "Sharpy.ZipFileArchive.Extract(string, string)", "zipfile", PathRequirement.NeedsDir, Path0, "zipfile.ZipFile(@auxzip).extract(\"m.txt\", @0)"),
        P("ZipFile.extractall", "Sharpy.ZipFileArchive.Extractall(string, Sharpy.List<string>)", "zipfile", PathRequirement.NeedsDir, Path0, "zipfile.ZipFile(@auxzip).extractall(@0)"),
        P("ZipFile.write", "Sharpy.ZipFileArchive.Write(string, string, int?)", "zipfile", PathRequirement.NeedsFileToRead, new[] { "filename" }, "zipfile.ZipFile(@auxout, \"w\").write(@0)"),

        // ---- sqlite3 ----
        P("sqlite3.connect", "Sharpy.Sqlite3.Connect(string)", "sqlite3", PathRequirement.NeedsFileToWrite, new[] { "database" }, "sqlite3.connect(@0).close()"),

        // ---- xml (python: xml.etree.ElementTree) ----
        P("xml.parse", "Sharpy.Xml.Parse(string)", "xml", PathRequirement.NeedsFileToRead, new[] { "source" }, "xml.parse(@0)"),
        P("xml.ElementTree.parse", "Sharpy.ElementTree.Parse(string)", "xml", PathRequirement.NeedsFileToRead, new[] { "source" }, "xml.ElementTree.parse(@0)", "xml.ElementTree().parse(@0)"),
        P("xml.ElementTree.write", "Sharpy.ElementTree.Write(string, bool, string)", "xml", PathRequirement.NeedsFileToWrite, new[] { "filePath" }, "xml.ElementTree(xml.Element(\"a\")).write(@0)"),

        // ---- toml (python: tomllib reads; a write is python's open(p, "w")) ----
        P("toml.load_file", "Sharpy.Toml.LoadFile(string)", "toml", PathRequirement.NeedsFileToRead, Path0, "toml.load_file(@0)", "tomllib.load(open(@0, \"rb\"))"),
        P("toml.dump_file", "Sharpy.Toml.DumpFile(object, string)", "toml", PathRequirement.NeedsFileToWrite, Path0, "d: dict[str, object] = {\"a\": 1}\ntoml.dump_file(d, @0)", "open(@0, \"w\").write(\"a = 1\\n\")"),
        P("toml.dump_file(sort_keys)", "Sharpy.Toml.DumpFile(object, string, bool)", "toml", PathRequirement.NeedsFileToWrite, Path0, "d: dict[str, object] = {\"a\": 1}\ntoml.dump_file(d, @0, True)", "open(@0, \"w\").write(\"a = 1\\n\")"),

        // ---- configparser ----
        P("ConfigParser.read", "Sharpy.ConfigParser.Read(string)", "configparser", PathRequirement.NeedsFileToRead, new[] { "filename" }, "configparser.ConfigParser().read(@0)"),
        P("ConfigParser.write_to_file", "Sharpy.ConfigParser.WriteToFile(string, bool)", "configparser", PathRequirement.NeedsFileToWrite, new[] { "filename" },
            "configparser.ConfigParser().write_to_file(@0)", "configparser.ConfigParser().write(open(@0, \"w\"))"),

    }.Concat(SinglePathDoors.SelectMany(d => new[]
    {
        P("PathErrors.check_" + d.Snake, PathErrors + "Check" + d.Door + "(string)", "PathErrors", d.Requirement, Path0,
            "PathErrors.check_" + d.Snake + "(@0)", d.Python),
        P("PathErrors.translate_" + d.Snake, PathErrors + "Translate" + d.Door + "(System.Exception, string)", "PathErrors", d.Requirement, Path0,
            "raise PathErrors.translate_" + d.Snake + "(IOException(\"probe\"), @0)", d.Python + "\nraise OSError(\"probe\")"),
    })).ToArray();

    /// <summary>
    /// Path positions (axis 2): <c>entry :: parameter</c> → null for <c>path</c> (a placeholder of ≥ 1 probe
    /// of that entry point) or <c>not a path: &lt;reason&gt;</c>. Only probed entry points are classified.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string?> Positions = BuildPositions();

    private static Dictionary<string, string?> BuildPositions()
    {
        const string Mode = "not a path: the open mode";
        const string Encoding = "not a path: a text encoding name";
        const string Pattern = "not a path: a glob pattern matched below the receiver";
        const string Member = "not a path: an archive member name";
        const string OpenArchive = "not a path: an already-open archive (its path is the constructor's position, probed there)";
        const string Parser = "not a path: an in-memory parser object";
        const string Tree = "not a path: an in-memory element tree";
        var d = new Dictionary<string, string?>(StringComparer.Ordinal);
        void Path(string entry, params string[] names)
        {
            foreach (var n in names)
                d.Add($"{entry} :: {n}", null);
        }
        void Not(string entry, string name, string reason) => d.Add($"{entry} :: {name}", reason);

        Path(Open1, "path");
        Path(Open2, "path");
        Not(Open2, "mode", Mode);
        Path(Open3, "path");
        Not(Open3, "mode", Mode);
        Not(Open3, "encoding", Encoding);
        foreach (var m in new[] { "Remove(string)", "Mkdir(string)", "Makedirs(string, bool)", "Rmdir(string)", "Listdir(string)", "Chdir(string)", "Stat(string)", "PathExists(string)" })
            Path(Os + m, "path");
        Path(Os + "Rename(string, string)", "src", "dst");
        Path(PathErrors + "Rename(string, string)", "src", "dst");
        foreach (var door in SinglePathDoors)
        {
            Path(PathErrors + "Check" + door.Door + "(string)", "path");
            Path(PathErrors + "Translate" + door.Door + "(System.Exception, string)", "path");
        }
        Path(Os + "Walk(string)", "top");
        foreach (var m in new[] { "Exists(string)", "Isfile(string)", "Isdir(string)", "Getsize(string)" })
            Path(OsPath + m, "path");
        foreach (var m in new[] { "Exists()", "IsDir()", "IsFile()", "IsSymlink()", "Stat()", "Iterdir()", "Mkdir(bool, bool)", "ReadBytes()", "Rmdir()", "Touch(bool)", "Unlink(bool)", "WriteBytes(byte[])" })
            Path("Sharpy.Path." + m, PathPositions.Receiver);
        Path("Sharpy.Path.Glob(string)", PathPositions.Receiver);
        Not("Sharpy.Path.Glob(string)", "pattern", Pattern);
        Path("Sharpy.Path.Rglob(string)", PathPositions.Receiver);
        Not("Sharpy.Path.Rglob(string)", "pattern", Pattern);
        Path("Sharpy.Path.ReadText(string)", PathPositions.Receiver);
        Not("Sharpy.Path.ReadText(string)", "encoding", Encoding);
        Path("Sharpy.Path.Rename(string)", PathPositions.Receiver, "target");
        Path("Sharpy.Path.Replace(string)", PathPositions.Receiver, "target");
        Path("Sharpy.Path.WriteText(string, string)", PathPositions.Receiver);
        Not("Sharpy.Path.WriteText(string, string)", "data", "not a path: the text written");
        Not("Sharpy.Path.WriteText(string, string)", "encoding", Encoding);
        foreach (var m in new[] { "Copy(string, string)", "Copy2(string, string)", "Copytree(string, string)", "Move(string, string)" })
            Path(Shutil + m, "src", "dst");
        Path(Shutil + "Rmtree(string)", "path");
        Path(Shutil + "DiskUsage(string)", "path");
        Path(Shutil + "Which(string)", "name");
        Path("Sharpy.GlobModule.Glob(string)", "pattern");
        Path("Sharpy.GlobModule.Iglob(string)", "pattern");
        Path("Sharpy.GzipModule.Open(string, string, int)", "filename");
        Not("Sharpy.GzipModule.Open(string, string, int)", "mode", Mode);
        Path("Sharpy.GzipFile.GzipFile(string, string, int, System.IO.Stream)", "filename");
        Not("Sharpy.GzipFile.GzipFile(string, string, int, System.IO.Stream)", "mode", Mode);
        Path("Sharpy.TarfileModule.Open(string, string)", "name");
        Not("Sharpy.TarfileModule.Open(string, string)", "mode", Mode);
        Path("Sharpy.TarfileModule.IsTarfile(string)", "name");
        Path("Sharpy.TarFile.Getnames()", PathPositions.Receiver);
        Path("Sharpy.TarFile.Getmembers()", PathPositions.Receiver);
        Path("Sharpy.TarFile.Getmember(string)", PathPositions.Receiver);
        Not("Sharpy.TarFile.Getmember(string)", "name", Member);
        Path("Sharpy.TarFile.Extractfile(string)", PathPositions.Receiver);
        Not("Sharpy.TarFile.Extractfile(string)", "name", Member);
        Not("Sharpy.TarFile.Add(string, string, bool)", PathPositions.Receiver, OpenArchive);
        Path("Sharpy.TarFile.Add(string, string, bool)", "name");
        Not("Sharpy.TarFile.Add(string, string, bool)", "arcname", Member);
        Not("Sharpy.TarFile.Extract(string, string)", PathPositions.Receiver, OpenArchive);
        Not("Sharpy.TarFile.Extract(string, string)", "name", Member);
        Path("Sharpy.TarFile.Extract(string, string)", "path");
        Not("Sharpy.TarFile.Extractall(string, Sharpy.List<Sharpy.TarInfo>)", PathPositions.Receiver, OpenArchive);
        Path("Sharpy.TarFile.Extractall(string, Sharpy.List<Sharpy.TarInfo>)", "path");
        Path("Sharpy.ZipFileArchive.ZipFileArchive(string, string, int, bool)", "file");
        Not("Sharpy.ZipFileArchive.ZipFileArchive(string, string, int, bool)", "mode", Mode);
        Path("Sharpy.ZipfileModule.IsZipfile(string)", "filename");
        Not("Sharpy.ZipFileArchive.Extract(string, string)", PathPositions.Receiver, OpenArchive);
        Not("Sharpy.ZipFileArchive.Extract(string, string)", "member", Member);
        Path("Sharpy.ZipFileArchive.Extract(string, string)", "path");
        Not("Sharpy.ZipFileArchive.Extractall(string, Sharpy.List<string>)", PathPositions.Receiver, OpenArchive);
        Path("Sharpy.ZipFileArchive.Extractall(string, Sharpy.List<string>)", "path");
        Not("Sharpy.ZipFileArchive.Write(string, string, int?)", PathPositions.Receiver, OpenArchive);
        Path("Sharpy.ZipFileArchive.Write(string, string, int?)", "filename");
        Not("Sharpy.ZipFileArchive.Write(string, string, int?)", "arcname", Member);
        Path("Sharpy.Sqlite3.Connect(string)", "database");
        Path("Sharpy.Xml.Parse(string)", "source");
        Path("Sharpy.ElementTree.Parse(string)", "source");
        Not("Sharpy.ElementTree.Write(string, bool, string)", PathPositions.Receiver, Tree);
        Path("Sharpy.ElementTree.Write(string, bool, string)", "filePath");
        Not("Sharpy.ElementTree.Write(string, bool, string)", "encoding", Encoding);
        Path("Sharpy.Toml.LoadFile(string)", "path");
        Not("Sharpy.Toml.DumpFile(object, string)", "obj", "not a path: the value serialized");
        Path("Sharpy.Toml.DumpFile(object, string)", "path");
        Not("Sharpy.Toml.DumpFile(object, string, bool)", "obj", "not a path: the value serialized");
        Path("Sharpy.Toml.DumpFile(object, string, bool)", "path");
        Not("Sharpy.ConfigParser.Read(string)", PathPositions.Receiver, Parser);
        Path("Sharpy.ConfigParser.Read(string)", "filename");
        Not("Sharpy.ConfigParser.WriteToFile(string, bool)", PathPositions.Receiver, Parser);
        Path("Sharpy.ConfigParser.WriteToFile(string, bool)", "filename");
        return d;
    }

    /// <summary>
    /// Entry points with no probe row, each in one of the two closed forms: <c>unreachable with a user
    /// path: &lt;why the edge cannot fire&gt;</c> or <c>out of contract: #&lt;open issue&gt;</c> (the cell
    /// is another class's; such rows are mirrored, with their issue, in
    /// <c>oserror-path-state-out-of-contract-allowlist.txt</c> so the issue-state gate sees them).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Exclusions = BuildExclusions();

    private static Dictionary<string, string> BuildExclusions()
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        const string MergeWhy = Unreachable + "the reach is the over-approximated IDisposable.Dispose edge of the merge's enumerators "
            + "to unittest's TmpPathFixture.Dispose; no heapq argument is a path";
        foreach (var m in new[]
                 {
                     "Merge<T, TKey>(Sharpy.List<T>, Sharpy.List<T>, Sharpy.List<T>, System.Func<T, TKey>, bool)",
                     "Merge<T, TKey>(Sharpy.List<T>, Sharpy.List<T>, System.Func<T, TKey>, bool)",
                     "Merge<T, TKey>(Sharpy.List<T>[], System.Func<T, TKey>, bool)",
                     "Merge<T>(Sharpy.List<T>, Sharpy.List<T>, Sharpy.List<T>, bool)",
                     "Merge<T>(Sharpy.List<T>, Sharpy.List<T>, bool)",
                     "Merge<T>(Sharpy.List<T>[], bool)",
                     "Merge<T>(params Sharpy.List<T>[])",
                 })
            d.Add("Sharpy.Heapq.HeapqModule." + m, MergeWhy);

        const string TempWhy = Unreachable + "no parameter names a directory (python's dir= is not in this signature); "
            + "the file lives in the system temp directory under a generated name, and prefix/suffix are name parts";
        d.Add("Sharpy.TempfileModule.TempfileModuleModule.Mkdtemp(string)", TempWhy);
        d.Add("Sharpy.TempfileModule.TempfileModuleModule.Mkstemp(string, string)", TempWhy);
        d.Add("Sharpy.TempfileModule.NamedTemporaryFile.NamedTemporaryFile(string, string, string, bool)", TempWhy);
        d.Add("Sharpy.TempfileModule.TemporaryDirectory.TemporaryDirectory(string, string)", TempWhy);
        const string OwnTempWhy = Unreachable + "acts on the object's own generated temp path, never on a user path";
        foreach (var m in new[]
                 {
                     "NamedTemporaryFile.Close()", "NamedTemporaryFile.Exit()", "NamedTemporaryFile.Read()", "NamedTemporaryFile.Write(string)",
                     "SpooledTemporaryFile.Close()", "SpooledTemporaryFile.Exit()", "SpooledTemporaryFile.Read()", "SpooledTemporaryFile.Rollover()",
                     "SpooledTemporaryFile.Write(string)", "TemporaryDirectory.Cleanup()", "TemporaryDirectory.Exit()",
                 })
            d.Add("Sharpy.TempfileModule." + m, OwnTempWhy);
        d.Add("Sharpy.TmpPathFixture.TmpPathFixture()", Unreachable + "unittest's tmp_path fixture creates its own temp directory");
        d.Add("Sharpy.TmpPathFixture.Dispose()", Unreachable + "unittest's tmp_path fixture removes its own temp directory");
        return d;
    }

    /// <summary>R1: a requirement no probe declares, with the reason. Empty: every member is probed.</summary>
    public static readonly IReadOnlyDictionary<PathRequirement, string> RequirementExclusions =
        new Dictionary<PathRequirement, string>();
}
