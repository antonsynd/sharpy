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
    /// <summary>
    /// Pre-pass: resolve parameter and return types for a module-level function
    /// so forward references from class methods see resolved types instead of Unknown.
    /// Only resolves type annotations — does not check the function body.
    /// </summary>
    private void ResolveModuleFunctionSignature(FunctionDef functionDef)
    {
        // For overloaded module-level functions, the symbol table only holds the
        // first overload, so resolve the specific symbol by declaration line.
        var overloads = _symbolTable.LookupFunctionOverloads(functionDef.Name);
        var functionSymbol = overloads is { Count: > 1 }
            ? overloads.FirstOrDefault(o => o.DeclarationLine == functionDef.LineStart)
            : _symbolTable.LookupFunction(functionDef.Name);
        if (functionSymbol == null)
            return;

        ResolveFunctionSignatureInto(functionSymbol, functionDef);
    }

    /// <summary>
    /// Pre-pass: resolve parameter and return types for all methods of a class so that
    /// forward references between methods (e.g., __init__ calling a method declared later
    /// in the body) see resolved types instead of Unknown. Without this, the type checker
    /// processes method bodies in declaration order, so a call to a method defined later
    /// reads a null/Unknown return type and produces an internal error (SPY0907).
    /// Must run after _currentClass is set so LookupFunctionSymbol resolves class members.
    /// </summary>
    private void ResolveClassMethodSignatures(IReadOnlyList<Statement> body)
    {
        foreach (var statement in body)
        {
            if (statement is FunctionDef methodDef)
            {
                var methodSymbol = LookupFunctionSymbol(methodDef);
                if (methodSymbol != null)
                    ResolveFunctionSignatureInto(methodSymbol, methodDef);
            }
        }
    }

    /// <summary>
    /// Resolves a function/method symbol's parameter and return types from its
    /// annotations and writes them onto the symbol in-place. Shared by the module-level
    /// and class-method signature pre-passes. The 'self' parameter is left untouched —
    /// it is typed as the enclosing class when the method body is checked.
    /// </summary>
    private void ResolveFunctionSignatureInto(FunctionSymbol functionSymbol, FunctionDef functionDef)
    {
        // Enter a temporary scope for generic type parameters
        _symbolTable.EnterScope($"pre-pass:{functionDef.Name}");

        foreach (var typeParam in functionDef.TypeParameters)
        {
            var typeParamSymbol = new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                DeclaringType = null,
                Constraints = typeParam.Constraints,
                Variance = typeParam.Variance,
                DeclarationLine = typeParam.LineStart,
                DeclarationColumn = typeParam.ColumnStart,
                NameDeclarationLine = typeParam.LineStart,
                NameDeclarationColumn = typeParam.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        // Set static context for Self type validation before resolving types, mirroring
        // CheckFunction. A method is static if it has @static or no self parameter.
        // This must match the body-check path so the (cached) Self resolution emits the
        // correct diagnostic exactly once.
        bool isStaticMethod = _currentClass != null &&
            (functionDef.Decorators.Any(d => d.Name == DecoratorNames.Static) ||
             functionDef.Parameters.Length == 0 ||
             functionDef.Parameters[0].Name != PythonNames.Self);
        _typeResolver.SetIsStaticContext(isStaticMethod);

        // Resolve return type from annotation
        var returnType = _typeResolver.ResolveTypeAnnotation(functionDef.ReturnType);
        if (functionDef.Name == DunderNames.Init)
        {
            returnType = SemanticType.Void;
        }
        else if (returnType == SemanticType.Unknown && functionDef.ReturnType == null)
        {
            returnType = SemanticType.Void;
        }

        // Resolve parameter types from annotations (skip 'self' — it is typed during
        // body checking against the enclosing class).
        for (int i = 0; i < functionDef.Parameters.Length; i++)
        {
            var param = functionDef.Parameters[i];
            if (param.Name == PythonNames.Self)
                continue;
            var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);
            if (param.Type == null)
            {
                paramType = SemanticType.Unknown;
            }
            if (i < functionSymbol.Parameters.Count)
            {
                functionSymbol.Parameters[i] = functionSymbol.Parameters[i] with { Type = paramType };
            }
        }

        _typeResolver.SetIsStaticContext(false);

        // Mutate the function symbol in-place so that all scopes referencing
        // this symbol (e.g., importing module scopes) see the updated return type.
        // Using `with` would create a new record, breaking reference sharing.
        functionSymbol.ReturnType = returnType;

        _symbolTable.ExitScope();
    }

    private void CheckFunction(FunctionDef functionDef)
    {
        _logger.LogDebug($"Type checking function: {functionDef.Name}");

        // Nested function: if we're inside a function (not at module level),
        // register the nested function symbol in the enclosing scope so it can be called by name.
        // NameResolver only handles module-level declarations, so nested functions need registration here.
        if (_currentFunctionReturnType != null)
        {
            var existingSymbol = _symbolTable.Lookup(functionDef.Name, searchParents: false);
            if (existingSymbol == null)
            {
                var parameters = functionDef.Parameters.Select(p => new ParameterSymbol
                {
                    Name = p.Name,
                    Type = SemanticType.Unknown,
                    HasDefault = p.DefaultValue != null,
                    DefaultValue = p.DefaultValue,
                    IsVariadic = p.IsVariadic,
                    IsPositionalOnly = p.Kind == ParameterKind.PositionalOnly,
                    IsKeywordOnly = p.Kind == ParameterKind.KeywordOnly,
                    Modifier = p.Modifier
                }).ToList();

                var nestedFuncSymbol = new FunctionSymbol
                {
                    Name = functionDef.Name,
                    Kind = SymbolKind.Function,
                    AccessLevel = AccessLevel.Private,
                    Parameters = parameters,
                    TypeParameters = functionDef.TypeParameters.ToList(),
                    DeclarationSpan = functionDef.Span,
                    DeclarationLine = functionDef.LineStart,
                    DeclarationColumn = functionDef.ColumnStart,
                    NameDeclarationLine = functionDef.NameLineStart,
                    NameDeclarationColumn = functionDef.NameColumnStart,
                    Documentation = functionDef.DocString
                };

                _symbolTable.Define(nestedFuncSymbol);
            }
        }

        // Look up the function symbol to update its types
        var functionSymbol = LookupFunctionSymbol(functionDef);

        // Enter function scope FIRST so we can register type parameters before resolving types
        _symbolTable.EnterScope($"function:{functionDef.Name}");

        // Enter an isolated narrowing scope for this function.
        // Type narrowings from the enclosing scope should NOT be visible inside this function,
        // because nested functions can be called later when the narrowing condition no longer holds.
        // This is control-flow narrowing isolation, not lexical scoping.
        using var _ = _narrowingContext.EnterIsolatedScope();

        ValidateTypeParameterDefaultOrdering(functionDef.TypeParameters);

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
                DeclarationColumn = typeParam.ColumnStart,
                NameDeclarationLine = typeParam.LineStart,
                NameDeclarationColumn = typeParam.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        // Detect bracket attributes that are source generators (module-level functions only)
        if (_currentClass == null && _currentFunctionReturnType == null)
            DetectGeneratorAttributes(functionDef);

        // Set static context for Self type validation before resolving types.
        // A method is static if it has @static decorator or doesn't have a self parameter.
        bool isStaticMethod = _currentClass != null &&
            (functionDef.Decorators.Any(d => d.Name == DecoratorNames.Static) ||
             functionDef.Parameters.Length == 0 ||
             functionDef.Parameters[0].Name != PythonNames.Self);
        _typeResolver.SetIsStaticContext(isStaticMethod);

        // Resolve return type AFTER type parameters are registered
        var returnType = ResolveReturnType(functionDef);

        // Save and set method context for super() validation and nested function restore
        var previousFunctionReturnType = _currentFunctionReturnType;
        _currentFunctionReturnType = returnType;

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

        // Validate override requirements and abstract method constraints
        ValidateOverrideRequirements(functionDef);

        // Validate self parameter for instance methods
        ValidateSelfParameter(functionDef);

        // Validate parameter ordering and register parameters in scope
        RegisterFunctionParameters(functionDef, functionSymbol);

        // Update function symbol return type and sync with owning TypeSymbol
        UpdateFunctionSymbol(functionDef, functionSymbol, returnType);

        // Save and set async/generator flags before body checking
        var previousIsAsync = _currentFunctionIsAsync;
        _currentFunctionIsAsync = functionDef.IsAsync;

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

        // Mark generator/async metadata and wrap return types
        ProcessGeneratorMetadata(functionDef, functionSymbol, isGenerator);
        ProcessAsyncMetadata(functionDef, functionSymbol, isGenerator);

        // Mark memoization decorator metadata (@lru_cache, @cache)
        ProcessCacheMetadata(functionDef, functionSymbol);

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
        _typeResolver.SetIsStaticContext(false);
    }

    /// <summary>
    /// Look up the function symbol for a FunctionDef — from the class's Methods/Constructors
    /// list for class methods, or from the symbol table for top-level functions.
    /// Nested functions inside class methods are looked up from the symbol table
    /// (they're registered there, not in the class methods list).
    /// </summary>
    private FunctionSymbol? LookupFunctionSymbol(FunctionDef functionDef)
    {
        if (_currentClass != null)
        {
            if (functionDef.Name == DunderNames.Init)
            {
                // Find the matching constructor by declaration line number
                // This uniquely identifies which overload we're checking
                return _currentClass.Constructors
                    .FirstOrDefault(c => c.DeclarationLine == functionDef.LineStart);
            }
            else
            {
                // Find the method in the class's Methods list by name and line number
                var classMethod = _currentClass.Methods
                    .FirstOrDefault(m => m.Name == functionDef.Name && m.DeclarationLine == functionDef.LineStart);
                if (classMethod != null)
                    return classMethod;

                // Not found in class methods — this is a nested function inside a class method.
                // Fall through to symbol table lookup below.
            }
        }

        // For top-level/nested functions, look up from symbol table.
        // When the function is overloaded, the symbol table only holds the first
        // overload, so resolve the specific symbol by declaration line.
        var overloads = _symbolTable.LookupFunctionOverloads(functionDef.Name);
        if (overloads is { Count: > 1 })
        {
            return overloads.FirstOrDefault(o => o.DeclarationLine == functionDef.LineStart);
        }
        return _symbolTable.LookupFunction(functionDef.Name);
    }

    /// <summary>
    /// Resolve the return type for a function definition from its type annotation.
    /// </summary>
    private SemanticType ResolveReturnType(FunctionDef functionDef)
    {
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

        return returnType;
    }

    /// <summary>
    /// Validate override decorator requirements: missing @override, invalid @override target,
    /// and abstract method constraints.
    /// </summary>
    private void ValidateOverrideRequirements(FunctionDef functionDef)
    {
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

                // Also check if the base class method implements an interface method,
                // which makes it implicitly virtual in the generated C#
                if (!isInterfaceMethod && baseOwner != null)
                {
                    foreach (var iface in GetInterfaces(baseOwner))
                    {
                        if (iface.Methods.Any(m => m.Name == functionDef.Name))
                        {
                            isInterfaceMethod = true;
                            break;
                        }
                    }
                }

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
    }

    /// <summary>
    /// Validate self parameter for instance methods.
    /// In Sharpy, methods without 'self' as the first parameter are treated as static methods.
    /// </summary>
    private void ValidateSelfParameter(FunctionDef functionDef)
    {
        if (_currentClass == null)
            return;

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

    /// <summary>
    /// Validate parameter ordering (non-default cannot follow default) and register
    /// all parameters in scope, updating the function symbol's parameter types.
    /// </summary>
    private void RegisterFunctionParameters(FunctionDef functionDef, FunctionSymbol? functionSymbol)
    {
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
                paramType = new UserDefinedType { Name = _currentClass.Name, Symbol = _currentClass };
            }
            else if (param.Type == null)
            {
                // Require type annotations on all parameters except 'self'
                AddError($"Parameter '{param.Name}' requires a type annotation",
                    param.LineStart, param.ColumnStart, code: DiagnosticCodes.Semantic.MissingTypeAnnotation,
                    span: param.Span);
            }

            if (param.Modifier != Parser.Ast.ParameterModifier.None)
            {
                if (param.DefaultValue != null)
                {
                    AddError($"Parameter '{param.Name}' with '{param.Modifier.ToString().ToLowerInvariant()}' modifier cannot have a default value",
                        param.LineStart, param.ColumnStart, code: DiagnosticCodes.Semantic.ModifierWithDefault,
                        span: param.Span);
                }
                if (param.IsVariadic)
                {
                    AddError($"Variadic parameter '{param.Name}' cannot have a '{param.Modifier.ToString().ToLowerInvariant()}' modifier",
                        param.LineStart, param.ColumnStart, code: DiagnosticCodes.Semantic.ModifierWithVariadic,
                        span: param.Span);
                }
            }

            var paramSymbol = new VariableSymbol
            {
                Name = param.Name,
                Kind = SymbolKind.Parameter,
                Type = paramType,
                IsParameter = true,
                ParameterModifier = param.Modifier,
                DeclarationLine = null,
                DeclarationColumn = null,
                NameDeclarationLine = null,
                NameDeclarationColumn = null
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
    }

    /// <summary>
    /// Update the function symbol's return type and sync with the owning TypeSymbol's
    /// Methods/Constructors/MethodOverloads/OperatorMethods/ProtocolMethods lists.
    /// </summary>
    private void UpdateFunctionSymbol(FunctionDef functionDef, FunctionSymbol? functionSymbol, SemanticType returnType)
    {
        if (functionSymbol == null)
            return;

        // Create a new FunctionSymbol with updated return type
        var updatedSymbol = functionSymbol with { ReturnType = returnType };
        // Update the symbol in the symbol table
        _symbolTable.UpdateSymbol(updatedSymbol);

        // Also update the reference in the owning TypeSymbol's lists or the
        // module-level overload list.  Without this sync, downstream consumers
        // (FindMethodInHierarchy, ResolveOverloadCore) read stale return types.
        if (_currentClass == null)
        {
            var overloads = _symbolTable.LookupFunctionOverloads(functionDef.Name);
            if (overloads != null)
            {
                var idx = overloads.IndexOf(functionSymbol);
                if (idx >= 0)
                    overloads[idx] = updatedSymbol;
            }
        }
        else
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

                // Also update MethodOverloads so ResolveUserMethodOverload
                // reads resolved return types instead of stale Unknown.
                if (_currentClass.MethodOverloads.TryGetValue(functionDef.Name, out var overloadList))
                {
                    var overloadIdx = overloadList.IndexOf(functionSymbol);
                    if (overloadIdx >= 0)
                        overloadList[overloadIdx] = updatedSymbol;
                }

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

    /// <summary>
    /// Mark generator metadata on the function and wrap the return type
    /// so callers see IEnumerable&lt;T&gt;/IAsyncEnumerable&lt;T&gt;.
    /// </summary>
    private void ProcessGeneratorMetadata(FunctionDef functionDef, FunctionSymbol? functionSymbol, bool isGenerator)
    {
        if (!isGenerator)
            return;

        _semanticInfo.MarkAsGenerator(functionDef);
        if (functionSymbol == null)
            return;

        // For class methods, look up the current symbol from the TypeSymbol's Methods list,
        // because the TypeChecker creates a new class scope (NameResolver's scope was popped),
        // so the symbol table doesn't contain the method. The Methods list has the
        // updatedSymbol (from the return type update), which is also the same
        // reference stored in ProtocolMethods/OperatorMethods.
        // For top-level functions, check the overload list first (disambiguate by line),
        // then fall back to the symbol table for non-overloaded functions.
        var currentSymbol = _currentClass?.Methods.FirstOrDefault(m => m.Name == functionDef.Name && m.DeclarationLine == functionDef.LineStart)
            ?? _symbolTable.LookupFunctionOverloads(functionDef.Name)?.FirstOrDefault(o => o.DeclarationLine == functionDef.LineStart)
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

    /// <summary>
    /// Mark async metadata on the function and wrap the return type in TaskType.
    /// Async generators skip Task wrapping since their type is already IAsyncEnumerable&lt;T&gt;.
    /// </summary>
    private void ProcessAsyncMetadata(FunctionDef functionDef, FunctionSymbol? functionSymbol, bool isGenerator)
    {
        // Skip if not async or if async was used in an invalid context (async __init__)
        // to avoid emitting broken C# (e.g., async constructor).
        var asyncHasError = functionDef.IsAsync && functionDef.Name == DunderNames.Init;
        if (!functionDef.IsAsync || asyncHasError)
            return;

        var asyncSymbol = _currentClass?.Methods.FirstOrDefault(m => m.Name == functionDef.Name && m.DeclarationLine == functionDef.LineStart)
            ?? _symbolTable.LookupFunctionOverloads(functionDef.Name)?.FirstOrDefault(o => o.DeclarationLine == functionDef.LineStart)
            ?? _symbolTable.Lookup(functionDef.Name) as FunctionSymbol;
        if (asyncSymbol == null)
            return;

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

    /// <summary>
    /// Marks a function symbol with memoization metadata when decorated with
    /// <c>@lru_cache</c> or <c>@cache</c>. The CodeGen layer reads
    /// <see cref="FunctionSymbol.IsCached"/> and <see cref="FunctionSymbol.CacheMaxSize"/>
    /// to emit a wrapper that delegates to <c>Sharpy.LruCache</c>.
    /// </summary>
    private void ProcessCacheMetadata(FunctionDef functionDef, FunctionSymbol? functionSymbol)
    {
        if (functionSymbol == null)
            return;

        var (isCached, maxSize) = ExtractCacheConfig(functionDef);
        if (!isCached)
            return;

        // Look up the current symbol the same way ProcessGeneratorMetadata/ProcessAsyncMetadata do,
        // because the TypeChecker class scope is already popped at this point.
        var currentSymbol = _currentClass?.Methods.FirstOrDefault(m =>
                m.Name == functionDef.Name && m.DeclarationLine == functionDef.LineStart)
            ?? _symbolTable.LookupFunctionOverloads(functionDef.Name)?.FirstOrDefault(o => o.DeclarationLine == functionDef.LineStart)
            ?? _symbolTable.Lookup(functionDef.Name) as FunctionSymbol;
        if (currentSymbol == null)
            return;

        currentSymbol.IsCached = true;
        currentSymbol.CacheMaxSize = maxSize;
    }

    /// <summary>
    /// Returns whether the function is decorated with <c>@lru_cache</c> or <c>@cache</c>
    /// and, when applicable, the integer maxsize. Validation of argument shape is performed
    /// by <c>DecoratorValidator</c>; this helper assumes the AST is well-formed when
    /// extracting the value.
    /// </summary>
    private static (bool IsCached, int? MaxSize) ExtractCacheConfig(FunctionDef functionDef)
    {
        // @cache → unbounded
        var cacheDecorator = functionDef.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Cache);
        if (cacheDecorator != null)
            return (true, null);

        var lruDecorator = functionDef.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.LruCache);
        if (lruDecorator == null)
            return (false, null);

        // No-argument @lru_cache → default to Python's documented maxsize=128.
        if (lruDecorator.Arguments.Length == 0 && lruDecorator.KeywordArguments.Length == 0)
            return (true, 128);

        // Single keyword: @lru_cache(maxsize=N) or @lru_cache(maxsize=None)
        if (lruDecorator.KeywordArguments.Length == 1
            && lruDecorator.KeywordArguments[0].Name == "maxsize")
        {
            var value = lruDecorator.KeywordArguments[0].Value;
            return value switch
            {
                NoneLiteral => (true, null),
                IntegerLiteral intLit when int.TryParse(intLit.Value.Replace("_", "", System.StringComparison.Ordinal), out var n) => (true, (int?)n),
                _ => (true, 128),
            };
        }

        // Single positional: @lru_cache(N) or @lru_cache(None)
        if (lruDecorator.Arguments.Length == 1)
        {
            var value = lruDecorator.Arguments[0];
            return value switch
            {
                NoneLiteral => (true, null),
                IntegerLiteral intLit when int.TryParse(intLit.Value.Replace("_", "", System.StringComparison.Ordinal), out var n) => (true, (int?)n),
                _ => (true, 128),
            };
        }

        // Malformed (DecoratorValidator already reported); fall back to default.
        return (true, 128);
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

        ValidateTypeParameterDefaultOrdering(classDef.TypeParameters);

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
                DeclarationColumn = typeParam.ColumnStart,
                NameDeclarationLine = typeParam.LineStart,
                NameDeclarationColumn = typeParam.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        // Re-register class-scoped type aliases in this scope
        // (aliases are first registered during NameResolver Pass 1, but that scope is destroyed;
        // we re-register here so they're visible during type checking)
        foreach (var statement in classDef.Body)
        {
            if (statement is TypeAlias typeAlias)
            {
                RegisterScopedTypeAlias(typeAlias);
            }
        }

        // Re-register nested type symbols in this scope
        foreach (var nestedType in classSymbol.NestedTypes)
        {
            _symbolTable.Define(nestedType);
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

        // Detect bracket attributes that are source generators
        DetectGeneratorAttributes(classDef);

        // Process @dataclass decorator (after field types are resolved)
        ProcessDataclassDecorator(classSymbol, classDef);

        // Resolve property types (before checking methods that might reference them)
        ResolvePropertyTypes(classSymbol, classDef.Body);

        // Resolve event types (before checking methods that might reference them)
        ResolveEventTypes(classSymbol, classDef.Body);

        // Set current class for method type checking (used for self parameter typing and Self type resolution)
        var previousClass = _currentClass;
        _currentClass = classSymbol;
        _typeResolver.SetCurrentTypeContext(classSymbol);

        // Pre-pass: resolve method signatures so forward references between methods
        // (e.g., __init__ calling a method declared later) see resolved return types.
        ResolveClassMethodSignatures(classDef.Body);

        // Check all members
        foreach (var statement in classDef.Body)
        {
            CheckStatement(statement);
        }

        // Restore previous class
        _currentClass = previousClass;
        _typeResolver.SetCurrentTypeContext(previousClass);
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

        ValidateTypeParameterDefaultOrdering(structDef.TypeParameters);

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
                DeclarationColumn = typeParam.ColumnStart,
                NameDeclarationLine = typeParam.LineStart,
                NameDeclarationColumn = typeParam.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        // Re-register struct-scoped type aliases in this scope
        foreach (var statement in structDef.Body)
        {
            if (statement is TypeAlias typeAlias)
            {
                RegisterScopedTypeAlias(typeAlias);
            }
        }

        // Re-register nested type symbols in this scope
        foreach (var nestedType in structSymbol.NestedTypes)
        {
            _symbolTable.Define(nestedType);
        }

        // Detect bracket attributes that are source generators
        DetectGeneratorAttributes(structDef);

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

        // Resolve event types (before checking methods that might reference them)
        ResolveEventTypes(structSymbol, structDef.Body);

        // Set current class for method type checking (structs behave like classes for self parameter)
        var previousClass = _currentClass;
        _currentClass = structSymbol;
        _typeResolver.SetCurrentTypeContext(structSymbol);

        // Pre-pass: resolve method signatures so forward references between methods
        // (e.g., a method calling another declared later) see resolved return types.
        ResolveClassMethodSignatures(structDef.Body);

        // Check all members
        foreach (var statement in structDef.Body)
        {
            CheckStatement(statement);
        }

        // Restore previous class
        _currentClass = previousClass;
        _typeResolver.SetCurrentTypeContext(previousClass);
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

        ValidateTypeParameterDefaultOrdering(interfaceDef.TypeParameters);

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
                DeclarationColumn = typeParam.ColumnStart,
                NameDeclarationLine = typeParam.LineStart,
                NameDeclarationColumn = typeParam.ColumnStart
            };
            _symbolTable.Define(typeParamSymbol);
        }

        // Re-register nested type symbols in this scope
        foreach (var nestedType in interfaceSymbol.NestedTypes)
        {
            _symbolTable.Define(nestedType);
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
                            IsKeywordOnly = param.Kind == Parser.Ast.ParameterKind.KeywordOnly,
                            Modifier = param.Modifier
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

        // Resolve event types
        ResolveEventTypes(interfaceSymbol, interfaceDef.Body);

        // Type-check default method bodies (methods with real implementations)
        // Set _currentClass to the interface symbol so CheckFunction resolves self correctly
        var previousClass = _currentClass;
        _currentClass = interfaceSymbol;
        _typeResolver.SetCurrentTypeContext(interfaceSymbol);

        foreach (var statement in interfaceDef.Body)
        {
            if (statement is FunctionDef method && !IsAbstractBody(method))
            {
                CheckFunction(method);
            }
            else if (statement is PropertyDef propDef)
            {
                // Default property implementations; abstract/body-less
                // declarations are skipped inside CheckClassProperty (#849)
                CheckClassProperty(propDef);
            }
        }

        _currentClass = previousClass;
        _typeResolver.SetCurrentTypeContext(previousClass);

        _symbolTable.ExitScope();
    }

    private void CheckEnum(EnumDef enumDef)
    {
        _logger.LogDebug($"Type checking enum: {enumDef.Name}");

        // Type-check enum member values so their types are stored in SemanticInfo
        // for downstream validation by EnumRulesValidator
        foreach (var member in enumDef.Members)
        {
            if (member.Value != null)
            {
                CheckExpression(member.Value);
            }
        }
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
                    DeclarationColumn = typeParam.ColumnStart,
                    NameDeclarationLine = typeParam.LineStart,
                    NameDeclarationColumn = typeParam.ColumnStart
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
                        DeclarationColumn = field.ColumnStart,
                        NameDeclarationLine = field.LineStart,
                        NameDeclarationColumn = field.ColumnStart
                    });
                }
            }

            // Type-check union body methods. Set the current type context so 'self'
            // and Self resolve to the union type (mirrors CheckClass).
            var previousClass = _currentClass;
            _currentClass = unionSymbol;
            _typeResolver.SetCurrentTypeContext(unionSymbol);

            foreach (var statement in unionDef.Body)
            {
                CheckStatement(statement);
            }

            _currentClass = previousClass;
            _typeResolver.SetCurrentTypeContext(previousClass);
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
                    DeclarationColumn = typeParam.ColumnStart,
                    NameDeclarationLine = typeParam.LineStart,
                    NameDeclarationColumn = typeParam.ColumnStart
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
                    IsKeywordOnly = param.Kind == Parser.Ast.ParameterKind.KeywordOnly,
                    Modifier = param.Modifier
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
    /// Resolves event types from their type annotations in the AST.
    /// For auto-events, resolves the delegate type annotation and verifies it's a delegate type.
    /// For function-style events, resolves the handler parameter type and validates add/remove consistency.
    /// Must be called after field type resolution and before member checking.
    /// </summary>
    private void ResolveEventTypes(TypeSymbol typeSymbol, System.Collections.Immutable.ImmutableArray<Statement> body)
    {
        for (int i = 0; i < typeSymbol.Events.Count; i++)
        {
            var eventSymbol = typeSymbol.Events[i];
            if (eventSymbol.Type is UnknownType)
            {
                // Find all EventDefs for this event (may have separate add/remove)
                var eventDefs = body
                    .OfType<EventDef>()
                    .Where(e => e.Name == eventSymbol.Name)
                    .ToList();

                var eventDef = eventDefs.FirstOrDefault();
                if (eventDef != null)
                {
                    SemanticType resolvedType;
                    if (eventDef.IsFunctionStyle)
                    {
                        // For function-style events, the handler type comes from the handler parameter
                        // (second parameter after self, or first parameter for static events)
                        var handlerParam = eventDef.Parameters
                            .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
                        if (handlerParam?.Type != null)
                        {
                            resolvedType = _typeResolver.ResolveTypeAnnotation(handlerParam.Type);
                        }
                        else
                        {
                            resolvedType = SemanticType.Unknown;
                        }

                        // Validate handler parameter type consistency between add/remove accessors (#262)
                        if (eventDefs.Count > 1)
                        {
                            var addDef = eventDefs.FirstOrDefault(e => e.Accessor == Parser.Ast.EventAccessor.Add);
                            var removeDef = eventDefs.FirstOrDefault(e => e.Accessor == Parser.Ast.EventAccessor.Remove);
                            if (addDef != null && removeDef != null)
                            {
                                var addHandlerParam = addDef.Parameters
                                    .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
                                var removeHandlerParam = removeDef.Parameters
                                    .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));

                                if (addHandlerParam?.Type != null && removeHandlerParam?.Type != null)
                                {
                                    var addType = _typeResolver.ResolveTypeAnnotation(addHandlerParam.Type);
                                    var removeType = _typeResolver.ResolveTypeAnnotation(removeHandlerParam.Type);

                                    if (!addType.Equals(removeType))
                                    {
                                        AddError(
                                            $"Event '{eventDef.Name}' add/remove accessors have mismatched handler types: add expects '{addType.GetDisplayName()}', remove expects '{removeType.GetDisplayName()}'",
                                            removeDef.LineStart, removeDef.ColumnStart,
                                            DiagnosticCodes.Semantic.EventAccessorParamMismatch,
                                            removeDef.Span);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Auto-event: type comes from type annotation
                        resolvedType = _typeResolver.ResolveTypeAnnotation(eventDef.Type);
                    }

                    if (resolvedType != SemanticType.Unknown)
                    {
                        // Verify the resolved type is a delegate type
                        if (resolvedType is UserDefinedType udt && udt.Symbol?.TypeKind == TypeKind.Delegate)
                        {
                            typeSymbol.Events[i] = eventSymbol with { Type = resolvedType };
                        }
                        else if (resolvedType is GenericType gt)
                        {
                            // Check if the generic type is a known delegate (e.g., EventHandler[T], Action[T])
                            var baseSymbol = _symbolTable.LookupType(gt.Name);
                            if (baseSymbol?.TypeKind == TypeKind.Delegate)
                            {
                                typeSymbol.Events[i] = eventSymbol with { Type = resolvedType };
                            }
                            else
                            {
                                AddError(
                                    $"Event '{eventDef.Name}' type '{resolvedType}' is not a delegate type",
                                    eventDef.LineStart, eventDef.ColumnStart,
                                    DiagnosticCodes.Semantic.EventTypeNotDelegate,
                                    eventDef.Span);
                            }
                        }
                        else
                        {
                            AddError(
                                $"Event '{eventDef.Name}' type '{resolvedType}' is not a delegate type",
                                eventDef.LineStart, eventDef.ColumnStart,
                                DiagnosticCodes.Semantic.EventTypeNotDelegate,
                                eventDef.Span);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Type-checks an event declaration.
    /// For auto-events, type resolution is handled by ResolveEventTypes().
    /// For function-style events, checks the accessor body.
    /// </summary>
    private void CheckEvent(EventDef eventDef)
    {
        if (!eventDef.IsFunctionStyle)
        {
            // Auto-events are fully handled by ResolveEventTypes()
            return;
        }

        // Function-style events: enter accessor scope and register parameters
        _symbolTable.EnterScope($"event:{eventDef.Name}:{eventDef.Accessor}");

        // Register parameters in scope (self, handler)
        for (int i = 0; i < eventDef.Parameters.Length; i++)
        {
            var param = eventDef.Parameters[i];
            SemanticType paramType;

            // Special handling for 'self' parameter
            if (i == 0 && param.Name == PythonNames.Self && _currentClass != null)
            {
                paramType = new UserDefinedType { Name = _currentClass.Name, Symbol = _currentClass };
            }
            else
            {
                paramType = _typeResolver.ResolveTypeAnnotation(param.Type);
            }

            var paramSymbol = new VariableSymbol
            {
                Name = param.Name,
                Kind = SymbolKind.Parameter,
                Type = paramType,
                IsParameter = true,
                DeclarationLine = param.LineStart,
                DeclarationColumn = param.ColumnStart,
                NameDeclarationLine = param.LineStart,
                NameDeclarationColumn = param.ColumnStart
            };
            _symbolTable.Define(paramSymbol);
            SemanticBinding.SetVariableType(paramSymbol, paramType);
        }

        // Type-check the accessor body
        foreach (var stmt in eventDef.Body)
        {
            CheckStatement(stmt);
        }

        _symbolTable.ExitScope();
    }

    /// <summary>
    /// Type-checks a module-level property declaration (#844).
    /// Resolves the property type onto the VariableSymbol registered by
    /// NameResolver.ResolveModulePropertyDeclaration, then checks the accessor
    /// body (function-style) or the default value (auto-property).
    /// Class/struct/interface properties are handled via ResolvePropertyTypes
    /// and CheckClassProperty instead.
    /// </summary>
    private void CheckModuleProperty(PropertyDef propDef)
    {
        _logger.LogDebug($"Type checking module-level property: {propDef.Name}");

        bool isSetter = propDef.Accessor == PropertyAccessor.Set;

        // Resolve the declared property type from the accessor signature,
        // mirroring class-level ResolvePropertyTypes.
        SemanticType propertyType;
        if (propDef.IsFunctionStyle)
        {
            if (propDef.ReturnType != null && !isSetter)
            {
                // Getter: type comes from the return annotation
                propertyType = _typeResolver.ResolveTypeAnnotation(propDef.ReturnType);
            }
            else if (isSetter && propDef.Parameters.Length > 0)
            {
                // Setter: type comes from the value parameter (last parameter)
                propertyType = _typeResolver.ResolveTypeAnnotation(propDef.Parameters[^1].Type);
            }
            else
            {
                propertyType = SemanticType.Unknown;
            }
        }
        else
        {
            // Auto-property: type comes from the type annotation
            propertyType = _typeResolver.ResolveTypeAnnotation(propDef.Type);
        }

        // Auto-property: check the default value against the declared type,
        // inferring the type from the initializer when no annotation is given
        // (mirrors CheckVariableDeclaration).
        if (!propDef.IsFunctionStyle && propDef.DefaultValue != null)
        {
            var previousExpectedType = _expectedType;
            _expectedType = propertyType is UnknownType ? null : propertyType;
            var defaultType = CheckExpression(propDef.DefaultValue);
            _expectedType = previousExpectedType;

            if (propertyType is UnknownType)
            {
                propertyType = defaultType;
                if (propDef.Type != null)
                {
                    _semanticInfo.SetTypeAnnotation(propDef.Type, defaultType);
                }
            }
            else if (!IsAssignable(defaultType, propertyType))
            {
                AddError(
                    $"Cannot assign type '{defaultType.GetDisplayName()}' to property of type '{propertyType.GetDisplayName()}'",
                    propDef.LineStart, propDef.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: propDef.DefaultValue.Span);
            }
        }

        // Update the symbol registered by NameResolver (pass 1). Getter and
        // setter declarations share one merged symbol; the first accessor to
        // resolve sets the type, subsequent accessors must agree with it.
        if (_symbolTable.Lookup(propDef.Name) is VariableSymbol { IsModuleProperty: true } propSymbol)
        {
            var currentType = GetVariableType(propSymbol);
            if (currentType is UnknownType)
            {
                propSymbol.Type = propertyType;
                SemanticBinding.SetVariableType(propSymbol, propertyType);
            }
            else if (propertyType is not UnknownType
                && (!IsAssignable(propertyType, currentType) || !IsAssignable(currentType, propertyType)))
            {
                AddError(
                    $"Property '{propDef.Name}' accessor type '{propertyType.GetDisplayName()}' does not match previously declared type '{currentType.GetDisplayName()}'",
                    propDef.LineStart, propDef.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: propDef.Span);
            }
        }

        if (!propDef.IsFunctionStyle)
            return;

        // Module-level properties are implicitly static; reject 'self'
        foreach (var param in propDef.Parameters)
        {
            if (param.Name == "self")
            {
                AddError(
                    "Module-level properties cannot have a 'self' parameter (they are implicitly static)",
                    param.LineStart, param.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSelfUsage,
                    span: param.Span);
                return;
            }
        }

        // Function-style: type-check the accessor body like a function body
        _symbolTable.EnterScope($"property:{propDef.Name}:{propDef.Accessor}");

        // Isolate control-flow narrowing from the enclosing scope (see CheckFunction)
        using var _ = _narrowingContext.EnterIsolatedScope();

        // Register accessor parameters in scope (the setter value parameter;
        // module-level properties have no self)
        foreach (var param in propDef.Parameters)
        {
            var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);
            var paramSymbol = new VariableSymbol
            {
                Name = param.Name,
                Kind = SymbolKind.Parameter,
                Type = paramType,
                IsParameter = true,
                DeclarationLine = param.LineStart,
                DeclarationColumn = param.ColumnStart,
                NameDeclarationLine = param.LineStart,
                NameDeclarationColumn = param.ColumnStart
            };
            _symbolTable.Define(paramSymbol);
            SemanticBinding.SetVariableType(paramSymbol, paramType);
        }

        // Getter bodies must return the property type; setter bodies return nothing
        var previousFunctionReturnType = _currentFunctionReturnType;
        _currentFunctionReturnType = isSetter ? SemanticType.Void : propertyType;

        var previousMethodName = _currentMethodName;
        var previousControlFlowDepth = _controlFlowDepth;
        var previousIsGenerator = _currentFunctionIsGenerator;
        var previousIsAsync = _currentFunctionIsAsync;
        _currentMethodName = propDef.Name;
        _controlFlowDepth = 0;
        _currentFunctionIsGenerator = false;
        _currentFunctionIsAsync = false;

        foreach (var stmt in propDef.Body)
        {
            CheckStatement(stmt);
        }

        _currentMethodName = previousMethodName;
        _controlFlowDepth = previousControlFlowDepth;
        _currentFunctionIsGenerator = previousIsGenerator;
        _currentFunctionIsAsync = previousIsAsync;
        _currentFunctionReturnType = previousFunctionReturnType;

        _symbolTable.ExitScope();
    }

    /// <summary>
    /// Type-checks a class/struct/interface property declaration (#849).
    /// The declared property type is resolved by ResolvePropertyTypes; this
    /// method checks the default value (auto-property) or the accessor body
    /// (function-style), registering 'self' and accessor parameters like a
    /// method body. Abstract/body-less declarations are skipped.
    /// </summary>
    private void CheckClassProperty(PropertyDef propDef)
    {
        if (_currentClass == null)
            return;

        _logger.LogDebug($"Type checking property: {_currentClass.Name}.{propDef.Name}");

        // Setter and init accessor bodies return nothing; getters return the property type
        bool isSetter = propDef.Accessor == PropertyAccessor.Set
            || propDef.Accessor == PropertyAccessor.Init;

        // The declared property type was resolved by ResolvePropertyTypes
        var propSymbol = _currentClass.Properties.FirstOrDefault(p => p.Name == propDef.Name);
        var propertyType = propSymbol?.Type ?? SemanticType.Unknown;

        // Fallback: re-resolve from this accessor's annotation if the merged
        // symbol type is still unknown (e.g. resolution from another accessor failed)
        if (propertyType is UnknownType)
        {
            if (propDef.IsFunctionStyle && propDef.ReturnType != null)
            {
                propertyType = _typeResolver.ResolveTypeAnnotation(propDef.ReturnType);
            }
            else if (!propDef.IsFunctionStyle && propDef.Type != null)
            {
                propertyType = _typeResolver.ResolveTypeAnnotation(propDef.Type);
            }
        }

        // Auto-property: check the default value against the declared type
        if (!propDef.IsFunctionStyle)
        {
            if (propDef.DefaultValue != null)
            {
                var previousExpectedType = _expectedType;
                _expectedType = propertyType is UnknownType ? null : propertyType;
                var defaultType = CheckExpression(propDef.DefaultValue);
                _expectedType = previousExpectedType;

                if (propertyType is not UnknownType && !IsAssignable(defaultType, propertyType))
                {
                    AddError(
                        $"Cannot assign type '{defaultType.GetDisplayName()}' to property of type '{propertyType.GetDisplayName()}'",
                        propDef.LineStart, propDef.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: propDef.DefaultValue.Span);
                }
            }
            return;
        }

        // Skip abstract properties and body-less declarations (single ellipsis
        // or pass) — there is no body to check
        bool isAbstractBody = propDef.Body.Length == 0 ||
            (propDef.Body.Length == 1 &&
                (propDef.Body[0] is PassStatement ||
                 (propDef.Body[0] is ExpressionStatement es && es.Expression is EllipsisLiteral)));
        if ((propSymbol?.IsAbstract ?? false) || isAbstractBody)
            return;

        // Function-style: type-check the accessor body like a method body
        _symbolTable.EnterScope($"property:{propDef.Name}:{propDef.Accessor}");

        // Isolate control-flow narrowing from the enclosing scope (see CheckFunction)
        using var _ = _narrowingContext.EnterIsolatedScope();

        // Register accessor parameters in scope. 'self' (first parameter on
        // instance properties) is typed as the current class; static properties
        // have no self parameter, so none is registered.
        for (int i = 0; i < propDef.Parameters.Length; i++)
        {
            var param = propDef.Parameters[i];
            SemanticType paramType;
            if (i == 0 && param.Name == PythonNames.Self)
            {
                paramType = new UserDefinedType { Name = _currentClass.Name, Symbol = _currentClass };
            }
            else
            {
                paramType = _typeResolver.ResolveTypeAnnotation(param.Type);
            }

            var paramSymbol = new VariableSymbol
            {
                Name = param.Name,
                Kind = SymbolKind.Parameter,
                Type = paramType,
                IsParameter = true,
                DeclarationLine = param.LineStart,
                DeclarationColumn = param.ColumnStart,
                NameDeclarationLine = param.LineStart,
                NameDeclarationColumn = param.ColumnStart
            };
            _symbolTable.Define(paramSymbol);
            SemanticBinding.SetVariableType(paramSymbol, paramType);
        }

        // Getter bodies must return the property type; setter bodies return nothing
        var previousFunctionReturnType = _currentFunctionReturnType;
        _currentFunctionReturnType = isSetter ? SemanticType.Void : propertyType;

        var previousMethodName = _currentMethodName;
        var previousControlFlowDepth = _controlFlowDepth;
        var previousIsGenerator = _currentFunctionIsGenerator;
        var previousIsAsync = _currentFunctionIsAsync;
        var previousMethodIsOverride = _currentMethodIsOverride;
        var previousMethodIsDunder = _currentMethodIsDunder;
        _currentMethodName = propDef.Name;
        _controlFlowDepth = 0;
        _currentFunctionIsGenerator = false;
        _currentFunctionIsAsync = false;
        _currentMethodIsOverride = propDef.Decorators.Any(d => d.Name == DecoratorNames.Override);
        _currentMethodIsDunder = false;

        foreach (var stmt in propDef.Body)
        {
            CheckStatement(stmt);
        }

        _currentMethodName = previousMethodName;
        _controlFlowDepth = previousControlFlowDepth;
        _currentFunctionIsGenerator = previousIsGenerator;
        _currentFunctionIsAsync = previousIsAsync;
        _currentMethodIsOverride = previousMethodIsOverride;
        _currentMethodIsDunder = previousMethodIsDunder;
        _currentFunctionReturnType = previousFunctionReturnType;

        _symbolTable.ExitScope();
    }

    /// <summary>
    /// Resolves property types from their type annotations in the AST.
    /// Must be called after field type resolution and before member checking.
    /// For mixed auto+custom properties, also synthesizes a backing field.
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

        // Synthesize backing fields for mixed auto+custom properties.
        // Group PropertyDefs by name. If a group has both auto and function-style defs,
        // the auto-property generates a private backing field (_name) so that custom
        // accessor bodies can reference self._name.
        var propGroups = body.OfType<PropertyDef>().GroupBy(p => p.Name);
        foreach (var group in propGroups)
        {
            var autoProp = group.FirstOrDefault(p => !p.IsFunctionStyle);
            var hasFunctionStyle = group.Any(p => p.IsFunctionStyle);

            if (autoProp != null && hasFunctionStyle)
            {
                var backingFieldName = "_" + autoProp.Name;
                // Only add if not already declared
                if (!typeSymbol.Fields.Any(f => f.Name == backingFieldName))
                {
                    var propType = autoProp.Type != null
                        ? _typeResolver.ResolveTypeAnnotation(autoProp.Type)
                        : SemanticType.Unknown;

                    var backingField = new VariableSymbol
                    {
                        Name = backingFieldName,
                        Kind = SymbolKind.Variable,
                        Type = propType,
                        AccessLevel = AccessLevel.Private,
                        DeclaringFilePath = _currentFilePath,
                        NameDeclarationLine = autoProp.NameLineStart,
                        NameDeclarationColumn = autoProp.NameColumnStart,
                    };
                    SemanticBinding.SetVariableType(backingField, propType);
                    typeSymbol.Fields.Add(backingField);
                    _symbolTable.Define(backingField);
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

    /// <summary>
    /// Processes @dataclass decorator on a class: extracts options, collects fields,
    /// validates field ordering, and sets IsDataclass/DataclassInfo/DataclassFields on the symbol.
    /// </summary>
    private void ProcessDataclassDecorator(TypeSymbol classSymbol, ClassDef classDef)
    {
        var dataclassDecorator = classDef.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Dataclass);
        if (dataclassDecorator == null)
            return;

        // Extract options from keyword arguments
        bool frozen = false;
        bool eq = true;
        bool repr = true;

        foreach (var kwArg in dataclassDecorator.KeywordArguments)
        {
            if (kwArg.Value is BooleanLiteral boolLit)
            {
                // Option names must match DataclassOptionNames.KnownOptions
                switch (kwArg.Name)
                {
                    case DataclassOptionNames.Frozen:
                        frozen = boolLit.Value;
                        break;
                    case DataclassOptionNames.Eq:
                        eq = boolLit.Value;
                        break;
                    case DataclassOptionNames.Repr:
                        repr = boolLit.Value;
                        break;
                }
            }
        }

        classSymbol.IsDataclass = true;
        classSymbol.DataclassInfo = new DataclassOptions(frozen, eq, repr);

        // Collect dataclass fields: typed field declarations (not properties, not methods)
        var fieldDecls = classDef.Body.OfType<VariableDeclaration>().ToList();
        var dataclassFields = new List<VariableSymbol>();

        // Check for Assignment nodes in class body — these are untyped field declarations
        // that need type annotations in a @dataclass context
        foreach (var assignment in classDef.Body.OfType<Assignment>())
        {
            if (assignment.Target is Identifier ident && assignment.Operator == AssignmentOperator.Assign)
            {
                AddError(
                    $"Dataclass field '{ident.Name}' in '{classDef.Name}' must have a type annotation " +
                    $"(use '{ident.Name}: type = ...' instead of '{ident.Name} = ...').",
                    assignment.LineStart,
                    assignment.ColumnStart,
                    code: DiagnosticCodes.Semantic.DataclassFieldNoType,
                    span: assignment.Span);
            }
        }

        // Collect inherited fields from parent @dataclass (parent fields first)
        if (classSymbol.BaseType is { IsDataclass: true, DataclassFields: { } parentFields })
        {
            dataclassFields.AddRange(parentFields);
        }

        bool seenDefault = dataclassFields.Any(f => f.HasDefaultValue);

        foreach (var fieldDecl in fieldDecls)
        {
            // Skip static fields — they're not instance fields for the dataclass
            if (fieldDecl.Decorators.Any(d => d.Name == DecoratorNames.Static))
                continue;

            // Dataclass fields must have type annotations
            if (fieldDecl.Type == null)
            {
                AddError(
                    $"Dataclass field '{fieldDecl.Name}' in '{classDef.Name}' must have a type annotation.",
                    fieldDecl.LineStart,
                    fieldDecl.ColumnStart,
                    code: DiagnosticCodes.Semantic.DataclassFieldNoType,
                    span: fieldDecl.Span);
                continue;
            }

            bool hasDefault = fieldDecl.InitialValue != null;

            // Enforce ordering: non-default fields before default fields
            if (!hasDefault && seenDefault)
            {
                AddError(
                    $"Non-default field '{fieldDecl.Name}' in dataclass '{classDef.Name}' " +
                    "cannot follow a field with a default value.",
                    fieldDecl.LineStart,
                    fieldDecl.ColumnStart,
                    code: DiagnosticCodes.Semantic.DataclassFieldOrdering,
                    span: fieldDecl.Span);
            }

            if (hasDefault)
                seenDefault = true;

            // Find the corresponding field symbol
            var fieldSymbol = classSymbol.Fields.FirstOrDefault(f => f.Name == fieldDecl.Name);
            if (fieldSymbol != null)
            {
                dataclassFields.Add(fieldSymbol);
            }
        }

        classSymbol.DataclassFields = dataclassFields;

        // Synthesize methods that don't have explicit definitions
        SynthesizeDataclassMethods(classSymbol, classDef, dataclassFields);
    }

    private void DetectGeneratorAttributes(Statement declaration)
    {
        var decorators = declaration switch
        {
            ClassDef cd => cd.Decorators,
            FunctionDef fd => fd.Decorators,
            StructDef sd => sd.Decorators,
            _ => System.Collections.Immutable.ImmutableArray<Decorator>.Empty
        };

        foreach (var decorator in decorators)
        {
            if (!decorator.IsBracketAttribute)
                continue;

            var symbol = _symbolTable.Lookup(decorator.Name) as TypeSymbol;
            if (symbol is { IsSourceGenerator: true })
            {
                _semanticInfo.AddGeneratorBinding(declaration, symbol, decorator);
            }
        }
    }

    /// <summary>
    /// Synthesizes __init__, __eq__, __repr__, and __hash__ FunctionSymbols for a @dataclass
    /// if not explicitly defined by the user.
    /// </summary>
    private void SynthesizeDataclassMethods(
        TypeSymbol classSymbol,
        ClassDef classDef,
        List<VariableSymbol> dataclassFields)
    {
        var options = classSymbol.DataclassInfo!;
        var explicitMethods = classDef.Body.OfType<FunctionDef>().Select(f => f.Name).ToHashSet();

        // Synthesize __init__ if not explicitly defined
        if (!explicitMethods.Contains(DunderNames.Init))
        {
            var initParams = new List<ParameterSymbol>
            {
                new() { Name = PythonNames.Self, Type = new UserDefinedType { Name = classSymbol.Name, Symbol = classSymbol } }
            };

            foreach (var field in dataclassFields)
            {
                initParams.Add(new ParameterSymbol
                {
                    Name = field.Name,
                    Type = GetVariableType(field),
                    HasDefault = field.HasDefaultValue,
                });
            }

            var initSymbol = new FunctionSymbol
            {
                Name = DunderNames.Init,
                Kind = SymbolKind.Function,
                ReturnType = SemanticType.Void,
                Parameters = initParams,
                DeclarationLine = classDef.LineStart,
                DeclarationColumn = classDef.ColumnStart,
                NameDeclarationLine = classDef.LineStart,
                NameDeclarationColumn = classDef.ColumnStart,
            };

            classSymbol.Constructors.Add(initSymbol);
            classSymbol.ProtocolMethods[DunderNames.Init] = new List<FunctionSymbol> { initSymbol };
        }

        // Synthesize __eq__ if eq=True and not explicitly defined
        if (options.Eq && !explicitMethods.Contains(DunderNames.Eq))
        {
            var eqSymbol = new FunctionSymbol
            {
                Name = DunderNames.Eq,
                Kind = SymbolKind.Function,
                ReturnType = SemanticType.Bool,
                Parameters = new List<ParameterSymbol>
                {
                    new() { Name = PythonNames.Self, Type = new UserDefinedType { Name = classSymbol.Name, Symbol = classSymbol } },
                    new() { Name = "other", Type = SemanticType.Object },
                },
                IsOverride = true,
                DeclarationLine = classDef.LineStart,
                DeclarationColumn = classDef.ColumnStart,
                NameDeclarationLine = classDef.LineStart,
                NameDeclarationColumn = classDef.ColumnStart,
            };

            classSymbol.OperatorMethods[DunderNames.Eq] = new List<FunctionSymbol> { eqSymbol };
            classSymbol.Methods.Add(eqSymbol);
        }

        // Synthesize __hash__ if eq=True and no explicit __hash__
        // .NET requires GetHashCode whenever Equals is overridden, regardless of frozen
        if (options.Eq && !explicitMethods.Contains(DunderNames.Hash))
        {
            var hashSymbol = new FunctionSymbol
            {
                Name = DunderNames.Hash,
                Kind = SymbolKind.Function,
                ReturnType = SemanticType.Int,
                Parameters = new List<ParameterSymbol>
                {
                    new() { Name = PythonNames.Self, Type = new UserDefinedType { Name = classSymbol.Name, Symbol = classSymbol } },
                },
                IsOverride = true,
                DeclarationLine = classDef.LineStart,
                DeclarationColumn = classDef.ColumnStart,
                NameDeclarationLine = classDef.LineStart,
                NameDeclarationColumn = classDef.ColumnStart,
            };

            classSymbol.ProtocolMethods[DunderNames.Hash] = new List<FunctionSymbol> { hashSymbol };
            classSymbol.Methods.Add(hashSymbol);
        }

        // Synthesize __repr__ if repr=True and not explicitly defined
        if (options.Repr && !explicitMethods.Contains(DunderNames.Repr))
        {
            var reprSymbol = new FunctionSymbol
            {
                Name = DunderNames.Repr,
                Kind = SymbolKind.Function,
                ReturnType = SemanticType.Str,
                Parameters = new List<ParameterSymbol>
                {
                    new() { Name = PythonNames.Self, Type = new UserDefinedType { Name = classSymbol.Name, Symbol = classSymbol } },
                },
                IsOverride = true,
                DeclarationLine = classDef.LineStart,
                DeclarationColumn = classDef.ColumnStart,
                NameDeclarationLine = classDef.LineStart,
                NameDeclarationColumn = classDef.ColumnStart,
            };

            classSymbol.ProtocolMethods[DunderNames.Repr] = new List<FunctionSymbol> { reprSymbol };
            classSymbol.Methods.Add(reprSymbol);
        }
    }

    private void ValidateTypeParameterDefaultOrdering(System.Collections.Immutable.ImmutableArray<TypeParameterDef> typeParams)
    {
        bool seenDefault = false;
        string? firstDefaultName = null;

        foreach (var tp in typeParams)
        {
            if (tp.DefaultType != null)
            {
                seenDefault = true;
                firstDefaultName ??= tp.Name;
                ValidateTypeParameterDefaultConstraints(tp);
            }
            else if (seenDefault)
            {
                AddError(
                    $"Type parameter '{tp.Name}' without a default follows type parameter '{firstDefaultName}' which has a default",
                    tp.LineStart, tp.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypeParameterDefaultOrdering,
                    span: tp.Span);
            }
        }
    }

    private void ValidateTypeParameterDefaultConstraints(TypeParameterDef typeParam)
    {
        if (typeParam.DefaultType == null || typeParam.Constraints.IsEmpty)
            return;

        var defaultType = _typeResolver.ResolveTypeAnnotation(typeParam.DefaultType);
        if (defaultType is UnknownType)
            return;

        foreach (var constraint in typeParam.Constraints)
        {
            switch (constraint)
            {
                case Parser.Ast.ClassConstraint when defaultType.IsValueType:
                    AddError(
                        $"Default type '{defaultType.GetDisplayName()}' for type parameter '{typeParam.Name}' is a value type, but constraint requires a reference type (class)",
                        typeParam.DefaultType.LineStart, typeParam.DefaultType.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeParameterDefaultViolatesConstraint,
                        span: typeParam.DefaultType.Span);
                    break;

                case Parser.Ast.StructConstraint when !defaultType.IsValueType:
                    AddError(
                        $"Default type '{defaultType.GetDisplayName()}' for type parameter '{typeParam.Name}' is a reference type, but constraint requires a value type (struct)",
                        typeParam.DefaultType.LineStart, typeParam.DefaultType.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeParameterDefaultViolatesConstraint,
                        span: typeParam.DefaultType.Span);
                    break;

                case Parser.Ast.TypeConstraint tc:
                    var constraintType = _typeResolver.ResolveTypeAnnotation(tc.Type);
                    if (constraintType is UnknownType)
                        break;
                    if (!defaultType.IsAssignableTo(constraintType))
                    {
                        AddError(
                            $"Default type '{defaultType.GetDisplayName()}' for type parameter '{typeParam.Name}' does not satisfy constraint '{constraintType.GetDisplayName()}'",
                            typeParam.DefaultType.LineStart, typeParam.DefaultType.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeParameterDefaultViolatesConstraint,
                            span: typeParam.DefaultType.Span);
                    }
                    break;
            }
        }
    }

}
