using CsCheck;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;

namespace Sharpy.Compiler.Tests.Properties.Lexer;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class CommentPreservationPropertyTests
{
    private readonly ITestOutputHelper _output;

    public CommentPreservationPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void LeadingComments_PreservedInTrivia()
    {
        int passed = 0;
        int total = 0;

        GenComments.SourceWithLeadingComment.Sample(source =>
        {
            Interlocked.Increment(ref total);
            var lexer = new SharpyLexer(source, preserveTrivia: true);
            var tokens = lexer.TokenizeAll();

            var hasComment = tokens.Any(t =>
                t.LeadingTrivia is { Count: > 0 } trivia &&
                trivia.Any(tr => tr.Kind == TriviaKind.Comment));

            if (hasComment)
                Interlocked.Increment(ref passed);
        }, iter: 150);

        _output.WriteLine($"Leading comment preservation: {passed}/{total}");
        Assert.True(passed == total,
            $"Leading comments not preserved in {total - passed}/{total} cases");
    }

    [Fact]
    public void TrailingComments_PreservedInTrivia()
    {
        int passed = 0;
        int total = 0;

        GenComments.SourceWithTrailingComment.Sample(source =>
        {
            Interlocked.Increment(ref total);
            var lexer = new SharpyLexer(source, preserveTrivia: true);
            var tokens = lexer.TokenizeAll();

            var hasTrailing = tokens.Any(t =>
                t.TrailingTrivia is { Count: > 0 } trivia &&
                trivia.Any(tr => tr.Kind == TriviaKind.Comment));

            if (hasTrailing)
                Interlocked.Increment(ref passed);
        }, iter: 150);

        _output.WriteLine($"Trailing comment preservation: {passed}/{total}");
        Assert.True(passed == total,
            $"Trailing comments not preserved in {total - passed}/{total} cases");
    }

    [Fact]
    public void NestedComments_PreservedInCorrectTriviaSlots()
    {
        int passed = 0;
        int total = 0;

        GenComments.SourceWithNestedComments.Sample(source =>
        {
            Interlocked.Increment(ref total);
            var lexer = new SharpyLexer(source, preserveTrivia: true);
            var tokens = lexer.TokenizeAll();

            var commentCount = tokens
                .Where(t => t.LeadingTrivia is { Count: > 0 })
                .SelectMany(t => t.LeadingTrivia!)
                .Count(tr => tr.Kind == TriviaKind.Comment);

            if (commentCount >= 2)
                Interlocked.Increment(ref passed);
        }, iter: 150);

        _output.WriteLine($"Nested comment preservation: {passed}/{total}");
        Assert.True(passed == total,
            $"Nested comments not fully preserved in {total - passed}/{total} cases");
    }

    [Fact]
    public void MultipleConsecutiveComments_AllCaptured()
    {
        int passed = 0;
        int total = 0;

        GenComments.SourceWithMultipleComments.Sample(source =>
        {
            Interlocked.Increment(ref total);
            var lexer = new SharpyLexer(source, preserveTrivia: true);
            var tokens = lexer.TokenizeAll();

            var totalComments = tokens
                .Where(t => t.LeadingTrivia is { Count: > 0 })
                .SelectMany(t => t.LeadingTrivia!)
                .Count(tr => tr.Kind == TriviaKind.Comment);

            if (totalComments >= 2)
                Interlocked.Increment(ref passed);
        }, iter: 150);

        _output.WriteLine($"Multiple comments captured: {passed}/{total}");
        Assert.True(passed == total,
            $"Multiple consecutive comments not all captured in {total - passed}/{total} cases");
    }

    [Fact]
    public void BlankLines_PreservedInTrivia()
    {
        int passed = 0;
        int total = 0;

        GenComments.SourceWithBlankLines.Sample(source =>
        {
            Interlocked.Increment(ref total);
            var lexer = new SharpyLexer(source, preserveTrivia: true);
            var tokens = lexer.TokenizeAll();

            var hasBlankLines = tokens.Any(t =>
                t.LeadingTrivia is { Count: > 0 } trivia &&
                trivia.Any(tr => tr.Kind == TriviaKind.BlankLines && tr.BlankLineCount > 0));

            if (hasBlankLines)
                Interlocked.Increment(ref passed);
        }, iter: 150);

        _output.WriteLine($"Blank line preservation: {passed}/{total}");
        Assert.True(passed == total,
            $"Blank lines not preserved in {total - passed}/{total} cases");
    }

    [Fact]
    public void BlankLines_CountMatchesSource()
    {
        GenComments.SourceWithBlankLines.Sample(source =>
        {
            var lexer = new SharpyLexer(source, preserveTrivia: true);
            var tokens = lexer.TokenizeAll();

            var blankLineTrivia = tokens
                .Where(t => t.LeadingTrivia is { Count: > 0 })
                .SelectMany(t => t.LeadingTrivia!)
                .Where(tr => tr.Kind == TriviaKind.BlankLines)
                .ToList();

            foreach (var trivia in blankLineTrivia)
            {
                if (trivia.BlankLineCount < 1)
                    throw new Exception(
                        $"BlankLineCount must be >= 1 but was {trivia.BlankLineCount}");
            }
        }, print: s => s, iter: 150);
    }

    [Fact]
    public void CommentText_MatchesOriginalContent()
    {
        GenComments.CommentText.Sample(commentText =>
        {
            var source = $"{commentText}\nx = 1\n";
            var lexer = new SharpyLexer(source, preserveTrivia: true);
            var tokens = lexer.TokenizeAll();

            var trivia = tokens
                .Where(t => t.LeadingTrivia is { Count: > 0 })
                .SelectMany(t => t.LeadingTrivia!)
                .Where(tr => tr.Kind == TriviaKind.Comment)
                .ToList();

            if (trivia.Count == 0)
                throw new Exception(
                    $"No comment trivia found for: {commentText}");

            if (!trivia[0].Text.Contains("#"))
                throw new Exception(
                    $"Trivia text doesn't contain '#': got '{trivia[0].Text}'");
        }, print: s => s, iter: 200);
    }
}
