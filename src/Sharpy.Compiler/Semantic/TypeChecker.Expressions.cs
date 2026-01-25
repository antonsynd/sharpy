using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Expression checking (operators, member access, calls, collections)
/// </summary>
public partial class TypeChecker
{
    public SemanticType CheckExpression(Expression expr)
    {
        // Check cache
        var cached = _semanticInfo.GetExpressionType(expr);
        if (cached != null)
            return cached;

        SemanticType type = expr switch
        {
            IntegerLiteral => SemanticType.Int,
            FloatLiteral => SemanticType.Double,
            StringLiteral => SemanticType.Str,
            BooleanLiteral => SemanticType.Bool,
            NoneLiteral => SemanticType.Void,
            Identifier id => CheckIdentifier(id),
            BinaryOp binOp => CheckBinaryOp(binOp),
            UnaryOp unOp => CheckUnaryOp(unOp),
            ComparisonChain chain => CheckComparisonChain(chain),
            SuperExpression superExpr => CheckSuperExpression(superExpr),
            MemberAccess memberAccess => CheckMemberAccess(memberAccess),
            IndexAccess indexAccess => CheckIndexAccess(indexAccess),
            FunctionCall call => CheckFunctionCall(call),
            ListLiteral list => CheckListLiteral(list),
            DictLiteral dict => CheckDictLiteral(dict),
            SetLiteral set => CheckSetLiteral(set),
            TupleLiteral tuple => CheckTupleLiteral(tuple),
            ListComprehension listComp => CheckListComprehension(listComp),
            SetComprehension setComp => CheckSetComprehension(setComp),
            DictComprehension dictComp => CheckDictComprehension(dictComp),
            ConditionalExpression cond => CheckConditionalExpression(cond),
            LambdaExpression lambda => CheckLambda(lambda),
            TypeCast cast => CheckTypeCast(cast),
            TypeCoercion coercion => CheckTypeCoercion(coercion),
            TypeCheck typeCheck => CheckTypeCheck(typeCheck),
            Parenthesized paren => CheckExpression(paren.Expression),
            _ => SemanticType.Unknown
        };

        // Cache the result
        _semanticInfo.SetExpressionType(expr, type);
        return type;
    }

    private SemanticType CheckIdentifier(Identifier id)
    {
        // Special validation for 'self' - must be used inside an instance method
        if (id.Name == "self")
        {
            if (_currentClass == null)
            {
                AddError("'self' can only be used inside instance methods",
                    id.LineStart, id.ColumnStart);
                return SemanticType.Unknown;
            }
            // Normal identifier lookup will follow and find the self parameter
        }

        var symbol = _symbolTable.Lookup(id.Name);
        if (symbol == null)
        {
            AddError($"Undefined identifier '{id.Name}'",
                id.LineStart, id.ColumnStart);
            return SemanticType.Unknown;
        }

        _semanticInfo.SetIdentifierSymbol(id, symbol);

        // Check if this identifier has a narrowed type in the current context
        if (_narrowedTypes.TryGetValue(id.Name, out var narrowedType))
        {
            // Persist the narrowed type for code generation
            // This allows RoslynEmitter to use the narrowed type when generating code
            _semanticInfo.SetNarrowedType(id, narrowedType);
            return narrowedType;
        }

        return symbol switch
        {
            VariableSymbol varSymbol => varSymbol.Type,
            FunctionSymbol funcSymbol => new FunctionType
            {
                ParameterTypes = funcSymbol.Parameters.Select(p => p.Type).ToList(),
                ReturnType = funcSymbol.ReturnType
            },
            ModuleSymbol moduleSymbol => new ModuleType { Symbol = moduleSymbol },
            TypeSymbol => SemanticType.Unknown, // Type names used as values need special handling
            _ => SemanticType.Unknown
        };
    }

