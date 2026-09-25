extern alias SharpyRT;

using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Bidirectional authority for Sharpy&lt;-&gt;C# name transformations.
/// The forward direction (Sharpy -> C#) mangles snake_case/SCREAMING_SNAKE_CASE to
/// PascalCase/camelCase; the reverse direction (C#/.NET -> Sharpy) demangles PascalCase
/// back to snake_case/SCREAMING_SNAKE_CASE for discovered .NET APIs.
/// </summary>
/// <remarks>
/// The forward member rule is Sharpy.Core's <c>NameMangling</c> (#2040, R-CG): the runtime needs it
/// too (<c>str.format</c>'s <c>{0.attr}</c>), so it is written once there and every forward method
/// here delegates, adding only the C#-text concern this class owns — keyword escaping. The module
/// identifier casings, the collection-verb tables and the reverse direction stay here.
/// </remarks>
internal static class NameMangler
{
    // Python collection method mappings to C# equivalents.
    // With Sharpy.List<T>/Dict<K, V>, Pythonic method names are used directly (Append, Extend,
    // Pop, etc.) so most mappings just use ToPascalCase. Only add entries here for methods whose
    // PascalCase name differs from the target C# method name — i.e. single lowercase words that
    // ToPascalCase cannot re-split into their true multi-word C# form.
    //
    //   setdefault -> SetDefault (ToPascalCase would give "Setdefault" -> CS1061)
    //   popitem    -> PopItem    (ToPascalCase would give "Popitem"    -> CS1061)
    //
    // See: #99 (unconditional mapping should use type information from semantic analysis),
    //      #1069 (dict setdefault/popitem mangled to non-existent CLR names).
    private static readonly Dictionary<string, string> _collectionMethodMap = new()
    {
        ["setdefault"] = "SetDefault",
        ["popitem"] = "PopItem",
    };

    // #1571: Python collection verbs → exact-semantics CLR INSTANCE methods.
    // Distinct from _collectionMethodMap (which is the #1069 casing table for
    // Sharpy's OWN collections). This table maps verbs to CLR methods that have
    // the same mutation semantics on ICollection<T>-implementing receivers.
    private static readonly Dictionary<string, string> _clrCollectionVerbMap = new()
    {
        ["append"] = "Add",
        ["index"] = "IndexOf",
    };

