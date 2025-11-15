namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Registry of builtin types and functions from Sharpy.Core
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
        // Numeric types
        RegisterType("int", typeof(int), TypeKind.Struct);
        RegisterType("long", typeof(long), TypeKind.Struct);
        RegisterType("float", typeof(float), TypeKind.Struct);
        RegisterType("double", typeof(double), TypeKind.Struct);
        RegisterType("decimal", typeof(decimal), TypeKind.Struct);

        // Boolean
        RegisterType("bool", typeof(bool), TypeKind.Struct);

        // String
        RegisterType("str", typeof(string), TypeKind.Class);

        // Collections (generic)
        RegisterType("list", typeof(System.Collections.Generic.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
        RegisterType("dict", typeof(System.Collections.Generic.Dictionary<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
        RegisterType("set", typeof(System.Collections.Generic.HashSet<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);

        // Special
        RegisterType("object", typeof(object), TypeKind.Class);
        RegisterType("None", typeof(void), TypeKind.Struct); // void for return type

        // Register builtin functions
        RegisterFunction("print", SemanticType.Void, new ParameterSymbol { Name = "value", Type = SemanticType.Str });
        RegisterFunction("len", SemanticType.Int, new ParameterSymbol { Name = "obj", Type = SemanticType.Str });
        RegisterFunction("range", new GenericType { Name = "list", TypeArguments = new() { SemanticType.Int } },
            new ParameterSymbol { Name = "stop", Type = SemanticType.Int });
    }

    private void RegisterType(string sharpyName, Type clrType, TypeKind kind, bool isGeneric = false, int typeParamCount = 0)
    {
        var typeSymbol = new TypeSymbol
        {
            Name = sharpyName,
            Kind = SymbolKind.Type,
            TypeKind = kind,
            ClrType = clrType,
            TypeParameters = isGeneric
                ? Enumerable.Range(0, typeParamCount).Select(i => $"T{i}").ToList()
                : new List<string>(),
            AccessLevel = AccessLevel.Public
        };

        _types[sharpyName] = typeSymbol;
    }

    private void RegisterFunction(string name, SemanticType returnType, params ParameterSymbol[] parameters)
    {
        _functions[name] = new FunctionSymbol
        {
            Name = name,
            Kind = SymbolKind.Function,
            ReturnType = returnType,
            Parameters = parameters.ToList(),
            AccessLevel = AccessLevel.Public
        };
    }

    public TypeSymbol? GetType(string name) => _types.GetValueOrDefault(name);
    public FunctionSymbol? GetFunction(string name) => _functions.GetValueOrDefault(name);

    public IEnumerable<(string Name, TypeSymbol Type)> GetAllTypes() => _types.Select(kv => (kv.Key, kv.Value));
    public IEnumerable<(string Name, FunctionSymbol Function)> GetAllFunctions() => _functions.Select(kv => (kv.Key, kv.Value));
}
