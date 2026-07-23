using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.CodeGen;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Unit tests for <see cref="LineDirectiveEditPlanner"/> — the shared source of the #line
/// post-processing arithmetic (#1108). These pin the plan itself (offsets, insertion positions,
/// no-op cases); the end-to-end text byte-identity is proven by <see cref="LineDirectivePostProcessorTests"/>.
/// </summary>
[Collection("Sequential")]
public class LineDirectiveEditPlannerTests
{
    [Fact]
    public void Plan_CharOffset_FromFollowingLineIndentation()
    {
        var input = "#line (1, 5) - (1, 20) 1 \"test.spy\"\n        var x = 42;\n";
        var plan = LineDirectiveEditPlanner.Plan(input);

        var offsetEdit = plan.Edits.OfType<CharOffsetEdit>().Should().ContainSingle().Subject;
        offsetEdit.NewOffset.Should().Be(8, "the following line is indented 8 spaces");

        // The placeholder digit '1' sits just before the quoted file name.
        input[offsetEdit.PlaceholderPosition].Should().Be('1');
        offsetEdit.DirectiveStart.Should().Be(0);
    }

    [Fact]
    public void Plan_CharOffset_TabsCountAsFour()
    {
        var input = "#line (1, 5) - (1, 20) 1 \"test.spy\"\n\t\tvar x = 42;\n";
        var plan = LineDirectiveEditPlanner.Plan(input);

        plan.Edits.OfType<CharOffsetEdit>().Single().NewOffset.Should().Be(8,
            "two tabs count as 4 columns each");
    }

    [Fact]
    public void Plan_CharOffset_MinimumIsOne_WhenNoIndentation()
    {
        var input = "#line (1, 5) - (1, 20) 1 \"test.spy\"\nvar x = 42;\n";
        var plan = LineDirectiveEditPlanner.Plan(input);

        plan.Edits.OfType<CharOffsetEdit>().Single().NewOffset.Should().Be(1);
    }

    [Fact]
    public void Plan_NoEnhancedDirectives_IsEmpty()
    {
        var plan = LineDirectiveEditPlanner.Plan("var x = 42;\nvar y = 10;\n");
        plan.Edits.Should().BeEmpty();
    }

    [Fact]
    public void Plan_BasicDirective_NotTreatedAsEnhanced()
    {
        var plan = LineDirectiveEditPlanner.Plan("#line 5 \"test.spy\"\n    {\nvar x = 1;\n");
        plan.Edits.Should().BeEmpty("a basic #line directive carries no placeholder offset to rewrite");
    }

    [Fact]
    public void Plan_MultiLineConstruct_InsertsHiddenBeforeSecondCodeLine()
    {
        var input =
            "#line (1, 5) - (1, 30) 1 \"test.spy\"\n" +
            "        var items = new List<int>(new int[]\n" +
            "        {\n" +
            "            1\n" +
            "        });\n" +
            "#line (2, 5) - (2, 20) 1 \"test.spy\"\n" +
            "        var y = 10;\n";
        var plan = LineDirectiveEditPlanner.Plan(input);

        var hidden = plan.Edits.OfType<LineInsertionEdit>().Should().ContainSingle().Subject;
        hidden.Text.Should().Be("#line hidden\n");
        // Anchored at the start of the second code line ("        {").
        input.Substring(hidden.Position, 9).Should().Be("        {");
        // No #line default: a following directive restores the mapping.
        plan.Edits.OfType<LineInsertionEdit>().Should().NotContain(e => e.Text.Contains("#line default"));
    }

    [Fact]
    public void Plan_SingleLineConstructs_NoHidden()
    {
        var input =
            "#line (1, 5) - (1, 30) 1 \"test.spy\"\n" +
            "        var x = 42;\n" +
            "#line (2, 5) - (2, 20) 1 \"test.spy\"\n" +
            "        var y = 10;\n";
        var plan = LineDirectiveEditPlanner.Plan(input);

        plan.Edits.OfType<LineInsertionEdit>().Should().BeEmpty();
        plan.Edits.OfType<CharOffsetEdit>().Should().HaveCount(2);
    }

    [Fact]
    public void Plan_MultiLineAtEof_InsertsHiddenAndDefault()
    {
        var input =
            "#line (1, 5) - (1, 30) 1 \"test.spy\"\n" +
            "        var items = new List<int>(new int[]\n" +
            "        {\n" +
            "            1\n" +
            "        });\n";
        var plan = LineDirectiveEditPlanner.Plan(input);

        var insertions = plan.Edits.OfType<LineInsertionEdit>().ToList();
        insertions.Should().Contain(e => e.Text == "#line hidden\n");
        var def = insertions.Should().ContainSingle(e => e.Text.Contains("#line default")).Subject;
        def.Text.Should().Be("\n#line default\n", "the EOF #line default carries a leading newline");
        def.Position.Should().Be(input.Length);
    }

    [Fact]
    public void Plan_DetectsCrlf()
    {
        LineDirectiveEditPlanner.Plan("a\r\nb\r\n").Newline.Should().Be("\r\n");
        LineDirectiveEditPlanner.Plan("a\nb\n").Newline.Should().Be("\n");
    }

    [Fact]
    public void Plan_Crlf_InsertionsUseCrlf()
    {
        var input =
            "#line (1, 5) - (1, 30) 1 \"test.spy\"\r\n" +
            "        var items = new List<int>(new int[]\r\n" +
            "        {\r\n" +
            "            1\r\n" +
            "        });\r\n";
        var plan = LineDirectiveEditPlanner.Plan(input);

        plan.Edits.OfType<LineInsertionEdit>().Should()
            .Contain(e => e.Text == "#line hidden\r\n")
            .And.Contain(e => e.Text == "\r\n#line default\r\n");
    }
}
