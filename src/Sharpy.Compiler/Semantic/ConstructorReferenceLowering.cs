namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Which C# shape a builtin constructor reference emits as once a signature is pinned (#1182).
/// </summary>
public enum ConstructorReferenceFamily
{
    /// <summary>
    /// <c>int</c>, <c>str</c>, <c>float</c>, <c>bool</c>, … — a name backed by a
    /// <c>Sharpy.Builtins.X</c> overload set. Emits as that method group, letting C#'s own
    /// method-group conversion bind the overload against the pinned delegate type.
    /// </summary>
    Conversion,

    /// <summary>
    /// <c>list</c>, <c>dict</c>, <c>set</c> — a generic collection type with no conversion overload
    /// set. Emits as a constructor lambda closed over the pinned signature's types.
    /// </summary>
    Collection,
}

/// <summary>
/// How code generation must emit a builtin constructor reference that semantic analysis pinned to a
/// concrete signature (#1182).
///
/// <para>Node-keyed in <see cref="SemanticInfo"/> and merged by <c>SemanticInfo.MergeFrom</c>. The
/// emitter switches on <see cref="Family"/> and reads <see cref="Signature"/>; it never inspects the
/// builtin or re-derives which shape applies (Critical Rule 2, pattern (b)). A reference with no
/// recorded lowering never reaches code generation — it was either rejected (SPY0342) or resolved
/// per call site as an alias, which emits the direct builtin call instead.</para>
/// </summary>
/// <param name="Family">Conversion (method group) or Collection (constructor lambda).</param>
/// <param name="Name">The builtin type name as written, e.g. <c>int</c> or <c>dict</c>.</param>
/// <param name="Signature">The signature the reference was pinned to: the parameter and return
/// types the emitted delegate must have.</param>
public sealed record ConstructorReferenceLowering(
    ConstructorReferenceFamily Family,
    string Name,
    FunctionType Signature);
