using System.Collections.Frozen;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Exhaustive registry of primitive types supported by Sharpy.
/// Provides type information, promotion rules, and conversion checking.
/// </summary>
public static class PrimitiveCatalog
{
    /// <summary>
    /// Categorizes the numeric nature of a primitive type.
    /// </summary>
    public enum NumericKind
    {
        None,           // Not numeric (void, bool, string, char)
        SignedInteger,  // sbyte, short, int, long
        UnsignedInteger,// byte, ushort, uint, ulong
        FloatingPoint,  // float, double
        Decimal         // decimal (128-bit)
    }

    /// <summary>
    /// Describes a primitive type's characteristics.
    /// </summary>
    /// <param name="SharpyName">The name used in Sharpy source code (e.g., "int", "str")</param>
    /// <param name="CSharpName">The C# keyword to emit (e.g., "int", "string")</param>
    /// <param name="ClrType">The .NET runtime type (e.g., typeof(int), typeof(void))</param>
    /// <param name="Kind">The numeric classification</param>
    /// <param name="SizeInBits">Size in bits (8, 16, 32, 64, 128 for decimal)</param>
    /// <param name="IsSigned">True for signed numeric types</param>
    public record PrimitiveInfo(
        string SharpyName,
        string CSharpName,
        Type ClrType,
        NumericKind Kind,
        int SizeInBits,
        bool IsSigned
    );

    private static readonly FrozenDictionary<string, PrimitiveInfo> _bySharpyName;
    private static readonly FrozenDictionary<Type, PrimitiveInfo> _byClrType;

    static PrimitiveCatalog()
    {
        var byName = new Dictionary<string, PrimitiveInfo>();
        var byClr = new Dictionary<Type, PrimitiveInfo>();
        RegisterAll(byName, byClr);
        _bySharpyName = byName.ToFrozenDictionary();
        _byClrType = byClr.ToFrozenDictionary();
    }

    private static void Register(Dictionary<string, PrimitiveInfo> byName, Dictionary<Type, PrimitiveInfo> byClr, PrimitiveInfo info)
    {
        byName[info.SharpyName] = info;
        byClr[info.ClrType] = info;
    }

    private static void RegisterAll(Dictionary<string, PrimitiveInfo> byName, Dictionary<Type, PrimitiveInfo> byClr)
    {
        // 1.2.1 Signed integer types
        Register(byName, byClr, new PrimitiveInfo("sbyte", "sbyte", typeof(sbyte), NumericKind.SignedInteger, 8, true));
        Register(byName, byClr, new PrimitiveInfo("short", "short", typeof(short), NumericKind.SignedInteger, 16, true));
        Register(byName, byClr, new PrimitiveInfo("int", "int", typeof(int), NumericKind.SignedInteger, 32, true));
        Register(byName, byClr, new PrimitiveInfo("long", "long", typeof(long), NumericKind.SignedInteger, 64, true));

        // 1.2.2 Unsigned integer types
        Register(byName, byClr, new PrimitiveInfo("byte", "byte", typeof(byte), NumericKind.UnsignedInteger, 8, false));
        Register(byName, byClr, new PrimitiveInfo("ushort", "ushort", typeof(ushort), NumericKind.UnsignedInteger, 16, false));
        Register(byName, byClr, new PrimitiveInfo("uint", "uint", typeof(uint), NumericKind.UnsignedInteger, 32, false));
        Register(byName, byClr, new PrimitiveInfo("ulong", "ulong", typeof(ulong), NumericKind.UnsignedInteger, 64, false));

        // 1.2.3 Floating-point types
        Register(byName, byClr, new PrimitiveInfo("float", "float", typeof(float), NumericKind.FloatingPoint, 32, true));
        Register(byName, byClr, new PrimitiveInfo("double", "double", typeof(double), NumericKind.FloatingPoint, 64, true));
        Register(byName, byClr, new PrimitiveInfo("decimal", "decimal", typeof(decimal), NumericKind.Decimal, 128, true));

        // 1.2.4 Non-numeric primitives
        Register(byName, byClr, new PrimitiveInfo("bool", "bool", typeof(bool), NumericKind.None, 8, false));
        Register(byName, byClr, new PrimitiveInfo("char", "char", typeof(char), NumericKind.None, 16, false));
        Register(byName, byClr, new PrimitiveInfo("str", "string", typeof(string), NumericKind.None, 0, false));
        Register(byName, byClr, new PrimitiveInfo("string", "string", typeof(string), NumericKind.None, 0, false)); // Alias

        // 1.2.5 Void/None - typeof(void) is a valid Type representing System.Void
        Register(byName, byClr, new PrimitiveInfo("None", "void", typeof(void), NumericKind.None, 0, false));
        Register(byName, byClr, new PrimitiveInfo("void", "void", typeof(void), NumericKind.None, 0, false)); // Alias
    }

    // ==================== 1.3 Query Methods ====================

    /// <summary>Returns primitive info for a Sharpy type name, or null if not a primitive.</summary>
    public static PrimitiveInfo? GetByName(string sharpyName)
        => _bySharpyName.GetValueOrDefault(sharpyName);

