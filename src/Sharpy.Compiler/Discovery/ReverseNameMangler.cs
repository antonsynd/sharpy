using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Context for reverse name mangling, determining the target casing convention.
/// </summary>
internal enum ReverseNameContext
{
    Method,       // → snake_case
    Property,     // → snake_case
    Parameter,    // → snake_case
    EnumMember,   // → SCREAMING_SNAKE_CASE
    Constant,     // → SCREAMING_SNAKE_CASE
    Type,         // → preserved as-is
    Interface     // → preserved as-is
}

/// <summary>
/// Converts PascalCase C#/.NET names back to Sharpy naming conventions.
/// Used by <see cref="Caching.OverloadIndexBuilder"/> when discovering .NET APIs.
/// </summary>
internal static class ReverseNameMangler
{
    /// <summary>
    /// Converts a PascalCase name to snake_case using a 3-pass regex algorithm:
    /// <list type="number">
    /// <item>Pass 1: Acronym boundaries (XMLParser → XML_Parser)</item>
    /// <item>Pass 2: Digit→word boundaries (Base64Encoder → Base64_Encoder)</item>
    /// <item>Pass 3: camelCase boundaries (getUserName → get_User_Name)</item>
    /// </list>
    /// </summary>
    internal static string ToSnakeCase(string name)
    {
        // Pass 1: Acronym boundaries (XMLParser → XML_Parser)
        name = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1_$2");
        // Pass 2: Digit→word boundaries (Base64Encoder → Base64_Encoder)
        name = Regex.Replace(name, "([0-9])([A-Z][a-z])", "$1_$2");
        // Pass 3: camelCase boundaries (getUserName → get_User_Name)
        name = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2");
        return name.ToLowerInvariant();
    }

    /// <summary>
    /// Converts a PascalCase name to SCREAMING_SNAKE_CASE using the same 3-pass
    /// word splitting as <see cref="ToSnakeCase"/>, but joins with uppercase.
    /// </summary>
    internal static string ToScreamingSnakeCase(string name)
    {
        // Pass 1: Acronym boundaries (XMLParser → XML_Parser)
        name = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1_$2");
        // Pass 2: Digit→word boundaries (Base64Encoder → Base64_Encoder)
        name = Regex.Replace(name, "([0-9])([A-Z][a-z])", "$1_$2");
        // Pass 3: camelCase boundaries (getUserName → get_User_Name)
        name = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2");
        return name.ToUpperInvariant();
    }

    /// <summary>
    /// Converts a C#/.NET name to the appropriate Sharpy convention based on context:
    /// <list type="bullet">
    /// <item>Method/Property/Parameter → snake_case</item>
    /// <item>EnumMember/Constant → SCREAMING_SNAKE_CASE</item>
    /// <item>Type/Interface → preserved as-is</item>
    /// </list>
    /// </summary>
    internal static string ToSharpyName(string name, ReverseNameContext context)
    {
        return context switch
        {
            ReverseNameContext.Type or ReverseNameContext.Interface => name,
            ReverseNameContext.EnumMember or ReverseNameContext.Constant => ToScreamingSnakeCase(name),
            _ => ToSnakeCase(name)
        };
    }
}
