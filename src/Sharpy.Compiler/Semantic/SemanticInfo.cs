using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Maps AST nodes to their semantic information.
/// Provides a way to annotate the AST without modifying it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Threading:</b> All mutable annotation fields use <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// for thread safety, with <c>_symbolReferences</c> using a <see cref="ConcurrentBag{T}"/>
/// per symbol so concurrent writers can record references without locking. The
/// <c>_symbolTable</c> backing field and <c>CurrentFilePath</c> are intended to be set
/// once per instance during initialization and read concurrently afterward.
/// </para>
/// </remarks>
[NotThreadSafe(Reason = "All annotation dictionaries are concurrent, but _symbolTable/CurrentFilePath are set-once initialization fields. Treat the instance as read-mostly after type checking completes.")]
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
    private readonly ConcurrentDictionary<FunctionDef, byte> _generatorFunctions = new(ReferenceEqualityComparer.Instance);

    // Track member access expressions that resolve to events (for codegen to emit +=/-= correctly)
    private readonly ConcurrentDictionary<Expression, byte> _eventAccessNodes = new(ReferenceEqualityComparer.Instance);

    // Track expressions that denote a type rather than a value (e.g., a module-qualified
    // reference to an exported TypeSymbol). Used to accept such expressions for parameters
    // backed by CLR System.Type (e.g., assert_raises(zoneinfo.ZoneInfoNotFoundError)).
    private readonly ConcurrentDictionary<Expression, byte> _typeReferenceNodes = new(ReferenceEqualityComparer.Instance);

    // Map patterns to their resolved union case type symbols
    // Used when a PositionalPattern or MemberAccessPattern matches a union case
    private readonly ConcurrentDictionary<Pattern, TypeSymbol> _patternUnionCases =
        new(ReferenceEqualityComparer.Instance);

    // Map BindingPatterns to constant VariableSymbols when the identifier resolves
    // to a module-level Final/const variable (RFC 3535 — constants in match patterns)
    private readonly ConcurrentDictionary<Pattern, VariableSymbol> _patternConstants =
        new(ReferenceEqualityComparer.Instance);

    // Map TypePatterns to their fully-resolved SemanticType when the TypeChecker
    // computed a type that differs from a naive resolution of the AST type node
    // (e.g., unparameterized collection patterns like `case list()` against an
    // `object` scrutinee get default `object` type arguments filled in). CodeGen
    // prefers this over re-resolving the AST type node.
    private readonly ConcurrentDictionary<Pattern, SemanticType> _patternTypes =
        new(ReferenceEqualityComparer.Instance);

    // Track expressions whose type was set to UnknownType due to a user error
    // (i.e., a diagnostic was already emitted for the node). This distinguishes
    // expected error-recovery Unknown types from unexpected ones (compiler bugs).
    private readonly ConcurrentDictionary<Expression, byte> _errorRecoveryNodes =
        new(ReferenceEqualityComparer.Instance);

    // Map with-item context expressions to their context manager kind
    // (Disposable, DunderProtocol, or AsyncDisposable/AsyncDunderProtocol)
    private readonly ConcurrentDictionary<Expression, ContextManagerKind> _contextManagerKinds =
        new(ReferenceEqualityComparer.Instance);

    // Map with-item nodes to their 'as' variable symbols
    // Needed because the with-scope is exited after type checking, making SymbolTable lookup impossible
    private readonly ConcurrentDictionary<WithItem, VariableSymbol> _withItemSymbols =
        new(ReferenceEqualityComparer.Instance);

    // Map conditional test expressions to their narrowing decisions
    // Used by codegen to determine how to emit branches with type narrowing
    private readonly ConcurrentDictionary<Expression, NarrowingDecision> _narrowingDecisions =
        new(ReferenceEqualityComparer.Instance);

    // Map binary-op expressions (==/!=) to the strategy codegen must use to emit them.
    // Only present when the strategy differs from the default native operator — e.g.
    // tuple equality and CLR types that implement Equals/IEquatable but define no
    // op_Equality must lower to an Equals call instead of a C# `==`. Keyed by node identity.
    private readonly ConcurrentDictionary<Expression, BinaryOpLowering> _binaryOpLowerings =
        new(ReferenceEqualityComparer.Instance);

    // Map declarations to their source generator bindings (bracket attributes that resolve to SourceGenerator subclasses)
    private readonly ConcurrentDictionary<Statement, List<GeneratorBinding>> _generatorBindings =
        new(ReferenceEqualityComparer.Instance);

    // Track statements that were produced by a source generator.
    // Value is the generator name (e.g., "GenerateEquals"). Used by LSP to display
    // "Generated by @[X]" on hover (Phase 7).
    private readonly ConcurrentDictionary<Statement, string> _generatedStatements =
        new(ReferenceEqualityComparer.Instance);

    // Track all reference locations for each symbol (for find-references and rename).
    // Key is Symbol (reference-equality), value is a thread-safe bag of references.
    // The FilePath may be null for the main file in single-file compilation.
    // THREADING: ConcurrentDictionary + ConcurrentBag allow lock-free concurrent writes
    // during type checking. Read order is unspecified, which is acceptable because
    // consumers (find-references / rename) sort or treat results as a set.
    private readonly ConcurrentDictionary<Symbol, ConcurrentBag<SymbolReference>> _symbolReferences = new();

    private SymbolTable? _symbolTable;

    /// <summary>
    /// The file path of the current compilation unit, used to tag symbol references.
    /// </summary>
    public string? CurrentFilePath { get; internal set; }

    public void SetSymbolTable(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
    }

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
    /// Records that a BindingPattern resolved to a module-level constant (RFC 3535).
    /// </summary>
    public void SetPatternConstantSymbol(Pattern pattern, VariableSymbol constantSymbol)
    {
        _patternConstants[pattern] = constantSymbol;
    }

    /// <summary>
    /// Gets the constant variable symbol for a pattern, if one was recorded.
    /// Returns null if the pattern is a normal capture binding.
    /// </summary>
    public VariableSymbol? GetPatternConstantSymbol(Pattern pattern)
    {
        return _patternConstants.TryGetValue(pattern, out var symbol) ? symbol : null;
    }

    /// <summary>
    /// Records the fully-resolved SemanticType the TypeChecker computed for a pattern.
    /// Used for type patterns where the resolved type differs from a naive resolution
    /// of the AST type node (e.g., default `object` type arguments filled in for
    /// unparameterized collection patterns).
    /// </summary>
    public void SetPatternType(Pattern pattern, SemanticType type)
    {
        _patternTypes[pattern] = type;
    }

    /// <summary>
    /// Gets the fully-resolved SemanticType recorded for a pattern, if one was recorded.
    /// Returns null if the TypeChecker did not record a specialized type.
    /// </summary>
    public SemanticType? GetPatternType(Pattern pattern)
    {
        return _patternTypes.TryGetValue(pattern, out var type) ? type : null;
    }

    /// <summary>
    /// Marks an expression as having UnknownType due to error recovery.
    /// Call this when the type is set to UnknownType because a user-facing diagnostic
    /// was already emitted. This allows the invariant checker to distinguish expected
    /// Unknown types (error recovery) from unexpected ones (compiler bugs).
    /// </summary>
    public void MarkErrorRecovery(Expression expr)
    {
        _errorRecoveryNodes.TryAdd(expr, 0);
    }

    /// <summary>
    /// Returns true if the given expression was marked as error recovery,
    /// meaning its UnknownType is expected (a diagnostic was emitted).
    /// </summary>
    public bool IsErrorRecoveryType(Expression expr)
    {
        return _errorRecoveryNodes.ContainsKey(expr);
    }

    /// <summary>
    /// Marks a function as a generator (contains yield statements).
    /// </summary>
    public void MarkAsGenerator(FunctionDef funcDef) => _generatorFunctions.TryAdd(funcDef, 0);

    /// <summary>
    /// Returns true if the function has been marked as a generator.
    /// </summary>
    public bool IsGenerator(FunctionDef funcDef) => _generatorFunctions.ContainsKey(funcDef);

    /// <summary>
    /// Marks an expression as an event access (for codegen to emit event += / -= correctly).
    /// </summary>
    public void MarkAsEventAccess(Expression expr) => _eventAccessNodes.TryAdd(expr, 0);

    /// <summary>
    /// Returns true if the expression has been marked as an event access.
    /// </summary>
    public bool IsEventAccess(Expression expr) => _eventAccessNodes.ContainsKey(expr);

    /// <summary>
    /// Marks an expression as denoting a type reference (rather than a value), e.g., a
    /// module-qualified reference to an exported TypeSymbol.
    /// </summary>
    public void MarkTypeReference(Expression expr) => _typeReferenceNodes.TryAdd(expr, 0);

    /// <summary>
    /// Returns true if the expression has been marked as a type reference.
    /// </summary>
    public bool IsTypeReference(Expression expr) => _typeReferenceNodes.ContainsKey(expr);

    public void AddGeneratorBinding(Statement declaration, TypeSymbol generatorType, Decorator trigger)
    {
        var binding = new GeneratorBinding(generatorType, trigger);
        _generatorBindings.AddOrUpdate(
            declaration,
            _ => new List<GeneratorBinding> { binding },
            (_, list) => { list.Add(binding); return list; });
    }

    public IReadOnlyList<GeneratorBinding> GetGeneratorBindings(Statement declaration)
    {
        return _generatorBindings.TryGetValue(declaration, out var bindings)
            ? bindings
            : Array.Empty<GeneratorBinding>();
    }

    public IEnumerable<(Statement Declaration, IReadOnlyList<GeneratorBinding> Bindings)> GetAllGeneratorBindings()
    {
        foreach (var kvp in _generatorBindings)
            yield return (kvp.Key, kvp.Value);
    }

    /// <summary>
    /// Marks a statement as having been produced by a source generator.
    /// Used by LSP to display "Generated by @[X]" on hover.
    /// </summary>
    /// <param name="statement">The generated statement.</param>
    /// <param name="generatorName">The name of the generator that produced it.</param>
    public void MarkAsGenerated(Statement statement, string generatorName)
    {
        _generatedStatements[statement] = generatorName;
    }

    /// <summary>
    /// Returns true if the given statement was produced by a source generator.
    /// </summary>
    public bool IsGenerated(Statement statement) => _generatedStatements.ContainsKey(statement);

    /// <summary>
    /// Gets the name of the generator that produced the given statement, or null if
    /// the statement was not produced by a generator.
    /// </summary>
    public string? GetGeneratorName(Statement statement)
    {
        return _generatedStatements.TryGetValue(statement, out var name) ? name : null;
    }

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
            .Where(kvp => kvp.Value is UnknownType && !_errorRecoveryNodes.ContainsKey(kvp.Key))
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

    /// <summary>
    /// Records the variable symbol for a with-item's <c>as</c> variable.
    /// Called during type checking so the symbol is retrievable after the with-scope is exited.
    /// </summary>
    public void SetWithItemSymbol(WithItem item, VariableSymbol symbol)
    {
        _withItemSymbols[item] = symbol;
    }

    /// <summary>
    /// Gets the variable symbol for a with-item's <c>as</c> variable.
    /// Returns null if no symbol was recorded (e.g., no <c>as</c> clause).
    /// </summary>
    public VariableSymbol? GetWithItemSymbol(WithItem item)
    {
        return _withItemSymbols.TryGetValue(item, out var symbol) ? symbol : null;
    }

    /// <summary>
    /// Records a narrowing decision for a conditional test expression.
    /// Used by the TypeChecker to communicate narrowing context to codegen.
    /// </summary>
    public void SetNarrowingDecision(Expression test, NarrowingDecision decision)
    {
        _narrowingDecisions[test] = decision;
    }

    /// <summary>
    /// Gets the narrowing decision for a conditional test expression.
    /// Returns null if no narrowing was recorded for this expression.
    /// </summary>
    public NarrowingDecision? GetNarrowingDecision(Expression test)
    {
        return _narrowingDecisions.TryGetValue(test, out var decision) ? decision : null;
    }

    /// <summary>
    /// Records how an equality binary operation (<c>==</c>/<c>!=</c>) should be lowered by codegen.
    /// Only set when the strategy is not the default <see cref="BinaryOpLowering.NativeOperator"/>;
    /// the absence of an entry means codegen should emit a native C# operator.
    /// </summary>
    public void SetBinaryOpLowering(Expression binaryOp, BinaryOpLowering lowering)
    {
        _binaryOpLowerings[binaryOp] = lowering;
    }

    /// <summary>
    /// Gets the lowering strategy for an equality binary operation.
    /// Returns <see cref="BinaryOpLowering.NativeOperator"/> when no override was recorded.
    /// </summary>
    public BinaryOpLowering GetBinaryOpLowering(Expression binaryOp)
    {
        return _binaryOpLowerings.TryGetValue(binaryOp, out var lowering)
            ? lowering
            : BinaryOpLowering.NativeOperator;
    }

    /// <summary>
    /// Merges all entries from another SemanticInfo into this instance.
    /// Used to combine per-file SemanticInfo back into a project-level instance.
    /// </summary>
    public void MergeFrom(SemanticInfo other)
    {
        foreach (var kvp in other._expressionTypes)
            _expressionTypes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._identifierSymbols)
            _identifierSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._callTargets)
            _callTargets.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._typeAnnotations)
            _typeAnnotations.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._narrowedExpressionTypes)
            _narrowedExpressionTypes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._inferredTypeArguments)
            _inferredTypeArguments.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._memberAccessResolutions)
            _memberAccessResolutions.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._generatorFunctions)
            _generatorFunctions.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._eventAccessNodes)
            _eventAccessNodes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._typeReferenceNodes)
            _typeReferenceNodes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._patternUnionCases)
            _patternUnionCases.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._patternConstants)
            _patternConstants.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._patternTypes)
            _patternTypes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._errorRecoveryNodes)
            _errorRecoveryNodes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._contextManagerKinds)
            _contextManagerKinds.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._withItemSymbols)
            _withItemSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._narrowingDecisions)
            _narrowingDecisions.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._binaryOpLowerings)
            _binaryOpLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._generatedStatements)
            _generatedStatements.TryAdd(kvp.Key, kvp.Value);

        foreach (var (symbol, refs) in other._symbolReferences)
        {
            var bag = _symbolReferences.GetOrAdd(symbol, static _ => new ConcurrentBag<SymbolReference>());
            foreach (var reference in refs)
            {
                bag.Add(reference);
            }
        }
    }

    // === Symbol Reference Tracking ===

    private void RecordReference(Symbol symbol, Node node)
    {
        if (node.Span == null)
            return;

        var reference = new SymbolReference(CurrentFilePath, node.Span.Value, node.LineStart, node.ColumnStart);
        var bag = _symbolReferences.GetOrAdd(symbol, static _ => new ConcurrentBag<SymbolReference>());
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

    /// <inheritdoc/>
    public IReadOnlyList<SymbolReference> FindReferencesBySymbolIdentity(string symbolName, string? declaringFilePath)
    {
        foreach (var (symbol, refs) in _symbolReferences)
        {
            if (symbol.Name == symbolName &&
                string.Equals(symbol.DeclaringFilePath, declaringFilePath, StringComparison.Ordinal))
            {
                return refs.ToArray();
            }
        }
        return Array.Empty<SymbolReference>();
    }

    /// <inheritdoc/>
    public Symbol? FindSymbolByDeclaration(string name, int line, int column)
    {
        foreach (var symbol in _symbolReferences.Keys)
        {
            if (symbol.Name == name
                && symbol.DeclarationLine == line
                && symbol.DeclarationColumn == column)
            {
                return symbol;
            }
        }

        foreach (var symbol in _identifierSymbols.Values)
        {
            if (symbol.Name == name
                && symbol.DeclarationLine == line
                && symbol.DeclarationColumn == column)
            {
                return symbol;
            }
        }

        if (_symbolTable != null)
        {
            foreach (var symbol in _symbolTable.GetAllModuleScopeSymbols())
            {
                if (symbol.Name == name
                    && symbol.DeclarationLine == line
                    && symbol.DeclarationColumn == column)
                {
                    return symbol;
                }
            }
        }

        return null;
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

/// <summary>
/// Represents a narrowing decision for a conditional statement.
/// Records all type narrowings that a condition's test expression implies,
/// so codegen can emit the correct accessor patterns (e.g., <c>.Value</c> for value-type nullables).
/// </summary>
/// <param name="OptionalNarrowings">Optional/Nullable narrowings implied by the test.</param>
/// <param name="IsInstanceNarrowings">isinstance() narrowings implied by the test.</param>
/// <param name="NarrowsFollowingStatements">
/// True if the else-branch narrowings (entries with <c>NarrowInThenBranch == false</c>) also apply
/// to the statements following the if statement, because the then-branch unconditionally exits
/// (e.g., <c>if x is None: return</c>). Only set by the TypeChecker for if statements at the top
/// level of a function/module body so the narrowing's lifetime matches the rest of the method (#817).
/// </param>
public sealed record NarrowingDecision(
    IReadOnlyList<OptionalNarrowing> OptionalNarrowings,
    IReadOnlyList<IsInstanceNarrowing> IsInstanceNarrowings,
    bool NarrowsFollowingStatements = false);

/// <summary>
/// A variable narrowed from <see cref="OptionalType"/> or <see cref="NullableType"/> to its underlying type.
/// </summary>
/// <param name="VariableName">The name of the narrowed variable.</param>
/// <param name="NarrowedType">The type after narrowing (the underlying type of Optional/Nullable).</param>
/// <param name="IsValueTypeNullable">True if <see cref="NullableType"/> (value-type); needs <c>.Value</c> access in codegen.</param>
/// <param name="NarrowInThenBranch">True if narrowing applies in the then-branch; false for the else-branch.</param>
public sealed record OptionalNarrowing(
    string VariableName,
    SemanticType NarrowedType,
    bool IsValueTypeNullable,
    bool NarrowInThenBranch,
    bool IsReferenceTypeNullable = false);

/// <summary>
/// A variable narrowed via an <c>isinstance()</c> check.
/// </summary>
/// <param name="VariableName">The name of the narrowed variable.</param>
/// <param name="NarrowedType">The type after narrowing (the target type of the isinstance check).</param>
/// <param name="NarrowInThenBranch">True if narrowing applies in the then-branch; false for the else-branch.</param>
public sealed record IsInstanceNarrowing(
    string VariableName,
    SemanticType NarrowedType,
    bool NarrowInThenBranch);

public sealed record GeneratorBinding(TypeSymbol GeneratorType, Decorator Trigger);

/// <summary>
/// How codegen should emit an equality binary operation (<c>==</c>/<c>!=</c>).
/// The TypeChecker records this during inference because the emitter cannot re-derive
/// it from the operand types alone (it needs the same operator-resolution rules).
/// </summary>
public enum BinaryOpLowering
{
    /// <summary>Emit a native C# operator (<c>left == right</c> / <c>left != right</c>). Default.</summary>
    NativeOperator,

    /// <summary>
    /// Lower to an <c>Equals</c> call: <c>object.Equals(left, right)</c> for reference types or
    /// <c>left.Equals(right)</c> for tuples/value types; <c>!=</c> wraps the result in <c>!(...)</c>.
    /// Used for tuples and CLR types that implement <c>Equals</c>/<c>IEquatable</c> but define no
    /// <c>op_Equality</c>, where a native C# <c>==</c> would be wrong (reference equality) or fail to compile.
    /// </summary>
    EqualsCall,

    /// <summary>
    /// Lower to a C# null pattern check: <c>operand is null</c> (<c>==</c>) / <c>operand is not null</c>
    /// (<c>!=</c>), where <c>operand</c> is the non-None side. Used for <c>x == None</c>/<c>x != None</c>
    /// on reference-semantics types — this bypasses any overloaded <c>op_Equality</c> and matches Python's
    /// identity fallback (a live object <c>== None</c> is <c>False</c>). Operand order is irrelevant (#901).
    /// </summary>
    NoneCheck
}
