using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Binary/unary operators, comparison chains,
/// conditional expressions, type casts/coercions/checks, pipe forward, string repetition
/// </summary>
internal partial class RoslynEmitter
{
    private ExpressionSyntax GenerateBinaryOp(BinaryOp binOp)
    {
        var left = GenerateExpression(binOp.Left);
        var right = GenerateExpression(binOp.Right);

        // Special cases that need method calls or casts
        switch (binOp.Operator)
        {
            case BinaryOperator.Power:
                // x ** y → System.Math.Pow(x, y)
                // Note: We use fully qualified System.Math to avoid conflicts with Sharpy.Math namespace
                var powCall = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("System"),
                            IdentifierName("Math")),
                        IdentifierName("Pow")))
                    .AddArgumentListArguments(
                        Argument(left),
                        Argument(right));
                // If both operands are integers, cast result back to integer type
                // Math.Pow returns double, but int ** int should stay integer
                if (!IsFloatExpression(binOp.Left) && !IsFloatExpression(binOp.Right))
                {
                    // Check semantic type to determine the right integer cast type
                    var resultType = GetExpressionSemanticType(binOp);
                    var castKind = resultType == SemanticType.Long
                        ? SyntaxKind.LongKeyword
                        : SyntaxKind.IntKeyword;
                    return CastExpression(
                        PredefinedType(Token(castKind)),
                        ParenthesizedExpression(powCall));
                }
                return powCall;

            case BinaryOperator.Divide:
                // User-defined types with __div__: emit plain left / right (C# operator overload)
                var leftDivType = _context.SemanticInfo?.GetExpressionType(binOp.Left);
                var rightDivType = _context.SemanticInfo?.GetExpressionType(binOp.Right);
                if (leftDivType is UserDefinedType || rightDivType is UserDefinedType)
                    return BinaryExpression(SyntaxKind.DivideExpression, left, right);

                // x / y → true division with Python semantics (always returns float64)
                // Cast at least one operand to double to ensure float result
                // If either operand is already float, the division will naturally produce float
                // Decimal division: no cast needed, C# decimal / decimal works natively
                if (IsDecimalExpression(binOp.Left) || IsDecimalExpression(binOp.Right))
                    return BinaryExpression(SyntaxKind.DivideExpression, left, right);
                var leftIsFloat = IsFloatExpression(binOp.Left);
                var rightIsFloat = IsFloatExpression(binOp.Right);
                if (!leftIsFloat && !rightIsFloat)
                {
                    // Both operands are integers: cast left to double
                    return BinaryExpression(SyntaxKind.DivideExpression,
                        CastExpression(PredefinedType(Token(SyntaxKind.DoubleKeyword)), ParenthesizedExpression(left)),
                        right);
                }
                // At least one operand is float, so result will be float naturally
                return BinaryExpression(SyntaxKind.DivideExpression, left, right);

            case BinaryOperator.FloorDivide:
                // x // y → floor division with Python semantics (toward negative infinity)
                // Integer operands: (long)Math.Floor((double)x / y) → result is int64
                // Float operands: Math.Floor(x / y) → result is float type
                var hasFloatOperand = IsFloatExpression(binOp.Left) || IsFloatExpression(binOp.Right);
                return GenerateFloorDivision(left, right, hasFloatOperand);

            case BinaryOperator.In:
                // x in y → y.Contains(x)
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        right,
                        IdentifierName("Contains")))
                    .AddArgumentListArguments(Argument(left));

            case BinaryOperator.NotIn:
                // x not in y → !y.Contains(x)
                return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            right,
                            IdentifierName("Contains")))
                        .AddArgumentListArguments(Argument(left)));

            case BinaryOperator.Is:
                // x is y → object.ReferenceEquals(x, y)
                // Special optimization for None: x is None
                if (binOp.Right is NoneLiteral)
                {
                    // For Optional<T> (struct): emit x.IsNone (property access)
                    if (GetExpressionSemanticType(binOp.Left) is OptionalType)
                    {
                        return MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            left,
                            IdentifierName("IsNone"));
                    }
                    // For nullable/reference types: x == null
                    return BinaryExpression(SyntaxKind.EqualsExpression,
                        left,
                        LiteralExpression(SyntaxKind.NullLiteralExpression));
                }
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                        IdentifierName("ReferenceEquals")))
                    .AddArgumentListArguments(
                        Argument(left),
                        Argument(right));

            case BinaryOperator.IsNot:
                // x is not y → !object.ReferenceEquals(x, y)
                // Special optimization for None: x is not None
                if (binOp.Right is NoneLiteral)
                {
                    // For Optional<T> (struct): emit x.IsSome (property access)
                    if (GetExpressionSemanticType(binOp.Left) is OptionalType)
                    {
                        return MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            left,
                            IdentifierName("IsSome"));
                    }
                    // For nullable/reference types: x != null
                    return BinaryExpression(SyntaxKind.NotEqualsExpression,
                        left,
                        LiteralExpression(SyntaxKind.NullLiteralExpression));
                }
                return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                            IdentifierName("ReferenceEquals")))
                        .AddArgumentListArguments(
                            Argument(left),
                            Argument(right)));

            case BinaryOperator.NullCoalesce:
                // x ?? y — for Optional<T>, lower to UnwrapOr or ternary
                if (GetExpressionSemanticType(binOp.Left) is OptionalType)
                {
                    if (GetExpressionSemanticType(binOp.Right) is OptionalType)
                    {
                        // Both Optional: safeLeft.IsSome ? safeLeft : right
                        // Ensure left is only evaluated once for complex expressions
                        var (safeLeft, captureLeft) = EnsureSingleEvaluation(left, binOp.Left);
                        ExpressionSyntax coalesceCondition = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            ParenthesizedExpression(safeLeft), IdentifierName("IsSome"));
                        if (captureLeft != null)
                            coalesceCondition = BinaryExpression(SyntaxKind.LogicalAndExpression, captureLeft, coalesceCondition);
                        return ConditionalExpression(coalesceCondition, safeLeft, right);
                    }
                    // Left Optional, right raw T: left.UnwrapOr(right)
                    // UnwrapOr only evaluates left once (method call on the struct)
                    return InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            ParenthesizedExpression(left), IdentifierName("UnwrapOr")))
                        .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(right))));
                }
                // For nullable/reference types: C# ?? operator
                return BinaryExpression(SyntaxKind.CoalesceExpression, left, right);

            case BinaryOperator.Multiply:
                {
                    // String repetition: str * int or int * str
                    // String extension provides operator* via StringExtensions.
                    // Not string repetition — fall through to standard multiply
                    break;
                }

            case BinaryOperator.PipeForward:
                // x |> f → f(x)
                // x |> f(y) → f(x, y) (prepend to argument list)
                return GeneratePipeForward(binOp.Left, binOp.Right);
        }

        // Standard binary operators
        var kind = binOp.Operator switch
        {
            // Arithmetic (Divide is handled specially above for Python semantics)
            BinaryOperator.Add => SyntaxKind.AddExpression,
            BinaryOperator.Subtract => SyntaxKind.SubtractExpression,
            BinaryOperator.Multiply => SyntaxKind.MultiplyExpression,
            BinaryOperator.Modulo => SyntaxKind.ModuloExpression,

            // Comparison
            BinaryOperator.Equal => SyntaxKind.EqualsExpression,
            BinaryOperator.NotEqual => SyntaxKind.NotEqualsExpression,
            BinaryOperator.LessThan => SyntaxKind.LessThanExpression,
            BinaryOperator.LessThanOrEqual => SyntaxKind.LessThanOrEqualExpression,
            BinaryOperator.GreaterThan => SyntaxKind.GreaterThanExpression,
            BinaryOperator.GreaterThanOrEqual => SyntaxKind.GreaterThanOrEqualExpression,

            // Logical (with short-circuit)
            BinaryOperator.And => SyntaxKind.LogicalAndExpression,
            BinaryOperator.Or => SyntaxKind.LogicalOrExpression,

            // Bitwise
            BinaryOperator.BitwiseAnd => SyntaxKind.BitwiseAndExpression,
            BinaryOperator.BitwiseOr => SyntaxKind.BitwiseOrExpression,
            BinaryOperator.BitwiseXor => SyntaxKind.ExclusiveOrExpression,
            BinaryOperator.LeftShift => SyntaxKind.LeftShiftExpression,
            BinaryOperator.RightShift => SyntaxKind.RightShiftExpression,

            // NullCoalesce is handled in the special-cases switch above

            _ => SyntaxKind.None
        };

        if (kind == SyntaxKind.None)
        {
            return EmitNotImplementedExpression(
                $"Unsupported operator in code generation: binary operator '{binOp.Operator}'",
                DiagnosticCodes.CodeGen.UnsupportedOperator, binOp.LineStart, binOp.ColumnStart);
        }

        // For == and != on generic type parameters, use EqualityComparer<T>.Default.Equals()
        // because C# does not allow == on unconstrained generic types
        if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
        {
            var leftType = GetExpressionSemanticType(binOp.Left);
            var rightType = GetExpressionSemanticType(binOp.Right);
            if (leftType is Semantic.TypeParameterType || rightType is Semantic.TypeParameterType)
            {
                // EqualityComparer<T>.Default.Equals(left, right)
                var typeParamType = (leftType as Semantic.TypeParameterType ?? rightType as Semantic.TypeParameterType)!;
                var equalsCall = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            GenericName("EqualityComparer")
                                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(
                                    (TypeSyntax)IdentifierName(typeParamType.Name)))),
                            IdentifierName("Default")),
                        IdentifierName("Equals")))
                    .AddArgumentListArguments(Argument(left), Argument(right));

                return kind == SyntaxKind.NotEqualsExpression
                    ? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(equalsCall))
                    : (ExpressionSyntax)equalsCall;
            }
        }

        // For <, >, <=, >= on generic type parameters with IComparable constraint,
        // emit x.CompareTo(y) <op> 0 because C# does not allow relational operators
        // on unconstrained generic types
        if (kind is SyntaxKind.LessThanExpression or SyntaxKind.GreaterThanExpression
            or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanOrEqualExpression)
        {
            var leftType = GetExpressionSemanticType(binOp.Left);
            var rightType = GetExpressionSemanticType(binOp.Right);
            if ((leftType is Semantic.TypeParameterType leftTp && HasComparableConstraint(leftTp))
                || (rightType is Semantic.TypeParameterType rightTp && HasComparableConstraint(rightTp)))
            {
                // left.CompareTo(right) <op> 0
                var compareToCall = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        left,
                        IdentifierName("CompareTo")))
                    .AddArgumentListArguments(Argument(right));

                return BinaryExpression(kind,
                    compareToCall,
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));
            }
        }

        return BinaryExpression(kind, left, right);
    }

    /// <summary>
    /// Generate code for pipe forward operator (|>).
    /// x |> f → f(x)
    /// x |> f(y) → f(x, y) (prepend to argument list)
    /// x |> f |> g → g(f(x)) (chains via left-associativity in parser)
    /// </summary>
    private ExpressionSyntax GeneratePipeForward(Expression leftExpr, Expression rightExpr)
    {
        var left = GenerateExpression(leftExpr);

        // Case 1: Right side is already a function call - prepend left to its arguments
        // x |> f(y, z) → f(x, y, z)
        if (rightExpr is FunctionCall funcCall)
        {
            // Generate the function name with proper name mangling (same as GenerateCall)
            var func = GeneratePipeCallTarget(funcCall.Function);

            // Resolve the callee's FunctionSymbol for parameter reordering
            var pipeFuncSymbol = _context.SemanticInfo?.GetCallTarget(funcCall);
            if (pipeFuncSymbol == null && funcCall.Function is Identifier pipeFuncId)
                pipeFuncSymbol = _context.LookupSymbol(pipeFuncId.Name) as FunctionSymbol;

            // Delegate to shared call-site reordering with the piped value prepended
            var allArgs = GenerateReorderedCallArguments(funcCall, pipeFuncSymbol, Argument(left));

            return InvocationExpression(func)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Case 2: Right side is a lambda from partial application lowering
        // x |> multiply(_, 3) → parser lowered to: x |> lambda __p0: multiply(__p0, 3)
        // Unwrap: substitute the pipe value for the placeholder and generate a direct call
        if (rightExpr is LambdaExpression partialLambda
            && partialLambda.Parameters.Length == 1
            && partialLambda.Body is FunctionCall partialCall)
        {
            var placeholderName = partialLambda.Parameters[0].Name;
            var func = GeneratePipeCallTarget(partialCall.Function);

            // Build arguments, replacing the placeholder identifier with the piped value
            var substitutedArgs = new List<ArgumentSyntax>();
            foreach (var arg in partialCall.Arguments)
            {
                if (arg is Identifier id && id.Name == placeholderName)
                    substitutedArgs.Add(Argument(left));
                else
                    substitutedArgs.Add(Argument(GenerateExpression(arg)));
            }
            foreach (var kw in partialCall.KeywordArguments)
            {
                substitutedArgs.Add(Argument(GenerateExpression(kw.Value))
                    .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kw.Name)))));
            }

            return InvocationExpression(func)
                .WithArgumentList(ArgumentList(SeparatedList(substitutedArgs)));
        }

        // Case 3: Right side is an identifier or member access - call it with left as the only argument
        // x |> f → f(x)
        var right = GeneratePipeCallTarget(rightExpr);
        return InvocationExpression(right)
            .AddArgumentListArguments(Argument(left));
    }

    /// <summary>
    /// Generate the call target expression for a pipe operator.
    /// Handles proper name mangling for function names (PascalCase) and builtin functions.
    /// </summary>
    private ExpressionSyntax GeneratePipeCallTarget(Expression expr)
    {
        if (expr is Identifier funcName)
        {
            // User-defined functions shadow builtins (Python scoping rules)
            var isBuiltin = _context.IsBuiltinFunction(funcName.Name);
            var symbol = _context.LookupSymbol(funcName.Name);
            if (isBuiltin && symbol is FunctionSymbol { CodeGenInfo: not null })
                isBuiltin = false;

            if (isBuiltin)
            {
                return MakeGlobalQualifiedName("Sharpy", "Builtins", NameMangler.ToPascalCase(funcName.Name));
            }
            return ParseName(NameMangler.ToPascalCase(funcName.Name));
        }

        // For member access and other expressions, use standard expression generation
        return GenerateExpression(expr);
    }

    private ExpressionSyntax GenerateUnaryOp(UnaryOp unaryOp)
    {
        var operand = GenerateExpression(unaryOp.Operand);

        var kind = unaryOp.Operator switch
        {
            UnaryOperator.Plus => SyntaxKind.UnaryPlusExpression,
            UnaryOperator.Minus => SyntaxKind.UnaryMinusExpression,
            UnaryOperator.Not => SyntaxKind.LogicalNotExpression,
            UnaryOperator.BitwiseNot => SyntaxKind.BitwiseNotExpression,
            _ => SyntaxKind.None
        };

        if (kind == SyntaxKind.None)
        {
            return EmitNotImplementedExpression(
                $"Unsupported operator in code generation: unary operator '{unaryOp.Operator}'",
                DiagnosticCodes.CodeGen.UnsupportedOperator, unaryOp.LineStart, unaryOp.ColumnStart);
        }

        // Wrap binary expressions in parentheses when negated to avoid precedence issues.
        // e.g., `not isinstance(x, T)` → `!(x is T)` not `!x is T`
        if (kind == SyntaxKind.LogicalNotExpression && operand is BinaryExpressionSyntax)
        {
            operand = ParenthesizedExpression(operand);
        }

        return PrefixUnaryExpression(kind, operand);
    }

    private ExpressionSyntax GenerateComparisonChain(ComparisonChain chain)
    {
        // a < b < c → a < b && b < c
        // Python guarantees intermediate expressions are evaluated exactly once.
        // For non-trivial intermediate expressions (function calls, member access, etc.),
        // we use the C# "is var" pattern to capture the value inline:
        //   a < (f() is var __cmp_0 ? __cmp_0 : __cmp_0) && __cmp_0 < c

        if (chain.Operands.Length < 2 || chain.Operators.Length != chain.Operands.Length - 1)
        {
            throw new InvalidOperationException("Invalid comparison chain");
        }

        // For intermediate operands (indices 1..n-2), decide if they need a temp variable.
        // First and last operands are only used once and don't need temps.
        var tempNames = new string?[chain.Operands.Length];
        for (int i = 1; i < chain.Operands.Length - 1; i++)
        {
            if (!IsTrivialExpression(chain.Operands[i]))
            {
                tempNames[i] = GenerateTempVarName("cmp");
            }
        }

        ExpressionSyntax? result = null;

        for (int i = 0; i < chain.Operators.Length; i++)
        {
            ExpressionSyntax left;
            ExpressionSyntax right;

            // Left operand: use temp name from previous iteration if available
            if (i > 0 && tempNames[i] != null)
            {
                left = IdentifierName(tempNames[i]!);
            }
            else
            {
                left = GenerateExpression(chain.Operands[i]);
            }

            // Right operand: capture into temp if this is an intermediate with side effects
            var rightExpr = GenerateExpression(chain.Operands[i + 1]);
            if (tempNames[i + 1] != null)
            {
                // Wrap in: (expr is var __cmp_N ? __cmp_N : __cmp_N)
                // This evaluates expr once, binds to __cmp_N, and returns the value
                right = ParenthesizedExpression(
                    ConditionalExpression(
                        IsPatternExpression(
                            rightExpr,
                            VarPattern(SingleVariableDesignation(Identifier(tempNames[i + 1]!)))),
                        IdentifierName(tempNames[i + 1]!),
                        IdentifierName(tempNames[i + 1]!)));
            }
            else
            {
                right = rightExpr;
            }

            var op = chain.Operators[i];
            var kind = MapComparisonOperator(op);

            if (kind == SyntaxKind.None)
            {
                return EmitNotImplementedExpression(
                    $"Unsupported operator in code generation: comparison operator '{op}' in chains",
                    DiagnosticCodes.CodeGen.UnsupportedOperator, chain.LineStart, chain.ColumnStart);
            }

            var comparison = BinaryExpression(kind, left, right);

            result = result == null
                ? comparison
                : BinaryExpression(SyntaxKind.LogicalAndExpression, result, comparison);
        }

        return result ?? throw new InvalidOperationException("Empty comparison chain");
    }

    /// <summary>
    /// Maps a comparison operator to the corresponding C# syntax kind.
    /// </summary>
    private SyntaxKind MapComparisonOperator(ComparisonOperator op)
    {
        return op switch
        {
            ComparisonOperator.Equal => SyntaxKind.EqualsExpression,
            ComparisonOperator.NotEqual => SyntaxKind.NotEqualsExpression,
            ComparisonOperator.LessThan => SyntaxKind.LessThanExpression,
            ComparisonOperator.LessThanOrEqual => SyntaxKind.LessThanOrEqualExpression,
            ComparisonOperator.GreaterThan => SyntaxKind.GreaterThanExpression,
            ComparisonOperator.GreaterThanOrEqual => SyntaxKind.GreaterThanOrEqualExpression,
            _ => SyntaxKind.None
        };
    }

    /// <summary>
    /// Returns true if the expression is trivial (identifier, literal) and
    /// safe to evaluate multiple times without side effects.
    /// </summary>
    private static bool IsTrivialExpression(Expression expr)
    {
        return expr is Parser.Ast.Identifier
            or IntegerLiteral
            or FloatLiteral
            or StringLiteral
            or BooleanLiteral
            or NoneLiteral;
    }

    private ExpressionSyntax GenerateConditionalExpression(ConditionalExpression cond)
    {
        // value if test else other → test ? value : other
        var test = GenerateExpression(cond.Test);
        var whenTrue = GenerateExpression(cond.ThenValue);
        var whenFalse = GenerateExpression(cond.ElseValue);

        return ConditionalExpression(test, whenTrue, whenFalse);
    }

    private ExpressionSyntax GenerateTypeCoercion(TypeCoercion coercion)
    {
        // The `to` operator:
        // - value to T → (T)value (throws InvalidCastException on failure)
        // - value to T? → value is T _temp ? Optional<T>.Some(_temp) : default

        var value = GenerateExpression(coercion.Value);

        if (coercion.TargetType.IsOptional)
        {
            // Safe form: value to T?
            // Generate: value is T _temp ? Optional<T>.Some(_temp) : default
            // Works for both value types and reference types.
            // default produces Optional<T>.None (struct with _hasValue = false).
            var baseType = new TypeAnnotation
            {
                Name = coercion.TargetType.Name,
                TypeArguments = coercion.TargetType.TypeArguments,
                IsOptional = false
            };
            var baseTypeSyntax = _typeMapper.MapType(baseType);

            var tempName = $"__coerce_temp_{_tempVarCounter++}";

            var optionalType = GenericName("Optional")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(baseTypeSyntax)));
            var someExpr = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    optionalType,
                    IdentifierName("Some")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName(tempName)))));

            return ConditionalExpression(
                IsPatternExpression(
                    value,
                    DeclarationPattern(
                        baseTypeSyntax,
                        SingleVariableDesignation(Identifier(tempName)))),
                someExpr,
                LiteralExpression(SyntaxKind.DefaultLiteralExpression));
        }
        else
        {
            // Throwing form: value to T → (T)value
            var targetType = _typeMapper.MapType(coercion.TargetType);
            return CastExpression(targetType, value);
        }
    }

    private ExpressionSyntax GenerateTypeCheck(TypeCheck check)
    {
        // value is Type → value is Type
        var value = GenerateExpression(check.Value);
        var checkType = _typeMapper.MapType(check.CheckType);

        return BinaryExpression(
            SyntaxKind.IsExpression,
            value,
            checkType);
    }

    /// <summary>
    /// Returns true if the type parameter has an IComparable constraint.
    /// Matches IComparable, IComparable[T], System.IComparable, etc.
    /// </summary>
    private static bool HasComparableConstraint(Semantic.TypeParameterType typeParam)
    {
        foreach (var constraint in typeParam.Constraints)
        {
            if (constraint is TypeConstraint tc
                && (tc.Type.Name == "IComparable" || tc.Type.Name == "System.IComparable"
                    || tc.Type.Name.StartsWith("IComparable<", System.StringComparison.Ordinal)
                    || tc.Type.Name.StartsWith("System.IComparable<", System.StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }
}
