using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;
using Sharpy.Lsp.Refactoring;
using Sharpy.Lsp.Tests.Conformance;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests.Refactoring;

/// <summary>
/// The one source-region seam every code-action provider reads (P47, #2232, plan D1): the line table
/// over <c>LineDiff.Split</c>, a statement's true last line, the verbatim slice, the uniform re-indent
/// that leaves multi-line-literal lines alone, and a line's inline comment — and, at the bottom, the
/// providers' consumption of it: a document and its <c>\r\n</c> / lone-<c>\r</c> twins get the same
/// actions and the same applied text modulo the break.
/// </summary>
public class SourceRegionTests
{
    #region SourceLines

    [Fact]
    public void SourceLines_Lf_SplitsOnNewlineAndWritesNewline()
    {
        var lines = SourceLines.Of("a\nbb\n");

        lines.Lines.Should().Equal("a", "bb", "");
        lines.Breaks.Should().Equal("\n", "\n");
        lines.DocumentLineBreak.Should().Be("\n");
        lines.Count.Should().Be(3);
        lines.StartOffset(2).Should().Be(2);
        lines.StartOffset(3).Should().Be(5);
    }

    [Fact]
    public void SourceLines_Crlf_CountsCrlfAsOneBreakAndWritesIt()
    {
        var lines = SourceLines.Of("a\r\nbb\r\n");

        lines.Lines.Should().Equal("a", "bb", "");
        lines.Breaks.Should().Equal("\r\n", "\r\n");
        lines.DocumentLineBreak.Should().Be("\r\n");
        lines.StartOffset(2).Should().Be(3);
        lines.StartOffset(3).Should().Be(7);
    }

    [Fact]
    public void SourceLines_LoneCr_IsALineBreak()
    {
        var lines = SourceLines.Of("def f():\r    x = 1\r\r");

        lines.Lines.Should().Equal("def f():", "    x = 1", "", "");
        lines.DocumentLineBreak.Should().Be("\r");
        lines.Indentation(2).Should().Be("    ");
        lines.IsBlank(2).Should().BeFalse();
        lines.IsBlank(3).Should().BeTrue();
        lines.IndentUnit.Should().Be("    ");
    }

    [Fact]
    public void SourceLines_Mixed_KeepsEachBreakAndWritesTheFirst()
    {
        var lines = SourceLines.Of("a\r\nb\nc\rd");

        lines.Lines.Should().Equal("a", "b", "c", "d");
        lines.Breaks.Should().Equal("\r\n", "\n", "\r");
        lines.DocumentLineBreak.Should().Be("\r\n");
        lines.StartOffset(3).Should().Be(5);
        lines.LineOfOffset(1).Should().Be(1, "the break belongs to the line it ends");
        lines.LineOfOffset(5).Should().Be(3);
        lines.LineOfOffset(7).Should().Be(4);
    }

    [Fact]
    public void SourceLines_NoBreak_WritesNewline()
    {
        var lines = SourceLines.Of("x = 1");

        lines.Count.Should().Be(1);
        lines.DocumentLineBreak.Should().Be("\n");
        lines.Indentation(0).Should().Be("", "line 0 is outside the 1-based table");
        lines.Indentation(2).Should().Be("");
    }

    [Fact]
    public void SourceLines_TabIndentedDocument_ReindentsByOneTab()
    {
        var lines = SourceLines.Of("def f():\n\tif x:\n\t\tpass\n");

        lines.IndentUnit.Should().Be("\t");
        lines.Indentation(3).Should().Be("\t\t");
        Region.Reindent(lines.Slice(2, 3), +1, lines.LiteralLines, '\t')
            .Should().Equal("\t\tif x:", "\t\t\tpass");
    }

    [Fact]
    public void SourceLines_IsOneInstancePerText()
    {
        var text = string.Concat("x = 1", "\n");

        SourceLines.Of(text).Should().BeSameAs(SourceLines.Of(text));
    }

    [Fact]
    public void NodeText_MultiLineNode_IsVerbatimWithItsOwnBreaks()
    {
        foreach (var lineBreak in new[] { "\n", "\r\n", "\r" })
        {
            var source = "f(1,\n  2)\n".Replace("\n", lineBreak, StringComparison.Ordinal);
            var call = StatementsOf(source)[0];

            SourceLines.Of(source).NodeText(call).Should().Be("f(1," + lineBreak + "  2)", $"break {Escape(lineBreak)}");
        }
    }

