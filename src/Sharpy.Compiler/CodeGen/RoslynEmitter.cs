using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Generates C# code using Roslyn syntax trees.
///
/// Name Resolution:
/// - Module-level symbols (variables, constants, functions, types, imports):
///   Use Symbol.CodeGenInfo which is computed during semantic analysis
/// - Local variables: Use runtime tracking (_declaredVariables, _variableVersions)
///   because local variable redeclarations happen during emission
/// - Type detection (class/struct instantiation): Use SymbolTable lookup
/// - String enum detection: Use CodeGenInfo.IsStringEnum
/// </summary>
internal partial class RoslynEmitter
{
    private readonly CodeGenContext _context;
    private readonly TypeMapper _typeMapper;
    private readonly NameResolutionService _nameResolutionService;
    private readonly CancellationToken _cancellationToken;
    private readonly HashSet<string> _declaredVariables = new();

    // ============================================================
    // LOCAL SCOPE TRACKING FIELDS
    //
    // These fields track mutable state during emission for LOCAL variables.
    // They are needed because local variable redeclarations happen during
    // emission, not during semantic analysis (so CodeGenInfo can't pre-compute them).
    //
    // For module-level symbols (variables, functions, types, imports),
    // use Symbol.CodeGenInfo which is computed during semantic analysis.
    // ============================================================

    /// <summary>
    /// Tracks variable version numbers for handling local variable redeclarations.
    /// E.g., x = 1; x = "hello" produces x then x_1.
    /// </summary>
    private readonly Dictionary<string, int> _variableVersions = new();

    /// <summary>
    /// Tracks all source variable names (C# camelCase) declared in the current function scope.
    /// Used to avoid generating versioned names (x_1, x_2) that collide with user-declared variables.
    /// Pre-populated by scanning the function body before emission.
    /// </summary>
    private readonly HashSet<string> _sourceVariableNames = new();

    /// <summary>
    /// Tracks const variable names (original Sharpy names) within local scopes.
    /// Needed for local const declarations within functions.
    /// </summary>
    private readonly HashSet<string> _constVariables = new();

    /// <summary>
    /// Tracks module-level field names (C# names) to prevent duplicate field declarations.
    /// This is still needed during emission even with CodeGenInfo because we need to
    /// track which C# field names have already been emitted.
    /// </summary>
    private readonly HashSet<string> _moduleFieldNames = new();

    /// <summary>
    /// When true, forces module-level variable declarations to be generated as static fields
    /// even if they have execution order issues. This is set when there's a user-defined main()
    /// function, because in that case the user is responsible for execution order.
    /// </summary>
    private bool _forceModuleLevelFields;

    // ============================================================
    // END LOCAL SCOPE TRACKING FIELDS
    // ============================================================

    // Note: _classNames, _structNames, and _stringEnumNames tracking sets were removed.
    // Type detection is now done via SymbolTable lookup (for classes/structs) and
    // CodeGenInfo.IsStringEnum (for string enums). This information is populated
    // during semantic analysis.

    private readonly Dictionary<string, InterfaceDef> _interfaceDefinitions = new(); // Track interface definitions for abstract class stub generation
    private int _tempVarCounter = 0;

    // Target type context for collection literal type inference
    // Set before generating expressions that need target type information
    private TypeAnnotation? _targetTypeContext;

    // Track if we're currently generating methods for an abstract class
    // Used for implicit abstract method detection (ellipsis body in abstract class = abstract method)
    private bool _isInAbstractClass;

    // Track the current TypeSymbol being generated (for IEquatable virtual detection, etc.)
    private TypeSymbol? _currentTypeSymbol;

    // When set, `self` maps to this identifier instead of `this`.
    // Used for inlining dunder bodies into static operators (self → left/value).
    private string? _selfReplacementIdentifier;

    /// <summary>
    /// Tracks variable names (original Sharpy names) that have been narrowed from
    /// Optional&lt;T&gt; to T via an is-not-None check. Uses reference counting to support
    /// nested scopes (e.g., nested if-statements checking the same variable).
    /// When a variable's count is &gt; 0, identifier references emit .Unwrap().
    /// </summary>
    private readonly Dictionary<string, int> _narrowedOptionals = new();

