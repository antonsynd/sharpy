using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The loop a <see cref="BreakStatement"/> or <see cref="ContinueStatement"/> binds to, recorded by
/// <see cref="Validation.LoopTransferBindingValidator"/> and read by the emitter (#1816).
/// </summary>
/// <remarks>
/// Python binds a <c>break</c>/<c>continue</c> to the innermost enclosing loop regardless of which
/// statements the emitter lowers between the transfer and that loop. The emitter cannot re-derive
/// this because a C# <c>switch</c> (manufactured for a <c>match</c>) captures a bare <c>break</c>,
/// and the loop-<c>else</c> flag must be cleared for the correct loop. So the binding is recorded
/// here (Rule 2): <see cref="TargetLoop"/> is the <see cref="ForStatement"/> or
/// <see cref="WhileStatement"/> node the transfer targets; <see cref="CrossesMatch"/> is true when a
/// <c>match</c> statement sits between the transfer and that loop (the match must lower to the
/// <c>is</c>-chain so a plain C# <c>break</c> escapes to the loop rather than the manufactured
/// <c>switch</c>).
/// </remarks>
public sealed record LoopTransferTarget(Statement TargetLoop, bool CrossesMatch);
