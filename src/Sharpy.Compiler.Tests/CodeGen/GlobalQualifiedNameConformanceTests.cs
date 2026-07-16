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
/// <c>RoslynEmitter.ParseQualifiedName</c> / <c>ParseQualifiedTypeName</c> wrappers (which special-case
/// the <c>global::</c> prefix and pass plain names through unchanged). This scan bans the raw pattern
/// across <c>src/Sharpy.Compiler/CodeGen/</c> and <c>src/Sharpy.Compiler/Lowering/</c> so a new
/// string-built <c>global::</c> name cannot silently reintroduce the reparse gap. Comments are stripped
/// before scanning, so the doc-comments that name the pattern as historical context are not flagged.
/// </para>
///
/// <para>
/// <b>Scope.</b> This scan enforces only the statically-detectable <b>literal</b> form — a
/// <c>global::</c> string handed directly to <c>ParseName</c>/<c>ParseTypeName</c>/<c>IdentifierName</c>.
/// It deliberately does <b>not</b> flag <c>global::</c> strings built dynamically inside helper methods
/// (e.g. <c>$"global::{fullName}"</c>) that flow into <c>ParseName(variable)</c> elsewhere; those
/// residual dynamic sites (and the two non-<c>global::</c> reparse-equivalence classes that also block
/// the direct <c>CSharpSyntaxTree.Create</c> handoff) are tracked as the remaining scope of #1095. The
/// zero-parse handoff is not enabled; the compiler still round-trips through <c>ParseText</c>.
/// </para>
/// </summary>
public class GlobalQualifiedNameConformanceTests
{
    /// <summary>
    /// Raw node-building calls that take a <c>global::</c>-prefixed string literal. Matched against
    /// comment-stripped code. The sanctioned <c>ParseQualifiedName</c>/<c>ParseQualifiedTypeName</c>
    /// wrappers and <c>MakeGlobalQualifiedName</c> are not listed, so routing through them is allowed.
    /// </summary>
    private static readonly string[] BannedPatterns =
    {
        "ParseName(\"global::",
        "ParseTypeName(\"global::",
        "ParseExpression(\"global::",
        "IdentifierName(\"global::",
        "IdentifierName($\"global::",
    };

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
                var fileName = Path.GetFileName(file);
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var code = StripLineComment(lines[i]);
                    foreach (var pattern in BannedPatterns)
                    {
                        if (code.Contains(pattern, StringComparison.Ordinal))
                            violations.Add($"{fileName}:{i + 1} — string-built global:: name: {lines[i].Trim()}");
                    }
                }
            }
        }

        violations.Should().BeEmpty(
            "A global:: name built from a string literal is not reparse-equivalent and blocks the "
            + "zero-parse tree handoff (#1095). Build it with RoslynEmitter.MakeGlobalQualifiedName, or "
            + "parse it through RoslynEmitter.ParseQualifiedName / ParseQualifiedTypeName (which produce "
            + "a real AliasQualifiedNameSyntax on the leftmost segment and pass plain names through).\n"
            + "Violations:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// Removes a single-line <c>//</c> comment so a banned pattern named only in a doc-comment
    /// (historical context) is not treated as a code reference. Same naive rule as
    /// <see cref="EmitterPurityConformanceTests"/>.
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
