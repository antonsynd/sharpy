using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Statement checking (assignments, control flow, try/catch)
/// </summary>
internal partial class TypeChecker
{
    private void CheckAssignment(Assignment assignment)
    {
        // First, validate that the assignment target is a valid assignable expression
        // Valid targets: Identifier, MemberAccess (attribute), IndexAccess, TupleLiteral (unpacking)
        // Invalid targets: FunctionCall, Literal, BinaryExpression, etc.
        if (!IsValidAssignmentTarget(assignment.Target))
        {
            AddError($"Cannot assign to {GetAssignmentTargetDescription(assignment.Target)}",
                assignment.Target.LineStart, assignment.Target.ColumnStart, code: DiagnosticCodes.Semantic.InvalidAssignmentTarget,
                span: assignment.Span);
            return;
        }

        // Validate that 'self' cannot be reassigned
        if (assignment.Target is Identifier selfId && selfId.Name == PythonNames.Self)
        {
            AddError("Cannot reassign 'self'",
                assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.InvalidAssignmentTarget,
                span: assignment.Span);
            return;
        }

        // Validate that 'in' parameters cannot be reassigned
        if (assignment.Target is Identifier inParamId)
        {
            var sym = _symbolTable.Lookup(inParamId.Name, searchParents: true);
            if (sym is VariableSymbol vs && vs.IsParameter && vs.ParameterModifier == Parser.Ast.ParameterModifier.In)
            {
                AddError($"Cannot reassign 'in' parameter '{inParamId.Name}'",
                    assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.InParameterReassignment,
                    span: assignment.Span);
                return;
            }
        }

        // Handle tuple unpacking: x, y = expr  or  first, *rest = items
        if (assignment.Operator == AssignmentOperator.Assign && assignment.Target is TupleLiteral targetTuple)
        {
            var tupleValueType = CheckExpression(assignment.Value);

            // Check for star expression (rest pattern)
            bool hasStar = targetTuple.Elements.Any(e => e is StarExpression);

            if (hasStar)
            {
                CheckStarUnpacking(targetTuple, tupleValueType, assignment);
                return;
            }

            // Value must be a tuple type
            if (tupleValueType is not TupleType tupleType)
            {
                AddError($"Cannot unpack non-tuple type '{tupleValueType.GetDisplayName()}' into tuple",
                    assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                    span: assignment.Span);
                return;
            }

            // Check element count matches
            if (targetTuple.Elements.Length != tupleType.ElementTypes.Count)
            {
                AddError($"Cannot unpack {tupleType.ElementTypes.Count} values into {targetTuple.Elements.Length} variables",
                    assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                    span: assignment.Span);
                return;
            }

            // Type-check each unpacking element (supports nested tuple targets)
            CheckTupleUnpackingElements(targetTuple.Elements, tupleType.ElementTypes);

            return;
        }

        // Check if this is a simple assignment to an identifier (type inference and redefinition case)
        if (assignment.Operator == AssignmentOperator.Assign && assignment.Target is Identifier targetId)
        {
            // Check current scope first
            var existingSymbol = _symbolTable.Lookup(targetId.Name, searchParents: false);

            // A hoisted def/class with this name makes the assignment a duplicate definition,
            // not a rebinding — Scope.Define would throw on the collision.
            if (TryReportNonVariableRedefinition(targetId.Name, assignment.LineStart, assignment.ColumnStart, assignment.Span))
                return;

            // Check if trying to reassign a constant in current scope
            if (existingSymbol is VariableSymbol varSymbol && varSymbol.IsConstant)
            {
                AddError($"Cannot reassign constant variable '{targetId.Name}'",
                    assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.InvalidAssignmentTarget,
                    span: assignment.Span);
                return;
            }

            // Also check parent scopes for consts (can't reassign outer scope const)
            var parentSymbol = _symbolTable.Lookup(targetId.Name, searchParents: true);
            if (parentSymbol is VariableSymbol parentVar && parentVar.IsConstant)
            {
                AddError($"Cannot reassign constant variable '{targetId.Name}'",
                    assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.InvalidAssignmentTarget,
                    span: assignment.Span);
                return;
            }

            // In Sharpy, simple assignments (x = value) create new variable versions
            // This enables Python-like behavior where variables can be reassigned to different types
            // Set expected type for constructor inference if the variable was previously declared
            var previousExpectedType2 = _expectedType;
            if (existingSymbol is VariableSymbol existingVarSym)
            {
                var existingType = GetVariableType(existingVarSym);
                _expectedType = existingType is UnknownType ? null : existingType;
            }
            else if (parentSymbol is VariableSymbol parentVarSym)
            {
                var parentType = GetVariableType(parentVarSym);
                _expectedType = parentType is UnknownType ? null : parentType;
            }
            SemanticType inferredType;
            using (ScopedValue.Push(ref _currentBindingValue, assignment.Value))
                inferredType = CheckExpression(assignment.Value);
            _expectedType = previousExpectedType2;
            inferredType = CheckLambdaBindingInferable(assignment.Value, inferredType);

            // Create a new variable symbol with the inferred type (or redefine existing)
            var newSymbol = new VariableSymbol
            {
                Name = targetId.Name,
                Kind = SymbolKind.Variable,
                Type = inferredType,
                IsConstant = false,
                DeclarationLine = assignment.LineStart,
                DeclarationColumn = assignment.ColumnStart,
                NameDeclarationLine = targetId.LineStart,
                NameDeclarationColumn = targetId.ColumnStart,
                AccessLevel = AccessLevel.Public,
                DeclarationSpan = assignment.Span,
                DeclaringFilePath = _currentFilePath
            };
            _symbolTable.Define(newSymbol);
            SemanticBinding.SetVariableType(newSymbol, inferredType);
            _semanticInfo.SetIdentifierSymbol(targetId, newSymbol);

            // Cache the expression type for the identifier
            _semanticInfo.SetExpressionType(targetId, inferredType);
            if (inferredType is UnknownType)
                MarkExpressionAsErrorRecovery(targetId);
            return;
        }

        // Check if the assignment target is an event member access
        if (assignment.Target is MemberAccess eventMa)
        {
            var eventSymbol = TryResolveEventAccess(eventMa);
            if (eventSymbol != null)
            {
                if (assignment.Operator == AssignmentOperator.Assign)
                {
                    // Direct assignment to events is not allowed from outside
                    AddError(
                        $"Cannot assign directly to event '{eventMa.Member}'. Use '+=' to subscribe or '-=' to unsubscribe.",
                        assignment.LineStart, assignment.ColumnStart,
                        DiagnosticCodes.Semantic.DirectEventAssignment,
                        assignment.Span);
                    return;
                }

                if (assignment.Operator == AssignmentOperator.PlusAssign
                    || assignment.Operator == AssignmentOperator.MinusAssign)
                {
                    // Mark as event access for codegen
                    _semanticInfo.MarkAsEventAccess(assignment.Target);

                    // Type-check the handler value
                    var handlerType = CheckExpression(assignment.Value);

                    // Verify handler type matches event type (if event type is resolved)
                    // Use IsAssignable (not IsAssignableTo) to handle FunctionType-to-delegate
                    // structural compatibility (e.g., a function with matching signature can
                    // subscribe to an event whose type is a named delegate).
                    if (eventSymbol.Type is not UnknownType && handlerType is not UnknownType)
                    {
                        if (!IsAssignable(handlerType, eventSymbol.Type))
                        {
                            AddError(
                                $"Handler type '{handlerType.GetDisplayName()}' is not compatible with event type '{eventSymbol.Type.GetDisplayName()}'",
                                assignment.Value.LineStart, assignment.Value.ColumnStart,
                                DiagnosticCodes.Semantic.EventHandlerTypeMismatch,
                                assignment.Value.Span);
                        }
                    }
                    return;
                }

                // Other augmented operators are not valid on events
                AddError(
                    $"Events only support '+=' and '-=' operators",
                    assignment.LineStart, assignment.ColumnStart,
                    DiagnosticCodes.Semantic.EventUnsupportedOperator,
                    assignment.Span);
                return;
            }
        }

        // Check target and value types
        var targetType = CheckExpression(assignment.Target);
        // For assignments to self.field, use the DECLARED field type rather than the
        // narrowed type. When a field like `x: int?` is narrowed to `int` inside
        // `if x is not None:`, we still need `Some(v)` to resolve as `int?` and the
        // assignment `self.x = Some(v)` to be valid (assigning int? to the int? field).
        var assignmentTargetType = targetType;
        if (assignment.Target is MemberAccess { Object: Identifier selfAccess } targetMa
            && selfAccess.Name == PythonNames.Self
            && _currentClass != null)
        {
            var fieldSymbol = _currentClass.Fields
                .FirstOrDefault(f => f.Name == targetMa.Member);
            if (fieldSymbol != null)
            {
                var declaredType = fieldSymbol.Type;
                if (declaredType is OptionalType || declaredType is ResultType || declaredType is NullableType)
                    assignmentTargetType = declaredType;
            }
        }
        // Set expected type for constructor inference (Some/None()/Ok/Err)
        var previousExpectedType = _expectedType;
        _expectedType = assignmentTargetType is UnknownType ? null : assignmentTargetType;
        SemanticType valueType;
        using (ScopedValue.Push(ref _currentBindingValue, assignment.Value))
            valueType = CheckExpression(assignment.Value);
        _expectedType = previousExpectedType;

        // Handle augmented assignment operators (+=, -=, *=, /=, //=, %=, **=, &=, |=, ^=, <<=, >>=)
        if (assignment.Operator != AssignmentOperator.Assign)
        {
            // Check if trying to use augmented assignment on a constant
            if (assignment.Target is Identifier augTargetId)
            {
                var symbol = _symbolTable.Lookup(augTargetId.Name, searchParents: true);
                if (symbol is VariableSymbol varSym && varSym.IsConstant)
                {
                    AddError($"Cannot use augmented assignment on constant variable '{augTargetId.Name}'",
                        assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.InvalidAssignmentTarget,
                        span: assignment.Span);
                    return;
                }
            }

            // For augmented assignments, use TypeInferenceService (errors reported by validator in pipeline)
            // Augmented assignment desugars to the regular binary operator (e.g., += uses __add__)
            var resultType = _typeInference.InferAugmentedAssignmentType(
                assignment.Operator,
                targetType,
                valueType);

            // Verify result type is assignable to target type (if inference succeeded)
            if (resultType != null && !resultType.IsAssignableTo(targetType))
            {
                AddError(
                    $"Result type '{resultType.GetDisplayName()}' of augmented assignment is not assignable to target type '{targetType.GetDisplayName()}'",
                    assignment.LineStart,
                    assignment.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: assignment.Span);
            }
            return;
        }

        // Otherwise, check as a regular simple assignment
        // Use assignmentTargetType (declared type) for fields where narrowing may differ
        if (!IsAssignable(valueType, assignmentTargetType))
        {
            if (valueType is VoidType && assignmentTargetType is not NullableType and not OptionalType)
            {
                AddError($"Cannot assign 'None' to non-nullable type '{assignmentTargetType.GetDisplayName()}'",
                    assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.NullabilityViolation,
                    span: assignment.Value.Span);
            }
            else
            {
                AddError($"Cannot assign type '{valueType.GetDisplayName()}' to '{assignmentTargetType.GetDisplayName()}'",
                    assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: assignment.Span);
            }
        }
    }