    private SemanticType CheckBinaryOp(BinaryOp binOp)
    {
        // Handle pipe forward operator specially - it's a syntactic transformation, not a regular operator
        if (binOp.Operator == BinaryOperator.PipeForward)
        {
            return CheckPipeForward(binOp);
        }

        var leftType = CheckExpression(binOp.Left);
        var rightType = CheckExpression(binOp.Right);

        // If either operand is Unknown, return Unknown to avoid cascading errors
        if (leftType is UnknownType || rightType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Use TypeInferenceService for type inference
        var resultType = _typeInference.InferBinaryOpType(binOp.Operator, leftType, rightType);

        // If type inference fails, report the error directly
        // (V2 validators may not catch all type incompatibilities)
        if (resultType == null)
        {
            AddError(
                $"Type '{leftType.GetDisplayName()}' does not support operator '{GetOperatorSymbol(binOp.Operator)}' with operand of type '{rightType.GetDisplayName()}'",
                binOp.LineStart,
                binOp.ColumnStart);
            return SemanticType.Unknown;
        }

        return resultType;
    }

    /// <summary>
    /// Type-checks the pipe forward operator (|>).
    /// x |> f → f(x)
    /// x |> f(y) → f(x, y) (prepend x to argument list)
    /// x |> f |> g → g(f(x)) (chains via left-associativity)
    /// </summary>
    private SemanticType CheckPipeForward(BinaryOp binOp)
    {
        var leftType = CheckExpression(binOp.Left);

        if (leftType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Case 1: x |> f(y, z) - right side is already a function call
        // We need to re-validate with x prepended to arguments
        if (binOp.Right is FunctionCall funcCall)
        {
            return CheckPipeForwardWithFunctionCall(leftType, funcCall, binOp);
        }

        // Case 2: x |> f - right side is an identifier or expression that should be callable
        var rightType = CheckExpression(binOp.Right);

        if (rightType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Check if right side is a function type
        if (rightType is FunctionType ft)
        {
            // Validate that the function accepts leftType as first argument
            if (ft.ParameterTypes.Count < 1)
            {
                AddError($"Pipe target function takes no arguments, cannot pipe a value to it",
                    binOp.Right.LineStart, binOp.Right.ColumnStart);
                return SemanticType.Unknown;
            }

            if (!leftType.IsAssignableTo(ft.ParameterTypes[0]))
            {
                AddError($"Cannot pipe value of type '{leftType.GetDisplayName()}' to function expecting '{ft.ParameterTypes[0].GetDisplayName()}'",
                    binOp.LineStart, binOp.ColumnStart);
                return SemanticType.Unknown;
            }

            return ft.ReturnType;
        }

        // If right side is an identifier, look up the function symbol
        if (binOp.Right is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);

            if (symbol is FunctionSymbol funcSymbol)
            {
                // Validate argument count
                var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);

                if (requiredParamCount < 1)
                {
                    AddError($"Pipe target function '{id.Name}' takes no required arguments, cannot pipe a value to it",
                        binOp.Right.LineStart, binOp.Right.ColumnStart);
                    return SemanticType.Unknown;
                }

                // Validate the piped value type matches first parameter
                var firstParam = funcSymbol.Parameters[0];
                if (!leftType.IsAssignableTo(firstParam.Type))
                {
                    AddError($"Cannot pipe value of type '{leftType.GetDisplayName()}' to function '{id.Name}' expecting '{firstParam.Type.GetDisplayName()}'",
                        binOp.LineStart, binOp.ColumnStart);
                    return SemanticType.Unknown;
                }

                // Check if remaining required args are satisfied (they must all have defaults)
                if (requiredParamCount > 1)
                {
                    AddError($"Function '{id.Name}' requires {requiredParamCount} arguments but only 1 is provided via pipe",
                        binOp.Right.LineStart, binOp.Right.ColumnStart);
                    return SemanticType.Unknown;
                }

                return funcSymbol.ReturnType;
            }

            if (symbol is TypeSymbol)
            {
                // Constructor call via pipe - x |> SomeClass → SomeClass(x)
                // This is allowed, handled similarly to function call
                AddError($"Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart);
                return SemanticType.Unknown;
            }

            AddError($"'{id.Name}' is not callable",
                binOp.Right.LineStart, binOp.Right.ColumnStart);
            return SemanticType.Unknown;
        }

        // Right side is some other expression that's not callable
        AddError($"Pipe target must be callable, got '{rightType.GetDisplayName()}'",
            binOp.Right.LineStart, binOp.Right.ColumnStart);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Type-checks a pipe forward where the right side is a function call.
    /// x |> f(y, z) → f(x, y, z) - prepend piped value to argument list.
    /// </summary>
    private SemanticType CheckPipeForwardWithFunctionCall(SemanticType pipedType, FunctionCall call, BinaryOp binOp)
    {
        // Get the function being called
        var calleeType = CheckExpression(call.Function);

        // Collect existing argument types
        var existingArgTypes = new List<SemanticType>();
        foreach (var arg in call.Arguments)
        {
            existingArgTypes.Add(CheckExpression(arg));
        }

        // Check keyword arguments and collect their types
        var kwargTypes = new Dictionary<string, SemanticType>();
        foreach (var kwarg in call.KeywordArguments)
        {
            kwargTypes[kwarg.Name] = CheckExpression(kwarg.Value);
        }

        // Build the full argument list: piped value + existing args
        var allArgTypes = new List<SemanticType> { pipedType };
        allArgTypes.AddRange(existingArgTypes);

        // Total argument count includes piped value, positional args, and keyword args
        var totalArgCount = allArgTypes.Count + kwargTypes.Count;

        // Try to resolve the function symbol for better validation
        if (call.Function is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);

            if (symbol is FunctionSymbol funcSymbol)
            {
                // Validate argument count (include both positional and keyword args)
                var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);
                var totalParamCount = funcSymbol.Parameters.Count;

                if (totalArgCount < requiredParamCount || totalArgCount > totalParamCount)
                {
                    if (requiredParamCount == totalParamCount)
                    {
                        AddError($"Function '{id.Name}' expects {totalParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart);
                    }
                    else
                    {
                        AddError($"Function '{id.Name}' expects {requiredParamCount} to {totalParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart);
                    }
                    return SemanticType.Unknown;
                }

                // Validate positional argument types (piped value + call.Arguments)
                for (int i = 0; i < allArgTypes.Count; i++)
                {
                    var argType = allArgTypes[i];
                    var paramType = funcSymbol.Parameters[i].Type;

                    if (!argType.IsAssignableTo(paramType))
                    {
                        var argDesc = i == 0 ? "piped value" : $"argument {i}";
                        AddError($"Cannot pass {argDesc} of type '{argType.GetDisplayName()}' to parameter '{funcSymbol.Parameters[i].Name}' of type '{paramType.GetDisplayName()}'",
                            i == 0 ? binOp.Left.LineStart : call.Arguments[i - 1].LineStart,
                            i == 0 ? binOp.Left.ColumnStart : call.Arguments[i - 1].ColumnStart);
                    }
                }

                // Validate keyword arguments
                foreach (var kwarg in call.KeywordArguments)
                {
                    var param = funcSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                    if (param == null)
                    {
                        AddError($"Unknown keyword argument '{kwarg.Name}'",
                            kwarg.LineStart, kwarg.ColumnStart);
                    }
                    else
                    {
                        // Check if this parameter was already provided positionally (including piped value)
                        var paramIndex = funcSymbol.Parameters.ToList().IndexOf(param);
                        if (paramIndex < allArgTypes.Count)
                        {
                            AddError($"Argument '{kwarg.Name}' was already provided positionally",
                                kwarg.LineStart, kwarg.ColumnStart);
                        }
                        else if (!IsAssignable(kwargTypes[kwarg.Name], param.Type))
                        {
                            AddError($"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{param.Type.GetDisplayName()}'",
                                kwarg.LineStart, kwarg.ColumnStart);
                        }
                    }
                }

                return funcSymbol.ReturnType;
            }

            if (symbol is TypeSymbol typeSymbol)
            {
                // Constructor call via pipe - x |> SomeClass(y) → SomeClass(x, y)
                AddError($"Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart);
                return SemanticType.Unknown;
            }

            if (symbol != null)
            {
                AddError($"'{id.Name}' is not callable",
                    call.LineStart, call.ColumnStart);
                return SemanticType.Unknown;
            }
        }

        // Fallback: check if callee is a FunctionType
        if (calleeType is FunctionType ft)
        {
            if (totalArgCount != ft.ParameterTypes.Count)
            {
                AddError($"Function expects {ft.ParameterTypes.Count} arguments but got {totalArgCount} (including piped value)",
                    call.LineStart, call.ColumnStart);
                return SemanticType.Unknown;
            }

            // Validate positional argument types
            for (int i = 0; i < allArgTypes.Count; i++)
            {
                if (!allArgTypes[i].IsAssignableTo(ft.ParameterTypes[i]))
                {
                    var argDesc = i == 0 ? "piped value" : $"argument {i}";
                    AddError($"Cannot pass {argDesc} of type '{allArgTypes[i].GetDisplayName()}' where '{ft.ParameterTypes[i].GetDisplayName()}' is expected",
                        i == 0 ? binOp.Left.LineStart : call.Arguments[i - 1].LineStart,
                        i == 0 ? binOp.Left.ColumnStart : call.Arguments[i - 1].ColumnStart);
                }
            }

            return ft.ReturnType;
        }

        AddError($"Pipe target must be callable, got '{calleeType.GetDisplayName()}'",
            call.LineStart, call.ColumnStart);
        return SemanticType.Unknown;
    }

    private SemanticType CheckUnaryOp(UnaryOp unOp)
    {
        var operandType = CheckExpression(unOp.Operand);

        // If operand is Unknown, return Unknown to avoid cascading errors
        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Use TypeInferenceService for type inference
        var resultType = _typeInference.InferUnaryOpType(unOp.Operator, operandType);

        // If type inference fails, report the error directly
        if (resultType == null)
        {
            AddError(
                $"Type '{operandType.GetDisplayName()}' does not support unary operator '{GetOperatorSymbol(unOp.Operator)}'",
                unOp.LineStart,
                unOp.ColumnStart);
            return SemanticType.Unknown;
        }

        return resultType;
    }

    private SemanticType CheckComparisonChain(ComparisonChain chain)
    {
        // A comparison chain like "a < b < c" has:
        // - Operands: [a, b, c]
        // - Operators: [LessThan, LessThan]
        // We need to validate each adjacent pair: (a < b) and (b < c)

        // Validate chain structure: operators count should equal operands count minus 1
        // (e.g., 3 operands need 2 operators: a < b < c)
        if (chain.Operands.Length < 2 || chain.Operators.Length != chain.Operands.Length - 1)
        {
            // Malformed chain, just return bool and let parser handle errors
            return SemanticType.Bool;
        }

        // Check all operands and build their types
        var operandTypes = new List<SemanticType>();
        for (int i = 0; i < chain.Operands.Length; i++)
        {
            operandTypes.Add(CheckExpression(chain.Operands[i]));
        }

        // Validate each comparison pair using OperatorValidator
        for (int i = 0; i < chain.Operators.Length; i++)
        {
            var leftType = operandTypes[i];
            var rightType = operandTypes[i + 1];

            // Skip validation if either operand is Unknown to avoid cascading errors
            if (leftType is UnknownType || rightType is UnknownType)
            {
                continue;
            }

            // Map ComparisonOperator to BinaryOperator and validate
            // TODO: Move ComparisonOperatorToBinaryOperator to a utility class
#pragma warning disable CS0618 // Using obsolete OperatorValidator for static utility method
            var binaryOp = OperatorValidator.ComparisonOperatorToBinaryOperator(chain.Operators[i]);
#pragma warning restore CS0618
            var resultType = _typeInference.InferBinaryOpType(binaryOp, leftType, rightType);

            // If type inference fails, report the error directly
            if (resultType == null)
            {
                AddError(
                    $"Type '{leftType.GetDisplayName()}' does not support operator '{GetOperatorSymbol(binaryOp)}' with operand of type '{rightType.GetDisplayName()}'",
                    chain.Operands[i].LineStart,
                    chain.Operands[i].ColumnStart);
            }
        }

        // All comparison chains return bool
        return SemanticType.Bool;
    }

    private SemanticType CheckMemberAccess(MemberAccess memberAccess)
    {
        // Check for super() usage - the parser directly produces SuperExpression for super()
        if (memberAccess.Object is SuperExpression superExpr)
        {
            return ValidateSuperMemberAccess(memberAccess, superExpr);
        }

        var objectType = CheckExpression(memberAccess.Object);

        // Handle null conditional access (?.)
        SemanticType memberLookupType = objectType;
        if (memberAccess.IsNullConditional)
        {
            // Null conditional can only be used on nullable types
            if (objectType is not NullableType nullableObjectType)
            {
                AddError(
                    $"Null conditional operator '?.' can only be used on nullable types, but got '{objectType.GetDisplayName()}'",
                    memberAccess.LineStart, memberAccess.ColumnStart);
                return SemanticType.Unknown;
            }
            // Use the underlying type for member lookup
            memberLookupType = nullableObjectType.UnderlyingType;
        }

        // Handle module member access (e.g., config.MAX_SIZE, utils.helper())
        if (memberLookupType is ModuleType moduleType)
        {
            var moduleSymbol = moduleType.Symbol;
            if (moduleSymbol.Exports.TryGetValue(memberAccess.Member, out var exportedSymbol))
            {
                return exportedSymbol switch
                {
                    VariableSymbol varSymbol => varSymbol.Type,
                    FunctionSymbol funcSymbol => new FunctionType
                    {
                        ParameterTypes = funcSymbol.Parameters.Select(p => p.Type).ToList(),
                        ReturnType = funcSymbol.ReturnType
                    },
                    TypeSymbol typeSymbol => new UserDefinedType { Name = typeSymbol.Name, Symbol = typeSymbol },
                    ModuleSymbol nestedModule => new ModuleType { Symbol = nestedModule },
                    _ => SemanticType.Unknown
                };
            }

            AddError($"Module '{moduleSymbol.Name}' has no member '{memberAccess.Member}'",
                memberAccess.LineStart, memberAccess.ColumnStart);
            return SemanticType.Unknown;
        }

        if (memberLookupType is UserDefinedType udt && udt.Symbol != null)
        {
            // Look for field or property (including inherited fields)
            var (field, fieldOwner) = FindFieldInHierarchy(udt.Symbol, memberAccess.Member);
            if (field != null && fieldOwner != null)
            {
                // Access level validation is handled by AccessValidatorV2 in the validation pipeline

                var fieldType = field.Type;

                // Wrap result in nullable for null conditional access
                if (memberAccess.IsNullConditional && fieldType is not NullableType)
                {
                    return new NullableType { UnderlyingType = fieldType };
                }
                return fieldType;
            }

            // Look for method (including inherited methods)
            var (method, methodOwner) = FindMethodInHierarchy(udt.Symbol, memberAccess.Member);
            if (method != null && methodOwner != null)
            {
                // Access level validation is handled by AccessValidatorV2 in the validation pipeline

                // When accessing a method via member access (obj.method), the object is implicitly
                // bound as the first parameter (self), so we skip it when creating the FunctionType
                var paramTypes = method.Parameters.Skip(1).Select(p => p.Type).ToList();

                var methodFunctionType = new FunctionType
                {
                    ParameterTypes = paramTypes,
                    ReturnType = method.ReturnType
                };

                // For null conditional method access, we don't wrap the FunctionType itself,
                // but the eventual call result should be nullable (handled in CheckFunctionCall)
                return methodFunctionType;
            }

            AddError($"Type '{memberLookupType.GetDisplayName()}' has no member '{memberAccess.Member}'",
                memberAccess.LineStart, memberAccess.ColumnStart);
        }

        return SemanticType.Unknown;
    }

    /// <summary>
    /// Finds a field by name in the type's hierarchy (including parent classes and interfaces).
    /// </summary>
    private (VariableSymbol? Field, TypeSymbol? Owner) FindFieldInHierarchy(TypeSymbol type, string fieldName)
    {
        // First check the type itself
        var field = type.Fields.FirstOrDefault(f => f.Name == fieldName);
        if (field != null)
            return (field, type);

        // Check base class chain
        var current = type.BaseType;
        while (current != null)
        {
            field = current.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (field != null)
                return (field, current);
            current = current.BaseType;
        }

        return (null, null);
    }

    /// <summary>
    /// Finds a method by name in the type's hierarchy (including parent classes and interfaces).
    /// </summary>
    private (FunctionSymbol? Method, TypeSymbol? Owner) FindMethodInHierarchy(TypeSymbol type, string methodName)
    {
        // First check the type itself
        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        if (method != null)
            return (method, type);

        // Check base class chain
        var current = type.BaseType;
        while (current != null)
        {
            method = current.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method != null)
                return (method, current);
            current = current.BaseType;
        }

        // Check interfaces (for method contracts)
        foreach (var iface in type.Interfaces)
        {
            method = iface.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method != null)
                return (method, iface);
        }

        return (null, null);
    }

    private SemanticType CheckIndexAccess(IndexAccess indexAccess)
    {
        // Check if this subscript expression has a narrowed type
        var narrowingKey = ExtractNarrowingKey(indexAccess);
        if (narrowingKey != null && _narrowedTypes.TryGetValue(narrowingKey, out var narrowedType))
        {
            return narrowedType;
        }

        // Special handling for generic type reference: Box[int] or Pair[int, str]
        // This is parsed as IndexAccess(Object: Box, Index: int or TupleLiteral)
        // When the object is a generic type and the index can be resolved as type(s),
        // this represents a generic type with type arguments, not an index operation
        if (indexAccess.Object is Identifier typeId)
        {
            var symbol = _symbolTable.Lookup(typeId.Name);

            // Handle generic type reference (e.g., Box[int])
            if (symbol is TypeSymbol genericTypeSymbol && genericTypeSymbol.IsGeneric)
            {
                var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                if (typeArgs != null)
                {
                    // Return a GenericType representing the instantiated type
                    return new GenericType
                    {
                        Name = genericTypeSymbol.Name,
                        TypeArguments = typeArgs,
                        GenericDefinition = genericTypeSymbol
                    };
                }
            }

            // Handle generic function reference (e.g., identity[int])
            // This creates a special "instantiated generic function" type for use in function calls
            if (symbol is FunctionSymbol genericFuncSymbol && genericFuncSymbol.IsGeneric)
            {
                var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                if (typeArgs != null)
                {
                    // Store the type arguments in SemanticInfo for use in CheckFunctionCall
                    _semanticInfo.SetExpressionType(indexAccess, new GenericFunctionType
                    {
                        FunctionSymbol = genericFuncSymbol,
                        TypeArguments = typeArgs
                    });
                    return _semanticInfo.GetExpressionType(indexAccess)!;
                }
            }
        }

        var objectType = CheckExpression(indexAccess.Object);
        var indexType = CheckExpression(indexAccess.Index);

        // Use TypeInferenceService for type inference (errors reported by V2 validator in pipeline)
        var resultType = _typeInference.InferIndexAccessType(objectType, indexType);

        // TypeInferenceService covers all supported operations - return Unknown for unsupported
        return resultType ?? SemanticType.Unknown;
    }

    private SemanticType CheckFunctionCall(FunctionCall call)
    {
        // Check if this is a null conditional method call (obj?.method())
        bool isNullConditionalCall = call.Function is MemberAccess { IsNullConditional: true };

        // Check the called expression type first
        var calleeType = CheckExpression(call.Function);

        // Track super().__init__() calls AFTER validation completes
        // (do this after CheckExpression so the validation doesn't see it as already called)
        if (call.Function is MemberAccess ma && ma.Object is SuperExpression && ma.Member == "__init__")
        {
            _superInitCalled = true;
        }

        // Check arguments and collect their types
        var argTypes = new List<SemanticType>();
        foreach (var arg in call.Arguments)
        {
            argTypes.Add(CheckExpression(arg));
        }

        // Check keyword arguments and collect their types
        var kwargTypes = new Dictionary<string, SemanticType>();
        foreach (var kwarg in call.KeywordArguments)
        {
            kwargTypes[kwarg.Name] = CheckExpression(kwarg.Value);
        }

        // Total argument count includes both positional and keyword arguments
        var totalArgCount = argTypes.Count + kwargTypes.Count;

        // Try to get the function symbol directly for better validation
        FunctionSymbol? funcSymbol = null;

        // Special handling for generic type instantiation: Box[int](42) or Pair[int, str](1, "a")
        // This is parsed as FunctionCall(Function: IndexAccess(Object: Box, Index: int or TupleLiteral), Arguments: [...])
        if (call.Function is IndexAccess indexAccess &&
            indexAccess.Object is Identifier genericTypeId &&
            _symbolTable.Lookup(genericTypeId.Name) is TypeSymbol genericTypeSymbol &&
            genericTypeSymbol.IsGeneric)
        {
            // The "index" is actually type argument(s) - try to resolve them as types
            var typeArgs = TryResolveTypeArguments(indexAccess.Index);
            if (typeArgs != null)
            {
                // Cannot instantiate abstract classes
                if (genericTypeSymbol.IsAbstract)
                {
                    AddError($"Cannot instantiate abstract class '{genericTypeSymbol.Name}'",
                        call.LineStart, call.ColumnStart);
                    return SemanticType.Unknown;
                }

                // Return a GenericType with the type arguments
                return new GenericType
                {
                    Name = genericTypeSymbol.Name,
                    TypeArguments = typeArgs,
                    GenericDefinition = genericTypeSymbol
                };
            }
        }

        // Handle generic function call: identity[int](42)
        // The calleeType will be GenericFunctionType from CheckIndexAccess
        if (calleeType is GenericFunctionType genericFuncType)
        {
            // For now, just return the substituted return type
            // We substitute type parameters with type arguments in the return type
            var substitutedReturnType = SubstituteTypeParameters(
                genericFuncType.FunctionSymbol.ReturnType,
                genericFuncType.FunctionSymbol.TypeParameters,
                genericFuncType.TypeArguments);
            return substitutedReturnType;
        }

        if (call.Function is Identifier id)
        {
            // Special handling for builtin len() - validate that argument supports __len__ protocol
            // TODO: Consider using a constant from BuiltinRegistry or BuiltinNames class instead of hardcoded string
            if (id.Name == "len" && argTypes.Count == 1)
            {
                // Use TypeInferenceService for type inference (errors reported by V2 validator in pipeline)
                var lenType = _typeInference.InferLenType(argTypes[0]);

                // TypeInferenceService always returns Int for len() - return Unknown only if completely unsupported
                return lenType ?? SemanticType.Unknown;
            }

            var symbol = _symbolTable.Lookup(id.Name);

            // Special handling for constructor calls (calling a type)
            if (symbol is TypeSymbol typeSymbol)
            {
                // For primitive types (int, float, str, bool, long, etc.), route to builtin function overloads
                // instead of treating as constructor. This matches Python semantics where int(x) calls
                // the int conversion function, not constructs a new int object.
                var primitiveOverloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
                if (primitiveOverloads != null && primitiveOverloads.Count > 0 && typeSymbol.ClrType != null && typeSymbol.ClrType.IsPrimitive)
                {
                    // Route to builtin function overload resolution below
                    // (fall through to overload handling)
                }
                else
                {
                    // Cannot instantiate abstract classes
                    if (typeSymbol.IsAbstract)
                    {
                        AddError($"Cannot instantiate abstract class '{typeSymbol.Name}'",
                            call.LineStart, call.ColumnStart);
                        return SemanticType.Unknown;
                    }

                    // Constructor call returns an instance of the type
                    return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
                }
            }

            funcSymbol = symbol as FunctionSymbol;

            // If we found a symbol but it's not a function or type, it's not callable
            if (symbol != null && funcSymbol == null && symbol is not TypeSymbol)
            {
                AddError($"'{id.Name}' is not callable (type: {calleeType.GetDisplayName()})",
                    call.LineStart, call.ColumnStart);
                return SemanticType.Unknown;
            }

            // Special handling for builtin functions with overloads
            // When there are multiple overloads, we need to perform overload resolution to find the right one.
            // The funcSymbol from Lookup is just the first overload, which may not match the call.
            // Only use builtin overloads if there's no user-defined function shadowing the builtin.
            var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
            var isBuiltinWithOverloads = overloads != null && overloads.Count > 1;
            // If funcSymbol was found in symbol table AND it's one of the builtin overloads, use overload resolution
            var needsOverloadResolution = isBuiltinWithOverloads &&
                (funcSymbol == null || (funcSymbol != null && overloads!.Contains(funcSymbol)));
            if (needsOverloadResolution)
            {
                // First pass: filter by argument count (considering default parameters and variadic parameters)
                var candidateOverloads = overloads.Where(o =>
                {
                    var requiredParams = o.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
                    var hasVariadic = o.Parameters.Any(p => p.IsVariadic);
                    var totalParams = o.Parameters.Count;
                    // Variadic functions can accept any number of arguments >= required
                    if (hasVariadic)
                        return totalArgCount >= requiredParams;
                    return totalArgCount >= requiredParams && totalArgCount <= totalParams;
                }).ToList();

                // Second pass: check type compatibility
                FunctionSymbol? matchingOverload = null;
                foreach (var overload in candidateOverloads)
                {
                    bool typesMatch = true;
                    var hasVariadic = overload.Parameters.Any(p => p.IsVariadic);
                    var variadicParam = overload.Parameters.FirstOrDefault(p => p.IsVariadic);

                    for (int i = 0; i < argTypes.Count; i++)
                    {
                        SemanticType expectedType;
                        if (i < overload.Parameters.Count && !overload.Parameters[i].IsVariadic)
                        {
                            // Regular parameter
                            expectedType = overload.Parameters[i].Type;
                        }
                        else if (variadicParam != null)
                        {
                            // Variadic parameter - all remaining args must match the element type
                            expectedType = variadicParam.Type;
                        }
                        else
                        {
                            // Index out of bounds - shouldn't happen with valid candidates
                            typesMatch = false;
                            break;
                        }

                        if (!IsAssignable(argTypes[i], expectedType))
                        {
                            typesMatch = false;
                            break;
                        }
                    }
                    if (typesMatch)
                    {
                        matchingOverload = overload;
                        break;
                    }
                }

                if (matchingOverload != null)
                {
                    // Update the identifier symbol to point to the matching overload
                    _semanticInfo.SetIdentifierSymbol(id, matchingOverload);
                    return matchingOverload.ReturnType;
                }
                else
                {
                    // No matching overload found
                    var expectedCounts = string.Join(" or ", overloads.Select(o =>
                    {
                        var required = o.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
                        var total = o.Parameters.Count;
                        var hasVariadic = o.Parameters.Any(p => p.IsVariadic);
                        if (hasVariadic)
                            return $"{required}+";
                        return required == total ? total.ToString() : $"{required}-{total}";
                    }).Distinct());
                    AddError($"Function '{id.Name}' expects {expectedCounts} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart);
                    return SemanticType.Unknown;
                }
            }
        }
        // Handle member access function calls (e.g., module.function() or obj.method())
        // Skip super() calls - they're already validated by ValidateSuperMemberAccess
        else if (call.Function is MemberAccess memberAccessCall && memberAccessCall.Object is not SuperExpression)
        {
            // For module member access (lib.math.add), we already checked the expression at line 1697
            // which gave us a FunctionType. But we also need the FunctionSymbol for better validation.
            // The calleeType already validated that memberAccessCall resolves to a function.
            // We just need to extract the FunctionSymbol.

            // Note: We can't just call CheckExpression again on memberAccessCall.Object because
            // it was already called as part of checking call.Function at line 1697.
            // Instead, we need to use the already-computed calleeType information or
            // re-traverse the member access to find the function symbol.

            // Since the semantic info stores resolved symbols, let's try to get the function symbol
            // from the semantic info for the member access.
            // But actually, the issue is that CheckMemberAccess returns a FunctionType but doesn't
            // store the underlying FunctionSymbol anywhere we can retrieve it.

            // The best approach is to re-evaluate the object to get the module, then lookup the member.
            // This is duplicate work but necessary until we refactor to store symbols in SemanticInfo.
            var objectType = CheckExpression(memberAccessCall.Object);
            if (objectType is ModuleType moduleType)
            {
                var moduleSymbol = moduleType.Symbol;
                if (moduleSymbol.Exports.TryGetValue(memberAccessCall.Member, out var exportedSymbol))
                {
                    funcSymbol = exportedSymbol as FunctionSymbol;
                }
            }
        }

        // If we have a FunctionSymbol, use it for validation (supports default parameters)
        if (funcSymbol != null)
        {
            // Count required parameters (those without defaults)
            var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);
            var totalParamCount = funcSymbol.Parameters.Count;

            // Validate argument count considering defaults (include both positional and keyword args)
            if (totalArgCount < requiredParamCount || totalArgCount > totalParamCount)
            {
                if (requiredParamCount == totalParamCount)
                {
                    AddError($"Function expects {totalParamCount} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart);
                }
                else
                {
                    AddError($"Function expects {requiredParamCount} to {totalParamCount} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart);
                }
            }
            else
            {
                // Validate positional argument types
                for (int i = 0; i < argTypes.Count; i++)
                {
                    if (!IsAssignable(argTypes[i], funcSymbol.Parameters[i].Type))
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{funcSymbol.Parameters[i].Type.GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart);
                    }
                }

                // Validate keyword arguments
                foreach (var kwarg in call.KeywordArguments)
                {
                    var param = funcSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                    if (param == null)
                    {
                        AddError($"Unknown keyword argument '{kwarg.Name}'",
                            kwarg.LineStart, kwarg.ColumnStart);
                    }
                    else
                    {
                        // Check if this parameter was already provided positionally
                        var paramIndex = funcSymbol.Parameters.ToList().IndexOf(param);
                        if (paramIndex < argTypes.Count)
                        {
                            AddError($"Argument '{kwarg.Name}' was already provided positionally",
                                kwarg.LineStart, kwarg.ColumnStart);
                        }
                        else if (!IsAssignable(kwargTypes[kwarg.Name], param.Type))
                        {
                            AddError($"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{param.Type.GetDisplayName()}'",
                                kwarg.LineStart, kwarg.ColumnStart);
                        }
                    }
                }
            }

            var returnType = funcSymbol.ReturnType;

            // Wrap result in nullable for null conditional calls
            if (isNullConditionalCall && returnType is not NullableType)
            {
                return new NullableType { UnderlyingType = returnType };
            }
            return returnType;
        }

        // Fallback to FunctionType validation (no default parameter support)
        var funcType = CheckExpression(call.Function);

        if (funcType is FunctionType ft)
        {
            // Skip validation for .NET types with multiple constructor overloads
            // (C# compiler will handle overload resolution)
            if (!ft.SkipArgumentValidation)
            {
                // Validate argument count (include both positional and keyword arguments)
                if (totalArgCount != ft.ParameterTypes.Count)
                {
                    AddError($"Function expects {ft.ParameterTypes.Count} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart);
                }
                else
                {
                    // Validate positional argument types
                    for (int i = 0; i < argTypes.Count; i++)
                    {
                        if (!IsAssignable(argTypes[i], ft.ParameterTypes[i]))
                        {
                            AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{ft.ParameterTypes[i].GetDisplayName()}'",
                                call.Arguments[i].LineStart, call.Arguments[i].ColumnStart);
                        }
                    }
                }
            }

            var returnType = ft.ReturnType;

            // Wrap result in nullable for null conditional calls
            if (isNullConditionalCall && returnType is not NullableType)
            {
                return new NullableType { UnderlyingType = returnType };
            }
            return returnType;
        }

        return SemanticType.Unknown;
    }

    /// <summary>
    /// Tries to resolve an expression as a type. This is used for generic type instantiation
    /// where Box[int](42) parses the type argument as an expression.
    /// Returns null if the expression cannot be interpreted as a type.
    /// </summary>
    private SemanticType? TryResolveExpressionAsType(Expression expr)
    {
        // Handle simple identifier as type name (e.g., "int", "str", "MyClass")
        if (expr is Identifier typeId)
        {
            // Create a synthetic type annotation and resolve it
            var typeAnnotation = new Parser.Ast.TypeAnnotation
            {
                Name = typeId.Name,
                LineStart = expr.LineStart,
                ColumnStart = expr.ColumnStart
            };
            var resolved = _typeResolver.ResolveTypeAnnotation(typeAnnotation);
            return resolved != SemanticType.Unknown ? resolved : null;
        }

        // Handle nested generic types (e.g., Box[int] in Container[Box[int]])
        if (expr is IndexAccess indexAccess &&
            indexAccess.Object is Identifier nestedTypeId &&
            _symbolTable.Lookup(nestedTypeId.Name) is TypeSymbol nestedGenericType &&
            nestedGenericType.IsGeneric)
        {
            var nestedTypeArgs = TryResolveTypeArguments(indexAccess.Index);
            if (nestedTypeArgs != null)
            {
                return new GenericType
                {
                    Name = nestedGenericType.Name,
                    TypeArguments = nestedTypeArgs,
                    GenericDefinition = nestedGenericType
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Tries to resolve one or more type arguments from an index expression.
    /// Handles both single type arguments (int) and multiple type arguments (int, str as TupleLiteral).
    /// Returns null if the expressions cannot be interpreted as types.
    /// </summary>
    private List<SemanticType>? TryResolveTypeArguments(Expression indexExpr)
    {
        var typeArgs = new List<SemanticType>();

        // Handle multiple type arguments: Pair[int, str] parses as TupleLiteral
        if (indexExpr is TupleLiteral tuple)
        {
            foreach (var element in tuple.Elements)
            {
                var typeArg = TryResolveExpressionAsType(element);
                if (typeArg == null)
                    return null;
                typeArgs.Add(typeArg);
            }
            return typeArgs;
        }

        // Handle single type argument
        var singleTypeArg = TryResolveExpressionAsType(indexExpr);
        if (singleTypeArg == null)
            return null;
        typeArgs.Add(singleTypeArg);
        return typeArgs;
    }

    private SemanticType CheckListLiteral(ListLiteral list)
    {
        if (list.Elements.Length == 0)
        {
            return new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown }
            };
        }

        var elementTypes = list.Elements.Select(CheckExpression).ToList();
        var commonType = elementTypes[0];

        // Try to find common type
        foreach (var elemType in elementTypes.Skip(1))
        {
            if (!IsAssignable(elemType, commonType))
            {
                commonType = SemanticType.Unknown;
                break;
            }
        }

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
            return new GenericType
            {
                Name = "dict",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown, SemanticType.Unknown }
            };
        }

        var keyTypes = dict.Entries.Select(e => CheckExpression(e.Key)).ToList();
        var valueTypes = dict.Entries.Select(e => CheckExpression(e.Value)).ToList();

        var commonKeyType = keyTypes[0];
        var commonValueType = valueTypes[0];

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
            return new GenericType
            {
                Name = "set",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown }
            };
        }

        var elementTypes = set.Elements.Select(CheckExpression).ToList();
        var commonType = elementTypes[0];

        return new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { commonType }
        };
    }

