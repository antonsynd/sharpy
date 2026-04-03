using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

internal partial class NameResolver
{
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

            case UnionDef unionDef:
                ResolveUnionDeclaration(unionDef);
                break;

            case DelegateDef delegateDef:
                ResolveDelegateDeclaration(delegateDef);
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
        bool isAbstract = classDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract);

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
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = classDef.Span,
            DeclarationLine = classDef.LineStart,
            DeclarationColumn = classDef.ColumnStart,
            Documentation = classDef.DocString
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
                Variance = typeParam.Variance,
                DeclarationLine = typeParam.LineStart,
                DeclarationColumn = typeParam.ColumnStart
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
            else if (statement is EventDef eventDef)
            {
                ResolveEventDeclaration(eventDef, typeSymbol);
            }
            else if (statement is TypeAlias typeAlias)
            {
                ResolveTypeAliasDeclaration(typeAlias);
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
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = structDef.Span,
            DeclarationLine = structDef.LineStart,
            DeclarationColumn = structDef.ColumnStart,
            Documentation = structDef.DocString
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
                Variance = typeParam.Variance,
                DeclarationLine = typeParam.LineStart,
                DeclarationColumn = typeParam.ColumnStart
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
            else if (statement is EventDef eventDef)
            {
                ResolveEventDeclaration(eventDef, typeSymbol);
            }
            else if (statement is TypeAlias typeAlias)
            {
                ResolveTypeAliasDeclaration(typeAlias);
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
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = interfaceDef.Span,
            DeclarationLine = interfaceDef.LineStart,
            DeclarationColumn = interfaceDef.ColumnStart,
            Documentation = interfaceDef.DocString
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
                Variance = typeParam.Variance,
                DeclarationLine = typeParam.LineStart,
                DeclarationColumn = typeParam.ColumnStart
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
            else if (statement is EventDef eventDef)
            {
                ResolveEventDeclaration(eventDef, typeSymbol);
            }
        }

        _symbolTable.ExitScope();
    }

    private void ResolveDelegateDeclaration(DelegateDef delegateDef)
    {
        _logger.LogDebug($"Resolving delegate declaration: {delegateDef.Name}");

        if (_symbolTable.Lookup(delegateDef.Name, searchParents: false) != null)
        {
            AddError($"Delegate '{delegateDef.Name}' is already defined",
                delegateDef.LineStart, delegateDef.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: delegateDef.Span);
            return;
        }

        var typeSymbol = new TypeSymbol
        {
            Name = delegateDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Delegate,
            AccessLevel = AccessLevel.Public,
            TypeParameters = delegateDef.TypeParameters.ToList(),
            DefiningFilePath = _currentFilePath,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = delegateDef.Span,
            DeclarationLine = delegateDef.LineStart,
            DeclarationColumn = delegateDef.ColumnStart,
            Documentation = delegateDef.DocString
        };

        _symbolTable.EnterScope($"delegate:{delegateDef.Name}");

        // Register type parameters in the scope so they can be resolved
        foreach (var typeParam in delegateDef.TypeParameters)
        {
            var typeParamSymbol = new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                DeclaringType = typeSymbol,
                Constraints = typeParam.Constraints,
                Variance = typeParam.Variance,
                DeclarationLine = typeParam.LineStart,
                DeclarationColumn = typeParam.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        // Create a synthetic Invoke method with the delegate's parameters and return type
        var parameters = delegateDef.Parameters.Select(p => new ParameterSymbol
        {
            Name = p.Name,
            Type = SemanticType.Unknown,  // Will be resolved during type checking
            HasDefault = p.DefaultValue != null,
            DefaultValue = p.DefaultValue,
            IsVariadic = p.IsVariadic,
            IsPositionalOnly = p.Kind == ParameterKind.PositionalOnly,
            IsKeywordOnly = p.Kind == ParameterKind.KeywordOnly
        }).ToList();

        var invokeSymbol = new FunctionSymbol
        {
            Name = "Invoke",
            Kind = SymbolKind.Function,
            AccessLevel = AccessLevel.Public,
            Parameters = parameters,
            ReturnType = SemanticType.Unknown,  // Will be resolved during type checking
            IsAbstract = true,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = delegateDef.Span,
            DeclarationLine = delegateDef.LineStart,
            DeclarationColumn = delegateDef.ColumnStart
        };

        typeSymbol.Methods.Add(invokeSymbol);

        _symbolTable.ExitScope();

        _symbolTable.Define(typeSymbol);
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
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = enumDef.Span,
            DeclarationLine = enumDef.LineStart,
            DeclarationColumn = enumDef.ColumnStart,
            Documentation = enumDef.DocString
        };

        // Register enum members as static fields so pattern matching and
        // exhaustiveness checking can resolve them via TypeSymbol.Fields.
        // Type is left as Unknown here; it will be set during type checking.
        foreach (var member in enumDef.Members)
        {
            typeSymbol.Fields.Add(new VariableSymbol
            {
                Name = member.Name,
                Kind = SymbolKind.Variable,
                IsStatic = true,
                IsConstant = true,
                AccessLevel = AccessLevel.Public,
                DeclarationLine = member.LineStart,
                DeclarationColumn = member.ColumnStart,
                DeclarationSpan = member.Span
            });
        }

        _symbolTable.Define(typeSymbol);
    }

    private void ResolveUnionDeclaration(UnionDef unionDef)
    {
        _logger.LogDebug($"Resolving union declaration: {unionDef.Name}");

        if (_symbolTable.Lookup(unionDef.Name, searchParents: false) != null)
        {
            AddError($"Union '{unionDef.Name}' is already defined",
                unionDef.LineStart, unionDef.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: unionDef.Span);
            return;
        }

        var unionSymbol = new TypeSymbol
        {
            Name = unionDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Union,
            AccessLevel = AccessLevel.Public,
            IsAbstract = true,
            TypeParameters = unionDef.TypeParameters.ToList(),
            DefiningFilePath = _currentFilePath,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = unionDef.Span,
            DeclarationLine = unionDef.LineStart,
            DeclarationColumn = unionDef.ColumnStart,
            Documentation = unionDef.DocString
        };

        // Create case type symbols as nested types
        foreach (var caseDef in unionDef.Cases)
        {
            var caseSymbol = new TypeSymbol
            {
                Name = caseDef.Name,
                Kind = SymbolKind.Type,
                TypeKind = TypeKind.Class,
                AccessLevel = AccessLevel.Public,
                BaseType = unionSymbol,
                TypeParameters = unionDef.TypeParameters.ToList(),
                DefiningFilePath = _currentFilePath,
                DeclaringFilePath = _currentFilePath,
                DeclarationSpan = caseDef.Span,
                DeclarationLine = caseDef.LineStart,
                DeclarationColumn = caseDef.ColumnStart
            };

            // Fields resolved during type checking phase
            unionSymbol.UnionCases.Add(caseSymbol);
        }

        _symbolTable.Define(unionSymbol);
    }

}
