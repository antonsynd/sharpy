using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// The instrument the formatting route-parity sweep reads (#2168, plan P22e): the strict LSP edit
/// applier, the route roster's totality, and one request per route through the real handlers.
/// </summary>
public sealed class LspFormattingDriverTests : IDisposable
{
    private readonly LspFormattingDriver _driver = new();

    public void Dispose() => _driver.Dispose();

    private static TextEdit E(int startLine, int startChar, int endLine, int endChar, string newText) => new()
    {
        Range = new LspRange(new Position(startLine, startChar), new Position(endLine, endChar)),
        NewText = newText,
    };

    private static string Apply(string text, params TextEdit[] edits) => LspFormattingDriver.ApplyStrict(text, edits);

    // ---- the applier: legal edits ----

    [Fact]
    public void ApplyStrict_NoEdits_ReturnsTextUnchanged()
        => Apply("a\nb").Should().Be("a\nb");

    [Fact]
    public void ApplyStrict_Insert()
        => Apply("abc", E(0, 1, 0, 1, "X")).Should().Be("aXbc");

    [Fact]
    public void ApplyStrict_DeleteAcrossALineBreak()
        => Apply("a\nb\nc", E(0, 1, 1, 1, "")).Should().Be("a\nc");

    [Fact]
    public void ApplyStrict_Replace()
        => Apply("hello world", E(0, 6, 0, 11, "there")).Should().Be("hello there");

    [Fact]
    public void ApplyStrict_InsertAtEndOfDocument()
        => Apply("a\nb", E(1, 1, 1, 1, "!")).Should().Be("a\nb!");

    [Fact]
    public void ApplyStrict_EmptyFinalLine_InsertAtItsStartIsLegal()
        => Apply("a\n", E(1, 0, 1, 0, "b")).Should().Be("a\nb");

    [Fact]
    public void ApplyStrict_Crlf_LinesAreEndedByCrlf()
    {
        Apply("a\r\nb\r\n", E(1, 0, 1, 1, "B")).Should().Be("a\r\nB\r\n");
        Apply("a\r\nb\r\n", E(2, 0, 2, 0, "c")).Should().Be("a\r\nb\r\nc");
        // Deleting a CRLF line whole: from its start to the next line's start.
        Apply("a\r\nb\r\nc", E(1, 0, 2, 0, "")).Should().Be("a\r\nc");
    }

    [Fact]
    public void ApplyStrict_LoneCr_EndsALine()
    {
        Apply("a\rb\rc", E(2, 0, 2, 1, "C")).Should().Be("a\rb\rC");
        // "\r\n" is one break and the following lone "\r" another: three lines, the middle one empty.
        Apply("a\r\n\rb", E(1, 0, 1, 0, "M")).Should().Be("a\r\nM\rb");
        Apply("a\r\n\rb", E(2, 0, 2, 1, "B")).Should().Be("a\r\n\rB");
    }

    [Fact]
    public void ApplyStrict_MultipleEdits_AddressTheOriginalText()
    {
        // The first edit adds a line and the second addresses ORIGINAL line 2 ("c"). An applier that
        // applied them in turn against the moving text would rewrite "b" instead.
        Apply("a\nb\nc", E(0, 0, 0, 1, "x\ny"), E(2, 0, 2, 1, "z")).Should().Be("x\ny\nb\nz");
        // The same on one line: the first edit lengthens the line before the second's columns.
        Apply("abcdef", E(0, 0, 0, 1, "XYZ"), E(0, 4, 0, 5, "Q")).Should().Be("XYZbcdQf");
        // Array order does not matter for disjoint edits.
        Apply("a\nb\nc", E(2, 0, 2, 1, "z"), E(0, 0, 0, 1, "x\ny")).Should().Be("x\ny\nb\nz");
    }

    [Fact]
    public void ApplyStrict_TouchingEdits_AreNotAnOverlap()
        => Apply("abcd", E(0, 0, 0, 2, "X"), E(0, 2, 0, 4, "Y")).Should().Be("XY");

    [Fact]
    public void ApplyStrict_InsertsAtOnePosition_KeepArrayOrder()
    {
        Apply("ab", E(0, 1, 0, 1, "1"), E(0, 1, 0, 1, "2")).Should().Be("a12b");
        Apply("ab", E(0, 1, 0, 1, "2"), E(0, 1, 0, 1, "1")).Should().Be("a21b");
    }

    // ---- the applier: illegal edits (the instrument's positive controls) ----

    [Theory]
    [InlineData("abcdef", 0, 0, 0, 3, 0, 2, 0, 4)] // partial overlap
    [InlineData("abcdef", 0, 1, 0, 4, 0, 2, 0, 2)] // an insert strictly inside a replacement
    [InlineData("a\nb\nc", 0, 0, 1, 1, 1, 0, 2, 0)] // overlap across a line break
    public void ApplyStrict_OverlappingEdits_Throw(
        string text, int s1l, int s1c, int e1l, int e1c, int s2l, int s2c, int e2l, int e2c)
    {
        var act = () => Apply(text, E(s1l, s1c, e1l, e1c, "X"), E(s2l, s2c, e2l, e2c, "Y"));
        act.Should().Throw<StrictEditApplicationException>().WithMessage("*overlaps*");
    }

