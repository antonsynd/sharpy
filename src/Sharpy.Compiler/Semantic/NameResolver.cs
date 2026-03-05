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
    private readonly List<ClassDef> _classDefs = new();
    private readonly List<StructDef> _structDefs = new();
    private readonly List<InterfaceDef> _interfaceDefs = new();
    private string? _currentFilePath;

    public NameResolver(SymbolTable symbolTable, ICompilerLogger? logger = null, SemanticBinding? semanticBinding = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
        _semanticBinding = semanticBinding ?? new SemanticBinding();
    }

    private IReadOnlyList<TypeSymbol> GetInterfaces(TypeSymbol symbol)
    {
        var refs = _semanticBinding.GetInterfaces(symbol);
        if (refs != null)
            return refs.Select(r => r.Definition).ToList();
        return symbol.Interfaces.Select(r => r.Definition).ToList();
    }

    public DiagnosticBag Diagnostics => _diagnostics;

    /// <summary>
    /// Set the current source file path for tracking type definitions.
    /// </summary>
    public void SetCurrentFilePath(string? filePath)
    {
        _currentFilePath = filePath;
    }

    /// <summary>
    /// Resolve names in a module (first pass: declarations only)
    /// </summary>
    public void ResolveDeclarations(Module module, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Starting name resolution pass 1: Declarations");

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

        foreach (var classDef in _classDefs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResolveClassInheritance(classDef);
        }

        foreach (var structDef in _structDefs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResolveStructInheritance(structDef);
        }

        foreach (var interfaceDef in _interfaceDefs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResolveInterfaceInheritance(interfaceDef);
        }

        DetectCircularInheritance();

        var totalTypes = _classDefs.Count + _structDefs.Count + _interfaceDefs.Count;
        _logger.LogInfo($"Completed name resolution pass 2 ({totalTypes} types processed)");
    }

    private void DetectCircularInheritance()
    {
        // Check class base-type chains for cycles
        foreach (var classDef in _classDefs)
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

        // Check struct base-type chains for cycles (structs only implement interfaces)
        foreach (var structDef in _structDefs)
        {
            var typeSymbol = _symbolTable.Lookup(structDef.Name) as TypeSymbol;
            if (typeSymbol == null)
                continue;

            DetectInterfaceCycleForType(typeSymbol, structDef.LineStart, structDef.ColumnStart, structDef.Span);
        }

        // Check interface chains for cycles
        foreach (var interfaceDef in _interfaceDefs)
        {
            var typeSymbol = _symbolTable.Lookup(interfaceDef.Name) as TypeSymbol;
            if (typeSymbol == null)
                continue;

            DetectInterfaceCycle(typeSymbol, interfaceDef);
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

            foreach (var iface in GetInterfaces(current))
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

            foreach (var iface in GetInterfaces(current))
            {
                queue.Enqueue(iface);
            }
        }
    }

    private AccessLevel DetermineAccessLevel(string name)
    {
        // Python naming conventions:
        // __name__ (dunder methods) = public (special methods)
        // __name (but not __name__) = private (name mangling)
        // _name = protected
        // name = public
        if (name.StartsWith("__") && name.EndsWith("__"))
            return AccessLevel.Public; // Special methods like __init__, __str__
        if (name.StartsWith("__") && !name.EndsWith("__"))
            return AccessLevel.Private;
        if (name.StartsWith("_"))
            return AccessLevel.Protected;
        return AccessLevel.Public;
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
        _diagnostics.AddError(message, span, line, column, _currentFilePath, code: code, phase: CompilerPhase.NameResolution);
        _logger.LogError(message, line ?? 0, column ?? 0);
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
            var baseSymbol = rawSymbol as TypeSymbol;
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
            var interfaceSymbol = rawSymbol as TypeSymbol;
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
            var baseInterfaceSymbol = rawSymbol as TypeSymbol;
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
        var queue = new Queue<TypeSymbol>(GetInterfaces(interfaceSymbol));

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
            foreach (var grandBase in GetInterfaces(baseInterface))
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
}
