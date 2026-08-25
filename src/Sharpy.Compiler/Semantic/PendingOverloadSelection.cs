using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Captures a deferred overload selection for a callable reference whose arity-divergent overloads
/// cannot be resolved because the expected type still contains unsolved type parameters (#1589).
/// After generic inference binds those parameters, the selection is re-entered with the now-concrete
/// expected type.
/// </summary>
/// <param name="ConstructorFamily">Non-null when the reference is a constructor reference (a builtin
/// type name like <c>bytes</c>) that needs a <see cref="ConstructorReferenceLowering"/> recorded at
/// resolution time. Populated at the deferral site from the constructor-reference classification.</param>
/// <param name="ConstructorName">The type name for the lowering, when <paramref name="ConstructorFamily"/>
/// is non-null.</param>
internal sealed record PendingOverloadSelection(
    Expression Reference,
    List<(FunctionSymbol Symbol, FunctionType Signature)> Candidates,
    FunctionType ExpectedType,
    int ArgumentIndex,
    ConstructorReferenceFamily? ConstructorFamily = null,
    string? ConstructorName = null);
