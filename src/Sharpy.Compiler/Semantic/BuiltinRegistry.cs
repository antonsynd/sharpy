namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Registry of builtin types and functions from Sharpy.Runtime
/// </summary>
public class BuiltinRegistry
{
    private readonly Dictionary<string, TypeSymbol> _types = new();
    private readonly Dictionary<string, FunctionSymbol> _functions = new();

    public BuiltinRegistry()
    {
        LoadBuiltins();
    }

    private void LoadBuiltins()
    {
        // TODO: Use reflection to load from Sharpy.Runtime assembly
        // For now, manually register core types

        RegisterType("int", typeof(int));
        RegisterType("float", typeof(double));
        RegisterType("str", typeof(string));
        RegisterType("bool", typeof(bool));

        // Register builtin functions
        RegisterFunction("print", typeof(void));
        RegisterFunction("len", typeof(int), typeof(object));
    }

    private void RegisterType(string name, Type clrType)
    {
        _types[name] = new TypeSymbol
        {
            Name = name,
            ClrType = clrType,
            Kind = SymbolKind.Type
        };
    }

    private void RegisterFunction(string name, Type returnType, params Type[] parameterTypes)
    {
        _functions[name] = new FunctionSymbol
        {
            Name = name,
            Kind = SymbolKind.Function,
            ReturnType = MapClrType(returnType),
            Parameters = parameterTypes.Select((t, i) => new ParameterSymbol
            {
                Name = $"arg{i}",
                Type = MapClrType(t)
            }).ToList()
        };
    }

    private SemanticType MapClrType(Type clrType)
    {
        if (clrType == typeof(void))
            return SemanticType.None;
        if (clrType == typeof(int) || clrType == typeof(long))
            return SemanticType.Int;
        if (clrType == typeof(double) || clrType == typeof(float))
            return SemanticType.Float;
        if (clrType == typeof(string))
            return SemanticType.Str;
        if (clrType == typeof(bool))
            return SemanticType.Bool;

        return SemanticType.Unknown;
    }

    public TypeSymbol? GetType(string name) => _types.GetValueOrDefault(name);
    public FunctionSymbol? GetFunction(string name) => _functions.GetValueOrDefault(name);

    public IEnumerable<(string Name, TypeSymbol Type)> GetAllTypes() => _types.Select(kv => (kv.Key, kv.Value));
    public IEnumerable<(string Name, FunctionSymbol Function)> GetAllFunctions() => _functions.Select(kv => (kv.Key, kv.Value));
}