    /// <summary>
    /// Refuses a lambda bound to a name when inference could not fill in its unannotated parameter
    /// types and the binding supplied no expected type to take them from (#1212). Returns the type
    /// to bind: <paramref name="inferred"/> when the binding is fine, or
    /// <see cref="SemanticType.Unknown"/> after reporting, so downstream reads of the target do not
    /// cascade secondary errors.
    ///
    /// <para>Without this the emitter falls back to <c>var</c> — the recorded
    /// <see cref="FunctionType"/> is unusable because it contains <see cref="UnknownType"/> — and
    /// C# rejects the result with CS8917 ("the delegate type could not be inferred") behind
    /// SPY0908. Per #1146 an accepted shape must either compile or be refused at semantic time;
    /// there is no principled type to infer here (<c>len(s)</c> admits every sized type), so this
    /// refuses and names the remedy.</para>
    ///
    /// <para>Its reach is deliberately narrow. Both binding seams set <c>_expectedType</c> from the
    /// target before checking the value, so every binding that <em>can</em> type the lambda already
    /// does; <c>TryInferLambdaParamTypesFromBody</c> has already resolved the body shapes it can
    /// (<c>lambda x: x * 2</c>, <c>lambda x: f(x)</c>); a parameter the user annotated is excluded
    /// even when the annotation failed to resolve, since that error is already reported; a
    /// return-type-only unresolution is not this diagnostic, so an erroring body is left to report
    /// itself; and lambdas in argument position never reach here at all.</para>
    /// </summary>
    private SemanticType CheckLambdaBindingInferable(Expression? value, SemanticType inferred)
    {
        if (value == null
            || UnwrapParenthesized(value) is not LambdaExpression lambda
            || inferred is not FunctionType functionType)
        {
            return inferred;
        }

        var unresolved = new List<Parameter>();
        for (int i = 0; i < lambda.Parameters.Length && i < functionType.ParameterTypes.Count; i++)
        {
            if (lambda.Parameters[i].Type == null && functionType.ParameterTypes[i] is UnknownType)
                unresolved.Add(lambda.Parameters[i]);
        }

        if (unresolved.Count == 0)
            return inferred;

        // Operator sections and partial application lower to a lambda whose parameters the user
        // never wrote and cannot annotate, so the remedy has to be a different one.
        bool isSynthesized = unresolved.Any(p =>
            p.Name.StartsWith(SynthesizedPlaceholderPrefix, StringComparison.Ordinal));

        var message = isSynthesized
            ? "Cannot infer the parameter types of this operator section. Annotate the target with a "
              + "function type (for example 'f: (int, int) -> int = (_ + _)'), or write a lambda "
              + "with annotated parameters instead."
            : $"Cannot infer {DescribeUnresolvedParameters(unresolved)} of this lambda. Annotate "
              + $"{(unresolved.Count == 1 ? "it" : "them")} (for example 'lambda "
              + $"{unresolved[0].Name}: str: ...'), or annotate the target with a function type.";

        AddError(message, lambda.LineStart, lambda.ColumnStart,
            code: DiagnosticCodes.Semantic.UnresolvedLambdaParameterType,
            span: lambda.Span);

        return SemanticType.Unknown;
    }

    /// <summary>Prefix the parser gives operator-section and partial-application parameters.</summary>
    private const string SynthesizedPlaceholderPrefix = "__placeholder_";

    private static string DescribeUnresolvedParameters(List<Parameter> unresolved)
    {
        var names = string.Join(", ", unresolved.Select(p => $"'{p.Name}'"));
        return unresolved.Count == 1
            ? $"the type of parameter {names}"
            : $"the types of parameters {names}";
    }

