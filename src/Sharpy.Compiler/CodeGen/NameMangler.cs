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
    /// Convert snake_case to PascalCase for methods and functions.
    /// Uses form detection to handle different naming conventions:
    /// - SnakeCase/SingleWordLower: split on _, capitalize each segment (preserving rest)
    /// - ScreamingSnakeCase: split on _, title-case each segment (normalizing rest)
    /// - PascalCase/SingleWordUpper: pass through as-is
    /// - CamelCase: pass through as-is
    /// - Unrecognized: pass through as-is
    /// </summary>
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle literal names (backtick-escaped) - strip backticks and return as-is
        if (name.StartsWith("`") && name.EndsWith("`"))
            return name[1..^1];

        // Handle double-private prefix (__foo but NOT __foo__)
        var hasDoublePrivatePrefix = name.StartsWith("__") && !name.EndsWith("__");
        // Handle single-private prefix (_foo but NOT __foo)
        var hasPrivatePrefix = !hasDoublePrivatePrefix && name.StartsWith("_") && !name.StartsWith("__");

        string cleanName;
        if (hasDoublePrivatePrefix)
            cleanName = name[2..];
        else if (hasPrivatePrefix)
            cleanName = name[1..];
        else
            cleanName = name;

        // Count and preserve trailing underscores (Python allows x_, x__, etc. as different variables)
        var trailingUnderscoreCount = 0;
        for (int i = cleanName.Length - 1; i >= 0 && cleanName[i] == '_'; i--)
            trailingUnderscoreCount++;
        // Remove trailing underscores for processing (but preserve if they're the entire name)
        if (trailingUnderscoreCount > 0 && trailingUnderscoreCount < cleanName.Length)
            cleanName = cleanName[..^trailingUnderscoreCount];

        // Detect name form and transform accordingly
        var form = NameFormDetector.Detect(cleanName);
        var result = form switch
        {
            NameForm.SnakeCase or NameForm.SingleWordLower =>
                string.Join("", cleanName.Split('_').Select(CapitalizePreserving)),
            NameForm.ScreamingSnakeCase =>
                string.Join("", cleanName.Split('_').Select(CapitalizeNormalizing)),
            NameForm.PascalCase or NameForm.SingleWordUpper => cleanName,
            NameForm.CamelCase => cleanName,
            NameForm.Dunder => cleanName, // Dunders pass through — callers use DunderMapping directly
            _ => cleanName, // Unrecognized, Literal
        };

        // Restore trailing underscores
        if (trailingUnderscoreCount > 0)
            result += new string('_', trailingUnderscoreCount);

        // Restore prefix
        if (hasDoublePrivatePrefix)
            result = "__" + result;
        else if (hasPrivatePrefix)
            result = "_" + result;

        return EscapeKeywordIfNeeded(result);
    }

    /// <summary>
    /// Convert snake_case to camelCase for variables and parameters.
    /// Uses form detection to handle different naming conventions:
    /// - SnakeCase/SingleWordLower: first segment lowercase, rest capitalized (preserving)
    /// - ScreamingSnakeCase: first segment fully lowered, rest title-cased (normalizing)
    /// - PascalCase: first char to lower, rest preserved
    /// - CamelCase: pass through as-is
    /// - SingleWordUpper: fully lowercase
    /// - Unrecognized: pass through as-is
    /// </summary>
    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle literal names (backtick-escaped)
        if (name.StartsWith("`") && name.EndsWith("`"))
            return name[1..^1];

        // Handle dunder methods - shouldn't be used for variables, but just in case
        if (name.StartsWith("__") && name.EndsWith("__") && name.Length > 4)
            return name;

        // Handle double-private prefix (__foo but NOT __foo__)
        var hasDoublePrivatePrefix = name.StartsWith("__") && !name.EndsWith("__");
        // Handle single-private prefix (_foo but NOT __foo)
        var hasPrivatePrefix = !hasDoublePrivatePrefix && name.StartsWith("_") && !name.StartsWith("__");

        string cleanName;
        if (hasDoublePrivatePrefix)
            cleanName = name[2..];
        else if (hasPrivatePrefix)
            cleanName = name[1..];
        else
            cleanName = name;

        // Count and preserve trailing underscores (Python allows x_, x__, etc. as different variables)
        var trailingUnderscoreCount = 0;
        for (int i = cleanName.Length - 1; i >= 0 && cleanName[i] == '_'; i--)
            trailingUnderscoreCount++;
        // Remove trailing underscores for processing (but preserve if they're the entire name)
        if (trailingUnderscoreCount > 0 && trailingUnderscoreCount < cleanName.Length)
            cleanName = cleanName[..^trailingUnderscoreCount];

        // Detect name form and transform accordingly
        var form = NameFormDetector.Detect(cleanName);
        string result;
        switch (form)
        {
            case NameForm.SnakeCase or NameForm.SingleWordLower:
                {
                    var parts = cleanName.Split('_');
                    result = parts[0].ToLowerInvariant();
                    if (parts.Length > 1)
                        result += string.Join("", parts.Skip(1).Select(CapitalizePreserving));
                    break;
                }
            case NameForm.ScreamingSnakeCase:
                {
                    var parts = cleanName.Split('_');
                    result = parts[0].ToLowerInvariant();
                    if (parts.Length > 1)
                        result += string.Join("", parts.Skip(1).Select(CapitalizeNormalizing));
                    break;
                }
            case NameForm.PascalCase:
                result = char.ToLowerInvariant(cleanName[0]) + cleanName[1..];
                break;
            case NameForm.SingleWordUpper:
                result = cleanName.ToLowerInvariant();
                break;
            case NameForm.CamelCase:
                result = cleanName;
                break;
            default:
                result = cleanName; // Unrecognized, Dunder, Literal — pass through
                break;
        }

        // Restore trailing underscores
        if (trailingUnderscoreCount > 0)
            result += new string('_', trailingUnderscoreCount);

        // Restore prefix
        if (hasDoublePrivatePrefix)
            result = "__" + result;
        else if (hasPrivatePrefix)
            result = "_" + result;

        return EscapeKeywordIfNeeded(result);
    }

    /// <summary>
    /// Convert constant names to PascalCase (matching spec: CAPS_SNAKE_CASE → PascalCase).
    /// Uses form detection:
    /// - ScreamingSnakeCase: PascalCase via normalizing capitalize. MAX_SIZE → MaxSize
    /// - SingleWordUpper: title-case. HTTP → Http
    /// - SnakeCase: PascalCase (same as ToPascalCase for snake_case)
    /// - PascalCase/CamelCase: pass through
    /// - Unrecognized: pass through
    /// </summary>
    public static string ToConstantCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle literal names
        if (name.StartsWith("`") && name.EndsWith("`"))
            return name[1..^1];

        var form = NameFormDetector.Detect(name);
        var result = form switch
        {
            NameForm.ScreamingSnakeCase =>
                string.Join("", name.Split('_').Select(CapitalizeNormalizing)),
            NameForm.SingleWordUpper => CapitalizeNormalizing(name),
            NameForm.SnakeCase or NameForm.SingleWordLower =>
                string.Join("", name.Split('_').Select(CapitalizePreserving)),
            _ => name, // PascalCase, CamelCase, Unrecognized — pass through
        };

        return EscapeKeywordIfNeeded(result);
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
    /// Get the C# equivalent name for a Python list method, if it exists
    /// </summary>
    public static string? GetListMethodMapping(string methodName)
    {
        return _listMethodMap.TryGetValue(methodName, out var mapped) ? mapped : null;
    }

    /// <summary>
    /// For snake_case: capitalize first char, preserve rest as-is.
    /// </summary>
    private static string CapitalizePreserving(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;
        return char.ToUpperInvariant(word[0]) + word[1..];
    }

    /// <summary>
    /// For SCREAMING_SNAKE_CASE: capitalize first char, normalize rest to lowercase.
    /// </summary>
    private static string CapitalizeNormalizing(string word)
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
