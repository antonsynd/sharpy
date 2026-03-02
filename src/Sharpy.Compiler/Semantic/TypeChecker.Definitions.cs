using System.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

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
            if (functionDef.Name == DunderNames.Init)
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

        // Enter an isolated narrowing scope for this function.
        // Type narrowings from the enclosing scope should NOT be visible inside this function,
        // because nested functions can be called later when the narrowing condition no longer holds.
        // This is control-flow narrowing isolation, not lexical scoping.
        using var _ = _narrowingContext.EnterIsolatedScope();

        // Register type parameters for generic functions so they can be resolved in parameter/return types
        foreach (var typeParam in functionDef.TypeParameters)
        {
            var typeParamSymbol = new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                DeclaringType = null,  // No declaring type for standalone generic functions
                Constraints = typeParam.Constraints,
                Variance = typeParam.Variance,
                DeclarationLine = typeParam.LineStart,
                DeclarationColumn = typeParam.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        // Resolve return type AFTER type parameters are registered
        var returnType = _typeResolver.ResolveTypeAnnotation(functionDef.ReturnType);

        // Special case: __init__ always returns None/void
        // (signature validation is in SignatureValidator)
        if (functionDef.Name == DunderNames.Init)
        {
            returnType = SemanticType.Void;
        }
        // Functions without explicit return type annotation default to void
        else if (returnType == SemanticType.Unknown && functionDef.ReturnType == null)
        {
            returnType = SemanticType.Void;
        }

        // Save previous return type BEFORE overwriting (needed for nested function restore)
        var previousFunctionReturnType = _currentFunctionReturnType;
        _currentFunctionReturnType = returnType;

        // Save previous method context and set new context for super() validation
        var previousMethodName = _currentMethodName;
        var previousMethodIsOverride = _currentMethodIsOverride;
        var previousMethodIsDunder = _currentMethodIsDunder;
        var previousControlFlowDepth = _controlFlowDepth;
        var previousSuperInitCalled = _superInitCalled;

        _currentMethodName = functionDef.Name;
        _currentMethodIsOverride = functionDef.Decorators.Any(d => d.Name == DecoratorNames.Override)
            || (_currentClass != null && IsDunderMethod(functionDef.Name)
                && ProtocolRegistry.IsObjectOverrideDunder(functionDef.Name));
        _currentMethodIsDunder = IsDunderMethod(functionDef.Name);
        _controlFlowDepth = 0;
        _superInitCalled = false;

        // Validate @override is required when a subclass method shadows a virtual, abstract, or override base method
        var currentClassBaseType = _currentClass != null ? GetBaseType(_currentClass) : null;
        if (_currentClass != null && !_currentMethodIsOverride && currentClassBaseType != null)
        {
            var (baseMethod, baseOwner) = FindMethodInHierarchy(currentClassBaseType, functionDef.Name);
            if (baseMethod != null && (baseMethod.IsVirtual || baseMethod.IsAbstract || baseMethod.IsOverride))
            {
                var methodKind = baseMethod.IsAbstract ? "an abstract" : "a virtual";
                AddError(
                    $"Method '{functionDef.Name}' overrides {methodKind} method in base class '{baseOwner?.Name ?? currentClassBaseType.Name}' and requires the @override decorator",
                    functionDef.LineStart,
                    functionDef.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidOverride,
                    span: functionDef.Span);
            }
        }

        // Validate @override is required when a class method shadows an interface default method
        if (_currentClass != null && !_currentMethodIsOverride && _currentClass.TypeKind != TypeKind.Interface)
        {
            foreach (var iface in GetInterfaces(_currentClass))
            {
                var ifaceMethod = iface.Methods.FirstOrDefault(m => m.Name == functionDef.Name);
                if (ifaceMethod != null && !ifaceMethod.IsAbstract)
                {
                    AddError(
                        $"Method '{functionDef.Name}' overrides a default method in interface '{iface.Name}' and requires the @override decorator",
                        functionDef.LineStart,
                        functionDef.ColumnStart,
                        code: DiagnosticCodes.Semantic.InvalidOverride,
                        span: functionDef.Span);
                    break;
                }
            }
        }

        // Validate @override is only used when base class method is virtual, abstract, or override
        if (_currentClass != null && _currentMethodIsOverride && !_currentMethodIsDunder)
        {
            var (baseMethod, baseOwner) = currentClassBaseType != null
                ? FindMethodInHierarchy(currentClassBaseType, functionDef.Name)
                : (null, null);

            // If not found in base class, also check interfaces for default methods
            if (baseMethod == null)
            {
                foreach (var iface in GetInterfaces(_currentClass))
                {
                    var ifaceMethod = iface.Methods.FirstOrDefault(m => m.Name == functionDef.Name);
                    if (ifaceMethod != null)
                    {
                        baseMethod = ifaceMethod;
                        baseOwner = iface;
                        break;
                    }
                }
            }

            if (baseMethod == null)
            {
                // No matching method in base class or interfaces
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
        bool hasAbstractDecorator = functionDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
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
                d.Name == DecoratorNames.Static);

            bool hasSelfParameter = functionDef.Parameters.Length > 0 &&
                functionDef.Parameters[0].Name == PythonNames.Self;

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
        // Keyword-only params (after * or *args) reset the default tracking since they
        // may be required even when previous normal/positional-only params have defaults
        bool hasSeenDefault = false;
        for (int i = 0; i < functionDef.Parameters.Length; i++)
        {
            var param = functionDef.Parameters[i];

            // Keyword-only zone resets the default tracking
            if (param.Kind == Parser.Ast.ParameterKind.KeywordOnly)
            {
                hasSeenDefault = false;
            }

            // Variadic parameters don't participate in default ordering validation
            if (param.IsVariadic)
                continue;

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
            if (i == 0 && param.Name == PythonNames.Self && _currentClass != null)
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

            // Also update the reference in the owning TypeSymbol's lists.
            // The symbol table and TypeSymbol.Methods/Constructors are separate storage;
            // without this sync, FindMethodInHierarchy reads stale Unknown return types.
            if (_currentClass != null)
            {
                if (functionDef.Name == DunderNames.Init)
                {
                    var idx = _currentClass.Constructors.IndexOf(functionSymbol);
                    if (idx >= 0)
                        _currentClass.Constructors[idx] = updatedSymbol;

                    // __init__ is stored in both Constructors and Methods (NameResolver adds to both).
                    // FindMethodInHierarchy searches Methods, so we must sync here too.
                    var methodIdx = _currentClass.Methods.IndexOf(functionSymbol);
                    if (methodIdx >= 0)
                        _currentClass.Methods[methodIdx] = updatedSymbol;
                }
                else
                {
                    var idx = _currentClass.Methods.IndexOf(functionSymbol);
                    if (idx >= 0)
                        _currentClass.Methods[idx] = updatedSymbol;

                    // Also update OperatorMethods/ProtocolMethods if the method appears there
                    foreach (var kvp in _currentClass.OperatorMethods)
                    {
                        var opIdx = kvp.Value.IndexOf(functionSymbol);
                        if (opIdx >= 0)
                            kvp.Value[opIdx] = updatedSymbol;
                    }
                    foreach (var kvp in _currentClass.ProtocolMethods)
                    {
                        var protoIdx = kvp.Value.IndexOf(functionSymbol);
                        if (protoIdx >= 0)
                            kvp.Value[protoIdx] = updatedSymbol;
                    }
                }
            }
        }

        // Save and set async flag before body checking so CheckAwaitExpression
        // can validate that await is used inside async functions.
        var previousIsAsync = _currentFunctionIsAsync;
        _currentFunctionIsAsync = functionDef.IsAsync;

        // Detect generators early: set flag before body checking so CheckReturn
        // knows bare return is valid (it becomes yield break).
        var previousIsGenerator = _currentFunctionIsGenerator;
        var isGenerator = ContainsYield(functionDef.Body);
        _currentFunctionIsGenerator = isGenerator;

        // Async constructors are not supported (C# constructors cannot be async)
        if (functionDef.IsAsync && functionDef.Name == DunderNames.Init)
        {
            AddError(
                "Constructors cannot be declared as 'async'; remove 'async' from '__init__'",
                functionDef.LineStart, functionDef.ColumnStart,
                code: DiagnosticCodes.Semantic.UnsupportedFeature,
                span: functionDef.Span);
        }

        // Check function body
        foreach (var statement in functionDef.Body)
        {
            CheckStatement(statement);
        }

        // Mark generator metadata and update symbol after body checking
        if (isGenerator)
        {
            _semanticInfo.MarkAsGenerator(functionDef);
            if (functionSymbol != null)
            {
                // For class methods, look up the current symbol from the TypeSymbol's Methods list,
                // because the TypeChecker creates a new class scope (NameResolver's scope was popped),
                // so the symbol table doesn't contain the method. The Methods list has the
                // updatedSymbol (from the return type update at line 319), which is also the same
                // reference stored in ProtocolMethods/OperatorMethods.
                // For top-level functions, fall back to the symbol table.
                var currentSymbol = _currentClass?.Methods.FirstOrDefault(m => m.Name == functionDef.Name && m.DeclarationLine == functionDef.LineStart)
                    ?? _symbolTable.Lookup(functionDef.Name) as FunctionSymbol;
                Debug.Assert(currentSymbol != null, $"Generator function '{functionDef.Name}' not found in Methods list or SymbolTable");
                if (currentSymbol != null)
                {
                    currentSymbol.IsGenerator = true;

                    // For non-dunder generators (standalone functions and regular class methods),
                    // wrap the return type so callers see IEnumerable<T>/IAsyncEnumerable<T> instead of T.
                    // Dunder methods (__iter__, __reversed__) keep their element type as ReturnType
                    // because SynthesisAnalyzer reads it to determine interface type arguments,
                    // and codegen handles the wrapping independently via AST annotations.
                    var isDunder = DunderDetector.IsDunderMethod(functionDef.Name);
                    if (!isDunder && currentSymbol.ReturnType is not (VoidType or UnknownType))
                    {
                        var enumerableName = functionDef.IsAsync
                            ? Shared.CSharpTypeNames.IAsyncEnumerable
                            : Shared.CSharpTypeNames.IEnumerable;
                        currentSymbol.ReturnType = new GenericType
                        {
                            Name = enumerableName,
                            TypeArguments = new List<SemanticType> { currentSymbol.ReturnType }
                        };
                    }
                }
            }
        }

        // Mark async metadata and wrap return type in TaskType.
        // Skip if async was used in an invalid context (async __init__)
        // to avoid emitting broken C# (e.g., async constructor).
        // Async generators get IsAsync but NOT TaskType wrapping — their return type
        // is already IAsyncEnumerable<T> from the generator section above.
        var asyncHasError = functionDef.IsAsync && functionDef.Name == DunderNames.Init;
        if (functionDef.IsAsync && !asyncHasError)
        {
            var asyncSymbol = _currentClass?.Methods.FirstOrDefault(m => m.Name == functionDef.Name && m.DeclarationLine == functionDef.LineStart)
                ?? _symbolTable.Lookup(functionDef.Name) as FunctionSymbol;
            if (asyncSymbol != null)
            {
                asyncSymbol.IsAsync = true;

                // Async generators already have IAsyncEnumerable<T> return type — skip Task wrapping
                if (!isGenerator)
                {
                    if (asyncSymbol.ReturnType is VoidType or UnknownType)
                    {
                        asyncSymbol.ReturnType = new TaskType { ResultType = null };
                    }
                    else
                    {
                        asyncSymbol.ReturnType = new TaskType { ResultType = asyncSymbol.ReturnType };
                    }
                }
            }
        }

        // Control flow validation (break/continue, unreachable code, missing return)
        // is handled by ControlFlowValidator in the validation pipeline

        // Restore previous method context
        _currentMethodName = previousMethodName;
        _currentMethodIsOverride = previousMethodIsOverride;
        _currentMethodIsDunder = previousMethodIsDunder;
        _currentFunctionIsGenerator = previousIsGenerator;
        _currentFunctionIsAsync = previousIsAsync;
        _controlFlowDepth = previousControlFlowDepth;
        _superInitCalled = previousSuperInitCalled;

        _symbolTable.ExitScope();
        _currentFunctionReturnType = previousFunctionReturnType;
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
                Variance = typeParam.Variance,
                DeclarationLine = typeParam.LineStart,
                DeclarationColumn = typeParam.ColumnStart
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

        // Resolve property types (before checking methods that might reference them)
        ResolvePropertyTypes(classSymbol, classDef.Body);

        // Set current class for method type checking (used for self parameter typing)
        var previousClass = _currentClass;
        _currentClass = classSymbol;

        // Check all members
        foreach (var statement in classDef.Body)
        {
            CheckStatement(statement);
        }

        // Validate constructor overloads after all members are checked
        ValidateConstructorOverloads(classSymbol, classDef.Body);

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
                Variance = typeParam.Variance,
                DeclarationLine = typeParam.LineStart,
                DeclarationColumn = typeParam.ColumnStart
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

        // Resolve property types (before checking methods that might reference them)
        ResolvePropertyTypes(structSymbol, structDef.Body);

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
                Variance = typeParam.Variance,
                DeclarationLine = typeParam.LineStart,
                DeclarationColumn = typeParam.ColumnStart
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
                        if (i == 0 && param.Name == PythonNames.Self)
                        {
                            paramType = new UserDefinedType { Name = interfaceSymbol.Name, Symbol = interfaceSymbol };
                        }
                        else if (param.Type == null && param.Name != PythonNames.Self)
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
                            DefaultValue = param.DefaultValue,
                            IsVariadic = param.IsVariadic,
                            IsPositionalOnly = param.Kind == Parser.Ast.ParameterKind.PositionalOnly,
                            IsKeywordOnly = param.Kind == Parser.Ast.ParameterKind.KeywordOnly
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

        // Resolve property types
        ResolvePropertyTypes(interfaceSymbol, interfaceDef.Body);

        // Type-check default method bodies (methods with real implementations)
        // Set _currentClass to the interface symbol so CheckFunction resolves self correctly
        var previousClass = _currentClass;
        _currentClass = interfaceSymbol;

        foreach (var statement in interfaceDef.Body)
        {
            if (statement is FunctionDef method && !IsAbstractBody(method))
            {
                CheckFunction(method);
            }
        }

        _currentClass = previousClass;

        _symbolTable.ExitScope();
    }

    private void CheckEnum(EnumDef enumDef)
    {
        _logger.LogDebug($"Type checking enum: {enumDef.Name}");

        // Validate enum-specific rules
        ValidateEnumRules(enumDef);
    }

    private void CheckUnion(UnionDef unionDef)
    {
        _logger.LogDebug($"Type checking union: {unionDef.Name}");

        var unionSymbol = _symbolTable.Lookup(unionDef.Name) as TypeSymbol;
        if (unionSymbol == null)
        {
            AddError($"Union symbol for '{unionDef.Name}' not found", unionDef.LineStart, unionDef.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedType, span: unionDef.Span);
            return;
        }

        // Enter a scope for type parameter resolution
        _symbolTable.EnterScope($"union:{unionDef.Name}");

        try
        {
            // Register type parameters in the scope
            foreach (var typeParam in unionDef.TypeParameters)
            {
                var typeParamSymbol = new TypeParameterSymbol
                {
                    Name = typeParam.Name,
                    Kind = SymbolKind.TypeParameter,
                    DeclaringType = unionSymbol,
                    Constraints = typeParam.Constraints,
                    Variance = typeParam.Variance,
                    DeclarationLine = typeParam.LineStart,
                    DeclarationColumn = typeParam.ColumnStart
                };
                _symbolTable.Define(typeParamSymbol);
            }

            // Check for duplicate case names and resolve field types
            var seenCaseNames = new HashSet<string>();

            for (int i = 0; i < unionDef.Cases.Length; i++)
            {
                var caseDef = unionDef.Cases[i];

                if (!seenCaseNames.Add(caseDef.Name))
                {
                    AddError($"Union case '{caseDef.Name}' is already defined in union '{unionDef.Name}'",
                        caseDef.LineStart, caseDef.ColumnStart,
                        code: DiagnosticCodes.Semantic.DuplicateUnionCase, span: caseDef.Span);
                    continue;
                }

                // Reject case names that collide with the union type name itself,
                // which would produce a C# nested class with the same name as its enclosing class
                if (caseDef.Name == unionDef.Name)
                {
                    AddError($"Union case '{caseDef.Name}' cannot have the same name as its enclosing union '{unionDef.Name}'",
                        caseDef.LineStart, caseDef.ColumnStart,
                        code: DiagnosticCodes.Semantic.UnionCaseNameConflict, span: caseDef.Span);
                    continue;
                }

                // Get the corresponding case symbol
                var caseSymbol = unionSymbol.UnionCases.FirstOrDefault(c => c.Name == caseDef.Name);
                if (caseSymbol == null)
                    continue;

                // Resolve field types
                foreach (var field in caseDef.Fields)
                {
                    var resolvedType = _typeResolver.ResolveTypeAnnotation(field.Type);
                    caseSymbol.Fields.Add(new VariableSymbol
                    {
                        Name = field.Name,
                        Kind = SymbolKind.Variable,
                        Type = resolvedType,
                        DeclarationLine = field.LineStart,
                        DeclarationColumn = field.ColumnStart
                    });
                }
            }
        }
        finally
        {
            _symbolTable.ExitScope();
        }
    }

    private void CheckDelegate(DelegateDef delegateDef)
    {
        _logger.LogDebug($"Type checking delegate: {delegateDef.Name}");

        var delegateSymbol = _symbolTable.Lookup(delegateDef.Name) as TypeSymbol;
        if (delegateSymbol == null)
        {
            AddError($"Delegate symbol for '{delegateDef.Name}' not found", delegateDef.LineStart, delegateDef.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedType, span: delegateDef.Span);
            return;
        }

        _symbolTable.EnterScope($"delegate:{delegateDef.Name}");

        try
        {
            // Register type parameters in the scope so they can be resolved
            foreach (var typeParam in delegateDef.TypeParameters)
            {
                var typeParamSymbol = new TypeParameterSymbol
                {
                    Name = typeParam.Name,
                    Kind = SymbolKind.TypeParameter,
                    DeclaringType = delegateSymbol,
                    Constraints = typeParam.Constraints,
                    Variance = typeParam.Variance,
                    DeclarationLine = typeParam.LineStart,
                    DeclarationColumn = typeParam.ColumnStart
                };
                _symbolTable.Define(typeParamSymbol);
            }

            // Find the synthetic Invoke method
            var invokeSymbol = delegateSymbol.Methods.FirstOrDefault(m => m.Name == "Invoke");
            if (invokeSymbol == null)
                return;

            // Resolve parameter types
            var updatedParameters = new List<ParameterSymbol>();
            for (int i = 0; i < delegateDef.Parameters.Length; i++)
            {
                var param = delegateDef.Parameters[i];
                var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);

                if (param.Type == null)
                {
                    AddError($"Delegate parameter '{param.Name}' requires a type annotation",
                        param.LineStart, param.ColumnStart, code: DiagnosticCodes.Semantic.MissingTypeAnnotation,
                        span: param.Span);
                }

                updatedParameters.Add(new ParameterSymbol
                {
                    Name = param.Name,
                    Type = paramType,
                    HasDefault = param.DefaultValue != null,
                    DefaultValue = param.DefaultValue,
                    IsVariadic = param.IsVariadic,
                    IsPositionalOnly = param.Kind == Parser.Ast.ParameterKind.PositionalOnly,
                    IsKeywordOnly = param.Kind == Parser.Ast.ParameterKind.KeywordOnly
                });
            }

            // Resolve return type
            var returnType = _typeResolver.ResolveTypeAnnotation(delegateDef.ReturnType);
            if (returnType == SemanticType.Unknown && delegateDef.ReturnType == null)
            {
                returnType = SemanticType.Void;
            }

            // Update the Invoke method symbol with resolved types
            var invokeIndex = delegateSymbol.Methods.IndexOf(invokeSymbol);
            delegateSymbol.Methods[invokeIndex] = invokeSymbol with
            {
                Parameters = updatedParameters,
                ReturnType = returnType
            };
        }
        finally
        {
            _symbolTable.ExitScope();
        }
    }

    /// <summary>
    /// Resolves property types from their type annotations in the AST.
    /// Must be called after field type resolution and before member checking.
    /// </summary>
    private void ResolvePropertyTypes(TypeSymbol typeSymbol, System.Collections.Immutable.ImmutableArray<Statement> body)
    {
        for (int i = 0; i < typeSymbol.Properties.Count; i++)
        {
            var propSymbol = typeSymbol.Properties[i];
            if (propSymbol.Type is UnknownType)
            {
                // Find the corresponding PropertyDef in the AST
                var propDef = body
                    .OfType<PropertyDef>()
                    .FirstOrDefault(p => p.Name == propSymbol.Name);

                if (propDef != null)
                {
                    SemanticType resolvedType;
                    if (propDef.IsFunctionStyle)
                    {
                        // For function-style properties, type comes from ReturnType (getter) or parameter type (setter)
                        if (propDef.ReturnType != null)
                        {
                            resolvedType = _typeResolver.ResolveTypeAnnotation(propDef.ReturnType);
                        }
                        else if (propDef.Parameters.Length > 1)
                        {
                            // Setter: second parameter is the value (first is self)
                            resolvedType = _typeResolver.ResolveTypeAnnotation(propDef.Parameters[^1].Type);
                        }
                        else
                        {
                            resolvedType = SemanticType.Unknown;
                        }
                    }
                    else
                    {
                        // Auto-property: type comes from type annotation
                        resolvedType = _typeResolver.ResolveTypeAnnotation(propDef.Type);
                    }

                    if (resolvedType != SemanticType.Unknown)
                    {
                        typeSymbol.Properties[i] = propSymbol with { Type = resolvedType };
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check if a function body is abstract (single ellipsis or pass statement).
    /// </summary>
    private static bool IsAbstractBody(FunctionDef func)
    {
        return func.Body.Length == 1 &&
            (func.Body[0] is PassStatement ||
             (func.Body[0] is ExpressionStatement es && es.Expression is EllipsisLiteral));
    }

    private static bool ContainsYield(System.Collections.Immutable.ImmutableArray<Statement> statements)
        => StatementWalker.Any(statements, stmt => stmt is YieldStatement);

}
