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

    /// <summary>
    /// The C# spelling of a local or parameter — the base the <c>LocalNameAllocator</c> claims, the
    /// parameter and forwarder declarations, and the collision validator's key.
    /// </summary>
    /// <remarks>
    /// A Sharpy local or parameter named <c>_</c> (escaped or not) is spelled
    /// <see cref="UnderscoreLocalSpelling"/> (#2166). Two C# rules make the plain spelling wrong:
    /// a <i>designation</i> spelled <c>_</c> — <c>var (a, _)</c>, <c>out var _</c>, <c>case var _</c>,
    /// <c>.. var _</c> — is a discard, so the local was never declared and an escaped read
    /// <c>`_`</c> failed CS0103; and when a local named <c>_</c> IS in scope, every discard the
    /// emitter writes itself — the <c>_ = expr;</c> expression statement, <c>out _</c> — binds to it
    /// instead of discarding (<c>a, _ = 1, 2</c> then a bare <c>"s"</c> statement was CS0029, and a
    /// bare <c>a + 1</c> silently overwrote <c>_</c>). The verbatim token <c>@_</c> is the same
    /// identifier as <c>_</c>, so it fixes only the first rule; a name in the compiler's reserved
    /// <c>__</c> space (which <see cref="NameMangler.ToCamelCase"/> never produces from a user name
    /// and an escaped local may not spell) is never a discard and never in a discard's way.
    /// </remarks>
    /// <summary>The C# spelling of a Sharpy local or parameter named <c>_</c>; see <see cref="ResolveVariable"/>.</summary>
    public const string UnderscoreLocalSpelling = "__spy_underscore";

    public static string ResolveVariable(string name, bool isBacktickEscaped)
    {
        if (name == "_")
            return UnderscoreLocalSpelling;
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

    /// <summary>
    /// The C# identifier an enum member compiles to (#2037) — the one speller behind the member
    /// symbol's materialized <c>CodeGenInfo.CSharpName</c>, which every declaration, reference and
    /// collision check reads. The DECLARATION's escape flag governs: an escaped member is verbatim
    /// for both kinds, and every reference follows it whether or not the use is escaped. Otherwise a
    /// string-backed member is its singleton field (<see cref="StringEnumShape.MemberFieldName"/>) and
    /// an int-backed one a C# enum member (<see cref="NameMangler.ToEnumMemberName"/>).
    /// </summary>
    public static string ResolveEnumMember(string name, bool isStringEnum, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return Verbatim(name);
        return isStringEnum
            ? StringEnumShape.MemberFieldName(name)
            : NameMangler.ToEnumMemberName(name);
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
