using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Generates C# code using Roslyn syntax trees
/// </summary>
public class RoslynEmitter
{
    private readonly CodeGenContext _context;
    private readonly TypeMapper _typeMapper;

    public RoslynEmitter(CodeGenContext context)
    {
        _context = context;
        _typeMapper = new TypeMapper(context);
    }

    public CompilationUnitSyntax GenerateCompilationUnit(Module module)
    {
        // Add using directives
        var usings = new[]
        {
            UsingDirective(ParseName("System")),
            UsingDirective(ParseName("Sharpy"))
        };

        // Generate module class wrapper
        var moduleClass = GenerateModuleClass(module);

        // Create namespace
        var namespaceName = ParseName("SharpyGenerated");
        var namespaceDecl = FileScopedNamespaceDeclaration(namespaceName)
            .WithMembers(SingletonList<MemberDeclarationSyntax>(moduleClass));

        return CompilationUnit()
            .WithUsings(List(usings))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(namespaceDecl))
            .NormalizeWhitespace();
    }

    private ClassDeclarationSyntax GenerateModuleClass(Module module)
    {
        var members = module.Body
            .Select(GenerateStatement)
            .OfType<MemberDeclarationSyntax>()
            .ToArray();

        return ClassDeclaration("__Module__")
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword)))
            .WithMembers(List(members));
    }

    private SyntaxNode? GenerateStatement(Statement stmt)
    {
        return stmt switch
        {
            FunctionDef funcDef => GenerateFunctionDeclaration(funcDef),
            ReturnStatement ret => GenerateReturn(ret),
            Assignment assign => GenerateAssignment(assign),
            // Add more statement types...
            _ => null
        };
    }

    private MethodDeclarationSyntax GenerateFunctionDeclaration(FunctionDef func)
    {
        var mangledName = NameMangler.ToPascalCase(func.Name);
        var returnType = PredefinedType(Token(SyntaxKind.VoidKeyword)); // TODO: Infer return type

        // Default to public static for now
        var modifiers = TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword));

        var parameters = func.Parameters
            .Select(p => Parameter(Identifier(NameMangler.ToCamelCase(p.Name)))
                .WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))))
            .ToArray();

        var body = Block(func.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        return MethodDeclaration(returnType, mangledName)
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);
    }

    private StatementSyntax? GenerateBodyStatement(Statement stmt)
    {
        return stmt switch
        {
            ReturnStatement ret => GenerateReturn(ret),
            Assignment assign => GenerateAssignment(assign),
            VariableDeclaration varDecl => GenerateVariableDeclaration(varDecl),
            ExpressionStatement exprStmt => ExpressionStatement(GenerateExpression(exprStmt.Expression)),
            
            // Simple statements
            PassStatement => EmptyStatement(),
            Sharpy.Compiler.Parser.Ast.BreakStatement => BreakStatement(),
            Sharpy.Compiler.Parser.Ast.ContinueStatement => ContinueStatement(),
            AssertStatement assert => GenerateAssert(assert),
            RaiseStatement raise => GenerateRaise(raise),
            
            // Control flow
            IfStatement ifStmt => GenerateIf(ifStmt),
            WhileStatement whileStmt => GenerateWhile(whileStmt),
            ForStatement forStmt => GenerateFor(forStmt),
            
            // Exception handling
            TryStatement tryStmt => GenerateTry(tryStmt),
            
            _ => null
        };
    }

    private ReturnStatementSyntax GenerateReturn(ReturnStatement ret)
    {
        if (ret.Value != null)
        {
            return ReturnStatement(GenerateExpression(ret.Value));
        }
        return ReturnStatement();
    }

    private StatementSyntax GenerateAssignment(Assignment assign)
    {
        var value = GenerateExpression(assign.Value);
        
        // Handle different assignment operators
        var kind = assign.Operator switch
        {
            AssignmentOperator.Assign => SyntaxKind.SimpleAssignmentExpression,
            AssignmentOperator.PlusAssign => SyntaxKind.AddAssignmentExpression,
            AssignmentOperator.MinusAssign => SyntaxKind.SubtractAssignmentExpression,
            AssignmentOperator.StarAssign => SyntaxKind.MultiplyAssignmentExpression,
            AssignmentOperator.SlashAssign => SyntaxKind.DivideAssignmentExpression,
            AssignmentOperator.PercentAssign => SyntaxKind.ModuloAssignmentExpression,
            AssignmentOperator.AndAssign => SyntaxKind.AndAssignmentExpression,
            AssignmentOperator.OrAssign => SyntaxKind.OrAssignmentExpression,
            AssignmentOperator.XorAssign => SyntaxKind.ExclusiveOrAssignmentExpression,
            AssignmentOperator.LeftShiftAssign => SyntaxKind.LeftShiftAssignmentExpression,
            AssignmentOperator.RightShiftAssign => SyntaxKind.RightShiftAssignmentExpression,
            AssignmentOperator.DoubleSlashAssign => SyntaxKind.SimpleAssignmentExpression, // Special case
            AssignmentOperator.PowerAssign => SyntaxKind.SimpleAssignmentExpression, // Special case
            _ => throw new NotImplementedException($"Assignment operator {assign.Operator} not implemented")
        };

        // Special handling for //= and **=
        if (assign.Operator == AssignmentOperator.DoubleSlashAssign)
        {
            // target //= value → target = (int)(target / value)
            var target = GenerateExpression(assign.Target);
            var divideExpr = BinaryExpression(SyntaxKind.DivideExpression, target, value);
            var castExpr = CastExpression(PredefinedType(Token(SyntaxKind.IntKeyword)), divideExpr);
            return ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, target, castExpr));
        }
        else if (assign.Operator == AssignmentOperator.PowerAssign)
        {
            // target **= value → target = Math.Pow(target, value)
            var target = GenerateExpression(assign.Target);
            var powCall = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Math"),
                    IdentifierName("Pow")))
                .AddArgumentListArguments(Argument(target), Argument(value));
            return ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, target, powCall));
        }
        
        // For simple assignment to identifier, might be first declaration
        if (assign.Operator == AssignmentOperator.Assign && assign.Target is Identifier name)
        {
            var varName = NameMangler.ToCamelCase(name.Name);
            
            // Check if this is the first assignment (declaration)
            // For now, always use var declaration for simple assignments
            // TODO: Track variable scope to know if re-assignment
            var declaration = VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(varName))
                        .WithInitializer(EqualsValueClause(value))));

            return LocalDeclarationStatement(declaration);
        }

        // For all other cases (re-assignment, augmented assignment, complex targets)
        var targetExpr = GenerateExpression(assign.Target);
        return ExpressionStatement(AssignmentExpression(kind, targetExpr, value));
    }

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

            // Primary expressions
            Identifier name => IdentifierName(NameMangler.ToCamelCase(name.Name)),
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
            TypeCheck check => GenerateTypeCheck(check),
            Parenthesized paren => GenerateExpression(paren.Expression),

            // F-strings
            FStringLiteral fstring => GenerateFString(fstring),

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
        if (call.Function is Identifier funcName)
        {
            var name = _context.IsBuiltinFunction(funcName.Name)
                ? $"Sharpy.Exports.{NameMangler.ToPascalCase(funcName.Name)}"
                : NameMangler.ToPascalCase(funcName.Name);

            var args = call.Arguments.Select(GenerateExpression).ToArray();

            return InvocationExpression(ParseName(name))
                .WithArgumentList(ArgumentList(SeparatedList(args.Select(Argument))));
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
                // x ** y → Math.Pow(x, y)
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("Math"),
                        IdentifierName("Pow")))
                    .AddArgumentListArguments(
                        Argument(left),
                        Argument(right));

            case BinaryOperator.FloorDivide:
                // x // y → (int)(x / y) for integers
                // For now, cast to int (TODO: handle different numeric types)
                return CastExpression(
                    PredefinedType(Token(SyntaxKind.IntKeyword)),
                    BinaryExpression(SyntaxKind.DivideExpression, left, right));
        }

        // Standard binary operators
        var kind = binOp.Operator switch
        {
            // Arithmetic
            BinaryOperator.Add => SyntaxKind.AddExpression,
            BinaryOperator.Subtract => SyntaxKind.SubtractExpression,
            BinaryOperator.Multiply => SyntaxKind.MultiplyExpression,
            BinaryOperator.Divide => SyntaxKind.DivideExpression,
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

            // Membership and identity operators need special handling
            BinaryOperator.In => throw new NotImplementedException("'in' operator requires runtime support"),
            BinaryOperator.NotIn => throw new NotImplementedException("'not in' operator requires runtime support"),
            BinaryOperator.Is => throw new NotImplementedException("'is' operator requires type check support"),
            BinaryOperator.IsNot => throw new NotImplementedException("'is not' operator requires type check support"),

            _ => throw new NotImplementedException($"Binary operator not implemented: {binOp.Operator}")
        };

        return BinaryExpression(kind, left, right);
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
        // Ellipsis in v0.5 is used as a placeholder, similar to pass
        // We'll generate a comment or throw NotImplementedException
        // For now, generate: throw new NotImplementedException()
        return ThrowExpression(
            ObjectCreationExpression(ParseTypeName("System.NotImplementedException"))
                .WithArgumentList(ArgumentList()));
    }

    private ExpressionSyntax GenerateListLiteral(ListLiteral list)
    {
        // new Sharpy.List<T> { elem1, elem2, elem3 }
        var elementType = _typeMapper.InferElementType(list.Elements);
        var elements = list.Elements.Select(GenerateExpression);

        var listType = GenericName("Sharpy.List")
            .AddTypeArgumentListArguments(elementType);

        return ObjectCreationExpression(listType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateDictLiteral(DictLiteral dict)
    {
        // new Sharpy.Dict<K,V> { { key1, value1 }, { key2, value2 } }
        var keyType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Key));
        var valueType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Value));

        var initializers = dict.Entries.Select(entry =>
            InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
                SeparatedList(new[]
                {
                    GenerateExpression(entry.Key),
                    GenerateExpression(entry.Value)
                })));

        var dictType = GenericName("Sharpy.Dict")
            .AddTypeArgumentListArguments(keyType, valueType);

        return ObjectCreationExpression(dictType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList<ExpressionSyntax>(initializers)));
    }

    private ExpressionSyntax GenerateSetLiteral(SetLiteral set)
    {
        // new Sharpy.Set<T> { elem1, elem2, elem3 }
        var elementType = _typeMapper.InferElementType(set.Elements);
        var elements = set.Elements.Select(GenerateExpression);

        var setType = GenericName("Sharpy.Set")
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

    private ExpressionSyntax GenerateMemberAccess(MemberAccess memberAccess)
    {
        var obj = GenerateExpression(memberAccess.Object);
        var member = IdentifierName(memberAccess.Member);

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
        // Translates to: Sharpy.Runtime.Slice(arr, start, stop, step)
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
                IdentifierName("Sharpy.Runtime"),
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
        // For simplicity in v0.5, we'll allow re-evaluation
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

    private StatementSyntax GenerateVariableDeclaration(VariableDeclaration varDecl)
    {
        var varName = NameMangler.ToCamelCase(varDecl.Name);
        var varType = varDecl.Type != null ? _typeMapper.MapType(varDecl.Type) : IdentifierName("var");
        
        if (varDecl.InitialValue != null)
        {
            var value = GenerateExpression(varDecl.InitialValue);
            var declaration = VariableDeclaration(varType)
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(varName))
                        .WithInitializer(EqualsValueClause(value))));

            if (varDecl.IsConst)
            {
                return LocalDeclarationStatement(declaration)
                    .WithModifiers(TokenList(Token(SyntaxKind.ConstKeyword)));
            }
            return LocalDeclarationStatement(declaration);
        }
        else
        {
            // Declaration without initialization
            var declaration = VariableDeclaration(varType)
                .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier(varName))));
            return LocalDeclarationStatement(declaration);
        }
    }

    private StatementSyntax GenerateAssert(AssertStatement assert)
    {
        // assert condition, message → if (!condition) throw new AssertionException(message)
        var condition = GenerateExpression(assert.Test);
        var negatedCondition = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, condition);
        
        ExpressionSyntax throwExpr;
        if (assert.Message != null)
        {
            var message = GenerateExpression(assert.Message);
            throwExpr = ObjectCreationExpression(ParseTypeName("System.Diagnostics.Debug"))
                .WithArgumentList(ArgumentList());
            
            // Use Debug.Assert in C#
            var assertCall = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("System.Diagnostics.Debug"),
                    IdentifierName("Assert")))
                .AddArgumentListArguments(Argument(condition), Argument(message));
            
            return ExpressionStatement(assertCall);
        }
        else
        {
            var assertCall = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("System.Diagnostics.Debug"),
                    IdentifierName("Assert")))
                .AddArgumentListArguments(Argument(condition));
            
            return ExpressionStatement(assertCall);
        }
    }

    private StatementSyntax GenerateRaise(RaiseStatement raise)
    {
        if (raise.Exception != null)
        {
            var exception = GenerateExpression(raise.Exception);
            return ThrowStatement(exception);
        }
        else
        {
            // Re-throw current exception
            return ThrowStatement();
        }
    }

    private StatementSyntax GenerateIf(IfStatement ifStmt)
    {
        var condition = GenerateExpression(ifStmt.Test);
        var thenBlock = Block(ifStmt.ThenBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        
        IfStatementSyntax result = IfStatement(condition, thenBlock);
        
        // Handle elif clauses by chaining else-if
        foreach (var elifClause in ifStmt.ElifClauses)
        {
            var elifCondition = GenerateExpression(elifClause.Test);
            var elifBlock = Block(elifClause.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            var elifStmt = IfStatement(elifCondition, elifBlock);
            
            result = result.WithElse(ElseClause(elifStmt));
        }
        
        // Handle else clause
        if (ifStmt.ElseBody.Count > 0)
        {
            var elseBlock = Block(ifStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            
            if (ifStmt.ElifClauses.Count > 0)
            {
                // Need to find the last elif and attach else to it
                // This is complex, so we'll rebuild the chain properly
                result = BuildIfElifElseChain(ifStmt);
            }
            else
            {
                result = result.WithElse(ElseClause(elseBlock));
            }
        }
        
        return result;
    }

    private IfStatementSyntax BuildIfElifElseChain(IfStatement ifStmt)
    {
        var condition = GenerateExpression(ifStmt.Test);
        var thenBlock = Block(ifStmt.ThenBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        
        if (ifStmt.ElifClauses.Count == 0)
        {
            var result = IfStatement(condition, thenBlock);
            if (ifStmt.ElseBody.Count > 0)
            {
                var elseBlock = Block(ifStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
                result = result.WithElse(ElseClause(elseBlock));
            }
            return result;
        }
        
        // Build the chain from the last elif backwards
        StatementSyntax currentElse;
        if (ifStmt.ElseBody.Count > 0)
        {
            currentElse = Block(ifStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        }
        else
        {
            currentElse = null!;
        }
        
        // Build elif chain from last to first
        for (int i = ifStmt.ElifClauses.Count - 1; i >= 0; i--)
        {
            var elifClause = ifStmt.ElifClauses[i];
            var elifCondition = GenerateExpression(elifClause.Test);
            var elifBlock = Block(elifClause.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            
            var elifStmt = IfStatement(elifCondition, elifBlock);
            if (currentElse != null)
            {
                elifStmt = elifStmt.WithElse(ElseClause(currentElse));
            }
            currentElse = elifStmt;
        }
        
        var mainIf = IfStatement(condition, thenBlock);
        if (currentElse != null)
        {
            mainIf = mainIf.WithElse(ElseClause(currentElse));
        }
        
        return mainIf;
    }

    private StatementSyntax GenerateWhile(WhileStatement whileStmt)
    {
        var condition = GenerateExpression(whileStmt.Test);
        var body = Block(whileStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        
        return WhileStatement(condition, body);
    }

    private StatementSyntax GenerateFor(ForStatement forStmt)
    {
        // Python-style for loop: for item in iterable:
        // Translates to: foreach (var item in iterable)
        
        var iterator = GenerateExpression(forStmt.Iterator);
        var body = Block(forStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        
        // Handle different target types
        if (forStmt.Target is Identifier id)
        {
            var varName = NameMangler.ToCamelCase(id.Name);
            return ForEachStatement(
                IdentifierName("var"),
                Identifier(varName),
                iterator,
                body);
        }
        else if (forStmt.Target is TupleLiteral tuple)
        {
            // Tuple unpacking: for (x, y) in items:
            // For now, use a temporary variable and destructure in the loop body
            var tempVar = "_item";
            var loopBody = new List<StatementSyntax>();
            
            // Add destructuring statements
            for (int i = 0; i < tuple.Elements.Count; i++)
            {
                if (tuple.Elements[i] is Identifier elemId)
                {
                    var elemName = NameMangler.ToCamelCase(elemId.Name);
                    var itemAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(tempVar),
                        IdentifierName($"Item{i + 1}"));
                    
                    var decl = VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(elemName))
                                .WithInitializer(EqualsValueClause(itemAccess))));
                    loopBody.Add(LocalDeclarationStatement(decl));
                }
            }
            
            // Add original loop body
            loopBody.AddRange(forStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            
            return ForEachStatement(
                IdentifierName("var"),
                Identifier(tempVar),
                iterator,
                Block(loopBody));
        }
        else
        {
            throw new NotImplementedException($"For loop target type {forStmt.Target.GetType().Name} not supported");
        }
    }

    private StatementSyntax GenerateTry(TryStatement tryStmt)
    {
        var tryBlock = Block(tryStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        
        var catches = new List<CatchClauseSyntax>();
        foreach (var handler in tryStmt.Handlers)
        {
            CatchClauseSyntax catchClause;
            
            if (handler.ExceptionType != null)
            {
                var exceptionType = _typeMapper.MapType(handler.ExceptionType);
                
                if (handler.Name != null)
                {
                    var varName = NameMangler.ToCamelCase(handler.Name);
                    var catchDecl = CatchDeclaration(exceptionType)
                        .WithIdentifier(Identifier(varName));
                    catchClause = CatchClause()
                        .WithDeclaration(catchDecl);
                }
                else
                {
                    var catchDecl = CatchDeclaration(exceptionType);
                    catchClause = CatchClause()
                        .WithDeclaration(catchDecl);
                }
            }
            else
            {
                // Catch all exceptions
                catchClause = CatchClause();
            }
            
            var catchBody = Block(handler.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            catchClause = catchClause.WithBlock(catchBody);
            catches.Add(catchClause);
        }
        
        var result = TryStatement(tryBlock, List(catches), null);
        
        // Add finally block if present
        if (tryStmt.FinallyBody.Count > 0)
        {
            var finallyBlock = Block(tryStmt.FinallyBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            result = result.WithFinally(FinallyClause(finallyBlock));
        }
        
        return result;
    }
}
