using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Collection literals, comprehensions, f-strings, slicing
/// </summary>
internal partial class TypeChecker
{
    private SemanticType CheckListLiteral(ListLiteral list)
    {
        if (list.Elements.Length == 0)
        {
            if (_expectedType is GenericType expected && expected.Name == BuiltinNames.List && expected.TypeArguments.Count == 1)
            {
                return new GenericType
                {
                    Name = "list",
                    TypeArguments = new List<SemanticType> { expected.TypeArguments[0] }
                };
            }

            // Cannot infer element type for empty list literal without annotation
            AddError("Cannot infer type of empty list literal; add a type annotation (e.g., x: list[int] = [])",
                list.LineStart, list.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                span: list.Span);
            return SemanticType.Unknown;
        }

        var elementTypes = list.Elements.Select(CheckExpression).ToList();

        // Find least common ancestor of all element types
        // This handles cases like [Bug(), Feature()] -> list[WorkItem]
        var commonType = FindLeastCommonAncestor(elementTypes);

        return new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { commonType }
        };
    }

    private SemanticType CheckDictLiteral(DictLiteral dict)
    {
        if (dict.Entries.Length == 0)
        {
            if (_expectedType is GenericType expected && expected.Name == BuiltinNames.Dict && expected.TypeArguments.Count == 2)
            {
                return new GenericType
                {
                    Name = "dict",
                    TypeArguments = new List<SemanticType> { expected.TypeArguments[0], expected.TypeArguments[1] }
                };
            }

            // Cannot infer key/value types for empty dict literal without annotation
            AddError("Cannot infer type of empty dict literal; add a type annotation (e.g., d: dict[str, int] = {})",
                dict.LineStart, dict.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                span: dict.Span);
            return SemanticType.Unknown;
        }

        var keyTypes = dict.Entries.Select(e => CheckExpression(e.Key)).ToList();
        var valueTypes = dict.Entries.Select(e => CheckExpression(e.Value)).ToList();

        // Find least common ancestor for both keys and values
        var commonKeyType = FindLeastCommonAncestor(keyTypes);
        var commonValueType = FindLeastCommonAncestor(valueTypes);

        return new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType> { commonKeyType, commonValueType }
        };
    }

    private SemanticType CheckSetLiteral(SetLiteral set)
    {
        if (set.Elements.Length == 0)
        {
            if (_expectedType is GenericType expected && expected.Name == BuiltinNames.Set && expected.TypeArguments.Count == 1)
            {
                return new GenericType
                {
                    Name = "set",
                    TypeArguments = new List<SemanticType> { expected.TypeArguments[0] }
                };
            }

            // Cannot infer element type for empty set literal without annotation
            AddError("Cannot infer type of empty set literal; add a type annotation (e.g., s: set[int] = set())",
                set.LineStart, set.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                span: set.Span);
            return SemanticType.Unknown;
        }

        var elementTypes = set.Elements.Select(CheckExpression).ToList();

        // Find least common ancestor of all element types
        var commonType = FindLeastCommonAncestor(elementTypes);

        return new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { commonType }
        };
    }

    private SemanticType CheckTupleLiteral(TupleLiteral tuple)
    {
        var elementTypes = tuple.Elements.Select(CheckExpression).ToList();
        var tupleType = new TupleType { ElementTypes = elementTypes };

        // Propagate element names for named tuple literals
        if (!tuple.ElementNames.IsEmpty)
        {
            tupleType = tupleType with { ElementNames = tuple.ElementNames };
        }

        return tupleType;
    }

    private SemanticType CheckListComprehension(ListComprehension listComp)
    {
        // Enter comprehension scope (variables don't leak)
        _symbolTable.EnterScope("list-comprehension");

        // Process clauses (for and if)
        CheckComprehensionClauses(listComp.Clauses);

        // Check element expression
        var elementType = CheckExpression(listComp.Element);

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { elementType }
        };
    }

    private SemanticType CheckSetComprehension(SetComprehension setComp)
    {
        // Enter comprehension scope (variables don't leak)
        _symbolTable.EnterScope("set-comprehension");

        // Process clauses (for and if)
        CheckComprehensionClauses(setComp.Clauses);

        // Check element expression
        var elementType = CheckExpression(setComp.Element);

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { elementType }
        };
    }

    private SemanticType CheckDictComprehension(DictComprehension dictComp)
    {
        // Enter comprehension scope (variables don't leak)
        _symbolTable.EnterScope("dict-comprehension");

        // Process clauses (for and if)
        CheckComprehensionClauses(dictComp.Clauses);

        // Check key and value expressions
        var keyType = CheckExpression(dictComp.Key);
        var valueType = CheckExpression(dictComp.Value);

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType> { keyType, valueType }
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
                DeclarationColumn = id.ColumnStart
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
        if (objType is GenericType gt && gt.Name == "list")
            return objType;
        if (objType == SemanticType.Str)
            return SemanticType.Str;

        // For other types, return the same type (best effort)
        return objType;
    }
}
