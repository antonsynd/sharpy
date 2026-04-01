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

    /// <summary>
    /// Extracts the TypeSymbol from a SemanticType, handling UserDefinedType,
    /// NullableType, OptionalType, and GenericType wrappers.
    /// </summary>
    private TypeSymbol? GetTypeSymbolFromSemanticType(SemanticType type)
    {
        return type switch
        {
            UserDefinedType udt => udt.Symbol,
            NullableType nullable => GetTypeSymbolFromSemanticType(nullable.UnderlyingType),
            OptionalType optional => GetTypeSymbolFromSemanticType(optional.UnderlyingType),
            GenericType gt => _symbolTable.Lookup(gt.Name) as TypeSymbol,
            _ => null
        };
    }

    private void CheckAssert(AssertStatement assertStmt)
    {
        var testType = CheckExpression(assertStmt.Test);
        if (assertStmt.Message != null)
        {
            CheckExpression(assertStmt.Message);
        }
    }

    private void CheckMatch(MatchStatement matchStmt)
    {
        var scrutineeType = CheckExpression(matchStmt.Scrutinee);

        foreach (var matchCase in matchStmt.Cases)
        {
            using (_narrowingContext.EnterScope())
            {
                _symbolTable.EnterScope("match-case");
                _controlFlowDepth++;

                CheckPattern(matchCase.Pattern, scrutineeType);

                if (matchCase.Guard != null)
                {
                    var guardType = CheckExpression(matchCase.Guard);
                    if (!IsTruthTestable(guardType))
                    {
                        AddError("Guard condition must be a boolean expression",
                            matchCase.Guard.LineStart, matchCase.Guard.ColumnStart,
                            code: DiagnosticCodes.Semantic.ConditionNotBoolean,
                            span: matchCase.Guard.Span);
                    }
                }

                foreach (var stmt in matchCase.Body)
                    CheckStatement(stmt);

                _controlFlowDepth--;
                _symbolTable.ExitScope();
            }
        }
    }

    private void CheckPattern(Pattern pattern, SemanticType scrutineeType)
    {
        switch (pattern)
        {
            case WildcardPattern:
                break;

            case BindingPattern binding:
                {
                    var newSymbol = new VariableSymbol
                    {
                        Name = binding.Name.Name,
                        Kind = SymbolKind.Variable,
                        Type = scrutineeType,
                        IsConstant = false,
                        DeclarationLine = binding.LineStart,
                        DeclarationColumn = binding.ColumnStart,
                        AccessLevel = AccessLevel.Public
                    };

                    _symbolTable.Define(newSymbol);
                    SemanticBinding.SetVariableType(newSymbol, scrutineeType);
                    _semanticInfo.SetIdentifierSymbol(binding.Name, newSymbol);
                    break;
                }

            case LiteralPattern literal:
                {
                    // Handle None() pattern when matching against Optional[T]
                    if (literal.Literal is FunctionCall { Function: NoneLiteral } noneCall
                        && noneCall.Arguments.Length == 0
                        && scrutineeType is OptionalType)
                    {
                        // Record synthetic None union case for exhaustiveness checking
                        var synth = GetSyntheticOptionalUnion();
                        var noneCase = synth.UnionCases.First(c => c.Name == "None");
                        _semanticInfo.SetPatternUnionCase(literal, noneCase);
                        break;
                    }

                    // Handle bare None literal when matching against Optional[T]
                    if (literal.Literal is NoneLiteral && scrutineeType is OptionalType)
                    {
                        var synth = GetSyntheticOptionalUnion();
                        var noneCase = synth.UnionCases.First(c => c.Name == "None");
                        _semanticInfo.SetPatternUnionCase(literal, noneCase);
                        break;
                    }

                    var litType = CheckExpression(literal.Literal);
                    if (!IsAssignable(litType, scrutineeType) && !IsAssignable(scrutineeType, litType))
                    {
                        AddError(
                            $"Pattern type '{litType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                            literal.LineStart, literal.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: literal.Span);
                    }
                    break;
                }

            case TuplePattern tuplePattern:
                {
                    if (scrutineeType is TupleType tupleType)
                    {
                        if (tuplePattern.Elements.Length != tupleType.ElementTypes.Count)
                        {
                            AddError(
                                $"Tuple pattern has {tuplePattern.Elements.Length} elements but scrutinee has {tupleType.ElementTypes.Count}",
                                tuplePattern.LineStart, tuplePattern.ColumnStart,
                                code: DiagnosticCodes.Semantic.TuplePatternLengthMismatch,
                                span: tuplePattern.Span);
                        }
                        else
                        {
                            for (int i = 0; i < tuplePattern.Elements.Length; i++)
                                CheckPattern(tuplePattern.Elements[i], tupleType.ElementTypes[i]);
                        }
                    }
                    else
                    {
                        AddError(
                            $"Cannot destructure non-tuple type '{scrutineeType.GetDisplayName()}' with tuple pattern",
                            tuplePattern.LineStart, tuplePattern.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: tuplePattern.Span);
                    }
                    break;
                }

            case TypePattern typePattern:
                CheckTypePattern(typePattern, scrutineeType);
                break;

            case RelationalPattern relational:
                {
                    var valueType = CheckExpression(relational.Value);
                    if (!TypeUtils.IsNumericOrUnknown(scrutineeType))
                    {
                        AddError(
                            $"Relational patterns require a numeric scrutinee type, got '{scrutineeType.GetDisplayName()}'",
                            relational.LineStart, relational.ColumnStart,
                            code: DiagnosticCodes.Semantic.RelationalPatternTypeMismatch,
                            span: relational.Span);
                    }
                    if (!IsAssignable(valueType, scrutineeType) && !IsAssignable(scrutineeType, valueType)
                        && valueType is not UnknownType)
                    {
                        AddError(
                            $"Pattern value type '{valueType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                            relational.LineStart, relational.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: relational.Span);
                    }
                    break;
                }

            case OrPattern orPattern:
                {
                    bool hasMemberAccess = orPattern.Alternatives.Any(a => a is MemberAccessPattern);
                    foreach (var alt in orPattern.Alternatives)
                    {
                        if (alt is BindingPattern)
                        {
                            AddError(
                                "Binding patterns are not allowed inside or-patterns",
                                alt.LineStart, alt.ColumnStart,
                                code: DiagnosticCodes.Semantic.BindingInOrPattern,
                                span: alt.Span);
                        }
                        else if (hasMemberAccess && alt is not MemberAccessPattern && alt is not LiteralPattern && alt is not WildcardPattern)
                        {
                            AddError(
                                "Only literal, member access, and wildcard patterns can be combined with member access patterns in or-patterns",
                                alt.LineStart, alt.ColumnStart,
                                code: DiagnosticCodes.Semantic.UnsupportedPatternInMemberAccessOr,
                                span: alt.Span);
                        }
                        else
                        {
                            CheckPattern(alt, scrutineeType);
                        }
                    }
                    break;
                }

            case PropertyPattern propertyPattern:
                CheckPropertyPattern(propertyPattern, scrutineeType);
                break;

            case PositionalPattern positionalPattern:
                CheckPositionalPattern(positionalPattern, scrutineeType);
                break;

            case MemberAccessPattern memberAccess:
                CheckMemberAccessPattern(memberAccess, scrutineeType);
                break;

            default:
                AddError(
                    $"Unsupported pattern type '{pattern.GetType().Name}'. This pattern is not yet implemented.",
                    pattern.LineStart, pattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnsupportedFeature);
                break;
        }
    }

    /// <summary>
    /// Check a type pattern: resolve the type, handle union cases, validate compatibility,
    /// and register any binding variable.
    /// </summary>
    private void CheckTypePattern(TypePattern typePattern, SemanticType scrutineeType)
    {
        var resolvedType = _typeResolver.ResolveTypeAnnotation(typePattern.Type);
        if (resolvedType is UnknownType)
        {
            // Try to resolve as a union case (e.g., case Point(): when matching Shape)
            var unionCaseSymbol = TryResolveUnionCaseFromPattern(
                typePattern.Type.Name, scrutineeType);
            if (unionCaseSymbol != null)
            {
                _semanticInfo.SetPatternUnionCase(typePattern, unionCaseSymbol);
                resolvedType = new UserDefinedType { Name = unionCaseSymbol.Name, Symbol = unionCaseSymbol };
            }
            else
            {
                AddError(
                    $"Unknown type '{typePattern.Type.Name}' in type pattern",
                    typePattern.LineStart, typePattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedType,
                    span: typePattern.Span);
            }
        }
        else if (scrutineeType is not UnknownType
            && !IsAssignable(resolvedType, scrutineeType)
            && !IsAssignable(scrutineeType, resolvedType))
        {
            AddError(
                $"Type pattern '{resolvedType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                typePattern.LineStart, typePattern.ColumnStart,
                code: DiagnosticCodes.Semantic.TypePatternIncompatible,
                span: typePattern.Span);
        }
        if (typePattern.BindingName != null)
        {
            var newSymbol = new VariableSymbol
            {
                Name = typePattern.BindingName.Name,
                Kind = SymbolKind.Variable,
                Type = resolvedType,
                IsConstant = false,
                DeclarationLine = typePattern.BindingName.LineStart,
                DeclarationColumn = typePattern.BindingName.ColumnStart,
                AccessLevel = AccessLevel.Public
            };
            _symbolTable.Define(newSymbol);
            SemanticBinding.SetVariableType(newSymbol, resolvedType);
            _semanticInfo.SetIdentifierSymbol(typePattern.BindingName, newSymbol);
        }
    }

    /// <summary>
    /// Check a property pattern: resolve the type, then validate each field sub-pattern.
    /// </summary>
    private void CheckPropertyPattern(PropertyPattern propertyPattern, SemanticType scrutineeType)
    {
        TypeSymbol? typeSymbol = null;
        if (propertyPattern.Type != null)
        {
            var resolvedType = _typeResolver.ResolveTypeAnnotation(propertyPattern.Type);
            if (resolvedType is UnknownType)
            {
                AddError(
                    $"Unknown type '{propertyPattern.Type.Name}' in property pattern",
                    propertyPattern.LineStart, propertyPattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedType,
                    span: propertyPattern.Span);
            }
            else if (resolvedType is UserDefinedType udt)
            {
                typeSymbol = udt.Symbol;
            }
        }

        foreach (var field in propertyPattern.Fields)
        {
            if (typeSymbol != null)
            {
                var fieldSymbol = typeSymbol.Fields.FirstOrDefault(f => f.Name == field.Name);
                if (fieldSymbol == null)
                {
                    AddError(
                        $"Type '{typeSymbol.Name}' has no field '{field.Name}'",
                        field.LineStart, field.ColumnStart,
                        code: DiagnosticCodes.Semantic.PropertyPatternUnknownField,
                        span: field.Span);
                }
                else
                {
                    CheckPattern(field.Pattern, fieldSymbol.Type);
                }
            }
            else
            {
                CheckPattern(field.Pattern, scrutineeType);
            }
        }
    }

    /// <summary>
    /// Check a positional pattern: resolve the type (including union cases),
    /// validate deconstruction support, and check element sub-patterns.
    /// </summary>
    private void CheckPositionalPattern(PositionalPattern positionalPattern, SemanticType scrutineeType)
    {
        TypeSymbol? typeSymbol = null;
        if (positionalPattern.Type != null)
        {
            // Try to resolve as a union case first when scrutinee is a union type
            var unionCaseSymbol = TryResolveUnionCaseFromPattern(
                positionalPattern.Type.Name, scrutineeType);

            if (unionCaseSymbol != null)
            {
                typeSymbol = unionCaseSymbol;
                _semanticInfo.SetPatternUnionCase(positionalPattern, unionCaseSymbol);
            }
            else
            {
                var resolvedType = _typeResolver.ResolveTypeAnnotation(positionalPattern.Type);
                if (resolvedType is UnknownType)
                {
                    AddError(
                        $"Unknown type '{positionalPattern.Type.Name}' in positional pattern",
                        positionalPattern.LineStart, positionalPattern.ColumnStart,
                        code: DiagnosticCodes.Semantic.UndefinedType,
                        span: positionalPattern.Span);
                }
                else if (resolvedType is UserDefinedType udt)
                {
                    typeSymbol = udt.Symbol;
                    // For non-union types, check if positional deconstruction is supported
                    if (typeSymbol != null
                        && typeSymbol.BaseType?.TypeKind != TypeKind.Union
                        && typeSymbol.TypeKind != TypeKind.Union)
                    {
                        bool hasDeconstruct = typeSymbol.Methods.Any(m => m.Name == "Deconstruct");
                        bool hasMatchingFields = typeSymbol.Fields.Count == positionalPattern.Elements.Length;
                        if (!hasDeconstruct && !hasMatchingFields)
                        {
                            AddError(
                                $"Type '{typeSymbol.Name}' does not support positional deconstruction (no Deconstruct method and field count {typeSymbol.Fields.Count} does not match pattern element count {positionalPattern.Elements.Length})",
                                positionalPattern.LineStart, positionalPattern.ColumnStart,
                                code: DiagnosticCodes.Semantic.PositionalPatternNoDeconstruct,
                                span: positionalPattern.Span);
                        }
                    }
                }
            }
        }

        if (typeSymbol != null)
        {
            // Get field types, substituting type parameters for generic unions
            var fieldTypes = GetUnionCaseFieldTypes(typeSymbol, scrutineeType);

            if (positionalPattern.Elements.Length != fieldTypes.Count)
            {
                AddError(
                    $"Positional pattern has {positionalPattern.Elements.Length} elements but type '{typeSymbol.Name}' has {fieldTypes.Count} fields",
                    positionalPattern.LineStart, positionalPattern.ColumnStart,
                    code: typeSymbol.BaseType is { TypeKind: TypeKind.Union }
                        ? DiagnosticCodes.Semantic.UnionCaseFieldMismatch
                        : DiagnosticCodes.Semantic.PositionalPatternCountMismatch,
                    span: positionalPattern.Span);
            }
            else
            {
                for (int i = 0; i < positionalPattern.Elements.Length; i++)
                {
                    CheckPattern(positionalPattern.Elements[i], fieldTypes[i]);
                }
            }
        }
        else
        {
            foreach (var element in positionalPattern.Elements)
            {
                CheckPattern(element, scrutineeType);
            }
        }
    }

    /// <summary>
    /// Check a member access pattern: resolve dotted paths for enum members,
    /// union cases, and field/property access chains.
    /// </summary>
    private void CheckMemberAccessPattern(MemberAccessPattern memberAccess, SemanticType scrutineeType)
    {
        // Resolve the dotted path as a member access (e.g., Color.RED).
        // Look up the first part as a type, then resolve subsequent parts as fields/members.
        var typeName = memberAccess.Parts[0];
        var typeSymbol = _symbolTable.Lookup(typeName) as TypeSymbol;
        if (typeSymbol == null)
        {
            AddError(
                $"Undefined type '{typeName}' in pattern",
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedType,
                span: memberAccess.Span);
            return;
        }

        // Check if this is a union case pattern (e.g., Option.None, Result.Ok)
        if (typeSymbol.TypeKind == TypeKind.Union && memberAccess.Parts.Length == 2)
        {
            var caseName = memberAccess.Parts[1];
            var caseSymbol = typeSymbol.UnionCases.FirstOrDefault(c => c.Name == caseName);
            if (caseSymbol != null)
            {
                _semanticInfo.SetPatternUnionCase(memberAccess, caseSymbol);
                return;
            }
            else
            {
                AddError(
                    $"Union '{typeSymbol.Name}' has no case '{caseName}'",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnionCaseNotFound,
                    span: memberAccess.Span);
                return;
            }
        }

        // Check if this is an enum member pattern (e.g., Color.RED)
        if (typeSymbol.TypeKind == TypeKind.Enum && memberAccess.Parts.Length == 2)
        {
            var memberName = memberAccess.Parts[1];
            var enumField = typeSymbol.Fields.FirstOrDefault(f => f.Name == memberName);
            if (enumField != null)
            {
                // Verify the enum type matches the scrutinee type
                if (scrutineeType is UserDefinedType udt && udt.Symbol == typeSymbol)
                {
                    // Valid enum member pattern matching the scrutinee
                    return;
                }
                else
                {
                    AddError(
                        $"Enum member '{typeName}.{memberName}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                        memberAccess.LineStart, memberAccess.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: memberAccess.Span);
                    return;
                }
            }
            else
            {
                AddError(
                    $"Enum '{typeSymbol.Name}' has no member '{memberName}'",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedMember,
                    span: memberAccess.Span);
                return;
            }
        }

        // Resolve remaining parts as field or property access
        SemanticType? resolvedType = null;
        for (int i = 1; i < memberAccess.Parts.Length; i++)
        {
            var fieldName = memberAccess.Parts[i];
            var field = typeSymbol.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (field != null)
            {
                resolvedType = field.Type;
            }
            else
            {
                var prop = typeSymbol.Properties.FirstOrDefault(p => p.Name == fieldName);
                if (prop != null)
                {
                    resolvedType = prop.Type;
                }
                else
                {
                    AddError(
                        $"Type '{typeName}' has no member '{fieldName}'",
                        memberAccess.LineStart, memberAccess.ColumnStart,
                        code: DiagnosticCodes.Semantic.UndefinedMember,
                        span: memberAccess.Span);
                    return;
                }
            }
        }

        if (resolvedType != null && !IsAssignable(resolvedType, scrutineeType) && !IsAssignable(scrutineeType, resolvedType))
        {
            AddError(
                $"Pattern type '{resolvedType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: memberAccess.Span);
        }
    }

    /// <summary>
    /// Tries to resolve a pattern type name as a union case of the scrutinee type.
    /// Supports both short form (e.g., "Ok" when scrutinee is Result) and
    /// long form (e.g., "Result.Ok" via dotted name in TypeAnnotation).
    /// Returns the union case TypeSymbol if found, or null otherwise.
    /// </summary>
    private TypeSymbol? TryResolveUnionCaseFromPattern(string typeName, SemanticType scrutineeType)
    {
        var (unionSymbol, _) = GetUnionSymbolAndTypeArgs(scrutineeType);
        if (unionSymbol == null)
            return null;

        // Short form: name matches a union case directly (e.g., "Ok" for Result union)
        var caseSymbol = unionSymbol.UnionCases.FirstOrDefault(c => c.Name == typeName);
        if (caseSymbol != null)
            return caseSymbol;

        // Long form: "UnionName.CaseName" — the TypeAnnotation name includes the dot
        if (typeName.Contains('.', StringComparison.Ordinal))
        {
            var parts = typeName.Split('.');
            if (parts.Length == 2 && parts[0] == unionSymbol.Name)
            {
                return unionSymbol.UnionCases.FirstOrDefault(c => c.Name == parts[1]);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the field types for a type symbol, applying generic type substitution
    /// when the type is a union case with a generic parent union.
    /// </summary>
    private List<SemanticType> GetUnionCaseFieldTypes(TypeSymbol typeSymbol, SemanticType scrutineeType)
    {
        var fieldTypes = typeSymbol.Fields.Select(f => f.Type).ToList();

        // If this is a union case, substitute type parameters from the scrutinee
        if (typeSymbol.BaseType is { TypeKind: TypeKind.Union } unionParent
            && unionParent.TypeParameters.Count > 0)
        {
            var (_, typeArgs) = GetUnionSymbolAndTypeArgs(scrutineeType);
            if (typeArgs != null && typeArgs.Count == unionParent.TypeParameters.Count)
            {
                for (int i = 0; i < fieldTypes.Count; i++)
                {
                    fieldTypes[i] = SubstituteTypeParameters(
                        fieldTypes[i], unionParent.TypeParameters, typeArgs);
                }
            }
        }

        return fieldTypes;
    }

    /// <summary>
    /// Extracts the union TypeSymbol and type arguments from a scrutinee type.
    /// Handles both UserDefinedType (non-generic unions) and GenericType (generic unions).
    /// </summary>
    private (TypeSymbol? UnionSymbol, List<SemanticType>? TypeArgs) GetUnionSymbolAndTypeArgs(
        SemanticType scrutineeType)
    {
        if (scrutineeType is UserDefinedType udt
            && udt.Symbol?.TypeKind == TypeKind.Union)
        {
            return (udt.Symbol, null);
        }

        if (scrutineeType is GenericType gt
            && gt.GenericDefinition?.TypeKind == TypeKind.Union)
        {
            return (gt.GenericDefinition, gt.TypeArguments);
        }

        // OptionalType -> synthetic union with Some(T) and None() cases
        if (scrutineeType is OptionalType optionalType)
        {
            var synth = GetSyntheticOptionalUnion();
            return (synth, new List<SemanticType> { optionalType.UnderlyingType });
        }

        // ResultType -> synthetic union with Ok(T) and Err(E) cases
        if (scrutineeType is ResultType resultType)
        {
            var synth = GetSyntheticResultUnion();
            return (synth, new List<SemanticType> { resultType.OkType, resultType.ErrorType });
        }

        return (null, null);
    }

    private TypeSymbol? _syntheticOptionalUnion;
    private TypeSymbol? _syntheticResultUnion;

    /// <summary>
    /// Returns a synthetic union TypeSymbol for Optional[T] with cases Some(T) and None().
    /// The type parameter T is substituted at pattern-check time via GetUnionCaseFieldTypes.
    /// </summary>
    private TypeSymbol GetSyntheticOptionalUnion()
    {
        if (_syntheticOptionalUnion != null)
            return _syntheticOptionalUnion;

        var tParam = new TypeParameterType { Name = "T" };

        var someCase = new TypeSymbol
        {
            Name = "Some",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>
            {
                new() { Name = "value", Kind = SymbolKind.Variable, Type = tParam, AccessLevel = AccessLevel.Public }
            }
        };

        var noneCase = new TypeSymbol
        {
            Name = "None",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>()
        };

        var optionalUnion = new TypeSymbol
        {
            Name = "Optional",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Union,
            AccessLevel = AccessLevel.Public,
            TypeParameters = new List<TypeParameterDef>
            {
                new() { Name = "T" }
            },
            UnionCases = new List<TypeSymbol> { someCase, noneCase }
        };

        someCase.BaseType = optionalUnion;
        noneCase.BaseType = optionalUnion;

        _syntheticOptionalUnion = optionalUnion;
        return optionalUnion;
    }

    /// <summary>
    /// Returns a synthetic union TypeSymbol for Result[T, E] with cases Ok(T) and Err(E).
    /// The type parameters T and E are substituted at pattern-check time via GetUnionCaseFieldTypes.
    /// </summary>
    private TypeSymbol GetSyntheticResultUnion()
    {
        if (_syntheticResultUnion != null)
            return _syntheticResultUnion;

        var tParam = new TypeParameterType { Name = "T" };
        var eParam = new TypeParameterType { Name = "E" };

        var okCase = new TypeSymbol
        {
            Name = "Ok",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>
            {
                new() { Name = "value", Kind = SymbolKind.Variable, Type = tParam, AccessLevel = AccessLevel.Public }
            }
        };

        var errCase = new TypeSymbol
        {
            Name = "Err",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>
            {
                new() { Name = "error", Kind = SymbolKind.Variable, Type = eParam, AccessLevel = AccessLevel.Public }
            }
        };

        var resultUnion = new TypeSymbol
        {
            Name = "Result",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Union,
            AccessLevel = AccessLevel.Public,
            TypeParameters = new List<TypeParameterDef>
            {
                new() { Name = "T" },
                new() { Name = "E" }
            },
            UnionCases = new List<TypeSymbol> { okCase, errCase }
        };

        okCase.BaseType = resultUnion;
        errCase.BaseType = resultUnion;

        _syntheticResultUnion = resultUnion;
        return resultUnion;
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

    /// <summary>
    /// Recursively type-checks tuple unpacking target elements against their value types.
    /// Handles nested tuple targets like (a, b), c and (a, (b, c)), d.
    /// </summary>
    private void CheckTupleUnpackingElements(ImmutableArray<Expression> targets, IReadOnlyList<SemanticType> valueTypes)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var targetElem = targets[i];
            var valueElemType = valueTypes[i];

            if (targetElem is Identifier tupleTargetId)
            {
                var existingSymbol = _symbolTable.Lookup(tupleTargetId.Name, searchParents: false);

                // Check if trying to reassign a constant
                if (existingSymbol is VariableSymbol varSymbol && varSymbol.IsConstant)
                {
                    AddError($"Cannot reassign constant variable '{tupleTargetId.Name}' in tuple unpacking",
                        tupleTargetId.LineStart, tupleTargetId.ColumnStart, code: DiagnosticCodes.Semantic.InvalidAssignmentTarget,
                        span: tupleTargetId.Span);
                    continue;
                }

                // In Sharpy, tuple unpacking creates new variable versions
                // Create/redefine with inferred type from tuple element
                var newSymbol = new VariableSymbol
                {
                    Name = tupleTargetId.Name,
                    Kind = SymbolKind.Variable,
                    Type = valueElemType,
                    IsConstant = false,
                    DeclarationLine = tupleTargetId.LineStart,
                    DeclarationColumn = tupleTargetId.ColumnStart,
                    AccessLevel = AccessLevel.Public
                };
                _symbolTable.Define(newSymbol);
                SemanticBinding.SetVariableType(newSymbol, valueElemType);
                _semanticInfo.SetIdentifierSymbol(tupleTargetId, newSymbol);
                _semanticInfo.SetExpressionType(tupleTargetId, valueElemType);
                if (valueElemType is UnknownType)
                    MarkExpressionAsErrorRecovery(tupleTargetId);
            }
            else if (targetElem is TupleLiteral nestedTuple)
            {
                // Nested tuple unpacking: (a, b), c = expr
                if (valueElemType is not TupleType nestedTupleType)
                {
                    AddError($"Cannot unpack non-tuple type '{valueElemType.GetDisplayName()}' into nested tuple",
                        targetElem.LineStart, targetElem.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                        span: targetElem.Span);
                    continue;
                }

                if (nestedTuple.Elements.Length != nestedTupleType.ElementTypes.Count)
                {
                    AddError($"Cannot unpack {nestedTupleType.ElementTypes.Count} values into {nestedTuple.Elements.Length} variables",
                        targetElem.LineStart, targetElem.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                        span: targetElem.Span);
                    continue;
                }

                // Recurse into nested tuple
                CheckTupleUnpackingElements(nestedTuple.Elements, nestedTupleType.ElementTypes);
            }
            else
            {
                // For more complex targets (like attributes), just check type compatibility
                var targetElemType = CheckExpression(targetElem);
                if (!IsAssignable(valueElemType, targetElemType))
                {
                    AddError($"Cannot assign type '{valueElemType.GetDisplayName()}' to '{targetElemType.GetDisplayName()}' in tuple unpacking",
                        targetElem.LineStart, targetElem.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: targetElem.Span);
                }
            }
        }
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

    /// <summary>
    /// Type-checks star unpacking patterns: first, *rest = items
    /// The RHS can be a list[T] or tuple[...].
    /// </summary>
    private void CheckStarUnpacking(TupleLiteral targetTuple, SemanticType valueType, Assignment assignment)
    {
        // Validate only one star expression
        int starCount = targetTuple.Elements.Count(e => e is StarExpression);
        if (starCount > 1)
        {
            AddError("Only one starred expression is allowed in an unpacking assignment",
                assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.MultipleStarExpressions,
                span: assignment.Span);
            return;
        }

        // Determine element type from the source
        SemanticType elementType;
        if (valueType is GenericType { Name: BuiltinNames.List } listType && listType.TypeArguments.Count > 0)
        {
            elementType = listType.TypeArguments[0];
        }
        else if (valueType is TupleType tupleType)
        {
            // For tuples, use a common element type (first element's type for simplicity)
            elementType = tupleType.ElementTypes.Count > 0 ? tupleType.ElementTypes[0] : SemanticType.Unknown;
        }
        else
        {
            AddError($"Cannot use starred unpacking with type '{valueType.GetDisplayName()}'",
                assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                span: assignment.Span);
            return;
        }

        // Define variables for each target
        foreach (var targetElem in targetTuple.Elements)
        {
            if (targetElem is StarExpression starExpr && starExpr.Operand is Identifier starId)
            {
                // Starred variable gets list[T] type
                var listTypeForStar = new GenericType
                {
                    Name = BuiltinNames.List,
                    TypeArguments = new List<SemanticType> { elementType }
                };
                var starSymbol = new VariableSymbol
                {
                    Name = starId.Name,
                    Kind = SymbolKind.Variable,
                    Type = listTypeForStar,
                    IsConstant = false,
                    DeclarationLine = starId.LineStart,
                    DeclarationColumn = starId.ColumnStart,
                    AccessLevel = AccessLevel.Public
                };
                _symbolTable.Define(starSymbol);
                SemanticBinding.SetVariableType(starSymbol, listTypeForStar);
                _semanticInfo.SetIdentifierSymbol(starId, starSymbol);
                _semanticInfo.SetExpressionType(starId, listTypeForStar);
                _semanticInfo.SetExpressionType(starExpr, listTypeForStar);
            }
            else if (targetElem is Identifier id)
            {
                var symbol = new VariableSymbol
                {
                    Name = id.Name,
                    Kind = SymbolKind.Variable,
                    Type = elementType,
                    IsConstant = false,
                    DeclarationLine = id.LineStart,
                    DeclarationColumn = id.ColumnStart,
                    AccessLevel = AccessLevel.Public
                };
                _symbolTable.Define(symbol);
                SemanticBinding.SetVariableType(symbol, elementType);
                _semanticInfo.SetIdentifierSymbol(id, symbol);
                _semanticInfo.SetExpressionType(id, elementType);
            }
        }
    }

    /// <summary>
    /// Type check an expression and return its type
    /// </summary>
}
