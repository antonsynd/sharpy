extern alias SharpyRT;
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
    private readonly List<(ClassDef Def, string? ModulePath)> _classDefs = new();
    private readonly List<(StructDef Def, string? ModulePath)> _structDefs = new();
    private readonly List<(InterfaceDef Def, string? ModulePath)> _interfaceDefs = new();
    private string? _currentFilePath;
    private string? _currentModulePath;

    public NameResolver(SymbolTable symbolTable, ICompilerLogger? logger = null, SemanticBinding? semanticBinding = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
        _semanticBinding = semanticBinding ?? new SemanticBinding();
    }

    public DiagnosticBag Diagnostics => _diagnostics;

    public IReadOnlyList<(ClassDef Def, string? ModulePath)> ClassDefs => _classDefs;
    public IReadOnlyList<(StructDef Def, string? ModulePath)> StructDefs => _structDefs;
    public IReadOnlyList<(InterfaceDef Def, string? ModulePath)> InterfaceDefs => _interfaceDefs;

    /// <summary>
    /// Aggregates type definition lists from per-file resolvers into this resolver.
    /// Used to prepare a merged NameResolver for inheritance resolution after
    /// per-file name resolution and symbol table merge.
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

        foreach (var (classDef, modulePath) in _classDefs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (modulePath != null)
                _symbolTable.EnterModuleScope(modulePath);
            try
            {
                ResolveClassInheritance(classDef);
            }
            finally
            {
                if (modulePath != null)
                    _symbolTable.ExitScope();
            }
        }

        foreach (var (structDef, modulePath) in _structDefs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (modulePath != null)
                _symbolTable.EnterModuleScope(modulePath);
            try
            {
                ResolveStructInheritance(structDef);
            }
            finally
            {
                if (modulePath != null)
                    _symbolTable.ExitScope();
            }
        }

        foreach (var (interfaceDef, modulePath) in _interfaceDefs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (modulePath != null)
                _symbolTable.EnterModuleScope(modulePath);
            try
            {
                ResolveInterfaceInheritance(interfaceDef);
            }
            finally
            {
                if (modulePath != null)
                    _symbolTable.ExitScope();
            }
        }

        DetectCircularInheritance();

        var totalTypes = _classDefs.Count + _structDefs.Count + _interfaceDefs.Count;
        _logger.LogInfo($"Completed name resolution pass 2 ({totalTypes} types processed)");
    }

    private void DetectCircularInheritance()
    {
        // Check class base-type chains for cycles
        foreach (var (classDef, modulePath) in _classDefs)
        {
            if (modulePath != null)
                _symbolTable.EnterModuleScope(modulePath);
            try
            {
                var typeSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
                if (typeSymbol == null)
                    continue;

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
        foreach (var (structDef, modulePath) in _structDefs)
        {
            if (modulePath != null)
                _symbolTable.EnterModuleScope(modulePath);
            try
            {
                var typeSymbol = _symbolTable.Lookup(structDef.Name) as TypeSymbol;
                if (typeSymbol == null)
                    continue;

                DetectInterfaceCycleForType(typeSymbol, structDef.LineStart, structDef.ColumnStart, structDef.Span);
            }
            finally
            {
                if (modulePath != null)
                    _symbolTable.ExitScope();
            }
        }

        // Check interface chains for cycles
        foreach (var (interfaceDef, modulePath) in _interfaceDefs)
        {
            if (modulePath != null)
                _symbolTable.EnterModuleScope(modulePath);
            try
            {
                var typeSymbol = _symbolTable.Lookup(interfaceDef.Name) as TypeSymbol;
                if (typeSymbol == null)
                    continue;

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

    private void ResolveClassInheritance(ClassDef classDef)
    {
        if (classDef.BaseClasses.Length == 0)
            return;

        var typeSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
        if (typeSymbol == null)
            return;

        // Process all base classes
        // First class (if present) becomes BaseType, all interfaces go to Interfaces list
        bool hasSetBaseType = false;

        foreach (var baseAnnot in classDef.BaseClasses)
        {
            var rawSymbol = _symbolTable.Lookup(baseAnnot.Name);
            var baseSymbol = rawSymbol as TypeSymbol
                ?? LookupModuleQualifiedType(baseAnnot.Name);
            if (baseSymbol == null)
            {
                // Check if this is an error recovery symbol (from a failed import).
                // If so, suppress this error - the import error was already reported.
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

            if (baseSymbol.TypeKind == TypeKind.Class)
            {
                // Only one base class allowed (C# single inheritance)
                if (hasSetBaseType)
                {
                    AddError($"Class '{classDef.Name}' cannot have multiple base classes (only one class inheritance allowed)",
                        classDef.LineStart, classDef.ColumnStart, code: DiagnosticCodes.Semantic.InvalidInheritance, span: classDef.Span);
                    continue;
                }
                _semanticBinding.SetBaseType(typeSymbol, baseSymbol);
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
                    TypeArgAnnotations = baseAnnot.TypeArguments
                });
            }
        }
    }

    private void ResolveStructInheritance(StructDef structDef)
    {
        if (structDef.BaseClasses.Length == 0)
            return;

        var typeSymbol = _symbolTable.Lookup(structDef.Name) as TypeSymbol;
        if (typeSymbol == null)
            return;

        // Structs can only implement interfaces
        foreach (var baseAnnot in structDef.BaseClasses)
        {
            var rawSymbol = _symbolTable.Lookup(baseAnnot.Name);
            var interfaceSymbol = rawSymbol as TypeSymbol
                ?? LookupModuleQualifiedType(baseAnnot.Name);
            if (interfaceSymbol == null)
            {
                // Check if this is an error recovery symbol (from a failed import).
                // If so, suppress this error - the import error was already reported.
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

            _semanticBinding.AddInterface(typeSymbol, new InterfaceReference
            {
                Definition = interfaceSymbol,
                TypeArgAnnotations = baseAnnot.TypeArguments
            });
        }
    }

    private void ResolveInterfaceInheritance(InterfaceDef interfaceDef)
    {
        if (interfaceDef.BaseInterfaces.Length == 0)
            return;

        var typeSymbol = _symbolTable.Lookup(interfaceDef.Name) as TypeSymbol;
        if (typeSymbol == null)
            return;

        // Interfaces can extend other interfaces
        foreach (var baseAnnot in interfaceDef.BaseInterfaces)
        {
            var rawSymbol = _symbolTable.Lookup(baseAnnot.Name);
            var baseInterfaceSymbol = rawSymbol as TypeSymbol
                ?? LookupModuleQualifiedType(baseAnnot.Name);
            if (baseInterfaceSymbol == null)
            {
                // Check if this is an error recovery symbol (from a failed import).
                // If so, suppress this error - the import error was already reported.
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

            _semanticBinding.AddInterface(typeSymbol, new InterfaceReference
            {
                Definition = baseInterfaceSymbol,
                TypeArgAnnotations = baseAnnot.TypeArguments
            });
        }

        // Propagate inherited methods from base interfaces
        PropagateInterfaceMethods(typeSymbol);
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
