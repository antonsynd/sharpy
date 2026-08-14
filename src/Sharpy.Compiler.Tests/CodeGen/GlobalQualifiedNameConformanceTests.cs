using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Conformance test for the zero-parse tree handoff (#1095, #1050).
///
/// <para>
/// A <c>global::</c>-prefixed name built by handing a string literal straight to Roslyn's
/// <c>ParseName</c> / <c>ParseTypeName</c> / <c>IdentifierName</c> is <b>not reparse-equivalent</b>:
/// <c>ParseName("global::X.Y")</c> mis-tokenizes <c>global</c> as an ordinary identifier, so the tree
/// prints correctly (<c>ToFullString()</c> round-trips) but fails to bind when handed directly to
/// <c>CSharpSyntaxTree.Create</c> instead of being reparsed from text. That is the seam #1095 tracks.
/// </para>
///
/// <para>
/// The sanctioned way to build a global-qualified name is a real
/// <c>AliasQualifiedNameSyntax</c> via <c>RoslynEmitter.MakeGlobalQualifiedName</c> or the
/// <c>RoslynEmitter.ParseQualifiedName</c> / <c>ParseQualifiedTypeName</c> wrappers, which special-case
/// the <c>global::</c> prefix and pass plain names through unchanged. To close the dynamic-string
/// loophole for good (#1095), this scan bans <b>every</b> raw <c>ParseName(</c> / <c>ParseTypeName(</c>
/// call across <c>src/Sharpy.Compiler/CodeGen/</c> and <c>src/Sharpy.Compiler/Lowering/</c> — not only
/// the ones with a visible <c>global::</c> literal — so a name can no longer be built from an
/// unstructured string and then acquire a <c>global::</c> prefix out of a helper. The two wrapper
/// implementations in <c>RoslynEmitter.cs</c> are the only sanctioned raw calls; they opt out with an
/// explicit <c>conformance:allow-raw-parse</c> marker. Comments are stripped before scanning, so
/// doc-comments that name the pattern as historical context are not flagged.
/// </para>
///
/// <para>
/// <c>ParseExpression</c> and <c>IdentifierName</c> have no <c>ParseQualified*</c> counterpart, so for
/// those the scan continues to ban the <b>literal</b> <c>global::</c> form specifically. The remaining
/// non-<c>global::</c> reparse-equivalence classes (variadic <c>params</c> array rank specifiers and the
/// spread <c>.ToArray()</c> materialization) are handled directly in the emitter. The zero-parse handoff
/// is enabled separately; the compiler may still round-trip through <c>ParseText</c> on the
/// line-directive path.
/// </para>
/// </summary>
public class GlobalQualifiedNameConformanceTests
{
    /// <summary>
    /// Node-building calls banned in <c>CodeGen/</c> + <c>Lowering/</c>, matched against comment-stripped
    /// code. Any raw <c>ParseName(</c> / <c>ParseTypeName(</c> is banned outright — the
    /// <c>ParseQualifiedName</c> / <c>ParseQualifiedTypeName</c> wrappers (and <c>MakeGlobalQualifiedName</c>)
    /// are the only sanctioned entry, and the wrapper implementations themselves opt out via
    /// <see cref="WrapperAllowMarker"/>. <c>ParseExpression</c> / <c>IdentifierName</c> have no wrapper, so
    /// only their <c>global::</c> literal form is banned.
    /// </summary>
    private static readonly string[] BannedPatterns =
    {
        "ParseName(",
        "ParseTypeName(",
        "ParseExpression(\"global::",
        "IdentifierName(\"global::",
        "IdentifierName($\"global::",
    };

    /// <summary>
    /// File whose <see cref="WrapperAllowMarker"/>-tagged lines are exempt: the only place a raw
    /// <c>ParseName</c>/<c>ParseTypeName</c> may legitimately appear is inside the two wrapper bodies.
    /// </summary>
    private const string WrapperFileName = "RoslynEmitter.cs";

    /// <summary>
    /// Marker comment that exempts a line in <see cref="WrapperFileName"/> from the raw-call ban. Only the
    /// <c>ParseQualifiedName</c> / <c>ParseQualifiedTypeName</c> implementation lines carry it.
    /// </summary>
    private const string WrapperAllowMarker = "conformance:allow-raw-parse";

    [Fact]
    public void CodeGenAndLowering_BuildNoStringGlobalQualifiedNames()
    {
        var directories = new[]
        {
            FindSourceDirectory("CodeGen"),
            FindSourceDirectory("Lowering"),
        };

        var violations = new List<string>();

        foreach (var dir in directories)
        {
            Directory.Exists(dir).Should().BeTrue($"source directory should exist at {dir}");

            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                violations.AddRange(ScanFileLines(Path.GetFileName(file), File.ReadAllLines(file)));
            }
        }

        violations.Should().BeEmpty(
            "A qualified name must be built as syntax, not parsed from a string, or it is not "
            + "reparse-equivalent and blocks the zero-parse tree handoff (#1095). Build it with "
            + "RoslynEmitter.MakeGlobalQualifiedName, or route it through RoslynEmitter.ParseQualifiedName "
            + "/ ParseQualifiedTypeName (which produce a real AliasQualifiedNameSyntax on the leftmost "
            + "segment and pass plain names through unchanged).\nViolations:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// The ban is self-consistent: raw <c>ParseName</c>/<c>ParseTypeName</c> and the literal
    /// <c>global::</c> forms are flagged, the wrapper marker exempts only <see cref="WrapperFileName"/>,
    /// and a properly routed call passes. Guards against the scan silently going toothless.
    /// </summary>
    [Fact]
    public void Scan_FlagsRawCalls_AndHonorsOnlyTheWrapperMarker()
    {
        ScanFileLines("SomeEmitter.cs", new[] { "        return ParseName(fqn);" })
            .Should().ContainSingle().Which.Should().Contain("ParseName(");

        ScanFileLines("SomeEmitter.cs", new[] { "        var t = ParseTypeName(dotted);" })
            .Should().ContainSingle();

        // Literal global:: forms with no wrapper counterpart are still flagged.
        ScanFileLines("SomeEmitter.cs", new[] { "        var n = IdentifierName(\"global::Sharpy\");" })
            .Should().ContainSingle();

        // The sanctioned wrapper lines in RoslynEmitter.cs opt out via the marker.
        ScanFileLines(WrapperFileName, new[]
        {
            "            : ParseName(name); // conformance:allow-raw-parse — sanctioned wrapper",
        }).Should().BeEmpty();

        // The marker exempts only RoslynEmitter.cs, never an arbitrary file.
        ScanFileLines("Other.cs", new[] { "        return ParseName(x); // conformance:allow-raw-parse" })
            .Should().ContainSingle();

        // A properly routed call is not flagged.
        ScanFileLines("SomeEmitter.cs", new[] { "        return ParseQualifiedName(fqn);" })
            .Should().BeEmpty();
    }

    /// <summary>
    /// Applies <see cref="BannedPatterns"/> to one file's lines, honoring the
    /// <see cref="WrapperAllowMarker"/> exemption for <see cref="WrapperFileName"/>. Returns one entry per
    /// violation. Extracted so the ban logic itself is unit-testable (see the negative self-test).
    /// </summary>
    private static List<string> ScanFileLines(string fileName, IReadOnlyList<string> lines)
    {
        var violations = new List<string>();
        for (int i = 0; i < lines.Count; i++)
        {
            // The ParseQualifiedName/ParseQualifiedTypeName wrapper bodies are the one sanctioned place a
            // raw ParseName/ParseTypeName may appear; they opt out with an explicit marker.
            if (fileName == WrapperFileName && lines[i].Contains(WrapperAllowMarker, StringComparison.Ordinal))
                continue;

            var code = StripLineComment(lines[i]);
            foreach (var pattern in BannedPatterns)
            {
                if (code.Contains(pattern, StringComparison.Ordinal))
                    violations.Add($"{fileName}:{i + 1} [{pattern}] {lines[i].Trim()}");
            }
        }

        return violations;
    }

    /// <summary>
    /// Removes a single-line <c>//</c> comment so a banned pattern named only in a doc-comment
    /// (historical context) is not treated as a code reference. Same naive rule as
    /// <see cref="EmitterBannedTokenScanTests"/>.
    /// </summary>
    private static string StripLineComment(string line)
    {
        var idx = line.IndexOf("//", StringComparison.Ordinal);
        return idx >= 0 ? line.Substring(0, idx) : line;
    }

    private static string FindSourceDirectory(string leaf)
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", "Sharpy.Compiler", leaf);
            if (Directory.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Sharpy.Compiler", leaf));
    }
}
