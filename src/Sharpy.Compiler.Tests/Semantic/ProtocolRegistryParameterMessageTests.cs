using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Every fixed-count protocol dunder's SPY0320 count message names the dunder's OWN parameter list.
/// The two-parameter default was the binary-operator spelling <c>(self, other)</c>, so
/// <c>__format__</c> (#2009), the one two-parameter row without an arm, was told it needed an
/// <c>other</c> (cells-b F13). The expected lists are literals — python's data model spellings — and
/// the table must cover exactly the registry's fixed-count rows, so a new row without a message arm
/// fails here rather than inheriting a neighbour's parameters.
/// </summary>
public class ProtocolRegistryParameterMessageTests : IntegrationTestBase
{
    public ProtocolRegistryParameterMessageTests(ITestOutputHelper output) : base(output) { }

    private static readonly Dictionary<string, string> ExpectedCountClause = new()
    {
        ["__post_init__"] = "exactly 1 parameter (self)",
        ["__len__"] = "exactly 1 parameter (self)",
        ["__contains__"] = "exactly 2 parameters (self, item)",
        ["__getitem__"] = "exactly 2 parameters (self, index)",
        ["__setitem__"] = "exactly 3 parameters (self, index, value)",
        ["__iter__"] = "exactly 1 parameter (self)",
        ["__next__"] = "exactly 1 parameter (self)",
        ["__str__"] = "exactly 1 parameter (self)",
        ["__repr__"] = "exactly 1 parameter (self)",
        ["__format__"] = "exactly 2 parameters (self, format_spec: str)",
        ["__hash__"] = "exactly 1 parameter (self)",
        ["__bool__"] = "exactly 1 parameter (self)",
        ["__reversed__"] = "exactly 1 parameter (self)",
        ["__enter__"] = "exactly 1 parameter (self)",
        ["__exit__"] = "exactly 1 parameter (self) or 4 parameters (self, exc_type, exc_val, exc_tb)",
        ["__aenter__"] = "exactly 1 parameter (self)",
        ["__aexit__"] = "exactly 1 parameter (self) or 4 parameters (self, exc_type, exc_val, exc_tb)",
    };

    public static IEnumerable<object[]> Rows() => ExpectedCountClause.Keys.Select(k => new object[] { k });

    [Fact]
    public void Table_CoversExactlyTheRegistrysFixedCountRows()
    {
        ProtocolRegistry.GetAllProtocols()
            .Where(p => p.ExpectedParamCount >= 1)
            .Select(p => p.DunderName)
            .Should().BeEquivalentTo(ExpectedCountClause.Keys);
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void WrongParameterCount_NamesTheDundersOwnParameters(string dunder)
    {
        // Six parameters: no protocol row accepts that count.
        var source = $"class F:\n    def {dunder}(self, a: int, b: int, c: int, d: int, e: int) -> None:\n        pass\n\n"
            + "def main() -> None:\n    print(1)\n";
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.ProtocolMissingMethod
                && d.Message.StartsWith($"Protocol method '{dunder}' on 'F' must have {ExpectedCountClause[dunder]}, got 6."),
            $"[{dunder}] {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
    }
}
