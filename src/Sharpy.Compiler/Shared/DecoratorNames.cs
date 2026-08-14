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

    // Diagnostic-oriented decorators. Compile-time only: they steer validation
    // (suppression, must-use), never the emitted C#.
    public const string Suppress = "suppress";
    public const string MustUse = "must_use";

    // Test framework sub-decorators (member access form: @test.parametrize, etc.)
    public const string TestParametrize = "test.parametrize";
    public const string TestSkip = "test.skip";
    public const string TestSkipIf = "test.skip_if";
    public const string TestFixture = "test.fixture";
    public const string TestMark = "test.mark";
    public const string TestCollection = "test.collection";

    /// <summary>
    /// Whether a decorator name maps to an <c>Xunit.*</c> attribute in the emitted C#, and therefore
    /// only means anything where the framework is present (#1495).
    ///
    /// <para>Kept as one list rather than a set of scattered checks, so a NEW test decorator is
    /// host-conditional by joining it — the failure this guards is a framework attribute reaching a
    /// compilation that has no framework, which surfaces as CS0246 behind SPY0908 rather than as a
    /// diagnostic.</para>
    /// </summary>
    public static bool IsTestFrameworkDecorator(string name)
        => name is Test or TestParametrize or TestSkip or TestSkipIf or TestFixture
            or TestMark or TestCollection;

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
    /// Test decorators that register a function as a test entry point.
    /// TestMark and TestCollection are excluded — they are metadata annotations,
    /// not test entry markers, and are validated separately.
    /// </summary>
    public static readonly ImmutableHashSet<string> KnownTestDecorators = new[]
    {
        Test,
        TestParametrize,
        TestSkip,
        TestSkipIf,
        TestFixture,
    }.ToImmutableHashSet();

    /// <summary>
    /// Returns true when the decorator registers its function as a test entry point
    /// (<c>@test</c>, <c>@test.parametrize</c>, <c>@test.skip</c>, <c>@test.skip_if</c>,
    /// <c>@test.fixture</c>). Bracket attributes (<c>@[...]</c>) are excluded. Shared by the
    /// TypeChecker and the emitter so both agree on what a test function is.
    /// </summary>
    public static bool IsTestDecorator(Parser.Ast.Decorator decorator)
        => !decorator.IsBracketAttribute && KnownTestDecorators.Contains(decorator.Name);
}
