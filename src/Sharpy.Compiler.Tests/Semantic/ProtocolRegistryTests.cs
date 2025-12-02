using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for the ProtocolRegistry class which provides protocol dunder mappings
/// to Sharpy.Core interfaces and .NET methods.
/// </summary>
public class ProtocolRegistryTests
{
    // ==================== Test Protocol Registration ====================

    [Theory]
    [InlineData("__init__", ProtocolKind.Lifecycle)]
    [InlineData("__len__", ProtocolKind.Container)]
    [InlineData("__contains__", ProtocolKind.Container)]
    [InlineData("__getitem__", ProtocolKind.Container)]
    [InlineData("__setitem__", ProtocolKind.Container)]
    [InlineData("__delitem__", ProtocolKind.Container)]
    [InlineData("__iter__", ProtocolKind.Iterator)]
    [InlineData("__next__", ProtocolKind.Iterator)]
    [InlineData("__str__", ProtocolKind.Representation)]
    [InlineData("__repr__", ProtocolKind.Representation)]
    [InlineData("__hash__", ProtocolKind.Hashing)]
    [InlineData("__bool__", ProtocolKind.Conversion)]
    public void GetProtocol_ReturnsCorrectKind(string dunderName, ProtocolKind expectedKind)
    {
        var protocol = ProtocolRegistry.GetProtocol(dunderName);
        protocol.Should().NotBeNull();
        protocol!.Kind.Should().Be(expectedKind);
    }

    [Fact]
    public void GetProtocol_ReturnsNullForUnknownDunder()
    {
        var protocol = ProtocolRegistry.GetProtocol("__unknown__");
        protocol.Should().BeNull();
    }

    [Fact]
    public void GetProtocol_ReturnsNullForOperatorDunder()
    {
        // Operator dunders are handled by OperatorSignatureValidator, not ProtocolRegistry
        var protocol = ProtocolRegistry.GetProtocol("__add__");
        protocol.Should().BeNull();
    }

    // ==================== Test Interface Mappings ====================

    [Theory]
    [InlineData("__len__", "ISized")]
    [InlineData("__contains__", "IContainer")]
    [InlineData("__getitem__", "ISequence")]
    [InlineData("__setitem__", "IMutableSequence")]
    [InlineData("__delitem__", "IMutableSequence")]
    [InlineData("__iter__", "IIterable")]
    [InlineData("__str__", "IStrConvertible")]
    [InlineData("__repr__", "IRepresentable")]
    [InlineData("__hash__", "IHashable")]
    [InlineData("__bool__", "IBoolConvertible")]
    public void GetProtocol_ReturnsCorrectInterface(string dunderName, string expectedInterface)
    {
        var protocol = ProtocolRegistry.GetProtocol(dunderName);
        protocol.Should().NotBeNull();
        protocol!.SharpyCoreInterface.Should().Be(expectedInterface);
    }

    [Theory]
    [InlineData("__init__")]  // Maps to constructor, no interface
    [InlineData("__next__")]  // Part of Iterator<T> class, not an interface
    public void GetProtocol_ReturnsNullInterfaceForSpecialCases(string dunderName)
    {
        var protocol = ProtocolRegistry.GetProtocol(dunderName);
        protocol.Should().NotBeNull();
        protocol!.SharpyCoreInterface.Should().BeNull();
    }

    // ==================== Test CLR Method Mappings ====================

    [Theory]
    [InlineData("__init__", ".ctor")]
    [InlineData("__len__", "get_Count")]
    [InlineData("__contains__", "Contains")]
    [InlineData("__getitem__", "get_Item")]
    [InlineData("__setitem__", "set_Item")]
    [InlineData("__iter__", "GetEnumerator")]
    [InlineData("__str__", "ToString")]
    [InlineData("__hash__", "GetHashCode")]
    [InlineData("__bool__", "op_Explicit")]
    public void GetProtocol_ReturnsCorrectClrMethodName(string dunderName, string expectedClrMethod)
    {
        var protocol = ProtocolRegistry.GetProtocol(dunderName);
        protocol.Should().NotBeNull();
        protocol!.ClrMethodName.Should().Be(expectedClrMethod);
    }

    [Theory]
    [InlineData("__repr__")]   // No direct .NET equivalent
    [InlineData("__delitem__")] // No direct .NET equivalent
    [InlineData("__next__")]    // Semantically different from MoveNext
    public void GetProtocol_ReturnsNullClrMethodForNoMapping(string dunderName)
    {
        var protocol = ProtocolRegistry.GetProtocol(dunderName);
        protocol.Should().NotBeNull();
        protocol!.ClrMethodName.Should().BeNull();
    }

