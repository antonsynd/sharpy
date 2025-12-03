using System.Reflection;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Validates protocol usage in Sharpy code, supporting both Sharpy dunder methods
/// and CLR interface implementations for .NET interop.
///
/// NOTE: This class is NOT thread-safe (same as OperatorValidator).
/// </summary>
public class ProtocolValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();
    private readonly ClrMemberCache _clrMemberCache;

    // Cache for CLR protocol discovery (type -> protocols it supports)
    private readonly Dictionary<Type, HashSet<string>> _clrProtocolCache = new();

    public ProtocolValidator(SymbolTable symbolTable, ICompilerLogger? logger = null, ClrMemberCache? clrCache = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
        _clrMemberCache = clrCache ?? new ClrMemberCache();
    }

    /// <summary>Gets the errors collected during protocol validation.</summary>
    public IReadOnlyList<SemanticError> Errors => _errors;

    private void AddError(string message, int line, int column)
    {
        _errors.Add(new SemanticError(message, line, column));
        _logger.LogError(message, line, column);
    }

    /// <summary>
    /// Checks if a type has a specific protocol dunder method.
    /// </summary>
    public bool HasProtocol(SemanticType type, string dunderName)
    {
        // Check Sharpy built-in types first (before CLR discovery)
        // This ensures Python-style protocol support for string type
        if (type == SemanticType.Str)
        {
            // Strings support __len__, __iter__, __contains__, __getitem__
            return dunderName is "__len__" or "__iter__" or "__contains__" or "__getitem__";
        }

        // Check TupleType (heterogeneous tuples like (int, str, bool))
        if (type is TupleType)
        {
            // Tuples support __len__, __iter__, __getitem__ (but not __setitem__ - immutable)
            return dunderName is "__len__" or "__iter__" or "__getitem__";
        }

        // Check generic container types (list[T], dict[K,V], set[T])
        if (type is GenericType generic)
        {
            return generic.Name switch
            {
                "list" => dunderName is "__len__" or "__iter__" or "__contains__" or "__getitem__" or "__setitem__",
                "dict" => dunderName is "__len__" or "__iter__" or "__contains__" or "__getitem__" or "__setitem__",
                "set" => dunderName is "__len__" or "__iter__" or "__contains__",
                "tuple" => dunderName is "__len__" or "__iter__" or "__getitem__",
                _ => false
            };
        }

        // Check Sharpy user-defined types
        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            // Check cached protocol methods first
            if (udt.Symbol.ProtocolMethods.ContainsKey(dunderName))
                return true;

            // Also check regular methods (some protocols might not be in cache yet)
            if (udt.Symbol.Methods.Any(m => m.Name == dunderName))
                return true;
        }

        // Check CLR types via reflection
        var clrType = GetClrType(type);
        if (clrType != null)
        {
            return HasClrProtocol(clrType, dunderName);
        }

        return false;
    }

    private Type? GetClrType(SemanticType type)
    {
        return type switch
        {
            BuiltinType builtin => builtin.ClrType,
            UserDefinedType udt => udt.Symbol?.ClrType,
            GenericType generic => generic.GenericDefinition?.ClrType,
            _ => null
        };
    }

    /// <summary>
    /// Checks if a CLR type supports a protocol by examining its interfaces.
    /// Results are cached per CLR type.
    /// </summary>
    private bool HasClrProtocol(Type clrType, string dunderName)
    {
        // Get or build cache for this type
        if (!_clrProtocolCache.TryGetValue(clrType, out var protocols))
        {
            protocols = DiscoverClrProtocols(clrType);
            _clrProtocolCache[clrType] = protocols;
        }

        return protocols.Contains(dunderName);
    }

    private HashSet<string> DiscoverClrProtocols(Type clrType)
    {
        var protocols = new HashSet<string>();

        // Check for Sharpy.Core.Collections.Interfaces.IIterable<T> -> __iter__
        // This includes Iterator<T> and all Sharpy collections
        // NOTE: Uses hardcoded type name - ensure this matches Sharpy.Core.Collections.Interfaces.IIterable<T>
        var interfaces = _clrMemberCache.GetImplementedInterfaces(clrType);
        if (interfaces.Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition().FullName == "Sharpy.Core.Collections.Interfaces.IIterable`1"))
        {
            protocols.Add("__iter__");
        }

        // IEnumerable<T> or IEnumerable -> __iter__
        if (_clrMemberCache.ImplementsInterface(clrType, typeof(System.Collections.IEnumerable)) ||
            _clrMemberCache.ImplementsInterface(clrType, typeof(IEnumerable<>)))
        {
            protocols.Add("__iter__");
        }

        // Check if type or any base class is Iterator<T> from Sharpy.Core
        // Iterators support __iter__ (returns self)
        // NOTE: Uses hardcoded type name - ensure this matches Sharpy.Core.Iterator<T>
        var currentType = clrType;
        while (currentType != null)
        {
            if (currentType.IsGenericType &&
                currentType.GetGenericTypeDefinition().FullName == "Sharpy.Core.Iterator`1")
            {
                protocols.Add("__iter__");
                break;
            }
            currentType = currentType.BaseType;
        }

        // ICollection<T> or ICollection -> __len__, __contains__
        if (_clrMemberCache.ImplementsInterface(clrType, typeof(System.Collections.ICollection)) ||
            _clrMemberCache.ImplementsInterface(clrType, typeof(ICollection<>)))
        {
            protocols.Add("__len__");
            protocols.Add("__contains__");
        }

        // IList<T> or IList -> __getitem__, __setitem__
        if (_clrMemberCache.ImplementsInterface(clrType, typeof(System.Collections.IList)) ||
            _clrMemberCache.ImplementsInterface(clrType, typeof(IList<>)))
        {
            protocols.Add("__getitem__");
            protocols.Add("__setitem__");
        }

        // IDictionary<K,V> -> __getitem__, __setitem__, __contains__, __len__
        if (_clrMemberCache.ImplementsInterface(clrType, typeof(System.Collections.IDictionary)) ||
            _clrMemberCache.ImplementsInterface(clrType, typeof(IDictionary<,>)))
        {
            protocols.Add("__getitem__");
            protocols.Add("__setitem__");
            protocols.Add("__contains__");
            protocols.Add("__len__");
        }

        // Note: Arrays are handled via IList check above (arrays implement System.Collections.IList)

        // Any object has __str__ (ToString) and __hash__ (GetHashCode)
        protocols.Add("__str__");
        protocols.Add("__hash__");

        return protocols;
    }

    /// <summary>
    /// Validates that a type can be used with len() and returns int.
    /// </summary>
    public SemanticType ValidateLen(SemanticType containerType, int line, int column)
    {
        if (!HasProtocol(containerType, "__len__"))
        {
            AddError(
                $"Type '{containerType.GetDisplayName()}' does not support len() " +
                "(missing '__len__' method). Consider implementing ISized interface.",
                line, column);
            return SemanticType.Unknown;
        }
        return SemanticType.Int;
    }

    /// <summary>
    /// Validates that a type is iterable (for 'for' loops and comprehensions).
    /// Returns the element type if known, otherwise Unknown.
    /// </summary>
    public SemanticType ValidateIteration(SemanticType iterableType, int line, int column)
    {
        if (!HasProtocol(iterableType, "__iter__"))
        {
            AddError(
                $"Type '{iterableType.GetDisplayName()}' is not iterable " +
                "(missing '__iter__' method). Consider implementing IIterable<T> interface.",
                line, column);
            return SemanticType.Unknown;
        }

        // Try to infer element type from generic argument
        if (iterableType is GenericType generic && generic.TypeArguments.Count > 0)
        {
            // For dict, iteration yields keys (first type argument)
            return generic.TypeArguments[0];
        }

        // For tuples, return the first element type (simplified for heterogeneous tuples)
        if (iterableType is TupleType tuple && tuple.ElementTypes.Count > 0)
        {
            return tuple.ElementTypes[0];
        }

        // For strings, element type is str (single characters)
        if (iterableType == SemanticType.Str)
        {
            return SemanticType.Str;
        }

        // For BuiltinTypes with CLR type that is an Iterator<T>, extract element type
        if (iterableType is BuiltinType builtin && builtin.ClrType != null)
        {
            var elementType = GetIteratorElementType(builtin.ClrType);
            if (elementType != null)
            {
                return MapClrTypeToSemanticType(elementType);
            }
        }

        return SemanticType.Unknown;
    }

    /// <summary>
    /// Gets the element type if the given type is Iterator&lt;T&gt; or extends Iterator&lt;T&gt;.
    /// Returns null otherwise.
    /// </summary>
    /// <remarks>
    /// TODO: This method is duplicated in Discovery/TypeMapper.cs. 
    /// Consider consolidating in Phase 7 (ClrMemberCache extraction).
    /// </remarks>
    private Type? GetIteratorElementType(Type clrType)
    {
        var currentType = clrType;
        while (currentType != null)
        {
            if (currentType.IsGenericType &&
                currentType.GetGenericTypeDefinition().FullName == "Sharpy.Core.Iterator`1")
            {
                return currentType.GetGenericArguments()[0];
            }
            currentType = currentType.BaseType;
        }
        return null;
    }

    /// <summary>
    /// Maps a CLR type to a SemanticType. Basic implementation for element type extraction.
    /// </summary>
    /// <remarks>
    /// TODO: This duplicates Discovery/TypeMapper logic. Consolidate in Phase 6.
    /// Missing coverage for: char, byte, short, uint, ulong, decimal, etc.
    /// </remarks>
    private SemanticType MapClrTypeToSemanticType(Type clrType)
    {
        if (clrType == typeof(int)) return SemanticType.Int;
        if (clrType == typeof(long)) return SemanticType.Long;
        if (clrType == typeof(float)) return SemanticType.Float;
        if (clrType == typeof(double)) return SemanticType.Double;
        if (clrType == typeof(bool)) return SemanticType.Bool;
        if (clrType == typeof(string)) return SemanticType.Str;
        return SemanticType.Object;
    }

    /// <summary>
    /// Validates the 'in' operator (membership test).
    /// </summary>
    public SemanticType ValidateMembership(
        SemanticType containerType,
        SemanticType itemType,
        int line,
        int column)
    {
        if (!HasProtocol(containerType, "__contains__"))
        {
            AddError(
                $"Type '{containerType.GetDisplayName()}' does not support membership testing " +
                "(missing '__contains__' method). Consider implementing IContainer<T> interface.",
                line, column);
            return SemanticType.Unknown;
        }

        // TODO: Consider validating that itemType is assignable to the container's element type
        // For now, we just check protocol support and return bool

        return SemanticType.Bool;
    }

    /// <summary>
    /// Validates indexing access (e.g., x[0]).
    /// Returns the element type if known.
    /// </summary>
    public SemanticType ValidateIndexAccess(
        SemanticType containerType,
        SemanticType indexType,
        int line,
        int column)
    {
        if (!HasProtocol(containerType, "__getitem__"))
        {
            AddError(
                $"Type '{containerType.GetDisplayName()}' does not support indexing " +
                "(missing '__getitem__' method). Consider implementing ISequence<T> interface.",
                line, column);
            return SemanticType.Unknown;
        }

        // TODO: Validate index type matches container expectations:
        // - list/tuple: index should be int (or slice)
        // - dict: index should be compatible with key type

        // Infer element type from generic argument
        if (containerType is GenericType generic)
        {
            // For dict, indexing returns value type (second argument)
            if (generic.Name == "dict" && generic.TypeArguments.Count > 1)
            {
                return generic.TypeArguments[1];
            }
            // For list/tuple, return element type (first argument)
            if (generic.TypeArguments.Count > 0)
            {
                return generic.TypeArguments[0];
            }
        }

        // For tuples, indexing returns the element type (simplified: returns first element type)
        // Ideally would track the exact index for precise typing with heterogeneous tuples
        if (containerType is TupleType tuple && tuple.ElementTypes.Count > 0)
        {
            return tuple.ElementTypes[0];
        }

        // For strings, indexing returns str
        if (containerType == SemanticType.Str)
        {
            return SemanticType.Str;
        }

        return SemanticType.Unknown;
    }

    /// <summary>
    /// Validates boolean conversion (for if/while conditions).
    /// Returns bool.
    /// </summary>
    public SemanticType ValidateBoolConversion(SemanticType type, int line, int column)
    {
        // All types can be used in boolean context in Python/Sharpy
        // __bool__ is optional - if missing, truthiness is determined by:
        // 1. If __len__ exists, truthy if len > 0
        // 2. Otherwise, always truthy (non-None objects)

        // For now, we don't require __bool__ - this is informational
        // Just return bool, no error
        return SemanticType.Bool;
    }
}
