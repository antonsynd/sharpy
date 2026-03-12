using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Services;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Information about variable usage and control flow within a code selection.
/// Used by extract method to determine parameters and return values.
/// </summary>
public sealed record ScopeInfo
{
    /// <summary>Variables read within the selection that are declared outside it.</summary>
    public IReadOnlySet<string> ReadsFromOuterScope { get; init; } = new HashSet<string>();

    /// <summary>Variables assigned within the selection that are used after it.</summary>
    public IReadOnlySet<string> WritesToOuterScope { get; init; } = new HashSet<string>();

    /// <summary>Variables declared within the selection.</summary>
    public IReadOnlySet<string> DeclaredInScope { get; init; } = new HashSet<string>();

    /// <summary>Whether the selection contains a return statement.</summary>
    public bool ContainsReturn { get; init; }

    /// <summary>Whether the selection contains a break statement.</summary>
    public bool ContainsBreak { get; init; }

    /// <summary>Whether the selection contains a continue statement.</summary>
    public bool ContainsContinue { get; init; }

    /// <summary>Whether the selection contains a yield expression.</summary>
    public bool ContainsYield { get; init; }
}

/// <summary>
/// Analyzes the scope of selected statements to determine captured variables
/// and control flow properties for extract method refactoring.
/// </summary>
internal static class ScopeAnalyzer
{
    /// <summary>
    /// Analyzes the scope of the given statements within their containing function.
    /// </summary>
    /// <param name="selectedStatements">The statements selected for extraction.</param>
    /// <param name="allStatements">All statements in the containing scope (function body).</param>
    /// <param name="query">Semantic query interface for symbol resolution.</param>
    public static ScopeInfo AnalyzeScope(
        IReadOnlyList<Statement> selectedStatements,
        IReadOnlyList<Statement> allStatements,
        ISemanticQuery? query)
    {
        // Walk selected statements to collect reads, declarations, writes, and control flow.
        var selectionVisitor = new SelectionVisitor();
        foreach (var stmt in selectedStatements)
        {
            selectionVisitor.Visit(stmt);
        }

        // Variables read but NOT declared in the selection are reads from outer scope.
        var readsFromOuter = new HashSet<string>(selectionVisitor.IdentifiersRead);
        readsFromOuter.ExceptWith(selectionVisitor.VariablesDeclared);

        // Variables written (declared or assigned) in the selection that are read
        // in statements AFTER the selection are writes to outer scope.
        var writtenInSelection = new HashSet<string>(selectionVisitor.VariablesDeclared);
        writtenInSelection.UnionWith(selectionVisitor.VariablesAssigned);

        var writesToOuter = new HashSet<string>();
        if (writtenInSelection.Count > 0)
        {
            // Find the index range of the selected statements in allStatements.
            // We use reference equality because AST nodes are records but we want
            // identity matching (same node instance).
            int selectionEnd = FindSelectionEndIndex(selectedStatements, allStatements);

            if (selectionEnd >= 0 && selectionEnd < allStatements.Count)
            {
                // Walk statements after the selection to find which written variables are read.
                var afterVisitor = new IdentifierCollector();
                for (int i = selectionEnd; i < allStatements.Count; i++)
                {
                    afterVisitor.Visit(allStatements[i]);
                }

                writesToOuter = new HashSet<string>(writtenInSelection);
                writesToOuter.IntersectWith(afterVisitor.IdentifiersRead);
            }
        }

        return new ScopeInfo
        {
            ReadsFromOuterScope = readsFromOuter,
            WritesToOuterScope = writesToOuter,
            DeclaredInScope = selectionVisitor.VariablesDeclared,
            ContainsReturn = selectionVisitor.ContainsReturn,
            ContainsBreak = selectionVisitor.ContainsBreak,
            ContainsContinue = selectionVisitor.ContainsContinue,
            ContainsYield = selectionVisitor.ContainsYield
        };
    }

    /// <summary>
    /// Finds the index in <paramref name="allStatements"/> immediately after the last
    /// selected statement. Returns -1 if no selected statement was found.
    /// </summary>
    private static int FindSelectionEndIndex(
        IReadOnlyList<Statement> selectedStatements,
        IReadOnlyList<Statement> allStatements)
    {
        if (selectedStatements.Count == 0)
        {
            return -1;
        }

        var lastSelected = selectedStatements[selectedStatements.Count - 1];

        for (int i = 0; i < allStatements.Count; i++)
        {
            if (ReferenceEquals(allStatements[i], lastSelected))
            {
                return i + 1;
            }
        }

        return -1;
    }

    /// <summary>
    /// Visitor that collects all variable-related information and control flow
    /// flags from a set of selected statements.
    /// </summary>
    private sealed class SelectionVisitor : AstVisitor
    {
        public HashSet<string> IdentifiersRead { get; } = new();
        public HashSet<string> VariablesDeclared { get; } = new();
        public HashSet<string> VariablesAssigned { get; } = new();
        public bool ContainsReturn { get; private set; }
        public bool ContainsBreak { get; private set; }
        public bool ContainsContinue { get; private set; }
        public bool ContainsYield { get; private set; }