    // ==================== Test Interface Method Names ====================

    [Theory]
    [InlineData("__len__", "__Len__")]
    [InlineData("__contains__", "__Contains__")]
    [InlineData("__getitem__", "__GetItem__")]
    [InlineData("__setitem__", "__SetItem__")]
    [InlineData("__delitem__", "__DelItem__")]
    [InlineData("__iter__", "__Iter__")]
    [InlineData("__next__", "__Next__")]
    [InlineData("__str__", "__Str__")]
    [InlineData("__repr__", "__Repr__")]
    [InlineData("__hash__", "__Hash__")]
    [InlineData("__bool__", "__Bool__")]
    public void GetProtocol_ReturnsCorrectInterfaceMethodName(string dunderName, string expectedMethodName)
    {
        var protocol = ProtocolRegistry.GetProtocol(dunderName);
        protocol.Should().NotBeNull();
        protocol!.InterfaceMethodName.Should().Be(expectedMethodName);
    }

    // ==================== Test Return Types ====================

    [Theory]
    [InlineData("__init__", "None")]
    [InlineData("__len__", "int")]
    [InlineData("__contains__", "bool")]
    [InlineData("__setitem__", "None")]
    [InlineData("__delitem__", "None")]
    [InlineData("__str__", "str")]
    [InlineData("__repr__", "str")]
    [InlineData("__hash__", "int")]
    [InlineData("__bool__", "bool")]
    public void GetProtocol_ReturnsCorrectExpectedReturnType(string dunderName, string expectedReturnType)
    {
        var protocol = ProtocolRegistry.GetProtocol(dunderName);
        protocol.Should().NotBeNull();
        protocol!.ExpectedReturnType.Should().Be(expectedReturnType);
    }

    [Theory]
    [InlineData("__getitem__")]  // Returns element type (generic)
    [InlineData("__iter__")]     // Returns Iterator<T> (generic)
    [InlineData("__next__")]     // Returns element type (generic)
    public void GetProtocol_ReturnsNullReturnTypeForGenericMethods(string dunderName)
    {
        var protocol = ProtocolRegistry.GetProtocol(dunderName);
        protocol.Should().NotBeNull();
        protocol!.ExpectedReturnType.Should().BeNull();
    }

    // ==================== Test Parameter Counts ====================

    [Theory]
    [InlineData("__len__", 1)]      // self
    [InlineData("__contains__", 2)] // self, item
    [InlineData("__getitem__", 2)]  // self, key
    [InlineData("__setitem__", 3)]  // self, key, value
    [InlineData("__delitem__", 2)]  // self, key
    [InlineData("__iter__", 1)]     // self
    [InlineData("__next__", 1)]     // self
    [InlineData("__str__", 1)]      // self
    [InlineData("__repr__", 1)]     // self
    [InlineData("__hash__", 1)]     // self
    [InlineData("__bool__", 1)]     // self
    public void GetProtocol_ReturnsCorrectParameterCount(string dunderName, int expectedCount)
    {
        var protocol = ProtocolRegistry.GetProtocol(dunderName);
        protocol.Should().NotBeNull();
        protocol!.ExpectedParamCount.Should().Be(expectedCount);
    }

    [Fact]
    public void GetProtocol_Init_HasVariableParamCount()
    {
        // __init__ has variable parameter count (1+ including self)
        var protocol = ProtocolRegistry.GetProtocol("__init__");
        protocol.Should().NotBeNull();
        protocol!.ExpectedParamCount.Should().Be(-1);
    }

    // ==================== Test Query Methods ====================

    [Fact]
    public void IsProtocolDunder_ReturnsTrueForProtocols()
    {
        ProtocolRegistry.IsProtocolDunder("__init__").Should().BeTrue();
        ProtocolRegistry.IsProtocolDunder("__len__").Should().BeTrue();
        ProtocolRegistry.IsProtocolDunder("__iter__").Should().BeTrue();
        ProtocolRegistry.IsProtocolDunder("__str__").Should().BeTrue();
    }

    [Fact]
    public void IsProtocolDunder_ReturnsFalseForOperators()
    {
        // Operator dunders are NOT protocol dunders
        ProtocolRegistry.IsProtocolDunder("__add__").Should().BeFalse();
        ProtocolRegistry.IsProtocolDunder("__sub__").Should().BeFalse();
        ProtocolRegistry.IsProtocolDunder("__eq__").Should().BeFalse();
    }

