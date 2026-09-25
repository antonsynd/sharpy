using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Determines access level from Python underscore naming convention.
/// Single source of truth for the mapping used by NameResolver, AccessValidator,
/// and RoslynEmitter.
/// </summary>
internal static class AccessLevelConventions
{
    /// <summary>
    /// Determines access level from Python underscore naming convention.
    /// __name__ (dunder) → Public, __name → Private, _name → Protected, name → Public.
    ///
    /// <para>A backtick-escaped name is a LITERAL and carries no convention: <c>`_m`</c> is a public
    /// member spelled <c>_m</c>, on every host (#2033, R-CA — how an interface member keeps an
    /// underscore spelling, since interface members are public in .NET). The flag is required so no
    /// caller can apply the convention to an escaped name by forgetting it.</para>
    /// </summary>
    public static AccessLevel FromName(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
            return AccessLevel.Public;

        // Python naming conventions:
        // __name__ (dunder methods) = public (special methods)
        // __name (but not __name__) = private (name mangling)
        // _name = protected
        // name = public
        if (name.StartsWith("__") && name.EndsWith("__"))
            return AccessLevel.Public;
        if (name.StartsWith("__") && !name.EndsWith("__"))
            return AccessLevel.Private;
        if (name.StartsWith("_"))
            return AccessLevel.Protected;
        return AccessLevel.Public;
    }
}
