using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// The validator half of P93 (plan-67a01f T4): <c>ProtocolValidator</c> and <c>OperatorValidator</c> validate an
/// operator SITE, so every link of a comparison chain draws its binary twin's verdict (D3, #2648 — SPY0320,
/// SPY0326, SPY0402, at the link's position), and the membership protocol check accepts a class the iteration
/// ring enumerates by reading the checker's record on the container (D1, #2653), while the legacy
/// <c>__getitem__</c> + <c>__len__</c> pair and an <c>__iter__</c> naming no producer stay SPY0320 (R-OA, #1915).
/// </summary>
public class ChainLinkValidationTests
{
    private const string Prelude = """
        class IterBag:
            items: list[int]

            def __init__(self, a: int, b: int) -> None:
                self.items = [a, b]

            def __iter__(self) -> int:
                for v in self.items:
                    yield v

        class GIterBag[T]:
            items: list[T]

            def __init__(self, items: list[T]) -> None:
                self.items = items

            def __iter__(self) -> T:
                for v in self.items:
                    yield v

        class SeqBag:
            items: list[int]

            def __init__(self, a: int, b: int) -> None:
                self.items = [a, b]

            def __len__(self) -> int:
                return len(self.items)

            def __getitem__(self, i: int) -> int:
                return self.items[i]

        class LB:
            def __iter__(self) -> list[int]:
                return [1, 2]

        class Plain:
            pass


        """;

    private sealed record Analysis(Module Module, IReadOnlyList<CompilerDiagnostic> Errors)
    {
        public IReadOnlyList<CompilerDiagnostic> ValidatorErrors
            => Errors.Where(d => d.Phase == CompilerPhase.Validation).ToList();

        public string Describe() => string.Join("; ", Errors.Select(d => $"{d.Code}@{d.Line}:{d.Column} {d.Message}"));
    }

    private static Analysis Analyze(string declarations, string expression)
    {
        var body = string.Concat(declarations
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => $"    {line}\n"));
        var source = Prelude + "def main() -> None:\n" + body + $"    b: bool = {expression}\n    print(b)\n";
        var options = new CompilerOptions
        {
            References = new[] { Sharpy.TestInfrastructure.SharpyCoreReference.Location, SharpyStdlibReference.Location },
            OutputType = "exe",
            WarningsAsErrors = false,
        };
        var result = new Compiler(options, NullLogger.Instance).Analyze(source, "chain_link_validation.spy");
        Assert.True(result.Module != null, "analysis produced no module:\n" + source);
        return new Analysis(result.Module!, result.Diagnostics.GetErrors().ToList());
    }

    private static IEnumerable<Node> Descendants(Node node) =>
        node.GetChildNodes().SelectMany(child => Descendants(child).Prepend(child));

    private static ComparisonChain TestedChain(Analysis analysis) =>
        Descendants(analysis.Module).OfType<ComparisonChain>().Single();

    // ---------------------------------------------------------------- D1: the SPY0320 gate

    /// <summary>
    /// D1 (R-NZ): a class with no <c>__contains__</c> that the iteration ring enumerates is not refused by the
    /// membership protocol check — in a binary operator or a chain link.
    /// </summary>
    [Theory]
    [InlineData("1 in IterBag(1, 2)")]
    [InlineData("1 not in IterBag(1, 2)")]
    [InlineData("1 in GIterBag[int]([1, 2])")]
    [InlineData("0 < 1 in IterBag(1, 2)")]
    public void IteratingClass_PassesTheMembershipProtocolCheck(string expression)
    {
        var analysis = Analyze("", expression);

        Assert.True(analysis.Errors.Count == 0, $"`{expression}` is refused: {analysis.Describe()}");
    }

    /// <summary>
    /// The positive control on the same check: the legacy sequence pair (R-OA), an <c>__iter__</c> that names no
    /// producer (#1915) and a class with neither are SPY0320, with the binary form's message.
    /// </summary>
    [Theory]
    [InlineData("1 in SeqBag(1, 2)", "SeqBag")]
    [InlineData("1 in LB()", "LB")]
    [InlineData("1 in Plain()", "Plain")]
    public void NonIteratingClass_IsRefusedByTheMembershipProtocolCheck(string expression, string type)
    {
        var analysis = Analyze("", expression);

        var error = Assert.Single(analysis.Errors);
        Assert.Equal(DiagnosticCodes.Semantic.ProtocolMissingMethod, error.Code);
        Assert.Equal($"Type '{type}' does not support membership testing (missing '__contains__' method).", error.Message);
    }

    // ---------------------------------------------------------------- D3: a link draws its twin's verdict

    /// <summary>
    /// D3: a chain link whose binary twin a validator refuses is refused by the same code and message, at the
    /// link's position (its left operand). <paramref name="link"/> is the index of the refused link in the chain.
    /// </summary>
    [Theory]
    // ProtocolValidator: SPY0320 for an `in` / `not in` link, SPY0326 for a strict Optional container.
    [InlineData("s: SeqBag = SeqBag(1, 2)", "2 in s", "1 < 2 in s", 1)]
    [InlineData("s: SeqBag = SeqBag(1, 2)", "2 not in s", "1 < 2 not in s", 1)]
    [InlineData("s: SeqBag = SeqBag(1, 2)", "2 in s", "2 in s == True", 0)]
    [InlineData("xs: list[int]? = Some([1])", "2 in xs", "1 < 2 in xs", 1)]
    // OperatorValidator: SPY0402 for an `==` / `!=` link the binary form refuses.
    [InlineData("o: object = 1; p: object = 1", "o == p", "o == p == p", 0)]
    [InlineData("o: object = 1; p: object = 1", "o != p", "p != o != p", 1)]
    [InlineData("q: Plain = Plain(); r: Plain = Plain()", "q == r", "q == r == r", 0)]
    public void ChainLink_IsRefusedAsItsTwin(string declarations, string twinExpression, string chainExpression, int link)
    {
        var twin = Analyze(declarations, twinExpression);
        var chained = Analyze(declarations, chainExpression);

        var expected = Assert.Single(twin.ValidatorErrors);
        var refused = chained.ValidatorErrors.Where(e => e.Code == expected.Code).ToList();
        Assert.True(refused.Count >= 1, $"`{chainExpression}` is not refused as `{twinExpression}` is ({expected.Code}): {chained.Describe()}");
        Assert.All(refused, e => Assert.Equal(expected.Message, e.Message));

        var left = TestedChain(chained).Operands[link];
        Assert.Contains(refused, e => e.Line == left.LineStart && e.Column == left.ColumnStart);
    }
}
