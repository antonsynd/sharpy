using System.Collections.Generic;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Valid keyword argument names for the @dataclass decorator.
/// Used by both DecoratorValidator (to reject unknown options) and
/// TypeChecker (to extract option values).
/// </summary>
internal static class DataclassOptionNames
{
    public const string Frozen = "frozen";
    public const string Eq = "eq";
    public const string Repr = "repr";

    public static readonly IReadOnlySet<string> KnownOptions = new HashSet<string>
    {
        Frozen,
        Eq,
        Repr,
    };
}
