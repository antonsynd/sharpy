using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Lsp.Tests.Conformance;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Refactoring;

/// <summary>
/// The movers move whole source regions (P47, #2232, plan Phase 2 Task 2 — D2, D5, D7, D8): Wrap in
/// try/except, Extract method, Inline variable and Remove unused import, driven through the real
/// code-action handler (<see cref="LspCodeActionDriver"/>) and judged on the byte-exact APPLIED text.
/// Every expectation here is a text the base binary (7f0502de3) gets wrong — the plan's probe cells
/// 3, 5, 6, 7, 13 and the edge cells of its Testing Strategy — except the one row marked as a direction
/// guard. Cell numbers are the plan's Current State table.
/// </summary>
public sealed class RegionMoverTests : IDisposable
{
    private readonly LspCodeActionDriver _driver = new(new CompilerApi(Sharpy.Compiler.Logging.NullLogger.Instance, StdlibReferences.Paths()));

    public void Dispose() => _driver.Dispose();

    private string Apply(string text, int sl, int sc, int el, int ec, string titlePrefix, bool unusedImportContext = false)
    {
        using var session = _driver.Open(text);
        var context = unusedImportContext
            ? session.Diagnostics.Where(d => d.Code?.String == DiagnosticCodes.Validation.UnusedImport).ToArray()
            : Array.Empty<Diagnostic>();
        var actions = session.Request(new LspRange(new Position(sl, sc), new Position(el, ec)), context);
        var action = actions.FirstOrDefault(a => a.Title.StartsWith(titlePrefix, StringComparison.Ordinal));
        action.Should().NotBeNull($"'{titlePrefix}' is offered; offered: {string.Join(" | ", actions.Select(a => a.Title))}");
        return session.Apply(action!);
    }

    // ---- Wrap in try/except (D7) ----

    [Fact]
    public void Wrap_Cell3_NestedBodyKeepsItsRelativeIndentation()
    {
        var text = "def main():\n    x = 1\n    if x > 0:  # positive\n        print(x)\n    print(\"done\")\n";

        Apply(text, 1, 0, 3, 16, "Wrap in try/except").Should().Be(
            "def main():\n    try:\n        x = 1\n        if x > 0:  # positive\n            print(x)\n"
            + "    except Exception as e:\n        raise\n    print(\"done\")\n");
    }

    [Fact]
    public void Wrap_Cell5_CommentBlankContinuationAndLiteralTailMoveWithTheRegion()
    {
        var text = "def f(p: int, q: int) -> int:\n    return p + q\n\ndef main():\n    a = 1\n    # between\n    b = f(a,\n          2)\n\n    s = \"\"\"x\n  y\"\"\"\n    print(a, b, s)\n";

        Apply(text, 4, 0, 10, 6, "Wrap in try/except").Should().Be(
            "def f(p: int, q: int) -> int:\n    return p + q\n\ndef main():\n    try:\n        a = 1\n        # between\n"
            + "        b = f(a,\n              2)\n\n        s = \"\"\"x\n  y\"\"\"\n    except Exception as e:\n        raise\n"
            + "    print(a, b, s)\n");
    }

    [Fact]
    public void Wrap_Cell6_TrailingCommentOfTheLastLineStays()
    {
        var text = "def main():\n    x = 1\n    if x > 0:\n        print(x)  # tail\n    print(\"done\")\n";

        Apply(text, 1, 0, 3, 24, "Wrap in try/except").Should().Be(
            "def main():\n    try:\n        x = 1\n        if x > 0:\n            print(x)  # tail\n"
            + "    except Exception as e:\n        raise\n    print(\"done\")\n");
    }

    [Fact]
    public void Wrap_Cell7_ModuleLevelNestedBodyKeepsItsRelativeIndentation()
    {
        var text = "x = 1\nif x > 0:\n    print(x)\nprint(\"done\")\n";

        Apply(text, 0, 0, 2, 12, "Wrap in try/except").Should().Be(
            "try:\n    x = 1\n    if x > 0:\n        print(x)\nexcept Exception as e:\n    raise\nprint(\"done\")\n");
    }

    [Fact]
    public void Wrap_LastStatementWithNoTrailingBreak_EndsAtTheLineEndWithoutAddingABreak()
    {
        Apply("x = 1\nprint(x)", 0, 0, 1, 8, "Wrap in try/except").Should().Be(
            "try:\n    x = 1\n    print(x)\nexcept Exception as e:\n    raise");
    }

    [Fact]
    public void Wrap_DocumentWhoseOnlyBreakIsALoneCrAtTheEnd_WritesThatBreak()
    {
        Apply("print(1)\r", 0, 0, 0, 8, "Wrap in try/except").Should().Be(
            "try:\r    print(1)\rexcept Exception as e:\r    raise\r");
    }

    // ---- Extract method (D8) ----

