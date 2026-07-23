using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Xunit;
using FluentAssertions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Unit tests for <see cref="LineDirectiveTreeRewriter"/> (#1108). The load-bearing assertion is
/// byte-identity with the text oracle (<see cref="LineDirectivePostProcessor.Process"/>) over trees
/// shaped exactly like the emitter's output (enhanced #line directives attached before
/// <c>NormalizeWhitespace</c>; <c>#nullable enable</c> prepended with LF after).
/// </summary>
[Collection("Sequential")]
public class LineDirectiveTreeRewriterTests
{
    private static StatementSyntax WithLineDirective(StatementSyntax statement, int line, int col)
    {
        // Mirror RoslynEmitter.CreateLineDirectiveTrivia: placeholder char offset 1, LF terminator,
        // attached BEFORE NormalizeWhitespace so the directive's own newline is normalized to CRLF.
        return statement.WithLeadingTrivia(
            ParseLeadingTrivia($"#line ({line}, {col}) - ({line}, {col + 5}) 1 \"test.spy\"\n"));
    }

    /// <summary>
    /// Builds a compilation unit the emitter's way: a method body of the given statements, wrapped in a
    /// class, NormalizeWhitespace'd (CRLF), then <c>#nullable enable\n\n</c> (LF) prepended.
    /// </summary>
    private static CompilationUnitSyntax BuildUnit(params StatementSyntax[] statements)
    {
        var method = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Main")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithBody(Block(statements));
        var cls = ClassDeclaration("Test")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(method));
        var cu = CompilationUnit()
            .WithUsings(SingletonList(UsingDirective(ParseName("System"))))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(cls))
            .NormalizeWhitespace();
        return (CompilationUnitSyntax)cu.WithLeadingTrivia(ParseLeadingTrivia("#nullable enable\n\n"));
    }

    /// <summary>The core invariant: the rewritten tree's text equals the oracle byte-for-byte.</summary>
    private static string AssertRewriteMatchesOracle(CompilationUnitSyntax unit)
    {
        var text = unit.ToFullString();
        var plan = LineDirectiveEditPlanner.Plan(text);
        var rewritten = LineDirectiveTreeRewriter.Apply(unit, plan);
        var oracle = LineDirectivePostProcessor.Process(text);

        rewritten.ToFullString().Should().Be(oracle);
        return oracle;
    }

    [Fact]
    public void Apply_SingleLineStatements_MatchesOracle_WithHiddenAndDefaultFraming()
    {
        var unit = BuildUnit(
            WithLineDirective(ParseStatement("int x = 42;"), 2, 5),
            WithLineDirective(ParseStatement("global::System.Console.WriteLine(x);"), 3, 5));

        var output = AssertRewriteMatchesOracle(unit);

        // charOffset corrected to the 8-space body indentation.
        output.Should().Contain("#line (2, 5) - (2, 10) 8 \"test.spy\"");
        // Closing braces after the last statement are code lines -> hidden + EOF default.
        output.Should().Contain("#line hidden");
        output.TrimEnd().Should().EndWith("#line default");
        // No hidden between two consecutive single-line statements.
        System.Text.RegularExpressions.Regex.Matches(output, "#line hidden").Count.Should().Be(1);
    }

    [Fact]
    public void Apply_MultiLineConstruct_MatchesOracle_WithHiddenInsideConstruct()
    {
        var unit = BuildUnit(
            WithLineDirective(
                ParseStatement("var items = new global::System.Collections.Generic.List<int> "
                    + "{ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };"),
                2, 5),
            WithLineDirective(ParseStatement("global::System.Console.WriteLine(items.Count);"), 3, 5));

        var output = AssertRewriteMatchesOracle(unit);

        // The multi-line list initializer forces a hidden inside the construct, plus one before the
        // closing braces = two hidden directives.
        System.Text.RegularExpressions.Regex.Matches(output, "#line hidden").Count.Should().Be(2);
        output.TrimEnd().Should().EndWith("#line default");
    }

    [Fact]
    public void Apply_MultiLineConstructAtEof_MatchesOracle()
    {
        var unit = BuildUnit(
            WithLineDirective(
                ParseStatement("var items = new global::System.Collections.Generic.List<int> "
                    + "{ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };"),
                2, 5));

        AssertRewriteMatchesOracle(unit);
    }

    [Fact]
    public void Apply_PreservesCrlf()
    {
        var unit = BuildUnit(WithLineDirective(ParseStatement("int x = 42;"), 2, 5));
        var oracle = AssertRewriteMatchesOracle(unit);

        oracle.Should().Contain("\r\n");
        System.Text.RegularExpressions.Regex.Matches(oracle, "(?<!\r)\n").Count
            .Should().Be(0, "the oracle normalizes the #nullable LF to the CRLF the body uses");
    }

    [Fact]
    public void Apply_NormalizesNullableEnableNewline()
    {
        var unit = BuildUnit(WithLineDirective(ParseStatement("int x = 42;"), 2, 5));
        // Raw tree has the mixed #nullable LF; rewritten tree must carry the normalized CRLF form.
        unit.ToFullString().Should().StartWith("#nullable enable\n\n");

        var plan = LineDirectiveEditPlanner.Plan(unit.ToFullString());
        var rewritten = LineDirectiveTreeRewriter.Apply(unit, plan);
        rewritten.ToFullString().Should().StartWith("#nullable enable\r\n\r\n");
    }

    [Fact]
    public void Apply_ProducesStructuredDirectives_ForZeroParseHandoff()
    {
        var unit = BuildUnit(WithLineDirective(ParseStatement("int x = 42;"), 2, 5));
        var plan = LineDirectiveEditPlanner.Plan(unit.ToFullString());
        var rewritten = LineDirectiveTreeRewriter.Apply(unit, plan);

        // The inserted #line hidden / #line default must be real structured directive trivia (so the
        // Create'd tree carries correct debug mapping) — not opaque text.
        var directives = rewritten.DescendantTrivia()
            .Where(t => t.IsKind(SyntaxKind.LineDirectiveTrivia))
            .Select(t => t.ToString())
            .ToList();
        directives.Should().Contain(d => d.Contains("#line hidden"));
        directives.Should().Contain(d => d.Contains("#line default"));
    }
}
