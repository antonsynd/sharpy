using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Registry;

/// <summary>
/// Declarative method, operator, and protocol definitions for built-in collection types.
/// Returns metadata that BuiltinRegistry uses to populate TypeSymbol.Methods,
/// TypeSymbol.OperatorMethods, and TypeSymbol.ProtocolMethods at registration time.
/// </summary>
/// <remarks>
/// Type parameters use TypeParameterType with names matching BuiltinRegistry conventions
/// (T0, T1, etc.). This class is static and pure — no compiler state dependencies.
/// </remarks>
internal static class BuiltinMethodDefinitions
{
    /// <summary>
    /// Returns FunctionSymbols for regular methods on the given builtin type.
    /// </summary>
    public static List<FunctionSymbol> GetMethods(string typeName, List<TypeParameterDef> typeParams)
    {
        var tps = MakeTypeParams(typeParams);

        return typeName switch
        {
            BuiltinNames.Dict => GetDictMethods(tps),
            BuiltinNames.List => GetListMethods(tps),
            BuiltinNames.Set => GetSetMethods(tps),
            BuiltinNames.Int => GetIntStaticMethods(),
            BuiltinNames.Float => GetFloatStaticMethods(),
            _ => new List<FunctionSymbol>()
        };
    }

    /// <summary>
    /// Returns operator dunder methods for the given builtin type.
    /// </summary>
    public static Dictionary<string, List<FunctionSymbol>> GetOperatorMethods(
        string typeName, List<TypeParameterDef> typeParams)
    {
        var tps = MakeTypeParams(typeParams);

        return typeName switch
        {
            BuiltinNames.Dict => GetDictOperators(tps),
            BuiltinNames.List => GetListOperators(tps),
            BuiltinNames.Set => GetSetOperators(tps),
            BuiltinNames.Tuple => GetTupleOperators(tps),
            _ => new Dictionary<string, List<FunctionSymbol>>()
        };
    }

    /// <summary>
    /// Returns protocol dunder methods for the given builtin type.
    /// </summary>
    public static Dictionary<string, List<FunctionSymbol>> GetProtocolMethods(
        string typeName, List<TypeParameterDef> typeParams)
    {
        var tps = MakeTypeParams(typeParams);

        return typeName switch
        {
            BuiltinNames.Dict => GetDictProtocols(tps),
            BuiltinNames.List => GetListProtocols(tps),
            BuiltinNames.Set => GetSetProtocols(tps),
            BuiltinNames.Tuple => GetTupleProtocols(tps),
            BuiltinNames.DictItemsView => GetViewProtocols(),
            BuiltinNames.DictKeyView => GetViewProtocols(),
            BuiltinNames.DictValuesView => GetViewProtocols(),
            BuiltinNames.Iterator => GetIteratorProtocols(),
            BuiltinNames.IEnumerable => GetIteratorProtocols(),
            BuiltinNames.IEnumerator => GetIteratorProtocols(),
            _ => new Dictionary<string, List<FunctionSymbol>>()
        };
    }

    // ---- Dict methods ----

    private static List<FunctionSymbol> GetDictMethods(TypeParameterType[] tps)
    {
        var t0 = tps.Length > 0 ? (SemanticType)tps[0] : SemanticType.Unknown;
        var t1 = tps.Length > 1 ? (SemanticType)tps[1] : SemanticType.Unknown;

        return new List<FunctionSymbol>
        {
            // get(key) -> Optional[V]
            MakeMethod("get", new[] { Param("key", t0) },
                new OptionalType { UnderlyingType = t1 }),
            // get(key, default) -> V
            MakeMethod("get", new[] { Param("key", t0), Param("default", t1) }, t1),
            // items() -> DictItemsView[K, V]
            MakeMethod("items", Array.Empty<ParameterSymbol>(),
                new GenericType
                {
                    Name = BuiltinNames.DictItemsView,
                    TypeArguments = new List<SemanticType> { t0, t1 }
                }),
            // keys() -> DictKeyView[K, V]
            MakeMethod("keys", Array.Empty<ParameterSymbol>(),
                new GenericType
                {
                    Name = BuiltinNames.DictKeyView,
                    TypeArguments = new List<SemanticType> { t0, t1 }
                }),
            // values() -> DictValuesView[K, V]
            MakeMethod("values", Array.Empty<ParameterSymbol>(),
                new GenericType
                {
                    Name = BuiltinNames.DictValuesView,
                    TypeArguments = new List<SemanticType> { t0, t1 }
                }),
        };
    }

    private static Dictionary<string, List<FunctionSymbol>> GetDictOperators(TypeParameterType[] tps)
    {
        return MakeOperatorDict(DunderNames.Or, DunderNames.Eq, DunderNames.Ne);
    }

