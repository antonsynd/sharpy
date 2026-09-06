using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Structural guard for the emitter's const-emission seam (#1791). Every <c>ConstKeyword</c> site
/// in <c>src/Sharpy.Compiler/CodeGen/</c> must be guarded by <c>IsCompileTimeConstant</c> -- the
/// semantic fact computed by <c>ConstEligibility</c> and materialized on <c>CodeGenInfo</c>. No
/// const decision may be keyed on <c>PredefinedTypeSyntax</c> (the deleted <c>IsConstEligibleType</c>).
///
/// <para><b>Contract.</b> 3 <c>ConstKeyword</c> sites, each under <c>IsCompileTimeConstant</c>;
/// 0 <c>PredefinedTypeSyntax</c>-keyed const decisions in CodeGen.</para>
/// </summary>
public class EmitterConstEmissionSeamConformanceTests
{
    /// <summary>
    /// ConstKeyword sites @ 86b4c32a8: class/struct field (1), local declaration (1),
    /// module-level field (1).
    /// </summary>
    private const int ConstKeywordSiteCount = 3;

    private static string GetCodeGenDirectory()
    {
        var repoRoot = DispatchSiteScan.FindRepoRoot();
        return Path.Combine(repoRoot, "src", "Sharpy.Compiler", "CodeGen");
    }

    private static IEnumerable<string> ReadCodeGenSources()
    {
        var dir = GetCodeGenDirectory();
        return Directory.EnumerateFiles(dir, "RoslynEmitter*.cs")
            .Select(File.ReadAllText);
    }

    [Fact]
    public void ConstKeywordSites_AreUnderIsCompileTimeConstant()
    {
        var constKeywordPattern = new Regex(
            @"Token\s*\(\s*SyntaxKind\s*\.\s*ConstKeyword\s*\)",
            RegexOptions.Compiled);

        var isCompileTimePattern = new Regex(
            @"IsCompileTimeConstant",
            RegexOptions.Compiled);

        var sites = new List<(string File, int Line)>();
        var unguarded = new List<(string File, int Line)>();

        foreach (var filePath in Directory.EnumerateFiles(GetCodeGenDirectory(), "RoslynEmitter*.cs"))
        {
            var lines = File.ReadAllLines(filePath);
            var fileName = Path.GetFileName(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                if (constKeywordPattern.IsMatch(lines[i]))
                {
                    sites.Add((fileName, i + 1));

                    // Check the surrounding context (20 lines above) for IsCompileTimeConstant
                    var start = Math.Max(0, i - 20);
                    var context = string.Join("\n", lines[start..(i + 1)]);
                    if (!isCompileTimePattern.IsMatch(context))
                    {
                        unguarded.Add((fileName, i + 1));
                    }
                }
            }
        }

        sites.Count.Should().Be(ConstKeywordSiteCount,
            $"the ConstKeyword site count is anchored to a literal. Sites: "
            + string.Join(", ", sites.Select(s => $"{s.File}:{s.Line}")));

        unguarded.Should().BeEmpty(
            "every ConstKeyword site in CodeGen must be guarded by IsCompileTimeConstant. "
            + $"Unguarded: {string.Join(", ", unguarded.Select(s => $"{s.File}:{s.Line}"))}");
    }

    [Fact]
    public void NoPredefinedTypeSyntaxKeyedConstDecisions()
    {
        // The deleted IsConstEligibleType checked `typeSyntax is PredefinedTypeSyntax` to decide
        // const emission. No method in CodeGen may use PredefinedTypeSyntax to gate a const decision.
        // We search for the pattern within 5 lines of any ConstKeyword mention.
        var violations = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(GetCodeGenDirectory(), "RoslynEmitter*.cs"))
        {
            var lines = File.ReadAllLines(filePath);
            var fileName = Path.GetFileName(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("ConstKeyword", StringComparison.Ordinal))
                    continue;

                // Check 10 lines above and below for PredefinedTypeSyntax
                var start = Math.Max(0, i - 10);
                var end = Math.Min(lines.Length, i + 10);
                for (int j = start; j < end; j++)
                {
                    if (lines[j].Contains("PredefinedTypeSyntax", StringComparison.Ordinal))
                    {
                        violations.Add($"{fileName}:{j + 1} (near ConstKeyword at line {i + 1})");
                    }
                }
            }
        }

        violations.Should().BeEmpty(
            "const decisions must use CodeGenInfo.IsCompileTimeConstant, not PredefinedTypeSyntax");
    }
}