    private void CheckVariableDeclaration(VariableDeclaration varDecl)
    {
        var declaredType = _typeResolver.ResolveTypeAnnotation(varDecl.Type);

        if (varDecl.InitialValue != null)
        {
            // Set expected type for constructor inference (Some/None()/Ok/Err)
            var previousExpectedType = _expectedType;
            _expectedType = declaredType is UnknownType ? null : declaredType;
            SemanticType initType;
            using (ScopedValue.Push(ref _currentBindingValue, varDecl.InitialValue))
                initType = CheckExpression(varDecl.InitialValue);
            _expectedType = previousExpectedType;

            // Only on the 'auto' path: a declared annotation that cannot type the lambda already
            // reports its own mismatch below, and telling the user to annotate a parameter on top
            // of that would be noise (#1212).
            if (declaredType is UnknownType)
                initType = CheckLambdaBindingInferable(varDecl.InitialValue, initType);

            // Handle type inference for 'auto'
            if (declaredType is UnknownType)
            {
                declaredType = initType;
                if (varDecl.Type != null)
                {
                    _semanticInfo.SetTypeAnnotation(varDecl.Type, initType);
                }
            }
            else if (!IsAssignable(initType, declaredType))
            {
                // Allow implicit narrowing of double literals to float32 (matches C# behavior)
                if (declaredType is BuiltinType { Name: "float32" } && initType is BuiltinType { Name: "float" }
                    && varDecl.InitialValue is FloatLiteral)
                {
                    // Literal narrowing is safe — no runtime data loss risk
                }
                else if (initType is VoidType && declaredType is not NullableType and not OptionalType)
                {
                    AddError($"Cannot assign 'None' to non-nullable type '{declaredType.GetDisplayName()}'",
                        varDecl.LineStart, varDecl.ColumnStart, code: DiagnosticCodes.Semantic.NullabilityViolation,
                        span: varDecl.InitialValue!.Span);
                }
                else
                {
                    AddError($"Cannot assign type '{initType.GetDisplayName()}' to variable of type '{declaredType.GetDisplayName()}'",
                        varDecl.LineStart, varDecl.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: varDecl.Span);
                }
            }
        }
        else if (declaredType is UnknownType)
        {
            AddError($"Variable '{varDecl.Name}' declared with 'auto' must have an initializer",
                varDecl.LineStart, varDecl.ColumnStart, code: DiagnosticCodes.Semantic.InvalidAutoVariable,
                span: varDecl.Span);
        }

        // Check if symbol already exists in current scope
        var existingSymbol = _symbolTable.Lookup(varDecl.Name, searchParents: false);

        // A hoisted def/class with this name makes the declaration a duplicate definition —
        // Scope.Define would throw on the collision.
        if (TryReportNonVariableRedefinition(varDecl.Name, varDecl.LineStart, varDecl.ColumnStart, varDecl.Span))
            return;

        // For constants:
        // - Module-level consts are already created by NameResolver, so we update their type
        // - Function-level consts are NOT created by NameResolver, so we need to create them
        if (varDecl.IsConst)
        {
            if (existingSymbol is VariableSymbol existingConst)
            {
                // Module-level const was already created by NameResolver
                // Update its type now that we've resolved it
                SemanticBinding.SetVariableType(existingConst, declaredType);
                _semanticInfo.SetDeclarationSymbol(varDecl, existingConst);
                return;
            }

            // Function-level const - we need to create it
            var constSymbol = new VariableSymbol
            {
                Name = varDecl.Name,
                Kind = SymbolKind.Variable,
                Type = declaredType,
                IsConstant = true,
                DeclarationLine = varDecl.LineStart,
                DeclarationColumn = varDecl.ColumnStart,
                NameDeclarationLine = varDecl.NameLineStart,
                NameDeclarationColumn = varDecl.NameColumnStart,
                DeclarationSpan = varDecl.Span,
                DeclaringFilePath = _currentFilePath
            };
            _symbolTable.Define(constSymbol);
            SemanticBinding.SetVariableType(constSymbol, declaredType);
            _semanticInfo.SetDeclarationSymbol(varDecl, constSymbol);
            return;
        }

        if (existingSymbol is VariableSymbol existingVar)
        {
            // In Sharpy, variables can be redefined in the same scope (Python-like behavior)
            // However, constants cannot be redefined
            if (existingVar.IsConstant)
            {
                AddError($"Cannot redefine constant variable '{varDecl.Name}'",
                    varDecl.LineStart, varDecl.ColumnStart, code: DiagnosticCodes.Semantic.InvalidAssignmentTarget,
                    span: varDecl.Span);
                return;
            }

            // For non-const variables, allow redefinition with new type
            // This enables Python-like behavior where variables can be reassigned to different types
            // The Scope.Define will replace the existing symbol
        }

        // Create new variable symbol (or redefine existing non-const variable)
        var newSymbol = new VariableSymbol
        {
            Name = varDecl.Name,
            Kind = SymbolKind.Variable,
            Type = declaredType,
            IsConstant = false,  // Non-const variable
            DeclarationLine = varDecl.LineStart,
            DeclarationColumn = varDecl.ColumnStart,
            NameDeclarationLine = varDecl.NameLineStart,
            NameDeclarationColumn = varDecl.NameColumnStart,
            DeclarationSpan = varDecl.Span,
            DeclaringFilePath = _currentFilePath
        };
        _symbolTable.Define(newSymbol);
        SemanticBinding.SetVariableType(newSymbol, declaredType);
        _semanticInfo.SetDeclarationSymbol(varDecl, newSymbol);

        // A local declared with an explicit list[T] annotation emits as a concrete Sharpy.List<T>
        // (the annotation forces that C# type), so it is eligible for the non-negative index fast
        // path (#1052). Inferred locals go through `var` and may bind a CLR array, so they default
        // to Unknown (not recorded here).
        if (varDecl.Type != null && declaredType is GenericType { Name: BuiltinNames.List })
            _listBackingKinds[newSymbol] = ListBackingKind.SharpyList;
    }

    private void CheckReturn(ReturnStatement returnStmt)
    {
        if (_inExceptStarBlock)
        {
            AddError("'return' is not allowed inside 'except*' handler",
                returnStmt.LineStart, returnStmt.ColumnStart,
                code: DiagnosticCodes.Semantic.ReturnInExceptStar,
                span: returnStmt.Span);
            return;
        }

        if (_currentFunctionReturnType == null)
        {
            AddError("Return statement outside of function",
                returnStmt.LineStart, returnStmt.ColumnStart, code: DiagnosticCodes.Semantic.ReturnOutsideFunction,
                span: returnStmt.Span);
            return;
        }

        if (returnStmt.Value != null)
        {
            // Set expected type for constructor inference (Some/None()/Ok/Err)
            var previousExpectedType = _expectedType;
            _expectedType = _currentFunctionReturnType;
            var returnType = CheckExpression(returnStmt.Value);
            _expectedType = previousExpectedType;
            if (!IsAssignable(returnType, _currentFunctionReturnType))
            {
                AddError($"Cannot return type '{returnType.GetDisplayName()}' from function expecting '{_currentFunctionReturnType.GetDisplayName()}'",
                    returnStmt.LineStart, returnStmt.ColumnStart, code: DiagnosticCodes.Semantic.MissingReturnValue,
                    span: returnStmt.Span);
            }
        }
        else if (_currentFunctionReturnType != SemanticType.Void && !_currentFunctionIsGenerator)
        {
            // Bare return in a generator is valid (becomes yield break in C#)
            AddError($"Function expects return type '{_currentFunctionReturnType.GetDisplayName()}' but got no return value",
                returnStmt.LineStart, returnStmt.ColumnStart, code: DiagnosticCodes.Semantic.MissingReturnValue,
                span: returnStmt.Span);
        }
    }

