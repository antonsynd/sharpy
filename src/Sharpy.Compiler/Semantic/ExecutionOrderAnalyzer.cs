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
/// <item><description>Initializer references an assignment variable (no type annotation)</description></item>
/// <item><description>Initializer references another non-const module variable (transitive)</description></item>
/// </list>
///
/// <para>
/// This logic was previously embedded in RoslynEmitter.GenerateModuleMembers().
/// Moving it here allows CodeGenInfoComputer to use it during semantic analysis,
/// enabling removal of legacy tracking fields from RoslynEmitter.
/// </para>
/// </summary>
internal class ExecutionOrderAnalyzer
{
    private readonly SymbolTable _symbolTable;

    // Track statement positions
    private readonly Dictionary<string, int> _variableFirstSeen = new();      // First Assignment position
    private readonly Dictionary<string, int> _variableFirstDeclared = new();  // First VariableDeclaration position

    // Track variable categories
    private readonly HashSet<string> _constVariables = new();
    private readonly HashSet<string> _assignmentVariables = new();  // Created by Assignment, not VariableDeclaration
    private readonly HashSet<string> _typeAndFunctionNames = new();

    // Result
    private readonly HashSet<string> _variablesWithIssues = new();

    public ExecutionOrderAnalyzer(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
    }

    /// <summary>
    /// Analyze all module statements and return set of variable names with execution order issues.
    /// </summary>
    public HashSet<string> Analyze(IReadOnlyList<Statement> statements)
    {
        // Clear state from any previous analysis
        _variableFirstSeen.Clear();
        _variableFirstDeclared.Clear();
        _constVariables.Clear();
        _assignmentVariables.Clear();
        _typeAndFunctionNames.Clear();
        _variablesWithIssues.Clear();

        // Pass 1: Collect type/function names and const variables
        CollectDeclarationNames(statements);

        // Pass 2: Track variable positions and detect basic issues
        DetectBasicIssues(statements);

        // Pass 3: Detect initializer dependencies (transitive closure)
        DetectInitializerDependencies(statements);

        return new HashSet<string>(_variablesWithIssues);
    }

