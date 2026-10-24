using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// The generated lane's filter fails loudly (#2324, P63b Decision 13): a generator whose every sample the
/// compiler refuses fails with the sink's report — the refusal's code and the first refused source — not
/// with CsCheck's "Failing Where max count", which names neither.
/// </summary>
[Trait("Category", "Property")]
[Collection("PropertySerial")]
public class RejectionSinkTests
{
    private readonly ITestOutputHelper _output;

    public RejectionSinkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Every sample reads a name nothing defines: the failure names the sink's refusal codes and the source.</summary>
    [Fact]
    public void AnAlwaysRefusedGenerator_FailsWithTheSinksReport()
    {
        var refused = Gen.Int[0, 9].Select(n => new Module
        {
            Body = ImmutableArray.Create<Statement>(new FunctionDef
            {
                Name = "main",
                Body = ImmutableArray.Create<Statement>(new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = ImmutableArray.Create<Expression>(new Identifier { Name = $"undefined_{n}" }),
                    },
                }),
            }),
        });
        var sink = new RejectionSink("RejectionSinkTests.always refused");

        var thrown = Record.Exception(() => SemanticFilter.WellTypedProgram(refused, sink).Sample(_ => { }, iter: 1));

        var text = thrown?.ToString() ?? "(nothing thrown)";
        _output.WriteLine(text.Length > 2000 ? text[..2000] : text);
        // The failure carries the sink's own bucket (its refusal codes, read from the sink — a code literal
        // here would be a second judging core) and a refused source.
        Assert.Contains("unexplained by bucket: " + sink.Unexplained[0].Describe(), text, StringComparison.Ordinal);
        Assert.Contains("undefined_", text, StringComparison.Ordinal);
    }
}
