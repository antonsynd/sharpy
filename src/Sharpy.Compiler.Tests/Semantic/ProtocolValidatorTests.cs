using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Tests.Semantic;

public class ProtocolValidatorTests
{
    private ProtocolValidator CreateValidator()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        return new ProtocolValidator(symbolTable);
    }

    #region HasProtocol - Built-in types

    [Fact]
    public void HasProtocol_StringSupportsExpectedProtocols()
    {
        var validator = CreateValidator();

        validator.HasProtocol(SemanticType.Str, "__len__").Should().BeTrue();
        validator.HasProtocol(SemanticType.Str, "__iter__").Should().BeTrue();
        validator.HasProtocol(SemanticType.Str, "__contains__").Should().BeTrue();
        validator.HasProtocol(SemanticType.Str, "__getitem__").Should().BeTrue();
    }

    [Fact]
    public void HasProtocol_IntDoesNotSupportContainerProtocols()
    {
        var validator = CreateValidator();

        validator.HasProtocol(SemanticType.Int, "__len__").Should().BeFalse();
        validator.HasProtocol(SemanticType.Int, "__iter__").Should().BeFalse();
        validator.HasProtocol(SemanticType.Int, "__contains__").Should().BeFalse();
        validator.HasProtocol(SemanticType.Int, "__getitem__").Should().BeFalse();
    }

    #endregion

    #region HasProtocol - Generic container types

    [Fact]
    public void HasProtocol_GenericListSupportsContainerProtocols()
    {
        var validator = CreateValidator();
        var listOfInt = new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { SemanticType.Int }
        };

        validator.HasProtocol(listOfInt, "__len__").Should().BeTrue();
        validator.HasProtocol(listOfInt, "__iter__").Should().BeTrue();
        validator.HasProtocol(listOfInt, "__getitem__").Should().BeTrue();
        validator.HasProtocol(listOfInt, "__setitem__").Should().BeTrue();
        validator.HasProtocol(listOfInt, "__contains__").Should().BeTrue();
    }

    [Fact]
    public void HasProtocol_GenericDictSupportsContainerProtocols()
    {
        var validator = CreateValidator();
        var dictOfStrInt = new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType> { SemanticType.Str, SemanticType.Int }
        };

        validator.HasProtocol(dictOfStrInt, "__len__").Should().BeTrue();
        validator.HasProtocol(dictOfStrInt, "__iter__").Should().BeTrue();
        validator.HasProtocol(dictOfStrInt, "__getitem__").Should().BeTrue();
        validator.HasProtocol(dictOfStrInt, "__setitem__").Should().BeTrue();
        validator.HasProtocol(dictOfStrInt, "__contains__").Should().BeTrue();
    }

    [Fact]
    public void HasProtocol_GenericSetDoesNotSupportIndexing()
    {
        var validator = CreateValidator();
        var setOfStr = new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { SemanticType.Str }
        };

        validator.HasProtocol(setOfStr, "__getitem__").Should().BeFalse();
        validator.HasProtocol(setOfStr, "__setitem__").Should().BeFalse();
        // But set does support these:
        validator.HasProtocol(setOfStr, "__len__").Should().BeTrue();
        validator.HasProtocol(setOfStr, "__iter__").Should().BeTrue();
        validator.HasProtocol(setOfStr, "__contains__").Should().BeTrue();
    }

    [Fact]
    public void HasProtocol_GenericTupleSupportsExpectedProtocols()
    {
        var validator = CreateValidator();
        var tupleOfIntStr = new GenericType
        {
            Name = "tuple",
            TypeArguments = new List<SemanticType> { SemanticType.Int, SemanticType.Str }
        };

        validator.HasProtocol(tupleOfIntStr, "__len__").Should().BeTrue();
        validator.HasProtocol(tupleOfIntStr, "__iter__").Should().BeTrue();
        validator.HasProtocol(tupleOfIntStr, "__getitem__").Should().BeTrue();
        // Tuples don't support mutation
        validator.HasProtocol(tupleOfIntStr, "__setitem__").Should().BeFalse();
    }

    #endregion

    #region ValidateLen

    [Fact]
    public void ValidateLen_ReturnsIntForString()
    {
        var validator = CreateValidator();

        var result = validator.ValidateLen(SemanticType.Str, 1, 1);

        result.Should().Be(SemanticType.Int);
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateLen_ReturnsIntForGenericList()
    {
        var validator = CreateValidator();
        var listOfInt = new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { SemanticType.Int }
        };

        var result = validator.ValidateLen(listOfInt, 1, 1);

        result.Should().Be(SemanticType.Int);
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateLen_AddsErrorForInt()
    {
        var validator = CreateValidator();

        var result = validator.ValidateLen(SemanticType.Int, 1, 1);

        result.Should().Be(SemanticType.Unknown);
        validator.Errors.Should().ContainSingle();
        validator.Errors[0].Message.Should().Contain("does not support len()");
    }

    [Fact]
    public void ValidateLen_AddsErrorForBool()
    {
        var validator = CreateValidator();

        var result = validator.ValidateLen(SemanticType.Bool, 5, 10);

        result.Should().Be(SemanticType.Unknown);
        validator.Errors.Should().ContainSingle();
        validator.Errors[0].Message.Should().Contain("does not support len()");
        validator.Errors[0].Line.Should().Be(5);
        validator.Errors[0].Column.Should().Be(10);
    }

    #endregion

    #region ValidateIteration

    [Fact]
    public void ValidateIteration_InfersElementTypeFromGenericList()
    {
        var validator = CreateValidator();
        var listOfInt = new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { SemanticType.Int }
        };

        var result = validator.ValidateIteration(listOfInt, 1, 1);

        result.Should().Be(SemanticType.Int);
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateIteration_InfersKeyTypeFromDict()
    {
        var validator = CreateValidator();
        var dictOfStrInt = new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType> { SemanticType.Str, SemanticType.Int }
        };

        var result = validator.ValidateIteration(dictOfStrInt, 1, 1);

        result.Should().Be(SemanticType.Str); // Dict iteration yields keys
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateIteration_ReturnsStrForString()
    {
        var validator = CreateValidator();

        var result = validator.ValidateIteration(SemanticType.Str, 1, 1);

        result.Should().Be(SemanticType.Str);
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateIteration_AddsErrorForNonIterable()
    {
        var validator = CreateValidator();

        var result = validator.ValidateIteration(SemanticType.Int, 1, 1);

        result.Should().Be(SemanticType.Unknown);
        validator.Errors.Should().ContainSingle();
        validator.Errors[0].Message.Should().Contain("not iterable");
    }

    #endregion

    #region ValidateMembership

    [Fact]
    public void ValidateMembership_ReturnsBoolForList()
    {
        var validator = CreateValidator();
        var listOfInt = new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { SemanticType.Int }
        };

        var result = validator.ValidateMembership(listOfInt, SemanticType.Int, 1, 1);

        result.Should().Be(SemanticType.Bool);
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateMembership_ReturnsBoolForString()
    {
        var validator = CreateValidator();

        var result = validator.ValidateMembership(SemanticType.Str, SemanticType.Str, 1, 1);

        result.Should().Be(SemanticType.Bool);
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateMembership_AddsErrorForNonContainer()
    {
        var validator = CreateValidator();

        var result = validator.ValidateMembership(SemanticType.Int, SemanticType.Int, 1, 1);

        result.Should().Be(SemanticType.Unknown);
        validator.Errors.Should().ContainSingle();
        validator.Errors[0].Message.Should().Contain("does not support membership testing");
    }

    #endregion

    #region ValidateIndexAccess

    [Fact]
    public void ValidateIndexAccess_InfersElementTypeFromList()
    {
        var validator = CreateValidator();
        var listOfStr = new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { SemanticType.Str }
        };

        var result = validator.ValidateIndexAccess(listOfStr, SemanticType.Int, 1, 1);

        result.Should().Be(SemanticType.Str);
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateIndexAccess_InfersValueTypeFromDict()
    {
        var validator = CreateValidator();
        var dictOfStrInt = new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType> { SemanticType.Str, SemanticType.Int }
        };

        var result = validator.ValidateIndexAccess(dictOfStrInt, SemanticType.Str, 1, 1);

        result.Should().Be(SemanticType.Int); // Dict indexing returns value type
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateIndexAccess_ReturnsStrForString()
    {
        var validator = CreateValidator();

        var result = validator.ValidateIndexAccess(SemanticType.Str, SemanticType.Int, 1, 1);

        result.Should().Be(SemanticType.Str);
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateIndexAccess_AddsErrorForNonIndexable()
    {
        var validator = CreateValidator();

        var result = validator.ValidateIndexAccess(SemanticType.Int, SemanticType.Int, 1, 1);

        result.Should().Be(SemanticType.Unknown);
        validator.Errors.Should().ContainSingle();
        validator.Errors[0].Message.Should().Contain("does not support indexing");
    }

    [Fact]
    public void ValidateIndexAccess_AddsErrorForSet()
    {
        var validator = CreateValidator();
        var setOfInt = new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { SemanticType.Int }
        };

        var result = validator.ValidateIndexAccess(setOfInt, SemanticType.Int, 1, 1);

        result.Should().Be(SemanticType.Unknown);
        validator.Errors.Should().ContainSingle();
        validator.Errors[0].Message.Should().Contain("does not support indexing");
    }

    #endregion

    #region ValidateBoolConversion

    [Fact]
    public void ValidateBoolConversion_ReturnsBoolForAnyType()
    {
        var validator = CreateValidator();

        validator.ValidateBoolConversion(SemanticType.Int, 1, 1).Should().Be(SemanticType.Bool);
        validator.ValidateBoolConversion(SemanticType.Str, 1, 1).Should().Be(SemanticType.Bool);
        validator.ValidateBoolConversion(SemanticType.Bool, 1, 1).Should().Be(SemanticType.Bool);

        // No errors should be added - all types can be used in boolean context
        validator.Errors.Should().BeEmpty();
    }

    #endregion

    #region CLR Type Discovery

    [Fact]
    public void HasProtocol_DiscoversCLRListProtocols()
    {
        var validator = CreateValidator();
        var listType = new BuiltinType
        {
            Name = "List<int>",
            ClrType = typeof(List<int>)
        };

        validator.HasProtocol(listType, "__iter__").Should().BeTrue();
        validator.HasProtocol(listType, "__len__").Should().BeTrue();
        validator.HasProtocol(listType, "__getitem__").Should().BeTrue();
        validator.HasProtocol(listType, "__contains__").Should().BeTrue();
        validator.HasProtocol(listType, "__str__").Should().BeTrue(); // All objects have __str__
        validator.HasProtocol(listType, "__hash__").Should().BeTrue(); // All objects have __hash__
    }

    [Fact]
    public void HasProtocol_DiscoversCLRDictionaryProtocols()
    {
        var validator = CreateValidator();
        var dictType = new BuiltinType
        {
            Name = "Dictionary<string, int>",
            ClrType = typeof(Dictionary<string, int>)
        };

        validator.HasProtocol(dictType, "__getitem__").Should().BeTrue();
        validator.HasProtocol(dictType, "__setitem__").Should().BeTrue();
        validator.HasProtocol(dictType, "__contains__").Should().BeTrue();
        validator.HasProtocol(dictType, "__len__").Should().BeTrue();
    }

    [Fact]
    public void HasProtocol_DiscoversCLRArrayProtocols()
    {
        var validator = CreateValidator();
        var arrayType = new BuiltinType
        {
            Name = "int[]",
            ClrType = typeof(int[])
        };

        validator.HasProtocol(arrayType, "__iter__").Should().BeTrue();
        validator.HasProtocol(arrayType, "__len__").Should().BeTrue();
        validator.HasProtocol(arrayType, "__getitem__").Should().BeTrue();
    }

    #endregion

    #region User-defined types

    [Fact]
    public void HasProtocol_ChecksUserDefinedTypeProtocolMethods()
    {
        var validator = CreateValidator();

        // Create a user-defined type with __len__ protocol method
        var typeSymbol = new TypeSymbol
        {
            Name = "MyCollection",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        // Add __len__ to protocol methods
        var lenMethod = new FunctionSymbol
        {
            Name = "__len__",
            Kind = SymbolKind.Function,
            ReturnType = SemanticType.Int,
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "MyCollection", Symbol = typeSymbol } }
            }
        };
        typeSymbol.ProtocolMethods["__len__"] = new List<FunctionSymbol> { lenMethod };

        var userType = new UserDefinedType
        {
            Name = "MyCollection",
            Symbol = typeSymbol
        };

        validator.HasProtocol(userType, "__len__").Should().BeTrue();
        validator.HasProtocol(userType, "__iter__").Should().BeFalse(); // Not implemented
    }

    [Fact]
    public void HasProtocol_ChecksUserDefinedTypeRegularMethods()
    {
        var validator = CreateValidator();

        // Create a user-defined type with __iter__ as a regular method (not in ProtocolMethods cache)
        var typeSymbol = new TypeSymbol
        {
            Name = "MyIterable",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Methods = new List<FunctionSymbol>
            {
                new FunctionSymbol
                {
                    Name = "__iter__",
                    Kind = SymbolKind.Function,
                    ReturnType = SemanticType.Unknown
                }
            }
        };

        var userType = new UserDefinedType
        {
            Name = "MyIterable",
            Symbol = typeSymbol
        };

        validator.HasProtocol(userType, "__iter__").Should().BeTrue();
    }

    #endregion
}
