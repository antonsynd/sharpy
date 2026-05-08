using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenModule
{
    public static Gen<Module> Module(GenContext ctx) =>
        Gen.Frequency(
            (3, SimpleModule(ctx)),
            (2, ModuleWithClasses(ctx)),
            (1, ModuleWithFunctions(ctx)))
        .Select(stmts => new Module { Body = stmts });

    private static Gen<ImmutableArray<Statement>> SimpleModule(GenContext ctx) =>
        GenStatements.FunctionDefStmt(ctx).Select(f =>
            ImmutableArray.Create<Statement>(f));

    private static Gen<ImmutableArray<Statement>> ModuleWithFunctions(GenContext ctx) =>
        GenStatements.FunctionDefStmt(ctx).Array[1, 3]
            .Select(fns => fns.Cast<Statement>().ToImmutableArray());

    private static Gen<ImmutableArray<Statement>> ModuleWithClasses(GenContext ctx) =>
        Gen.Select(
            GenStatements.ClassDefStmt(ctx),
            GenStatements.FunctionDefStmt(ctx),
            (cls, fn) => ImmutableArray.Create<Statement>(cls, fn));
}
