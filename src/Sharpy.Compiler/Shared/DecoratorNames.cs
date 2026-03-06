using System.Collections.Immutable;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Constants for decorator name strings used across Semantic and CodeGen layers.
/// </summary>
internal static class DecoratorNames
{
    public const string Abstract = "abstract";
    public const string Virtual = "virtual";
    public const string Override = "override";
    public const string Final = "final";
    public const string Static = "static";
    public const string StaticMethod = "staticmethod";
    public const string ClassMethod = "classmethod";

    // Access modifiers
    public const string Public = "public";
    public const string Protected = "protected";
    public const string Private = "private";
    public const string Internal = "internal";

    /// <summary>
    /// All decorator names that map to C# modifier keywords.
    /// These built-in decorators must not accept arguments.
    /// </summary>
    public static readonly ImmutableHashSet<string> KnownModifierDecorators = new[]
    {
        Virtual,
        Static,
        Abstract,
        Override,
        Final,
        Public,
        Protected,
        Private,
        Internal,
    }.ToImmutableHashSet();
}
