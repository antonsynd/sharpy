using Sharpy.Compiler.Semantic;
using TypeParameterVariance = Sharpy.Compiler.Parser.Ast.TypeParameterVariance;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class BuiltinRegistryTests
{
    private readonly BuiltinRegistry _registry = new();

    [Theory]
    [InlineData("TypeError")]
    [InlineData("ValueError")]
    [InlineData("RuntimeError")]
    [InlineData("NotImplementedError")]
    [InlineData("AttributeError")]
    [InlineData("ZeroDivisionError")]
    [InlineData("OverflowError")]
    public void LoadBuiltinTypes_RegistersExceptionType(string typeName)
    {
        // Act
        var typeSymbol = _registry.GetType(typeName);

        // Assert
        Assert.NotNull(typeSymbol);
        Assert.Equal(typeName, typeSymbol.Name);
        Assert.NotNull(typeSymbol.ClrType);
        Assert.True(typeof(Exception).IsAssignableFrom(typeSymbol.ClrType),
            $"{typeName} should be assignable to Exception");
    }

    [Fact]
    public void LoadBuiltinTypes_ExceptionBaseTypeIsRegistered()
    {
        // Act
        var exceptionType = _registry.GetType("Exception");

        // Assert
        Assert.NotNull(exceptionType);
        Assert.Equal(typeof(System.Exception), exceptionType.ClrType);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("str")]
    [InlineData("bool")]
    [InlineData("float")]
    [InlineData("long")]
    [InlineData("double")]
    [InlineData("decimal")]
    public void LoadBuiltinTypes_PrimitivesStillRegistered(string typeName)
    {
        // Act
        var typeSymbol = _registry.GetType(typeName);

        // Assert - regression guard: primitives must not be disrupted by auto-discovery
        Assert.NotNull(typeSymbol);
        Assert.NotNull(typeSymbol.ClrType);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("dict")]
    [InlineData("set")]
    public void LoadBuiltinTypes_CollectionsStillRegistered(string typeName)
    {
        // Act
        var typeSymbol = _registry.GetType(typeName);

        // Assert - regression guard
        Assert.NotNull(typeSymbol);
        Assert.True(typeSymbol.IsGeneric);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("set")]
    public void PopulateClrInterfaces_CollectionImplementsGenericIEnumerable(string typeName)
    {
        // Act
        var typeSymbol = _registry.GetType(typeName);

        // Assert - the collection's TypeSymbol exposes IEnumerable[T0] (#827)
        Assert.NotNull(typeSymbol);
        var enumerableRef = typeSymbol.Interfaces
            .SingleOrDefault(i => i.Definition.Name == "IEnumerable");
        Assert.NotNull(enumerableRef);
        var arg = Assert.Single(enumerableRef.ResolvedTypeArguments);
        var typeParam = Assert.IsType<TypeParameterType>(arg);
        Assert.Equal(typeSymbol.TypeParameters[0].Name, typeParam.Name);
    }

    [Fact]
    public void PopulateClrInterfaces_DictImplementsIEnumerableOfKeyValueTuple()
    {
        // Act
        var typeSymbol = _registry.GetType("dict");

        // Assert - dict implements IEnumerable[tuple[T0, T1]] (KeyValuePair maps to tuple)
        Assert.NotNull(typeSymbol);
        var enumerableRef = typeSymbol.Interfaces
            .SingleOrDefault(i => i.Definition.Name == "IEnumerable");
        Assert.NotNull(enumerableRef);
        var arg = Assert.Single(enumerableRef.ResolvedTypeArguments);
        var tuple = Assert.IsType<TupleType>(arg);
        Assert.Equal(2, tuple.ElementTypes.Count);
        Assert.Equal("T0", Assert.IsType<TypeParameterType>(tuple.ElementTypes[0]).Name);
        Assert.Equal("T1", Assert.IsType<TypeParameterType>(tuple.ElementTypes[1]).Name);
    }

    [Fact]
    public void PopulateClrInterfaces_IEnumerableDefinitionIsRegisteredSymbol()
    {
        // Act
        var listSymbol = _registry.GetType("list");
        var enumerableSymbol = _registry.GetType("IEnumerable");

        // Assert - InterfaceReference.Definition reuses the registered IEnumerable symbol
        Assert.NotNull(listSymbol);
        Assert.NotNull(enumerableSymbol);
        var enumerableRef = listSymbol.Interfaces
            .SingleOrDefault(i => i.Definition.Name == "IEnumerable");
        Assert.NotNull(enumerableRef);
        Assert.Same(enumerableSymbol, enumerableRef.Definition);
    }

    [Fact]
    public void PopulateClrInterfaces_SharedInterfaceSymbolAcrossImplementers()
    {
        // Act
        var listSymbol = _registry.GetType("list");
        var setSymbol = _registry.GetType("set");

        // Assert - list and set share the same ICollection definition symbol
        Assert.NotNull(listSymbol);
        Assert.NotNull(setSymbol);
        var listCollection = listSymbol.Interfaces
            .FirstOrDefault(i => i.Definition.Name == "ICollection");
        var setCollection = setSymbol.Interfaces
            .FirstOrDefault(i => i.Definition.Name == "ICollection");
        Assert.NotNull(listCollection);
        Assert.NotNull(setCollection);
        Assert.Same(listCollection.Definition, setCollection.Definition);
    }

    [Fact]
    public void PopulateClrInterfaces_NonGenericDuplicatesAreFiltered()
    {
        // Act
        var listSymbol = _registry.GetType("list");

        // Assert - only the generic IEnumerable[T] form is kept; the non-generic
        // System.Collections.IEnumerable duplicate is filtered out
        Assert.NotNull(listSymbol);
        var enumerableRefs = listSymbol.Interfaces
            .Where(i => i.Definition.Name == "IEnumerable")
            .ToList();
        var singleRef = Assert.Single(enumerableRefs);
        Assert.NotEmpty(singleRef.ResolvedTypeArguments);
    }

    [Fact]
    public void ClrVariance_RegisteredIEnumerable_IsCovariant()
    {
        // IEnumerable<out T> — the registered builtin interface symbol must carry
        // the CLR-declared covariance on its type parameter (#827)
        var enumerableSymbol = _registry.GetType("IEnumerable");

        Assert.NotNull(enumerableSymbol);
        var typeParam = Assert.Single(enumerableSymbol.TypeParameters);
        Assert.Equal(TypeParameterVariance.Covariant, typeParam.Variance);
    }

    [Fact]
    public void ClrVariance_InterfaceSymbolFromImplementer_IsCovariant()
    {
        // The IEnumerable definition referenced from list's interface list must also
        // carry covariance (it is the same registered symbol)
        var listSymbol = _registry.GetType("list");

        Assert.NotNull(listSymbol);
        var enumerableRef = listSymbol.Interfaces
            .Single(i => i.Definition.Name == "IEnumerable");
        var typeParam = Assert.Single(enumerableRef.Definition.TypeParameters);
        Assert.Equal(TypeParameterVariance.Covariant, typeParam.Variance);
    }

    [Fact]
    public void ClrVariance_CreatedInterfaceSymbol_ReadsClrCovariance()
    {
        // IReadOnlyList<out T> has no builtin registration, so its symbol is created by
        // GetOrCreateInterfaceSymbol — verify CLR covariance is read during creation.
        var listSymbol = _registry.GetType("list");

        Assert.NotNull(listSymbol);
        var readOnlyListRef = listSymbol.Interfaces
            .FirstOrDefault(i => i.Definition.Name == "IReadOnlyList");
        Assert.NotNull(readOnlyListRef);
        var typeParam = Assert.Single(readOnlyListRef.Definition.TypeParameters);
        Assert.Equal(TypeParameterVariance.Covariant, typeParam.Variance);
    }
}
