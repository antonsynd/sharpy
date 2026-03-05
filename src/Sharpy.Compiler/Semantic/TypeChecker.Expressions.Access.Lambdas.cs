using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Lambda expressions and parameter type inference
/// </summary>
internal partial class TypeChecker
{
    private SemanticType CheckLambda(LambdaExpression lambda)
    {
        // Use _expectedType for bidirectional type inference: if the context expects
        // a FunctionType, extract parameter types from it to infer lambda parameter types.
        FunctionType? expectedFunc = _expectedType as FunctionType;

        var paramTypes = new List<SemanticType>();
        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var param = lambda.Parameters[i];
            if (param.Type != null)
            {
                // Explicit type annotation — use it
                paramTypes.Add(_typeResolver.ResolveTypeAnnotation(param.Type));
            }
            else if (expectedFunc != null && i < expectedFunc.ParameterTypes.Count)
            {
                // Infer from expected function type context
                paramTypes.Add(expectedFunc.ParameterTypes[i]);
            }
            else
            {
                paramTypes.Add(SemanticType.Unknown);
            }
        }

        // If any params are still Unknown (no target type context, no annotations),
        // try to infer types from the lambda body. This enables partial application
        // lowering (e.g., add(5, _) → lambda __placeholder_0: add(5, __placeholder_0))
        // to work without explicit type annotations.
        if (paramTypes.Any(t => t is UnknownType))
        {
            TryInferLambdaParamTypesFromBody(lambda, paramTypes);
        }

        // Enter lambda scope
        _symbolTable.EnterScope("lambda");

        // Enter an isolated narrowing scope for this lambda.
        // Type narrowings from the enclosing scope should NOT be visible inside the lambda,
        // because lambdas can be stored and called later when the narrowing condition no longer holds.
        // This is the same logic as for nested function definitions (task 1.7).
        using var _ = _narrowingContext.EnterIsolatedScope();

