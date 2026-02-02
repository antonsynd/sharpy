using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Type definition checking (functions, classes, structs, interfaces, enums)
/// </summary>
internal partial class TypeChecker
{
    private void CheckFunction(FunctionDef functionDef)
    {
        _logger.LogDebug($"Type checking function: {functionDef.Name}");

        // Look up the function symbol to update its types
        // For class methods, we need to look up from the class's Methods list
        // since the methods were registered in a scope that no longer exists
        FunctionSymbol? functionSymbol = null;
        if (_currentClass != null)
        {
            if (functionDef.Name == "__init__")
            {
                // Find the matching constructor by declaration line number
                // This uniquely identifies which overload we're checking
                functionSymbol = _currentClass.Constructors
                    .FirstOrDefault(c => c.DeclarationLine == functionDef.LineStart);
            }
            else
            {
                // Find the method in the class's Methods list by name and line number
                functionSymbol = _currentClass.Methods
                    .FirstOrDefault(m => m.Name == functionDef.Name && m.DeclarationLine == functionDef.LineStart);
            }
        }
        else
        {
            // For top-level functions, look up from symbol table
            functionSymbol = _symbolTable.LookupFunction(functionDef.Name);
        }

        // Enter function scope FIRST so we can register type parameters before resolving types
        _symbolTable.EnterScope($"function:{functionDef.Name}");

        // Register type parameters for generic functions so they can be resolved in parameter/return types
        foreach (var typeParam in functionDef.TypeParameters)
        {
            var typeParamSymbol = new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                DeclaringType = null,  // No declaring type for standalone generic functions
                Constraints = typeParam.Constraints,
                DeclarationLine = functionDef.LineStart,
                DeclarationColumn = functionDef.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        // Resolve return type AFTER type parameters are registered
        var returnType = _typeResolver.ResolveTypeAnnotation(functionDef.ReturnType);

        // Special case: __init__ always returns None/void
        // (signature validation is in SignatureValidator)
        if (functionDef.Name == "__init__")
        {
            returnType = SemanticType.Void;
        }
        // Functions without explicit return type annotation default to void
        else if (returnType == SemanticType.Unknown && functionDef.ReturnType == null)
        {
            returnType = SemanticType.Void;
        }

        _currentFunctionReturnType = returnType;

        // Save previous method context and set new context for super() validation
        var previousMethodName = _currentMethodName;
        var previousMethodIsOverride = _currentMethodIsOverride;
        var previousMethodIsDunder = _currentMethodIsDunder;
        var previousControlFlowDepth = _controlFlowDepth;
        var previousSuperInitCalled = _superInitCalled;

        _currentMethodName = functionDef.Name;
        _currentMethodIsOverride = functionDef.Decorators.Any(d => d.Name == "override");
        _currentMethodIsDunder = IsDunderMethod(functionDef.Name);
        _controlFlowDepth = 0;
        _superInitCalled = false;

        // Validate @override is required for dunders that override System.Object methods
        if (_currentClass != null && _currentMethodIsDunder)
        {
            bool requiresOverride = ProtocolRegistry.IsObjectOverrideDunder(functionDef.Name);

            if (requiresOverride && !_currentMethodIsOverride)
            {
                AddError(
                    $"Dunder method '{functionDef.Name}' overrides a System.Object method and requires the @override decorator",
                    functionDef.LineStart,
                    functionDef.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidOverride,
                    span: functionDef.Span);
            }
        }

        // Validate @override is required when a subclass method shadows a virtual base method
        var currentClassBaseType = _currentClass != null ? GetBaseType(_currentClass) : null;
        if (_currentClass != null && !_currentMethodIsOverride && currentClassBaseType != null)
        {
            var (baseMethod, baseOwner) = FindMethodInHierarchy(currentClassBaseType, functionDef.Name);
            if (baseMethod != null && baseMethod.IsVirtual)
            {
                AddError(
                    $"Method '{functionDef.Name}' overrides a virtual method in base class '{baseOwner?.Name ?? currentClassBaseType.Name}' and requires the @override decorator",
                    functionDef.LineStart,
                    functionDef.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidOverride,
                    span: functionDef.Span);
            }
        }

        // Validate @override is only used when base class method is virtual, abstract, or override
        if (_currentClass != null && _currentMethodIsOverride && !_currentMethodIsDunder)
        {
            var (baseMethod, baseOwner) = currentClassBaseType != null
                ? FindMethodInHierarchy(currentClassBaseType, functionDef.Name)
                : (null, null);

            if (baseMethod == null)
            {
                // No matching method in base class
                AddError(
                    $"Method '{functionDef.Name}' is marked @override but no matching method exists in base class",
                    functionDef.LineStart,
                    functionDef.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidOverride,
                    span: functionDef.Span);
            }
            else if (!baseMethod.IsVirtual && !baseMethod.IsAbstract && !baseMethod.IsOverride)
            {
                // Check if the base owner is an interface - interface methods are implicitly abstract
                bool isInterfaceMethod = baseOwner?.TypeKind == TypeKind.Interface;

                if (!isInterfaceMethod)
                {
                    // Base method exists but is not virtual/abstract/override
                    AddError(
                        $"Cannot override '{functionDef.Name}' because the base class method in '{baseOwner?.Name}' is not marked @virtual or @abstract. Add @virtual to the method in the base class.",
                        functionDef.LineStart,
                        functionDef.ColumnStart,
                        code: DiagnosticCodes.Semantic.InvalidOverride,
                        span: functionDef.Span);
                }
            }
        }

        // Determine if method is abstract:
        // 1. Has @abstract decorator explicitly, OR
        // 2. Is in an @abstract class AND has ellipsis body (implicit abstract)
        bool hasAbstractDecorator = functionDef.Decorators.Any(d => d.Name == "abstract");
        bool isInAbstractClass = _currentClass?.IsAbstract == true;
        bool hasEllipsisBody = functionDef.Body.Length == 1
            && functionDef.Body[0] is ExpressionStatement exprStmt
            && exprStmt.Expression is EllipsisLiteral;

        bool isAbstractMethod = hasAbstractDecorator || (isInAbstractClass && hasEllipsisBody);

        // Validation
        if (hasAbstractDecorator && !hasEllipsisBody)
        {
            AddError($"Abstract method '{functionDef.Name}' must have '...' as its body",
                functionDef.LineStart, functionDef.ColumnStart, code: DiagnosticCodes.Semantic.MissingMethodBody,
                span: functionDef.Span);
        }

        if (hasAbstractDecorator && !isInAbstractClass && _currentClass != null)
        {
            AddError($"Abstract method '{functionDef.Name}' can only be declared in an abstract class. Add @abstract decorator to class '{_currentClass.Name}'",
                functionDef.LineStart, functionDef.ColumnStart, code: DiagnosticCodes.Semantic.MissingMethodBody,
                span: functionDef.Span);
        }

        // Note: Ellipsis body in concrete class is valid (generates NotImplementedException)
        // So we don't error on that case - it generates a stub that throws at runtime

        // Validate self parameter for instance methods
        // In Sharpy, methods without 'self' as the first parameter are treated as static methods
        // This is consistent with how the code generator handles them
        if (_currentClass != null)
        {
            // Check if this is a static method (explicitly decorated OR no self parameter)
            bool hasStaticDecorator = functionDef.Decorators.Any(d =>
                d.Name == "static");

            bool hasSelfParameter = functionDef.Parameters.Length > 0 &&
                functionDef.Parameters[0].Name == "self";

            // Method is static if it has decorator OR doesn't have self parameter
            // No error needed - code generator will make it static
            if (!hasStaticDecorator && !hasSelfParameter)
            {
                // This is implicitly a static method - valid, no error
            }
            else if (hasSelfParameter && hasStaticDecorator)
            {
                // Warning: static decorator with self parameter - self will be ignored
                // This is allowed but could be confusing
            }
            // Instance methods with self are valid - no action needed
        }

        // Validate parameter ordering: non-default parameters cannot follow default parameters
        bool hasSeenDefault = false;
        for (int i = 0; i < functionDef.Parameters.Length; i++)
        {
            var param = functionDef.Parameters[i];

            if (param.DefaultValue != null)
            {
                hasSeenDefault = true;
            }
            else if (hasSeenDefault)
            {
                AddError($"Non-default parameter '{param.Name}' cannot follow default parameters",
                    param.LineStart, param.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateParameter,
                    span: param.Span);
            }
        }

        // Default parameter validation is handled by DefaultParameterValidator in the validation pipeline

        // Register parameters in scope and update the function symbol's parameter types
        for (int i = 0; i < functionDef.Parameters.Length; i++)
        {
            var param = functionDef.Parameters[i];
            var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);

            // Special handling for 'self' parameter in methods
            if (i == 0 && param.Name == "self" && _currentClass != null)
            {
                paramType = new UserDefinedType { Symbol = _currentClass };
            }
            else if (param.Type == null)
            {
                // Require type annotations on all parameters except 'self'
                AddError($"Parameter '{param.Name}' requires a type annotation",
                    param.LineStart, param.ColumnStart, code: DiagnosticCodes.Semantic.MissingTypeAnnotation,
                    span: param.Span);
            }

            var paramSymbol = new VariableSymbol
            {
                Name = param.Name,
                Kind = SymbolKind.Parameter,
                Type = paramType,
                IsParameter = true,
                DeclarationLine = null,
                DeclarationColumn = null
            };
            _symbolTable.Define(paramSymbol);
            SemanticBinding.SetVariableType(paramSymbol, paramType);

            // Update the function symbol's parameter type
            if (functionSymbol != null && i < functionSymbol.Parameters.Count)
            {
                functionSymbol.Parameters[i] = functionSymbol.Parameters[i] with { Type = paramType };
            }

            // Type check default value if present
            if (param.DefaultValue != null)
            {
                // Set expected type for constructor inference (Some/None()/Ok/Err)
                var previousExpectedType = _expectedType;
                _expectedType = paramType is UnknownType ? null : paramType;
                var defaultType = CheckExpression(param.DefaultValue);
                _expectedType = previousExpectedType;
                if (!IsAssignable(defaultType, paramType))
                {
                    AddError($"Default value type '{defaultType.GetDisplayName()}' is not assignable to parameter type '{paramType.GetDisplayName()}'",
                        param.LineStart, param.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: param.DefaultValue?.Span);
                }
            }
        }

        // Update the function symbol's return type and parameter types
        if (functionSymbol != null)
        {
            // Create a new FunctionSymbol with updated return type
            var updatedSymbol = functionSymbol with { ReturnType = returnType };
            // Update the symbol in the symbol table
            _symbolTable.UpdateSymbol(updatedSymbol);
        }

        // Check function body
        foreach (var statement in functionDef.Body)
        {
            CheckStatement(statement);
        }

        // Control flow validation (break/continue, unreachable code, missing return)
        // is handled by ControlFlowValidator in the validation pipeline

        // Restore previous method context
        _currentMethodName = previousMethodName;
        _currentMethodIsOverride = previousMethodIsOverride;
        _currentMethodIsDunder = previousMethodIsDunder;
        _controlFlowDepth = previousControlFlowDepth;
        _superInitCalled = previousSuperInitCalled;

        _symbolTable.ExitScope();
        _currentFunctionReturnType = null;
    }