    [Fact]
    public void IsProtocolDunder_ReturnsFalseForRegularMethods()
    {
        ProtocolRegistry.IsProtocolDunder("regular_method").Should().BeFalse();
        ProtocolRegistry.IsProtocolDunder("MyMethod").Should().BeFalse();
    }

    [Fact]
    public void GetAllProtocols_ReturnsAllRegisteredProtocols()
    {
        var protocols = ProtocolRegistry.GetAllProtocols().ToList();
        // Protocols (dunders) for operator overloading - v0.5
        // __init__, __len__, __contains__, __getitem__, __setitem__, __delitem__,
        // __iter__, __next__, __str__, __repr__, __hash__, __bool__
        protocols.Should().HaveCount(12, "exactly 12 protocols are registered for operator overloading");

        // Verify we have at least one of each kind (except Comparison which is handled by operators)
        protocols.Should().Contain(p => p.Kind == ProtocolKind.Lifecycle);
        protocols.Should().Contain(p => p.Kind == ProtocolKind.Container);
        protocols.Should().Contain(p => p.Kind == ProtocolKind.Iterator);
        protocols.Should().Contain(p => p.Kind == ProtocolKind.Representation);
        protocols.Should().Contain(p => p.Kind == ProtocolKind.Hashing);
        protocols.Should().Contain(p => p.Kind == ProtocolKind.Conversion);
    }

    [Fact]
    public void GetProtocolsByKind_ReturnsCorrectProtocols()
    {
        var containerProtocols = ProtocolRegistry.GetProtocolsByKind(ProtocolKind.Container).ToList();
        containerProtocols.Should().HaveCount(5);  // __len__, __contains__, __getitem__, __setitem__, __delitem__
        containerProtocols.Should().OnlyContain(p => p.Kind == ProtocolKind.Container);
    }

    [Fact]
    public void GetProtocolsByKind_Iterator_ReturnsIteratorProtocols()
    {
        var iteratorProtocols = ProtocolRegistry.GetProtocolsByKind(ProtocolKind.Iterator).ToList();
        iteratorProtocols.Should().HaveCount(2);  // __iter__, __next__
        iteratorProtocols.Select(p => p.DunderName).Should().Contain("__iter__");
        iteratorProtocols.Select(p => p.DunderName).Should().Contain("__next__");
    }

    // ==================== Test Helper Methods ====================

    [Theory]
    [InlineData("__len__", "ISized")]
    [InlineData("__iter__", "IIterable")]
    [InlineData("__str__", "IStrConvertible")]
    [InlineData("__init__", null)]  // No interface for constructor
    public void GetInterfaceName_ReturnsCorrectInterface(string dunderName, string? expectedInterface)
    {
        ProtocolRegistry.GetInterfaceName(dunderName).Should().Be(expectedInterface);
    }

    [Theory]
    [InlineData("__len__", "get_Count")]
    [InlineData("__str__", "ToString")]
    [InlineData("__repr__", null)]  // No CLR method
    public void GetClrMethodName_ReturnsCorrectMethod(string dunderName, string? expectedMethod)
    {
        ProtocolRegistry.GetClrMethodName(dunderName).Should().Be(expectedMethod);
    }

    [Fact]
    public void HasReturnTypeConstraint_ReturnsTrueForConstrainedMethods()
    {
        ProtocolRegistry.HasReturnTypeConstraint("__len__").Should().BeTrue();
        ProtocolRegistry.HasReturnTypeConstraint("__str__").Should().BeTrue();
        ProtocolRegistry.HasReturnTypeConstraint("__bool__").Should().BeTrue();
    }

    [Fact]
    public void HasReturnTypeConstraint_ReturnsFalseForGenericMethods()
    {
        ProtocolRegistry.HasReturnTypeConstraint("__getitem__").Should().BeFalse();
        ProtocolRegistry.HasReturnTypeConstraint("__iter__").Should().BeFalse();
    }

    [Fact]
    public void HasReturnTypeConstraint_ReturnsFalseForUnknownMethods()
    {
        ProtocolRegistry.HasReturnTypeConstraint("__unknown__").Should().BeFalse();
    }

    [Fact]
    public void Count_ReturnsNumberOfRegisteredProtocols()
    {
        ProtocolRegistry.Count.Should().Be(12, "exactly 12 protocols are registered for operator overloading");
    }

