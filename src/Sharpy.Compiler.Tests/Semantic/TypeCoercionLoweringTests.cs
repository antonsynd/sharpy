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
/// Verifies that the TypeChecker materializes a <see cref="TypeCoercionLowering"/> on numeric casts so
/// the emitter applies the shape without inspecting operand types — the range-checked/widening shapes for
/// the failable form (#1110) and the throwing helper for the checked form (#1306) — and that sources the
/// helpers do not cover (object, optional, class) record nothing, keeping the emitter's default lowering
/// byte-for-byte.
/// </summary>
public class TypeCoercionLoweringTests
{
    [Fact]
    public void FloatToIntOptional_RecordsRangeCheckedToInt()
    {
        var lowering = CoercionLowering(@"
def f(x: float) -> int?:
    return x as? int
");

        lowering.Should().NotBeNull("float -> int? narrows and must lower via a range-checked helper");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericRangeChecked);
        lowering.HelperMethod.Should().Be("ToIntOrNone");
    }

    [Fact]
    public void DoubleToLongOptional_RecordsRangeCheckedToLong()
    {
        var lowering = CoercionLowering(@"
def f(x: float) -> long?:
    return x as? long
");

        lowering.Should().NotBeNull();
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericRangeChecked);
        lowering.HelperMethod.Should().Be("ToLongOrNone");
    }

    [Fact]
    public void IntToFloatOptional_RecordsAlwaysFits()
    {
        var lowering = CoercionLowering(@"
def f(x: int) -> float?:
    return x as? float
");

        lowering.Should().NotBeNull("int -> float? widens and always fits");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericAlwaysFits);
        lowering.HelperMethod.Should().BeNull();
    }

    [Fact]
    public void IdentityIntToIntOptional_RecordsAlwaysFits()
    {
        var lowering = CoercionLowering(@"
def f(x: int) -> int?:
    return x as? int
");

        lowering.Should().NotBeNull("int -> int? is identity and always fits");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericAlwaysFits);
    }

    [Fact]
    public void LongToIntOptional_RecordsRangeChecked()
    {
        var lowering = CoercionLowering(@"
def f(x: long) -> int?:
    return x as? int
");

        lowering.Should().NotBeNull("long -> int? narrows");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericRangeChecked);
        lowering.HelperMethod.Should().Be("ToIntOrNone");
    }

    [Fact]
    public void DoubleToFloat32Optional_RecordsAlwaysFits()
    {
        // The spec's designed edge case: double -> float32 always fits — overflow maps to ±inf and
        // NaN is preserved (IEEE semantics), so there is no None case and no range check.
        var lowering = CoercionLowering(@"
def f(x: float) -> float32?:
    return x as? float32
");

        lowering.Should().NotBeNull("float -> float32? always fits under IEEE semantics");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericAlwaysFits);
        lowering.HelperMethod.Should().BeNull();
    }

    [Fact]
    public void Float32ToIntOptional_RecordsRangeCheckedToInt()
    {
        var lowering = CoercionLowering(@"
def f(x: float32) -> int?:
    return x as? int
");

        lowering.Should().NotBeNull("float32 -> int? narrows and must lower via a range-checked helper");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericRangeChecked);
        lowering.HelperMethod.Should().Be("ToIntOrNone");
    }

