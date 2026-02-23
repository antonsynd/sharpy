using System.Runtime.CompilerServices;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Services;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Maps AST nodes to their semantic information.
/// Provides a way to annotate the AST without modifying it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Threading:</b> This type is not thread-safe. It uses <see cref="Dictionary{TKey,TValue}"/>
/// internally, so concurrent reads and writes will cause data corruption. Each compilation
/// creates its own instance. In <see cref="Project.ProjectCompiler"/>, a single shared instance
/// is used because files are processed sequentially in dependency order.
/// </para>
/// <para>
/// For parallel per-file analysis (e.g., an LSP server), create one <see cref="SemanticInfo"/>
/// per file. The shared <see cref="SemanticBinding"/> and <see cref="SymbolTable"/> are
/// thread-safe and can be accessed concurrently.
/// </para>
/// </remarks>
public class SemanticInfo : ISemanticQuery
{
    // Use ReferenceEqualityComparer because AST nodes are records with value-based equality,
    // but we need to distinguish between different instances (e.g., two super().__init__() calls
    // in different files should be cached separately even if they have the same structure)

    // Map expressions to their resolved types
    private readonly Dictionary<Expression, SemanticType> _expressionTypes =
        new(ReferenceEqualityComparer.Instance);

    // Map identifiers to their symbols
    private readonly Dictionary<Identifier, Symbol> _identifierSymbols =
        new(ReferenceEqualityComparer.Instance);

    // Map function calls to resolved function symbols
    private readonly Dictionary<FunctionCall, FunctionSymbol> _callTargets =
        new(ReferenceEqualityComparer.Instance);

    // Map type annotations to resolved semantic types
    private readonly Dictionary<TypeAnnotation, SemanticType> _typeAnnotations =
        new(ReferenceEqualityComparer.Instance);

    // Map expressions to their narrowed types (for type narrowing after is not None / isinstance checks)
    // This captures the narrowed type at each specific usage of an identifier within a narrowing context
    private readonly Dictionary<Expression, SemanticType> _narrowedExpressionTypes =
        new(ReferenceEqualityComparer.Instance);

    // Map generic function calls to their inferred type arguments
    // Used by codegen to emit explicit type arguments in generated C#
    private readonly Dictionary<FunctionCall, List<SemanticType>> _inferredTypeArguments =
        new(ReferenceEqualityComparer.Instance);

    // Track functions that contain yield statements (generators)
    private readonly HashSet<FunctionDef> _generatorFunctions = new(ReferenceEqualityComparer.Instance);

    // Track expressions whose type was set to UnknownType due to a user error
    // (i.e., a diagnostic was already emitted for the node). This distinguishes
    // expected error-recovery Unknown types from unexpected ones (compiler bugs).
    private readonly HashSet<Expression> _errorRecoveryNodes =
        new(ReferenceEqualityComparer.Instance);

    public void SetExpressionType(Expression expr, SemanticType type)
    {
        _expressionTypes[expr] = type;
    }

    public SemanticType? GetExpressionType(Expression expr)
    {
        return _expressionTypes.TryGetValue(expr, out var type) ? type : null;
    }

    public void SetIdentifierSymbol(Identifier id, Symbol symbol)
    {
        _identifierSymbols[id] = symbol;
    }

    public Symbol? GetIdentifierSymbol(Identifier id)
    {
        return _identifierSymbols.TryGetValue(id, out var symbol) ? symbol : null;
    }

    public void SetCallTarget(FunctionCall call, FunctionSymbol target)
    {
        _callTargets[call] = target;
    }

    public FunctionSymbol? GetCallTarget(FunctionCall call)
    {
        return _callTargets.TryGetValue(call, out var target) ? target : null;
    }

    public void SetTypeAnnotation(TypeAnnotation annotation, SemanticType type)
    {
        _typeAnnotations[annotation] = type;
    }

    public SemanticType? GetTypeAnnotation(TypeAnnotation annotation)
    {
        return _typeAnnotations.TryGetValue(annotation, out var type) ? type : null;
    }

