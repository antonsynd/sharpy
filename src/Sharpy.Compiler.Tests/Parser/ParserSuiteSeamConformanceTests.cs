using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Guards the suite-seam consolidation (#1736): every <c>Expect(TokenType.Dedent)</c> in the
/// parser must live inside one of the two suite helpers -- <c>ParseIndentedSuite</c> or
/// <c>CloseSuite</c> -- and those helpers must have exactly the measured census of call sites
/// across the parser.
/// A raw <c>Expect(Dedent)</c> outside the helpers means a suite whose end position is still
/// keyed on the Dedent token rather than the last body statement, which causes folding ranges,
/// selection ranges, and document symbols to extend one line past the suite's content.
/// </summary>
public class ParserSuiteSeamConformanceTests
{
    private static readonly HashSet<string> SuiteHelpers = new(StringComparer.Ordinal)
    {
        "ParseIndentedSuite",
        "CloseSuite",
    };

    [Fact]
    public void ZeroRawExpectDedent_OutsideSuiteHelpers()
    {
        var violations = new List<string>();

        foreach (var (fileName, root) in ParserSyntaxTrees())
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not IdentifierNameSyntax { Identifier.ValueText: "Expect" })
                    continue;
                if (invocation.ArgumentList.Arguments.Count != 1)
                    continue;
                if (invocation.ArgumentList.Arguments[0].ToString() != "TokenType.Dedent")
                    continue;

                var method = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                var memberName = method?.Identifier.ValueText ?? "<unknown>";

                if (SuiteHelpers.Contains(memberName))
                    continue;

                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add($"{fileName} :: {memberName}  [{fileName}:{line}]");
            }
        }

        violations.Should().BeEmpty(
            "every Expect(TokenType.Dedent) must live inside a suite helper (ParseIndentedSuite or "
            + "CloseSuite). A raw Dedent expectation means the suite's end position is keyed on the "
            + "Dedent token, not the last body statement (#1736).\nViolations:\n"
            + string.Join("\n", violations));
    }

    /// <summary>
    /// The measured census of suite-helper call sites in the parser, counted at fb728b9be by this
    /// very scan. Anchored to a LITERAL rather than a lower bound: a bound cannot detect a site
    /// drifting upward, and a new Indent/Dedent suite that bypasses the helpers would sit under it
    /// unnoticed. A change to this number is a change to the seam's reach -- re-measure, name the
    /// site, and update the literal in the same commit.
    /// </summary>
    private const int SuiteHelperCallSiteCensus = 26;

    [Fact]
    public void SuiteHelperCallSites_MatchTheMeasuredCensus()
    {
        var callSites = new List<string>();

        foreach (var (fileName, root) in ParserSyntaxTrees())
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string? helperName = invocation.Expression switch
                {
                    IdentifierNameSyntax id when SuiteHelpers.Contains(id.Identifier.ValueText)
                        => id.Identifier.ValueText,
                    _ => null
                };
                if (helperName == null)
                    continue;

                var enclosingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (enclosingMethod != null && SuiteHelpers.Contains(enclosingMethod.Identifier.ValueText))
                    continue;

                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                callSites.Add($"{fileName}:{line} -> {helperName}");
            }
        }

        callSites.Should().HaveCount(SuiteHelperCallSiteCensus,
            "every Indent/Dedent suite site in the parser routes through ParseIndentedSuite or "
            + $"CloseSuite, and there are exactly {SuiteHelperCallSiteCensus} of them (#1736). "
            + "Fewer means a suite left the seam; more means a new suite arrived and its extent "
            + "behaviour is unmeasured.\nSites found:\n"
            + string.Join("\n", callSites));
    }

    private static IEnumerable<(string FileName, CompilationUnitSyntax Root)> ParserSyntaxTrees()
    {
        var dir = Path.Combine(FindSourceDir("Sharpy.Compiler"), "Parser");
        Directory.Exists(dir).Should().BeTrue($"the parser source directory must be locatable (looked in {dir})");

        var files = Directory.GetFiles(dir, "Parser*.cs", SearchOption.TopDirectoryOnly);
        files.Should().NotBeEmpty("the scan is vacuous if it finds no parser sources");

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            yield return (Path.GetFileName(file), (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text).GetRoot());
        }
    }

    private static string FindSourceDir(string project)
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", project);
            if (Directory.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", project));
    }
}
