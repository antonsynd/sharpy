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

        // Two passes (#1839, R-AI): a read unassigned even when suppression edges are ignored is a
        // genuine violation (refused, SPY0600); a read assigned then but not when they are honoured
        // is runtime-checked (an UnboundLocalError at runtime, not refused).
        var result = DefiniteAssignmentAnalysis.Analyze(cfg);

        foreach (var v in result.Violations)
        {
            AddError(
                $"Variable '{v.ReadSite.Name}' is used before being assigned",
                v.ReadSite.LineStart, v.ReadSite.ColumnStart,
                code: DiagnosticCodes.SemanticOverflow.UseBeforeAssignment,
                span: v.ReadSite.Span);
        }

        // A runtime-checked read is lowered to Builtins.CheckedLocal by the emitter.
        foreach (var v in result.RuntimeChecked)
            Context.SemanticInfo.RecordRuntimeCheckedRead(v.ReadSite, v.Declaration);

        var bareDecls = DefiniteAssignmentAnalysis.FindBareDeclarations(cfg);
        if (bareDecls.Count > 0)
        {
            var violatedNames = new HashSet<string>();
            foreach (var v in result.Violations)
                violatedNames.Add(v.ReadSite.Name);

            foreach (var (name, decl) in bareDecls)
            {
                // A flagged local has no GENUINE violation, so it is still recorded
                // definitely-assigned — the emitter's `default!` arm fires (T n = default!) and the
                // assigned-flag is declared beside it (verifier note, DD4).
                if (!violatedNames.Contains(name))
                    Context.SemanticInfo.RecordDefinitelyAssignedBareLocal(decl);

                // Flag the symbol so LocalNameAllocator sets CodeGenInfo.HasRuntimeAssignedFlag.
                if (result.FlaggedNames.Contains(name)
                    && Context.SemanticInfo.GetDeclarationSymbol(decl) is VariableSymbol symbol)
                {
                    Context.SemanticInfo.RecordRuntimeAssignedFlagLocal(symbol);
                }
            }

            // A nested `def` is skipped by the flow CFG (it runs when CALLED), so its reads of a
            // flagged ENCLOSING local are collected here instead — runtime-checked, like a lambda's
            // (#1839). Lambdas are already handled inside the flow analysis.
            if (result.FlaggedNames.Count > 0)
            {
                foreach (var stmt in func.Body)
                    RecordNestedDefFlaggedReads(stmt, result.FlaggedNames, bareDecls);
            }
        }
    }

    /// <summary>
    /// Records every read of a flagged local that occurs inside a nested <c>def</c> body as
    /// runtime-checked (#1839), recursing into further nested defs. Does not descend into lambda
    /// bodies (the flow analysis already judged those).
    /// </summary>
    private void RecordNestedDefFlaggedReads(
        Node node,
        IReadOnlyCollection<string> flagged,
        IReadOnlyDictionary<string, VariableDeclaration> bareDecls)
    {
        if (node is FunctionDef nestedDef)
        {
            foreach (var bodyStmt in nestedDef.Body)
                RecordFlaggedReadsIn(bodyStmt, flagged, bareDecls);
            return;
        }

        foreach (var child in node.GetChildNodes())
            RecordNestedDefFlaggedReads(child, flagged, bareDecls);
    }

    private void RecordFlaggedReadsIn(
        Node node,
        IReadOnlyCollection<string> flagged,
        IReadOnlyDictionary<string, VariableDeclaration> bareDecls)
    {
        if (node is Identifier id && flagged.Contains(id.Name) && bareDecls.TryGetValue(id.Name, out var decl))
            Context.SemanticInfo.RecordRuntimeCheckedRead(id, decl);

        foreach (var child in node.GetChildNodes())
            RecordFlaggedReadsIn(child, flagged, bareDecls);
    }
}
