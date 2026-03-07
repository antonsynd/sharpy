using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

internal partial class NameResolver
{
    private void ResolveFunctionDeclaration(FunctionDef functionDef)
    {
        _logger.LogDebug($"Resolving function declaration: {functionDef.Name}");

        var existingSymbol = _symbolTable.Lookup(functionDef.Name, searchParents: false);
        if (existingSymbol != null)
        {
            // If this function was already registered by the pre-pass, skip it
            if (existingSymbol is FunctionSymbol && existingSymbol.DeclarationSpan == functionDef.Span)
            {
                return;
            }

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
            DefaultValue = p.DefaultValue,
            IsVariadic = p.IsVariadic,
            IsPositionalOnly = p.Kind == ParameterKind.PositionalOnly,
            IsKeywordOnly = p.Kind == ParameterKind.KeywordOnly
        }).ToList();

        var funcSymbol = new FunctionSymbol
        {
            Name = functionDef.Name,
            Kind = SymbolKind.Function,
            AccessLevel = AccessLevel.Public,
            Parameters = parameters,
            TypeParameters = functionDef.TypeParameters.ToList(),
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = functionDef.Span,
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
        bool hasStaticDecorator = method.Decorators.Any(d => d.Name == DecoratorNames.Static);

        // Primary mechanism: Method is static if it doesn't have 'self' parameter (Pythonic)
        // @static decorator is valid but OPTIONAL/redundant
        bool hasSelfParameter = method.Parameters.Any(p =>
            string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));

        bool isStatic = hasStaticDecorator || !hasSelfParameter;

        // Determine if method is abstract:
        // 1. Has @abstract decorator explicitly, OR
        // 2. Is in an @abstract class AND has ellipsis body (implicit abstract), OR
        // 3. Is in an interface AND has ellipsis or pass body (implicit abstract)
        bool hasAbstractDecorator = method.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        bool hasEllipsisBody = method.Body.Length == 1
            && method.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
        bool hasPassBody = method.Body.Length == 1
            && method.Body[0] is PassStatement;
        bool isInterfaceAbstract = owningType.TypeKind == TypeKind.Interface
            && (hasEllipsisBody || hasPassBody);

        bool isAbstract = hasAbstractDecorator || (owningType.IsAbstract && hasEllipsisBody) || isInterfaceAbstract;
        bool isVirtual = method.Decorators.Any(d => d.Name == DecoratorNames.Virtual);
        bool isOverride = method.Decorators.Any(d => d.Name == DecoratorNames.Override)
            || ProtocolRegistry.IsObjectOverrideDunder(method.Name);

