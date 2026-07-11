using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Conformance test enforcing the "emitter is a pure translator" rule (#1039, #1041).
///
/// <para>
/// Code generation must make no type or lowering decisions of its own: all such decisions are made
/// during semantic analysis and materialized onto <c>SemanticInfo</c>/<c>CodeGenInfo</c>, which the
/// emitter only reads. Two concrete bans enforce this by scanning every source file under
/// <c>src/Sharpy.Compiler/CodeGen/</c>:
/// </para>
///
/// <list type="number">
///   <item><description>
///     No reference to <c>TypeInferenceService</c> or <c>GenericTypeInferenceService</c> — type
///     inference belongs to the semantic phase; the emitter reads materialized types.
///   </description></item>
///   <item><description>
///     No reflection (<c>System.Reflection</c> namespace, <c>BindingFlags</c>,
///     <c>GetCustomAttribute</c>, <c>GetIndexParameters</c>) — CLR inspection belongs to
///     <c>Discovery</c>/semantic (e.g. <c>Discovery.ClrTypeBridge</c>,
///     <c>Discovery.ClrTypeHelper</c>), materialized for the emitter.
///   </description></item>
/// </list>
///
/// <para>
/// The emitter's sanctioned API is Roslyn's <c>SyntaxFactory</c> (the
/// <c>Microsoft.CodeAnalysis.CSharp</c> namespaces), which is explicitly permitted — none of the
/// banned tokens occur in <c>SyntaxFactory</c>-based construction, so no per-token allowlist is
/// needed. Comments are stripped before scanning so a doc-comment that merely mentions a banned
/// type by name (as historical context) is not a violation; only real code references are flagged.
/// </para>
/// </summary>
public class EmitterPurityConformanceTests
{
    /// <summary>Substrings banned from CodeGen source (matched against comment-stripped code).</summary>
    private static readonly string[] BannedTokens =
    {
        // Inference engines — decisions belong in semantic analysis.
        "TypeInferenceService",       // also matches GenericTypeInferenceService (superstring)
        // Reflection — CLR inspection belongs in Discovery/semantic.
        "System.Reflection",
        "BindingFlags",
        "GetCustomAttribute",
        "GetIndexParameters",
    };

    [Fact]
    public void CodeGenSources_MakeNoInferenceOrReflectionReferences()
    {
        var codeGenDir = FindCodeGenSourceDirectory();
        Directory.Exists(codeGenDir).Should().BeTrue(
            $"CodeGen source directory should exist at {codeGenDir}");

        var files = Directory.GetFiles(codeGenDir, "*.cs", SearchOption.AllDirectories);
        files.Should().NotBeEmpty("Should find CodeGen source files");

        var violations = new List<string>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var code = StripLineComment(lines[i]);
                foreach (var token in BannedTokens)
                {
                    if (code.Contains(token, StringComparison.Ordinal))
                        violations.Add($"{fileName}:{i + 1} — references banned '{token}': {lines[i].Trim()}");
                }
            }
        }

        violations.Should().BeEmpty(
            "The emitter is a pure translator: it must make no type/lowering decisions and perform no " +
            "reflection. Move the decision into semantic analysis and materialize it onto SemanticInfo/" +
            "CodeGenInfo (and, for a new SemanticInfo dictionary, add it to SemanticInfo.MergeFrom so it " +
            "survives the per-file → project merge codegen reads from).\nViolations:\n" +
            string.Join("\n", violations));
    }

    /// <summary>
    /// Removes a single-line <c>//</c> comment from a line so that a banned type named only in a
    /// comment (historical context) is not treated as a code reference. Naive: does not attempt to
    /// honor <c>//</c> inside string literals — a banned token inside a CodeGen string literal would
    /// itself be suspicious and is intentionally still flagged.
    /// </summary>
    private static string StripLineComment(string line)
    {
        var idx = line.IndexOf("//", StringComparison.Ordinal);
        return idx >= 0 ? line.Substring(0, idx) : line;
    }

    private static string FindCodeGenSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var codeGenPath = Path.Combine(current, "src", "Sharpy.Compiler", "CodeGen");
            if (Directory.Exists(codeGenPath))
                return codeGenPath;
            current = Directory.GetParent(current)?.FullName;
        }

        // Fallback: relative from the test assembly location.
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Sharpy.Compiler", "CodeGen"));
    }
}
