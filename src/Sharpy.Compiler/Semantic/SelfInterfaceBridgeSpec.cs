namespace Sharpy.Compiler.Semantic;

/// <summary>
/// A fully-resolved specification for one explicit-interface bridge a class must emit so that a
/// <c>Self</c>-annotated interface member binds (#1342). Computed during semantic analysis from
/// <see cref="GenericInstantiationWalker.EnumerateImplementedInterfaces"/> and materialized
/// symbol-keyed on the class's <see cref="CodeGenInfo"/> at <c>MaterializeCodeGenInfo</c>
/// (CLAUDE.md Rule 2 pattern (a) — the class symbol owns the fact). Code generation reads this
/// verbatim: every TYPE decision (which interface arguments, where <c>Self</c> resolves, whether a
/// forwarding argument needs a downcast) is already made here, leaving the emitter only to map
/// types, mangle the name, and assemble syntax.
/// </summary>
/// <remarks>
/// <para>
/// The bridge C# is <c>ReturnType InterfaceType.MethodName(params...) =&gt; this.MethodName(args...);</c>
/// where the interface's own <c>Self</c> resolves to <see cref="InterfaceType"/> (the interface at
/// its composed base-clause arguments) and each forwarding argument is downcast to the implementing
/// parameter's type where the two differ.
/// </para>
/// </remarks>
public sealed record SelfInterfaceBridgeSpec
{
    /// <summary>
    /// The interface at its composed base-clause arguments (e.g. <c>IBuilder[int]</c> for
    /// <c>class Box(IBuilder[int])</c>). Both the explicit-interface specifier and the type a
    /// top-level <c>Self</c> resolves to in this bridge's signature.
    /// </summary>
    public required SemanticType InterfaceType { get; init; }

    /// <summary>The interface member's Sharpy name; the emitter applies dunder/name mangling.</summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// The bridge's return type — the interface member's return with top-level <c>Self</c> resolved
    /// to <see cref="InterfaceType"/> and the interface's own type parameters substituted at
    /// <see cref="InterfaceType"/>'s arguments. Null (or a void/unknown type) emits <c>void</c>.
    /// </summary>
    public SemanticType? ReturnType { get; init; }

    /// <summary>
    /// The type the forwarded call's result must be explicitly cast to, or null when the result
    /// converts implicitly. Needed when the implementing member is INHERITED and returns <c>Self</c>
    /// (shape 3): the base's C# method returns the base type, which the interface is not assignable
    /// from — the actual runtime object is the derived type, so an explicit cast to
    /// <see cref="InterfaceType"/> is sound.
    /// </summary>
    public SemanticType? ReturnCast { get; init; }

    /// <summary>
    /// The bridge parameters (self excluded) in DECLARATION order, each carrying the interface
    /// signature's type (top-level <c>Self</c> resolved, type parameters substituted). The emitter
    /// reorders these for C# and renders them like any parameter list.
    /// </summary>
    public required IReadOnlyList<ParameterSymbol> Parameters { get; init; }

    /// <summary>
    /// The downcast target for a forwarding argument, keyed by the parameter's Sharpy name — present
    /// only where the implementing parameter's type is more specific than the bridge's (a
    /// <c>Self</c> parameter: the bridge takes the interface, the implementation the class). Absent
    /// keys forward without a cast.
    /// </summary>
    public required IReadOnlyDictionary<string, SemanticType> ParameterCasts { get; init; }
}