    /// <summary>Returns primitive info for a CLR type, or null if not a primitive.</summary>
    public static PrimitiveInfo? GetByClrType(Type clrType)
        => _byClrType.GetValueOrDefault(clrType);

    /// <summary>Returns true if the name refers to a registered primitive.</summary>
    public static bool IsPrimitive(string sharpyName)
        => _bySharpyName.ContainsKey(sharpyName);

    /// <summary>
    /// Gets PrimitiveInfo from a SemanticType by checking if it's a BuiltinType
    /// and looking up its name or CLR type.
    /// </summary>
    public static PrimitiveInfo? GetPrimitiveInfo(SemanticType type)
    {
        if (type is BuiltinType builtin)
        {
            // Try CLR type first (more reliable)
            if (builtin.ClrType != null && _byClrType.TryGetValue(builtin.ClrType, out var info))
                return info;
            // Fall back to name lookup
            return _bySharpyName.GetValueOrDefault(builtin.Name);
        }
        return null;
    }

    /// <summary>Returns true if the type is any numeric type (integer, float, or decimal).</summary>
    public static bool IsNumeric(SemanticType type)
    {
        var info = GetPrimitiveInfo(type);
        return info != null && info.Kind != NumericKind.None;
    }

    /// <summary>Returns true if the type is an integer (signed or unsigned).</summary>
    public static bool IsInteger(SemanticType type)
    {
        var info = GetPrimitiveInfo(type);
        return info != null &&
               (info.Kind == NumericKind.SignedInteger || info.Kind == NumericKind.UnsignedInteger);
    }

    /// <summary>Returns true if the type is floating-point (float or double).</summary>
    public static bool IsFloatingPoint(SemanticType type)
    {
        var info = GetPrimitiveInfo(type);
        return info != null && info.Kind == NumericKind.FloatingPoint;
    }

    /// <summary>Returns true if the type is decimal.</summary>
    public static bool IsDecimal(SemanticType type)
    {
        var info = GetPrimitiveInfo(type);
        return info != null && info.Kind == NumericKind.Decimal;
    }

    /// <summary>Returns all registered primitives for iteration.</summary>
    public static IEnumerable<(string Name, PrimitiveInfo Info)> GetAllPrimitives()
        => _bySharpyName.Select(kv => (kv.Key, kv.Value));

    // ==================== 1.4 Numeric Promotion Rules ====================

    // Promotion priority: higher value = wider type
    // When mixing types, the result is the type with higher priority
    private static int GetPromotionPriority(PrimitiveInfo info)
    {
        // Handle void type (no promotion possible)
        if (info.ClrType == typeof(void))
            return 0;

        return info.ClrType switch
        {
            // Decimals don't mix with floats
            Type when info.Kind == NumericKind.Decimal => 100,
            // Floating point: double > float
            Type t when t == typeof(double) => 50,
            Type t when t == typeof(float) => 40,
            // Integers by size and signedness
            Type t when t == typeof(ulong) => 35,
            Type t when t == typeof(long) => 34,
            Type t when t == typeof(uint) => 33,
            Type t when t == typeof(int) => 32,
            Type t when t == typeof(ushort) => 31,
            Type t when t == typeof(short) => 30,
            Type t when t == typeof(byte) => 29,
            Type t when t == typeof(sbyte) => 28,
            _ => 0
        };
    }

    /// <summary>
    /// Returns the result type when performing arithmetic between two numeric types.
    /// Uses standard .NET numeric promotion rules.
    /// </summary>
    /// <returns>The promoted type, or null if types cannot be combined.</returns>
    public static PrimitiveInfo? GetPromotedType(PrimitiveInfo left, PrimitiveInfo right)
    {
        // Non-numeric types cannot be promoted
        if (left.Kind == NumericKind.None || right.Kind == NumericKind.None)
            return null;

        // Decimal doesn't mix with float/double
        if ((left.Kind == NumericKind.Decimal) != (right.Kind == NumericKind.Decimal))
            return null;

        // Special case: mixing signed and unsigned integers of same size
        // e.g., int + uint -> long (to avoid overflow)
        if (left.Kind != right.Kind &&
            (left.Kind == NumericKind.SignedInteger || left.Kind == NumericKind.UnsignedInteger) &&
            (right.Kind == NumericKind.SignedInteger || right.Kind == NumericKind.UnsignedInteger) &&
            left.SizeInBits == right.SizeInBits)
        {
            // Promote to next larger signed type, or return null if no safe promotion exists
            // Use direct lookup instead of FirstOrDefault for efficiency
            return left.SizeInBits switch
            {
                8 => GetByName("short"),   // sbyte + byte -> short
                16 => GetByName("int"),    // short + ushort -> int
                32 => GetByName("long"),   // int + uint -> long
                64 => null,   // long + ulong: cannot safely promote; return null to force error
                _ => null
            };
        }

        // Return the type with higher priority
        var leftPriority = GetPromotionPriority(left);
        var rightPriority = GetPromotionPriority(right);

        return leftPriority >= rightPriority ? left : right;
    }

