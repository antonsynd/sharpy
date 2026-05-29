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

    /// <summary>
    /// Generates a module whose top-level statements may carry BlankLines trivia.
    /// Used by trivia-aware property tests to exercise the blank-line round-trip path.
    /// </summary>
    public static Gen<Module> ModuleWithTrivia(GenContext ctx) =>
        Gen.Frequency(
            (3, SimpleModuleWithTrivia(ctx)),
            (2, ModuleWithClassesWithTrivia(ctx)),
            (1, ModuleWithFunctionsWithTrivia(ctx)))
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

    private static Gen<ImmutableArray<Statement>> SimpleModuleWithTrivia(GenContext ctx) =>
        GenStatements.WithOptionalBlankLineTrivia(GenStatements.FunctionDefStmt(ctx))
            .Select(f => ImmutableArray.Create<Statement>(f));

    private static Gen<ImmutableArray<Statement>> ModuleWithFunctionsWithTrivia(GenContext ctx) =>
        GenStatements.WithOptionalBlankLineTrivia(GenStatements.FunctionDefStmt(ctx)).Array[1, 3]
            .Select(fns => fns.Cast<Statement>().ToImmutableArray());

    private static Gen<ImmutableArray<Statement>> ModuleWithClassesWithTrivia(GenContext ctx) =>
        Gen.Select(
            GenStatements.WithOptionalBlankLineTrivia(GenStatements.ClassDefStmt(ctx)),
            GenStatements.WithOptionalBlankLineTrivia(GenStatements.FunctionDefStmt(ctx)),
            (cls, fn) => ImmutableArray.Create<Statement>(cls, fn));
}
