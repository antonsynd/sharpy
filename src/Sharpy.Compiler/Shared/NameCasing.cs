namespace Sharpy.Compiler.Shared;

/// <summary>
/// Centralized name casing service that respects backtick escaping.
/// Backtick-escaped names are used verbatim (no mangling). All other
/// names are transformed via NameMangler based on their target kind.
/// </summary>
/// <remarks>
/// "Verbatim" means the Sharpy spelling survives into C#, not that the emitted token is
/// byte-identical to the source: a name that collides with a C# keyword still needs the
/// <c>@</c> prefix to be a legal identifier, exactly as the mangling path does via
/// <see cref="NameMangler"/>. Returning the raw name here emitted the bare keyword —
/// <c>class `event`[T]</c> became <c>class event&lt;T&gt;</c> (#1246). Escaping lives in this
/// one place so every resolver inherits it; <see cref="CSharpKeywords.EscapeIfNeeded"/> is
/// idempotent (<c>All</c> holds bare keywords, so <c>@class</c> is not re-escaped), which is
/// what makes it safe for the downstream callers that also escape module and CLR names.
/// </remarks>
internal static class NameCasing
{
    /// <summary>
    /// Emits a backtick-escaped name verbatim, adding the <c>@</c> prefix when the spelling
    /// collides with a C# keyword.
    /// </summary>
    private static string Verbatim(string name) => CSharpKeywords.EscapeIfNeeded(name);

    public static string ResolveType(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return Verbatim(name);
        return NameMangler.ToPascalCase(name);
    }

    public static string ResolveMethod(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return Verbatim(name);
        return NameMangler.ToPascalCase(name);
    }

    /// <summary>
    /// Resolves a method name, preferring the original CLR method name when available.
    /// CLR names are emitted verbatim so acronym casing survives (e.g., "IsOSPlatform"
    /// instead of round-tripping through name mangling to "IsOsPlatform"). Backtick
    /// escaping still takes precedence.
    /// </summary>
    public static string ResolveMethod(string name, bool isBacktickEscaped, string? clrMethodName)
    {
        if (isBacktickEscaped)
            return Verbatim(name);
        if (clrMethodName is not null)
            return clrMethodName;
        return NameMangler.ToPascalCase(name);
    }

    public static string ResolveField(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return Verbatim(name);
        return NameMangler.ToPascalCase(name);
    }

    /// <summary>
    /// Resolves a field/property name, preferring the original CLR member name when available.
    /// CLR names are emitted verbatim so a lowercase or acronym-cased property survives (e.g.,
    /// socket's <c>type</c> instead of forward-mangling to <c>Type</c>, #1093). Backtick escaping
    /// still takes precedence.
    /// </summary>
    public static string ResolveField(string name, bool isBacktickEscaped, string? clrName)
    {
        if (isBacktickEscaped)
            return Verbatim(name);
        if (clrName is not null)
            return clrName;
        return NameMangler.ToPascalCase(name);
    }

    public static string ResolveVariable(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return Verbatim(name);
        return NameMangler.ToCamelCase(name);
    }

    public static string ResolveConstant(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return Verbatim(name);
        return NameMangler.ToConstantCase(name);
    }

    public static string ResolveNamespace(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return Verbatim(name);
        return NameMangler.ToNamespacePart(name);
    }

    public static string ResolveInterface(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return Verbatim(name);
        return NameMangler.ToInterfaceName(name);
    }
}
