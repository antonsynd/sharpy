using System.Collections;
using System.Reflection;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Maps CLR types to Sharpy SemanticType instances.
/// </summary>
public class TypeMapper
{
    private readonly Dictionary<Type, SemanticType> _typeCache = new();

    /// <summary>
    /// Map a CLR type to a Sharpy SemanticType.
    /// </summary>
    public SemanticType MapClrTypeToSemanticType(Type clrType)
    {
        // Check cache first
        if (_typeCache.TryGetValue(clrType, out var cached))
            return cached;

        var result = MapTypeInternal(clrType);
        _typeCache[clrType] = result;
        return result;
    }

    private SemanticType MapTypeInternal(Type clrType)
    {
        // Handle primitive types
        if (clrType == typeof(int)) return SemanticType.Int;
        if (clrType == typeof(long)) return SemanticType.Long;
        if (clrType == typeof(float)) return SemanticType.Float;
        if (clrType == typeof(double)) return SemanticType.Double;
        if (clrType == typeof(bool)) return SemanticType.Bool;
        if (clrType == typeof(string)) return SemanticType.Str;
        if (clrType == typeof(void)) return SemanticType.Void;
        if (clrType == typeof(object)) return SemanticType.Object;

        // Handle arrays
        if (clrType.IsArray)
        {
            var elementType = clrType.GetElementType()!;
            return new GenericType
            {
                Name = "list",
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

        // Fallback to object for unknown types
        return SemanticType.Object;
    }

    private SemanticType MapGenericType(Type clrType)
    {
        var genericDef = clrType.GetGenericTypeDefinition();
        var typeArgs = clrType.GetGenericArguments();

        // List<T> or IList<T>
        if (IsGenericTypeDefinition(genericDef, typeof(List<>)) ||
            IsGenericTypeDefinition(genericDef, typeof(IList<>)))
        {
            return new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // Dictionary<K, V> or IDictionary<K, V>
        if (IsGenericTypeDefinition(genericDef, typeof(Dictionary<,>)) ||
            IsGenericTypeDefinition(genericDef, typeof(IDictionary<,>)))
        {
            return new GenericType
            {
                Name = "dict",
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // HashSet<T> or ISet<T>
        if (IsGenericTypeDefinition(genericDef, typeof(HashSet<>)) ||
            IsGenericTypeDefinition(genericDef, typeof(ISet<>)))
        {
            return new GenericType
            {
                Name = "set",
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
                Name = "list",  // Treat iterables as lists for simplicity
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

        // Unknown generic type - fallback to object
        return SemanticType.Object;
    }

    private bool IsGenericTypeDefinition(Type type, Type genericTypeDef)
    {
        return type == genericTypeDef || type.FullName == genericTypeDef.FullName;
    }
}
