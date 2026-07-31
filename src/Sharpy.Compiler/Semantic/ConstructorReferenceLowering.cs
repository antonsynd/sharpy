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
/// How code generation must emit a builtin constructor reference (#1182). Node-keyed in
/// <see cref="SemanticInfo"/> and merged by <c>SemanticInfo.MergeFrom</c>.
///
/// <para>Recorded against two kinds of node, for the two ways a constructor reference reaches
/// codegen. On a REFERENCE node it describes a reference pinned to an expected function type: the
/// conversion families emit the <c>Builtins.X</c> method group, the collection families a
/// constructor lambda of <see cref="ParameterCount"/> parameters. On a CALL node it describes a call
/// through a constructor ALIAS, which semantic analysis resolved exactly as a call of the builtin:
/// the conversion families emit <c>Builtins.X(args)</c>, the collection families
/// <c>new ConstructedType(args)</c>. One record because it is one decision — which builtin, in which
/// shape — and one dictionary so nothing node-keyed can miss the per-file merge.</para>
///
/// <para>The emitter switches on the recorded values and never inspects the builtin or re-derives
/// which shape applies (Critical Rule 2, pattern (b)). A reference with no recorded lowering never
/// reaches code generation: it was rejected (SPY0342), or it is an alias BINDING, which emits
/// nothing at all.</para>
/// </summary>
/// <param name="Family">Conversion (method group / direct call) or Collection (constructor lambda /
/// object creation).</param>
/// <param name="Name">The builtin type name as written, e.g. <c>int</c> or <c>dict</c>.</param>
/// <param name="ConstructedType">What the reference constructs: the pinned signature's return type
/// at a reference, the call's result type at an alias call. Read only by the collection families.</param>
/// <param name="ParameterCount">How many parameters the emitted form takes — the pinned signature's
/// arity at a reference, the argument count at an alias call.</param>
public sealed record ConstructorReferenceLowering(
    ConstructorReferenceFamily Family,
    string Name,
    SemanticType ConstructedType,
    int ParameterCount);
