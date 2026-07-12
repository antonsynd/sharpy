using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1052: the TypeChecker tags a list index access <see cref="IndexAccessLowering.NativeUnchecked"/>
/// only when the index is provably &gt;= 0 — a non-negative int literal, or a <c>range(...)</c>-loop
/// induction variable that is not reassigned in the loop body. Everything else stays
/// <see cref="IndexAccessLowering.Native"/> (or its container-specific lowering) so the emitter keeps
/// the negative-index Normalize. These tests assert the materialized tag directly.
/// </summary>
public class NonNegativeIndexLoweringTests
{
    private static (Module module, SemanticInfo info) Analyze(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module, isEntryPoint: false);

        return (module, semanticInfo);
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

    /// <summary>
    /// The lowering recorded for the (single) <c>obj[...]</c> access whose object is <paramref name="objectName"/>.
    /// </summary>
    private static IndexAccessLowering LoweringOf(string source, string objectName)
    {
        var (module, info) = Analyze(source);
        var access = Descendants(module).OfType<IndexAccess>()
            .Single(ia => ia.Object is Identifier id && id.Name == objectName);
        return info.GetIndexAccessLowering(access);
    }

    [Fact]
    public void NonNegativeLiteralIndex_IsNativeUnchecked()
    {
        var source = @"
def main() -> None:
    xs: list[int] = [1, 2, 3]
    print(xs[0])
";
        LoweringOf(source, "xs").Should().Be(IndexAccessLowering.NativeUnchecked);
    }

    [Fact]
    public void NegativeLiteralIndex_StaysNative()
    {
        var source = @"
def main() -> None:
    xs: list[int] = [1, 2, 3]
    print(xs[-1])
";
        LoweringOf(source, "xs").Should().Be(IndexAccessLowering.Native);
    }

    [Fact]
    public void RangeLoopInductionVar_NotReassigned_IsNativeUnchecked()
    {
        var source = @"
def main() -> None:
    xs: list[int] = [1, 2, 3]
    for i in range(len(xs)):
        print(xs[i])
";
        LoweringOf(source, "xs").Should().Be(IndexAccessLowering.NativeUnchecked);
    }

    [Fact]
    public void RangeLoopInductionVar_Reassigned_StaysNative()
    {
        var source = @"
def main() -> None:
    xs: list[int] = [1, 2, 3]
    for i in range(len(xs)):
        i = i + 1
        print(xs[i])
";
        LoweringOf(source, "xs").Should().Be(IndexAccessLowering.Native);
    }

    [Fact]
    public void RangeLoopInductionVar_ReassignedInNestedBlock_StaysNative()
    {
        var source = @"
def main() -> None:
    xs: list[int] = [1, 2, 3]
    for i in range(len(xs)):
        if i == 0:
            i = 2
        print(xs[i])
";
        LoweringOf(source, "xs").Should().Be(IndexAccessLowering.Native);
    }

    [Fact]
    public void RangeStartStop_NonNegativeLiteralStart_IsNativeUnchecked()
    {
        var source = @"
def main() -> None:
    xs: list[int] = [1, 2, 3, 4]
    for i in range(1, 3):
        print(xs[i])
";
        LoweringOf(source, "xs").Should().Be(IndexAccessLowering.NativeUnchecked);
    }

    [Fact]
    public void RangeStartStop_NegativeLiteralStart_StaysNative()
    {
        var source = @"
def main() -> None:
    xs: list[int] = [1, 2, 3, 4]
    for i in range(-1, 3):
        print(xs[i])
";
        LoweringOf(source, "xs").Should().Be(IndexAccessLowering.Native);
    }

    [Fact]
    public void RangeStartStopStep_NegativeStep_StaysNative()
    {
        var source = @"
def main() -> None:
    xs: list[int] = [1, 2, 3, 4]
    for i in range(3, 0, -1):
        print(xs[i])
";
        LoweringOf(source, "xs").Should().Be(IndexAccessLowering.Native);
    }

    [Fact]
    public void NonListContainer_NonNegativeLiteral_IsNotNativeUnchecked()
    {
        // Dict access is not a list; the fast path (which skips list negative-index Normalize) must
        // not apply. A dict key lookup is Native regardless of the key's sign.
        var source = @"
def main() -> None:
    d: dict[int, int] = {0: 10}
    print(d[0])
";
        LoweringOf(source, "d").Should().Be(IndexAccessLowering.Native);
    }

    [Fact]
    public void ShadowedRange_InductionVar_StaysNative()
    {
        // A user-defined range shadows the builtin, so its yielded values are not provably >= 0.
        var source = @"
def range(n: int) -> list[int]:
    return [n]

def main() -> None:
    xs: list[int] = [1, 2, 3]
    for i in range(2):
        print(xs[i])
";
        LoweringOf(source, "xs").Should().Be(IndexAccessLowering.Native);
    }
}
