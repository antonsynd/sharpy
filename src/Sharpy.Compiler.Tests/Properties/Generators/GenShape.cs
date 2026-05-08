using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenShape
{
    public static Gen<Module> LinearProgram(GenContext ctx) =>
        GenStatements.FunctionDefStmt(ctx with { Fuel = 2 }).Select(f =>
        {
            var body = ImmutableArray.CreateBuilder<Statement>();
            body.AddRange(f.Body);
            return new Module
            {
                Body = ImmutableArray.Create<Statement>(f with
                {
                    Name = "main",
                    Body = body.ToImmutable()
                })
            };
        });

    public static Gen<Module> ClassHierarchy(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.ClassName,
            GenIdentifier.ClassName,
            GenStatements.FunctionDefStmt(ctx.Burn() with { InClass = true }),
            GenStatements.FunctionDefStmt(ctx.Burn() with { InClass = true }),
            (baseName, childName, baseMethod, childMethod) => new Module
            {
                Body = ImmutableArray.Create<Statement>(
                    new ClassDef
                    {
                        Name = baseName,
                        Body = ImmutableArray.Create<Statement>(baseMethod)
                    },
                    new ClassDef
                    {
                        Name = childName,
                        BaseClasses = ImmutableArray.Create(new TypeAnnotation { Name = baseName }),
                        Body = ImmutableArray.Create<Statement>(childMethod)
                    })
            });

    public static Gen<Module> ComprehensionProgram(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name,
            GenExpressions.IdentifierExpr(ctx).Select(x => (Expression)x),
            GenExpressions.Expression(ctx.Burn()),
            (varName, iter, body) => new Module
            {
                Body = ImmutableArray.Create<Statement>(
                    new FunctionDef
                    {
                        Name = "main",
                        Body = ImmutableArray.Create<Statement>(
                            new VariableDeclaration
                            {
                                Name = "result",
                                Type = new TypeAnnotation
                                {
                                    Name = "list",
                                    TypeArguments = ImmutableArray.Create(new TypeAnnotation { Name = "int" })
                                },
                                InitialValue = new ListComprehension
                                {
                                    Element = body,
                                    Clauses = ImmutableArray.Create<ComprehensionClause>(
                                        new ForClause
                                        {
                                            Target = new Identifier { Name = varName },
                                            Iterator = iter
                                        })
                                }
                            })
                    })
            });
}
