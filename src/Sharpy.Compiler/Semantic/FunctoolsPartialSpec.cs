namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The materialized spec for a <c>functools.partial(f, ...)</c> call, recorded node-keyed by the
/// TypeChecker (<c>CheckFunctoolsPartialCall</c> — it already resolves everything) so the emitter
/// reads every resolved fact instead of re-deriving the target, the remaining-parameter subset, or
/// the kwarg C# names at emit time (#1520; CLAUDE.md Rule 2 pattern (b) — the fact belongs to the
/// call node). The emitter's only jobs are spelling (lambda-parameter mangling) and syntax
/// assembly; the side-effect hoisting of fixed arguments (<c>CaptureFixedArg</c>) is genuinely
/// syntactic and stays emitter-side.
/// </summary>
/// <param name="TargetSymbol">
/// The resolved target function, or null when the target is only known as a
/// <see cref="FunctionType"/> (lambda-typed variable, delegate) — keyword fixing is refused for
/// those at check time, so a null target implies <see cref="FixedKeywords"/> is empty.
/// </param>
/// <param name="FixedPositionalCount">How many leading positional parameters the call fixes.</param>
/// <param name="RemainingParameters">
/// The parameters the produced callable still takes, in declaration order: the target's parameters
/// after the fixed positionals, minus the keyword-fixed ones. <c>Name</c> is the Sharpy spelling
/// (the emitter mangles it for the lambda parameter); <c>CSharpName</c> is the resolved C#
/// parameter name on the target (verbatim for CLR-backed targets, #942), used to bind remaining
/// arguments BY NAME when keywords are fixed — positional binding would walk them into the
/// keyword-fixed parameter's slot (CS1744). <c>Type</c> matches the result
/// <see cref="FunctionType"/>'s parameter vector positionally.
/// </param>
/// <param name="FixedKeywords">
/// The keyword-fixed arguments: <c>CSharpName</c> is the resolved C# parameter name and
/// <c>ArgumentIndex</c> indexes the call node's <c>KeywordArguments</c>.
/// </param>
public sealed record FunctoolsPartialSpec(
    FunctionSymbol? TargetSymbol,
    int FixedPositionalCount,
    System.Collections.Generic.IReadOnlyList<(string Name, string CSharpName, SemanticType Type)> RemainingParameters,
    System.Collections.Generic.IReadOnlyList<(string CSharpName, int ArgumentIndex)> FixedKeywords);