    /// <summary>
    /// Tracks variable names that are narrowed from NullableType (T | None) rather than
    /// OptionalType. For value-type nullables (int?, bool?, etc.), the emitter generates
    /// .Value instead of .Unwrap(). Reference-type nullables (string?) don't need .Value
    /// because C# narrows them automatically after a null check.
    /// </summary>
    private readonly HashSet<string> _isNullableNarrowing = new();

    /// <summary>
    /// Pushes a narrowing scope for the given variable. Reference-counted so
    /// nested scopes (e.g., nested if-statements) work correctly.
    /// </summary>
    private void PushNarrowing(string variableName)
    {
        _narrowedOptionals.TryGetValue(variableName, out var count);
        _narrowedOptionals[variableName] = count + 1;
    }

    /// <summary>
    /// Pops a narrowing scope for the given variable. Only fully removes
    /// narrowing when the last scope is popped.
    /// </summary>
    private void PopNarrowing(string variableName)
    {
        if (_narrowedOptionals.TryGetValue(variableName, out var count) && count > 1)
        {
            _narrowedOptionals[variableName] = count - 1;
        }
        else
        {
            _narrowedOptionals.Remove(variableName);
        }
    }

    /// <summary>
    /// Returns true if the variable is currently narrowed from Optional&lt;T&gt; to T.
    /// </summary>
    private bool IsNarrowed(string variableName)
        => _narrowedOptionals.TryGetValue(variableName, out var count) && count > 0;

    /// <summary>
    /// Returns true if the variable is narrowed as a value-type nullable (needs .Value).
    /// </summary>
    private bool IsNullableNarrowed(string variableName)
        => _isNullableNarrowing.Contains(variableName);

    /// <summary>
    /// Clears narrowing for a variable (e.g., after reassignment).
    /// </summary>
    private void ClearNarrowing(string variableName)
    {
        _narrowedOptionals.Remove(variableName);
        _isNullableNarrowing.Remove(variableName);
        _isInstanceNarrowed.Remove(variableName);
    }

    /// <summary>
    /// Tracks variables narrowed by isinstance() checks.
    /// Maps variable name → stack of C# type names to cast to.
    /// </summary>
    private readonly Dictionary<string, Stack<string>> _isInstanceNarrowed = new();

    private void PushIsInstanceNarrowing(string variableName, string csharpTypeName)
    {
        if (!_isInstanceNarrowed.TryGetValue(variableName, out var stack))
        {
            stack = new Stack<string>();
            _isInstanceNarrowed[variableName] = stack;
        }
        stack.Push(csharpTypeName);
    }

    private void PopIsInstanceNarrowing(string variableName)
    {
        if (_isInstanceNarrowed.TryGetValue(variableName, out var stack))
        {
            stack.Pop();
            if (stack.Count == 0)
                _isInstanceNarrowed.Remove(variableName);
        }
    }

    private bool IsInstanceNarrowed(string variableName)
        => _isInstanceNarrowed.TryGetValue(variableName, out var stack) && stack.Count > 0;

    private string? GetIsInstanceNarrowedType(string variableName)
        => _isInstanceNarrowed.TryGetValue(variableName, out var stack) && stack.Count > 0
            ? stack.Peek() : null;

    /// <summary>
    /// Snapshot of local scope tracking state, used for block-scoped constructs (for loops).
    /// </summary>
    private record ScopeSnapshot(
        HashSet<string> DeclaredVariables,
        Dictionary<string, int> VariableVersions,
        HashSet<string> ConstVariables);

    /// <summary>
    /// Saves a snapshot of the current local scope state.
    /// Used before entering a for-loop body so that loop variables
    /// and body-declared variables are removed from scope after the loop.
    /// </summary>
    private ScopeSnapshot SaveScope()
    {
        return new ScopeSnapshot(
            new HashSet<string>(_declaredVariables),
            new Dictionary<string, int>(_variableVersions),
            new HashSet<string>(_constVariables));
    }

    /// <summary>
    /// Restores the local scope state from a snapshot.
    /// Variables declared inside the block are removed from scope.
    /// </summary>
    private void RestoreScope(ScopeSnapshot snapshot)
    {
        _declaredVariables.Clear();
        _declaredVariables.UnionWith(snapshot.DeclaredVariables);
        _variableVersions.Clear();
        foreach (var (k, v) in snapshot.VariableVersions)
            _variableVersions[k] = v;
        _constVariables.Clear();
        _constVariables.UnionWith(snapshot.ConstVariables);
    }

