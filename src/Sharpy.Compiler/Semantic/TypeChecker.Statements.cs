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
            var inferredType = CheckExpression(assignment.Value);
            _expectedType = previousExpectedType2;

            // Create a new variable symbol with the inferred type (or redefine existing)
            var newSymbol = new VariableSymbol
            {
                Name = targetId.Name,
                Kind = SymbolKind.Variable,
                Type = inferredType,
                IsConstant = false,
                DeclarationLine = assignment.LineStart,
                DeclarationColumn = assignment.ColumnStart,
                AccessLevel = AccessLevel.Public
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
        var valueType = CheckExpression(assignment.Value);
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
            // Special case: Provide helpful error messages for None misuse
            if (valueType is VoidType && assignmentTargetType is OptionalType)
            {
                AddError($"Cannot assign 'None' to '{assignmentTargetType.GetDisplayName()}'. 'None' is the C# null literal. Did you mean 'None()' to construct an empty Optional?",
                    assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.NullabilityViolation,
                    span: assignment.Value.Span);
            }
            else if (valueType is VoidType && assignmentTargetType is not NullableType)
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

    private void CheckVariableDeclaration(VariableDeclaration varDecl)
    {
        var declaredType = _typeResolver.ResolveTypeAnnotation(varDecl.Type);

        if (varDecl.InitialValue != null)
        {
            // Set expected type for constructor inference (Some/None()/Ok/Err)
            var previousExpectedType = _expectedType;
            _expectedType = declaredType is UnknownType ? null : declaredType;
            var initType = CheckExpression(varDecl.InitialValue);
            _expectedType = previousExpectedType;

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
                // Special case: Provide helpful error messages for None misuse
                else if (initType is VoidType && declaredType is OptionalType)
                {
                    AddError($"Cannot assign 'None' to '{declaredType.GetDisplayName()}'. 'None' is the C# null literal. Did you mean 'None()' to construct an empty Optional?",
                        varDecl.LineStart, varDecl.ColumnStart, code: DiagnosticCodes.Semantic.NullabilityViolation,
                        span: varDecl.InitialValue!.Span);
                }
                else if (initType is VoidType && declaredType is not NullableType)
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
                DeclarationColumn = varDecl.ColumnStart
            };
            _symbolTable.Define(constSymbol);
            SemanticBinding.SetVariableType(constSymbol, declaredType);
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
            DeclarationColumn = varDecl.ColumnStart
        };
        _symbolTable.Define(newSymbol);
        SemanticBinding.SetVariableType(newSymbol, declaredType);
    }

    private void CheckReturn(ReturnStatement returnStmt)
    {
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
                if (returnType is VoidType && _currentFunctionReturnType is OptionalType)
                {
                    AddError($"Cannot return 'None' from function expecting '{_currentFunctionReturnType.GetDisplayName()}'. 'None' is the C# null literal. Did you mean 'None()' to construct an empty Optional?",
                        returnStmt.LineStart, returnStmt.ColumnStart, code: DiagnosticCodes.Semantic.MissingReturnValue,
                        span: returnStmt.Span);
                }
                else
                {
                    AddError($"Cannot return type '{returnType.GetDisplayName()}' from function expecting '{_currentFunctionReturnType.GetDisplayName()}'",
                        returnStmt.LineStart, returnStmt.ColumnStart, code: DiagnosticCodes.Semantic.MissingReturnValue,
                        span: returnStmt.Span);
                }
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
        var condType = CheckExpression(ifStmt.Test);
        if (!IsTruthTestable(condType))
        {
            AddError($"If condition must be boolean, got '{condType.GetDisplayName()}'",
                ifStmt.LineStart, ifStmt.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                span: ifStmt.Test.Span);
        }

        // Check for type narrowing patterns
        var (narrowedTypesInThen, thenDecision) = ExtractNarrowedTypes(ifStmt.Test, true);
        var (narrowedTypesInElse, elseDecision) = ExtractNarrowedTypes(ifStmt.Test, false);

        // Record narrowing decisions for codegen
        // Merge then/else decisions — they describe opposite branches of the same test.
        // thenDecision entries apply in the then-branch; elseDecision entries apply in the else-branch.
        // Override NarrowInThenBranch to ensure correct tagging after merge.
        var allOptional = new List<OptionalNarrowing>();
        foreach (var n in thenDecision.OptionalNarrowings)
            allOptional.Add(n with { NarrowInThenBranch = true });
        foreach (var n in elseDecision.OptionalNarrowings)
            allOptional.Add(n with { NarrowInThenBranch = false });
        var allIsInstance = new List<IsInstanceNarrowing>();
        foreach (var n in thenDecision.IsInstanceNarrowings)
            allIsInstance.Add(n with { NarrowInThenBranch = true });
        foreach (var n in elseDecision.IsInstanceNarrowings)
            allIsInstance.Add(n with { NarrowInThenBranch = false });
        if (allOptional.Count > 0 || allIsInstance.Count > 0)
            _semanticInfo.SetNarrowingDecision(ifStmt.Test, new NarrowingDecision(allOptional, allIsInstance));

        // Check then branch with its own narrowing scope
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(narrowedTypesInThen);

            _symbolTable.EnterScope("if-then");
            _controlFlowDepth++;
            foreach (var stmt in ifStmt.ThenBody)
                CheckStatement(stmt);
            _controlFlowDepth--;
            _symbolTable.ExitScope();
        }

        // Check elif clauses, each with its own narrowing scope
        foreach (var elif in ifStmt.ElifClauses)
        {
            var elifCondType = CheckExpression(elif.Test);
            if (!IsTruthTestable(elifCondType))
            {
                AddError($"Elif condition must be boolean, got '{elifCondType.GetDisplayName()}'",
                    elif.LineStart, elif.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: elif.Test.Span);
            }

            var (narrowedTypesInElif, elifDecision) = ExtractNarrowedTypes(elif.Test, true);
            if (elifDecision.OptionalNarrowings.Count > 0 || elifDecision.IsInstanceNarrowings.Count > 0)
                _semanticInfo.SetNarrowingDecision(elif.Test, elifDecision);

            using (_narrowingContext.EnterScope())
            {
                _narrowingContext.ApplyNarrowings(narrowedTypesInElif);

                _symbolTable.EnterScope("elif");
                _controlFlowDepth++;
                foreach (var stmt in elif.Body)
                    CheckStatement(stmt);
                _controlFlowDepth--;
                _symbolTable.ExitScope();
            }
        }

        // Check else branch with its own narrowing scope (only if there are statements)
        if (ifStmt.ElseBody.Length > 0)
        {
            using (_narrowingContext.EnterScope())
            {
                _narrowingContext.ApplyNarrowings(narrowedTypesInElse);

                _symbolTable.EnterScope("if-else");
                _controlFlowDepth++;
                foreach (var stmt in ifStmt.ElseBody)
                    CheckStatement(stmt);
                _controlFlowDepth--;
                _symbolTable.ExitScope();
            }
        }
    }

    private void CheckWhile(WhileStatement whileStmt)
    {
        var condType = CheckExpression(whileStmt.Test);
        if (!IsTruthTestable(condType))
        {
            AddError($"While condition must be boolean, got '{condType.GetDisplayName()}'",
                whileStmt.LineStart, whileStmt.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                span: whileStmt.Test.Span);
        }

        // Check for type narrowing patterns (similar to if statement)
        var (narrowedTypesInBody, whileDecision) = ExtractNarrowedTypes(whileStmt.Test, true);
        if (whileDecision.OptionalNarrowings.Count > 0 || whileDecision.IsInstanceNarrowings.Count > 0)
            _semanticInfo.SetNarrowingDecision(whileStmt.Test, whileDecision);

        // Enter narrowing scope for while body
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(narrowedTypesInBody);

            _symbolTable.EnterScope("while-body");
            _controlFlowDepth++;
            foreach (var stmt in whileStmt.Body)
                CheckStatement(stmt);
            _controlFlowDepth--;
            _symbolTable.ExitScope();
        }
    }

    private void CheckFor(ForStatement forStmt)
    {
        if (forStmt.IsAsync && !_currentFunctionIsAsync)
        {
            AddError("'async for' can only be used inside 'async def' functions",
                forStmt.LineStart, forStmt.ColumnStart,
                code: DiagnosticCodes.Semantic.AwaitOutsideAsync, span: forStmt.Span);
        }

        var iterType = CheckExpression(forStmt.Iterator);

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
        else if (forStmt.Target is Identifier id)
        {
            // Infer the type of the loop variable from the iterator
            var loopVarSymbol = new VariableSymbol
            {
                Name = id.Name,
                Kind = SymbolKind.Variable,
                Type = elementType,
                AccessLevel = AccessLevel.Public,
                DeclarationLine = id.LineStart,
                DeclarationColumn = id.ColumnStart
            };

            // Check if already defined in this scope
            if (_symbolTable.Lookup(id.Name, searchParents: false) == null)
            {
                _symbolTable.Define(loopVarSymbol);
                SemanticBinding.SetVariableType(loopVarSymbol, elementType);
                _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
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

        // Exit for-body scope
        _symbolTable.ExitScope();
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

            // Register the 'as' variable binding (e.g., except ValueError as e:)
            if (handler.Name != null)
            {
                var exceptionType = handler.ExceptionType != null
                    ? _typeResolver.ResolveTypeAnnotation(handler.ExceptionType)
                    : _typeResolver.ResolveTypeAnnotation(
                        new TypeAnnotation { Name = "Exception", LineStart = handler.LineStart, ColumnStart = handler.ColumnStart });

                var varSymbol = new VariableSymbol
                {
                    Name = handler.Name,
                    Kind = SymbolKind.Variable,
                    Type = exceptionType,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = handler.LineStart,
                    DeclarationColumn = handler.ColumnStart
                };

                _symbolTable.Define(varSymbol);
                SemanticBinding.SetVariableType(varSymbol, exceptionType);
            }

            foreach (var stmt in handler.Body)
                CheckStatement(stmt);
            _inExceptBlock = false;
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
            _symbolTable.EnterScope("finally");
            _controlFlowDepth++;
            foreach (var stmt in tryStmt.FinallyBody)
                CheckStatement(stmt);
            _controlFlowDepth--;
            _symbolTable.ExitScope();
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

            if (item.Name != null)
            {
                var varSymbol = new VariableSymbol
                {
                    Name = item.Name,
                    Kind = SymbolKind.Variable,
                    Type = asVarType,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = item.LineStart,
                    DeclarationColumn = item.ColumnStart,
                    DeclaringFilePath = _currentFilePath
                };

                _symbolTable.Define(varSymbol);
                SemanticBinding.SetVariableType(varSymbol, asVarType);
                _semanticInfo.SetWithItemSymbol(item, varSymbol);
            }
        }

        foreach (var stmt in withStmt.Body)
            CheckStatement(stmt);

        _controlFlowDepth--;
        _symbolTable.ExitScope();
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
        var testType = CheckExpression(assertStmt.Test);
        if (assertStmt.Message != null)
        {
            CheckExpression(assertStmt.Message);
        }
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
                    DeclarationColumn = id.ColumnStart
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
