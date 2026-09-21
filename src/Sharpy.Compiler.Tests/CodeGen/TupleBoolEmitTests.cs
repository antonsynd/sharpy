using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Emit-level guard for #1935: <c>bool(())</c> must bind the non-generic
/// <c>Builtins.Bool(ITuple)</c> overload directly, so the emitter records no
/// <c>(object?)</c> argument cast. Before the overload existed the checker selected the
/// nullable <c>Bool(object?)</c> formal and materialized <c>SetArgumentSlotCast</c>, emitting
/// <c>Builtins.Bool((object?)...)</c>; the tuple then fell through to the truthy default at
/// runtime. The falsifier is the cast reappearing — deleting <c>Bool(ITuple)</c> reddens this test.
/// </summary>
[Collection("Sequential")]
public class TupleBoolEmitTests
{
    [Fact]
    public void BoolOfEmptyTuple_EmitsBoolCall_WithNoObjectCast()
    {
        const string source = """
            def main() -> None:
                print(bool(()))
            """;

        var csharp = EmitterTestPipeline.CompileToCSharp(source, isEntryPoint: true, requireNoErrors: true);

        csharp.Should().Contain("Builtins.Bool(",
            "bool(()) must lower to a Builtins.Bool call");
        csharp.Should().NotContain("Bool((object?)",
            "the ITuple overload is selected, so the emitter records no (object?) cast on the argument (#1935)");
    }
}
