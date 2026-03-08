using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Maps CLR types to Sharpy SemanticType instances.
/// Thread-safe for concurrent use.
/// </summary>
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
                "int" => SemanticType.Int,
                "long" => SemanticType.Long,
                "float" => SemanticType.Float,       // float -> double (per spec)
                "float32" => SemanticType.Float32,   // float32 -> C# float
                "float64" or "double" => SemanticType.Double,
                "bool" => SemanticType.Bool,
                "str" or "string" => SemanticType.Str,
                "void" or "None" => SemanticType.Void,
                "object" => SemanticType.Object,
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

        // Unknown generic type - fallback to object
        return SemanticType.Object;
    }

    private bool IsGenericTypeDefinition(Type type, Type genericTypeDef)
    {
        return type == genericTypeDef;
    }

    /// <summary>
    /// Builds the interface list for a CLR type, synthesizing IDisposable if implemented.
    /// Returns an empty list if no interfaces are synthesized.
    /// </summary>
    private static List<InterfaceReference> BuildInterfaceList(Type clrType)
    {
        if (!typeof(System.IDisposable).IsAssignableFrom(clrType))
            return new List<InterfaceReference>();

        var disposableSymbol = new TypeSymbol
        {
            Name = "IDisposable",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface
        };
        return new List<InterfaceReference>
        {
            new InterfaceReference { Definition = disposableSymbol }
        };
    }
}
