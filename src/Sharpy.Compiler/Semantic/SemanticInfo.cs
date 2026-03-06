using System.Collections.Concurrent;
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
/// <b>Threading:</b> Dictionary fields use <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// for thread safety. HashSet fields are not concurrent but are only accessed during
/// single-threaded analysis phases. Each compilation creates its own instance.
/// </para>
/// </remarks>
public class SemanticInfo : ISemanticQuery
{
    // Use ReferenceEqualityComparer because AST nodes are records with value-based equality,
    // but we need to distinguish between different instances (e.g., two super().__init__() calls
    // in different files should be cached separately even if they have the same structure)

    // Map expressions to their resolved types
    private readonly ConcurrentDictionary<Expression, SemanticType> _expressionTypes =
        new(ReferenceEqualityComparer.Instance);

    // Map identifiers to their symbols
    private readonly ConcurrentDictionary<Identifier, Symbol> _identifierSymbols =
        new(ReferenceEqualityComparer.Instance);

    // Map function calls to resolved function symbols
    private readonly ConcurrentDictionary<FunctionCall, FunctionSymbol> _callTargets =
        new(ReferenceEqualityComparer.Instance);

    // Map type annotations to resolved semantic types
    private readonly ConcurrentDictionary<TypeAnnotation, SemanticType> _typeAnnotations =
        new(ReferenceEqualityComparer.Instance);

    // Map expressions to their narrowed types (for type narrowing after is not None / isinstance checks)
    // This captures the narrowed type at each specific usage of an identifier within a narrowing context
    private readonly ConcurrentDictionary<Expression, SemanticType> _narrowedExpressionTypes =
        new(ReferenceEqualityComparer.Instance);

    // Map generic function calls to their inferred type arguments
    // Used by codegen to emit explicit type arguments in generated C#
    private readonly ConcurrentDictionary<FunctionCall, List<SemanticType>> _inferredTypeArguments =
        new(ReferenceEqualityComparer.Instance);

    // Map member access expressions to their resolved symbols (type owner + member).
    // Used to communicate TypeChecker's resolution to codegen so it doesn't re-resolve.
    // Covers: ClassName.FIELD (static/const), ClassName.method (static), self.static_field.
    private readonly ConcurrentDictionary<MemberAccess, (TypeSymbol Owner, Symbol Member)> _memberAccessResolutions =
        new(ReferenceEqualityComparer.Instance);

    // Track functions that contain yield statements (generators)
    // THREADING: single-threaded access only — not wrapped in ConcurrentDictionary
    private readonly HashSet<FunctionDef> _generatorFunctions = new(ReferenceEqualityComparer.Instance);

    // Track member access expressions that resolve to events (for codegen to emit +=/-= correctly)
    // THREADING: single-threaded access only — not wrapped in ConcurrentDictionary
    private readonly HashSet<Expression> _eventAccessNodes = new(ReferenceEqualityComparer.Instance);

    // Map patterns to their resolved union case type symbols
    // Used when a PositionalPattern or MemberAccessPattern matches a union case
    private readonly ConcurrentDictionary<Pattern, TypeSymbol> _patternUnionCases =
        new(ReferenceEqualityComparer.Instance);

    // Track expressions whose type was set to UnknownType due to a user error
    // (i.e., a diagnostic was already emitted for the node). This distinguishes
    // expected error-recovery Unknown types from unexpected ones (compiler bugs).
    // THREADING: single-threaded access only — not wrapped in ConcurrentDictionary
    private readonly HashSet<Expression> _errorRecoveryNodes =
        new(ReferenceEqualityComparer.Instance);

    // Map with-item context expressions to their context manager kind
    // (Disposable, DunderProtocol, or AsyncDisposable/AsyncDunderProtocol)
    private readonly ConcurrentDictionary<Expression, ContextManagerKind> _contextManagerKinds =
        new(ReferenceEqualityComparer.Instance);

    // Track all reference locations for each symbol (for find-references and rename).
    // Key is Symbol (reference-equality), value is list of (FilePath, Line, Column, Span) tuples.
    // The FilePath may be null for the main file in single-file compilation.
    private readonly ConcurrentDictionary<Symbol, ConcurrentBag<SymbolReference>> _symbolReferences = new();

