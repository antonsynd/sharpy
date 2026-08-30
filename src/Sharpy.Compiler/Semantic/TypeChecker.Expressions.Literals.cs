using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Collection literals, comprehensions, f-strings, slicing
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// Tries to infer the type of an empty collection literal from the expected type context.
    /// Returns the inferred GenericType if successful, or null if no contextual type is available
    /// (after emitting an error diagnostic).
    /// </summary>
    private SemanticType? TryInferEmptyCollectionType(
        string collectionName, int expectedArgCount, Expression node, string errorHint)
    {
        if (_expectedType is GenericType expected
            && expected.Name == collectionName
            && expected.TypeArguments.Count == expectedArgCount)
        {
            return new GenericType
            {
                Name = collectionName,
                TypeArguments = expected.TypeArguments.ToList()
            };
        }

        AddError(
            $"Cannot infer type of empty {collectionName} literal; add a type annotation (e.g., {errorHint})",
            node.LineStart, node.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
            span: node.Span);
        return null;
    }

    /// <summary>
    /// Guards against a <see cref="VoidType"/> collection element/value type, which arises when
    /// every element is the <c>None</c> literal and no contextual element type was available
    /// (e.g. bare <c>[None]</c>, <c>{None}</c>, <c>{"k": None}</c>). Emitting <c>List&lt;void&gt;</c>
    /// produces invalid C#, so report <see cref="DiagnosticCodes.Semantic.CannotInferType"/> and fall
    /// back to <see cref="SemanticType.Unknown"/>. Callers must apply any contextual-type resolution
    /// before calling this, so a surviving <c>Void</c> is genuinely un-inferable (#950).
    /// </summary>
    private SemanticType ResolveVoidElementType(
        SemanticType elementType, string collectionName, Expression node, string annotationHint)
    {
        if (elementType is not VoidType)
            return elementType;

        AddError(
            $"Cannot infer element type from a {collectionName} of only 'None'; add a type annotation (e.g., {annotationHint})",
            node.LineStart, node.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
            span: node.Span);
        return SemanticType.Unknown;
    }

    private SemanticType CheckListLiteral(ListLiteral list)
    {
        if (list.Elements.Length == 0)
        {
            return TryInferEmptyCollectionType(
                BuiltinNames.List, 1, list, "x: list[int] = []") ?? SemanticType.Unknown;
        }

        SemanticType? elementExpectation = null;
        if (_expectedType is GenericType { Name: BuiltinNames.List, TypeArguments.Count: 1 } expectedList)
            elementExpectation = expectedList.TypeArguments[0];

        var elementTypes = new List<SemanticType>();
        foreach (var elem in list.Elements)
        {
            if (elem is SpreadElement spread)
            {
                var spreadType = CheckExpression(spread.Value);
                // Extract element type from the spread iterable
                if (spreadType is GenericType { Name: BuiltinNames.List or BuiltinNames.Set or BuiltinNames.Array } gt && gt.TypeArguments.Count > 0)
                    elementTypes.Add(gt.TypeArguments[0]);
                else if (spreadType is TupleType tupleSpread)
                    elementTypes.AddRange(tupleSpread.ElementTypes);
                else
                    elementTypes.Add(spreadType);
            }
            else
            {
                var previousExpected = _expectedType;
                _expectedType = elementExpectation;
                elementTypes.Add(CheckExpression(elem));
                _expectedType = previousExpected;
            }
        }

        var commonType = FindLeastCommonAncestor(elementTypes);

        // When a contextual element type is available and every element is assignable to it,
        // record the expected type. This handles covariant assignments (list[Base] = [Derived()])
        // and depth > 1 contextual inference (#1671).
        if (elementExpectation != null && AllAssignableTo(elementTypes, elementExpectation))
            commonType = elementExpectation;

        commonType = ResolveVoidElementType(
            commonType, BuiltinNames.List, list, "list[object]");

        return new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType> { commonType },
            GenericDefinition = _symbolTable.BuiltinRegistry.GetType(BuiltinNames.List)
        };
    }

    private SemanticType CheckDictLiteral(DictLiteral dict)
    {
        if (dict.Entries.Length == 0)
        {
            return TryInferEmptyCollectionType(
                BuiltinNames.Dict, 2, dict, "d: dict[str, int] = {}") ?? SemanticType.Unknown;
        }

        SemanticType? keyExpectation = null;
        SemanticType? valueExpectation = null;
        if (_expectedType is GenericType { Name: BuiltinNames.Dict, TypeArguments.Count: 2 } expectedDict)
        {
            keyExpectation = expectedDict.TypeArguments[0];
            valueExpectation = expectedDict.TypeArguments[1];
        }

        var keyTypes = new List<SemanticType>();
        var valueTypes = new List<SemanticType>();
        foreach (var entry in dict.Entries)
        {
            if (entry.Key == null)
            {
                // Dict spread: **other_dict — extract K, V from dict[K, V]
                var spreadType = CheckExpression(entry.Value);
                if (spreadType is GenericType { Name: BuiltinNames.Dict } gt && gt.TypeArguments.Count == 2)
                {
                    keyTypes.Add(gt.TypeArguments[0]);
                    valueTypes.Add(gt.TypeArguments[1]);
                }
            }
            else
            {
                var previousExpected = _expectedType;
                _expectedType = keyExpectation;
                keyTypes.Add(CheckExpression(entry.Key));
                _expectedType = valueExpectation;
                valueTypes.Add(CheckExpression(entry.Value));
                _expectedType = previousExpected;
            }
        }

        var commonKeyType = FindLeastCommonAncestor(keyTypes);
        var commonValueType = FindLeastCommonAncestor(valueTypes);

        // When a contextual key/value type is available and every element is assignable,
        // record the expected type (#1671).
        if (keyExpectation != null && AllAssignableTo(keyTypes, keyExpectation))
            commonKeyType = keyExpectation;
        if (valueExpectation != null && AllAssignableTo(valueTypes, valueExpectation))
            commonValueType = valueExpectation;

        commonKeyType = ResolveVoidElementType(
            commonKeyType, BuiltinNames.Dict, dict, "dict[str, object]");
        commonValueType = ResolveVoidElementType(
            commonValueType, BuiltinNames.Dict, dict, "dict[str, object]");

        return new GenericType
        {
            Name = BuiltinNames.Dict,
            TypeArguments = new List<SemanticType> { commonKeyType, commonValueType },
            GenericDefinition = _symbolTable.BuiltinRegistry.GetType(BuiltinNames.Dict)
        };
    }

    private SemanticType CheckSetLiteral(SetLiteral set)
    {
        if (set.Elements.Length == 0)
        {
            return TryInferEmptyCollectionType(
                BuiltinNames.Set, 1, set, "s: set[int] = set()") ?? SemanticType.Unknown;
        }

        SemanticType? elementExpectation = null;
        if (_expectedType is GenericType { Name: BuiltinNames.Set, TypeArguments.Count: 1 } expectedSet)
            elementExpectation = expectedSet.TypeArguments[0];

        var elementTypes = new List<SemanticType>();
        foreach (var elem in set.Elements)
        {
            if (elem is SpreadElement spread)
            {
                var spreadType = CheckExpression(spread.Value);
                if (spreadType is GenericType { Name: BuiltinNames.List or BuiltinNames.Set or BuiltinNames.Array } gt && gt.TypeArguments.Count > 0)
                    elementTypes.Add(gt.TypeArguments[0]);
                else if (spreadType is TupleType tupleSpread)
                    elementTypes.AddRange(tupleSpread.ElementTypes);
                else
                    elementTypes.Add(spreadType);
            }
            else
            {
                var previousExpected = _expectedType;
                _expectedType = elementExpectation;
                elementTypes.Add(CheckExpression(elem));
                _expectedType = previousExpected;
            }
        }

        var commonType = FindLeastCommonAncestor(elementTypes);

        if (elementExpectation != null && AllAssignableTo(elementTypes, elementExpectation))
            commonType = elementExpectation;

        commonType = ResolveVoidElementType(
            commonType, BuiltinNames.Set, set, "set[object]");

        return new GenericType
        {
            Name = BuiltinNames.Set,
            TypeArguments = new List<SemanticType> { commonType },
            GenericDefinition = _symbolTable.BuiltinRegistry.GetType(BuiltinNames.Set)
        };
    }

    private SemanticType CheckTupleLiteral(TupleLiteral tuple)
    {
        var hasSpread = tuple.Elements.Any(e => e is SpreadElement);

        if (hasSpread)
        {
            var elementTypes = new List<SemanticType>();
            foreach (var elem in tuple.Elements)
            {
                if (elem is SpreadElement spread)
                {
                    var spreadType = CheckExpression(spread.Value);
                    if (spreadType is TupleType tupleSpread)
                    {
                        elementTypes.AddRange(tupleSpread.ElementTypes);
                    }
                    else
                    {
                        AddError(
                            $"Cannot spread non-tuple type '{spreadType.GetDisplayName()}' into tuple literal; spread target must be a tuple with known arity",
                            spread.LineStart, spread.ColumnStart,
                            code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                            span: spread.Span);
                        elementTypes.Add(SemanticType.Unknown);
                    }
                }
                else
                {
                    elementTypes.Add(CheckExpression(elem));
                }
            }

            return new TupleType { ElementTypes = elementTypes };
        }

        List<SemanticType>? indexExpectations = null;
        if (_expectedType is TupleType expectedTuple
            && expectedTuple.ElementTypes.Count == tuple.Elements.Length)
        {
            indexExpectations = expectedTuple.ElementTypes;
        }

        var directElementTypes = new List<SemanticType>(tuple.Elements.Length);
        for (int i = 0; i < tuple.Elements.Length; i++)
        {
            var previousExpected = _expectedType;
            _expectedType = indexExpectations?[i];
            directElementTypes.Add(CheckExpression(tuple.Elements[i]));
            _expectedType = previousExpected;
        }

        var tupleType = new TupleType { ElementTypes = directElementTypes };

        // Propagate element names for named tuple literals
        if (!tuple.ElementNames.IsEmpty)
        {
            tupleType = tupleType with { ElementNames = tuple.ElementNames };
        }

        return tupleType;
    }

    /// <summary>
    /// The element expectation a comprehension of kind <paramref name="collectionName"/> takes from
    /// the enclosing context, or <c>null</c> when the context expects something else (or nothing).
    ///
    /// <para>A comprehension is a collection literal whose elements are written once, so it obeys
    /// the SAME contextual-typing rule as <c>[…]</c>, <c>{…}</c> and <c>{k: v}</c> (#1671): when the
    /// expected type is a same-kind collection and the produced element is assignable to its
    /// element type, the EXPECTED type is what gets recorded. Without it,
    /// <c>xs: list[Base] = [Derived() for _ in range(2)]</c> recorded <c>list[Derived]</c> and
    /// emitted <c>List&lt;Derived&gt;</c> into a <c>List&lt;Base&gt;</c> slot — CS0029 behind
    /// SPY0908 for list and set, SPY0220 for dict. The expectation is read BEFORE the comprehension
    /// scope's own expressions are checked and is captured here so the clause walk (whose iterables
    /// are not the comprehension's elements) cannot be typed by it.</para>
    /// </summary>
    private IReadOnlyList<SemanticType>? ComprehensionElementExpectations(
        string collectionName, int expectedArgCount)
    {
        return _expectedType is GenericType expected
            && expected.Name == collectionName
            && expected.TypeArguments.Count == expectedArgCount
            ? expected.TypeArguments
            : null;
    }

    /// <summary>
    /// Records <paramref name="expectation"/> as the comprehension's element type when the produced
    /// element is assignable to it; otherwise keeps the produced type so assignability decides
    /// (a mistyped comprehension stays SPY0220). The comprehension twin of the collection literals'
    /// <c>AllAssignableTo</c> arm (#1671).
    /// </summary>
    private SemanticType ContextualElementType(SemanticType produced, SemanticType? expectation)
    {
        return expectation != null && IsAssignable(produced, expectation) ? expectation : produced;
    }

    private SemanticType CheckListComprehension(ListComprehension listComp)
    {
        var expectations = ComprehensionElementExpectations(BuiltinNames.List, 1);

        _symbolTable.EnterScope("list-comprehension");
        CheckComprehensionClauses(listComp.Clauses);

        SemanticType elementType;
        if (listComp.Element is SpreadElement spread)
        {
            // [*it for it in its] — result type is the inner element type of the spread value
            var spreadType = CheckExpression(spread);  // caches type for spread node
            elementType = _typeInference.InferIterableElementType(spreadType) ?? SemanticType.Unknown;
        }
        else
        {
            var previousExpected = _expectedType;
            _expectedType = expectations?[0];
            elementType = CheckExpression(listComp.Element);
            _expectedType = previousExpected;
        }

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType>
            {
                ContextualElementType(elementType, expectations?[0])
            }
        };
    }

    private SemanticType CheckSetComprehension(SetComprehension setComp)
    {
        var expectations = ComprehensionElementExpectations(BuiltinNames.Set, 1);

        _symbolTable.EnterScope("set-comprehension");
        CheckComprehensionClauses(setComp.Clauses);

        SemanticType elementType;
        if (setComp.Element is SpreadElement spread)
        {
            // {*it for it in its} — result type is the inner element type of the spread value
            var spreadType = CheckExpression(spread);  // caches type for spread node
            elementType = _typeInference.InferIterableElementType(spreadType) ?? SemanticType.Unknown;
        }
        else
        {
            var previousExpected = _expectedType;
            _expectedType = expectations?[0];
            elementType = CheckExpression(setComp.Element);
            _expectedType = previousExpected;
        }

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = BuiltinNames.Set,
            TypeArguments = new List<SemanticType>
            {
                ContextualElementType(elementType, expectations?[0])
            }
        };
    }

    private SemanticType CheckDictComprehension(DictComprehension dictComp)
    {
        var expectations = ComprehensionElementExpectations(BuiltinNames.Dict, 2);

        _symbolTable.EnterScope("dict-comprehension");
        CheckComprehensionClauses(dictComp.Clauses);

        var previousExpected = _expectedType;
        _expectedType = expectations?[0];
        var keyType = CheckExpression(dictComp.Key);
        _expectedType = expectations?[1];
        var valueType = CheckExpression(dictComp.Value);
        _expectedType = previousExpected;

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = BuiltinNames.Dict,
            TypeArguments = new List<SemanticType>
            {
                ContextualElementType(keyType, expectations?[0]),
                ContextualElementType(valueType, expectations?[1])
            }
        };
    }

    private SemanticType CheckDictSpreadComprehension(DictSpreadComprehension dictSpreadComp)
    {
        // {**d for d in dicts} — result type is dict[K, V] from the spread value type
        _symbolTable.EnterScope("dict-spread-comprehension");
        CheckComprehensionClauses(dictSpreadComp.Clauses);

        var spreadType = CheckExpression(dictSpreadComp.Spread);

        _symbolTable.ExitScope();

        if (spreadType is GenericType { Name: "dict" } gType && gType.TypeArguments.Count >= 2)
        {
            return new GenericType
            {
                Name = BuiltinNames.Dict,
                TypeArguments = new List<SemanticType> { gType.TypeArguments[0], gType.TypeArguments[1] }
            };
        }

        return new GenericType
        {
            Name = BuiltinNames.Dict,
            TypeArguments = new List<SemanticType> { SemanticType.Unknown, SemanticType.Unknown }
        };
    }

    /// <summary>
    /// Processes comprehension clauses (ForClause and IfClause), defining loop variables
    /// and validating filter conditions. This is shared logic used by list, set, and dict
    /// comprehensions.
    /// </summary>
    /// <param name="clauses">The comprehension clauses to process</param>
    private void CheckComprehensionClauses(IReadOnlyList<ComprehensionClause> clauses)
    {
        foreach (var clause in clauses)
        {
            switch (clause)
            {
                case ForClause forClause:
                    CheckComprehensionForClause(forClause);
                    break;

                case IfClause ifClause:
                    CheckComprehensionIfClause(ifClause);
                    break;
            }
        }
    }

    /// <summary>
    /// Processes a for clause in a comprehension, checking the iterator type and
    /// defining the loop variable in the current scope.
    /// </summary>
    private void CheckComprehensionForClause(ForClause forClause)
    {
        if (forClause.IsAsync && !_currentFunctionIsAsync)
        {
            AddError("'async for' can only be used inside 'async def' functions",
                forClause.LineStart, forClause.ColumnStart,
                code: DiagnosticCodes.Semantic.AwaitOutsideAsync, span: forClause.Span);
        }

        // Check iterator type and infer element type (errors reported by validator in pipeline)
        SemanticType iterType;
        using (ScopedValue.Push(ref _currentIterationSource, forClause.Iterator))
            iterType = CheckExpression(forClause.Iterator);

        // Enum type used as iterable in comprehension: `[c.name for c in Color]`
        if (iterType is UnknownType && forClause.Iterator is Identifier enumId)
        {
            var sym = _symbolTable.Lookup(enumId.Name);
            if (sym is TypeSymbol { TypeKind: TypeKind.Enum } enumTypeSym)
            {
                iterType = new UserDefinedType { Name = enumTypeSym.Name, Symbol = enumTypeSym };
                _semanticInfo.SetExpressionType(forClause.Iterator, iterType);
            }
        }

        if (iterType == SemanticType.Str)
        {
            _semanticInfo.SetIterationLowering(forClause.Iterator,
                new IterationLowering(IterationLoweringKind.StringChars));
        }
        else if (iterType is UserDefinedType { Symbol: { TypeKind: TypeKind.Enum } enumSym2 })
        {
            var kind = enumSym2.IsStringEnum
                ? IterationLoweringKind.StringEnumValues
                : IterationLoweringKind.EnumValues;
            _semanticInfo.SetIterationLowering(forClause.Iterator,
                new IterationLowering(kind));
        }

        var elemType = _typeInference.InferIterableElementType(iterType) ?? SemanticType.Unknown;

        if (forClause.Target is Identifier id)
        {
            // Simple variable: for x in iterable
            var loopVarSymbol = new VariableSymbol
            {
                Name = id.Name,
                Kind = SymbolKind.Variable,
                Type = elemType,
                AccessLevel = AccessLevel.Public,
                // The escape travels to the symbol, as it does for a statement for-target: a
                // comprehension binding spelled `` `int` `` must not answer a bare `int` in the
                // element expression, which silently ran the loop variable instead (#1326).
                IsNameBacktickEscaped = id.IsNameBacktickEscaped,
                DeclarationLine = id.LineStart,
                DeclarationColumn = id.ColumnStart,
                NameDeclarationLine = id.LineStart,
                NameDeclarationColumn = id.ColumnStart
            };
            _symbolTable.Define(loopVarSymbol);
            _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
            _semanticInfo.SetTargetBinding(id, new TargetBinding(TargetBindingKind.Declares));
            _semanticInfo.SetExpressionType(forClause.Target, elemType);
            if (elemType is UnknownType)
            {
                MarkExpressionAsErrorRecovery(forClause.Target,
                    ErrorRecoveryReason.Propagated("the comprehension source's element type"));
            }
        }
        else if (forClause.Target is TupleLiteral targetTuple)
        {
            // Tuple unpacking: for a, b in iterable
            if (elemType is not TupleType tupleType)
            {
                AddError($"Cannot unpack non-tuple type '{elemType.GetDisplayName()}' in comprehension",
                    forClause.LineStart, forClause.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                    span: forClause.Target.Span);
            }
            else if (targetTuple.Elements.Length != tupleType.ElementTypes.Count)
            {
                AddError($"Cannot unpack {tupleType.ElementTypes.Count} values into {targetTuple.Elements.Length} variables in comprehension",
                    forClause.LineStart, forClause.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                    span: forClause.Target.Span);
            }
            else
            {
                // Define loop variables (supports nested tuple targets)
                DefineForLoopTupleTargets(targetTuple.Elements, tupleType.ElementTypes);
            }

            _semanticInfo.SetExpressionType(forClause.Target, elemType);
            if (elemType is UnknownType)
            {
                MarkExpressionAsErrorRecovery(forClause.Target,
                    ErrorRecoveryReason.Propagated("the comprehension source's element type"));
            }
        }
        else if (forClause.Target is MemberAccess or IndexAccess)
        {
            CheckExpression(forClause.Target);
            _semanticInfo.SetExpressionType(forClause.Target, elemType);
        }
        else
        {
            AddError($"Unsupported target type in comprehension for clause",
                forClause.LineStart, forClause.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                span: forClause.Target.Span);
        }
    }

    /// <summary>
    /// Processes an if clause in a comprehension, validating that the condition
    /// is a boolean expression.
    /// </summary>
    private void CheckComprehensionIfClause(IfClause ifClause)
    {
        var condType = CheckExpression(ifClause.Condition);
        var (compTruthTestable, compTruthLowering) = ClassifyTruthiness(condType);
        if (!compTruthTestable)
        {
            AddError($"Comprehension filter must be truth-testable, got '{condType.GetDisplayName()}'",
                ifClause.LineStart, ifClause.ColumnStart, code: DiagnosticCodes.Semantic.ConditionNotBoolean,
                span: ifClause.Condition.Span);
        }
        else
        {
            _semanticInfo.SetTruthinessLowering(ifClause.Condition, compTruthLowering);
        }
    }

    private SemanticType CheckFStringLiteral(FStringLiteral fstr)
    {
        // Type-check all interpolated expressions within the f-string
        foreach (var part in fstr.Parts)
        {
            if (part.Expression != null)
            {
                var partType = CheckExpression(part.Expression);
                RecordInterpolationStrWrapping(part, partType);
            }
        }
        return SemanticType.Str;
    }

    /// <summary>
    /// Marks an f-string interpolation operand whose default <c>$"{x}"</c> rendering would not be
    /// what Python prints, so codegen wraps it in <c>Builtins.Str</c> instead (#1480).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exception-typed operands are the recorded case. C# interpolation calls
    /// <c>Exception.ToString()</c>, which renders the type name, the message AND a stack trace
    /// carrying the ABSOLUTE build path of the source file — so <c>print(f"failed: {e}")</c> leaked
    /// a machine path into stdout where CPython prints only the message. <c>str(e)</c>,
    /// <c>{e!s}</c> and <c>{e!r}</c> were already correct (Core's Str has an exception arm, and the
    /// conversion flags route through it), which is exactly what made the plain form's divergence
    /// easy to miss.
    /// </para>
    /// <para>
    /// An explicit conversion flag is left alone: <c>!s</c>/<c>!r</c>/<c>!a</c> already emit
    /// <c>Builtins.Str</c>/<c>Repr</c>/<c>Ascii</c>, and <c>{e=}</c> (self-documenting) supplies
    /// <c>!r</c> of its own. Recording here as well would double-wrap or, worse, override the repr
    /// the user asked for.
    /// </para>
    /// </remarks>
    private void RecordInterpolationStrWrapping(FStringPart part, SemanticType partType)
    {
        if (part.Expression == null || part.Conversion != null || part.IsSelfDocumenting)
            return;

        var exceptionSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("Exception");
        if (exceptionSymbol == null || !IsExceptionSubtype(partType, exceptionSymbol))
            return;

        _semanticInfo.SetInterpolationStrWrapping(part.Expression, InterpolationStrWrapping.Str);
    }

    private SemanticType CheckTStringLiteral(TStringLiteral tstr)
    {
        foreach (var part in tstr.Parts)
        {
            if (part.Expression != null)
            {
                CheckExpression(part.Expression);
            }
        }
        return TemplateType.Instance;
    }

    private SemanticType CheckBytesLiteral(BytesLiteralExpression bytesLit)
    {
        var bytesSymbol = _symbolTable.BuiltinRegistry.GetType(BuiltinNames.Bytes)
            ?? throw new InvalidOperationException("bytes type must be registered in BuiltinRegistry");
        return new UserDefinedType { Name = bytesSymbol.Name, Symbol = bytesSymbol };
    }

    private SemanticType CheckSliceAccess(SliceAccess sliceAccess)
    {
        var objType = CheckExpression(sliceAccess.Object);

        // #1608: validate bounds — each must be assignable to int?
        CheckSliceBound(sliceAccess.Start);
        CheckSliceBound(sliceAccess.Stop);
        CheckSliceBound(sliceAccess.Step);

        // Classify the receiver and record the lowering fact
        if (objType is GenericType gt && gt.Name == BuiltinNames.List)
        {
            _semanticInfo.SetSliceLowering(sliceAccess, new SliceLowering(SliceLoweringKind.List));
            return objType;
        }
        if (objType == SemanticType.Str)
        {
            _semanticInfo.SetSliceLowering(sliceAccess, new SliceLowering(SliceLoweringKind.Str));
            return SemanticType.Str;
        }
        if (objType is UserDefinedType { Name: "bytes" })
        {
            _semanticInfo.SetSliceLowering(sliceAccess, new SliceLowering(SliceLoweringKind.Bytes));
            return objType;
        }
        if (objType is GenericType { Name: BuiltinNames.Array } arrayType
            && arrayType.TypeArguments.Count == 1)
        {
            _semanticInfo.SetSliceLowering(sliceAccess, new SliceLowering(SliceLoweringKind.Array));
            return new GenericType
            {
                Name = BuiltinNames.List,
                TypeArguments = new List<SemanticType> { arrayType.TypeArguments[0] }
            };
        }
        // #1608: ndarray — a slice is a sub-array VIEW of the same ndarray type (the rule
        // CheckMultiAxisAccess's hasSlice arm applies). Lowered to NdArray.Slice(SliceSpec):
        // Sharpy.Slice.GetSlice has no NdArray overload, which is what made a[1:4] fail as
        // CS1503 behind SPY0908 before this arm existed.
        if (IsNdArrayType(objType))
        {
            _semanticInfo.SetSliceLowering(sliceAccess, new SliceLowering(SliceLoweringKind.NdArray));
            return objType;
        }

        // #1610: user-defined __getitem__(self, s: slice) protocol
        {
            TypeSymbol? receiverSymbol = objType switch
            {
                UserDefinedType receiverUdt => receiverUdt.Symbol,
                GenericType receiverGt => receiverGt.GenericDefinition,
                _ => null
            };
            if (receiverSymbol != null)
            {
                List<FunctionSymbol>? getItemMethods = null;
                receiverSymbol.OperatorMethods.TryGetValue(DunderNames.GetItem, out getItemMethods);
                if (getItemMethods == null)
                    receiverSymbol.ProtocolMethods.TryGetValue(DunderNames.GetItem, out getItemMethods);

                if (getItemMethods != null)
                {
                    var sliceOverload = getItemMethods.FirstOrDefault(m =>
                    {
                        var nonSelfParams = m.Parameters.Where(p => p.Name != "self").ToList();
                        return nonSelfParams.Count == 1
                            && nonSelfParams[0].Type is UserDefinedType paramType
                            && paramType.Name == "slice";
                    });
                    if (sliceOverload != null)
                    {
                        _semanticInfo.SetSliceLowering(sliceAccess,
                            new SliceLowering(SliceLoweringKind.UserProtocol,
                                ResultType: sliceOverload.ReturnType));
                        return sliceOverload.ReturnType;
                    }
                }
            }
        }

        // #1609: tuple constant-bound slicing — v1: positive constants only, step absent or 1
        if (objType is TupleType tupleType)
        {
            var arity = tupleType.ElementTypes.Count;

            if (sliceAccess.Step != null
                && !(TryResolveConstantIntBound(sliceAccess.Step, out var stepVal) && stepVal == 1))
            {
                AddError(
                    "Tuple slicing does not support a step value; use positive constant bounds only",
                    sliceAccess.LineStart, sliceAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.ProtocolMissingMethod, span: sliceAccess.Span);
                return objType;
            }

            int startIdx = 0;
            int stopIdx = arity;

            if (sliceAccess.Start != null)
            {
                if (!TryResolveConstantIntBound(sliceAccess.Start, out startIdx) || startIdx < 0)
                {
                    AddError(
                        "Tuple slicing requires constant non-negative integer bounds (use positive indices)",
                        sliceAccess.LineStart, sliceAccess.ColumnStart,
                        code: DiagnosticCodes.Semantic.ProtocolMissingMethod, span: sliceAccess.Span);
                    return objType;
                }
            }

            if (sliceAccess.Stop != null)
            {
                if (!TryResolveConstantIntBound(sliceAccess.Stop, out stopIdx) || stopIdx < 0)
                {
                    AddError(
                        "Tuple slicing requires constant non-negative integer bounds (use positive indices)",
                        sliceAccess.LineStart, sliceAccess.ColumnStart,
                        code: DiagnosticCodes.Semantic.ProtocolMissingMethod, span: sliceAccess.Span);
                    return objType;
                }
            }

            startIdx = Math.Min(startIdx, arity);
            stopIdx = Math.Min(stopIdx, arity);
            if (startIdx > stopIdx)
                stopIdx = startIdx;

            var indices = Enumerable.Range(startIdx, stopIdx - startIdx).ToArray();
            var resultElements = indices.Select(i => tupleType.ElementTypes[i]).ToList();

            _semanticInfo.SetSliceLowering(sliceAccess,
                new SliceLowering(SliceLoweringKind.Tuple, TupleElementIndices: indices));

            return new TupleType { ElementTypes = resultElements };
        }
        else if (objType is not UnknownType)
        {
            AddError(
                $"Type '{objType.GetDisplayName()}' does not support slicing",
                sliceAccess.LineStart, sliceAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.ProtocolMissingMethod, span: sliceAccess.Span);
        }
        return objType;
    }

    /// <summary>
    /// #1608: a slice bound (start/stop/step) must be assignable to <c>int?</c> — a plain
    /// <c>int</c>, an <c>int | None</c> nullable, or the bare <c>None</c> literal (absent bound).
    /// The Optional ADT does not implicitly cross; unwrap or narrow first. Shared between
    /// single-axis slices and the slice dimensions of a multi-axis subscript.
    /// </summary>
    private void CheckSliceBound(Expression? bound)
    {
        if (bound == null)
            return;
        var boundType = CheckExpression(bound);
        if (boundType is UnknownType || bound is NoneLiteral)
            return;
        bool isIntCompatible = boundType == BuiltinType.Int
            || (boundType is NullableType { UnderlyingType: BuiltinType nbt } && nbt == BuiltinType.Int);
        if (!isIntCompatible)
        {
            AddError(
                $"Slice bound must be 'int' or 'None', got '{boundType.GetDisplayName()}'",
                bound.LineStart, bound.ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch, span: bound.Span);
        }
    }

    private SemanticType CheckMultiAxisAccess(MultiAxisAccess multiAxis)
    {
        var objType = CheckExpression(multiAxis.Object);
        var isNdArray = IsNdArrayType(objType);

        // Every dimension is checked BEFORE the receiver is classified or refused (#1644): a
        // refusal must not swallow the diagnostics (or the symbol/type facts tooling reads) of the
        // expressions nested in the subscript. Only the ndarray-specific int-index rule is gated on
        // the receiver; the per-dimension checks themselves are receiver-independent.
        var dimensionKinds = System.Collections.Immutable.ImmutableArray.CreateBuilder<MultiAxisDimensionKind>();
        var hasSlice = false;
        foreach (var dim in multiAxis.Dimensions)
        {
            if (dim.IsSlice)
            {
                hasSlice = true;
                dimensionKinds.Add(MultiAxisDimensionKind.Slice);
                CheckSliceBound(dim.Start);
                CheckSliceBound(dim.Stop);
                CheckSliceBound(dim.Step);
            }
            else
            {
                dimensionKinds.Add(MultiAxisDimensionKind.Index);
                var indexType = CheckExpression(dim.Index!);
                if (isNdArray)
                    CheckIntIndex(dim.Index!, indexType);
            }
        }

        // Classify the receiver or refuse. A refused (or already-Unknown) receiver records NO
        // lowering: the emitter throws on an absent fact, so an unclassified receiver can never
        // reach code generation silently (D6 classify-or-refuse; SliceLowering precedent #1608).
        if (!isNdArray)
        {
            if (objType is not UnknownType)
            {
                AddError(
                    $"Type '{objType.GetDisplayName()}' does not support multi-axis subscripting",
                    multiAxis.LineStart, multiAxis.ColumnStart,
                    code: DiagnosticCodes.SemanticOverflow.MultiAxisNotSupported,
                    span: multiAxis.Span);
            }
            return SemanticType.Unknown;
        }

        var accessKind = hasSlice ? MultiAxisAccessKind.SliceCall : MultiAxisAccessKind.IndexSpread;
        _semanticInfo.SetMultiAxisAccessLowering(multiAxis,
            new MultiAxisAccessLowering(accessKind, dimensionKinds.ToImmutable()));

        // Any slice → same type as object (sub-array view)
        if (hasSlice)
            return objType;

        // All indices → element type (scalar access).
        // For CLR-backed types (NdArray), resolve via the closed CLR indexer.
        if (objType is UserDefinedType { Symbol.ClrType: not null }
            or GenericType { GenericDefinition.ClrType: not null })
        {
            var closedClrType = TryGetClrType(objType);
            if (closedClrType != null)
            {
                var clrIndexerType = _typeInference.InferClrIndexerReturnType(closedClrType);
                if (clrIndexerType != null)
                    return clrIndexerType;
            }
        }

        if (objType is GenericType gt && gt.TypeArguments.Count > 0)
            return gt.TypeArguments[0];

        return SemanticType.Unknown;
    }
}
