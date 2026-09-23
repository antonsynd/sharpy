using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1967 residue: a type argument written in EXPRESSION position (<c>list[tuple[()]]()</c>,
/// <c>Box[()](x)</c>, <c>ident[tuple[()]](x)</c>) is parsed as an ordinary expression, so the
/// parser's annotation normalization of <c>tuple[()]</c> never reaches it; the expression-as-type
/// resolver must read the empty <c>()</c> as the zero-arity tuple itself. An empty <c>()</c> is
/// the zero-arity tuple as an argument or element, and the empty element LIST only directly under
/// <c>tuple</c>. These rows read the recorded <see cref="GenericReference"/> vector — the fact the
/// emitter spells the C# type arguments from.
/// </summary>
public class ExpressionTypeArgumentZeroArityTupleTests
{
    private static readonly TupleType ZeroArityTuple = new() { ElementTypes = new List<SemanticType>() };

    private static (Module module, SemanticInfo info, DiagnosticBag diagnostics) Analyze(string source)
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

        return (module, semanticInfo, typeChecker.Diagnostics);
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildNodes())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    /// <summary>The generic reference recorded for the callee of the single call to <paramref name="calleeName"/>.</summary>
    private static GenericReference CalleeReference(string source, string calleeName)
    {
        var (module, info, diagnostics) = Analyze(source);
        diagnostics.GetErrors().Should().BeEmpty("every row is a program that must type-check");

        var callee = Descendants(module).OfType<FunctionCall>()
            .Select(call => call.Function)
            .OfType<IndexAccess>()
            .Single(ia => ia.Object is Identifier { } id && id.Name == calleeName);
        var reference = info.GetGenericReference(callee);
        reference.Should().NotBeNull($"{calleeName}[...] is a resolved generic reference");
        return reference!;
    }

    private const string Box = @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value
";

    [Theory]
    [InlineData("xs = list[tuple[()]]()", "list")]
    [InlineData("xs = list[()]()", "list")]
    [InlineData("xs: list[tuple[()]] = list[tuple[()]]()", "list")]
    [InlineData("b = Box[tuple[()]](())", "Box")]
    [InlineData("b = Box[()](())", "Box")]
    public void EmptyParensTypeArgument_IsOneZeroArityTupleArgument(string statement, string calleeName)
    {
        var reference = CalleeReference($"{Box}\ndef use() -> None:\n    {statement}\n", calleeName);

        reference.Kind.Should().Be(GenericReferenceKind.GenericTypeRef);
        reference.TypeArgs.Should().ContainSingle()
            .Which.Should().Be(ZeroArityTuple, "`()` as a type argument is the zero-arity tuple (#1967)");
    }

    [Theory]
    [InlineData("y = ident[tuple[()]](())")]
    [InlineData("y = ident[()](())")]
    public void EmptyParensFunctionTypeArgument_IsOneZeroArityTupleArgument(string statement)
    {
        var reference = CalleeReference(
            $"def ident[T](x: T) -> T:\n    return x\n\ndef use() -> None:\n    {statement}\n", "ident");

        reference.Kind.Should().Be(GenericReferenceKind.UserFunction);
        reference.TypeArgs.Should().ContainSingle().Which.Should().Be(ZeroArityTuple);
    }

    [Theory]
    [InlineData("d = dict[str, tuple[()]]()")]
    [InlineData("d = dict[str, ()]()")]
    public void EmptyParensElementOfMultiArgumentList_IsZeroArityTuple(string statement)
    {
        var reference = CalleeReference($"def use() -> None:\n    {statement}\n", "dict");

        reference.TypeArgs.Should().Equal(SemanticType.Str, ZeroArityTuple);
    }

    [Theory]
    [InlineData("a = array[()](2)")]
    [InlineData("a = array[tuple[()]](2)")]
    public void EmptyParensArrayElement_IsZeroArityTuple(string statement)
    {
        var reference = CalleeReference($"def use() -> None:\n    {statement}\n", "array");

        reference.Kind.Should().Be(GenericReferenceKind.ArrayTypeRef);
        reference.TypeArgs.Should().ContainSingle().Which.Should().Be(ZeroArityTuple,
            "the emitter spells the array element type from this recorded argument (#2003)");
    }

    [Fact]
    public void TupleOfEmptyParens_IsTheZeroArityTupleReference()
    {
        var reference = CalleeReference("def use() -> None:\n    t = tuple[()](())\n", "tuple");

        reference.Kind.Should().Be(GenericReferenceKind.TupleTypeRef);
        reference.TypeArgs.Should().BeEmpty("directly under `tuple`, `()` is the empty element list");
    }

    [Fact]
    public void Control_TupleOfTupleOfEmptyParens_IsOneElement()
    {
        var reference = CalleeReference("def use() -> None:\n    t = tuple[tuple[()]](((),))\n", "tuple");

        reference.Kind.Should().Be(GenericReferenceKind.TupleTypeRef);
        reference.TypeArgs.Should().ContainSingle().Which.Should().Be(ZeroArityTuple);
    }

    [Fact]
    public void Control_ListOfTupleOfTupleOfEmptyParens_ElementIsOneTuple()
    {
        var reference = CalleeReference("def use() -> None:\n    xs = list[tuple[tuple[()]]]()\n", "list");

        reference.TypeArgs.Should().ContainSingle()
            .Which.Should().Be(new TupleType { ElementTypes = new List<SemanticType> { ZeroArityTuple } });
    }

    [Fact]
    public void Control_TwoArgumentList_StaysTwoArguments()
    {
        var reference = CalleeReference(@"
class Pair[K, V]:
    first: K
    second: V

    def __init__(self, first: K, second: V):
        self.first = first
        self.second = second

def use() -> None:
    p = Pair[int, str](1, ""a"")
", "Pair");

        reference.TypeArgs.Should().Equal(SemanticType.Int, SemanticType.Str);
    }

    [Fact]
    public void Control_TupleTypeArgument_StaysTwoElementTuple()
    {
        var reference = CalleeReference("def use() -> None:\n    xs = list[tuple[int, int]]()\n", "list");

        reference.TypeArgs.Should().ContainSingle().Which.Should().Be(
            new TupleType { ElementTypes = new List<SemanticType> { SemanticType.Int, SemanticType.Int } });
    }
}
