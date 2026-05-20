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
    public const string Dataclass = "dataclass";
    public const string Deprecated = "deprecated";
    public const string Readonly = "readonly";
    public const string LruCache = "lru_cache";
    public const string Cache = "cache";
    public const string Test = "test";

    // Test framework sub-decorators (member access form: @test.parametrize, etc.)
    public const string TestParametrize = "test.parametrize";
    public const string TestSkip = "test.skip";
    public const string TestSkipIf = "test.skip_if";
    public const string TestFixture = "test.fixture";

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
        Readonly,
    }.ToImmutableHashSet();

    /// <summary>
    /// Built-in decorators that emit as C# attributes (not modifier keywords).
    /// These require special handling in codegen rather than default name-mangling.
    /// </summary>
    public static readonly ImmutableHashSet<string> KnownAttributeDecorators = new[]
    {
        Deprecated,
    }.ToImmutableHashSet();

    /// <summary>
    /// Built-in decorators for test framework features.
    /// These require special codegen handling (e.g., @test → [Fact]).
    /// </summary>
    public static readonly ImmutableHashSet<string> KnownTestDecorators = new[]
    {
        Test,
        TestParametrize,
        TestSkip,
        TestSkipIf,
        TestFixture,
    }.ToImmutableHashSet();
}
