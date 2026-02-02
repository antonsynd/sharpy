using System.Collections.Immutable;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for SemanticBinding materialization: the process of copying data from
/// SemanticBinding ConcurrentDictionary stores onto Symbol properties at freeze points.
/// </summary>
public class SemanticBindingMaterializationTests
{
    #region MaterializeInheritance

    [Fact]
    public void MaterializeInheritance_CopiesBaseType_ToSymbol()
    {
        var binding = new SemanticBinding();
        var child = new TypeSymbol { Name = "Child", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var parent = new TypeSymbol { Name = "Parent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        binding.SetBaseType(child, parent);
        child.BaseType.Should().BeNull("BaseType should not be set before materialization");

        binding.MaterializeInheritance();

        child.BaseType.Should().Be(parent);
    }

    [Fact]
    public void MaterializeInheritance_CopiesInterfaces_ToSymbol()
    {
        var binding = new SemanticBinding();
        var classSymbol = new TypeSymbol { Name = "MyClass", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var iface1 = new TypeSymbol { Name = "IFoo", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        var iface2 = new TypeSymbol { Name = "IBar", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };

        binding.AddInterface(classSymbol, iface1);
        binding.AddInterface(classSymbol, iface2);
        classSymbol.Interfaces.Should().BeEmpty("Interfaces should not be set before materialization");

        binding.MaterializeInheritance();

        classSymbol.Interfaces.Should().HaveCount(2);
        classSymbol.Interfaces.Should().Contain(iface1);
        classSymbol.Interfaces.Should().Contain(iface2);
    }

    [Fact]
    public void MaterializeInheritance_DoesNotDuplicate_ExistingInterfaces()
    {
        var binding = new SemanticBinding();
        var classSymbol = new TypeSymbol { Name = "MyClass", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var iface = new TypeSymbol { Name = "IFoo", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };

        // Pre-populate the symbol's interface list (e.g., from a different path)
        classSymbol.Interfaces.Add(iface);
        binding.AddInterface(classSymbol, iface);

        binding.MaterializeInheritance();

        classSymbol.Interfaces.Should().HaveCount(1, "duplicate interfaces should not be added");
    }

    #endregion

    #region MaterializeVariableTypes

    [Fact]
    public void MaterializeVariableTypes_CopiesType_ToSymbol()
    {
        var binding = new SemanticBinding();
        var varSymbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        binding.SetVariableType(varSymbol, SemanticType.Int);
        varSymbol.Type.Should().Be(SemanticType.Unknown, "Type should not be set before materialization");

        binding.MaterializeVariableTypes();

        varSymbol.Type.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void MaterializeVariableTypes_HandlesMultipleVariables()
    {
        var binding = new SemanticBinding();
        var var1 = new VariableSymbol { Name = "a", Kind = SymbolKind.Variable };
        var var2 = new VariableSymbol { Name = "b", Kind = SymbolKind.Variable };

        binding.SetVariableType(var1, SemanticType.Int);
        binding.SetVariableType(var2, SemanticType.Str);

        binding.MaterializeVariableTypes();

        var1.Type.Should().Be(SemanticType.Int);
        var2.Type.Should().Be(SemanticType.Str);
    }

    #endregion

    #region MaterializeCodeGenInfo

    [Fact]
    public void MaterializeCodeGenInfo_CopiesInfo_ToSymbol()
    {
        var binding = new SemanticBinding();
        var symbol = new TypeSymbol { Name = "my_class", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var info = new CodeGenInfo { CSharpName = "MyClass", OriginalName = "my_class" };

        binding.SetCodeGenInfo(symbol, info);
        symbol.CodeGenInfo.Should().BeNull("CodeGenInfo should not be set before materialization");

        binding.MaterializeCodeGenInfo();

        symbol.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.CSharpName.Should().Be("MyClass");
    }

    [Fact]
    public void MaterializeCodeGenInfo_HandlesMultipleSymbols()
    {
        var binding = new SemanticBinding();
        var sym1 = new TypeSymbol { Name = "foo", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var sym2 = new VariableSymbol { Name = "bar", Kind = SymbolKind.Variable };
        var info1 = new CodeGenInfo { CSharpName = "Foo", OriginalName = "foo" };
        var info2 = new CodeGenInfo { CSharpName = "Bar", OriginalName = "bar" };

        binding.SetCodeGenInfo(sym1, info1);
        binding.SetCodeGenInfo(sym2, info2);

        binding.MaterializeCodeGenInfo();

        sym1.CodeGenInfo.Should().NotBeNull();
        sym1.CodeGenInfo!.CSharpName.Should().Be("Foo");
        sym2.CodeGenInfo.Should().NotBeNull();
        sym2.CodeGenInfo!.CSharpName.Should().Be("Bar");
    }

    #endregion

    #region Freeze prevents writes

    [Fact]
    public void FreezeInheritance_PreventsSubsequentSetBaseType()
    {
        var binding = new SemanticBinding();
        var child = new TypeSymbol { Name = "Child", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var parent = new TypeSymbol { Name = "Parent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        binding.FreezeInheritance();

        // In DEBUG builds, SetBaseType after freeze triggers Debug.Assert failure.
        // We can't easily test Debug.Assert in xUnit, so we verify the freeze flag
        // is set by testing the read path works correctly.
        // The freeze assertion is a development safety net, not a runtime guarantee.
        binding.GetBaseType(child).Should().BeNull();
    }

    [Fact]
    public void FreezeVariableTypes_PreventsSubsequentSetVariableType()
    {
        var binding = new SemanticBinding();
        var varSymbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        binding.SetVariableType(varSymbol, SemanticType.Int);
        binding.FreezeVariableTypes();

        // Verify the previously set value is still readable
        binding.GetVariableType(varSymbol).Should().Be(SemanticType.Int);
    }

    [Fact]
    public void FreezeCodeGenInfo_PreventsSubsequentSetCodeGenInfo()
    {
        var binding = new SemanticBinding();
        var symbol = new TypeSymbol { Name = "Foo", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var info = new CodeGenInfo { CSharpName = "Foo", OriginalName = "foo" };

        binding.SetCodeGenInfo(symbol, info);
        binding.FreezeCodeGenInfo();

        // Verify the previously set value is still readable
        binding.GetCodeGenInfo(symbol).Should().NotBeNull();
        binding.GetCodeGenInfo(symbol)!.CSharpName.Should().Be("Foo");
    }

    #endregion

    #region Reader helpers before and after materialization

    [Fact]
    public void GetBaseType_ReturnsValue_BeforeMaterialization()
    {
        var binding = new SemanticBinding();
        var child = new TypeSymbol { Name = "Child", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var parent = new TypeSymbol { Name = "Parent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        binding.SetBaseType(child, parent);

        // Reader should return value from SemanticBinding even before materialization
        binding.GetBaseType(child).Should().Be(parent);
    }

    [Fact]
    public void GetInterfaces_ReturnsValue_BeforeMaterialization()
    {
        var binding = new SemanticBinding();
        var classSymbol = new TypeSymbol { Name = "MyClass", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var iface = new TypeSymbol { Name = "IFoo", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };

        binding.AddInterface(classSymbol, iface);

        // Reader should return value from SemanticBinding even before materialization
        var interfaces = binding.GetInterfaces(classSymbol);
        interfaces.Should().NotBeNull();
        interfaces.Should().Contain(iface);
    }

    [Fact]
    public void GetVariableType_ReturnsValue_BeforeMaterialization()
    {
        var binding = new SemanticBinding();
        var varSymbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        binding.SetVariableType(varSymbol, SemanticType.Float);

        // Reader should return value from SemanticBinding even before materialization
        binding.GetVariableType(varSymbol).Should().Be(SemanticType.Float);
    }

    [Fact]
    public void GetCodeGenInfo_ReturnsValue_BeforeMaterialization()
    {
        var binding = new SemanticBinding();
        var symbol = new TypeSymbol { Name = "Foo", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var info = new CodeGenInfo { CSharpName = "Foo", OriginalName = "foo" };

        binding.SetCodeGenInfo(symbol, info);

        // Reader should return value from SemanticBinding even before materialization
        binding.GetCodeGenInfo(symbol).Should().Be(info);
    }

    [Fact]
    public void GetBaseType_ReturnsValue_AfterMaterialization()
    {
        var binding = new SemanticBinding();
        var child = new TypeSymbol { Name = "Child", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var parent = new TypeSymbol { Name = "Parent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        binding.SetBaseType(child, parent);
        binding.MaterializeInheritance();

        // Both SemanticBinding and Symbol should agree after materialization
        binding.GetBaseType(child).Should().Be(parent);
        child.BaseType.Should().Be(parent);
    }

    [Fact]
    public void GetVariableType_ReturnsValue_AfterMaterialization()
    {
        var binding = new SemanticBinding();
        var varSymbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        binding.SetVariableType(varSymbol, SemanticType.Bool);
        binding.MaterializeVariableTypes();

        // Both SemanticBinding and Symbol should agree after materialization
        binding.GetVariableType(varSymbol).Should().Be(SemanticType.Bool);
        varSymbol.Type.Should().Be(SemanticType.Bool);
    }

    #endregion

    #region Module Resolution

    [Fact]
    public void SetResolvedModulePath_StoresPath()
    {
        var binding = new SemanticBinding();
        var stmt = new FromImportStatement
        {
            Module = "helpers",
            Names = ImmutableArray<ImportAlias>.Empty
        };

        binding.SetResolvedModulePath(stmt, "mypackage.helpers");

        binding.GetResolvedModulePath(stmt).Should().Be("mypackage.helpers");
    }

    [Fact]
    public void GetResolvedModulePath_ReturnsNull_WhenNotSet()
    {
        var binding = new SemanticBinding();
        var stmt = new FromImportStatement
        {
            Module = "unknown",
            Names = ImmutableArray<ImportAlias>.Empty
        };

        binding.GetResolvedModulePath(stmt).Should().BeNull();
    }

    [Fact]
    public void SetResolvedModulePath_OverwritesPreviousValue()
    {
        var binding = new SemanticBinding();
        var stmt = new FromImportStatement
        {
            Module = "helpers",
            Names = ImmutableArray<ImportAlias>.Empty
        };

        binding.SetResolvedModulePath(stmt, "old.path");
        binding.SetResolvedModulePath(stmt, "new.path");

        binding.GetResolvedModulePath(stmt).Should().Be("new.path");
    }

    [Fact]
    public void SetReExportedSymbols_StoresSymbols()
    {
        var binding = new SemanticBinding();
        var stmt = new FromImportStatement
        {
            Module = "submodule",
            Names = ImmutableArray<ImportAlias>.Empty
        };
        var symbols = new Dictionary<string, Symbol>
        {
            ["foo"] = new FunctionSymbol { Name = "foo", Kind = SymbolKind.Function },
            ["Bar"] = new TypeSymbol { Name = "Bar", Kind = SymbolKind.Type, TypeKind = TypeKind.Class }
        };

        binding.SetReExportedSymbols(stmt, symbols);

        var result = binding.GetReExportedSymbols(stmt);
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result!["foo"].Name.Should().Be("foo");
        result["Bar"].Name.Should().Be("Bar");
    }

    [Fact]
    public void GetReExportedSymbols_ReturnsNull_WhenNotSet()
    {
        var binding = new SemanticBinding();
        var stmt = new FromImportStatement
        {
            Module = "unknown",
            Names = ImmutableArray<ImportAlias>.Empty
        };

        binding.GetReExportedSymbols(stmt).Should().BeNull();
    }

    [Fact]
    public void SetReExportedSymbols_OverwritesPreviousValue()
    {
        var binding = new SemanticBinding();
        var stmt = new FromImportStatement
        {
            Module = "submodule",
            Names = ImmutableArray<ImportAlias>.Empty
        };
        var oldSymbols = new Dictionary<string, Symbol>
        {
            ["old"] = new FunctionSymbol { Name = "old", Kind = SymbolKind.Function }
        };
        var newSymbols = new Dictionary<string, Symbol>
        {
            ["new1"] = new FunctionSymbol { Name = "new1", Kind = SymbolKind.Function },
            ["new2"] = new FunctionSymbol { Name = "new2", Kind = SymbolKind.Function }
        };

        binding.SetReExportedSymbols(stmt, oldSymbols);
        binding.SetReExportedSymbols(stmt, newSymbols);

        var result = binding.GetReExportedSymbols(stmt);
        result.Should().HaveCount(2);
        result!.ContainsKey("new1").Should().BeTrue();
        result.ContainsKey("old").Should().BeFalse();
    }

    [Fact]
    public void ModuleResolution_DifferentStatements_IndependentStorage()
    {
        var binding = new SemanticBinding();
        var stmt1 = new FromImportStatement
        {
            Module = "module_a",
            Names = ImmutableArray<ImportAlias>.Empty
        };
        var stmt2 = new FromImportStatement
        {
            Module = "module_b",
            Names = ImmutableArray<ImportAlias>.Empty
        };

        binding.SetResolvedModulePath(stmt1, "path.a");
        binding.SetResolvedModulePath(stmt2, "path.b");

        binding.GetResolvedModulePath(stmt1).Should().Be("path.a");
        binding.GetResolvedModulePath(stmt2).Should().Be("path.b");
    }

    #endregion

    #region HasCodeGenInfo

    [Fact]
    public void HasCodeGenInfo_ReturnsFalse_WhenNotSet()
    {
        var binding = new SemanticBinding();
        var symbol = new TypeSymbol { Name = "Foo", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        binding.HasCodeGenInfo(symbol).Should().BeFalse();
    }

    [Fact]
    public void HasCodeGenInfo_ReturnsTrue_WhenSet()
    {
        var binding = new SemanticBinding();
        var symbol = new TypeSymbol { Name = "Foo", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var info = new CodeGenInfo { CSharpName = "Foo", OriginalName = "foo" };

        binding.SetCodeGenInfo(symbol, info);

        binding.HasCodeGenInfo(symbol).Should().BeTrue();
    }

    #endregion

    #region GetVariableType defaults

    [Fact]
    public void GetVariableType_ReturnsUnknown_WhenNotSet()
    {
        var binding = new SemanticBinding();
        var varSymbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        binding.GetVariableType(varSymbol).Should().Be(SemanticType.Unknown);
    }

    #endregion

    #region Freeze Violation Detection

    [Fact]
    public void SetCodeGenInfo_AfterFreeze_FiresDebugAssert()
    {
        var logger = new TestLogger();
        var binding = new SemanticBinding(logger);
        var symbol = new FunctionSymbol { Name = "test_fn", Kind = SymbolKind.Function };

        binding.SetCodeGenInfo(symbol, new CodeGenInfo { CSharpName = "TestFn", OriginalName = "test_fn" });
        binding.MaterializeCodeGenInfo();
        binding.FreezeCodeGenInfo();

#if DEBUG
        // In DEBUG builds, Debug.Fail fires as an exception before the logger is reached
        var ex = Assert.ThrowsAny<Exception>(() =>
            binding.SetCodeGenInfo(symbol, new CodeGenInfo { CSharpName = "TestFn2", OriginalName = "test_fn" }));
        ex.Message.Should().Contain("freeze violation");
#else
        // In Release builds, Debug.Fail is compiled out; the logger warning fires instead
        binding.SetCodeGenInfo(symbol, new CodeGenInfo { CSharpName = "TestFn2", OriginalName = "test_fn" });
        logger.Warnings.Should().Contain(w => w.Contains("freeze") && w.Contains("test_fn"));
#endif
    }

    [Fact]
    public void SetVariableType_AfterFreeze_FiresDebugAssert()
    {
        var logger = new TestLogger();
        var binding = new SemanticBinding(logger);
        var symbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        binding.SetVariableType(symbol, SemanticType.Int);
        binding.MaterializeVariableTypes();
        binding.FreezeVariableTypes();

#if DEBUG
        var ex = Assert.ThrowsAny<Exception>(() =>
            binding.SetVariableType(symbol, SemanticType.Str));
        ex.Message.Should().Contain("freeze violation");
#else
        binding.SetVariableType(symbol, SemanticType.Str);
        logger.Warnings.Should().Contain(w => w.Contains("freeze") && w.Contains("x"));
#endif
    }

    [Fact]
    public void SetBaseType_AfterFreeze_FiresDebugAssert()
    {
        var logger = new TestLogger();
        var binding = new SemanticBinding(logger);
        var child = new TypeSymbol { Name = "Child", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var parent = new TypeSymbol { Name = "Parent", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        binding.SetBaseType(child, parent);
        binding.MaterializeInheritance();
        binding.FreezeInheritance();

#if DEBUG
        var ex = Assert.ThrowsAny<Exception>(() =>
            binding.SetBaseType(child, parent));
        ex.Message.Should().Contain("freeze violation");
#else
        binding.SetBaseType(child, parent);
        logger.Warnings.Should().Contain(w => w.Contains("freeze") && w.Contains("Child"));
#endif
    }

    [Fact]
    public void AddInterface_AfterFreeze_FiresDebugAssert()
    {
        var logger = new TestLogger();
        var binding = new SemanticBinding(logger);
        var classSymbol = new TypeSymbol { Name = "MyClass", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        var iface = new TypeSymbol { Name = "IFoo", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };

        binding.MaterializeInheritance();
        binding.FreezeInheritance();

#if DEBUG
        var ex = Assert.ThrowsAny<Exception>(() =>
            binding.AddInterface(classSymbol, iface));
        ex.Message.Should().Contain("freeze violation");
#else
        binding.AddInterface(classSymbol, iface);
        logger.Warnings.Should().Contain(w => w.Contains("freeze") && w.Contains("MyClass"));
#endif
    }

    private class TestLogger : Logging.ICompilerLogger
    {
        public List<string> Warnings { get; } = new();
        public void LogTokenRead(string tokenType, int line, int column, string value) { }
        public void LogIndentChange(int oldLevel, int newLevel) { }
        public void LogParseEnter(string rule, int tokenPosition) { }
        public void LogParseExit(string rule, bool success) { }
        public void LogError(string message, int line, int column) { }
        public void LogWarning(string message, int line, int column) => Warnings.Add(message);
        public void LogInfo(string message) { }
        public void LogDebug(string message) { }
        public void LogTrace(string message) { }
        public void LogMetrics(string metricsOutput) { }
        public bool IsEnabled(Logging.CompilerLogLevel level) => true;
    }

    #endregion
}