    private static Dictionary<string, List<FunctionSymbol>> GetDictProtocols(TypeParameterType[] tps)
    {
        return MakeProtocolDict(
            DunderNames.Len, DunderNames.Iter, DunderNames.Contains,
            DunderNames.GetItem, DunderNames.SetItem);
    }

    // ---- List methods ----

    private static List<FunctionSymbol> GetListMethods(TypeParameterType[] tps)
    {
        var t0 = tps.Length > 0 ? (SemanticType)tps[0] : SemanticType.Unknown;

        return new List<FunctionSymbol>
        {
            MakeMethod("append", new[] { Param("item", t0) }, SemanticType.Void),
            MakeMethod("extend", new[] { Param("items", MakeGeneric(BuiltinNames.List, t0)) }, SemanticType.Void),
            MakeMethod("insert", new[] { Param("index", SemanticType.Int), Param("item", t0) }, SemanticType.Void),
            MakeMethod("pop", Array.Empty<ParameterSymbol>(), t0),
            MakeMethod("pop", new[] { Param("index", SemanticType.Int) }, t0),
            MakeMethod("remove", new[] { Param("item", t0) }, SemanticType.Void),
            MakeMethod("index", new[] { Param("item", t0) }, SemanticType.Int),
            MakeMethod("count", new[] { Param("item", t0) }, SemanticType.Int),
            MakeMethod("sort", Array.Empty<ParameterSymbol>(), SemanticType.Void),
            MakeMethod("reverse", Array.Empty<ParameterSymbol>(), SemanticType.Void),
            MakeMethod("copy", Array.Empty<ParameterSymbol>(), MakeGeneric(BuiltinNames.List, t0)),
            MakeMethod("clear", Array.Empty<ParameterSymbol>(), SemanticType.Void),
        };
    }

    private static Dictionary<string, List<FunctionSymbol>> GetListOperators(TypeParameterType[] tps)
    {
        return MakeOperatorDict(DunderNames.Add, DunderNames.Mul, DunderNames.Eq, DunderNames.Ne);
    }

    private static Dictionary<string, List<FunctionSymbol>> GetListProtocols(TypeParameterType[] tps)
    {
        return MakeProtocolDict(
            DunderNames.Len, DunderNames.Iter, DunderNames.Contains,
            DunderNames.GetItem, DunderNames.SetItem);
    }

    // ---- Set methods ----

    private static List<FunctionSymbol> GetSetMethods(TypeParameterType[] tps)
    {
        var t0 = tps.Length > 0 ? (SemanticType)tps[0] : SemanticType.Unknown;
        var setOfT0 = MakeGeneric(BuiltinNames.Set, t0);

        return new List<FunctionSymbol>
        {
            MakeMethod("add", new[] { Param("item", t0) }, SemanticType.Void),
            MakeMethod("discard", new[] { Param("item", t0) }, SemanticType.Void),
            MakeMethod("remove", new[] { Param("item", t0) }, SemanticType.Void),
            MakeMethod("union", new[] { Param("other", setOfT0) }, setOfT0),
            MakeMethod("intersection", new[] { Param("other", setOfT0) }, setOfT0),
            MakeMethod("difference", new[] { Param("other", setOfT0) }, setOfT0),
            MakeMethod("symmetric_difference", new[] { Param("other", setOfT0) }, setOfT0),
            MakeMethod("issubset", new[] { Param("other", setOfT0) }, SemanticType.Bool),
            MakeMethod("issuperset", new[] { Param("other", setOfT0) }, SemanticType.Bool),
            MakeMethod("copy", Array.Empty<ParameterSymbol>(), setOfT0),
            MakeMethod("clear", Array.Empty<ParameterSymbol>(), SemanticType.Void),
        };
    }

    private static Dictionary<string, List<FunctionSymbol>> GetSetOperators(TypeParameterType[] tps)
    {
        return MakeOperatorDict(
            DunderNames.Or, DunderNames.And, DunderNames.Sub, DunderNames.Xor,
            DunderNames.Eq, DunderNames.Ne);
    }

    private static Dictionary<string, List<FunctionSymbol>> GetSetProtocols(TypeParameterType[] tps)
    {
        return MakeProtocolDict(DunderNames.Len, DunderNames.Iter, DunderNames.Contains);
    }

    // ---- Tuple (operators and protocols only) ----

    private static Dictionary<string, List<FunctionSymbol>> GetTupleOperators(TypeParameterType[] tps)
    {
        return MakeOperatorDict(DunderNames.Add, DunderNames.Mul, DunderNames.Eq, DunderNames.Ne);
    }

