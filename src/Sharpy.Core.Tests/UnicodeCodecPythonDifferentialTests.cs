using System.Globalization;
using System.Text.Json;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Core.Tests;

/// <summary>
/// <c>str.encode</c> / <c>bytes.decode</c> against CPython, cell by cell (#2477, #2495): a GENERATED matrix
/// codec name × input × error handler whose expected value is python's answer to the same call, read from
/// <c>python3</c> in one batch, never from a hand table.
/// <para><b>Axes.</b> Codec names: every key of python's own <c>encodings.aliases</c> table whose codec is one
/// Sharpy supports (or a recorded gap), plus each codec's module name and display name, each in spelling
/// variants (case, <c>-</c>/<c>_</c>/space, doubled and edge separators, non-ASCII characters, which 3.15
/// deprecates but still reads as separators), names python rejects, and names holding NUL. Inputs
/// (as UTF-16 code units for encode, bytes for decode): ASCII, Latin-1 range, BMP, astral (surrogate pairs),
/// lone surrogates, empty; and for decode valid, truncated, invalid lead and continuation, UTF-8/16/32 BOMs
/// little/big/absent, odd length, out-of-range and surrogate code points. Handlers: python's eight plus an
/// unknown name.</para>
/// <para><b>Compared.</b> The bytes or the string (UTF-16 code units), or the exception type and message;
/// for <see cref="UnicodeEncodeError"/>/<see cref="UnicodeDecodeError"/> also <c>encoding</c>, <c>object</c>,
/// <c>start</c>, <c>end</c>, <c>reason</c>.</para>
/// <para><b>Generated deviation (Axiom 1).</b> A Sharpy <c>str</c> is UTF-16 code units, so an encode error's
/// <c>start</c>/<c>end</c> count code units where python counts code points: the expected positions (and the
/// position in the message) are python's mapped through the input's code units (<see cref="CodePointToUnit"/>),
/// computed per cell, never listed.</para>
/// <para><b>Allowlist.</b> <see cref="KnownGaps"/>: one row per class of cell that still differs, each citing
/// #2495 and pinning Sharpy's exception and message; a handler row covers only cells where python really
/// reached the handler. It ratchets both ways: a differing cell no row covers fails, and a row that covers no
/// differing cell fails (delete it).</para>
/// </summary>
public class UnicodeCodecPythonDifferentialTests
{
    private readonly ITestOutputHelper _output;

    public UnicodeCodecPythonDifferentialTests(ITestOutputHelper output) => _output = output;

    private const int MinPythonMinor = 12;

    /// <summary>Python codec module names Sharpy implements.</summary>
    private static readonly string[] SupportedModules =
    {
        "utf_8", "utf_8_sig", "ascii", "latin_1",
        "utf_16", "utf_16_le", "utf_16_be", "utf_32", "utf_32_le", "utf_32_be",
    };

    /// <summary>The spellings the semantic axis crosses with every input and handler: one per codec.</summary>
    private static readonly string[] CanonicalNames =
    {
        "utf-8", "utf-8-sig", "ascii", "latin-1",
        "utf-16", "utf-16-le", "utf-16-be", "utf-32", "utf-32-le", "utf-32-be",
    };

    /// <summary>Python's error handlers, plus a name python rejects.</summary>
    private static readonly string[] Handlers =
    {
        "strict", "ignore", "replace", "backslashreplace", "xmlcharrefreplace",
        "namereplace", "surrogateescape", "surrogatepass", "bogus",
    };

    /// <summary>
    /// Names outside the alias-derived variants, whatever python makes of them: rejected spellings, non-ASCII
    /// characters (separators in 3.15's <c>normalize_encoding</c>, deprecated but accepted) and NUL (a
    /// <c>ValueError</c> before any lookup).
    /// </summary>
    private static readonly string[] ExtraNames =
    {
        "", "bogus", "utf.8", "utf8sig", "ucs2", "utf-8-bogus", "_", "--",
        "uséascii", "ußtf8", "utf８", "u\U0001F600tf8", "utf\U0001F6008", "ǅutf8",
        "latin¹1", "asciié", "éascii", "utf 8", "é", "utféé8",
        "utf-8\0", "utf\08", "\0utf8", "bogus\0", "\0", "ascii\0",
    };