        // Lambdas cannot be async, so await inside a lambda is invalid
        // (matches Python: await in lambda produces SyntaxError).
        var previousIsAsync = _currentFunctionIsAsync;
        _currentFunctionIsAsync = false;

        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var paramSymbol = new VariableSymbol
            {
                Name = lambda.Parameters[i].Name,
                Kind = SymbolKind.Parameter,
                Type = paramTypes[i],
                IsParameter = true
            };
            _symbolTable.Define(paramSymbol);
        }

        // Type-check default value expressions and validate compatibility
        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var param = lambda.Parameters[i];
            if (param.DefaultValue != null)
            {
                var defaultType = CheckExpression(param.DefaultValue);
                if (paramTypes[i] is not UnknownType && !IsAssignable(defaultType, paramTypes[i]))
                {
                    AddError(
                        $"Default value of type '{defaultType.GetDisplayName()}' is not assignable to parameter type '{paramTypes[i].GetDisplayName()}'",
                        param.DefaultValue.LineStart, param.DefaultValue.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: param.DefaultValue.Span);
                }
            }
        }

        var bodyType = CheckExpression(lambda.Body);

        _currentFunctionIsAsync = previousIsAsync;
        _symbolTable.ExitScope();

        var optionalParamCount = lambda.Parameters.Count(p => p.DefaultValue != null);

        return new FunctionType
        {
            ParameterTypes = paramTypes,
            ReturnType = bodyType,
            OptionalParameterCount = optionalParamCount
        };
    }

    /// <summary>
    /// Try to infer Unknown lambda parameter types from the lambda body.
    /// Handles partial application lowering where the body is a FunctionCall,
    /// BinaryOp, UnaryOp, or ComparisonChain containing placeholder parameters.
    /// </summary>
    private void TryInferLambdaParamTypesFromBody(LambdaExpression lambda, List<SemanticType> paramTypes)
    {
        // Build a map from placeholder param name to its index
        var unknownParams = new Dictionary<string, int>();
        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            if (paramTypes[i] is UnknownType)
                unknownParams[lambda.Parameters[i].Name] = i;
        }

        if (unknownParams.Count == 0)
            return;

        switch (lambda.Body)
        {
            case FunctionCall call:
                InferParamTypesFromCall(call, unknownParams, paramTypes);
                break;
            case BinaryOp binOp:
                InferParamTypesFromBinaryOp(binOp, unknownParams, paramTypes);
                break;
            case UnaryOp unaryOp:
                InferParamTypesFromUnaryOp(unaryOp, unknownParams, paramTypes);
                break;
            case ComparisonChain chain:
                InferParamTypesFromComparison(chain, unknownParams, paramTypes);
                break;
        }
    }

    /// <summary>
    /// Infer placeholder types from a function call body (e.g., add(5, __placeholder_0)).
    /// Looks up the called function's signature and maps placeholder args to param types.
    /// </summary>
    private void InferParamTypesFromCall(FunctionCall call, Dictionary<string, int> unknownParams, List<SemanticType> paramTypes)
    {
        // Resolve the function being called to get its parameter types
        List<ParameterSymbol>? funcParams = null;

        if (call.Function is Identifier funcId)
        {
            var symbol = _symbolTable.Lookup(funcId.Name);
            if (symbol is FunctionSymbol fs)
                funcParams = fs.Parameters;
        }
        else if (call.Function is MemberAccess memberAccess)
        {
            // For method calls like obj.method(_, y), resolve the method
            var objType = TryResolveExpressionType(memberAccess.Object);
            if (objType != null)
            {
                var methodSymbol = TryResolveMember(objType, memberAccess.Member);
                if (methodSymbol is FunctionSymbol fs)
                    funcParams = fs.Parameters;
            }
        }

        if (funcParams == null)
            return;

        // Match positional arguments to function parameters
        // Skip 'self' parameter for instance methods
        int paramOffset = 0;
        if (funcParams.Count > 0 && funcParams[0].Name == "self")
            paramOffset = 1;

        for (int argIdx = 0; argIdx < call.Arguments.Length; argIdx++)
        {
            int funcParamIdx = argIdx + paramOffset;
            if (funcParamIdx >= funcParams.Count)
                break;

            if (call.Arguments[argIdx] is Identifier id && unknownParams.TryGetValue(id.Name, out int placeholderIdx))
            {
                var expectedType = funcParams[funcParamIdx].Type;
                if (expectedType is not UnknownType)
                    paramTypes[placeholderIdx] = expectedType;
            }
        }
    }

    /// <summary>
    /// Infer placeholder types from a binary operation body (e.g., __placeholder_0 * 2).
    /// Uses the non-placeholder operand's type to infer the placeholder type.
    /// </summary>
    private void InferParamTypesFromBinaryOp(BinaryOp binOp, Dictionary<string, int> unknownParams, List<SemanticType> paramTypes)
    {
        var leftIsPlaceholder = binOp.Left is Identifier leftId && unknownParams.ContainsKey(leftId.Name);
        var rightIsPlaceholder = binOp.Right is Identifier rightId && unknownParams.ContainsKey(rightId.Name);

        if (leftIsPlaceholder && !rightIsPlaceholder)
        {
            // (_ op expr) — infer from expr's type
            var rightType = TryResolveExpressionType(binOp.Right);
            if (rightType != null && rightType is not UnknownType)
            {
                var id = (Identifier)binOp.Left;
                paramTypes[unknownParams[id.Name]] = rightType;
            }
        }
        else if (!leftIsPlaceholder && rightIsPlaceholder)
        {
            // (expr op _) — infer from expr's type
            var leftType = TryResolveExpressionType(binOp.Left);
            if (leftType != null && leftType is not UnknownType)
            {
                var id = (Identifier)binOp.Right;
                paramTypes[unknownParams[id.Name]] = leftType;
            }
        }
        else if (leftIsPlaceholder && rightIsPlaceholder)
        {
            // (_ op _) — both placeholders, can't infer without more context
            // Leave as Unknown; will need explicit type annotation
        }
    }

    /// <summary>
    /// Infer placeholder type from a unary operation body (e.g., -__placeholder_0).
    /// </summary>
    private void InferParamTypesFromUnaryOp(UnaryOp unaryOp, Dictionary<string, int> unknownParams, List<SemanticType> paramTypes)
    {
        if (unaryOp.Operand is Identifier id && unknownParams.TryGetValue(id.Name, out int placeholderIdx))
        {
            // For numeric unary operators (-, +, ~), default to int
            // For 'not', default to bool
            SemanticType inferredType = unaryOp.Operator switch
            {
                UnaryOperator.Not => BuiltinType.Bool,
                _ => BuiltinType.Int // -, +, ~ default to int
            };
            paramTypes[placeholderIdx] = inferredType;
        }
    }

    /// <summary>
    /// Infer placeholder types from a comparison chain body (e.g., __placeholder_0 > 0).
    /// </summary>
    private void InferParamTypesFromComparison(ComparisonChain chain, Dictionary<string, int> unknownParams, List<SemanticType> paramTypes)
    {
        // For each operand that is a placeholder, try to infer from an adjacent non-placeholder operand
        for (int i = 0; i < chain.Operands.Length; i++)
        {
            if (chain.Operands[i] is Identifier id && unknownParams.TryGetValue(id.Name, out int placeholderIdx))
            {
                // Check adjacent operands for type info
                SemanticType? inferredType = null;
                if (i > 0)
                    inferredType = TryResolveExpressionType(chain.Operands[i - 1]);
                if ((inferredType == null || inferredType is UnknownType) && i + 1 < chain.Operands.Length)
                    inferredType = TryResolveExpressionType(chain.Operands[i + 1]);

                if (inferredType != null && inferredType is not UnknownType)
                    paramTypes[placeholderIdx] = inferredType;
            }
        }
    }

    /// <summary>
    /// Try to resolve the type of an expression without entering a full check pass.
    /// Used for pre-inference of lambda parameter types from body expressions.
    /// </summary>
    private SemanticType? TryResolveExpressionType(Expression expr)
    {
        return expr switch
        {
            IntegerLiteral => BuiltinType.Int,
            FloatLiteral => BuiltinType.Double,
            StringLiteral => BuiltinType.Str,
            BooleanLiteral => BuiltinType.Bool,
            Identifier id => (_symbolTable.Lookup(id.Name) as VariableSymbol)?.Type,
            _ => null
        };
    }

    /// <summary>
    /// Try to resolve a member symbol on a type.
    /// Used for pre-inference of method call parameter types.
    /// </summary>
    private Symbol? TryResolveMember(SemanticType objType, string memberName)
    {
        TypeSymbol? typeSymbol = objType switch
        {
            UserDefinedType udt => udt.Symbol,
            _ => null
        };

        if (typeSymbol == null)
            return null;

        return typeSymbol.Methods.FirstOrDefault(m => m.Name == memberName);
    }

}
