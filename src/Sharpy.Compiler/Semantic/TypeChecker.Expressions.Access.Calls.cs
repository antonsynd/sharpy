using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Function calls, overload resolution, argument validation
/// </summary>
internal partial class TypeChecker
{
    private SemanticType CheckFunctionCall(FunctionCall call)
    {
        // Handle None() — empty Optional constructor
        if (call.Function is NoneLiteral && call.Arguments.Length == 0 && call.KeywordArguments.Length == 0)
        {
            if (_expectedType is OptionalType)
            {
                return _expectedType;
            }
            else if (_expectedType != null)
            {
                AddError($"'None()' can only construct Optional types, not '{_expectedType.GetDisplayName()}'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.InvalidNoneConstructor,
                    span: call.Span);
                return SemanticType.Unknown;
            }
            else
            {
                AddError("Cannot infer type for 'None()' without a type annotation. Add a type annotation like 'x: int? = None()'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                    span: call.Span);
                return SemanticType.Unknown;
            }
        }

        // Check if this is a tagged union constructor shorthand (Some/Ok/Err)
        if (call.Function is Identifier constructorId && call.Arguments.Length == 1 && call.KeywordArguments.Length == 0)
        {
            var constructorResult = TryCheckTaggedUnionConstructor(constructorId, call);
            if (constructorResult != null)
                return constructorResult;
        }

        // Detect IIFE: (lambda x: ...)(args) — check arguments first to infer lambda param types
        var iifeLambda = call.Function is LambdaExpression directLambda ? directLambda
            : call.Function is Parenthesized paren && paren.Expression is LambdaExpression innerLambda ? innerLambda
            : null;
        if (iifeLambda != null && call.KeywordArguments.Length == 0)
        {
            return CheckIifeLambdaCall(call, iifeLambda);
        }

        // Check if this is a null conditional method call (obj?.method())
        bool isNullConditionalCall = call.Function is MemberAccess { IsNullConditional: true };
        bool isOptionalNullConditional = false;

        // Check the called expression type first
        var calleeType = CheckExpression(call.Function);

        // After checking the callee, determine if this is ?. on an Optional object
        if (isNullConditionalCall && call.Function is MemberAccess nullCondMa)
        {
            var objType = _semanticInfo.GetExpressionType(nullCondMa.Object);
            isOptionalNullConditional = objType is OptionalType;
        }

        // Check event raise restriction: events can only be invoked from within the declaring class
        if (call.Function is MemberAccess invokeMA && invokeMA.Member == "invoke")
        {
            // The object of .invoke() might be an event access (e.g., self.on_click?.invoke(...))
            if (_semanticInfo.IsEventAccess(invokeMA.Object))
            {
                // Determine the event's declaring type
                if (invokeMA.Object is MemberAccess eventMA)
                {
                    var eventOwner = ResolveEventOwner(eventMA);
                    if (eventOwner != null && (_currentClass == null || !ReferenceEquals(_currentClass, eventOwner)))
                    {
                        AddError(
                            $"Cannot raise event '{eventMA.Member}' from outside the declaring class",
                            call.LineStart, call.ColumnStart,
                            DiagnosticCodes.Semantic.RaiseEventOutsideClass,
                            call.Span);
                        return SemanticType.Void;
                    }
                }
            }
        }

        // Track super().__init__() calls AFTER validation completes
        // (do this after CheckExpression so the validation doesn't see it as already called)
        if (call.Function is MemberAccess ma && ma.Object is SuperExpression && ma.Member == DunderNames.Init)
        {
            _superInitCalled = true;
        }

        // Validate self.__init__() is only called inside a constructor
        if (call.Function is MemberAccess selfInitMa &&
            selfInitMa.Object is Identifier { Name: "self" } &&
            selfInitMa.Member == DunderNames.Init)
        {
            if (_currentMethodName != DunderNames.Init)
            {
                AddError("self.__init__() can only be called inside a constructor (__init__)",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.SelfInitOutsideConstructor,
                    span: call.Span);
            }
            else if (_superInitCalled)
            {
                AddError("Cannot use both super().__init__() and self.__init__() in the same constructor",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.ConflictingConstructorInitializers,
                    span: call.Span);
            }
        }

        // Try to resolve the function symbol early for constructor inference on arguments.
        // For simple identifier calls (foo(Some(42))), we can look up the function before
        // checking arguments, allowing _expectedType to be set per-parameter.
        FunctionSymbol? earlyFuncSymbol = null;
        int earlyParamOffset = 0; // offset to skip 'self' parameter for __init__ methods
        if (call.Function is Identifier earlyId)
        {
            var earlySymbol = _symbolTable.Lookup(earlyId.Name);
            if (earlySymbol is FunctionSymbol fs && !fs.IsGeneric)
            {
                // Only use early resolution for non-generic, non-overloaded functions.
                // Generic functions need argument types first for inference.
                // Overloaded builtins need argument types for resolution.
                var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(earlyId.Name);
                if (overloads == null || overloads.Count <= 1 || !overloads.Contains(fs))
                {
                    earlyFuncSymbol = fs;
                }
            }
            else if (earlySymbol is TypeSymbol ts && !ts.IsGeneric)
            {
                // Constructor call: Person(Some(42)) — look up __init__ for parameter types.
                // __init__ includes 'self' at index 0, but call arguments don't, so offset by 1.
                var initMethod = ts.Methods.FirstOrDefault(m => m.Name == DunderNames.Init);
                if (initMethod != null && !initMethod.IsGeneric)
                {
                    earlyFuncSymbol = initMethod;
                    earlyParamOffset = 1; // skip 'self' parameter
                }
            }
        }

        // Check arguments and collect their types
        // When we have an early function symbol or callee FunctionType, set _expectedType per-parameter
        // to enable constructor inference (Some/None()/Ok/Err) in function arguments.
        var calleeFunctionType = calleeType as FunctionType;
        var argTypes = new List<SemanticType>();
        for (int argIdx = 0; argIdx < call.Arguments.Length; argIdx++)
        {
            var previousExpectedType = _expectedType;

            // Handle spread arguments: *expr
            if (call.Arguments[argIdx] is SpreadElement spreadArg)
            {
                var spreadValueType = CheckExpression(spreadArg.Value);

                if (spreadValueType is TupleType tupleSpread)
                {
                    // Tuple spread: expand element types as individual arguments
                    argTypes.AddRange(tupleSpread.ElementTypes);
                }
                else
                {
                    // Iterable spread: extract element type for variadic param matching
                    var elemType = _typeInference.InferIterableElementType(spreadValueType);
                    if (elemType != null)
                        argTypes.Add(elemType);
                    else
                        argTypes.Add(SemanticType.Unknown);
                }
                _expectedType = previousExpectedType;
                continue;
            }

            if (earlyFuncSymbol != null && argIdx + earlyParamOffset < earlyFuncSymbol.Parameters.Count)
            {
                var paramType = earlyFuncSymbol.Parameters[argIdx + earlyParamOffset].Type;
                _expectedType = paramType is UnknownType ? null : paramType;
            }
            else if (calleeFunctionType != null && argIdx < calleeFunctionType.ParameterTypes.Count)
            {
                var paramType = calleeFunctionType.ParameterTypes[argIdx];
                _expectedType = paramType is UnknownType ? null : paramType;
            }
            argTypes.Add(CheckExpression(call.Arguments[argIdx]));
            _expectedType = previousExpectedType;
        }

        // Check keyword arguments and collect their types
        var kwargTypes = new Dictionary<string, SemanticType>();
        foreach (var kwarg in call.KeywordArguments)
        {
            var previousExpectedType = _expectedType;
            if (earlyFuncSymbol != null)
            {
                var param = earlyFuncSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                if (param != null)
                {
                    _expectedType = param.Type is UnknownType ? null : param.Type;
                }
            }
            kwargTypes[kwarg.Name] = CheckExpression(kwarg.Value);
            _expectedType = previousExpectedType;
        }

        // Total argument count includes both positional and keyword arguments
        var totalArgCount = argTypes.Count + kwargTypes.Count;

        // Try to get the function symbol directly for better validation
        FunctionSymbol? funcSymbol = null;

        // Handle generic type/function instantiation: Box[int](42) or identity[int](42)
        var genericResult = CheckGenericInstantiation(call, calleeType, argTypes, kwargTypes, totalArgCount);
        if (genericResult != null)
            return genericResult;

        if (call.Function is Identifier id)
        {
            // Data-driven builtin function return type inference (len, hash, reversed, sorted, min, max)
            var builtinReturn = BuiltinReturnTypeInference.InferReturnType(
                id.Name, argTypes, _typeInference);
            if (builtinReturn != null)
                return builtinReturn;

            var symbol = _symbolTable.Lookup(id.Name);

            // Special handling for constructor calls (calling a type)
            if (symbol is TypeSymbol typeSymbol)
            {
                // For primitive types (int, float, str, bool, long, etc.), route to builtin function overloads
                // instead of treating as constructor. This matches Python semantics where int(x) calls
                // the int conversion function, not constructs a new int object.
                var primitiveOverloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
                if (primitiveOverloads != null && primitiveOverloads.Count > 0 && PrimitiveCatalog.IsPrimitive(id.Name))
                {
                    // Route to builtin function overload resolution below
                    // (fall through to overload handling)
                }
                else
                {
                    // Validate constructor arguments against __init__ parameters (skip 'self').
                    // Only validate when there's a single __init__ (no overloads) — overloaded
                    // constructors have complex resolution that the C# compiler handles.
                    var initMethods = typeSymbol.Methods.Where(m => m.Name == DunderNames.Init).ToList();
                    if (initMethods.Count == 1)
                    {
                        var initParams = initMethods[0].Parameters.Skip(1).ToList(); // skip 'self'

                        // SPY0357: Check for iterable spread into non-variadic constructor
                        if (CheckSpreadIntoNonVariadic(call, typeSymbol.Name, initParams))
                            return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };

                        // Validate argument count and positional-only/keyword-only constraints.
                        // Skip type checking — the C# compiler handles type validation, and there
                        // are edge cases (None to nullable, enum conversions) it handles correctly.
                        ValidateCallArgumentsCountAndKinds(call, initParams, argTypes, kwargTypes, totalArgCount);
                    }
                    else if (initMethods.Count > 1)
                    {
                        // Multiple __init__ overloads — only check spread into non-variadic
                        var firstInit = initMethods[0];
                        var initParams = firstInit.Parameters.Skip(1).ToList();
                        if (CheckSpreadIntoNonVariadic(call, typeSymbol.Name, initParams))
                            return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
                    }

                    // Cannot instantiate abstract classes
                    if (typeSymbol.IsAbstract)
                    {
                        AddError($"Cannot instantiate abstract class '{typeSymbol.Name}'",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AbstractInstantiation,
                            span: call.Span);
                        return SemanticType.Unknown;
                    }

                    // For generic types called without type arguments (e.g., set()),
                    // infer type arguments from the expected type annotation if available,
                    // otherwise emit a diagnostic for empty constructors or fall back to
                    // UnknownType args for wildcard matching.
                    if (typeSymbol.IsGeneric)
                    {
                        List<SemanticType>? typeArgs = null;
                        if (_expectedType is GenericType expectedGeneric
                            && expectedGeneric.Name == typeSymbol.Name
                            && expectedGeneric.TypeArguments.Count == typeSymbol.TypeParameters.Count)
                        {
                            typeArgs = expectedGeneric.TypeArguments;
                        }
                        else if (call.Arguments.Length == 0 && call.KeywordArguments.Length == 0)
                        {
                            // Empty generic constructor with no type annotation — cannot infer type args
                            AddError($"Cannot infer type of empty {typeSymbol.Name} constructor; add a type annotation (e.g., x: {typeSymbol.Name}[...] = {typeSymbol.Name}())",
                                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                                span: call.Span);
                            return SemanticType.Unknown;
                        }
                        else if (call.Arguments.Length == 1 && call.KeywordArguments.Length == 0)
                        {
                            // Single-argument constructor: try to infer type args from iterable argument type
                            var argType = argTypes.Count > 0 ? argTypes[0] : null;
                            if (argType != null && argType != SemanticType.Unknown)
                            {
                                var elementType = _typeInference.InferIterableElementType(argType);
                                if (elementType != null && elementType != SemanticType.Unknown)
                                {
                                    if (typeSymbol.Name is BuiltinNames.List or BuiltinNames.Set
                                        && typeSymbol.TypeParameters.Count == 1)
                                    {
                                        typeArgs = new List<SemanticType> { elementType };
                                    }
                                    else if (typeSymbol.Name == BuiltinNames.Dict
                                             && typeSymbol.TypeParameters.Count == 2
                                             && elementType is TupleType tt && tt.ElementTypes.Count == 2)
                                    {
                                        typeArgs = new List<SemanticType> { tt.ElementTypes[0], tt.ElementTypes[1] };
                                    }
                                }
                            }

                            // Fall through to Unknown if inference failed
                            typeArgs ??= Enumerable.Range(0, typeSymbol.TypeParameters.Count)
                                .Select(_ => (SemanticType)SemanticType.Unknown)
                                .ToList();
                        }
                        else
                        {
                            // Multiple arguments or keyword arguments: cannot infer type args
                            typeArgs = Enumerable.Range(0, typeSymbol.TypeParameters.Count)
                                .Select(_ => (SemanticType)SemanticType.Unknown)
                                .ToList();
                        }
                        return new GenericType
                        {
                            Name = typeSymbol.Name,
                            TypeArguments = typeArgs,
                            GenericDefinition = typeSymbol
                        };
                    }

                    // Constructor call returns an instance of the type
                    return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
                }
            }

            funcSymbol = symbol as FunctionSymbol;

            // If we found a symbol but it's not a function or type, it's not callable
            // UNLESS it's a variable with a FunctionType (e.g., a parameter with type (T) -> U)
            if (symbol != null && funcSymbol == null && symbol is not TypeSymbol)
            {
                // Check if it's an error recovery symbol - suppress cascading errors
                if (symbol.IsErrorRecovery)
                {
                    return SemanticType.Unknown;
                }

                // Check if it's a variable with a FunctionType or delegate type - those are callable
                if (symbol is VariableSymbol varSym &&
                    (GetVariableType(varSym) is FunctionType
                     || TryGetDelegateInvokeMethod(GetVariableType(varSym)) != null))
                {
                    // Let the FunctionType / delegate handling below deal with this
                }
                else
                {
                    AddError($"'{id.Name}' is not callable (type: {calleeType.GetDisplayName()})",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                        span: call.Function.Span);
                    return SemanticType.Unknown;
                }
            }

            // Special handling for builtin functions with overloads
            var overloadResult = ResolveBuiltinOverload(id, argTypes, totalArgCount, call);
            if (overloadResult != null)
                return overloadResult;
        }
        // Handle union case construction: Shape.Circle(5.0) → new Shape.Circle(5.0)
        // The calleeType is a UserDefinedType for the case class whose BaseType is the union.
        else if (call.Function is MemberAccess unionCaseAccess
            && calleeType is UserDefinedType caseUdt
            && caseUdt.Symbol?.BaseType is { TypeKind: TypeKind.Union } unionBaseSymbol)
        {
            var caseFields = caseUdt.Symbol.Fields;

            // For generic unions, substitute type parameters using the expected type
            var typeParams = unionBaseSymbol.TypeParameters;
            List<SemanticType>? typeArgs = null;
            if (typeParams.Count > 0 && _expectedType is GenericType expectedGenericType
                && expectedGenericType.Name == unionBaseSymbol.Name
                && expectedGenericType.TypeArguments.Count == typeParams.Count)
            {
                typeArgs = expectedGenericType.TypeArguments;
            }

            // Validate argument count
            if (argTypes.Count != caseFields.Count)
            {
                AddError($"Union case '{unionBaseSymbol.Name}.{caseUdt.Name}' expects {caseFields.Count} argument(s) but got {argTypes.Count}",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
            }
            else
            {
                // Validate argument types (with type parameter substitution for generics)
                for (int i = 0; i < caseFields.Count; i++)
                {
                    var expectedFieldType = caseFields[i].Type;
                    if (typeArgs != null)
                    {
                        expectedFieldType = SubstituteTypeParameters(expectedFieldType, typeParams, typeArgs);
                    }

                    if (!IsAssignable(argTypes[i], expectedFieldType))
                    {
                        AddError($"Argument {i + 1} has type '{argTypes[i].GetDisplayName()}' but field '{caseFields[i].Name}' expects '{expectedFieldType.GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: call.Arguments[i].Span);
                    }
                }
            }

            // For generic unions, return a GenericType matching the expected type
            if (typeArgs != null)
            {
                return new GenericType
                {
                    Name = unionBaseSymbol.Name,
                    TypeArguments = typeArgs,
                    GenericDefinition = unionBaseSymbol
                };
            }

            // For non-generic unions, return the union base type
            return new UserDefinedType { Name = unionBaseSymbol.Name, Symbol = unionBaseSymbol };
        }
        // Handle member access function calls (e.g., module.function() or obj.method())
        // Skip super() calls - they're already validated by ValidateSuperMemberAccess
        else if (call.Function is MemberAccess memberAccessCall && memberAccessCall.Object is not SuperExpression)
        {
            funcSymbol = ResolveFunctionSymbolFromMemberAccess(memberAccessCall);

            // Try module function overloads (e.g., os.path.join with different arities)
            {
                var moduleOverloadResult = ResolveModuleFunctionOverload(
                    memberAccessCall, argTypes, totalArgCount, call,
                    isNullConditionalCall, isOptionalNullConditional);
                if (moduleOverloadResult != null)
                    return moduleOverloadResult;
            }

            // Try user-defined method overloads: either when no symbol was found,
            // or when the found symbol's method has multiple overloads on the owning type
            {
                var overloadResult = ResolveUserMethodOverload(
                    memberAccessCall, argTypes, totalArgCount, call,
                    isNullConditionalCall, isOptionalNullConditional);
                if (overloadResult != null)
                    return overloadResult;
            }

            // Builtin method overloads (dict.get, list.pop) are now handled by
            // ResolveUserMethodOverload above via discovery-populated metadata.
        }

        // If we have a FunctionSymbol, use it for validation (supports default parameters)
        if (funcSymbol != null)
        {
            return ValidateFunctionSymbolCall(call, funcSymbol, argTypes, kwargTypes, totalArgCount,
                isNullConditionalCall, isOptionalNullConditional);
        }

        // Fallback to FunctionType validation (no default parameter support)
        // Use the already-computed calleeType to avoid re-evaluating call.Function
        // (which causes double validation, e.g., super().__init__() being flagged as duplicate)
        if (calleeType is FunctionType ft)
        {
            return CheckLambdaCall(call, ft, argTypes, totalArgCount,
                isNullConditionalCall, isOptionalNullConditional);
        }

        // Handle delegate-typed variable invocation: extract the Invoke method and validate
        {
            var delegateInvoke = TryGetDelegateInvokeMethod(calleeType);
            if (delegateInvoke != null)
            {
                return ValidateFunctionSymbolCall(call, delegateInvoke, argTypes, kwargTypes, totalArgCount,
                    isNullConditionalCall, isOptionalNullConditional);
            }
        }

        // If callee type is Unknown, this is error recovery from a sub-expression
        // (covered by transitive error recovery tracking in CheckExpression).
        // Otherwise, the callee evaluated to a non-callable type — emit an error.
        if (calleeType is not UnknownType)
        {
            AddError($"Expression of type '{calleeType.GetDisplayName()}' is not callable",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                span: call.Function.Span);
        }
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Handles generic type instantiation (Box[int](42)) and generic function calls (identity[int](42)).
    /// Returns null if the call is not a generic instantiation.
    /// </summary>
    private SemanticType? CheckGenericInstantiation(FunctionCall call, SemanticType calleeType,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes, int totalArgCount)
    {
        // Special handling for generic type instantiation: Box[int](42) or Pair[int, str](1, "a")
        // This is parsed as FunctionCall(Function: IndexAccess(Object: Box, Index: int or TupleLiteral), Arguments: [...])
        if (call.Function is IndexAccess indexAccess &&
            indexAccess.Object is Identifier genericTypeId &&
            _symbolTable.Lookup(genericTypeId.Name) is TypeSymbol genericTypeSymbol &&
            genericTypeSymbol.IsGeneric)
        {
            // The "index" is actually type argument(s) - try to resolve them as types
            var typeArgs = TryResolveTypeArguments(indexAccess.Index);
            if (typeArgs != null)
            {
                // Validate constructor arguments against __init__ parameters (skip 'self').
                // Only validate when there's a single __init__ (no overloads).
                var initMethods = genericTypeSymbol.Methods.Where(m => m.Name == DunderNames.Init).ToList();
                if (initMethods.Count == 1)
                {
                    var initParams = initMethods[0].Parameters.Skip(1).ToList();

                    // SPY0357: Check for iterable spread into non-variadic generic constructor
                    if (CheckSpreadIntoNonVariadic(call, genericTypeSymbol.Name, initParams))
                        return new GenericType
                        {
                            Name = genericTypeSymbol.Name,
                            TypeArguments = typeArgs,
                            GenericDefinition = genericTypeSymbol
                        };

                    ValidateCallArgumentsCountAndKinds(call, initParams, argTypes, kwargTypes, totalArgCount);
                }
                else if (initMethods.Count > 1)
                {
                    var initParams = initMethods[0].Parameters.Skip(1).ToList();
                    if (CheckSpreadIntoNonVariadic(call, genericTypeSymbol.Name, initParams))
                        return new GenericType
                        {
                            Name = genericTypeSymbol.Name,
                            TypeArguments = typeArgs,
                            GenericDefinition = genericTypeSymbol
                        };
                }

                // Cannot instantiate abstract classes
                if (genericTypeSymbol.IsAbstract)
                {
                    AddError($"Cannot instantiate abstract class '{genericTypeSymbol.Name}'",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AbstractInstantiation,
                        span: call.Span);
                    return SemanticType.Unknown;
                }

                // Return a GenericType with the type arguments
                return new GenericType
                {
                    Name = genericTypeSymbol.Name,
                    TypeArguments = typeArgs,
                    GenericDefinition = genericTypeSymbol
                };
            }
        }

        // Handle generic function call: identity[int](42)
        // The calleeType will be GenericFunctionType from CheckIndexAccess
        if (calleeType is GenericFunctionType genericFuncType)
        {
            // Record the resolved call target for codegen
            _semanticInfo.SetCallTarget(call, genericFuncType.FunctionSymbol);

            // Substitute type parameters with type arguments in the return type
            var substitutedReturnType = SubstituteTypeParameters(
                genericFuncType.FunctionSymbol.ReturnType,
                genericFuncType.FunctionSymbol.TypeParameters,
                genericFuncType.TypeArguments);
            return substitutedReturnType;
        }

        return null;
    }

    /// <summary>
    /// Resolves builtin function overloads for a call. Returns the resolved return type,
    /// or null if no overload resolution is needed.
    /// </summary>
    private SemanticType? ResolveBuiltinOverload(
        Identifier id, List<SemanticType> argTypes, int totalArgCount, FunctionCall call)
    {
        // When there are multiple overloads, we need to perform overload resolution to find the right one.
        // The funcSymbol from Lookup is just the first overload, which may not match the call.
        // Only use builtin overloads if there's no user-defined function shadowing the builtin.
        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
        var isBuiltinWithOverloads = overloads != null && overloads.Count > 1;
        var funcSymbol = _symbolTable.Lookup(id.Name) as FunctionSymbol;
        // If funcSymbol was found in symbol table AND it's one of the builtin overloads, use overload resolution
        var needsOverloadResolution = isBuiltinWithOverloads &&
            (funcSymbol == null || (funcSymbol != null && overloads!.Contains(funcSymbol)));
        if (!needsOverloadResolution)
            return null;

        // First pass: filter by argument count (considering default parameters and variadic parameters)
        var candidateOverloads = overloads!.Where(o =>
        {
            var requiredParams = o.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
            var hasVariadic = o.Parameters.Any(p => p.IsVariadic);
            var totalParams = o.Parameters.Count;
            // Variadic functions can accept any number of arguments >= required
            if (hasVariadic)
                return totalArgCount >= requiredParams;
            return totalArgCount >= requiredParams && totalArgCount <= totalParams;
        }).ToList();

        // Second pass: check type compatibility
        FunctionSymbol? matchingOverload = null;
        foreach (var overload in candidateOverloads)
        {
            bool typesMatch = true;
            var variadicParam = overload.Parameters.FirstOrDefault(p => p.IsVariadic);

            for (int i = 0; i < argTypes.Count; i++)
            {
                SemanticType expectedType;
                if (i < overload.Parameters.Count && !overload.Parameters[i].IsVariadic)
                {
                    // Regular parameter
                    expectedType = overload.Parameters[i].Type;
                }
                else if (variadicParam != null)
                {
                    // Variadic parameter - all remaining args must match the element type
                    expectedType = variadicParam.Type;
                }
                else
                {
                    // Index out of bounds - shouldn't happen with valid candidates
                    typesMatch = false;
                    break;
                }

                if (!IsAssignable(argTypes[i], expectedType))
                {
                    typesMatch = false;
                    break;
                }
            }
            if (typesMatch)
            {
                matchingOverload = overload;
                break;
            }
        }

        if (matchingOverload != null)
        {
            // Update the identifier symbol to point to the matching overload
            _semanticInfo.SetIdentifierSymbol(id, matchingOverload);
            // Record the resolved call target for codegen
            _semanticInfo.SetCallTarget(call, matchingOverload);
            return matchingOverload.ReturnType;
        }

        // No matching overload found
        var expectedCounts = string.Join(" or ", overloads!.Select(o =>
        {
            var required = o.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
            var total = o.Parameters.Count;
            var hasVariadic = o.Parameters.Any(p => p.IsVariadic);
            if (hasVariadic)
                return $"{required}+";
            return required == total ? total.ToString() : $"{required}-{total}";
        }).Distinct());
        AddError($"Function '{id.Name}' expects {expectedCounts} arguments but got {totalArgCount}",
            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
            span: call.Span);
        return SemanticType.Unknown;
    }

    // TODO(#205): Add language spec for method overloading (docs/language_specification/method_overloading.md)
    // TODO(#207): Add test fixtures for ambiguous overloads and overloads with default parameters
    /// <summary>
    /// Resolves a method overload from a member access call (e.g., obj.method(args)).
    /// Handles both user-defined types and built-in generic types (dict, list, set).
    /// Returns the resolved return type when the method has multiple overloads, null if not applicable.
    /// </summary>
    private SemanticType? ResolveUserMethodOverload(
        MemberAccess memberAccess, List<SemanticType> argTypes, int totalArgCount, FunctionCall call,
        bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        var objectType = _semanticInfo.GetExpressionType(memberAccess.Object);

        // Unwrap nullable/optional types for null-conditional method calls
        if (objectType is NullableType nt)
            objectType = nt.UnderlyingType;
        else if (objectType is OptionalType ot)
            objectType = ot.UnderlyingType;

        TypeSymbol? typeSymbol = null;
        List<SemanticType>? typeArgs = null;

        if (objectType is UserDefinedType { Symbol: { } udt })
        {
            typeSymbol = udt;
        }
        else if (objectType is GenericType gt)
        {
            typeSymbol = _symbolTable.BuiltinRegistry.GetType(gt.Name);
            typeArgs = gt.TypeArguments;
        }

        if (typeSymbol == null)
            return null;

        // Walk the hierarchy looking for overloads
        var overloads = FindMethodOverloadsInHierarchy(typeSymbol, memberAccess.Member);
        if (overloads == null || overloads.Count <= 1)
            return null;

        // SPY0357: Check for iterable spread into non-variadic overloaded method.
        // Must run before argument count filtering, since spread collapses N args into 1.
        var anyOverloadVariadic = overloads.Any(o => o.Parameters.Any(p => p.IsVariadic));
        if (!anyOverloadVariadic)
        {
            for (int i = 0; i < call.Arguments.Length; i++)
            {
                if (call.Arguments[i] is SpreadElement spreadElem)
                {
                    var spreadType = _semanticInfo.GetExpressionType(spreadElem.Value);
                    if (spreadType is not null and not UnknownType and not TupleType)
                    {
                        AddError(
                            $"Cannot spread '{spreadType.GetDisplayName()}' into non-variadic function '{memberAccess.Member}'; " +
                            "use a function with *args parameter or pass arguments individually",
                            spreadElem.LineStart, spreadElem.ColumnStart,
                            code: DiagnosticCodes.Semantic.SpreadIntoNonVariadic,
                            span: spreadElem.Span);
                        return SemanticType.Unknown;
                    }
                }
            }
        }

        // First pass: filter by argument count (skip 'self' parameter)
        var candidates = overloads.Where(o =>
        {
            var selfOffset = o.Parameters.Count > 0 && o.Parameters[0].Name == PythonNames.Self ? 1 : 0;
            var requiredParams = o.Parameters.Skip(selfOffset).Count(p => !p.HasDefault && !p.IsVariadic);
            var hasVariadic = o.Parameters.Skip(selfOffset).Any(p => p.IsVariadic);
            var totalParams = o.Parameters.Count - selfOffset;
            if (hasVariadic)
                return totalArgCount >= requiredParams;
            return totalArgCount >= requiredParams && totalArgCount <= totalParams;
        }).ToList();

        // Second pass: check type compatibility
        var matchingOverloads = new List<FunctionSymbol>();
        foreach (var overload in candidates)
        {
            var selfOffset = overload.Parameters.Count > 0 && overload.Parameters[0].Name == PythonNames.Self ? 1 : 0;
            bool typesMatch = true;
            var variadicParam = overload.Parameters.Skip(selfOffset).FirstOrDefault(p => p.IsVariadic);

            for (int i = 0; i < argTypes.Count; i++)
            {
                SemanticType expectedType;
                var paramIdx = i + selfOffset;
                if (paramIdx < overload.Parameters.Count && !overload.Parameters[paramIdx].IsVariadic)
                {
                    expectedType = overload.Parameters[paramIdx].Type;
                }
                else if (variadicParam != null)
                {
                    expectedType = variadicParam.Type;
                }
                else
                {
                    typesMatch = false;
                    break;
                }

                // Substitute type parameters for builtin generic types before comparison
                var resolvedExpected = typeArgs != null && typeSymbol.TypeParameters.Count > 0
                    ? SubstituteTypeParameters(expectedType, typeSymbol.TypeParameters, typeArgs)
                    : expectedType;

                if (resolvedExpected is not UnknownType && argTypes[i] is not UnknownType
                    && !IsAssignable(argTypes[i], resolvedExpected))
                {
                    typesMatch = false;
                    break;
                }
            }
            if (typesMatch)
            {
                matchingOverloads.Add(overload);
            }
        }

        // When multiple overloads match, prefer the one with exact arity (no defaults/variadic used)
        FunctionSymbol? matchingOverload;
        if (matchingOverloads.Count > 1)
        {
            var exactArityMatches = matchingOverloads.Where(o =>
            {
                var selfOffset = o.Parameters.Count > 0 && o.Parameters[0].Name == PythonNames.Self ? 1 : 0;
                return o.Parameters.Count - selfOffset == totalArgCount;
            }).ToList();

            if (exactArityMatches.Count == 1)
            {
                matchingOverload = exactArityMatches[0];
            }
            else
            {
                AddError($"Ambiguous call to overloaded method '{memberAccess.Member}' — multiple overloads match the argument types",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AmbiguousOverload,
                    span: call.Span);
                return SemanticType.Unknown;
            }
        }
        else
        {
            matchingOverload = matchingOverloads.Count == 1 ? matchingOverloads[0] : null;
        }

        if (matchingOverload == null)
        {
            if (candidates.Count == 0)
            {
                AddError($"No matching overload for '{memberAccess.Member}' with {totalArgCount} argument(s)",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NoMatchingOverload,
                    span: call.Span);
            }
            else
            {
                // Candidates matched by arity but not by type
                AddError($"No matching overload for '{memberAccess.Member}' with the given argument types",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NoMatchingOverload,
                    span: call.Span);
            }
            return SemanticType.Unknown;
        }

        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, matchingOverload);

        var returnType = matchingOverload.ReturnType;

        // Substitute type parameters for builtin generic types (e.g., T0 -> int for dict[str, int])
        if (typeArgs != null && typeSymbol.TypeParameters.Count > 0)
        {
            returnType = SubstituteTypeParameters(returnType, typeSymbol.TypeParameters, typeArgs);
        }

        if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
        {
            if (isOptionalNullConditional)
                return new OptionalType { UnderlyingType = returnType };
            return new NullableType { UnderlyingType = returnType };
        }
        return returnType;
    }

    /// <summary>
    /// Finds all overloads for a method name walking the type hierarchy.
    /// Returns null if no overloads are found.
    /// </summary>
    private List<FunctionSymbol>? FindMethodOverloadsInHierarchy(TypeSymbol type, string methodName)
    {
        // Check the type itself
        if (type.MethodOverloads.TryGetValue(methodName, out var overloads) && overloads.Count > 0)
            return overloads;

        // Check base class chain
        var current = GetBaseType(type);
        while (current != null)
        {
            if (current.MethodOverloads.TryGetValue(methodName, out overloads) && overloads.Count > 0)
                return overloads;
            current = GetBaseType(current);
        }

        return null;
    }

    /// <summary>
    /// Resolves a FunctionSymbol from a member access expression (e.g., module.function()).
    /// Returns null if the member does not resolve to a FunctionSymbol.
    /// </summary>
    private FunctionSymbol? ResolveFunctionSymbolFromMemberAccess(MemberAccess memberAccess)
    {
        // Re-evaluate the object to get the module, then lookup the member.
        // This is duplicate work but necessary until we refactor to store symbols in SemanticInfo.
        var objectType = CheckExpression(memberAccess.Object);
        if (objectType is ModuleType moduleType)
        {
            var moduleSymbol = moduleType.Symbol;
            if (moduleSymbol.Exports.TryGetValue(memberAccess.Member, out var exportedSymbol))
            {
                return exportedSymbol as FunctionSymbol;
            }
        }
        return null;
    }

    /// <summary>
    /// Resolves overloaded module-level functions (e.g., os.path.join with different arities).
    /// Returns null if the object is not a module or has no overloads for the member.
    /// </summary>
    private SemanticType? ResolveModuleFunctionOverload(
        MemberAccess memberAccess, List<SemanticType> argTypes, int totalArgCount, FunctionCall call,
        bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        var objectType = _semanticInfo.GetExpressionType(memberAccess.Object);
        if (objectType is not ModuleType moduleType)
            return null;

        var moduleSymbol = moduleType.Symbol;
        if (!moduleSymbol.FunctionOverloads.TryGetValue(memberAccess.Member, out var overloads) || overloads.Count <= 1)
            return null;

        // Filter by argument count (module functions have no 'self' parameter)
        var candidates = overloads.Where(o =>
        {
            var requiredParams = o.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
            var hasVariadic = o.Parameters.Any(p => p.IsVariadic);
            var totalParams = o.Parameters.Count;
            if (hasVariadic)
                return totalArgCount >= requiredParams;
            return totalArgCount >= requiredParams && totalArgCount <= totalParams;
        }).ToList();

        // Check type compatibility
        var matchingOverloads = new List<FunctionSymbol>();
        foreach (var overload in candidates)
        {
            bool typesMatch = true;
            var variadicParam = overload.Parameters.FirstOrDefault(p => p.IsVariadic);

            for (int i = 0; i < argTypes.Count; i++)
            {
                SemanticType expectedType;
                if (i < overload.Parameters.Count && !overload.Parameters[i].IsVariadic)
                {
                    expectedType = overload.Parameters[i].Type;
                }
                else if (variadicParam != null)
                {
                    expectedType = variadicParam.Type;
                }
                else
                {
                    typesMatch = false;
                    break;
                }

                if (expectedType is not UnknownType && argTypes[i] is not UnknownType
                    && !IsAssignable(argTypes[i], expectedType))
                {
                    typesMatch = false;
                    break;
                }
            }
            if (typesMatch)
            {
                matchingOverloads.Add(overload);
            }
        }

        // Prefer exact arity match
        FunctionSymbol? matchingOverload;
        if (matchingOverloads.Count > 1)
        {
            var exactArityMatches = matchingOverloads.Where(o =>
                o.Parameters.Count == totalArgCount).ToList();

            if (exactArityMatches.Count == 1)
            {
                matchingOverload = exactArityMatches[0];
            }
            else
            {
                AddError($"Ambiguous call to overloaded function '{memberAccess.Member}' — multiple overloads match the argument types",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AmbiguousOverload,
                    span: call.Span);
                return SemanticType.Unknown;
            }
        }
        else
        {
            matchingOverload = matchingOverloads.Count == 1 ? matchingOverloads[0] : null;
        }

        if (matchingOverload == null)
        {
            if (candidates.Count == 0)
            {
                AddError($"No matching overload for '{memberAccess.Member}' with {totalArgCount} argument(s)",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NoMatchingOverload,
                    span: call.Span);
            }
            else
            {
                AddError($"No matching overload for '{memberAccess.Member}' with the given argument types",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NoMatchingOverload,
                    span: call.Span);
            }
            return SemanticType.Unknown;
        }

        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, matchingOverload);

        var returnType = matchingOverload.ReturnType;

        if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
        {
            if (isOptionalNullConditional)
                return new OptionalType { UnderlyingType = returnType };
            return new NullableType { UnderlyingType = returnType };
        }
        return returnType;
    }

    /// <summary>
    /// Checks for iterable spread arguments in a call to a non-variadic target.
    /// Returns true if a violation was found and a diagnostic was emitted.
    /// TupleType spreads are excluded because their size is statically known.
    /// </summary>
    private bool CheckSpreadIntoNonVariadic(
        FunctionCall call, string targetName, IReadOnlyList<ParameterSymbol>? parameters)
    {
        if (parameters == null)
            return false;

        var hasVariadicParam = parameters.Any(p => p.IsVariadic);
        if (hasVariadicParam)
            return false;

        for (int i = 0; i < call.Arguments.Length; i++)
        {
            if (call.Arguments[i] is SpreadElement spreadElem)
            {
                var spreadType = _semanticInfo.GetExpressionType(spreadElem.Value);
                if (spreadType is not null and not UnknownType and not TupleType)
                {
                    AddError(
                        $"Cannot spread '{spreadType.GetDisplayName()}' into non-variadic function '{targetName}'; " +
                        "use a function with *args parameter or pass arguments individually",
                        spreadElem.LineStart, spreadElem.ColumnStart,
                        code: DiagnosticCodes.Semantic.SpreadIntoNonVariadic,
                        span: spreadElem.Span);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Validates a function call against a resolved FunctionSymbol, including generic inference,
    /// argument count, positional/keyword argument type checking.
    /// </summary>
    private SemanticType ValidateFunctionSymbolCall(
        FunctionCall call, FunctionSymbol funcSymbol,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes,
        int totalArgCount, bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, funcSymbol);

        // Check for iterable spread into non-variadic function (SPY0357)
        // Must run before generic inference — generic functions without *args must also reject
        // iterable spread. Tuple spread is excluded because tuple size is statically known.
        if (CheckSpreadIntoNonVariadic(call, funcSymbol.Name, funcSymbol.Parameters))
        {
            var earlyReturn = funcSymbol.ReturnType;
            if (isNullConditionalCall && earlyReturn is not NullableType and not OptionalType)
            {
                if (isOptionalNullConditional)
                    return new OptionalType { UnderlyingType = earlyReturn };
                return new NullableType { UnderlyingType = earlyReturn };
            }
            return earlyReturn;
        }

        // Handle generic function inference: identity(42) -> infer T=int
        // This is triggered when calling a generic function without explicit type arguments
        if (funcSymbol.IsGeneric)
        {
            var inferenceResult = _genericInference.InferTypeArguments(funcSymbol, argTypes);
            if (inferenceResult.Success && inferenceResult.InferredTypes != null)
            {
                // Inference succeeded - substitute type parameters and return the result
                var substitutedReturnType = SubstituteTypeParameters(
                    funcSymbol.ReturnType,
                    funcSymbol.TypeParameters,
                    inferenceResult.InferredTypes);

                // Store the inferred type arguments for codegen
                _semanticInfo.SetInferredTypeArguments(call, inferenceResult.InferredTypes);

                // Wrap result in optional/nullable for null conditional calls
                if (isNullConditionalCall && substitutedReturnType is not NullableType and not OptionalType)
                {
                    if (isOptionalNullConditional)
                        return new OptionalType { UnderlyingType = substitutedReturnType };
                    return new NullableType { UnderlyingType = substitutedReturnType };
                }
                return substitutedReturnType;
            }
            else
            {
                // Inference failed - report error
                AddError(inferenceResult.ErrorMessage ?? "Type arguments cannot be inferred",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferGenericType,
                    span: call.Span);
                return SemanticType.Unknown;
            }
        }

        ValidateCallArguments(call, funcSymbol.Parameters, argTypes, kwargTypes, totalArgCount);

        var returnType = funcSymbol.ReturnType;

        // Wrap result in optional/nullable for null conditional calls
        if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
        {
            if (isOptionalNullConditional)
                return new OptionalType { UnderlyingType = returnType };
            return new NullableType { UnderlyingType = returnType };
        }
        return returnType;
    }

    /// <summary>
    /// Validates a function call against a FunctionType (lambda/delegate calls without a FunctionSymbol).
    /// </summary>
    private SemanticType CheckLambdaCall(
        FunctionCall call, FunctionType ft, List<SemanticType> argTypes,
        int totalArgCount, bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        // Skip validation for .NET types with multiple constructor overloads
        // (C# compiler will handle overload resolution)
        if (!ft.SkipArgumentValidation)
        {
            // Validate argument count (accounting for optional parameters with defaults)
            var requiredCount = ft.ParameterTypes.Count - ft.OptionalParameterCount;
            var tooFew = totalArgCount < requiredCount;
            var tooMany = totalArgCount > ft.ParameterTypes.Count;

            if (tooFew || tooMany)
            {
                if (ft.OptionalParameterCount > 0 && requiredCount != ft.ParameterTypes.Count)
                {
                    AddError($"Function expects {requiredCount} to {ft.ParameterTypes.Count} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                }
                else
                {
                    AddError($"Function expects {ft.ParameterTypes.Count} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                }
            }
            else
            {
                // Validate positional argument types
                for (int i = 0; i < argTypes.Count; i++)
                {
                    if (!IsAssignable(argTypes[i], ft.ParameterTypes[i]))
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{ft.ParameterTypes[i].GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: call.Arguments[i].Span);
                    }
                }
            }
        }

        var returnType = ft.ReturnType;

        // Wrap result in optional/nullable for null conditional calls
        if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
        {
            if (isOptionalNullConditional)
                return new OptionalType { UnderlyingType = returnType };
            return new NullableType { UnderlyingType = returnType };
        }
        return returnType;
    }

    /// <summary>
    /// Validates call arguments against a parameter list: argument count, types,
    /// and positional-only/keyword-only constraints. Used by both regular function
    /// calls and constructor calls (with __init__ params minus self).
    /// </summary>
    private void ValidateCallArguments(
        FunctionCall call, IReadOnlyList<ParameterSymbol> parameters,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes,
        int totalArgCount)
    {
        var hasVariadicParam = parameters.Any(p => p.IsVariadic);
        var requiredParamCount = parameters.Count(p => !p.HasDefault && !p.IsVariadic);
        var totalParamCount = parameters.Count;

        // Count parameters eligible for positional arguments (not keyword-only)
        var positionalParamCount = parameters.Count(p => !p.IsKeywordOnly);

        // Validate argument count considering defaults and variadic params
        var tooFew = totalArgCount < requiredParamCount;
        var tooManyPositional = !hasVariadicParam && argTypes.Count > positionalParamCount;
        var tooMany = !hasVariadicParam && totalArgCount > totalParamCount;
        if (tooFew || tooMany || tooManyPositional)
        {
            if (hasVariadicParam)
            {
                AddError($"Function expects at least {requiredParamCount} arguments but got {totalArgCount}",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
            }
            else if (requiredParamCount == totalParamCount)
            {
                AddError($"Function expects {totalParamCount} arguments but got {totalArgCount}",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
            }
            else
            {
                AddError($"Function expects {requiredParamCount} to {totalParamCount} arguments but got {totalArgCount}",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
            }
        }
        else
        {
            // Validate positional argument types
            var variadicParamIndex = parameters.ToList().FindIndex(p => p.IsVariadic);
            for (int i = 0; i < argTypes.Count; i++)
            {
                ParameterSymbol param;
                if (variadicParamIndex >= 0 && i >= variadicParamIndex)
                {
                    param = parameters[variadicParamIndex];
                }
                else if (i < parameters.Count)
                {
                    param = parameters[i];
                }
                else
                {
                    break;
                }

                if (param.IsKeywordOnly)
                {
                    AddError($"'{param.Name}' is keyword-only and must be passed as a keyword argument",
                        call.Arguments[i].LineStart, call.Arguments[i].ColumnStart,
                        code: DiagnosticCodes.Semantic.KeywordOnlyPassedPositionally,
                        span: call.Arguments[i].Span);
                    continue;
                }

                if (!IsAssignable(argTypes[i], param.Type))
                {
                    AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{param.Type.GetDisplayName()}'",
                        call.Arguments[i].LineStart, call.Arguments[i].ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: call.Arguments[i].Span);
                }
            }

            // Validate keyword arguments
            foreach (var kwarg in call.KeywordArguments)
            {
                var param = parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                if (param == null)
                {
                    AddError($"Unknown keyword argument '{kwarg.Name}'",
                        kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                        span: kwarg.Value.Span);
                }
                else if (param.IsPositionalOnly)
                {
                    AddError($"'{kwarg.Name}' is positional-only and cannot be passed as a keyword argument",
                        kwarg.LineStart, kwarg.ColumnStart,
                        code: DiagnosticCodes.Semantic.PositionalOnlyPassedByKeyword,
                        span: kwarg.Value.Span);
                }
                else
                {
                    var paramIndex = parameters.ToList().IndexOf(param);
                    if (!param.IsKeywordOnly && paramIndex < argTypes.Count)
                    {
                        AddError($"Argument '{kwarg.Name}' was already provided positionally",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                            span: kwarg.Value.Span);
                    }
                    else if (!IsAssignable(kwargTypes[kwarg.Name], param.Type))
                    {
                        AddError($"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{param.Type.GetDisplayName()}'",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: kwarg.Value.Span);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates only argument count and positional-only/keyword-only constraints,
    /// without type checking. Used for generic constructors without explicit type args
    /// where __init__ parameters contain unsubstituted type parameters.
    /// </summary>
    private void ValidateCallArgumentsCountAndKinds(
        FunctionCall call, IReadOnlyList<ParameterSymbol> parameters,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes,
        int totalArgCount)
    {
        var hasVariadicParam = parameters.Any(p => p.IsVariadic);
        var requiredParamCount = parameters.Count(p => !p.HasDefault && !p.IsVariadic);
        var totalParamCount = parameters.Count;
        var positionalParamCount = parameters.Count(p => !p.IsKeywordOnly);

        var tooFew = totalArgCount < requiredParamCount;
        var tooManyPositional = !hasVariadicParam && argTypes.Count > positionalParamCount;
        var tooMany = !hasVariadicParam && totalArgCount > totalParamCount;
        if (tooFew || tooMany || tooManyPositional)
        {
            if (hasVariadicParam)
            {
                AddError($"Function expects at least {requiredParamCount} arguments but got {totalArgCount}",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
            }
            else if (requiredParamCount == totalParamCount)
            {
                AddError($"Function expects {totalParamCount} arguments but got {totalArgCount}",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
            }
            else
            {
                AddError($"Function expects {requiredParamCount} to {totalParamCount} arguments but got {totalArgCount}",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
            }
        }
        else
        {
            // Check positional-only/keyword-only constraints (skip type checks)
            var variadicParamIndex = parameters.ToList().FindIndex(p => p.IsVariadic);
            for (int i = 0; i < argTypes.Count; i++)
            {
                ParameterSymbol param;
                if (variadicParamIndex >= 0 && i >= variadicParamIndex)
                    param = parameters[variadicParamIndex];
                else if (i < parameters.Count)
                    param = parameters[i];
                else
                    break;

                if (param.IsKeywordOnly)
                {
                    AddError($"'{param.Name}' is keyword-only and must be passed as a keyword argument",
                        call.Arguments[i].LineStart, call.Arguments[i].ColumnStart,
                        code: DiagnosticCodes.Semantic.KeywordOnlyPassedPositionally,
                        span: call.Arguments[i].Span);
                }
            }

            foreach (var kwarg in call.KeywordArguments)
            {
                var param = parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                if (param == null)
                {
                    AddError($"Unknown keyword argument '{kwarg.Name}'",
                        kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                        span: kwarg.Value.Span);
                }
                else if (param.IsPositionalOnly)
                {
                    AddError($"'{kwarg.Name}' is positional-only and cannot be passed as a keyword argument",
                        kwarg.LineStart, kwarg.ColumnStart,
                        code: DiagnosticCodes.Semantic.PositionalOnlyPassedByKeyword,
                        span: kwarg.Value.Span);
                }
                else
                {
                    var paramIndex = parameters.ToList().IndexOf(param);
                    if (!param.IsKeywordOnly && paramIndex < argTypes.Count)
                    {
                        AddError($"Argument '{kwarg.Name}' was already provided positionally",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                            span: kwarg.Value.Span);
                    }
                }
            }
        }
    }

    private SemanticType CheckIifeLambdaCall(FunctionCall call, LambdaExpression lambda)
    {
        // 1. Check arguments first to get concrete types
        var argTypes = new List<SemanticType>();
        foreach (var arg in call.Arguments)
            argTypes.Add(CheckExpression(arg));

        // 2. Validate argument count (accounting for default parameters)
        var totalParamCount = lambda.Parameters.Length;
        var optionalCount = lambda.Parameters.Count(p => p.DefaultValue != null);
        var requiredParamCount = totalParamCount - optionalCount;

        if (argTypes.Count < requiredParamCount || argTypes.Count > totalParamCount)
        {
            var countDesc = requiredParamCount == totalParamCount
                ? $"{totalParamCount}"
                : $"{requiredParamCount} to {totalParamCount}";
            AddError($"Lambda expects {countDesc} argument(s) but {argTypes.Count} were given",
                call.LineStart, call.ColumnStart,
                code: DiagnosticCodes.Semantic.WrongArgumentCount,
                span: call.Span);
            // Check lambda for error recovery (records its type in SemanticInfo)
            CheckExpression(call.Function);
            return SemanticType.Unknown;
        }

        // 3. Build expected FunctionType from argument types
        var expectedFuncType = new FunctionType
        {
            ParameterTypes = argTypes,
            ReturnType = SemanticType.Unknown
        };

        // 4. Check lambda with expected type context
        var saved = _expectedType;
        _expectedType = expectedFuncType;
        var lambdaType = CheckExpression(call.Function);
        _expectedType = saved;

        // 5. Return the inferred return type
        return lambdaType is FunctionType ft ? ft.ReturnType : SemanticType.Unknown;
    }

}