    /// <summary>
    /// The last legal character of a line applies; one past it throws. Pairing the two keeps the
    /// refusal from being a vacuous "this applier throws on everything".
    /// </summary>
    [Theory]
    [InlineData("a", 0, 1)]
    [InlineData("abc\ndef", 0, 3)]
    [InlineData("a\r\nb", 0, 1)] // character 2 would sit between "\r" and "\n"
    [InlineData("a\rb", 0, 1)]
    [InlineData("a\n", 1, 0)] // the empty final line has exactly one position
    [InlineData("", 0, 0)]
    public void ApplyStrict_CharacterPastLineContent_Throws(string text, int line, int lastLegalCharacter)
    {
        Apply(text, E(line, lastLegalCharacter, line, lastLegalCharacter, "X")).Should().NotBeNull();
        var act = () => Apply(text, E(line, lastLegalCharacter + 1, line, lastLegalCharacter + 1, "X"));
        act.Should().Throw<StrictEditApplicationException>().WithMessage("*outside line*");
    }

    [Theory]
    [InlineData("a", 0)]
    [InlineData("a\n", 1)]
    [InlineData("a\r\nb\r\n", 2)]
    [InlineData("a\rb", 1)]
    public void ApplyStrict_LinePastTheLastLine_Throws(string text, int lastLine)
    {
        Apply(text, E(lastLine, 0, lastLine, 0, "X")).Should().NotBeNull();
        var act = () => Apply(text, E(lastLine + 1, 0, lastLine + 1, 0, "X"));
        act.Should().Throw<StrictEditApplicationException>().WithMessage("*past the document's last line*");
    }

    [Fact]
    public void ApplyStrict_NegativePosition_Throws()
    {
        var act = () => Apply("abc", E(0, -1, 0, 0, "X"));
        act.Should().Throw<StrictEditApplicationException>();
    }

    [Theory]
    [InlineData("abc", 0, 2, 0, 1)]
    [InlineData("a\nb", 1, 0, 0, 0)]
    public void ApplyStrict_StartAfterEnd_Throws(string text, int sl, int sc, int el, int ec)
    {
        Apply(text, E(el, ec, sl, sc, "X")).Should().NotBeNull(); // the same range, ordered
        var act = () => Apply(text, E(sl, sc, el, ec, "X"));
        act.Should().Throw<StrictEditApplicationException>().WithMessage("*start is after end*");
    }

    [Fact]
    public void ApplyStrict_Exception_CarriesTheEdits()
    {
        var edits = new[] { E(0, 0, 0, 2, "X"), E(0, 1, 0, 3, "Y") };
        var act = () => LspFormattingDriver.ApplyStrict("abc", edits);
        act.Should().Throw<StrictEditApplicationException>().Which.Edits.Should().Equal(edits);
    }

    // ---- the roster ----

    [Fact]
    public void Routes_AreTotalOverTheFormattingHandlersOfTheLspAssembly()
    {
        var bases = new[]
        {
            typeof(DocumentFormattingHandlerBase),
            typeof(DocumentRangeFormattingHandlerBase),
            typeof(DocumentOnTypeFormattingHandlerBase),
        };
        var discovered = typeof(SharpyWorkspace).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && bases.Any(b => b.IsAssignableFrom(t)))
            .ToArray();
        var roster = LspFormattingDriver.Routes.Select(r => r.HandlerType).ToArray();

        // Anchored to a literal, not to the roster: a fourth formatting handler must be added here AND there.
        discovered.Should().HaveCount(3);
        discovered.Should().BeSubsetOf(roster, "every formatting handler the server can register is a route the sweep drives");
        roster.Should().HaveCount(3).And.OnlyHaveUniqueItems().And.BeSubsetOf(discovered);
        LspFormattingDriver.Routes.Select(r => r.Kind).Should().OnlyHaveUniqueItems()
            .And.BeSubsetOf(Enum.GetValues<LspFormattingRouteKind>());
    }

    // ---- one request per route through the real handlers ----

    [Fact]
    public void Full_FormatsTheDocument()
    {
        var (edits, applied) = _driver.Full("def foo():\n    x: int = 1\n\n    return x\n");
        edits.Should().NotBeEmpty();
        applied.Should().Be("def foo():\n    x: int = 1\n    return x\n");
        _driver.OpenDocumentCount.Should().Be(0);
    }

    [Fact]
    public void Full_AlreadyFormatted_NoEdits()
    {
        var (edits, applied) = _driver.Full("def foo():\n    return 1\n");
        edits.Should().BeEmpty();
        applied.Should().Be("def foo():\n    return 1\n");
    }

    [Fact]
    public void Range_FormatsTheSelectedLine()
    {
        var (edits, applied) = _driver.Range(
            "def foo():\n        x: int = 1\n    return x",
            new LspRange(new Position(1, 0), new Position(1, 14)));
        edits.Should().ContainSingle();
        applied.Should().Be("def foo():\n    x: int = 1\n    return x");
        _driver.OpenDocumentCount.Should().Be(0);
    }

    [Fact]
    public void OnType_ReindentsTheLine()
    {
        var (edits, applied) = _driver.OnType("def foo():\n        x: int = 1\n", line: 1);
        edits.Should().ContainSingle();
        applied.Should().Be("def foo():\n    x: int = 1\n");
        _driver.OpenDocumentCount.Should().Be(0);
    }

    [Fact]
    public void Requests_FromManyThreads_SeeOnlyTheirOwnDocument_AndLeaveNothingOpen()
    {
        // Each document is distinguishable by its name; a request that read another's document
        // would produce another's name.
        var results = new string[64];
        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            results[i] = _driver.Full($"def f{i}():\n    x: int = {i}\n\n    return x\n").Applied);

        for (var i = 0; i < results.Length; i++)
            results[i].Should().Be($"def f{i}():\n    x: int = {i}\n    return x\n");
        _driver.OpenDocumentCount.Should().Be(0);
    }
}
