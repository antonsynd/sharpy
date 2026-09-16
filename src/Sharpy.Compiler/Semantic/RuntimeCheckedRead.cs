using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// A read of a bare local that definite-assignment classified as runtime-checked (#1839, R-AI): the
/// local is assigned when a suppression-capable <c>with</c> completes normally, but a body exception
/// its <c>__exit__</c> swallows can leave it unset. The emitter lowers such a read to
/// <c>Builtins.CheckedLocal(flag, value, "name")</c>, so an unset read raises <c>UnboundLocalError</c>
/// at runtime (matching Python) instead of being refused (SPY0600). Recorded by
/// <see cref="Validation.DefiniteAssignmentValidator"/> and read by the emitter (Rule 2, node-keyed).
/// </summary>
/// <param name="Declaration">The bare declaration the read resolves to.</param>
public sealed record RuntimeCheckedRead(VariableDeclaration Declaration);
