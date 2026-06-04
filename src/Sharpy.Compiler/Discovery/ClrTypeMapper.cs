using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using TypeParameterDef = Sharpy.Compiler.Parser.Ast.TypeParameterDef;
using TypeParameterVariance = Sharpy.Compiler.Parser.Ast.TypeParameterVariance;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Maps CLR types to Sharpy SemanticType instances.
/// Thread-safe for concurrent use.
/// </summary>
[ThreadSafe]
internal class ClrTypeMapper
{
    private readonly ConcurrentDictionary<Type, SemanticType> _typeCache = new();

    /// <summary>
    /// Map a CLR type to a Sharpy SemanticType.
    /// </summary>
    public SemanticType MapClrTypeToSemanticType(Type clrType)
    {
        // Use GetOrAdd for thread-safe caching
        return _typeCache.GetOrAdd(clrType, MapTypeInternal);
    }

    private SemanticType MapTypeInternal(Type clrType)
    {
        // Handle generic type parameters (e.g., T in List<T>)
        if (clrType.IsGenericParameter)
        {
            return new TypeParameterType { Name = clrType.Name };
        }

        // Check PrimitiveCatalog first for primitive types
        var primitiveInfo = PrimitiveCatalog.GetByClrType(clrType);
        if (primitiveInfo != null)
        {
            // Return the appropriate SemanticType singleton or create BuiltinType
            return primitiveInfo.SharpyName switch
            {
                BuiltinNames.Int => SemanticType.Int,
                BuiltinNames.Long => SemanticType.Long,
                BuiltinNames.Float => SemanticType.Float,       // float -> double (per spec)
                BuiltinNames.Float32 => SemanticType.Float32,   // float32 -> C# float
                BuiltinNames.Float64 or BuiltinNames.Double => SemanticType.Double,
                BuiltinNames.Bool => SemanticType.Bool,
                BuiltinNames.Str or "string" => SemanticType.Str,
                BuiltinNames.Void or BuiltinNames.None => SemanticType.Void,
                BuiltinNames.Object => SemanticType.Object,
                _ => new BuiltinType { Name = primitiveInfo.SharpyName, ClrType = clrType }
            };
        }

        // Handle arrays
        if (clrType.IsArray)
        {
            var elementType = clrType.GetElementType();
            if (elementType == null)
                return SemanticType.Object; // Defensive fallback for safety

            return new GenericType
            {
                Name = BuiltinNames.List,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(elementType)
                }
            };
        }

        // Handle nullable value types
        var underlyingNullable = Nullable.GetUnderlyingType(clrType);
        if (underlyingNullable != null)
        {
            return new NullableType
            {
                UnderlyingType = MapClrTypeToSemanticType(underlyingNullable)
            };
        }

        // Handle Sharpy.Core types that extend Iterator<T> (like RangeIterator)
        // These are iterable types that yield elements of a specific type
        var iteratorElementType = ClrTypeHelper.GetIteratorElementType(clrType);
        if (iteratorElementType != null)
        {
            return new BuiltinType
            {
                Name = clrType.Name,
                ClrType = clrType
            };
        }

        // Handle non-generic Task
        if (clrType == typeof(System.Threading.Tasks.Task))
            return new TaskType { ResultType = null };

        // Handle generic types
        if (clrType.IsGenericType)
        {
            return MapGenericType(clrType);
        }

        // Handle enums
        if (clrType.IsEnum)
        {
            return SemanticType.Int;
        }

        // Non-generic CLR class/struct: map to UserDefinedType with CLR type info
        if (clrType.IsClass || clrType.IsValueType)
        {
            var symbol = new TypeSymbol
            {
                Name = clrType.Name,
                Kind = SymbolKind.Type,
                TypeKind = clrType.IsValueType ? TypeKind.Struct : TypeKind.Class,
                ClrType = clrType,
                Interfaces = BuildInterfaceList(clrType)
            };

            return new UserDefinedType
            {
                Name = clrType.Name,
                Symbol = symbol
            };
        }

