using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Converts PascalCase C#/.NET names back to snake_case for Sharpy consumption.
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
}