    private SemanticType CheckTupleLiteral(TupleLiteral tuple)
    {
        var elementTypes = tuple.Elements.Select(CheckExpression).ToList();
        return new TupleType { ElementTypes = elementTypes };
    }

    private SemanticType CheckListComprehension(ListComprehension listComp)
    {
        // Enter comprehension scope (variables don't leak)
        _symbolTable.EnterScope("list-comprehension");

        // Process clauses in order
        foreach (var clause in listComp.Clauses)
        {
            if (clause is ForClause forClause)
            {
                // Check iterator type and infer element type (errors reported by V2 validator in pipeline)
                var iterType = CheckExpression(forClause.Iterator);
                var elemType = _typeInference.InferIterableElementType(iterType) ?? SemanticType.Unknown;

                // Define loop variable (single identifier only for now)
                if (forClause.Target is Identifier id)
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
                    _symbolTable.Define(loopVarSymbol);
                    _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
                    _semanticInfo.SetExpressionType(forClause.Target, elemType);
                }
                else
                {
                    // For tuple unpacking or other complex targets
                    // TODO: Implement tuple unpacking in comprehensions
                    AddError($"Tuple unpacking in comprehensions not yet supported",
                        forClause.LineStart, forClause.ColumnStart);
                }
            }
            else if (clause is IfClause ifClause)
            {
                // Check condition is boolean
                var condType = CheckExpression(ifClause.Condition);
                if (!condType.IsAssignableTo(SemanticType.Bool))
                {
                    AddError($"Comprehension filter must be bool, got '{condType.GetDisplayName()}'",
                        ifClause.LineStart, ifClause.ColumnStart);
                }
            }
        }

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

