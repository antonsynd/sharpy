using System.Collections.Immutable;
using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.TestInfrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// <see cref="TypeSymbol.RefusedDeclarations"/> (P21h D7, R-JO) holds exactly the property and event
/// declarations the member seam refused, by node identity, and none it admitted — one cell per refusal
/// arm of <c>NameResolver.ResolvePropertyDeclaration</c> / <c>ResolveEventDeclaration</c>: a second
/// meaning of the name (SPY0204), a duplicate accessor half (SPY0204), the interface abstract half
/// beside a bodied one (SPY0617). The legal merges are the positive controls: every half admitted,
/// the set empty.
/// </summary>
public class RefusedDeclarationsTests
{
    private readonly CompilerApi _api = new(null, new[] { SharpyCoreReference.Location });

    public static TheoryData<string, string, int[], string?> Cells() => new()
    {
        // A property over a field: the seam's holder refusal.
        { "property over a field", "class Host:\n    A: int = 1\n    property A: int = 2\n", new[] { 3 }, "SPY0204" },
        // A function-style getter twice: Admits refuses the filled slot.
        {
            "duplicate getter half",
            "class Host:\n    property get A(self) -> int:\n        return 1\n    property get A(self) -> int:\n        return 2\n",
            new[] { 4 }, "SPY0204"
        },
        // An event over a property.
        {
            "event over a property",
            "from system import Action\n\nclass Host:\n    property A: int = 1\n    event A: Action[int]\n",
            new[] { 5 }, "SPY0204"
        },
        // An auto event twice: an auto event fills both slots and takes no other half.
        {
            "auto event twice",
            "from system import Action\n\nclass Host:\n    event A: Action[int]\n    event A: Action[int]\n",
            new[] { 5 }, "SPY0204"
        },
        // R-JG: an interface's abstract auto half beside a bodied getter.
        {
            "interface abstract beside bodied",
            "interface Host:\n    property A: int\n    property get A(self) -> int:\n        return 6\n",
            new[] { 3 }, "SPY0617"
        },
        // Positive controls: legal merges admit every half.
        {
            "function-style getter + setter",
            "class Host:\n    property get A(self) -> int:\n        return 1\n    property set A(self, v: int):\n        print(v)\n",
            Array.Empty<int>(), null
        },
        {
            "interface auto half + stub getter",
            "interface Host:\n    property A: int\n    property get A(self) -> int: ...\n",
            Array.Empty<int>(), null
        },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    public void RefusedDeclarations_HoldsTheRefusedHalves_AndNoAdmittedOne(
        string cell, string source, int[] refusedLines, string? code)
    {
        var analysis = _api.Analyze(source);
        var errors = analysis.Diagnostics.Where(d => d.IsError).Select(d => $"{d.Code}@{d.Line}").ToList();
        errors.Should().BeEquivalentTo(
            refusedLines.Select(line => $"{code}@{line}"),
            $"{cell}: the seam refuses exactly the listed declarations");

        var host = analysis.SymbolTable!.LookupType("Host");
        host.Should().NotBeNull($"{cell}: Host is declared");

        var body = analysis.Ast!.Body.Select(s => s switch
        {
            ClassDef { Name: "Host" } c => c.Body,
            InterfaceDef { Name: "Host" } i => i.Body,
            _ => (ImmutableArray<Statement>?)null,
        }).Single(b => b != null)!.Value;
        var halves = body.Where(s => s is PropertyDef or EventDef).ToList();
        halves.Should().NotBeEmpty($"{cell}: the body declares property or event halves");

        var expected = halves.Where(s => refusedLines.Contains(s.LineStart)).ToList();
        expected.Should().HaveCount(refusedLines.Length, $"{cell}: every listed line is a property or event half");

        host!.RefusedDeclarations.Should().HaveCount(expected.Count, $"{cell}: only the refused halves are recorded");
        foreach (var half in halves)
            host.RefusedDeclarations.Contains(half).Should().Be(
                expected.Any(e => ReferenceEquals(e, half)),
                $"{cell}: the half at line {half.LineStart} is {(refusedLines.Contains(half.LineStart) ? "refused" : "admitted")}");
    }
}
