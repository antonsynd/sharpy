using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for TypeHierarchyService — centralized type hierarchy traversal.
/// </summary>
public class TypeHierarchyServiceTests
{
    // ─── Helpers ─────────────────────────────────────────────────────

    private static TypeSymbol MakeClass(string name, TypeSymbol? baseType = null)
    {
        return new TypeSymbol
        {
            Name = name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            BaseType = baseType
        };
    }

    private static TypeSymbol MakeInterface(string name)
    {
        return new TypeSymbol
        {
            Name = name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface
        };
    }

    private static InterfaceReference MakeRef(TypeSymbol iface)
    {
        return new InterfaceReference { Definition = iface };
    }

    // ─── GetAllBaseTypes ─────────────────────────────────────────────

    [Fact]
    public void GetAllBaseTypes_NoInheritance_ReturnsEmpty()
    {
        var cls = MakeClass("Leaf");

        var bases = TypeHierarchyService.GetAllBaseTypes(cls);

        bases.Should().BeEmpty();
    }

    [Fact]
    public void GetAllBaseTypes_SingleInheritance_ReturnsParent()
    {
        var parent = MakeClass("Parent");
        var child = MakeClass("Child", parent);

        var bases = TypeHierarchyService.GetAllBaseTypes(child);

        bases.Should().ContainSingle().Which.Should().BeSameAs(parent);
    }

    [Fact]
    public void GetAllBaseTypes_ThreeLevelChain_ReturnsInOrder()
    {
        var grandparent = MakeClass("GrandParent");
        var parent = MakeClass("Parent", grandparent);
        var child = MakeClass("Child", parent);

        var bases = TypeHierarchyService.GetAllBaseTypes(child);

        bases.Should().HaveCount(2);
        bases[0].Should().BeSameAs(parent);
        bases[1].Should().BeSameAs(grandparent);
    }

    [Fact]
    public void GetAllBaseTypes_CycleDetection_DoesNotInfiniteLoop()
    {
        // Create a cycle: A -> B -> A
        var a = MakeClass("A");
        var b = MakeClass("B", a);
        // Mutate to create cycle
        a.BaseType = b;

        var bases = TypeHierarchyService.GetAllBaseTypes(a);

        // Should terminate and include b (but not revisit a)
        bases.Should().ContainSingle().Which.Should().BeSameAs(b);
    }

    // ─── GetAllInterfaces ────────────────────────────────────────────

    [Fact]
    public void GetAllInterfaces_NoInterfaces_ReturnsEmpty()
    {
        var cls = MakeClass("Plain");

        var ifaces = TypeHierarchyService.GetAllInterfaces(cls);

        ifaces.Should().BeEmpty();
    }

    [Fact]
    public void GetAllInterfaces_DirectInterface_ReturnsIt()
    {
        var iface = MakeInterface("IFoo");
        var cls = new TypeSymbol
        {
            Name = "Foo",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Interfaces = { MakeRef(iface) }
        };

        var ifaces = TypeHierarchyService.GetAllInterfaces(cls);

        ifaces.Should().ContainSingle().Which.Should().BeSameAs(iface);
    }

    [Fact]
    public void GetAllInterfaces_DiamondPattern_DeduplicatesInterfaces()
    {
        // IBase <- ILeft, IRight; class implements ILeft + IRight
        var iBase = MakeInterface("IBase");
        var iLeft = new TypeSymbol
        {
            Name = "ILeft",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            Interfaces = { MakeRef(iBase) }
        };
        var iRight = new TypeSymbol
        {
            Name = "IRight",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            Interfaces = { MakeRef(iBase) }
        };
        var cls = new TypeSymbol
        {
            Name = "Diamond",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Interfaces = { MakeRef(iLeft), MakeRef(iRight) }
        };

        var ifaces = TypeHierarchyService.GetAllInterfaces(cls);

        // Should include ILeft, IRight, IBase — no duplicates
        ifaces.Should().HaveCount(3);
        ifaces.Should().Contain(iBase);
        ifaces.Should().Contain(iLeft);
        ifaces.Should().Contain(iRight);
    }

