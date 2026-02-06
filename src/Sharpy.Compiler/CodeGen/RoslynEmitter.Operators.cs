using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Operator overloads and utility methods
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Determines if a dunder method should generate a C# method (for overrides or special methods)
    /// Most dunder methods should NOT generate methods to avoid conflicts with user-defined methods
    /// </summary>
    private static bool ShouldGenerateDunderMethod(string dunderName)
    {
        // __init__ is explicitly checked here for clarity, even though it IS in ProtocolRegistry.
        // This makes the special constructor handling obvious to readers.
        if (dunderName == DunderNames.Init)
            return true;

        // Protocol dunders that map to .NET methods should be generated
        return ProtocolRegistry.IsProtocolDunder(dunderName);
    }

    /// <summary>
    /// Try to generate an operator overload from a dunder method
    /// </summary>
    private MemberDeclarationSyntax? TryGenerateOperatorOverload(FunctionDef funcDef, string className)
    {
        return funcDef.Name switch
        {
            // Arithmetic operators (binary)
            DunderNames.Add => GenerateBinaryOperator(funcDef, className, SyntaxKind.PlusToken),
            DunderNames.Sub => GenerateBinaryOperator(funcDef, className, SyntaxKind.MinusToken),
            DunderNames.Mul => GenerateBinaryOperator(funcDef, className, SyntaxKind.AsteriskToken),
            DunderNames.TrueDiv => GenerateBinaryOperator(funcDef, className, SyntaxKind.SlashToken),
            DunderNames.Mod => GenerateBinaryOperator(funcDef, className, SyntaxKind.PercentToken),

            // Bitwise operators (binary)
            DunderNames.And => GenerateBinaryOperator(funcDef, className, SyntaxKind.AmpersandToken),
            DunderNames.Or => GenerateBinaryOperator(funcDef, className, SyntaxKind.BarToken),
            DunderNames.Xor => GenerateBinaryOperator(funcDef, className, SyntaxKind.CaretToken),
            DunderNames.LShift => GenerateBinaryOperator(funcDef, className, SyntaxKind.LessThanLessThanToken),
            DunderNames.RShift => GenerateBinaryOperator(funcDef, className, SyntaxKind.GreaterThanGreaterThanToken),

            // Comparison operators (binary)
            DunderNames.Eq => GenerateComparisonOperator(funcDef, className, SyntaxKind.EqualsEqualsToken),
            DunderNames.Ne => GenerateComparisonOperator(funcDef, className, SyntaxKind.ExclamationEqualsToken),
            DunderNames.Lt => GenerateComparisonOperator(funcDef, className, SyntaxKind.LessThanToken),
            DunderNames.Le => GenerateComparisonOperator(funcDef, className, SyntaxKind.LessThanEqualsToken),
            DunderNames.Gt => GenerateComparisonOperator(funcDef, className, SyntaxKind.GreaterThanToken),
            DunderNames.Ge => GenerateComparisonOperator(funcDef, className, SyntaxKind.GreaterThanEqualsToken),

            // Unary operators
            DunderNames.Neg => GenerateUnaryOperator(funcDef, className, SyntaxKind.MinusToken),
            DunderNames.Pos => GenerateUnaryOperator(funcDef, className, SyntaxKind.PlusToken),
            DunderNames.Invert => GenerateUnaryOperator(funcDef, className, SyntaxKind.TildeToken),

            // Not supported as operators (handled as methods)
            DunderNames.Pow => null,     // No ** operator in C#, use Math.Pow
            DunderNames.GetItem => null, // Requires indexer syntax, not operator
            DunderNames.SetItem => null, // Requires indexer syntax, not operator

            _ => null
        };
    }

    /// <summary>
    /// Generate a binary operator overload (e.g., operator +, operator -, etc.)
    /// </summary>
    private OperatorDeclarationSyntax GenerateBinaryOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        // Binary operators should have 2 parameters: self and other
        // We skip 'self' and use the other parameter
        var otherParam = funcDef.Parameters
            .Where(p => !string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (otherParam == null)
        {
            throw new InvalidOperationException($"Binary operator {funcDef.Name} must have at least 2 parameters");
        }

        // Determine return type - default to class type if not specified
        var returnType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : IdentifierName(className);

        // Generate parameter for the operator
        var param1 = Parameter(Identifier("left"))
            .WithType(IdentifierName(className));

        var param2Type = otherParam.Type != null
            ? _typeMapper.MapType(otherParam.Type)
            : IdentifierName(className);

        var param2 = Parameter(Identifier("right"))
            .WithType(param2Type);

        // Generate body - call the actual dunder method on left operand
        // Use the transformed dunder name (e.g., __add__ -> Add)
        var methodName = NameMangler.Transform(funcDef.Name, NameContext.Method);
        var invocation = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("left"),
                IdentifierName(methodName)))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("right")))));

        var body = Block(ReturnStatement(invocation));

        return OperatorDeclaration(returnType, Token(operatorToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[] { param1, param2 })))
            .WithBody(body);
    }

    /// <summary>
    /// Generate a comparison operator overload (==, !=, <, >, <=, >=)
    /// </summary>
    private OperatorDeclarationSyntax GenerateComparisonOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        // Similar to binary operators but always returns bool
        var otherParam = funcDef.Parameters
            .Where(p => !string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (otherParam == null)
        {
            throw new InvalidOperationException($"Comparison operator {funcDef.Name} must have at least 2 parameters");
        }

        // Comparison operators always return bool
        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        // Generate parameters
        var param1 = Parameter(Identifier("left"))
            .WithType(IdentifierName(className));

        var param2Type = otherParam.Type != null
            ? _typeMapper.MapType(otherParam.Type)
            : IdentifierName(className);

        var param2 = Parameter(Identifier("right"))
            .WithType(param2Type);

        // Generate body - call the actual dunder method on left operand
        var methodName = NameMangler.Transform(funcDef.Name, NameContext.Method);
        var invocation = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("left"),
                IdentifierName(methodName)))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("right")))));

        var body = Block(ReturnStatement(invocation));

        return OperatorDeclaration(returnType, Token(operatorToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[] { param1, param2 })))
            .WithBody(body);
    }

    /// <summary>
    /// Generate a unary operator overload (-, +, ~)
    /// </summary>
    private OperatorDeclarationSyntax GenerateUnaryOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        // Unary operators should have only 1 parameter: self

        // Determine return type - default to class type if not specified
        var returnType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : IdentifierName(className);

        // Generate parameter for the operator
        var param = Parameter(Identifier("value"))
            .WithType(IdentifierName(className));

        // Generate body - call the actual dunder method on the operand
        var methodName = NameMangler.Transform(funcDef.Name, NameContext.Method);
        var invocation = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("value"),
                IdentifierName(methodName)))
            .WithArgumentList(ArgumentList());

        var body = Block(ReturnStatement(invocation));

        return OperatorDeclaration(returnType, Token(operatorToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(param)))
            .WithBody(body);
    }

    /// <summary>
    /// Generate complementary operator == when only __ne__ is defined
    /// </summary>
    private OperatorDeclarationSyntax GenerateComplementaryEqualsOperator(string className)
    {
        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        var param1 = Parameter(Identifier("left"))
            .WithType(IdentifierName(className));
        var param2 = Parameter(Identifier("right"))
            .WithType(IdentifierName(className));

        // operator == returns !(left != right)
        var body = Block(ReturnStatement(
            PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                BinaryExpression(
                    SyntaxKind.NotEqualsExpression,
                    IdentifierName("left"),
                    IdentifierName("right")))));

        return OperatorDeclaration(returnType, Token(SyntaxKind.EqualsEqualsToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[] { param1, param2 })))
            .WithBody(body);
    }

    /// <summary>
    /// Generate complementary operator != when only __eq__ is defined
    /// </summary>
    private OperatorDeclarationSyntax GenerateComplementaryNotEqualsOperator(string className)
    {
        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        var param1 = Parameter(Identifier("left"))
            .WithType(IdentifierName(className));
        var param2 = Parameter(Identifier("right"))
            .WithType(IdentifierName(className));

        // operator != returns !(left == right)
        var body = Block(ReturnStatement(
            PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                BinaryExpression(
                    SyntaxKind.EqualsExpression,
                    IdentifierName("left"),
                    IdentifierName("right")))));

        return OperatorDeclaration(returnType, Token(SyntaxKind.ExclamationEqualsToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[] { param1, param2 })))
            .WithBody(body);
    }

    /// <summary>
    /// Generate a try expression: try expr or try[ExceptionType] expr
    /// Wraps the expression in Result[T, E] using Result.Try().
    /// Default: Result.Try&lt;T&gt;(() => operand) → Result&lt;T, Exception&gt;
    /// Typed: Result.Try&lt;T, E&gt;(() => operand) → Result&lt;T, E&gt;
    /// </summary>
    private ExpressionSyntax GenerateTryExpression(TryExpression tryExpr)
    {
        // Generate the operand expression wrapped in a lambda
        var operandExpr = GenerateExpression(tryExpr.Operand);
        var lambdaExpr = ParenthesizedLambdaExpression()
            .WithExpressionBody(operandExpr);

        // Get the ResultType from semantic info to extract T and E
        var resultType = GetExpressionSemanticType(tryExpr) as Semantic.ResultType;

        if (tryExpr.ExceptionType != null || tryExpr.Operand is TypeCoercion)
        {
            // Typed version: Result.Try<T, E>(() => operand)
            // Used for try[ValueError] expr and try x to Cat
            var okTypeSyntax = resultType != null
                ? _typeMapper.MapSemanticType(resultType.OkType)
                : (TypeSyntax)PredefinedType(Token(SyntaxKind.ObjectKeyword));
            var errTypeSyntax = resultType != null
                ? _typeMapper.MapSemanticType(resultType.ErrorType)
                : (TypeSyntax)IdentifierName("Exception");

            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ParseName("global::Sharpy.Result"),
                    GenericName("Try")
                        .WithTypeArgumentList(TypeArgumentList(
                            SeparatedList(new[] { okTypeSyntax, errTypeSyntax })))))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(lambdaExpr))));
        }
        else
        {
            // Default version: Result.Try<T>(() => operand) → Result<T, Exception>
            // C# infers T from the lambda when only one type parameter is provided
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ParseName("global::Sharpy.Result"),
                    IdentifierName("Try")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(lambdaExpr))));
        }
    }

    /// <summary>
    /// Generate a maybe expression: maybe expr
    /// Converts a C# nullable (T | None) to Optional[T].
    /// Since both NullableType and OptionalType map to C# T? in code generation,
    /// this is a semantic pass-through — the value is already in the correct C# form.
    /// </summary>
    private ExpressionSyntax GenerateMaybeExpression(MaybeExpression maybeExpr)
    {
        // Both NullableType (T | None) and OptionalType (T?) map to C# T?,
        // so the conversion is a no-op at the C# level. The type checker has
        // already validated that the operand is a NullableType and the result
        // is an OptionalType — the semantic distinction is enforced there.
        return GenerateExpression(maybeExpr.Operand);
    }

    /// <summary>
    /// Generate a unique temporary variable name
    /// </summary>
    private string GenerateTempVarName(string prefix)
    {
        return $"__{prefix}_{_tempVarCounter++}";
    }

    /// <summary>
    /// Transform loop body statements for else clause support.
    /// Wraps break statements with flag assignment: { flag = false; break; }
    /// </summary>
    private ImmutableArray<Statement> TransformLoopBodyForElse(IReadOnlyList<Statement> body, string flagName)
    {
        var builder = ImmutableArray.CreateBuilder<Statement>(body.Count);
        foreach (var stmt in body)
        {
            builder.Add(TransformStatementForLoopElse(stmt, flagName));
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// Transform a single statement for loop else support.
    /// Recursively handles nested structures.
    /// </summary>
    private Statement TransformStatementForLoopElse(Statement stmt, string flagName)
    {
        return stmt switch
        {
            // Transform break statements to set flag before breaking
            BreakStatement breakStmt => new BreakWithFlagStatement
            {
                FlagName = flagName,
                LineStart = breakStmt.LineStart,
                ColumnStart = breakStmt.ColumnStart,
                LineEnd = breakStmt.LineEnd,
                ColumnEnd = breakStmt.ColumnEnd
            },

            // Recursively transform if statements
            IfStatement ifStmt => ifStmt with
            {
                ThenBody = TransformLoopBodyForElse(ifStmt.ThenBody, flagName),
                ElifClauses = ifStmt.ElifClauses.Select(e => e with
                {
                    Body = TransformLoopBodyForElse(e.Body, flagName)
                }).ToImmutableArray(),
                ElseBody = TransformLoopBodyForElse(ifStmt.ElseBody, flagName)
            },

            // Don't transform nested loops - their break statements apply to their own loop
            WhileStatement _ => stmt,
            ForStatement _ => stmt,

            // All other statements pass through unchanged
            _ => stmt
        };
    }

    /// <summary>
    /// Checks if an expression evaluates to a floating-point type.
    /// Used to determine floor division semantics.
    /// Consults SemanticInfo for resolved types when available (variables, function calls, etc.).
    /// Falls back to AST-based heuristic for literals and compound expressions.
    /// </summary>
    private bool IsFloatExpression(Expression expr)
    {
        // First, try to resolve via SemanticInfo (handles variables, function calls, etc.)
        var semanticType = GetExpressionSemanticType(expr);
        if (semanticType != null)
        {
            return semanticType == SemanticType.Float
                || semanticType == SemanticType.Double
                || semanticType == SemanticType.Float32;
        }

        // Fallback to AST-based heuristic for cases where SemanticInfo is not available
        return expr switch
        {
            FloatLiteral => true,
            UnaryOp unary => IsFloatExpression(unary.Operand),
            BinaryOp binOp => binOp.Operator switch
            {
                // Division always produces float
                BinaryOperator.Divide => true,
                // Power: float if either operand is float, otherwise integer (cast to long)
                BinaryOperator.Power => IsFloatExpression(binOp.Left) || IsFloatExpression(binOp.Right),
                // Floor division depends on operands
                BinaryOperator.FloorDivide => IsFloatExpression(binOp.Left) || IsFloatExpression(binOp.Right),
                // Other operators: float if either operand is float
                _ => IsFloatExpression(binOp.Left) || IsFloatExpression(binOp.Right)
            },
            Parenthesized paren => IsFloatExpression(paren.Expression),
            _ => false
        };
    }

    /// <summary>
    /// Generates floor division expression with correct Python semantics.
    /// Floors toward negative infinity (not truncation toward zero).
    /// - Integer operands: (int)Math.Floor((double)a / b) → result is int32 (pragmatic for .NET)
    /// - Float operands: Math.Floor((double)(a / b)) → result is double (cast to avoid CS0121 ambiguity)
    /// Note: Spec says integer floor division should return int64, but we return int32
    /// for .NET compatibility with most use cases (augmented assignment, common variables).
    /// </summary>
    private ExpressionSyntax GenerateFloorDivision(ExpressionSyntax left, ExpressionSyntax right, bool hasFloatOperand)
    {
        // System.Math.Floor((double)(left / right)) for both cases
        // Note: We use fully qualified System.Math to avoid conflicts with Sharpy.Math namespace
        // Note: We always cast to double to avoid CS0121 ambiguity between Math.Floor(double) and Math.Floor(decimal)
        var divisionExpr = BinaryExpression(SyntaxKind.DivideExpression,
            hasFloatOperand ? left : CastExpression(PredefinedType(Token(SyntaxKind.DoubleKeyword)), ParenthesizedExpression(left)),
            right);

        // Cast division result to double to resolve Math.Floor overload ambiguity
        var castToDouble = CastExpression(
            PredefinedType(Token(SyntaxKind.DoubleKeyword)),
            ParenthesizedExpression(divisionExpr));

        var floorCall = InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("System"),
                    IdentifierName("Math")),
                IdentifierName("Floor")))
            .AddArgumentListArguments(Argument(castToDouble));

        // For integer operands, cast to int (pragmatic .NET-first approach);
        // for float operands, return as-is (double from Math.Floor)
        return hasFloatOperand
            ? floorCall
            : CastExpression(PredefinedType(Token(SyntaxKind.IntKeyword)), floorCall);
    }

    /// <summary>
    /// Checks if an expression evaluates to an enum type.
    /// Used to determine whether .value access should be translated to an int cast.
    /// </summary>
    private bool IsEnumTypeExpression(Expression expr)
    {
        if (expr is Identifier id)
        {
            var symbol = _context.LookupSymbol(id.Name);
            if (symbol is VariableSymbol varSymbol &&
                GetVariableType(varSymbol) is Semantic.UserDefinedType udt &&
                udt.Symbol?.TypeKind == Semantic.TypeKind.Enum)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Collects all identifier names referenced in an expression.
    /// Used for dependency analysis to determine if a variable declaration
    /// should be a module-level field or a local variable in Main.
    /// </summary>
    private void CollectReferencedIdentifiers(Expression? expr, HashSet<string> identifiers)
    {
        if (expr == null)
            return;

        switch (expr)
        {
            case Identifier id:
                identifiers.Add(id.Name);
                break;
            case FunctionCall call:
                CollectReferencedIdentifiers(call.Function, identifiers);
                foreach (var arg in call.Arguments)
                    CollectReferencedIdentifiers(arg, identifiers);
                foreach (var kwarg in call.KeywordArguments)
                    CollectReferencedIdentifiers(kwarg.Value, identifiers);
                break;
            case MemberAccess ma:
                CollectReferencedIdentifiers(ma.Object, identifiers);
                break;
            case IndexAccess ia:
                CollectReferencedIdentifiers(ia.Object, identifiers);
                CollectReferencedIdentifiers(ia.Index, identifiers);
                break;
            case SliceAccess sa:
                CollectReferencedIdentifiers(sa.Object, identifiers);
                CollectReferencedIdentifiers(sa.Start, identifiers);
                CollectReferencedIdentifiers(sa.Stop, identifiers);
                CollectReferencedIdentifiers(sa.Step, identifiers);
                break;
            case BinaryOp binOp:
                CollectReferencedIdentifiers(binOp.Left, identifiers);
                CollectReferencedIdentifiers(binOp.Right, identifiers);
                break;
            case UnaryOp unOp:
                CollectReferencedIdentifiers(unOp.Operand, identifiers);
                break;
            case ListLiteral list:
                foreach (var elem in list.Elements)
                    CollectReferencedIdentifiers(elem, identifiers);
                break;
            case DictLiteral dict:
                foreach (var entry in dict.Entries)
                {
                    CollectReferencedIdentifiers(entry.Key, identifiers);
                    CollectReferencedIdentifiers(entry.Value, identifiers);
                }
                break;
            case SetLiteral set:
                foreach (var elem in set.Elements)
                    CollectReferencedIdentifiers(elem, identifiers);
                break;
            case TupleLiteral tuple:
                foreach (var elem in tuple.Elements)
                    CollectReferencedIdentifiers(elem, identifiers);
                break;
            case LambdaExpression lambda:
                // Lambda body may reference outer scope variables
                CollectReferencedIdentifiers(lambda.Body, identifiers);
                break;
            case ConditionalExpression cond:
                CollectReferencedIdentifiers(cond.Test, identifiers);
                CollectReferencedIdentifiers(cond.ThenValue, identifiers);
                CollectReferencedIdentifiers(cond.ElseValue, identifiers);
                break;
            case ComparisonChain chain:
                foreach (var operand in chain.Operands)
                    CollectReferencedIdentifiers(operand, identifiers);
                break;
            case FStringLiteral fstr:
                foreach (var part in fstr.Parts)
                    CollectReferencedIdentifiers(part.Expression, identifiers);
                break;
            case ListComprehension comp:
                CollectReferencedIdentifiers(comp.Element, identifiers);
                foreach (var clause in comp.Clauses)
                {
                    if (clause is ForClause fc)
                        CollectReferencedIdentifiers(fc.Iterator, identifiers);
                    else if (clause is IfClause ic)
                        CollectReferencedIdentifiers(ic.Condition, identifiers);
                }
                break;
            case SetComprehension comp:
                CollectReferencedIdentifiers(comp.Element, identifiers);
                foreach (var clause in comp.Clauses)
                {
                    if (clause is ForClause fc)
                        CollectReferencedIdentifiers(fc.Iterator, identifiers);
                    else if (clause is IfClause ic)
                        CollectReferencedIdentifiers(ic.Condition, identifiers);
                }
                break;
            case DictComprehension comp:
                CollectReferencedIdentifiers(comp.Key, identifiers);
                CollectReferencedIdentifiers(comp.Value, identifiers);
                foreach (var clause in comp.Clauses)
                {
                    if (clause is ForClause fc)
                        CollectReferencedIdentifiers(fc.Iterator, identifiers);
                    else if (clause is IfClause ic)
                        CollectReferencedIdentifiers(ic.Condition, identifiers);
                }
                break;
            // Literals and other expressions with no identifier references
            case IntegerLiteral:
            case FloatLiteral:
            case StringLiteral:
            case BooleanLiteral:
            case NoneLiteral:
            case EllipsisLiteral:
            case SuperExpression:
                // No identifiers to collect
                break;
        }
    }
}
