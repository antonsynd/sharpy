using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class CodeGenInfoTests
{
    [Fact]
    public void GetVersionedCSharpName_Version0_ReturnsBaseName()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "myVariable",
            OriginalName = "my_variable",
            Version = 0
        };

        Assert.Equal("myVariable", info.GetVersionedCSharpName());
    }

    [Fact]
    public void GetVersionedCSharpName_Version1_ReturnsNameWithSuffix()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "myVariable",
            OriginalName = "my_variable",
            Version = 1
        };

        Assert.Equal("myVariable_1", info.GetVersionedCSharpName());
    }

    [Fact]
    public void GetVersionedCSharpName_Version3_ReturnsNameWithSuffix()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "counter",
            OriginalName = "counter",
            Version = 3
        };

        Assert.Equal("counter_3", info.GetVersionedCSharpName());
    }

    [Fact]
    public void CodeGenInfo_ModuleLevelConstant_HasCorrectFlags()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "MAX_VALUE",
            OriginalName = "MAX_VALUE",
            IsModuleLevel = true,
            IsConstant = true
        };

        Assert.True(info.IsModuleLevel);
        Assert.True(info.IsConstant);
        Assert.False(info.HasExecutionOrderIssues);
        Assert.Equal(ImportKind.None, info.ImportKind);
    }

    [Fact]
    public void CodeGenInfo_FromImportWithAlias_TracksOriginalName()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "MAX",
            OriginalName = "MAX",
            ImportKind = ImportKind.FromImportWithAlias,
            OriginalImportName = "MAX_VALUE"
        };

        Assert.Equal(ImportKind.FromImportWithAlias, info.ImportKind);
        Assert.Equal("MAX_VALUE", info.OriginalImportName);
    }

    [Fact]
    public void CodeGenInfo_IsRecord_SupportsWithExpression()
    {
        var original = new CodeGenInfo
        {
            CSharpName = "count",
            OriginalName = "count",
            Version = 0
        };

        var redeclared = original with { Version = 1 };

        Assert.Equal(0, original.Version);
        Assert.Equal(1, redeclared.Version);
        Assert.Equal("count", redeclared.CSharpName);
    }

    [Fact]
    public void CodeGenInfo_LocalVariable_HasCorrectDefaults()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "localVar",
            OriginalName = "local_var"
        };

        Assert.Equal(0, info.Version);
        Assert.False(info.IsModuleLevel);
        Assert.False(info.IsConstant);
        Assert.False(info.HasExecutionOrderIssues);
        Assert.Equal(ImportKind.None, info.ImportKind);
        Assert.Null(info.OriginalImportName);
        Assert.Null(info.UnionDiscriminatorValue);
        Assert.Null(info.AsyncStateId);
        Assert.Null(info.PropertyAccessorName);
    }

    [Fact]
    public void CodeGenInfo_ExecutionOrderIssues_TrackedCorrectly()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "RuntimeVar",
            OriginalName = "runtime_var",
            IsModuleLevel = true,
            HasExecutionOrderIssues = true
        };

        Assert.True(info.IsModuleLevel);
        Assert.True(info.HasExecutionOrderIssues);
    }

    [Fact]
    public void Symbol_CodeGenInfo_InitiallyNull()
    {
        var symbol = new VariableSymbol
        {
            Name = "test",
            Kind = SymbolKind.Variable
        };

        Assert.Null(symbol.CodeGenInfo);
    }

    [Fact]
    public void Symbol_CodeGenInfo_CanBeSetAfterCreation()
    {
        var symbol = new VariableSymbol
        {
            Name = "test",
            Kind = SymbolKind.Variable
        };

        symbol.CodeGenInfo = new CodeGenInfo
        {
            CSharpName = "Test",
            OriginalName = "test"
        };

        Assert.NotNull(symbol.CodeGenInfo);
        Assert.Equal("Test", symbol.CodeGenInfo.CSharpName);
    }
}
