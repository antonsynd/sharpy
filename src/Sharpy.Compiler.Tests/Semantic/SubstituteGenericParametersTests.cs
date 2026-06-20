using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class SubstituteGenericParametersTests
{
    [Fact]
    public void UnconstrainedGenericParameter_ReturnsObject()
    {
        var typeParam = typeof(System.Collections.Generic.List<>).GetGenericArguments()[0];
        var result = TypeChecker.SubstituteGenericParameters(typeParam);
        result.Should().Be(typeof(object));
    }

    [Fact]
    public void StructConstrainedGenericParameter_ReturnsInt()
    {
        // Nullable<T> has where T : struct
        var typeParam = typeof(System.Nullable<>).GetGenericArguments()[0];
        var result = TypeChecker.SubstituteGenericParameters(typeParam);
        result.Should().Be(typeof(int));
    }

    [Fact]
    public void OpenGenericWithStructConstraint_ProducesClosedWithInt()
    {
        // Nullable<T> (open) → Nullable<int> (closed), not Nullable<object> which would throw
        var openNullable = typeof(System.Nullable<>);
        var result = TypeChecker.SubstituteGenericParameters(openNullable);
        result.Should().Be(typeof(int?));
    }

    [Fact]
    public void OpenGenericWithoutStructConstraint_ProducesClosedWithObject()
    {
        var openList = typeof(System.Collections.Generic.List<>);
        var result = TypeChecker.SubstituteGenericParameters(openList);
        result.Should().Be(typeof(System.Collections.Generic.List<object>));
    }

    [Fact]
    public void ConcreteType_PassesThrough()
    {
        var result = TypeChecker.SubstituteGenericParameters(typeof(double));
        result.Should().Be(typeof(double));
    }

    [Fact]
    public void ClosedGenericType_PassesThrough()
    {
        var result = TypeChecker.SubstituteGenericParameters(typeof(System.Collections.Generic.List<string>));
        result.Should().Be(typeof(System.Collections.Generic.List<string>));
    }

    [Fact]
    public void ArrayOfGenericParameter_SubstitutesElement()
    {
        var typeParam = typeof(System.Collections.Generic.List<>).GetGenericArguments()[0];
        var arrayType = typeParam.MakeArrayType();
        var result = TypeChecker.SubstituteGenericParameters(arrayType);
        result.Should().Be(typeof(object[]));
    }
}
