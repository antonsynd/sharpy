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
                // Special case: Provide helpful error messages for None misuse
                if (initType is VoidType && declaredType is OptionalType)
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
        var narrowedTypesInThen = ExtractNarrowedTypes(ifStmt.Test, true);
        var narrowedTypesInElse = ExtractNarrowedTypes(ifStmt.Test, false);

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

            var narrowedTypesInElif = ExtractNarrowedTypes(elif.Test, true);

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
        var narrowedTypesInBody = ExtractNarrowedTypes(whileStmt.Test, true);

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
        _symbolTable.EnterScope("with");
        _controlFlowDepth++;

        // Type-check each context expression and register 'as' variable bindings
        foreach (var item in withStmt.Items)
        {
            var exprType = CheckExpression(item.ContextExpression);

            // Validate that the expression type implements IDisposable
            if (!IsDisposableType(exprType))
            {
                AddError(
                    $"Type '{exprType.GetDisplayName()}' does not implement IDisposable and cannot be used in a with statement",
                    item.ContextExpression.LineStart,
                    item.ContextExpression.ColumnStart,
                    code: DiagnosticCodes.Semantic.WithNotDisposable,
                    span: item.ContextExpression.Span);
            }

            if (item.Name != null)
            {
                var varSymbol = new VariableSymbol
                {
                    Name = item.Name,
                    Kind = SymbolKind.Variable,
                    Type = exprType,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = item.LineStart,
                    DeclarationColumn = item.ColumnStart
                };

                _symbolTable.Define(varSymbol);
                SemanticBinding.SetVariableType(varSymbol, exprType);
            }
        }

        foreach (var stmt in withStmt.Body)
            CheckStatement(stmt);

        _controlFlowDepth--;
        _symbolTable.ExitScope();
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
                {
                    var resolvedType = _typeResolver.ResolveTypeAnnotation(typePattern.Type);
                    if (resolvedType is UnknownType)
                    {
                        AddError(
                            $"Unknown type '{typePattern.Type.Name}' in type pattern",
                            typePattern.LineStart, typePattern.ColumnStart,
                            code: DiagnosticCodes.Semantic.UndefinedType,
                            span: typePattern.Span);
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
                    break;
                }

            case RelationalPattern relational:
                {
                    var valueType = CheckExpression(relational.Value);
                    if (!IsNumericType(scrutineeType) && scrutineeType is not UnknownType)
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
                    break;
                }

            case PositionalPattern positionalPattern:
                {
                    TypeSymbol? typeSymbol = null;
                    if (positionalPattern.Type != null)
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
                        }
                    }

                    if (typeSymbol != null && positionalPattern.Elements.Length != typeSymbol.Fields.Count)
                    {
                        AddError(
                            $"Positional pattern has {positionalPattern.Elements.Length} elements but type '{typeSymbol.Name}' has {typeSymbol.Fields.Count} fields",
                            positionalPattern.LineStart, positionalPattern.ColumnStart,
                            code: DiagnosticCodes.Semantic.PositionalPatternCountMismatch,
                            span: positionalPattern.Span);
                    }
                    else if (typeSymbol != null)
                    {
                        for (int i = 0; i < positionalPattern.Elements.Length; i++)
                        {
                            var fieldType = typeSymbol.Fields[i].Type;
                            CheckPattern(positionalPattern.Elements[i], fieldType);
                        }
                    }
                    else
                    {
                        foreach (var element in positionalPattern.Elements)
                        {
                            CheckPattern(element, scrutineeType);
                        }
                    }
                    break;
                }

            case MemberAccessPattern memberAccess:
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
                        break;
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
                                break;
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
                    break;
                }

            default:
                AddError(
                    $"Unsupported pattern type '{pattern.GetType().Name}'. This pattern is not yet implemented.",
                    pattern.LineStart, pattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnsupportedFeature);
                break;
        }
    }

    /// <summary>
    /// Checks whether a type implements IDisposable (required for 'with' statement / C# 'using').
    /// </summary>
    private bool IsDisposableType(SemanticType type)
    {
        // Unknown type: skip to avoid cascading errors
        if (type is UnknownType)
            return true;

        // Nullable/Optional: check underlying type
        if (type is NullableType nullable)
            return IsDisposableType(nullable.UnderlyingType);
        if (type is OptionalType optional)
            return IsDisposableType(optional.UnderlyingType);

        // User-defined types: check CLR type or interface list
        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            // Check CLR backing type
            if (udt.Symbol.ClrType != null)
                return typeof(System.IDisposable).IsAssignableFrom(udt.Symbol.ClrType);

            // Check Sharpy interfaces for IDisposable
            var allInterfaces = CollectAllInterfaces(udt.Symbol);
            foreach (var iface in allInterfaces)
            {
                if (iface.Name == "IDisposable")
                    return true;
                if (iface.ClrType != null && typeof(System.IDisposable).IsAssignableFrom(iface.ClrType))
                    return true;
            }

            return false;
        }

        // Generic types backed by a symbol (e.g., List<T>, Dict<K,V>)
        if (type is GenericType gt)
        {
            var sym = _symbolTable.Lookup(gt.Name) as TypeSymbol;
            if (sym?.ClrType != null)
                return typeof(System.IDisposable).IsAssignableFrom(sym.ClrType);
            return false;
        }

        // All other types (builtins, functions, tuples, etc.) are not disposable
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
