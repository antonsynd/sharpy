using System.Globalization;
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
        // Handle functools.partial(f, ...) — compatibility shim that desugars to a placeholder lambda
        if (FunctoolsPartialHelper.IsFunctoolsPartialCall(call, _symbolTable))
        {
            return CheckFunctoolsPartialCall(call);
        }

        // Handle None() — empty Optional constructor
        var noneResult = CheckNoneConstruction(call);
        if (noneResult != null)
            return noneResult;

        // Check for invalid tagged union constructor usage (wrong arity)
        if (call.Function is Identifier taggedId && call.KeywordArguments.Length == 0
            && _symbolTable.BuiltinRegistry.IsTaggedUnionConstructor(taggedId.Name)
            && _symbolTable.Lookup(taggedId.Name) == null)
        {
            if (call.Arguments.Length == 0)
            {
                var code = taggedId.Name == "Some"
                    ? DiagnosticCodes.Semantic.InvalidSomeConstructor
                    : DiagnosticCodes.Semantic.InvalidOkErrConstructor;
                AddError($"'{taggedId.Name}()' requires exactly one argument",
                    call.LineStart, call.ColumnStart, code: code, span: call.Span);
                return SemanticType.Unknown;
            }
            if (call.Arguments.Length > 1)
            {
                var code = taggedId.Name == "Some"
                    ? DiagnosticCodes.Semantic.InvalidSomeConstructor
                    : DiagnosticCodes.Semantic.InvalidOkErrConstructor;
                AddError($"'{taggedId.Name}()' takes exactly one argument, got {call.Arguments.Length}",
                    call.LineStart, call.ColumnStart, code: code, span: call.Span);
                foreach (var arg in call.Arguments)
                    CheckExpression(arg);
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

        // type(None) has no Sharpy equivalent — NoneType is not a real type
        if (call.Function is Identifier { Name: "type" } && call.Arguments.Length == 1
            && call.Arguments[0] is NoneLiteral)
        {
            AddError("type(None) is not supported; NoneType has no Sharpy equivalent",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UnsupportedTypeNone,
                span: call.Span);
            return SemanticType.Unknown;
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

        // Validate event invoke restrictions and __init__() call tracking
        var initEventResult = ValidateInitAndEventCalls(call);
        if (initEventResult != null)
            return initEventResult;

        // Resolve function symbol early for constructor inference on arguments
        var (earlyFuncSymbol, earlyParamOffset) = ResolveEarlyFunctionSymbol(call);

        // Check arguments and keyword arguments, collecting their types
        var calleeFunctionType = calleeType as FunctionType;
        var (argTypes, kwargTypes) = CheckCallArguments(call, earlyFuncSymbol, earlyParamOffset, calleeFunctionType);
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
                    return CheckConstructorCall(call, typeSymbol, argTypes, kwargTypes, totalArgCount);
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

                // Check if it's a variable with a FunctionType or delegate type - those are callable.
                // Use calleeType (the narrowed type) so an Optional function type narrowed via
                // `is not None` is recognized as callable.
                if (symbol is VariableSymbol varSym &&
                    (calleeType is FunctionType
                     || TryGetDelegateInvokeMethod(calleeType) != null))
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

            // Resolve user-defined module-level function overloads (same compilation)
            {
                var userOverloadResult = ResolveUserDefinedFunctionOverload(
                    id, argTypes, totalArgCount, call);
                if (userOverloadResult != null)
                    return userOverloadResult;
            }

            // Resolve imported function overloads (e.g., from os.path import join)
            {
                var importedOverloadResult = ResolveImportedFunctionOverload(
                    id, argTypes, totalArgCount, call);
                if (importedOverloadResult != null)
                    return importedOverloadResult;
            }
        }
        // Handle union case construction: Shape.Circle(5.0) → new Shape.Circle(5.0)
        else if (call.Function is MemberAccess unionCaseAccess
            && calleeType is UserDefinedType caseUdt
            && caseUdt.Symbol?.BaseType is { TypeKind: TypeKind.Union } unionBaseSymbol)
        {
            return CheckUnionCaseConstruction(call, caseUdt, unionBaseSymbol, argTypes);
        }
        // Handle member access function calls (e.g., module.function() or obj.method())
        // Skip super() calls - they're already validated by ValidateSuperMemberAccess
        else if (call.Function is MemberAccess memberAccessCall && memberAccessCall.Object is not SuperExpression)
        {
            // Module-qualified constructor call (e.g., fractions.Fraction(1, 2)): resolve the
            // member to an exported TypeSymbol and route into the shared constructor-checking
            // path, which validates arguments, abstract-class usage, and deprecation.
            if (TryResolveTypeSymbolFromMemberAccess(memberAccessCall) is { } moduleTypeSymbol)
            {
                return CheckConstructorCall(call, moduleTypeSymbol, argTypes, kwargTypes, totalArgCount);
            }

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

        // If callee type is Unknown, this is error recovery from a sub-expression.
        // Explicitly mark the FunctionCall as error recovery as a safety net — transitive
        // tracking in CheckExpression usually handles this, but some paths (e.g., property
        // type resolution) can return Unknown without marking or emitting an error.
        // Otherwise, the callee evaluated to a non-callable type — emit an error.
        if (calleeType is UnknownType)
        {
            MarkExpressionAsErrorRecovery(call);
        }
        else
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
        // Special handling for array construction: array[T](size) -> new T[size]
        if (call.Function is IndexAccess arrayAccess &&
            arrayAccess.Object is Identifier arrayId &&
            arrayId.Name == BuiltinNames.Array)
        {
            var arrayTypeArgs = TryResolveTypeArguments(arrayAccess.Index);
            if (arrayTypeArgs != null && arrayTypeArgs.Count == 1)
            {
                if (call.Arguments.Length != 1)
                {
                    AddError("Array constructor requires exactly 1 argument (the size)",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                    return SemanticType.Unknown;
                }

                if (argTypes.Count > 0 && argTypes[0] != SemanticType.Unknown &&
                    argTypes[0] != SemanticType.Int && argTypes[0] != SemanticType.Long)
                {
                    AddError("Array size must be an integer",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: call.Span);
                }

                return new GenericType
                {
                    Name = BuiltinNames.Array,
                    TypeArguments = arrayTypeArgs
                };
            }
        }

        // Special handling for generic type instantiation: Box[int](42) or Pair[int, str](1, "a")
        // This is parsed as FunctionCall(Function: IndexAccess(Object: Box, Index: int or TupleLiteral), Arguments: [...])
        // The object may be a bare identifier (Box) or a module-qualified member access
        // (difflib.SequenceMatcher), both of which can resolve to a generic TypeSymbol.
        if (call.Function is IndexAccess indexAccess &&
            TryResolveGenericTypeSymbolFromIndexObject(indexAccess.Object) is { IsGeneric: true } genericTypeSymbol)
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
    /// Captures all inputs for overload resolution, reducing parameter count on <see cref="ResolveOverloadCore"/>.
    /// </summary>
    /// <param name="Candidates">The list of overload candidates.</param>
    /// <param name="TotalArgCount">Total argument count at the call site.</param>
    /// <param name="ArgTypes">Resolved argument types.</param>
    /// <param name="SkipSelfParam">If true, computes per-overload self offset (for instance methods).</param>
    /// <param name="TypeSubstitution">Optional function to substitute type parameters before comparison.</param>
    /// <param name="ReturnFirstMatch">If true, returns the first matching overload (builtin behavior).
    /// If false, collects all matches and disambiguates by exact arity.</param>
    /// <param name="SkipUnknownTypes">If true, skip type comparison when either side is UnknownType.</param>
    /// <param name="KeywordArgNames">Names of keyword arguments at the call site, used to filter out
    /// overloads that lack matching parameter names (e.g., a params overload with no 'reverse' param).</param>
    internal record OverloadResolutionContext(
        List<FunctionSymbol> Candidates,
        int TotalArgCount,
        List<SemanticType> ArgTypes,
        bool SkipSelfParam = false,
        Func<SemanticType, SemanticType>? TypeSubstitution = null,
        bool ReturnFirstMatch = false,
        bool SkipUnknownTypes = false,
        IReadOnlyCollection<string>? KeywordArgNames = null);

    /// <summary>
    /// Core overload resolution algorithm shared by all overload resolution methods.
    /// Performs two-pass matching: first filters by argument count, then checks type compatibility.
    /// </summary>
    /// <returns>A tuple of (matched overload, arity-filtered candidates, whether resolution was ambiguous).</returns>
    private (FunctionSymbol? Match, List<FunctionSymbol> ArityCandidates, bool IsAmbiguous) ResolveOverloadCore(
        OverloadResolutionContext context)
    {
        int GetSelfOffset(FunctionSymbol o) =>
            context.SkipSelfParam && o.Parameters.Count > 0 && o.Parameters[0].Name == PythonNames.Self ? 1 : 0;

        // First pass: filter by argument count
        var arityCandidates = context.Candidates.Where(o =>
        {
            var selfOffset = GetSelfOffset(o);
            var requiredParams = o.Parameters.Skip(selfOffset).Count(p => !p.HasDefault && !p.IsVariadic);
            var hasVariadic = o.Parameters.Skip(selfOffset).Any(p => p.IsVariadic);
            var totalParams = o.Parameters.Count - selfOffset;
            if (hasVariadic)
                return context.TotalArgCount >= requiredParams;
            return context.TotalArgCount >= requiredParams && context.TotalArgCount <= totalParams;
        }).ToList();

        // Filter by keyword argument names: exclude overloads where
        // (a) any keyword arg name has no matching parameter, or
        // (b) the positional arg count doesn't cover the remaining required params
        //     after removing keyword-satisfied ones.
        // This disambiguates calls like merge(a, b, reverse=True) between a params
        // overload and one with a named 'reverse' parameter.
        if (context.KeywordArgNames is { Count: > 0 })
        {
            var positionalArgCount = context.TotalArgCount - context.KeywordArgNames.Count;
            var kwFiltered = arityCandidates.Where(o =>
            {
                var selfOffset = GetSelfOffset(o);
                var paramsAfterSelf = o.Parameters.Skip(selfOffset).ToList();
                var paramNames = paramsAfterSelf.Select(p => p.Name).ToHashSet();

                // Every keyword arg must have a matching parameter name
                if (!context.KeywordArgNames.All(kw => paramNames.Contains(kw)))
                    return false;

                // For non-variadic overloads, verify that positional args cover
                // exactly the required parameters NOT supplied by keyword args.
                if (!paramsAfterSelf.Any(p => p.IsVariadic))
                {
                    var kwSet = context.KeywordArgNames.ToHashSet();
                    var nonKwRequired = paramsAfterSelf
                        .Where(p => !p.HasDefault && !kwSet.Contains(p.Name))
                        .Count();
                    var nonKwTotal = paramsAfterSelf
                        .Where(p => !kwSet.Contains(p.Name))
                        .Count();
                    if (positionalArgCount < nonKwRequired || positionalArgCount > nonKwTotal)
                        return false;
                }

                return true;
            }).ToList();

            // Only apply the filter if it leaves at least one candidate;
            // otherwise fall through to normal resolution so existing error
            // reporting (unknown keyword argument) kicks in.
            if (kwFiltered.Count > 0)
                arityCandidates = kwFiltered;
        }

        // Second pass: check type compatibility
        var matchingOverloads = new List<FunctionSymbol>();
        foreach (var overload in arityCandidates)
        {
            var selfOffset = GetSelfOffset(overload);
            bool typesMatch = true;
            var variadicParam = overload.Parameters.Skip(selfOffset).FirstOrDefault(p => p.IsVariadic);

            for (int i = 0; i < context.ArgTypes.Count; i++)
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

                if (context.TypeSubstitution != null)
                    expectedType = context.TypeSubstitution(expectedType);

                if (context.SkipUnknownTypes && (expectedType is UnknownType || context.ArgTypes[i] is UnknownType))
                    continue;

                // Type parameters act as wildcards during overload resolution —
                // generic type inference happens later in C# compilation.
                if (expectedType is TypeParameterType || ContainsTypeParameter(expectedType))
                    continue;

                if (!IsAssignable(context.ArgTypes[i], expectedType))
                {
                    typesMatch = false;
                    break;
                }
            }
            if (typesMatch)
            {
                if (context.ReturnFirstMatch)
                    return (overload, arityCandidates, false);
                matchingOverloads.Add(overload);
            }
        }

        if (context.ReturnFirstMatch)
            return (null, arityCandidates, false);

        // Disambiguate: prefer exact arity match
        if (matchingOverloads.Count > 1)
        {
            var exactArityMatches = matchingOverloads.Where(o =>
                o.Parameters.Count - GetSelfOffset(o) == context.TotalArgCount
            ).ToList();

            if (exactArityMatches.Count == 1)
                return (exactArityMatches[0], arityCandidates, false);

            // When multiple exact-arity overloads remain, prefer the one with fewer
            // type parameters. This breaks ties between e.g. Merge<T>(a, b, reverse)
            // and Merge<T, TKey>(iterables[], key, reverse) by choosing the simpler generic.
            var candidates = exactArityMatches.Count > 1 ? exactArityMatches : matchingOverloads;
            var minTypeParams = candidates.Min(o => o.TypeParameters.Count);
            var fewerTypeParamMatches = candidates.Where(o => o.TypeParameters.Count == minTypeParams).ToList();
            if (fewerTypeParamMatches.Count == 1)
                return (fewerTypeParamMatches[0], arityCandidates, false);

            // Specificity tiebreaker: prefer the overload whose parameter types are
            // strictly more specific (e.g., list[int] beats IEnumerable<int>).
            // Follows C#'s "better function member" rule (§12.6.4.3).
            var specificityWinner = FindMostSpecificOverload(fewerTypeParamMatches, context);
            if (specificityWinner != null)
                return (specificityWinner, arityCandidates, false);

            return (null, arityCandidates, true);
        }

        return (matchingOverloads.Count == 1 ? matchingOverloads[0] : null, arityCandidates, false);
    }

    /// <summary>
    /// Determines whether overload <paramref name="a"/> has strictly more specific parameter
    /// types than overload <paramref name="b"/> for the given call arguments.  A parameter is
    /// "more specific" when its type is assignable to the other's but not vice-versa
    /// (e.g., <c>list[int]</c> is more specific than <c>IEnumerable&lt;int&gt;</c>).
    /// Mirrors C#'s "better function member" rule (§12.6.4.3).
    /// </summary>
    private bool IsMoreSpecificOverload(FunctionSymbol a, FunctionSymbol b, OverloadResolutionContext context)
    {
        int SelfOffset(FunctionSymbol o) =>
            context.SkipSelfParam && o.Parameters.Count > 0 && o.Parameters[0].Name == PythonNames.Self ? 1 : 0;

        var selfOffsetA = SelfOffset(a);
        var selfOffsetB = SelfOffset(b);
        var variadicA = a.Parameters.Skip(selfOffsetA).FirstOrDefault(p => p.IsVariadic);
        var variadicB = b.Parameters.Skip(selfOffsetB).FirstOrDefault(p => p.IsVariadic);

        bool hasStrictlyBetter = false;

        for (int i = 0; i < context.ArgTypes.Count; i++)
        {
            SemanticType GetParamType(FunctionSymbol o, int selfOff, ParameterSymbol? variadic)
            {
                var paramIdx = i + selfOff;
                if (paramIdx < o.Parameters.Count && !o.Parameters[paramIdx].IsVariadic)
                    return o.Parameters[paramIdx].Type;
                if (variadic != null)
                    return variadic.Type;
                return SemanticType.Unknown;
            }

            var paramTypeA = GetParamType(a, selfOffsetA, variadicA);
            var paramTypeB = GetParamType(b, selfOffsetB, variadicB);

            if (context.TypeSubstitution != null)
            {
                paramTypeA = context.TypeSubstitution(paramTypeA);
                paramTypeB = context.TypeSubstitution(paramTypeB);
            }

            // Equal types contribute nothing to the comparison — unless the
            // underlying CLR types differ (e.g., ClrTypeMapper maps IEnumerable<T>
            // and Sharpy.List<T> both to list[T]).
            if (paramTypeA.Equals(paramTypeB))
            {
                var clrTypeA = ResolveClrParameterType(a, i + selfOffsetA, paramTypeA);
                var clrTypeB = ResolveClrParameterType(b, i + selfOffsetB, paramTypeB);
                if (clrTypeA != null && clrTypeB != null && clrTypeA != clrTypeB)
                {
                    if (clrTypeB.IsAssignableFrom(clrTypeA) && !clrTypeA.IsAssignableFrom(clrTypeB))
                        hasStrictlyBetter = true;
                    else if (clrTypeA.IsAssignableFrom(clrTypeB) && !clrTypeB.IsAssignableFrom(clrTypeA))
                        return false;
                }
                continue;
            }

            var aToB = IsAssignable(paramTypeA, paramTypeB);
            var bToA = IsAssignable(paramTypeB, paramTypeA);

            if (aToB && !bToA)
            {
                // A's parameter is strictly more specific at this position.
                hasStrictlyBetter = true;
            }
            else if (bToA && !aToB)
            {
                // A's parameter is strictly less specific at this position — A cannot win.
                return false;
            }
            // Both assignable or neither: no preference at this position.
        }

        return hasStrictlyBetter;
    }

    /// <summary>
    /// Returns the effective CLR type for a parameter, with generic type parameters
    /// replaced by <c>typeof(object)</c> so <see cref="Type.IsAssignableFrom"/> works.
    /// Prefers the original CLR metadata from <see cref="FunctionSymbol.ClrMethod"/>
    /// (preserving IEnumerable vs List distinction that <see cref="Discovery.ClrTypeMapper"/>
    /// erases), falling back to <see cref="TryGetClrType"/> for source-defined overloads.
    /// </summary>
    private Type? ResolveClrParameterType(FunctionSymbol func, int paramIdx, SemanticType semanticType)
    {
        if (func.ClrMethod != null)
        {
            var clrParams = func.ClrMethod.GetParameters();
            if (paramIdx < clrParams.Length)
                return SubstituteGenericParameters(clrParams[paramIdx].ParameterType);
        }

        if (paramIdx < func.Parameters.Count)
        {
            var clrTypeName = func.Parameters[paramIdx].ClrTypeName;
            if (!string.IsNullOrEmpty(clrTypeName))
            {
                var clrType = Type.GetType(clrTypeName);
                if (clrType == null)
                {
                    clrType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType(clrTypeName!))
                        .FirstOrDefault(t => t != null);
                }
                if (clrType != null)
                {
                    if (clrType.IsGenericTypeDefinition)
                        return clrType.MakeGenericType(
                            Enumerable.Repeat(typeof(object), clrType.GetGenericArguments().Length).ToArray());
                    return clrType;
                }
            }
        }

        return TryGetClrType(semanticType);
    }

    private static Type SubstituteGenericParameters(Type type)
    {
        if (type.IsGenericParameter)
            return typeof(object);
        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            var resolved = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                resolved[i] = SubstituteGenericParameters(args[i]);
            return type.GetGenericTypeDefinition().MakeGenericType(resolved);
        }
        if (type.IsArray)
            return SubstituteGenericParameters(type.GetElementType()!).MakeArrayType();
        return type;
    }

    /// <summary>
    /// Finds the single candidate whose parameter types are more specific than all other
    /// candidates (the "best" function member).  Returns <see langword="null"/> if no single
    /// candidate dominates or if the list has fewer than two entries.
    /// </summary>
    private FunctionSymbol? FindMostSpecificOverload(List<FunctionSymbol> candidates, OverloadResolutionContext context)
    {
        if (candidates.Count < 2)
            return null;

        FunctionSymbol? best = null;
        foreach (var candidate in candidates)
        {
            bool beatsAll = true;
            foreach (var other in candidates)
            {
                if (ReferenceEquals(candidate, other))
                    continue;
                if (!IsMoreSpecificOverload(candidate, other, context))
                {
                    beatsAll = false;
                    break;
                }
            }
            if (beatsAll)
            {
                if (best != null)
                    return null; // Two candidates both beat all others — still ambiguous.
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Extracts keyword argument names from a function call for use in overload filtering.
    /// Returns null when there are no keyword arguments (avoids allocating an empty collection).
    /// </summary>
    private static IReadOnlyCollection<string>? ExtractKeywordArgNames(FunctionCall call)
    {
        if (call.KeywordArguments.Length == 0)
            return null;
        return call.KeywordArguments.Select(kw => kw.Name).ToList();
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
            (funcSymbol == null || overloads!.Contains(funcSymbol));
        if (!needsOverloadResolution)
            return null;

        var kwNames = ExtractKeywordArgNames(call);
        var (matchingOverload, _, _) = ResolveOverloadCore(
            new OverloadResolutionContext(overloads!, totalArgCount, argTypes,
                ReturnFirstMatch: true, KeywordArgNames: kwNames));

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
            return required == total ? total.ToString(CultureInfo.InvariantCulture) : $"{required}-{total}";
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
        var rawObjectType = _semanticInfo.GetExpressionType(memberAccess.Object);
        if (rawObjectType == null)
            return null;
        var objectType = UnwrapCallTarget(rawObjectType);

        TypeSymbol? typeSymbol = null;
        List<SemanticType>? typeArgs = null;

        if (objectType is UserDefinedType { Symbol: { } udt })
        {
            typeSymbol = udt;
        }
        else
        {
            var (resolved, resolvedTypeArgs) = ResolveBuiltinTypeInfo(objectType);
            typeSymbol = resolved;
            typeArgs = resolvedTypeArgs;
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

        Func<SemanticType, SemanticType>? typeSubstitution = null;
        if (typeArgs != null && typeSymbol.TypeParameters.Count > 0)
        {
            var capturedTypeSymbol = typeSymbol;
            var capturedTypeArgs = typeArgs;
            typeSubstitution = t => SubstituteTypeParameters(t, capturedTypeSymbol.TypeParameters, capturedTypeArgs);
        }

        var kwNames = ExtractKeywordArgNames(call);
        var (matchingOverload, arityCandidates, isAmbiguous) = ResolveOverloadCore(
            new OverloadResolutionContext(overloads, totalArgCount, argTypes,
                SkipSelfParam: true, TypeSubstitution: typeSubstitution,
                SkipUnknownTypes: true, KeywordArgNames: kwNames));

        if (isAmbiguous || matchingOverload == null)
        {
            ReportOverloadError(memberAccess.Member, call, isAmbiguous, arityCandidates, totalArgCount);
            return SemanticType.Unknown;
        }

        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, matchingOverload);

        var returnType = matchingOverload.ReturnType;

        // Substitute type parameters for builtin generic types (e.g., T0 -> int for dict[str, int])
        if (typeSubstitution != null)
        {
            returnType = typeSubstitution(returnType);
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

        // Check base class chain using TypeHierarchyService
        foreach (var baseType in TypeHierarchyService.GetAllBaseTypes(type, SemanticBinding))
        {
            if (baseType.MethodOverloads.TryGetValue(methodName, out overloads) && overloads.Count > 0)
                return overloads;
        }

        // Check interfaces — handles interface-typed variables and interface
        // methods not found via base class chain (#364)
        foreach (var iface in TypeHierarchyService.GetAllInterfaces(type, SemanticBinding))
        {
            if (iface.MethodOverloads.TryGetValue(methodName, out overloads) && overloads.Count > 0)
                return overloads;
        }

        return null;
    }

    /// <summary>
    /// Reports an overload resolution error (ambiguous or no matching overload).
    /// Shared by all overload resolution methods to avoid duplicating diagnostic logic.
    /// </summary>
    private void ReportOverloadError(
        string calleeName, FunctionCall call, bool isAmbiguous,
        List<FunctionSymbol> arityCandidates, int totalArgCount)
    {
        if (isAmbiguous)
        {
            AddError($"Ambiguous call to overloaded method '{calleeName}' — multiple overloads match the argument types",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AmbiguousOverload,
                span: call.Span);
        }
        else if (arityCandidates.Count == 0)
        {
            AddError($"No matching overload for '{calleeName}' with {totalArgCount} argument(s)",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NoMatchingOverload,
                span: call.Span);
        }
        else
        {
            AddError($"No matching overload for '{calleeName}' with the given argument types",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NoMatchingOverload,
                span: call.Span);
        }
    }

    /// <summary>
    /// Unwraps nullable/optional types for null-conditional method calls.
    /// Returns the unwrapped type.
    /// </summary>
    private static SemanticType UnwrapCallTarget(SemanticType type)
    {
        if (type is NullableType nt)
            return nt.UnderlyingType;
        if (type is OptionalType ot)
            return ot.UnderlyingType;
        return type;
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
    /// Resolves a member access whose object is an (possibly nested) imported module and whose
    /// member is an exported <see cref="TypeSymbol"/> — e.g., <c>fractions.Fraction</c> or
    /// <c>email.message.Message</c>. Applies a PascalCase fallback for .NET modules. Returns the
    /// exported type symbol, or null when the object is not a module or the member is not a type.
    /// </summary>
    private TypeSymbol? TryResolveTypeSymbolFromMemberAccess(MemberAccess memberAccess)
    {
        // The object is usually already checked (CheckCall runs CheckExpression on the callee
        // before routing here); fall back to checking it for paths that reach this helper
        // first (e.g., generic index-access resolution). Nested module access (email.message)
        // returns a ModuleType, so this handles both direct and nested module qualifiers.
        var objectType = _semanticInfo.GetExpressionType(memberAccess.Object)
            ?? CheckExpression(memberAccess.Object);
        if (objectType is not ModuleType moduleType)
            return null;

        var moduleSymbol = moduleType.Symbol;
        var memberName = memberAccess.Member;

        // For .NET modules, try PascalCase conversion if the exact name isn't found
        // (mirrors the module branch in CheckMemberAccess / CheckIndexAccess).
        if (!moduleSymbol.Exports.ContainsKey(memberName) && moduleSymbol.IsNetModule)
        {
            var pascalName = NameMangler.ToPascalCase(memberName);
            if (moduleSymbol.Exports.ContainsKey(pascalName))
                memberName = pascalName;
        }

        if (moduleSymbol.Exports.TryGetValue(memberName, out var exportedSymbol)
            && exportedSymbol is TypeSymbol typeSymbol)
        {
            return typeSymbol;
        }

        return null;
    }

    /// <summary>
    /// Resolves the object of a generic-instantiation index access (e.g., the <c>Box</c> in
    /// <c>Box[int](42)</c> or the <c>difflib.SequenceMatcher</c> in
    /// <c>difflib.SequenceMatcher[str](...)</c>) to its underlying <see cref="TypeSymbol"/>.
    /// Returns null when the object is neither a known type identifier nor a module-exported type.
    /// </summary>
    private TypeSymbol? TryResolveGenericTypeSymbolFromIndexObject(Expression indexObject) => indexObject switch
    {
        Identifier id => _symbolTable.Lookup(id.Name) as TypeSymbol,
        MemberAccess ma => TryResolveTypeSymbolFromMemberAccess(ma),
        _ => null
    };

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

        var kwNames = ExtractKeywordArgNames(call);
        var (matchingOverload, arityCandidates, isAmbiguous) = ResolveOverloadCore(
            new OverloadResolutionContext(overloads, totalArgCount, argTypes,
                SkipUnknownTypes: true, KeywordArgNames: kwNames));

        if (isAmbiguous || matchingOverload == null)
        {
            ReportOverloadError(memberAccess.Member, call, isAmbiguous, arityCandidates, totalArgCount);
            return SemanticType.Unknown;
        }

        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, matchingOverload);

        var returnType = InferGenericReturnType(matchingOverload, argTypes, call);

        if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
        {
            if (isOptionalNullConditional)
                return new OptionalType { UnderlyingType = returnType };
            return new NullableType { UnderlyingType = returnType };
        }
        return returnType;
    }

    /// <summary>
    /// Resolves calls to overloaded module-level functions defined in the current
    /// compilation (i.e., not imported). Reads overloads from
    /// SymbolTable.LookupFunctionOverloads and dispatches to the matching overload
    /// by arity/signature. Imported overloads are handled separately by
    /// ResolveImportedFunctionOverload.
    /// </summary>
    private SemanticType? ResolveUserDefinedFunctionOverload(
        Identifier id, List<SemanticType> argTypes, int totalArgCount, FunctionCall call)
    {
        var overloads = _symbolTable.LookupFunctionOverloads(id.Name);
        if (overloads == null || overloads.Count <= 1)
            return null;

        // Only handle overloads declared in the current file. Imported overloads
        // (different DeclaringFilePath) are resolved by ResolveImportedFunctionOverload.
        if (_currentFilePath == null || overloads[0].DeclaringFilePath != _currentFilePath)
            return null;

        var kwNames = ExtractKeywordArgNames(call);
        var (matchingOverload, arityCandidates, isAmbiguous) = ResolveOverloadCore(
            new OverloadResolutionContext(overloads!, totalArgCount, argTypes,
                SkipUnknownTypes: true, KeywordArgNames: kwNames));

        if (isAmbiguous || matchingOverload == null)
        {
            ReportOverloadError(id.Name, call, isAmbiguous, arityCandidates, totalArgCount);
            return SemanticType.Unknown;
        }

        // Update the identifier symbol to point to the matching overload
        _semanticInfo.SetIdentifierSymbol(id, matchingOverload);
        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, matchingOverload);

        return InferGenericReturnType(matchingOverload, argTypes, call);
    }

    /// <summary>
    /// Resolves overloaded functions that were imported via from-import (e.g., from os.path import join).
    /// Uses the same overload resolution logic as ResolveModuleFunctionOverload but reads from
    /// SymbolTable.LookupFunctionOverloads instead of ModuleSymbol.FunctionOverloads.
    /// </summary>
    private SemanticType? ResolveImportedFunctionOverload(
        Identifier id, List<SemanticType> argTypes, int totalArgCount, FunctionCall call)
    {
        var overloads = _symbolTable.LookupFunctionOverloads(id.Name);
        if (overloads == null || overloads.Count <= 1)
            return null;

        // Shadow check: if a user-defined function with the same name exists and it
        // was NOT imported from the same source as the overloads, it shadows them.
        // Skip overload resolution so the normal call path uses the user's function.
        var funcSymbol = _symbolTable.Lookup(id.Name) as FunctionSymbol;
        if (funcSymbol != null)
        {
            // Check if funcSymbol is from a different source than the overloads.
            // Imported overloads share a DeclaringFilePath; a local shadow won't.
            // Guard against null paths (e.g. CLR-discovered symbols) to avoid
            // null == null being treated as "same source".
            var overloadPath = overloads[0].DeclaringFilePath;
            if (overloadPath != null && funcSymbol.DeclaringFilePath != overloadPath)
                return null;
        }

        var kwNames = ExtractKeywordArgNames(call);
        var (matchingOverload, arityCandidates, isAmbiguous) = ResolveOverloadCore(
            new OverloadResolutionContext(overloads!, totalArgCount, argTypes,
                SkipUnknownTypes: true, KeywordArgNames: kwNames));

        if (isAmbiguous || matchingOverload == null)
        {
            ReportOverloadError(id.Name, call, isAmbiguous, arityCandidates, totalArgCount);
            return SemanticType.Unknown;
        }

        // Update the identifier symbol to point to the matching overload
        _semanticInfo.SetIdentifierSymbol(id, matchingOverload);
        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, matchingOverload);

        return InferGenericReturnType(matchingOverload, argTypes, call);
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

    private SemanticType InferGenericReturnType(
        FunctionSymbol overload, List<SemanticType> argTypes, FunctionCall call)
    {
        if (!overload.IsGeneric)
            return overload.ReturnType;

        var inferenceResult = _genericInference.InferTypeArguments(overload, argTypes);
        if (inferenceResult.Success && inferenceResult.InferredTypes != null)
        {
            _semanticInfo.SetInferredTypeArguments(call, inferenceResult.InferredTypes);
            return SubstituteTypeParameters(
                overload.ReturnType,
                overload.TypeParameters,
                inferenceResult.InferredTypes);
        }

        return overload.ReturnType;
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

        CheckDeprecatedUsage(funcSymbol, call);

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
            // Diagnose a type parameter that can only be inferred from a lambda argument whose
            // parameters are unannotated (#904). Without this, inference binds the parameter to
            // Unknown and the leak surfaces as a C# CS0411 in generated code. Annotated lambdas
            // provide concrete types and are unaffected.
            if (TryReportUninferrableLambdaTypeArg(call, funcSymbol))
                return SemanticType.Unknown;

            // Keyword arguments also carry type information for inference (e.g.
            // cmp_to_key(cmp=lambda a: int, b: int: ...)). Map them into their formal
            // parameter slots so the index-aligned inference can see them (#909).
            var inferenceArgTypes = BuildInferenceArgumentTypes(funcSymbol, argTypes, kwargTypes);
            var inferenceResult = _genericInference.InferTypeArguments(funcSymbol, inferenceArgTypes);
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
    /// Builds the positionally-aligned argument-type list used for generic inference, filling
    /// keyword arguments into their matching formal parameter slots. Generic inference is
    /// index-aligned to <see cref="FunctionSymbol.Parameters"/>, so a lambda passed by keyword
    /// (e.g. <c>cmp_to_key(cmp=...)</c>) must be placed in its parameter slot to contribute its
    /// type. When there are no keyword arguments the positional list is returned unchanged — the
    /// same <paramref name="argTypes"/> instance, not a copy, so callers must not mutate the result.
    /// Stops at the first parameter slot with neither a positional nor a keyword argument, since
    /// the index alignment cannot represent a gap.
    /// </summary>
    private static List<SemanticType> BuildInferenceArgumentTypes(
        FunctionSymbol funcSymbol,
        List<SemanticType> argTypes,
        Dictionary<string, SemanticType> kwargTypes)
    {
        if (kwargTypes.Count == 0)
            return argTypes;

        var ordered = new List<SemanticType>();
        for (int i = 0; i < funcSymbol.Parameters.Count; i++)
        {
            if (i < argTypes.Count)
                ordered.Add(argTypes[i]);
            else if (kwargTypes.TryGetValue(funcSymbol.Parameters[i].Name, out var kwargType))
                ordered.Add(kwargType);
            else
                break;
        }

        return ordered;
    }

    /// <summary>
    /// Emits SPY0237 when a generic function has a type parameter that appears only in
    /// <see cref="FunctionType"/>-shaped parameter positions and the argument bound to that
    /// formal slot — positionally or by keyword name — is a lambda with one or more unannotated
    /// parameters — i.e. the type parameter is genuinely uninferrable. Returns true (and reports)
    /// when such a case is found, so the caller can stop instead of binding the parameter to
    /// Unknown and leaking CS0411 (#904).
    /// Annotated lambdas supply concrete parameter types and never trigger this.
    /// </summary>
    private bool TryReportUninferrableLambdaTypeArg(FunctionCall call, FunctionSymbol funcSymbol)
    {
        if (!funcSymbol.IsGeneric || funcSymbol.TypeParameters.Count == 0)
            return false;

        foreach (var typeParam in funcSymbol.TypeParameters)
        {
            if (!IsTypeParameterUninferrableWithoutLambdaAnnotation(funcSymbol, typeParam.Name))
                continue;

            for (int i = 0; i < funcSymbol.Parameters.Count; i++)
            {
                var paramType = funcSymbol.Parameters[i].Type;
                if (paramType is not FunctionType functionParam
                    || !functionParam.ParameterTypes.Any(p => ReferencesTypeParameterNamed(p, typeParam.Name)))
                    continue;

                // Bind the i-th formal parameter to its argument: positionally to the i-th
                // positional argument, or by name to a matching keyword argument.
                var boundArg = i < call.Arguments.Length
                    ? call.Arguments[i]
                    : call.KeywordArguments.FirstOrDefault(k => k.Name == funcSymbol.Parameters[i].Name)?.Value;
                if (boundArg is LambdaExpression lambda
                    && lambda.Parameters.Any(p => p.Type == null))
                {
                    AddError(
                        $"Cannot infer type argument '{typeParam.Name}' for generic function " +
                        $"'{funcSymbol.Name}' from an unannotated lambda. Annotate the lambda " +
                        "parameters (e.g. `lambda a: int, b: int: ...`).",
                        call.LineStart, call.ColumnStart,
                        code: DiagnosticCodes.Semantic.CannotInferGenericType,
                        span: call.Span);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true when the named type parameter can only be inferred from the <em>parameter</em>
    /// types of a lambda/delegate argument — i.e. it appears in the parameter-type positions of some
    /// <see cref="FunctionType"/> parameter and in no position that an argument could bind on its own:
    /// a non-function parameter (bound directly from its argument) or a <see cref="FunctionType"/>
    /// parameter's <em>return</em> position (inferred from the lambda body, no annotation needed).
    /// This distinguishes <c>cmp_to_key(Func&lt;T,T,int&gt;)</c> (T only in lambda param positions →
    /// needs annotation) from <c>list.sort(key=Func&lt;T,TKey&gt;)</c> (TKey is in the return position,
    /// inferred from the body). The function's own outer return type is irrelevant — it can never
    /// bind a type parameter from arguments.
    /// </summary>
    private static bool IsTypeParameterUninferrableWithoutLambdaAnnotation(FunctionSymbol funcSymbol, string name)
    {
        var inLambdaParamPosition = false;
        var inInferablePosition = false;

        foreach (var param in funcSymbol.Parameters)
        {
            var type = param.Type;
            if (type is null || !ReferencesTypeParameterNamed(type, name))
                continue;

            if (type is FunctionType functionParam)
            {
                if (functionParam.ParameterTypes.Any(p => ReferencesTypeParameterNamed(p, name)))
                    inLambdaParamPosition = true;
                if (ReferencesTypeParameterNamed(functionParam.ReturnType, name))
                    inInferablePosition = true;
            }
            else
            {
                // A non-function parameter binds the type parameter directly from its argument.
                inInferablePosition = true;
            }
        }

        return inLambdaParamPosition && !inInferablePosition;
    }

    /// <summary>
    /// Validates a function call against a FunctionType (lambda/delegate calls without a FunctionSymbol).
    /// </summary>
    private SemanticType CheckLambdaCall(
        FunctionCall call, FunctionType ft, List<SemanticType> argTypes,
        int totalArgCount, bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        var paramTypes = ft.ParameterTypes;
        var returnType = ft.ReturnType;

        // Infer method-level generic type parameters from arguments BEFORE validation.
        // For methods like Map<U>(Func<T, U> f) -> Result<U, E>, the method-level
        // TypeParameterType("U") appears in both parameter types and return type.
        // We infer U from the actual argument types and substitute everywhere.
        if (ContainsTypeParameterType(returnType) || paramTypes.Any(ContainsTypeParameterType))
        {
            var typeParamMap = _genericInference.UnifyTypes(paramTypes, argTypes);
            if (typeParamMap != null && typeParamMap.Count > 0)
            {
                returnType = GenericTypeInferenceService.SubstituteTypeParameters(returnType, typeParamMap);
                paramTypes = paramTypes
                    .Select(p => GenericTypeInferenceService.SubstituteTypeParameters(p, typeParamMap))
                    .ToList();
            }
        }

        // Skip validation for .NET types with multiple constructor overloads
        // (C# compiler will handle overload resolution)
        if (!ft.SkipArgumentValidation)
        {
            var variadicIndex = ft.VariadicParameterIndex;
            var hasVariadic = variadicIndex.HasValue;

            // Validate argument count (accounting for optional parameters with defaults
            // and variadic params). The variadic parameter itself is not counted toward
            // the required minimum (it accepts zero or more), and variadic calls have
            // no upper bound on positional arguments.
            var requiredCount = paramTypes.Count - ft.OptionalParameterCount - (hasVariadic ? 1 : 0);
            var tooFew = totalArgCount < requiredCount;
            var tooMany = !hasVariadic && totalArgCount > paramTypes.Count;

            if (tooFew || tooMany)
            {
                if (hasVariadic)
                {
                    AddError($"Function expects at least {requiredCount} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                }
                else if (ft.OptionalParameterCount > 0 && requiredCount != paramTypes.Count)
                {
                    AddError($"Function expects {requiredCount} to {paramTypes.Count} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                }
                else
                {
                    AddError($"Function expects {paramTypes.Count} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                }
            }
            else
            {
                // Validate positional argument types. Arguments at or after the variadic
                // parameter index all bind to the variadic element type (paramTypes holds
                // the element type at that slot, not an array).
                for (int i = 0; i < argTypes.Count; i++)
                {
                    SemanticType expected;
                    if (hasVariadic && i >= variadicIndex!.Value)
                    {
                        expected = paramTypes[variadicIndex.Value];
                    }
                    else if (i < paramTypes.Count)
                    {
                        expected = paramTypes[i];
                    }
                    else
                    {
                        break;
                    }

                    if (!IsAssignable(argTypes[i], expected))
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{expected.GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: call.Arguments[i].Span);
                    }
                }
            }
        }

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
    /// Returns true when the parameter type is backed by CLR <see cref="System.Type"/> — i.e., it
    /// expects a type reference (e.g. assert_raises's exceptionType parameter). Discovery rehydrates
    /// System.Type as a <see cref="BuiltinType"/>; ClrTypeMapper produces a <see cref="UserDefinedType"/>.
    /// </summary>
    private static bool IsSystemTypeParameter(SemanticType paramType)
    {
        var clrType = paramType switch
        {
            BuiltinType bt => bt.ClrType,
            UserDefinedType udt => udt.Symbol?.ClrType,
            _ => null
        };
        return clrType == typeof(System.Type);
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
                    // PEP 675: string literals (and concatenations thereof) satisfy LiteralString
                    if (param.Type is LiteralStringType && i < call.Arguments.Length
                        && IsLiteralStringExpression(call.Arguments[i]))
                    {
                        // Allow — literal string expression satisfies LiteralString
                    }
                    // A type-reference expression (e.g. module.SomeError) satisfies a
                    // parameter backed by CLR System.Type (e.g. assert_raises's exceptionType).
                    else if (IsSystemTypeParameter(param.Type) && i < call.Arguments.Length
                        && _semanticInfo.IsTypeReference(call.Arguments[i]))
                    {
                        // Allow — type reference satisfies a System.Type parameter
                    }
                    else
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{param.Type.GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: call.Arguments[i].Span);
                    }
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
                        span: kwarg.Span ?? kwarg.Value.Span);
                }
                else if (param.IsPositionalOnly)
                {
                    AddError($"'{kwarg.Name}' is positional-only and cannot be passed as a keyword argument",
                        kwarg.LineStart, kwarg.ColumnStart,
                        code: DiagnosticCodes.Semantic.PositionalOnlyPassedByKeyword,
                        span: kwarg.Span ?? kwarg.Value.Span);
                }
                else
                {
                    var paramIndex = parameters.ToList().IndexOf(param);
                    if (!param.IsKeywordOnly && paramIndex < argTypes.Count)
                    {
                        AddError($"Argument '{kwarg.Name}' was already provided positionally",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                            span: kwarg.Span ?? kwarg.Value.Span);
                    }
                    else if (!IsAssignable(kwargTypes[kwarg.Name], param.Type)
                        && !(IsSystemTypeParameter(param.Type) && _semanticInfo.IsTypeReference(kwarg.Value)))
                    {
                        AddError($"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{param.Type.GetDisplayName()}'",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: kwarg.Span ?? kwarg.Value.Span);
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
                        span: kwarg.Span ?? kwarg.Value.Span);
                }
                else if (param.IsPositionalOnly)
                {
                    AddError($"'{kwarg.Name}' is positional-only and cannot be passed as a keyword argument",
                        kwarg.LineStart, kwarg.ColumnStart,
                        code: DiagnosticCodes.Semantic.PositionalOnlyPassedByKeyword,
                        span: kwarg.Span ?? kwarg.Value.Span);
                }
                else
                {
                    var paramIndex = parameters.ToList().IndexOf(param);
                    if (!param.IsKeywordOnly && paramIndex < argTypes.Count)
                    {
                        AddError($"Argument '{kwarg.Name}' was already provided positionally",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                            span: kwarg.Span ?? kwarg.Value.Span);
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

    /// <summary>
    /// Attempts to infer generic type arguments for a constructor call by creating a synthetic
    /// FunctionSymbol from the class's __init__ method and using GenericTypeInferenceService.
    /// Returns null if inference fails (caller should fall back to error or UnknownType).
    /// </summary>
    /// <summary>
    /// Handles union case construction: validates arguments against case fields
    /// and performs type parameter substitution for generic unions.
    /// </summary>
    private SemanticType CheckUnionCaseConstruction(
        FunctionCall call, UserDefinedType caseUdt, TypeSymbol unionBaseSymbol,
        List<SemanticType> argTypes)
    {
        var caseFields = caseUdt.Symbol!.Fields;

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

    /// <summary>
    /// Handles constructor calls: validates arguments against __init__ parameters,
    /// checks for abstract instantiation, and infers generic type arguments.
    /// </summary>
    private SemanticType CheckConstructorCall(
        FunctionCall call, TypeSymbol typeSymbol, List<SemanticType> argTypes,
        Dictionary<string, SemanticType> kwargTypes, int totalArgCount)
    {
        CheckDeprecatedUsage(typeSymbol, call);

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
                && expectedGeneric.TypeArguments.Count == typeSymbol.TypeParameters.Count
                && !expectedGeneric.TypeArguments.Any(ContainsTypeParameter))
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

                // Fallback: try __init__-based inference for user-defined generic constructors
                if (typeArgs == null)
                {
                    typeArgs = TryInferConstructorTypeArgs(typeSymbol, call, argTypes);
                }
            }
            else
            {
                // Multiple arguments or keyword arguments: infer type args from __init__ parameters
                typeArgs = TryInferConstructorTypeArgs(typeSymbol, call, argTypes);
            }

            // If inference failed, fall back to UnknownType args for builtin
            // collections (lets C# compiler report the real error) or emit
            // a diagnostic for user-defined generic types.
            if (typeArgs == null)
            {
                if (typeSymbol.Name is BuiltinNames.List or BuiltinNames.Set or BuiltinNames.Dict)
                {
                    typeArgs = Enumerable.Range(0, typeSymbol.TypeParameters.Count)
                        .Select(_ => (SemanticType)SemanticType.Unknown)
                        .ToList();
                }
                else
                {
                    AddError(
                        $"Cannot infer type arguments for '{typeSymbol.Name}'; " +
                        $"use explicit syntax: {typeSymbol.Name}[{string.Join(", ", typeSymbol.TypeParameters.Select(tp => tp.Name))}](...)",
                        call.LineStart, call.ColumnStart,
                        code: DiagnosticCodes.Semantic.CannotInferGenericType,
                        span: call.Span);
                    return SemanticType.Unknown;
                }
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

    /// <summary>
    /// Checks call arguments and keyword arguments, collecting their types.
    /// Sets _expectedType per-parameter when an early function symbol or callee FunctionType
    /// is available, enabling constructor inference (Some/None()/Ok/Err) in function arguments.
    /// </summary>
    private (List<SemanticType> ArgTypes, Dictionary<string, SemanticType> KwargTypes) CheckCallArguments(
        FunctionCall call, FunctionSymbol? earlyFuncSymbol, int earlyParamOffset, FunctionType? calleeFunctionType)
    {
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

        return (argTypes, kwargTypes);
    }

    /// <summary>
    /// Resolves the function symbol early for constructor inference on arguments.
    /// For simple identifier calls (foo(Some(42))), looks up the function before
    /// checking arguments, allowing _expectedType to be set per-parameter.
    /// </summary>
    /// <returns>The early-resolved function symbol and parameter offset (0 for functions, 1 for constructors skipping 'self').</returns>
    private (FunctionSymbol? Symbol, int ParamOffset) ResolveEarlyFunctionSymbol(FunctionCall call)
    {
        // Method calls (obj.method(...)): resolve the method on the receiver type so lambda
        // arguments get their parameter types inferred from the receiver-substituted signature
        // (e.g. list[str].sort(key=lambda s: ...) → s: str). See #889.
        if (call.Function is MemberAccess earlyMa && earlyMa.Object is not SuperExpression)
            return ResolveEarlyMethodSymbol(call, earlyMa);

        if (call.Function is not Identifier earlyId)
            return (null, 0);

        var earlySymbol = _symbolTable.Lookup(earlyId.Name);
        if (earlySymbol is FunctionSymbol fs && !fs.IsGeneric)
        {
            // Only use early resolution for non-generic, non-overloaded functions.
            // Generic functions need argument types first for inference.
            // Overloaded builtins need argument types for resolution.
            var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(earlyId.Name);
            if (overloads == null || overloads.Count <= 1 || !overloads.Contains(fs))
            {
                return (fs, 0);
            }
        }
        else if (earlySymbol is TypeSymbol ts && !ts.IsGeneric)
        {
            // Constructor call: Person(Some(42)) — look up __init__ for parameter types.
            // __init__ includes 'self' at index 0, but call arguments don't, so offset by 1.
            var initMethod = ts.Methods.FirstOrDefault(m => m.Name == DunderNames.Init);
            if (initMethod != null && !initMethod.IsGeneric)
            {
                return (initMethod, 1); // skip 'self' parameter
            }
        }

        return (null, 0);
    }

    /// <summary>
    /// Resolves a method symbol early for a member-access call (obj.method(...)) so that lambda
    /// arguments can have their parameter types inferred from the method signature. Parameter
    /// types are rewritten with the receiver's generic substitution applied (e.g. T → str for a
    /// <c>list[str]</c> receiver), while method-level type parameters (e.g. TKey in
    /// <c>Sort&lt;TKey&gt;(Func&lt;T, TKey&gt;, bool)</c>) are left unsubstituted — CheckLambda skips
    /// those per-position. Returns a synthesized FunctionSymbol used only for expected-type hints;
    /// the real overload is resolved later. Bails (null) when the method is missing or overloaded
    /// candidates disagree on a parameter type, so we never guess (#889).
    /// </summary>
    private (FunctionSymbol? Symbol, int ParamOffset) ResolveEarlyMethodSymbol(FunctionCall call, MemberAccess memberAccess)
    {
        // Scope this to calls that actually pass a lambda — the only case that benefits from
        // pre-resolved parameter types. This keeps every other method call on its existing
        // expected-type path, avoiding over-constraining context-sensitive args like None() (#889).
        if (!CallHasLambdaArgument(call))
            return (null, 0);

        var objectType = _semanticInfo.GetExpressionType(memberAccess.Object);
        if (objectType is null or UnknownType)
            return (null, 0);

        // Resolve the receiver's TypeSymbol and (for builtin generics) its type arguments.
        TypeSymbol? typeSymbol;
        List<SemanticType>? typeArgs = null;
        if (objectType is UserDefinedType { Symbol: { } udt })
        {
            typeSymbol = udt;
        }
        else
        {
            var (resolved, resolvedArgs) = ResolveBuiltinTypeInfo(objectType);
            typeSymbol = resolved;
            typeArgs = resolvedArgs;
        }

        if (typeSymbol == null)
            return (null, 0);

        // Gather candidate overloads for the method name. Prefer the explicit overload set;
        // otherwise fall back to same-named entries in Methods (how builtin collection methods
        // like list.sort are stored) and finally a single hierarchy lookup.
        var candidates = new List<FunctionSymbol>();
        var overloads = FindMethodOverloadsInHierarchy(typeSymbol, memberAccess.Member);
        if (overloads != null && overloads.Count > 0)
        {
            candidates.AddRange(overloads);
        }
        else
        {
            candidates.AddRange(typeSymbol.Methods.Where(m => m.Name == memberAccess.Member));
            if (candidates.Count == 0)
            {
                var (single, _) = FindMethodInHierarchy(typeSymbol, memberAccess.Member);
                if (single != null)
                    candidates.Add(single);
            }
        }

        if (candidates.Count == 0)
            return (null, 0);

        // Build the receiver substitution (T → str for list[str]). Method-level type
        // parameters (TKey) are not in typeSymbol.TypeParameters and stay unsubstituted.
        Func<SemanticType, SemanticType> substitution = static t => t;
        if (typeArgs != null && typeSymbol.TypeParameters.Count > 0
            && typeSymbol.TypeParameters.Count == typeArgs.Count)
        {
            var capturedSymbol = typeSymbol;
            var capturedArgs = typeArgs;
            substitution = t => SubstituteTypeParameters(t, capturedSymbol.TypeParameters, capturedArgs);
        }

        // Keep only the overloads compatible with this call's keyword-argument names, so a
        // `key=` call doesn't pick an overload that lacks `key` (e.g. list has both
        // Sort(bool reverse) and Sort<TKey>(Func<T,TKey> key, bool reverse)).
        var kwNames = ExtractKeywordArgNames(call);
        if (kwNames != null && kwNames.Count > 0)
        {
            var compatible = candidates
                .Where(c => kwNames.All(n => c.Parameters.Any(p => p.Name == n)))
                .ToList();
            if (compatible.Count > 0)
                candidates = compatible;
        }

        // Only provide expected types when unambiguous: all candidates must agree on the
        // substituted type of each parameter (by name). Otherwise bail — never guess.
        if (candidates.Count > 1 && !OverloadsAgreeOnParameterTypes(candidates, substitution))
            return (null, 0);

        // Use the parameter-richest remaining overload as the expected-type source. Since the
        // candidates agree per name, the superset gives the widest set of expected parameter
        // types (e.g. both `key` and `reverse`) without conflicting with the others.
        var chosen = candidates.OrderByDescending(c => c.Parameters.Count).First();
        var substitutedParameters = chosen.Parameters
            .Select(p => p with { Type = NormalizeExpectedParamType(substitution(p.Type)) })
            .ToList();
        var synthesized = chosen with { Parameters = substitutedParameters };
        return (synthesized, 0);
    }

    /// <summary>
    /// Maps an uninformative early-resolved parameter type to <see cref="SemanticType.Unknown"/>
    /// so CheckCallArguments leaves <c>_expectedType</c> unset. The discovery layer collapses some
    /// generic parameters (e.g. <c>list.append(T)</c>) to <c>object</c>; forcing that as an expected
    /// type would wrongly constrain context-sensitive arguments such as <c>None()</c> (#889).
    /// </summary>
    private static SemanticType NormalizeExpectedParamType(SemanticType type)
    {
        return type is BuiltinType { Name: BuiltinNames.Object } or UserDefinedType { Name: BuiltinNames.Object }
            ? SemanticType.Unknown
            : type;
    }

    /// <summary>
    /// Returns true when any positional or keyword argument of the call is a lambda
    /// (possibly wrapped in parentheses).
    /// </summary>
    private static bool CallHasLambdaArgument(FunctionCall call)
    {
        static bool IsLambda(Expression e) =>
            e is LambdaExpression || (e is Parenthesized p && p.Expression is LambdaExpression);

        foreach (var arg in call.Arguments)
            if (IsLambda(arg))
                return true;
        foreach (var kwarg in call.KeywordArguments)
            if (IsLambda(kwarg.Value))
                return true;
        return false;
    }

    /// <summary>
    /// Returns true when every overload exposes the same substituted parameter type for each
    /// parameter name shared across the candidates. Used to decide whether it is safe to pre-set
    /// expected parameter types for lambda inference without guessing between conflicting overloads.
    /// </summary>
    private static bool OverloadsAgreeOnParameterTypes(
        List<FunctionSymbol> candidates, Func<SemanticType, SemanticType> substitution)
    {
        var seen = new Dictionary<string, SemanticType>();
        foreach (var candidate in candidates)
        {
            foreach (var parameter in candidate.Parameters)
            {
                var substituted = substitution(parameter.Type);
                if (seen.TryGetValue(parameter.Name, out var existing))
                {
                    if (!existing.Equals(substituted))
                        return false;
                }
                else
                {
                    seen[parameter.Name] = substituted;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Validates event invoke restrictions and __init__() call tracking.
    /// Checks that events are only raised from within the declaring class,
    /// tracks super().__init__() calls, and validates self.__init__() usage.
    /// Returns a type if an early return is needed (e.g., event invoke violation),
    /// or null if the dispatcher should continue.
    /// </summary>
    private SemanticType? ValidateInitAndEventCalls(FunctionCall call)
    {
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

            // Validate keyword-argument names against the base constructor overloads so an
            // unknown kwarg surfaces as SPY0234 here instead of leaking a C# CS1739 (#907).
            // super()/no-parent context errors are already reported by ValidateSuperMemberAccess;
            // a null base yields no candidates, so this stays silent in those cases.
            if (_currentClass != null)
                ValidateInitializerKeywordArguments(call, GetBaseType(_currentClass));
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
            else if (_currentClass != null)
            {
                // Context is valid — validate keyword-argument names against this class's own
                // constructor overloads (#907). Skipped above to avoid double-reporting on calls
                // already flagged as out-of-context or conflicting.
                ValidateInitializerKeywordArguments(call, _currentClass);
            }
        }

        return null;
    }

    /// <summary>
    /// Reports SPY0234 for any keyword argument of a <c>super().__init__</c>/<c>self.__init__</c>
    /// call whose name matches no non-self parameter of any candidate constructor overload (#907).
    /// An empty candidate set (CLR base with no enumerated metadata, or no constructors) defers to
    /// the C# compiler and reports nothing.
    /// </summary>
    private void ValidateInitializerKeywordArguments(FunctionCall initCall, TypeSymbol? candidateRoot)
    {
        if (initCall.KeywordArguments.Length == 0 || candidateRoot == null)
            return;

        var candidates = ResolveInitializerConstructorCandidates(candidateRoot);
        if (candidates.Count == 0)
            return;

        foreach (var kwarg in initCall.KeywordArguments)
        {
            var matchesSomeOverload = candidates.Any(
                ctor => ctor.Parameters.Skip(1).Any(p => p.Name == kwarg.Name));
            if (!matchesSomeOverload)
            {
                AddError($"Unknown keyword argument '{kwarg.Name}'",
                    kwarg.LineStart, kwarg.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                    span: kwarg.Span ?? kwarg.Value.Span);
            }
        }
    }

    /// <summary>
    /// Handles None() — empty Optional constructor.
    /// Returns the result type if this is a None() call, or null if the dispatcher should continue.
    /// </summary>
    private SemanticType? CheckNoneConstruction(FunctionCall call)
    {
        if (call.Function is not NoneLiteral || call.Arguments.Length != 0 || call.KeywordArguments.Length != 0)
            return null;

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

    private List<SemanticType>? TryInferConstructorTypeArgs(
        TypeSymbol typeSymbol, FunctionCall call, List<SemanticType> argTypes)
    {
        var initMethods = typeSymbol.Methods.Where(m => m.Name == DunderNames.Init).ToList();
        if (initMethods.Count != 1)
            return null;

        var initMethod = initMethods[0];
        // Skip 'self' parameter
        var initParams = initMethod.Parameters.Skip(1).ToList();

        // Create a synthetic FunctionSymbol with the class's type parameters
        // and the __init__'s parameters (minus self) so GenericTypeInferenceService
        // can unify the argument types against the parameter types.
        var syntheticFunc = new FunctionSymbol
        {
            Name = typeSymbol.Name,
            Parameters = initParams,
            TypeParameters = typeSymbol.TypeParameters,
        };

        var inferenceResult = _genericInference.InferTypeArguments(syntheticFunc, argTypes);
        if (inferenceResult.Success && inferenceResult.InferredTypes != null)
        {
            _semanticInfo.SetInferredTypeArguments(call, inferenceResult.InferredTypes);
            return inferenceResult.InferredTypes;
        }

        return null;
    }

    private void CheckDeprecatedUsage(Symbol symbol, Expression callSite)
    {
        if (symbol.DeprecationMessage != null)
        {
            _diagnostics.AddWarning(
                $"'{symbol.Name}' is deprecated: {symbol.DeprecationMessage}",
                callSite.LineStart, callSite.ColumnStart, _currentFilePath,
                code: DiagnosticCodes.Validation.DeprecatedUsage,
                phase: CompilerPhase.TypeChecking);
        }
    }

    private static bool IsLiteralStringExpression(Expression expr)
    {
        return expr switch
        {
            StringLiteral => true,
            BinaryOp { Operator: BinaryOperator.Add, Left: var left, Right: var right }
                => IsLiteralStringExpression(left) && IsLiteralStringExpression(right),
            _ => false
        };
    }

    /// <summary>
    /// Type-checks a <c>functools.partial(f, fixed_args..., kw=val, ...)</c> call.
    /// Validates the target is callable, type-checks the fixed arguments against the target's
    /// parameters, and returns a <see cref="FunctionType"/> describing the remaining (unfixed)
    /// parameters. Emits SPY1010 to encourage migration to the idiomatic <c>_</c> placeholder form.
    /// </summary>
    private SemanticType CheckFunctoolsPartialCall(FunctionCall call)
    {
        // Resolve the 'functools' module identifier so SemanticInfo records the binding;
        // LSP find-references and go-to-definition for 'functools' continues to work even
        // though we bypass the normal member-access resolution.
        if (call.Function is MemberAccess memberAccess && memberAccess.Object is Identifier moduleId)
        {
            _ = CheckExpression(moduleId);
        }

        // Emit the placeholder-form suggestion.
        AddInfo(
            "Prefer the '_' placeholder syntax over functools.partial for new code; e.g., 'add(5, _)' instead of 'functools.partial(add, 5)'.",
            call.LineStart, call.ColumnStart,
            code: DiagnosticCodes.Info.FunctoolsPartialPlaceholderHint);

        if (call.Arguments.IsDefaultOrEmpty || call.Arguments.Length == 0)
        {
            AddError(
                "functools.partial() requires at least one argument (the target callable)",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                span: call.Span);
            return SemanticType.Unknown;
        }

        // Validate the target is callable
        var targetExpr = call.Arguments[0];
        var targetType = CheckExpression(targetExpr);

        FunctionType? targetFunctionType = null;
        FunctionSymbol? targetFunctionSymbol = null;

        // Prefer FunctionSymbol when available (preserves parameter names for keyword fixing)
        if (targetExpr is Identifier targetId)
        {
            targetFunctionSymbol = _symbolTable.Lookup(targetId.Name) as FunctionSymbol;
        }

        if (targetType is FunctionType ft)
        {
            targetFunctionType = ft;
        }
        else if (targetFunctionSymbol != null)
        {
            targetFunctionType = BuildFunctionTypeFromSymbol(targetFunctionSymbol);
        }
        else if (targetType is UnknownType)
        {
            // Error recovery — already emitted
            MarkExpressionAsErrorRecovery(call);
            return SemanticType.Unknown;
        }

        if (targetFunctionType == null)
        {
            AddError(
                $"First argument to functools.partial() must be callable; got '{targetType.GetDisplayName()}'",
                targetExpr.LineStart, targetExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedFunction,
                span: targetExpr.Span);
            return SemanticType.Unknown;
        }

        // Type-check fixed positional and keyword args so SemanticInfo records their types
        var fixedPositionalCount = call.Arguments.Length - 1;
        for (var i = 1; i < call.Arguments.Length; i++)
        {
            _ = CheckExpression(call.Arguments[i]);
        }

        var fixedKwargNames = new HashSet<string>(call.KeywordArguments.Length, System.StringComparer.Ordinal);
        foreach (var kwarg in call.KeywordArguments)
        {
            _ = CheckExpression(kwarg.Value);
            fixedKwargNames.Add(kwarg.Name);
        }

        // Compute remaining parameters:
        //   Positional fix consumes leading parameters in declaration order.
        //   Keyword fix removes parameters by name (requires FunctionSymbol for names).
        FunctionType resultType;
        if (targetFunctionSymbol != null)
        {
            resultType = ComputeResultTypeFromSymbol(targetFunctionSymbol, fixedPositionalCount,
                fixedKwargNames, targetExpr);
        }
        else
        {
            // FunctionType has no parameter names — keyword fixing is unsupported in this path
            if (fixedKwargNames.Count > 0)
            {
                AddError(
                    "Keyword arguments to functools.partial() require the target to be a named function; consider using '_' placeholder syntax with explicit keyword arguments instead.",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: call.Span);
                return SemanticType.Unknown;
            }

            var computed = FunctoolsPartialHelper.ComputeResultTypeFromFunctionType(
                targetFunctionType, fixedPositionalCount);
            if (computed == null)
            {
                AddError(
                    $"Too many positional arguments to functools.partial(); target accepts at most {targetFunctionType.ParameterTypes.Count} positional parameter(s).",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
                return SemanticType.Unknown;
            }
            resultType = computed;
        }

        _semanticInfo.SetExpressionType(call, resultType);
        return resultType;
    }

    /// <summary>
    /// Builds a <see cref="FunctionType"/> from a <see cref="FunctionSymbol"/>, projecting
    /// each parameter's resolved <see cref="SemanticType"/> into the parameter list.
    /// </summary>
    private static FunctionType BuildFunctionTypeFromSymbol(FunctionSymbol funcSymbol)
    {
        return FunctionType.FromParameters(funcSymbol.Parameters, funcSymbol.ReturnType);
    }

    /// <summary>
    /// Computes the result <see cref="FunctionType"/> for a <c>functools.partial</c> call when
    /// the target is a named <see cref="FunctionSymbol"/>. Positional fixing removes leading
    /// parameters; keyword fixing removes parameters by name.
    /// </summary>
    private FunctionType ComputeResultTypeFromSymbol(FunctionSymbol targetSymbol,
        int fixedPositionalCount, HashSet<string> fixedKwargNames, Expression targetExpr)
    {
        var parameters = targetSymbol.Parameters;

        if (fixedPositionalCount > parameters.Count)
        {
            AddError(
                $"Too many positional arguments to functools.partial(); '{targetSymbol.Name}' accepts at most {parameters.Count} positional parameter(s).",
                targetExpr.LineStart, targetExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.WrongArgumentCount,
                span: targetExpr.Span);
            fixedPositionalCount = parameters.Count;
        }

        var knownNames = new HashSet<string>(parameters.Count, System.StringComparer.Ordinal);
        for (var i = 0; i < parameters.Count; i++)
        {
            knownNames.Add(parameters[i].Name);
        }
        foreach (var kwName in fixedKwargNames)
        {
            if (!knownNames.Contains(kwName))
            {
                AddError(
                    $"functools.partial(): '{targetSymbol.Name}' has no parameter named '{kwName}'",
                    targetExpr.LineStart, targetExpr.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                    span: targetExpr.Span);
            }
        }

        var remaining = new List<SemanticType>();
        var optionalCount = 0;
        for (var i = fixedPositionalCount; i < parameters.Count; i++)
        {
            var p = parameters[i];
            if (fixedKwargNames.Contains(p.Name))
            {
                continue;
            }
            remaining.Add(p.Type);
            if (p.HasDefault)
            {
                optionalCount++;
            }
        }

        return new FunctionType
        {
            ParameterTypes = remaining,
            ReturnType = targetSymbol.ReturnType,
            OptionalParameterCount = optionalCount,
        };
    }

}
