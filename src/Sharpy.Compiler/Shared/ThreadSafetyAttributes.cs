using System;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Documents that a class is designed to be thread-safe.
/// This is a documentation attribute only — it does not enforce thread safety at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
internal sealed class ThreadSafeAttribute : Attribute
{
}

/// <summary>
/// Documents that a class is NOT thread-safe and should not be shared across threads.
/// This is a documentation attribute only — it does not enforce anything at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
internal sealed class NotThreadSafeAttribute : Attribute
{
    /// <summary>
    /// Explains why the class is not thread-safe or describes the expected usage pattern.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
