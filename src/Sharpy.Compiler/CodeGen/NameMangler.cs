namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Converts Sharpy naming conventions to C# naming conventions
/// </summary>
public static class NameMangler
{
    private static readonly HashSet<string> _usedNames = new();

    /// <summary>
    /// Convert snake_case to PascalCase for methods and types
    /// </summary>
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle dunder methods
        if (name.StartsWith("__") && name.EndsWith("__"))
        {
            return name; // Keep as-is for now
        }

        var parts = name.Split('_');
        var result = string.Join("", parts.Select(Capitalize));

        return EnsureUnique(result);
    }

    /// <summary>
    /// Convert snake_case to camelCase for variables and parameters
    /// </summary>
    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var parts = name.Split('_');
        if (parts.Length == 0)
            return name;

        var result = parts[0].ToLowerInvariant();
        if (parts.Length > 1)
        {
            result += string.Join("", parts.Skip(1).Select(Capitalize));
        }

        return EnsureUnique(result);
    }

    private static string Capitalize(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
    }

    private static string EnsureUnique(string name)
    {
        var original = name;
        var counter = 1;

        while (_usedNames.Contains(name))
        {
            name = $"{original}{counter}";
            counter++;
        }

        _usedNames.Add(name);
        return name;
    }

    public static void Reset()
    {
        _usedNames.Clear();
    }
}
