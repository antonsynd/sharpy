using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Unit tests for InheritanceResolver: resolves imported type inheritance
/// (string-based base/interface names → TypeSymbol references) and
/// transitive base type auto-import.
/// </summary>
public class InheritanceResolverTests
{
    private static SymbolTable CreateSymbolTable()
    {
        var builtins = new BuiltinRegistry();
        return new SymbolTable(builtins);
    }

    #region ResolveImportedTypeInheritance

    [Fact]
    public void ResolveImportedTypeInheritance_ResolvesBaseClass()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var parent = new TypeSymbol { Name = "Parent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var child = new TypeSymbol
        {
            Name = "Child",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedBaseName = "Parent"
        };

        symbolTable.Define(parent);
        symbolTable.Define(child);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);
        resolver.ResolveImportedTypeInheritance();

        binding.GetBaseType(child).Should().Be(parent);
    }

    [Fact]
    public void ResolveImportedTypeInheritance_ResolvesInterface()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var iface = new TypeSymbol { Name = "ISerializable", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var impl = new TypeSymbol
        {
            Name = "Data",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedInterfaceNames = new List<string> { "ISerializable" }
        };

        symbolTable.Define(iface);
        symbolTable.Define(impl);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);
        resolver.ResolveImportedTypeInheritance();

        var interfaces = binding.GetInterfaces(impl);
        interfaces.Should().NotBeNull();
        interfaces.Should().Contain(iface);
    }

    [Fact]
    public void ResolveImportedTypeInheritance_InterfaceInUnresolvedBaseName_AddedAsInterface()
    {
        // When UnresolvedBaseName points to an interface, it should be added as an interface, not base type
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var iface = new TypeSymbol { Name = "IDrawable", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var impl = new TypeSymbol
        {
            Name = "Shape",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedBaseName = "IDrawable"
        };

        symbolTable.Define(iface);
        symbolTable.Define(impl);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);
        resolver.ResolveImportedTypeInheritance();

        binding.GetBaseType(impl).Should().BeNull("interface should not become base type");
        var interfaces = binding.GetInterfaces(impl);
        interfaces.Should().NotBeNull();
        interfaces.Should().Contain(iface);
    }

    [Fact]
    public void ResolveImportedTypeInheritance_ResolvesMultipleInterfaces()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var iface1 = new TypeSymbol { Name = "IFoo", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var iface2 = new TypeSymbol { Name = "IBar", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var impl = new TypeSymbol
        {
            Name = "Baz",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedInterfaceNames = new List<string> { "IFoo", "IBar" }
        };

        symbolTable.Define(iface1);
        symbolTable.Define(iface2);
        symbolTable.Define(impl);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);
        resolver.ResolveImportedTypeInheritance();

        var interfaces = binding.GetInterfaces(impl);
        interfaces.Should().HaveCount(2);
        interfaces.Should().Contain(iface1);
        interfaces.Should().Contain(iface2);
    }

    [Fact]
    public void ResolveImportedTypeInheritance_ResolvesBaseClassAndInterfaces()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var parent = new TypeSymbol { Name = "Base", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var iface = new TypeSymbol { Name = "IComparable", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var child = new TypeSymbol
        {
            Name = "Derived",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedBaseName = "Base",
            UnresolvedInterfaceNames = new List<string> { "IComparable" }
        };

        symbolTable.Define(parent);
        symbolTable.Define(iface);
        symbolTable.Define(child);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);
        resolver.ResolveImportedTypeInheritance();

        binding.GetBaseType(child).Should().Be(parent);
        var interfaces = binding.GetInterfaces(child);
        interfaces.Should().NotBeNull();
        interfaces.Should().Contain(iface);
    }

    [Fact]
    public void ResolveImportedTypeInheritance_SkipsAlreadyResolvedBaseType()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var parent = new TypeSymbol { Name = "Parent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var child = new TypeSymbol
        {
            Name = "Child",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedBaseName = "Parent"
        };

        // Pre-resolve via SemanticBinding
        binding.SetBaseType(child, parent);

        symbolTable.Define(parent);
        symbolTable.Define(child);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);
        resolver.ResolveImportedTypeInheritance();

        // Should still be the same parent, not re-resolved
        binding.GetBaseType(child).Should().Be(parent);
    }

    [Fact]
    public void ResolveImportedTypeInheritance_SkipsTypeWithNoUnresolvedNames()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var type = new TypeSymbol { Name = "Standalone", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        symbolTable.Define(type);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);
        resolver.ResolveImportedTypeInheritance();

        binding.GetBaseType(type).Should().BeNull();
        binding.GetInterfaces(type).Should().BeNull();
    }

    [Fact]
    public void ResolveImportedTypeInheritance_HandlesUnresolvableBaseName()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var child = new TypeSymbol
        {
            Name = "Orphan",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedBaseName = "NonExistent"
        };
        symbolTable.Define(child);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);

        // Should not throw - unresolvable names are logged as warnings
        resolver.ResolveImportedTypeInheritance();

        binding.GetBaseType(child).Should().BeNull();
    }

    [Fact]
    public void ResolveImportedTypeInheritance_HandlesUnresolvableInterfaceName()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var impl = new TypeSymbol
        {
            Name = "Impl",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedInterfaceNames = new List<string> { "IMissing" }
        };
        symbolTable.Define(impl);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);

        // Should not throw
        resolver.ResolveImportedTypeInheritance();

        binding.GetInterfaces(impl).Should().BeNull();
    }

    [Fact]
    public void ResolveImportedTypeInheritance_DoesNotDuplicateExistingInterface()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var iface = new TypeSymbol { Name = "IFoo", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var impl = new TypeSymbol
        {
            Name = "Foo",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedInterfaceNames = new List<string> { "IFoo" }
        };

        // Pre-add the interface via SemanticBinding
        binding.AddInterface(impl, iface);

        symbolTable.Define(iface);
        symbolTable.Define(impl);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);
        resolver.ResolveImportedTypeInheritance();

        // After materialization, should have exactly one interface (no duplicates)
        binding.MaterializeInheritance();
        impl.Interfaces.Should().HaveCount(1);
    }

    #endregion

    #region ResolveAll

    [Fact]
    public void ResolveAll_WithoutImportResolver_OnlyResolvesImportedTypeInheritance()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var parent = new TypeSymbol { Name = "Base", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var child = new TypeSymbol
        {
            Name = "Derived",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedBaseName = "Base"
        };

        symbolTable.Define(parent);
        symbolTable.Define(child);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);
        resolver.ResolveAll(importResolver: null);

        binding.GetBaseType(child).Should().Be(parent);
    }

    #endregion

    #region Dual-Read Pattern

    [Fact]
    public void DualRead_PrefersSemanticBinding_OverSymbolProperty()
    {
        // InheritanceResolver's GetBaseType should prefer SemanticBinding data
        // over Symbol.BaseType (which may contain stale/imported data)
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var staleParent = new TypeSymbol { Name = "StaleParent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var correctParent = new TypeSymbol { Name = "CorrectParent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var child = new TypeSymbol
        {
            Name = "Child",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            BaseType = staleParent, // Stale data on Symbol
            UnresolvedBaseName = "CorrectParent"
        };

        // SemanticBinding already has the correct parent
        binding.SetBaseType(child, correctParent);

        symbolTable.Define(staleParent);
        symbolTable.Define(correctParent);
        symbolTable.Define(child);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);
        resolver.ResolveImportedTypeInheritance();

        // Should not overwrite the SemanticBinding value since GetBaseType returns non-null
        binding.GetBaseType(child).Should().Be(correctParent);
    }

    [Fact]
    public void DualRead_FallsBackToSymbolProperty_WhenSemanticBindingEmpty()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var parent = new TypeSymbol { Name = "Parent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var child = new TypeSymbol
        {
            Name = "Child",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            BaseType = parent // Already set on Symbol (e.g., from import)
        };

        symbolTable.Define(parent);
        symbolTable.Define(child);

        var resolver = new InheritanceResolver(symbolTable, semanticBinding: binding);
        resolver.ResolveImportedTypeInheritance();

        // SemanticBinding should remain empty since Symbol.BaseType was already set
        // (the dual-read pattern found it via fallback)
        binding.GetBaseType(child).Should().BeNull("no new binding should be written when Symbol.BaseType is already set");
    }

    #endregion
}