    /// <summary>
    /// Cell 13's content axes (own-line comment, blank line, bracket continuation, multi-line literal tail)
    /// on a document whose region reads no outer name: the plan's cell-13 document also draws #2294's
    /// parameter inference (an <c>int32</c>-spelled parameter, a free <c>a</c>), which this test must not pin.
    /// </summary>
    [Fact]
    public void ExtractMethod_Cell13_TheBodyIsTheRegionVerbatimAndTheLiteralTailMovesWithIt()
    {
        var text = "def main():\n    a = \"x\"\n    # between\n    b = [\"y\",\n         \"z\"]\n\n    s = \"\"\"p\n  q\"\"\"\n    print(a, b, s)\n";

        Apply(text, 1, 0, 7, 6, "Extract method").Should().Be(
            "def extracted_method() -> tuple[str, list[str], str]:\n    a = \"x\"\n    # between\n    b = [\"y\",\n"
            + "         \"z\"]\n\n    s = \"\"\"p\n  q\"\"\"\n    return a, b, s\n\ndef main():\n    a, b, s = extracted_method()\n"
            + "    print(a, b, s)\n");
    }

    [Fact]
    public void ExtractMethod_ContinuationAtColumnZeroInsideBrackets_ClampsAtColumnZeroUnderANegativeOffset()
    {
        // The base strips the region's indent only from lines that start with it, so the column-0 line gained
        // the body indent (`    "b"]`); one uniform −4 offset clamps it at column 0 instead.
        var text = "def main():\n    if True:\n        y = [\"a\",\n\"b\"]\n        print(1)\n";

        Apply(text, 2, 0, 3, 4, "Extract method").Should().Be(
            "def extracted_method():\n    y = [\"a\",\n\"b\"]\n\ndef main():\n    if True:\n        extracted_method()\n        print(1)\n");
    }

    [Fact]
    public void ExtractMethod_LastStatementWithNoTrailingBreak_ReplacesThroughTheLiteralTail()
    {
        Apply("s = \"\"\"a\n  b\"\"\"\nt = s", 0, 0, 1, 6, "Extract method").Should().Be(
            "def extracted_method():\n    s = \"\"\"a\n  b\"\"\"\n    return s\n\ns = extracted_method()\nt = s");
    }

    // ---- Inline variable (D2, D5) ----

    /// <summary>
    /// Cell 16 retargeted: the base deletes the declaration line with its trailing comment correctly, so the
    /// discriminating document is one whose initializer is a multi-line string — the base does not offer the
    /// action at all (its node text was null past the string's first line); now the declaration's lines go
    /// through the string's tail, its trailing comment with them (D5).
    /// </summary>
    [Fact]
    public void InlineVariable_Cell16_MultiLineStringInitializer_DeletesThroughItsTail()
    {
        var text = "def main():\n    s: str = \"\"\"a\n  b\"\"\"  # s\n    print(s)\n";

        Apply(text, 1, 4, 1, 4, "Inline variable").Should().Be("def main():\n    print(\"\"\"a\n  b\"\"\")\n");
    }

    // ---- Remove unused import (D2, D5) ----

    [Fact]
    public void RemoveUnusedImport_ParenthesizedMultiLineImportAllUnused_DeletesTheWholeStatement()
    {
        var text = "import sys\nfrom os.path import (\n    join,\n    basename,\n)\n\ndef main():\n    print(len(sys.argv))\n";

        Apply(text, 2, 4, 2, 4, "Remove unused import", unusedImportContext: true).Should().Be(
            "import sys\n\ndef main():\n    print(len(sys.argv))\n");
    }

    [Fact]
    public void RemoveUnusedImport_ParenthesizedSingleName_DeletesTheWholeStatement()
    {
        var text = "import sys\nfrom os.path import (\n    basename\n)\n\ndef main():\n    print(len(sys.argv))\n";

        Apply(text, 2, 4, 2, 4, "Remove unused import", unusedImportContext: true).Should().Be(
            "import sys\n\ndef main():\n    print(len(sys.argv))\n");
    }

    [Fact]
    public void RemoveUnusedImport_OneOfTwoNamesOnALine_DeletesOnlyThatName()
    {
        var text = "from os.path import join, basename\n\ndef main():\n    print(join(\"a\", \"b\"))\n";

        Apply(text, 0, 0, 0, 0, "Remove unused import", unusedImportContext: true).Should().Be(
            "from os.path import join\n\ndef main():\n    print(join(\"a\", \"b\"))\n");
    }

    /// <summary>
    /// Direction guard, not a discriminating cell: the base already deletes only this name's own line. It
    /// pins that deleting "through the import statement" never takes a still-used name with it.
    /// </summary>
    [Fact]
    public void RemoveUnusedImport_ParenthesizedImportKeepingAUsedName_DeletesOnlyTheUnusedNamesLine()
    {
        var text = "import sys\nfrom os.path import (\n    join,\n    basename,\n)\n\ndef main():\n    print(len(sys.argv), join(\"a\", \"b\"))\n";

        Apply(text, 3, 4, 3, 4, "Remove unused import", unusedImportContext: true).Should().Be(
            "import sys\nfrom os.path import (\n    join,\n)\n\ndef main():\n    print(len(sys.argv), join(\"a\", \"b\"))\n");
    }

    [Fact]
    public void RemoveUnusedImport_LastLineWithNoTrailingBreak_DeletesThePrecedingBreak()
    {
        Apply("def main():\n    print(1)\nimport math", 2, 0, 2, 0, "Remove unused import", unusedImportContext: true)
            .Should().Be("def main():\n    print(1)");
    }
}
