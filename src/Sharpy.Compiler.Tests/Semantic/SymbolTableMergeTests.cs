using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class SymbolTableMergeTests
{
    private static BuiltinRegistry CreateBuiltins()
    {
        return new BuiltinRegistry(NullLogger.Instance);
    }

    [Fact]
    public void MergeFrom_TwoNonOverlappingTables_AllSymbolsPresent()
    {
        var builtins = CreateBuiltins();

        var table1 = new SymbolTable(builtins);
        table1.EnterModuleScope("module_a");
        var symA = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        table1.Define(symA);
        table1.ExitScope();

        var table2 = new SymbolTable(builtins);
        table2.EnterModuleScope("module_b");
        var symB = new VariableSymbol { Name = "y", Kind = SymbolKind.Variable };
        table2.Define(symB);
        table2.ExitScope();

        var perFileTables = new List<(string, SymbolTable)>
        {
            ("module_a", table1),
            ("module_b", table2)
        };

        var merged = SymbolTable.MergeFrom(perFileTables, builtins);

        var scopeA = merged.GetModuleScope("module_a");
        var scopeB = merged.GetModuleScope("module_b");

        Assert.NotNull(scopeA);
        Assert.NotNull(scopeB);
        Assert.NotNull(scopeA!.Lookup("x", searchParent: false));
        Assert.NotNull(scopeB!.Lookup("y", searchParent: false));
    }

    [Fact]
    public void MergeFrom_SymbolIdentityPreserved()
    {
        var builtins = CreateBuiltins();

        var table = new SymbolTable(builtins);
        table.EnterModuleScope("module_a");
        var original = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        table.Define(original);
        table.ExitScope();

        var perFileTables = new List<(string, SymbolTable)> { ("module_a", table) };
        var merged = SymbolTable.MergeFrom(perFileTables, builtins);

        var scopeA = merged.GetModuleScope("module_a");
        var found = scopeA!.Lookup("x", searchParent: false);
        Assert.Same(original, found);
    }

    [Fact]
    public void MergeFrom_DuplicateSymbol_ReportsError()
    {
        var builtins = CreateBuiltins();

        var table1 = new SymbolTable(builtins);
        table1.EnterModuleScope("module_a");
        table1.Define(new TypeSymbol { Name = "MyClass", Kind = SymbolKind.Type, TypeKind = TypeKind.Class });
        table1.ExitScope();

        var table2 = new SymbolTable(builtins);
        table2.EnterModuleScope("module_a");
        table2.Define(new TypeSymbol { Name = "MyClass", Kind = SymbolKind.Type, TypeKind = TypeKind.Class });
        table2.ExitScope();

        var diagnostics = new DiagnosticBag();
        var perFileTables = new List<(string, SymbolTable)>
        {
            ("module_a", table1),
            ("module_a", table2)
        };

        SymbolTable.MergeFrom(perFileTables, builtins, diagnostics);

        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.GetErrors(), e => e.Message.Contains("Duplicate definition"));
    }

    [Fact]
    public void MergeFrom_BuiltinsPresent()
    {
        var builtins = CreateBuiltins();
        var table = new SymbolTable(builtins);
        table.EnterModuleScope("module_a");
        table.Define(new VariableSymbol { Name = "x", Kind = SymbolKind.Variable });
        table.ExitScope();

        var perFileTables = new List<(string, SymbolTable)> { ("module_a", table) };
        var merged = SymbolTable.MergeFrom(perFileTables, builtins);

        Assert.NotNull(merged.GlobalScope.Lookup("print", searchParent: false));
    }

    [Fact]
    public void MergeFrom_ModuleScopesPartitioned()
    {
        var builtins = CreateBuiltins();

        var tableA = new SymbolTable(builtins);
        tableA.EnterModuleScope("a");
        tableA.Define(new VariableSymbol { Name = "x", Kind = SymbolKind.Variable });
        tableA.ExitScope();

        var tableB = new SymbolTable(builtins);
        tableB.EnterModuleScope("b");
        tableB.Define(new VariableSymbol { Name = "y", Kind = SymbolKind.Variable });
        tableB.ExitScope();

        var perFileTables = new List<(string, SymbolTable)> { ("a", tableA), ("b", tableB) };
        var merged = SymbolTable.MergeFrom(perFileTables, builtins);

        var scopeA = merged.GetModuleScope("a");
        var scopeB = merged.GetModuleScope("b");

        Assert.Null(scopeA!.Lookup("y", searchParent: false));
        Assert.Null(scopeB!.Lookup("x", searchParent: false));
    }

    [Fact]
    public void MergeFrom_IGlobalSymbolTable_WorksOnMergedTable()
    {
        var builtins = CreateBuiltins();

        var table = new SymbolTable(builtins);
        table.EnterModuleScope("module_a");
        table.Define(new FunctionSymbol { Name = "greet", Kind = SymbolKind.Function });
        table.ExitScope();

        var perFileTables = new List<(string, SymbolTable)> { ("module_a", table) };
        IGlobalSymbolTable merged = SymbolTable.MergeFrom(perFileTables, builtins);

        Assert.NotNull(merged.GetModuleScope("module_a"));
        var symbols = merged.GetAllModuleScopeSymbols().ToList();
        Assert.Contains(symbols, s => s.Name == "greet");
    }
}
