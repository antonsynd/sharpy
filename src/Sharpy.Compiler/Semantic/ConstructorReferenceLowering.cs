namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Which C# shape a constructor reference emits as once a signature is pinned (#1182, #1211).
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
    /// <c>list</c>, <c>dict</c>, <c>set</c>, <c>tuple</c> — a generic collection type with no
    /// conversion overload set. Emits as a constructor lambda closed over the pinned signature's
    /// types. (<c>tuple</c> is recognized but can never pin: its return type is a TupleType, not the
    /// GenericType the collection pin requires, so it always falls to the alias or SPY0342.)
    /// </summary>
    Collection,

    /// <summary>
    /// A user-declared class or struct (#1211). Pins against the class's DECLARED constructor
    /// overloads rather than a builtin overload set or a known collection shape — three families
    /// because there are three pinning rules. Emission coincides with
    /// <see cref="Collection"/> (a constructor lambda at a reference, <c>new T(args)</c> at an alias
    /// call), which is why the emitter arms are shared and the classification is not.
    /// </summary>
    UserType,
}

/// <summary>
/// How code generation must emit a constructor reference (#1182, #1211). Node-keyed in
/// <see cref="SemanticInfo"/> and merged by <c>SemanticInfo.MergeFrom</c>.
///
/// <para>Recorded against two kinds of node, for the two ways a constructor reference reaches
/// codegen. On a REFERENCE node it describes a reference pinned to an expected function type: the
/// conversion family emits the <c>Builtins.X</c> method group, the collection and user-type families
/// a constructor lambda of <see cref="ParameterCount"/> parameters. On a CALL node it describes a
/// call through a constructor ALIAS, which semantic analysis resolved exactly as a call of the type
/// itself: the conversion family emits <c>Builtins.X(args)</c>, the collection and user-type
/// families <c>new ConstructedType(args)</c>. One record because it is one decision — which type, in
/// which shape — and one dictionary so nothing node-keyed can miss the per-file merge.</para>
///
/// <para>Three families, two emitter shapes: <c>UserType</c> is classified apart from
/// <c>Collection</c> because the PINNING rules differ (declared constructors versus a known generic
/// shape), while the emission coincides. Do not collapse them on the strength of the emitter.</para>
///
/// <para>The emitter switches on the recorded values and never inspects the type or re-derives
/// which shape applies (Critical Rule 2, pattern (b)). A reference with no recorded lowering never
/// reaches code generation: it was rejected (SPY0342), or it is an alias BINDING, which emits
/// nothing at all.</para>
/// </summary>
/// <param name="Family">Conversion (method group / direct call), or Collection or UserType
/// (constructor lambda / object creation — the two share the emitter arms).</param>
/// <param name="Name">The type name as written, e.g. <c>int</c>, <c>dict</c>, or a user class.</param>
/// <param name="ConstructedType">What the reference constructs: the pinned signature's return type
/// at a reference, the call's result type at an alias call. Read by the collection and user-type
/// families; the conversion family emits a method group and needs none of it.</param>
/// <param name="ParameterCount">How many parameters the emitted form takes — the pinned signature's
/// arity at a reference, the argument count at an alias call.
/// <para>Read only on REFERENCE lowerings, by <c>RoslynEmitter.GenerateConstructorReference</c>,
/// which generates that many lambda parameters. It is inert on CALL lowerings:
/// <c>GenerateConstructorReferenceCall</c> reads Family, Name and ConstructedType only, because the
/// arguments come from the call node itself. Recorded on both for one record shape; do not delete it
/// on the strength of the call side alone (#1221).</para></param>
public sealed record ConstructorReferenceLowering(
    ConstructorReferenceFamily Family,
    string Name,
    SemanticType ConstructedType,
    int ParameterCount);
