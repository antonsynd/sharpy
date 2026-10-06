using FluentAssertions;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// <see cref="IndentationService.BuildIndentMap"/> surfaces the lexer's
/// <c>LiteralStateUnknown</c> (P22e decision 7, #2168) next to the map it computes.
/// </summary>
public class IndentationServiceLiteralStateTests
{
    /// <summary>D19: a docstring opened ABOVE an existing multi-line string.</summary>
    private const string D19 =
        "def f() -> int:\n    \"\"\"\n    return 1\ndef g() -> str:\n    s = \"\"\"\n        key: value\n    \"\"\"\n    return s\n";

    /// <summary>D19 without the inserted opener: lexes clean.</summary>
    private const string Clean =
        "def f() -> int:\n    return 1\ndef g() -> str:\n    s = \"\"\"\n        key: value\n    \"\"\"\n    return s\n";

    [Fact]
    public void BuildIndentMap_SurfacesTheLexersLiteralState()
    {
        IndentationService.BuildIndentMap(D19).LiteralStateUnknown.Should().BeTrue();
        IndentationService.BuildIndentMap(Clean).LiteralStateUnknown.Should().BeFalse();
    }
}
