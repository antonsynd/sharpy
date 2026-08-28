using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Resolves a <see cref="TypeSymbol"/> to its conversion-function overloads by CLR type
/// identity, replacing six spelling-keyed lookup sites that broke when the user wrote an
/// alias or a non-canonical width spelling (#1637, #1636).
/// </summary>
internal static class PrimitiveConversionResolver
{
    /// <summary>
    /// Returns the conversion-function overloads for a primitive TypeSymbol, or null when the
    /// symbol is not a builtin primitive with a conversion function. Keyed on CLR type identity,
    /// not on the spelling that appears in source.
    /// </summary>
    public static List<FunctionSymbol>? ResolveOverloads(
        TypeSymbol typeSymbol, BuiltinRegistry registry)
    {
        if (typeSymbol.ClrType == null)
            return null;

        var info = PrimitiveCatalog.GetByClrType(typeSymbol.ClrType);
        if (info?.ConversionFunction == null)
            return null;

        if (!registry.IsBuiltinSymbol(typeSymbol))
            return null;

        // Use the TypeSymbol's registered name, not the catalog's primary name —
        // the catalog primary is int32/float64 but the overloads are registered
        // under int/float (the user-facing name that matches the CLR discovery).
        return registry.GetFunctionOverloads(typeSymbol.Name)
            ?? registry.GetFunctionOverloads(info.SharpyName);
    }

    /// <summary>
    /// True when the TypeSymbol is a builtin primitive that has a callable conversion function.
    /// </summary>
    public static bool IsPrimitiveConversion(TypeSymbol typeSymbol, BuiltinRegistry registry)
    {
        return ResolveOverloads(typeSymbol, registry) != null;
    }
}
