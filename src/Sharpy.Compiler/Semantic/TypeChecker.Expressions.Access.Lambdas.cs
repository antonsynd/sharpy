using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Lambda expressions and parameter type inference
/// </summary>
internal partial class TypeChecker
{
    private SemanticType CheckLambda(LambdaExpression lambda)
    {
        // Use _expectedType for bidirectional type inference: if the context expects
        // a FunctionType, extract parameter types from it to infer lambda parameter types.
        FunctionType? expectedFunc = _expectedType as FunctionType;

        var paramTypes = new List<SemanticType>();
        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var param = lambda.Parameters[i];
            if (param.Type != null)
            {
                // Explicit type annotation — use it
                paramTypes.Add(_typeResolver.ResolveTypeAnnotation(param.Type));
            }
            else if (expectedFunc != null && i < expectedFunc.ParameterTypes.Count
                     && !ContainsTypeParameterType(expectedFunc.ParameterTypes[i]))
            {
                // Infer from expected function type context, but only if the expected
                // type is fully resolved. Unresolved TypeParameterType entries (e.g., T
                // in filter<T>(Func<T,bool>,...)) cannot be used for lambda param inference
                // because T hasn't been inferred yet at this point.
                paramTypes.Add(expectedFunc.ParameterTypes[i]);
            }
            else
            {
                paramTypes.Add(SemanticType.Unknown);
            }
        }

        // If any params are still Unknown (no target type context, no annotations),
        // try to infer types from the lambda body. This enables partial application
        // lowering (e.g., add(5, _) → lambda __placeholder_0: add(5, __placeholder_0))
        // to work without explicit type annotations.
        if (paramTypes.Any(t => t is UnknownType))
        {
            TryInferLambdaParamTypesFromBody(lambda, paramTypes);
        }

        // Enter lambda scope
        _symbolTable.EnterScope("lambda");

        // Enter an isolated narrowing scope for this lambda.
        // Type narrowings from the enclosing scope should NOT be visible inside the lambda,
        // because lambdas can be stored and called later when the narrowing condition no longer holds.
        // This is the same logic as for nested function definitions (task 1.7). The statement-level CFG
        // facts (#1042) are isolated the same way: cleared for the lambda body, restored afterward.
        using var _ = _narrowingContext.EnterIsolatedScope();
        var savedFacts = _currentFacts;
        _currentFacts = System.Array.Empty<Analysis.ControlFlow.NarrowingFact>();