    private void CheckYield(YieldStatement yieldStmt)
    {
        // yield is only valid inside a function
        if (_currentFunctionReturnType == null)
        {
            AddError("'yield' cannot be used outside of a function",
                yieldStmt.LineStart, yieldStmt.ColumnStart,
                code: DiagnosticCodes.Semantic.YieldOutsideFunction,
                span: yieldStmt.Span);
            return;
        }

        if (yieldStmt.IsFrom)
        {
            // yield from expr: the value must be iterable, element type must match
            var iterableType = CheckExpression(yieldStmt.Value);
            var elementType = _typeInference.InferIterableElementType(iterableType);

            if (elementType == null && iterableType is not UnknownType)
            {
                AddError(
                    $"'yield from' requires an iterable, but got '{iterableType.GetDisplayName()}'",
                    yieldStmt.Value.LineStart, yieldStmt.Value.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: yieldStmt.Value.Span);
                return;
            }

            if (elementType != null && _currentFunctionReturnType != SemanticType.Void
                && _currentFunctionReturnType is not UnknownType)
            {
                // If there's a return type annotation, verify the element type matches
                if (!IsAssignable(elementType, _currentFunctionReturnType))
                {
                    AddError(
                        $"'yield from' element type '{elementType.GetDisplayName()}' is not assignable to declared return type '{_currentFunctionReturnType.GetDisplayName()}'",
                        yieldStmt.LineStart, yieldStmt.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: yieldStmt.Span);
                }
            }
        }
        else
        {
            // yield expr: type-check the value
            var valueType = CheckExpression(yieldStmt.Value);

            if (_currentFunctionReturnType != SemanticType.Void
                && _currentFunctionReturnType is not UnknownType)
            {
                // If there's a return type annotation, verify the yielded type matches
                if (!IsAssignable(valueType, _currentFunctionReturnType))
                {
                    AddError(
                        $"Yielded type '{valueType.GetDisplayName()}' is not assignable to declared return type '{_currentFunctionReturnType.GetDisplayName()}'",
                        yieldStmt.LineStart, yieldStmt.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: yieldStmt.Span);
                }
            }
        }
    }

    private void CheckIf(IfStatement ifStmt)
    {
        // Resolve reads in the condition against the facts in effect at the branch point (#1042),
        // so a narrowed value from the enclosing flow is visible in a nested condition.
        _currentFacts = _narrowingFlow?.FactsBeforeBranch(ifStmt.Test) ?? _currentFacts;

        var condType = CheckExpression(ifStmt.Test);
        if (!IsTruthTestable(condType))
        {
            AddError($"If condition must be boolean, got '{condType.GetDisplayName()}'",
                ifStmt.LineStart, ifStmt.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                span: ifStmt.Test.Span);
        }

        // Check then branch. Narrowing inside the body is driven by the CFG facts each statement
        // resolves (#1042); only symbol scoping and control-flow depth are managed here.
        _symbolTable.EnterScope("if-then");
        _controlFlowDepth++;
        foreach (var stmt in ifStmt.ThenBody)
            CheckStatement(stmt);
        _controlFlowDepth--;
        _symbolTable.ExitScope();

        // Check elif clauses
        foreach (var elif in ifStmt.ElifClauses)
        {
            _currentFacts = _narrowingFlow?.FactsBeforeBranch(elif.Test) ?? _currentFacts;

            var elifCondType = CheckExpression(elif.Test);
            if (!IsTruthTestable(elifCondType))
            {
                AddError($"Elif condition must be boolean, got '{elifCondType.GetDisplayName()}'",
                    elif.LineStart, elif.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: elif.Test.Span);
            }

            _symbolTable.EnterScope("elif");
            _controlFlowDepth++;
            foreach (var stmt in elif.Body)
                CheckStatement(stmt);
            _controlFlowDepth--;
            _symbolTable.ExitScope();
        }

        // Check else branch (only if there are statements)
        if (ifStmt.ElseBody.Length > 0)
        {
            _symbolTable.EnterScope("if-else");
            _controlFlowDepth++;
            foreach (var stmt in ifStmt.ElseBody)
                CheckStatement(stmt);
            _controlFlowDepth--;
            _symbolTable.ExitScope();
        }

        // #817 (early-exit narrowing of statements following the if) is handled natively by the CFG
        // dataflow: when the then-branch exits, the else-edge facts flow to the post-if statements,
        // and read-site resolution materializes the accessor per node (#1081).
    }

    private void CheckWhile(WhileStatement whileStmt)
    {
        _currentFacts = _narrowingFlow?.FactsBeforeBranch(whileStmt.Test) ?? _currentFacts;

        var condType = CheckExpression(whileStmt.Test);
        if (!IsTruthTestable(condType))
        {
            AddError($"While condition must be boolean, got '{condType.GetDisplayName()}'",
                whileStmt.LineStart, whileStmt.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                span: whileStmt.Test.Span);
        }

        // Body narrowing is applied via CFG facts (#1042); read sites materialize the accessor (#1081).
        _symbolTable.EnterScope("while-body");
        _controlFlowDepth++;
        foreach (var stmt in whileStmt.Body)
            CheckStatement(stmt);
        _controlFlowDepth--;
        _symbolTable.ExitScope();
    }

    private void CheckFor(ForStatement forStmt)
    {
        if (forStmt.IsAsync && !_currentFunctionIsAsync)
        {
            AddError("'async for' can only be used inside 'async def' functions",
                forStmt.LineStart, forStmt.ColumnStart,
                code: DiagnosticCodes.Semantic.AwaitOutsideAsync, span: forStmt.Span);
        }

        // The iterator is the for-loop's branch condition in the CFG; resolve reads in it against the
        // facts at that point so an enclosing narrowing is visible (#1042).
        _currentFacts = _narrowingFlow?.FactsBeforeBranch(forStmt.Iterator) ?? _currentFacts;

        SemanticType iterType;
        using (ScopedValue.Push(ref _currentIterationSource, forStmt.Iterator))
            iterType = CheckExpression(forStmt.Iterator);

        // Enum type used as iterable: `for c in Color:` — CheckIdentifier returns Unknown
        // for TypeSymbol references, so resolve enum types explicitly here.
        if (iterType is UnknownType && forStmt.Iterator is Identifier enumId)
        {
            var sym = _symbolTable.Lookup(enumId.Name);
            if (sym is TypeSymbol { TypeKind: TypeKind.Enum } enumTypeSym)
            {
                iterType = new UserDefinedType { Name = enumTypeSym.Name, Symbol = enumTypeSym };
                _semanticInfo.SetExpressionType(forStmt.Iterator, iterType);
            }
        }

        // Infer element type from the iterator (errors reported by validator in pipeline)
        var elementType = _typeInference.InferIterableElementType(iterType) ?? SemanticType.Unknown;

        // Enter scope for for-body block FIRST
        // This ensures loop variables are scoped to the loop
        _symbolTable.EnterScope("for-body");

        // Handle tuple unpacking: for x, y in items
        if (forStmt.Target is TupleLiteral targetTuple)
        {
            // Element type must be a tuple type
            if (elementType is not TupleType tupleType)
            {
                AddError($"Cannot unpack non-tuple type '{elementType.GetDisplayName()}' in for loop",
                    forStmt.LineStart, forStmt.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                    span: forStmt.Target.Span);
            }
            else
            {
                // Check element count matches
                if (targetTuple.Elements.Length != tupleType.ElementTypes.Count)
                {
                    AddError($"Cannot unpack {tupleType.ElementTypes.Count} values into {targetTuple.Elements.Length} variables in for loop",
                        forStmt.LineStart, forStmt.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                        span: forStmt.Target.Span);
                }
                else
                {
                    // Define loop variables with inferred types INSIDE the for-body scope
                    // (supports nested tuple targets like (x, y), name)
                    DefineForLoopTupleTargets(targetTuple.Elements, tupleType.ElementTypes);
                }
            }

            _semanticInfo.SetExpressionType(forStmt.Target, elementType);
            if (elementType is UnknownType)
                MarkExpressionAsErrorRecovery(forStmt.Target);
        }
        // Add loop variable to scope
        // The target is typically an Identifier or TupleExpression
        Symbol? inductionVarToUnmark = null;
        if (forStmt.Target is Identifier id)
        {
            // Infer the type of the loop variable from the iterator
            var loopVarSymbol = new VariableSymbol
            {
                Name = id.Name,
                Kind = SymbolKind.Variable,
                Type = elementType,
                AccessLevel = AccessLevel.Public,
                DeclarationLine = id.LineStart,
                DeclarationColumn = id.ColumnStart,
                NameDeclarationLine = id.LineStart,
                NameDeclarationColumn = id.ColumnStart,
                DeclarationSpan = id.Span,
                DeclaringFilePath = _currentFilePath
            };

            // Check if already defined in this scope
            if (_symbolTable.Lookup(id.Name, searchParents: false) == null)
            {
                _symbolTable.Define(loopVarSymbol);
                SemanticBinding.SetVariableType(loopVarSymbol, elementType);
                _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);

                // Mark the induction variable as provably non-negative for the body's duration when
                // it iterates a non-negative range(...) and is never reassigned in the body (#1052).
                if (RangeYieldsNonNegativeInts(forStmt.Iterator)
                    && !IsNameReassignedIn(id.Name, forStmt.Body))
                {
                    _nonNegativeInductionVars.Add(loopVarSymbol);
                    inductionVarToUnmark = loopVarSymbol;
                }
            }

            _semanticInfo.SetExpressionType(forStmt.Target, elementType);
            if (elementType is UnknownType)
                MarkExpressionAsErrorRecovery(forStmt.Target);
        }

