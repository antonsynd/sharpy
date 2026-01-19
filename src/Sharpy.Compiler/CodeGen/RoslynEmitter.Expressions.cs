using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Expression generation (literals, operators, calls, comprehensions)
/// </summary>
public partial class RoslynEmitter
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
            Identifier name when string.Equals(name.Name, "self", StringComparison.OrdinalIgnoreCase) => ThisExpression(),
            Identifier name => IdentifierName(GetMangledVariableName(name.Name, isNewDeclaration: false)),
            SuperExpression => BaseExpression(),  // super() -> base
            MemberAccess memberAccess => GenerateMemberAccess(memberAccess),
            IndexAccess indexAccess => GenerateIndexAccess(indexAccess),
            SliceAccess sliceAccess => GenerateSliceAccess(sliceAccess),
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

            _ => throw new NotImplementedException($"Expression type not implemented: {expr.GetType().Name}")
        };
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
                        .WithNameColon(NameColon(IdentifierName(kwarg.Name))));
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
                        .WithNameColon(NameColon(IdentifierName(kwarg.Name))));
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
            // We check both:
            // 1. The _classNames and _structNames sets (populated during type declaration generation)
            // 2. The symbol table (for testing and imported types)
            // BUT: If it's a builtin function, it's NOT a type instantiation (e.g., int(x) is a conversion function)
            var symbol = _context.LookupSymbol(funcName.Name);
            var isTypeInstantiation = !isBuiltinFunc &&
                                     (_classNames.Contains(funcName.Name) ||
                                      _structNames.Contains(funcName.Name) ||
                                      (symbol is TypeSymbol typeSymbol &&
                                       (typeSymbol.TypeKind == Semantic.TypeKind.Class ||
                                        typeSymbol.TypeKind == Semantic.TypeKind.Struct)));

            string name;
            if (isBuiltinFunc)
            {
                name = $"global::Sharpy.Core.Exports.{NameMangler.ToPascalCase(funcName.Name)}";
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
                    .WithNameColon(NameColon(IdentifierName(kwarg.Name))));

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

            // Apply name mangling to method name
            var methodName = NameMangler.ToPascalCase(memberAccess.Member);

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

        throw new NotImplementedException("Complex function expressions not yet supported");
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
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("System"),
                            IdentifierName("Math")),
                        IdentifierName("Pow")))
                    .AddArgumentListArguments(
                        Argument(left),
                        Argument(right));

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
                // x in y → y.__Contains__(x)
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        right,
                        IdentifierName("__Contains__")))
                    .AddArgumentListArguments(Argument(left));

            case BinaryOperator.NotIn:
                // x not in y → !y.__Contains__(x)
                return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            right,
                            IdentifierName("__Contains__")))
                        .AddArgumentListArguments(Argument(left)));

            case BinaryOperator.Is:
                // x is y → object.ReferenceEquals(x, y)
                // Special optimization for None: x is None → x == null
                if (binOp.Right is NoneLiteral)
                {
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
                // Special optimization for None: x is not None → x != null
                if (binOp.Right is NoneLiteral)
                {
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

            // Null coalescing
            BinaryOperator.NullCoalesce => SyntaxKind.CoalesceExpression,

            _ => throw new NotImplementedException($"Binary operator not implemented: {binOp.Operator}")
        };

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
                    .WithNameColon(NameColon(IdentifierName(k.Name))));

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
                ? $"global::Sharpy.Core.Exports.{NameMangler.ToPascalCase(funcName.Name)}"
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
            _ => throw new NotImplementedException($"Unary operator not implemented: {unaryOp.Operator}")
        };

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
        // new global::Sharpy.Core.List<T> { elem1, elem2, elem3 }
        // Prefer target type annotation if available (e.g., list[int] = [...])
        TypeSyntax elementType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == "list" &&
            _targetTypeContext.TypeArguments.Count > 0)
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

        var listType = GenericName("global::Sharpy.Core.List")
            .AddTypeArgumentListArguments(elementType);

        return ObjectCreationExpression(listType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateDictLiteral(DictLiteral dict)
    {
        // new global::Sharpy.Core.Dict<K,V> { { key1, value1 }, { key2, value2 } }
        // Prefer target type annotation if available (e.g., dict[str, int] = {...})
        TypeSyntax keyType, valueType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == "dict" &&
            _targetTypeContext.TypeArguments.Count >= 2)
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

        var dictType = GenericName("global::Sharpy.Core.Dict")
            .AddTypeArgumentListArguments(keyType, valueType);

        return ObjectCreationExpression(dictType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList<ExpressionSyntax>(initializers)));
    }

    private ExpressionSyntax GenerateSetLiteral(SetLiteral set)
    {
        // new global::Sharpy.Core.Set<T> { elem1, elem2, elem3 }
        // Prefer target type annotation if available (e.g., set[int] = {...})
        TypeSyntax elementType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == "set" &&
            _targetTypeContext.TypeArguments.Count > 0)
        {
            elementType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
        }
        else
        {
            elementType = _typeMapper.InferElementType(set.Elements);
        }

        var elements = set.Elements.Select(GenerateExpression);

        var setType = GenericName("global::Sharpy.Core.Set")
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

    // TODO: For nested or complex comprehensions, consider switching to imperative code generation
    // (using foreach loops and temporary lists) to improve readability and handle edge cases.
    // A complexity heuristic could be: multiple for clauses, or deeply nested comprehensions.

    private ExpressionSyntax GenerateListComprehension(ListComprehension listComp)
    {
        // Generate LINQ method chain: iterator.Where(...).Select(...).ToList()
        // Example: [x * 2 for x in items if x > 0]
        // becomes: items.Where(x => x > 0).Select(x => x * 2).ToList()

        if (listComp.Clauses.Count == 0 || listComp.Clauses[0] is not ForClause firstFor)
        {
            throw new InvalidOperationException("List comprehension must start with a for clause");
        }

        // Get the loop variable name (single identifier only)
        if (firstFor.Target is not Identifier loopVar)
        {
            throw new NotImplementedException("Tuple unpacking in comprehensions not yet supported");
        }

        var varName = NameMangler.ToCamelCase(loopVar.Name);
        var param = Parameter(Identifier(varName));

        // Start with the iterator expression
        ExpressionSyntax current = GenerateExpression(firstFor.Iterator);

        // Apply each if clause as .Where(x => condition)
        foreach (var clause in listComp.Clauses.Skip(1))
        {
            if (clause is IfClause ifClause)
            {
                var condition = GenerateExpression(ifClause.Condition);
                var lambda = SimpleLambdaExpression(param)
                    .WithExpressionBody(condition);

                current = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        current,
                        IdentifierName("Where")))
                    .AddArgumentListArguments(Argument(lambda));
            }
            else if (clause is ForClause)
            {
                // Multiple for clauses (nested iteration) - requires more complex LINQ
                // For now, throw NotImplementedException
                throw new NotImplementedException("Nested comprehensions (multiple for clauses) not yet supported");
            }
        }

        // Apply .Select(x => element_expression)
        var elementExpr = GenerateExpression(listComp.Element);
        var selectLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(elementExpr);

        current = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName("Select")))
            .AddArgumentListArguments(Argument(selectLambda));

        // Apply .ToList()
        current = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName("ToList")))
            .WithArgumentList(ArgumentList());

        return current;
    }

    private ExpressionSyntax GenerateSetComprehension(SetComprehension setComp)
    {
        // Generate LINQ method chain: iterator.Where(...).Select(...).ToHashSet()
        // Example: {x * 2 for x in items if x > 0}
        // becomes: items.Where(x => x > 0).Select(x => x * 2).ToHashSet()

        if (setComp.Clauses.Count == 0 || setComp.Clauses[0] is not ForClause firstFor)
        {
            throw new InvalidOperationException("Set comprehension must start with a for clause");
        }

        // Get the loop variable name (single identifier only)
        if (firstFor.Target is not Identifier loopVar)
        {
            throw new NotImplementedException("Tuple unpacking in comprehensions not yet supported");
        }

        var varName = NameMangler.ToCamelCase(loopVar.Name);
        var param = Parameter(Identifier(varName));

        // Start with the iterator expression
        ExpressionSyntax current = GenerateExpression(firstFor.Iterator);

        // Apply each if clause as .Where(x => condition)
        foreach (var clause in setComp.Clauses.Skip(1))
        {
            if (clause is IfClause ifClause)
            {
                var condition = GenerateExpression(ifClause.Condition);
                var lambda = SimpleLambdaExpression(param)
                    .WithExpressionBody(condition);

                current = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        current,
                        IdentifierName("Where")))
                    .AddArgumentListArguments(Argument(lambda));
            }
            else if (clause is ForClause)
            {
                throw new NotImplementedException("Nested comprehensions (multiple for clauses) not yet supported");
            }
        }

        // Apply .Select(x => element_expression)
        var elementExpr = GenerateExpression(setComp.Element);
        var selectLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(elementExpr);

        current = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName("Select")))
            .AddArgumentListArguments(Argument(selectLambda));

        // Apply .ToHashSet()
        current = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName("ToHashSet")))
            .WithArgumentList(ArgumentList());

        return current;
    }

    private ExpressionSyntax GenerateDictComprehension(DictComprehension dictComp)
    {
        // Generate LINQ method chain: iterator.Where(...).ToDictionary(x => key, x => value)
        // Example: {k: v for k, v in pairs if v > 0}
        // For now, only support single variable (not tuple unpacking)
        // becomes: pairs.Where(p => p.v > 0).ToDictionary(p => p.k, p => p.v)

        if (dictComp.Clauses.Count == 0 || dictComp.Clauses[0] is not ForClause firstFor)
        {
            throw new InvalidOperationException("Dict comprehension must start with a for clause");
        }

        // Get the loop variable name (single identifier only)
        if (firstFor.Target is not Identifier loopVar)
        {
            throw new NotImplementedException("Tuple unpacking in comprehensions not yet supported");
        }

        var varName = NameMangler.ToCamelCase(loopVar.Name);
        var param = Parameter(Identifier(varName));

        // Start with the iterator expression
        ExpressionSyntax current = GenerateExpression(firstFor.Iterator);

        // Apply each if clause as .Where(x => condition)
        foreach (var clause in dictComp.Clauses.Skip(1))
        {
            if (clause is IfClause ifClause)
            {
                var condition = GenerateExpression(ifClause.Condition);
                var lambda = SimpleLambdaExpression(param)
                    .WithExpressionBody(condition);

                current = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        current,
                        IdentifierName("Where")))
                    .AddArgumentListArguments(Argument(lambda));
            }
            else if (clause is ForClause)
            {
                throw new NotImplementedException("Nested comprehensions (multiple for clauses) not yet supported");
            }
        }

        // Generate key and value selector lambdas
        var keyExpr = GenerateExpression(dictComp.Key);
        var valueExpr = GenerateExpression(dictComp.Value);

        var keyLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(keyExpr);
        var valueLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(valueExpr);

        // Apply .ToDictionary(x => key, x => value)
        current = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName("ToDictionary")))
            .AddArgumentListArguments(
                Argument(keyLambda),
                Argument(valueLambda));

        return current;
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
                // Enum member access: Color.RED -> (int)Program.Color.Red
                // We fully qualify with "Program." to avoid shadowing by local variables/fields with the same name
                // This makes the enum value resolve to its underlying int value (Python semantics)
                var enumTypeName = NameMangler.ToPascalCase(enumTypeIdentifier.Name);

                // Build qualified enum type: Program.EnumName
                var qualifiedEnumType = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Program"),
                    IdentifierName(enumTypeName));

                // Check if this is a string enum (string enums are generated as classes, not C# enums)
                if (IsStringEnumSymbol(enumSymbol))
                {
                    // String enums use CONSTANT_CASE field names (same as NameContext.Constant)
                    var fieldName = NameMangler.Transform(memberAccess.Member, NameContext.Constant);
                    var enumMemberAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        qualifiedEnumType,
                        IdentifierName(fieldName));
                    // String enums: Color.RED already returns the string value from the static field
                    return enumMemberAccess;
                }
                else
                {
                    // Integer enums use PascalCase member names
                    var enumMemberName = TransformEnumMemberName(memberAccess.Member);
                    var enumMemberAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        qualifiedEnumType,
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
        // - ALL_CAPS names (Python-style constants) use CONSTANT_CASE
        // - Other names use PascalCase
        var mangledMemberName = IsConstantCaseName(memberAccess.Member)
            ? NameMangler.ToConstantCase(memberAccess.Member)
            : NameMangler.ToPascalCase(memberAccess.Member);
        var member = IdentifierName(mangledMemberName);

        if (memberAccess.IsNullConditional)
        {
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
                var mangledMemberName = IsConstantCaseName(memberPart)
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
            var memberName = IsConstantCaseName(memberPart)
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
        // Translates to: Sharpy.Core.Slice(arr, start, stop, step)
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
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Sharpy"),
                    IdentifierName("Core")),
                IdentifierName("Slice")))
            .AddArgumentListArguments(
                Argument(obj),
                Argument(start),
                Argument(stop),
                Argument(step));
    }

    private ExpressionSyntax GenerateComparisonChain(ComparisonChain chain)
    {
        // a < b < c → a < b && b < c (with b evaluated once)
        // For simplicity in v0.6, we'll allow re-evaluation
        // TODO: Store intermediate values in temp variables

        if (chain.Operands.Count < 2 || chain.Operators.Count != chain.Operands.Count - 1)
        {
            throw new InvalidOperationException("Invalid comparison chain");
        }

        ExpressionSyntax? result = null;

        for (int i = 0; i < chain.Operators.Count; i++)
        {
            var left = GenerateExpression(chain.Operands[i]);
            var right = GenerateExpression(chain.Operands[i + 1]);
            var op = chain.Operators[i];

            var kind = op switch
            {
                ComparisonOperator.Equal => SyntaxKind.EqualsExpression,
                ComparisonOperator.NotEqual => SyntaxKind.NotEqualsExpression,
                ComparisonOperator.LessThan => SyntaxKind.LessThanExpression,
                ComparisonOperator.LessThanOrEqual => SyntaxKind.LessThanOrEqualExpression,
                ComparisonOperator.GreaterThan => SyntaxKind.GreaterThanExpression,
                ComparisonOperator.GreaterThanOrEqual => SyntaxKind.GreaterThanOrEqualExpression,
                _ => throw new NotImplementedException($"Comparison operator {op} not supported in chains")
            };

            var comparison = BinaryExpression(kind, left, right);

            result = result == null
                ? comparison
                : BinaryExpression(SyntaxKind.LogicalAndExpression, result, comparison);
        }

        return result ?? throw new InvalidOperationException("Empty comparison chain");
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
        // - value to T? → value as T (for reference types, returns null on failure)
        //                 value is T _temp ? (T?)_temp : null (for value types)

        var value = GenerateExpression(coercion.Value);

        if (coercion.TargetType.IsNullable)
        {
            // Safe form: value to T?
            // Create a non-nullable version of the target type for the 'as' expression
            var baseType = new TypeAnnotation
            {
                Name = coercion.TargetType.Name,
                TypeArguments = coercion.TargetType.TypeArguments,
                IsNullable = false
            };
            var baseTypeSyntax = _typeMapper.MapType(baseType);
            var nullableTypeSyntax = _typeMapper.MapType(coercion.TargetType);

            // Check if this is a value type (primitives are value types except string/object)
            var primitiveInfo = PrimitiveCatalog.GetByName(coercion.TargetType.Name);
            bool isValueType = primitiveInfo != null &&
                               primitiveInfo.ClrType != typeof(string) &&
                               primitiveInfo.ClrType != typeof(object) &&
                               primitiveInfo.ClrType != typeof(void);

            if (isValueType)
            {
                // For value types: value is T _temp ? (T?)_temp : null
                // Generate unique temp variable name
                var tempName = $"__coerce_temp_{_tempVarCounter++}";

                // value is T tempName ? (T?)tempName : (T?)null
                return ConditionalExpression(
                    IsPatternExpression(
                        value,
                        DeclarationPattern(
                            baseTypeSyntax,
                            SingleVariableDesignation(Identifier(tempName)))),
                    CastExpression(nullableTypeSyntax, IdentifierName(tempName)),
                    CastExpression(nullableTypeSyntax, LiteralExpression(SyntaxKind.NullLiteralExpression)));
            }
            else
            {
                // For reference types: value as T
                return BinaryExpression(
                    SyntaxKind.AsExpression,
                    value,
                    baseTypeSyntax);
            }
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
                parts.Add(Interpolation(GenerateExpression(part.Expression)));
            }
        }

        return InterpolatedStringExpression(Token(SyntaxKind.InterpolatedStringStartToken))
            .WithContents(List(parts));
    }

    /// <summary>
    /// Gets the fully qualified C# type name for a type, handling cross-file references.
    /// </summary>
    private string GetFullyQualifiedTypeName(TypeSymbol typeSymbol, string sharpyTypeName)
    {
        // Check if type is from a different file (cross-file reference)
        if (!string.IsNullOrEmpty(typeSymbol.DefiningFilePath) &&
            !string.IsNullOrEmpty(_context.SourceFilePath) &&
            !string.Equals(typeSymbol.DefiningFilePath, _context.SourceFilePath, StringComparison.OrdinalIgnoreCase))
        {
            // Type from another file - use fully qualified name
            var moduleNamespace = GetModuleNameFromFilePath(typeSymbol.DefiningFilePath);
            var typeName = NameMangler.ToPascalCase(sharpyTypeName);

            if (!string.IsNullOrEmpty(_context.ProjectNamespace))
            {
                return $"{_context.ProjectNamespace}.{moduleNamespace}.Exports.{typeName}";
            }
            return $"{moduleNamespace}.Exports.{typeName}";
        }

        // Check if type is from an external module (imported via DefiningModule)
        if (!string.IsNullOrEmpty(typeSymbol.DefiningModule))
        {
            var moduleNamespace = ConvertModuleToNamespace(typeSymbol.DefiningModule);
            var typeName = NameMangler.ToPascalCase(sharpyTypeName);

            if (!string.IsNullOrEmpty(_context.ProjectNamespace))
            {
                return $"{_context.ProjectNamespace}.{moduleNamespace}.Exports.{typeName}";
            }
            return $"{moduleNamespace}.Exports.{typeName}";
        }

        // Type is in current file - use simple name
        return NameMangler.ToPascalCase(sharpyTypeName);
    }

    /// <summary>
    /// Derives a module namespace from a file path.
    /// E.g., "/path/to/animal.spy" -> "Animal"
    /// </summary>
    private static string GetModuleNameFromFilePath(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return NameMangler.ToPascalCase(fileName);
    }

    /// <summary>
    /// Converts a module path (e.g., "animal" or "lib.animal") to a C# namespace segment.
    /// </summary>
    private static string ConvertModuleToNamespace(string modulePath)
    {
        var parts = modulePath.Split('.');
        return string.Join(".", parts.Select(p => NameMangler.ToPascalCase(p)));
    }

}
