using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Scan guard: no diagnostic message interpolates a raw <c>SemanticType</c> (#1512).
///
/// <para>
/// A <c>SemanticType</c> is a C# record, so <c>$"... '{resolvedType}' ..."</c> renders the
/// generated <c>ToString()</c> — for a <c>UserDefinedType</c> that is a multi-kilobyte
/// <c>TypeSymbol</c> dump, which is what made every SPY0373 unreadable (#1512). Types in
/// user-facing text must flow through <c>GetDisplayName()</c>.
/// </para>
///
/// <para>
/// <b>How it scans.</b> Every interpolation hole in every source file under
/// <c>src/Sharpy.Compiler/Semantic/</c> (including <c>Validation/</c>) is inspected with Roslyn.
/// A hole is flagged when its expression is (a) a bare identifier whose name ends in
/// <c>Type</c>/<c>type</c> (<c>resolvedType</c>, <c>constType</c>, <c>scrutineeType</c> — the
/// naming convention for <c>SemanticType</c> locals), or (b) a member access ending in
/// <c>.Type</c> (<c>derivedPropSymbol.Type</c>). Wrapping the expression —
/// <c>{resolvedType.GetDisplayName()}</c> — changes the hole's shape and passes. Known
/// string-typed locals that merely follow the naming convention are allowlisted below.
/// </para>
///
/// <para>
/// <b>Named limitation</b> (the <c>NameExtentReconstructionScanTests</c> doctrine): a
/// <c>SemanticType</c> local whose name does not end in <c>Type</c> (<c>{lhs}</c>, <c>{elem}</c>)
/// is invisible to this scan. It is a tripwire for the dominant naming convention, not a proof;
/// the four #1512 sites all carried the convention.
/// </para>
/// </summary>
public class DiagnosticInterpolationScanTests
{
    /// <summary>
    /// Interpolation holes that match the scan patterns but are verified NOT to be
    /// <c>SemanticType</c>-typed: keyed <c>fileName::holeExpression</c>. Every entry must cite
    /// why it is safe. Delete an entry if the site is removed — a stale entry fails the scan.
    /// </summary>
    private static readonly Dictionary<string, string> Allowlist = new(StringComparer.Ordinal)
    {
        // string local naming the enclosing block kind ("try"/"with"), not a SemanticType.
        ["TypeChecker.Expressions.cs::blockType"] = "string local (block-kind word)",
        // string locals pre-rendered via GetDisplayName() at the call site that builds them.
        ["SignatureValidator.cs::expectedReturnType"] = "string local (pre-rendered display name)",
        ["SignatureValidator.cs::actualReturnType"] = "string local (pre-rendered display name)",
    };

    [Fact]
    public void SemanticDiagnostics_InterpolateNoRawSemanticType()
    {
        var semanticDir = FindSemanticSourceDirectory();
        Directory.Exists(semanticDir).Should().BeTrue(
            $"Semantic source directory should exist at {semanticDir}");

        var files = Directory.GetFiles(semanticDir, "*.cs", SearchOption.AllDirectories);
        files.Should().NotBeEmpty("Should find Semantic source files");

        var violations = new List<string>();
        var matchedAllowlistKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();

            foreach (var hole in root.DescendantNodes().OfType<InterpolationSyntax>())
            {
                var suspect = hole.Expression switch
                {
                    IdentifierNameSyntax id when EndsInType(id.Identifier.Text) => id.Identifier.Text,
                    MemberAccessExpressionSyntax { Name.Identifier.Text: "Type" } ma => ma.ToString(),
                    _ => null,
                };
                if (suspect == null)
                    continue;

                var key = $"{fileName}::{suspect}";
                if (Allowlist.ContainsKey(key))
                {
                    matchedAllowlistKeys.Add(key);
                    continue;
                }

                var line = hole.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add($"{fileName}:{line} — hole '{{{suspect}}}' interpolates a suspected raw SemanticType");
            }
        }

        violations.Should().BeEmpty(
            "diagnostic text must render types via GetDisplayName(), never a raw SemanticType — " +
            "the record ToString() prints a multi-KB TypeSymbol dump (#1512). Wrap the hole as " +
            "{x.GetDisplayName()}, or — if the flagged expression is genuinely not a SemanticType — " +
            "add an allowlist entry citing its actual type.\nViolations:\n" +
            string.Join("\n", violations));

        var stale = Allowlist.Keys.Except(matchedAllowlistKeys, StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
        stale.Should().BeEmpty(
            "allowlist entries no longer matched by any interpolation hole — whatever removed the " +
            "site must also delete the entry (drain on fix):\n" + string.Join("\n", stale));
    }

    private static bool EndsInType(string identifier)
        => identifier.EndsWith("Type", StringComparison.Ordinal)
           || identifier.EndsWith("type", StringComparison.Ordinal);

    /// <summary>The <c>src/Sharpy.Compiler/Semantic/</c> directory (includes <c>Validation/</c>).</summary>
    private static string FindSemanticSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var semanticPath = Path.Combine(current, "src", "Sharpy.Compiler", "Semantic");
            if (Directory.Exists(semanticPath))
                return semanticPath;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Sharpy.Compiler", "Semantic"));
    }
}
