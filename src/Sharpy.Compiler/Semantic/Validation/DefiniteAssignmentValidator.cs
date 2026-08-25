using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Detects use-before-assign on bare-declared local variables (<c>x: int</c> with no initializer).
/// Uses the #1042 CFG engine and forward dataflow analysis (#1559).
/// </summary>
internal class DefiniteAssignmentValidator : ValidatingAstWalker
{
    public override string Name => "DefiniteAssignmentValidator";
    public override int Order => 402;

    public override void VisitFunctionDef(FunctionDef node)
    {
        ValidateFunction(node);
        base.VisitFunctionDef(node);
    }

    private void ValidateFunction(FunctionDef func)
    {
        if (MemberClassification.HasAbstractDecorator(func.Decorators))
            return;

        if (AstHelper.IsEllipsisStubBody(func.Body))
            return;

        var cfg = Context.ControlFlowGraphs.GetOrBuild(func);

        var violations = DefiniteAssignmentAnalysis.FindViolations(cfg);
        foreach (var v in violations)
        {
            AddError(
                $"Variable '{v.ReadSite.Name}' is used before being assigned",
                v.ReadSite.LineStart, v.ReadSite.ColumnStart,
                code: DiagnosticCodes.SemanticOverflow.UseBeforeAssignment,
                span: v.ReadSite.Span);
        }
    }
}
