using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Semantic-side recording tests for <see cref="MultiAxisAccessLowering"/> (#1621, #1644, plan
/// c6ae1b D0c/D6): <c>CheckMultiAxisAccess</c> records the access kind and one kind per
/// dimension for a classified (ndarray) receiver, and records <b>nothing</b> for a refused
/// receiver — while still checking every dimension, so nested diagnostics survive the refusal.
/// Every parser-produced <see cref="MultiAxisAccess"/> has at least one slice dimension, so
/// every recorded fact here is a <see cref="MultiAxisAccessKind.SliceCall"/>; the all-index
/// spelling is pinned as what it really is (a tuple-index <see cref="IndexAccess"/>).
///
/// <para>
/// The ndarray receiver needs <c>Sharpy.Stdlib</c>; the test loads the copy next to the test
/// assembly through <see cref="CompilerApi"/> (the same route <c>ManglingCollisionGridTests</c>
/// uses), so this runs in Compiler.Tests without a Stdlib project reference. Classification keys
/// on the CLR full name (<c>IsNdArrayType</c>), which is why the positive cases also assert the
/// program analyzed cleanly: an unresolved <c>numpy</c> would refuse with SPY0602 and the
/// lowering assertions would fail for the wrong reason.
/// </para>
/// </summary>
public class MultiAxisAccessLoweringRecordingTests
{
    private static CompilerApi Api()
    {
        var binDir = Path.GetDirectoryName(typeof(MultiAxisAccessLoweringRecordingTests).Assembly.Location)!;
        return new CompilerApi(NullLogger.Instance, new[]
        {
            Path.Combine(binDir, "Sharpy.Core.dll"),
            Path.Combine(binDir, "Sharpy.Stdlib.dll"),
        });
    }

    private static SemanticResult Analyze(string source)
    {
        var result = Api().Analyze(source);
        result.Ast.Should().NotBeNull("the source must parse");
        result.SemanticInfo.Should().NotBeNull("semantic analysis must run");
        return result;
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildNodes())
        {
            yield return child;
            foreach (var d in Descendants(child))
                yield return d;
        }
    }

    private static MultiAxisAccess SingleMultiAxis(SemanticResult result)
        => Descendants(result.Ast!).OfType<MultiAxisAccess>().Single();

    private static string Describe(SemanticResult result)
        => string.Join(" ;; ", result.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));

    private const string NdArrayPrelude = @"
import numpy as np

def main() -> None:
    a = np.array([[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]])
";

    [Fact]
    public void AllIndexSubscript_IsNotMultiAxis_ItIsATupleIndexAccess()
    {
        // The parser produces a MultiAxisAccess only when at least one dimension is a slice
        // (Parser.Expressions.cs ParseSliceOrIndexCore); `a[1, 2]` is an IndexAccess whose index
        // is the tuple (1, 2), lowered through the ndarray's CLR indexer like any other index
        // read. So MultiAxisAccessKind.IndexSpread is reachable only from a constructed AST —
        // this pins the parser side of that boundary so the emitter's IndexSpread arm and the
        // Stdlib ndarray_multi_axis_index_1621 fixture are read for what they are.
        var result = Analyze(NdArrayPrelude + "    x = a[1, 2]\n");

        result.Diagnostics.Where(d => d.IsError)
            .Should().BeEmpty(Describe(result));
        Descendants(result.Ast!).OfType<MultiAxisAccess>().Should().BeEmpty();
        var access = Descendants(result.Ast!).OfType<IndexAccess>()
            .Single(ia => ia.Object is Identifier { Name: "a" });
        access.Index.Should().BeOfType<TupleLiteral>();
    }

    [Theory]
    [InlineData("a[0:2, 1]", new[] { MultiAxisDimensionKind.Slice, MultiAxisDimensionKind.Index })]
    [InlineData("a[1, ::2]", new[] { MultiAxisDimensionKind.Index, MultiAxisDimensionKind.Slice })]
    [InlineData("a[0:2, 1:3]", new[] { MultiAxisDimensionKind.Slice, MultiAxisDimensionKind.Slice })]
    [InlineData("a[:, 1]", new[] { MultiAxisDimensionKind.Slice, MultiAxisDimensionKind.Index })]
    public void AnySliceDimension_RecordsSliceCallWithPerDimensionKinds(
        string subscript, MultiAxisDimensionKind[] expectedKinds)
    {
        var result = Analyze(NdArrayPrelude + $"    x = {subscript}\n");

        result.Diagnostics.Where(d => d.IsError)
            .Should().BeEmpty(Describe(result));
        var lowering = result.SemanticInfo!.GetMultiAxisAccessLowering(SingleMultiAxis(result));
        lowering.Should().NotBeNull();
        lowering!.Kind.Should().Be(MultiAxisAccessKind.SliceCall);
        lowering.Dimensions.Should().Equal(expectedKinds);
    }

    [Fact]
    public void RefusedReceiver_RecordsNoLowering_AndStillReportsNestedDiagnostics()
    {
        // A list receiver is refused (SPY0602). The dimension holds a wrong-typed call whose
        // SPY0220 must still be reported: the refusal never swallows the operand checks (#1644).
        var result = Analyze(@"
def probe(n: int) -> int:
    return n

def main() -> None:
    xs: list[int] = [1, 2, 3]
    x = xs[probe(""s""), 1:2]
");

        var codes = result.Diagnostics.Select(d => d.Code).ToList();
        codes.Should().Contain(DiagnosticCodes.SemanticOverflow.MultiAxisNotSupported, Describe(result));
        codes.Should().Contain(DiagnosticCodes.Semantic.TypeMismatch, Describe(result));
        result.SemanticInfo!.GetMultiAxisAccessLowering(SingleMultiAxis(result))
            .Should().BeNull("a refused receiver must not carry a lowering the emitter could act on");
    }

    [Fact]
    public void RefusedReceiver_PositiveControl_SameProgramWithNdArrayIsClean()
    {
        // The refusal case above proves nothing if numpy failed to load; the same subscript on an
        // ndarray analyzes cleanly and records a fact.
        var result = Analyze(@"
import numpy as np

def probe(n: int) -> int:
    return n

def main() -> None:
    a = np.array([[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]])
    x = a[probe(0), 1:2]
");

        result.Diagnostics.Where(d => d.IsError)
            .Should().BeEmpty(Describe(result));
        result.SemanticInfo!.GetMultiAxisAccessLowering(SingleMultiAxis(result))
            .Should().NotBeNull();
    }
}
