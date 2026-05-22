namespace Sharpy.Compiler.Shared;

/// <summary>
/// Centralized name casing service that respects backtick escaping.
/// Backtick-escaped names are used verbatim (no mangling). All other
/// names are transformed via NameMangler based on their target kind.
/// </summary>
internal static class NameCasing
{
    public static string ResolveType(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return name;
        return NameMangler.ToPascalCase(name);
    }

    public static string ResolveMethod(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return name;
        return NameMangler.ToPascalCase(name);
    }

    public static string ResolveField(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return name;
        return NameMangler.ToPascalCase(name);
    }

    public static string ResolveVariable(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return name;
        return NameMangler.ToCamelCase(name);
    }

    public static string ResolveConstant(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return name;
        return NameMangler.ToConstantCase(name);
    }

    public static string ResolveNamespace(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return name;
        return NameMangler.ToNamespacePart(name);
    }

    public static string ResolveInterface(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return name;
        return NameMangler.ToInterfaceName(name);
    }
}
