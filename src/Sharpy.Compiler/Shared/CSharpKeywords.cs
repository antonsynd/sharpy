namespace Sharpy.Compiler.Shared;

/// <summary>
/// Single source of truth for C# reserved keywords.
/// Used by NameMangler, RoslynEmitter, and NameResolutionService
/// to consistently escape identifiers that collide with C# keywords.
/// </summary>
internal static class CSharpKeywords
{
    internal static readonly HashSet<string> All = new()
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

    /// <summary>
    /// Returns the name prefixed with @ if it is a C# keyword, otherwise returns the name unchanged.
    /// </summary>
    internal static string EscapeIfNeeded(string name)
    {
        return All.Contains(name) ? "@" + name : name;
    }

    /// <summary>
    /// Returns true if the given name is a C# reserved keyword.
    /// </summary>
    internal static bool IsKeyword(string name)
    {
        return All.Contains(name);
    }
}