    // Maps original parameter base names (camelCase) to C# replacement names.
    // Used for inlined operator bodies: e.g., "other" → "right".
    private Dictionary<string, string>? _parameterNameOverrides;

    // Common .NET namespace acronyms that should be all uppercase
    private static readonly HashSet<string> UpperCaseAcronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "io", "ui", "xml", "html", "api", "sql", "db", "http", "ftp",
        "smtp", "tcp", "udp", "ip", "uri", "url", "json", "csv", "guid"
    };

    public RoslynEmitter(CodeGenContext context, CancellationToken cancellationToken = default)
    {
        _context = context;
        _typeMapper = new TypeMapper(context);
        _nameResolutionService = new NameResolutionService(context.Logger);
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Build a fully-qualified name using the global:: alias qualifier via explicit Roslyn syntax nodes.
    /// ParseName("global::X.Y") misparses "global" as a regular identifier instead of the alias qualifier,
    /// which breaks in constrained expression contexts (e.g., f-string interpolation holes).
    /// </summary>
    /// <param name="parts">The namespace/type segments after global:: (e.g., "Sharpy", "Builtins", "Len").</param>
    private static NameSyntax MakeGlobalQualifiedName(params string[] parts)
    {
        NameSyntax name = AliasQualifiedName(
            IdentifierName(Token(SyntaxKind.GlobalKeyword)),
            IdentifierName(parts[0]));
        for (int i = 1; i < parts.Length; i++)
            name = QualifiedName(name, IdentifierName(parts[i]));
        return name;
    }

    /// <summary>
    /// Resolve the C# name for a variable using CodeGenInfo.
    /// Returns null if CodeGenInfo is not available or if this is a local redeclaration.
    /// </summary>
    /// <remarks>
    /// This method delegates to NameResolutionService for the core resolution logic.
    /// The service handles CodeGenInfo-based resolution, including:
    /// - Module-level vs local variable detection
    /// - Force module-level fields override
    /// - C# keyword escaping for module symbols
    /// </remarks>
    private string? TryGetCSharpNameFromCodeGenInfo(string sharpyName, bool isNewDeclaration)
    {
        var symbol = _context.LookupSymbol(sharpyName);
        if (symbol == null)
            return null;

        var info = GetCodeGenInfo(symbol);
        if (info == null)
            return null;

        // Delegate to NameResolutionService for CodeGenInfo-based resolution
        // The service returns null if this should fall through to local variable handling
        return _nameResolutionService.TryResolveFromCodeGenInfo(
            symbol,
            info,
            isNewDeclaration,
            _forceModuleLevelFields);
    }

    /// <summary>
    /// Get the mangled variable name with version suffix if this is a redefinition.
    /// </summary>
    /// <param name="name">The original Sharpy variable name</param>
    /// <param name="isNewDeclaration">True if this is a new declaration/redefinition, false if this is a reference</param>
    /// <returns>The C# variable name with version suffix (e.g., "x", "x_1", "x_2")</returns>
    /// <remarks>
    /// This method delegates to NameResolutionService for name computation while
    /// maintaining local state (_variableVersions, _sourceVariableNames, _constVariables)
    /// in the emitter.
    ///
    /// Resolution order:
    /// 1. Local variables (tracked in _variableVersions) - highest precedence
    /// 2. Local const variables (tracked in _constVariables)
    /// 3. Type/module symbols (via SymbolTable lookup and NameResolutionService)
    /// 4. CodeGenInfo-based resolution (module-level symbols, imports)
    /// 5. New local variable registration
    /// </remarks>
    private string GetMangledVariableName(string name, bool isNewDeclaration)
    {
        var baseName = _nameResolutionService.GetBaseName(name);

        // Check parameter name overrides (used for inlined operator bodies: "other" → "right")
        if (_parameterNameOverrides != null
            && !isNewDeclaration
            && _parameterNameOverrides.TryGetValue(baseName, out var overrideName))
        {
            return overrideName;
        }

        // FIRST: Check if this is a local variable (including parameters)
        // Local variables take precedence over module-level variables and CodeGenInfo
        // This handles parameter shadowing correctly (parameter x shadows global x)
        if (_variableVersions.ContainsKey(baseName))
        {
            // There's a local variable with this name - use local resolution via service
            var resolvedName = _nameResolutionService.ResolveLocalName(
                name,
                isNewDeclaration,
                _variableVersions,
                _sourceVariableNames);

            if (resolvedName != null)
            {
                // For redeclarations, update state after service computes the new version
                if (isNewDeclaration)
                {
                    var newVersion = _nameResolutionService.ComputeNextVersion(
                        name,
                        _variableVersions[baseName],
                        _sourceVariableNames);
                    _variableVersions[baseName] = newVersion;
                }
                return resolvedName;
            }
        }

        // Check if this is a reference to a local const variable - use constant case
        // (still needed for local scope tracking during emission)
        if (_constVariables.Contains(name))
        {
            return NameMangler.ToConstantCase(name);
        }

        // Look up the symbol to check its kind
        var symbol = _context.LookupSymbol(name);

        // Check if this is a reference to a class or struct name - preserve PascalCase
        // Uses symbol table lookup instead of legacy tracking sets
        if (symbol is TypeSymbol typeSymbol &&
            (typeSymbol.TypeKind == Semantic.TypeKind.Class ||
             typeSymbol.TypeKind == Semantic.TypeKind.Struct))
        {
            return NameMangler.ToPascalCase(name);
        }

        // Check if this is a module symbol - use service for name resolution
        if (symbol is ModuleSymbol)
        {
            return NameResolutionService.EscapeCSharpKeyword(name.Replace(".", "_"));
        }

        // Try CodeGenInfo-based resolution for module-level symbols and from-imports
        // CodeGenInfo handles: module-level variables, constants, from-imports (with aliases)
        // This comes after local variable checks to ensure parameters shadow globals correctly
        var codeGenName = TryGetCSharpNameFromCodeGenInfo(name, isNewDeclaration);
        if (codeGenName != null)
            return codeGenName;

        // If we reach here, this is a new local variable that doesn't shadow any module-level var
        if (isNewDeclaration)
        {
            // First declaration of this local variable
            _variableVersions[baseName] = 0;
            return baseName;
        }
        else
        {
            // Reference to a variable not yet declared (shouldn't happen in valid code)
            // Fall back to just returning the base name
            return baseName;
        }
    }

    /// <summary>
    /// Pre-scans statements to collect all variable names that will be declared.
    /// This is used to avoid generating versioned names (x_1, x_2) that collide
    /// with user-declared variables.
    /// </summary>
    private void CollectSourceVariableNames(IEnumerable<Statement> statements)
    {
        foreach (var stmt in statements)
        {
            CollectSourceVariableNamesFromStatement(stmt);
        }
    }

    /// <summary>
    /// Recursively collects variable names from a single statement and its nested statements.
    /// </summary>
    private void CollectSourceVariableNamesFromStatement(Statement stmt)
    {
        switch (stmt)
        {
            case Assignment assign:
                CollectVariableNamesFromExpression(assign.Target);
                break;

            case VariableDeclaration varDecl:
                var mangledName = NameMangler.ToCamelCase(varDecl.Name);
                _sourceVariableNames.Add(mangledName);
                break;

            case ForStatement forStmt:
                CollectVariableNamesFromExpression(forStmt.Target);
                CollectSourceVariableNames(forStmt.Body);
                CollectSourceVariableNames(forStmt.ElseBody);
                break;

            case IfStatement ifStmt:
                CollectSourceVariableNames(ifStmt.ThenBody);
                CollectSourceVariableNames(ifStmt.ElseBody);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    CollectSourceVariableNames(elif.Body);
                }
                break;

            case WhileStatement whileStmt:
                CollectSourceVariableNames(whileStmt.Body);
                CollectSourceVariableNames(whileStmt.ElseBody);
                break;

            case TryStatement tryStmt:
                CollectSourceVariableNames(tryStmt.Body);
                foreach (var handler in tryStmt.Handlers)
                {
                    if (handler.Name != null)
                    {
                        var handlerMangledName = NameMangler.ToCamelCase(handler.Name);
                        _sourceVariableNames.Add(handlerMangledName);
                    }
                    CollectSourceVariableNames(handler.Body);
                }
                CollectSourceVariableNames(tryStmt.ElseBody);
                CollectSourceVariableNames(tryStmt.FinallyBody);
                break;

            case MatchStatement matchStmt:
                foreach (var caseClause in matchStmt.Cases)
                {
                    CollectVariableNamesFromPattern(caseClause.Pattern);
                    CollectSourceVariableNames(caseClause.Body);
                }
                break;
        }
    }

    /// <summary>
    /// Collects variable names from an expression (used for assignment targets).
    /// </summary>
    private void CollectVariableNamesFromExpression(Expression expr)
    {
        switch (expr)
        {
            case Identifier id:
                var mangledName = NameMangler.ToCamelCase(id.Name);
                _sourceVariableNames.Add(mangledName);
                break;

            case TupleLiteral tuple:
                foreach (var elem in tuple.Elements)
                {
                    CollectVariableNamesFromExpression(elem);
                }
                break;
        }
    }

    /// <summary>
    /// Collects variable names from match patterns.
    /// </summary>
    private void CollectVariableNamesFromPattern(Pattern pattern)
    {
        switch (pattern)
        {
            case BindingPattern binding:
                var mangledName = NameMangler.ToCamelCase(binding.Name);
                _sourceVariableNames.Add(mangledName);
                break;

            case TuplePattern tuplePattern:
                foreach (var elem in tuplePattern.Elements)
                {
                    CollectVariableNamesFromPattern(elem);
                }
                break;

            case UnionCasePattern unionCase:
                foreach (var fieldPattern in unionCase.FieldPatterns)
                {
                    CollectVariableNamesFromPattern(fieldPattern);
                }
                break;

            case ListPattern listPattern:
                foreach (var elem in listPattern.Elements)
                {
                    CollectVariableNamesFromPattern(elem);
                }
                if (listPattern.RestPattern != null)
                {
                    CollectVariableNamesFromPattern(listPattern.RestPattern);
                }
                break;

            case TypePattern typePattern when typePattern.BindingName != null:
                var typeBindingName = NameMangler.ToCamelCase(typePattern.BindingName);
                _sourceVariableNames.Add(typeBindingName);
                break;

            case OrPattern orPattern:
                foreach (var alt in orPattern.Alternatives)
                {
                    CollectVariableNamesFromPattern(alt);
                }
                break;

            case AndPattern andPattern:
                CollectVariableNamesFromPattern(andPattern.Left);
                CollectVariableNamesFromPattern(andPattern.Right);
                break;

            case GuardPattern guardPattern:
                CollectVariableNamesFromPattern(guardPattern.Inner);
                break;
        }
    }

    // ============================================================
    // CodeGenInfo helper methods
    //
    // These methods read CodeGenInfo via SemanticBinding (preferred)
    // with fallback to Symbol.CodeGenInfo (post-materialization).
    // Materialization copies data from SemanticBinding to Symbol
    // properties at phase boundaries, so both sources should agree
    // after materialization.
    // ============================================================

    /// <summary>
    /// Get CodeGenInfo for a symbol from SemanticBinding.
    /// Falls back to symbol.CodeGenInfo for symbols not tracked by this binding.
    /// </summary>
    private CodeGenInfo? GetCodeGenInfo(Symbol symbol)
        => _context.SemanticBinding.GetCodeGenInfo(symbol) ?? symbol.CodeGenInfo;

    /// <summary>
    /// Get the type for a VariableSymbol from SemanticBinding.
    /// Falls back to symbol.Type for symbols not tracked by this binding.
    /// </summary>
    private SemanticType GetVariableType(VariableSymbol symbol)
    {
        var bindingType = _context.SemanticBinding.GetVariableType(symbol);
        return bindingType != SemanticType.Unknown ? bindingType : symbol.Type;
    }

    /// <summary>
    /// Get the C# name for a symbol using CodeGenInfo.
    /// </summary>
    /// <remarks>
    /// This method first tries CodeGenInfo resolution. If that fails,
    /// it delegates to NameResolutionService for fallback logic,
    /// except for Variables which need stateful local variable tracking.
    /// </remarks>
    private string GetCSharpNameForSymbol(Symbol symbol, bool isNewDeclaration = false)
    {
        var info = GetCodeGenInfo(symbol);
        if (info != null)
        {
            return info.GetVersionedCSharpName();
        }

        // CodeGenInfo not available - use fallback logic
        // Variables need special handling due to local state tracking
        if (symbol.Kind == Semantic.SymbolKind.Variable)
        {
            return GetMangledVariableName(symbol.Name, isNewDeclaration);
        }

        // For non-variable symbols, delegate to NameResolutionService
        return _nameResolutionService.ResolveName(symbol, codeGenInfo: null);
    }

    /// <summary>
    /// Check if a symbol is a module-level constant using CodeGenInfo.
    /// </summary>
    private bool IsModuleLevelConstant(Symbol symbol)
    {
        var info = GetCodeGenInfo(symbol);
        return info?.IsModuleLevel == true && info.IsConstant;
    }

    /// <summary>
    /// Check if a symbol is a module-level variable (not constant) using CodeGenInfo.
    /// </summary>
    private bool IsModuleLevelVariable(Symbol symbol)
    {
        var info = GetCodeGenInfo(symbol);
        return info?.IsModuleLevel == true && !info.IsConstant;
    }

    /// <summary>
    /// Check if a symbol has execution order issues using CodeGenInfo.
    /// </summary>
    private bool HasExecutionOrderIssues(Symbol symbol)
    {
        return GetCodeGenInfo(symbol)?.HasExecutionOrderIssues == true;
    }

    /// <summary>
    /// Check if a symbol is a from-import symbol using CodeGenInfo.
    /// </summary>
    private bool IsFromImportSymbol(Symbol symbol)
    {
        var info = GetCodeGenInfo(symbol);
        return info?.ImportKind == ImportKind.FromImport ||
               info?.ImportKind == ImportKind.FromImportWithAlias;
    }

    /// <summary>
    /// Get the original import name for an aliased from-import using CodeGenInfo.
    /// </summary>
    private string? GetOriginalImportName(Symbol symbol)
    {
        return GetCodeGenInfo(symbol)?.OriginalImportName;
    }

    // ============================================================
    // SemanticBinding helper methods for FromImportStatement data
    //
    // These methods read from SemanticBinding when available,
    // falling back to direct AST properties for backward compatibility.
    // ============================================================

    /// <summary>
    /// Gets the resolved module path for a FromImportStatement from SemanticBinding or AST.
    /// </summary>
    private string? GetResolvedModulePath(FromImportStatement fromImport)
    {
        return _context.SemanticBinding.GetResolvedModulePath(fromImport)
            ?? fromImport.ResolvedModulePath;
    }

    /// <summary>
    /// Gets the re-exported symbols for a FromImportStatement from SemanticBinding or AST.
    /// </summary>
    private Dictionary<string, Symbol>? GetReExportedSymbols(FromImportStatement fromImport)
    {
        return _context.SemanticBinding.GetReExportedSymbols(fromImport)
            ?? fromImport.ReExportedSymbols;
    }

    /// <summary>
    /// Checks if a FromImportStatement has re-exported symbols.
    /// </summary>
    private bool HasReExportedSymbols(FromImportStatement fromImport)
    {
        var symbols = GetReExportedSymbols(fromImport);
        return symbols != null && symbols.Count > 0;
    }

    /// <summary>
    /// Emits a diagnostic for an unrecognized statement type in code generation.
    /// Returns null so it can be used in switch expressions.
    /// </summary>
    private SyntaxNode? EmitUnrecognizedStatementDiagnostic(Statement stmt)
    {
        _context.AddError(
            $"Internal: unrecognized statement type '{stmt.GetType().Name}' was not emitted. This is a compiler bug — please report it.",
            DiagnosticCodes.CodeGen.UnrecognizedStatementType,
            stmt.LineStart,
            stmt.ColumnStart);
        return null;
    }

    /// <summary>
    /// Emits a diagnostic for a not-yet-implemented feature in code generation and returns
    /// a <c>default</c> literal as a safe placeholder expression. The diagnostic error ensures
    /// compilation reports failure, so this code should never execute.
    /// </summary>
    private ExpressionSyntax EmitNotImplementedExpression(string message, string code, int? line = null, int? column = null)
    {
        _context.AddError(message, code, line, column);
        return LiteralExpression(SyntaxKind.DefaultLiteralExpression);
    }

    /// <summary>
    /// Emits a diagnostic for a not-yet-implemented feature in code generation and returns
    /// an empty statement as a safe fallback.
    /// </summary>
    private StatementSyntax EmitNotImplementedStatement(string message, string code, int? line = null, int? column = null)
    {
        _context.AddError(message, code, line, column);
        return EmptyStatement();
    }
}