    [Fact]
    public void GetAllInterfaces_InheritedFromBaseClass_IncludesParentInterfaces()
    {
        var iface = MakeInterface("ISized");
        var parent = new TypeSymbol
        {
            Name = "Parent",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Interfaces = { MakeRef(iface) }
        };
        var child = MakeClass("Child", parent);

        var ifaces = TypeHierarchyService.GetAllInterfaces(child);

        ifaces.Should().ContainSingle().Which.Should().BeSameAs(iface);
    }

    // ─── InheritsFrom ────────────────────────────────────────────────

    [Fact]
    public void InheritsFrom_DirectParent_ReturnsTrue()
    {
        var parent = MakeClass("Parent");
        var child = MakeClass("Child", parent);

        TypeHierarchyService.InheritsFrom(child, parent).Should().BeTrue();
    }

    [Fact]
    public void InheritsFrom_GrandParent_ReturnsTrue()
    {
        var grandparent = MakeClass("GrandParent");
        var parent = MakeClass("Parent", grandparent);
        var child = MakeClass("Child", parent);

        TypeHierarchyService.InheritsFrom(child, grandparent).Should().BeTrue();
    }

    [Fact]
    public void InheritsFrom_UnrelatedType_ReturnsFalse()
    {
        var a = MakeClass("A");
        var b = MakeClass("B");

        TypeHierarchyService.InheritsFrom(a, b).Should().BeFalse();
    }

    [Fact]
    public void InheritsFrom_NullDerived_ReturnsFalse()
    {
        var parent = MakeClass("Parent");

        TypeHierarchyService.InheritsFrom(null, parent).Should().BeFalse();
    }

    [Fact]
    public void InheritsFrom_DirectInterface_ReturnsTrue()
    {
        var iface = MakeInterface("IFoo");
        var cls = new TypeSymbol
        {
            Name = "Foo",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Interfaces = { MakeRef(iface) }
        };

        TypeHierarchyService.InheritsFrom(cls, iface).Should().BeTrue();
    }

    // ─── FindField ───────────────────────────────────────────────────

    [Fact]
    public void FindField_OnType_ReturnsFieldAndOwner()
    {
        var field = new VariableSymbol { Name = "x" };
        var cls = new TypeSymbol
        {
            Name = "Point",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Fields = { field }
        };

        var (found, owner) = TypeHierarchyService.FindField(cls, "x");

        found.Should().BeSameAs(field);
        owner.Should().BeSameAs(cls);
    }

    [Fact]
    public void FindField_InheritedFromParent_ReturnsFieldAndParent()
    {
        var field = new VariableSymbol { Name = "y" };
        var parent = new TypeSymbol
        {
            Name = "Base",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Fields = { field }
        };
        var child = MakeClass("Derived", parent);

        var (found, owner) = TypeHierarchyService.FindField(child, "y");

        found.Should().BeSameAs(field);
        owner.Should().BeSameAs(parent);
    }

    [Fact]
    public void FindField_NotFound_ReturnsNull()
    {
        var cls = MakeClass("Empty");

        var (found, owner) = TypeHierarchyService.FindField(cls, "missing");

        found.Should().BeNull();
        owner.Should().BeNull();
    }

    // ─── FindMethod ──────────────────────────────────────────────────

    [Fact]
    public void FindMethod_OnInterface_ReturnsMethodAndInterface()
    {
        var method = new FunctionSymbol { Name = "do_something" };
        var iface = new TypeSymbol
        {
            Name = "IDoer",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            Methods = { method }
        };
        var cls = new TypeSymbol
        {
            Name = "Doer",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Interfaces = { MakeRef(iface) }
        };

        var (found, owner) = TypeHierarchyService.FindMethod(cls, "do_something");

        found.Should().BeSameAs(method);
        owner.Should().BeSameAs(iface);
    }

    [Fact]
    public void FindMethod_DerivedOverridesBase_ReturnsDerivedVersion()
    {
        var baseMethod = new FunctionSymbol { Name = "run" };
        var derivedMethod = new FunctionSymbol { Name = "run" };
        var parent = new TypeSymbol
        {
            Name = "Base",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Methods = { baseMethod }
        };
        var child = new TypeSymbol
        {
            Name = "Derived",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            BaseType = parent,
            Methods = { derivedMethod }
        };

        var (found, owner) = TypeHierarchyService.FindMethod(child, "run");

        found.Should().BeSameAs(derivedMethod);
        owner.Should().BeSameAs(child);
    }

