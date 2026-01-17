using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Validates control flow in Sharpy code:
/// - Detects unreachable code
/// - Validates return paths
/// - Ensures break/continue are only in loops
/// </summary>
public class ControlFlowValidator
{
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    private int _loopDepth = 0;
    private bool _inFunction = false;

    public ControlFlowValidator(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

    /// <summary>
    /// Validates control flow for a function
    /// </summary>
    public void ValidateFunction(FunctionDef functionDef, SemanticType returnType)
    {
        _logger.LogDebug($"Validating control flow for function: {functionDef.Name}");

        // Skip control flow validation for abstract methods - they're just declarations
        // Note: Implicit abstract methods (ellipsis body in @abstract class) are also skipped
        // because we check for ellipsis body below
        bool hasAbstractDecorator = functionDef.Decorators.Any(d => d.Name == "abstract");
        bool hasEllipsisBody = functionDef.Body.Count == 1
            && functionDef.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };

        // Skip for explicit @abstract decorator or for ellipsis-only bodies
        // (ellipsis body in concrete class generates NotImplementedException, also doesn't need return validation)
        if (hasAbstractDecorator || hasEllipsisBody)
        {
            return;
        }

        _inFunction = true;
        var (alwaysReturns, _) = ValidateBlock(functionDef.Body);
        _inFunction = false;

        // Check if function needs to return a value
        if (returnType != SemanticType.Void && !alwaysReturns)
        {
            AddError($"Function '{functionDef.Name}' must return a value of type '{returnType.GetDisplayName()}' in all code paths",
                functionDef.LineStart, functionDef.ColumnStart);
        }
    }

    /// <summary>
    /// Validates a block of statements
    /// Returns (alwaysReturns, hasUnreachableCode)
    /// </summary>
    private (bool, bool) ValidateBlock(List<Statement> statements)
    {
        bool alwaysReturns = false;
        bool alwaysExits = false; // returns, raises, break, continue
        bool hasUnreachableCode = false;

        for (int i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];

            // Check for unreachable code
            if (alwaysExits && i < statements.Count)
            {
                if (!hasUnreachableCode)
                {
                    AddError("Unreachable code detected",
                        statement.LineStart, statement.ColumnStart);
                    hasUnreachableCode = true;
                }
                continue;
            }

            var (stmtReturns, stmtExits) = ValidateStatement(statement);

            if (stmtReturns)
                alwaysReturns = true;

            if (stmtExits)
                alwaysExits = true;
        }

        return (alwaysReturns, hasUnreachableCode);
    }

    /// <summary>
    /// Validates a single statement
    /// Returns (alwaysReturns, alwaysExits)
    /// </summary>
    private (bool, bool) ValidateStatement(Statement statement)
    {
        switch (statement)
        {
            case ReturnStatement:
                return (true, true);

            case RaiseStatement:
                return (false, true);

            case BreakStatement:
                if (_loopDepth == 0)
                {
                    AddError("'break' statement outside loop",
                        statement.LineStart, statement.ColumnStart);
                }
                return (false, true);

            case ContinueStatement:
                if (_loopDepth == 0)
                {
                    AddError("'continue' statement outside loop",
                        statement.LineStart, statement.ColumnStart);
                }
                return (false, true);

            case IfStatement ifStmt:
                return ValidateIf(ifStmt);

            case WhileStatement whileStmt:
                return ValidateWhile(whileStmt);

            case ForStatement forStmt:
                return ValidateFor(forStmt);

            case TryStatement tryStmt:
                return ValidateTry(tryStmt);

            case FunctionDef functionDef:
                // Nested function - don't validate here, it will be validated separately
                return (false, false);

            case ClassDef:
            case StructDef:
            case InterfaceDef:
            case EnumDef:
                // Type definitions don't affect control flow
                return (false, false);

            default:
                return (false, false);
        }
    }

    private (bool, bool) ValidateIf(IfStatement ifStmt)
    {
        var (thenReturns, _) = ValidateBlock(ifStmt.ThenBody);

        bool allBranchesReturn = thenReturns;

        // Check elif branches
        foreach (var elifClause in ifStmt.ElifClauses)
        {
            var (elifReturns, _) = ValidateBlock(elifClause.Body);
            allBranchesReturn = allBranchesReturn && elifReturns;
        }

        // Check else branch
        if (ifStmt.ElseBody != null && ifStmt.ElseBody.Count > 0)
        {
            var (elseReturns, _) = ValidateBlock(ifStmt.ElseBody);
            allBranchesReturn = allBranchesReturn && elseReturns;
        }
        else
        {
            // No else branch means not all paths return
            allBranchesReturn = false;
        }

        return (allBranchesReturn, allBranchesReturn);
    }

    private (bool, bool) ValidateWhile(WhileStatement whileStmt)
    {
        _loopDepth++;
        var (bodyReturns, _) = ValidateBlock(whileStmt.Body);
        _loopDepth--;

        // While loop doesn't guarantee execution, so doesn't always return
        return (false, false);
    }

    private (bool, bool) ValidateFor(ForStatement forStmt)
    {
        _loopDepth++;
        var (bodyReturns, _) = ValidateBlock(forStmt.Body);
        _loopDepth--;

        // For loop doesn't guarantee execution (iterator might be empty)
        return (false, false);
    }

    private (bool, bool) ValidateTry(TryStatement tryStmt)
    {
        var (tryReturns, _) = ValidateBlock(tryStmt.Body);

        bool allHandlersReturn = true;
        foreach (var handler in tryStmt.Handlers)
        {
            var (handlerReturns, _) = ValidateBlock(handler.Body);
            allHandlersReturn = allHandlersReturn && handlerReturns;
        }

        bool finallyReturns = false;
        if (tryStmt.FinallyBody != null && tryStmt.FinallyBody.Count > 0)
        {
            var (finReturns, _) = ValidateBlock(tryStmt.FinallyBody);
            finallyReturns = finReturns;
        }

        // All paths return if:
        // - Finally returns (overrides everything), OR
        // - Try returns AND all handlers return
        bool allPathsReturn = finallyReturns || (tryReturns && allHandlersReturn);

        return (allPathsReturn, allPathsReturn);
    }

    private void AddError(string message, int? line, int? column)
    {
        _errors.Add(new SemanticError(message, line, column));
    }
}
