using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for DualWriteAssertions: assertions that verify materialization consistency
/// between SemanticBinding stores and Symbol properties.
/// These assertions are always active (not DEBUG-only) to catch issues in production.
/// Tests validate that assertions pass for consistent state, skip CLR/re-exported symbols,
/// and throw InvalidOperationException for inconsistent state.
/// </summary>
public class DualWriteAssertionsTests
{
    private static SymbolTable CreateSymbolTable()
    {
        var builtins = new BuiltinRegistry();
        return new SymbolTable(builtins);
    }

    #region AssertInheritanceConsistency

    [Fact]
    public void AssertInheritanceConsistency_PassesForConsistentState()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var parent = new TypeSymbol { Name = "Parent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var child = new TypeSymbol { Name = "Child", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var iface = new TypeSymbol { Name = "IFoo", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };

        symbolTable.Define(parent);
        symbolTable.Define(child);
        symbolTable.Define(iface);

        binding.SetBaseType(child, parent);
        binding.AddInterface(child, iface);
        binding.MaterializeInheritance();

        // Should not throw
        DualWriteAssertions.AssertInheritanceConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertInheritanceConsistency_PassesForTypesWithNoInheritance()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var standalone = new TypeSymbol { Name = "Standalone", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        symbolTable.Define(standalone);

        // No inheritance set, should pass
        DualWriteAssertions.AssertInheritanceConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertInheritanceConsistency_SkipsClrTypes()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        // CLR types have ClrType set (e.g., from ModuleRegistry)
        // They should be skipped even if their BaseType is set but not in SemanticBinding
        var clrType = new TypeSymbol
        {
            Name = "String",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            ClrType = typeof(string),
            BaseType = new TypeSymbol { Name = "Object", Kind = SymbolKind.Type, TypeKind = TypeKind.Class }
        };
        symbolTable.Define(clrType);

        // Should not throw even though CLR type has BaseType not in SemanticBinding
        DualWriteAssertions.AssertInheritanceConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertInheritanceConsistency_PassesWithMultipleInterfaces()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var iface1 = new TypeSymbol { Name = "IFoo", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var iface2 = new TypeSymbol { Name = "IBar", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var impl = new TypeSymbol { Name = "Impl", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        symbolTable.Define(iface1);
        symbolTable.Define(iface2);
        symbolTable.Define(impl);

        binding.AddInterface(impl, iface1);
        binding.AddInterface(impl, iface2);
        binding.MaterializeInheritance();

        DualWriteAssertions.AssertInheritanceConsistency(symbolTable, binding);
    }

    #endregion

    #region AssertCodeGenInfoConsistency

    [Fact]
    public void AssertCodeGenInfoConsistency_PassesForConsistentState()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var symbol = new TypeSymbol { Name = "my_class", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var info = new CodeGenInfo { CSharpName = "MyClass", OriginalName = "my_class" };

        symbolTable.Define(symbol);
        binding.SetCodeGenInfo(symbol, info);
        binding.MaterializeCodeGenInfo();

        DualWriteAssertions.AssertCodeGenInfoConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertCodeGenInfoConsistency_PassesForSymbolsWithNoCodeGenInfo()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var symbol = new TypeSymbol { Name = "Foo", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        symbolTable.Define(symbol);

        // No CodeGenInfo set, should pass
        DualWriteAssertions.AssertCodeGenInfoConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertCodeGenInfoConsistency_SkipsReExportedSymbols()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        // Re-exported symbols have their CodeGenInfo from a different compilation's SemanticBinding
        var reExported = new TypeSymbol
        {
            Name = "ReExported",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            IsReExport = true,
            OriginalModule = "other_module",
            CodeGenInfo = new CodeGenInfo { CSharpName = "ReExported", OriginalName = "re_exported" }
        };
        symbolTable.Define(reExported);

        // Should not throw even though CodeGenInfo is set on Symbol but not in this SemanticBinding
        DualWriteAssertions.AssertCodeGenInfoConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertCodeGenInfoConsistency_PassesForMultipleSymbols()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var sym1 = new TypeSymbol { Name = "foo", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var sym2 = new FunctionSymbol { Name = "bar", Kind = SymbolKind.Function };
        var info1 = new CodeGenInfo { CSharpName = "Foo", OriginalName = "foo" };
        var info2 = new CodeGenInfo { CSharpName = "Bar", OriginalName = "bar" };

        symbolTable.Define(sym1);
        symbolTable.Define(sym2);
        binding.SetCodeGenInfo(sym1, info1);
        binding.SetCodeGenInfo(sym2, info2);
        binding.MaterializeCodeGenInfo();

        DualWriteAssertions.AssertCodeGenInfoConsistency(symbolTable, binding);
    }

    #endregion

    #region AssertVariableTypeConsistency

    [Fact]
    public void AssertVariableTypeConsistency_PassesForConsistentGlobalVariable()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var varSymbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        symbolTable.Define(varSymbol);
        binding.SetVariableType(varSymbol, SemanticType.Int);
        binding.MaterializeVariableTypes();

        DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertVariableTypeConsistency_PassesForUnknownTypeVariables()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        // Variables with Unknown type are skipped (type not yet resolved)
        var varSymbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        symbolTable.Define(varSymbol);

        DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertVariableTypeConsistency_SkipsReExportedVariables()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var reExported = new VariableSymbol
        {
            Name = "imported_var",
            Kind = SymbolKind.Variable,
            IsReExport = true,
            OriginalModule = "other_module",
            Type = SemanticType.Int
        };
        symbolTable.Define(reExported);

        // Should not throw even though Type is set but not in this SemanticBinding
        DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertVariableTypeConsistency_PassesForConsistentTypeFields()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var field = new VariableSymbol { Name = "value", Kind = SymbolKind.Variable };
        var typeSymbol = new TypeSymbol
        {
            Name = "MyClass",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Fields = new List<VariableSymbol> { field }
        };

        symbolTable.Define(typeSymbol);
        binding.SetVariableType(field, SemanticType.Str);
        binding.MaterializeVariableTypes();

        DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertVariableTypeConsistency_SkipsClrTypeFields()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        // CLR types have fields typed by .NET reflection, not by SemanticBinding
        var clrType = new TypeSymbol
        {
            Name = "String",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            ClrType = typeof(string),
            Fields = new List<VariableSymbol>
            {
                new VariableSymbol { Name = "Length", Kind = SymbolKind.Variable, Type = SemanticType.Int }
            }
        };
        symbolTable.Define(clrType);

        // Should not throw even though field has type not in SemanticBinding
        DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertVariableTypeConsistency_SkipsImportedTypeFields()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        // Types with DefiningModule are imported and have fields typed in another compilation
        var importedType = new TypeSymbol
        {
            Name = "ImportedClass",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            DefiningModule = "other_module",
            Fields = new List<VariableSymbol>
            {
                new VariableSymbol { Name = "data", Kind = SymbolKind.Variable, Type = SemanticType.Str }
            }
        };
        symbolTable.Define(importedType);

        // Should not throw
        DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, binding);
    }

    #endregion

    #region Reverse Direction: SemanticBinding → Symbol

    [Fact]
    public void AssertInheritanceConsistency_ReverseDirection_PassesAfterMaterialization()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var parent = new TypeSymbol { Name = "Parent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var child = new TypeSymbol { Name = "Child", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        symbolTable.Define(parent);
        symbolTable.Define(child);

        binding.SetBaseType(child, parent);
        binding.MaterializeInheritance();

        // Reverse check: SemanticBinding has BaseType → Symbol should too
        DualWriteAssertions.AssertInheritanceConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertCodeGenInfoConsistency_ReverseDirection_PassesAfterMaterialization()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var symbol = new TypeSymbol { Name = "my_class", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var info = new CodeGenInfo { CSharpName = "MyClass", OriginalName = "my_class" };

        symbolTable.Define(symbol);
        binding.SetCodeGenInfo(symbol, info);
        binding.MaterializeCodeGenInfo();

        // Reverse check: SemanticBinding has CodeGenInfo → Symbol should too
        DualWriteAssertions.AssertCodeGenInfoConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertVariableTypeConsistency_ReverseDirection_PassesAfterMaterialization()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var varSymbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        symbolTable.Define(varSymbol);
        binding.SetVariableType(varSymbol, SemanticType.Int);
        binding.MaterializeVariableTypes();

        // Reverse check: SemanticBinding has Type → Symbol should too
        DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertVariableTypeConsistency_ReverseDirection_PassesForFieldsAfterMaterialization()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var field = new VariableSymbol { Name = "value", Kind = SymbolKind.Variable };
        var typeSymbol = new TypeSymbol
        {
            Name = "MyClass",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Fields = new List<VariableSymbol> { field }
        };

        symbolTable.Define(typeSymbol);
        binding.SetVariableType(field, SemanticType.Str);
        binding.MaterializeVariableTypes();

        // Reverse check: SemanticBinding has field Type → Symbol field should too
        DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, binding);
    }

    [Fact]
    public void AssertInheritanceConsistency_ReverseDirection_PassesForInterfaces()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var iface1 = new TypeSymbol { Name = "IFoo", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var iface2 = new TypeSymbol { Name = "IBar", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var impl = new TypeSymbol { Name = "Impl", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        symbolTable.Define(iface1);
        symbolTable.Define(iface2);
        symbolTable.Define(impl);

        binding.AddInterface(impl, iface1);
        binding.AddInterface(impl, iface2);
        binding.MaterializeInheritance();

        // Reverse check: SemanticBinding has interfaces → Symbol should too
        DualWriteAssertions.AssertInheritanceConsistency(symbolTable, binding);
    }

    #endregion

    #region End-to-End Materialization Consistency

    [Fact]
    public void AllAssertions_PassForFullMaterializationCycle()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        // Set up a type with inheritance, fields, and CodeGenInfo
        var parent = new TypeSymbol { Name = "Base", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var iface = new TypeSymbol { Name = "ISerializable", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var field = new VariableSymbol { Name = "name", Kind = SymbolKind.Variable };
        var child = new TypeSymbol
        {
            Name = "Derived",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Fields = new List<VariableSymbol> { field }
        };
        var globalVar = new VariableSymbol { Name = "counter", Kind = SymbolKind.Variable };

        symbolTable.Define(parent);
        symbolTable.Define(iface);
        symbolTable.Define(child);
        symbolTable.Define(globalVar);

        // Phase 1: Inheritance
        binding.SetBaseType(child, parent);
        binding.AddInterface(child, iface);
        binding.MaterializeInheritance();
        DualWriteAssertions.AssertInheritanceConsistency(symbolTable, binding);
        binding.FreezeInheritance();

        // Phase 2: Type checking
        binding.SetVariableType(field, SemanticType.Str);
        binding.SetVariableType(globalVar, SemanticType.Int);
        binding.SetCodeGenInfo(child, new CodeGenInfo { CSharpName = "Derived", OriginalName = "derived" });
        binding.SetCodeGenInfo(parent, new CodeGenInfo { CSharpName = "Base", OriginalName = "base" });

        binding.MaterializeCodeGenInfo();
        binding.MaterializeVariableTypes();
        DualWriteAssertions.AssertCodeGenInfoConsistency(symbolTable, binding);
        DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, binding);
        binding.FreezeCodeGenInfo();
        binding.FreezeVariableTypes();

        // Verify final state
        child.BaseType.Should().Be(parent);
        child.Interfaces.Should().Contain(r => r.Definition == iface);
        field.Type.Should().Be(SemanticType.Str);
        globalVar.Type.Should().Be(SemanticType.Int);
        child.CodeGenInfo.Should().NotBeNull();
    }

    #endregion

    #region Inconsistency Detection (Throws)

    [Fact]
    public void AssertInheritanceConsistency_ThrowsForMissingMaterialization_BaseType()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var parent = new TypeSymbol { Name = "Parent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var child = new TypeSymbol { Name = "Child", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        symbolTable.Define(parent);
        symbolTable.Define(child);

        binding.SetBaseType(child, parent);
        // Intentionally skip MaterializeInheritance()

        // SemanticBinding has BaseType but Symbol doesn't - should throw
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DualWriteAssertions.AssertInheritanceConsistency(symbolTable, binding));
        ex.Message.Should().Contain("materialization missed");
        ex.Message.Should().Contain("Child");
    }

    [Fact]
    public void AssertInheritanceConsistency_ThrowsForMissingMaterialization_Interfaces()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var iface = new TypeSymbol { Name = "IFoo", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var impl = new TypeSymbol { Name = "Impl", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        symbolTable.Define(iface);
        symbolTable.Define(impl);

        binding.AddInterface(impl, iface);
        // Intentionally skip MaterializeInheritance()

        // SemanticBinding has Interface but Symbol doesn't - should throw
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DualWriteAssertions.AssertInheritanceConsistency(symbolTable, binding));
        ex.Message.Should().Contain("materialization missed");
        ex.Message.Should().Contain("Impl");
    }

    [Fact]
    public void AssertCodeGenInfoConsistency_ThrowsForMissingMaterialization()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var symbol = new TypeSymbol { Name = "MyClass", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var info = new CodeGenInfo { CSharpName = "MyClass", OriginalName = "my_class" };

        symbolTable.Define(symbol);
        binding.SetCodeGenInfo(symbol, info);
        // Intentionally skip MaterializeCodeGenInfo()

        // SemanticBinding has CodeGenInfo but Symbol doesn't - should throw
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DualWriteAssertions.AssertCodeGenInfoConsistency(symbolTable, binding));
        ex.Message.Should().Contain("materialization missed");
        ex.Message.Should().Contain("MyClass");
    }

    [Fact]
    public void AssertVariableTypeConsistency_ThrowsForMissingMaterialization()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var varSymbol = new VariableSymbol { Name = "counter", Kind = SymbolKind.Variable };
        symbolTable.Define(varSymbol);
        binding.SetVariableType(varSymbol, SemanticType.Int);
        // Intentionally skip MaterializeVariableTypes()

        // SemanticBinding has Type but Symbol doesn't - should throw
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, binding));
        ex.Message.Should().Contain("materialization missed");
        ex.Message.Should().Contain("counter");
    }

    [Fact]
    public void AssertVariableTypeConsistency_ThrowsForMissingFieldMaterialization()
    {
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var field = new VariableSymbol { Name = "data", Kind = SymbolKind.Variable };
        var typeSymbol = new TypeSymbol
        {
            Name = "Container",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Fields = new List<VariableSymbol> { field }
        };

        symbolTable.Define(typeSymbol);
        binding.SetVariableType(field, SemanticType.Str);
        // Intentionally skip MaterializeVariableTypes()

        // SemanticBinding has field Type but Symbol field doesn't - should throw
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, binding));
        ex.Message.Should().Contain("materialization missed");
        ex.Message.Should().Contain("Container");
        ex.Message.Should().Contain("data");
    }

    #endregion
}