    /// <summary>
    /// Sets a narrowed type for an expression (typically an Identifier) within a narrowing context.
    /// Used for type narrowing after `is not None` or `isinstance()` checks.
    /// </summary>
    public void SetNarrowedType(Expression expr, SemanticType narrowedType)
    {
        _narrowedExpressionTypes[expr] = narrowedType;
    }

    /// <summary>
    /// Gets the narrowed type for an expression, if one was recorded.
    /// Returns null if the expression wasn't in a narrowing context.
    /// </summary>
    public SemanticType? GetNarrowedType(Expression expr)
    {
        return _narrowedExpressionTypes.TryGetValue(expr, out var type) ? type : null;
    }

    /// <summary>
    /// Gets the effective type of an expression, considering type narrowing.
    /// Returns the narrowed type if one was recorded, otherwise returns the expression type.
    /// This is the primary method for LSP hover and other tooling that needs the "best known" type.
    /// </summary>
    /// <param name="expr">The expression to get the type for.</param>
    /// <returns>The narrowed type if available, otherwise the expression type, or null if unknown.</returns>
    public SemanticType? GetEffectiveType(Expression expr)
    {
        return GetNarrowedType(expr) ?? GetExpressionType(expr);
    }

    /// <summary>
    /// Sets the inferred type arguments for a generic function call.
    /// Used when calling a generic function without explicit type arguments (e.g., identity(42) -> T=int).
    /// </summary>
    public void SetInferredTypeArguments(FunctionCall call, List<SemanticType> typeArguments)
    {
        _inferredTypeArguments[call] = typeArguments;
    }

    /// <summary>
    /// Gets the inferred type arguments for a generic function call.
    /// Returns null if no type arguments were inferred (explicit call or non-generic function).
    /// </summary>
    public List<SemanticType>? GetInferredTypeArguments(FunctionCall call)
    {
        return _inferredTypeArguments.TryGetValue(call, out var types) ? types : null;
    }

    /// <summary>
    /// Marks an expression as having UnknownType due to error recovery.
    /// Call this when the type is set to UnknownType because a user-facing diagnostic
    /// was already emitted. This allows the invariant checker to distinguish expected
    /// Unknown types (error recovery) from unexpected ones (compiler bugs).
    /// </summary>
    public void MarkErrorRecovery(Expression expr)
    {
        _errorRecoveryNodes.Add(expr);
    }

    /// <summary>
    /// Returns true if the given expression was marked as error recovery,
    /// meaning its UnknownType is expected (a diagnostic was emitted).
    /// </summary>
    public bool IsErrorRecoveryType(Expression expr)
    {
        return _errorRecoveryNodes.Contains(expr);
    }

    /// <summary>
    /// Marks a function as a generator (contains yield statements).
    /// </summary>
    public void MarkAsGenerator(FunctionDef funcDef) => _generatorFunctions.Add(funcDef);

    /// <summary>
    /// Returns true if the function has been marked as a generator.
    /// </summary>
    public bool IsGenerator(FunctionDef funcDef) => _generatorFunctions.Contains(funcDef);

    /// <summary>
    /// Returns true if any expression type in the semantic info is UnknownType.
    /// Used by tests to verify the invariant: if no semantic errors, no types should be unknown.
    /// </summary>
    public bool HasUnknownExpressionTypes()
    {
        return _expressionTypes.Values.Any(t => t is UnknownType);
    }

    /// <summary>
    /// Returns expressions that have UnknownType but are NOT in the error recovery set.
    /// These represent potential compiler bugs where type inference failed silently.
    /// </summary>
    public IReadOnlyList<Expression> GetUnexpectedUnknownExpressions()
    {
        return _expressionTypes
            .Where(kvp => kvp.Value is UnknownType && !_errorRecoveryNodes.Contains(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Returns the total number of expression types recorded.
    /// Used for consistency assertions and diagnostics.
    /// </summary>
    public int ExpressionTypeCount => _expressionTypes.Count;

    /// <summary>
    /// Returns the total number of identifier-to-symbol mappings.
    /// </summary>
    public int IdentifierSymbolCount => _identifierSymbols.Count;
}
