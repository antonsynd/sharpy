using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class GlobalSymbolTableInterfaceTests
{
    private static SymbolTable CreateSymbolTable()
    {
        var builtins = new BuiltinRegistry(NullLogger.Instance);
        return new SymbolTable(builtins);
    }

    [Fact]
    public void SymbolTable_ImplementsIGlobalSymbolTable()
    {
        var table = CreateSymbolTable();
        Assert.IsAssignableFrom<IGlobalSymbolTable>(table);
    }

    [Fact]
    public void Lookup_ThroughInterface_FindsBuiltins()
    {
        IGlobalSymbolTable table = CreateSymbolTable();
        var printSymbol = table.Lookup("print");
        Assert.NotNull(printSymbol);
    }

    [Fact]
    public void LookupType_ThroughInterface_FindsBuiltinType()
    {
        IGlobalSymbolTable table = CreateSymbolTable();
        var intType = table.LookupType("int");
        Assert.NotNull(intType);
    }

    [Fact]
    public void GlobalScope_ThroughInterface_ReturnsGlobalScope()
    {
        IGlobalSymbolTable table = CreateSymbolTable();
        Assert.NotNull(table.GlobalScope);
        Assert.Equal("global", table.GlobalScope.Name);
    }

    [Fact]
    public void GetVisibleSymbols_ThroughInterface_ReturnsBuiltins()
    {
        IGlobalSymbolTable table = CreateSymbolTable();
        var symbols = table.GetVisibleSymbols().ToList();
        Assert.True(symbols.Count > 0);
        Assert.Contains(symbols, s => s.Name == "print");
    }

    [Fact]
    public void GetVisibleSymbolNames_ThroughInterface_ReturnsNames()
    {
        IGlobalSymbolTable table = CreateSymbolTable();
        var names = table.GetVisibleSymbolNames().ToList();
        Assert.Contains("print", names);
        Assert.Contains("len", names);
    }

    [Fact]
    public void GetModuleScope_ThroughInterface_ReturnsNullForNonexistent()
    {
        IGlobalSymbolTable table = CreateSymbolTable();
        Assert.Null(table.GetModuleScope("nonexistent"));
    }

    [Fact]
    public void GetModuleScope_ThroughInterface_ReturnsScope()
    {
        var concreteTable = CreateSymbolTable();
        concreteTable.EnterModuleScope("test_module");
        concreteTable.Define(new VariableSymbol { Name = "x", Kind = SymbolKind.Variable });
        concreteTable.ExitScope();

        IGlobalSymbolTable table = concreteTable;
        var scope = table.GetModuleScope("test_module");
        Assert.NotNull(scope);
        Assert.NotNull(scope!.Lookup("x", searchParent: false));
    }
}
