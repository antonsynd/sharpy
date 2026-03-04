using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

public class OverloadExpanderTests
{
    private static TypeSignature MakeType(string name, bool isGenericParam = false)
    {
        return new TypeSignature { Name = name, IsGenericParameter = isGenericParam };
    }

    private static ParameterSignature MakeParam(string name, TypeSignature type, bool hasDefault = false)
    {
        return new ParameterSignature { Name = name, Type = type, HasDefault = hasDefault };
    }

    private static FunctionSignature MakeSignature(string name, List<ParameterSignature> parameters, TypeSignature returnType)
    {
        return new FunctionSignature
        {
            Name = name,
            Parameters = parameters,
            ReturnType = returnType,
        };
    }

    [Fact]
    public void Expand_NoDefaults_ReturnsSingleOverload()
    {
        var sig = MakeSignature("append",
            [MakeParam("item", MakeType("T", isGenericParam: true))],
            MakeType("None"));

        var result = OverloadExpander.Expand(sig, "List");

        Assert.Single(result);
        Assert.Same(sig, result[0]);
    }

    [Fact]
    public void Expand_OneDefaultParam_ReturnsTwoOverloads()
    {
        // dict.get(key, default=None) -> 2 overloads: get(key) and get(key, default)
        var sig = MakeSignature("get",
        [
            MakeParam("key", MakeType("TKey", isGenericParam: true)),
            MakeParam("default", MakeType("TValue", isGenericParam: true), hasDefault: true),
        ],
            MakeType("TValue", isGenericParam: true));

        var result = OverloadExpander.Expand(sig, "Dict");

        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Parameters);
        Assert.Equal("key", result[0].Parameters[0].Name);
        Assert.Equal(2, result[1].Parameters.Count);
        Assert.Equal("default", result[1].Parameters[1].Name);
    }

    [Fact]
    public void Expand_TwoDefaultParams_ReturnsThreeOverloads()
    {
        // sort(key=None, reverse=False) -> 3 overloads: sort(), sort(key), sort(key, reverse)
        var sig = MakeSignature("sort",
        [
            MakeParam("key", MakeType("object"), hasDefault: true),
            MakeParam("reverse", MakeType("bool"), hasDefault: true),
        ],
            MakeType("None"));

        var result = OverloadExpander.Expand(sig, "List");

        Assert.Equal(3, result.Count);
        Assert.Empty(result[0].Parameters);
        Assert.Single(result[1].Parameters);
        Assert.Equal("key", result[1].Parameters[0].Name);
        Assert.Equal(2, result[2].Parameters.Count);
    }

    [Fact]
    public void Expand_DictGet_FirstOverloadReturnsOptional()
    {
        var valueType = MakeType("TValue", isGenericParam: true);
        var sig = MakeSignature("get",
        [
            MakeParam("key", MakeType("TKey", isGenericParam: true)),
            MakeParam("default", valueType, hasDefault: true),
        ],
            valueType);

        var result = OverloadExpander.Expand(sig, "Dict");

        // 1-param overload: return type should be Optional[V]
        var firstOverload = result[0];
        Assert.True(firstOverload.ReturnType.IsGeneric);
        Assert.Equal("Optional", firstOverload.ReturnType.Name);
        Assert.Single(firstOverload.ReturnType.TypeArguments);
        Assert.Equal("TValue", firstOverload.ReturnType.TypeArguments[0].Name);
        Assert.True(firstOverload.ReturnType.TypeArguments[0].IsGenericParameter);

        // 2-param overload: return type should be V (unchanged)
        var secondOverload = result[1];
        Assert.Equal("TValue", secondOverload.ReturnType.Name);
        Assert.True(secondOverload.ReturnType.IsGenericParameter);
    }

    [Fact]
    public void Expand_DictPop_FirstOverloadReturnsOptional()
    {
        var valueType = MakeType("TValue", isGenericParam: true);
        var sig = MakeSignature("pop",
        [
            MakeParam("key", MakeType("TKey", isGenericParam: true)),
            MakeParam("default", valueType, hasDefault: true),
        ],
            valueType);

        var result = OverloadExpander.Expand(sig, "Dict");

        // 1-param overload: return type should be Optional[V]
        var firstOverload = result[0];
        Assert.True(firstOverload.ReturnType.IsGeneric);
        Assert.Equal("Optional", firstOverload.ReturnType.Name);
        Assert.Single(firstOverload.ReturnType.TypeArguments);

        // 2-param overload: return type should be V (unchanged)
        var secondOverload = result[1];
        Assert.Equal("TValue", secondOverload.ReturnType.Name);
        Assert.True(secondOverload.ReturnType.IsGenericParameter);
    }

    [Fact]
    public void Expand_NonSpecialCaseWithDefault_ReturnTypeUnchanged()
    {
        // list.pop(index=-1) -> pop() and pop(index), both return T
        var returnType = MakeType("T", isGenericParam: true);
        var sig = MakeSignature("pop",
        [
            MakeParam("index", MakeType("int"), hasDefault: true),
        ],
            returnType);

        var result = OverloadExpander.Expand(sig, "List");

        Assert.Equal(2, result.Count);
        // Both overloads return T (no special case for list.pop)
        Assert.Equal("T", result[0].ReturnType.Name);
        Assert.True(result[0].ReturnType.IsGenericParameter);
        Assert.Equal("T", result[1].ReturnType.Name);
        Assert.True(result[1].ReturnType.IsGenericParameter);
    }

    [Fact]
    public void Expand_PreservesMethodNameAndTypeParameters()
    {
        var sig = new FunctionSignature
        {
            Name = "get",
            Parameters =
            [
                MakeParam("key", MakeType("TKey", isGenericParam: true)),
                MakeParam("default", MakeType("TValue", isGenericParam: true), hasDefault: true),
            ],
            ReturnType = MakeType("TValue", isGenericParam: true),
            TypeParameters = ["T"],
            MethodToken = "Sharpy.Core|Dict|get|2",
        };

        var result = OverloadExpander.Expand(sig, "Dict");

        foreach (var overload in result)
        {
            Assert.Equal("get", overload.Name);
            Assert.Equal(["T"], overload.TypeParameters);
            Assert.Equal("Sharpy.Core|Dict|get|2", overload.MethodToken);
        }
    }
}
