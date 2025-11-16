namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Registry of builtin types and functions from Sharpy.Core
/// </summary>
public class BuiltinRegistry
{
    private readonly Dictionary<string, TypeSymbol> _types = new();
    private readonly Dictionary<string, List<FunctionSymbol>> _functions = new();

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
        // print() accepts object - can print any type
        RegisterFunction("print", SemanticType.Void, new ParameterSymbol { Name = "value", Type = SemanticType.Object });
        RegisterFunction("len", SemanticType.Int, new ParameterSymbol { Name = "obj", Type = SemanticType.Str });
        
        // range() has three overloads: range(stop), range(start, stop), range(start, stop, step)
        var rangeReturnType = new GenericType { Name = "list", TypeArguments = new() { SemanticType.Int } };
        RegisterFunction("range", rangeReturnType,
            new ParameterSymbol { Name = "stop", Type = SemanticType.Int });
        RegisterFunction("range", rangeReturnType,
            new ParameterSymbol { Name = "start", Type = SemanticType.Int },
            new ParameterSymbol { Name = "stop", Type = SemanticType.Int });
        RegisterFunction("range", rangeReturnType,
            new ParameterSymbol { Name = "start", Type = SemanticType.Int },
            new ParameterSymbol { Name = "stop", Type = SemanticType.Int },
            new ParameterSymbol { Name = "step", Type = SemanticType.Int });
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
        var functionSymbol = new FunctionSymbol
        {
            Name = name,
            Kind = SymbolKind.Function,
            ReturnType = returnType,
            Parameters = parameters.ToList(),
            AccessLevel = AccessLevel.Public
        };

        if (!_functions.ContainsKey(name))
        {
            _functions[name] = new List<FunctionSymbol>();
        }
        _functions[name].Add(functionSymbol);
    }

    public TypeSymbol? GetType(string name) => _types.GetValueOrDefault(name);
    public FunctionSymbol? GetFunction(string name) => _functions.GetValueOrDefault(name)?.FirstOrDefault();
    public List<FunctionSymbol>? GetFunctionOverloads(string name) => _functions.GetValueOrDefault(name);

    public IEnumerable<(string Name, TypeSymbol Type)> GetAllTypes() => _types.Select(kv => (kv.Key, kv.Value));
    public IEnumerable<(string Name, FunctionSymbol Function)> GetAllFunctions() => 
        _functions.SelectMany(kv => kv.Value.Select(f => (kv.Key, f)));
}
