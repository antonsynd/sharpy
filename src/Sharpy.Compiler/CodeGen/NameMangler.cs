using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Converts Sharpy naming conventions to C# naming conventions
/// </summary>
internal static class NameMangler
{
    // C# keywords that need @ prefix when used as identifiers
    private static readonly HashSet<string> _csharpKeywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default",
        "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal",
        "is", "lock", "long", "namespace", "new", "null", "object",
        "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while"
    };

    // Dunder method name mappings to C# equivalents
    // Only map dunder methods that have C# override equivalents or special constructs
    // Operator-related dunder methods should preserve their dunder name
    private static readonly Dictionary<string, string> _dunderMethodMap = new()
    {
        { DunderNames.Init, "Constructor" },  // Special handling needed
        { DunderNames.Str, "ToString" },
        { DunderNames.Repr, "ToString" },
        { DunderNames.Eq, "Equals" },
        { DunderNames.Hash, "GetHashCode" },
        { DunderNames.GetItem, "GetItem" },  // For indexer properties
        { DunderNames.SetItem, "SetItem" },  // For indexer properties
        { DunderNames.Len, "Length" },  // For Length property
        { DunderNames.Contains, "Contains" },  // For Contains method
        { DunderNames.Iter, "GetEnumerator" },  // For IEnumerable
        { DunderNames.Bool, "ToBoolean" },  // For explicit boolean conversion
        // Operator dunder methods are NOT in this map - they preserve their dunder name
        // e.g., __add__ becomes __Add__, __sub__ becomes __Sub__, etc.
        // This avoids conflicts with user-defined Add(), Sub(), etc. methods
    };

    // Python list method mappings to C# equivalents
    // These are needed because Python and C# have different names for the same operations
    //
    // See: #99 (unconditional mapping should use type information from semantic analysis)
    private static readonly Dictionary<string, string> _listMethodMap = new()
    {
        { "append", "Add" },      // Python list.append() -> C# List.Add()
        { "extend", "AddRange" }, // Python list.extend() -> C# List.AddRange()
        { "pop", "RemoveAt" },    // Python list.pop(i) -> C# List.RemoveAt(i) (Note: pop() without args needs special handling)
        { "remove", "Remove" },   // Python list.remove() -> C# List.Remove()
        { "clear", "Clear" },     // Same name, but included for completeness
    };

#if DEBUG
    static NameMangler()
    {
        // Verify all protocol dunders with CLR mappings are in _dunderMethodMap
        foreach (var protocol in ProtocolRegistry.GetAllProtocols())
        {
            if (protocol.ClrMethodName != null && !_dunderMethodMap.ContainsKey(protocol.DunderName))
            {
                // Fail fast during development - RegistryConsistencyTests also covers this
                System.Diagnostics.Debug.Assert(false,
                    $"Protocol '{protocol.DunderName}' with CLR mapping '{protocol.ClrMethodName}' " +
                    $"is missing from _dunderMethodMap. Add: {{ \"{protocol.DunderName}\", \"...\" }}");
            }
        }
    }