    /// <summary>
    /// A class of cells that still differs from python. A cell is covered only when Sharpy's outcome is exactly
    /// <c>SharpyExc</c> with <c>SharpyMessage</c> (<c>{name}</c> is the cell's encoding name) and, for a handler
    /// row, python really reached the handler (the same call under <c>strict</c> raises): a clean-input cell
    /// with a handler Sharpy lacks must match python, never be absorbed.
    /// </summary>
    private sealed record Gap(string Axis, string Value, string Issue, string SharpyExc, string SharpyMessage);

    /// <summary>Cells that still differ from python, one row per class (see the class comment).</summary>
    private static readonly Gap[] KnownGaps =
    {
        // namereplace needs the Unicode character name table.
        new("handler", "namereplace", "#2495", "LookupError", "error handler 'namereplace' is not supported yet (#2495)"),
        new("handler", "surrogateescape", "#2495", "LookupError", "error handler 'surrogateescape' is not supported yet (#2495)"),
        new("handler", "surrogatepass", "#2495", "LookupError", "error handler 'surrogatepass' is not supported yet (#2495)"),
        // Code pages need CodePagesEncodingProvider, a package dependency on netstandard2.1.
        new("codec", "cp1252", "#2495", "LookupError", "unknown encoding: {name}"),
    };

    private const string OracleScript = """
        import codecs, json, struct, sys, warnings
        import encodings.aliases

        # 3.15 deprecates non-ASCII encoding names; they are measured, not warned about.
        warnings.simplefilter("ignore")

        def units(s):
            b = s.encode("utf-16-le", "surrogatepass")
            return list(struct.unpack("<%dH" % (len(b) // 2), b))

        def text(u):
            return struct.pack("<%dH" % len(u), *u).decode("utf-16-le", "surrogatepass")

        def canon(name):
            try:
                return codecs.lookup(name).name
            except (LookupError, ValueError):
                return None

        def dirty(c):
            try:
                if c["op"] == "e":
                    text(c["in"]).encode(c["enc"], "strict")
                else:
                    bytes(c["in"]).decode(c["enc"], "strict")
            except UnicodeError:
                return True
            except Exception:
                pass
            return False

        for line in sys.stdin:
            c = json.loads(line)
            if c["op"] == "aliases":
                print(json.dumps({"aliases": sorted([k, v] for k, v in encodings.aliases.aliases.items())}))
                continue
            out = {"id": c["id"], "codec": canon(c["enc"]), "dirty": dirty(c)}
            try:
                if c["op"] == "e":
                    out["ok"] = list(text(c["in"]).encode(c["enc"], c["err"]))
                else:
                    out["ok"] = units(bytes(c["in"]).decode(c["enc"], c["err"]))
            except UnicodeEncodeError as e:
                out.update(exc="UnicodeEncodeError", msg=str(e), enc=e.encoding, start=e.start, end=e.end,
                           reason=e.reason, obj=units(e.object))
            except UnicodeDecodeError as e:
                out.update(exc="UnicodeDecodeError", msg=str(e), enc=e.encoding, start=e.start, end=e.end,
                           reason=e.reason, obj=list(e.object))
            except (LookupError, TypeError, ValueError) as e:
                out.update(exc=type(e).__name__, msg=str(e))
            print(json.dumps(out))
        """;

    // ── inputs ──

    private static readonly string[] EncodeInputs =
    {
        "",
        "abc",
        "café",
        "ÿ\u0080",
        "a€b",
        "a\U0001F600b",
        "café€\U0001F600",
        "\U0001F600é",
        "x\udc80\udc81y\udc82",
        "\ud800",
        "\ud800a",
        "a\udfff",
        "\U0001F600\udc80",
        "﻿a",
    };