    private void CheckClass(ClassDef classDef)
    {
        _logger.LogDebug($"Type checking class: {classDef.Name}");

        // Look up the class symbol
        var classSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
        if (classSymbol == null)
        {
            AddError($"Class symbol for '{classDef.Name}' not found", classDef.LineStart, classDef.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedType,
                span: classDef.Span);
            return;
        }

        // Enter class scope
        _symbolTable.EnterScope($"class:{classDef.Name}");

        // Register type parameters in the scope so they can be resolved in field/method types
        foreach (var typeParam in classDef.TypeParameters)
        {
            var typeParamSymbol = new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                DeclaringType = classSymbol,
                Constraints = typeParam.Constraints,
                DeclarationLine = classDef.LineStart,
                DeclarationColumn = classDef.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        // Resolve field types first (before checking methods that might reference them)
        for (int i = 0; i < classSymbol.Fields.Count; i++)
        {
            var fieldSymbol = classSymbol.Fields[i];
            if (GetVariableType(fieldSymbol) == SemanticType.Unknown)
            {
                // Find the corresponding VariableDeclaration in the AST
                var fieldDecl = classDef.Body
                    .OfType<VariableDeclaration>()
                    .FirstOrDefault(v => v.Name == fieldSymbol.Name);

                if (fieldDecl != null)
                {
                    var resolvedType = _typeResolver.ResolveTypeAnnotation(fieldDecl.Type);
                    classSymbol.Fields[i] = fieldSymbol with { Type = resolvedType };
                    SemanticBinding.SetVariableType(classSymbol.Fields[i], resolvedType);
                }
            }
        }

        // Set current class for method type checking (used for self parameter typing)
        var previousClass = _currentClass;
        _currentClass = classSymbol;

        // Check all members
        foreach (var statement in classDef.Body)
        {
            CheckStatement(statement);
        }

        // Validate constructor overloads after all members are checked
        ValidateConstructorOverloads(classSymbol);

        // Validate interface implementations (skip for abstract classes)
        if (!classSymbol.IsAbstract)
        {
            ValidateInterfaceImplementations(classSymbol, classDef.LineStart, classDef.ColumnStart, classDef.Span);
        }

        // Restore previous class
        _currentClass = previousClass;
        // Access validation is handled by AccessValidator in the validation pipeline

        _symbolTable.ExitScope();
    }

    private void CheckStruct(StructDef structDef)
    {
        _logger.LogDebug($"Type checking struct: {structDef.Name}");

        // Look up the struct symbol
        var structSymbol = _symbolTable.Lookup(structDef.Name) as TypeSymbol;
        if (structSymbol == null)
        {
            AddError($"Struct symbol for '{structDef.Name}' not found", structDef.LineStart, structDef.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedType,
                span: structDef.Span);
            return;
        }

        // Enter struct scope
        _symbolTable.EnterScope($"struct:{structDef.Name}");

        // Register type parameters in the scope so they can be resolved in field/method types
        foreach (var typeParam in structDef.TypeParameters)
        {
            var typeParamSymbol = new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                DeclaringType = structSymbol,
                Constraints = typeParam.Constraints,
                DeclarationLine = structDef.LineStart,
                DeclarationColumn = structDef.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        // Resolve field types first (before checking methods that might reference them)
        for (int i = 0; i < structSymbol.Fields.Count; i++)
        {
            var fieldSymbol = structSymbol.Fields[i];
            if (GetVariableType(fieldSymbol) == SemanticType.Unknown)
            {
                // Find the corresponding VariableDeclaration in the AST
                var fieldDecl = structDef.Body
                    .OfType<VariableDeclaration>()
                    .FirstOrDefault(v => v.Name == fieldSymbol.Name);

                if (fieldDecl != null)
                {
                    var resolvedType = _typeResolver.ResolveTypeAnnotation(fieldDecl.Type);
                    structSymbol.Fields[i] = fieldSymbol with { Type = resolvedType };
                    SemanticBinding.SetVariableType(structSymbol.Fields[i], resolvedType);
                }
            }
        }

        // Set current class for method type checking (structs behave like classes for self parameter)
        var previousClass = _currentClass;
        _currentClass = structSymbol;

        // Check all members
        foreach (var statement in structDef.Body)
        {
            CheckStatement(statement);
        }

        // Validate struct-specific rules
        ValidateStructRules(structSymbol, structDef);

        // Validate interface implementations (structs must implement all interface methods)
        ValidateInterfaceImplementations(structSymbol, structDef.LineStart, structDef.ColumnStart, structDef.Span);

        // Restore previous class
        _currentClass = previousClass;
        // Access validation is handled by AccessValidator in the validation pipeline

        _symbolTable.ExitScope();
    }

    private void CheckInterface(InterfaceDef interfaceDef)
    {
        _logger.LogDebug($"Type checking interface: {interfaceDef.Name}");

        // Look up the interface symbol
        var interfaceSymbol = _symbolTable.Lookup(interfaceDef.Name) as TypeSymbol;
        if (interfaceSymbol == null)
        {
            AddError($"Interface symbol for '{interfaceDef.Name}' not found", interfaceDef.LineStart, interfaceDef.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedType,
                span: interfaceDef.Span);
            return;
        }

        // Enter interface scope to resolve type parameters
        _symbolTable.EnterScope($"interface:{interfaceDef.Name}");

        // Register type parameters in the scope so they can be resolved in method signatures
        foreach (var typeParam in interfaceDef.TypeParameters)
        {
            var typeParamSymbol = new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                DeclaringType = interfaceSymbol,
                Constraints = typeParam.Constraints,
                DeclarationLine = interfaceDef.LineStart,
                DeclarationColumn = interfaceDef.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        // Resolve method parameter types and return types
        // Interface methods are registered in NameResolver but with Unknown types
        // We need to resolve them here using the TypeResolver
        foreach (var statement in interfaceDef.Body)
        {
            if (statement is FunctionDef method)
            {
                // Find the corresponding method symbol in the interface
                var methodIndex = interfaceSymbol.Methods.FindIndex(m => m.Name == method.Name);
                if (methodIndex >= 0)
                {
                    var methodSymbol = interfaceSymbol.Methods[methodIndex];

                    // Resolve return type
                    var returnType = _typeResolver.ResolveTypeAnnotation(method.ReturnType);
                    if (returnType == SemanticType.Unknown && method.ReturnType == null)
                    {
                        returnType = SemanticType.Void;
                    }

                    // Resolve parameter types
                    var updatedParameters = new List<ParameterSymbol>();
                    for (int i = 0; i < method.Parameters.Length; i++)
                    {
                        var param = method.Parameters[i];
                        var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);

                        // Special handling for 'self' parameter
                        if (i == 0 && param.Name == "self")
                        {
                            paramType = new UserDefinedType { Name = interfaceSymbol.Name, Symbol = interfaceSymbol };
                        }
                        else if (param.Type == null && param.Name != "self")
                        {
                            AddError($"Interface method parameter '{param.Name}' requires a type annotation",
                                param.LineStart, param.ColumnStart, code: DiagnosticCodes.Semantic.MissingTypeAnnotation,
                                span: param.Span);
                        }

                        updatedParameters.Add(new ParameterSymbol
                        {
                            Name = param.Name,
                            Type = paramType,
                            HasDefault = param.DefaultValue != null,
                            DefaultValue = param.DefaultValue
                        });
                    }

                    // Update the method symbol with resolved types
                    interfaceSymbol.Methods[methodIndex] = methodSymbol with
                    {
                        ReturnType = returnType,
                        Parameters = updatedParameters
                    };
                }
            }
        }

        _symbolTable.ExitScope();
    }

    private void CheckEnum(EnumDef enumDef)
    {
        _logger.LogDebug($"Type checking enum: {enumDef.Name}");

        // Validate enum-specific rules
        ValidateEnumRules(enumDef);
    }

}
