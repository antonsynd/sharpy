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
/// </summary>
public class DiagnosticCodeEmissionSiteTests
{
    private readonly ITestOutputHelper _output;

    public DiagnosticCodeEmissionSiteTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly Regex ActiveCodePattern = new(
        @"public\s+const\s+string\s+(\w+)\s*=\s*""(SPY\d{4})""\s*;\s*//\s*Active",
        RegexOptions.Compiled);

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

        Assert.True(activeCodes.Count > 100,
            $"Expected >100 Active codes, found {activeCodes.Count} — is the regex matching?");

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
                if (content.Contains(fieldName, StringComparison.Ordinal)
                    || content.Contains(code, StringComparison.Ordinal))
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
            $"{missing.Count} Active diagnostic code(s) have no emission site outside Diagnostics/. " +
            "Either emit the code somewhere, or change its status to Reserved/Retired:\n  " +
            string.Join("\n  ", missing));
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
