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

    public CodeGenContext(SymbolTable symbolTable, BuiltinRegistry builtins)
    {
        SymbolTable = symbolTable;
        Builtins = builtins;
    }

    public void Indent() => _indentLevel++;
    public void Dedent() => _indentLevel = Math.Max(0, _indentLevel - 1);

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
