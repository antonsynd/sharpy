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
    /// </summary>
    public static AccessLevel FromName(string name)
    {
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