    /// <summary>
    /// Preserve type names as-is. Only handles keyword escaping and special prefixes.
    /// </summary>
    public static string ToTypeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Type declarations must use the same casing as type references
        // (which go through ToPascalCase via NameCasing.ResolveType / TypeSyntaxMapper).
        // ToPascalCase is idempotent on already-PascalCase names (e.g. CsvReader, HTTPServer),
        // so existing PascalCase types are unaffected, while Python-style lowercase type names
        // (e.g. socket's `error`, `timeout`) are mangled consistently in both declaration and
        // reference positions. Without this, `class error` would declare `error` but be
        // referenced as `Error`, producing non-compiling C#.
        return ToPascalCase(name);
    }

    /// <summary>
    /// Convert snake_case to PascalCase for methods and functions — Sharpy.Core's
    /// <c>NameMangling.ToPascalCase</c> (the one copy of the rule, #2040), escaped when the result is
    /// a C# keyword.
    /// </summary>
    public static string ToPascalCase(string name)
        => EscapeKeywordIfNeeded(SharpyRT::Sharpy.NameMangling.ToPascalCase(name));

    /// <summary>
    /// Convert snake_case to camelCase for variables and parameters — Sharpy.Core's
    /// <c>NameMangling.ToCamelCase</c>, escaped when the result is a C# keyword.
    /// </summary>
    public static string ToCamelCase(string name)
        => EscapeKeywordIfNeeded(SharpyRT::Sharpy.NameMangling.ToCamelCase(name));

    /// <summary>
    /// Resolve constant names (SCREAMING_SNAKE_CASE kept, snake_case → PascalCase) — Sharpy.Core's
    /// <c>NameMangling.ToConstantCase</c>, escaped when the result is a C# keyword.
    /// </summary>
    public static string ToConstantCase(string name)
        => EscapeKeywordIfNeeded(SharpyRT::Sharpy.NameMangling.ToConstantCase(name));

    /// <summary>
    /// Resolve int-enum member names (SCREAMING_SNAKE_CASE kept, other forms title-cased) —
    /// Sharpy.Core's <c>NameMangling.ToEnumMemberName</c>, escaped when the result is a C# keyword.
    /// </summary>
    public static string ToEnumMemberName(string name)
        => EscapeKeywordIfNeeded(SharpyRT::Sharpy.NameMangling.ToEnumMemberName(name));

    /// <summary>
    /// Transform identifier based on context
    /// </summary>
    public static string Transform(string name, NameContext context)
    {
        return context switch
        {
            NameContext.Type => ToTypeName(name),
            NameContext.Interface => ToInterfaceName(name),
            NameContext.Method => ToPascalCase(name),
            NameContext.Function => ToPascalCase(name),
            NameContext.Variable => ToCamelCase(name),
            NameContext.Parameter => ToCamelCase(name),
            NameContext.Constant => ToConstantCase(name),
            NameContext.Field => ToPascalCase(name),
            NameContext.EnumMember => ToEnumMemberName(name),
            _ => name
        };
    }

    /// <summary>
    /// Preserve interface names as-is. Only handles keyword escaping.
    /// </summary>
    public static string ToInterfaceName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Interfaces preserve user's exact casing
        return EscapeKeywordIfNeeded(name);
    }

    // Common .NET namespace acronyms that should be all uppercase
    private static readonly HashSet<string> _upperCaseAcronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "io", "ui", "xml", "html", "api", "sql", "db", "http", "ftp",
        "smtp", "tcp", "udp", "ip", "uri", "url", "json", "csv", "guid"
    };

    /// <summary>
    /// Convert a module-name identifier (or a single dot-delimited segment of one) to
    /// PascalCase using the given <paramref name="policy"/> to control acronym handling.
    /// This is the single mechanics implementation behind the three intentionally
    /// divergent module-name casings (see <see cref="AcronymPolicy"/> for why they differ).
    /// </summary>
    public static string ToModuleIdentifier(string name, AcronymPolicy policy)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return policy switch
        {
            // Only the first character is upper-cased; the rest is preserved verbatim.
            // Deliberately no acronym logic (stdlib module class: json → Json, not JSON).
            AcronymPolicy.FirstCharOnly => CapitalizePreserving(name),
            // "io" → "IO" only; every other segment splits on '_' and capitalizes each
            // part's first char (system.io namespace mapping).
            AcronymPolicy.IoOnly =>
                name == "io" ? "IO" : string.Concat(name.Split('_').Select(CapitalizePreserving)),
            // Full .NET acronym set upper-cased wholesale; otherwise sanitize invalid
            // identifier characters and PascalCase (user-module namespace parts).
            AcronymPolicy.NamespaceAcronyms => ToNamespaceIdentifier(name),
            _ => name,
        };
    }

    /// <summary>
    /// Convert a name to a valid C# namespace part using simple PascalCase.
    /// Unlike <see cref="ToPascalCase"/>, this does not use form detection or
    /// uniqueness tracking. It handles acronyms, sanitizes invalid characters,
    /// and applies simple capitalize-first-of-each-segment logic.
    /// </summary>
    public static string ToNamespacePart(string name)
    {
        return ToModuleIdentifier(name, AcronymPolicy.NamespaceAcronyms);
    }

    /// <summary>
    /// Full-acronym-set namespace-part mechanics (the <see cref="AcronymPolicy.NamespaceAcronyms"/>
    /// implementation). Precondition: <paramref name="name"/> is non-empty.
    /// </summary>
    private static string ToNamespaceIdentifier(string name)
    {
        // Check if this is a known acronym that should be all uppercase
        if (_upperCaseAcronyms.Contains(name))
        {
            return name.ToUpperInvariant();
        }

        // Replace invalid identifier characters with underscores, then split
        // Valid C# identifier chars: letters, digits (not at start), underscores
        var sanitized = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sanitized.Append(c);
            else
                sanitized.Append('_');
        }

        // Split by underscore and capitalize each part
        var parts = sanitized.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries);

        // Handle edge case where name is only underscores (e.g., "___")
        if (parts.Length == 0)
            return "_";

        var result = string.Join("", parts.Select(p =>
            char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..] : "")
        ));

        // If result starts with a digit, prefix with underscore to make it valid
        if (result.Length > 0 && char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return string.IsNullOrEmpty(result) ? "_" : result;
    }

    /// <summary>
    /// Get the C# equivalent name for a builtin-collection (list/dict/set) method whose
    /// PascalCase form differs from the target CLR name, if such a mapping exists.
    /// </summary>
    public static string? GetCollectionMethodMapping(string methodName)
    {
        return _collectionMethodMap.TryGetValue(methodName, out var mapped) ? mapped : null;
    }

    /// <summary>
    /// Get the CLR INSTANCE method name for a Python collection verb on a CLR
    /// ICollection&lt;T&gt;-implementing receiver (#1571). Returns null when the
    /// verb has no exact-semantics CLR instance target (the caller should refuse
    /// rather than map approximately).
    /// </summary>
    public static string? GetClrCollectionVerbMapping(string methodName)
    {
        return _clrCollectionVerbMap.TryGetValue(methodName, out var mapped) ? mapped : null;
    }

    /// <summary>
    /// The set of Python collection verb names for which a CLR collection receiver
    /// has an exact-semantics instance method mapping (#1571).
    /// </summary>
    public static IReadOnlyDictionary<string, string> ClrCollectionVerbMap => _clrCollectionVerbMap;

    /// <summary>
    /// Module-identifier segments: capitalize the first char, preserve the rest.
    /// </summary>
    private static string CapitalizePreserving(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;
        return char.ToUpperInvariant(word[0]) + word[1..];
    }

    private static string EscapeKeywordIfNeeded(string name)
    {
        return CSharpKeywords.EscapeIfNeeded(name);
    }

    // ------------------------------------------------------------------
    // Reverse direction: C#/.NET PascalCase -> Sharpy snake_case forms.
    // Used when discovering .NET APIs (see Discovery.Caching.OverloadIndexBuilder).
    // ------------------------------------------------------------------

    /// <summary>
    /// Splits a PascalCase/camelCase name into underscore-delimited words using a
    /// 3-pass regex algorithm, without changing letter case:
    /// <list type="number">
    /// <item>Pass 1: Acronym boundaries (XMLParser → XML_Parser)</item>
    /// <item>Pass 2: Digit→word boundaries (Base64Encoder → Base64_Encoder)</item>
    /// <item>Pass 3: camelCase boundaries (getUserName → get_User_Name)</item>
    /// </list>
    /// Shared by <see cref="ToSnakeCase"/> and <see cref="ToScreamingSnakeCase"/>.
    /// </summary>
    private static string SplitWordBoundaries(string name)
    {
        // Pass 1: Acronym boundaries (XMLParser → XML_Parser)
        name = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1_$2");
        // Pass 2: Digit→word boundaries (Base64Encoder → Base64_Encoder)
        name = Regex.Replace(name, "([0-9])([A-Z][a-z])", "$1_$2");
        // Pass 3: camelCase boundaries (getUserName → get_User_Name)
        name = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2");
        return name;
    }

    /// <summary>
    /// Converts a PascalCase name to snake_case. Demangling drops C# keyword escaping:
    /// <c>@do</c> (a camelCase-mangled keyword like Sharpy <c>do</c>) maps back to <c>do</c> —
    /// the <c>@</c> exists only to make the C# identifier legal and is never part of the
    /// Sharpy name.
    /// </summary>
    public static string ToSnakeCase(string name)
    {
        return SplitWordBoundaries(StripKeywordEscape(name)).ToLowerInvariant();
    }

    /// <summary>
    /// Converts a PascalCase name to SCREAMING_SNAKE_CASE using the same word splitting
    /// as <see cref="ToSnakeCase"/>, but joins with uppercase.
    /// </summary>
    public static string ToScreamingSnakeCase(string name)
    {
        return SplitWordBoundaries(StripKeywordEscape(name)).ToUpperInvariant();
    }

    /// <summary>Removes the C# verbatim-identifier prefix (<c>@do</c> → <c>do</c>), if present.</summary>
    private static string StripKeywordEscape(string name) =>
        name.StartsWith("@", StringComparison.Ordinal) ? name.Substring(1) : name;

    /// <summary>
    /// Converts a C#/.NET name to the appropriate Sharpy convention based on context:
    /// <list type="bullet">
    /// <item>Method/Property/Parameter → snake_case</item>
    /// <item>EnumMember/Constant → SCREAMING_SNAKE_CASE</item>
    /// <item>Type/Interface → preserved as-is</item>
    /// </list>
    /// </summary>
    public static string ToSharpyName(string name, ReverseNameContext context)
    {
        return context switch
        {
            ReverseNameContext.Type or ReverseNameContext.Interface => name,
            ReverseNameContext.EnumMember or ReverseNameContext.Constant => ToScreamingSnakeCase(name),
            ReverseNameContext.Field => NameFormDetector.IsConstantCaseName(name) && name.Length > 1 ? name : ToSnakeCase(name),
            _ => ToSnakeCase(name)
        };
    }
}

