using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Emit-level guard for #1935: <c>bool(())</c> must bind the non-generic
/// <c>Builtins.Bool(ITuple)</c> overload directly, so the emitter records no
/// <c>(object?)</c> argument cast. Before the overload existed the checker selected the
/// nullable <c>Bool(object?)</c> formal and materialized <c>SetArgumentSlotCast</c>, emitting
/// <c>Builtins.Bool((object?)...)</c>; the tuple then fell through to the truthy default at
/// runtime.
/// <para>
/// The assertion is on the syntax tree, not on a spelling: the argument node of the one
/// <c>Bool</c> invocation must not be a <see cref="CastExpressionSyntax"/>. The original guard
/// asserted <c>NotContain("Bool((object?)")</c> on the normalized text and stayed GREEN with
/// <c>Bool(ITuple)</c> deleted — <c>NormalizeWhitespace()</c> spells a nullable cast
/// <c>(object? )</c>, with a space, so the probe never matched the cast it was written to catch
/// (plan-6ca898 verify, measured @ 44cf5dddc; the overload-index cache was ruled out — the
/// mutated host rebuilt its index by reflection and still emitted the cast).
/// </para>
/// <para>
/// The positive control is the #1721 program that legitimately materializes a slot cast:
/// <c>g(1)</c> against <c>g(x: int?)</c> / <c>g(x: int | None)</c> binds the <c>T | None</c> slot
/// and is emitted <c>g((int?)1)</c>. The same cast-node probe must find it there — an absence
/// assertion whose probe never hits proves nothing. (<c>bool(C())</c> is NOT a control: with the
/// ITuple overload present the checker binds it as <c>bool(object)</c> for any non-nullable
/// argument, so no <c>bool</c> call records the cast — measured.)
/// </para>
/// </summary>
[Collection("Sequential")]
public class TupleBoolEmitTests
{
    /// <summary>
    /// The rightmost identifier of a call's callee. The emitter spells <c>global::Sharpy.Builtins.Bool</c>
    /// as a <see cref="QualifiedNameSyntax"/>, not a member access (measured), so both shapes are read.
    /// </summary>
    private static string? CalleeName(InvocationExpressionSyntax call) => call.Expression switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        _ => null,
    };

    /// <summary>The argument expression of the single call to <paramref name="callee"/> in <paramref name="source"/>.</summary>
    private static ExpressionSyntax SingleCallArgument(string source, string callee)
    {
        var unit = EmitterTestPipeline.EmitCompilationUnit(source, isEntryPoint: true, requireNoErrors: true);
        var call = unit.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => CalleeName(invocation) == callee)
            .Should().ContainSingle("the source calls {0} exactly once", callee)
            .Which;
        return call.ArgumentList.Arguments
            .Should().ContainSingle("{0} is called with one argument", callee)
            .Which.Expression;
    }

    [Theory]
    [InlineData("()")]
    [InlineData("(1, 2)")]
    public void BoolOfTuple_BindsITupleOverload_ArgumentIsNotCast(string tuple)
    {
        var source = $$"""
            def main() -> None:
                print(bool({{tuple}}))
            """;

        var argument = SingleCallArgument(source, "Bool");

        argument.Should().NotBeOfType<CastExpressionSyntax>(
            "the ITuple overload is selected, so the emitter records no (object?) cast on the argument (#1935); " +
            "the argument was emitted as `{0}`", argument);
        argument.DescendantNodesAndSelf().OfType<CastExpressionSyntax>().Should().BeEmpty(
            "no cast may be wrapped anywhere around the tuple argument");
    }

    [Fact]
    public void BareValueAtNullableSlotOfOverloadedCallee_ArgumentIsCast_PositiveControl()
    {
        const string source = """
            def g(x: int?) -> str:
                return "opt"
            def g(x: int | None) -> str:
                return "nullable"

            def main() -> None:
                print(g(1))
            """;

        // User functions are emitted PascalCase: `g` is `global::Module.G`.
        var argument = SingleCallArgument(source, "G");

        var cast = argument.Should().BeOfType<CastExpressionSyntax>(
            "a bare value bound to the `int | None` slot of an overloaded callee is emitted cast to that " +
            "slot (#1721) — this is the cast-node probe's positive control; the argument was emitted as `{0}`",
            argument)
            .Which;
        cast.Type.ToString().Should().Be("int?", "the recorded slot is the nullable int formal");
    }
}
