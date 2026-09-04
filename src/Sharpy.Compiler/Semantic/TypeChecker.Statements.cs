using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Registry;
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
        // The target is canonical (no redundant parentheses) — see AstHelper.CanonicalizeStoreTarget.
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

            // Type-check each unpacking element (supports nested tuple targets). The RHS element
            // NODES travel with the types so each element is a store the seam can classify by
            // value shape, not a bare type comparison (#1698, #1701).
            CheckTupleUnpackingElements(targetTuple.Elements, tupleType.ElementTypes,
                TupleLiteralElements(assignment.Value));

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

            var storePredecessor = (existingSymbol ?? parentSymbol) as VariableSymbol;
            var storeTarget = storePredecessor != null ? DeclaredBindingType(storePredecessor) : SemanticType.Unknown;
            SemanticType inferredType;
            using (EnterStore(StorePosition.PlainStore, storeTarget, assignment.Value))
                inferredType = CheckExpression(assignment.Value);
            inferredType = CheckLambdaBindingInferable(assignment.Value, inferredType);

            var boundType = NativeCollectionForm(inferredType);
            RecordSequenceMaterialization(assignment.Value, inferredType, boundType);
            inferredType = boundType;

            if (inferredType is VoidType)
            {
                if (storePredecessor != null && storeTarget is not UnknownType)
                {
                    if (IsAssignable(SemanticType.Void, storeTarget))
                    {
                        inferredType = storeTarget;
                    }
                    else
                    {
                        CheckStore(StorePosition.PlainStore, assignment.Value, inferredType, storeTarget,
                            assignment, assignment.Value.Span);
                        return;
                    }
                }
                else if (RefuseUntypedVoidBinding(
                    targetId.Name, assignment.Value, inferredType,
                    assignment.LineStart, assignment.ColumnStart, assignment.Span))
                {
                    return;
                }
            }

            if (storePredecessor != null && storeTarget is not UnknownType
                && inferredType is not UnknownType
                && !IsAssignable(inferredType, storeTarget))
            {
                // R-T: when the target is narrowed and the declared type is a wrapper,
                // classify against the payload first — a payload-accepted value re-wraps.
                var payloadType = storeTarget is OptionalType opt ? opt.UnderlyingType
                    : storeTarget is NullableType { IsValueType: true } nt ? nt.UnderlyingType
                    : (SemanticType?)null;

                if (payloadType != null
                    && HasRemoveNoneFact(targetId.Name)
                    && (IsAssignable(inferredType, payloadType)
                        || IsAcceptedVerdict(ClassifyStore(StorePosition.PlainStore, assignment.Value, inferredType, payloadType))))
                {
                    if (storeTarget is OptionalType wrapOpt)
                        _semanticInfo.SetOptionalStoreWrap(assignment, wrapOpt);
                    inferredType = ClassifyStore(StorePosition.PlainStore, assignment.Value, inferredType, payloadType) switch
                    {
                        StoreVerdict.AcceptedFloat32Narrowing => SemanticType.Float32,
                        StoreVerdict.AcceptedDecimalNarrowing => SemanticType.Decimal,
                        _ => payloadType,
                    };
                }
                else
                {
                    if (!CheckStore(StorePosition.PlainStore, assignment.Value, inferredType, storeTarget,
                            assignment, assignment.Span))
                        return;
                    inferredType = ClassifyStore(StorePosition.PlainStore, assignment.Value, inferredType, storeTarget) switch
                    {
                        StoreVerdict.AcceptedFloat32Narrowing => SemanticType.Float32,
                        StoreVerdict.AcceptedDecimalNarrowing => SemanticType.Decimal,
                        _ => storeTarget,
                    };
                }
            }

            // Create a new variable symbol with the inferred type (or redefine existing)
            var newSymbol = new VariableSymbol
            {
                Name = targetId.Name,
                Kind = SymbolKind.Variable,
                Type = inferredType,
                IsConstant = false,
                IsNameBacktickEscaped = targetId.IsNameBacktickEscaped,
                DeclarationLine = assignment.LineStart,
                DeclarationColumn = assignment.ColumnStart,
                NameDeclarationLine = targetId.LineStart,
                NameDeclarationColumn = targetId.ColumnStart,
                AccessLevel = AccessLevel.Public,
                DeclarationSpan = assignment.Span,
                DeclaringFilePath = _currentFilePath
            };
            _symbolTable.Define(newSymbol);

            // Link the rebinding — same scope or cross-scope write-through (owner ruling option 1).
            var predecessor = (existingSymbol ?? parentSymbol) as VariableSymbol;
            if (predecessor != null && !predecessor.IsConstant && !ReferenceEquals(predecessor, newSymbol))
            {
                _semanticInfo.SetRebindingPredecessor(newSymbol, predecessor);
            }

            var bindingKind = predecessor != null ? TargetBindingKind.Rebinds : TargetBindingKind.Declares;
            _semanticInfo.SetTargetBinding(targetId, new TargetBinding(bindingKind));

            SemanticBinding.SetVariableType(newSymbol, inferredType);
            _semanticInfo.SetIdentifierSymbol(targetId, newSymbol);

            // Cache the expression type for the identifier
            _semanticInfo.SetExpressionType(targetId, inferredType);
            if (inferredType is UnknownType)
            {
                MarkExpressionAsErrorRecovery(targetId,
                    ErrorRecoveryReason.Propagated("the assigned value's type"));
            }
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

        // Check target and value types. An index-access target is checked in STORE position (#1620).
        // A plain-store target is typed by its DECLARATION: a member access in store position takes
        // no read narrowing (`assert b.v is not None; b.v = None` writes the declared `str | None`),
        // so the check and the expectation (`self.x = Some(v)` inside `if self.x is not None:`) both
        // see the declared slot — every receiver, not only `self`, and every declared type (#1706).
        // An augmented target is also read, so it keeps the narrowed read.
        SemanticType targetType;
        using (ScopedValue.Push(ref _indexStoreTarget, IndexStoreTarget.Of(assignment)))
        using (ScopedValue.Push(ref _plainStoreTarget,
            assignment.Operator == AssignmentOperator.Assign ? assignment.Target : null))
            targetType = CheckExpression(assignment.Target);
        var assignmentTargetType = targetType;
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

            var classified = AugmentedCollectionAssignment.Classify(assignment, targetType);

            if (classified is not null
                && _semanticInfo.GetNarrowedReadLowering(assignment.Target)
                    is { Kind: NarrowedReadKind.Cast })
            {
                var targetName = assignment.Target is Identifier id ? id.Name : assignment.Target.ToString();
                AddError(
                    $"Cannot use augmented assignment on isinstance-narrowed receiver '{targetName}' "
                    + $"— rebind through a typed local: `items: list[int] = {targetName}; items += [4]`",
                    assignment.LineStart, assignment.ColumnStart,
                    code: DiagnosticCodes.Semantic.NarrowedReceiverAugAssign,
                    span: assignment.Span);
                return;
            }

            // Collection-mutation path (#1682): validate the RHS against the mutator's Python
            // contract and record the mutation — no InferBinaryOpType needed.
            if (classified is not null)
            {
                if (targetType is not UnknownType && valueType is not UnknownType
                    && !ValidateCollectionMutationRhs(classified, assignment, targetType, valueType))
                {
                    return;
                }
                _semanticInfo.SetAugmentedAssignMutation(assignment, classified.ClrName);
                return;
            }

            // The RHS's value SHAPE converts to the target's type before the operator question —
            // the SAME pre-step the binary site applies (EffectiveOperandTypes, §10.2.11), plus the
            // float32/decimal literal arms the seam admits at every other store position
            // (Decision 6, ruled A). Without the second half, `f: float32 = 1.0; f += 1.0` was
            // SPY0220 and `d: decimal = 1.5; d += 1.5` was SPY0222 while both run at a declaration.
            var effectiveValueType = valueType;
            if (assignment.Operator is not (AssignmentOperator.LeftShiftAssign or AssignmentOperator.RightShiftAssign))
            {
                if (AugmentedBinaryOperator(assignment.Operator) is { } binaryOp)
                {
                    // Only the RIGHT operand can move: the left is the assignment target, which is
                    // never a constant expression (a const cannot be augment-assigned).
                    effectiveValueType = EffectiveOperandTypes(
                        binaryOp, assignment.Target, targetType, assignment.Value, valueType).right;
                }

                var literalVerdict = ClassifyStore(
                    StorePosition.Augmented, assignment.Value, valueType, targetType);
                if (literalVerdict is StoreVerdict.AcceptedFloat32Narrowing
                    or StoreVerdict.AcceptedDecimalNarrowing)
                {
                    ApplyAcceptedVerdict(
                        StorePosition.Augmented, literalVerdict, assignment.Value, valueType, targetType);
                    effectiveValueType = literalVerdict == StoreVerdict.AcceptedFloat32Narrowing
                        ? SemanticType.Float32
                        : SemanticType.Decimal;
                }
            }

            // PEP 675: a LiteralString IS a str at the operator level, and
            // `LiteralString + LiteralString` stays literal (#1731). Asking the operator question of
            // `str` lets the seam decide the RESULT — admitted when the RHS is literal-derived,
            // refused when it is not — instead of refusing the operator itself.
            var operatorTargetType = targetType is LiteralStringType ? SemanticType.Str : targetType;

            var resultType = _typeInference.InferAugmentedAssignmentType(
                assignment.Operator,
                operatorTargetType,
                effectiveValueType);

            if (resultType == null)
            {
                if (targetType is not UnknownType && valueType is not UnknownType)
                {
                    ReportUnsupportedBinaryOperator(assignment,
                        GetAssignmentOperatorSymbol(assignment.Operator), targetType, valueType);
                }
                return;
            }

            // The augmented-narrowing decision — one point, every target kind, every operator
            // (#1666). `TryNarrowAugmentedResult` is C#'s §12.21.4 rule; when it declines, the
            // SPY0220 below is the answer, and it is the same answer for `x8 += i` (a variable
            // RHS) and `x8 += 300` (an out-of-range constant) as it was before the rule existed.
            SemanticType? narrowTo = null;
            if (!IsAssignable(resultType, targetType))
            {
                narrowTo = TryNarrowAugmentedResult(
                    assignment.Operator, targetType, valueType, resultType, assignment.Value);

                if (narrowTo == null)
                {
                    // The seam owns this position's admission AND its refusal (Decision 1): a
                    // result it admits by value shape — a literal-derived `str` into a
                    // `LiteralString` slot — needs no cast, and a refusal carries the seam's steers
                    // under the Augmented message template, which is the text this site used to
                    // format by hand.
                    CheckStore(StorePosition.Augmented, assignment.Value, resultType, targetType,
                        assignment, assignment.Span);
                }
            }

            // Record operator lowering for augmented assignments (#1623).
            if (assignment.Operator == AssignmentOperator.SlashAssign
                && resultType == SemanticType.Double
                && !PrimitiveCatalog.IsDecimal(targetType) && !PrimitiveCatalog.IsDecimal(valueType)
                && !PrimitiveCatalog.IsFloatingPoint(targetType) && !PrimitiveCatalog.IsFloatingPoint(valueType)
                && targetType is not UserDefinedType and not GenericType
                && valueType is not UserDefinedType and not GenericType)
            {
                _semanticInfo.SetOperatorLowering(assignment,
                    new OperatorLowering(OperatorLoweringKind.TrueDivisionCastLeft));
            }

            if (assignment.Operator is AssignmentOperator.LeftShiftAssign or AssignmentOperator.RightShiftAssign
                && TypeUtils.IsInteger(valueType) && valueType != SemanticType.Int
                && targetType is not UserDefinedType and not GenericType)
            {
                _semanticInfo.SetOperatorLowering(assignment,
                    new OperatorLowering(OperatorLoweringKind.ShiftCountCastToInt));
            }

            if (assignment.Operator == AssignmentOperator.NullCoalesceAssign
                && targetType is OptionalType)
            {
                _semanticInfo.SetOperatorLowering(assignment,
                    new OperatorLowering(OperatorLoweringKind.OptionalCoalesceBothOptional));
            }

            // `x *= n` reads the same string-repeat tag family as the binary form (#1623): the
            // target is the string (StrLeft) or the count (StrRight — refused above as a str
            // result assigned to a non-str target, but classified identically all the same).
            if (assignment.Operator == AssignmentOperator.StarAssign && targetType == SemanticType.Str)
            {
                _semanticInfo.SetOperatorLowering(assignment,
                    new OperatorLowering(OperatorLoweringKind.StringRepeatStrLeft));
            }
            else if (assignment.Operator == AssignmentOperator.StarAssign && valueType == SemanticType.Str)
            {
                _semanticInfo.SetOperatorLowering(assignment,
                    new OperatorLowering(OperatorLoweringKind.StringRepeatStrRight));
            }

            if (assignment.Operator == AssignmentOperator.PowerAssign
                && ClassifyIntegerPower(targetType, valueType) is { } powKind)
            {
                _semanticInfo.SetOperatorLowering(assignment, new OperatorLowering(powKind));
            }

            // `//=` and `%=` read the same tags as their binary forms, from the ONE classifier
            // the binary site uses (#1658) — target is the left operand, value the right.
            var flooredOp = assignment.Operator switch
            {
                AssignmentOperator.DoubleSlashAssign => BinaryOperator.FloorDivide,
                AssignmentOperator.PercentAssign => BinaryOperator.Modulo,
                _ => (BinaryOperator?)null,
            };
            if (flooredOp is { } floored
                && ClassifyFlooredArithmetic(floored, targetType, valueType) is { } flooredKind)
            {
                _semanticInfo.SetOperatorLowering(assignment, new OperatorLowering(flooredKind));
            }

            if (narrowTo != null)
            {
                var existing = _semanticInfo.GetOperatorLowering(assignment);
                var kind = existing?.Kind ?? OperatorLoweringKind.Native;
                _semanticInfo.SetOperatorLowering(assignment, new OperatorLowering(kind, narrowTo));
            }

            // R-T: an augmented result on a narrowed Optional identifier re-wraps.
            if (assignment.Target is Identifier augWrapId)
            {
                var augPred = (_symbolTable.Lookup(augWrapId.Name, searchParents: false)
                    ?? _symbolTable.Lookup(augWrapId.Name, searchParents: true)) as VariableSymbol;
                if (augPred != null
                    && DeclaredBindingType(augPred) is OptionalType augWrapOpt
                    && HasRemoveNoneFact(augWrapId.Name))
                {
                    _semanticInfo.SetOptionalStoreWrap(assignment, augWrapOpt);
                }
            }

            return;
        }

        var storePos = assignment.Target is MemberAccess
            ? StorePosition.MemberStore
            : StorePosition.IndexStore;
        CheckStore(storePos, assignment.Value, valueType, assignmentTargetType,
            assignment, assignment.Span);
    }

    /// <summary>
    /// The binary operator an augmented assignment desugars to, or null for the forms that have no
    /// binary twin. Lets the augmented site reuse the binary site's constant pre-step
    /// (<c>EffectiveOperandTypes</c>) so `u64 += 1` and `u32 += 1` decide a constant operand the
    /// way `u64 + 1` does (plan-299c1b Decision 3).
    /// </summary>
    private static BinaryOperator? AugmentedBinaryOperator(AssignmentOperator op) => op switch
    {
        AssignmentOperator.PlusAssign => BinaryOperator.Add,
        AssignmentOperator.MinusAssign => BinaryOperator.Subtract,
        AssignmentOperator.StarAssign => BinaryOperator.Multiply,
        AssignmentOperator.MatMulAssign => BinaryOperator.MatMul,
        AssignmentOperator.SlashAssign => BinaryOperator.Divide,
        AssignmentOperator.DoubleSlashAssign => BinaryOperator.FloorDivide,
        AssignmentOperator.PercentAssign => BinaryOperator.Modulo,
        AssignmentOperator.PowerAssign => BinaryOperator.Power,
        AssignmentOperator.AndAssign => BinaryOperator.BitwiseAnd,
        AssignmentOperator.OrAssign => BinaryOperator.BitwiseOr,
        AssignmentOperator.XorAssign => BinaryOperator.BitwiseXor,
        AssignmentOperator.LeftShiftAssign => BinaryOperator.LeftShift,
        AssignmentOperator.RightShiftAssign => BinaryOperator.RightShift,
        AssignmentOperator.NullCoalesceAssign => BinaryOperator.NullCoalesce,
        _ => null,
    };

    /// <summary>
    /// Validates the RHS of a classified collection-mutation augmented assignment against the
    /// mutator's Python contract (#1682). Returns <c>true</c> when the RHS is accepted;
    /// reports SPY0222 and returns <c>false</c> when it is not.
    /// </summary>
    private bool ValidateCollectionMutationRhs(
        AugmentedCollectionAssignment.AugmentedMutation classified,
        Assignment assignment,
        SemanticType targetType,
        SemanticType valueType)
    {
        var gt = (GenericType)targetType;
        if (gt.TypeArguments.Count == 0)
            return true;

        switch (classified.RhsShape)
        {
            case AugmentedCollectionAssignment.RhsShapeKind.IterableOfElement:
                {
                    var targetElement = gt.TypeArguments[0];
                    var rhsElement = _typeInference.InferIterableElementType(valueType);
                    if (rhsElement == null)
                    {
                        ReportUnsupportedBinaryOperator(assignment,
                            GetAssignmentOperatorSymbol(assignment.Operator), targetType, valueType,
                            messageSuffix: $" — '{classified.PythonName}' requires an iterable");
                        return false;
                    }
                    if (!IsAssignable(rhsElement, targetElement))
                    {
                        AddError(
                            $"Element type '{rhsElement.GetDisplayName()}' of the iterable is not assignable to "
                            + $"'{targetElement.GetDisplayName()}'",
                            assignment.LineStart, assignment.ColumnStart,
                            code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                            span: assignment.Span);
                        return false;
                    }
                    if (ClassifyIterableArgument(valueType) is { } projection)
                        _semanticInfo.SetIterableProjection(assignment.Value, projection);
                    return true;
                }

            case AugmentedCollectionAssignment.RhsShapeKind.ExactInt:
                {
                    // `xs *= n` lowers to InPlaceRepeat(int), so the count is anything a C# `int`
                    // parameter admits — an int8 count is an implicit widening, not a different shape.
                    // Requiring the type to BE `int` refused `n: int8; xs *= n`, which ran before the
                    // shape rule existed (#1682).
                    if (!IsAssignable(valueType, SemanticType.Int))
                    {
                        ReportUnsupportedBinaryOperator(assignment,
                            GetAssignmentOperatorSymbol(assignment.Operator), targetType, valueType,
                            messageSuffix: " — list repetition requires an int count");
                        return false;
                    }
                    return true;
                }

            case AugmentedCollectionAssignment.RhsShapeKind.SetLike:
                {
                    if (valueType is not GenericType { Name: "set" or "frozenset" })
                    {
                        ReportUnsupportedBinaryOperator(assignment,
                            GetAssignmentOperatorSymbol(assignment.Operator), targetType, valueType,
                            messageSuffix: $" — use s.{classified.PythonName}(xs) to update from any iterable");
                        return false;
                    }
                    var targetElement = gt.TypeArguments[0];
                    var rhsGt = (GenericType)valueType;
                    if (rhsGt.TypeArguments.Count > 0 && !IsAssignable(rhsGt.TypeArguments[0], targetElement))
                    {
                        AddError(
                            $"Element type '{rhsGt.TypeArguments[0].GetDisplayName()}' is not assignable to "
                            + $"set element type '{targetElement.GetDisplayName()}'",
                            assignment.LineStart, assignment.ColumnStart,
                            code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                            span: assignment.Span);
                        return false;
                    }
                    return true;
                }

            case AugmentedCollectionAssignment.RhsShapeKind.MappingOrPairs:
                {
                    var targetK = gt.TypeArguments[0];
                    var targetV = gt.TypeArguments.Count > 1 ? gt.TypeArguments[1] : SemanticType.Unknown;

                    if (valueType is GenericType { Name: "dict" } dictRhs && dictRhs.TypeArguments.Count >= 2)
                    {
                        // EXACT type arguments, not assignable ones. `d |= e` lowers to
                        // `Dict.Update(IReadOnlyDictionary<K, V>)`, and that interface is INVARIANT in
                        // both parameters, so a `dict[str, Derived]` cannot bind to a
                        // `dict[str, Base]`'s mutator however assignable the elements are. Accepting it
                        // on element assignability is what produced CS1503 behind SPY0908 (#1682); the
                        // refusal names invariance instead. `d.update(e)` is NOT the steer — it takes
                        // the same invariant interface and is SPY0354 (measured), so the advice is a
                        // per-item loop or an explicit copy.
                        if (!dictRhs.TypeArguments[0].Equals(targetK)
                            || !dictRhs.TypeArguments[1].Equals(targetV))
                        {
                            AddError(
                                $"dict[{dictRhs.TypeArguments[0].GetDisplayName()}, {dictRhs.TypeArguments[1].GetDisplayName()}] "
                                + $"is not assignable to dict[{targetK.GetDisplayName()}, {targetV.GetDisplayName()}]"
                                + " — the merge binds an invariant IReadOnlyDictionary, so the key and"
                                + " value types must match exactly; copy the entries in a loop instead",
                                assignment.LineStart, assignment.ColumnStart,
                                code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                                span: assignment.Span);
                            return false;
                        }
                        return true;
                    }

                    var elemType = _typeInference.InferIterableElementType(valueType);
                    if (elemType is TupleType { ElementTypes.Count: 2 } pair)
                    {
                        if (!IsAssignable(pair.ElementTypes[0], targetK)
                            || !IsAssignable(pair.ElementTypes[1], targetV))
                        {
                            AddError(
                                $"Pair type ({pair.ElementTypes[0].GetDisplayName()}, {pair.ElementTypes[1].GetDisplayName()}) "
                                + $"is not assignable to ({targetK.GetDisplayName()}, {targetV.GetDisplayName()})",
                                assignment.LineStart, assignment.ColumnStart,
                                code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                                span: assignment.Span);
                            return false;
                        }
                        if (ClassifyIterableArgument(valueType) is { } projection)
                            _semanticInfo.SetIterableProjection(assignment.Value, projection);
                        return true;
                    }

                    ReportUnsupportedBinaryOperator(assignment,
                        GetAssignmentOperatorSymbol(assignment.Operator), targetType, valueType,
                        messageSuffix: " — dict |= requires a dict or an iterable of (key, value) pairs");
                    return false;
                }

            default:
                return false;
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

    /// <summary>
    /// Refuses a binding whose value has NO type, and reports true when it did (#1516).
    ///
    /// <para>Two producers, one rule. A bare <c>None</c> with nothing to take a type from
    /// (<c>bar = None</c>) and a call to a <c>None</c>-returning function (<c>x = f()</c>) both give
    /// the binding <see cref="VoidType"/>, which the emitter wrote as <c>var x = null;</c> and
    /// <c>var x = f();</c> — CS0815 either way, behind SPY0908. That is the #1146 contract failing:
    /// the front end accepted a program Roslyn refuses.</para>
    ///
    /// <para><b>Why refusal and not a lowering.</b> The spec defines <c>None</c> only against a
    /// declared type — <c>value: str | None = None</c> emits C# <c>null</c>, <c>x: int? = None</c>
    /// emits <c>default</c> — so bare <c>None</c> is a value whose MEANING comes from its
    /// destination, and a destination that supplies none has no meaning to give it. Inventing one
    /// (<c>object?</c>) would make <c>bar</c> hold a type the user never wrote and cannot use, and
    /// would silently diverge from Axiom 3. CPython's answer (<c>NoneType</c>) is not available to a
    /// statically typed target, and Axiom 1 &gt; Axiom 3 &gt; Axiom 2 settles it.</para>
    ///
    /// <para><b>What must NOT be refused</b>, each measured: <c>x: int? = None</c> and
    /// <c>x: str | None = None</c> (the destination supplies the type); <c>takes(None)</c> (the
    /// parameter does); and <c>x: int? = None; x = None</c> (the existing binding does). Only a NEW
    /// binding with no annotation reaches here.</para>
    ///
    /// <para>Note what is NOT in that list: <c>x = None; x = f()</c> — "the type comes from the
    /// later assignment" — has never worked. It drew SPY0220 ("Cannot assign type 'int' to variable
    /// of type 'None'") before this change and draws this refusal after it, one statement earlier
    /// and naming the real problem.</para>
    /// </summary>
    private bool RefuseUntypedVoidBinding(
        string name, Expression? value, SemanticType valueType,
        int line, int column, Text.TextSpan? span)
    {
        if (valueType is not VoidType)
            return false;

        var reason = value is NoneLiteral
            ? $"'None' names no type on its own, so '{name}' has nothing to be. Annotate the "
                + $"binding with the type it will hold ('{name}: T? = None' for a Sharpy optional, "
                + $"'{name}: T | None = None' for a .NET-nullable reference)"
            : $"this expression produces no value, so there is nothing for '{name}' to hold. Call "
                + "it as a statement, or bind the result of an expression that returns one";

        AddError($"cannot infer a type for '{name}': {reason}",
            line, column, code: DiagnosticCodes.Semantic.CannotInferType, span: span);
        return true;
    }

    private void CheckVariableDeclaration(VariableDeclaration varDecl)
    {
        var declaredType = _typeResolver.ResolveTypeAnnotation(varDecl.Type);

        if (varDecl.InitialValue != null)
        {
            SemanticType initType;
            using (EnterStore(StorePosition.Declaration, declaredType, varDecl.InitialValue))
                initType = CheckExpression(varDecl.InitialValue);

            if (declaredType is UnknownType)
                initType = CheckLambdaBindingInferable(varDecl.InitialValue, initType);

            if (declaredType is UnknownType
                && RefuseUntypedVoidBinding(
                    varDecl.Name, varDecl.InitialValue, initType,
                    varDecl.LineStart, varDecl.ColumnStart, varDecl.Span))
            {
                initType = SemanticType.Unknown;
            }

            if (declaredType is UnknownType)
            {
                declaredType = NativeCollectionForm(initType);
                if (varDecl.Type != null)
                {
                    _semanticInfo.SetTypeAnnotation(varDecl.Type, declaredType);
                }
            }
            else
            {
                CheckStore(StorePosition.Declaration, varDecl.InitialValue, initType, declaredType,
                    varDecl, varDecl.Span);
            }

            if (initType is FunctionType { OptionalParameterCount: > 0 } sourceFt
                && declaredType is FunctionType targetFt
                && targetFt.OptionalParameterCount < sourceFt.OptionalParameterCount)
            {
                _diagnostics.AddWarning(
                    "Default parameters are erased when converting to a function type — " +
                    "callers through this value must provide all arguments",
                    varDecl.Span,
                    varDecl.LineStart, varDecl.ColumnStart,
                    _currentFilePath,
                    DiagnosticCodes.Validation.DefaultsErasedByConversion,
                    CompilerPhase.TypeChecking);
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
                TryFoldConstantValue(existingConst, declaredType, varDecl.InitialValue);
                return;
            }

            // Function-level const - we need to create it
            var constSymbol = new VariableSymbol
            {
                Name = varDecl.Name,
                Kind = SymbolKind.Variable,
                Type = declaredType,
                IsConstant = true,
                IsNameBacktickEscaped = varDecl.IsNameBacktickEscaped,
                DeclarationLine = varDecl.LineStart,
                DeclarationColumn = varDecl.ColumnStart,
                NameDeclarationLine = varDecl.NameLineStart,
                NameDeclarationColumn = varDecl.NameColumnStart,
                NameDeclarationColumnEnd = varDecl.NameColumnEnd,
                DeclarationSpan = varDecl.Span,
                DeclaringFilePath = _currentFilePath
            };
            _symbolTable.Define(constSymbol);
            SemanticBinding.SetVariableType(constSymbol, declaredType);
            _semanticInfo.SetDeclarationSymbol(varDecl, constSymbol);
            _semanticInfo.SetTargetBinding(varDecl, new TargetBinding(TargetBindingKind.Declares));
            TryFoldConstantValue(constSymbol, declaredType, varDecl.InitialValue);
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

        var newSymbol = new VariableSymbol
        {
            Name = varDecl.Name,
            Kind = SymbolKind.Variable,
            Type = declaredType,
            IsConstant = false,
            IsNameBacktickEscaped = varDecl.IsNameBacktickEscaped,
            DeclarationLine = varDecl.LineStart,
            DeclarationColumn = varDecl.ColumnStart,
            NameDeclarationLine = varDecl.NameLineStart,
            NameDeclarationColumn = varDecl.NameColumnStart,
            NameDeclarationColumnEnd = varDecl.NameColumnEnd,
            DeclarationSpan = varDecl.Span,
            DeclaringFilePath = _currentFilePath
        };
        _symbolTable.Define(newSymbol);
        SemanticBinding.SetVariableType(newSymbol, declaredType);
        _semanticInfo.SetDeclarationSymbol(varDecl, newSymbol);
        _semanticInfo.SetTargetBinding(varDecl, new TargetBinding(TargetBindingKind.Declares));

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
            SemanticType returnType;
            using (EnterStore(StorePosition.Return, _currentFunctionReturnType!, returnStmt.Value))
                returnType = CheckExpression(returnStmt.Value);
            CheckStore(StorePosition.Return, returnStmt.Value, returnType, _currentFunctionReturnType!,
                returnStmt, returnStmt.Span);
            if (_currentFunctionReturnType is VoidType && returnType is VoidType)
            {
                var unwrapped = UnwrapParenthesized(returnStmt.Value);
                var kind = unwrapped is NoneLiteral
                    ? ReturnLoweringKind.ElideNoneOperand
                    : ReturnLoweringKind.EvaluateOperandThenReturn;
                _semanticInfo.SetReturnLowering(returnStmt, new ReturnLowering(kind));
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
            SemanticType valueType;
            using (EnterStore(StorePosition.Yield, _currentFunctionReturnType!, yieldStmt.Value))
                valueType = CheckExpression(yieldStmt.Value);

            if (_currentFunctionReturnType != SemanticType.Void
                && _currentFunctionReturnType is not UnknownType)
            {
                CheckStore(StorePosition.Yield, yieldStmt.Value, valueType, _currentFunctionReturnType!,
                    yieldStmt, yieldStmt.Span);
            }
        }
    }

    private void CheckIf(IfStatement ifStmt)
    {
        // Resolve reads in the condition against the facts in effect at the branch point (#1042),
        // so a narrowed value from the enclosing flow is visible in a nested condition.
        _currentFacts = _narrowingFlow?.FactsBeforeBranch(ifStmt.Test) ?? _currentFacts;

        var condType = CheckExpression(ifStmt.Test);
        var (truthTestable, truthLowering) = ClassifyTruthiness(condType);
        if (!truthTestable)
        {
            AddError($"If condition must be boolean, got '{condType.GetDisplayName()}'",
                ifStmt.LineStart, ifStmt.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                span: ifStmt.Test.Span);
        }
        else
        {
            _semanticInfo.SetTruthinessLowering(ifStmt.Test, truthLowering);
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
            var (elifTruthTestable, elifTruthLowering) = ClassifyTruthiness(elifCondType);
            if (!elifTruthTestable)
            {
                AddError($"Elif condition must be boolean, got '{elifCondType.GetDisplayName()}'",
                    elif.LineStart, elif.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: elif.Test.Span);
            }
            else
            {
                _semanticInfo.SetTruthinessLowering(elif.Test, elifTruthLowering);
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
        var (whileTruthTestable, whileTruthLowering) = ClassifyTruthiness(condType);
        if (!whileTruthTestable)
        {
            AddError($"While condition must be boolean, got '{condType.GetDisplayName()}'",
                whileStmt.LineStart, whileStmt.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                span: whileStmt.Test.Span);
        }
        else
        {
            _semanticInfo.SetTruthinessLowering(whileStmt.Test, whileTruthLowering);
        }

        // Body narrowing is applied via CFG facts (#1042); read sites materialize the accessor (#1081).
        _symbolTable.EnterScope("while-body");
        _controlFlowDepth++;
        foreach (var stmt in whileStmt.Body)
            CheckStatement(stmt);
        _controlFlowDepth--;
        _symbolTable.ExitScope();

        // The else body runs once the condition is false without a break. It was never
        // type-checked (#1659): errors in it leaked to the C# compiler and its
        // expression statements carried no StatementLowering.
        if (whileStmt.ElseBody.Length > 0)
        {
            _symbolTable.EnterScope("while-else");
            foreach (var stmt in whileStmt.ElseBody)
                CheckStatement(stmt);
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

        if (iterType == SemanticType.Str)
        {
            _semanticInfo.SetIterationLowering(forStmt.Iterator,
                new IterationLowering(IterationLoweringKind.StringChars));
        }
        else if (iterType is UserDefinedType { Symbol: { TypeKind: TypeKind.Enum } enumSym })
        {
            var kind = enumSym.IsStringEnum
                ? IterationLoweringKind.StringEnumValues
                : IterationLoweringKind.EnumValues;
            _semanticInfo.SetIterationLowering(forStmt.Iterator,
                new IterationLowering(kind));
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
            {
                MarkExpressionAsErrorRecovery(forStmt.Target,
                    ErrorRecoveryReason.Propagated("the iterated sequence's element type"));
            }
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
                IsNameBacktickEscaped = id.IsNameBacktickEscaped,
                AccessLevel = AccessLevel.Public,
                DeclarationLine = id.LineStart,
                DeclarationColumn = id.ColumnStart,
                NameDeclarationLine = id.LineStart,
                NameDeclarationColumn = id.ColumnStart,
                DeclarationSpan = id.Span,
                DeclaringFilePath = _currentFilePath
            };

            _symbolTable.Define(loopVarSymbol);
            SemanticBinding.SetVariableType(loopVarSymbol, elementType);
            _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
            _semanticInfo.SetTargetBinding(id, new TargetBinding(TargetBindingKind.Declares));

            if (RangeYieldsNonNegativeInts(forStmt.Iterator)
                && !IsNameReassignedIn(id.Name, forStmt.Body))
            {
                _nonNegativeInductionVars.Add(loopVarSymbol);
                inductionVarToUnmark = loopVarSymbol;
            }

            _semanticInfo.SetExpressionType(forStmt.Target, elementType);
            if (elementType is UnknownType)
            {
                MarkExpressionAsErrorRecovery(forStmt.Target,
                    ErrorRecoveryReason.Propagated("the iterated sequence's element type"));
            }
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

        // The else body runs once the iterator is exhausted without a break. It was never
        // type-checked (#1659): errors in it leaked to the C# compiler and its
        // expression statements carried no StatementLowering.
        if (forStmt.ElseBody.Length > 0)
        {
            _symbolTable.EnterScope("for-else");
            foreach (var stmt in forStmt.ElseBody)
                CheckStatement(stmt);
            _symbolTable.ExitScope();
        }
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
            if (node.Items.Any(item => item.Target is Identifier id && id.Name == _name))
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
            RequireRaisedValueIsException(raiseStmt.Exception);
        }

        if (raiseStmt.Cause != null)
        {
            CheckExpression(raiseStmt.Cause);
        }
    }

    /// <summary>
    /// Requires the operand of <c>raise</c> to be an exception. Nothing checked this, so
    /// <c>raise b"abc"</c>, <c>raise 42</c> and <c>raise 701.5625</c> all passed semantic analysis
    /// and codegen faithfully emitted <c>throw &lt;non-Exception&gt;</c> — leaving the user a C#
    /// error naming a C# type (CS0029) for a Sharpy-level mistake (#1477, the #1035 class).
    ///
    /// <para>
    /// CPython agrees the program is wrong (<c>TypeError: exceptions must derive from
    /// BaseException</c>), and Axiom 1 makes it a compile-time error rather than a runtime one
    /// because <c>throw</c> requires <c>System.Exception</c>.
    /// </para>
    ///
    /// <para>
    /// Deliberately FAILS OPEN in three cases, so that a check whose job is to catch obvious
    /// nonsense cannot start refusing valid programs: when <c>Exception</c> itself does not
    /// resolve, when the operand's type is unknown (an error is already reported elsewhere), and
    /// when it is a type parameter — <c>raise t</c> for <c>t: T</c> carries no symbol to inherit
    /// from here, and an over-strict answer would be a new false positive rather than a fix.
    /// </para>
    /// </summary>
    private void RequireRaisedValueIsException(Expression raised)
    {
        var exceptionSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("Exception");
        if (exceptionSymbol == null)
        {
            return;
        }

        var raisedType = _semanticInfo.GetExpressionType(raised);
        if (raisedType == null || raisedType is UnknownType or TypeParameterType
            || IsExceptionSubtype(raisedType, exceptionSymbol))
        {
            return;
        }

        AddError(
            $"Cannot raise a value of type '{raisedType.GetDisplayName()}' — 'raise' requires an "
            + "exception, a value whose type derives from 'Exception'",
            raised.LineStart, raised.ColumnStart,
            code: DiagnosticCodes.Semantic.InvalidRaise,
            span: raised.Span);
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

            // Classify the handler's exception type for EVERY handler, bound or not (#1235). Before
            // this the annotation was resolved only when there was an `as` binding, so an unbound
            // `except MyError:` for a generic MyError was never validated at all and reached codegen
            // as an open generic — CS0305 behind SPY0908. Bare `except:` carries no annotation.
            var classifiedExceptionType = handler.IsExceptStar || handler.ExceptionType == null
                ? null
                : ClassifyExceptHandlerType(handler, handler.ExceptionType);

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
                    // The classified type is what the binding takes. For `except (A, B) as e:` that
                    // is the common base the catch clause binds at — previously the binding was typed
                    // as the TUPLE, which emitted `catch (ValueTuple<A, B> e)` (CS0155).
                    exceptionType = classifiedExceptionType
                        ?? (handler.ExceptionType != null
                            // Reached only when classification declined or refused; a bare generic
                            // there already has its own diagnosis, so this fallback must not add the
                            // annotation arity error on top (#1331).
                            ? _typeResolver.ResolveTypeAnnotation(
                                handler.ExceptionType, bareGenericFillsFromContext: true)
                            : _typeResolver.ResolveTypeAnnotation(
                                new TypeAnnotation { Name = "Exception", LineStart = handler.LineStart, ColumnStart = handler.ColumnStart }));
                }

                var varSymbol = new VariableSymbol
                {
                    Name = handler.Name,
                    Kind = SymbolKind.Variable,
                    IsNameBacktickEscaped = handler.IsNameBacktickEscaped,
                    Type = exceptionType,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = handler.LineStart,
                    DeclarationColumn = handler.ColumnStart,
                    NameDeclarationLine = handler.NameLineStart,
                    NameDeclarationColumn = handler.NameColumnStart,
                    NameDeclarationColumnEnd = handler.NameColumnEnd,
                    DeclarationSpan = handler.Span,
                    DeclaringFilePath = _currentFilePath
                };

                if (!TryReportNonVariableRedefinition(handler.Name, handler.LineStart, handler.ColumnStart, handler.Span))
                {
                    _symbolTable.Define(varSymbol);
                    SemanticBinding.SetVariableType(varSymbol, exceptionType);
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

    /// <summary>
    /// Classifies an <c>except</c> handler's exception type and records what codegen must emit for it
    /// (#1235). Returns the type an <c>as</c> binding takes, or null when nothing was decided.
    /// <para>
    /// A handler has <b>no subject</b> — it tests whatever was thrown — so a bare generic name can
    /// never be filled and is always refused (SPY0345), which is what turns the open-generic CS0305
    /// into a diagnostic that names a spelling that works.
    /// </para>
    /// <para>
    /// The tuple spelling is Python's OR-of-exception-types. Unbound, it expands to one catch clause
    /// per element and already worked. <b>Bound</b>, it has to bind somewhere, and C# has no
    /// multi-type catch — so it lowers to a catch at the most specific shared base with an
    /// is-alternation filter. That base comes from <see cref="FindCommonExceptionBase"/>, the same
    /// helper <c>try[A | B]</c> uses for its Result error type: the two spellings ask the same
    /// question and must not answer it from two places. CPython semantics verified with python3 —
    /// the first textually matching handler wins, and <c>e</c> is bound to the raised instance —
    /// which is exactly how C# orders catch clauses and evaluates their filters.
    /// </para>
    /// <para>
    /// <b>There is deliberately no Exception-derivation check here.</b> One was added with this
    /// lowering and then <b>withdrawn</b> — it was removed because it is unsound, not because it was
    /// unwanted. Under <c>--incremental</c> a dependency served from the symbol cache comes back with
    /// its base chain missing (#1309), so the check refused <i>valid</i> user exception types on any
    /// build where their defining file was cached.
    /// </para>
    /// <para>
    /// <b>Restored</b> after #1309's fix: the incremental cache now serializes real symbols with
    /// <c>UnresolvedBaseName</c>, so the Phase 4b/4c inheritance machinery resolves CLR bases like
    /// <c>Exception</c> on warm builds. The check is sound and SPY0399 fires correctly.
    /// </para>
    /// </summary>
    private SemanticType? ClassifyExceptHandlerType(ExceptHandler handler, TypeAnnotation annotation)
    {
        var exceptionSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("Exception");

        if (annotation.Name == BuiltinNames.Tuple && annotation.TypeArguments.Length > 0)
        {
            var alternatives = new List<SemanticType>(annotation.TypeArguments.Length);
            foreach (var element in annotation.TypeArguments)
            {
                // Each element is classified in its own right, so the unbound form's per-element
                // catch expansion has a recorded type to read for every clause it emits.
                var resolvedElement = ClassifyTypeTestAnnotation(
                    element, lodgeOn: element, subjectType: null,
                    siteNoun: "except clause", erasure: CollectionErasure.Disallowed);
                if (resolvedElement == null)
                    return null;

                RequireExceptionDerivation(element, resolvedElement, exceptionSymbol);
                alternatives.Add(resolvedElement);
            }

            if (handler.Name == null)
            {
                // Unbound: the emitter expands one catch clause per element, so there is nothing for
                // an alternation to bind and recording one would describe a lowering nobody applies.
                return null;
            }

            var commonBase = ClampToExceptionBase(
                FindCommonExceptionBase(alternatives, exceptionSymbol), exceptionSymbol);
            _semanticInfo.SetTypeTestLowering(
                annotation,
                new TypeTestLowering(TypeTestLoweringKind.ExceptionAlternation, commonBase, alternatives));
            return commonBase;
        }

        var resolved = ClassifyTypeTestAnnotation(
            annotation, lodgeOn: annotation, subjectType: null,
            siteNoun: "except clause", erasure: CollectionErasure.Disallowed);
        if (resolved != null)
            RequireExceptionDerivation(annotation, resolved, exceptionSymbol);
        return resolved;
    }

    /// <summary>
    /// Requires an <c>except</c> clause's type to derive from <c>Exception</c>.
    /// Fails open when <c>Exception</c> cannot be resolved, matching <c>try[E]</c>.
    /// Withdrawn in <c>6bd193925</c> because #1309 made inherited types invisible under warm
    /// caches — restored now that the incremental cache serializes real symbols (SPY0399).
    /// </summary>
    private void RequireExceptionDerivation(TypeAnnotation at, SemanticType resolved, TypeSymbol? exceptionSymbol)
    {
        if (exceptionSymbol == null || resolved is UnknownType || IsExceptionSubtype(resolved, exceptionSymbol))
            return;

        AddError(
            $"Type '{at.Name}' in an 'except' clause must be a subclass of 'Exception'",
            at.LineStart, at.ColumnStart,
            code: DiagnosticCodes.Semantic.TryExceptionTypeNotException,
            span: at.Span);
    }

    /// <summary>
    /// Belt-and-braces guard on the alternation's catch type: a common base that is not an
    /// <c>Exception</c> subtype becomes <c>Exception</c>, so <c>catch (object e)</c> — which is not
    /// legal C# — is unreachable. <see cref="RequireExceptionDerivation"/> enforces this at type-check
    /// time; this clamp is the codegen-time backstop that needs no inheritance data to be safe.
    /// </summary>
    private static SemanticType ClampToExceptionBase(SemanticType candidate, TypeSymbol? exceptionSymbol)
        => exceptionSymbol != null && !IsExceptionSubtype(candidate, exceptionSymbol)
            ? new UserDefinedType { Name = "Exception", Symbol = exceptionSymbol }
            : candidate;

    private void CheckWith(WithStatement withStmt)
    {
        if (withStmt.IsAsync && !_currentFunctionIsAsync)
        {
            AddError("'async with' can only be used inside 'async def' functions",
                withStmt.LineStart, withStmt.ColumnStart,
                code: DiagnosticCodes.Semantic.AwaitOutsideAsync, span: withStmt.Span);
        }

        // `assert_raises` is a marker with no runtime — `Unittest.AssertRaises` throws
        // NotSupportedException and its Dispose is empty by design. It works because the emitter
        // rewrites `with assert_raises(E):` away entirely. That rewrite used to name Xunit, so it
        // only ran inside a @test function and SPY0494 refused the form anywhere else (#1283); it
        // now lowers to a flag, a try/catch and a `Sharpy.AssertionError`, which any function can
        // hold, so the restriction is gone and SPY0494 is retired (#1413).

        // For `with assert_raises(E) as exc:`, define the capture variable in the enclosing scope so
        // it's accessible after the with block: codegen emits the capture as a flat statement there
        // rather than inside a block, in every function.
        if (withStmt.Items.Length == 1 && withStmt.Items[0].Target is Identifier arId
            && IsAssertRaisesExpression(withStmt.Items[0].ContextExpression))
        {
            var item = withStmt.Items[0];
            CheckExpression(item.ContextExpression);

            var exceptionType = ResolveAssertRaisesExceptionType(item.ContextExpression);
            var varSymbol = new VariableSymbol
            {
                Name = arId.Name,
                Kind = SymbolKind.Variable,
                IsNameBacktickEscaped = arId.IsNameBacktickEscaped,
                Type = exceptionType,
                AccessLevel = AccessLevel.Public,
                DeclarationLine = item.LineStart,
                DeclarationColumn = item.ColumnStart,
                NameDeclarationLine = arId.LineStart,
                NameDeclarationColumn = arId.ColumnStart,
                NameDeclarationColumnEnd = arId.ColumnEnd,
                DeclarationSpan = item.Span,
                DeclaringFilePath = _currentFilePath
            };
            if (!TryReportNonVariableRedefinition(arId.Name, item.LineStart, item.ColumnStart, item.Span))
            {
                _symbolTable.Define(varSymbol);
                SemanticBinding.SetVariableType(varSymbol, exceptionType);
                _semanticInfo.SetWithItemSymbol(item, varSymbol);
                _semanticInfo.SetIdentifierSymbol(arId, varSymbol);
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
            if (item.Target is Identifier withId && !IsAssertRaisesExpression(item.ContextExpression))
            {
                var varSymbol = new VariableSymbol
                {
                    Name = withId.Name,
                    Kind = SymbolKind.Variable,
                    IsNameBacktickEscaped = withId.IsNameBacktickEscaped,
                    Type = asVarType,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = item.LineStart,
                    DeclarationColumn = item.ColumnStart,
                    NameDeclarationLine = withId.LineStart,
                    NameDeclarationColumn = withId.ColumnStart,
                    NameDeclarationColumnEnd = withId.ColumnEnd,
                    DeclarationSpan = item.Span,
                    DeclaringFilePath = _currentFilePath
                };

                if (!TryReportNonVariableRedefinition(withId.Name, item.LineStart, item.ColumnStart, item.Span))
                {
                    _symbolTable.Define(varSymbol);
                    SemanticBinding.SetVariableType(varSymbol, asVarType);
                    _semanticInfo.SetWithItemSymbol(item, varSymbol);
                    _semanticInfo.SetIdentifierSymbol(withId, varSymbol);
                }
            }
            // `with CM() as (a, b):` binds the names of a tuple target, exactly as `for a, b in …`
            // does — python3 binds a=1, b=2 when __enter__ returns (1, "two"). The parser accepts
            // the target and the emitter already lowers it (GenerateStore's TupleLiteral arm), so
            // the binder is the only piece that was missing: without it every name in the target
            // was SPY0200 "Undefined identifier" at its first use (#1672 E2). The shared tuple
            // binder is the same one the for-statement uses, so nesting and per-element error
            // recovery behave identically.
            else if (item.Target is TupleLiteral withTuple && !IsAssertRaisesExpression(item.ContextExpression))
            {
                if (asVarType is not TupleType asTupleType)
                {
                    AddError($"Cannot unpack non-tuple type '{asVarType.GetDisplayName()}' into tuple target in with statement",
                        withTuple.LineStart, withTuple.ColumnStart,
                        code: DiagnosticCodes.Semantic.InvalidTupleUnpacking, span: withTuple.Span);
                }
                else if (withTuple.Elements.Length != asTupleType.ElementTypes.Count)
                {
                    AddError($"Cannot unpack {asTupleType.ElementTypes.Count} values into {withTuple.Elements.Length} variables in with statement",
                        withTuple.LineStart, withTuple.ColumnStart,
                        code: DiagnosticCodes.Semantic.InvalidTupleUnpacking, span: withTuple.Span);
                }
                else
                {
                    DefineForLoopTupleTargets(withTuple.Elements, asTupleType.ElementTypes);
                    _semanticInfo.SetExpressionType(withTuple, asTupleType);
                }
            }
            else if (item.Target != null && !IsAssertRaisesExpression(item.ContextExpression))
            {
                CheckExpression(item.Target);
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

            case MatchStatement matchStmt:
                foreach (var matchCase in matchStmt.Cases)
                    foreach (var s in matchCase.Body)
                        CheckDeferBodyControlFlow(s, insideLoop);
                return;

            case DecoratedStatement decorated:
                CheckDeferBodyControlFlow(decorated.Statement, insideLoop);
                return;

            // FunctionDef / ClassDef / lambdas open their own scope: a return inside them
            // belongs to that scope, not the deferred block, so we stop here.
            default:
                return;
        }
    }

    private static bool IsAssertRaisesExpression(Expression expr)
        => AssertRaisesForm.IsCall(UnwrapParenthesized(expr));

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
        SemanticType testType = CheckExpression(assertStmt.Test);

        // The same condition rule `if`/`while` enforce, which `assert` was missing: Sharpy has no
        // implicit truthiness, so a non-boolean test is an error rather than an emptiness check.
        // Without it, codegen emitted `if (!(<non-bool>))` and Roslyn reported CS0023 "Operator '!'
        // cannot be applied to operand of type 'Bytes'" — a C# error naming a C# type, for a
        // Sharpy-level mistake (#1485, the #1035 class). Skipped inside a @test function, where the
        // emitter rewrites the whole assert into a framework assertion and the test expression is
        // deliberately not lowered as an ordinary boolean expression.
        if (!_inTestFunction)
        {
            var (assertTruthTestable, assertTruthLowering) = ClassifyTruthiness(testType);
            if (!assertTruthTestable)
            {
                AddError($"Assert condition must be boolean, got '{testType.GetDisplayName()}'",
                    assertStmt.LineStart, assertStmt.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: assertStmt.Test.Span);
            }
            else
            {
                _semanticInfo.SetTruthinessLowering(assertStmt.Test, assertTruthLowering);
            }
        }

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

                _symbolTable.Define(loopVarSymbol);
                SemanticBinding.SetVariableType(loopVarSymbol, elemType);
                _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
                _semanticInfo.SetTargetBinding(id, new TargetBinding(TargetBindingKind.Declares));

                _semanticInfo.SetExpressionType(targetElem, elemType);
                if (elemType is UnknownType)
                {
                    MarkExpressionAsErrorRecovery(targetElem,
                        ErrorRecoveryReason.Propagated("the unpacked tuple element's type"));
                }
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

    private void TryFoldConstantValue(
        VariableSymbol symbol, SemanticType declaredType, Expression? initializer)
    {
        if (initializer == null)
            return;

        var info = Registry.PrimitiveCatalog.GetPrimitiveInfo(declaredType);
        if (info?.Kind is not (Registry.PrimitiveCatalog.NumericKind.SignedInteger
                or Registry.PrimitiveCatalog.NumericKind.UnsignedInteger))
            return;

        System.Numerics.BigInteger? ResolveConstant(Identifier id)
        {
            var sym = _symbolTable.Lookup(id.Name);
            if (sym != null && id.IsNameBacktickEscaped != sym.IsNameBacktickEscaped)
                return null;
            return sym is VariableSymbol { IsConstant: true, ConstantValue: not null } vs
                ? vs.ConstantValue
                : null;
        }

        if (IntegerConstantEvaluator.TryGetConstantInteger(initializer, out var folded, ResolveConstant))
            symbol.ConstantValue = folded;
    }
}
