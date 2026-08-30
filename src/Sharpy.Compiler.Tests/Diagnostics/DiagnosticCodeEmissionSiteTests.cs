using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Every diagnostic code marked <c>// Active</c> in <c>DiagnosticCodes.cs</c> is referenced
/// from at least one non-test source file outside the <c>Diagnostics/</c> directory. A code
/// that is Active but never emitted is dead weight in the explanations table and misleading
/// in the CLI's <c>explain</c> output — remove it or mark it Reserved/Retired.
///
/// <para>
/// Codes whose status comment is <c>Retired</c> or <c>Reserved</c> are accepted without an
/// emission site. The status is read from the source comment, not from the field name, so a
/// field named <c>XyzRetired</c> with an <c>// Active</c> tag is still enforced.
/// </para>
///
/// <para>
/// Every <c>SPY\d{4}</c> const must carry exactly one of <c>Active</c>, <c>Reserved</c>, or
/// <c>Retired</c> in its trailing comment. No <c>Deprecated</c> — fold into <c>Retired</c>
/// with a note. No bare (unlabeled) codes.
/// </para>
/// </summary>
public class DiagnosticCodeEmissionSiteTests
{
    private readonly ITestOutputHelper _output;

    public DiagnosticCodeEmissionSiteTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly Regex CodeConstPattern = new(
        @"public\s+const\s+string\s+(\w+)\s*=\s*""(SPY\d{4})""\s*;(.*)",
        RegexOptions.Compiled);

    private static readonly Regex ActiveCodePattern = new(
        @"public\s+const\s+string\s+(\w+)\s*=\s*""(SPY\d{4})""\s*;\s*//\s*Active",
        RegexOptions.Compiled);

    private static readonly Regex SingleLineComment = new(
        @"//[^\n]*", RegexOptions.Compiled);

    private static readonly Regex MultiLineComment = new(
        @"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Every SPY code const carries exactly one of Active / Reserved / Retired.
    /// No Deprecated (fold into Retired), no bare unlabeled codes.
    /// </summary>
    [Fact]
    public void EveryDiagnosticCode_CarriesExactlyOneStatusLabel()
    {
        var compilerDir = FindCompilerSourceDirectory();
        var codesFile = Path.Combine(compilerDir, "Diagnostics", "DiagnosticCodes.cs");
        Assert.True(File.Exists(codesFile), $"DiagnosticCodes.cs not found at {codesFile}");

        var violations = new List<string>();
        var active = 0;
        var reserved = 0;
        var retired = 0;

        foreach (var line in File.ReadAllLines(codesFile))
        {
            var match = CodeConstPattern.Match(line);
            if (!match.Success)
                continue;

            var fieldName = match.Groups[1].Value;
            var code = match.Groups[2].Value;
            var trailing = match.Groups[3].Value;

            var hasActive = trailing.Contains("Active", StringComparison.Ordinal);
            var hasReserved = trailing.Contains("Reserved", StringComparison.Ordinal);
            var hasRetired = trailing.Contains("Retired", StringComparison.Ordinal);
            var hasDeprecated = trailing.Contains("Deprecated", StringComparison.Ordinal);

            var labelCount = (hasActive ? 1 : 0) + (hasReserved ? 1 : 0) + (hasRetired ? 1 : 0);

            if (hasDeprecated && !hasRetired)
            {
                violations.Add($"{code} ({fieldName}): uses Deprecated — fold into Retired");
            }
            else if (labelCount == 0)
            {
                violations.Add($"{code} ({fieldName}): no status label (need Active, Reserved, or Retired)");
            }
            else if (labelCount > 1)
            {
                violations.Add($"{code} ({fieldName}): multiple status labels");
            }

            if (hasActive)
                active++;
            if (hasReserved)
                reserved++;
            if (hasRetired)
                retired++;
        }

        _output.WriteLine($"Census: {active + reserved + retired} total = {active} Active + {reserved} Reserved + {retired} Retired");

        Assert.True(violations.Count == 0,
            $"{violations.Count} diagnostic code(s) violate the one-label rule:\n  " +
            string.Join("\n  ", violations));
    }

    [Fact]
    public void EveryActiveDiagnosticCode_HasAnEmissionSite()
    {
        var compilerDir = FindCompilerSourceDirectory();
        Assert.True(Directory.Exists(compilerDir),
            $"Compiler source directory should exist at {compilerDir}");

        var codesFile = Path.Combine(compilerDir, "Diagnostics", "DiagnosticCodes.cs");
        Assert.True(File.Exists(codesFile), $"DiagnosticCodes.cs not found at {codesFile}");

        var activeCodes = new List<(string FieldName, string Code)>();
        foreach (var line in File.ReadAllLines(codesFile))
        {
            var match = ActiveCodePattern.Match(line);
            if (match.Success)
                activeCodes.Add((match.Groups[1].Value, match.Groups[2].Value));
        }

        Assert.True(activeCodes.Count >= 374,
            $"Expected ≥374 Active codes, found {activeCodes.Count} — is the regex matching? " +
            "If codes were retired/reserved, lower this tripwire.");

        var diagnosticsDir = Path.Combine(compilerDir, "Diagnostics");
        var sourceFiles = Directory.GetFiles(compilerDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(diagnosticsDir, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var missing = new List<string>();

        foreach (var (fieldName, code) in activeCodes)
        {
            var found = false;
            foreach (var file in sourceFiles)
            {
                var content = File.ReadAllText(file);
                var stripped = StripComments(content);

                if (stripped.Contains(fieldName, StringComparison.Ordinal)
                    || stripped.Contains(code, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                missing.Add($"{code} ({fieldName})");
        }

        _output.WriteLine($"Active codes: {activeCodes.Count}  Missing emission sites: {missing.Count}");

        Assert.True(missing.Count == 0,
            $"{missing.Count} Active diagnostic code(s) have no emission site outside Diagnostics/ " +
            "(comment-only references don't count). " +
            "Either emit the code somewhere, or change its status to Reserved/Retired:\n  " +
            string.Join("\n  ", missing));
    }

    private static string StripComments(string source)
    {
        var result = MultiLineComment.Replace(source, " ");
        result = SingleLineComment.Replace(result, " ");
        return result;
    }

    private static string FindCompilerSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var candidate = Path.Combine(current, "src", "Sharpy.Compiler");
            if (Directory.Exists(candidate))
                return candidate;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, "src", "Sharpy.Compiler");
    }
}