    /// <summary>Overload that accepts SemanticTypes directly.</summary>
    public static SemanticType? GetPromotedType(SemanticType left, SemanticType right)
    {
        var leftInfo = GetPrimitiveInfo(left);
        var rightInfo = GetPrimitiveInfo(right);
        if (leftInfo == null || rightInfo == null)
            return null;

        var promoted = GetPromotedType(leftInfo, rightInfo);
        if (promoted == null)
            return null;

        // Return the matching SemanticType singleton for common types,
        // or create a BuiltinType for less common numeric types
        return promoted.ClrType switch
        {
            // Common types - use SemanticType singletons
            Type t when t == typeof(int) => SemanticType.Int,
            Type t when t == typeof(long) => SemanticType.Long,
            Type t when t == typeof(float) => SemanticType.Float,
            Type t when t == typeof(double) => SemanticType.Double,
            // Less common types - create BuiltinType instances
            // (these are used less frequently in promotion, so creating instances is acceptable)
            Type t when t == typeof(sbyte) => new BuiltinType { Name = "sbyte", ClrType = typeof(sbyte) },
            Type t when t == typeof(byte) => new BuiltinType { Name = "byte", ClrType = typeof(byte) },
            Type t when t == typeof(short) => new BuiltinType { Name = "short", ClrType = typeof(short) },
            Type t when t == typeof(ushort) => new BuiltinType { Name = "ushort", ClrType = typeof(ushort) },
            Type t when t == typeof(uint) => new BuiltinType { Name = "uint", ClrType = typeof(uint) },
            Type t when t == typeof(ulong) => new BuiltinType { Name = "ulong", ClrType = typeof(ulong) },
            Type t when t == typeof(decimal) => new BuiltinType { Name = "decimal", ClrType = typeof(decimal) },
            // For null ClrType (void), this shouldn't happen in numeric promotion
            null => SemanticType.Unknown,
            _ => new BuiltinType { Name = promoted.SharpyName, ClrType = promoted.ClrType }
        };
    }

    // ==================== 1.5 Conversion Checking ====================

    /// <summary>
    /// Returns true if 'from' can be implicitly converted to 'to' without data loss.
    /// </summary>
    public static bool CanImplicitlyConvert(PrimitiveInfo from, PrimitiveInfo to)
    {
        // Handle void type (no conversion possible)
        if (from.ClrType == typeof(void) || to.ClrType == typeof(void))
            return false;

        if (from.ClrType == to.ClrType)
            return true;

        // Non-numeric types only convert to themselves
        if (from.Kind == NumericKind.None || to.Kind == NumericKind.None)
            return false;

        // Decimal only accepts integers, not floats
        if (to.Kind == NumericKind.Decimal)
            return from.Kind == NumericKind.SignedInteger || from.Kind == NumericKind.UnsignedInteger;

        // From decimal: no implicit conversions
        if (from.Kind == NumericKind.Decimal)
            return false;

        // Integer to float/double: allowed by C# spec (precision may be lost for large values)
        if ((from.Kind == NumericKind.SignedInteger || from.Kind == NumericKind.UnsignedInteger) &&
            to.Kind == NumericKind.FloatingPoint)
            return true;

        // Float to double: allowed
        if (from.ClrType == typeof(float) && to.ClrType == typeof(double))
            return true;

        // Integer widening: allowed if target is larger and signedness is compatible
        if ((from.Kind == NumericKind.SignedInteger || from.Kind == NumericKind.UnsignedInteger) &&
            (to.Kind == NumericKind.SignedInteger || to.Kind == NumericKind.UnsignedInteger))
        {
            // Unsigned to signed requires extra bit
            if (!from.IsSigned && to.IsSigned)
                return to.SizeInBits > from.SizeInBits;
            // Signed to unsigned: not implicit
            if (from.IsSigned && !to.IsSigned)
                return false;
            // Same signedness: size must be >=
            return to.SizeInBits >= from.SizeInBits;
        }

        return false;
    }

    /// <summary>
    /// Returns true if 'from' can be explicitly cast to 'to' (may lose data).
    /// </summary>
    public static bool CanExplicitlyConvert(PrimitiveInfo from, PrimitiveInfo to)
    {
        // Handle void type (no conversion possible)
        if (from.ClrType == typeof(void) || to.ClrType == typeof(void))
            return false;

        // Anything numeric can be explicitly cast to any other numeric
        if (from.Kind != NumericKind.None && to.Kind != NumericKind.None)
            return true;

        // char <-> integer explicit conversions
        if (from.ClrType == typeof(char) &&
            (to.Kind == NumericKind.SignedInteger || to.Kind == NumericKind.UnsignedInteger))
            return true;
        if (to.ClrType == typeof(char) &&
            (from.Kind == NumericKind.SignedInteger || from.Kind == NumericKind.UnsignedInteger))
            return true;

        return CanImplicitlyConvert(from, to);
    }
}
