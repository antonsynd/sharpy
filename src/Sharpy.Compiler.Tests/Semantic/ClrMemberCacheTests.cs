using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Semantic;

public class ClrMemberCacheTests
{
    [Fact]
    public void GetOperatorMethods_FindsDecimalOperators()
    {
        var cache = new ClrMemberCache();

        var operators = cache.GetOperatorMethods(typeof(decimal));

        operators.Should().ContainKey("op_Addition");
        operators.Should().ContainKey("op_Subtraction");
        operators.Should().ContainKey("op_Multiply");
        operators.Should().ContainKey("op_Division");
        operators.Should().ContainKey("op_Equality");
        operators.Should().ContainKey("op_Inequality");
    }

    [Fact]
    public void GetOperatorMethods_CachesSameResult()
    {
        var cache = new ClrMemberCache();

        var first = cache.GetOperatorMethods(typeof(decimal));
        var second = cache.GetOperatorMethods(typeof(decimal));

        first.Should().BeSameAs(second, "Cache should return same dictionary instance");
    }

    [Fact]
    public void GetOperatorMethods_ReturnsEmptyForTypeWithoutOperators()
    {
        var cache = new ClrMemberCache();

        var operators = cache.GetOperatorMethods(typeof(object));

        operators.Should().BeEmpty();
    }

    [Fact]
    public void GetOperatorMethods_FindsMultipleOperatorTypes()
    {
        var cache = new ClrMemberCache();

        // decimal has many different operator methods
        var operators = cache.GetOperatorMethods(typeof(decimal));

        operators.Keys.Count().Should().BeGreaterThan(5, "decimal has many operator types");
        operators.Should().ContainKey("op_Addition");
        operators.Should().ContainKey("op_UnaryNegation");
    }

    [Fact]
    public void GetImplementedInterfaces_FindsListInterfaces()
    {
        var cache = new ClrMemberCache();

        var interfaces = cache.GetImplementedInterfaces(typeof(List<int>));

        interfaces.Should().Contain(typeof(IList<int>));
        interfaces.Should().Contain(typeof(ICollection<int>));
        interfaces.Should().Contain(typeof(IEnumerable<int>));
        interfaces.Should().Contain(typeof(System.Collections.IEnumerable));
    }

    [Fact]
    public void GetImplementedInterfaces_CachesSameResult()
    {
        var cache = new ClrMemberCache();

        var first = cache.GetImplementedInterfaces(typeof(List<int>));
        var second = cache.GetImplementedInterfaces(typeof(List<int>));

        first.Should().BeSameAs(second, "Cache should return same HashSet instance");
    }

    [Fact]
    public void GetImplementedInterfaces_ReturnsEmptyForNonInterfaceType()
    {
        var cache = new ClrMemberCache();

        // Primitives like int still implement some interfaces (IComparable, IFormattable, etc.)
        // but they do implement interfaces, so let's test with object which truly has none
        var objectInterfaces = cache.GetImplementedInterfaces(typeof(object));
        objectInterfaces.Should().BeEmpty();
    }

    [Fact]
    public void ImplementsInterface_WorksWithGenericDefinition()
    {
        var cache = new ClrMemberCache();

        cache.ImplementsInterface(typeof(List<int>), typeof(IList<>)).Should().BeTrue();
        cache.ImplementsInterface(typeof(List<int>), typeof(IEnumerable<>)).Should().BeTrue();
        cache.ImplementsInterface(typeof(List<int>), typeof(IDictionary<,>)).Should().BeFalse();
    }

    [Fact]
    public void ImplementsInterface_WorksWithClosedGeneric()
    {
        var cache = new ClrMemberCache();

        cache.ImplementsInterface(typeof(List<int>), typeof(IList<int>)).Should().BeTrue();
        cache.ImplementsInterface(typeof(List<int>), typeof(IList<string>)).Should().BeFalse();
    }

