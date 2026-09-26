using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Sharpy.Compiler.CodeGen.EmittedTreePrecedence;

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
        VariableDeclaration varDecl, string propertyName, bool frozen, bool constructorOwnsDefault)
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

        // The field symbol's classified access, like every other member (#1937, #2012): a dataclass
        // `_x` is protected (the AccessValidator already refuses `d._x` from outside, SPY0283), not the
        // hard-coded public this site used to spell.
        var propDecl = PropertyDeclaration(propType, Identifier(propertyName))
            .WithModifiers(TokenList(Token(MemberAccessKeyword(varDecl))))
            .WithAccessorList(AccessorList(List(accessors)));

        // Add default value initializer if present. A comprehension/generator/lambda/walrus in the
        // initializer hoists under its own scope sink so it has somewhere to land (#1685) — see
        // GenerateInitializerExpression.
        //
        // A property initializer runs on EVERY construction, including one that passed an explicit
        // argument, so for R-A's per-instance family it was a second, unconditional evaluation of
        // the default (#1901): `Bag([9])` printed the side effects of `[side(i) for i in range(2)]`
        // and threw the list away, and `Bag()` printed them twice. `constructorOwnsDefault` is true
        // exactly where the synthesized constructor definitely assigns this property on both of its
        // paths (GeneratePerInstanceDefaultAssignment), so dropping the initializer leaves the
        // property definitely assigned and evaluates the default exactly once, only when absent.
        if (varDecl.InitialValue != null && !constructorOwnsDefault)
        {
            var initExpr = GenerateInitializerExpression(varDecl.InitialValue, propType);
            propDecl = propDecl.WithInitializer(EqualsValueClause(initExpr))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
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

        // Generate Equals + GetHashCode + operator ==/!= if eq=True. The class names itself with its
        // own type parameters (`Box<T>`): a bare `Box` does not bind a generic class (CS0305), and in a
        // module namespace spelled like the class it binds the NAMESPACE (CS0118, #2095).
        if (options.Eq)
        {
            var selfType = DataclassSelfType(typeSymbol, className);
            members.Add(GenerateDataclassEquals(selfType, fields));
            members.Add(GenerateDataclassGetHashCode(fields));
            members.Add(GenerateDataclassOperatorEquals(selfType));
            members.Add(GenerateDataclassOperatorNotEquals(selfType));
        }

        // Generate ToString if repr=True
        if (options.Repr)
        {
            members.Add(GenerateDataclassToString(typeSymbol.Name, fields));
        }

        return members;
    }

    /// <summary>
    /// The C# name of a generated dataclass constructor parameter: the SAME authority every other
    /// emitted parameter uses (<c>NameCasing.ResolveVariable</c>, i.e. camelCase, backtick-escaped
    /// spellings kept verbatim).
    /// </summary>
    /// <remarks>
    /// <para>This was the one parameter-emitting site in the emitter that did not use it: it wrote
    /// the raw Sharpy field name (<c>max_connections</c>) while the property beside it is
    /// PascalCase (<c>MaxConnections</c>) and the CALL SITE already emitted the camelCase spelling
    /// through <c>GetCSharpParameterName</c>. Two naming channels for one fact, and both consumers
    /// were broken by the one that deviated (#1504, #1499's meta-class):</para>
    /// <list type="bullet">
    ///   <item><description><c>TwoWord(max_connections=10)</c> from <c>.spy</c> emitted
    ///     <c>new TwoWord(maxConnections: 10)</c> against a parameter named
    ///     <c>max_connections</c> — CS1739 behind SPY0908, for every multi-word keyword
    ///     construction of a dataclass.</description></item>
    ///   <item><description><c>json.loads[T]</c> binds System.Text.Json constructor parameters to
    ///     properties case-INsensitively but not underscore-insensitively, so
    ///     <c>max_connections</c> ↔ <c>MaxConnections</c> never matched and STJ threw
    ///     <c>InvalidOperationException</c> out of a function returning <c>Result</c>, killing the
    ///     process for EVERY document. <c>maxConnections</c> ↔ <c>MaxConnections</c> matches.
    ///     Single-word fields (<c>port</c> ↔ <c>Port</c>) matched all along, which is why the
    ///     earlier control passed.</description></item>
    /// </list>
    /// <para>The fix is deleting the second channel, not bridging it — the call site's authority
    /// was already the correct one.</para>
    /// </remarks>
    private static string DataclassConstructorParameterName(VariableSymbol field)
        => NameCasing.ResolveVariable(field.Name, field.IsNameBacktickEscaped);

    /// <summary>
    /// Generates a constructor for a @dataclass.
    /// Parameters match the field list (inherited + own), with default values where applicable.
    /// Calls base() for inherited fields, assigns own fields, then calls __post_init__ if present.
    /// </summary>
    private ConstructorDeclarationSyntax GenerateDataclassConstructor(
        TypeSymbol typeSymbol,
        string className,
        IReadOnlyList<VariableSymbol> allFields,
        List<VariableSymbol> ownFields,
        List<VariableSymbol> inheritedFields,
        IReadOnlyList<Statement> classBody)
    {
        // Build parameter list
        var parameters = new List<ParameterSyntax>();
        foreach (var field in allFields)
        {
            var paramName = DataclassConstructorParameterName(field);
            var paramType = _typeMapper.MapSemanticType(GetVariableType(field));
            bool requiresPerInstanceDefault = GetCodeGenInfo(field)?.RequiresPerInstanceDefault == true;

            ParameterSyntax param;
            if (requiresPerInstanceDefault)
            {
                // R-A / Design Decision 2 (#1684): a mutable-collection field default is a
                // per-instance initializer, never a C# default-parameter value
                // (GenerateParameterDefault would emit an invalid non-constant default, CS1736).
                // Sentinel: T? name = null — unobservable from Sharpy because
                // RequiresPerInstanceDefault is true only for a non-nullable field (a
                // nullable/Optional field keeps the SPY0400 refusal).
                param = Parameter(EscapedIdentifier(paramName))
                    .WithType(NullableType(paramType))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.NullLiteralExpression)));
            }
            else
            {
                param = Parameter(EscapedIdentifier(paramName)).WithType(paramType);

                // Add default value if present
                if (field.HasDefaultValue)
                {
                    var fieldDecl = classBody.OfType<VariableDeclaration>()
                        .FirstOrDefault(v => v.Name == field.Name);
                    if (fieldDecl?.InitialValue != null)
                    {
                        param = param.WithDefault(GenerateParameterDefault(
                            fieldDecl.InitialValue,
                            GetVariableType(field) is Semantic.OptionalType));
                    }
                }
            }

            parameters.Add(param);
        }

        // Build constructor body: assignments for own fields
        var statements = new List<StatementSyntax>();

        foreach (var field in ownFields)
        {
            var propName = GetCodeGenInfo(field)?.CSharpName
                ?? NameCasing.ResolveField(field.Name, field.IsNameBacktickEscaped);
            var paramName = DataclassConstructorParameterName(field);
            bool requiresPerInstanceDefault = GetCodeGenInfo(field)?.RequiresPerInstanceDefault == true;

            if (requiresPerInstanceDefault)
            {
                // The default evaluates only on the absent-argument path (#1684 R-A, #1901) —
                // one shared arm with the struct host. Emitted here, ahead of the PostInit() call
                // below, so __post_init__ still sees the assigned field (R-AV ordering).
                var fieldDecl = classBody.OfType<VariableDeclaration>()
                    .First(v => v.Name == field.Name);
                statements.Add(GeneratePerInstanceDefaultAssignment(
                    fieldDecl.InitialValue!, propName, paramName));
            }
            else
            {
                statements.Add(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            ThisExpression(),
                            IdentifierName(propName)),
                        EscapedIdentifierName(paramName))));
            }
        }

        // Call __post_init__ if present
        if (typeSymbol.ProtocolMethods.ContainsKey(DunderNames.PostInit)
            || typeSymbol.Methods.Any(m => m.Name == DunderNames.PostInit))
        {
            statements.Add(ExpressionStatement(
                InvocationExpression(IdentifierName("PostInit"))));
        }

        var constructor = ConstructorDeclaration(EscapedIdentifier(className))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(Block(statements));

        // Add base() call if there are inherited fields
        if (inheritedFields.Count > 0)
        {
            var baseArgs = inheritedFields
                .Select(f => Argument(EscapedIdentifierName(DataclassConstructorParameterName(f))))
                .ToArray();

            constructor = constructor.WithInitializer(
                ConstructorInitializer(
                    SyntaxKind.BaseConstructorInitializer,
                    ArgumentList(SeparatedList(baseArgs))));
        }

        return constructor;
    }

    /// <summary>
    /// The type a @dataclass names itself with in its synthesized members: its name with its own type
    /// parameters (<c>Box&lt;T&gt;</c>), since a bare <c>Box</c> does not bind a generic class (CS0305,
    /// #2095).
    /// </summary>
    private static TypeSyntax DataclassSelfType(TypeSymbol typeSymbol, string className)
        => typeSymbol.TypeParameters.Count == 0
            ? IdentifierName(className)
            : GenericName(Identifier(className)).WithTypeArgumentList(TypeArgumentList(
                SeparatedList<TypeSyntax>(typeSymbol.TypeParameters.Select(tp => TypeParameterIdentifierName(tp.Name)))));

    /// <summary>
    /// Generates override bool Equals(object? obj) for a @dataclass.
    /// Pattern: if (obj is not ClassName other) return false;
    ///          return Equals(F1, other.F1) && Equals(F2, other.F2) && ...;
    /// </summary>
    private MethodDeclarationSyntax GenerateDataclassEquals(
        TypeSyntax selfType, IReadOnlyList<VariableSymbol> fields)
    {
        var statements = new List<StatementSyntax>();

        // if (obj is not ClassName other) return false;
        statements.Add(
            IfStatement(
                IsPatternExpression(
                    IdentifierName("obj"),
                    UnaryPattern(
                        DeclarationPattern(
                            selfType,
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
            returnExpr = GenerateFieldEqualsChain(fields);
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

    private ExpressionSyntax GenerateFieldEqualsChain(IReadOnlyList<VariableSymbol> fields)
    {
        if (fields.Count == 0)
            return LiteralExpression(SyntaxKind.TrueLiteralExpression);

        var expr = GenerateSingleFieldEquals(fields[0]);
        for (int i = 1; i < fields.Count; i++)
        {
            expr = Binary(
                SyntaxKind.LogicalAndExpression,
                expr,
                GenerateSingleFieldEquals(fields[i]));
        }

        return expr;
    }

    private ExpressionSyntax GenerateSingleFieldEquals(VariableSymbol field)
    {
        var propName = GetCodeGenInfo(field)?.CSharpName
            ?? NameCasing.ResolveField(field.Name, field.IsNameBacktickEscaped);

        // Equals(this.Field, other.Field)
        return InvocationExpression(
            IdentifierName("Equals"),
            ArgumentList(SeparatedList(new[]
            {
                Argument(IdentifierName(propName)),
                Argument(MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("other"),
                    IdentifierName(propName)))
            })));
    }

    /// <summary>
    /// Generates override int GetHashCode() for a @dataclass.
    /// Pattern: return HashCode.Combine(F1, F2, ...);
    /// </summary>
    private MethodDeclarationSyntax GenerateDataclassGetHashCode(IReadOnlyList<VariableSymbol> fields)
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
                    ?? NameCasing.ResolveField(f.Name, f.IsNameBacktickEscaped);
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
                    ?? NameCasing.ResolveField(f.Name, f.IsNameBacktickEscaped);
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
    private static OperatorDeclarationSyntax GenerateDataclassOperatorEquals(TypeSyntax selfType)
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
                    .WithType(NullableType(selfType)),
                Parameter(Identifier("right"))
                    .WithType(NullableType(selfType)),
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
    private static OperatorDeclarationSyntax GenerateDataclassOperatorNotEquals(TypeSyntax selfType)
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
                    .WithType(NullableType(selfType)),
                Parameter(Identifier("right"))
                    .WithType(NullableType(selfType)),
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
        string originalTypeName, IReadOnlyList<VariableSymbol> fields)
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
                    ?? NameCasing.ResolveField(field.Name, field.IsNameBacktickEscaped);

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