#endif

    /// <summary>
    /// Preserve type names as-is. Only handles keyword escaping and special prefixes.
    /// </summary>
    public static string ToTypeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle literal names (backtick-escaped) - strip backticks and return as-is
        if (name.StartsWith("`") && name.EndsWith("`"))
            return name[1..^1];

        // Types preserve user's exact casing
        return EscapeKeywordIfNeeded(name);
    }

    /// <summary>
    /// Convert snake_case to PascalCase for methods and functions
    /// </summary>
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle literal names (backtick-escaped) - strip backticks and return as-is
        if (name.StartsWith("`") && name.EndsWith("`"))
            return name[1..^1];

        // Handle dunder methods
        if (name.StartsWith("__") && name.EndsWith("__"))
        {
            if (_dunderMethodMap.TryGetValue(name, out var mapped))
                return mapped;

            // Unknown dunder method - preserve dunder but capitalize the middle part
            // e.g., __add__ -> __Add__, __custom_method__ -> __CustomMethod__
            var middle = name[2..^2]; // Remove leading and trailing __
            var capitalizedMiddle = string.Join("", middle.Split('_').Select(Capitalize));
            return $"__{capitalizedMiddle}__";
        }

        // Handle private names (single underscore prefix)
        var hasPrivatePrefix = name.StartsWith("_") && !name.StartsWith("__");
        var cleanName = hasPrivatePrefix ? name[1..] : name;

        // Count and preserve trailing underscores (Python allows x_, x__, etc. as different variables)
        var trailingUnderscoreCount = 0;
        for (int i = cleanName.Length - 1; i >= 0 && cleanName[i] == '_'; i--)
            trailingUnderscoreCount++;
        // Remove trailing underscores for processing (but preserve if they're the entire name)
        if (trailingUnderscoreCount > 0 && trailingUnderscoreCount < cleanName.Length)
            cleanName = cleanName[..^trailingUnderscoreCount];

        // If the name doesn't contain underscores and starts with an uppercase letter,
        // it's already in PascalCase - preserve it as-is
        if (!cleanName.Contains('_') && cleanName.Length > 0 && char.IsUpper(cleanName[0]))
        {
            var pascalResult = cleanName;
            if (trailingUnderscoreCount > 0)
                pascalResult += new string('_', trailingUnderscoreCount);
            // Restore private prefix if needed
            if (hasPrivatePrefix)
                return EscapeKeywordIfNeeded("_" + pascalResult);
            return EscapeKeywordIfNeeded(pascalResult);
        }

        var parts = cleanName.Split('_');
        var result = string.Join("", parts.Select(Capitalize));

        // Restore trailing underscores
        if (trailingUnderscoreCount > 0)
            result += new string('_', trailingUnderscoreCount);

        // Restore private prefix
        if (hasPrivatePrefix)
            result = "_" + result;

        return EscapeKeywordIfNeeded(result);
    }

    /// <summary>
    /// Convert snake_case to camelCase for variables and parameters
    /// </summary>
    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle literal names (backtick-escaped)
        if (name.StartsWith("`") && name.EndsWith("`"))
            return name[1..^1];

        // Handle dunder methods - shouldn't be used for variables, but just in case
        if (name.StartsWith("__") && name.EndsWith("__"))
            return name;

        // Handle private names
        var hasPrivatePrefix = name.StartsWith("_") && !name.StartsWith("__");
        var cleanName = hasPrivatePrefix ? name[1..] : name;

        // Count and preserve trailing underscores (Python allows x_, x__, etc. as different variables)
        var trailingUnderscoreCount = 0;
        for (int i = cleanName.Length - 1; i >= 0 && cleanName[i] == '_'; i--)
            trailingUnderscoreCount++;
        // Remove trailing underscores for processing (but preserve if they're the entire name)
        if (trailingUnderscoreCount > 0 && trailingUnderscoreCount < cleanName.Length)
            cleanName = cleanName[..^trailingUnderscoreCount];

        var parts = cleanName.Split('_');
        if (parts.Length == 0)
            return EscapeKeywordIfNeeded(name);

        var result = parts[0].ToLowerInvariant();
        if (parts.Length > 1)
        {
            result += string.Join("", parts.Skip(1).Select(Capitalize));
        }

        // Restore trailing underscores
        if (trailingUnderscoreCount > 0)
            result += new string('_', trailingUnderscoreCount);

        // Restore private prefix
        if (hasPrivatePrefix)
            result = "_" + result;

        return EscapeKeywordIfNeeded(result);
    }

    /// <summary>
    /// Keep CAPS_SNAKE_CASE for constants
    /// </summary>
    public static string ToConstantCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle literal names
        if (name.StartsWith("`") && name.EndsWith("`"))
            return name[1..^1];

        // Constants stay in CAPS_SNAKE_CASE
        return EscapeKeywordIfNeeded(name);
    }

    /// <summary>
    /// Convert enum member names (typically SCREAMING_SNAKE_CASE) to PascalCase.
    /// </summary>
    public static string ToEnumMemberName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle literal names (backtick-escaped) - strip backticks and return as-is
        if (name.StartsWith("`") && name.EndsWith("`"))
            return name[1..^1];

        // Split by underscores (RemoveEmptyEntries handles consecutive underscores)
        var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var capitalizedParts = parts.Select(part =>
            string.IsNullOrEmpty(part) ? part :
            char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant());

        return string.Join("", capitalizedParts);
    }

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
            NameContext.Field => ToCamelCase(name),
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

        // Handle literal names (backtick-escaped) - strip backticks and return as-is
        if (name.StartsWith("`") && name.EndsWith("`"))
            return name[1..^1];

        // Interfaces preserve user's exact casing
        return EscapeKeywordIfNeeded(name);
    }

    /// <summary>
    /// Check if a name is a dunder method
    /// </summary>
    public static bool IsDunderMethod(string name)
    {
        return name.StartsWith("__") && name.EndsWith("__") && name.Length > 5;
    }

    /// <summary>
    /// Get the C# equivalent name for a dunder method, if it exists
    /// </summary>
    public static string? GetDunderMethodMapping(string dunderName)
    {
        return _dunderMethodMap.TryGetValue(dunderName, out var mapped) ? mapped : null;
    }

    /// <summary>
    /// Get the C# equivalent name for a Python list method, if it exists
    /// </summary>
    public static string? GetListMethodMapping(string methodName)
    {
        return _listMethodMap.TryGetValue(methodName, out var mapped) ? mapped : null;
    }

    private static string Capitalize(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
    }

    private static string EscapeKeywordIfNeeded(string name)
    {
        // If the name is a C# keyword, prefix with @
        return _csharpKeywords.Contains(name)
            ? "@" + name
            : name;
    }
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
