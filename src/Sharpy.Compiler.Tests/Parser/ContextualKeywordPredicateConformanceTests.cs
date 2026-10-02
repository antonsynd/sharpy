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

    // ---- The comparison shape, independent of the word (verifier finding on plan-be89c1) --------
    //
    // The literal scan keys on the 13 roster words, so it cannot see (a) a NEW soft keyword read
    // escape-blind (`Current.Value == "where"`) or (b) an escape-blind read of a roster word spelled
    // through the roster (`Current.Value == ContextualKeywords.When`). Since #2166 the parser compares
    // a token's text in exactly one place — IsContextualKeyword in the roster file — so the shape
    // itself is the subject: outside that file no token `.Value` is compared to anything but null,
    // switched on, or matched against a constant; and no ContextualKeywords member is a comparison
    // operand, case label or constant pattern (that covers an AST `.Name == ContextualKeywords.K`).
    // A legitimate new contextual read goes through IsContextualKeyword and adds a roster word.

    private static bool IsValueRead(ExpressionSyntax e)
        => e is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Value" }
            or MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Value" };

    // `x is ContextualKeywords.Get` parses as a type test whose right side is a QualifiedName, so the
    // roster member is matched in both spellings.
    private static bool IsRosterMember(ExpressionSyntax e)
        => (e is MemberAccessExpressionSyntax { Expression: var lhs } ? lhs : e is QualifiedNameSyntax { Left: var left } ? left : null)
            ?.ToString() is "ContextualKeywords" or "Parser.ContextualKeywords" or "Sharpy.Compiler.Parser.ContextualKeywords";

    private static bool IsNullLiteral(ExpressionSyntax e) => e.IsKind(SyntaxKind.NullLiteralExpression);

    private static bool HasNonNullConstant(PatternSyntax p)
        => p.DescendantNodesAndSelf().OfType<ConstantPatternSyntax>().Any(c => !IsNullLiteral(c.Expression));

    /// <summary>Escape-blind comparison shapes in <paramref name="source"/>, with their 1-based lines.</summary>
    internal static IReadOnlyList<(int Line, string Text)> EscapeBlindComparisons(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var hits = new List<(int, string)>();
        void Hit(SyntaxNode n) => hits.Add((n.GetLocation().GetLineSpan().StartLinePosition.Line + 1, n.ToString()));

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.EqualsExpression) || b.IsKind(SyntaxKind.NotEqualsExpression):
                    if ((IsValueRead(b.Left) && !IsNullLiteral(b.Right)) || (IsValueRead(b.Right) && !IsNullLiteral(b.Left))
                        || IsRosterMember(b.Left) || IsRosterMember(b.Right))
                        Hit(b);
                    break;
                case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.IsExpression) && IsRosterMember(b.Right):
                    Hit(b);
                    break;
                case SwitchStatementSyntax s when IsValueRead(s.Expression):
                    Hit(s.Expression);
                    break;
                case SwitchExpressionSyntax s when IsValueRead(s.GoverningExpression):
                    Hit(s.GoverningExpression);
                    break;
                case IsPatternExpressionSyntax ip when IsValueRead(ip.Expression) && HasNonNullConstant(ip.Pattern):
                    Hit(ip);
                    break;
                case SubpatternSyntax { ExpressionColon: { } ec } sp when ec.Expression.ToString() == "Value" && HasNonNullConstant(sp.Pattern):
                    Hit(sp);
                    break;
                case ConstantPatternSyntax c when IsRosterMember(c.Expression):
                    Hit(c);
                    break;
                case CaseSwitchLabelSyntax l when IsRosterMember(l.Value):
                    Hit(l);
                    break;
                case InvocationExpressionSyntax inv
                    when inv.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Equals" } ma
                        && (IsValueRead(ma.Expression) || inv.ArgumentList.Arguments.Any(a => IsValueRead(a.Expression) || IsRosterMember(a.Expression))):
                    Hit(inv);
                    break;
            }
        }

        return hits;
    }

    [Fact]
    public void NoParserSource_ComparesATokenValue_OutsideTheRoster()
    {
        var files = Directory.GetFiles(ParserDirectory, "*.cs", SearchOption.AllDirectories);
        files.Length.Should().BeGreaterThan(8, "the scan must see the parser partials and the AST files, not an empty directory");

        var offenders = files
            .Where(f => Path.GetFileName(f) != RosterFile)
            .SelectMany(f => EscapeBlindComparisons(File.ReadAllText(f))
                .Select(h => $"{Path.GetRelativePath(ParserDirectory, f)}:{h.Line}: {h.Text}"))
            .ToList();

        offenders.Should().BeEmpty(
            "a token's text is compared only by IsContextualKeyword, which honours the escape (#2166); "
            + "a new soft keyword is a roster word read through it; offenders: " + string.Join("; ", offenders));
    }

    /// <summary>Positive control on a real file: the roster file's own predicate is the one comparison the scan must see.</summary>
    [Fact]
    public void PositiveControl_TheRosterPredicate_IsTheComparison()
        => EscapeBlindComparisons(File.ReadAllText(Path.Combine(ParserDirectory, RosterFile)))
            .Select(h => h.Text).Should().ContainSingle(t => t.Contains("token.Value == keyword"));

    [Theory]
    [InlineData("class P { bool M(Token t) => t.Value == \"where\"; }")]
    [InlineData("class P { bool M() => \"where\" != Current.Value; }")]
    [InlineData("class P { bool M() => Current.Value == ContextualKeywords.When; }")]
    [InlineData("class P { bool M(Identifier id) => id.Name == ContextualKeywords.Placeholder; }")]
    [InlineData("class P { void M() { switch (Current.Value) { default: break; } } }")]
    [InlineData("class P { int M() => Current.Value switch { _ => 0 }; }")]
    [InlineData("class P { bool M() => Current.Value is \"where\" or \"when\"; }")]
    [InlineData("class P { bool M(Token t) => t is { Value: \"where\" }; }")]
    [InlineData("class P { bool M(string n) => n is ContextualKeywords.Get; }")]
    [InlineData("class P { void M(string n) { switch (n) { case ContextualKeywords.Set: break; } } }")]
    [InlineData("class P { bool M() => Current.Value.Equals(\"where\"); }")]
    [InlineData("class P { bool M() => string.Equals(Current.Value, \"where\"); }")]
    public void PositiveControl_AnEscapeBlindComparison_IsReported(string source)
        => EscapeBlindComparisons(source).Should().ContainSingle();

    [Theory]
    [InlineData("class P { bool M() => IsContextualKeyword(Current, ContextualKeywords.When); }")]
    [InlineData("class P { void M(Node node) { if (node.Value != null) { } } }")]
    [InlineData("class P { bool M(Kw kwarg) => kwarg.Value is LambdaExpression lambda; }")]
    [InlineData("class P { bool M(Kw kwarg) => kwarg.Value is null; }")]
    [InlineData("class P { void M(W w) => w.Write(ContextualKeywords.BeforeSet); }")]
    [InlineData("class P { bool M() => Current.Type == TokenType.Identifier; }")]
    public void NegativeControl_PredicateCallsNullChecksAndSpellings_AreNotComparisons(string source)
        => EscapeBlindComparisons(source).Should().BeEmpty();
}
