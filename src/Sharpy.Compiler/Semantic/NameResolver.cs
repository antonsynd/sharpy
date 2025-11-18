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

    public NameResolver(SymbolTable symbolTable, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

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

        // Create type symbol
        var typeSymbol = new TypeSymbol
        {
            Name = classDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            TypeParameters = classDef.TypeParameters,
            DeclarationLine = classDef.LineStart,
            DeclarationColumn = classDef.ColumnStart
        };

        // Define in current scope
        _symbolTable.Define(typeSymbol);

        // Store for second pass (inheritance resolution)
        _classDefs.Add(classDef);

        // Enter class scope to resolve members
        _symbolTable.EnterScope($"class:{classDef.Name}");

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
            TypeParameters = structDef.TypeParameters,
            DeclarationLine = structDef.LineStart,
            DeclarationColumn = structDef.ColumnStart
        };

        _symbolTable.Define(typeSymbol);

        // Store for second pass (inheritance resolution)
        _structDefs.Add(structDef);

        _symbolTable.EnterScope($"struct:{structDef.Name}");

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
            TypeParameters = interfaceDef.TypeParameters,
            DeclarationLine = interfaceDef.LineStart,
            DeclarationColumn = interfaceDef.ColumnStart
        };

        _symbolTable.Define(typeSymbol);

        // Store for second pass (inheritance resolution)
        _interfaceDefs.Add(interfaceDef);

        _symbolTable.EnterScope($"interface:{interfaceDef.Name}");

        foreach (var statement in interfaceDef.Body)
        {
            if (statement is FunctionDef method)
            {
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
            DeclarationLine = enumDef.LineStart,
            DeclarationColumn = enumDef.ColumnStart
        };

        _symbolTable.Define(typeSymbol);
    }

    private void ResolveFunctionDeclaration(FunctionDef functionDef)
    {
        _logger.LogDebug($"Resolving function declaration: {functionDef.Name}");

        if (_symbolTable.Lookup(functionDef.Name, searchParents: false) != null)
        {
            AddError($"Function '{functionDef.Name}' is already defined",
                functionDef.LineStart, functionDef.ColumnStart);
            return;
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
        bool isStatic = method.Decorators.Any(d => d.Name == "static" || d.Name == "staticmethod");
        bool isAbstract = method.Decorators.Any(d => d.Name == "abstract" || d.Name == "abstractmethod");
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
            IsStatic = isStatic,
            IsAbstract = isAbstract,
            IsVirtual = isVirtual,
            IsOverride = isOverride,
            DeclarationLine = method.LineStart,
            DeclarationColumn = method.ColumnStart
        };

        owningType.Methods.Add(funcSymbol);
        _symbolTable.Define(funcSymbol);
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

    private void AddError(string message, int? line = null, int? column = null)
    {
        var error = new SemanticError(message, line, column);
        _errors.Add(error);
        _logger.LogError(error.Message, line ?? 0, column ?? 0);
    }

    private void ResolveClassInheritance(ClassDef classDef)
    {
        if (classDef.BaseClasses.Count == 0)
            return;

        var typeSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
        if (typeSymbol == null)
            return;

        // Resolve first base class as the base type (C# single inheritance model)
        var baseClassAnnot = classDef.BaseClasses[0];
        var baseSymbol = _symbolTable.Lookup(baseClassAnnot.Name) as TypeSymbol;
        if (baseSymbol == null)
        {
            AddError($"Base class '{baseClassAnnot.Name}' not found",
                classDef.LineStart, classDef.ColumnStart);
            return;
        }

        if (baseSymbol.TypeKind != TypeKind.Class && baseSymbol.TypeKind != TypeKind.Interface)
        {
            AddError($"'{baseClassAnnot.Name}' is not a class or interface",
                classDef.LineStart, classDef.ColumnStart);
            return;
        }

        // Update the type symbol with the base type
        typeSymbol.BaseType = baseSymbol;

        // Handle remaining base classes as interfaces
        for (int i = 1; i < classDef.BaseClasses.Count; i++)
        {
            var interfaceAnnot = classDef.BaseClasses[i];
            var interfaceSymbol = _symbolTable.Lookup(interfaceAnnot.Name) as TypeSymbol;
            if (interfaceSymbol == null)
            {
                AddError($"Interface '{interfaceAnnot.Name}' not found",
                    classDef.LineStart, classDef.ColumnStart);
                continue;
            }

            if (interfaceSymbol.TypeKind != TypeKind.Interface)
            {
                AddError($"'{interfaceAnnot.Name}' is not an interface",
                    classDef.LineStart, classDef.ColumnStart);
                continue;
            }

            typeSymbol.Interfaces.Add(interfaceSymbol);
        }
    }

    private void ResolveStructInheritance(StructDef structDef)
    {
        if (structDef.BaseClasses.Count == 0)
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
        if (interfaceDef.BaseInterfaces.Count == 0)
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
    }
}
