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

    public RoslynEmitter(CodeGenContext context)
    {
        _context = context;
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
            Return ret => GenerateReturn(ret),
            Assign assign => GenerateAssignment(assign),
            // Add more statement types...
            _ => null
        };
    }

    private MethodDeclarationSyntax GenerateFunctionDeclaration(FunctionDef func)
    {
        var mangledName = NameMangler.ToPascalCase(func.Name);
        var returnType = PredefinedType(Token(SyntaxKind.VoidKeyword)); // TODO: Infer return type

        var modifiers = func.AccessModifier switch
        {
            "public" => TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)),
            "private" => TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)),
            _ => TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
        };

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
            Return ret => GenerateReturn(ret),
            Assign assign => GenerateAssignment(assign),
            ExpressionStatement exprStmt => ExpressionStatement(GenerateExpression(exprStmt.Expression)),
            // Add more...
            _ => null
        };
    }

    private ReturnStatementSyntax GenerateReturn(Return ret)
    {
        if (ret.Value != null)
        {
            return ReturnStatement(GenerateExpression(ret.Value));
        }
        return ReturnStatement();
    }

    private LocalDeclarationStatementSyntax GenerateAssignment(Assign assign)
    {
        if (assign.Target is Name name)
        {
            var varName = NameMangler.ToCamelCase(name.Id);
            var value = GenerateExpression(assign.Value);

            var declaration = VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(varName))
                        .WithInitializer(EqualsValueClause(value))));

            return LocalDeclarationStatement(declaration);
        }

        throw new NotImplementedException("Complex assignment targets not yet supported");
    }

    private ExpressionSyntax GenerateExpression(Sharpy.Compiler.Parser.Ast.Expression expr)
    {
        return expr switch
        {
            Constant constant => GenerateConstant(constant),
            Name name => IdentifierName(NameMangler.ToCamelCase(name.Id)),
            Call call => GenerateCall(call),
            BinaryOp binOp => GenerateBinaryOp(binOp),
            // Add more...
            _ => throw new NotImplementedException($"Expression type not implemented: {expr.GetType().Name}")
        };
    }

    private ExpressionSyntax GenerateConstant(Constant constant)
    {
        return constant.Value switch
        {
            null => LiteralExpression(SyntaxKind.NullLiteralExpression),
            int i => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i)),
            string s => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(s)),
            bool b => LiteralExpression(b ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            _ => throw new NotImplementedException($"Constant type not implemented: {constant.Value?.GetType().Name}")
        };
    }

    private ExpressionSyntax GenerateCall(Call call)
    {
        if (call.Function is Name funcName)
        {
            var name = _context.IsBuiltinFunction(funcName.Id)
                ? $"Sharpy.Exports.{NameMangler.ToPascalCase(funcName.Id)}"
                : NameMangler.ToPascalCase(funcName.Id);

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

        var kind = binOp.Operator switch
        {
            BinaryOperator.Add => SyntaxKind.AddExpression,
            BinaryOperator.Sub => SyntaxKind.SubtractExpression,
            BinaryOperator.Mult => SyntaxKind.MultiplyExpression,
            BinaryOperator.Div => SyntaxKind.DivideExpression,
            _ => throw new NotImplementedException($"Binary operator not implemented: {binOp.Operator}")
        };

        return BinaryExpression(kind, left, right);
    }
}