    /// <summary>
    /// The file path of the current compilation unit, used to tag symbol references.
    /// </summary>
    public string? CurrentFilePath { get; set; }

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
        RecordReference(symbol, id);
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
    /// Records that a MemberAccess was resolved to a specific member symbol owned by a type.
    /// Used for static/const field access via type name (ClassName.FIELD) and
    /// static method access via type name (ClassName.method).
    /// Allows codegen to skip re-resolving the symbol table lookup.
    /// </summary>
    public void SetMemberAccessResolution(MemberAccess memberAccess, TypeSymbol owner, Symbol member)
    {
        _memberAccessResolutions[memberAccess] = (owner, member);
    }

    /// <summary>
    /// Gets the resolved member access symbol, if the TypeChecker recorded one.
    /// Returns null if this MemberAccess was not resolved via type name access.
    /// </summary>
    public (TypeSymbol Owner, Symbol Member)? GetMemberAccessResolution(MemberAccess memberAccess)
    {
        return _memberAccessResolutions.TryGetValue(memberAccess, out var resolution) ? resolution : null;
    }

    /// <summary>
    /// Records that a pattern was resolved to a specific union case type symbol.
    /// Used for PositionalPattern and MemberAccessPattern matching union cases.
    /// </summary>
    public void SetPatternUnionCase(Pattern pattern, TypeSymbol caseSymbol)
    {
        _patternUnionCases[pattern] = caseSymbol;
    }

    /// <summary>
    /// Gets the resolved union case symbol for a pattern, if one was recorded.
    /// Returns null if the pattern was not resolved as a union case.
    /// </summary>
    public TypeSymbol? GetPatternUnionCase(Pattern pattern)
    {
        return _patternUnionCases.TryGetValue(pattern, out var symbol) ? symbol : null;
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
    /// Marks an expression as an event access (for codegen to emit event += / -= correctly).
    /// </summary>
    public void MarkAsEventAccess(Expression expr) => _eventAccessNodes.Add(expr);

    /// <summary>
    /// Returns true if the expression has been marked as an event access.
    /// </summary>
    public bool IsEventAccess(Expression expr) => _eventAccessNodes.Contains(expr);

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

    /// <summary>
    /// Records how a with-item's context expression should be handled at codegen time.
    /// Keyed on the context expression (each with-item has a unique expression reference).
    /// </summary>
    public void SetContextManagerKind(Expression contextExpr, ContextManagerKind kind)
    {
        _contextManagerKinds[contextExpr] = kind;
    }

    /// <summary>
    /// Gets the context manager kind for a with-item's context expression.
    /// Returns null if not recorded (defaults to Disposable in codegen).
    /// </summary>
    public ContextManagerKind? GetContextManagerKind(Expression contextExpr)
    {
        return _contextManagerKinds.TryGetValue(contextExpr, out var kind) ? kind : null;
    }

    // === Symbol Reference Tracking ===

    private void RecordReference(Symbol symbol, Node node)
    {
        if (node.Span == null) return;

        var reference = new SymbolReference(CurrentFilePath, node.Span.Value, node.LineStart, node.ColumnStart);
        var bag = _symbolReferences.GetOrAdd(symbol, _ => new ConcurrentBag<SymbolReference>());
        bag.Add(reference);
    }

    /// <summary>
    /// Gets all recorded reference locations for a symbol.
    /// Returns an empty list if no references have been recorded.
    /// </summary>
    public IReadOnlyList<SymbolReference> GetReferences(Symbol symbol)
    {
        return _symbolReferences.TryGetValue(symbol, out var bag)
            ? bag.ToArray()
            : Array.Empty<SymbolReference>();
    }
}

/// <summary>
/// Records a single location where a symbol is referenced.
/// </summary>
public record SymbolReference(string? FilePath, Text.TextSpan Span, int Line, int Column);

/// <summary>
/// Describes how a with-item's context expression implements the context manager protocol.
/// Used by codegen to decide between C# using statements and explicit Enter/Exit calls.
/// </summary>
public enum ContextManagerKind
{
    /// <summary>Implements IDisposable — use C# using statement.</summary>
    Disposable,

    /// <summary>Implements __enter__/__exit__ dunder protocol — emit Enter()/Exit() calls.</summary>
    DunderProtocol,

    /// <summary>Implements IAsyncDisposable — use C# await using statement.</summary>
    AsyncDisposable,

    /// <summary>Implements __aenter__/__aexit__ async dunder protocol — emit AenterAsync()/AexitAsync() calls.</summary>
    AsyncDunderProtocol
}