    // ==================== Test Consistency with OperatorSignatureValidator ====================

    [Fact]
    public void ProtocolRegistry_DoesNotOverlapWithOperatorSignatureValidator()
    {
        // Ensure no dunders are registered in both registries by querying OperatorSignatureValidator directly
        foreach (var protocol in ProtocolRegistry.GetAllProtocols())
        {
            OperatorSignatureValidator.IsOperatorDunder(protocol.DunderName)
                .Should().BeFalse(
                    $"'{protocol.DunderName}' should not be registered in both registries");
        }
    }

    // ==================== Test New Query Methods (2.3.2, 2.3.3, 2.3.4) ====================

    [Theory]
    [InlineData("ISized", "__len__")]
    [InlineData("IContainer", "__contains__")]
    [InlineData("ISequence", "__getitem__")]
    [InlineData("IMutableSequence", "__setitem__")]  // Note: __delitem__ also maps here; returns first match
    [InlineData("IIterable", "__iter__")]
    [InlineData("IStrConvertible", "__str__")]
    [InlineData("IRepresentable", "__repr__")]
    [InlineData("IHashable", "__hash__")]
    [InlineData("IBoolConvertible", "__bool__")]
    public void GetDunderForInterface_ReturnsCorrectDunder(string interfaceName, string expectedDunder)
    {
        ProtocolRegistry.GetDunderForInterface(interfaceName).Should().Be(expectedDunder);
    }

    [Fact]
    public void GetDunderForInterface_ReturnsNullForUnknownInterface()
    {
        ProtocolRegistry.GetDunderForInterface("IUnknown").Should().BeNull();
    }

    [Fact]
    public void IsAnyDunder_ReturnsTrueForProtocolDunders()
    {
        // Protocol dunders
        ProtocolRegistry.IsAnyDunder("__len__").Should().BeTrue();
        ProtocolRegistry.IsAnyDunder("__str__").Should().BeTrue();
        ProtocolRegistry.IsAnyDunder("__iter__").Should().BeTrue();
    }

    [Fact]
    public void IsAnyDunder_ReturnsTrueForOperatorDunders()
    {
        // Operator dunders (from OperatorSignatureValidator)
        ProtocolRegistry.IsAnyDunder("__add__").Should().BeTrue();
        ProtocolRegistry.IsAnyDunder("__eq__").Should().BeTrue();
        ProtocolRegistry.IsAnyDunder("__neg__").Should().BeTrue();
    }

    [Fact]
    public void IsAnyDunder_ReturnsFalseForRegularMethods()
    {
        ProtocolRegistry.IsAnyDunder("regular_method").Should().BeFalse();
        ProtocolRegistry.IsAnyDunder("MyMethod").Should().BeFalse();
        ProtocolRegistry.IsAnyDunder("__unknown__").Should().BeFalse();
    }

    [Theory]
    [InlineData("__len__", 1, "int")]
    [InlineData("__contains__", 2, "bool")]
    [InlineData("__str__", 1, "str")]
    [InlineData("__hash__", 1, "int")]
    [InlineData("__bool__", 1, "bool")]
    public void GetExpectedSignature_ReturnsCorrectValues(string dunder, int paramCount, string returnType)
    {
        var result = ProtocolRegistry.GetExpectedSignature(dunder);
        result.Should().NotBeNull();
        result!.Value.ParamCount.Should().Be(paramCount);
        result.Value.ReturnType.Should().Be(returnType);
    }

    [Theory]
    [InlineData("__getitem__")]  // Returns element type (generic)
    [InlineData("__iter__")]     // Returns Iterator<T> (generic)
    public void GetExpectedSignature_ReturnsNullReturnTypeForGenericMethods(string dunder)
    {
        var result = ProtocolRegistry.GetExpectedSignature(dunder);
        result.Should().NotBeNull();
        result!.Value.ReturnType.Should().BeNull();
    }

    [Fact]
    public void GetExpectedSignature_ReturnsNullForUnknownDunder()
    {
        ProtocolRegistry.GetExpectedSignature("__unknown__").Should().BeNull();
    }

    [Fact]
    public void GetProtocol_Init_HasNullInterfaceMethodName()
    {
        // __init__ has no interface method since constructors are special-cased
        var protocol = ProtocolRegistry.GetProtocol("__init__");
        protocol.Should().NotBeNull();
        protocol!.InterfaceMethodName.Should().BeNull();
    }
}