    #endregion

    #region Extent.EndLine

    [Fact]
    public void EndLine_StatementEndingInAThreeLineString_IsTheStringsLastLine()
    {
        // The parser records LineEnd = the string token's START line for this statement (#2295);
        // the extent reads the token's span.
        var source = "x = \"\"\"a\nb\nc\"\"\"\ny = 1\n";
        var statements = StatementsOf(source);

        EndLine(source, statements[0]).Should().Be(3);
        EndLine(source, statements[1]).Should().Be(4);
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void EndLine_ThreeLineString_IsCountedOnTheOneLineTable(string lineBreak)
    {
        var source = "x = \"\"\"a\nb\nc\"\"\"\ny = 1\n".Replace("\n", lineBreak, StringComparison.Ordinal);

        EndLine(source, StatementsOf(source)[0]).Should().Be(3);
    }

    [Fact]
    public void EndLine_StatementEndingInAMultiLineFString_IsTheFStringsLastLine()
    {
        var source = "n = 1\nx = f\"\"\"a\n{n}\nb\"\"\"\ny = 2\n";

        EndLine(source, StatementsOf(source)[1]).Should().Be(4);
    }

    [Fact]
    public void EndLine_ParenthesizedImport_IsTheClosingParenthesisLine()
    {
        var source = "from os.path import (\n    join,  # j\n    basename,\n)\nimport sys\n";
        var statements = StatementsOf(source);

        EndLine(source, statements[0]).Should().Be(4);
        EndLine(source, statements[1]).Should().Be(5);
    }

    [Fact]
    public void EndLine_SingleLineStatement_IsItsLineEnd()
    {
        var source = "x = 1  # one\ny = \"s\"\n";
        var statements = StatementsOf(source);

        EndLine(source, statements[0]).Should().Be(statements[0].LineEnd).And.Be(1);
        EndLine(source, statements[1]).Should().Be(statements[1].LineEnd).And.Be(2);
    }

    [Fact]
    public void EndLine_CompoundStatementWhoseLastBodyLineIsAString_IsTheStringsLastLine()
    {
        var source = "def f():\n    s = \"\"\"a\n  b\"\"\"\nx = 1\n";

        EndLine(source, StatementsOf(source)[0]).Should().Be(3);
    }

    [Fact]
    public void EndLine_ExpressionBeforeAMultiLineToken_DoesNotTakeThatTokensLine()
    {
        // `a` ends on line 1; the string after it on the same line spans to line 2 but is not `a`'s.
        var source = "f(a, \"\"\"p\nq\"\"\")\n";
        var a = IdentifiersOf(source, "a").Single();

        EndLine(source, a).Should().Be(1);
    }

    #endregion

    #region Region.Slice / Region.Reindent

    [Fact]
    public void Slice_IsWholeLinesVerbatim_CommentsAndBlankLinesIncluded()
    {
        var lines = SourceLines.Of("a = 1  # t\n\n    # own\nb = 2\n");

        var region = lines.Slice(1, 4);

        region.First.Should().Be(1);
        region.Last.Should().Be(4);
        region.Lines.Should().Equal("a = 1  # t", "", "    # own", "b = 2");
        region.Join("\r\n").Should().Be("a = 1  # t\r\n\r\n    # own\r\nb = 2");
    }

    [Fact]
    public void Reindent_PlusFour_ShiftsEveryLineByTheSameOffset()
    {
        var region = new Region(1, 2, new[] { "x = 1", "    y()" });

        Region.Reindent(region, +4, new HashSet<int>()).Should().Equal("    x = 1", "        y()");
    }

    [Fact]
    public void Reindent_MinusFour_ShiftsEveryLineByTheSameOffset()
    {
        var region = new Region(1, 2, new[] { "        a", "            b" });

        Region.Reindent(region, -4, new HashSet<int>()).Should().Equal("    a", "        b");
    }

    [Fact]
    public void Reindent_MinusFour_ClampsALineIndentedLessAtColumnZero()
    {
        var region = new Region(1, 3, new[] { "    a", "b", "  c" });

        Region.Reindent(region, -4, new HashSet<int>()).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Reindent_LeavesALineInsideAMultiLineLiteralByteIdentical()
    {
        var lines = SourceLines.Of("def f():\n    s = \"\"\"a\n    b\"\"\"\n    t = 1\n");
        lines.LiteralLines.Should().Equal(new[] { 3 }, "line 3 starts inside the string (positive control)");

        Region.Reindent(lines.Slice(2, 4), +4, lines.LiteralLines)
            .Should().Equal("        s = \"\"\"a", "    b\"\"\"", "        t = 1");
        Region.Reindent(lines.Slice(2, 4), -4, lines.LiteralLines)
            .Should().Equal("s = \"\"\"a", "    b\"\"\"", "t = 1");
    }

    [Fact]
    public void Reindent_LeavesABlankLineByteIdentical()
    {
        var region = new Region(1, 4, new[] { "    a", "   ", "", "    b" });

        Region.Reindent(region, +4, new HashSet<int>()).Should().Equal("        a", "   ", "", "        b");
        Region.Reindent(region, -4, new HashSet<int>()).Should().Equal("a", "   ", "", "b");
    }

    #endregion

    #region InlineComment

    [Fact]
    public void InlineComment_HeaderWithOne_IsThatComment()
    {
        var lines = SourceLines.Of("if x == 1:  # one\n    pass\n");

        lines.InlineComment(1)!.Text.Should().Be("# one");
        lines.InlineComment(2).Should().BeNull();
    }

    [Fact]
    public void InlineComment_HeaderWithoutOne_IsNull_AndAnOwnLineCommentIsNotInline()
    {
        var lines = SourceLines.Of("# own\nif x == 1:\n    pass  # body\n");

        lines.InlineComment(1).Should().BeNull("an own-line comment ends no code line");
        lines.InlineComment(2).Should().BeNull();
        lines.InlineComment(3)!.Text.Should().Be("# body", "positive control on the same document");
    }

    #endregion

    #region Consumers: a break twin gets the same actions and the same applied text

    private static readonly DocumentUri TestUri = DocumentUri.From("file:///source-region-test.spy");

    /// <summary>
    /// The probe documents of plan-e39946's Current State table (cells 1–4, 8, 11, 12/14, 15–21) whose
    /// actions read the line table: <c>(name, lf text, range, title prefix, with diagnostics)</c>.
    /// </summary>
    public static IEnumerable<object[]> TwinCases()
    {
        yield return Case("organize-imports", "import sys\n# string helpers\nfrom os.path import (\n    join,  # path joining\n    basename,\n)\nimport math  # numeric\n\ndef main():\n    print(math.floor(1.5), join(\"a\", \"b\"), basename(\"/x/y\"), len(sys.argv) >= 0)\n", 0, 0, 0, 0, "Organize Imports");
        yield return Case("organize-imports-blank", "import sys\n\n# helpers\nimport math\n\n\ndef main():\n    print(math.floor(1.5), len(sys.argv))\n", 0, 0, 0, 0, "Organize Imports");
        yield return Case("wrap", "def main():\n    x = 1\n    if x > 0:  # positive\n        print(x)\n    print(\"done\")\n", 1, 0, 3, 16, "Wrap in try");
        yield return Case("extract-method", "def main():\n    x = 1\n    if x > 0:  # positive\n        print(x)\n    print(\"done\")\n", 1, 0, 3, 16, "Extract method");
        yield return Case("convert-to-match", "def main():\n    x = 2\n    if x == 1:  # one\n        print(\"one\")\n    elif x == 2:\n        print(\"two\")\n    else:\n        print(\"other\")\n", 2, 4, 2, 4, "Convert to match");
        yield return Case("convert-to-if", "def main():\n    x = 2\n    match x:\n        case 1:  # one\n            print(\"one\")\n        case _:\n            print(\"other\")\n", 2, 4, 2, 4, "Convert to if");
        yield return Case("extract-variable", "def main():\n    y = [1,\n         2]\n    print(y)\n", 1, 8, 2, 11, "Extract variable");
        yield return Case("inline-variable", "def main():\n    k: int = 1 + 2  # k\n    print(k)\n", 1, 4, 1, 4, "Inline variable");
        yield return Case("inline-function", "def add(a: int, b: int) -> int:\n    return a + b  # sum\n\ndef main():\n    print(add(1, 2))\n", 4, 10, 4, 10, "Inline function");
        yield return Case("remove-type-annotation", "def main():\n    k: int = 1 + 2\n    print(k)\n", 1, 4, 1, 4, "Remove type annotation");
        yield return Case("implement-interface", "interface IGreeter:\n    def greet(self) -> str: ...\n    def farewell(self) -> str: ...\nclass HelloGreeter(IGreeter):\n    def greet(self) -> str:\n        return \"hello\"\ndef main():\n    pass\n", 3, 6, 3, 6, "Implement");
        yield return Case("remove-unused-import", "import sys  # a\nimport math\n\ndef main():\n    print(math.floor(1.5))\n", 0, 0, 0, 15, "Remove unused import", diagnostics: true);

        static object[] Case(string name, string lf, int sl, int sc, int el, int ec, string title, bool diagnostics = false) =>
            new object[] { name, lf, sl, sc, el, ec, title, diagnostics };
    }

    [Theory]
    [MemberData(nameof(TwinCases))]
    public async Task Providers_AnswerABreakTwinWithTheSameActionsAndTheSameTextModuloTheBreak(
        string name, string lf, int sl, int sc, int el, int ec, string title, bool diagnostics)
    {
        var range = new LspRange(new Position(sl, sc), new Position(el, ec));
        var (lfTitles, lfApplied) = await RequestAsync(lf, range, title, diagnostics);
        lfApplied.Should().NotBeNull($"{name}: the lf document is offered '{title}' (positive control)");

        foreach (var lineBreak in new[] { "\r\n", "\r" })
        {
            var twin = lf.Replace("\n", lineBreak, StringComparison.Ordinal);
            var (titles, applied) = await RequestAsync(twin, range, title, diagnostics);

            titles.Should().Equal(lfTitles, $"{name} / {Escape(lineBreak)}: the same actions are offered");
            applied.Should().Be(lfApplied!.Replace("\n", lineBreak, StringComparison.Ordinal),
                $"{name} / {Escape(lineBreak)}: the applied text is the lf twin's with the document's break");
        }
    }

    private static readonly ICodeActionProvider[] Providers =
    {
        new DiagnosticQuickFixProvider(),
        new OrganizeImportsProvider(),
        new ImplementInterfaceProvider(),
        new ExtractVariableProvider(),
        new ExtractMethodProvider(),
        new ConvertFormsProvider(),
        new InlineProvider(),
    };

    private static async Task<(SCG.List<string> Titles, string? Applied)> RequestAsync(
        string text, LspRange range, string titlePrefix, bool withDiagnostics)
    {
        var analysis = new CompilerApi().Analyze(text, CancellationToken.None);
        var diagnostics = new SCG.List<Diagnostic>();
        if (withDiagnostics)
        {
            var sourceText = new SourceText(text, TestUri.ToString());
            foreach (var d in analysis.Diagnostics)
                diagnostics.Add(DiagnosticPublisher.ConvertDiagnostic(d, sourceText));
        }

        var context = new CodeActionProviderContext(TestUri, range, new Container<Diagnostic>(diagnostics), analysis, text);
        var titles = new SCG.List<string>();
        string? applied = null;
        foreach (var provider in Providers)
        {
            foreach (var action in await provider.GetCodeActionsAsync(context, CancellationToken.None))
            {
                titles.Add(action.Title);
                if (applied is null && action.Title.StartsWith(titlePrefix, StringComparison.Ordinal))
                    applied = LspFormattingDriver.ApplyStrict(text, action.Edit!.Changes![TestUri].ToList());
            }
        }

        return (titles, applied);
    }

    #endregion

    #region Helpers

    private static int EndLine(string source, Node node)
    {
        var lines = SourceLines.Of(source);
        return Extent.EndLine(node, lines.Tokens, lines);
    }

    private static IReadOnlyList<Statement> StatementsOf(string source)
    {
        var lex = FileCompilationPipeline.Lex(new SourceText(source, "<test>"), NullLogger.Instance);
        var parse = FileCompilationPipeline.Parse(lex.Tokens, NullLogger.Instance);
        parse.HasErrors.Should().BeFalse($"the test document parses: {Escape(source)}");
        return parse.Module!.Body;
    }

    private static SCG.List<Identifier> IdentifiersOf(string source, string name)
    {
        var finder = new IdentifierCollector(name);
        foreach (var statement in StatementsOf(source))
            finder.Visit(statement);
        return finder.Found;
    }

    private static string Escape(string text) =>
        text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private sealed class IdentifierCollector(string name) : AstVisitor
    {
        public SCG.List<Identifier> Found { get; } = new();

        public override void VisitIdentifier(Identifier node)
        {
            if (node.Name == name)
                Found.Add(node);
            base.VisitIdentifier(node);
        }
    }

    #endregion
}
