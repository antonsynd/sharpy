using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Tests.Conformance;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Refactoring;

/// <summary>
/// Organize Imports moves each import as a UNIT — its own-line comment lines and its whole lines, verbatim —
/// and leaves the blank lines after the block alone (P47, #2232, plan D4). Driven through the real
/// code-action handler (<see cref="LspCodeActionDriver"/>) and judged on the byte-exact APPLIED text; every
/// expectation is one the base binary (7f0502de3) gets wrong. Cell numbers are the plan's Current State table.
/// </summary>
public sealed class OrganizeImportsUnitTests : IDisposable
{
    private readonly LspCodeActionDriver _driver = new(new CompilerApi(Sharpy.Compiler.Logging.NullLogger.Instance, StdlibReferences.Paths()));

    public void Dispose() => _driver.Dispose();

    private string Organize(string text)
    {
        using var session = _driver.Open(text);
        var actions = session.Request(new LspRange(new Position(0, 0), new Position(0, 0)));
        var action = actions.Should().ContainSingle(a => a.Title == "Organize Imports").Subject;
        return session.Apply(action);
    }

    /// <summary>
    /// Cell 1, the issue's repro: the comment above the parenthesized import travels with it, the parentheses
    /// and the inner comment stay, `import math`'s trailing comment stays, the blank line before `def main` stays.
    /// </summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Cell1_TheIssuesRepro_MovesUnitsVerbatim(string lineBreak)
    {
        var text = ("import sys\n# string helpers\nfrom os.path import (\n    join,  # path joining\n    basename,\n)\n"
            + "import math  # numeric\n\ndef main():\n    print(math.floor(1.5), join(\"a\", \"b\"), basename(\"/x/y\"), len(sys.argv) >= 0)\n")
            .Replace("\n", lineBreak, StringComparison.Ordinal);

        Organize(text).Should().Be(
            ("import math  # numeric\nimport sys\n# string helpers\nfrom os.path import (\n    join,  # path joining\n    basename,\n)\n"
            + "\ndef main():\n    print(math.floor(1.5), join(\"a\", \"b\"), basename(\"/x/y\"), len(sys.argv) >= 0)\n")
            .Replace("\n", lineBreak, StringComparison.Ordinal));
    }

    /// <summary>Cell 22: the comment above `import math` travels with it; both blank lines after the block stay.</summary>
    [Fact]
    public void Cell22_CommentTravelsWithItsImportAndTheBlankLinesAfterTheBlockStay()
    {
        Organize("import sys\n\n# helpers\nimport math\n\n\ndef main():\n    print(math.floor(1.5), len(sys.argv))\n")
            .Should().Be("# helpers\nimport math\nimport sys\n\n\ndef main():\n    print(math.floor(1.5), len(sys.argv))\n");
    }

    [Fact]
    public void ParenthesizedImportWithAnInnerComment_IsMovedVerbatim()
    {
        Organize("import sys\nfrom os.path import (\n    join,  # j\n    basename,\n)\nimport math\n\n"
                + "def main():\n    print(math.floor(1.5), join(\"a\", \"b\"), basename(\"/x\"), len(sys.argv))\n")
            .Should().Be("import math\nimport sys\nfrom os.path import (\n    join,  # j\n    basename,\n)\n\n"
                + "def main():\n    print(math.floor(1.5), join(\"a\", \"b\"), basename(\"/x\"), len(sys.argv))\n");
    }

    [Fact]
    public void DecoratedImportBetweenPlainOnes_StaysPinnedVerbatim()
    {
        Organize("import sys\n@suppress(\"SPY0452\")\nimport mymodule\nimport math\n\ndef main():\n    print(math.floor(1.5))\n    print(sys.argv)\n")
            .Should().Be("import math\n@suppress(\"SPY0452\")\nimport mymodule\nimport sys\n\ndef main():\n    print(math.floor(1.5))\n    print(sys.argv)\n");
    }

    /// <summary>
    /// An unused parenthesized import loses its lines; the comment above it stays, written before the next
    /// unit the action writes (D4).
    /// </summary>
    [Fact]
    public void UnusedMultiLineImportWithACommentAbove_TheImportGoesTheCommentStays()
    {
        Organize("import sys\n# path helpers\nfrom os.path import (\n    join,\n    basename,\n)\nimport math\n\n"
                + "def main():\n    print(math.floor(1.5), len(sys.argv))\n")
            .Should().Be("import math\n# path helpers\nimport sys\n\ndef main():\n    print(math.floor(1.5), len(sys.argv))\n");
    }

    [Fact]
    public void OneUnusedNameOfTwoOnALine_OnlyThatNameGoes()
    {
        Organize("from os.path import join, basename\nimport sys\n\ndef main():\n    print(join(\"a\", \"b\"), len(sys.argv))\n")
            .Should().Be("import sys\nfrom os.path import join\n\ndef main():\n    print(join(\"a\", \"b\"), len(sys.argv))\n");
    }

    [Fact]
    public void StdlibAndProjectGroups_AreSeparatedByExactlyOneBlankLine_AndTheBlankAfterTheBlockStays()
    {
        Organize("import mymodule\nimport sys\n\ndef main():\n    print(len(sys.argv), mymodule)\n")
            .Should().Be("import sys\n\nimport mymodule\n\ndef main():\n    print(len(sys.argv), mymodule)\n");
    }

    /// <summary>
    /// D4's pinned edge cell (a): the replaced range starts at the first import, so a comment directly above it
    /// is a file header outside the region — it stays at line 0 even though that import sorts later, and
    /// `import sys` lands under it.
    /// </summary>
    [Fact]
    public void CommentAboveTheFirstImport_StaysOutsideTheRegion_WhenThatImportSortsLater()
    {
        Organize("# helpers\nfrom os.path import join\nimport sys\n\ndef main():\n    print(join(\"a\", \"b\"), len(sys.argv))\n")
            .Should().Be("# helpers\nimport sys\nfrom os.path import join\n\ndef main():\n    print(join(\"a\", \"b\"), len(sys.argv))\n");
    }
}