    // ─── FindProperty ────────────────────────────────────────────────

    [Fact]
    public void FindProperty_OnInterfaceViaSearch_ReturnsPropertyAndInterface()
    {
        var prop = new PropertySymbol { Name = "count", HasGetter = true };
        var iface = new TypeSymbol
        {
            Name = "ISized",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            Properties = { prop }
        };
        var cls = new TypeSymbol
        {
            Name = "MyList",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Interfaces = { MakeRef(iface) }
        };

        var (found, owner) = TypeHierarchyService.FindProperty(cls, "count");

        found.Should().BeSameAs(prop);
        owner.Should().BeSameAs(iface);
    }

    // ─── CollectAllMethods ───────────────────────────────────────────

    [Fact]
    public void CollectAllMethods_MergesBaseAndDerived_PrefersDerived()
    {
        var baseOnly = new FunctionSymbol { Name = "base_only" };
        var baseRun = new FunctionSymbol { Name = "run" };
        var derivedRun = new FunctionSymbol { Name = "run" };
        var derivedOnly = new FunctionSymbol { Name = "derived_only" };

        var parent = new TypeSymbol
        {
            Name = "Base",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Methods = { baseOnly, baseRun }
        };
        var child = new TypeSymbol
        {
            Name = "Derived",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            BaseType = parent,
            Methods = { derivedRun, derivedOnly }
        };

        var methods = TypeHierarchyService.CollectAllMethods(child);

        methods.Should().HaveCount(3);
        methods["run"].Should().BeSameAs(derivedRun);
        methods["base_only"].Should().BeSameAs(baseOnly);
        methods["derived_only"].Should().BeSameAs(derivedOnly);
    }

    // ─── GetAncestorChain ────────────────────────────────────────────

    [Fact]
    public void GetAncestorChain_SingleClass_EndsWithObject()
    {
        var cls = MakeClass("Leaf");
        var udt = new UserDefinedType { Name = "Leaf", Symbol = cls };

        var chain = TypeHierarchyService.GetAncestorChain(udt);

        chain.Should().HaveCount(2);
        chain[0].Should().BeSameAs(udt);
        chain[1].Should().Be(SemanticType.Object);
    }

    [Fact]
    public void GetAncestorChain_WithBase_IncludesAllAncestors()
    {
        var grandparent = MakeClass("GrandParent");
        var parent = MakeClass("Parent", grandparent);
        var child = MakeClass("Child", parent);
        var udt = new UserDefinedType { Name = "Child", Symbol = child };

        var chain = TypeHierarchyService.GetAncestorChain(udt);

        chain.Should().HaveCount(4); // Child, Parent, GrandParent, object
        chain[0].Should().BeSameAs(udt);
        chain[1].GetDisplayName().Should().Be("Parent");
        chain[2].GetDisplayName().Should().Be("GrandParent");
        chain[3].Should().Be(SemanticType.Object);
    }

    // ─── SemanticBinding integration ─────────────────────────────────

    [Fact]
    public void GetAllBaseTypes_WithBinding_UsesBindingOverSymbol()
    {
        var bindingParent = MakeClass("BindingParent");
        var cls = MakeClass("Child");
        // No BaseType on symbol — it's in the binding

        var binding = new SemanticBinding();
        binding.SetBaseType(cls, bindingParent);

        var bases = TypeHierarchyService.GetAllBaseTypes(cls, binding);

        bases.Should().ContainSingle().Which.Should().BeSameAs(bindingParent);
    }

    [Fact]
    public void GetAllInterfaces_WithBinding_UsesBindingOverSymbol()
    {
        var iface = MakeInterface("IBound");
        var cls = MakeClass("MyClass");
        // No Interfaces on symbol — set via binding

        var binding = new SemanticBinding();
        binding.AddInterface(cls, new InterfaceReference { Definition = iface });

        var ifaces = TypeHierarchyService.GetAllInterfaces(cls, binding);

        ifaces.Should().ContainSingle().Which.Should().BeSameAs(iface);
    }
}
