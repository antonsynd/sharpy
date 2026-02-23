using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates generator-related constraints:
/// - yield in __next__ is forbidden (SPY0268)
/// - Generator __iter__ conflicts with __next__ (SPY0269)
/// - yield + return-with-value in the same function (SPY0267)
/// - yield in try block with catch/except handlers (SPY0270)
/// </summary>
internal class GeneratorValidator : SemanticValidatorBase
{
    public override string Name => "GeneratorValidator";
    public override int Order => 155; // After SignatureValidator (150)

    public override void Validate(Module module, SemanticContext context)
    {
        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    ValidateClassGenerators(classDef, context);
                    break;
                case StructDef structDef:
                    ValidateStructGenerators(structDef, context);
                    break;
            }

            // Check top-level functions
            if (stmt is FunctionDef funcDef)
            {
                ValidateGeneratorFunction(funcDef, context);
            }
        }
    }

    private void ValidateClassGenerators(ClassDef classDef, SemanticContext context)
    {
        FunctionDef? iterFunc = null;
        FunctionDef? nextFunc = null;

        foreach (var member in classDef.Body)
        {
            if (member is FunctionDef funcDef)
            {
                if (funcDef.Name == DunderNames.Iter)
                    iterFunc = funcDef;
                else if (funcDef.Name == DunderNames.Next)
                    nextFunc = funcDef;

                ValidateGeneratorFunction(funcDef, context);
            }
        }

        // Guard 1: Generator __iter__ + __next__ conflict (SPY0269)
        if (iterFunc != null && nextFunc != null && context.SemanticInfo.IsGenerator(iterFunc))
        {
            AddError(context,
                $"Class '{classDef.Name}' cannot have both a generator '__iter__' and '__next__'; " +
                "choose either generator-based or explicit iterator pattern",
                classDef.LineStart, classDef.ColumnStart,
                code: DiagnosticCodes.Semantic.GeneratorIterConflict,
                span: classDef.Span);
        }
    }

    private void ValidateStructGenerators(StructDef structDef, SemanticContext context)
    {
        foreach (var member in structDef.Body)
        {
            if (member is FunctionDef funcDef)
            {
                ValidateGeneratorFunction(funcDef, context);
            }
        }
    }

    private void ValidateGeneratorFunction(FunctionDef funcDef, SemanticContext context)
    {
        if (!context.SemanticInfo.IsGenerator(funcDef))
            return;

        // Guard 2: yield in __next__ is forbidden (SPY0268)
        if (funcDef.Name == DunderNames.Next)
        {
            AddError(context,
                "'__next__' cannot contain 'yield'; use '__iter__' for generator-based iteration",
                funcDef.LineStart, funcDef.ColumnStart,
                code: DiagnosticCodes.Semantic.YieldInNext,
                span: funcDef.Span);
        }

        // Guard 3: yield + return-with-value in the same function (SPY0267)
        var returnWithValue = FindReturnWithValue(funcDef.Body);
        if (returnWithValue != null)
        {
            AddError(context,
                "Cannot use 'return' with a value in a generator function; " +
                "use 'yield' to produce values or bare 'return' to stop",
                returnWithValue.LineStart, returnWithValue.ColumnStart,
                code: DiagnosticCodes.Semantic.YieldWithReturn,
                span: returnWithValue.Span);
        }

        // Guard 4: yield in try/except is forbidden (SPY0270)
        var yieldInTry = FindYieldInTryExcept(funcDef.Body);
        if (yieldInTry != null)
        {
            AddError(context,
                "'yield' cannot be used inside a 'try' block that has 'except' handlers; " +
                "move the 'yield' outside the try/except or use try/finally instead",
                yieldInTry.LineStart, yieldInTry.ColumnStart,
                code: DiagnosticCodes.Semantic.YieldInTryExcept,
                span: yieldInTry.Span);
        }
    }

    private static ReturnStatement? FindReturnWithValue(ImmutableArray<Statement> statements)
        => StatementWalker.FirstOrDefault(statements,
            stmt => stmt is ReturnStatement ret && ret.Value != null ? ret : null);

    private static YieldStatement? FindYieldInTryExcept(ImmutableArray<Statement> statements)
    {
        return StatementWalker.FirstOrDefault(statements, stmt =>
        {
            if (stmt is TryStatement tryStmt && tryStmt.Handlers.Length > 0)
            {
                return StatementWalker.FirstOrDefault(tryStmt.Body,
                    inner => inner is YieldStatement ys ? ys : null);
            }
            return null;
        });
    }
}
