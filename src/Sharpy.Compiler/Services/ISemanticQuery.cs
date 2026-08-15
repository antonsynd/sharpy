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

    CalleeRouting? GetCalleeRouting(FunctionCall call);

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
    /// Finds a symbol by matching its name and declaration position, for a cursor on a declaration
    /// node rather than an Identifier reference.
    /// <para>
    /// <b>Prefer a node-keyed lookup where one exists</b> — <see cref="GetDeclarationSymbol"/>,
    /// <see cref="GetFunctionDeclarationSymbol"/>, <see cref="GetExceptHandlerSymbol"/>,
    /// <see cref="GetWithItemSymbol"/>. This is a name-and-position scan over two
    /// reference-populated collections plus module scope, so it is blind to any function-local
    /// binding that nothing reads (#1222, #1232).
    /// </para>
    /// <para>
    /// Known residual holes, all measured (#1232). A declaration whose kind has no node-keyed map
    /// is still resolved here, and for these the scan answers only when something references the
    /// name:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Class, struct, interface and enum declarations nested inside a function.</b> Not a
    /// reachable hole today: the checker reports SPY0202 for a function-local type declaration, so
    /// there is no symbol to find in the first place.
    /// </description></item>
    /// <item><description>
    /// <b>Match-statement capture patterns and comprehension targets</b> — bound in scopes this
    /// scan never sees. Unmeasured; rename does not route those node kinds here today.
    /// </description></item>
    /// </list>
    /// </summary>
    Symbol? FindSymbolByDeclaration(string name, int line, int column);

    /// <summary>
    /// Gets the symbol a variable declaration binds, keyed on the declaration node itself.
    /// Recorded where the checker binds the declaration, so it answers for a binding nothing
    /// references — which the name-and-position scan in <see cref="FindSymbolByDeclaration"/>
    /// cannot do for a function-local one.
    /// </summary>
    Symbol? GetDeclarationSymbol(Parser.Ast.VariableDeclaration declaration);

    /// <summary>
    /// Gets the symbol a function definition declares, keyed on the definition node. Recorded where
    /// the checker resolves it, so a nested <c>def</c> that nothing calls is reachable from its
    /// declaration — the scan cannot see one, since it is neither referenced nor in module scope.
    /// </summary>
    FunctionSymbol? GetFunctionDeclarationSymbol(Parser.Ast.FunctionDef definition);

    /// <summary>
    /// Gets the variable symbol an except handler's <c>as</c> clause binds, keyed on the handler
    /// node. Returns null when the handler has no <c>as</c> clause.
    /// </summary>
    VariableSymbol? GetExceptHandlerSymbol(Parser.Ast.ExceptHandler handler);

    /// <summary>
    /// Gets the variable symbol a parameter binds, keyed on the parameter node. Recorded where the
    /// checker defines it, so a parameter the body never reads is still reachable from its
    /// declaration. <c>Parameter</c> is not a <c>Node</c>, so a cursor on one resolves to the
    /// enclosing <c>FunctionDef</c> — the caller scans that definition's parameters for a
    /// name-extent hit and asks here (#1359).
    /// </summary>
    VariableSymbol? GetParameterSymbol(Parser.Ast.Parameter parameter);

    /// <summary>
    /// Returns true if the given statement was produced by a source generator
    /// (i.e., it was emitted via <c>SemanticInfo.MarkAsGenerated</c>).
    /// Used by LSP for hover attribution and semantic token modifiers.
    /// </summary>
    bool IsGenerated(Parser.Ast.Statement statement);

    /// <summary>
    /// Returns the name of the source generator that produced the given statement,
    /// or null if the statement was not produced by a generator.
    /// </summary>
    string? GetGeneratorName(Parser.Ast.Statement statement);

    /// <summary>
    /// Returns the recorded source-generator bindings (bracket attributes resolving to
    /// <c>SourceGenerator</c> subclasses) attached to the given declaration. Returns an
    /// empty list when none are recorded.
    /// </summary>
    IReadOnlyList<GeneratorBinding> GetGeneratorBindings(Parser.Ast.Statement declaration);

    /// <summary>
    /// Enumerates every recorded (declaration, bindings) pair tracked by the semantic
    /// model. Used by LSP for diagnostic routing and statistics.
    /// </summary>
    IEnumerable<(Parser.Ast.Statement Declaration, IReadOnlyList<GeneratorBinding> Bindings)> GetAllGeneratorBindings();
}
