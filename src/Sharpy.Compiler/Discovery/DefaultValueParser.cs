using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Parses cached default value string representations back to AST Expression nodes.
/// Used by <see cref="CachedModuleDiscovery"/> to reconstruct default parameter values
/// from the OverloadIndex cache.
/// </summary>
internal static class DefaultValueParser
{
    /// <summary>
    /// Parses a cached default value string into an AST Expression.
    /// Returns null for unrecognized formats.
    /// </summary>
    /// <remarks>
    /// The cached format is produced by <c>OverloadIndexBuilder.ConvertDefaultValue()</c>:
    /// <list type="bullet">
    ///   <item><c>"42"</c> → IntegerLiteral</item>
    ///   <item><c>"true"</c> / <c>"false"</c> → BooleanLiteral</item>
    ///   <item><c>"\"hello\""</c> → StringLiteral</item>
    ///   <item><c>"'c'"</c> → StringLiteral (char)</item>
    ///   <item><c>"3.14"</c> or scientific notation → FloatLiteral</item>
    /// </list>
    /// </remarks>
    public static Expression? Parse(string? value)
    {
        if (value == null)
            return null;

        // Boolean literals
        if (value == "true")
            return new BooleanLiteral { Value = true };
        if (value == "false")
            return new BooleanLiteral { Value = false };

        // Quoted string literals: "hello"
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return new StringLiteral { Value = value[1..^1] };

        // Char literals stored as 'c' — treat as string in Sharpy
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
            return new StringLiteral { Value = value[1..^1] };

        // Integer literals (no decimal point, no exponent)
        if (long.TryParse(value, out _))
            return new IntegerLiteral { Value = value };

        // Float literals (contains decimal point or exponent)
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _))
            return new FloatLiteral { Value = value };

        return null;
    }
}
