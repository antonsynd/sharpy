using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: @dataclass code generation.
/// Generates auto-properties, constructor, Equals, GetHashCode, and ToString
/// for classes decorated with @dataclass.
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Generates a C# auto-property for a @dataclass field.
    /// Uses { get; set; } normally, or { get; init; } when frozen=True.
    /// </summary>
    private PropertyDeclarationSyntax GenerateDataclassProperty(
        VariableDeclaration varDecl, string propertyName, bool frozen)
    {
        TypeSyntax propType;
        if (varDecl.Type != null)
        {
            propType = _typeMapper.MapType(varDecl.Type);
        }
        else
        {
            propType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        var accessors = new List<AccessorDeclarationSyntax>
        {
            AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
        };

        if (frozen)
        {
            accessors.Add(
                AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
        }
        else
        {
            accessors.Add(
                AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
        }

        var propDecl = PropertyDeclaration(propType, Identifier(propertyName))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithAccessorList(AccessorList(List(accessors)));

        // Add default value initializer if present
        if (varDecl.InitialValue != null)
        {
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = varDecl.Type;
            try
            {
                var initExpr = GenerateExpression(varDecl.InitialValue);
                propDecl = propDecl.WithInitializer(EqualsValueClause(initExpr))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }
            finally
            {
                _targetTypeContext = previousTargetType;
            }
        }

        return propDecl;
    }

    /// <summary>
    /// Generates all synthesized members for a @dataclass: constructor, Equals, GetHashCode, ToString.
    /// Only generates members that are not explicitly defined by the user.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateDataclassMembers(
        TypeSymbol typeSymbol, string className, IReadOnlyList<Statement> classBody)
    {
        var members = new List<MemberDeclarationSyntax>();
        var options = typeSymbol.DataclassInfo!;
        var fields = typeSymbol.DataclassFields ?? new List<VariableSymbol>();

        // Separate own fields from inherited fields
        var ownFields = new List<VariableSymbol>();
        var inheritedFields = new List<VariableSymbol>();
        var ownFieldNames = new HashSet<string>(typeSymbol.Fields.Select(f => f.Name));
        foreach (var field in fields)
        {
            if (ownFieldNames.Contains(field.Name))
                ownFields.Add(field);
            else
                inheritedFields.Add(field);
        }

        // Generate constructor
        members.Add(GenerateDataclassConstructor(
            typeSymbol, className, fields, ownFields, inheritedFields, classBody));

        // Generate Equals + GetHashCode + operator ==/!= if eq=True
        if (options.Eq)
        {
            members.Add(GenerateDataclassEquals(className, fields));
            members.Add(GenerateDataclassGetHashCode(fields));
            members.Add(GenerateDataclassOperatorEquals(className));
            members.Add(GenerateDataclassOperatorNotEquals(className));
        }

        // Generate ToString if repr=True
        if (options.Repr)
        {
            members.Add(GenerateDataclassToString(typeSymbol.Name, fields));
        }

        return members;
    }

    /// <summary>
    /// Generates a constructor for a @dataclass.
    /// Parameters match the field list (inherited + own), with default values where applicable.
    /// Calls base() for inherited fields, assigns own fields, then calls __post_init__ if present.
    /// </summary>
    private ConstructorDeclarationSyntax GenerateDataclassConstructor(
        TypeSymbol typeSymbol,
        string className,
        List<VariableSymbol> allFields,
        List<VariableSymbol> ownFields,
        List<VariableSymbol> inheritedFields,
        IReadOnlyList<Statement> classBody)
    {
        // Build parameter list
        var parameters = new List<ParameterSyntax>();
        foreach (var field in allFields)
        {
            var paramName = field.Name;
            var paramType = _typeMapper.MapSemanticType(GetVariableType(field));

            var param = Parameter(Identifier(paramName))
                .WithType(paramType);

            // Add default value if present
            if (field.HasDefaultValue)
            {
                // Find the corresponding VariableDeclaration AST node for the initializer
                var fieldDecl = classBody.OfType<VariableDeclaration>()
                    .FirstOrDefault(v => v.Name == field.Name);
                if (fieldDecl?.InitialValue != null)
                {
                    var defaultExpr = GenerateExpression(fieldDecl.InitialValue);
                    param = param.WithDefault(EqualsValueClause(defaultExpr));
                }
            }

            parameters.Add(param);
        }

        // Build constructor body: assignments for own fields
        var statements = new List<StatementSyntax>();
        foreach (var field in ownFields)
        {
            var propName = GetCodeGenInfo(field)?.CSharpName
                ?? NameMangler.ToPascalCase(field.Name);
            var paramName = field.Name;

            statements.Add(ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ThisExpression(),
                        IdentifierName(propName)),
                    IdentifierName(paramName))));
        }

        // Call __post_init__ if present
        if (typeSymbol.ProtocolMethods.ContainsKey(DunderNames.PostInit)
            || typeSymbol.Methods.Any(m => m.Name == DunderNames.PostInit))
        {
            statements.Add(ExpressionStatement(
                InvocationExpression(IdentifierName("PostInit"))));
        }

        var constructor = ConstructorDeclaration(Identifier(className))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(Block(statements));

        // Add base() call if there are inherited fields
        if (inheritedFields.Count > 0)
        {
            var baseArgs = inheritedFields
                .Select(f => Argument(IdentifierName(f.Name)))
                .ToArray();

            constructor = constructor.WithInitializer(
                ConstructorInitializer(
                    SyntaxKind.BaseConstructorInitializer,
                    ArgumentList(SeparatedList(baseArgs))));
        }

        return constructor;
    }

    /// <summary>
    /// Generates override bool Equals(object? obj) for a @dataclass.
    /// Pattern: if (obj is not ClassName other) return false;
    ///          return Equals(F1, other.F1) && Equals(F2, other.F2) && ...;
    /// </summary>
    private MethodDeclarationSyntax GenerateDataclassEquals(
        string className, List<VariableSymbol> fields)
    {
        var statements = new List<StatementSyntax>();

        // if (obj is not ClassName other) return false;
        statements.Add(
            IfStatement(
                IsPatternExpression(
                    IdentifierName("obj"),
                    UnaryPattern(
                        DeclarationPattern(
                            IdentifierName(className),
                            SingleVariableDesignation(Identifier("other"))))),
                ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression))));

        // return Equals(F1, other.F1) && Equals(F2, other.F2) && ...;
        ExpressionSyntax returnExpr;
        if (fields.Count == 0)
        {
            returnExpr = LiteralExpression(SyntaxKind.TrueLiteralExpression);
        }
        else
        {
            returnExpr = GenerateFieldEqualsChain(fields, 0);
        }

        statements.Add(ReturnStatement(returnExpr));

        return MethodDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), "Equals")
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.OverrideKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(
                Parameter(Identifier("obj"))
                    .WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))))))
            .WithBody(Block(statements));
    }

    private ExpressionSyntax GenerateFieldEqualsChain(List<VariableSymbol> fields, int index)
    {
        var field = fields[index];
        var propName = GetCodeGenInfo(field)?.CSharpName
            ?? NameMangler.ToPascalCase(field.Name);

        // Equals(this.Field, other.Field)
        var equalsCall = InvocationExpression(
            IdentifierName("Equals"),
            ArgumentList(SeparatedList(new[]
            {
                Argument(IdentifierName(propName)),
                Argument(MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("other"),
                    IdentifierName(propName)))
            })));

        if (index == fields.Count - 1)
            return equalsCall;

        return BinaryExpression(
            SyntaxKind.LogicalAndExpression,
            equalsCall,
            GenerateFieldEqualsChain(fields, index + 1));
    }

    /// <summary>
    /// Generates override int GetHashCode() for a @dataclass.
    /// Pattern: return HashCode.Combine(F1, F2, ...);
    /// </summary>
    private MethodDeclarationSyntax GenerateDataclassGetHashCode(List<VariableSymbol> fields)
    {
        StatementSyntax[] statements;
        if (fields.Count == 0)
        {
            statements = new[]
            {
                ReturnStatement(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)))
            };
        }
        else if (fields.Count <= 8)
        {
            // Use HashCode.Combine for up to 8 fields (max overload arity)
            var args = fields.Select(f =>
            {
                var propName = GetCodeGenInfo(f)?.CSharpName
                    ?? NameMangler.ToPascalCase(f.Name);
                return Argument(IdentifierName(propName));
            }).ToArray();

            var hashExpr = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("HashCode"),
                    IdentifierName("Combine")),
                ArgumentList(SeparatedList(args)));

            statements = new[] { ReturnStatement(hashExpr) };
        }
        else
        {
            // For 9+ fields, use incremental HashCode.Add
            var stmts = new List<StatementSyntax>();

            // var hc = new HashCode();
            stmts.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator("hc")
                            .WithInitializer(EqualsValueClause(
                                ObjectCreationExpression(IdentifierName("HashCode"))
                                    .WithArgumentList(ArgumentList())))))));

            // hc.Add(Field) for each field
            foreach (var f in fields)
            {
                var propName = GetCodeGenInfo(f)?.CSharpName
                    ?? NameMangler.ToPascalCase(f.Name);
                stmts.Add(ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("hc"),
                            IdentifierName("Add")),
                        ArgumentList(SingletonSeparatedList(
                            Argument(IdentifierName(propName)))))));
            }

            // return hc.ToHashCode();
            stmts.Add(ReturnStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("hc"),
                        IdentifierName("ToHashCode")))));

            statements = stmts.ToArray();
        }

        return MethodDeclaration(
            PredefinedType(Token(SyntaxKind.IntKeyword)), "GetHashCode")
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.OverrideKeyword)))
            .WithBody(Block(statements));
    }

    /// <summary>
    /// Generates operator == for a @dataclass, delegating to Equals.
    /// </summary>
    private static OperatorDeclarationSyntax GenerateDataclassOperatorEquals(string className)
    {
        return OperatorDeclaration(
            PredefinedType(Token(SyntaxKind.BoolKeyword)),
            Token(SyntaxKind.EqualsEqualsToken))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[]
            {
                Parameter(Identifier("left"))
                    .WithType(NullableType(IdentifierName(className))),
                Parameter(Identifier("right"))
                    .WithType(NullableType(IdentifierName(className))),
            })))
            .WithExpressionBody(ArrowExpressionClause(
                InvocationExpression(
                    IdentifierName("Equals"),
                    ArgumentList(SeparatedList(new[]
                    {
                        Argument(IdentifierName("left")),
                        Argument(IdentifierName("right")),
                    })))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    /// <summary>
    /// Generates operator != for a @dataclass, delegating to Equals.
    /// </summary>
    private static OperatorDeclarationSyntax GenerateDataclassOperatorNotEquals(string className)
    {
        return OperatorDeclaration(
            PredefinedType(Token(SyntaxKind.BoolKeyword)),
            Token(SyntaxKind.ExclamationEqualsToken))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[]
            {
                Parameter(Identifier("left"))
                    .WithType(NullableType(IdentifierName(className))),
                Parameter(Identifier("right"))
                    .WithType(NullableType(IdentifierName(className))),
            })))
            .WithExpressionBody(ArrowExpressionClause(
                PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    InvocationExpression(
                        IdentifierName("Equals"),
                        ArgumentList(SeparatedList(new[]
                        {
                            Argument(IdentifierName("left")),
                            Argument(IdentifierName("right")),
                        }))))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    /// <summary>
    /// Generates override string ToString() for a @dataclass.
    /// Pattern: return $"ClassName(field1={Field1}, field2={Field2}, ...)";
    /// </summary>
    private MethodDeclarationSyntax GenerateDataclassToString(
        string originalTypeName, List<VariableSymbol> fields)
    {
        ExpressionSyntax returnExpr;
        if (fields.Count == 0)
        {
            returnExpr = LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                Literal($"{originalTypeName}()"));
        }
        else
        {
            // Build interpolated string: $"ClassName(f1={F1}, f2={F2}, ...)"
            var parts = new List<InterpolatedStringContentSyntax>();

            parts.Add(InterpolatedStringText()
                .WithTextToken(Token(
                    TriviaList(),
                    SyntaxKind.InterpolatedStringTextToken,
                    $"{originalTypeName}(",
                    $"{originalTypeName}(",
                    TriviaList())));

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var propName = GetCodeGenInfo(field)?.CSharpName
                    ?? NameMangler.ToPascalCase(field.Name);

                var prefix = i > 0 ? $", {field.Name}=" : $"{field.Name}=";
                parts.Add(InterpolatedStringText()
                    .WithTextToken(Token(
                        TriviaList(),
                        SyntaxKind.InterpolatedStringTextToken,
                        prefix,
                        prefix,
                        TriviaList())));

                parts.Add(Interpolation(IdentifierName(propName)));
            }

            parts.Add(InterpolatedStringText()
                .WithTextToken(Token(
                    TriviaList(),
                    SyntaxKind.InterpolatedStringTextToken,
                    ")",
                    ")",
                    TriviaList())));

            returnExpr = InterpolatedStringExpression(
                Token(SyntaxKind.InterpolatedStringStartToken),
                List(parts));
        }

        return MethodDeclaration(
            PredefinedType(Token(SyntaxKind.StringKeyword)), "ToString")
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.OverrideKeyword)))
            .WithBody(Block(ReturnStatement(returnExpr)));
    }

}
