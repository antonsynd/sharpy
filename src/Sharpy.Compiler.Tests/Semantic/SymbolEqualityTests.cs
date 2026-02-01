using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests verifying that Symbol records use reference-based equality,
/// making them safe to use as dictionary keys even after mutation.
/// </summary>
public class SymbolEqualityTests
{
    [Fact]
    public void TwoSymbolsWithSameName_AreNotEqual()
    {
        var sym1 = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var sym2 = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        // With reference equality, two distinct instances are NOT equal
        sym1.Should().NotBe(sym2);
        (sym1 == sym2).Should().BeFalse();
    }

    [Fact]
    public void SameSymbolInstance_IsEqual()
    {
        var sym = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        (sym == sym).Should().BeTrue();
        sym.Equals(sym).Should().BeTrue();
    }

    [Fact]
    public void MutatedSymbol_StillFindableInDictionary()
    {
        var sym = new VariableSymbol
        {
            Name = "x",
            Kind = SymbolKind.Variable,
            Type = SemanticType.Unknown
        };

        // Add to a regular dictionary (no ReferenceEqualityComparer needed)
        var dict = new Dictionary<Symbol, string>();
        dict[sym] = "found";

        // Mutate the symbol's Type (this would break value-based hash)
        sym.Type = SemanticType.Int;

        // Should still be findable because we use reference equality
        dict.Should().ContainKey(sym);
        dict[sym].Should().Be("found");
    }

    [Fact]
    public void MutatedSymbol_StillFindableInHashSet()
    {
        var sym = new VariableSymbol
        {
            Name = "x",
            Kind = SymbolKind.Variable,
            Type = SemanticType.Unknown
        };

        var set = new HashSet<Symbol>();
        set.Add(sym);

        // Mutate the symbol
        sym.Type = SemanticType.Str;

        // Should still be in the set
        set.Should().Contain(sym);
    }

    [Fact]
    public void TypeSymbol_MutatedBaseType_StillFindableInDictionary()
    {
        var sym = new TypeSymbol
        {
            Name = "Dog",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var dict = new Dictionary<Symbol, int>();
        dict[sym] = 42;

        // Mutate BaseType after insertion (this happens during inheritance resolution)
        sym.BaseType = new TypeSymbol
        {
            Name = "Animal",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        dict.Should().ContainKey(sym);
        dict[sym].Should().Be(42);
    }

    [Fact]
    public void FunctionSymbol_UsesReferenceEquality()
    {
        var func1 = new FunctionSymbol { Name = "foo", Kind = SymbolKind.Function };
        var func2 = new FunctionSymbol { Name = "foo", Kind = SymbolKind.Function };

        (func1 == func2).Should().BeFalse("different instances should not be equal");
        (func1 == func1).Should().BeTrue("same instance should be equal");
    }

    [Fact]
    public void ModuleSymbol_UsesReferenceEquality()
    {
        var mod1 = new ModuleSymbol { Name = "mymod", Kind = SymbolKind.Module };
        var mod2 = new ModuleSymbol { Name = "mymod", Kind = SymbolKind.Module };

        (mod1 == mod2).Should().BeFalse();
    }
}
