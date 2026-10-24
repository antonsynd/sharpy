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

    /// <summary>Every sample reads a name nothing defines (SPY0200): the failure names SPY0200 and the source.</summary>
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
        Assert.Contains("SPY0200", text, StringComparison.Ordinal);
        Assert.Contains("undefined_", text, StringComparison.Ordinal);
    }
}
