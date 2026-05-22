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
            IsNameBacktickEscaped = classDef.IsNameBacktickEscaped,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            TypeParameters = classDef.TypeParameters.ToList(),
            IsAbstract = isAbstract,
            DefiningFilePath = _currentFilePath,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = classDef.Span,
            DeclarationLine = classDef.LineStart,
            DeclarationColumn = classDef.ColumnStart,
            NameDeclarationLine = classDef.NameLineStart,
            NameDeclarationColumn = classDef.NameColumnStart,
            Documentation = classDef.DocString,
            DeprecationMessage = GetDeprecationMessage(classDef.Decorators)
        };

        // Define in current scope
        _symbolTable.Define(typeSymbol);

        // Store for second pass (inheritance resolution)
        _classDefs.Add((classDef, _currentModulePath));

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
                DeclarationColumn = typeParam.ColumnStart,
                NameDeclarationLine = typeParam.LineStart,
                NameDeclarationColumn = typeParam.ColumnStart
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
            else if (statement is ClassDef or StructDef or InterfaceDef or EnumDef)
            {
                ResolveNestedTypeDeclaration(statement, typeSymbol);
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
            IsNameBacktickEscaped = structDef.IsNameBacktickEscaped,
            TypeKind = TypeKind.Struct,
            AccessLevel = AccessLevel.Public,
            TypeParameters = structDef.TypeParameters.ToList(),
            DefiningFilePath = _currentFilePath,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = structDef.Span,
            DeclarationLine = structDef.LineStart,
            DeclarationColumn = structDef.ColumnStart,
            NameDeclarationLine = structDef.NameLineStart,
            NameDeclarationColumn = structDef.NameColumnStart,
            Documentation = structDef.DocString
        };

        _symbolTable.Define(typeSymbol);

        // Store for second pass (inheritance resolution)
        _structDefs.Add((structDef, _currentModulePath));

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
                DeclarationColumn = typeParam.ColumnStart,
                NameDeclarationLine = typeParam.LineStart,
                NameDeclarationColumn = typeParam.ColumnStart
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
            else if (statement is ClassDef or StructDef or InterfaceDef or EnumDef)
            {
                ResolveNestedTypeDeclaration(statement, typeSymbol);
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
            IsNameBacktickEscaped = interfaceDef.IsNameBacktickEscaped,
            TypeKind = TypeKind.Interface,
            AccessLevel = AccessLevel.Public,
            TypeParameters = interfaceDef.TypeParameters.ToList(),
            DefiningFilePath = _currentFilePath,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = interfaceDef.Span,
            DeclarationLine = interfaceDef.LineStart,
            DeclarationColumn = interfaceDef.ColumnStart,
            NameDeclarationLine = interfaceDef.NameLineStart,
            NameDeclarationColumn = interfaceDef.NameColumnStart,
            Documentation = interfaceDef.DocString
        };

        _symbolTable.Define(typeSymbol);

        // Store for second pass (inheritance resolution)
        _interfaceDefs.Add((interfaceDef, _currentModulePath));

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
                DeclarationColumn = typeParam.ColumnStart,
                NameDeclarationLine = typeParam.LineStart,
                NameDeclarationColumn = typeParam.ColumnStart
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
            else if (statement is ClassDef or StructDef or InterfaceDef or EnumDef)
            {
                ResolveNestedTypeDeclaration(statement, typeSymbol);
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
            IsNameBacktickEscaped = delegateDef.IsNameBacktickEscaped,
            TypeKind = TypeKind.Delegate,
            AccessLevel = AccessLevel.Public,
            TypeParameters = delegateDef.TypeParameters.ToList(),
            DefiningFilePath = _currentFilePath,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = delegateDef.Span,
            DeclarationLine = delegateDef.LineStart,
            DeclarationColumn = delegateDef.ColumnStart,
            NameDeclarationLine = delegateDef.NameLineStart,
            NameDeclarationColumn = delegateDef.NameColumnStart,
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
                DeclarationColumn = typeParam.ColumnStart,
                NameDeclarationLine = typeParam.LineStart,
                NameDeclarationColumn = typeParam.ColumnStart
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
            IsKeywordOnly = p.Kind == ParameterKind.KeywordOnly,
            IsLateBound = p.IsLateBound,
            Modifier = p.Modifier
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
            DeclarationColumn = delegateDef.ColumnStart,
            NameDeclarationLine = delegateDef.NameLineStart,
            NameDeclarationColumn = delegateDef.NameColumnStart
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
            IsNameBacktickEscaped = enumDef.IsNameBacktickEscaped,
            TypeKind = TypeKind.Enum,
            AccessLevel = AccessLevel.Public,
            DefiningFilePath = _currentFilePath,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = enumDef.Span,
            DeclarationLine = enumDef.LineStart,
            DeclarationColumn = enumDef.ColumnStart,
            NameDeclarationLine = enumDef.NameLineStart,
            NameDeclarationColumn = enumDef.NameColumnStart,
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
                NameDeclarationLine = member.LineStart,
                NameDeclarationColumn = member.ColumnStart,
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
            IsNameBacktickEscaped = unionDef.IsNameBacktickEscaped,
            TypeKind = TypeKind.Union,
            AccessLevel = AccessLevel.Public,
            IsAbstract = true,
            TypeParameters = unionDef.TypeParameters.ToList(),
            DefiningFilePath = _currentFilePath,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = unionDef.Span,
            DeclarationLine = unionDef.LineStart,
            DeclarationColumn = unionDef.ColumnStart,
            NameDeclarationLine = unionDef.NameLineStart,
            NameDeclarationColumn = unionDef.NameColumnStart,
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
                DeclarationColumn = caseDef.ColumnStart,
                NameDeclarationLine = caseDef.NameLineStart,
                NameDeclarationColumn = caseDef.NameColumnStart
            };

            // Fields resolved during type checking phase
            unionSymbol.UnionCases.Add(caseSymbol);
        }

        _symbolTable.Define(unionSymbol);
    }

    private void ResolveNestedTypeDeclaration(Statement statement, TypeSymbol enclosingType)
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
        }

        var nestedName = statement switch
        {
            ClassDef c => c.Name,
            StructDef s => s.Name,
            InterfaceDef i => i.Name,
            EnumDef e => e.Name,
            _ => null
        };

        if (nestedName != null && _symbolTable.Lookup(nestedName) is TypeSymbol nestedSymbol)
        {
            nestedSymbol.DeclaringType = enclosingType;
            nestedSymbol.AccessLevel = GetAccessLevel(statement) ?? AccessLevel.Private;
            enclosingType.NestedTypes.Add(nestedSymbol);
        }
    }

    private static AccessLevel? GetAccessLevel(Statement statement)
    {
        var decorators = statement switch
        {
            ClassDef c => c.Decorators,
            StructDef s => s.Decorators,
            InterfaceDef i => i.Decorators,
            EnumDef e => e.Decorators,
            _ => System.Collections.Immutable.ImmutableArray<Decorator>.Empty
        };

        if (decorators.Any(d => d.Name == DecoratorNames.Public))
            return AccessLevel.Public;
        if (decorators.Any(d => d.Name == DecoratorNames.Protected))
            return AccessLevel.Protected;
        if (decorators.Any(d => d.Name == DecoratorNames.Private))
            return AccessLevel.Private;
        if (decorators.Any(d => d.Name == DecoratorNames.Internal))
            return AccessLevel.Internal;

        return null;
    }

}
