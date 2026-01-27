using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Converts Sharpy naming conventions to C# naming conventions
/// </summary>
public static class NameMangler
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
        { "__init__", "Constructor" },  // Special handling needed
        { "__str__", "ToString" },
        { "__repr__", "ToString" },
        { "__eq__", "Equals" },
        { "__hash__", "GetHashCode" },
        { "__getitem__", "GetItem" },  // For indexer properties
        { "__setitem__", "SetItem" },  // For indexer properties
        { "__len__", "Length" },  // For Length property
        { "__contains__", "Contains" },  // For Contains method
        { "__iter__", "GetEnumerator" },  // For IEnumerable
        { "__bool__", "ToBoolean" },  // For explicit boolean conversion
        // Operator dunder methods are NOT in this map - they preserve their dunder name
        // e.g., __add__ becomes __Add__, __sub__ becomes __Sub__, etc.
        // This avoids conflicts with user-defined Add(), Sub(), etc. methods
    };

    // Python list method mappings to C# equivalents
    // These are needed because Python and C# have different names for the same operations
    //
    // TODO: Remove this unconditional mapping. The proper fix is to use type information
    // from semantic analysis to determine when the receiver is a list type, then apply
    // the mapping only in that case. This requires passing SemanticInfo to the emitter
    // or storing resolved method names in SemanticBinding. The current approach works
    // because user-defined classes rarely have methods named "append" with list semantics,
    // but it's not correct in the general case.
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

        // If the name doesn't contain underscores and starts with an uppercase letter,
        // it's already in PascalCase - preserve it as-is
        if (!cleanName.Contains('_') && cleanName.Length > 0 && char.IsUpper(cleanName[0]))
        {
            // Restore private prefix if needed
            if (hasPrivatePrefix)
                return EscapeKeywordIfNeeded("_" + cleanName);
            return EscapeKeywordIfNeeded(cleanName);
        }

        var parts = cleanName.Split('_');
        var result = string.Join("", parts.Select(Capitalize));

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

        var parts = cleanName.Split('_');
        if (parts.Length == 0)
            return EscapeKeywordIfNeeded(name);

        var result = parts[0].ToLowerInvariant();
        if (parts.Length > 1)
        {
            result += string.Join("", parts.Skip(1).Select(Capitalize));
        }

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
        return _csharpKeywords.Contains(name.ToLowerInvariant())
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
    Constant    // Constants (CAPS_SNAKE_CASE)
}