        /// <summary>
        /// Tracks identifiers that appear as assignment targets so we don't
        /// count them as reads.
        /// </summary>
        private bool _inAssignmentTarget;

        public override void VisitIdentifier(Identifier node)
        {
            if (!_inAssignmentTarget)
            {
                IdentifiersRead.Add(node.Name);
            }
        }

        public override void VisitVariableDeclaration(VariableDeclaration node)
        {
            VariablesDeclared.Add(node.Name);

            // The initial value expression contains reads, not the name itself.
            if (node.InitialValue != null)
            {
                Visit(node.InitialValue);
            }
        }

        public override void VisitAssignment(Assignment node)
        {
            // The target side: collect assigned identifiers without treating them as reads.
            CollectAssignmentTargets(node.Target);

            // For compound assignments (+=, -=, etc.), the target is also read.
            if (node.Operator != AssignmentOperator.Assign)
            {
                Visit(node.Target);
            }

            // The value side is always read.
            Visit(node.Value);
        }

        public override void VisitForStatement(ForStatement node)
        {
            // The loop variable is an implicit declaration/assignment.
            CollectAssignmentTargets(node.Target);

            // The iterator expression is read.
            Visit(node.Iterator);

            // Walk the body and else body.
            foreach (var stmt in node.Body)
            {
                Visit(stmt);
            }

            foreach (var stmt in node.ElseBody)
            {
                Visit(stmt);
            }
        }

        public override void VisitWalrusExpression(WalrusExpression node)
        {
            // The walrus operator (:=) introduces a variable and reads the value.
            VariablesAssigned.Add(node.Target);
            Visit(node.Value);
        }

        public override void VisitReturnStatement(ReturnStatement node)
        {
            ContainsReturn = true;
            if (node.Value != null)
            {
                Visit(node.Value);
            }
        }

        public override void VisitBreakStatement(BreakStatement node)
        {
            ContainsBreak = true;
        }

        public override void VisitContinueStatement(ContinueStatement node)
        {
            ContainsContinue = true;
        }

        public override void VisitYieldStatement(YieldStatement node)
        {
            ContainsYield = true;
            Visit(node.Value);
        }

        /// <summary>
        /// Do not descend into nested function definitions -- they introduce their own scope.
        /// Variables referenced inside a nested function are not direct reads/writes of the
        /// enclosing selection's scope.
        /// </summary>
        public override void VisitFunctionDef(FunctionDef node)
        {
            // Intentionally do not recurse.
        }

        /// <summary>
        /// Do not descend into nested class definitions.
        /// </summary>
        public override void VisitClassDef(ClassDef node)
        {
            // Intentionally do not recurse.
        }

        /// <summary>
        /// Do not descend into lambda expressions -- they capture from outer scope
        /// but their body introduces a separate scope boundary.
        /// </summary>
        public override void VisitLambdaExpression(LambdaExpression node)
        {
            // Intentionally do not recurse.
        }

        /// <summary>
        /// Collects identifier names from an assignment target expression.
        /// Handles simple identifiers, tuple unpacking, and list unpacking.
        /// </summary>
        private void CollectAssignmentTargets(Expression target)
        {
            switch (target)
            {
                case Identifier id:
                    VariablesAssigned.Add(id.Name);
                    break;
                case TupleLiteral tuple:
                    foreach (var element in tuple.Elements)
                    {
                        CollectAssignmentTargets(element);
                    }

                    break;
                case ListLiteral list:
                    foreach (var element in list.Elements)
                    {
                        CollectAssignmentTargets(element);
                    }

                    break;
                case StarExpression star:
                    CollectAssignmentTargets(star.Operand);
                    break;
                default:
                    // For member access, index access, etc., walk them as reads.
                    // They are not local variable assignments.
                    _inAssignmentTarget = true;
                    Visit(target);
                    _inAssignmentTarget = false;
                    break;
            }
        }
    }

    /// <summary>
    /// Simple visitor that collects all identifier names referenced in a set of statements.
    /// Used to determine which variables are read after the selection.
    /// </summary>
    private sealed class IdentifierCollector : AstVisitor
    {
        public HashSet<string> IdentifiersRead { get; } = new();

        public override void VisitIdentifier(Identifier node)
        {
            IdentifiersRead.Add(node.Name);
        }

        /// <summary>
        /// Do not descend into nested function definitions.
        /// </summary>
        public override void VisitFunctionDef(FunctionDef node)
        {
            // Intentionally do not recurse.
        }

        /// <summary>
        /// Do not descend into nested class definitions.
        /// </summary>
        public override void VisitClassDef(ClassDef node)
        {
            // Intentionally do not recurse.
        }
    }
}
