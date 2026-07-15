using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests that "did you mean?" suggestions are exposed as machine-readable diagnostic
/// <c>Data["suggestedName"]</c> alongside the human-readable message (Wave 4 Phase 14,
/// borrowing-list "diagnostics-with-fixes"). The LSP quick-fix registry consumes this key
/// to offer a rename fix, mirroring the <c>NamingConventionValidator</c> pattern.
/// The message text itself is unchanged — this is purely additive data.
/// </summary>
public class IdentifierSuggestionDataTests
{
    private readonly CompilerApi _api = new();

    private const string SuggestedNameKey = "suggestedName";

    [Fact]
    public void Undefined_identifier_with_close_match_carries_suggestedName_data()
    {
        // 'countr' is undefined but 'counter' (a visible local) is one edit away.
        var result = _api.Compile("def main():\n    counter = 0\n    print(countr)");

        var diag = result.Diagnostics.Single(
            d => d.IsError && d.Code == DiagnosticCodes.Semantic.UndefinedVariable);

        diag.Data.Should().NotBeNull("the undefined-identifier diagnostic must carry rename data");
        diag.Data!.Should().ContainKey(SuggestedNameKey);
        diag.Data[SuggestedNameKey].Should().Be("counter");
    }

    [Fact]
    public void Suggestion_data_matches_the_message_text()
    {
        // The machine-readable data must agree with the "Did you mean 'X'?" message text,
        // so a quick-fix that consumes the data offers exactly what the message promises.
        var result = _api.Compile("def main():\n    counter = 0\n    print(countr)");

        var diag = result.Diagnostics.Single(
            d => d.IsError && d.Code == DiagnosticCodes.Semantic.UndefinedVariable);

        diag.Message.Should().Contain($"Did you mean '{diag.Data![SuggestedNameKey]}'?");
    }

    [Fact]
    public void Undefined_identifier_with_no_close_match_carries_no_suggestion_data()
    {
        // Nothing visible is within edit distance of this name → no suggestion, no data key.
        var result = _api.Compile("def main():\n    print(zzzqqqwww)");

        var diag = result.Diagnostics.Single(
            d => d.IsError && d.Code == DiagnosticCodes.Semantic.UndefinedVariable);

        (diag.Data == null || !diag.Data.ContainsKey(SuggestedNameKey))
            .Should().BeTrue("no suggestion means no suggestedName data key");
    }

    [Fact]
    public void Undefined_member_with_close_match_carries_suggestedName_data()
    {
        // Accessing 'widht' on a type whose field is 'width' (two edits away).
        var source =
            "class Widget:\n" +
            "    width: int = 0\n" +
            "\n" +
            "def main():\n" +
            "    w: Widget = Widget()\n" +
            "    print(w.widht)";

        var result = _api.Compile(source);

        var diag = result.Diagnostics.Single(
            d => d.IsError && d.Code == DiagnosticCodes.Semantic.UndefinedMember);

        diag.Data.Should().NotBeNull();
        diag.Data!.Should().ContainKey(SuggestedNameKey);
        diag.Data[SuggestedNameKey].Should().Be("width");
    }
}
