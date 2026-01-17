using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Maintains state during code generation
/// </summary>
public class CodeGenContext
{
    private int _indentLevel = 0;
    private const int IndentSize = 4;

    public SymbolTable SymbolTable { get; }
    public BuiltinRegistry Builtins { get; }
    public string? SourceFilePath { get; set; }

    /// <summary>
    /// Project root namespace (for multi-file compilation)
    /// </summary>
    public string? ProjectNamespace { get; set; }

    /// <summary>
    /// Project root directory path (for computing relative namespaces)
    /// </summary>
    public string? ProjectRootPath { get; set; }

    /// <summary>
    /// If true, this file should generate a Main entry point.
    /// Defaults to false; explicitly set to true for the entry point file.
    /// </summary>
    public bool IsEntryPoint { get; set; } = false;

    public CodeGenContext(SymbolTable symbolTable, BuiltinRegistry builtins)
    {
        SymbolTable = symbolTable;
        Builtins = builtins;
    }

    public void Indent() => _indentLevel++;
    public void Dedent() => _indentLevel = System.Math.Max(0, _indentLevel - 1);

    public string GetIndent() => new string(' ', _indentLevel * IndentSize);

    public Symbol? LookupSymbol(string name)
    {
        return SymbolTable.Lookup(name);
    }

    public bool IsBuiltinFunction(string name)
    {
        return Builtins.GetFunction(name) != null;
    }

    public bool IsBuiltinType(string name)
    {
        return Builtins.GetType(name) != null;
    }
}
