using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Read-only query interface for semantic information.
/// Provides type lookups and symbol resolution for LSP/tooling consumers
/// without exposing the mutation methods on <see cref="SemanticInfo"/>.
/// </summary>
public interface ISemanticQuery
{
    /// <summary>
    /// Gets the resolved type of an expression.
    /// </summary>
    SemanticType? GetExpressionType(Expression expr);

    /// <summary>
    /// Gets the narrowed type of an expression within a type narrowing context
    /// (e.g., after <c>is not None</c> checks).
    /// </summary>
    SemanticType? GetNarrowedType(Expression expr);

    /// <summary>
    /// Gets the effective type: narrowed type if available, otherwise the expression type.
    /// This is the primary method for LSP hover and tooling.
    /// </summary>
    SemanticType? GetEffectiveType(Expression expr);

    /// <summary>
    /// Gets the symbol that an identifier resolves to.
    /// </summary>
    Symbol? GetIdentifierSymbol(Identifier id);

    /// <summary>
    /// Gets the resolved function target of a function call.
    /// </summary>
    FunctionSymbol? GetCallTarget(FunctionCall call);

    /// <summary>
    /// Gets the resolved semantic type of a type annotation.
    /// </summary>
    SemanticType? GetTypeAnnotation(TypeAnnotation annotation);

    /// <summary>
    /// Gets the inferred type arguments for a generic function call.
    /// Returns null if no type arguments were inferred.
    /// </summary>
    List<SemanticType>? GetInferredTypeArguments(FunctionCall call);

    /// <summary>
    /// Gets the resolved symbol for a member access expression (e.g., ClassName.FIELD, self.field).
    /// Returns null if the TypeChecker did not record a resolution for this node.
    /// </summary>
    (TypeSymbol Owner, Symbol Member)? GetMemberAccessResolution(Parser.Ast.MemberAccess memberAccess);

    /// <summary>
    /// Gets all recorded reference locations for a symbol.
    /// Returns an empty list if no references have been recorded.
    /// </summary>
    IReadOnlyList<SymbolReference> GetReferences(Symbol symbol);

    /// <summary>
    /// Finds all recorded references for a symbol matched by name and declaring file path.
    /// Used for cross-file reference queries where the caller has a symbol instance from
    /// a different compilation (and thus reference equality won't match).
    /// </summary>
    IReadOnlyList<SymbolReference> FindReferencesBySymbolIdentity(string symbolName, string? declaringFilePath);

    /// <summary>
    /// Gets the variable symbol associated with a with-statement item's <c>as</c> variable.
    /// Returns null if no symbol was recorded (e.g., no <c>as</c> clause).
    /// </summary>
    VariableSymbol? GetWithItemSymbol(WithItem item);

    /// <summary>
    /// Finds a symbol by matching its name and declaration position.
    /// Used by rename/references handlers when the cursor is on a declaration node
    /// (e.g., VariableDeclaration, FunctionDef) rather than an Identifier reference.
    /// </summary>
    Symbol? FindSymbolByDeclaration(string name, int line, int column);
}
