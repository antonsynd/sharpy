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

    private SemanticType CheckListLiteral(ListLiteral list)
    {
        if (list.Elements.Length == 0)
        {
            return TryInferEmptyCollectionType(
                BuiltinNames.List, 1, list, "x: list[int] = []") ?? SemanticType.Unknown;
        }

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
                elementTypes.Add(CheckExpression(elem));
            }
        }

        // Find least common ancestor of all element types
        // This handles cases like [Bug(), Feature()] -> list[WorkItem]
        var commonType = FindLeastCommonAncestor(elementTypes);

        // When LCA falls back to object but a contextual type is available,
        // use the expected element type if all elements are assignable to it.
        // This handles cases like: x: list[float] = [a, b] where a,b are float.
        if (commonType is UserDefinedType { Name: "object" }
            && _expectedType is GenericType expectedList
            && expectedList.Name == BuiltinNames.List
            && expectedList.TypeArguments.Count == 1
            && AllAssignableTo(elementTypes, expectedList.TypeArguments[0]))
        {
            commonType = expectedList.TypeArguments[0];
        }

        return new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType> { commonType }
        };
    }

    private SemanticType CheckDictLiteral(DictLiteral dict)
    {
        if (dict.Entries.Length == 0)
        {
            return TryInferEmptyCollectionType(
                BuiltinNames.Dict, 2, dict, "d: dict[str, int] = {}") ?? SemanticType.Unknown;
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
                keyTypes.Add(CheckExpression(entry.Key));
                valueTypes.Add(CheckExpression(entry.Value));
            }
        }

        // Find least common ancestor for both keys and values
        var commonKeyType = FindLeastCommonAncestor(keyTypes);
        var commonValueType = FindLeastCommonAncestor(valueTypes);

        // When LCA falls back to object but a contextual type is available,
        // use the expected key/value types if all elements are assignable.
        // This handles cases like: d: dict[str, float] = {"a": x, "b": y}
        if (_expectedType is GenericType expectedDict
            && expectedDict.Name == BuiltinNames.Dict
            && expectedDict.TypeArguments.Count == 2)
        {
            if (commonKeyType is UserDefinedType { Name: "object" }
                && AllAssignableTo(keyTypes, expectedDict.TypeArguments[0]))
                commonKeyType = expectedDict.TypeArguments[0];
            if (commonValueType is UserDefinedType { Name: "object" }
                && AllAssignableTo(valueTypes, expectedDict.TypeArguments[1]))
                commonValueType = expectedDict.TypeArguments[1];
        }

        return new GenericType
        {
            Name = BuiltinNames.Dict,
            TypeArguments = new List<SemanticType> { commonKeyType, commonValueType }
        };
    }

    private SemanticType CheckSetLiteral(SetLiteral set)
    {
        if (set.Elements.Length == 0)
        {
            return TryInferEmptyCollectionType(
                BuiltinNames.Set, 1, set, "s: set[int] = set()") ?? SemanticType.Unknown;
        }

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
                elementTypes.Add(CheckExpression(elem));
            }
        }

        // Find least common ancestor of all element types
        var commonType = FindLeastCommonAncestor(elementTypes);

        // When LCA falls back to object but a contextual type is available,
        // use the expected element type if all elements are assignable to it.
        if (commonType is UserDefinedType { Name: "object" }
            && _expectedType is GenericType expectedSet
            && expectedSet.Name == BuiltinNames.Set
            && expectedSet.TypeArguments.Count == 1
            && AllAssignableTo(elementTypes, expectedSet.TypeArguments[0]))
        {
            commonType = expectedSet.TypeArguments[0];
        }

        return new GenericType
        {
            Name = BuiltinNames.Set,
            TypeArguments = new List<SemanticType> { commonType }
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

        var directElementTypes = tuple.Elements.Select(CheckExpression).ToList();
        var tupleType = new TupleType { ElementTypes = directElementTypes };

        // Propagate element names for named tuple literals
        if (!tuple.ElementNames.IsEmpty)
        {
            tupleType = tupleType with { ElementNames = tuple.ElementNames };
        }

        return tupleType;
    }

    private SemanticType CheckListComprehension(ListComprehension listComp)
    {
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
            elementType = CheckExpression(listComp.Element);
        }

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType> { elementType }
        };
    }

    private SemanticType CheckSetComprehension(SetComprehension setComp)
    {
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
            elementType = CheckExpression(setComp.Element);
        }

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = BuiltinNames.Set,
            TypeArguments = new List<SemanticType> { elementType }
        };
    }

    private SemanticType CheckDictComprehension(DictComprehension dictComp)
    {
        _symbolTable.EnterScope("dict-comprehension");
        CheckComprehensionClauses(dictComp.Clauses);

        var keyType = CheckExpression(dictComp.Key);
        var valueType = CheckExpression(dictComp.Value);

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = BuiltinNames.Dict,
            TypeArguments = new List<SemanticType> { keyType, valueType }
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
        // Check iterator type and infer element type (errors reported by validator in pipeline)
        var iterType = CheckExpression(forClause.Iterator);

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
                DeclarationLine = id.LineStart,
                DeclarationColumn = id.ColumnStart,
                NameDeclarationLine = id.LineStart,
                NameDeclarationColumn = id.ColumnStart
            };
            _symbolTable.Define(loopVarSymbol);
            _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
            _semanticInfo.SetExpressionType(forClause.Target, elemType);
            if (elemType is UnknownType)
                MarkExpressionAsErrorRecovery(forClause.Target);
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
                MarkExpressionAsErrorRecovery(forClause.Target);
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
        // Check condition is boolean
        var condType = CheckExpression(ifClause.Condition);
        if (!condType.IsAssignableTo(SemanticType.Bool))
        {
            AddError($"Comprehension filter must be bool, got '{condType.GetDisplayName()}'",
                ifClause.LineStart, ifClause.ColumnStart, code: DiagnosticCodes.Semantic.ConditionNotBoolean,
                span: ifClause.Condition.Span);
        }
    }

    private SemanticType CheckFStringLiteral(FStringLiteral fstr)
    {
        // Type-check all interpolated expressions within the f-string
        foreach (var part in fstr.Parts)
        {
            if (part.Expression != null)
            {
                CheckExpression(part.Expression);
            }
        }
        return SemanticType.Str;
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
        if (sliceAccess.Start != null)
            CheckExpression(sliceAccess.Start);
        if (sliceAccess.Stop != null)
            CheckExpression(sliceAccess.Stop);
        if (sliceAccess.Step != null)
            CheckExpression(sliceAccess.Step);

        // Slicing a list returns a list, slicing a str returns a str
        if (objType is GenericType gt && gt.Name == BuiltinNames.List)
            return objType;
        if (objType == SemanticType.Str)
            return SemanticType.Str;

        // For other types, return the same type (best effort)
        return objType;
    }
}