    private static Dictionary<string, List<FunctionSymbol>> GetTupleProtocols(TypeParameterType[] tps)
    {
        return MakeProtocolDict(DunderNames.Len, DunderNames.Iter, DunderNames.GetItem);
    }

    // ---- View and iterator types (protocols only) ----

    private static Dictionary<string, List<FunctionSymbol>> GetViewProtocols()
    {
        return MakeProtocolDict(DunderNames.Iter, DunderNames.Len);
    }

    private static Dictionary<string, List<FunctionSymbol>> GetIteratorProtocols()
    {
        return MakeProtocolDict(DunderNames.Iter);
    }

    // ---- Primitive static methods ----

    private static readonly UserDefinedType ValueErrorType = new() { Name = "ValueError" };

    private static List<FunctionSymbol> GetIntStaticMethods()
    {
        return new List<FunctionSymbol>
        {
            MakeStaticMethod("parse", new[] { Param("s", SemanticType.Str) },
                new ResultType { OkType = SemanticType.Int, ErrorType = ValueErrorType }),
        };
    }

    private static List<FunctionSymbol> GetFloatStaticMethods()
    {
        return new List<FunctionSymbol>
        {
            MakeStaticMethod("parse", new[] { Param("s", SemanticType.Str) },
                new ResultType { OkType = SemanticType.Float, ErrorType = ValueErrorType }),
        };
    }

    // ---- Helpers ----

    private static TypeParameterType[] MakeTypeParams(List<TypeParameterDef> typeParams)
    {
        return typeParams.Select(tp => new TypeParameterType { Name = tp.Name }).ToArray();
    }

    private static FunctionSymbol MakeMethod(string name, ParameterSymbol[] parameters, SemanticType returnType)
    {
        return new FunctionSymbol
        {
            Name = name,
            Kind = SymbolKind.Function,
            Parameters = parameters.ToList(),
            ReturnType = returnType,
            AccessLevel = AccessLevel.Public,
        };
    }

    private static FunctionSymbol MakeStaticMethod(string name, ParameterSymbol[] parameters, SemanticType returnType)
    {
        return new FunctionSymbol
        {
            Name = name,
            Kind = SymbolKind.Function,
            Parameters = parameters.ToList(),
            ReturnType = returnType,
            AccessLevel = AccessLevel.Public,
            IsStatic = true,
        };
    }

    private static ParameterSymbol Param(string name, SemanticType type)
    {
        return new ParameterSymbol { Name = name, Type = type };
    }

    private static GenericType MakeGeneric(string name, params SemanticType[] typeArgs)
    {
        return new GenericType { Name = name, TypeArguments = typeArgs.ToList() };
    }

    /// <summary>
    /// Creates a dictionary of operator dunder names, each with a single placeholder FunctionSymbol.
    /// The FunctionSymbol serves as a marker for "this operator is supported" — validators
    /// only check for key presence, not the actual method signatures.
    /// </summary>
    private static Dictionary<string, List<FunctionSymbol>> MakeOperatorDict(params string[] dunderNames)
        => MakeDunderDict(dunderNames);

    /// <summary>
    /// Creates a dictionary of protocol dunder names, each with a single placeholder FunctionSymbol.
    /// Semantically distinct from operators but structurally identical.
    /// </summary>
    private static Dictionary<string, List<FunctionSymbol>> MakeProtocolDict(params string[] dunderNames)
        => MakeDunderDict(dunderNames);

    /// <summary>
    /// Shared implementation for creating a dunder name → placeholder FunctionSymbol dictionary.
    /// Used by both MakeOperatorDict and MakeProtocolDict.
    /// </summary>
    private static Dictionary<string, List<FunctionSymbol>> MakeDunderDict(string[] dunderNames)
    {
        var dict = new Dictionary<string, List<FunctionSymbol>>();
        foreach (var name in dunderNames)
        {
            dict[name] = new List<FunctionSymbol>
            {
                new FunctionSymbol
                {
                    Name = name,
                    Kind = SymbolKind.Function,
                    AccessLevel = AccessLevel.Public,
                }
            };
        }
        return dict;
    }

    /// <summary>
    /// Populates MethodOverloads on a TypeSymbol for methods that share the same name.
    /// </summary>
    public static void PopulateMethodOverloads(TypeSymbol typeSymbol)
    {
        var overloadGroups = typeSymbol.Methods
            .GroupBy(m => m.Name)
            .Where(g => g.Count() > 1);

        foreach (var group in overloadGroups)
        {
            typeSymbol.MethodOverloads[group.Key] = group.ToList();
        }
    }
}
