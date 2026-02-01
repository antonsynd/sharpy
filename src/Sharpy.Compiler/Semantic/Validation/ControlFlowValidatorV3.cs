using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Control flow validator using CFG analysis.
/// Provides more accurate analysis than the AST-walking V2 validator.
/// </summary>
/// <remarks>
/// This validator builds a Control Flow Graph for each function and uses
/// graph-based analysis for:
/// - Unreachable code detection (via reachability analysis)
/// - Missing return path detection (via exit reachability)
/// - Break/continue validation (via loop context tracking)
/// </remarks>
public class ControlFlowValidatorV3 : SemanticValidatorBase
{
    public override string Name => "ControlFlowValidatorV3";
    public override int Order => 400; // Same slot as V2 - use one or the other

    private readonly ControlFlowGraphBuilder _cfgBuilder = new();
    private SemanticContext _context = null!;
    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting CFG-based control flow validation");

        foreach (var stmt in module.Body)
        {
            ValidateTopLevelStatement(stmt);
        }
    }

    private void ValidateTopLevelStatement(Statement stmt)
    {
        switch (stmt)
        {
            case FunctionDef func:
                ValidateFunction(func);
                break;

            case ClassDef cls:
                foreach (var member in cls.Body)
                    if (member is FunctionDef method)
                        ValidateFunction(method);
                break;

            case StructDef str:
                foreach (var member in str.Body)
                    if (member is FunctionDef method)
                        ValidateFunction(method);
                break;
        }
    }

    private void ValidateFunction(FunctionDef func)
    {
        _logger.LogDebug($"Building CFG for function: {func.Name}");

        // Skip abstract methods
        if (func.Decorators.Any(d => d.Name == "abstract"))
            return;

        // Skip stub bodies (ellipsis only)
        if (func.Body.Length == 1 && func.Body[0] is ExpressionStatement { Expression: EllipsisLiteral })
            return;

        // Build CFG
        var cfg = _cfgBuilder.Build(func);

        // 1. Check for unreachable code (warning, not error)
        var unreachable = ControlFlowAnalysis.FindUnreachableCode(cfg);
        foreach (var info in unreachable)
        {
            AddWarning(_context, "Unreachable code detected",
                info.FirstUnreachableStatement.LineStart,
                info.FirstUnreachableStatement.ColumnStart, code: DiagnosticCodes.Validation.UnreachableCodeWarning,
                span: info.FirstUnreachableStatement.Span);
        }

        // 2. Check return paths (if function has return type)
        var returnType = GetFunctionReturnType(func);
        if (returnType != SemanticType.Void)
        {
            var missingReturnBlocks = ControlFlowAnalysis.FindMissingReturnPaths(cfg);
            if (missingReturnBlocks.Length > 0)
            {
                AddError(_context,
                    $"Function '{func.Name}' must return a value of type '{returnType.GetDisplayName()}' in all code paths",
                    func.LineStart, func.ColumnStart, code: DiagnosticCodes.Semantic.NotAllPathsReturn,
                    span: func.Span);
            }
        }

        // 3. Validate loop control flow (break/continue outside loops)
        var loopErrors = ControlFlowAnalysis.ValidateLoopControlFlow(cfg);
        foreach (var error in loopErrors)
        {
            var code = error.Statement is BreakStatement
                ? DiagnosticCodes.Semantic.BreakOutsideLoop
                : DiagnosticCodes.Semantic.ContinueOutsideLoop;
            AddError(_context, error.Message,
                error.Statement.LineStart, error.Statement.ColumnStart, code: code,
                span: error.Statement.Span);
        }

        // 4. Recursively validate nested functions
        ValidateNestedFunctions(func.Body);
    }

    private void ValidateNestedFunctions(System.Collections.Immutable.ImmutableArray<Statement> statements)
    {
        foreach (var stmt in statements)
        {
            if (stmt is FunctionDef nestedFunc)
                ValidateFunction(nestedFunc);
            else if (stmt is IfStatement ifStmt)
            {
                ValidateNestedFunctions(ifStmt.ThenBody);
                foreach (var elif in ifStmt.ElifClauses)
                    ValidateNestedFunctions(elif.Body);
                ValidateNestedFunctions(ifStmt.ElseBody);
            }
            else if (stmt is WhileStatement whileStmt)
            {
                ValidateNestedFunctions(whileStmt.Body);
                ValidateNestedFunctions(whileStmt.ElseBody);
            }
            else if (stmt is ForStatement forStmt)
            {
                ValidateNestedFunctions(forStmt.Body);
                ValidateNestedFunctions(forStmt.ElseBody);
            }
            else if (stmt is TryStatement tryStmt)
            {
                ValidateNestedFunctions(tryStmt.Body);
                foreach (var handler in tryStmt.Handlers)
                    ValidateNestedFunctions(handler.Body);
                ValidateNestedFunctions(tryStmt.ElseBody);
                ValidateNestedFunctions(tryStmt.FinallyBody);
            }
        }
    }

    private SemanticType GetFunctionReturnType(FunctionDef func)
    {
        if (func.ReturnType == null)
            return SemanticType.Void;

        // Try cached type first
        var cachedType = _context.SemanticInfo.GetTypeAnnotation(func.ReturnType);
        if (cachedType != null)
            return cachedType;

        // Fall back to resolving
        return _context.TypeResolver.ResolveTypeAnnotation(func.ReturnType);
    }
}