    private void CollectDeclarationNames(IReadOnlyList<Statement> statements)
    {
        foreach (var stmt in statements)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    _typeAndFunctionNames.Add(classDef.Name);
                    break;
                case StructDef structDef:
                    _typeAndFunctionNames.Add(structDef.Name);
                    break;
                case FunctionDef funcDef:
                    _typeAndFunctionNames.Add(funcDef.Name);
                    break;
                case EnumDef enumDef:
                    _typeAndFunctionNames.Add(enumDef.Name);
                    break;
                case InterfaceDef interfaceDef:
                    _typeAndFunctionNames.Add(interfaceDef.Name);
                    break;
                case VariableDeclaration varDecl when varDecl.IsConst || NameFormDetector.IsConstantCaseName(varDecl.Name):
                    _constVariables.Add(varDecl.Name);
                    break;
            }
        }
    }

    private void DetectBasicIssues(IReadOnlyList<Statement> statements)
    {
        for (int i = 0; i < statements.Count; i++)
        {
            var stmt = statements[i];

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

                // Track variables created by Assignment (no VariableDeclaration)
                // These will be local variables in Main() - they always have execution order issues
                if (!_variableFirstDeclared.ContainsKey(varName) && !_constVariables.Contains(varName))
                {
                    _assignmentVariables.Add(varName);
                    _variablesWithIssues.Add(varName); // Assignment variables always have issues
                }
            }
        }
    }

    private void DetectInitializerDependencies(IReadOnlyList<Statement> statements)
    {
        // Build map of variable -> initializer
        var variableDeclarations = new Dictionary<string, VariableDeclaration>();
        foreach (var stmt in statements)
        {
            if (stmt is VariableDeclaration varDecl &&
                !_constVariables.Contains(varDecl.Name) &&
                varDecl.InitialValue != null)
            {
                variableDeclarations[varDecl.Name] = varDecl;
            }
        }

        // Iterate until no new issues found (transitive closure)
        bool changed = true;
        while (changed)
        {
            changed = false;

            foreach (var (varName, varDecl) in variableDeclarations)
            {
                if (_variablesWithIssues.Contains(varName))
                    continue;

                var referencedIds = new HashSet<string>();
                CollectReferencedIdentifiers(varDecl.InitialValue!, referencedIds);

                foreach (var refId in referencedIds)
                {
                    // Skip types, functions, consts
                    if (_typeAndFunctionNames.Contains(refId) || _constVariables.Contains(refId))
                        continue;

                    // Skip builtins
                    var symbol = _symbolTable.Lookup(refId);
                    if (symbol is FunctionSymbol or TypeSymbol)
                        continue;

                    // If references a variable with issues -> this has issues
                    if (_variablesWithIssues.Contains(refId))
                    {
                        _variablesWithIssues.Add(varName);
                        changed = true;
                        break;
                    }

                    // If references an assignment variable -> this has issues
                    if (_assignmentVariables.Contains(refId))
                    {
                        _variablesWithIssues.Add(varName);
                        changed = true;
                        break;
                    }

                    // If references another module variable (non-const) -> this has issues
                    // (static field initialization order is undefined)
                    if (variableDeclarations.ContainsKey(refId))
                    {
                        _variablesWithIssues.Add(varName);
                        changed = true;
                        break;
                    }
                }
            }
        }
    }

    private void CollectReferencedIdentifiers(Expression expr, HashSet<string> identifiers)
    {
        switch (expr)
        {
            case Identifier id:
                identifiers.Add(id.Name);
                break;
            case BinaryOp binOp:
                CollectReferencedIdentifiers(binOp.Left, identifiers);
                CollectReferencedIdentifiers(binOp.Right, identifiers);
                break;
            case UnaryOp unaryOp:
                CollectReferencedIdentifiers(unaryOp.Operand, identifiers);
                break;
            case FunctionCall call:
                if (call.Function is Identifier funcId)
                    identifiers.Add(funcId.Name);
                else
                    CollectReferencedIdentifiers(call.Function, identifiers);
                foreach (var arg in call.Arguments)
                    CollectReferencedIdentifiers(arg, identifiers);
                foreach (var kwarg in call.KeywordArguments)
                    CollectReferencedIdentifiers(kwarg.Value, identifiers);
                break;
            case MemberAccess memberAccess:
                CollectReferencedIdentifiers(memberAccess.Object, identifiers);
                break;
            case IndexAccess indexAccess:
                CollectReferencedIdentifiers(indexAccess.Object, identifiers);
                CollectReferencedIdentifiers(indexAccess.Index, identifiers);
                break;
            case SliceAccess sliceAccess:
                CollectReferencedIdentifiers(sliceAccess.Object, identifiers);
                if (sliceAccess.Start != null)
                    CollectReferencedIdentifiers(sliceAccess.Start, identifiers);
                if (sliceAccess.Stop != null)
                    CollectReferencedIdentifiers(sliceAccess.Stop, identifiers);
                if (sliceAccess.Step != null)
                    CollectReferencedIdentifiers(sliceAccess.Step, identifiers);
                break;
            case ConditionalExpression cond:
                CollectReferencedIdentifiers(cond.Test, identifiers);
                CollectReferencedIdentifiers(cond.ThenValue, identifiers);
                CollectReferencedIdentifiers(cond.ElseValue, identifiers);
                break;
            case Parenthesized paren:
                CollectReferencedIdentifiers(paren.Expression, identifiers);
                break;
            case ListLiteral list:
                foreach (var elem in list.Elements)
                    CollectReferencedIdentifiers(elem, identifiers);
                break;
            case DictLiteral dict:
                foreach (var entry in dict.Entries)
                {
                    if (entry.Key != null)
                        CollectReferencedIdentifiers(entry.Key, identifiers);
                    CollectReferencedIdentifiers(entry.Value, identifiers);
                }
                break;
            case SetLiteral set:
                foreach (var elem in set.Elements)
                    CollectReferencedIdentifiers(elem, identifiers);
                break;
            case TupleLiteral tuple:
                foreach (var elem in tuple.Elements)
                    CollectReferencedIdentifiers(elem, identifiers);
                break;
            case LambdaExpression lambda:
                CollectReferencedIdentifiers(lambda.Body, identifiers);
                break;
            case ListComprehension listComp:
                CollectReferencedIdentifiers(listComp.Element, identifiers);
                foreach (var clause in listComp.Clauses)
                {
                    if (clause is ForClause forClause)
                        CollectReferencedIdentifiers(forClause.Iterator, identifiers);
                    else if (clause is IfClause ifClause)
                        CollectReferencedIdentifiers(ifClause.Condition, identifiers);
                }
                break;
            case SetComprehension setComp:
                CollectReferencedIdentifiers(setComp.Element, identifiers);
                foreach (var clause in setComp.Clauses)
                {
                    if (clause is ForClause forClause)
                        CollectReferencedIdentifiers(forClause.Iterator, identifiers);
                    else if (clause is IfClause ifClause)
                        CollectReferencedIdentifiers(ifClause.Condition, identifiers);
                }
                break;
            case DictComprehension dictComp:
                CollectReferencedIdentifiers(dictComp.Key, identifiers);
                CollectReferencedIdentifiers(dictComp.Value, identifiers);
                foreach (var clause in dictComp.Clauses)
                {
                    if (clause is ForClause forClause)
                        CollectReferencedIdentifiers(forClause.Iterator, identifiers);
                    else if (clause is IfClause ifClause)
                        CollectReferencedIdentifiers(ifClause.Condition, identifiers);
                }
                break;
            case DictSpreadComprehension dictSpreadComp:
                CollectReferencedIdentifiers(dictSpreadComp.Spread, identifiers);
                foreach (var clause in dictSpreadComp.Clauses)
                {
                    if (clause is ForClause forClause)
                        CollectReferencedIdentifiers(forClause.Iterator, identifiers);
                    else if (clause is IfClause ifClause)
                        CollectReferencedIdentifiers(ifClause.Condition, identifiers);
                }
                break;
            case ComparisonChain chain:
                foreach (var operand in chain.Operands)
                    CollectReferencedIdentifiers(operand, identifiers);
                break;
            case FStringLiteral fString:
                foreach (var part in fString.Parts)
                {
                    if (part.Expression != null)
                        CollectReferencedIdentifiers(part.Expression, identifiers);
                }
                break;
            case TStringLiteral tString:
                foreach (var part in tString.Parts)
                {
                    if (part.Expression != null)
                        CollectReferencedIdentifiers(part.Expression, identifiers);
                }
                break;
            // Literals don't reference identifiers
            case IntegerLiteral:
            case FloatLiteral:
            case StringLiteral:
            case BooleanLiteral:
            case NoneLiteral:
                break;
        }
    }

}
