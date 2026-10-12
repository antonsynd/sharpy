using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Tests.Conformance;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Refactoring;

/// <summary>
/// if→match and match→if re-spell only the headers, keep each header's comment, and move the bodies as
/// regions (P47, #2232, plan D6 + D2). Driven through the real code-action handler
/// (<see cref="LspCodeActionDriver"/>) and judged on the byte-exact APPLIED text. Every expectation is one the
/// base binary (7f0502de3) gets wrong except cell 9, the module-level direction guard. Cell numbers are the
/// plan's Current State table.
/// </summary>
public sealed class ConvertFormsShapeTests : IDisposable
{
    private readonly LspCodeActionDriver _driver = new(new CompilerApi(Sharpy.Compiler.Logging.NullLogger.Instance, StdlibReferences.Paths()));

    public void Dispose() => _driver.Dispose();

    private string Convert(string text, int line, int character, string title)
    {
        using var session = _driver.Open(text);
        var actions = session.Request(new LspRange(new Position(line, character), new Position(line, character)));
        var action = actions.Should().ContainSingle(a => a.Title == title,
            $"'{title}' is offered; offered: {string.Join(" | ", actions.Select(a => a.Title))}").Subject;
        return session.Apply(action);
    }

    private const string ToMatch = "Convert to match statement";
    private const string ToIf = "Convert to if/elif/else";

    [Fact]
    public void Cell8_IfToMatch_InsideADef_HeaderAtItsColumn_NestedBodyKept_CommentsKept()
    {
        var text = "def main():\n    x = 2\n    if x == 1:  # one\n        print(\"one\")\n        # end one\n    elif x == 2:\n"
            + "        if x > 0:\n            print(\"two\")\n    else:\n        print(\"other\")\n";

        Convert(text, 2, 4, ToMatch).Should().Be(
            "def main():\n    x = 2\n    match x:\n        case 1:  # one\n            print(\"one\")\n            # end one\n"
            + "        case 2:\n            if x > 0:\n                print(\"two\")\n        case _:\n            print(\"other\")\n");
    }

    /// <summary>Direction guard: the base already converts a module-level chain correctly (indent "").</summary>
    [Fact]
    public void Cell9_IfToMatch_ModuleLevel_StaysCorrect()
    {
        var text = "x = 2\nif x == 1:\n    print(\"one\")\nelif x == 2:\n    print(\"two\")\nelse:\n    print(\"other\")\n";

        Convert(text, 1, 0, ToMatch).Should().Be(
            "x = 2\nmatch x:\n    case 1:\n        print(\"one\")\n    case 2:\n        print(\"two\")\n    case _:\n        print(\"other\")\n");
    }

    [Fact]
    public void Cell10_IfToMatch_InsideAMethod_HeaderAtItsColumn()
    {
        var text = "class A:\n    def m(self, x: int):\n        if x == 1:\n            print(\"one\")\n        else:\n            print(\"other\")\n";

        Convert(text, 2, 8, ToMatch).Should().Be(
            "class A:\n    def m(self, x: int):\n        match x:\n            case 1:\n                print(\"one\")\n"
            + "            case _:\n                print(\"other\")\n");
    }

    [Fact]
    public void Cell11_MatchToIf_CaseCommentGoesOnItsIf_NestedBodyKept()
    {
        var text = "def main():\n    x = 2\n    match x:\n        case 1:  # one\n            print(\"one\")\n        case 2:\n"
            + "            if x > 0:\n                print(\"two\")\n        case _:\n            print(\"other\")\n";

        Convert(text, 2, 4, ToIf).Should().Be(
            "def main():\n    x = 2\n    if x == 1:  # one\n        print(\"one\")\n    elif x == 2:\n        if x > 0:\n"
            + "            print(\"two\")\n    else:\n        print(\"other\")\n");
    }

    /// <summary>A header continued onto a second line: the body region starts after the colon's line.</summary>
    [Fact]
    public void MultiLineHeader_TheBodyStartsAfterTheColonsLine_AndItsCommentIsKept()
    {
        var text = "def main():\n    x = 1\n    if x == \\\n            1:  # one\n        print(\"one\")\n    else:\n        print(\"other\")\n";

        Convert(text, 2, 4, ToMatch).Should().Be(
            "def main():\n    x = 1\n    match x:\n        case 1:  # one\n            print(\"one\")\n        case _:\n            print(\"other\")\n");
    }

    [Fact]
    public void PassBody_StaysPass()
    {
        var text = "def main():\n    x = 1\n    if x == 1:\n        pass\n    else:\n        print(\"o\")\n";

        Convert(text, 2, 4, ToMatch).Should().Be(
            "def main():\n    x = 1\n    match x:\n        case 1:\n            pass\n        case _:\n            print(\"o\")\n");
    }

    /// <summary>A comment between two `case` clauses at the case column belongs to the preceding body's region and lands at its block's new column.</summary>
    [Fact]
    public void CommentBetweenCaseClauses_MovesWithThePrecedingBody()
    {
        var text = "def main():\n    x = 1\n    match x:\n        case 1:\n            print(\"one\")\n        # between\n        case _:\n            print(\"other\")\n";

        Convert(text, 2, 4, ToIf).Should().Be(
            "def main():\n    x = 1\n    if x == 1:\n        print(\"one\")\n    # between\n    else:\n        print(\"other\")\n");
    }

    /// <summary>D6's CORRECTED carrier: the `match` header's comment becomes an own-line comment above the new `if`.</summary>
    [Fact]
    public void MatchHeaderComment_BecomesAnOwnLineCommentAboveTheIf()
    {
        var text = "def main():\n    x = 1\n    match x:  # m\n        case 1:  # one\n            print(\"one\")\n        case 2:\n            print(\"two\")\n";

        Convert(text, 2, 4, ToIf).Should().Be(
            "def main():\n    x = 1\n    # m\n    if x == 1:  # one\n        print(\"one\")\n    elif x == 2:\n        print(\"two\")\n");
    }

    [Fact]
    public void MultiLineStringInABody_ItsTailLineKeepsItsBytes()
    {
        var text = "def main():\n    x = 1\n    if x == 1:\n        s = \"\"\"a\n  b\"\"\"\n        print(s)\n    elif x == 2:\n        print(\"two\")\n";

        Convert(text, 2, 4, ToMatch).Should().Be(
            "def main():\n    x = 1\n    match x:\n        case 1:\n            s = \"\"\"a\n  b\"\"\"\n            print(s)\n"
            + "        case 2:\n            print(\"two\")\n");
    }
}
