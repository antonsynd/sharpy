using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Verifies that the TypeChecker materializes a <see cref="TypeCoercionLowering"/> on a failable numeric
/// safe cast (<c>value to T?</c>) so the emitter applies the range-checked/widening shape without
/// inspecting operand types (#1110), and that non-plain-numeric sources (object, optional, class) record
/// nothing — keeping the emitter's default type-pattern lowering byte-for-byte.
/// </summary>
public class TypeCoercionLoweringTests
{
    [Fact]
    public void FloatToIntOptional_RecordsRangeCheckedToInt()
    {
        var lowering = CoercionLowering(@"
def f(x: float) -> int?:
    return x to int?
");

        lowering.Should().NotBeNull("float -> int? narrows and must lower via a range-checked helper");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericRangeChecked);
        lowering.SafeCastMethod.Should().Be("ToIntOrNone");
    }

    [Fact]
    public void DoubleToLongOptional_RecordsRangeCheckedToLong()
    {
        var lowering = CoercionLowering(@"
def f(x: float) -> long?:
    return x to long?
");

        lowering.Should().NotBeNull();
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericRangeChecked);
        lowering.SafeCastMethod.Should().Be("ToLongOrNone");
    }

    [Fact]
    public void IntToFloatOptional_RecordsAlwaysFits()
    {
        var lowering = CoercionLowering(@"
def f(x: int) -> float?:
    return x to float?
");

        lowering.Should().NotBeNull("int -> float? widens and always fits");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericAlwaysFits);
        lowering.SafeCastMethod.Should().BeNull();
    }

    [Fact]
    public void IdentityIntToIntOptional_RecordsAlwaysFits()
    {
        var lowering = CoercionLowering(@"
def f(x: int) -> int?:
    return x to int?
");

        lowering.Should().NotBeNull("int -> int? is identity and always fits");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericAlwaysFits);
    }

    [Fact]
    public void LongToIntOptional_RecordsRangeChecked()
    {
        var lowering = CoercionLowering(@"
def f(x: long) -> int?:
    return x to int?
");

        lowering.Should().NotBeNull("long -> int? narrows");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericRangeChecked);
        lowering.SafeCastMethod.Should().Be("ToIntOrNone");
    }

    [Fact]
    public void DoubleToFloat32Optional_RecordsAlwaysFits()
    {
        // The spec's designed edge case: double -> float32 always fits — overflow maps to ±inf and
        // NaN is preserved (IEEE semantics), so there is no None case and no range check.
        var lowering = CoercionLowering(@"
def f(x: float) -> float32?:
    return x to float32?
");

        lowering.Should().NotBeNull("float -> float32? always fits under IEEE semantics");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericAlwaysFits);
        lowering.SafeCastMethod.Should().BeNull();
    }

    [Fact]
    public void Float32ToIntOptional_RecordsRangeCheckedToInt()
    {
        var lowering = CoercionLowering(@"
def f(x: float32) -> int?:
    return x to int?
");

        lowering.Should().NotBeNull("float32 -> int? narrows and must lower via a range-checked helper");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericRangeChecked);
        lowering.SafeCastMethod.Should().Be("ToIntOrNone");
    }

    [Fact]
    public void ObjectSource_RecordsNothing()
    {
        // Unboxing from object keeps the default type-pattern lowering (`obj is int _t ? ... : default`),
        // which is legal for object sources — the numeric guard must not fire here.
        CoercionLowering(@"
def f(x: object) -> int?:
    return x to int?
").Should().BeNull();
    }

    [Fact]
    public void OptionalSource_RecordsNothing()
    {
        // An optional source is not a plain numeric BuiltinType, so no numeric lowering is recorded.
        CoercionLowering(@"
def f(x: int?) -> int?:
    return x to int?
").Should().BeNull();
    }

    [Fact]
    public void ClassToClassCast_RecordsNothing()
    {
        CoercionLowering(@"
class Animal:
    pass

class Dog(Animal):
    pass

def f(a: Animal) -> Dog?:
    return a to Dog?
").Should().BeNull();
    }

    // --- Harness -------------------------------------------------------------------------------

    private static TypeCoercionLowering? CoercionLowering(string source)
    {
        var (module, info) = Analyze(source);

        var coercions = Descendants(module).OfType<TypeCoercion>().ToList();
        coercions.Should().ContainSingle("each probe has exactly one safe cast");

        return info.GetTypeCoercionLowering(coercions[0]);
    }

    private static (Module Module, SemanticInfo Info) Analyze(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var pipeline = ValidationPipelineFactory.CreateDefault(NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance, pipeline)
        {
            SemanticBinding = semanticBinding
        };

        typeChecker.CheckModule(module);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty(
            "safe-cast probe programs must type-check cleanly");

        return (module, semanticInfo);
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
}
