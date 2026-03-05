namespace Sharpy.Compiler.Shared;

/// <summary>
/// Single source of truth for protocol interface property and method names used in code generation.
/// These constants match the Sharpy.Core protocol interfaces (ISized, IBoolConvertible, etc.)
/// and runtime wrapper types (Optional.Unwrap, etc.).
/// </summary>
internal static class ProtocolConstants
{
    /// <summary>IBoolConvertible.IsTrue property name.</summary>
    internal const string IsTrue = "IsTrue";

    /// <summary>ISized.Count property name.</summary>
    internal const string Count = "Count";

    /// <summary>Optional/Result .Unwrap() method name.</summary>
    internal const string Unwrap = "Unwrap";

    /// <summary>Context manager sync enter method (C# name for __enter__).</summary>
    internal const string Enter = "Enter";

    /// <summary>Context manager sync exit method (C# name for __exit__).</summary>
    internal const string Exit = "Exit";

    /// <summary>Context manager async enter method (C# name for __aenter__).</summary>
    internal const string AenterAsync = "AenterAsync";

    /// <summary>Context manager async exit method (C# name for __aexit__).</summary>
    internal const string AexitAsync = "AexitAsync";
}
