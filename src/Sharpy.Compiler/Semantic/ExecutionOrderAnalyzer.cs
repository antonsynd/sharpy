using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Analyzes module-level statements to detect variables with execution order issues.
/// A variable has execution order issues if it cannot safely be a static field initializer.
///
/// <para><b>Execution Order Issues Include:</b></para>
/// <list type="bullet">
/// <item><description>Assignment before declaration (e.g., x = 5 before x: int = 10)</description></item>
/// <item><description>Multiple declarations of the same variable</description></item>
/// <item><description>A variable created by assignment alone (no type annotation)</description></item>
/// </list>
///
/// <para>
/// What a declaration's initializer reads is NOT an issue (plan-775c50 D1, #2420): the
/// <c>CodeGenInfo</c> of a module-level <see cref="VariableDeclaration"/> does not depend on
/// its initializer, so a reference to it is emitted the same way whatever that initializer names.
/// </para>
///
/// <para>
/// This logic was previously embedded in RoslynEmitter.GenerateModuleMembers().
/// Moving it here allows CodeGenInfoComputer to use it during semantic analysis,
/// enabling removal of legacy tracking fields from RoslynEmitter.
/// </para>
/// </summary>
internal class ExecutionOrderAnalyzer
{
    // Track statement positions
    private readonly Dictionary<string, int> _variableFirstSeen = new();      // First Assignment position
    private readonly Dictionary<string, int> _variableFirstDeclared = new();  // First VariableDeclaration position

    // Track variable categories
    private readonly HashSet<string> _constVariables = new();

    // Result
    private readonly HashSet<string> _variablesWithIssues = new();

    /// <summary>
    /// Analyze all module statements and return set of variable names with execution order issues.
    /// </summary>
    public HashSet<string> Analyze(IReadOnlyList<Statement> statements)
    {
        // Clear state from any previous analysis
        _variableFirstSeen.Clear();
        _variableFirstDeclared.Clear();
        _constVariables.Clear();
        _variablesWithIssues.Clear();

        // Pass 1: Collect const variables
        CollectConstNames(statements);

        // Pass 2: Track variable positions and detect issues
        DetectBasicIssues(statements);

        return new HashSet<string>(_variablesWithIssues);
    }

    private void CollectConstNames(IReadOnlyList<Statement> statements)
    {
        // A decorated module-level declaration is refused (SPY0322), so only a bare
        // VariableDeclaration can name a const here.
        foreach (var stmt in statements)
        {
            if (stmt is VariableDeclaration varDecl
                && (varDecl.IsConst || NameFormDetector.IsConstantCaseName(varDecl.Name)))
            {
                _constVariables.Add(varDecl.Name);
            }
        }
    }

    private void DetectBasicIssues(IReadOnlyList<Statement> statements)
    {
        for (int i = 0; i < statements.Count; i++)
        {
            var stmt = statements[i];

            // @suppress wrapper (#1024): decorators are compile-time-only; the inner
            // assignment still participates in module-level execution ordering.
            if (stmt is DecoratedStatement decorated)
                stmt = decorated.Statement;

            if (stmt is VariableDeclaration varDecl && !_constVariables.Contains(varDecl.Name))
            {
                var varName = varDecl.Name;

                if (_variableFirstDeclared.ContainsKey(varName))
                {
                    // Multiple declarations
                    _variablesWithIssues.Add(varName);
                }
                else
                {
                    _variableFirstDeclared[varName] = i;

                    // Check if assigned before declared
                    if (_variableFirstSeen.TryGetValue(varName, out var firstSeen) && firstSeen < i)
                    {
                        _variablesWithIssues.Add(varName);
                    }
                }
            }
            else if (stmt is Assignment assign && assign.Target is Identifier targetId)
            {
                var varName = targetId.Name;

                if (!_variableFirstSeen.ContainsKey(varName))
                {
                    _variableFirstSeen[varName] = i;
                }

                // A variable created by Assignment (no VariableDeclaration) is a local
                // of Main() - it always has execution order issues
                if (!_variableFirstDeclared.ContainsKey(varName) && !_constVariables.Contains(varName))
                {
                    _variablesWithIssues.Add(varName);
                }
            }
        }
    }
}