    [Fact]
    public void ImplementsInterface_WorksWithNonGenericInterface()
    {
        var cache = new ClrMemberCache();

        cache.ImplementsInterface(typeof(List<int>), typeof(System.Collections.IEnumerable)).Should().BeTrue();
        cache.ImplementsInterface(typeof(int), typeof(System.Collections.IEnumerable)).Should().BeFalse();
    }

    [Fact]
    public void GetIndexerInfo_FindsListIndexer()
    {
        var cache = new ClrMemberCache();

        var (hasIndexer, elementType) = cache.GetIndexerInfo(typeof(List<string>));

        hasIndexer.Should().BeTrue();
        elementType.Should().Be(typeof(string));
    }

    [Fact]
    public void GetIndexerInfo_FindsStringIndexer()
    {
        var cache = new ClrMemberCache();

        var (hasIndexer, elementType) = cache.GetIndexerInfo(typeof(string));

        hasIndexer.Should().BeTrue();
        elementType.Should().Be(typeof(char));
    }

    [Fact]
    public void GetIndexerInfo_FindsDictionaryIndexer()
    {
        var cache = new ClrMemberCache();

        var (hasIndexer, elementType) = cache.GetIndexerInfo(typeof(Dictionary<string, int>));

        hasIndexer.Should().BeTrue();
        elementType.Should().Be(typeof(int));
    }

    [Fact]
    public void GetIndexerInfo_ReturnsFalseForNonIndexable()
    {
        var cache = new ClrMemberCache();

        var (hasIndexer, elementType) = cache.GetIndexerInfo(typeof(int));

        hasIndexer.Should().BeFalse();
        elementType.Should().BeNull();
    }

    [Fact]
    public void GetIndexerInfo_CachesSameResult()
    {
        var cache = new ClrMemberCache();

        var first = cache.GetIndexerInfo(typeof(List<int>));
        var second = cache.GetIndexerInfo(typeof(List<int>));

        first.Should().Be(second, "Cache should return same result");
    }

    [Fact]
    public void GetEnumerableElementType_InfersFromIEnumerable()
    {
        var cache = new ClrMemberCache();

        var elementType = cache.GetEnumerableElementType(typeof(List<double>));

        elementType.Should().Be(typeof(double));
    }

    [Fact]
    public void GetEnumerableElementType_WorksWithArrays()
    {
        var cache = new ClrMemberCache();

        var elementType = cache.GetEnumerableElementType(typeof(string[]));

        elementType.Should().Be(typeof(string));
    }

    [Fact]
    public void GetEnumerableElementType_WorksWithIEnumerableDirectly()
    {
        var cache = new ClrMemberCache();

        var elementType = cache.GetEnumerableElementType(typeof(IEnumerable<int>));

        elementType.Should().Be(typeof(int));
    }

    [Fact]
    public void GetEnumerableElementType_ReturnsNullForNonEnumerable()
    {
        var cache = new ClrMemberCache();

        var elementType = cache.GetEnumerableElementType(typeof(int));

        elementType.Should().BeNull();
    }

    [Fact]
    public void GetEnumerableElementType_CachesSameResult()
    {
        var cache = new ClrMemberCache();

        var first = cache.GetEnumerableElementType(typeof(List<int>));
        var second = cache.GetEnumerableElementType(typeof(List<int>));

        first.Should().Be(second, "Cache should return same result");
        first.Should().Be(typeof(int));
    }

    [Fact]
    public void GetEnumerableElementType_WorksWithHashSet()
    {
        var cache = new ClrMemberCache();

        var elementType = cache.GetEnumerableElementType(typeof(HashSet<string>));

        elementType.Should().Be(typeof(string));
    }

    [Fact]
    public void GetEnumerableElementType_WorksWithDictionary()
    {
        var cache = new ClrMemberCache();

        var elementType = cache.GetEnumerableElementType(typeof(Dictionary<string, int>));

        // Dictionary<K,V> implements IEnumerable<KeyValuePair<K,V>>
        elementType.Should().Be(typeof(KeyValuePair<string, int>));
    }
}
