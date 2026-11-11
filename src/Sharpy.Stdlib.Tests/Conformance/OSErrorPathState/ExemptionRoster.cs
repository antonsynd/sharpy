namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>One exemption: on this probe, python's outcome is this non-<c>OSError</c> class, for this reason.</summary>
internal sealed record ExemptionRow(string ProbeId, string PythonClass, string Reason);

/// <summary>
/// The committed exemption roster of G1 (plan-638e8b C2's Rule): a cell whose PYTHON outcome is a
/// class outside the <c>OSError</c> family is outside this contract and must match a row here. The
/// roster reads python's answer alone, so no Sharpy behaviour can move a cell into it; it is what stops
/// a harness bug (a misspelled twin's <c>AttributeError</c>) from doing so. Both directions are checked:
/// an exempt outcome with no row is red, and so is a row no cell produces. No row may name a class
/// python raises for a malformed program (SyntaxError, NameError, AttributeError, ImportError,
/// TypeError and their subclasses).
/// </summary>
internal static class ExemptionRoster
{
    private const string NotAnArchive = "a regular file whose content is not an archive (file CONTENT state, an excluded axis): python's module error, outside the OSError family";

    public static readonly IReadOnlyList<ExemptionRow> Rows = new ExemptionRow[]
    {
        new("tarfile.open(r)", "tarfile.ReadError", NotAnArchive),
        new("TarFile.getnames", "tarfile.ReadError", NotAnArchive),
        new("TarFile.getmembers", "tarfile.ReadError", NotAnArchive),
        new("TarFile.getmember", "tarfile.ReadError", NotAnArchive),
        new("TarFile.extractfile", "tarfile.ReadError", NotAnArchive),
        new("zipfile.ZipFile(r)", "zipfile.BadZipFile", NotAnArchive),
        new("sqlite3.connect", "sqlite3.OperationalError",
            "sqlite reports an unopenable database path as its own OperationalError (\"unable to open database file\"), not an errno-mapped OSError"),
        new("toml.load_file", "tomllib.TOMLDecodeError", "a regular file whose content is not TOML (file CONTENT state, an excluded axis); a ValueError"),
        new("ConfigParser.read", "configparser.MissingSectionHeaderError",
            "a regular file whose content has no section header (file CONTENT state, an excluded axis); configparser.Error"),
    };
}