        // Process clauses in order
        foreach (var clause in setComp.Clauses)
        {
            if (clause is ForClause forClause)
            {
                // Check iterator type and infer element type (errors reported by V2 validator in pipeline)
                var iterType = CheckExpression(forClause.Iterator);
                var elemType = _typeInference.InferIterableElementType(iterType) ?? SemanticType.Unknown;

                // Define loop variable (single identifier only for now)
                if (forClause.Target is Identifier id)
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
                    _symbolTable.Define(loopVarSymbol);
                    _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
                    _semanticInfo.SetExpressionType(forClause.Target, elemType);
                }
                else
                {
                    // For tuple unpacking or other complex targets
                    AddError($"Tuple unpacking in comprehensions not yet supported",
                        forClause.LineStart, forClause.ColumnStart);
                }
            }
            else if (clause is IfClause ifClause)
            {
                // Check condition is boolean
                var condType = CheckExpression(ifClause.Condition);
                if (!condType.IsAssignableTo(SemanticType.Bool))
                {
                    AddError($"Comprehension filter must be bool, got '{condType.GetDisplayName()}'",
                        ifClause.LineStart, ifClause.ColumnStart);
                }
            }
        }

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

        // Process clauses in order
        foreach (var clause in dictComp.Clauses)
        {
            if (clause is ForClause forClause)
            {
                // Check iterator type and infer element type (errors reported by V2 validator in pipeline)
                var iterType = CheckExpression(forClause.Iterator);
                var elemType = _typeInference.InferIterableElementType(iterType) ?? SemanticType.Unknown;

                // Define loop variable (single identifier only for now)
                if (forClause.Target is Identifier id)
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
                    _symbolTable.Define(loopVarSymbol);
                    _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
                    _semanticInfo.SetExpressionType(forClause.Target, elemType);
                }
                else
                {
                    // For tuple unpacking or other complex targets
                    AddError($"Tuple unpacking in comprehensions not yet supported",
                        forClause.LineStart, forClause.ColumnStart);
                }
            }
            else if (clause is IfClause ifClause)
            {
                // Check condition is boolean
                var condType = CheckExpression(ifClause.Condition);
                if (!condType.IsAssignableTo(SemanticType.Bool))
                {
                    AddError($"Comprehension filter must be bool, got '{condType.GetDisplayName()}'",
                        ifClause.LineStart, ifClause.ColumnStart);
                }
            }
        }

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

    private SemanticType CheckConditionalExpression(ConditionalExpression cond)
    {
        CheckExpression(cond.Test);
        var thenType = CheckExpression(cond.ThenValue);
        var elseType = CheckExpression(cond.ElseValue);

        // Return common type
        if (thenType.IsAssignableTo(elseType))
            return elseType;
        if (elseType.IsAssignableTo(thenType))
            return thenType;

        return SemanticType.Unknown;
    }

    private SemanticType CheckLambda(LambdaExpression lambda)
    {
        var paramTypes = lambda.Parameters
            .Select(p => _typeResolver.ResolveTypeAnnotation(p.Type))
            .ToList();

        // Enter lambda scope
        _symbolTable.EnterScope("lambda");

        foreach (var param in lambda.Parameters)
        {
            var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);
            var paramSymbol = new VariableSymbol
            {
                Name = param.Name,
                Kind = SymbolKind.Parameter,
                Type = paramType,
                IsParameter = true
            };
            _symbolTable.Define(paramSymbol);
        }

        var bodyType = CheckExpression(lambda.Body);

        _symbolTable.ExitScope();

        return new FunctionType
        {
            ParameterTypes = paramTypes,
            ReturnType = bodyType
        };
    }

    private SemanticType CheckTypeCast(TypeCast cast)
    {
        CheckExpression(cast.Value);
        return _typeResolver.ResolveTypeAnnotation(cast.TargetType);
    }

    /// <summary>
    /// Type-checks a type coercion expression (value to Type).
    /// Validates that the coercion is valid per the language specification.
    /// </summary>
    private SemanticType CheckTypeCoercion(TypeCoercion coercion)
    {
        var sourceType = CheckExpression(coercion.Value);
        var targetType = _typeResolver.ResolveTypeAnnotation(coercion.TargetType);

        // If either type is unknown, skip validation to avoid cascading errors
        if (sourceType is UnknownType || targetType is UnknownType)
        {
            return targetType;
        }

        // Get the underlying target type (strip nullable wrapper if present)
        var underlyingTargetType = targetType is NullableType nullable ? nullable.UnderlyingType : targetType;

        // Validate the coercion
        ValidateTypeCoercion(coercion, sourceType, underlyingTargetType);

        return targetType;
    }

    /// <summary>
    /// Validates that a type coercion is valid per the language specification.
    /// Reports errors for invalid casts.
    /// </summary>
    private void ValidateTypeCoercion(TypeCoercion coercion, SemanticType sourceType, SemanticType targetType)
    {
        // Unboxing: object to any type is valid (runtime check) - check this first
        if (IsObjectType(sourceType))
        {
            return; // Valid
        }

        // Numeric to numeric conversions are always valid (may throw at runtime for narrowing)
        if (PrimitiveCatalog.IsNumeric(sourceType) && PrimitiveCatalog.IsNumeric(targetType))
        {
            return; // Valid
        }

        // Check for invalid numeric/bool to string conversion
        // This is a common mistake - users should use str(x) instead
        if (IsStringType(targetType))
        {
            var sourceInfo = PrimitiveCatalog.GetPrimitiveInfo(sourceType);
            if (sourceInfo != null && sourceInfo.ClrType != typeof(string))
            {
                // Source is a primitive but not string - reject
                AddError(
                    $"Cannot cast '{sourceType.GetDisplayName()}' to 'str'. Use str(...) instead.",
                    coercion.LineStart, coercion.ColumnStart);
                return;
            }
        }

        // Check for valid reference type casts (inheritance relationship or interface implementation)
        if (!CanPotentiallyCast(sourceType, targetType))
        {
            AddError(
                $"Cannot cast '{sourceType.GetDisplayName()}' to '{targetType.GetDisplayName()}' (no inheritance relationship).",
                coercion.LineStart, coercion.ColumnStart);
        }
    }

    /// <summary>
    /// Returns true if the type is the str/string type.
    /// </summary>
    private static bool IsStringType(SemanticType type)
    {
        return type is BuiltinType builtin && (builtin.Name == "str" || builtin.Name == "string");
    }

    /// <summary>
    /// Returns true if the type is the object type.
    /// </summary>
    private static bool IsObjectType(SemanticType type)
    {
        return type is BuiltinType builtin && builtin.Name == "object";
    }

    /// <summary>
    /// Determines if a cast between two types COULD potentially succeed at runtime.
    /// Returns true if there's an inheritance relationship, interface implementation, or unboxing potential.
    /// Returns false if the cast is statically impossible.
    /// </summary>
    private bool CanPotentiallyCast(SemanticType source, SemanticType target)
    {
        // Same type is always castable
        if (source.Equals(target))
            return true;

        // Both must be user-defined types for inheritance checks
        if (source is UserDefinedType sourceUdt && target is UserDefinedType targetUdt)
        {
            // Check if source inherits from target (downcast - always safe)
            if (InheritsFrom(sourceUdt.Symbol, targetUdt.Symbol))
                return true;

            // Check if target inherits from source (upcast - runtime check)
            if (InheritsFrom(targetUdt.Symbol, sourceUdt.Symbol))
                return true;

            // Check if target is an interface that could be implemented
            if (targetUdt.Symbol?.TypeKind == TypeKind.Interface)
                return true;

            // Check if source is an interface that the target could implement
            if (sourceUdt.Symbol?.TypeKind == TypeKind.Interface)
                return true;

            // No relationship found
            return false;
        }

        // Interface casting is always potentially valid at runtime
        if (source is UserDefinedType && target is UserDefinedType targetType && targetType.Symbol?.TypeKind == TypeKind.Interface)
            return true;

        // Builtin to user-defined: only valid if unboxing from object
        if (source is BuiltinType sourceBuiltin && sourceBuiltin.Name == "object")
            return true;

        // User-defined to builtin: only valid for boxing to object
        if (target is BuiltinType targetBuiltin && targetBuiltin.Name == "object")
            return true;

        // For generic types, check the base definition
        if (source is GenericType sourceGeneric && target is GenericType targetGeneric)
        {
            // Same generic definition with potentially different type args
            if (sourceGeneric.GenericDefinition?.Name == targetGeneric.GenericDefinition?.Name)
                return true;
        }

        // Default: allow if types don't fit the checked categories (to be conservative)
        // This handles edge cases and allows the C# compiler to do final validation
        return true;
    }

    /// <summary>
    /// Checks if a type symbol inherits from another type symbol (directly or indirectly).
    /// </summary>
    private bool InheritsFrom(TypeSymbol? derived, TypeSymbol? baseType)
    {
        if (derived == null || baseType == null)
            return false;

        var current = derived.BaseType;
        while (current != null)
        {
            if (current == baseType || current.Name == baseType.Name)
                return true;
            current = current.BaseType;
        }

        // Also check interfaces
        foreach (var iface in derived.Interfaces)
        {
            if (iface == baseType || iface.Name == baseType.Name)
                return true;
        }

        return false;
    }

    private SemanticType CheckTypeCheck(TypeCheck typeCheck)
    {
        CheckExpression(typeCheck.Value);
        _typeResolver.ResolveTypeAnnotation(typeCheck.CheckType);
        return SemanticType.Bool;
    }

    /// <summary>
    /// Check if a type is numeric (int, long, float, double, etc.).
    /// Delegates to PrimitiveCatalog for exhaustive primitive type checking.
    /// Note: Also allows Unknown types to avoid cascading errors.
    /// </summary>
    private static bool IsNumericType(SemanticType type)
        => type is UnknownType || PrimitiveCatalog.IsNumeric(type);

    /// <summary>
    /// Extract narrowed types from a conditional expression
    /// </summary>

    /// <summary>
    /// Gets the human-readable symbol for a binary operator.
    /// </summary>
    private static string GetOperatorSymbol(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.FloorDivide => "//",
        BinaryOperator.Modulo => "%",
        BinaryOperator.Power => "**",
        BinaryOperator.BitwiseAnd => "&",
        BinaryOperator.BitwiseOr => "|",
        BinaryOperator.BitwiseXor => "^",
        BinaryOperator.LeftShift => "<<",
        BinaryOperator.RightShift => ">>",
        BinaryOperator.LessThan => "<",
        BinaryOperator.LessThanOrEqual => "<=",
        BinaryOperator.GreaterThan => ">",
        BinaryOperator.GreaterThanOrEqual => ">=",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.And => "and",
        BinaryOperator.Or => "or",
        BinaryOperator.Is => "is",
        BinaryOperator.IsNot => "is not",
        BinaryOperator.In => "in",
        BinaryOperator.NotIn => "not in",
        BinaryOperator.NullCoalesce => "??",
        BinaryOperator.PipeForward => "|>",
        _ => op.ToString()
    };

    /// <summary>
    /// Gets the human-readable symbol for a unary operator.
    /// </summary>
    private static string GetOperatorSymbol(UnaryOperator op) => op switch
    {
        UnaryOperator.Minus => "-",
        UnaryOperator.Plus => "+",
        UnaryOperator.Not => "not",
        UnaryOperator.BitwiseNot => "~",
        _ => op.ToString()
    };
}
