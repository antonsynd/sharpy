using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// Backstop source scan (P47, #2232, plan Phase 2 Task 1): no code-action source under
/// <c>src/Sharpy.Lsp/Refactoring/</c> splits lines or writes a line break of its own — every provider reads
/// the ONE line table (<c>SourceLines</c>, over <c>LineDiff.Split</c>) and writes the document's break.
/// It is a BACKSTOP, not evidence: the code-action shape sweep judges applied text; this only lists the five
/// spellings a provider that bypassed the seam would most likely use.
///
/// <para>Arms. Four are substrings of a line's CODE — its tokens concatenated without trivia, so a comment
/// never matches and <c>Split( '\n' )</c> matches like <c>Split('\n')</c>: <c>Split('\n')</c>,
/// <c>.AppendLine(</c> (the platform newline), <c>Environment.NewLine</c>, <c>TrimEnd('\r')</c>. The fifth,
/// <see cref="LineBreakLiteral"/>, is a string, character, interpolated-text or raw-string literal whose
/// VALUE contains <c>\n</c> or <c>\r</c> — a line terminator spelled in text a provider emits (<c>"\n"</c>,
/// <c>callSite + "\n"</c>, <c>$"…\n"</c>, <c>Append('\n')</c>) — with ONE exemption, by position: the search
/// argument (the first) of a <c>.Replace(…)</c> call, which matches a line break inside a string literal's
/// value to escape it (<c>ConvertFormsProvider.EscapeStringContent</c>) and is never written as a line
/// break.</para>
/// </summary>
public class RefactoringLineTableScanTests
{
    /// <summary>The fifth arm's name: a line-break character in a literal's value (see the class doc).</summary>
    public const string LineBreakLiteral = "line-break literal";

    /// <summary>
    /// The scan's root, anchored to a literal: the eleven Refactoring sources @ 7f0502de3 plus
    /// <c>SourceRegion.cs</c> (P47 Task 1) and <c>ImportNames.cs</c> (P47 Task 3). A scan of the wrong
    /// directory, or of a directory a file left, fails here instead of passing vacuously.
    /// </summary>
    private const int ExpectedSourceFileCount = 13;

    public static TheoryData<string> Arms => new()
    {
        "Split('\\n')",
        ".AppendLine(",
        "Environment.NewLine",
        "TrimEnd('\\r')",
        LineBreakLiteral,
    };

    [Fact]
    public void Scan_ReadsEveryRefactoringSource()
    {
        var files = RefactoringSources();

        files.Select(Path.GetFileName).Should().Contain(new[] { "SourceRegion.cs", "ConvertFormsProvider.cs", "OrganizeImportsProvider.cs" });
        files.Should().HaveCount(ExpectedSourceFileCount,
            "the anchor is the Refactoring directory's file count; a new file there changes it in the commit that adds it");
    }

    [Theory]
    [MemberData(nameof(Arms))]
    public void NoRefactoringSource_SplitsLinesOrWritesALineBreakOfItsOwn(string arm)
    {
        var offenders = new SCG.List<string>();
        foreach (var file in RefactoringSources())
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            var name = Path.GetFileName(file);
            offenders.AddRange(arm == LineBreakLiteral
                ? LineBreakLiteralOffenders(root, name)
                : CodeSubstringOffenders(root, name, arm));
        }

        offenders.Should().BeEmpty(
            $"every code-action provider reads SourceLines and writes its DocumentLineBreak (P47, #2232); arm '{arm}' found");
    }

    /// <summary>Lines whose code (tokens without trivia, concatenated) contains <paramref name="needle"/>.</summary>
    private static IEnumerable<string> CodeSubstringOffenders(SyntaxNode root, string file, string needle)
    {
        var lines = new SortedDictionary<int, System.Text.StringBuilder>();
        foreach (var token in root.DescendantTokens())
        {
            var line = token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            if (!lines.TryGetValue(line, out var code))
                lines[line] = code = new System.Text.StringBuilder();
            code.Append(token.Text);
        }

        foreach (var (line, code) in lines)
        {
            if (code.ToString().Contains(needle, StringComparison.Ordinal))
                yield return $"{file}:{line}: {code}";
        }
    }

    /// <summary>Literals whose value holds a line-break character, minus a <c>.Replace</c> search argument.</summary>
    private static IEnumerable<string> LineBreakLiteralOffenders(SyntaxNode root, string file)
    {
        foreach (var token in root.DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.StringLiteralToken)
                && !token.IsKind(SyntaxKind.CharacterLiteralToken)
                && !token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                && !token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
                && !token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
                && !token.IsKind(SyntaxKind.Utf8StringLiteralToken))
            {
                continue;
            }

            if (token.ValueText.IndexOfAny(new[] { '\n', '\r' }) < 0 || IsReplaceSearchArgument(token))
                continue;

            var line = token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return $"{file}:{line}: {token.Text}";
        }
    }

    private static bool IsReplaceSearchArgument(SyntaxToken token) =>
        token.Parent is LiteralExpressionSyntax { Parent: ArgumentSyntax argument }
        && argument.Parent is ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } arguments
        && arguments.Arguments.IndexOf(argument) == 0
        && invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Replace" };

    private static SCG.List<string> RefactoringSources()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "src", "Sharpy.Lsp", "Refactoring");
            if (Directory.Exists(candidate))
                return Directory.EnumerateFiles(candidate, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.Ordinal).ToList();

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not locate src/Sharpy.Lsp/Refactoring. This scan is only meaningful against real sources: a "
            + "walk of the wrong directory passes vacuously.");
    }
}
