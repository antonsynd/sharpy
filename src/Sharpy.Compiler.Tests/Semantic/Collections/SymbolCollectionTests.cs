using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Collections;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic.Collections;

/// <summary>
/// Tests for <see cref="SymbolSet"/>, <see cref="TypeSymbolSet"/>,
/// <see cref="SymbolDictionary{TValue}"/>, and <see cref="TypeSymbolDictionary{TValue}"/>.
/// </summary>
public class SymbolCollectionTests
{
    #region SymbolSet Tests

    [Fact]
    public void SymbolSet_TwoSymbolsWithSameName_AreTreatedAsDistinct()
    {
        var sym1 = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var sym2 = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        var set = new SymbolSet { sym1, sym2 };

        // Both should be in the set since they are different instances
        set.Should().HaveCount(2);
        set.Should().Contain(sym1);
        set.Should().Contain(sym2);
    }

    [Fact]
    public void SymbolSet_SameInstanceAddedTwice_OnlyAppearsOnce()
    {
        var sym = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        var set = new SymbolSet { sym, sym };

        set.Should().HaveCount(1);
        set.Should().Contain(sym);
    }

    [Fact]
    public void SymbolSet_MutatedSymbol_StillFindable()
    {
        var sym = new VariableSymbol
        {
            Name = "x",
            Kind = SymbolKind.Variable,
            Type = SemanticType.Unknown
        };

        var set = new SymbolSet { sym };

        // Mutate the symbol's Type (would break value-based equality)
        sym.Type = SemanticType.Int;

        // Should still be findable because we use reference equality
        set.Should().Contain(sym);
    }

    [Fact]
    public void SymbolSet_ContainsReturnsFalse_ForDifferentInstanceWithSameName()
    {
        var sym1 = new FunctionSymbol { Name = "foo", Kind = SymbolKind.Function };
        var sym2 = new FunctionSymbol { Name = "foo", Kind = SymbolKind.Function };

        var set = new SymbolSet { sym1 };

        set.Contains(sym2).Should().BeFalse("different instances should not match");
    }

    [Fact]
    public void SymbolSet_ConstructorWithCapacity_Works()
    {
        var set = new SymbolSet(100);
        var sym = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        set.Add(sym);

        set.Should().Contain(sym);
    }

    [Fact]
    public void SymbolSet_ConstructorWithCollection_Works()
    {
        var symbols = new[]
        {
            new VariableSymbol { Name = "x", Kind = SymbolKind.Variable },
            new VariableSymbol { Name = "y", Kind = SymbolKind.Variable }
        };

        var set = new SymbolSet(symbols);

        set.Should().HaveCount(2);
        set.Should().Contain(symbols[0]);
        set.Should().Contain(symbols[1]);
    }

    #endregion

    #region TypeSymbolSet Tests