    private static readonly byte[][] DecodeInputs =
    {
        Array.Empty<byte>(),
        "abc"u8.ToArray(),
        new byte[] { 0x63, 0x61, 0x66, 0xC3, 0xA9 },
        new byte[] { 0xC3, 0xA9, 0xFF },
        new byte[] { 0xF0, 0x9F, 0x98, 0x80 },
        new byte[] { 0xFF },
        new byte[] { 0x61, 0xFF, 0xFE, 0x62 },
        new byte[] { 0x80 },
        new byte[] { 0x61, 0xE2, 0x82, 0x62 },
        new byte[] { 0xED, 0xA0, 0x80 },
        new byte[] { 0xC0, 0x80 },
        new byte[] { 0xF0, 0x9F, 0x98 },
        new byte[] { 0x63, 0x61, 0x66, 0xE9 },
        new byte[] { 0xEF, 0xBB, 0xBF, 0x61 },
        new byte[] { 0xEF, 0xBB, 0xBF },
        new byte[] { 0xEF, 0xBB },
        new byte[] { 0xEF, 0xBB, 0xBF, 0x61, 0xFF },
        new byte[] { 0xFF, 0xFE, 0x61, 0x00 },
        new byte[] { 0xFE, 0xFF, 0x00, 0x61 },
        new byte[] { 0x61, 0x00, 0x62, 0x00 },
        new byte[] { 0x61, 0x00, 0x62 },
        new byte[] { 0x61 },
        new byte[] { 0xFF, 0xFE, 0x61 },
        new byte[] { 0xFE, 0xFF, 0x00 },
        new byte[] { 0x00, 0xDC },
        new byte[] { 0x00, 0xD8, 0x61, 0x00 },
        new byte[] { 0x00, 0xD8 },
        new byte[] { 0x00, 0xD8, 0x61 },
        new byte[] { 0x3D, 0xD8, 0x00, 0xDE },
        new byte[] { 0xFE, 0xFF, 0xD8, 0x3D, 0xDE, 0x00 },
        new byte[] { 0xFE, 0xFF, 0xD8, 0x00, 0x00, 0x61 },
        new byte[] { 0x61, 0x00, 0x00, 0xDC, 0x00, 0xDC, 0x62 },
        new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x61, 0x00, 0x00, 0x00 },
        new byte[] { 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x61 },
        new byte[] { 0x61, 0x00, 0x00, 0x00 },
        new byte[] { 0x61, 0x00, 0x00, 0x00, 0x62 },
        new byte[] { 0x00, 0x00, 0x11, 0x00 },
        new byte[] { 0x00, 0xD8, 0x00, 0x00 },
        new byte[] { 0x00, 0xF6, 0x01, 0x00 },
        new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x00, 0xD8, 0x00, 0x00 },
        new byte[] { 0x00, 0x00, 0x11, 0x00, 0x00, 0x00, 0x11, 0x00, 0x61, 0x00, 0x00, 0x00 },
        new byte[] { 0x80, 0xFF },
    };

    // A payload whose result tells every supported codec apart, for the name axis.
    private const string NameAxisText = "aé€\U0001F600";
    private static readonly byte[] NameAxisBytes = { 0xFF, 0xFE, 0x61, 0x00, 0xC3, 0xA9 };

    private sealed record Cell(int Id, bool Encode, string Encoding, string Errors, int[] Input);

    private sealed record Outcome(int[]? Ok, string? Exc, string? Msg, string? Enc, int Start, int End, string? Reason, int[]? Obj);

    [SkippableFact]
    public void Encode_and_decode_match_python_on_every_generated_cell()
    {
        var python = PythonInterpreter.RequireOrSkip(MinPythonMinor);
        var script = Path.Combine(Path.GetTempPath(), $"sharpy-codec-oracle-{Guid.NewGuid():N}.py");
        File.WriteAllText(script, OracleScript);
        try
        {
            var aliases = ReadAliases(python, script);
            var cells = GenerateCells(aliases, out int nameCount);
            var lines = python.RunBatch(script, cells.Select(ToJson));
            Assert.Equal(cells.Count, lines.Count);
            _output.WriteLine($"{cells.Count} cells, {nameCount} codec names, oracle {python.Version}");

            var failures = new System.Collections.Generic.List<string>();
            var gapHits = KnownGaps.ToDictionary(g => g, _ => 0);
            var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
            int deviationCells = 0;
            foreach (var line in lines)
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var cell = cells[root.GetProperty("id").GetInt32()];
                var codec = root.GetProperty("codec").ValueKind == JsonValueKind.Null ? null : root.GetProperty("codec").GetString();
                var expected = Expected(cell, root, ref deviationCells);
                var actual = RunSharpy(cell);
                var kind = expected.Exc ?? "ok";
                kinds[kind] = kinds.GetValueOrDefault(kind) + 1;

                var diff = Diff(expected, actual);
                if (diff is null)
                    continue;
                bool dirty = root.GetProperty("dirty").GetBoolean();
                var gap = KnownGaps.FirstOrDefault(g =>
                    actual.Exc == g.SharpyExc && actual.Msg == g.SharpyMessage.Replace("{name}", cell.Encoding)
                    && ((g.Axis == "handler" && g.Value == cell.Errors && dirty) || (g.Axis == "codec" && g.Value == codec)));
                if (gap is not null)
                {
                    gapHits[gap]++;
                    continue;
                }
                failures.Add($"{Describe(cell)}: {diff}");
            }

            // Positive controls: the generator really reaches every outcome arm, the alias table and the
            // Axiom 1 deviation, so the comparison cannot pass over a truncated matrix.
            Assert.True(nameCount >= 150, $"only {nameCount} codec names generated");
            int nonAscii = cells.Count(c => c.Encoding.Any(ch => ch >= 0x80));
            int withNul = cells.Count(c => c.Encoding.Contains('\0'));
            Assert.True(nonAscii >= 200 && withNul >= 10, $"only {nonAscii} non-ASCII-name and {withNul} NUL-name cells");
            foreach (var arm in new[] { "ok", "UnicodeEncodeError", "UnicodeDecodeError", "LookupError", "TypeError", "ValueError" })
                Assert.True(kinds.GetValueOrDefault(arm) >= 10, $"only {kinds.GetValueOrDefault(arm)} cells where python gives {arm}");
            Assert.True(deviationCells >= 5, $"only {deviationCells} cells exercised the UTF-16 position deviation");

            var stale = gapHits.Where(kv => kv.Value == 0).Select(kv => $"{kv.Key.Axis} {kv.Key.Value}").ToList();
            Assert.True(failures.Count == 0 && stale.Count == 0,
                $"{failures.Count} of {cells.Count} cells differ from {python.Version}:\n" +
                string.Join("\n", failures.Take(60)) +
                (stale.Count > 0 ? "\nKnownGaps rows that no longer cover a differing cell (delete them): " + string.Join(", ", stale) : ""));
        }
        finally
        {
            File.Delete(script);
        }
    }

    // ── generation ──

    private static System.Collections.Generic.List<(string Alias, string Module)> ReadAliases(PythonInterpreter python, string script)
    {
        var line = Assert.Single(python.RunBatch(script, new[] { """{"op": "aliases"}""" }));
        using var doc = JsonDocument.Parse(line);
        return doc.RootElement.GetProperty("aliases").EnumerateArray()
            .Select(pair => (pair[0].GetString()!, pair[1].GetString()!))
            .ToList();
    }

    private static System.Collections.Generic.List<Cell> GenerateCells(System.Collections.Generic.List<(string Alias, string Module)> aliases, out int nameCount)
    {
        var modules = new HashSet<string>(SupportedModules, StringComparer.Ordinal);
        var gapModules = KnownGaps.Where(g => g.Axis == "codec").Select(g => g.Value);
        var sources = new System.Collections.Generic.List<string>(SupportedModules);
        sources.AddRange(gapModules);
        sources.AddRange(CanonicalNames);
        sources.AddRange(new[] { "iso8859-1", "us-ascii", "UTF8", "Latin-1" });
        sources.AddRange(aliases.Where(a => modules.Contains(a.Module) || gapModules.Contains(a.Module)).Select(a => a.Alias));
        Assert.True(sources.Count > 60, $"python's alias table gave only {sources.Count} names for the supported codecs");

        var names = new System.Collections.Generic.List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            foreach (var variant in Variants(source))
            {
                if (seen.Add(variant))
                    names.Add(variant);
            }
        }
        foreach (var extra in ExtraNames)
        {
            if (seen.Add(extra))
                names.Add(extra);
        }
        nameCount = names.Count;

        var cells = new System.Collections.Generic.List<Cell>();
        foreach (var name in names)
        {
            cells.Add(new Cell(cells.Count, true, name, "replace", Units(NameAxisText)));
            cells.Add(new Cell(cells.Count, false, name, "replace", NameAxisBytes.Select(b => (int)b).ToArray()));
        }
        foreach (var name in CanonicalNames)
        {
            foreach (var errors in Handlers)
            {
                foreach (var text in EncodeInputs)
                    cells.Add(new Cell(cells.Count, true, name, errors, Units(text)));
                foreach (var data in DecodeInputs)
                    cells.Add(new Cell(cells.Count, false, name, errors, data.Select(b => (int)b).ToArray()));
            }
        }
        return cells;
    }

    private static IEnumerable<string> Variants(string name)
    {
        yield return name;
        yield return name.ToUpperInvariant();
        yield return name.Replace('_', '-');
        yield return name.Replace('-', '_');
        yield return name.Replace('_', ' ').Replace('-', ' ');
        yield return name.Replace("_", "--").Replace("-", "--");
        yield return "-" + name + " ";
        yield return name.Length > 0 ? char.ToUpperInvariant(name[0]) + name.Substring(1) : name;
        // Non-ASCII characters: separators in python 3.15, wherever they stand.
        yield return name.Replace('_', '\u00e9').Replace('-', '\u00e9');
        yield return name.Replace('_', '\u00a0').Replace('-', '\u00a0');
        yield return name + "\U0001F600";
        yield return name.Substring(0, name.Length / 2) + "\u00df" + name.Substring(name.Length / 2);
    }

    private static int[] Units(string s) => s.Select(c => (int)c).ToArray();

    private static string ToJson(Cell cell) => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["id"] = cell.Id,
        ["op"] = cell.Encode ? "e" : "d",
        ["enc"] = cell.Encoding,
        ["err"] = cell.Errors,
        ["in"] = cell.Input,
    });

    // ── expected, with the Axiom 1 deviation ──

    private static Outcome Expected(Cell cell, JsonElement root, ref int deviationCells)
    {
        if (root.TryGetProperty("ok", out var ok))
            return new Outcome(Ints(ok), null, null, null, 0, 0, null, null);

        var exc = root.GetProperty("exc").GetString()!;
        var msg = root.GetProperty("msg").GetString()!;
        if (exc != "UnicodeEncodeError" && exc != "UnicodeDecodeError")
            return new Outcome(null, exc, msg, null, 0, 0, null, null);

        int start = root.GetProperty("start").GetInt32();
        int end = root.GetProperty("end").GetInt32();
        if (exc == "UnicodeEncodeError")
        {
            int unitStart = CodePointToUnit(cell.Input, start);
            int unitEnd = CodePointToUnit(cell.Input, end);
            if (unitStart != start || unitEnd != end)
            {
                deviationCells++;
                string pythonPosition = end - start == 1
                    ? $"in position {start}:"
                    : $"in position {start}-{end - 1}:";
                string unitPosition = end - start == 1
                    ? $"in position {unitStart}:"
                    : $"in position {unitStart}-{unitEnd - 1}:";
                Assert.Contains(pythonPosition, msg, StringComparison.Ordinal);
                msg = msg.Replace(pythonPosition, unitPosition, StringComparison.Ordinal);
            }
            start = unitStart;
            end = unitEnd;
        }
        return new Outcome(null, exc, msg, root.GetProperty("enc").GetString(), start, end,
            root.GetProperty("reason").GetString(), Ints(root.GetProperty("obj")));
    }

    /// <summary>The UTF-16 index of python's code point index <paramref name="codePoints"/> in <paramref name="units"/>.</summary>
    private static int CodePointToUnit(int[] units, int codePoints)
    {
        int i = 0;
        for (int k = 0; k < codePoints && i < units.Length; k++)
        {
            bool pair = i + 1 < units.Length && char.IsSurrogatePair((char)units[i], (char)units[i + 1]);
            i += pair ? 2 : 1;
        }
        return i;
    }

    private static int[] Ints(JsonElement array) => array.EnumerateArray().Select(e => e.GetInt32()).ToArray();

    // ── actual ──

    private static Outcome RunSharpy(Cell cell)
    {
        try
        {
            if (cell.Encode)
            {
                var text = new string(cell.Input.Select(u => (char)u).ToArray());
                var bytes = text.Encode(cell.Encoding, cell.Errors);
                return new Outcome(bytes.ToArray().Select(b => (int)b).ToArray(), null, null, null, 0, 0, null, null);
            }
            var decoded = new Bytes(cell.Input.Select(b => (byte)b).ToArray()).Decode(cell.Encoding, cell.Errors);
            return new Outcome(Units(decoded), null, null, null, 0, 0, null, null);
        }
        catch (UnicodeEncodeError e)
        {
            return new Outcome(null, "UnicodeEncodeError", e.Message, e.Encoding, e.Start, e.End, e.Reason, Units(e.Object));
        }
        catch (UnicodeDecodeError e)
        {
            return new Outcome(null, "UnicodeDecodeError", e.Message, e.Encoding, e.Start, e.End, e.Reason,
                e.Object.ToArray().Select(b => (int)b).ToArray());
        }
        catch (Exception e)
        {
            return new Outcome(null, e.GetType().Name, e.Message, null, 0, 0, null, null);
        }
    }

    private static string? Diff(Outcome expected, Outcome actual)
    {
        if (expected.Ok is not null)
        {
            if (actual.Ok is null)
                return $"python returns {Show(expected.Ok)}, Sharpy raises {actual.Exc}: {actual.Msg}";
            return expected.Ok.SequenceEqual(actual.Ok) ? null : $"python returns {Show(expected.Ok)}, Sharpy {Show(actual.Ok)}";
        }
        if (actual.Exc != expected.Exc)
        {
            var got = actual.Ok is not null ? "returns " + Show(actual.Ok) : $"raises {actual.Exc}: {actual.Msg}";
            return $"python raises {expected.Exc}: {expected.Msg}; Sharpy {got}";
        }
        var fields = new System.Collections.Generic.List<string>();
        if (actual.Msg != expected.Msg)
            fields.Add($"message '{actual.Msg}' (python '{expected.Msg}')");
        if (actual.Enc != expected.Enc)
            fields.Add($"encoding '{actual.Enc}' (python '{expected.Enc}')");
        if (actual.Start != expected.Start || actual.End != expected.End)
            fields.Add($"start/end {actual.Start}/{actual.End} (python {expected.Start}/{expected.End})");
        if (actual.Reason != expected.Reason)
            fields.Add($"reason '{actual.Reason}' (python '{expected.Reason}')");
        if (expected.Obj is not null && (actual.Obj is null || !expected.Obj.SequenceEqual(actual.Obj)))
            fields.Add($"object {Show(actual.Obj)} (python {Show(expected.Obj)})");
        return fields.Count == 0 ? null : $"{expected.Exc}: " + string.Join("; ", fields);
    }

    private static string Show(int[]? values) =>
        values is null ? "null" : "[" + string.Join(" ", values.Select(v => v.ToString("x2", CultureInfo.InvariantCulture))) + "]";

    private static string Describe(Cell cell)
    {
        var input = cell.Encode
            ? "'" + string.Concat(cell.Input.Select(u => u < 0x80 && u >= 0x20 ? ((char)u).ToString() : $"\\u{u:x4}")) + "'"
            : "b" + Show(cell.Input);
        return $"{input}.{(cell.Encode ? "encode" : "decode")}('{cell.Encoding}', '{cell.Errors}')";
    }
}
