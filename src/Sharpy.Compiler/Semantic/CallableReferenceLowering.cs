namespace Sharpy.Compiler.Semantic;

/// <summary>
/// How code generation must emit a builtin or overloaded function name used as a value (#1638).
/// Node-keyed in <see cref="SemanticInfo"/> and merged by <c>SemanticInfo.MergeFrom</c>.
///
/// <para>A bare method group (<c>Sharpy.Builtins.Len</c>) breaks in four ways: boxing a struct
/// through an interface (Bytes), generic inference off a struct, CS0121 ambiguity (Reversed),
/// and optional/params elision. An eta-expanded lambda
/// <c>(T1 _p0, T2 _p1) =&gt; Sharpy.Builtins.X(_p0, _p1)</c> is accepted everywhere a method
/// group was, so the switch is regression-free by construction.</para>
///
/// <para>The emitter switches on the recorded values and never inspects the builtin or
/// re-derives which overload applies (Critical Rule 2, pattern (b)).</para>
/// </summary>
/// <param name="QualifiedName">The fully qualified C# callee name, e.g.
/// <c>"Sharpy.Builtins.Len"</c>.</param>
/// <param name="ParameterTypes">The selected overload's parameter types so the emitter can
/// generate a typed lambda.</param>
/// <param name="ReturnType">The selected overload's return type.</param>
public sealed record CallableReferenceLowering(
    string QualifiedName,
    IReadOnlyList<SemanticType> ParameterTypes,
    SemanticType ReturnType);
