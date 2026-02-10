using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for the SynthesisAnalyzer class which computes synthesized interfaces
/// from dunder method definitions on TypeSymbol instances.
/// </summary>
public class SynthesisAnalyzerTests
{
    // ==================== Helper Methods ====================

    /// <summary>
    /// Creates a minimal TypeSymbol with the given protocol and operator methods.
    /// </summary>
    private static TypeSymbol CreateTypeSymbol(
        string name = "TestType",
        Dictionary<string, List<FunctionSymbol>>? protocolMethods = null,
        Dictionary<string, List<FunctionSymbol>>? operatorMethods = null)
    {
        return new TypeSymbol
        {
            Name = name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            ProtocolMethods = protocolMethods ?? new(),
            OperatorMethods = operatorMethods ?? new(),
        };
    }

    /// <summary>
    /// Creates a FunctionSymbol with the given parameters and return type.
    /// </summary>
    private static FunctionSymbol CreateFunctionSymbol(
        string name,
        List<ParameterSymbol>? parameters = null,
        SemanticType? returnType = null)
    {
        return new FunctionSymbol
        {
            Name = name,
            Kind = SymbolKind.Function,
            Parameters = parameters ?? new List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown }
            },
            ReturnType = returnType ?? SemanticType.Unknown,
        };
    }

    // ==================== Phase 1: Sharpy.Core Protocol Interfaces ====================

    [Fact]
    public void ComputeSynthesizedInterfaces_LenDunder_ProducesISized()
    {
        // Arrange
        var lenFunc = CreateFunctionSymbol(DunderNames.Len, returnType: SemanticType.Int);
        var typeSymbol = CreateTypeSymbol(
            protocolMethods: new()
            {
                [DunderNames.Len] = new List<FunctionSymbol> { lenFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert
        result.Should().ContainSingle();
        var info = result[0];
        info.InterfaceName.Should().Be("ISized");
        info.Namespace.Should().Be("Sharpy");
        info.TypeArgs.Should().BeEmpty();
        info.TriggeringDunder.Should().Be(DunderNames.Len);
    }

    [Fact]
    public void ComputeSynthesizedInterfaces_BoolDunder_ProducesIBoolConvertible()
    {
        // Arrange
        var boolFunc = CreateFunctionSymbol(DunderNames.Bool, returnType: SemanticType.Bool);
        var typeSymbol = CreateTypeSymbol(
            protocolMethods: new()
            {
                [DunderNames.Bool] = new List<FunctionSymbol> { boolFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert
        result.Should().ContainSingle();
        var info = result[0];
        info.InterfaceName.Should().Be("IBoolConvertible");
        info.Namespace.Should().Be("Sharpy");
        info.TypeArgs.Should().BeEmpty();
        info.TriggeringDunder.Should().Be(DunderNames.Bool);
    }

    // ==================== Phase 2: Iterator Interfaces ====================

    [Fact]
    public void ComputeSynthesizedInterfaces_NextDunder_ProducesIEnumeratorWithElementType()
    {
        // Arrange: __next__ returning str should synthesize IEnumerator<str>
        var nextFunc = CreateFunctionSymbol(DunderNames.Next, returnType: SemanticType.Str);
        var typeSymbol = CreateTypeSymbol(
            protocolMethods: new()
            {
                [DunderNames.Next] = new List<FunctionSymbol> { nextFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert: Only IEnumerator<str> (no IEnumerable without __iter__)
        result.Should().ContainSingle();
        var info = result[0];
        info.InterfaceName.Should().Be("IEnumerator");
        info.Namespace.Should().Be("System.Collections.Generic");
        info.TypeArgs.Should().HaveCount(1);
        info.TypeArgs[0].Should().Be(SemanticType.Str);
        info.TriggeringDunder.Should().Be(DunderNames.Next);
    }

    [Fact]
    public void ComputeSynthesizedInterfaces_IterAndNextDunders_ProducesBothIEnumeratorAndIEnumerable()
    {
        // Arrange: __iter__ + __next__ returning str should synthesize both
        var nextFunc = CreateFunctionSymbol(DunderNames.Next, returnType: SemanticType.Str);
        var iterFunc = CreateFunctionSymbol(DunderNames.Iter);
        var typeSymbol = CreateTypeSymbol(
            protocolMethods: new()
            {
                [DunderNames.Next] = new List<FunctionSymbol> { nextFunc },
                [DunderNames.Iter] = new List<FunctionSymbol> { iterFunc },
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert: Should have IEnumerator<str> and IEnumerable<str>
        var enumerator = result.Should().Contain(i => i.InterfaceName == "IEnumerator").Which;
        enumerator.Namespace.Should().Be("System.Collections.Generic");
        enumerator.TypeArgs.Should().HaveCount(1);
        enumerator.TypeArgs[0].Should().Be(SemanticType.Str);
        enumerator.TriggeringDunder.Should().Be(DunderNames.Next);

        var enumerable = result.Should().Contain(i => i.InterfaceName == "IEnumerable").Which;
        enumerable.Namespace.Should().Be("System.Collections.Generic");
        enumerable.TypeArgs.Should().HaveCount(1);
        enumerable.TypeArgs[0].Should().Be(SemanticType.Str);
        enumerable.TriggeringDunder.Should().Be(DunderNames.Iter);
    }

    [Fact]
    public void ComputeSynthesizedInterfaces_NextWithUnknownReturnType_FallsBackToObject()
    {
        // Arrange: __next__ with UnknownType should fall back to object as element type
        var nextFunc = CreateFunctionSymbol(DunderNames.Next, returnType: SemanticType.Unknown);
        var typeSymbol = CreateTypeSymbol(
            protocolMethods: new()
            {
                [DunderNames.Next] = new List<FunctionSymbol> { nextFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert
        result.Should().ContainSingle();
        var info = result[0];
        info.InterfaceName.Should().Be("IEnumerator");
        info.TypeArgs.Should().HaveCount(1);
        info.TypeArgs[0].Should().BeOfType<UserDefinedType>();
        ((UserDefinedType)info.TypeArgs[0]).Name.Should().Be("object");
    }

    // ==================== Phase 3: IEquatable from __eq__ ====================

    [Fact]
    public void ComputeSynthesizedInterfaces_EqDunderWithUserType_ProducesIEquatable()
    {
        // Arrange: __eq__(self, other: Point) should synthesize IEquatable<Point>
        var pointType = new UserDefinedType { Name = "Point" };
        var eqFunc = CreateFunctionSymbol(
            DunderNames.Eq,
            parameters: new List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown },
                new() { Name = "other", Type = pointType },
            },
            returnType: SemanticType.Bool);

        var typeSymbol = CreateTypeSymbol(
            operatorMethods: new()
            {
                [DunderNames.Eq] = new List<FunctionSymbol> { eqFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert
        result.Should().ContainSingle();
        var info = result[0];
        info.InterfaceName.Should().Be("IEquatable");
        info.Namespace.Should().Be("System");
        info.TypeArgs.Should().HaveCount(1);
        info.TypeArgs[0].Should().BeOfType<UserDefinedType>();
        ((UserDefinedType)info.TypeArgs[0]).Name.Should().Be("Point");
        info.TriggeringDunder.Should().Be(DunderNames.Eq);
    }

    [Fact]
    public void ComputeSynthesizedInterfaces_EqDunderWithObjectType_SkipsIEquatable()
    {
        // Arrange: __eq__(self, other: object) should NOT synthesize IEquatable
        // because object-typed __eq__ maps to override Equals(object), not IEquatable<T>
        var objectType = new UserDefinedType { Name = "object" };
        var eqFunc = CreateFunctionSymbol(
            DunderNames.Eq,
            parameters: new List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown },
                new() { Name = "other", Type = objectType },
            },
            returnType: SemanticType.Bool);

        var typeSymbol = CreateTypeSymbol(
            operatorMethods: new()
            {
                [DunderNames.Eq] = new List<FunctionSymbol> { eqFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeSynthesizedInterfaces_EqDunderWithUnknownType_SkipsIEquatable()
    {
        // Arrange: __eq__(self, other: <unresolved>) should NOT synthesize IEquatable
        var eqFunc = CreateFunctionSymbol(
            DunderNames.Eq,
            parameters: new List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown },
                new() { Name = "other", Type = SemanticType.Unknown },
            },
            returnType: SemanticType.Bool);

        var typeSymbol = CreateTypeSymbol(
            operatorMethods: new()
            {
                [DunderNames.Eq] = new List<FunctionSymbol> { eqFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert
        result.Should().BeEmpty();
    }

    // ==================== Empty / No Dunders ====================

    [Fact]
    public void ComputeSynthesizedInterfaces_NoDunders_ReturnsEmptyList()
    {
        // Arrange
        var typeSymbol = CreateTypeSymbol();

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert
        result.Should().BeEmpty();
    }

    // ==================== Combined Scenarios ====================

    [Fact]
    public void ComputeSynthesizedInterfaces_MultipleDunders_ProducesAllInterfaces()
    {
        // Arrange: A type with __len__, __bool__, __next__, __iter__, __eq__(self, other: Point)
        var lenFunc = CreateFunctionSymbol(DunderNames.Len, returnType: SemanticType.Int);
        var boolFunc = CreateFunctionSymbol(DunderNames.Bool, returnType: SemanticType.Bool);
        var nextFunc = CreateFunctionSymbol(DunderNames.Next, returnType: SemanticType.Int);
        var iterFunc = CreateFunctionSymbol(DunderNames.Iter);

        var pointType = new UserDefinedType { Name = "Point" };
        var eqFunc = CreateFunctionSymbol(
            DunderNames.Eq,
            parameters: new List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown },
                new() { Name = "other", Type = pointType },
            },
            returnType: SemanticType.Bool);

        var typeSymbol = CreateTypeSymbol(
            protocolMethods: new()
            {
                [DunderNames.Len] = new List<FunctionSymbol> { lenFunc },
                [DunderNames.Bool] = new List<FunctionSymbol> { boolFunc },
                [DunderNames.Next] = new List<FunctionSymbol> { nextFunc },
                [DunderNames.Iter] = new List<FunctionSymbol> { iterFunc },
            },
            operatorMethods: new()
            {
                [DunderNames.Eq] = new List<FunctionSymbol> { eqFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert: ISized, IBoolConvertible, IEnumerator<int>, IEnumerable<int>, IEquatable<Point>
        result.Should().HaveCount(5);
        result.Should().Contain(i => i.InterfaceName == "ISized");
        result.Should().Contain(i => i.InterfaceName == "IBoolConvertible");
        result.Should().Contain(i => i.InterfaceName == "IEnumerator");
        result.Should().Contain(i => i.InterfaceName == "IEnumerable");
        result.Should().Contain(i => i.InterfaceName == "IEquatable");
    }

    // ==================== Non-Synthesizable Protocol Methods ====================

    [Fact]
    public void ComputeSynthesizedInterfaces_StrDunder_NotSynthesized()
    {
        // Arrange: __str__ maps to IStrConvertible, but it is NOT in SynthesizableSharpyCoreInterfaces
        var strFunc = CreateFunctionSymbol(DunderNames.Str, returnType: SemanticType.Str);
        var typeSymbol = CreateTypeSymbol(
            protocolMethods: new()
            {
                [DunderNames.Str] = new List<FunctionSymbol> { strFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert: __str__ should not produce any synthesized interface
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeSynthesizedInterfaces_IterAloneWithoutNext_DoesNotProduceIEnumerable()
    {
        // Arrange: __iter__ without __next__ should not produce IEnumerable
        var iterFunc = CreateFunctionSymbol(DunderNames.Iter);
        var typeSymbol = CreateTypeSymbol(
            protocolMethods: new()
            {
                [DunderNames.Iter] = new List<FunctionSymbol> { iterFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert
        result.Should().BeEmpty();
    }

    // ==================== SynthesizableSharpyCoreInterfaces Registry ====================

    [Fact]
    public void SynthesizableSharpyCoreInterfaces_ContainsExpectedInterfaces()
    {
        SynthesisAnalyzer.SynthesizableSharpyCoreInterfaces.Should().Contain("ISized");
        SynthesisAnalyzer.SynthesizableSharpyCoreInterfaces.Should().Contain("IBoolConvertible");
    }

    [Fact]
    public void SynthesizableSharpyCoreInterfaces_DoesNotContainNonSynthesizable()
    {
        // These interfaces exist in ProtocolRegistry but should NOT be auto-synthesized
        SynthesisAnalyzer.SynthesizableSharpyCoreInterfaces.Should().NotContain("IStrConvertible");
        SynthesisAnalyzer.SynthesizableSharpyCoreInterfaces.Should().NotContain("IHashable");
        SynthesisAnalyzer.SynthesizableSharpyCoreInterfaces.Should().NotContain("IContainer");
        SynthesisAnalyzer.SynthesizableSharpyCoreInterfaces.Should().NotContain("IIterable");
    }

    // ==================== Edge Cases ====================

    [Fact]
    public void ComputeSynthesizedInterfaces_EqWithOnlySelfParam_SkipsIEquatable()
    {
        // Arrange: __eq__(self) with no "other" parameter
        var eqFunc = CreateFunctionSymbol(
            DunderNames.Eq,
            parameters: new List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown },
            },
            returnType: SemanticType.Bool);

        var typeSymbol = CreateTypeSymbol(
            operatorMethods: new()
            {
                [DunderNames.Eq] = new List<FunctionSymbol> { eqFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeSynthesizedInterfaces_EqWithBuiltinType_ProducesIEquatable()
    {
        // Arrange: __eq__(self, other: int) should synthesize IEquatable<int>
        var eqFunc = CreateFunctionSymbol(
            DunderNames.Eq,
            parameters: new List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown },
                new() { Name = "other", Type = SemanticType.Int },
            },
            returnType: SemanticType.Bool);

        var typeSymbol = CreateTypeSymbol(
            operatorMethods: new()
            {
                [DunderNames.Eq] = new List<FunctionSymbol> { eqFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert
        result.Should().ContainSingle();
        var info = result[0];
        info.InterfaceName.Should().Be("IEquatable");
        info.Namespace.Should().Be("System");
        info.TypeArgs.Should().HaveCount(1);
        info.TypeArgs[0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ComputeSynthesizedInterfaces_NextWithIntReturn_ProducesIEnumeratorOfInt()
    {
        // Arrange: __next__ returning int should synthesize IEnumerator<int>
        var nextFunc = CreateFunctionSymbol(DunderNames.Next, returnType: SemanticType.Int);
        var typeSymbol = CreateTypeSymbol(
            protocolMethods: new()
            {
                [DunderNames.Next] = new List<FunctionSymbol> { nextFunc }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert
        result.Should().ContainSingle();
        var info = result[0];
        info.InterfaceName.Should().Be("IEnumerator");
        info.TypeArgs.Should().HaveCount(1);
        info.TypeArgs[0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ComputeSynthesizedInterfaces_EmptyOverloadList_HandledGracefully()
    {
        // Arrange: __next__ with empty overload list should not crash
        var typeSymbol = CreateTypeSymbol(
            protocolMethods: new()
            {
                [DunderNames.Next] = new List<FunctionSymbol>()
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert: Empty overload list produces nothing (FirstOrDefault returns null)
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeSynthesizedInterfaces_MultipleEqOverloads_SynthesizesOnlyNonObjectOverloads()
    {
        // Arrange: a class with both __eq__(self, other: Point) and __eq__(self, other: object)
        // This is the canonical pattern — typed overload for IEquatable<T>, object overload for Equals(object).
        // Only the typed overload should produce IEquatable<Point>; the object overload is skipped.
        var pointType = new UserDefinedType { Name = "Point" };
        var objectType = new UserDefinedType { Name = "object" };

        var eqPoint = CreateFunctionSymbol(
            DunderNames.Eq,
            parameters: new List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown },
                new() { Name = "other", Type = pointType },
            },
            returnType: SemanticType.Bool);

        var eqObject = CreateFunctionSymbol(
            DunderNames.Eq,
            parameters: new List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown },
                new() { Name = "other", Type = objectType },
            },
            returnType: SemanticType.Bool);

        var typeSymbol = CreateTypeSymbol(
            operatorMethods: new()
            {
                [DunderNames.Eq] = new List<FunctionSymbol> { eqPoint, eqObject }
            });

        // Act
        var result = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Assert: only IEquatable<Point>, not IEquatable<object>
        result.Should().ContainSingle();
        var info = result[0];
        info.InterfaceName.Should().Be("IEquatable");
        info.Namespace.Should().Be("System");
        info.TypeArgs.Should().HaveCount(1);
        info.TypeArgs[0].Should().BeOfType<UserDefinedType>();
        ((UserDefinedType)info.TypeArgs[0]).Name.Should().Be("Point");
        info.TriggeringDunder.Should().Be(DunderNames.Eq);
    }
}