/// <summary>
/// Acronym-handling policy for <see cref="NameMangler.ToModuleIdentifier"/>. The three
/// module-name→identifier algorithms differ intentionally and must keep producing
/// different output for the same input (e.g. "json" → Json vs "io" → IO vs the full
/// acronym set) — reconciling them is an emitted-API change out of scope for #1040.
/// </summary>
public enum AcronymPolicy
{
    /// <summary>Upper-case only the first character; preserve the rest verbatim, with no
    /// acronym logic (stdlib module class names: json → Json).</summary>
    FirstCharOnly,

    /// <summary>Special-case "io" → "IO"; otherwise split on '_' and capitalize each
    /// segment's first character (system.* namespace mapping).</summary>
    IoOnly,

    /// <summary>Upper-case the full .NET acronym set wholesale (io, ui, xml, ...);
    /// otherwise sanitize invalid identifier characters and PascalCase (user-module
    /// namespace parts, which may produce JSON).</summary>
    NamespaceAcronyms,
}

/// <summary>
/// Context for reverse name mangling, determining the target casing convention.
/// </summary>
public enum ReverseNameContext
{
    Method,       // → snake_case
    Property,     // → snake_case
    Parameter,    // → snake_case
    EnumMember,   // → SCREAMING_SNAKE_CASE
    Constant,     // → SCREAMING_SNAKE_CASE
    Type,         // → preserved as-is
    Interface,    // → preserved as-is
    Field         // → identity when SCREAMING_SNAKE (length > 1), snake_case otherwise
}

/// <summary>
/// Context for name transformation
/// </summary>
public enum NameContext
{
    Type,       // Classes, structs, enums
    Interface,  // Interfaces (special handling for I prefix)
    Method,     // Instance and static methods
    Function,   // Top-level functions
    Variable,   // Local variables
    Parameter,  // Function/method parameters
    Field,      // Class/struct fields
    Constant,   // Constants (CAPS_SNAKE_CASE)
    EnumMember  // Enum members (CAPS_SNAKE_CASE → PascalCase)
}
