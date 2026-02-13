using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Expression generation (literals, operators, calls, comprehensions)
/// </summary>
internal partial class RoslynEmitter
{
    private ExpressionSyntax GenerateExpression(Sharpy.Compiler.Parser.Ast.Expression expr)
    {
        return expr switch
        {
            // Literals
            IntegerLiteral intLit => GenerateIntegerLiteral(intLit),
            FloatLiteral floatLit => GenerateFloatLiteral(floatLit),
            StringLiteral strLit => GenerateStringLiteral(strLit),
            BooleanLiteral boolLit => LiteralExpression(boolLit.Value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            NoneLiteral => LiteralExpression(SyntaxKind.NullLiteralExpression),
            EllipsisLiteral => GenerateEllipsisLiteral(),

            // Collections
            ListLiteral listLit => GenerateListLiteral(listLit),
            DictLiteral dictLit => GenerateDictLiteral(dictLit),
            SetLiteral setLit => GenerateSetLiteral(setLit),
            TupleLiteral tupleLit => GenerateTupleLiteral(tupleLit),

            // Comprehensions
            ListComprehension listComp => GenerateListComprehension(listComp),
            SetComprehension setComp => GenerateSetComprehension(setComp),
            DictComprehension dictComp => GenerateDictComprehension(dictComp),

            // Primary expressions
            // Handle 'self' -> 'this' conversion for instance methods
            // When _selfReplacementIdentifier is set (inlined operator body), map to that instead
            Identifier name when string.Equals(name.Name, "self", StringComparison.OrdinalIgnoreCase) =>
                _selfReplacementIdentifier != null
                    ? IdentifierName(_selfReplacementIdentifier)
                    : ThisExpression(),
            Identifier name => GenerateIdentifierExpression(name),
            SuperExpression => BaseExpression(),  // super() -> base
            MemberAccess memberAccess => GenerateMemberAccess(memberAccess),
            IndexAccess indexAccess => GenerateIndexAccess(indexAccess),
            SliceAccess sliceAccess => GenerateSliceAccess(sliceAccess),
            // Handle None() -> Optional<T>.None
            FunctionCall call when call.Function is NoneLiteral
                && call.Arguments.Length == 0
                && GetExpressionSemanticType(call) is OptionalType optNone
                => GenerateOptionalNone(optNone),
            // Handle Some/Ok/Err -> Optional/Result factory calls (tagged union constructors)
            FunctionCall call when IsTaggedUnionConstructorCall(call) => GenerateTaggedUnionConstructor(call),
            FunctionCall call => GenerateCall(call),

            // Operators
            UnaryOp unaryOp => GenerateUnaryOp(unaryOp),
            BinaryOp binOp => GenerateBinaryOp(binOp),
            ComparisonChain chain => GenerateComparisonChain(chain),

            // Advanced expressions
            ConditionalExpression cond => GenerateConditionalExpression(cond),
            LambdaExpression lambda => GenerateLambdaExpression(lambda),
            TypeCast cast => GenerateTypeCast(cast),
            TypeCoercion coercion => GenerateTypeCoercion(coercion),
            TypeCheck check => GenerateTypeCheck(check),
            Parenthesized paren => ParenthesizedExpression(GenerateExpression(paren.Expression)),

            // F-strings
            FStringLiteral fstring => GenerateFString(fstring),

            // Try/Maybe expressions
            TryExpression tryExpr => GenerateTryExpression(tryExpr),
            MaybeExpression maybeExpr => GenerateMaybeExpression(maybeExpr),

            _ => EmitNotImplementedExpression(
                $"Unsupported expression type in code generation: '{expr.GetType().Name}'",
                DiagnosticCodes.CodeGen.UnsupportedExpressionType, expr.LineStart, expr.ColumnStart)
        };
    }

    /// <summary>
    /// Generates an identifier expression, with Optional narrowing support.
    /// When a variable has been narrowed from Optional&lt;T&gt; to T (via an is-not-None check),
    /// emits identifier.Unwrap() to extract the underlying value.
    /// </summary>
    private ExpressionSyntax GenerateIdentifierExpression(Identifier name)
    {
        var mangledName = GetMangledVariableName(name.Name, isNewDeclaration: false);
        ExpressionSyntax expr = IdentifierName(mangledName);

        // If this variable has been narrowed from Optional<T>/Nullable<T> to T,
        // emit .Unwrap() for Optional or .Value for value-type Nullable
        if (IsNarrowed(name.Name))
        {
            if (IsNullableNarrowed(name.Name))
            {
                // Value-type nullable (int?, bool?, etc.) → .Value
                expr = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expr,
                    IdentifierName("Value"));
            }
            else
            {
                // Optional<T> → .Unwrap()
                expr = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        expr,
                        IdentifierName("Unwrap")))
                    .WithArgumentList(ArgumentList());
            }
        }

        return expr;
    }

    private ExpressionSyntax GenerateIntegerLiteral(IntegerLiteral literal)
    {
        return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(int.Parse(literal.Value)));
    }

    private ExpressionSyntax GenerateFloatLiteral(FloatLiteral literal)
    {
        return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(double.Parse(literal.Value)));
    }

    private ExpressionSyntax GenerateStringLiteral(StringLiteral literal)
    {
        return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(literal.Value));
    }

    private ExpressionSyntax GenerateCall(FunctionCall call)
    {
        // Handle generic type/function instantiation: Box[int](42) or identity[int](42)
        // This is parsed as FunctionCall(Function: IndexAccess(Object: Box/identity, Index: int), Arguments: [42])
        if (call.Function is IndexAccess indexAccess &&
            indexAccess.Object is Identifier genericName)
        {
            var symbol = _context.LookupSymbol(genericName.Name);

            // Map the type argument(s)
            var typeArgsSyntax = _typeMapper.MapTypeArgumentsFromExpression(indexAccess.Index);

            if (symbol is TypeSymbol genericTypeSymbol && genericTypeSymbol.IsGeneric)
            {
                // Generate: new GenericType<TypeArgs>(args)
                var genericTypeSyntax = GenericName(NameMangler.ToPascalCase(genericName.Name))
                    .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax)));

                // Generate arguments
                var positionalArgs = call.Arguments.Select(arg => Argument(GenerateExpression(arg)));
                var keywordArgs = call.KeywordArguments.Select(kwarg =>
                    Argument(GenerateExpression(kwarg.Value))
                        .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kwarg.Name)))));
                var allArgs = positionalArgs.Concat(keywordArgs).ToArray();

                return ObjectCreationExpression(genericTypeSyntax)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            if (symbol is FunctionSymbol genericFuncSymbol && genericFuncSymbol.IsGeneric)
            {
                // Generate: GenericFunction<TypeArgs>(args)
                var genericFuncSyntax = GenericName(NameMangler.ToPascalCase(genericName.Name))
                    .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax)));

                // Generate arguments
                var positionalArgs = call.Arguments.Select(arg => Argument(GenerateExpression(arg)));
                var keywordArgs = call.KeywordArguments.Select(kwarg =>
                    Argument(GenerateExpression(kwarg.Value))
                        .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kwarg.Name)))));
                var allArgs = positionalArgs.Concat(keywordArgs).ToArray();

                return InvocationExpression(genericFuncSyntax)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }
        }

        if (call.Function is Identifier funcName)
        {
            // Check if this is a builtin function call (e.g., int(), str(), print(), len(), etc.)
            // Builtin functions are always invocation expressions, never constructor calls.
            var isBuiltinFunc = _context.IsBuiltinFunction(funcName.Name);

            // Check if this is a type instantiation (calling a class or struct constructor)
            // We use the symbol table which is populated during semantic analysis.
            // This handles both local type definitions and imported types.
            // NOTE: Builtin functions are NOT type instantiations (e.g., int(x) is a conversion function)
            var symbol = _context.LookupSymbol(funcName.Name);
            var isTypeInstantiation = !isBuiltinFunc &&
                                      symbol is TypeSymbol typeSymbol &&
                                      (typeSymbol.TypeKind == Semantic.TypeKind.Class ||
                                       typeSymbol.TypeKind == Semantic.TypeKind.Struct);

            string name;
            if (isBuiltinFunc)
            {
                name = $"global::Sharpy.Builtins.{NameMangler.ToPascalCase(funcName.Name)}";
            }
            else if (isTypeInstantiation && symbol is TypeSymbol typeSymbolForName)
            {
                // For type instantiation, use fully qualified name if type is from another file
                name = GetFullyQualifiedTypeName(typeSymbolForName, funcName.Name);
            }
            else
            {
                name = NameMangler.ToPascalCase(funcName.Name);
            }

            // Generate positional arguments
            var positionalArgs = call.Arguments.Select(arg => Argument(GenerateExpression(arg)));

            // Generate keyword arguments with named syntax
            var keywordArgs = call.KeywordArguments.Select(kwarg =>
                Argument(GenerateExpression(kwarg.Value))
                    .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kwarg.Name)))));

            // Combine positional and keyword arguments
            var allArgs = positionalArgs.Concat(keywordArgs).ToArray();

            // For type instantiation (class or struct), generate 'new TypeName(args)' instead of 'TypeName(args)'
            if (isTypeInstantiation)
            {
                return ObjectCreationExpression(ParseName(name))
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            // Regular function call
            return InvocationExpression(ParseName(name))
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Handle method calls on objects: obj.method() or ClassName.static_method()
        if (call.Function is MemberAccess memberAccess)
        {
            var obj = GenerateExpression(memberAccess.Object);

            // Cross-dunder calls: transform operator dunders to C# operator expressions.
            // e.g., self.__lt__(other) → this < other, self.__neg__() → -this
            // This must happen BEFORE regular method name resolution so that operator dunders
            // emit operators instead of __PascalCase__ method calls.
            if (DunderMapping.IsDunderMethod(memberAccess.Member))
            {
                var binaryKind = DunderMapping.TryGetBinaryExpressionKind(memberAccess.Member);
                if (binaryKind != null && call.Arguments.Length == 1)
                {
                    var arg = GenerateExpression(call.Arguments[0]);
                    return BinaryExpression(binaryKind.Value, obj, arg);
                }

                var unaryKind = DunderMapping.TryGetUnaryExpressionKind(memberAccess.Member);
                if (unaryKind != null && call.Arguments.Length == 0)
                {
                    return PrefixUnaryExpression(unaryKind.Value, obj);
                }
            }

            // Apply name mangling to method name
            // First check for dunder methods, then Python list method mappings (append -> Add, etc.)
            var methodName = DunderMapping.ResolveCSharpName(memberAccess.Member)
                ?? NameMangler.GetListMethodMapping(memberAccess.Member)
                ?? NameMangler.ToPascalCase(memberAccess.Member);

            // Property-vs-method dispatch for Optional/Result:
            // is_some/is_none/is_ok/is_err are C# properties, not methods.
            // Emit property access instead of method invocation.
            if (call.Arguments.Length == 0 && call.KeywordArguments.Length == 0)
            {
                var objType = GetExpressionSemanticType(memberAccess.Object);
                if (objType is OptionalType && methodName is "IsSome" or "IsNone")
                {
                    return MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, obj, IdentifierName(methodName));
                }
                if (objType is ResultType && methodName is "IsOk" or "IsErr")
                {
                    return MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, obj, IdentifierName(methodName));
                }
            }

            // Generate positional arguments
            var positionalArgs = call.Arguments.Select(arg => Argument(GenerateExpression(arg)));

            // Generate keyword arguments with named syntax
            var keywordArgs = call.KeywordArguments.Select(kwarg =>
                Argument(GenerateExpression(kwarg.Value))
                    .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kwarg.Name)))));

            // Combine positional and keyword arguments
            var allArgs = positionalArgs.Concat(keywordArgs).ToArray();

            // Handle null conditional method calls: obj?.Method(args)
            if (memberAccess.IsNullConditional)
            {
                // For Optional<T>: lower to ternary since ?.  doesn't work on structs
                if (GetExpressionSemanticType(memberAccess.Object) is OptionalType)
                {
                    // Ensure obj is only evaluated once for complex expressions
                    var (safeObj, capture) = EnsureSingleEvaluation(obj, memberAccess.Object);
                    // safeObj.IsSome ? safeObj.Unwrap().Method(args) : Optional<T>.None
                    var methodCall = InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    safeObj, IdentifierName("Unwrap")))
                                .WithArgumentList(ArgumentList()),
                            IdentifierName(methodName)))
                        .WithArgumentList(ArgumentList(SeparatedList(allArgs)));

                    ExpressionSyntax cond = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        ParenthesizedExpression(safeObj), IdentifierName("IsSome"));
                    if (capture != null)
                        cond = BinaryExpression(SyntaxKind.LogicalAndExpression, capture, cond);

                    // Use Optional<T>.None for the false branch so C# resolves the ternary
                    // as Optional<T> (via implicit conversion on the true branch if needed)
                    var falseExpr = GetExpressionSemanticType(call) is OptionalType optCallType
                        ? (ExpressionSyntax)GenerateOptionalNone(optCallType)
                        : (ExpressionSyntax)LiteralExpression(SyntaxKind.DefaultLiteralExpression);

                    return ConditionalExpression(cond, methodCall, falseExpr);
                }

                // Generate: obj?.Method(args)
                // Uses ConditionalAccessExpression with MemberBindingExpression for the method
                // followed by InvocationExpression for the call
                var memberBinding = MemberBindingExpression(IdentifierName(methodName));
                var invocation = InvocationExpression(memberBinding)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));

                return ConditionalAccessExpression(obj, invocation);
            }

            // Generate: obj.Method(args)
            var methodAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                IdentifierName(methodName));

            return InvocationExpression(methodAccess)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        return EmitNotImplementedExpression(
            "Unsupported expression type in code generation: complex function expressions are not yet supported",
            DiagnosticCodes.CodeGen.UnsupportedExpressionType, call.LineStart, call.ColumnStart);
    }

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
                // x / y → true division with Python semantics (always returns float64)
                // Cast at least one operand to double to ensure float result
                // If either operand is already float, the division will naturally produce float
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
            var prependedArg = Argument(left);
            var existingArgs = funcCall.Arguments.Select(a => Argument(GenerateExpression(a)));
            var keywordArgs = funcCall.KeywordArguments.Select(k =>
                Argument(GenerateExpression(k.Value))
                    .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(k.Name)))));

            var allArgs = new[] { prependedArg }.Concat(existingArgs).Concat(keywordArgs);

            return InvocationExpression(func)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Case 2: Right side is an identifier or member access - call it with left as the only argument
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
            // Use the same name mangling logic as GenerateCall
            var name = _context.IsBuiltinFunction(funcName.Name)
                ? $"global::Sharpy.Builtins.{NameMangler.ToPascalCase(funcName.Name)}"
                : NameMangler.ToPascalCase(funcName.Name);
            return ParseName(name);
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

        return PrefixUnaryExpression(kind, operand);
    }

    private ExpressionSyntax GenerateEllipsisLiteral()
    {
        // Ellipsis (...) in concrete method bodies generates throw NotImplementedException()
        // Note: For abstract methods/interface methods, the ellipsis is ignored and
        // the method has no body (handled in GenerateClassMethod/GenerateInterfaceMethod)
        return ThrowExpression(
            ObjectCreationExpression(ParseTypeName("System.NotImplementedException"))
                .WithArgumentList(ArgumentList()));
    }

    private ExpressionSyntax GenerateListLiteral(ListLiteral list)
    {
        // new System.Collections.Generic.List<T> { elem1, elem2, elem3 }
        // v0.1.x uses .NET types directly per phases.md
        // Prefer target type annotation if available (e.g., list[int] = [...])
        TypeSyntax elementType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == "list" &&
            _targetTypeContext.TypeArguments.Length > 0)
        {
            // Use the declared element type from the target type annotation
            elementType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
        }
        else
        {
            // Fall back to inference from elements
            elementType = _typeMapper.InferElementType(list.Elements);
        }

        var elements = list.Elements.Select(GenerateExpression);

        var listType = GenericName("System.Collections.Generic.List")
            .AddTypeArgumentListArguments(elementType);

        return ObjectCreationExpression(listType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateDictLiteral(DictLiteral dict)
    {
        // new System.Collections.Generic.Dictionary<K,V> { { key1, value1 }, { key2, value2 } }
        // v0.1.x uses .NET types directly per phases.md
        // Prefer target type annotation if available (e.g., dict[str, int] = {...})
        TypeSyntax keyType, valueType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == "dict" &&
            _targetTypeContext.TypeArguments.Length >= 2)
        {
            keyType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
            valueType = _typeMapper.MapType(_targetTypeContext.TypeArguments[1]);
        }
        else
        {
            keyType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Key));
            valueType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Value));
        }

        var initializers = dict.Entries.Select(entry =>
            InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
                SeparatedList(new[]
                {
                    GenerateExpression(entry.Key),
                    GenerateExpression(entry.Value)
                })));

        var dictType = GenericName("System.Collections.Generic.Dictionary")
            .AddTypeArgumentListArguments(keyType, valueType);

        return ObjectCreationExpression(dictType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList<ExpressionSyntax>(initializers)));
    }

    private ExpressionSyntax GenerateSetLiteral(SetLiteral set)
    {
        // new System.Collections.Generic.HashSet<T> { elem1, elem2, elem3 }
        // v0.1.x uses .NET types directly per phases.md
        // Prefer target type annotation if available (e.g., set[int] = {...})
        TypeSyntax elementType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == "set" &&
            _targetTypeContext.TypeArguments.Length > 0)
        {
            elementType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
        }
        else
        {
            elementType = _typeMapper.InferElementType(set.Elements);
        }

        var elements = set.Elements.Select(GenerateExpression);

        var setType = GenericName("System.Collections.Generic.HashSet")
            .AddTypeArgumentListArguments(elementType);

        return ObjectCreationExpression(setType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateTupleLiteral(TupleLiteral tuple)
    {
        // (elem1, elem2, ...)
        var elements = tuple.Elements.Select(GenerateExpression);

        return TupleExpression(SeparatedList(
            elements.Select(e => Argument(e))));
    }

    // See: #100 (consider imperative code generation for complex comprehensions)

    private ExpressionSyntax GenerateListComprehension(ListComprehension listComp)
    {
        // Generate LINQ method chain: iterator.Where(...).Select(...).ToList()
        // Example: [x * 2 for x in items if x > 0]
        // becomes: items.Where(x => x > 0).Select(x => x * 2).ToList()

        var (chain, param, errorExpr) = GenerateComprehensionChain(
            listComp.Clauses, "List", listComp.LineStart, listComp.ColumnStart);

        if (errorExpr != null)
            return errorExpr;

        // Apply .Select(x => element_expression).ToList()
        var elementExpr = GenerateExpression(listComp.Element);
        var selectLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(elementExpr);

        chain = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                chain,
                IdentifierName("Select")))
            .AddArgumentListArguments(Argument(selectLambda));

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                chain,
                IdentifierName("ToList")))
            .WithArgumentList(ArgumentList());
    }

    private ExpressionSyntax GenerateSetComprehension(SetComprehension setComp)
    {
        // Generate LINQ method chain: iterator.Where(...).Select(...).ToHashSet()
        // Example: {x * 2 for x in items if x > 0}
        // becomes: items.Where(x => x > 0).Select(x => x * 2).ToHashSet()

        var (chain, param, errorExpr) = GenerateComprehensionChain(
            setComp.Clauses, "Set", setComp.LineStart, setComp.ColumnStart);

        if (errorExpr != null)
            return errorExpr;

        // Apply .Select(x => element_expression).ToHashSet()
        var elementExpr = GenerateExpression(setComp.Element);
        var selectLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(elementExpr);

        chain = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                chain,
                IdentifierName("Select")))
            .AddArgumentListArguments(Argument(selectLambda));

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                chain,
                IdentifierName("ToHashSet")))
            .WithArgumentList(ArgumentList());
    }

    private ExpressionSyntax GenerateDictComprehension(DictComprehension dictComp)
    {
        // Generate LINQ method chain: iterator.Where(...).ToDictionary(x => key, x => value)
        // Example: {k: v for k, v in pairs if v > 0}
        // For now, only support single variable (not tuple unpacking)
        // becomes: pairs.Where(p => p.v > 0).ToDictionary(p => p.k, p => p.v)

        var (chain, param, errorExpr) = GenerateComprehensionChain(
            dictComp.Clauses, "Dict", dictComp.LineStart, dictComp.ColumnStart);

        if (errorExpr != null)
            return errorExpr;

        // Generate key and value selector lambdas
        var keyExpr = GenerateExpression(dictComp.Key);
        var valueExpr = GenerateExpression(dictComp.Value);

        var keyLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(keyExpr);
        var valueLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(valueExpr);

        // Apply .ToDictionary(x => key, x => value)
        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                chain,
                IdentifierName("ToDictionary")))
            .AddArgumentListArguments(
                Argument(keyLambda),
                Argument(valueLambda));
    }

    /// <summary>
    /// Generates the common LINQ chain for comprehensions: validates the first for clause,
    /// extracts the loop variable, and applies all Where clauses. Returns the chain so far,
    /// the parameter syntax for lambdas, and optionally an error expression if validation failed.
    /// </summary>
    /// <param name="clauses">The comprehension clauses</param>
    /// <param name="comprehensionType">Type name for error messages (List, Set, Dict)</param>
    /// <param name="lineStart">Line number for error reporting</param>
    /// <param name="columnStart">Column number for error reporting</param>
    /// <returns>Tuple of (chain expression, parameter, error expression or null)</returns>
    private (ExpressionSyntax Chain, ParameterSyntax Param, ExpressionSyntax? Error) GenerateComprehensionChain(
        ImmutableArray<ComprehensionClause> clauses,
        string comprehensionType,
        int lineStart,
        int columnStart)
    {
        if (clauses.IsEmpty || clauses[0] is not ForClause firstFor)
        {
            throw new InvalidOperationException($"{comprehensionType} comprehension must start with a for clause");
        }

        // Get the loop variable name (single identifier only)
        if (firstFor.Target is not Identifier loopVar)
        {
            var error = EmitNotImplementedExpression(
                "Tuple unpacking in comprehensions is not yet supported. Use a for loop instead.",
                DiagnosticCodes.CodeGen.TupleUnpackingComprehension, lineStart, columnStart);
            return (null!, null!, error);
        }

        var varName = NameMangler.ToCamelCase(loopVar.Name);
        var param = Parameter(Identifier(varName));

        // Start with the iterator expression
        ExpressionSyntax chain = GenerateExpression(firstFor.Iterator);

        // Apply each if clause as .Where(x => condition)
        foreach (var clause in clauses.Skip(1))
        {
            switch (clause)
            {
                case IfClause ifClause:
                    var condition = GenerateExpression(ifClause.Condition);
                    var lambda = SimpleLambdaExpression(param)
                        .WithExpressionBody(condition);

                    chain = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            chain,
                            IdentifierName("Where")))
                        .AddArgumentListArguments(Argument(lambda));
                    break;

                case ForClause:
                    var error = EmitNotImplementedExpression(
                        "Nested comprehensions (multiple 'for' clauses) are not yet supported. Use a for loop instead.",
                        DiagnosticCodes.CodeGen.NestedComprehension, lineStart, columnStart);
                    return (null!, null!, error);
            }
        }

        return (chain, param, null);
    }

    private ExpressionSyntax GenerateMemberAccess(MemberAccess memberAccess)
    {
        // Check for nested module access (e.g., lib.math.add -> Lib.Math.Add)
        // This must be checked before enum handling to ensure module paths take precedence
        if (TryExtractModulePath(memberAccess, out var modulePath))
        {
            return BuildModuleAccessExpression(modulePath);
        }

        // Check for enum member access (e.g., Color.RED -> Color.Red)
        if (memberAccess.Object is Identifier enumTypeIdentifier)
        {
            var symbol = _context.LookupSymbol(enumTypeIdentifier.Name);

            // If this is an enum type, handle member access specially
            if (symbol is TypeSymbol enumSymbol && enumSymbol.TypeKind == Semantic.TypeKind.Enum)
            {
                // Enum member access: Color.RED -> Color.Red
                // Types are nested in the module class, accessible via unqualified names
                var enumTypeName = NameMangler.ToPascalCase(enumTypeIdentifier.Name);

                // Use the enum type directly (nested types are accessible within the module class)
                var enumType = IdentifierName(enumTypeName);

                // Check if this is a string enum (string enums are generated as classes, not C# enums)
                if (IsStringEnumSymbol(enumSymbol))
                {
                    // String enums use CONSTANT_CASE field names (same as NameContext.Constant)
                    var fieldName = NameMangler.Transform(memberAccess.Member, NameContext.Constant);
                    var enumMemberAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        enumType,
                        IdentifierName(fieldName));
                    // String enums: Color.RED already returns the string value from the static field
                    return enumMemberAccess;
                }
                else
                {
                    // Integer enums use PascalCase member names
                    var enumMemberName = NameMangler.ToEnumMemberName(memberAccess.Member);
                    var enumMemberAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        enumType,
                        IdentifierName(enumMemberName));
                    // Return the enum member directly (not cast to int)
                    // The .value property is used to get the underlying int value
                    return enumMemberAccess;
                }
            }
        }

        var obj = GenerateExpression(memberAccess.Object);

        // Handle special .value property for enum instances
        // enum_instance.value -> (int)enum_instance
        if (string.Equals(memberAccess.Member, "value", StringComparison.OrdinalIgnoreCase))
        {
            // Only cast to int if the object expression is of an enum type
            if (IsEnumTypeExpression(memberAccess.Object))
            {
                return CastExpression(
                    PredefinedType(Token(SyntaxKind.IntKeyword)),
                    obj);
            }
        }

        // Apply name mangling to member names:
        // - Dunder methods use DunderMapping
        // - ALL_CAPS names (Python-style constants) use CONSTANT_CASE
        // - Other names use PascalCase
        var mangledMemberName = DunderMapping.ResolveCSharpName(memberAccess.Member)
            ?? (NameFormDetector.IsConstantCaseName(memberAccess.Member)
                ? NameMangler.ToConstantCase(memberAccess.Member)
                : NameMangler.ToPascalCase(memberAccess.Member));
        var member = IdentifierName(mangledMemberName);

        if (memberAccess.IsNullConditional)
        {
            // For Optional<T>: lower to ternary since ?. doesn't work on structs
            if (GetExpressionSemanticType(memberAccess.Object) is OptionalType)
            {
                // Ensure obj is only evaluated once for complex expressions
                var (safeObj, capture) = EnsureSingleEvaluation(obj, memberAccess.Object);
                // safeObj.IsSome ? safeObj.Unwrap().Member : Optional<T>.None
                ExpressionSyntax cond = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    ParenthesizedExpression(safeObj), IdentifierName("IsSome"));
                if (capture != null)
                    cond = BinaryExpression(SyntaxKind.LogicalAndExpression, capture, cond);

                var trueExpr = (ExpressionSyntax)MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            safeObj, IdentifierName("Unwrap")))
                        .WithArgumentList(ArgumentList()),
                    member);

                // Use Optional<T>.None for the false branch so C# resolves the ternary
                // as Optional<T> (via implicit conversion on the true branch if needed)
                var falseExpr = GetExpressionSemanticType(memberAccess) is OptionalType optExprType
                    ? (ExpressionSyntax)GenerateOptionalNone(optExprType)
                    : LiteralExpression(SyntaxKind.DefaultLiteralExpression);

                return ConditionalExpression(cond, trueExpr, falseExpr);
            }
            // obj?.member
            return ConditionalAccessExpression(obj,
                MemberBindingExpression(member));
        }
        else
        {
            // obj.member
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                member);
        }
    }

    /// <summary>
    /// Attempts to extract a module path from a member access chain.
    /// For example, lib.math.add becomes ["lib", "math", "add"].
    /// Returns true if the entire chain represents module access, false otherwise.
    /// </summary>
    private bool TryExtractModulePath(MemberAccess memberAccess, out List<string> modulePath)
    {
        modulePath = new List<string>();

        // Build the path by traversing the member access chain
        Expression current = memberAccess;
        while (current is MemberAccess ma)
        {
            // Add the member name to the front of the list
            modulePath.Insert(0, ma.Member);
            current = ma.Object;
        }

        // The base should be an identifier
        if (current is not Identifier identifier)
        {
            modulePath.Clear();
            return false;
        }

        // Add the base identifier to the front
        modulePath.Insert(0, identifier.Name);

        // Now check if this path represents module access
        // We need at least 2 parts (e.g., lib.math)
        if (modulePath.Count < 2)
        {
            modulePath.Clear();
            return false;
        }

        // Check if the base is a module symbol
        var baseSymbol = _context.LookupSymbol(modulePath[0]);
        if (baseSymbol is not ModuleSymbol)
        {
            modulePath.Clear();
            return false;
        }

        // Verify that the path exists in the module hierarchy
        var currentModule = (ModuleSymbol)baseSymbol;  // Safe cast - we already checked it's a ModuleSymbol
        for (int i = 1; i < modulePath.Count; i++)
        {
            var memberName = modulePath[i];

            // Check if this member exists in the current module's exports
            if (!currentModule.Exports.TryGetValue(memberName, out var exportedSymbol))
            {
                modulePath.Clear();
                return false;
            }

            // If this is not the last element, it should be a nested module
            if (i < modulePath.Count - 1)
            {
                if (exportedSymbol is not ModuleSymbol nestedModule)
                {
                    modulePath.Clear();
                    return false;
                }
                currentModule = nestedModule;
            }
            // The last element can be any symbol (function, variable, or module)
        }

        return true;
    }

    /// <summary>
    /// Builds a C# member access expression from a module path.
    /// For example, ["lib", "math", "add"] becomes Lib.Math.Add.
    /// Special handling for imported modules: if the base is an imported module with a using alias,
    /// use the alias directly. For example, ["config", "MAX_SIZE"] with "import config" becomes
    /// "config.MaxSize" (using the alias created by the using directive).
    /// </summary>
    private ExpressionSyntax BuildModuleAccessExpression(List<string> modulePath)
    {
        if (modulePath.Count == 0)
        {
            throw new ArgumentException("Module path cannot be empty", nameof(modulePath));
        }

        // Check if the base is an imported module symbol
        var baseSymbol = _context.LookupSymbol(modulePath[0]);
        if (baseSymbol is ModuleSymbol)
        {
            // For imported modules, we need to check if we have a using alias
            // For "import parent.child", the alias is "parent_child"
            // For accessing "parent.child.member", we use "parent_child.Member"

            // Find the longest module path prefix that matches an import
            // For example, if we have "import parent.child" and access "parent.child.child_func",
            // we want to find "parent.child" as the import and "child_func" as the member

            ModuleSymbol currentModule = (ModuleSymbol)baseSymbol;
            int modulePartCount = 1;

            // Try to traverse the module hierarchy to find how deep the imported module goes
            for (int i = 1; i < modulePath.Count; i++)
            {
                var memberName = modulePath[i];

                // Check if this is a nested module in the current module's exports
                if (currentModule.Exports.TryGetValue(memberName, out var exportedSymbol)
                    && exportedSymbol is ModuleSymbol nestedModule)
                {
                    currentModule = nestedModule;
                    modulePartCount++;
                }
                else
                {
                    // Not a nested module - this is a member access
                    break;
                }
            }

            // Build the import alias from the module path parts
            // Also escape C# keywords like "base" -> "@base"
            var moduleParts = modulePath.Take(modulePartCount);
            var aliasName = EscapeCSharpKeyword(string.Join("_", moduleParts));

            // If the entire path is just the module (no member access), return the alias
            if (modulePartCount == modulePath.Count)
            {
                return IdentifierName(aliasName);
            }

            // Build member access: alias.Member1.Member2...
            ExpressionSyntax expr = IdentifierName(aliasName);
            for (int i = modulePartCount; i < modulePath.Count; i++)
            {
                // Use CONSTANT_CASE for ALL_CAPS names (Python-style constants)
                var memberPart = modulePath[i];
                var mangledMemberName = NameFormDetector.IsConstantCaseName(memberPart)
                    ? NameMangler.ToConstantCase(memberPart)
                    : NameMangler.ToPascalCase(memberPart);
                expr = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expr,
                    IdentifierName(mangledMemberName));
            }

            return expr;
        }

        // For multi-part module paths (e.g., lib.math.add) or other cases,
        // build the full qualified path (e.g., Lib.Math.Add)
        ExpressionSyntax current = IdentifierName(NameMangler.ToPascalCase(modulePath[0]));

        // Chain the rest of the path
        for (int i = 1; i < modulePath.Count; i++)
        {
            // Use CONSTANT_CASE for ALL_CAPS names (Python-style constants)
            var memberPart = modulePath[i];
            var memberName = NameFormDetector.IsConstantCaseName(memberPart)
                ? NameMangler.ToConstantCase(memberPart)
                : NameMangler.ToPascalCase(memberPart);
            current = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName(memberName));
        }

        return current;
    }

    private ExpressionSyntax GenerateIndexAccess(IndexAccess indexAccess)
    {
        var obj = GenerateExpression(indexAccess.Object);
        var index = GenerateExpression(indexAccess.Index);

        return ElementAccessExpression(obj)
            .AddArgumentListArguments(Argument(index));
    }

    private ExpressionSyntax GenerateSliceAccess(SliceAccess sliceAccess)
    {
        // arr[start:stop:step]
        // Translates to: global::Sharpy.Slice(arr, start, stop, step)
        var obj = GenerateExpression(sliceAccess.Object);
        var start = sliceAccess.Start != null
            ? GenerateExpression(sliceAccess.Start)
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
        var stop = sliceAccess.Stop != null
            ? GenerateExpression(sliceAccess.Stop)
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
        var step = sliceAccess.Step != null
            ? GenerateExpression(sliceAccess.Step)
            : LiteralExpression(SyntaxKind.NullLiteralExpression);

        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                ParseName("global::Sharpy"),
                IdentifierName("Slice")))
            .AddArgumentListArguments(
                Argument(obj),
                Argument(start),
                Argument(stop),
                Argument(step));
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

    private ExpressionSyntax GenerateLambdaExpression(LambdaExpression lambda)
    {
        // lambda x, y: x + y → (x, y) => x + y
        var parameters = lambda.Parameters
            .Select(p => Parameter(Identifier(NameMangler.ToCamelCase(p.Name))))
            .ToArray();

        var body = GenerateExpression(lambda.Body);

        if (parameters.Length == 0)
        {
            return ParenthesizedLambdaExpression()
                .WithExpressionBody(body);
        }
        else if (parameters.Length == 1)
        {
            return SimpleLambdaExpression(parameters[0])
                .WithExpressionBody(body);
        }
        else
        {
            return ParenthesizedLambdaExpression()
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithExpressionBody(body);
        }
    }

    private ExpressionSyntax GenerateTypeCast(TypeCast cast)
    {
        // value as Type → (Type)value
        var value = GenerateExpression(cast.Value);
        var targetType = _typeMapper.MapType(cast.TargetType);

        return CastExpression(targetType, value);
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

    private ExpressionSyntax GenerateFString(FStringLiteral fstring)
    {
        // f"Hello {name}" → $"Hello {name}"
        var parts = new List<InterpolatedStringContentSyntax>();

        foreach (var part in fstring.Parts)
        {
            if (part.Text != null)
            {
                parts.Add(InterpolatedStringText()
                    .WithTextToken(Token(
                        TriviaList(),
                        SyntaxKind.InterpolatedStringTextToken,
                        part.Text,
                        part.Text,
                        TriviaList())));
            }
            else if (part.Expression != null)
            {
                // Special handling for percent format (.N%) - Python's % format doesn't add
                // a space before %, but .NET's P format does (even with InvariantCulture).
                // Generate: {value * 100:FN}% instead of {value:PN}
                if (!string.IsNullOrEmpty(part.FormatSpec) && IsPercentFormat(part.FormatSpec, out var percentPrecision))
                {
                    // Generate: value * 100
                    var multipliedExpr = BinaryExpression(
                        SyntaxKind.MultiplyExpression,
                        GenerateExpression(part.Expression),
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(100)));

                    var interpolation = Interpolation(multipliedExpr)
                        .WithFormatClause(
                            InterpolationFormatClause(
                                Token(SyntaxKind.ColonToken),
                                Token(
                                    TriviaList(),
                                    SyntaxKind.InterpolatedStringTextToken,
                                    "F" + percentPrecision,
                                    "F" + percentPrecision,
                                    TriviaList())));

                    parts.Add(interpolation);

                    // Add the literal "%" after the interpolation
                    parts.Add(InterpolatedStringText()
                        .WithTextToken(Token(
                            TriviaList(),
                            SyntaxKind.InterpolatedStringTextToken,
                            "%",
                            "%",
                            TriviaList())));
                }
                else
                {
                    var interpolation = Interpolation(GenerateExpression(part.Expression));

                    // Add format specifier if present (e.g., ".2f" in f"{value:.2f}")
                    if (!string.IsNullOrEmpty(part.FormatSpec))
                    {
                        var csharpFormatSpec = TranslatePythonFormatSpec(part.FormatSpec);
                        interpolation = interpolation.WithFormatClause(
                            InterpolationFormatClause(
                                Token(SyntaxKind.ColonToken),
                                Token(
                                    TriviaList(),
                                    SyntaxKind.InterpolatedStringTextToken,
                                    csharpFormatSpec,
                                    csharpFormatSpec,
                                    TriviaList())));
                    }

                    parts.Add(interpolation);
                }
            }
        }

        // Wrap with FormattableString.Invariant() to ensure consistent formatting
        // regardless of locale (e.g., percent format uses space before % in some locales)
        var interpolatedString = InterpolatedStringExpression(Token(SyntaxKind.InterpolatedStringStartToken))
            .WithContents(List(parts));

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("FormattableString"),
                IdentifierName("Invariant")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(interpolatedString))));
    }

    /// <summary>
    /// Translates Python format specifiers to C# format specifiers.
    /// Python: [[fill]align][sign][#][0][width][grouping_option][.precision][type]
    ///
    /// Supported conversions:
    /// - .Nf → FN (fixed-point, N decimal places)
    /// - .Ne → EN (scientific notation)
    /// - .N% → PN (percent)
    /// - 0N → DN (zero-padded integer width N)
    /// - , → N0 (number with thousand separators)
    /// - .Ng → GN (general format)
    /// </summary>
    private static string TranslatePythonFormatSpec(string pythonSpec)
    {
        if (string.IsNullOrEmpty(pythonSpec))
            return pythonSpec;

        // Handle thousand separator only: "," → "N0"
        if (pythonSpec == ",")
            return "N0";

        // Handle .Nf (fixed-point): ".2f" → "F2"
        if (pythonSpec.StartsWith(".") && pythonSpec.EndsWith("f"))
        {
            var precision = pythonSpec.Substring(1, pythonSpec.Length - 2);
            if (int.TryParse(precision, out _))
                return "F" + precision;
        }

        // Handle .Ne (scientific): ".2e" → "E2"
        if (pythonSpec.StartsWith(".") && pythonSpec.EndsWith("e"))
        {
            var precision = pythonSpec.Substring(1, pythonSpec.Length - 2);
            if (int.TryParse(precision, out _))
                return "E" + precision;
        }

        // Handle .N% (percent): ".1%" → "P1"
        if (pythonSpec.StartsWith(".") && pythonSpec.EndsWith("%"))
        {
            var precision = pythonSpec.Substring(1, pythonSpec.Length - 2);
            if (int.TryParse(precision, out _))
                return "P" + precision;
        }

        // Handle .Ng (general): ".3g" → "G3"
        if (pythonSpec.StartsWith(".") && pythonSpec.EndsWith("g"))
        {
            var precision = pythonSpec.Substring(1, pythonSpec.Length - 2);
            if (int.TryParse(precision, out _))
                return "G" + precision;
        }

        // Handle 0N (zero-padded): "05" → "D5" for integers
        if (pythonSpec.StartsWith("0") && pythonSpec.Length > 1)
        {
            var width = pythonSpec.Substring(1);
            if (int.TryParse(width, out _))
                return "D" + width;
        }

        // Fall back to passing through (may not work, but allows custom C# formats)
        return pythonSpec;
    }

    /// <summary>
    /// Checks if a Python format spec is a percent format (.N%) and extracts the precision.
    /// </summary>
    private static bool IsPercentFormat(string pythonSpec, out string precision)
    {
        precision = "0";
        if (pythonSpec.StartsWith(".") && pythonSpec.EndsWith("%"))
        {
            precision = pythonSpec.Substring(1, pythonSpec.Length - 2);
            return int.TryParse(precision, out _);
        }
        return false;
    }

    /// <summary>
    /// Gets the fully qualified C# type name for a type, handling cross-file references.
    /// Types are nested inside the module class, so cross-file references use
    /// Namespace.ModuleClass.TypeName.
    /// </summary>
    private string GetFullyQualifiedTypeName(TypeSymbol typeSymbol, string sharpyTypeName)
    {
        // Check if type is from a different file (cross-file reference)
        if (!string.IsNullOrEmpty(typeSymbol.DefiningFilePath) &&
            !string.IsNullOrEmpty(_context.SourceFilePath) &&
            !string.Equals(typeSymbol.DefiningFilePath, _context.SourceFilePath, StringComparison.OrdinalIgnoreCase))
        {
            var moduleNamespace = GetModuleNameFromFilePath(typeSymbol.DefiningFilePath);
            var typeName = NameMangler.ToPascalCase(sharpyTypeName);

            return BuildQualifiedTypeName(moduleNamespace, typeName);
        }

        // Check if type is from an external module (imported via DefiningModule)
        if (!string.IsNullOrEmpty(typeSymbol.DefiningModule))
        {
            var moduleNamespace = ConvertModuleToNamespace(typeSymbol.DefiningModule);
            var typeName = NameMangler.ToPascalCase(sharpyTypeName);

            return BuildQualifiedTypeName(moduleNamespace, typeName);
        }

        // Type is in current file - use simple name
        return NameMangler.ToPascalCase(sharpyTypeName);
    }

    /// <summary>
    /// Builds a fully qualified type name, handling collision cases where the type IS
    /// the module class (e.g., animal.spy defining class Animal).
    /// </summary>
    private string BuildQualifiedTypeName(string moduleNamespace, string typeName)
    {
        // Check for collision: when the module name matches the type name,
        // the type IS the module class, not nested inside it.
        var lastSegment = moduleNamespace.Contains('.')
            ? moduleNamespace.Split('.').Last()
            : moduleNamespace;

        if (string.Equals(lastSegment, typeName, StringComparison.Ordinal))
        {
            // Type IS the module class — module path is the type path
            if (!string.IsNullOrEmpty(_context.ProjectNamespace))
            {
                return $"{_context.ProjectNamespace}.{moduleNamespace}";
            }
            return moduleNamespace;
        }

        // Type is nested inside the module class
        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
        {
            return $"{_context.ProjectNamespace}.{moduleNamespace}.{typeName}";
        }
        return $"{moduleNamespace}.{typeName}";
    }

    /// <summary>
    /// Derives a module namespace from a file path, computing the full package path
    /// relative to the project root.
    /// E.g., for project root "/temp" and file "/temp/mypackage/submodule.spy" -> "Mypackage.Submodule"
    /// </summary>
    private string GetModuleNameFromFilePath(string filePath)
    {
        // If we have a project root, compute relative path for proper namespace
        if (!string.IsNullOrEmpty(_context.ProjectRootPath))
        {
            var relativePath = Path.GetRelativePath(_context.ProjectRootPath, filePath);
            var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            var namespaceParts = new List<string>();

            // Add directory parts (package hierarchy)
            if (!string.IsNullOrEmpty(relativeDir) && relativeDir != ".")
            {
                var dirParts = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var part in dirParts)
                {
                    if (!string.IsNullOrEmpty(part) && part != ".")
                    {
                        namespaceParts.Add(NameMangler.ToPascalCase(part));
                    }
                }
            }

            // Add file name part (skip __init__ as it represents the package itself)
            if (!string.Equals(fileName, DunderNames.Init, StringComparison.OrdinalIgnoreCase))
            {
                namespaceParts.Add(NameMangler.ToPascalCase(fileName));
            }

            if (namespaceParts.Count > 0)
            {
                return string.Join(".", namespaceParts);
            }
        }

        // Fallback: just use file name
        var fallbackFileName = Path.GetFileNameWithoutExtension(filePath);
        return NameMangler.ToPascalCase(fallbackFileName);
    }

    /// <summary>
    /// Converts a module path (e.g., "animal" or "lib.animal") to a C# namespace segment.
    /// </summary>
    private static string ConvertModuleToNamespace(string modulePath)
    {
        var parts = modulePath.Split('.');
        return string.Join(".", parts.Select(p => NameMangler.ToPascalCase(p)));
    }

    // ============================================================
    // Helper: Single-evaluation capture for complex expressions
    // ============================================================

    /// <summary>
    /// Returns true if the AST expression is side-effect-free (safe to evaluate multiple times).
    /// Simple identifiers, self, and literals are safe; everything else may have side effects.
    /// </summary>
    private static bool IsSideEffectFree(Expression expr)
        => expr is Parser.Ast.Identifier or NoneLiteral or BooleanLiteral or IntegerLiteral
                 or FloatLiteral or StringLiteral or SuperExpression;

    /// <summary>
    /// Ensures an expression is only evaluated once. For simple identifiers, returns the
    /// expression as-is. For complex expressions, captures the value using an inline
    /// <c>is var</c> pattern: <c>expr is var __temp &amp;&amp; __temp.Check ? __temp.Access : default</c>.
    /// Returns the safe-to-reuse expression and an optional capture condition to prepend.
    /// </summary>
    private (ExpressionSyntax SafeExpr, ExpressionSyntax? CaptureCondition) EnsureSingleEvaluation(
        ExpressionSyntax generated, Expression astExpr)
    {
        if (IsSideEffectFree(astExpr))
            return (generated, null);

        var tempName = GenerateTempVarName("opt");
        var tempIdent = IdentifierName(tempName);
        var capture = IsPatternExpression(
            generated,
            VarPattern(SingleVariableDesignation(Identifier(tempName))));
        return (tempIdent, capture);
    }

    // ============================================================
    // Tagged Union Constructor Generation (Some/Ok/Err)
    // ============================================================

    /// <summary>
    /// Gets the semantic type of an expression from SemanticInfo, if available.
    /// </summary>
    private SemanticType? GetExpressionSemanticType(Sharpy.Compiler.Parser.Ast.Expression expr)
    {
        return _context.SemanticInfo?.GetExpressionType(expr);
    }

    /// <summary>
    /// Checks if a function call is a tagged union constructor (Some, Ok, Err)
    /// by checking the expression's semantic type from SemanticInfo.
    /// </summary>
    private bool IsTaggedUnionConstructorCall(FunctionCall call)
    {
        if (call.Function is not Identifier id)
            return false;

        if (id.Name is not ("Some" or "Ok" or "Err"))
            return false;

        var exprType = GetExpressionSemanticType(call);
        return exprType is OptionalType or ResultType;
    }

    /// <summary>
    /// Generates code for a tagged union constructor call (Some, Ok, Err).
    /// Some(v) generates Optional&lt;T&gt;.Some(v).
    /// Ok(v)/Err(e) generate Result&lt;T,E&gt;.Ok(v)/Err(e).
    /// </summary>
    private ExpressionSyntax GenerateTaggedUnionConstructor(FunctionCall call)
    {
        var id = (Identifier)call.Function;
        var exprType = GetExpressionSemanticType(call)!;

        return (id.Name, exprType) switch
        {
            ("Some", OptionalType opt) => GenerateSomeExpression(call, opt),
            ("Ok", ResultType res) => GenerateOkExpression(call, res),
            ("Err", ResultType res) => GenerateErrExpression(call, res),
            _ => throw new InvalidOperationException($"Unexpected tagged union constructor: {id.Name}")
        };
    }

    /// <summary>
    /// Generates: Optional&lt;T&gt;.None (static property access)
    /// </summary>
    private ExpressionSyntax GenerateOptionalNone(OptionalType opt)
    {
        var underlyingType = _typeMapper.MapSemanticType(opt.UnderlyingType);

        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            GenericName("Optional")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(underlyingType))),
            IdentifierName("None"));
    }

    /// <summary>
    /// Generates: Optional&lt;T&gt;.Some(value)
    /// </summary>
    private ExpressionSyntax GenerateSomeExpression(FunctionCall call, OptionalType opt)
    {
        var underlyingType = _typeMapper.MapSemanticType(opt.UnderlyingType);
        var arg = GenerateExpression(call.Arguments[0]);

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GenericName("Optional")
                    .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(underlyingType))),
                IdentifierName("Some")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));
    }

    /// <summary>
    /// Generates: Result&lt;T, E&gt;.Ok(value)
    /// </summary>
    private ExpressionSyntax GenerateOkExpression(FunctionCall call, ResultType res)
    {
        var okType = _typeMapper.MapSemanticType(res.OkType);
        var errType = _typeMapper.MapSemanticType(res.ErrorType);
        var arg = GenerateExpression(call.Arguments[0]);

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GenericName("Result")
                    .WithTypeArgumentList(TypeArgumentList(SeparatedList(new[] { okType, errType }))),
                IdentifierName("Ok")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));
    }

    /// <summary>
    /// Generates: Result&lt;T, E&gt;.Err(error)
    /// </summary>
    private ExpressionSyntax GenerateErrExpression(FunctionCall call, ResultType res)
    {
        var okType = _typeMapper.MapSemanticType(res.OkType);
        var errType = _typeMapper.MapSemanticType(res.ErrorType);
        var arg = GenerateExpression(call.Arguments[0]);

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GenericName("Result")
                    .WithTypeArgumentList(TypeArgumentList(SeparatedList(new[] { okType, errType }))),
                IdentifierName("Err")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));
    }

}
