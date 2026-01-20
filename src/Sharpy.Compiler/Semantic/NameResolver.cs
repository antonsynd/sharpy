using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// First pass: Resolve all names and build symbol tables
/// </summary>
public class NameResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();
    private readonly List<ClassDef> _classDefs = new();
    private readonly List<StructDef> _structDefs = new();
    private readonly List<InterfaceDef> _interfaceDefs = new();
    private string? _currentFilePath;

    public NameResolver(SymbolTable symbolTable, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

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
    public void ResolveDeclarations(Module module)
    {
        _logger.LogInfo("Name resolution pass 1: Declarations in module");

        foreach (var statement in module.Body)
        {
            ResolveDeclaration(statement);
        }
    }

    /// <summary>
    /// Resolve inheritance relationships (second pass: after all types are declared)
    /// </summary>
    public void ResolveInheritance()
    {
        _logger.LogInfo("Name resolution pass 2: Inheritance relationships");

        foreach (var classDef in _classDefs)
        {
            ResolveClassInheritance(classDef);
        }

        foreach (var structDef in _structDefs)
        {
            ResolveStructInheritance(structDef);
        }

        foreach (var interfaceDef in _interfaceDefs)
        {
            ResolveInterfaceInheritance(interfaceDef);
        }
    }

    private void ResolveDeclaration(Statement statement)
    {
        switch (statement)
        {
            case ClassDef classDef:
                ResolveClassDeclaration(classDef);
                break;

            case StructDef structDef:
                ResolveStructDeclaration(structDef);
                break;

            case InterfaceDef interfaceDef:
                ResolveInterfaceDeclaration(interfaceDef);
                break;

            case EnumDef enumDef:
                ResolveEnumDeclaration(enumDef);
                break;

            case FunctionDef functionDef:
                ResolveFunctionDeclaration(functionDef);
                break;

            case VariableDeclaration varDecl when varDecl.IsConst:
                ResolveConstantDeclaration(varDecl);
                break;

            case ImportStatement import:
                ResolveImport(import);
                break;

            case FromImportStatement fromImport:
                ResolveFromImport(fromImport);
                break;

            case TypeAlias typeAlias:
                ResolveTypeAliasDeclaration(typeAlias);
                break;

                // Other statements are handled in later passes
        }
    }

    private void ResolveClassDeclaration(ClassDef classDef)
    {
        _logger.LogDebug($"Resolving class declaration: {classDef.Name}");

        // Check for redefinition
        if (_symbolTable.Lookup(classDef.Name, searchParents: false) != null)
        {
            AddError($"Class '{classDef.Name}' is already defined",
                classDef.LineStart, classDef.ColumnStart);
            return;
        }

        // Check for @abstract decorator
        bool isAbstract = classDef.Decorators.Any(d => d.Name == "abstract");

        // Create type symbol
        var typeSymbol = new TypeSymbol
        {
            Name = classDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            TypeParameters = classDef.TypeParameters.ToList(),
            IsAbstract = isAbstract,
            DefiningFilePath = _currentFilePath,
            DeclarationLine = classDef.LineStart,
            DeclarationColumn = classDef.ColumnStart
        };

        // Define in current scope
        _symbolTable.Define(typeSymbol);

        // Store for second pass (inheritance resolution)
        _classDefs.Add(classDef);

        // Enter class scope to resolve members
        _symbolTable.EnterScope($"class:{classDef.Name}");

        // Register type parameters in the scope so they can be resolved in field/method types
        foreach (var typeParam in classDef.TypeParameters)
        {
            var typeParamSymbol = new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                DeclaringType = typeSymbol,
                DeclarationLine = classDef.LineStart,
                DeclarationColumn = classDef.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        foreach (var statement in classDef.Body)
        {
            if (statement is FunctionDef method)
            {
                ResolveMethodDeclaration(method, typeSymbol);
            }
            else if (statement is VariableDeclaration field)
            {
                ResolveFieldDeclaration(field, typeSymbol);
            }
        }

        _symbolTable.ExitScope();
    }

    private void ResolveStructDeclaration(StructDef structDef)
    {
        _logger.LogDebug($"Resolving struct declaration: {structDef.Name}");

        if (_symbolTable.Lookup(structDef.Name, searchParents: false) != null)
        {
            AddError($"Struct '{structDef.Name}' is already defined",
                structDef.LineStart, structDef.ColumnStart);
            return;
        }

        var typeSymbol = new TypeSymbol
        {
            Name = structDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Struct,
            AccessLevel = AccessLevel.Public,
            TypeParameters = structDef.TypeParameters.ToList(),
            DefiningFilePath = _currentFilePath,
            DeclarationLine = structDef.LineStart,
            DeclarationColumn = structDef.ColumnStart
        };

        _symbolTable.Define(typeSymbol);

        // Store for second pass (inheritance resolution)
        _structDefs.Add(structDef);

        _symbolTable.EnterScope($"struct:{structDef.Name}");

        // Register type parameters in the scope so they can be resolved in field/method types
        foreach (var typeParam in structDef.TypeParameters)
        {
            var typeParamSymbol = new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                DeclaringType = typeSymbol,
                DeclarationLine = structDef.LineStart,
                DeclarationColumn = structDef.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        foreach (var statement in structDef.Body)
        {
            if (statement is FunctionDef method)
            {
                ResolveMethodDeclaration(method, typeSymbol);
            }
            else if (statement is VariableDeclaration field)
            {
                ResolveFieldDeclaration(field, typeSymbol);
            }
        }

        _symbolTable.ExitScope();
    }

    private void ResolveInterfaceDeclaration(InterfaceDef interfaceDef)
    {
        _logger.LogDebug($"Resolving interface declaration: {interfaceDef.Name}");

        if (_symbolTable.Lookup(interfaceDef.Name, searchParents: false) != null)
        {
            AddError($"Interface '{interfaceDef.Name}' is already defined",
                interfaceDef.LineStart, interfaceDef.ColumnStart);
            return;
        }

        var typeSymbol = new TypeSymbol
        {
            Name = interfaceDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            AccessLevel = AccessLevel.Public,
            TypeParameters = interfaceDef.TypeParameters.ToList(),
            DefiningFilePath = _currentFilePath,
            DeclarationLine = interfaceDef.LineStart,
            DeclarationColumn = interfaceDef.ColumnStart
        };

        _symbolTable.Define(typeSymbol);

        // Store for second pass (inheritance resolution)
        _interfaceDefs.Add(interfaceDef);

        _symbolTable.EnterScope($"interface:{interfaceDef.Name}");

        // Register type parameters in the scope so they can be resolved in method signatures
        foreach (var typeParam in interfaceDef.TypeParameters)
        {
            var typeParamSymbol = new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                DeclaringType = typeSymbol,
                DeclarationLine = interfaceDef.LineStart,
                DeclarationColumn = interfaceDef.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        foreach (var statement in interfaceDef.Body)
        {
            if (statement is FunctionDef method)
            {
                // Validate that interface methods have no implementation (only ... or pass)
                ValidateInterfaceMethod(method, interfaceDef.Name);
                ResolveMethodDeclaration(method, typeSymbol);
            }
        }

        _symbolTable.ExitScope();
    }

    private void ResolveEnumDeclaration(EnumDef enumDef)
    {
        _logger.LogDebug($"Resolving enum declaration: {enumDef.Name}");

        if (_symbolTable.Lookup(enumDef.Name, searchParents: false) != null)
        {
            AddError($"Enum '{enumDef.Name}' is already defined",
                enumDef.LineStart, enumDef.ColumnStart);
            return;
        }

        var typeSymbol = new TypeSymbol
        {
            Name = enumDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Enum,
            AccessLevel = AccessLevel.Public,
            DefiningFilePath = _currentFilePath,
            DeclarationLine = enumDef.LineStart,
            DeclarationColumn = enumDef.ColumnStart
        };

        _symbolTable.Define(typeSymbol);
    }

    private void ResolveFunctionDeclaration(FunctionDef functionDef)
    {
        _logger.LogDebug($"Resolving function declaration: {functionDef.Name}");

        var existingSymbol = _symbolTable.Lookup(functionDef.Name, searchParents: false);
        if (existingSymbol != null)
        {
            // Allow shadowing builtins (which have no source location)
            // This matches Python behavior where user code can shadow builtins
            bool isBuiltin = existingSymbol.DeclarationLine == null;
            if (!isBuiltin)
            {
                AddError($"Function '{functionDef.Name}' is already defined",
                    functionDef.LineStart, functionDef.ColumnStart);
                return;
            }
            // For builtins, we'll replace the symbol below
        }

        // Add parameters to the function symbol (types will be resolved later by TypeChecker)
        var parameters = functionDef.Parameters.Select(p => new ParameterSymbol
        {
            Name = p.Name,
            Type = SemanticType.Unknown,  // Will be resolved during type checking
            HasDefault = p.DefaultValue != null,
            DefaultValue = p.DefaultValue
        }).ToList();

        var funcSymbol = new FunctionSymbol
        {
            Name = functionDef.Name,
            Kind = SymbolKind.Function,
            AccessLevel = AccessLevel.Public,
            Parameters = parameters,
            TypeParameters = functionDef.TypeParameters.ToList(),
            DeclarationLine = functionDef.LineStart,
            DeclarationColumn = functionDef.ColumnStart
        };

        _symbolTable.Define(funcSymbol);
    }

    private void ResolveMethodDeclaration(FunctionDef method, TypeSymbol owningType)
    {
        _logger.LogDebug($"Resolving method declaration: {owningType.Name}.{method.Name}");

        // Determine access level from name
        var accessLevel = DetermineAccessLevel(method.Name);

        // Check for special decorators
        bool hasStaticDecorator = method.Decorators.Any(d => d.Name == "static" || d.Name == "staticmethod");

        // Primary mechanism: Method is static if it doesn't have 'self' parameter (Pythonic)
        // @static decorator is valid but OPTIONAL/redundant
        bool hasSelfParameter = method.Parameters.Any(p =>
            string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));

        bool isStatic = hasStaticDecorator || !hasSelfParameter;

        // Determine if method is abstract:
        // 1. Has @abstract decorator explicitly, OR
        // 2. Is in an @abstract class AND has ellipsis body (implicit abstract)
        bool hasAbstractDecorator = method.Decorators.Any(d => d.Name == "abstract");
        bool hasEllipsisBody = method.Body.Length == 1
            && method.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };

        bool isAbstract = hasAbstractDecorator || (owningType.IsAbstract && hasEllipsisBody);
        bool isVirtual = method.Decorators.Any(d => d.Name == "virtual");
        bool isOverride = method.Decorators.Any(d => d.Name == "override");

        // Add parameters to the method symbol (types will be resolved later by TypeChecker)
        var parameters = method.Parameters.Select(p => new ParameterSymbol
        {
            Name = p.Name,
            Type = SemanticType.Unknown,  // Will be resolved during type checking
            HasDefault = p.DefaultValue != null,
            DefaultValue = p.DefaultValue
        }).ToList();

        var funcSymbol = new FunctionSymbol
        {
            Name = method.Name,
            Kind = SymbolKind.Function,
            AccessLevel = accessLevel,
            Parameters = parameters,
            TypeParameters = method.TypeParameters.ToList(),
            IsStatic = isStatic,
            IsAbstract = isAbstract,
            IsVirtual = isVirtual,
            IsOverride = isOverride,
            DeclarationLine = method.LineStart,
            DeclarationColumn = method.ColumnStart
        };

        owningType.Methods.Add(funcSymbol);

        // Register constructors (__init__ methods)
        // For constructors, we allow multiple overloads with the same name
        if (method.Name == "__init__")
        {
            owningType.Constructors.Add(funcSymbol);
            _logger.LogDebug($"Registered constructor overload: {owningType.Name}.__init__ (params: {method.Parameters.Length})");

            // Only register the first __init__ in the symbol table to avoid duplicate name errors
            // All overloads are tracked in the Constructors list
            if (owningType.Constructors.Count == 1)
            {
                _symbolTable.Define(funcSymbol);
            }
        }
        else if (OperatorSignatureValidator.IsOperatorDunder(method.Name) ||
                 ProtocolSignatureValidator.IsProtocolDunder(method.Name))
        {
            // For operator and protocol dunder methods, allow multiple overloads
            // Only register the first overload in the symbol table to avoid duplicate name errors
            // All overloads are tracked in the OperatorMethods/ProtocolMethods dictionaries
            if (!_symbolTable.TryDefine(funcSymbol))
            {
                _logger.LogDebug($"Method overload registered (not in symbol table): {owningType.Name}.{method.Name}");
            }
        }
        else
        {
            // For regular methods, register in symbol table normally
            _symbolTable.Define(funcSymbol);
        }

        // Register operator dunder methods in cache (validation moved to SignatureValidatorV2)
        if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
        {
            if (!owningType.OperatorMethods.TryGetValue(method.Name, out var overloads))
            {
                overloads = new List<FunctionSymbol>();
                owningType.OperatorMethods[method.Name] = overloads;
            }
            overloads.Add(funcSymbol);
            _logger.LogDebug($"Registered operator method: {owningType.Name}.{method.Name}");
        }
        // Register protocol dunder methods in cache (validation moved to SignatureValidatorV2)
        else if (ProtocolSignatureValidator.IsProtocolDunder(method.Name))
        {
            if (!owningType.ProtocolMethods.TryGetValue(method.Name, out var overloads))
            {
                overloads = new List<FunctionSymbol>();
                owningType.ProtocolMethods[method.Name] = overloads;
            }
            overloads.Add(funcSymbol);
            _logger.LogDebug($"Registered protocol method: {owningType.Name}.{method.Name}");
        }
    }

    private void ResolveFieldDeclaration(VariableDeclaration field, TypeSymbol owningType)
    {
        _logger.LogDebug($"Resolving field declaration: {owningType.Name}.{field.Name}");

        var accessLevel = DetermineAccessLevel(field.Name);

        var varSymbol = new VariableSymbol
        {
            Name = field.Name,
            Kind = SymbolKind.Variable,
            AccessLevel = accessLevel,
            IsConstant = field.IsConst,
            DeclarationLine = field.LineStart,
            DeclarationColumn = field.ColumnStart
        };

        owningType.Fields.Add(varSymbol);
        _symbolTable.Define(varSymbol);
    }

    private void ResolveConstantDeclaration(VariableDeclaration constDecl)
    {
        _logger.LogDebug($"Resolving constant declaration: {constDecl.Name}");

        if (_symbolTable.Lookup(constDecl.Name, searchParents: false) != null)
        {
            AddError($"Constant '{constDecl.Name}' is already defined",
                constDecl.LineStart, constDecl.ColumnStart);
            return;
        }

        var varSymbol = new VariableSymbol
        {
            Name = constDecl.Name,
            Kind = SymbolKind.Variable,
            AccessLevel = AccessLevel.Public,
            IsConstant = true,
            DeclarationLine = constDecl.LineStart,
            DeclarationColumn = constDecl.ColumnStart
        };

        _symbolTable.Define(varSymbol);
    }

    private void ResolveTypeAliasDeclaration(TypeAlias typeAlias)
    {
        _logger.LogDebug($"Resolving type alias declaration: {typeAlias.Name}");

        // Check for redefinition
        if (_symbolTable.Lookup(typeAlias.Name, searchParents: false) != null)
        {
            AddError($"Type alias '{typeAlias.Name}' is already defined",
                typeAlias.LineStart, typeAlias.ColumnStart);
            return;
        }

        // Validate that exactly one of Type or FunctionType is set
        if (typeAlias.Type == null && typeAlias.FunctionType == null)
        {
            AddError($"Type alias '{typeAlias.Name}' must have a type",
                typeAlias.LineStart, typeAlias.ColumnStart);
            return;
        }

        if (typeAlias.Type != null && typeAlias.FunctionType != null)
        {
            AddError($"Type alias '{typeAlias.Name}' cannot have both Type and FunctionType",
                typeAlias.LineStart, typeAlias.ColumnStart);
            return;
        }

        // Create type alias symbol
        var aliasSymbol = new TypeAliasSymbol
        {
            Name = typeAlias.Name,
            Kind = SymbolKind.TypeAlias,
            AccessLevel = AccessLevel.Public,
            TypeAnnotation = typeAlias.Type,
            FunctionType = typeAlias.FunctionType,
            DeclarationLine = typeAlias.LineStart,
            DeclarationColumn = typeAlias.ColumnStart
        };

        _symbolTable.Define(aliasSymbol);
    }

    private void ResolveImport(ImportStatement import)
    {
        _logger.LogDebug($"Resolving import: {string.Join(", ", import.Names.Select(n => n.Name))}");

        // TODO: Implement module loading and resolution
        // For now, just log that we encountered an import
    }

    private void ResolveFromImport(FromImportStatement fromImport)
    {
        _logger.LogDebug($"Resolving from-import: from {fromImport.Module} import ...");

        // TODO: Implement selective import resolution
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
        // Interface methods must have only ... (ellipsis) or pass as their body
        // They cannot contain actual implementation

        if (method.Body.Length == 0)
        {
            AddError($"Interface method '{method.Name}' in interface '{interfaceName}' must have a body with '...' or 'pass'",
                method.LineStart, method.ColumnStart);
            return;
        }

        if (method.Body.Length == 1)
        {
            var stmt = method.Body[0];

            // Allow: pass
            if (stmt is PassStatement)
                return;

            // Allow: ... (ExpressionStatement containing EllipsisLiteral)
            if (stmt is ExpressionStatement exprStmt && exprStmt.Expression is EllipsisLiteral)
                return;
        }

        // If we get here, the method has an invalid body (implementation)
        AddError($"Interface method '{method.Name}' in interface '{interfaceName}' cannot have an implementation. Use '...' or 'pass' instead",
            method.LineStart, method.ColumnStart);
    }

    private void AddError(string message, int? line = null, int? column = null)
    {
        var error = new SemanticError(message, line, column);
        _errors.Add(error);
        _logger.LogError(error.Message, line ?? 0, column ?? 0);
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
            var baseSymbol = _symbolTable.Lookup(baseAnnot.Name) as TypeSymbol;
            if (baseSymbol == null)
            {
                AddError($"Base type '{baseAnnot.Name}' not found",
                    classDef.LineStart, classDef.ColumnStart);
                continue;
            }

            if (baseSymbol.TypeKind != TypeKind.Class && baseSymbol.TypeKind != TypeKind.Interface)
            {
                AddError($"'{baseAnnot.Name}' is not a class or interface",
                    classDef.LineStart, classDef.ColumnStart);
                continue;
            }

            if (baseSymbol.TypeKind == TypeKind.Class)
            {
                // Only one base class allowed (C# single inheritance)
                if (hasSetBaseType)
                {
                    AddError($"Class '{classDef.Name}' cannot have multiple base classes (only one class inheritance allowed)",
                        classDef.LineStart, classDef.ColumnStart);
                    continue;
                }
                typeSymbol.BaseType = baseSymbol;
                hasSetBaseType = true;
            }
            else // TypeKind.Interface
            {
                typeSymbol.Interfaces.Add(baseSymbol);
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
            var interfaceSymbol = _symbolTable.Lookup(baseAnnot.Name) as TypeSymbol;
            if (interfaceSymbol == null)
            {
                AddError($"Interface '{baseAnnot.Name}' not found",
                    structDef.LineStart, structDef.ColumnStart);
                continue;
            }

            if (interfaceSymbol.TypeKind != TypeKind.Interface)
            {
                AddError($"Structs can only implement interfaces, '{baseAnnot.Name}' is not an interface",
                    structDef.LineStart, structDef.ColumnStart);
                continue;
            }

            typeSymbol.Interfaces.Add(interfaceSymbol);
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
            var baseInterfaceSymbol = _symbolTable.Lookup(baseAnnot.Name) as TypeSymbol;
            if (baseInterfaceSymbol == null)
            {
                AddError($"Interface '{baseAnnot.Name}' not found",
                    interfaceDef.LineStart, interfaceDef.ColumnStart);
                continue;
            }

            if (baseInterfaceSymbol.TypeKind != TypeKind.Interface)
            {
                AddError($"'{baseAnnot.Name}' is not an interface",
                    interfaceDef.LineStart, interfaceDef.ColumnStart);
                continue;
            }

            typeSymbol.Interfaces.Add(baseInterfaceSymbol);
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
        var queue = new Queue<TypeSymbol>(interfaceSymbol.Interfaces);

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
            foreach (var grandBase in baseInterface.Interfaces)
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
            .Where(p => p.Name != "self")
            .Select(p => p.Type?.GetDisplayName() ?? "unknown");
        return $"{method.Name}({string.Join(",", paramTypes)})";
    }
}
