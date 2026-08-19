using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Parity tests ensuring <see cref="UnmappedClrType"/> has the same assignability behavior as the
/// old <c>SemanticType.UnmappedClr</c> singleton (which was a <c>UserDefinedType { Name = "object" }</c>).
/// The plan's Design Decision 7 (#1534) requires: "no working program changes" — every assignability
/// answer must be identical.
/// </summary>
public class UnmappedClrTypeParityTests
{
    private static readonly UnmappedClrType Specimen = new() { ClrTypeName = "System.SomeType" };

    private static readonly SemanticType[] TargetTypes =
    {
        SemanticType.Int,
        SemanticType.Long,
        SemanticType.Float,
        SemanticType.Bool,
        SemanticType.Str,
        SemanticType.Object,
        SemanticType.Unknown,
        SemanticType.Void,
        SemanticType.UnmappedClr,
        new GenericType { Name = "list", TypeArguments = { SemanticType.Int } },
        new GenericType { Name = "dict", TypeArguments = { SemanticType.Str, SemanticType.Int } },
        new GenericType { Name = "set", TypeArguments = { SemanticType.Int } },
        new NullableType { UnderlyingType = SemanticType.Int },
        new OptionalType { UnderlyingType = SemanticType.Int },
        new UserDefinedType { Name = "MyClass" },
        new UserDefinedType { Name = "object" },
        Specimen,
    };

    [Fact]
    public void GetDisplayName_ReturnsObject()
    {
        Assert.Equal("object", Specimen.GetDisplayName());
    }

    [Fact]
    public void IsValueType_ReturnsFalse()
    {
        Assert.False(Specimen.IsValueType);
    }

    [Fact]
    public void UnmappedClrType_IsAssignableTo_ObjectTarget()
    {
        Assert.True(Specimen.IsAssignableTo(SemanticType.Object));
    }

    [Fact]
    public void UnmappedClrType_IsAssignableTo_UnmappedClrTarget()
    {
        Assert.True(Specimen.IsAssignableTo(SemanticType.UnmappedClr));
    }

    [Fact]
    public void UnmappedClrType_IsAssignableTo_AnotherUnmappedClrType()
    {
        var other = new UnmappedClrType { ClrTypeName = "System.Other" };
        Assert.True(Specimen.IsAssignableTo(other));
    }

    [Fact]
    public void AllTypes_AreAssignableTo_UnmappedClrType()
    {
        foreach (var source in TargetTypes)
        {
            Assert.True(
                source.IsAssignableTo(Specimen),
                $"{source.GetType().Name}({source.GetDisplayName()}).IsAssignableTo(UnmappedClrType) should be true");
        }
    }

    [Fact]
    public void AllTypes_AreAssignableTo_ObjectSingleton()
    {
        foreach (var source in TargetTypes)
        {
            Assert.True(
                source.IsAssignableTo(SemanticType.Object),
                $"{source.GetType().Name}({source.GetDisplayName()}).IsAssignableTo(Object) should be true");
        }
    }

    [Fact]
    public void UnmappedClrType_NotAssignableTo_ConcreteNonObjectTypes()
    {
        Assert.False(Specimen.IsAssignableTo(SemanticType.Int));
        Assert.False(Specimen.IsAssignableTo(SemanticType.Str));
        Assert.False(Specimen.IsAssignableTo(SemanticType.Bool));
        Assert.False(Specimen.IsAssignableTo(new GenericType { Name = "list", TypeArguments = { SemanticType.Int } }));
        Assert.False(Specimen.IsAssignableTo(new UserDefinedType { Name = "MyClass" }));
    }

    [Fact]
    public void Singleton_IsUnmappedClrType()
    {
        Assert.IsType<UnmappedClrType>(SemanticType.UnmappedClr);
    }

    [Fact]
    public void IsObjectReceiver_ExcludesUnmappedClr()
    {
        Assert.False(ReferenceEquals(Specimen, SemanticType.Object));
        Assert.False(ReferenceEquals(SemanticType.UnmappedClr, SemanticType.Object));
    }

    [Fact]
    public void IsNotRecordEqual_ToObject()
    {
        Assert.NotEqual(SemanticType.Object, (SemanticType)Specimen);
        Assert.NotEqual(SemanticType.Object, SemanticType.UnmappedClr);
    }
}
