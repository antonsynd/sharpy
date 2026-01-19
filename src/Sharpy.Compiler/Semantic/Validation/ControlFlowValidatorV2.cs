using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates control flow in Sharpy code:
/// - Detects unreachable code
/// - Validates return paths
/// - Ensures break/continue are only in loops
///
/// This is the pipeline-compatible version of ControlFlowValidator.
/// </summary>
public class ControlFlowValidatorV2 : SemanticValidatorBase
{
    public override string Name => "ControlFlowValidator";
    public override int Order => 400; // After type checking (300)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting control flow validation");

        foreach (var stmt in module.Body)
        {
            ValidateTopLevelStatement(stmt);
        }
    }

    private void ValidateTopLevelStatement(Statement stmt)
    {
        switch (stmt)
        {
            case FunctionDef funcDef:
                ValidateFunction(funcDef);
                break;
            case ClassDef classDef:
                ValidateClass(classDef);
                break;
            case StructDef structDef:
                ValidateStruct(structDef);
                break;
                // Other top-level statements don't need control flow validation
        }
    }

    private void ValidateClass(ClassDef classDef)
    {
        foreach (var member in classDef.Body)
        {
            if (member is FunctionDef methodDef)
            {
                ValidateFunction(methodDef);
            }
        }
    }

    private void ValidateStruct(StructDef structDef)
    {
        foreach (var member in structDef.Body)
        {
            if (member is FunctionDef methodDef)
            {
                ValidateFunction(methodDef);
            }
        }
    }

    private void ValidateFunction(FunctionDef funcDef)
    {
        _logger.LogDebug($"Validating control flow for function: {funcDef.Name}");

        // Skip control flow validation for abstract methods
        bool hasAbstractDecorator = funcDef.Decorators.Any(d => d.Name == "abstract");
        bool hasEllipsisBody = funcDef.Body.Count == 1
            && funcDef.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };

        if (hasAbstractDecorator || hasEllipsisBody)
        {
            return;
        }

        // Get return type from type annotation
        var returnType = GetFunctionReturnType(funcDef);

        var (alwaysReturns, _) = ValidateBlock(funcDef.Body, loopDepth: 0);

        if (returnType != SemanticType.Void && !alwaysReturns)
        {
            AddError(_context,
                $"Function '{funcDef.Name}' must return a value of type '{returnType.GetDisplayName()}' in all code paths",
                funcDef.LineStart, funcDef.ColumnStart);
        }
    }

    private SemanticType GetFunctionReturnType(FunctionDef funcDef)
    {
        if (funcDef.ReturnType == null)
            return SemanticType.Void;

        // Try to get from semantic info cache first
        var cachedType = _context.SemanticInfo.GetTypeAnnotation(funcDef.ReturnType);
        if (cachedType != null)
            return cachedType;

        // Fall back to resolving the type annotation
        return _context.TypeResolver.ResolveTypeAnnotation(funcDef.ReturnType);
    }

    /// <summary>
    /// Validates a block of statements.
    /// Returns (alwaysReturns, hasUnreachableCode).
    /// </summary>
    private (bool, bool) ValidateBlock(List<Statement> statements, int loopDepth)
    {
        bool alwaysReturns = false;
        bool alwaysExits = false;
        bool hasUnreachableCode = false;

        for (int i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];

            if (alwaysExits && i < statements.Count)
            {
                if (!hasUnreachableCode)
                {
                    AddError(_context, "Unreachable code detected",
                        statement.LineStart, statement.ColumnStart);
                    hasUnreachableCode = true;
                }
                continue;
            }

            var (stmtReturns, stmtExits) = ValidateStatement(statement, loopDepth);

            if (stmtReturns) alwaysReturns = true;
            if (stmtExits) alwaysExits = true;
        }

        return (alwaysReturns, hasUnreachableCode);
    }

    /// <summary>
    /// Validates a single statement.
    /// Returns (alwaysReturns, alwaysExits).
    /// </summary>
    private (bool, bool) ValidateStatement(Statement statement, int loopDepth)
    {
        switch (statement)
        {
            case ReturnStatement:
                return (true, true);

            case RaiseStatement:
                return (false, true);

            case BreakStatement:
                if (loopDepth == 0)
                {
                    AddError(_context, "'break' statement outside loop",
                        statement.LineStart, statement.ColumnStart);
                }
                return (false, true);

            case ContinueStatement:
                if (loopDepth == 0)
                {
                    AddError(_context, "'continue' statement outside loop",
                        statement.LineStart, statement.ColumnStart);
                }
                return (false, true);

            case IfStatement ifStmt:
                return ValidateIf(ifStmt, loopDepth);

            case WhileStatement whileStmt:
                return ValidateWhile(whileStmt, loopDepth);

            case ForStatement forStmt:
                return ValidateFor(forStmt, loopDepth);

            case TryStatement tryStmt:
                return ValidateTry(tryStmt, loopDepth);

            case FunctionDef:
                // Nested function validated separately
                return (false, false);

            case ClassDef:
            case StructDef:
            case InterfaceDef:
            case EnumDef:
                return (false, false);

            default:
                return (false, false);
        }
    }

    private (bool, bool) ValidateIf(IfStatement ifStmt, int loopDepth)
    {
        var (thenReturns, _) = ValidateBlock(ifStmt.ThenBody, loopDepth);
        bool allBranchesReturn = thenReturns;

        foreach (var elifClause in ifStmt.ElifClauses)
        {
            var (elifReturns, _) = ValidateBlock(elifClause.Body, loopDepth);
            allBranchesReturn = allBranchesReturn && elifReturns;
        }

        if (ifStmt.ElseBody != null && ifStmt.ElseBody.Count > 0)
        {
            var (elseReturns, _) = ValidateBlock(ifStmt.ElseBody, loopDepth);
            allBranchesReturn = allBranchesReturn && elseReturns;
        }
        else
        {
            allBranchesReturn = false;
        }

        return (allBranchesReturn, allBranchesReturn);
    }

    private (bool, bool) ValidateWhile(WhileStatement whileStmt, int loopDepth)
    {
        ValidateBlock(whileStmt.Body, loopDepth + 1);
        return (false, false); // Loop doesn't guarantee execution
    }

    private (bool, bool) ValidateFor(ForStatement forStmt, int loopDepth)
    {
        ValidateBlock(forStmt.Body, loopDepth + 1);
        return (false, false); // Loop doesn't guarantee execution
    }

    private (bool, bool) ValidateTry(TryStatement tryStmt, int loopDepth)
    {
        var (tryReturns, _) = ValidateBlock(tryStmt.Body, loopDepth);

        bool allHandlersReturn = true;
        foreach (var handler in tryStmt.Handlers)
        {
            var (handlerReturns, _) = ValidateBlock(handler.Body, loopDepth);
            allHandlersReturn = allHandlersReturn && handlerReturns;
        }

        bool finallyReturns = false;
        if (tryStmt.FinallyBody != null && tryStmt.FinallyBody.Count > 0)
        {
            var (finReturns, _) = ValidateBlock(tryStmt.FinallyBody, loopDepth);
            finallyReturns = finReturns;
        }

        bool allPathsReturn = finallyReturns || (tryReturns && allHandlersReturn);
        return (allPathsReturn, allPathsReturn);
    }
}