    [Fact]
    public void ObjectSource_RecordsNothing()
    {
        // Unboxing from object keeps the default type-pattern lowering (`obj is int _t ? ... : default`),
        // which is legal for object sources — the numeric guard must not fire here.
        CoercionLowering(@"
def f(x: object) -> int?:
    return x as? int
").Should().BeNull();
    }

    [Fact]
    public void OptionalSource_RecordsNothing()
    {
        // An optional source is not a plain numeric BuiltinType, so no numeric lowering is recorded.
        CoercionLowering(@"
def f(x: int?) -> int?:
    return x as? int
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
    return a as? Dog
").Should().BeNull();
    }

    // --- Throwing form (`as! T`), #1306 ----------------------------------------------------------

    [Fact]
    public void LongToIntChecked_RecordsCheckedToInt()
    {
        var lowering = CoercionLowering(@"
def f(x: long) -> int:
    return x as! int
");

        lowering.Should().NotBeNull("long -> int narrows and a bare C# cast would wrap silently");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericChecked);
        lowering.HelperMethod.Should().Be("ToInt");
        lowering.SourceHubType.Should().BeNull("long is already a hub, so no cast is needed");
    }

    [Fact]
    public void DoubleToIntChecked_RecordsCheckedToInt()
    {
        var lowering = CoercionLowering(@"
def f(x: float) -> int:
    return x as! int
");

        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericChecked);
        lowering.HelperMethod.Should().Be("ToInt");
        lowering.SourceHubType.Should().BeNull();
    }

    [Fact]
    public void Float32ToIntChecked_CastsToTheDoubleHub()
    {
        var lowering = CoercionLowering(@"
def f(x: float32) -> int:
    return x as! int
");

        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericChecked);
        lowering.HelperMethod.Should().Be("ToInt");
        lowering.SourceHubType.Should().Be("double", "float widens losslessly to the double hub");
    }

    [Fact]
    public void WideningChecked_RecordsNothing()
    {
        // A widening `as!` cannot fail, so it keeps its bare C# cast — no helper, no output change.
        CoercionLowering(@"
def f(x: int) -> long:
    return x as! long
").Should().BeNull();
    }

    [Fact]
    public void ObjectSourceChecked_RecordsNothing()
    {
        // Unboxing keeps the bare cast, which throws InvalidCastException — not a numeric narrowing.
        CoercionLowering(@"
def f(x: object) -> int:
    return x as! int
").Should().BeNull();
    }

    // --- The widths beyond int/long, both modes (#1306) -------------------------------------------

    [Fact]
    public void IntToByteOptional_RecordsRangeCheckedToByte()
    {
        // Before the matrix widened, a byte target recorded nothing and the emitter's type pattern
        // produced CS8121 on a concrete numeric source — `x as? byte` was an ICE, not a cast.
        var lowering = CoercionLowering(@"
def f(x: int) -> byte?:
    return x as? byte
");

        lowering.Should().NotBeNull("int -> byte? narrows");
        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericRangeChecked);
        lowering.HelperMethod.Should().Be("ToByteOrNone");
        lowering.SourceHubType.Should().Be("long");
    }

    [Fact]
    public void IntToByteChecked_RecordsCheckedToByte()
    {
        var lowering = CoercionLowering(@"
def f(x: int) -> byte:
    return x as! byte
");

        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericChecked);
        lowering.HelperMethod.Should().Be("ToByte");
        lowering.SourceHubType.Should().Be("long");
    }

    [Fact]
    public void ULongToLongChecked_CastsToTheULongHub()
    {
        // uint64 is the one integral source with no implicit conversion to long, so it is its own hub.
        var lowering = CoercionLowering(@"
def f(x: uint64) -> long:
    return x as! long
");

        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericChecked);
        lowering.HelperMethod.Should().Be("ToLong");
        lowering.SourceHubType.Should().BeNull("ulong is a hub of its own");
    }

    [Fact]
    public void UIntToIntChecked_CastsToTheLongHub()
    {
        // uint fits in long, so it rides the long hub — and the explicit cast is what keeps the call
        // from being CS0121-ambiguous between the long and ulong overloads.
        var lowering = CoercionLowering(@"
def f(x: uint32) -> int:
    return x as! int
");

        lowering!.Kind.Should().Be(TypeCoercionLoweringKind.NumericChecked);
        lowering.HelperMethod.Should().Be("ToInt");
        lowering.SourceHubType.Should().Be("long");
    }

    [Fact]
    public void ByteToIntChecked_RecordsNothing()
    {
        // uint8 -> int is a widening: every byte value is an int.
        CoercionLowering(@"
def f(x: uint8) -> int:
    return x as! int
").Should().BeNull();
    }

    [Fact]
    public void IntToFloat32Checked_RecordsNothing()
    {
        // A floating target never fails — precision loss is not a range failure.
        CoercionLowering(@"
def f(x: int) -> float32:
    return x as! float32
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
