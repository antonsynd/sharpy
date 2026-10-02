using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser;
using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// No contextual-keyword spelling outside <see cref="ContextualKeywords"/> (#2166, plan-be89c1 Task 7).
/// Every contextual site reads its keyword through <c>IsContextualKeyword(token, ContextualKeywords.K)</c>
/// or <c>IsPlaceholder(expr)</c>, which honour a backtick escape; a raw <c>Current.Value == "when"</c>,
/// <c>case "get":</c> or <c>Identifier { Name: "_" }</c> is the escape-blind shape the fix removed from
/// fourteen sites. This scan keeps a fifteenth from appearing.
///
/// <para><b>What it scans.</b> A Roslyn walk over every <c>src/Sharpy.Compiler/Parser</c> source file
/// (including <c>Ast/</c>) except the roster file itself, reporting every string literal whose value
/// is a roster word. Comments are trivia, so prose such as <c>Identifier("_")</c> in a doc comment is
/// not a hit (the controls below prove both directions). There is no exemption: the AST dumper spells
/// the observer keywords through the roster too.</para>
/// </summary>
public class ContextualKeywordPredicateConformanceTests
{
    private const string RosterFile = "Parser.ContextualKeywords.cs";

    private static string ParserDirectory => Path.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Compiler", "Parser");

    /// <summary>The string literals in <paramref name="source"/> whose value is a contextual keyword, with their 1-based lines.</summary>
    internal static IReadOnlyList<(int Line, string Word)> RosterLiterals(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var hits = new List<(int, string)>();
        foreach (var token in root.DescendantTokens())
        {
            var text = token.Kind() switch
            {
                SyntaxKind.StringLiteralToken
                    or SyntaxKind.Utf8StringLiteralToken
                    or SyntaxKind.SingleLineRawStringLiteralToken
                    or SyntaxKind.MultiLineRawStringLiteralToken => token.ValueText,
                // `$"get"`: an interpolated string that is one text segment spells the word too.
                SyntaxKind.InterpolatedStringTextToken when token.Parent?.Parent is InterpolatedStringExpressionSyntax { Contents.Count: 1 } => token.ValueText,
                _ => null,
            };
            if (text != null && ContextualKeywords.All.Contains(text))
                hits.Add((token.GetLocation().GetLineSpan().StartLinePosition.Line + 1, text));
        }

        return hits;
    }

    [Fact]
    public void NoParserSource_SpellsAContextualKeyword_OutsideTheRoster()
    {
        var files = Directory.GetFiles(ParserDirectory, "*.cs", SearchOption.AllDirectories);
        files.Select(Path.GetFileName).Should().Contain(RosterFile, "the roster file is where the spellings live");
        files.Length.Should().BeGreaterThan(8, "the scan must see the parser partials and the AST files, not an empty directory");

        var offenders = files
            .Where(f => Path.GetFileName(f) != RosterFile)
            .SelectMany(f => RosterLiterals(File.ReadAllText(f))
                .Select(h => $"{Path.GetRelativePath(ParserDirectory, f)}:{h.Line}: \"{h.Word}\""))
            .ToList();

        offenders.Should().BeEmpty(
            "a contextual site reads its keyword through IsContextualKeyword/IsPlaceholder and a "
            + "spelling through ContextualKeywords.K (#2166); offenders: " + string.Join("; ", offenders));
    }

    /// <summary>
    /// Positive control on a real file: the roster file spells every one of the 13 words, and the
    /// scan sees each — a scan that could not read the parser's own sources would pass vacuously.
    /// </summary>
    [Fact]
    public void PositiveControl_TheRosterFile_SpellsEveryWord()
    {
        var hits = RosterLiterals(File.ReadAllText(Path.Combine(ParserDirectory, RosterFile)));

        hits.Select(h => h.Word).Distinct().Should().BeEquivalentTo(new[]
        {
            "_", "get", "set", "init", "add", "remove", "before_set", "after_set", "when", "out", "ref", "notnull", "new",
        });
    }

    [Theory]
    [InlineData("class P { bool M(Token t) => t.Value == \"when\"; }", "when")]
    [InlineData("class P { void M(string v) { switch (v) { case \"get\": break; } } }", "get")]
    [InlineData("class P { bool M(object e) => e is Identifier { Name: \"_\" }; }", "_")]
    [InlineData("class P { string M() => $\"out\"; }", "out")]
    [InlineData("class P { string U = \"http://x\"; bool M(string v) => v == \"ref\"; }", "ref")]
    public void PositiveControl_ACodeSpelling_IsReported(string source, string word)
        => RosterLiterals(source).Select(h => h.Word).Should().Equal(word);

    [Theory]
    [InlineData("class P { // Current.Value == \"when\"\n}")]
    [InlineData("class P {\n    /// Checks for an Identifier(\"_\") placeholder.\n    void M() { }\n}")]
    [InlineData("class P { /* case \"get\": */ }")]
    [InlineData("class P { string M() => \"Expected 'before_set' or 'after_set'\"; }")]
    [InlineData("class P { string M(string v) => $\"got '{v}' when\"; }")]
    public void NegativeControl_CommentsAndLargerStrings_AreNotSpellings(string source)
        => RosterLiterals(source).Should().BeEmpty();
}
