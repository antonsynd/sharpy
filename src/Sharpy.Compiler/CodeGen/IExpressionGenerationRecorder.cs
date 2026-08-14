using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Observes every expression the emitter generates, so a test can assert that no AST node is
/// generated twice within one statement scope (#1334).
///
/// <para>
/// <b>Why a general tripwire.</b> Double generation is a defect class, not a bug: the emitter
/// re-reaching a sub-expression produces C# that is <em>correct in value</em> and wrong in effect
/// count. Nothing in the output looks wrong — the snapshot matches, the fixture prints the right
/// number — unless the sub-expression happens to have a side effect, and only then, and only if a
/// test exercises it. Each instance so far (augmented assignment on an indexer, then
/// <c>abc5bf4b0</c>'s property-getter read) was found by someone writing a test for that one shape.
/// This makes the property structural instead: generation is counted at
/// <c>GenerateExpression</c>, the single wrapper every expression passes through, and any node
/// counted twice in a scope fails whether or not anyone thought to test it.
/// </para>
///
/// <para>
/// <b>Cost when absent.</b> The emitter holds a null reference and the hook is one null check per
/// expression. Nothing is allocated and no state is kept: production emit is unchanged, and the
/// recorder is installed only by a test-side <see cref="ICodeEmitterFactory"/> — no static mutable
/// state, no compilation flag, nothing to leave switched on by accident.
/// </para>
/// </summary>
internal interface IExpressionGenerationRecorder
{
    /// <summary>Called once per <c>GenerateExpression</c> call, before the expression is built.</summary>
    void OnGenerate(Expression expression);

    /// <summary>
    /// Called when <c>HoistAugmentedTargetOperand</c> hands an already-generated operand back
    /// UNHOISTED, so its single generation is spliced into two syntax positions — the read side of
    /// the augmented value and the write side of the assignment. One call means two evaluations.
    ///
    /// <para>
    /// <b>Why generation counting cannot see this (#1351).</b> The hoist helper takes syntax that
    /// <c>GenerateExpression</c> already produced exactly once, so <see cref="OnGenerate"/> counts
    /// one. The duplication happens at the splice, downstream of every counter at the generation
    /// choke point — which is why reintroducing <c>abc5bf4b0</c> left the re-generation sweep
    /// green. Both historical single-evaluation bugs were re-splicing, so this is the half of the
    /// defect class that has actually produced bugs.
    /// </para>
    ///
    /// <para>
    /// Declining is correct when evaluating the operand twice is unobservable; the emitter's
    /// <c>IsRepeatableTargetOperand</c> decides that, and this hook exists so a test can check the
    /// decision instead of trusting it.
    /// </para>
    /// </summary>
    /// <param name="handle">The operand's AST node — the identity spliced twice.</param>
    void OnSplice(Expression handle);

    /// <summary>
    /// Opens a statement scope. Disposing restores the enclosing one, so nested statements nest
    /// their scopes. A statement is the unit the property is stated over: generating the same node
    /// for two different statements is ordinary (a default-argument expression reused across
    /// constructors); generating it twice for one statement is the defect.
    /// </summary>
    IDisposable BeginStatementScope();
}
