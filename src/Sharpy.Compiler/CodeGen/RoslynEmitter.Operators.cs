using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Operator overloads and utility methods
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Returns the TypeSyntax for the current class, including generic type parameters if applicable.
    /// For generic classes like Box[T], returns GenericName("Box", TypeArgumentList("T")).
    /// For non-generic classes, returns IdentifierName(className).
    /// Used for operator parameter types where the enclosing class needs type parameters.
    /// </summary>
    private TypeSyntax GetCurrentClassTypeSyntax(string className)
    {
        if (_currentTypeSymbol?.IsGeneric == true && _currentTypeSymbol.TypeParameters.Count > 0)
        {
            return GenericName(className)
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(
                    _currentTypeSymbol.TypeParameters.Select(tp => (TypeSyntax)IdentifierName(tp.Name)))));
        }
        return IdentifierName(className);
    }

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
            DunderNames.Div => GenerateBinaryOperator(funcDef, className, SyntaxKind.SlashToken),
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

            // __bool__ is handled in GenerateClassMembers (IsTrue property + operators)
            DunderNames.Bool => null,

            // Not supported as operators (handled as methods)
            DunderNames.GetItem => null, // Requires indexer syntax, not operator
            DunderNames.SetItem => null, // Requires indexer syntax, not operator

            _ => null
        };
    }

    /// <summary>
    /// Try to generate an operator overload with the dunder body inlined directly
    /// into the static operator (no instance method). Returns null if the dunder
    /// is not an operator dunder, or returns a list with potentially two members:
    /// - For super() bodies: a private _Impl method + operator that delegates to it
    /// - Otherwise: just the operator with inlined body
    /// </summary>
    private List<MemberDeclarationSyntax>? TryGenerateInlinedOperatorOverload(FunctionDef funcDef, string className)
    {
        return funcDef.Name switch
        {
            // Arithmetic operators (binary)
            DunderNames.Add => GenerateInlinedBinaryOperator(funcDef, className, SyntaxKind.PlusToken),
            DunderNames.Sub => GenerateInlinedBinaryOperator(funcDef, className, SyntaxKind.MinusToken),
            DunderNames.Mul => GenerateInlinedBinaryOperator(funcDef, className, SyntaxKind.AsteriskToken),
            DunderNames.Div => GenerateInlinedBinaryOperator(funcDef, className, SyntaxKind.SlashToken),
            DunderNames.Mod => GenerateInlinedBinaryOperator(funcDef, className, SyntaxKind.PercentToken),

            // Bitwise operators (binary)
            DunderNames.And => GenerateInlinedBinaryOperator(funcDef, className, SyntaxKind.AmpersandToken),
            DunderNames.Or => GenerateInlinedBinaryOperator(funcDef, className, SyntaxKind.BarToken),
            DunderNames.Xor => GenerateInlinedBinaryOperator(funcDef, className, SyntaxKind.CaretToken),
            DunderNames.LShift => GenerateInlinedBinaryOperator(funcDef, className, SyntaxKind.LessThanLessThanToken),
            DunderNames.RShift => GenerateInlinedBinaryOperator(funcDef, className, SyntaxKind.GreaterThanGreaterThanToken),

            // Comparison operators (binary) — excluding __eq__/__ne__ which keep their Equals path
            DunderNames.Lt => GenerateInlinedComparisonOperator(funcDef, className, SyntaxKind.LessThanToken),
            DunderNames.Le => GenerateInlinedComparisonOperator(funcDef, className, SyntaxKind.LessThanEqualsToken),
            DunderNames.Gt => GenerateInlinedComparisonOperator(funcDef, className, SyntaxKind.GreaterThanToken),
            DunderNames.Ge => GenerateInlinedComparisonOperator(funcDef, className, SyntaxKind.GreaterThanEqualsToken),

            // Unary operators
            DunderNames.Neg => GenerateInlinedUnaryOperator(funcDef, className, SyntaxKind.MinusToken),
            DunderNames.Pos => GenerateInlinedUnaryOperator(funcDef, className, SyntaxKind.PlusToken),
            DunderNames.Invert => GenerateInlinedUnaryOperator(funcDef, className, SyntaxKind.TildeToken),

            _ => null
        };
    }

    /// <summary>
    /// Checks if an AST body contains any SuperExpression references.
    /// If so, the body cannot be inlined into a static operator (base requires instance context).
    /// </summary>
    private static bool ContainsSuperExpression(IReadOnlyList<Statement> body)
    {
        foreach (var stmt in body)
        {
            if (ContainsSuperExpressionInStatement(stmt))
                return true;
        }
        return false;
    }

    private static bool ContainsSuperExpressionInStatement(Statement stmt)
    {
        return stmt switch
        {
            ExpressionStatement es => ContainsSuperExpressionInExpression(es.Expression),
            ReturnStatement rs => rs.Value != null && ContainsSuperExpressionInExpression(rs.Value),
            Assignment a => ContainsSuperExpressionInExpression(a.Value) || ContainsSuperExpressionInExpression(a.Target),
            IfStatement ifs => ContainsSuperExpression(ifs.ThenBody)
                || ifs.ElifClauses.Any(e => ContainsSuperExpression(e.Body))
                || ContainsSuperExpression(ifs.ElseBody)
                || ContainsSuperExpressionInExpression(ifs.Test),
            VariableDeclaration vd => vd.InitialValue != null && ContainsSuperExpressionInExpression(vd.InitialValue),
            _ => false
        };
    }

    private static bool ContainsSuperExpressionInExpression(Expression expr)
    {
        return expr switch
        {
            SuperExpression => true,
            FunctionCall fc => ContainsSuperExpressionInExpression(fc.Function)
                || fc.Arguments.Any(ContainsSuperExpressionInExpression),
            MemberAccess ma => ContainsSuperExpressionInExpression(ma.Object),
            BinaryOp bo => ContainsSuperExpressionInExpression(bo.Left) || ContainsSuperExpressionInExpression(bo.Right),
            UnaryOp uo => ContainsSuperExpressionInExpression(uo.Operand),
            ConditionalExpression ce => ContainsSuperExpressionInExpression(ce.Test)
                || ContainsSuperExpressionInExpression(ce.ThenValue)
                || ContainsSuperExpressionInExpression(ce.ElseValue),
            Parenthesized p => ContainsSuperExpressionInExpression(p.Expression),
            _ => false
        };
    }

    /// <summary>
    /// Sets up scope state for inlined operator body generation.
    /// Clears tracking and sets self replacement + parameter name overrides.
    /// Returns the previous state for restoration.
    /// </summary>
    private (string? prevSelfReplacement, Dictionary<string, string>? prevParamOverrides)
        SetupInlinedOperatorScope(
            FunctionDef funcDef,
            string selfReplacement,
            Dictionary<string, string> paramOverrides)
    {
        var prev = (_selfReplacementIdentifier, _parameterNameOverrides);

        ResetMethodScope();
        CollectSourceVariableNames(funcDef.Body);

        _selfReplacementIdentifier = selfReplacement;
        _parameterNameOverrides = paramOverrides;

        return prev;
    }

    /// <summary>
    /// Restores scope state after inlined operator body generation.
    /// </summary>
    private void RestoreInlinedOperatorScope(
        (string? prevSelfReplacement, Dictionary<string, string>? prevParamOverrides) prev)
    {
        _selfReplacementIdentifier = prev.prevSelfReplacement;
        _parameterNameOverrides = prev.prevParamOverrides;
    }

    /// <summary>
    /// Generate an inlined binary operator. The dunder body is emitted directly inside the
    /// static operator method, with self→left and the non-self parameter→right.
    /// If the body contains super(), falls back to a private instance method.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateInlinedBinaryOperator(
        FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        var members = new List<MemberDeclarationSyntax>();

        var otherParam = funcDef.Parameters
            .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));

        if (otherParam == null)
            throw new InvalidOperationException($"Binary operator {funcDef.Name} must have at least 2 parameters");

        var classTypeSyntax = GetCurrentClassTypeSyntax(className);
        var returnType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : classTypeSyntax;

        var param1 = Parameter(Identifier("left")).WithType(classTypeSyntax);
        var param2Type = otherParam.Type != null ? _typeMapper.MapType(otherParam.Type) : classTypeSyntax;
        var param2 = Parameter(Identifier("right")).WithType(param2Type);

        BlockSyntax operatorBody;

        if (ContainsSuperExpression(funcDef.Body))
        {
            // Super() requires instance context — generate private impl method + delegation
            var implName = $"_{NameMangler.ToPascalCase(funcDef.Name[2..^2])}Impl";
            members.Add(GenerateClassMethod(funcDef)
                .WithIdentifier(Identifier(implName))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword))));

            var invocation = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("left"), IdentifierName(implName)))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("right")))));
            operatorBody = Block(ReturnStatement(invocation));
        }
        else
        {
            // Inline the body: self→left, other→right
            var paramBaseName = NameMangler.ToCamelCase(otherParam.Name);
            var overrides = new Dictionary<string, string> { { paramBaseName, "right" } };
            var prev = SetupInlinedOperatorScope(funcDef, "left", overrides);
            try
            {
                var bodyStatements = funcDef.Body.SelectMany(GenerateBodyStatements);
                operatorBody = Block(bodyStatements);
            }
            finally
            {
                RestoreInlinedOperatorScope(prev);
            }
        }

        members.Add(OperatorDeclaration(returnType, Token(operatorToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[] { param1, param2 })))
            .WithBody(operatorBody));

        return members;
    }

    /// <summary>
    /// Generate an inlined comparison operator (excluding __eq__/__ne__).
    /// The dunder body is emitted directly inside the static operator, always returning bool.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateInlinedComparisonOperator(
        FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        var members = new List<MemberDeclarationSyntax>();

        var otherParam = funcDef.Parameters
            .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));

        if (otherParam == null)
            throw new InvalidOperationException($"Comparison operator {funcDef.Name} must have at least 2 parameters");

        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        var classTypeSyntax = GetCurrentClassTypeSyntax(className);
        var param1 = Parameter(Identifier("left")).WithType(classTypeSyntax);
        var param2Type = otherParam.Type != null ? _typeMapper.MapType(otherParam.Type) : classTypeSyntax;
        var param2 = Parameter(Identifier("right")).WithType(param2Type);

        BlockSyntax operatorBody;

        if (ContainsSuperExpression(funcDef.Body))
        {
            var implName = $"_{NameMangler.ToPascalCase(funcDef.Name[2..^2])}Impl";
            members.Add(GenerateClassMethod(funcDef)
                .WithIdentifier(Identifier(implName))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword))));

            var invocation = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("left"), IdentifierName(implName)))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("right")))));
            operatorBody = Block(ReturnStatement(invocation));
        }
        else
        {
            var paramBaseName = NameMangler.ToCamelCase(otherParam.Name);
            var overrides = new Dictionary<string, string> { { paramBaseName, "right" } };
            var prev = SetupInlinedOperatorScope(funcDef, "left", overrides);
            try
            {
                var bodyStatements = funcDef.Body.SelectMany(GenerateBodyStatements);
                operatorBody = Block(bodyStatements);
            }
            finally
            {
                RestoreInlinedOperatorScope(prev);
            }
        }

        members.Add(OperatorDeclaration(returnType, Token(operatorToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[] { param1, param2 })))
            .WithBody(operatorBody));

        return members;
    }

    /// <summary>
    /// Generate an inlined unary operator. The dunder body is emitted directly inside
    /// the static operator, with self→value.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateInlinedUnaryOperator(
        FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        var members = new List<MemberDeclarationSyntax>();

        var classTypeSyntax = GetCurrentClassTypeSyntax(className);
        var returnType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : classTypeSyntax;

        var param = Parameter(Identifier("value")).WithType(classTypeSyntax);

        BlockSyntax operatorBody;

        if (ContainsSuperExpression(funcDef.Body))
        {
            var implName = $"_{NameMangler.ToPascalCase(funcDef.Name[2..^2])}Impl";
            members.Add(GenerateClassMethod(funcDef)
                .WithIdentifier(Identifier(implName))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword))));

            var invocation = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("value"), IdentifierName(implName)))
                .WithArgumentList(ArgumentList());
            operatorBody = Block(ReturnStatement(invocation));
        }
        else
        {
            var overrides = new Dictionary<string, string>();
            var prev = SetupInlinedOperatorScope(funcDef, "value", overrides);
            try
            {
                var bodyStatements = funcDef.Body.SelectMany(GenerateBodyStatements);
                operatorBody = Block(bodyStatements);
            }
            finally
            {
                RestoreInlinedOperatorScope(prev);
            }
        }

        members.Add(OperatorDeclaration(returnType, Token(operatorToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(param)))
            .WithBody(operatorBody));

        return members;
    }

    /// <summary>
    /// Generate a binary operator overload (e.g., operator +, operator -, etc.)
    /// </summary>
    private OperatorDeclarationSyntax GenerateBinaryOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        // Binary operators should have 2 parameters: self and other
        // We skip 'self' and use the other parameter
        var otherParam = funcDef.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (otherParam == null)
        {
            throw new InvalidOperationException($"Binary operator {funcDef.Name} must have at least 2 parameters");
        }

        // Determine return type - default to class type if not specified
        var classTypeSyntax = GetCurrentClassTypeSyntax(className);
        var returnType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : classTypeSyntax;

        // Generate parameter for the operator — use generic type syntax for generic classes
        var param1 = Parameter(Identifier("left"))
            .WithType(classTypeSyntax);

        var param2Type = otherParam.Type != null
            ? _typeMapper.MapType(otherParam.Type)
            : classTypeSyntax;

        var param2 = Parameter(Identifier("right"))
            .WithType(param2Type);

        // Generate body - call the actual dunder method on left operand
        // Only __eq__/__ne__ use this non-inlined path; all other operators use TryGenerateInlinedOperatorOverload.
        // DunderMapping resolves to the mapped C# name (e.g., __eq__ -> Equals, __ne__ -> NotEquals).
        var methodName = DunderMapping.ResolveCSharpName(funcDef.Name)
            ?? NameMangler.Transform(funcDef.Name, NameContext.Method);
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
    /// Generate a comparison operator overload (==, !=, &lt;, &gt;, &lt;=, &gt;=).
    /// For __eq__ on class types, routes through Equals(T) with null-safety:
    ///   left?.Equals(right) ?? right is null
    /// This enables proper polymorphic dispatch through IEquatable&lt;T&gt;.
    /// </summary>
    private OperatorDeclarationSyntax GenerateComparisonOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        // Similar to binary operators but always returns bool
        var otherParam = funcDef.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (otherParam == null)
        {
            throw new InvalidOperationException($"Comparison operator {funcDef.Name} must have at least 2 parameters");
        }

        // Comparison operators always return bool
        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        // Generate parameters — use generic type syntax for generic classes (e.g., Box<T>)
        var classTypeSyntax = GetCurrentClassTypeSyntax(className);
        var param1 = Parameter(Identifier("left"))
            .WithType(classTypeSyntax);

        var param2Type = otherParam.Type != null
            ? _typeMapper.MapType(otherParam.Type)
            : classTypeSyntax;

        var param2 = Parameter(Identifier("right"))
            .WithType(param2Type);

        // Generate body - for __eq__ on class types, use null-safe dispatch through Equals(T)
        ExpressionSyntax returnExpr;
        var methodName = DunderMapping.ResolveCSharpName(funcDef.Name)
            ?? NameMangler.Transform(funcDef.Name, NameContext.Method);

        if (funcDef.Name == DunderNames.Eq && !IsEqualsObjectOverload(funcDef)
            && _currentTypeSymbol?.TypeKind == Semantic.TypeKind.Class)
        {
            // For classes: return left?.Equals(right) ?? right is null;
            // This routes through virtual Equals(T), enables polymorphic dispatch,
            // and handles null correctly (null == null is true, null == x is false)
            returnExpr = GenerateNullSafeEqualsExpression();
        }
        else
        {
            // For all other comparison operators (and struct __eq__): call dunder directly
            returnExpr = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("left"),
                    IdentifierName(methodName)))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("right")))));
        }

        var body = Block(ReturnStatement(returnExpr));

        return OperatorDeclaration(returnType, Token(operatorToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[] { param1, param2 })))
            .WithBody(body);
    }

    /// <summary>
    /// Generates: left?.Equals(right) ?? right is null
    /// Used for null-safe equality dispatch through IEquatable&lt;T&gt;.
    /// </summary>
    private static ExpressionSyntax GenerateNullSafeEqualsExpression()
    {
        // left?.Equals(right)
        var nullConditionalEquals = ConditionalAccessExpression(
            IdentifierName("left"),
            InvocationExpression(
                MemberBindingExpression(IdentifierName("Equals")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("right"))))));

        // right is null
        var rightIsNull = IsPatternExpression(
            IdentifierName("right"),
            ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression)));

        // left?.Equals(right) ?? right is null
        return BinaryExpression(
            SyntaxKind.CoalesceExpression,
            nullConditionalEquals,
            rightIsNull);
    }

    /// <summary>
    /// Generate operator true for __bool__: public static bool operator true(T value) => value.IsTrue;
    /// </summary>
    private OperatorDeclarationSyntax GenerateBoolOperatorTrue(string className)
    {
        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        var param = Parameter(Identifier("value"))
            .WithType(GetCurrentClassTypeSyntax(className));

        // Reference the IsTrue property
        var isTrueAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName("value"),
            IdentifierName("IsTrue"));

        var body = Block(ReturnStatement(isTrueAccess));

        return OperatorDeclaration(returnType, Token(SyntaxKind.TrueKeyword))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(param)))
            .WithBody(body);
    }

    /// <summary>
    /// Generate operator false for __bool__: public static bool operator false(T value) => !value.IsTrue;
    /// </summary>
    private OperatorDeclarationSyntax GenerateBoolOperatorFalse(string className)
    {
        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        var param = Parameter(Identifier("value"))
            .WithType(GetCurrentClassTypeSyntax(className));

        // Negate the IsTrue property
        var isTrueAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName("value"),
            IdentifierName("IsTrue"));
        var negation = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, isTrueAccess);
        var body = Block(ReturnStatement(negation));

        return OperatorDeclaration(returnType, Token(SyntaxKind.FalseKeyword))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(param)))
            .WithBody(body);
    }

    /// <summary>
    /// Generate a unary operator overload (-, +, ~)
    /// </summary>
    private OperatorDeclarationSyntax GenerateUnaryOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        // Unary operators should have only 1 parameter: self

        // Determine return type - default to class type if not specified
        var classTypeSyntax = GetCurrentClassTypeSyntax(className);
        var returnType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : classTypeSyntax;

        // Generate parameter for the operator — use generic type syntax for generic classes
        var param = Parameter(Identifier("value"))
            .WithType(classTypeSyntax);

        // Generate body - call the actual dunder method on the operand
        var methodName = DunderMapping.ResolveCSharpName(funcDef.Name)
            ?? NameMangler.Transform(funcDef.Name, NameContext.Method);
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

        var classTypeSyntax = GetCurrentClassTypeSyntax(className);
        var param1 = Parameter(Identifier("left"))
            .WithType(classTypeSyntax);
        var param2 = Parameter(Identifier("right"))
            .WithType(classTypeSyntax);

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
    /// Generate complementary operator != when only __eq__ is defined.
    /// Matches the parameter types of the corresponding __eq__ overload.
    /// For class types with non-object __eq__, routes through Equals(T) for consistency:
    ///   !(left?.Equals(right) ?? right is null)
    /// </summary>
    private OperatorDeclarationSyntax GenerateComplementaryNotEqualsOperator(FunctionDef eqMethod, string className)
    {
        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        var otherParam = eqMethod.Parameters
            .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));

        var classTypeSyntax = GetCurrentClassTypeSyntax(className);
        var param2Type = otherParam?.Type != null
            ? _typeMapper.MapType(otherParam.Type)
            : classTypeSyntax;

        var param1 = Parameter(Identifier("left"))
            .WithType(classTypeSyntax);
        var param2 = Parameter(Identifier("right"))
            .WithType(param2Type);

        ExpressionSyntax returnExpr;
        if (!IsEqualsObjectOverload(eqMethod) && _currentTypeSymbol?.TypeKind == Semantic.TypeKind.Class)
        {
            // For classes: return !(left?.Equals(right) ?? right is null);
            returnExpr = PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                ParenthesizedExpression(GenerateNullSafeEqualsExpression()));
        }
        else
        {
            // operator != returns !(left == right)
            returnExpr = PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                ParenthesizedExpression(
                    BinaryExpression(
                        SyntaxKind.EqualsExpression,
                        IdentifierName("left"),
                        IdentifierName("right"))));
        }

        var body = Block(ReturnStatement(returnExpr));

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
                    MakeGlobalQualifiedName("Sharpy", "Result"),
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
                    MakeGlobalQualifiedName("Sharpy", "Result"),
                    IdentifierName("Try")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(lambdaExpr))));
        }
    }

    /// <summary>
    /// Generate a maybe expression: maybe expr
    /// Converts a C# nullable (T | None) to Optional&lt;T&gt; via Optional.From().
    /// </summary>
    private ExpressionSyntax GenerateMaybeExpression(MaybeExpression maybeExpr)
    {
        var operand = GenerateExpression(maybeExpr.Operand);

        // Generate: global::Sharpy.Optional.From(operand)
        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    AliasQualifiedName(IdentifierName(Token(SyntaxKind.GlobalKeyword)), IdentifierName("Sharpy")),
                    IdentifierName("Optional")),
                IdentifierName("From")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(operand))));
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
    /// Returns true if the given SemanticType is an integer type (int or long).
    /// </summary>
    private static bool IsIntegerSemanticType(SemanticType? type)
    {
        return type == SemanticType.Int || type == SemanticType.Long;
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
    /// Checks if an expression's type is an enum instance type.
    /// Uses both SemanticInfo (for type-checked expressions) and symbol table
    /// (for variables after reassignment where SemanticInfo may not have the type).
    /// </summary>
    private bool IsEnumInstance(Expression expr)
    {
        // First try SemanticInfo (most reliable for type-checked expressions)
        var semType = GetExpressionSemanticType(expr);
        if (semType is Semantic.UserDefinedType udt)
        {
            if (udt.Symbol?.TypeKind == Semantic.TypeKind.Enum)
                return true;

            // Cross-module: Symbol may be null but name is set — look up by name
            if (udt.Symbol == null && !string.IsNullOrEmpty(udt.Name))
            {
                var lookedUp = _context.LookupSymbol(udt.Name);
                if (lookedUp is TypeSymbol ts && ts.TypeKind == Semantic.TypeKind.Enum)
                    return true;
            }
        }

        // Fallback: symbol table lookup for identifiers (handles post-reassignment cases)
        if (expr is Identifier id)
        {
            var symbol = _context.LookupSymbol(id.Name);
            if (symbol is VariableSymbol varSymbol &&
                GetVariableType(varSymbol) is Semantic.UserDefinedType varUdt &&
                varUdt.Symbol?.TypeKind == Semantic.TypeKind.Enum)
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
                    if (entry.Key != null)
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