        // Add parameters to the method symbol (types will be resolved later by TypeChecker)
        var parameters = method.Parameters.Select(p => new ParameterSymbol
        {
            Name = p.Name,
            Type = SemanticType.Unknown,  // Will be resolved during type checking
            HasDefault = p.DefaultValue != null,
            DefaultValue = p.DefaultValue,
            IsVariadic = p.IsVariadic,
            IsPositionalOnly = p.Kind == ParameterKind.PositionalOnly,
            IsKeywordOnly = p.Kind == ParameterKind.KeywordOnly
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
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = method.Span,
            DeclarationLine = method.LineStart,
            DeclarationColumn = method.ColumnStart,
            SignatureKey = GetMethodSignatureKey(method)
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
            // For regular methods, allow overloads (multiple methods with the same name)
            // Only register the first overload in the symbol table
            if (!_symbolTable.TryDefine(funcSymbol))
            {
                _logger.LogDebug($"Method overload registered (not in symbol table): {owningType.Name}.{method.Name}");
            }
        }

        // Track regular method overloads in MethodOverloads dictionary
        if (!DunderDetector.IsDunderMethod(method.Name))
        {
            if (!owningType.MethodOverloads.TryGetValue(method.Name, out var methodOverloads))
            {
                methodOverloads = new List<FunctionSymbol>();
                owningType.MethodOverloads[method.Name] = methodOverloads;
            }

            // Check for duplicate signatures (same parameter count and type annotations)
            // Build a signature key from parameter type annotation names
            var newSignature = GetMethodSignatureKey(method);
            bool isDuplicate = methodOverloads.Any(existing =>
                existing.SignatureKey == newSignature);

            if (isDuplicate)
            {
                AddError($"Method '{owningType.Name}.{method.Name}' is already defined with the same signature",
                    method.LineStart, method.ColumnStart,
                    code: DiagnosticCodes.Semantic.DuplicateMethodSignature);
            }
            else
            {
                methodOverloads.Add(funcSymbol);
            }
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

    /// <summary>
    /// Builds a signature key from a method's AST parameter type annotations (excluding self).
    /// Used for detecting duplicate method signatures during overload registration.
    /// </summary>
    private static string GetMethodSignatureKey(FunctionDef method)
    {
        var paramTypes = method.Parameters
            .Where(p => p.Name != PythonNames.Self)
            .Select(p => FormatTypeAnnotation(p.Type));
        return string.Join(",", paramTypes);
    }

    private static string FormatTypeAnnotation(TypeAnnotation? type)
    {
        if (type == null)
            return "_";
        if (type.TypeArguments.Length == 0)
            return type.Name;
        var args = string.Join(",", type.TypeArguments.Select(FormatTypeAnnotation));
        return $"{type.Name}[{args}]";
    }

    private void ResolveFieldDeclaration(VariableDeclaration field, TypeSymbol owningType)
    {
        _logger.LogDebug($"Resolving field declaration: {owningType.Name}.{field.Name}");

        var accessLevel = DetermineAccessLevel(field.Name);

        bool isStatic = field.Decorators.Any(d => d.Name == DecoratorNames.Static);

        var varSymbol = new VariableSymbol
        {
            Name = field.Name,
            Kind = SymbolKind.Variable,
            AccessLevel = accessLevel,
            IsConstant = field.IsConst,
            IsStatic = isStatic,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = field.Span,
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

        bool isStatic = propDef.Decorators.Any(d => d.Name == DecoratorNames.Static)
            || !propDef.Parameters.Any(p => string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            && propDef.IsFunctionStyle;
        bool isVirtual = propDef.Decorators.Any(d => d.Name == DecoratorNames.Virtual);
        bool isAbstract = propDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        bool isOverride = propDef.Decorators.Any(d => d.Name == DecoratorNames.Override);
        bool isFinal = propDef.Decorators.Any(d => d.Name == DecoratorNames.Final);

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
                ExplicitInterface = propDef.ExplicitInterface,
            };

            owningType.Properties.Add(propSymbol);
        }
    }

    private void ResolveEventDeclaration(EventDef eventDef, TypeSymbol owningType)
    {
        _logger.LogDebug($"Resolving event declaration: {owningType.Name}.{eventDef.Name}");

        // Check if an event with this name already exists (for merging add/remove accessors)
        var existingEvent = owningType.Events.FirstOrDefault(e => e.Name == eventDef.Name);

        bool isStatic = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Static)
            || eventDef.IsFunctionStyle
            && !eventDef.Parameters.Any(p => string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
        bool isVirtual = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Virtual);
        bool isAbstract = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        bool isOverride = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Override);
        bool isFinal = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Final);

        bool hasAdd = eventDef.Accessor == EventAccessor.Add;
        bool hasRemove = eventDef.Accessor == EventAccessor.Remove;

        // Auto-events have both add and remove implicitly
        if (!eventDef.IsFunctionStyle)
        {
            hasAdd = true;
            hasRemove = true;
        }

        var accessLevel = DetermineAccessLevel(eventDef.Name);
        // Override with explicit decorator
        foreach (var decorator in eventDef.Decorators)
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
                case "public":
                    accessLevel = AccessLevel.Public;
                    break;
            }
        }

        if (existingEvent != null)
        {
            // Merge: combine accessor info from additional EventDef (add + remove pair)
            var merged = existingEvent with
            {
                HasAdd = existingEvent.HasAdd || hasAdd,
                HasRemove = existingEvent.HasRemove || hasRemove,
                AddAccessLevel = hasAdd ? accessLevel : existingEvent.AddAccessLevel,
                RemoveAccessLevel = hasRemove ? accessLevel : existingEvent.RemoveAccessLevel,
            };

            // Replace existing in the list
            var index = owningType.Events.IndexOf(existingEvent);
            owningType.Events[index] = merged;
        }
        else
        {
            var eventSymbol = new EventSymbol
            {
                Name = eventDef.Name,
                HasAdd = hasAdd,
                HasRemove = hasRemove,
                IsStatic = isStatic,
                IsVirtual = isVirtual,
                IsAbstract = isAbstract,
                IsOverride = isOverride,
                IsFinal = isFinal,
                AccessLevel = accessLevel,
                AddAccessLevel = hasAdd ? accessLevel : AccessLevel.Public,
                RemoveAccessLevel = hasRemove ? accessLevel : AccessLevel.Public,
            };

            owningType.Events.Add(eventSymbol);
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
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = constDecl.Span,
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
            TypeParameters = typeAlias.TypeParameters.IsEmpty
                ? Array.Empty<TypeParameterDef>()
                : typeAlias.TypeParameters.ToArray(),
            DeclarationLine = typeAlias.LineStart,
            DeclarationColumn = typeAlias.ColumnStart
        };

        _symbolTable.Define(aliasSymbol);
    }

}
