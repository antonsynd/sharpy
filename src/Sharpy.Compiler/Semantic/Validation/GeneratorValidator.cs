using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates generator function constraints:
/// - yield + return &lt;value&gt; in same function (SPY0267)
/// - yield inside __next__ (SPY0268)
/// - generator __iter__ + __next__ on same class (SPY0269)
/// - yield inside try/except (C# limitation)
/// </summary>
internal class GeneratorValidator : SemanticValidatorBase
{
    public override string Name => "GeneratorValidator";
    public override int Order => 155; // After SignatureValidator (150)

    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;

        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    ValidateClass(classDef);
                    break;
                case StructDef structDef:
                    ValidateStruct(structDef);
                    break;
                case FunctionDef funcDef:
                    ValidateFunction(funcDef);
                    break;
            }
        }
    }

    private void ValidateClass(ClassDef classDef)
    {
        ValidateTypeMembers(classDef.Name, classDef.Body);
    }

    private void ValidateStruct(StructDef structDef)
    {
        ValidateTypeMembers(structDef.Name, structDef.Body);
    }

    private void ValidateTypeMembers(string typeName, IReadOnlyList<Statement> body)
    {
        FunctionDef? iterMethod = null;
        FunctionDef? nextMethod = null;
        bool iterIsGenerator = false;

        foreach (var member in body)
        {
            if (member is FunctionDef funcDef)
            {
                if (funcDef.Name == DunderNames.Iter)
                {
                    iterMethod = funcDef;
                    iterIsGenerator = _context.SemanticInfo.IsGenerator(funcDef);
                }
                else if (funcDef.Name == DunderNames.Next)
                {
                    nextMethod = funcDef;
                }

                // Validate each method independently
                ValidateFunction(funcDef);
            }
        }

        // Guard 1: Generator __iter__ + __next__ on same class
        if (iterIsGenerator && nextMethod != null)
        {
            AddError(_context,
                $"Class '{typeName}' cannot have both a generator '__iter__' and '__next__'; " +
                "choose either generator-based or explicit iterator pattern",
                iterMethod!.LineStart, iterMethod.ColumnStart,
                code: DiagnosticCodes.Semantic.GeneratorIterConflict,
                span: iterMethod.Span);
        }

        // Guard 2: yield in __next__
        if (nextMethod != null && _context.SemanticInfo.IsGenerator(nextMethod))
        {
            AddError(_context,
                "'__next__' cannot contain 'yield'; use '__iter__' for generator-based iteration",
                nextMethod.LineStart, nextMethod.ColumnStart,
                code: DiagnosticCodes.Semantic.YieldInNext,
                span: nextMethod.Span);
        }
    }

    private void ValidateFunction(FunctionDef funcDef)
    {
        if (!_context.SemanticInfo.IsGenerator(funcDef))
            return;

        // Guard 3: yield + return <value> in same generator function
        if (ContainsReturnWithValue(funcDef.Body))
        {
            // Find the first return-with-value for error location
            var returnStmt = FindFirstReturnWithValue(funcDef.Body);
            if (returnStmt != null)
            {
                AddError(_context,
                    "Cannot use 'return' with a value in a generator function; " +
                    "use 'yield' to produce values or bare 'return' to stop",
                    returnStmt.LineStart, returnStmt.ColumnStart,
                    code: DiagnosticCodes.Semantic.YieldWithReturn,
                    span: returnStmt.Span);
            }
        }

        // Guard 4: yield in try block with catch/except (C# limitation)
        CheckYieldInTryCatch(funcDef.Body);
    }

    /// <summary>
    /// Checks whether a statement list contains a return statement with a non-null value.
    /// Does not descend into nested function definitions.
    /// </summary>
    private static bool ContainsReturnWithValue(System.Collections.Immutable.ImmutableArray<Statement> statements)
    {
        return FindFirstReturnWithValue(statements) != null;
    }

    private static ReturnStatement? FindFirstReturnWithValue(System.Collections.Immutable.ImmutableArray<Statement> statements)
    {
        foreach (var stmt in statements)
        {
            if (stmt is ReturnStatement { Value: not null } ret)
                return ret;

            // Do not descend into nested function definitions
            if (stmt is FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef)
                continue;

            // Recursively check compound statements
            var found = stmt switch
            {
                IfStatement ifStmt => FindFirstReturnWithValue(ifStmt.ThenBody)
                    ?? ifStmt.ElifClauses.Select(e => FindFirstReturnWithValue(e.Body)).FirstOrDefault(r => r != null)
                    ?? FindFirstReturnWithValue(ifStmt.ElseBody),
                WhileStatement whileStmt => FindFirstReturnWithValue(whileStmt.Body),
                ForStatement forStmt => FindFirstReturnWithValue(forStmt.Body),
                TryStatement tryStmt => FindFirstReturnWithValue(tryStmt.Body)
                    ?? tryStmt.Handlers.Select(h => FindFirstReturnWithValue(h.Body)).FirstOrDefault(r => r != null)
                    ?? FindFirstReturnWithValue(tryStmt.ElseBody)
                    ?? (tryStmt.FinallyBody.Length > 0 ? FindFirstReturnWithValue(tryStmt.FinallyBody) : null),
                WithStatement withStmt => FindFirstReturnWithValue(withStmt.Body),
                MatchStatement matchStmt => matchStmt.Cases.Select(c => FindFirstReturnWithValue(c.Body)).FirstOrDefault(r => r != null),
                _ => null
            };

            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Checks for yield statements inside try blocks that have catch/except handlers.
    /// C# does not allow 'yield return' inside a try block with a catch clause.
    /// </summary>
    private void CheckYieldInTryCatch(System.Collections.Immutable.ImmutableArray<Statement> statements)
    {
        foreach (var stmt in statements)
        {
            if (stmt is FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef)
                continue;

            if (stmt is TryStatement tryStmt && tryStmt.Handlers.Length > 0)
            {
                // Check for yield inside the try body (not handlers — those are separate)
                var yieldStmt = FindFirstYield(tryStmt.Body);
                if (yieldStmt != null)
                {
                    AddError(_context,
                        "'yield' cannot be used inside a 'try' block that has 'except' handlers " +
                        "(C# limitation: 'yield return' is not allowed in try/catch)",
                        yieldStmt.LineStart, yieldStmt.ColumnStart,
                        code: DiagnosticCodes.Semantic.YieldWithReturn, // Reuse code for now
                        span: yieldStmt.Span);
                }
            }

            // Recursively check compound statements
            switch (stmt)
            {
                case IfStatement ifStmt:
                    CheckYieldInTryCatch(ifStmt.ThenBody);
                    foreach (var elif in ifStmt.ElifClauses)
                        CheckYieldInTryCatch(elif.Body);
                    CheckYieldInTryCatch(ifStmt.ElseBody);
                    break;
                case WhileStatement whileStmt:
                    CheckYieldInTryCatch(whileStmt.Body);
                    break;
                case ForStatement forStmt:
                    CheckYieldInTryCatch(forStmt.Body);
                    break;
                case TryStatement tryStmt2:
                    CheckYieldInTryCatch(tryStmt2.Body);
                    foreach (var handler in tryStmt2.Handlers)
                        CheckYieldInTryCatch(handler.Body);
                    CheckYieldInTryCatch(tryStmt2.ElseBody);
                    if (tryStmt2.FinallyBody.Length > 0)
                        CheckYieldInTryCatch(tryStmt2.FinallyBody);
                    break;
                case WithStatement withStmt:
                    CheckYieldInTryCatch(withStmt.Body);
                    break;
                case MatchStatement matchStmt:
                    foreach (var matchCase in matchStmt.Cases)
                        CheckYieldInTryCatch(matchCase.Body);
                    break;
            }
        }
    }

    private static YieldStatement? FindFirstYield(System.Collections.Immutable.ImmutableArray<Statement> statements)
    {
        foreach (var stmt in statements)
        {
            if (stmt is YieldStatement yieldStmt)
                return yieldStmt;

            if (stmt is FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef)
                continue;

            var found = stmt switch
            {
                IfStatement ifStmt => FindFirstYield(ifStmt.ThenBody)
                    ?? ifStmt.ElifClauses.Select(e => FindFirstYield(e.Body)).FirstOrDefault(r => r != null)
                    ?? FindFirstYield(ifStmt.ElseBody),
                WhileStatement whileStmt => FindFirstYield(whileStmt.Body),
                ForStatement forStmt => FindFirstYield(forStmt.Body),
                TryStatement tryStmt => FindFirstYield(tryStmt.Body)
                    ?? tryStmt.Handlers.Select(h => FindFirstYield(h.Body)).FirstOrDefault(r => r != null)
                    ?? FindFirstYield(tryStmt.ElseBody)
                    ?? (tryStmt.FinallyBody.Length > 0 ? FindFirstYield(tryStmt.FinallyBody) : null),
                WithStatement withStmt => FindFirstYield(withStmt.Body),
                MatchStatement matchStmt => matchStmt.Cases.Select(c => FindFirstYield(c.Body)).FirstOrDefault(r => r != null),
                _ => null
            };

            if (found != null)
                return found;
        }

        return null;
    }
}
