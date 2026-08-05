using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Control flow validator using CFG analysis.
/// Provides accurate analysis using CFG-based graph traversal.
/// </summary>
/// <remarks>
/// This validator builds a Control Flow Graph for each function and uses
/// graph-based analysis for:
/// - Unreachable code detection (via reachability analysis)
/// - Missing return path detection (via exit reachability)
/// - Break/continue validation (via loop context tracking)
/// </remarks>
internal class ControlFlowValidator : ValidatingAstWalker
{
    public override string Name => "ControlFlowValidator";
    public override int Order => 400; // After type checking (300)

    private ICompilerLogger _logger = NullLogger.Instance;
    private HashSet<MatchStatement>? _exhaustiveMatches;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        _exhaustiveMatches = PreComputeExhaustiveMatches(module, context.SemanticInfo);
        _logger.LogDebug("Starting CFG-based control flow validation");
        base.Validate(module, context);
    }

    /// <summary>
    /// Pre-computes which match statements are semantically exhaustive over finite types.
    /// This decouples the CFG builder from SemanticInfo.
    /// </summary>
    private static HashSet<MatchStatement>? PreComputeExhaustiveMatches(
        Module module, SemanticInfo semanticInfo)
    {
        var collector = new MatchStatementCollector();
        collector.Visit(module);

        if (collector.MatchStatements.Count == 0)
            return null;

        var exhaustive = new HashSet<MatchStatement>(ReferenceEqualityComparer.Instance);
        foreach (var stmt in collector.MatchStatements)
        {
            var scrutineeType = semanticInfo.GetExpressionType(stmt.Scrutinee);
            if (scrutineeType == null)
                continue;

            if (ExhaustivenessHelper.IsExhaustiveMatch(
                scrutineeType,
                stmt.Cases.Select(c => (c.Pattern, c.Guard)),
                semanticInfo))
            {
                exhaustive.Add(stmt);
            }
        }

        return exhaustive.Count > 0 ? exhaustive : null;
    }

    private class MatchStatementCollector : AstVisitor
    {
        public List<MatchStatement> MatchStatements { get; } = new();

        public override void VisitMatchStatement(MatchStatement node)
        {
            MatchStatements.Add(node);
            DefaultVisit(node);
        }
    }

    public override void VisitFunctionDef(FunctionDef node)
    {
        ValidateFunction(node);
        // base.VisitFunctionDef traverses the body, finding nested FunctionDefs
        base.VisitFunctionDef(node);
    }

    private void ValidateFunction(FunctionDef func)
    {
        _logger.LogDebug($"Building CFG for function: {func.Name}");

        // Skip abstract methods
        if (func.Decorators.Any(d => d.Name == DecoratorNames.Abstract))
            return;

        // Skip stub bodies (ellipsis only)
        if (AstHelper.IsEllipsisStubBody(func.Body))
            return;

        // Build CFG (shared cache; pruning by exhaustive matches is this validator's concern only)
        var cfg = Context.ControlFlowGraphs.GetOrBuildPruned(func, _exhaustiveMatches);

        // 1. Check for unreachable code (warning, not error)
        var unreachable = ControlFlowAnalysis.FindUnreachableCode(cfg);
        foreach (var info in unreachable)
        {
            AddWarning("Unreachable code detected",
                info.FirstUnreachableStatement.LineStart,
                info.FirstUnreachableStatement.ColumnStart, code: DiagnosticCodes.Validation.UnreachableCodeWarning,
                span: info.FirstUnreachableStatement.Span);
        }

        // 2. Check return paths (if function has return type, and not a generator)
        var returnType = GetFunctionReturnType(func);
        if (returnType != SemanticType.Void && !Context.SemanticInfo.IsGenerator(func))
        {
            var missingReturnBlocks = ControlFlowAnalysis.FindMissingReturnPaths(cfg);
            if (missingReturnBlocks.Length > 0)
            {
                AddError(
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
            AddError(error.Message,
                error.Statement.LineStart, error.Statement.ColumnStart, code: code,
                span: error.Statement.Span);
        }

        // 4. For async functions, identify async state regions
        if (func.IsAsync)
        {
            var regions = ControlFlowAnalysis.IdentifyAsyncRegions(cfg);
            _logger.LogDebug($"Async function '{func.Name}': {regions.Length} state region(s), " +
                $"{regions.Count(r => r.AwaitExpression != null)} await point(s)");
        }
    }

    private SemanticType GetFunctionReturnType(FunctionDef func)
    {
        if (func.ReturnType == null)
            return SemanticType.Void;

        // Try cached type first
        var cachedType = Context.SemanticInfo.GetTypeAnnotation(func.ReturnType);
        if (cachedType != null)
            return cachedType;

        // Fall back to resolving
        return Context.TypeResolver.ResolveTypeAnnotation(func.ReturnType);
    }
}
