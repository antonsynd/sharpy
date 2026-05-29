using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenSharpy
{
    public static Gen<Module> Module() =>
        Module(GenContext.Default);

    public static Gen<Module> Module(GenContext ctx) =>
        GenModule.Module(ctx);

    public static Gen<Module> ModuleWithTrivia() =>
        ModuleWithTrivia(GenContext.Default);

    public static Gen<Module> ModuleWithTrivia(GenContext ctx) =>
        GenModule.ModuleWithTrivia(ctx);

    public static Gen<Expression> Expression() =>
        Expression(GenContext.Default);

    public static Gen<Expression> Expression(GenContext ctx) =>
        GenExpressions.Expression(ctx);

    public static Gen<Statement> Statement() =>
        Statement(GenContext.Default);

    public static Gen<Statement> Statement(GenContext ctx) =>
        GenStatements.Statement(ctx);

    public static Gen<Pattern> Pattern() =>
        Pattern(GenContext.Default);

    public static Gen<Pattern> Pattern(GenContext ctx) =>
        GenPatterns.Pattern(ctx);

    public static Gen<TypeAnnotation> TypeAnnotation() =>
        TypeAnnotation(2);

    public static Gen<TypeAnnotation> TypeAnnotation(int fuel) =>
        GenTypes.Annotation(fuel);
}
