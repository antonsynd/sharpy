using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// First pass: Resolve all names and build symbol tables
/// </summary>
internal class NameResolver
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
        => _semanticBinding.GetInterfaces(symbol) ?? (IReadOnlyList<TypeSymbol>)symbol.Interfaces;

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

            var interfaces = _semanticBinding.GetInterfaces(current) ?? (IReadOnlyList<TypeSymbol>)current.Interfaces;
            foreach (var iface in interfaces)
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

            var interfaces = _semanticBinding.GetInterfaces(current) ?? (IReadOnlyList<TypeSymbol>)current.Interfaces;
            foreach (var iface in interfaces)
            {
                queue.Enqueue(iface);
            }
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

            case TypeAlias typeAlias:
                ResolveTypeAliasDeclaration(typeAlias);
                break;

            case PropertyDef:
                // Property declarations are resolved as part of class/struct/interface bodies
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
                classDef.LineStart, classDef.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: classDef.Span);
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
                Constraints = typeParam.Constraints,
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
            else if (statement is PropertyDef propDef)
            {
                ResolvePropertyDeclaration(propDef, typeSymbol);
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
                structDef.LineStart, structDef.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: structDef.Span);
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
                Constraints = typeParam.Constraints,
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
            else if (statement is PropertyDef propDef)
            {
                ResolvePropertyDeclaration(propDef, typeSymbol);
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
                interfaceDef.LineStart, interfaceDef.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: interfaceDef.Span);
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
                Constraints = typeParam.Constraints,
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
            else if (statement is PropertyDef propDef)
            {
                ResolvePropertyDeclaration(propDef, typeSymbol);
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
                enumDef.LineStart, enumDef.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: enumDef.Span);
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
                    functionDef.LineStart, functionDef.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: functionDef.Span);
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
        bool hasStaticDecorator = method.Decorators.Any(d => d.Name == "static");

        // Primary mechanism: Method is static if it doesn't have 'self' parameter (Pythonic)
        // @static decorator is valid but OPTIONAL/redundant
        bool hasSelfParameter = method.Parameters.Any(p =>
            string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));

        bool isStatic = hasStaticDecorator || !hasSelfParameter;

        // Determine if method is abstract:
        // 1. Has @abstract decorator explicitly, OR
        // 2. Is in an @abstract class AND has ellipsis body (implicit abstract), OR
        // 3. Is in an interface AND has ellipsis or pass body (implicit abstract)
        bool hasAbstractDecorator = method.Decorators.Any(d => d.Name == "abstract");
        bool hasEllipsisBody = method.Body.Length == 1
            && method.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
        bool hasPassBody = method.Body.Length == 1
            && method.Body[0] is PassStatement;
        bool isInterfaceAbstract = owningType.TypeKind == TypeKind.Interface
            && (hasEllipsisBody || hasPassBody);

        bool isAbstract = hasAbstractDecorator || (owningType.IsAbstract && hasEllipsisBody) || isInterfaceAbstract;
        bool isVirtual = method.Decorators.Any(d => d.Name == "virtual");
        bool isOverride = method.Decorators.Any(d => d.Name == "override")
            || ProtocolRegistry.IsObjectOverrideDunder(method.Name);

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
        if (method.Name == DunderNames.Init)
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
        else if (OperatorRegistry.IsOperatorDunder(method.Name) ||
                 ProtocolRegistry.IsProtocolDunder(method.Name))
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

        // Register operator dunder methods in cache (validation moved to SignatureValidator)
        if (OperatorRegistry.IsOperatorDunder(method.Name))
        {
            if (!owningType.OperatorMethods.TryGetValue(method.Name, out var overloads))
            {
                overloads = new List<FunctionSymbol>();
                owningType.OperatorMethods[method.Name] = overloads;
            }
            overloads.Add(funcSymbol);
            _logger.LogDebug($"Registered operator method: {owningType.Name}.{method.Name}");
        }
        // Register protocol dunder methods in cache (validation moved to SignatureValidator)
        else if (ProtocolRegistry.IsProtocolDunder(method.Name))
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

    private void ResolvePropertyDeclaration(PropertyDef propDef, TypeSymbol owningType)
    {
        _logger.LogDebug($"Resolving property declaration: {owningType.Name}.{propDef.Name}");

        // Check if a property with this name already exists (for combining getter/setter)
        var existingProp = owningType.Properties.FirstOrDefault(p => p.Name == propDef.Name);

        bool isStatic = propDef.Decorators.Any(d => d.Name == "static")
            || !propDef.Parameters.Any(p => string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase))
            && propDef.IsFunctionStyle;
        bool isVirtual = propDef.Decorators.Any(d => d.Name == "virtual");
        bool isAbstract = propDef.Decorators.Any(d => d.Name == "abstract");
        bool isOverride = propDef.Decorators.Any(d => d.Name == "override");
        bool isFinal = propDef.Decorators.Any(d => d.Name == "final");

        bool hasGetter = propDef.Accessor == PropertyAccessor.Get || propDef.Accessor == PropertyAccessor.None;
        bool hasSetter = propDef.Accessor == PropertyAccessor.Set;
        bool hasInit = propDef.Accessor == PropertyAccessor.Init;

        var accessLevel = DetermineAccessLevel(propDef.Name);
        // Override with explicit decorator
        foreach (var decorator in propDef.Decorators)
        {
            switch (decorator.Name)
            {
                case "private":
                    accessLevel = AccessLevel.Private;
                    break;
                case "protected":
                    accessLevel = AccessLevel.Protected;
                    break;
                case "internal":
                    accessLevel = AccessLevel.Internal;
                    break;
            }
        }

        if (existingProp != null)
        {
            // Merge: combine accessor info from additional PropertyDef
            // This handles the case where getter and setter are defined separately
            var merged = existingProp with
            {
                HasGetter = existingProp.HasGetter || hasGetter,
                HasSetter = existingProp.HasSetter || hasSetter,
                HasInit = existingProp.HasInit || hasInit,
                SetterAccess = hasSetter || hasInit ? accessLevel : existingProp.SetterAccess,
            };

            // Replace existing in the list
            var index = owningType.Properties.IndexOf(existingProp);
            owningType.Properties[index] = merged;
        }
        else
        {
            var propSymbol = new PropertySymbol
            {
                Name = propDef.Name,
                HasGetter = hasGetter,
                HasSetter = hasSetter,
                HasInit = hasInit,
                IsStatic = isStatic,
                IsVirtual = isVirtual,
                IsAbstract = isAbstract,
                IsOverride = isOverride,
                IsFinal = isFinal,
                GetterAccess = hasGetter ? accessLevel : AccessLevel.Public,
                SetterAccess = hasSetter || hasInit ? accessLevel : AccessLevel.Public,
            };

            owningType.Properties.Add(propSymbol);
        }
    }

    private void ResolveConstantDeclaration(VariableDeclaration constDecl)
    {
        _logger.LogDebug($"Resolving constant declaration: {constDecl.Name}");

        if (_symbolTable.Lookup(constDecl.Name, searchParents: false) != null)
        {
            AddError($"Constant '{constDecl.Name}' is already defined",
                constDecl.LineStart, constDecl.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: constDecl.Span);
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
                typeAlias.LineStart, typeAlias.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: typeAlias.Span);
            return;
        }

        // Validate that exactly one of Type or FunctionType is set
        if (typeAlias.Type == null && typeAlias.FunctionType == null)
        {
            AddError($"Type alias '{typeAlias.Name}' must have a type",
                typeAlias.LineStart, typeAlias.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTypeAlias, span: typeAlias.Span);
            return;
        }

        if (typeAlias.Type != null && typeAlias.FunctionType != null)
        {
            AddError($"Type alias '{typeAlias.Name}' cannot have both Type and FunctionType",
                typeAlias.LineStart, typeAlias.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTypeAlias, span: typeAlias.Span);
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
                _semanticBinding.AddInterface(typeSymbol, baseSymbol);
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

            _semanticBinding.AddInterface(typeSymbol, interfaceSymbol);
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

            _semanticBinding.AddInterface(typeSymbol, baseInterfaceSymbol);
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
            .Where(p => p.Name != "self")
            .Select(p => p.Type?.GetDisplayName() ?? "unknown");
        return $"{method.Name}({string.Join(",", paramTypes)})";
    }
}
