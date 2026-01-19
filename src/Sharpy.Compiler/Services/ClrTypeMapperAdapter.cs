using Sharpy.Compiler.Semantic;
using System.Collections.Concurrent;
using System.Reflection;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Adapter that provides CLR type mapping using the existing ClrMemberCache.
/// </summary>
public class ClrTypeMapperAdapter : IClrTypeMapper
{
    private readonly ClrMemberCache _clrCache;

    // Thread-safe cache for member lookups
    private readonly ConcurrentDictionary<(Type, string), MemberInfo?> _memberCache = new();

    public ClrTypeMapperAdapter(ClrMemberCache clrCache)
    {
        _clrCache = clrCache ?? throw new ArgumentNullException(nameof(clrCache));
    }

    public Type? GetClrType(SemanticType semanticType)
    {
        // Delegate to SemanticType's built-in CLR type resolution
        return semanticType switch
        {
            BuiltinType bt => bt.ClrType,
            UserDefinedType udt => udt.Symbol?.ClrType,
            GenericType gt => GetGenericClrType(gt),
            NullableType nt => GetNullableClrType(nt),
            _ => null
        };
    }

    public SemanticType GetSemanticType(Type clrType)
    {
        // Map common CLR types to semantic types
        if (clrType == typeof(int)) return SemanticType.Int;
        if (clrType == typeof(long)) return SemanticType.Long;
        if (clrType == typeof(float)) return SemanticType.Float32;
        if (clrType == typeof(double)) return SemanticType.Double;
        if (clrType == typeof(bool)) return SemanticType.Bool;
        if (clrType == typeof(string)) return SemanticType.Str;
        if (clrType == typeof(void)) return SemanticType.Void;
        if (clrType == typeof(object)) return SemanticType.Object;

        // For other types, return unknown (caller should handle)
        return SemanticType.Unknown;
    }

    public bool HasMember(Type clrType, string memberName)
    {
        return GetMember(clrType, memberName) != null;
    }

    public MemberInfo? GetMember(Type clrType, string memberName)
    {
        return _memberCache.GetOrAdd((clrType, memberName), key =>
        {
            var (type, name) = key;

            // Try property first (properties are usually the most targeted lookup)
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (property != null) return property;

            // Try field
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (field != null) return field;

            // Try methods (use GetMethods to handle overloads - just return the first match)
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.Name == name)
                .ToArray();
            if (methods.Length > 0) return methods[0];

            return null;
        });
    }

    /// <summary>
    /// Get the underlying ClrMemberCache for cases that need direct access.
    /// </summary>
    public ClrMemberCache UnderlyingCache => _clrCache;

    private Type? GetGenericClrType(GenericType gt)
    {
        // Handle common generic types
        if (gt.GenericDefinition?.ClrType != null && gt.TypeArguments.Count > 0)
        {
            var typeArgs = gt.TypeArguments
                .Select(ta => GetClrType(ta))
                .Where(t => t != null)
                .ToArray();

            if (typeArgs.Length == gt.TypeArguments.Count)
            {
                try
                {
                    return gt.GenericDefinition.ClrType.MakeGenericType(typeArgs!);
                }
                catch
                {
                    return null;
                }
            }
        }
        return gt.GenericDefinition?.ClrType;
    }

    private Type? GetNullableClrType(NullableType nt)
    {
        var underlyingClr = GetClrType(nt.UnderlyingType);
        if (underlyingClr == null) return null;

        // For value types, wrap in Nullable<T>
        if (underlyingClr.IsValueType)
        {
            return typeof(Nullable<>).MakeGenericType(underlyingClr);
        }

        // Reference types are already nullable
        return underlyingClr;
    }
}
