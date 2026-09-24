extern alias SharpyRT;

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
    /// The element type of a list literal: arm 1 when the context supplies an element slot, else
    /// arms 2–3 of the R-W rule (<see cref="JoinCollectionOperands"/>).
    /// </summary>
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

        var elements = new List<(Expression? Node, SemanticType Type)>();
        foreach (var elem in list.Elements)
        {
            if (elem is SpreadElement spread)
            {
                var spreadType = CheckExpression(spread.Value);
                // Extract element type from the spread iterable
                if (spreadType is GenericType { Name: BuiltinNames.List or BuiltinNames.Set or BuiltinNames.Array } gt && gt.TypeArguments.Count > 0)
                    elements.Add((null, gt.TypeArguments[0]));
                else if (spreadType is TupleType tupleSpread)
                    elements.AddRange(tupleSpread.ElementTypes.Select(t => ((Expression?)null, t)));
                else
                    elements.Add((null, spreadType));
            }
            else
            {
                using (EnterStore(StorePosition.CollectionElement, elementExpectation ?? SemanticType.Unknown, elem))
                    elements.Add((elem, CheckExpression(elem)));
            }
        }

        var commonType = JoinCollectionOperands(
            elements, elementExpectation, list, "list element",
            new BestCommonTypeOptions(
                AnnotateSteer: "'xs: list[object] = ...'",
                NoneAnnotateSteer: "'xs: list[T | None] = ...' for .NET-nullable elements, "
                    + "or 'xs: list[T?] = ...' with None() elements for Sharpy optionals"));

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

        var keys = new List<(Expression? Node, SemanticType Type)>();
        var values = new List<(Expression? Node, SemanticType Type)>();
        foreach (var entry in dict.Entries)
        {
            if (entry.Key == null)
            {
                // Dict spread: **other_dict — extract K, V from dict[K, V]
                var spreadType = CheckExpression(entry.Value);
                if (spreadType is GenericType { Name: BuiltinNames.Dict } gt && gt.TypeArguments.Count == 2)
                {
                    keys.Add((null, gt.TypeArguments[0]));
                    values.Add((null, gt.TypeArguments[1]));
                }
            }
            else
            {
                using (EnterStore(StorePosition.CollectionElement, keyExpectation ?? SemanticType.Unknown, entry.Key))
                    keys.Add((entry.Key, CheckExpression(entry.Key)));
                using (EnterStore(StorePosition.CollectionElement, valueExpectation ?? SemanticType.Unknown, entry.Value))
                    values.Add((entry.Value, CheckExpression(entry.Value)));
            }
        }

        var commonKeyType = JoinCollectionOperands(
            keys, keyExpectation, dict, "dict key",
            new BestCommonTypeOptions(
                AnnotateSteer: "'d: dict[object, V] = ...'",
                NoneAnnotateSteer: "'d: dict[K | None, V] = ...' for a .NET-nullable key"));

        var commonValueType = JoinCollectionOperands(
            values, valueExpectation, dict, "dict value",
            new BestCommonTypeOptions(
                AnnotateSteer: "'d: dict[K, object] = ...'",
                NoneAnnotateSteer: "'d: dict[K, V | None] = ...' for .NET-nullable values, "
                    + "or 'd: dict[K, V?] = ...' with None() values for Sharpy optionals"));

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

        var elements = new List<(Expression? Node, SemanticType Type)>();
        foreach (var elem in set.Elements)
        {
            if (elem is SpreadElement spread)
            {
                var spreadType = CheckExpression(spread.Value);
                if (spreadType is GenericType { Name: BuiltinNames.List or BuiltinNames.Set or BuiltinNames.Array } gt && gt.TypeArguments.Count > 0)
                    elements.Add((null, gt.TypeArguments[0]));
                else if (spreadType is TupleType tupleSpread)
                    elements.AddRange(tupleSpread.ElementTypes.Select(t => ((Expression?)null, t)));
                else
                    elements.Add((null, spreadType));
            }
            else
            {
                using (EnterStore(StorePosition.CollectionElement, elementExpectation ?? SemanticType.Unknown, elem))
                    elements.Add((elem, CheckExpression(elem)));
            }
        }

        var commonType = JoinCollectionOperands(
            elements, elementExpectation, set, "set element",
            new BestCommonTypeOptions(
                AnnotateSteer: "'s: set[object] = ...'",
                NoneAnnotateSteer: "'s: set[T | None] = ...' for .NET-nullable elements, "
                    + "or 's: set[T?] = ...' with None() elements for Sharpy optionals"));

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

        var directElements = new List<(Expression? Node, SemanticType Type)>(tuple.Elements.Length);
        for (int i = 0; i < tuple.Elements.Length; i++)
        {
            using (EnterStore(StorePosition.TupleElement, indexExpectations?[i] ?? SemanticType.Unknown, tuple.Elements[i]))
                directElements.Add((tuple.Elements[i], CheckExpression(tuple.Elements[i])));
        }

        var directElementTypes = directElements.Select(e => e.Type).ToList();

        // A tuple literal under a tuple expectation is a row of collection-element stores, one per
        // index (its slots differ, unlike a list's). When EVERY index is admitted the literal adopts
        // the expected element types and each index's accepted verdict applies — that is what makes
        // `t: tuple[LiteralString, int8] = ("a", 1)` a store rather than a type comparison. All-or-
        // nothing, for the same reason the other literals are: a refused index keeps the produced
        // types so the enclosing store reports the composite mismatch unchanged (#1698, #1701).
        if (indexExpectations != null
            && !indexExpectations.Any(ContainsTypeParameterType)
            && AdmitTupleElements(directElements, indexExpectations) != ElementAdmissionResult.Refused)
        {
            directElementTypes = indexExpectations.ToList();
        }
        else
        {
            directElementTypes = DecideTupleIndexTypes(tuple, directElements);
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
    /// #1796's NAMED check: a tuple literal is a ROW OF SINGLE-OPERAND SEAMS — one
    /// <see cref="BestCommonType"/> call per index, because each index has its own slot (unlike a
    /// list's shared element type).
    ///
    /// <para>A typed index returns its own type unchanged, so this is an identity for
    /// <c>(1, "a")</c>. An UNTYPED index — a bare <c>None</c> or a void call — is refused AT THAT
    /// INDEX instead of being recorded as <c>void</c> and handed to the emitter: <c>t = (None, 1)</c>
    /// and <c>for s, n in [(None, 1)]</c> were SPY0599 "Keyword 'void' cannot be used in this
    /// context" (R-AB).</para>
    /// </summary>
    private List<SemanticType> DecideTupleIndexTypes(
        TupleLiteral tuple, IReadOnlyList<(Expression? Node, SemanticType Type)> directElements)
    {
        var decided = new List<SemanticType>(directElements.Count);
        for (int i = 0; i < directElements.Count; i++)
        {
            decided.Add(BestCommonType(
                new[] { directElements[i] },
                null, StorePosition.TupleElement,
                (Node?)directElements[i].Node ?? tuple,
                $"tuple element {i + 1}",
                new BestCommonTypeOptions(
                    AnnotateSteer: "'t: tuple[...] = ...'",
                    NoneAnnotateSteer: "'t: tuple[..., T | None, ...] = ...' for a "
                        + ".NET-nullable element, or a 'T?' element built with None()")));
        }

        return decided;
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
    /// element is assignable to it; otherwise keeps the produced type so assignability decides.
    /// Now only used for SPREAD comprehension elements, which have no single node to anchor a
    /// diagnostic. Non-spread comprehension elements route through
    /// <see cref="AdmitCollectionElements"/> instead (#1776).
    /// </summary>
    private SemanticType ContextualElementType(
        SemanticType produced, SemanticType? expectation, Expression? element = null)
    {
        if (expectation == null)
            return produced;

        var verdict = ClassifyStore(StorePosition.CollectionElement, element, produced, expectation);
        if (!IsAcceptedVerdict(verdict))
            return produced;

        ApplyAcceptedVerdict(StorePosition.CollectionElement, verdict, element, produced, expectation);
        return expectation;
    }

    private SemanticType CheckGeneratorExpression(GeneratorExpression genExpr)
    {
        _symbolTable.EnterScope("generator-expression");

        SemanticType elementType;
        using (ClearExpectation(null))
        {
            CheckComprehensionClauses(genExpr.Clauses);
            elementType = CheckExpression(genExpr.Element);
        }

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = BuiltinNames.Iterator,
            TypeArguments = new List<SemanticType> { elementType }
        };
    }

    private SemanticType CheckListComprehension(ListComprehension listComp)
    {
        var expectations = ComprehensionElementExpectations(BuiltinNames.List, 1);

        _symbolTable.EnterScope("list-comprehension");

        SemanticType elementType;
        using (ClearExpectation(null))
        {
            CheckComprehensionClauses(listComp.Clauses);

            if (listComp.Element is SpreadElement spread)
            {
                // [*it for it in its] — result type is the inner element type of the spread value
                var spreadType = CheckExpression(spread);  // caches type for spread node
                elementType = _typeInference.InferIterableElementType(spreadType) ?? SemanticType.Unknown;
            }
            else
            {
                using (EnterStore(StorePosition.CollectionElement, expectations?[0] ?? SemanticType.Unknown, listComp.Element))
                    elementType = CheckExpression(listComp.Element);
            }
        }

        _symbolTable.ExitScope();

        // A comprehension is a collection literal whose element is written once, so its element
        // row goes through the SAME join (#1671): the contextual element type is the slot and a
        // refused value reports at the ELEMENT span; slot-less, arms 2–3 decide, so
        // `[None for i in range(2)]` is refused by name instead of emitting `void` (#1796).
        // Spreads keep ContextualElementType because there is no single element node.
        var commonType = listComp.Element is SpreadElement
            ? ContextualElementType(elementType, expectations?[0])
            : JoinCollectionOperands(
                new (Expression?, SemanticType)[] { (listComp.Element, elementType) },
                expectations?[0], listComp, "list comprehension element",
                new BestCommonTypeOptions(
                    AnnotateSteer: "'xs: list[object] = ...'",
                    NoneAnnotateSteer: "'xs: list[T | None] = ...' for .NET-nullable elements, "
                        + "or 'xs: list[T?] = ...' with None() elements for Sharpy optionals"));

        return new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType> { commonType }
        };
    }

    private SemanticType CheckSetComprehension(SetComprehension setComp)
    {
        var expectations = ComprehensionElementExpectations(BuiltinNames.Set, 1);

        _symbolTable.EnterScope("set-comprehension");

        SemanticType elementType;
        using (ClearExpectation(null))
        {
            CheckComprehensionClauses(setComp.Clauses);

            if (setComp.Element is SpreadElement spread)
            {
                // {*it for it in its} — result type is the inner element type of the spread value
                var spreadType = CheckExpression(spread);  // caches type for spread node
                elementType = _typeInference.InferIterableElementType(spreadType) ?? SemanticType.Unknown;
            }
            else
            {
                using (EnterStore(StorePosition.CollectionElement, expectations?[0] ?? SemanticType.Unknown, setComp.Element))
                    elementType = CheckExpression(setComp.Element);
            }
        }

        _symbolTable.ExitScope();

        var commonType = setComp.Element is SpreadElement
            ? ContextualElementType(elementType, expectations?[0])
            : JoinCollectionOperands(
                new (Expression?, SemanticType)[] { (setComp.Element, elementType) },
                expectations?[0], setComp, "set comprehension element",
                new BestCommonTypeOptions(
                    AnnotateSteer: "'s: set[object] = ...'",
                    NoneAnnotateSteer: "'s: set[T | None] = ...' for .NET-nullable elements, "
                        + "or 's: set[T?] = ...' with None() elements for Sharpy optionals"));

        return new GenericType
        {
            Name = BuiltinNames.Set,
            TypeArguments = new List<SemanticType> { commonType }
        };
    }

    private SemanticType CheckDictComprehension(DictComprehension dictComp)
    {
        var expectations = ComprehensionElementExpectations(BuiltinNames.Dict, 2);

        _symbolTable.EnterScope("dict-comprehension");

        SemanticType keyType;
        SemanticType valueType;
        using (ClearExpectation(null))
        {
            CheckComprehensionClauses(dictComp.Clauses);

            using (EnterStore(StorePosition.CollectionElement, expectations?[0] ?? SemanticType.Unknown, dictComp.Key))
                keyType = CheckExpression(dictComp.Key);
            using (EnterStore(StorePosition.CollectionElement, expectations?[1] ?? SemanticType.Unknown, dictComp.Value))
                valueType = CheckExpression(dictComp.Value);
        }

        _symbolTable.ExitScope();

        // Dict comprehension keys and values always have a node, so both rows take the same
        // join every other literal row takes — refusals report at the element span.
        var commonKeyType = JoinCollectionOperands(
            new (Expression?, SemanticType)[] { (dictComp.Key, keyType) },
            expectations?[0], dictComp, "dict comprehension key",
            new BestCommonTypeOptions(
                AnnotateSteer: "'d: dict[object, V] = ...'",
                NoneAnnotateSteer: "'d: dict[K | None, V] = ...' for a .NET-nullable key"));

        var commonValueType = JoinCollectionOperands(
            new (Expression?, SemanticType)[] { (dictComp.Value, valueType) },
            expectations?[1], dictComp, "dict comprehension value",
            new BestCommonTypeOptions(
                AnnotateSteer: "'d: dict[K, object] = ...'",
                NoneAnnotateSteer: "'d: dict[K, V | None] = ...' for .NET-nullable values, "
                    + "or 'd: dict[K, V?] = ...' with None() values for Sharpy optionals"));

        return new GenericType
        {
            Name = BuiltinNames.Dict,
            TypeArguments = new List<SemanticType> { commonKeyType, commonValueType }
        };
    }

    private SemanticType CheckDictSpreadComprehension(DictSpreadComprehension dictSpreadComp)
    {
        // {**d for d in dicts} — result type is dict[K, V] from the spread value type
        _symbolTable.EnterScope("dict-spread-comprehension");

        using (ClearExpectation(null))
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

        var elemType = RecordIterationSourceFacts(
            forClause.Iterator, iterType, "comprehension iterator");

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
            // The one unpacking rule binds the comprehension for-clause target, star-aware at every
            // depth, with the "in comprehension for clause" suffix on SPY0239 (#1846).
            CheckUnpackingTargets(targetTuple.Elements, elemType,
                UnpackingPosition.ComprehensionForClause,
                forClause.LineStart, forClause.ColumnStart, forClause.Target.Span);

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
        var (compTruthTestable, condType) = CheckTruthinessTest(ifClause.Condition);
        if (!compTruthTestable)
        {
            ReportNotTruthTestable(ifClause.Condition, condType, "Comprehension filter must be truth-testable",
                code: DiagnosticCodes.Semantic.ConditionNotBoolean);
        }
    }

    private SemanticType CheckStringLiteral(StringLiteral sl)
    {
        _semanticInfo.SetLiteralDerived(sl);
        return SemanticType.Str;
    }

    private SemanticType CheckFStringLiteral(FStringLiteral fstr)
    {
        foreach (var part in fstr.Parts)
        {
            CheckInterpolationPart(part);
        }

        // #1741: every hole (recursively through its format spec) is literal-derived — a hole-free
        // f-string is derived vacuously. Checked AFTER the loop above, so every hole's own
        // literal-derived fact (recorded by CheckExpression during CheckInterpolationPart) is already
        // in place for LiteralDerivation to read.
        if (LiteralDerivation.IsFStringDerived(_semanticInfo, fstr.Parts))
            _semanticInfo.SetLiteralDerived(fstr);

        return SemanticType.Str;
    }

    private SemanticType CheckTStringLiteral(TStringLiteral tstr)
    {
        foreach (var part in tstr.Parts)
        {
            CheckInterpolationPart(part);
        }

        // PEP 750: a t-string is its registry symbol's CLR-backed type (Sharpy.Template), so its
        // members, __iter__, operators and SPY0203 come from reflection, as for bytes (#1996).
        var templateSymbol = _symbolTable.BuiltinRegistry.GetType(BuiltinNames.Template)
            ?? throw new InvalidOperationException("Template type must be registered in BuiltinRegistry");
        return new UserDefinedType { Name = templateSymbol.Name, Symbol = templateSymbol };
    }

    /// <summary>
    /// Type-checks one replacement field — its hole expression and, recursively, the expressions in
    /// its format spec (so a variable used only inside a spec, <c>{x:{w}}</c>, is seen as a use and
    /// SPY0451 stops lying) — and records how the emitter renders it. A STATIC spec (all literal
    /// text) is validated by name against the operand's kind, raising SPY0609 with CPython's message
    /// (#1814, #1815); a dynamic spec is validated by Core at runtime.
    /// </summary>
    private void CheckInterpolationPart(FStringPart part)
    {
        if (part.Expression == null)
        {
            return;
        }

        SemanticType partType;
        using (EnterStore(StorePosition.FStringHole, SemanticType.Object, part.Expression))
        {
            partType = CheckExpression(part.Expression);
        }

        // Walk the spec: check nested holes (they are expressions the AST owns) and collect the
        // literal text of an all-static spec.
        bool specIsStatic = true;
        string staticSpec = "";
        if (part.Spec is { } spec)
        {
            foreach (var specPart in spec)
            {
                if (specPart.Expression != null)
                {
                    specIsStatic = false;
                    CheckInterpolationPart(specPart);
                }
                else if (specPart.Text != null)
                {
                    staticSpec += specPart.Text;
                }
            }
        }

        // Effective conversion → base rendering. An explicit !r/!s/!a wins; a self-documenting '='
        // WITHOUT a spec defaults to repr (the value side of '=:spec' formats the raw value).
        char? effectiveConversion = part.Conversion
            ?? (part.IsSelfDocumenting && part.Spec == null ? 'r' : null);
        var kind = effectiveConversion switch
        {
            'r' => InterpolationKind.Repr,
            's' => InterpolationKind.Str,
            'a' => InterpolationKind.Ascii,
            _ => InterpolationKind.Format
        };

        // A static, non-empty spec is validated now. When a conversion applies (Str/Repr/Ascii), the
        // spec formats a STRING, so validate against Str; otherwise against the value's own kind.
        if (part.Spec != null && specIsStatic && staticSpec.Length > 0)
        {
            var operand = kind == InterpolationKind.Format
                ? FormatOperandOf(part.Expression, partType)
                : FormatOperand.Str;
            var message = FormatSpecGrammar.Validate(staticSpec, operand);
            if (message != null)
            {
                AddError(message, part.Expression.LineStart, part.Expression.ColumnStart,
                    code: DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification, span: part.Expression.Span);
            }
        }

        _semanticInfo.SetInterpolationLowering(part.Expression,
            new InterpolationLowering(kind, specIsStatic, part.Spec == null ? null : staticSpec));
    }

    /// <summary>
    /// The compile-time operand a static format spec is validated against — the projection of the
    /// value kinds <c>Sharpy.PyFormat</c> distinguishes at runtime (#1988, R-BY), with the python type
    /// name Core's messages spell. A None literal is its own kind. A Sharpy-declared <c>enum</c>
    /// formats as its <c>str</c>. A type whose CLR twin is known takes the kind the runtime gives
    /// its values (<see cref="FormatOperandOfClr"/>). A Sharpy-declared class or struct owns its spec
    /// when its base/interface closure reaches <c>System.IFormattable</c> (the CLR spelling of
    /// <c>__format__</c>), and otherwise has no <c>__format__</c>. Everything whose runtime class
    /// is not fixed by the static type (<c>object</c>, an interface, a union, a type parameter, a
    /// nullable, <c>Unknown</c>) is <see cref="FormatOperand.Unknown"/>: never refused statically,
    /// Core decides at runtime.
    /// </summary>
    private FormatOperand FormatOperandOf(Expression expr, SemanticType type)
    {
        if (expr is NoneLiteral)
        {
            return FormatOperand.NoneValue;
        }

        switch (type)
        {
            case UserDefinedType { Symbol.TypeKind: TypeKind.Enum }:
                // A Sharpy-declared enum has no CLR type at compile time, so the arm keys on the
                // symbol's kind: python's Enum.__format__ is str.__format__(str(self), spec).
                return FormatOperand.Str;
            case BuiltinType or UserDefinedType or GenericType or TupleType or OptionalType
                when TryGetClrType(type) is { } clr:
                return FormatOperandOfClr(clr);
        }

        var symbol = type switch
        {
            UserDefinedType udt => udt.Symbol,
            GenericType generic => generic.GenericDefinition,
            _ => null,
        };
        if (symbol is { TypeKind: TypeKind.Class or TypeKind.Struct })
        {
            // A base-typed hole holding a subclass that implements IFormattable is refused here
            // although the runtime would delegate — the hazard R-BY accepts; the dynamic spec is
            // the escape. A Sharpy-declared type has no CLR type at compile time, so it is named by
            // its declared (python) name, not through PyFormat.PyTypeName(Type); the runtime names
            // the emitted CLR class, which differs for a non-PascalCase name (#2030).
            return OwnsFormatSpec(symbol) ? FormatOperand.Formattable : FormatOperand.NoFormat(symbol.Name);
        }

        return FormatOperand.Unknown;
    }

    /// <summary>
    /// The operand kind the runtime gives a value of CLR type <paramref name="clr"/> — Core's own
    /// <c>Sharpy.PyFormat.KindOf(Type)</c>, the ONE kind table the runtime engine classifies values by
    /// (R-BX: the static and runtime twins are the same code; no CLR-type arm is restated here). The
    /// compiler adds only what a static type cannot say: a type whose values may be of another
    /// class (<c>object</c>, <c>ValueType</c>, an interface, an abstract class, a type parameter, a
    /// <c>Nullable&lt;T&gt;</c>) and that Core finds no <c>__format__</c> on is not statically known.
    /// The python type name is Core's too (<c>PyFormat.FormatOperandTypeName(Type)</c>).
    /// </summary>
    internal static FormatOperand FormatOperandOfClr(Type clr)
    {
        var kind = SharpyRT::Sharpy.PyFormat.KindOf(clr);
        if (kind == SharpyRT::Sharpy.FormatOperandKind.NoFormat && !IsStaticallyKnownFormatClass(clr))
            return FormatOperand.Unknown;
        return new FormatOperand(kind, SharpyRT::Sharpy.PyFormat.FormatOperandTypeName(clr));
    }

    /// <summary>
    /// Whether a value of static type <paramref name="clr"/> is of exactly that runtime class: not
    /// <c>object</c>/<c>ValueType</c>, an interface, an abstract non-sealed class, a generic
    /// parameter or a <c>Nullable&lt;T&gt;</c> (whose boxed value is its underlying type or null).
    /// </summary>
    private static bool IsStaticallyKnownFormatClass(Type clr)
        => !(clr == typeof(object) || clr == typeof(ValueType) || clr.IsInterface || (clr.IsAbstract && !clr.IsSealed)
            || clr.IsGenericParameter || Nullable.GetUnderlyingType(clr) != null);

    private const string IFormattableFullName = "System.IFormattable";

    /// <summary>
    /// Whether a Sharpy-declared type owns its format spec: some type in its base chain, or an
    /// interface any of them declares (transitively), is a CLR type implementing
    /// <c>System.IFormattable</c> — <c>class F(IFormattable)</c> with <c>to_string(self, fmt, provider)</c>.
    /// </summary>
    private bool OwnsFormatSpec(TypeSymbol symbol)
    {
        for (var current = symbol; current != null; current = current.BaseType)
        {
            if (current.ClrType is { } clr && ProtocolMembership.HasClrProtocolInterface(clr, IFormattableFullName))
                return true;
        }
        return GetInterfaces(symbol).Any(iface =>
            iface.ClrType is { } clr && ProtocolMembership.HasClrProtocolInterface(clr, IFormattableFullName));
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

        // #1792: T? (strict Optional) is refused at the slice route with SPY0326,
        // matching the index route's existing ReportOptionalProtocol twin.
        if (objType is OptionalType)
        {
            AddError(
                $"Optional type '{objType.GetDisplayName()}' does not support slicing directly. " +
                "Narrow it first (if x is not None:) or unwrap it (x.unwrap()).",
                sliceAccess.LineStart, sliceAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.OptionalRequiresNarrowing, span: sliceAccess.Span);
            return objType;
        }

        // #1792: unwrap T | None so str | None dispatches like str, and materialize the `.Value`
        // the emitted receiver needs when the payload is a struct (bytes, a tuple).
        var viewType = ProtocolReceiver(sliceAccess.Object, objType);

        // Classify the receiver and record the lowering fact
        if (viewType is GenericType gt && gt.Name == BuiltinNames.List)
        {
            _semanticInfo.SetSliceLowering(sliceAccess, new SliceLowering(SliceLoweringKind.List));
            return objType;
        }
        if (viewType == SemanticType.Str)
        {
            _semanticInfo.SetSliceLowering(sliceAccess, new SliceLowering(SliceLoweringKind.Str));
            return SemanticType.Str;
        }
        if (viewType is UserDefinedType { Name: "bytes" })
        {
            _semanticInfo.SetSliceLowering(sliceAccess, new SliceLowering(SliceLoweringKind.Bytes));
            return objType;
        }
        if (viewType is GenericType { Name: BuiltinNames.Array } arrayType
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
        if (IsNdArrayType(viewType))
        {
            _semanticInfo.SetSliceLowering(sliceAccess, new SliceLowering(SliceLoweringKind.NdArray));
            return objType;
        }

        // #1610: user-defined __getitem__(self, s: slice) protocol
        {
            TypeSymbol? receiverSymbol = viewType switch
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
        if (viewType is TupleType tupleType)
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
        else if (viewType is not UnknownType)
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
