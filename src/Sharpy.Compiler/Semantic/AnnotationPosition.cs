namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Where a type annotation sits, which decides what <c>None</c> may mean there (#2004, Decision 16).
/// Only a return annotation — a function's, a method's, a delegate's, a lambda's, a property
/// getter's, or the return slot of a function type <c>(…) -&gt; None</c> — spells "no value" (void).
/// Everywhere else — a local, parameter, field, constant, type argument, cast or type test —
/// <c>None</c> is not a type and is refused with SPY0614. Every
/// <see cref="TypeResolver.ResolveTypeAnnotation"/> caller states one; there is no default, so the
/// C# build is the totality check over the callers (ruling 14).
/// </summary>
public enum AnnotationPosition
{
    /// <summary>A value slot: <c>None</c> is SPY0614; spell it <c>T | None</c> or <c>object</c>.</summary>
    Value,

    /// <summary>A return slot: <c>None</c> is void.</summary>
    Return,
}
