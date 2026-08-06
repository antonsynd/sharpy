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
    /// GenericType the collection pin requires, so it always falls to SPY0342.)
    /// </summary>
    Collection,

    /// <summary>
    /// A user-declared class or struct (#1211). Pins against the class's DECLARED constructor
    /// overloads rather than a builtin overload set or a known collection shape — three families
    /// because there are three pinning rules. Emission coincides with <see cref="Collection"/> (a
    /// constructor lambda), which is why the emitter arm is shared and the classification is not.
    /// </summary>
    UserType,
}

/// <summary>
/// How code generation must emit a constructor reference (#1182, #1211). Node-keyed in
/// <see cref="SemanticInfo"/> and merged by <c>SemanticInfo.MergeFrom</c>.
///
/// <para>Recorded against a REFERENCE node only. It describes a reference pinned to an expected
/// function type: the conversion family emits the <c>Builtins.X</c> method group, the collection and
/// user-type families a constructor lambda of <see cref="ParameterCount"/> parameters.</para>
///
/// <para>Until #1248 it was also recorded against a CALL node, describing a call through a
/// constructor ALIAS that semantic analysis had resolved as a call of the type itself. That tier is
/// retired, so nothing writes a call-keyed lowering and the emitter arm that read one is gone. Both
/// remaining writers key on the reference; if a call-keyed lowering is ever reintroduced, the
/// emitter needs its arm back.</para>
///
/// <para>Three families, two emitter shapes: <c>UserType</c> is classified apart from
/// <c>Collection</c> because the PINNING rules differ (declared constructors versus a known generic
/// shape), while the emission coincides. Do not collapse them on the strength of the emitter.</para>
///
/// <para>The emitter switches on the recorded values and never inspects the type or re-derives
/// which shape applies (Critical Rule 2, pattern (b)). A reference with no recorded lowering never
/// reaches code generation: it was refused in semantic analysis (SPY0342, or SPY0346 for a type with
/// no construction at all).</para>
/// </summary>
/// <param name="Family">Conversion (method group), or Collection or UserType (constructor lambda —
/// the two share the emitter arm).</param>
/// <param name="Name">The type name as written, e.g. <c>int</c>, <c>dict</c>, or a user class.</param>
/// <param name="ConstructedType">What the reference constructs: the pinned signature's return type.
/// Read by the collection and user-type families; the conversion family emits a method group and
/// needs none of it.</param>
/// <param name="ParameterCount">How many parameters the emitted lambda takes — the pinned
/// signature's arity. Read by <c>RoslynEmitter.GenerateConstructorReference</c>.</param>
public sealed record ConstructorReferenceLowering(
    ConstructorReferenceFamily Family,
    string Name,
    SemanticType ConstructedType,
    int ParameterCount);
