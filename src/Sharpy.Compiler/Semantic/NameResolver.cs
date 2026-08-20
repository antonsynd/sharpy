extern alias SharpyRT;
using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// First pass: Resolve all names and build symbol tables
/// </summary>
internal partial class NameResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly SemanticBinding _semanticBinding;
    private readonly DiagnosticBag _diagnostics = new();
    // Each pending definition carries the file it was declared in as well as its module scope
    // (#1369): pass 2 can run on an aggregate resolver that has no file of its own, and a
    // definition that arrives there without its path produces an unattributable diagnostic.
    private readonly List<(ClassDef Def, TypeSymbol Symbol, string? ModulePath, string? FilePath)> _classDefs = new();
    private readonly List<(StructDef Def, TypeSymbol Symbol, string? ModulePath, string? FilePath)> _structDefs = new();
    private readonly List<(InterfaceDef Def, TypeSymbol Symbol, string? ModulePath, string? FilePath)> _interfaceDefs = new();
    private string? _currentFilePath;
    private string? _currentModulePath;

    public NameResolver(SymbolTable symbolTable, ICompilerLogger? logger = null, SemanticBinding? semanticBinding = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
        _semanticBinding = semanticBinding ?? new SemanticBinding();
    }

    public DiagnosticBag Diagnostics => _diagnostics;

    public IReadOnlyList<(ClassDef Def, TypeSymbol Symbol, string? ModulePath, string? FilePath)> ClassDefs => _classDefs;
    public IReadOnlyList<(StructDef Def, TypeSymbol Symbol, string? ModulePath, string? FilePath)> StructDefs => _structDefs;
    public IReadOnlyList<(InterfaceDef Def, TypeSymbol Symbol, string? ModulePath, string? FilePath)> InterfaceDefs => _interfaceDefs;

    /// <summary>
    /// Aggregates type definition lists from per-file resolvers into this resolver.
    /// Used to prepare a merged NameResolver for inheritance resolution after
    /// per-file name resolution and symbol table merge.
    ///
    /// <para>The aggregate has no file of its own — it holds every file's definitions at once —
    /// so each definition brings its declaring resolver's file path along with it (#1369). Copying
    /// only <c>(Def, ModulePath)</c> is what left every Phase-4b inheritance diagnostic with a null
    /// <c>FilePath</c>: the per-file resolvers are stamped correctly
    /// (<c>ProjectCompiler.CollectTypeDeclarations</c>), and the identity was dropped here.</para>
    /// </summary>
    public void AggregateTypeDefinitionsFrom(IEnumerable<NameResolver> perFileResolvers)
    {
        foreach (var resolver in perFileResolvers)
        {
            _classDefs.AddRange(resolver.ClassDefs);
            _structDefs.AddRange(resolver.StructDefs);
            _interfaceDefs.AddRange(resolver.InterfaceDefs);
        }
    }

    /// <summary>
    /// Set the current source file path for tracking type definitions.
    /// </summary>
    public void SetCurrentFilePath(string? filePath)
    {
        _currentFilePath = filePath;
    }

    /// <summary>
    /// Set the current module path for tracking which module each type belongs to.
    /// Used by ProjectCompiler to associate type definitions with their module scope
    /// so that inheritance resolution enters the correct scope.
    /// </summary>
    public void SetCurrentModulePath(string? modulePath)
    {
        _currentModulePath = modulePath;
    }

    /// <summary>
    /// Resolve names in a module (first pass: declarations only)
    /// </summary>
    public void ResolveDeclarations(Module module, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Starting name resolution pass 1: Declarations");

        // Pre-pass: register all module-level function signatures so that classes
        // defined before a function can reference it (forward function references).
        // This matches Python's behavior where all top-level names are available
        // throughout the module regardless of definition order.
        foreach (var statement in module.Body)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (statement is FunctionDef functionDef)
            {
                ResolveFunctionDeclaration(functionDef);
            }
        }

        // Main pass: process all declarations. FunctionDef statements are re-visited here
        // but ResolveFunctionDeclaration() detects the existing symbol and returns early,
        // so only non-function declarations (classes, structs, etc.) do real work.
        foreach (var statement in module.Body)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResolveDeclaration(statement);
        }

        _logger.LogInfo($"Completed name resolution pass 1 ({module.Body.Length} statements processed)");
    }

    /// <summary>
    /// Resolve inheritance relationships (second pass: after all types are declared)
    /// </summary>
    public void ResolveInheritance(CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Starting name resolution pass 2: Inheritance relationships");

        // Every definition re-stamps the file it was declared in before it is resolved, exactly as
        // the module scope is re-entered around it (#1369). Pass 2 runs on a resolver that may hold
        // definitions from many files — ProjectCompiler's aggregate does — and AddError reads
        // _currentFilePath, so without this a cross-file inheritance error reaches the project bag
        // with no file to point at. Restored afterwards so a single-file resolver, which stamps
        // itself once and resolves its own definitions, is left exactly as it was found.
        var previousFilePath = _currentFilePath;
        try
        {
            foreach (var (classDef, symbol, modulePath, filePath) in _classDefs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _currentFilePath = filePath;
                if (modulePath != null)
                    _symbolTable.EnterModuleScope(modulePath);
                try
                {
                    ResolveClassInheritance(classDef, symbol);
                }
                finally
                {
                    if (modulePath != null)
                        _symbolTable.ExitScope();
                }
            }

            foreach (var (structDef, symbol, modulePath, filePath) in _structDefs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _currentFilePath = filePath;
                if (modulePath != null)
                    _symbolTable.EnterModuleScope(modulePath);
                try
                {
                    ResolveStructInheritance(structDef, symbol);
                }
                finally
                {
                    if (modulePath != null)
                        _symbolTable.ExitScope();
                }
            }

            foreach (var (interfaceDef, symbol, modulePath, filePath) in _interfaceDefs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _currentFilePath = filePath;
                if (modulePath != null)
                    _symbolTable.EnterModuleScope(modulePath);
                try
                {
                    ResolveInterfaceInheritance(interfaceDef, symbol);
                }
                finally
                {
                    if (modulePath != null)
                        _symbolTable.ExitScope();
                }
            }

            DetectCircularInheritance();
        }
        finally
        {
            _currentFilePath = previousFilePath;
        }

        var totalTypes = _classDefs.Count + _structDefs.Count + _interfaceDefs.Count;
        _logger.LogInfo($"Completed name resolution pass 2 ({totalTypes} types processed)");
    }

    /// <summary>
    /// Cycle detection, run as the tail of <see cref="ResolveInheritance"/>. Its diagnostics are
    /// per-definition too, so it re-stamps the declaring file the same way (#1369); the caller
    /// restores <c>_currentFilePath</c> around the whole pass.
    /// </summary>
    private void DetectCircularInheritance()
    {
        // Check class base-type chains for cycles
        foreach (var (classDef, symbol, modulePath, filePath) in _classDefs)
        {
            _currentFilePath = filePath;
            if (modulePath != null)
                _symbolTable.EnterModuleScope(modulePath);
            try
            {
                var typeSymbol = symbol;

                var visited = new HashSet<string>();
                var current = typeSymbol;
                while (current != null)
                {
                    if (!visited.Add(current.Name))
                    {
                        // Found a cycle - build the chain for the error message
                        var chain = string.Join(" -> ", visited) + " -> " + current.Name;
                        AddError($"Circular inheritance detected: {chain}",
                            classDef.LineStart, classDef.ColumnStart,
                            code: DiagnosticCodes.Semantic.CircularInheritance, span: classDef.Span);
                        break;
                    }
                    current = _semanticBinding.GetBaseType(current);
                }
            }
            finally
            {
                if (modulePath != null)
                    _symbolTable.ExitScope();
            }
        }

        // Check struct base-type chains for cycles (structs only implement interfaces)
        foreach (var (structDef, symbol, modulePath, filePath) in _structDefs)
        {
            _currentFilePath = filePath;
            if (modulePath != null)
                _symbolTable.EnterModuleScope(modulePath);
            try
            {
                var typeSymbol = symbol;

                DetectInterfaceCycleForType(typeSymbol, structDef.LineStart, structDef.ColumnStart, structDef.Span);
            }
            finally
            {
                if (modulePath != null)
                    _symbolTable.ExitScope();
            }
        }

        // Check interface chains for cycles
        foreach (var (interfaceDef, symbol, modulePath, filePath) in _interfaceDefs)
        {
            _currentFilePath = filePath;
            if (modulePath != null)
                _symbolTable.EnterModuleScope(modulePath);
            try
            {
                var typeSymbol = symbol;

                DetectInterfaceCycle(typeSymbol, interfaceDef);
            }
            finally
            {
                if (modulePath != null)
                    _symbolTable.ExitScope();
            }
        }
    }

    private void DetectInterfaceCycle(TypeSymbol startSymbol, InterfaceDef interfaceDef)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<TypeSymbol>();
        queue.Enqueue(startSymbol);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current.Name))
            {
                if (current.Name == startSymbol.Name)
                {
                    AddError($"Circular inheritance detected: interface '{startSymbol.Name}' inherits from itself through its base interfaces",
                        interfaceDef.LineStart, interfaceDef.ColumnStart,
                        code: DiagnosticCodes.Semantic.CircularInheritance, span: interfaceDef.Span);
                }
                continue;
            }

            foreach (var iface in TypeHierarchyService.GetDirectInterfaces(current, _semanticBinding))
            {
                queue.Enqueue(iface);
            }
        }
    }

    private void DetectInterfaceCycleForType(TypeSymbol startSymbol, int? line, int? column, Text.TextSpan? span)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<TypeSymbol>();
        queue.Enqueue(startSymbol);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current.Name))
            {
                if (current.Name == startSymbol.Name)
                {
                    AddError($"Circular inheritance detected: type '{startSymbol.Name}' has a circular interface chain",
                        line, column,
                        code: DiagnosticCodes.Semantic.CircularInheritance, span: span);
                }
                continue;
            }

            foreach (var iface in TypeHierarchyService.GetDirectInterfaces(current, _semanticBinding))
            {
                queue.Enqueue(iface);
            }
        }
    }

    private AccessLevel DetermineAccessLevel(string name)
    {
        return AccessLevelConventions.FromName(name);
    }

    private void ValidateInterfaceMethod(FunctionDef method, string interfaceName)
    {
        // Interface methods can have:
        // 1. ... (ellipsis) or pass -> abstract (no C# body)
        // 2. A real body -> default implementation (C# 8.0+ default interface method)

        if (method.Body.Length == 0)
        {
            AddError($"Interface method '{method.Name}' in interface '{interfaceName}' must have a body with '...' or 'pass'",
                method.LineStart, method.ColumnStart, code: DiagnosticCodes.Semantic.InterfaceMethodBody, span: method.Span);
        }

        // Any non-empty body is now valid -- either abstract (ellipsis/pass) or default implementation
    }

    private void AddError(string message, int? line = null, int? column = null, string? code = null,
        Text.TextSpan? span = null)
    {
        _diagnostics.AddPhaseError(message, CompilerPhase.NameResolution,
            span, line, column, _currentFilePath, code, _logger);
    }

    private void ResolveClassInheritance(ClassDef classDef, TypeSymbol typeSymbol)
    {
        if (classDef.BaseClasses.Length == 0)
            return;

        // Process all base classes
        // First class (if present) becomes BaseType, all interfaces go to Interfaces list
        bool hasSetBaseType = false;

        foreach (var baseAnnot in classDef.BaseClasses)
        {
            var baseSymbol = ResolveBaseReference(baseAnnot, out var rawSymbol, resolvingType: typeSymbol);
            if (baseSymbol == null)
            {
                if (rawSymbol?.IsErrorRecovery == true)
                {
                    continue;
                }

                AddError($"Base type '{baseAnnot.Name}' not found",
                    classDef.LineStart, classDef.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedType, span: classDef.Span);
                continue;
            }

            if (baseSymbol.TypeKind != TypeKind.Class && baseSymbol.TypeKind != TypeKind.Interface)
            {
                AddError($"'{baseAnnot.Name}' is not a class or interface",
                    classDef.LineStart, classDef.ColumnStart, code: DiagnosticCodes.Semantic.InvalidInheritance, span: classDef.Span);
                continue;
            }

            if (!TryCompleteBaseReferenceArguments(baseAnnot, baseSymbol, out var baseTypeArgs))
                continue;

            if (baseSymbol.TypeKind == TypeKind.Class)
            {
                if (hasSetBaseType)
                {
                    AddError($"Class '{classDef.Name}' cannot have multiple base classes (only one class inheritance allowed)",
                        classDef.LineStart, classDef.ColumnStart, code: DiagnosticCodes.Semantic.InvalidInheritance, span: classDef.Span);
                    continue;
                }
                _semanticBinding.SetBaseType(typeSymbol, baseSymbol);
                _semanticBinding.SetBaseTypeReference(typeSymbol, new BaseTypeReference
                {
                    Definition = baseSymbol,
                    TypeArgAnnotations = baseTypeArgs,
                    SourceAnnotation = baseAnnot
                });
                hasSetBaseType = true;

                if (IsSourceGeneratorType(baseSymbol))
                {
                    typeSymbol.IsSourceGenerator = true;
                }

            }
            else // TypeKind.Interface
            {
                _semanticBinding.AddInterface(typeSymbol, new InterfaceReference
                {
                    Definition = baseSymbol,
                    TypeArgAnnotations = baseTypeArgs,
                    SourceAnnotation = baseAnnot
                });
            }
        }
    }

    private void ResolveStructInheritance(StructDef structDef, TypeSymbol typeSymbol)
    {
        if (structDef.BaseClasses.Length == 0)
            return;

        // Structs can only implement interfaces
        foreach (var baseAnnot in structDef.BaseClasses)
        {
            var interfaceSymbol = ResolveBaseReference(baseAnnot, out var rawSymbol, resolvingType: typeSymbol);
            if (interfaceSymbol == null)
            {
                if (rawSymbol?.IsErrorRecovery == true)
                {
                    continue;
                }

                AddError($"Interface '{baseAnnot.Name}' not found",
                    structDef.LineStart, structDef.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedType, span: structDef.Span);
                continue;
            }

            if (interfaceSymbol.TypeKind != TypeKind.Interface)
            {
                AddError($"Structs can only implement interfaces, '{baseAnnot.Name}' is not an interface",
                    structDef.LineStart, structDef.ColumnStart, code: DiagnosticCodes.Semantic.InvalidInheritance, span: structDef.Span);
                continue;
            }

            if (!TryCompleteBaseReferenceArguments(baseAnnot, interfaceSymbol, out var interfaceTypeArgs))
                continue;

            _semanticBinding.AddInterface(typeSymbol, new InterfaceReference
            {
                Definition = interfaceSymbol,
                TypeArgAnnotations = interfaceTypeArgs,
                SourceAnnotation = baseAnnot
            });
        }
    }

    private void ResolveInterfaceInheritance(InterfaceDef interfaceDef, TypeSymbol typeSymbol)
    {
        if (interfaceDef.BaseInterfaces.Length == 0)
            return;

        // Interfaces can extend other interfaces
        foreach (var baseAnnot in interfaceDef.BaseInterfaces)
        {
            var baseInterfaceSymbol = ResolveBaseReference(baseAnnot, out var rawSymbol, resolvingType: typeSymbol);
            if (baseInterfaceSymbol == null)
            {
                if (rawSymbol?.IsErrorRecovery == true)
                {
                    continue;
                }

                AddError($"Interface '{baseAnnot.Name}' not found",
                    interfaceDef.LineStart, interfaceDef.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedType, span: interfaceDef.Span);
                continue;
            }

            if (baseInterfaceSymbol.TypeKind != TypeKind.Interface)
            {
                AddError($"'{baseAnnot.Name}' is not an interface",
                    interfaceDef.LineStart, interfaceDef.ColumnStart, code: DiagnosticCodes.Semantic.InvalidInheritance, span: interfaceDef.Span);
                continue;
            }

            if (!TryCompleteBaseReferenceArguments(baseAnnot, baseInterfaceSymbol, out var baseInterfaceTypeArgs))
                continue;

            _semanticBinding.AddInterface(typeSymbol, new InterfaceReference
            {
                Definition = baseInterfaceSymbol,
                TypeArgAnnotations = baseInterfaceTypeArgs,
                SourceAnnotation = baseAnnot
            });
        }

        // Propagate inherited methods from base interfaces
        PropagateInterfaceMethods(typeSymbol);
    }

    /// <summary>
    /// Resolves a base-list reference to its symbol, honoring the backtick escape both ways
    /// (#1325) by IDENTITY, not flag equality: an escaped spelling never binds the registry's
    /// own builtin symbol, and a bare spelling never binds an escape-DECLARED user type (it
    /// falls back to the registry instead). An escaped spelling binding a bare-declared
    /// user/import symbol is quoting (#713's interop imports) and stands.
    /// </summary>
    private TypeSymbol? ResolveBaseReference(TypeAnnotation baseAnnot, out Symbol? rawSymbol,
        TypeSymbol? resolvingType = null)
    {
        rawSymbol = _symbolTable.Lookup(baseAnnot.Name);

        // For nested types, the base name may be a sibling nested type whose class scope
        // is transient and gone by pass 2. Walk the enclosing chain's NestedTypes
        // (innermost first — an inner name shadows an outer one, the #1371 rule).
        if (rawSymbol == null && resolvingType?.DeclaringType != null
            && !baseAnnot.Name.Contains('.', StringComparison.Ordinal))
        {
            var enclosing = resolvingType.DeclaringType;
            while (enclosing != null)
            {
                var nested = enclosing.NestedTypes.FirstOrDefault(n => n.Name == baseAnnot.Name);
                if (nested != null)
                {
                    rawSymbol = nested;
                    break;
                }
                enclosing = enclosing.DeclaringType;
            }
        }

        if (rawSymbol != null)
        {
            if (baseAnnot.IsNameBacktickEscaped && _symbolTable.BuiltinRegistry.IsBuiltinSymbol(rawSymbol))
                rawSymbol = null;
            else if (!baseAnnot.IsNameBacktickEscaped && rawSymbol.IsNameBacktickEscaped)
                rawSymbol = _symbolTable.BuiltinRegistry.GetType(baseAnnot.Name);
        }

        if (rawSymbol is TypeSymbol ts)
            return ts;

        if (baseAnnot.IsNameBacktickEscaped)
            return null;

        // Dotted base name: try the module-qualified path first, then the nested-type path
        // (e.g., `class Dog(Animal.Speakable)` where Animal is a class, not a module).
        var moduleResult = LookupModuleQualifiedType(baseAnnot.Name);
        if (moduleResult != null)
            return moduleResult;

        if (baseAnnot.Name.Contains('.', StringComparison.Ordinal))
        {
            var parts = baseAnnot.Name.Split('.');
            if (_symbolTable.Lookup(parts[0]) is TypeSymbol outerType)
            {
                var current = outerType;
                for (int i = 1; i < parts.Length && current != null; i++)
                    current = current.NestedTypes.FirstOrDefault(n => n.Name == parts[i]);
                if (current != null)
                    return current;
            }
        }

        return null;
    }

    /// <summary>
    /// Base-list arity (#1286) and the PEP-696 defaults fill (#1404). A generic base class or
    /// interface must reach the reference channels with exactly its declared number of type
    /// arguments: nothing downstream can repair a mismatch — the <see cref="InterfaceReference"/>
    /// and <see cref="BaseTypeReference"/> carry the annotations,
    /// <see cref="GenericInstantiationWalker"/> skips references it cannot read by design, and the
    /// emitter writes the base list from the annotation — so an unfilled reference reaches Roslyn
    /// as CS0305 through the SPY0908 net. The refusal wording is the arity authority's own
    /// (<c>TypeResolver.ResolveTypeAnnotation</c>).
    ///
    /// <para>A trailing run of parameters that all carry defaults (#1245) is FILLED here rather
    /// than refused, so a base-list position means what an annotation position means (#1331):
    /// <c>class Child(Box)</c> over <c>class Box[T = int]</c> is <c>Box[int]</c>. The refusal
    /// survives untouched for a parameter with NO default, which is the case the #1286 control
    /// pins. The fill is materialized by returning the completed annotation vector into the
    /// caller's reference — the earlier "nowhere to materialize" note predates
    /// <see cref="BaseTypeReference"/> (#1287), which gave the class arm the same two-channel
    /// carrier the interface arm already had.</para>
    /// </summary>
    /// <param name="typeArgAnnotations">
    /// The effective type arguments for the reference: the written ones, extended with each
    /// missing parameter's default. Always the written vector when no fill was needed.
    /// </param>
    private bool TryCompleteBaseReferenceArguments(
        TypeAnnotation baseAnnot,
        TypeSymbol baseSymbol,
        out ImmutableArray<TypeAnnotation> typeArgAnnotations)
    {
        typeArgAnnotations = baseAnnot.TypeArguments;

        if (!baseSymbol.IsGeneric || baseAnnot.TypeArguments.Length == baseSymbol.TypeParameters.Count)
            return true;

        if (baseAnnot.TypeArguments.Length < baseSymbol.TypeParameters.Count
            && baseSymbol.TypeParameters.Skip(baseAnnot.TypeArguments.Length).All(tp => tp.DefaultType != null))
        {
            typeArgAnnotations = FillTypeParameterDefaults(baseAnnot, baseSymbol);
            return true;
        }

        AddError(
            $"Type '{baseAnnot.Name}' expects {baseSymbol.TypeParameters.Count} type arguments but got {baseAnnot.TypeArguments.Length}",
            baseAnnot.LineStart, baseAnnot.ColumnStart,
            code: DiagnosticCodes.Semantic.WrongArgumentCount, span: baseAnnot.Span);
        return false;
    }

    /// <summary>
    /// Completes a base-list reference's type arguments from the definition's PEP-696 defaults.
    ///
    /// <para>The fill runs left to right so a default may be written in terms of the parameters
    /// declared BEFORE it — <c>class Dup[K, V = K]</c> written as <c>Dup[str]</c> is
    /// <c>Dup[str, str]</c>, the same answer <c>TypeResolver.ResolveTypeParameterDefault</c> gives
    /// in an annotation position. This arm substitutes on the ANNOTATION rather than on the
    /// resolved type because it runs in Pass 1, before any type resolution: appending the default
    /// verbatim would park the base's own parameter name in the DERIVED class's scope, where it
    /// means something else or nothing at all.</para>
    /// </summary>
    private static ImmutableArray<TypeAnnotation> FillTypeParameterDefaults(
        TypeAnnotation baseAnnot, TypeSymbol baseSymbol)
    {
        var filled = ImmutableArray.CreateBuilder<TypeAnnotation>(baseSymbol.TypeParameters.Count);
        filled.AddRange(baseAnnot.TypeArguments);

        var bound = new Dictionary<string, TypeAnnotation>(StringComparer.Ordinal);
        for (int i = 0; i < baseSymbol.TypeParameters.Count; i++)
        {
            var parameter = baseSymbol.TypeParameters[i];
            if (i >= filled.Count)
                filled.Add(SubstituteTypeParameterNames(parameter.DefaultType!, bound));
            bound[parameter.Name] = filled[i];
        }

        return filled.ToImmutable();
    }

    /// <summary>
    /// Rewrites the base definition's type-parameter names inside a default annotation to the
    /// arguments this reference supplies for them. A backtick-escaped spelling denotes the user's
    /// own type, never a parameter, so it is left alone (#1325).
    /// </summary>
    private static TypeAnnotation SubstituteTypeParameterNames(
        TypeAnnotation annotation, Dictionary<string, TypeAnnotation> bound)
    {
        if (bound.Count == 0)
            return annotation;

        if (!annotation.IsNameBacktickEscaped
            && annotation.TypeArguments.IsEmpty
            && annotation.ErrorType == null
            && bound.TryGetValue(annotation.Name, out var argument))
        {
            // `V = K?` under `Dup[str]` is `str?`: the default's own modifiers ride along.
            return argument with
            {
                IsOptional = argument.IsOptional || annotation.IsOptional,
                IsCSharpNullable = argument.IsCSharpNullable || annotation.IsCSharpNullable
            };
        }

        var rewrittenError = annotation.ErrorType == null
            ? null
            : SubstituteTypeParameterNames(annotation.ErrorType, bound);
        var rewrittenArgs = annotation.TypeArguments.IsEmpty
            ? annotation.TypeArguments
            : annotation.TypeArguments.Select(a => SubstituteTypeParameterNames(a, bound)).ToImmutableArray();

        if (ReferenceEquals(rewrittenError, annotation.ErrorType) && rewrittenArgs.SequenceEqual(annotation.TypeArguments))
            return annotation;

        return annotation with { TypeArguments = rewrittenArgs, ErrorType = rewrittenError };
    }

    /// <summary>
    /// Propagate methods from base interfaces to the derived interface.
    /// Uses BFS to handle multi-level interface inheritance.
    /// </summary>
    private void PropagateInterfaceMethods(TypeSymbol interfaceSymbol)
    {
        // Build a set of method signatures we already have
        var seenMethods = new HashSet<string>(
            interfaceSymbol.Methods.Select(m => GetMethodSignature(m)));

        var visited = new HashSet<string> { interfaceSymbol.Name };
        var queue = new Queue<TypeSymbol>(TypeHierarchyService.GetDirectInterfaces(interfaceSymbol, _semanticBinding));

        while (queue.Count > 0)
        {
            var baseInterface = queue.Dequeue();
            if (!visited.Add(baseInterface.Name))
                continue;

            // Copy methods from base interface that we don't already have
            foreach (var method in baseInterface.Methods)
            {
                var signature = GetMethodSignature(method);
                if (seenMethods.Add(signature))
                {
                    // Add a reference to the inherited method (don't clone, just add reference)
                    // The method is marked as coming from the base interface by keeping original line info
                    interfaceSymbol.Methods.Add(method);
                }
            }

            // Add base interface's bases to the queue
            foreach (var grandBase in TypeHierarchyService.GetDirectInterfaces(baseInterface, _semanticBinding))
            {
                queue.Enqueue(grandBase);
            }
        }
    }

    /// <summary>
    /// Get a unique signature string for method deduplication.
    /// Includes method name and parameter types (excluding 'self').
    /// </summary>
    private string GetMethodSignature(FunctionSymbol method)
    {
        var paramTypes = method.Parameters
            .Where(p => p.Name != PythonNames.Self)
            .Select(p => p.Type?.GetDisplayName() ?? "unknown");
        return $"{method.Name}({string.Join(",", paramTypes)})";
    }

    internal static string? GetDeprecationMessage(IEnumerable<Decorator> decorators)
    {
        var deprecated = decorators.FirstOrDefault(d => d.Name == DecoratorNames.Deprecated);
        if (deprecated != null && deprecated.Arguments.Length > 0 && deprecated.Arguments[0] is StringLiteral msg)
            return msg.Value;
        return null;
    }

    /// <summary>
    /// True when <paramref name="decorators"/> includes '@must_use' (#1022). Marks a function or
    /// type whose produced value must not be silently discarded (enforced by MustUseValidator).
    /// </summary>
    internal static bool HasMustUse(IEnumerable<Decorator> decorators)
        => decorators.Any(d => d.Name == DecoratorNames.MustUse);

    /// <summary>
    /// Whether an enum is string-backed: at least one member carries a string value (#1284). A
    /// string enum is emitted as a sealed class of singleton instances with an implicit conversion
    /// to <c>string</c>, not as a C# enum, so <c>.value</c> typing, <c>str</c> assignability and
    /// iteration all branch on it.
    /// </summary>
    /// <remarks>
    /// One predicate for three readers — this pass, <c>CodeGenInfoComputer.ProcessEnumDef</c>, and
    /// <c>ModuleLoader.ExtractFullEnumSymbol</c>. The first two held byte-identical copies and the
    /// third had none at all, so an imported string enum was checked as an int enum (#1442). A rule
    /// that decides both the checker's answers and the emitted shape cannot be spelled three times.
    /// </remarks>
    internal static bool IsStringEnum(EnumDef enumDef)
        => enumDef.Members.Any(m => m.Value is StringLiteral);

    private static bool IsSourceGeneratorType(TypeSymbol symbol)
    {
        if (symbol.ClrType != null)
            return typeof(SharpyRT::Sharpy.Generators.SourceGenerator).IsAssignableFrom(symbol.ClrType);

        return symbol.Name == "SourceGenerator" && symbol.IsSourceGenerator;
    }

    private TypeSymbol? LookupModuleQualifiedType(string dottedName)
    {
        if (!dottedName.Contains('.', StringComparison.Ordinal))
            return null;

        var parts = dottedName.Split('.');

        if (_symbolTable.Lookup(parts[0]) is not ModuleSymbol moduleSymbol)
            return null;

        return moduleSymbol.ResolveQualifiedType(parts, startIndex: 1);
    }
}