        // Fallback to object for unknown types
        return SemanticType.Object;
    }

    private SemanticType MapGenericType(Type clrType)
    {
        var genericDef = clrType.GetGenericTypeDefinition();
        var typeArgs = clrType.GetGenericArguments();

        // Sharpy.List<T>, List<T>, or IList<T>
        if (genericDef.FullName == "Sharpy.List`1" ||
            IsGenericTypeDefinition(genericDef, typeof(List<>)) ||
            IsGenericTypeDefinition(genericDef, typeof(IList<>)))
        {
            return new GenericType
            {
                Name = BuiltinNames.List,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // Sharpy.Dict<K, V>, Dictionary<K, V>, or IDictionary<K, V>
        if (genericDef.FullName == "Sharpy.Dict`2" ||
            IsGenericTypeDefinition(genericDef, typeof(Dictionary<,>)) ||
            IsGenericTypeDefinition(genericDef, typeof(IDictionary<,>)))
        {
            return new GenericType
            {
                Name = BuiltinNames.Dict,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // Sharpy.Set<T>, HashSet<T>, or ISet<T>
        if (genericDef.FullName == "Sharpy.Set`1" ||
            IsGenericTypeDefinition(genericDef, typeof(HashSet<>)) ||
            IsGenericTypeDefinition(genericDef, typeof(ISet<>)))
        {
            return new GenericType
            {
                Name = BuiltinNames.Set,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // IEnumerable<T>
        if (IsGenericTypeDefinition(genericDef, typeof(IEnumerable<>)))
        {
            return new GenericType
            {
                // Note: IEnumerable<T> is mapped to list for simplicity, losing lazy evaluation semantics.
                // This is a known limitation - iterables are treated as eager lists in the type system.
                Name = BuiltinNames.List,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // Task<T>
        if (IsGenericTypeDefinition(genericDef, typeof(System.Threading.Tasks.Task<>)))
        {
            return new TaskType
            {
                ResultType = MapClrTypeToSemanticType(typeArgs[0])
            };
        }

        // Sharpy.Optional<T>
        if (genericDef.FullName == "Sharpy.Optional`1")
        {
            return new OptionalType
            {
                UnderlyingType = MapClrTypeToSemanticType(typeArgs[0])
            };
        }

        // Sharpy.Result<T, E>
        if (genericDef.FullName == "Sharpy.Result`2")
        {
            return new ResultType
            {
                OkType = MapClrTypeToSemanticType(typeArgs[0]),
                ErrorType = MapClrTypeToSemanticType(typeArgs[1])
            };
        }

        // Sharpy.DictItemsView<K, V>
        if (genericDef.FullName == "Sharpy.DictItemsView`2")
        {
            return new GenericType
            {
                Name = BuiltinNames.DictItemsView,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // Sharpy.DictKeyView<K, V>
        if (genericDef.FullName == "Sharpy.DictKeyView`2")
        {
            return new GenericType
            {
                Name = BuiltinNames.DictKeyView,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // Sharpy.DictValuesView<K, V>
        if (genericDef.FullName == "Sharpy.DictValuesView`2")
        {
            return new GenericType
            {
                Name = BuiltinNames.DictValuesView,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // KeyValuePair<K, V> -> tuple[K, V]
        if (genericDef.FullName == "System.Collections.Generic.KeyValuePair`2")
        {
            return new TupleType
            {
                ElementTypes = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // IComparer<T> — preserve as CLR interface type
        if (IsGenericTypeDefinition(genericDef, typeof(IComparer<>)))
        {
            return new GenericType
            {
                Name = "IComparer",
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // IEqualityComparer<T> — preserve as CLR interface type
        if (IsGenericTypeDefinition(genericDef, typeof(IEqualityComparer<>)))
        {
            return new GenericType
            {
                Name = "IEqualityComparer",
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // Tuple types
        if (genericDef.FullName?.StartsWith("System.Tuple") == true ||
            genericDef.FullName?.StartsWith("System.ValueTuple") == true)
        {
            return new TupleType
            {
                ElementTypes = typeArgs
                    .Select(MapClrTypeToSemanticType)
                    .ToList()
            };
        }

        // Func<T1,...,TResult> -> FunctionType
        if (genericDef.FullName?.StartsWith("System.Func`") == true)
        {
            // Last type arg is the return type, rest are parameter types
            var paramTypes = typeArgs.Take(typeArgs.Length - 1)
                .Select(MapClrTypeToSemanticType)
                .ToList();
            var returnType = MapClrTypeToSemanticType(typeArgs[typeArgs.Length - 1]);
            return new FunctionType
            {
                ParameterTypes = paramTypes,
                ReturnType = returnType
            };
        }

        // Action<T1,...> -> FunctionType with void return
        if (genericDef.FullName?.StartsWith("System.Action`") == true)
        {
            var paramTypes = typeArgs
                .Select(MapClrTypeToSemanticType)
                .ToList();
            return new FunctionType
            {
                ParameterTypes = paramTypes,
                ReturnType = SemanticType.Void
            };
        }

        // Unknown generic type - fallback to object
        return SemanticType.Object;
    }

    private bool IsGenericTypeDefinition(Type type, Type genericTypeDef)
    {
        return type == genericTypeDef;
    }

    /// <summary>
    /// Cache of minimal interface TypeSymbols keyed by the open-generic (or non-generic)
    /// CLR interface definition, so all mapped types implementing the same interface share
    /// one definition symbol.
    /// </summary>
    private readonly ConcurrentDictionary<Type, TypeSymbol> _interfaceSymbolCache = new();

    /// <summary>
    /// Types whose interface lists are currently being built on this thread. Guards against
    /// infinite recursion through self-referential interface arguments
    /// (e.g., <c>Foo : IEquatable&lt;Foo&gt;</c>) and indirect cycles between types.
    /// </summary>
    [ThreadStatic]
    private static HashSet<Type>? _interfaceListInProgress;

    /// <summary>
    /// Builds the interface list for a CLR type with resolved type arguments (#827).
    /// All public CLR interfaces are enumerated; generic interfaces carry
    /// ResolvedTypeArguments. Non-generic duplicates of generic forms (IEnumerable vs
    /// IEnumerable&lt;T&gt;) are filtered out.
    /// </summary>
    private List<InterfaceReference> BuildInterfaceList(Type clrType)
    {
        var result = new List<InterfaceReference>();

        _interfaceListInProgress ??= new HashSet<Type>();
        if (!_interfaceListInProgress.Add(clrType))
            return result; // Re-entrant on the same type — break the cycle

        try
        {
            var allInterfaces = clrType.GetInterfaces()
                .Where(IsPublicInterface)
                .ToList();

            // When both generic and non-generic forms share a stripped name (IEnumerable vs
            // IEnumerable<T>), keep only the generic form — the non-generic form carries no
            // additional information and would create an ambiguous duplicate reference.
            var genericNames = new HashSet<string>(
                allInterfaces
                    .Where(i => i.IsGenericType)
                    .Select(i => ClrNameHelper.StripArity(i.GetGenericTypeDefinition().Name)));

            foreach (var iface in allInterfaces)
            {
                if (!iface.IsGenericType)
                {
                    if (genericNames.Contains(iface.Name))
                        continue;

                    result.Add(new InterfaceReference
                    {
                        Definition = GetOrCreateInterfaceSymbol(iface)
                    });
                    continue;
                }

                var resolvedArgs = iface.GetGenericArguments()
                    .Select(arg => MapInterfaceArgument(arg, clrType))
                    .ToImmutableArray();

                result.Add(new InterfaceReference
                {
                    Definition = GetOrCreateInterfaceSymbol(iface.GetGenericTypeDefinition()),
                    ResolvedTypeArguments = resolvedArgs
                });
            }

            return result;
        }
        finally
        {
            _interfaceListInProgress.Remove(clrType);
        }
    }

    /// <summary>
    /// Maps a CLR interface type argument to a SemanticType. Self-referential arguments
    /// (e.g., <c>Foo</c> in <c>Foo : IEquatable&lt;Foo&gt;</c>) and types whose interface
    /// lists are currently being built map to a shallow UserDefinedType to avoid recursing
    /// back through <see cref="MapTypeInternal"/>.
    /// </summary>
    private SemanticType MapInterfaceArgument(Type arg, Type declaringType)
    {
        if (arg == declaringType || (_interfaceListInProgress?.Contains(arg) == true))
            return new UserDefinedType { Name = arg.Name };

        return MapClrTypeToSemanticType(arg);
    }

    /// <summary>
    /// Finds or creates the TypeSymbol for an interface definition, cached per CLR
    /// definition so all implementers share one symbol. Type parameters follow the
    /// discovery naming convention (T0, T1, ...) with CLR variance preserved.
    /// </summary>
    private TypeSymbol GetOrCreateInterfaceSymbol(Type interfaceDef)
    {
        return _interfaceSymbolCache.GetOrAdd(interfaceDef, static def =>
        {
            var clrArgs = def.IsGenericTypeDefinition
                ? def.GetGenericArguments()
                : Type.EmptyTypes;
            var typeParams = clrArgs
                .Select((arg, i) => new TypeParameterDef { Name = $"T{i}", Variance = GetClrVariance(arg) })
                .ToList();

            return new TypeSymbol
            {
                Name = ClrNameHelper.StripArity(def.Name),
                Kind = SymbolKind.Type,
                TypeKind = TypeKind.Interface,
                ClrType = def,
                TypeParameters = typeParams,
                AccessLevel = AccessLevel.Public
            };
        });
    }

    /// <summary>
    /// Returns true when the interface (or its generic definition) is publicly visible.
    /// </summary>
    internal static bool IsPublicInterface(Type iface)
    {
        var def = iface.IsGenericType ? iface.GetGenericTypeDefinition() : iface;
        return def.IsVisible;
    }

    /// <summary>
    /// Reads the declared CLR variance (out/in) of a generic parameter.
    /// </summary>
    internal static TypeParameterVariance GetClrVariance(Type genericParameter)
    {
        return (genericParameter.GenericParameterAttributes & GenericParameterAttributes.VarianceMask) switch
        {
            GenericParameterAttributes.Covariant => TypeParameterVariance.Covariant,
            GenericParameterAttributes.Contravariant => TypeParameterVariance.Contravariant,
            _ => TypeParameterVariance.None
        };
    }
}