    [Fact]
    public void TypeSymbolSet_TwoTypeSymbolsWithSameName_AreTreatedAsDistinct()
    {
        var type1 = new TypeSymbol { Name = "Dog", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var type2 = new TypeSymbol { Name = "Dog", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        var set = new TypeSymbolSet { type1, type2 };

        set.Should().HaveCount(2);
    }

    [Fact]
    public void TypeSymbolSet_MutatedBaseType_StillFindable()
    {
        var type = new TypeSymbol { Name = "Dog", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var set = new TypeSymbolSet { type };

        // Mutate BaseType (happens during inheritance resolution)
        type.BaseType = new TypeSymbol { Name = "Animal", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        set.Should().Contain(type);
    }

    [Fact]
    public void TypeSymbolSet_ConstructorWithCollection_Works()
    {
        var types = new[]
        {
            new TypeSymbol { Name = "Cat", Kind = SymbolKind.Type, TypeKind = TypeKind.Class },
            new TypeSymbol { Name = "Dog", Kind = SymbolKind.Type, TypeKind = TypeKind.Class }
        };

        var set = new TypeSymbolSet(types);

        set.Should().HaveCount(2);
    }

    #endregion

    #region SymbolDictionary Tests

    [Fact]
    public void SymbolDictionary_TwoSymbolsWithSameName_AreTreatedAsDistinctKeys()
    {
        var sym1 = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var sym2 = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        var dict = new SymbolDictionary<string>
        {
            [sym1] = "first",
            [sym2] = "second"
        };

        dict.Should().HaveCount(2);
        dict[sym1].Should().Be("first");
        dict[sym2].Should().Be("second");
    }

    [Fact]
    public void SymbolDictionary_MutatedSymbol_StillFindable()
    {
        var sym = new VariableSymbol
        {
            Name = "x",
            Kind = SymbolKind.Variable,
            Type = SemanticType.Unknown
        };

        var dict = new SymbolDictionary<int> { [sym] = 42 };

        // Mutate the symbol
        sym.Type = SemanticType.Str;

        dict.Should().ContainKey(sym);
        dict[sym].Should().Be(42);
    }

    [Fact]
    public void SymbolDictionary_TryGetValue_ReturnsFalse_ForDifferentInstance()
    {
        var sym1 = new FunctionSymbol { Name = "foo", Kind = SymbolKind.Function };
        var sym2 = new FunctionSymbol { Name = "foo", Kind = SymbolKind.Function };

        var dict = new SymbolDictionary<string> { [sym1] = "original" };

        dict.TryGetValue(sym2, out _).Should().BeFalse();
    }

    [Fact]
    public void SymbolDictionary_ConstructorWithCapacity_Works()
    {
        var dict = new SymbolDictionary<int>(100);
        var sym = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        dict[sym] = 123;

        dict[sym].Should().Be(123);
    }

    #endregion

    #region TypeSymbolDictionary Tests

    [Fact]
    public void TypeSymbolDictionary_TwoTypeSymbolsWithSameName_AreTreatedAsDistinctKeys()
    {
        var type1 = new TypeSymbol { Name = "Dog", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var type2 = new TypeSymbol { Name = "Dog", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        var dict = new TypeSymbolDictionary<int>
        {
            [type1] = 1,
            [type2] = 2
        };

        dict.Should().HaveCount(2);
        dict[type1].Should().Be(1);
        dict[type2].Should().Be(2);
    }

    [Fact]
    public void TypeSymbolDictionary_MutatedTypeSymbol_StillFindable()
    {
        var type = new TypeSymbol
        {
            Name = "Dog",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var dict = new TypeSymbolDictionary<string> { [type] = "animal" };

        // Mutate BaseType
        type.BaseType = new TypeSymbol { Name = "Animal", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        dict.Should().ContainKey(type);
        dict[type].Should().Be("animal");
    }

    #endregion

    #region Basic Operations Tests

    [Fact]
    public void SymbolSet_RemoveWorks()
    {
        var sym = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var set = new SymbolSet { sym };

        set.Remove(sym).Should().BeTrue();
        set.Should().BeEmpty();
    }

    [Fact]
    public void SymbolSet_ClearWorks()
    {
        var sym1 = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var sym2 = new VariableSymbol { Name = "y", Kind = SymbolKind.Variable };
        var set = new SymbolSet { sym1, sym2 };

        set.Clear();

        set.Should().BeEmpty();
    }

    [Fact]
    public void SymbolDictionary_RemoveWorks()
    {
        var sym = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var dict = new SymbolDictionary<int> { [sym] = 42 };

        dict.Remove(sym).Should().BeTrue();
        dict.Should().BeEmpty();
    }

    [Fact]
    public void SymbolDictionary_ContainsKeyWorks()
    {
        var sym1 = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var sym2 = new VariableSymbol { Name = "y", Kind = SymbolKind.Variable };
        var dict = new SymbolDictionary<int> { [sym1] = 1 };

        dict.ContainsKey(sym1).Should().BeTrue();
        dict.ContainsKey(sym2).Should().BeFalse();
    }

    #endregion
}