        // Lambdas cannot be async, so await inside a lambda is invalid
        // (matches Python: await in lambda produces SyntaxError).
        var previousIsAsync = _currentFunctionIsAsync;
        _currentFunctionIsAsync = false;

        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var paramSymbol = new VariableSymbol
            {
                Name = lambda.Parameters[i].Name,
                Kind = SymbolKind.Parameter,
                Type = paramTypes[i],
                IsParameter = true
            };
            _symbolTable.Define(paramSymbol);
        }

        // Type-check default value expressions and validate compatibility
        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var param = lambda.Parameters[i];
            if (param.DefaultValue != null)
            {
                var defaultType = CheckExpression(param.DefaultValue);
                if (paramTypes[i] is not UnknownType && !IsAssignable(defaultType, paramTypes[i]))
                {
                    AddError(
                        $"Default value of type '{defaultType.GetDisplayName()}' is not assignable to parameter type '{paramTypes[i].GetDisplayName()}'",
                        param.DefaultValue.LineStart, param.DefaultValue.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: param.DefaultValue.Span);
                }
            }
        }

        var bodyType = CheckExpression(lambda.Body);

        // Validate explicit return type annotation (arrow lambda syntax)
        SemanticType returnType = bodyType;
        if (lambda.ReturnType != null)
        {
            var declaredReturnType = _typeResolver.ResolveTypeAnnotation(lambda.ReturnType);
            if (declaredReturnType is not UnknownType && bodyType is not UnknownType
                && !IsAssignable(bodyType, declaredReturnType))
            {
                AddError(
                    $"Arrow lambda body type '{bodyType.GetDisplayName()}' is not assignable to declared return type '{declaredReturnType.GetDisplayName()}'",
                    lambda.Body.LineStart, lambda.Body.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: lambda.Body.Span);
            }
            returnType = declaredReturnType;
        }

        _currentFunctionIsAsync = previousIsAsync;
        _currentFacts = savedFacts;
        _symbolTable.ExitScope();

        var optionalParamCount = lambda.Parameters.Count(p => p.DefaultValue != null);

        return new FunctionType
        {
            ParameterTypes = paramTypes,
            ReturnType = returnType,
            OptionalParameterCount = optionalParamCount
        };
    }

    /// <summary>
    /// #1161: checks a generic call's arguments with its unannotated lambda arguments <em>deferred</em>
    /// until the type parameters their parameter types depend on have been bound by the other
    /// arguments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A lambda sitting in a generic parameter slot — <c>filter(lambda x: x % 3 == 1, nums)</c> against
    /// <c>Filter&lt;T&gt;(Func&lt;T,bool&gt;, IEnumerable&lt;T&gt;)</c> — cannot be typed when it is
    /// reached in source order: <c>T</c> is still open, so <see cref="CheckLambda"/> declines the
    /// unresolved expected type and the parameter falls back to <see cref="SemanticType.Unknown"/>.
    /// Every fact recorded while checking the body is then computed from that placeholder: the read-node
    /// types codegen reads, operator lowerings, overload selections. Checking the other arguments first
    /// binds <c>T</c> from <c>nums</c>, so the body is checked ONCE, with final parameter types, and the
    /// node-keyed facts materialize correctly (the emitter's floored-<c>%</c> operand gate is the
    /// motivating reader — it is a pure applier and cannot recover the type itself).
    /// </para>
    /// <para>
    /// Deliberately conservative: returns false — leaving the caller's normal argument loop to run
    /// unchanged — unless a single unambiguous generic signature is in view AND some deferred lambda's
    /// parameter types reference a type parameter that a non-deferred argument can bind. A genuinely
    /// uninferrable lambda (type parameter only in lambda-parameter position) is therefore left on its
    /// existing path and still reaches <see cref="TryReportUninferrableLambdaTypeArg"/> (SPY0237, #904).
    /// </para>
    /// </remarks>
    /// <returns>
    /// True if this call's arguments were checked here, with <paramref name="argTypes"/> populated in
    /// source order and <paramref name="kwargTypes"/> keyed by keyword name; false to let the caller's
    /// normal argument loop run.
    /// </returns>
    private bool TryCheckDeferredLambdaArguments(
        FunctionCall call, Expression callee, FunctionSymbol? earlyFuncSymbol, int earlyParamOffset,
        out List<SemanticType> argTypes, out Dictionary<string, SemanticType> kwargTypes)
    {
        argTypes = null!;
        kwargTypes = null!;

        // A spread argument breaks the positional formal-to-actual alignment this pass relies on.
        foreach (var arg in call.Arguments)
        {
            if (arg is SpreadElement)
                return false;
        }

        var (signature, providesExpectedTypes) = ResolveDeferredLambdaSignature(call, callee, earlyFuncSymbol);
        if (signature == null || !signature.IsGeneric)
            return false;

        // Bind each formal parameter slot to the argument node that fills it: positionally when the
        // slot is within the positional list, otherwise by keyword name.
        var formalByPosition = new SemanticType?[call.Arguments.Length];
        var formalByKeyword = new Dictionary<string, SemanticType>();
        foreach (var (formalIndex, parameter) in signature.Parameters.Select((p, i) => (i, p)))
        {
            int position = formalIndex - earlyParamOffset;
            if (position >= 0 && position < call.Arguments.Length)
            {
                formalByPosition[position] = parameter.Type;
            }
            else if (call.KeywordArguments.Any(k => k.Name == parameter.Name))
            {
                formalByKeyword[parameter.Name] = parameter.Type;
            }
        }

        // Candidate deferrals: an unannotated lambda whose formal type is a function type with type
        // parameters in its parameter positions — exactly what CheckLambda cannot use yet.
        var deferredPositions = new List<int>();
        var deferredKeywords = new List<string>();
        for (int position = 0; position < call.Arguments.Length; position++)
        {
            if (IsDeferrableLambdaArgument(call.Arguments[position], formalByPosition[position]))
                deferredPositions.Add(position);
        }
        foreach (var kwarg in call.KeywordArguments)
        {
            if (formalByKeyword.TryGetValue(kwarg.Name, out var keywordFormal)
                && IsDeferrableLambdaArgument(kwarg.Value, keywordFormal))
            {
                deferredKeywords.Add(kwarg.Name);
            }
        }

        if (deferredPositions.Count == 0 && deferredKeywords.Count == 0)
            return false;

        // Only defer when deferral can actually help: some deferred lambda's parameter types must
        // reference a type parameter that a NON-deferred argument's formal type also references, so
        // checking the others first genuinely binds it.
        if (!AnyDeferredLambdaIsBindableByOtherArguments(
                call, formalByPosition, formalByKeyword, deferredPositions, deferredKeywords))
        {
            return false;
        }

        // Phase 1 — check every non-deferred argument in source order, collecting the formal/actual
        // pairs that can bind type parameters. Expected types are supplied only where the caller's
        // normal loop would have supplied them, so nothing outside the deferred slots changes.
        var positionTypes = new SemanticType?[call.Arguments.Length];
        kwargTypes = new Dictionary<string, SemanticType>();
        var formals = new List<SemanticType>();
        var actuals = new List<SemanticType>();
        var previousExpectedType = _expectedType;

        for (int position = 0; position < call.Arguments.Length; position++)
        {
            if (deferredPositions.Contains(position))
                continue;
            if (providesExpectedTypes && formalByPosition[position] is { } positionalFormal)
                _expectedType = positionalFormal is UnknownType ? null : positionalFormal;
            var actual = CheckExpression(call.Arguments[position]);
            _expectedType = previousExpectedType;
            positionTypes[position] = actual;
            if (formalByPosition[position] is { } formal)
            {
                formals.Add(formal);
                actuals.Add(actual);
            }
        }

        foreach (var kwarg in call.KeywordArguments)
        {
            if (deferredKeywords.Contains(kwarg.Name))
                continue;
            if (providesExpectedTypes && formalByKeyword.TryGetValue(kwarg.Name, out var keywordFormal))
                _expectedType = keywordFormal is UnknownType ? null : keywordFormal;
            var actual = CheckExpression(kwarg.Value);
            _expectedType = previousExpectedType;
            kwargTypes[kwarg.Name] = actual;
            if (formalByKeyword.TryGetValue(kwarg.Name, out var boundFormal))
            {
                formals.Add(boundFormal);
                actuals.Add(actual);
            }
        }

        var substitutions = _genericInference.UnifyTypes(formals, actuals)
            ?? new Dictionary<string, SemanticType>();

        // An iterable formal binds its type parameter to the ITERATION element type, which is not
        // always the structural unification result: iterating a bare dict yields its keys (#1154),
        // not its key/value pairs. Element-type inference is the same source the map path uses
        // (#1009), and it wins over the structural binding for these slots.
        for (int i = 0; i < formals.Count; i++)
        {
            if (formals[i] is GenericType { TypeArguments.Count: 1 } iterableFormal
                && iterableFormal.TypeArguments[0] is TypeParameterType elementParam
                && _typeInference.InferIterableElementType(actuals[i]) is { } elementType
                && elementType is not UnknownType)
            {
                substitutions[elementParam.Name] = elementType;
            }
        }

        // Phase 2 — check the deferred lambda bodies exactly once, with the substituted expected
        // function type. A type parameter the other arguments could not bind stays unsubstituted, and
        // CheckLambda declines it per position exactly as it does today.
        //
        // Each checked lambda is folded back into `substitutions` before the next position is checked,
        // because a deferred lambda's parameter types can depend on an EARLIER deferred lambda's return
        // type. That is what the three-stage shapes need: SelectMany's second selector takes the
        // TCollection its first selector's `list[TCollection]` return binds, and GroupBy's result
        // selector takes the TKey and TElement its first two bind. In all six measured three-stage
        // shapes on the acceptance surface, the dependent lambda is written AFTER the one it depends
        // on — dependency order is source order — so one pass in source order is enough and no
        // reordering exists to get wrong.
        foreach (var position in deferredPositions)
        {
            var formal = formalByPosition[position]!;
            SemanticType checkedType;
            using (ScopedValue.Push(ref _expectedType,
                       SubstituteExpectedLambdaType(formal, substitutions) ?? previousExpectedType))
            {
                checkedType = CheckExpression(call.Arguments[position]);
            }
            positionTypes[position] = checkedType;
            FoldCheckedArgumentIntoSubstitutions(formal, checkedType, substitutions);
        }
        foreach (var kwarg in call.KeywordArguments)
        {
            if (!deferredKeywords.Contains(kwarg.Name))
                continue;

            var keywordFormal = formalByKeyword[kwarg.Name];
            SemanticType checkedType;
            using (ScopedValue.Push(ref _expectedType,
                       SubstituteExpectedLambdaType(keywordFormal, substitutions) ?? previousExpectedType))
            {
                checkedType = CheckExpression(kwarg.Value);
            }
            kwargTypes[kwarg.Name] = checkedType;
            FoldCheckedArgumentIntoSubstitutions(keywordFormal, checkedType, substitutions);
        }

        argTypes = new List<SemanticType>(call.Arguments.Length);
        for (int position = 0; position < call.Arguments.Length; position++)
            argTypes.Add(positionTypes[position] ?? SemanticType.Unknown);
        return true;
    }

    /// <summary>
    /// Folds a just-checked argument's actual type back into <paramref name="substitutions"/>, so a
    /// later deferred lambda sees the type parameters this one bound.
    ///
    /// <para>
    /// Deliberately additive: a name phase 1 already bound is left alone. Phase 1's binding comes from
    /// unifying every non-deferred argument at once and is authoritative; letting a later lambda
    /// overwrite it would change what the existing single-deferral callers infer (#1161). With one
    /// deferred argument there is no later position, so those callers see no change at all.
    /// </para>
    /// </summary>
    private void FoldCheckedArgumentIntoSubstitutions(
        SemanticType formal, SemanticType actual, Dictionary<string, SemanticType> substitutions)
    {
        if (actual is UnknownType)
            return;

        var folded = _genericInference.UnifyTypes(new[] { formal }, new[] { actual });
        if (folded == null)
            return;

        foreach (var (name, type) in folded)
        {
            if (type is not UnknownType && !substitutions.ContainsKey(name))
                substitutions[name] = type;
        }
    }

    /// <summary>
    /// True when <paramref name="argument"/> is a lambda with at least one unannotated parameter and
    /// <paramref name="formal"/> is a function type whose parameter positions still contain type
    /// parameters — the shape <see cref="CheckLambda"/> cannot consume until unification runs (#1161).
    /// </summary>
    private static bool IsDeferrableLambdaArgument(Expression argument, SemanticType? formal)
    {
        return UnwrapParenthesized(argument) is LambdaExpression lambda
            && lambda.Parameters.Any(p => p.Type == null)
            && formal is FunctionType formalFunc
            && formalFunc.ParameterTypes.Any(ContainsTypeParameterType);
    }

    /// <summary>
    /// True when at least one deferred lambda's parameter types reference a type parameter that a
    /// non-deferred argument's formal type also references. Without this the deferral buys nothing:
    /// no argument can bind the type parameter, so the lambda must keep its existing path (and its
    /// existing SPY0237 report, #904).
    /// </summary>
    private static bool AnyDeferredLambdaIsBindableByOtherArguments(
        FunctionCall call,
        SemanticType?[] formalByPosition,
        Dictionary<string, SemanticType> formalByKeyword,
        List<int> deferredPositions,
        List<string> deferredKeywords)
    {
        var binderFormals = new List<SemanticType>();
        for (int position = 0; position < formalByPosition.Length; position++)
        {
            if (!deferredPositions.Contains(position) && formalByPosition[position] is { } formal)
                binderFormals.Add(formal);
        }
        foreach (var kwarg in call.KeywordArguments)
        {
            if (!deferredKeywords.Contains(kwarg.Name)
                && formalByKeyword.TryGetValue(kwarg.Name, out var keywordFormal))
            {
                binderFormals.Add(keywordFormal);
            }
        }

        if (binderFormals.Count == 0)
            return false;

        var deferredFormals = deferredPositions.Select(p => formalByPosition[p]!)
            .Concat(deferredKeywords.Select(n => formalByKeyword[n]));
        foreach (var deferredFormal in deferredFormals)
        {
            foreach (var name in CollectTypeParameterNames(((FunctionType)deferredFormal).ParameterTypes))
            {
                if (binderFormals.Any(f => ReferencesTypeParameterNamed(f, name)))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Collects the names of every type parameter referenced anywhere in <paramref name="types"/>.
    /// </summary>
    private static List<string> CollectTypeParameterNames(IEnumerable<SemanticType> types)
    {
        var names = new List<string>();
        foreach (var type in types)
            CollectTypeParameterNames(type, names);
        return names;
    }

    private static void CollectTypeParameterNames(SemanticType type, List<string> names)
    {
        switch (type)
        {
            case TypeParameterType tp:
                if (!names.Contains(tp.Name))
                    names.Add(tp.Name);
                break;
            case ResultType rt:
                CollectTypeParameterNames(rt.OkType, names);
                CollectTypeParameterNames(rt.ErrorType, names);
                break;
            case OptionalType ot:
                CollectTypeParameterNames(ot.UnderlyingType, names);
                break;
            case NullableType nt:
                CollectTypeParameterNames(nt.UnderlyingType, names);
                break;
            case GenericType gt:
                foreach (var arg in gt.TypeArguments)
                    CollectTypeParameterNames(arg, names);
                break;
            case FunctionType ft:
                foreach (var parameterType in ft.ParameterTypes)
                    CollectTypeParameterNames(parameterType, names);
                CollectTypeParameterNames(ft.ReturnType, names);
                break;
            case TupleType tt:
                foreach (var element in tt.ElementTypes)
                    CollectTypeParameterNames(element, names);
                break;
        }
    }

    /// <summary>
    /// Applies <paramref name="substitutions"/> to a deferred lambda's formal function type and
    /// normalizes each parameter position, so a discovery-collapsed <c>object</c> is not forced onto
    /// the lambda parameter (see <see cref="NormalizeExpectedParamType"/>).
    /// </summary>
    private static SemanticType? SubstituteExpectedLambdaType(
        SemanticType formal, Dictionary<string, SemanticType> substitutions)
    {
        if (GenericTypeInferenceService.SubstituteTypeParameters(formal, substitutions) is not FunctionType substituted)
            return null;

        return substituted with
        {
            ParameterTypes = substituted.ParameterTypes.Select(NormalizeExpectedParamType).ToList()
        };
    }

    /// <summary>
    /// Resolves the single unambiguous signature whose formal parameter types drive deferred lambda
    /// checking (#1161), or null when no such signature is in view.
    /// </summary>
    /// <returns>
    /// The signature, and whether its parameter types may also be used as expected types for the
    /// non-deferred arguments (true only for the member-access path, which already does so today).
    /// </returns>
    private (FunctionSymbol? Signature, bool ProvidesExpectedTypes) ResolveDeferredLambdaSignature(
        FunctionCall call, Expression callee, FunctionSymbol? earlyFuncSymbol)
    {
        // Member-access calls already resolve a receiver-substituted signature for exactly this
        // purpose (#889) and already feed its parameter types in as expected types; reuse both.
        if (earlyFuncSymbol != null)
            return (earlyFuncSymbol, true);

        if (callee is not Identifier id)
            return (null, false);

        // A user-defined or imported overload SET is resolved from the argument types later; choosing
        // one of its members here would be a guess.
        if (_symbolTable.LookupFunctionOverloads(id.Name) is { Count: > 1 })
            return (null, false);

        var symbol = _symbolTable.Lookup(id.Name) as FunctionSymbol;
        var builtinOverloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);

        List<FunctionSymbol> candidates;
        if (builtinOverloads is { Count: > 0 } && (symbol == null || builtinOverloads.Contains(symbol)))
            candidates = builtinOverloads.Where(c => AcceptsCallShape(c, call)).ToList();
        else if (symbol != null)
            candidates = new List<FunctionSymbol> { symbol };
        else
            return (null, false);

        if (candidates.Count == 0)
            return (null, false);
        if (candidates.Count > 1 && !OverloadsAgreeOnParameterTypes(candidates, static t => t))
            return (null, false);

        return (candidates.OrderByDescending(c => c.Parameters.Count).First(), false);
    }

    /// <summary>
    /// True when <paramref name="candidate"/> could accept this call's argument shape: the argument
    /// count fits its parameter list and every keyword name matches a parameter. Type-based overload
    /// resolution still runs later; this only narrows the candidate set enough to read formal
    /// parameter types without guessing.
    /// </summary>
    private static bool AcceptsCallShape(FunctionSymbol candidate, FunctionCall call)
    {
        int supplied = call.Arguments.Length + call.KeywordArguments.Length;
        bool hasVariadic = candidate.Parameters.Any(p => p.IsVariadic);
        if (!hasVariadic && supplied > candidate.Parameters.Count)
            return false;

        int required = candidate.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
        if (supplied < required)
            return false;

        foreach (var kwarg in call.KeywordArguments)
        {
            if (!candidate.Parameters.Any(p => p.Name == kwarg.Name))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Try to infer Unknown lambda parameter types from the lambda body.
    /// Handles partial application lowering where the body is a FunctionCall,
    /// BinaryOp, UnaryOp, or ComparisonChain containing placeholder parameters.
    /// </summary>
    private void TryInferLambdaParamTypesFromBody(LambdaExpression lambda, List<SemanticType> paramTypes)
    {
        // Build a map from placeholder param name to its index
        var unknownParams = new Dictionary<string, int>();
        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            if (paramTypes[i] is UnknownType)
                unknownParams[lambda.Parameters[i].Name] = i;
        }

        if (unknownParams.Count == 0)
            return;

        switch (lambda.Body)
        {
            case FunctionCall call:
                InferParamTypesFromCall(call, unknownParams, paramTypes);
                break;
            case BinaryOp binOp:
                InferParamTypesFromBinaryOp(binOp, unknownParams, paramTypes);
                break;
            case UnaryOp unaryOp:
                InferParamTypesFromUnaryOp(unaryOp, unknownParams, paramTypes);
                break;
            case ComparisonChain chain:
                InferParamTypesFromComparison(chain, unknownParams, paramTypes);
                break;
        }
    }

    /// <summary>
    /// Infer placeholder types from a function call body (e.g., add(5, __placeholder_0)).
    /// Looks up the called function's signature and maps placeholder args to param types.
    /// </summary>
    private void InferParamTypesFromCall(FunctionCall call, Dictionary<string, int> unknownParams, List<SemanticType> paramTypes)
    {
        // Resolve the function being called to get its parameter types. Dispatched on the canonical
        // (paren-stripped) callee so a placeholder under `(add)(5, _)` infers the same as `add(5, _)`
        // (#1170).
        List<ParameterSymbol>? funcParams = null;
        var callee = UnwrapParenthesized(call.Function);

        if (callee is Identifier funcId)
        {
            var symbol = _symbolTable.Lookup(funcId.Name);
            if (symbol is FunctionSymbol fs)
                funcParams = fs.Parameters;
        }
        else if (callee is MemberAccess memberAccess)
        {
            // For method calls like obj.method(_, y), resolve the method
            var objType = TryResolveExpressionType(memberAccess.Object);
            if (objType != null)
            {
                var methodSymbol = TryResolveMember(objType, memberAccess.Member);
                if (methodSymbol is FunctionSymbol fs)
                    funcParams = fs.Parameters;
            }
        }

        if (funcParams == null)
            return;

        // Match positional arguments to function parameters
        // Skip 'self' parameter for instance methods
        int paramOffset = 0;
        if (funcParams.Count > 0 && funcParams[0].Name == "self")
            paramOffset = 1;

        for (int argIdx = 0; argIdx < call.Arguments.Length; argIdx++)
        {
            int funcParamIdx = argIdx + paramOffset;
            if (funcParamIdx >= funcParams.Count)
                break;

            if (call.Arguments[argIdx] is Identifier id && unknownParams.TryGetValue(id.Name, out int placeholderIdx))
            {
                var expectedType = funcParams[funcParamIdx].Type;
                if (expectedType is not UnknownType)
                    paramTypes[placeholderIdx] = expectedType;
            }
        }
    }

    /// <summary>
    /// Infer placeholder types from a binary operation body (e.g., __placeholder_0 * 2).
    /// Uses the non-placeholder operand's type to infer the placeholder type.
    /// For logical operators (and/or), recursively processes sub-expressions.
    /// </summary>
    private void InferParamTypesFromBinaryOp(BinaryOp binOp, Dictionary<string, int> unknownParams, List<SemanticType> paramTypes)
    {
        // For logical operators (and/or), the operands may be ComparisonChains or
        // nested BinaryOps rather than simple identifiers. Recurse into each side.
        if (binOp.Operator is BinaryOperator.And or BinaryOperator.Or)
        {
            InferParamTypesFromSubExpression(binOp.Left, unknownParams, paramTypes);
            InferParamTypesFromSubExpression(binOp.Right, unknownParams, paramTypes);
            return;
        }

        var leftIsPlaceholder = binOp.Left is Identifier leftId && unknownParams.ContainsKey(leftId.Name);
        var rightIsPlaceholder = binOp.Right is Identifier rightId && unknownParams.ContainsKey(rightId.Name);

        if (leftIsPlaceholder && !rightIsPlaceholder)
        {
            // (_ op expr) — infer from expr's type
            var rightType = TryResolveExpressionType(binOp.Right);
            if (rightType != null && rightType is not UnknownType)
            {
                var id = (Identifier)binOp.Left;
                paramTypes[unknownParams[id.Name]] = rightType;
            }
        }
        else if (!leftIsPlaceholder && rightIsPlaceholder)
        {
            // (expr op _) — infer from expr's type
            var leftType = TryResolveExpressionType(binOp.Left);
            if (leftType != null && leftType is not UnknownType)
            {
                var id = (Identifier)binOp.Right;
                paramTypes[unknownParams[id.Name]] = leftType;
            }
        }
        else if (leftIsPlaceholder && rightIsPlaceholder)
        {
            // (_ op _) — both placeholders, can't infer without more context
            // Leave as Unknown; will need explicit type annotation
        }
    }

    /// <summary>
    /// Recursively infer parameter types from a sub-expression within a logical
    /// and/or chain. Dispatches to the appropriate handler based on expression type.
    /// </summary>
    private void InferParamTypesFromSubExpression(Expression expr, Dictionary<string, int> unknownParams, List<SemanticType> paramTypes)
    {
        switch (expr)
        {
            case ComparisonChain chain:
                InferParamTypesFromComparison(chain, unknownParams, paramTypes);
                break;
            case BinaryOp nestedBinOp:
                InferParamTypesFromBinaryOp(nestedBinOp, unknownParams, paramTypes);
                break;
            case UnaryOp unaryOp:
                InferParamTypesFromUnaryOp(unaryOp, unknownParams, paramTypes);
                break;
        }
    }

    /// <summary>
    /// Infer placeholder type from a unary operation body (e.g., -__placeholder_0).
    /// </summary>
    private void InferParamTypesFromUnaryOp(UnaryOp unaryOp, Dictionary<string, int> unknownParams, List<SemanticType> paramTypes)
    {
        if (unaryOp.Operand is Identifier id && unknownParams.TryGetValue(id.Name, out int placeholderIdx))
        {
            // For numeric unary operators (-, +, ~), default to int
            // For 'not', default to bool
            SemanticType inferredType = unaryOp.Operator switch
            {
                UnaryOperator.Not => BuiltinType.Bool,
                _ => BuiltinType.Int // -, +, ~ default to int
            };
            paramTypes[placeholderIdx] = inferredType;
        }
    }

    /// <summary>
    /// Infer placeholder types from a comparison chain body (e.g., __placeholder_0 > 0).
    /// </summary>
    private void InferParamTypesFromComparison(ComparisonChain chain, Dictionary<string, int> unknownParams, List<SemanticType> paramTypes)
    {
        // For each operand that is a placeholder, try to infer from an adjacent non-placeholder operand
        for (int i = 0; i < chain.Operands.Length; i++)
        {
            if (chain.Operands[i] is Identifier id && unknownParams.TryGetValue(id.Name, out int placeholderIdx))
            {
                // Check adjacent operands for type info
                SemanticType? inferredType = null;
                if (i > 0)
                    inferredType = TryResolveExpressionType(chain.Operands[i - 1]);
                if ((inferredType == null || inferredType is UnknownType) && i + 1 < chain.Operands.Length)
                    inferredType = TryResolveExpressionType(chain.Operands[i + 1]);

                if (inferredType != null && inferredType is not UnknownType)
                    paramTypes[placeholderIdx] = inferredType;
            }
        }
    }

    /// <summary>
    /// Try to resolve the type of an expression without entering a full check pass.
    /// Used for pre-inference of lambda parameter types from body expressions.
    /// </summary>
    private SemanticType? TryResolveExpressionType(Expression expr)
    {
        return expr switch
        {
            IntegerLiteral il => Shared.IntegerLiteralClassifier.Classify(il.Value, il.Suffix).Type,
            FloatLiteral fl => fl.Suffix?.ToUpperInvariant() == "F" ? BuiltinType.Float32 : BuiltinType.Double,
            StringLiteral => BuiltinType.Str,
            BooleanLiteral => BuiltinType.Bool,
            Identifier id => (_symbolTable.Lookup(id.Name) as VariableSymbol)?.Type,
            _ => null
        };
    }

    /// <summary>
    /// Try to resolve a member symbol on a type.
    /// Used for pre-inference of method call parameter types.
    /// </summary>
    private Symbol? TryResolveMember(SemanticType objType, string memberName)
    {
        TypeSymbol? typeSymbol = objType switch
        {
            UserDefinedType udt => udt.Symbol,
            _ => null
        };

        if (typeSymbol == null)
            return null;

        return typeSymbol.Methods.FirstOrDefault(m => m.Name == memberName);
    }

}