        // Check loop body statements
        _controlFlowDepth++;
        foreach (var stmt in forStmt.Body)
            CheckStatement(stmt);
        _controlFlowDepth--;

        if (inductionVarToUnmark != null)
            _nonNegativeInductionVars.Remove(inductionVarToUnmark);

        // Exit for-body scope
        _symbolTable.ExitScope();
    }

    /// <summary>
    /// True when <paramref name="iterator"/> is a call to the builtin <c>range(...)</c> whose every
    /// yielded value is provably &gt;= 0 (#1052): <c>range(stop)</c> (values in <c>[0, stop)</c>);
    /// <c>range(start, stop)</c> or <c>range(start, stop, step)</c> where <c>start</c> is a
    /// non-negative int literal and (for the 3-arg form) <c>step</c> is a positive int literal, so the
    /// sequence only increases from a non-negative start. A user-defined <c>range</c> shadowing the
    /// builtin, keyword/spread arguments, or a possibly-negative start/step all disqualify.
    /// </summary>
    private bool RangeYieldsNonNegativeInts(Expression iterator)
    {
        if (iterator is not FunctionCall call
            || UnwrapParenthesized(call.Function) is not Identifier { Name: BuiltinNames.Range }
            || !call.KeywordArguments.IsEmpty
            || !ResolvesToBuiltinRange())
        {
            return false;
        }

        var args = call.Arguments;
        foreach (var arg in args)
        {
            if (arg is StarExpression or SpreadElement)
                return false;
        }

        return args.Length switch
        {
            // range(stop): values lie in [0, stop), so every value is >= 0.
            1 => true,
            // range(start, stop): the implicit +1 step keeps values >= start.
            2 => TryGetConstantIntIndex(args[0], out var start2) && start2 >= 0,
            // range(start, stop, step): a positive step keeps values >= start >= 0.
            3 => TryGetConstantIntIndex(args[0], out var start3) && start3 >= 0
                && TryGetConstantIntIndex(args[2], out var step3) && step3 > 0,
            _ => false
        };
    }

    /// <summary>
    /// True when the name <c>range</c> resolves to the builtin (not a user-defined function that
    /// shadows it), so <see cref="RangeYieldsNonNegativeInts"/> only trusts genuine range semantics.
    /// </summary>
    private bool ResolvesToBuiltinRange()
    {
        var resolved = _symbolTable.Lookup(BuiltinNames.Range, searchParents: true);
        if (resolved == null)
            return false;

        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(BuiltinNames.Range);
        return overloads != null && overloads.Contains(resolved);
    }

    /// <summary>
    /// Conservatively reports whether the simple name <paramref name="name"/> is rebound anywhere in
    /// <paramref name="body"/> — by assignment (including augmented, tuple/list unpacking, and star
    /// targets), variable declaration, a nested <c>for</c>/<c>with</c>/<c>except</c> binding, or a
    /// walrus expression. Container/member mutations (<c>xs[i] = v</c>, <c>o.f = v</c>) are not
    /// rebindings and do not count. Descends into all nested statements and expressions; when in
    /// doubt it errs toward "reassigned" so the non-negative induction-variable proof stays sound.
    /// </summary>
    private static bool IsNameReassignedIn(string name, IEnumerable<Statement> body)
    {
        var finder = new ReassignmentFinder(name);
        foreach (var stmt in body)
            finder.Visit(stmt);
        return finder.Found;
    }

    /// <summary>
    /// AST walker that sets <see cref="Found"/> when it encounters any rebinding of a target name.
    /// Relies on <see cref="AstVisitor"/> for full recursive descent; only the binding forms are
    /// overridden.
    /// </summary>
    private sealed class ReassignmentFinder : AstVisitor
    {
        private readonly string _name;

        public ReassignmentFinder(string name) => _name = name;

        public bool Found { get; private set; }

        private static bool TargetBindsName(Expression target, string name)
        {
            switch (target)
            {
                case Identifier id:
                    return id.Name == name;
                case Parenthesized paren:
                    return TargetBindsName(paren.Expression, name);
                case StarExpression star:
                    return TargetBindsName(star.Operand, name);
                case TupleLiteral tuple:
                    return tuple.Elements.Any(e => TargetBindsName(e, name));
                case ListLiteral list:
                    return list.Elements.Any(e => TargetBindsName(e, name));
                // Index/member targets mutate a container; they do not rebind the simple name.
                default:
                    return false;
            }
        }

        public override void VisitAssignment(Assignment node)
        {
            if (TargetBindsName(node.Target, _name))
                Found = true;
            base.VisitAssignment(node);
        }

        public override void VisitVariableDeclaration(VariableDeclaration node)
        {
            if (node.Name == _name)
                Found = true;
            base.VisitVariableDeclaration(node);
        }

        public override void VisitForStatement(ForStatement node)
        {
            if (TargetBindsName(node.Target, _name))
                Found = true;
            base.VisitForStatement(node);
        }

        public override void VisitWithStatement(WithStatement node)
        {
            if (node.Items.Any(item => item.Name == _name))
                Found = true;
            base.VisitWithStatement(node);
        }

        public override void VisitTryStatement(TryStatement node)
        {
            if (node.Handlers.Any(handler => handler.Name == _name))
                Found = true;
            base.VisitTryStatement(node);
        }

        public override void VisitWalrusExpression(WalrusExpression node)
        {
            if (node.Target == _name)
                Found = true;
            base.VisitWalrusExpression(node);
        }
    }

    private void CheckRaise(RaiseStatement raiseStmt)
    {
        // Bare raise (no exception) is only valid inside an except block
        if (raiseStmt.Exception == null && !_inExceptBlock)
        {
            AddError("Bare 'raise' statement can only be used inside an exception handler",
                raiseStmt.LineStart, raiseStmt.ColumnStart, code: DiagnosticCodes.Semantic.InvalidRaise,
                span: raiseStmt.Span);
        }

        if (raiseStmt.Exception != null)
        {
            CheckExpression(raiseStmt.Exception);
        }

        if (raiseStmt.Cause != null)
        {
            CheckExpression(raiseStmt.Cause);
        }
    }

    private void CheckTry(TryStatement tryStmt)
    {
        // Try block has its own scope
        _symbolTable.EnterScope("try");
        _controlFlowDepth++;
        foreach (var stmt in tryStmt.Body)
            CheckStatement(stmt);
        _controlFlowDepth--;
        _symbolTable.ExitScope();

        // Each exception handler has its own scope
        foreach (var handler in tryStmt.Handlers)
        {
            _symbolTable.EnterScope("except");
            _controlFlowDepth++;
            _inExceptBlock = true;

            if (handler.IsExceptStar)
            {
                _inExceptStarBlock = true;

                // Validate: except* cannot catch ExceptionGroup itself (PEP 654)
                if (handler.ExceptionType != null && handler.ExceptionType.Name == "ExceptionGroup")
                {
                    AddError("'except*' cannot catch 'ExceptionGroup' directly; use 'except' instead",
                        handler.ExceptionType.LineStart, handler.ExceptionType.ColumnStart,
                        code: DiagnosticCodes.Semantic.ExceptStarCatchesExceptionGroup,
                        span: handler.ExceptionType.Span);
                }
            }

            // Register the 'as' variable binding (e.g., except ValueError as e:)
            if (handler.Name != null)
            {
                SemanticType exceptionType;
                if (handler.IsExceptStar)
                {
                    // In except* handlers, the 'as' variable is an ExceptionGroup
                    // wrapping the matched exception type, not the raw type itself
                    var exceptionGroupSymbol = _symbolTable.Lookup("ExceptionGroup") as TypeSymbol
                        ?? _symbolTable.BuiltinRegistry.TryResolveClrType("ExceptionGroup");
                    exceptionType = exceptionGroupSymbol != null
                        ? new UserDefinedType { Name = "ExceptionGroup", Symbol = exceptionGroupSymbol }
                        : _typeResolver.ResolveTypeAnnotation(
                            new TypeAnnotation { Name = "ExceptionGroup", LineStart = handler.LineStart, ColumnStart = handler.ColumnStart });
                }
                else
                {
                    exceptionType = handler.ExceptionType != null
                        ? _typeResolver.ResolveTypeAnnotation(handler.ExceptionType)
                        : _typeResolver.ResolveTypeAnnotation(
                            new TypeAnnotation { Name = "Exception", LineStart = handler.LineStart, ColumnStart = handler.ColumnStart });
                }

                var varSymbol = new VariableSymbol
                {
                    Name = handler.Name,
                    Kind = SymbolKind.Variable,
                    Type = exceptionType,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = handler.LineStart,
                    DeclarationColumn = handler.ColumnStart,
                    NameDeclarationLine = handler.NameLineStart,
                    NameDeclarationColumn = handler.NameColumnStart,
                    DeclarationSpan = handler.Span,
                    DeclaringFilePath = _currentFilePath
                };

                if (!TryReportNonVariableRedefinition(handler.Name, handler.LineStart, handler.ColumnStart, handler.Span))
                {
                    _symbolTable.Define(varSymbol);
                    SemanticBinding.SetVariableType(varSymbol, exceptionType);
                    // Keyed on the handler node so the binding is reachable from the declaration
                    // even when the handler body never reads it (#1232) — the except scope is gone
                    // by the time an LSP handler asks.
                    _semanticInfo.SetExceptHandlerSymbol(handler, varSymbol);
                }
            }

            if (handler.Filter != null)
            {
                var filterType = CheckExpression(handler.Filter);
                if (filterType != null && filterType is not UnknownType && filterType != BuiltinType.Bool)
                {
                    AddError("Exception filter must be a boolean expression",
                        handler.Filter.LineStart, handler.Filter.ColumnStart,
                        code: DiagnosticCodes.Semantic.ExceptionFilterNotBoolean,
                        span: handler.Filter.Span);
                }
            }

            foreach (var stmt in handler.Body)
                CheckStatement(stmt);
            _inExceptBlock = false;
            _inExceptStarBlock = false;
            _controlFlowDepth--;
            _symbolTable.ExitScope();
        }

        // Else body has its own scope
        if (tryStmt.ElseBody.Length > 0)
        {
            _symbolTable.EnterScope("try-else");
            _controlFlowDepth++;
            foreach (var stmt in tryStmt.ElseBody)
                CheckStatement(stmt);
            _controlFlowDepth--;
            _symbolTable.ExitScope();
        }

        // Finally block has its own scope
        if (tryStmt.FinallyBody != null && tryStmt.FinallyBody.Length > 0)
        {
            var previousInFinally = _inFinally;
            _inFinally = true;
            _symbolTable.EnterScope("finally");
            _controlFlowDepth++;
            foreach (var stmt in tryStmt.FinallyBody)
                CheckStatement(stmt);
            _controlFlowDepth--;
            _symbolTable.ExitScope();
            _inFinally = previousInFinally;
        }
    }

    private void CheckWith(WithStatement withStmt)
    {
        if (withStmt.IsAsync && !_currentFunctionIsAsync)
        {
            AddError("'async with' can only be used inside 'async def' functions",
                withStmt.LineStart, withStmt.ColumnStart,
                code: DiagnosticCodes.Semantic.AwaitOutsideAsync, span: withStmt.Span);
        }

        // For `with assert_raises(E) as exc:`, define the capture variable in the
        // enclosing scope so it's accessible after the with block. The codegen
        // transforms this to `var exc = Assert.Throws<E>(...)` which is in the
        // enclosing scope.
        if (withStmt.Items.Length == 1 && withStmt.Items[0].Name != null
            && IsAssertRaisesExpression(withStmt.Items[0].ContextExpression))
        {
            var item = withStmt.Items[0];
            CheckExpression(item.ContextExpression);

            var exceptionType = ResolveAssertRaisesExceptionType(item.ContextExpression);
            var varSymbol = new VariableSymbol
            {
                Name = item.Name!,
                Kind = SymbolKind.Variable,
                Type = exceptionType,
                AccessLevel = AccessLevel.Public,
                DeclarationLine = item.LineStart,
                DeclarationColumn = item.ColumnStart,
                NameDeclarationLine = item.NameLineStart,
                NameDeclarationColumn = item.NameColumnStart,
                DeclarationSpan = item.Span,
                DeclaringFilePath = _currentFilePath
            };
            if (!TryReportNonVariableRedefinition(item.Name!, item.LineStart, item.ColumnStart, item.Span))
            {
                _symbolTable.Define(varSymbol);
                SemanticBinding.SetVariableType(varSymbol, exceptionType);
                _semanticInfo.SetWithItemSymbol(item, varSymbol);
            }
        }

        _symbolTable.EnterScope("with");
        _controlFlowDepth++;

        // Type-check each context expression and register 'as' variable bindings
        foreach (var item in withStmt.Items)
        {
            var exprType = CheckExpression(item.ContextExpression);

            // Determine context manager kind: IDisposable, IAsyncDisposable, or dunder protocol
            var cmKind = ResolveContextManagerKind(exprType, withStmt.IsAsync);
            if (cmKind == null)
            {
                var protocolDesc = withStmt.IsAsync
                    ? "__aenter__/__aexit__ or IAsyncDisposable"
                    : "__enter__/__exit__ or IDisposable";
                AddError(
                    $"Type '{exprType.GetDisplayName()}' does not implement {protocolDesc} and cannot be used in a {(withStmt.IsAsync ? "async " : "")}with statement",
                    item.ContextExpression.LineStart,
                    item.ContextExpression.ColumnStart,
                    code: DiagnosticCodes.Semantic.WithNotDisposable,
                    span: item.ContextExpression.Span);
            }
            else
            {
                _semanticInfo.SetContextManagerKind(item.ContextExpression, cmKind.Value);
            }

            // Determine the type for the 'as' variable
            var asVarType = exprType;
            if (cmKind is ContextManagerKind.DunderProtocol or ContextManagerKind.AsyncDunderProtocol)
            {
                // The 'as' variable gets the return type of __enter__/__aenter__
                var enterType = GetDunderEnterReturnType(exprType, withStmt.IsAsync);
                if (enterType != null)
                    asVarType = enterType;
            }

            // Skip if already defined in the enclosing scope (assert_raises capture)
            if (item.Name != null && !IsAssertRaisesExpression(item.ContextExpression))
            {
                var varSymbol = new VariableSymbol
                {
                    Name = item.Name,
                    Kind = SymbolKind.Variable,
                    Type = asVarType,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = item.LineStart,
                    DeclarationColumn = item.ColumnStart,
                    NameDeclarationLine = item.NameLineStart,
                    NameDeclarationColumn = item.NameColumnStart,
                    DeclarationSpan = item.Span,
                    DeclaringFilePath = _currentFilePath
                };

                if (!TryReportNonVariableRedefinition(item.Name, item.LineStart, item.ColumnStart, item.Span))
                {
                    _symbolTable.Define(varSymbol);
                    SemanticBinding.SetVariableType(varSymbol, asVarType);
                    _semanticInfo.SetWithItemSymbol(item, varSymbol);
                }
            }
        }

        foreach (var stmt in withStmt.Body)
            CheckStatement(stmt);

        _controlFlowDepth--;
        _symbolTable.ExitScope();
    }

    private void CheckDefer(DeferStatement deferStmt)
    {
        // A defer's cleanup runs on the exit paths of its enclosing block, so it is only
        // meaningful inside a function or method body. At module or class-body level there
        // is no scope to attach the cleanup to.
        if (_currentFunctionReturnType == null)
        {
            AddError(
                "'defer' can only appear inside a function or method body",
                deferStmt.LineStart, deferStmt.ColumnStart,
                code: DiagnosticCodes.Semantic.DeferOutsideFunction,
                span: deferStmt.Span);
        }

        // The deferred body lowers to a `finally` block, and C# forbids control-flow that
        // leaves a finally (CS0157). Reject return/yield/break/continue that would escape,
        // matching issue #1023's rule that "deferred blocks must not return a value".
        foreach (var stmt in deferStmt.Body)
            CheckDeferBodyControlFlow(stmt, insideLoop: false);

        _symbolTable.EnterScope("defer");
        _controlFlowDepth++;

        foreach (var stmt in deferStmt.Body)
            CheckStatement(stmt);

        _controlFlowDepth--;
        _symbolTable.ExitScope();
    }

    /// <summary>
    /// Reports control-flow statements that would escape a deferred block once it is lowered
    /// to a <c>finally</c>. <c>return</c> and <c>yield</c> always escape; <c>break</c> and
    /// <c>continue</c> escape only when they are not enclosed by a loop declared inside the
    /// deferred body. Nested function/lambda bodies open a new scope and are not traversed.
    /// </summary>
    private void CheckDeferBodyControlFlow(Statement stmt, bool insideLoop)
    {
        switch (stmt)
        {
            case ReturnStatement ret:
                AddError(
                    "a deferred statement must not 'return' — control cannot leave a defer block",
                    ret.LineStart, ret.ColumnStart,
                    code: DiagnosticCodes.Semantic.DeferControlFlowEscape, span: ret.Span);
                return;

            case YieldStatement y:
                AddError(
                    "a deferred statement must not 'yield' — control cannot leave a defer block",
                    y.LineStart, y.ColumnStart,
                    code: DiagnosticCodes.Semantic.DeferControlFlowEscape, span: y.Span);
                return;

            case BreakStatement brk when !insideLoop:
                AddError(
                    "a deferred statement must not 'break' out of its enclosing loop",
                    brk.LineStart, brk.ColumnStart,
                    code: DiagnosticCodes.Semantic.DeferControlFlowEscape, span: brk.Span);
                return;

            case ContinueStatement cont when !insideLoop:
                AddError(
                    "a deferred statement must not 'continue' its enclosing loop",
                    cont.LineStart, cont.ColumnStart,
                    code: DiagnosticCodes.Semantic.DeferControlFlowEscape, span: cont.Span);
                return;

            // Loops introduce a new break/continue target, so break/continue inside them
            // stay within the deferred body. return/yield still escape, so keep descending.
            case WhileStatement w:
                foreach (var s in w.Body)
                    CheckDeferBodyControlFlow(s, insideLoop: true);
                foreach (var s in w.ElseBody)
                    CheckDeferBodyControlFlow(s, insideLoop: true);
                return;

            case ForStatement f:
                foreach (var s in f.Body)
                    CheckDeferBodyControlFlow(s, insideLoop: true);
                foreach (var s in f.ElseBody)
                    CheckDeferBodyControlFlow(s, insideLoop: true);
                return;

            case IfStatement ifs:
                foreach (var s in ifs.ThenBody)
                    CheckDeferBodyControlFlow(s, insideLoop);
                foreach (var elif in ifs.ElifClauses)
                    foreach (var s in elif.Body)
                        CheckDeferBodyControlFlow(s, insideLoop);
                foreach (var s in ifs.ElseBody)
                    CheckDeferBodyControlFlow(s, insideLoop);
                return;

            case WithStatement with:
                foreach (var s in with.Body)
                    CheckDeferBodyControlFlow(s, insideLoop);
                return;

            case TryStatement t:
                foreach (var s in t.Body)
                    CheckDeferBodyControlFlow(s, insideLoop);
                foreach (var h in t.Handlers)
                    foreach (var s in h.Body)
                        CheckDeferBodyControlFlow(s, insideLoop);
                foreach (var s in t.ElseBody)
                    CheckDeferBodyControlFlow(s, insideLoop);
                foreach (var s in t.FinallyBody)
                    CheckDeferBodyControlFlow(s, insideLoop);
                return;

            case DeferStatement nested:
                foreach (var s in nested.Body)
                    CheckDeferBodyControlFlow(s, insideLoop);
                return;

            // FunctionDef / ClassDef / lambdas open their own scope: a return inside them
            // belongs to that scope, not the deferred block, so we stop here.
            default:
                return;
        }
    }

    private static bool IsAssertRaisesExpression(Expression expr)
    {
        return expr is FunctionCall call && UnwrapParenthesized(call.Function) switch
        {
            Identifier { Name: "assert_raises" } => true,
            MemberAccess { Member: "assert_raises" } => true,
            _ => false
        };
    }

    private SemanticType ResolveAssertRaisesExceptionType(Expression contextExpr)
    {
        if (contextExpr is FunctionCall { Arguments.Length: 1 } call)
        {
            var argType = CheckExpression(call.Arguments[0]);
            if (argType is UserDefinedType udt)
                return udt;
        }
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Resolves the context manager kind for a type used in a with statement.
    /// Returns null if the type cannot be used as a context manager.
    /// </summary>
    private ContextManagerKind? ResolveContextManagerKind(SemanticType type, bool isAsync)
    {
        if (isAsync)
        {
            // Async with: check async dunder protocol first, then IAsyncDisposable
            if (HasContextManagerProtocol(type, isAsync: true))
                return ContextManagerKind.AsyncDunderProtocol;
            if (IsAsyncDisposableType(type))
                return ContextManagerKind.AsyncDisposable;
            return null;
        }

        // Sync with: check dunder protocol first, then IDisposable
        if (HasContextManagerProtocol(type, isAsync: false))
            return ContextManagerKind.DunderProtocol;
        if (IsDisposableType(type))
            return ContextManagerKind.Disposable;
        return null;
    }

    /// <summary>
    /// Checks whether a user-defined type has the context manager dunder protocol.
    /// For sync: requires both __enter__ and __exit__.
    /// For async: requires both __aenter__ and __aexit__.
    /// </summary>
    private bool HasContextManagerProtocol(SemanticType type, bool isAsync)
    {
        var typeSymbol = GetTypeSymbolFromSemanticType(type);
        if (typeSymbol == null)
            return false;

        var enterName = isAsync ? DunderNames.Aenter : DunderNames.Enter;
        var exitName = isAsync ? DunderNames.Aexit : DunderNames.Exit;

        bool hasEnter = typeSymbol.Methods.Any(m => m.Name == enterName);
        bool hasExit = typeSymbol.Methods.Any(m => m.Name == exitName);
        return hasEnter && hasExit;
    }

    /// <summary>
    /// Gets the return type of __enter__ or __aenter__ for the 'as' variable binding.
    /// Returns null if the method is not found or has no return type.
    /// </summary>
    private SemanticType? GetDunderEnterReturnType(SemanticType type, bool isAsync)
    {
        var typeSymbol = GetTypeSymbolFromSemanticType(type);
        if (typeSymbol == null)
            return null;

        var enterName = isAsync ? DunderNames.Aenter : DunderNames.Enter;
        var enterMethod = typeSymbol.Methods.FirstOrDefault(m => m.Name == enterName);
        if (enterMethod?.ReturnType != null && enterMethod.ReturnType is not VoidType)
        {
            var returnType = enterMethod.ReturnType;
            if (isAsync && returnType is TaskType taskType && taskType.ResultType != null)
                returnType = taskType.ResultType;
            return returnType;
        }

        // Default: return self type (common pattern for __enter__)
        return type;
    }

    private void CheckAssert(AssertStatement assertStmt)
    {
        // Inside a @test function the emitter rewrites the whole assert into an xUnit assertion
        // rather than lowering its test as an ordinary expression, so the type-test classifier
        // steps aside for exactly this expression (see _testAssertTest).
        using (ScopedValue.Push(ref _testAssertTest, _inTestFunction ? assertStmt.Test : null))
            CheckExpression(assertStmt.Test);

        if (assertStmt.Message != null)
        {
            CheckExpression(assertStmt.Message);
        }

        // `assert x is not None` narrows x for the rest of the enclosing scope: if the assert fails
        // execution halts, so the positive branch always holds afterward. This narrowing is applied via
        // the CFG dataflow (the assert generates facts that flow to following statements, #1042), and
        // read-site resolution materializes the accessor per node (#1081).
    }

    /// <summary>
    /// Checks whether a type implements IDisposable (required for 'with' statement / C# 'using').
    /// </summary>
    private bool IsDisposableType(SemanticType type)
    {
        // Builtin types with a CLR backing type (e.g., TextFile from open())
        // Only IDisposable needs this check — async disposable builtins don't exist yet.
        if (type is BuiltinType bt && bt.ClrType != null)
            return typeof(System.IDisposable).IsAssignableFrom(bt.ClrType);

        return IsImplementingInterface(type, typeof(System.IDisposable), "IDisposable");
    }

    /// <summary>
    /// Checks whether a type implements IAsyncDisposable (required for 'async with' / C# 'await using').
    /// </summary>
    private bool IsAsyncDisposableType(SemanticType type)
    {
        return IsImplementingInterface(type, typeof(System.IAsyncDisposable), "IAsyncDisposable");
    }

    /// <summary>
    /// Checks whether a type implements a given CLR interface by name and/or CLR type assignability.
    /// Handles UnknownType passthrough, Nullable/Optional unwrapping, UserDefinedType and GenericType checks.
    /// </summary>
    private bool IsImplementingInterface(SemanticType type, Type clrInterface, string interfaceName)
    {
        // Unknown type: skip to avoid cascading errors
        if (type is UnknownType)
            return true;

        // Nullable/Optional: check underlying type
        if (type is NullableType nullable)
            return IsImplementingInterface(nullable.UnderlyingType, clrInterface, interfaceName);
        if (type is OptionalType optional)
            return IsImplementingInterface(optional.UnderlyingType, clrInterface, interfaceName);

        // User-defined types: check CLR type or interface list
        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            if (udt.Symbol.ClrType != null)
                return clrInterface.IsAssignableFrom(udt.Symbol.ClrType);

            var allInterfaces = CollectAllInterfaces(udt.Symbol);
            foreach (var iface in allInterfaces)
            {
                if (iface.Name == interfaceName)
                    return true;
                if (iface.ClrType != null && clrInterface.IsAssignableFrom(iface.ClrType))
                    return true;
            }

            return false;
        }

        // Generic types backed by a symbol (e.g., List<T>, Dict<K,V>)
        if (type is GenericType gt)
        {
            var sym = _symbolTable.Lookup(gt.Name) as TypeSymbol;
            if (sym?.ClrType != null)
                return clrInterface.IsAssignableFrom(sym.ClrType);
            return false;
        }

        // All other types (builtins without CLR type, functions, tuples, etc.)
        return false;
    }

    // TODO(#206): Add language spec for complex tuple unpacking (docs/language_specification/tuple_unpacking.md)
    /// <summary>
    /// Recursively defines loop variables for nested tuple targets in for-loops.
    /// E.g., for (x, y), name in items: registers x, y, and name.
    /// </summary>
    private void DefineForLoopTupleTargets(ImmutableArray<Expression> targets, IReadOnlyList<SemanticType> elementTypes)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var targetElem = targets[i];
            var elemType = elementTypes[i];

            if (targetElem is Identifier id)
            {
                var loopVarSymbol = new VariableSymbol
                {
                    Name = id.Name,
                    Kind = SymbolKind.Variable,
                    Type = elemType,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = id.LineStart,
                    DeclarationColumn = id.ColumnStart,
                    NameDeclarationLine = id.LineStart,
                    NameDeclarationColumn = id.ColumnStart,
                    DeclarationSpan = id.Span,
                    DeclaringFilePath = _currentFilePath
                };

                if (_symbolTable.Lookup(id.Name, searchParents: false) == null)
                {
                    _symbolTable.Define(loopVarSymbol);
                    SemanticBinding.SetVariableType(loopVarSymbol, elemType);
                    _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
                }

                _semanticInfo.SetExpressionType(targetElem, elemType);
                if (elemType is UnknownType)
                    MarkExpressionAsErrorRecovery(targetElem);
            }
            else if (targetElem is TupleLiteral nestedTuple)
            {
                if (elemType is not TupleType nestedTupleType)
                {
                    AddError($"Cannot unpack non-tuple type '{elemType.GetDisplayName()}' into nested tuple in for loop",
                        targetElem.LineStart, targetElem.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                        span: targetElem.Span);
                    continue;
                }

                if (nestedTuple.Elements.Length != nestedTupleType.ElementTypes.Count)
                {
                    AddError($"Cannot unpack {nestedTupleType.ElementTypes.Count} values into {nestedTuple.Elements.Length} variables in for loop",
                        targetElem.LineStart, targetElem.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                        span: targetElem.Span);
                    continue;
                }

                DefineForLoopTupleTargets(nestedTuple.Elements, nestedTupleType.ElementTypes);
            }
            else
            {
                CheckExpression(targetElem);
            }
        }
    }
}
