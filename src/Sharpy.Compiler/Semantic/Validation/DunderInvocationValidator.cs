using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Enforces dunder method invocation rules:
/// - Dunder methods cannot be called directly from non-dunder code (SPY0460)
/// - Inside a dunder, dunder calls must be on self or super() (SPY0461)
/// - Dunder method references cannot be captured (SPY0462)
///
/// See docs/language_specification/dunder_invocation_rules.md for the full spec.
/// </summary>
internal class DunderInvocationValidator : SemanticValidatorBase
{
    public override string Name => "DunderInvocationValidator";
    public override int Order => 460; // After AccessValidator (450), before ProtocolValidator (500)

    private SemanticContext _context = null!;
    private bool _inDunderMethod;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _inDunderMethod = false;

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
                ValidateFunctionDef(funcDef);
                break;
            case ClassDef classDef:
                ValidateClass(classDef);
                break;
            case StructDef structDef:
                ValidateStruct(structDef);
                break;
            case ExpressionStatement exprStmt:
                ValidateExpression(exprStmt.Expression);
                break;
            case VariableDeclaration varDecl:
                if (varDecl.InitialValue != null)
                    ValidateExpression(varDecl.InitialValue);
                break;
            case Assignment assignment:
                ValidateExpression(assignment.Target);
                ValidateExpression(assignment.Value);
                break;
        }
    }

    private void ValidateClass(ClassDef classDef)
    {
        foreach (var member in classDef.Body)
        {
            ValidateStatement(member);
        }
    }

    private void ValidateStruct(StructDef structDef)
    {
        foreach (var member in structDef.Body)
        {
            ValidateStatement(member);
        }
    }

    private void ValidateFunctionDef(FunctionDef funcDef)
    {
        var wasDunder = _inDunderMethod;
        _inDunderMethod = DunderDetector.IsDunderMethod(funcDef.Name);

        foreach (var stmt in funcDef.Body)
        {
            ValidateStatement(stmt);
        }

        _inDunderMethod = wasDunder;
    }

    private void ValidateStatement(Statement stmt)
    {
        switch (stmt)
        {
            case FunctionDef funcDef:
                ValidateFunctionDef(funcDef);
                break;
            case ExpressionStatement exprStmt:
                ValidateExpression(exprStmt.Expression);
                break;
            case Assignment assignment:
                ValidateExpression(assignment.Target);
                ValidateExpression(assignment.Value);
                break;
            case VariableDeclaration varDecl:
                if (varDecl.InitialValue != null)
                    ValidateExpression(varDecl.InitialValue);
                break;
            case ReturnStatement returnStmt:
                if (returnStmt.Value != null)
                    ValidateExpression(returnStmt.Value);
                break;
            case IfStatement ifStmt:
                ValidateExpression(ifStmt.Test);
                foreach (var s in ifStmt.ThenBody)
                    ValidateStatement(s);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    ValidateExpression(elif.Test);
                    foreach (var s in elif.Body)
                        ValidateStatement(s);
                }
                foreach (var s in ifStmt.ElseBody)
                    ValidateStatement(s);
                break;
            case WhileStatement whileStmt:
                ValidateExpression(whileStmt.Test);
                foreach (var s in whileStmt.Body)
                    ValidateStatement(s);
                foreach (var s in whileStmt.ElseBody)
                    ValidateStatement(s);
                break;
            case ForStatement forStmt:
                ValidateExpression(forStmt.Iterator);
                foreach (var s in forStmt.Body)
                    ValidateStatement(s);
                foreach (var s in forStmt.ElseBody)
                    ValidateStatement(s);
                break;
            case TryStatement tryStmt:
                foreach (var s in tryStmt.Body)
                    ValidateStatement(s);
                foreach (var handler in tryStmt.Handlers)
                {
                    foreach (var s in handler.Body)
                        ValidateStatement(s);
                }
                foreach (var s in tryStmt.ElseBody)
                    ValidateStatement(s);
                foreach (var s in tryStmt.FinallyBody)
                    ValidateStatement(s);
                break;
            case WithStatement withStmt:
                foreach (var item in withStmt.Items)
                    ValidateExpression(item.ContextExpression);
                foreach (var s in withStmt.Body)
                    ValidateStatement(s);
                break;
            case RaiseStatement raiseStmt:
                if (raiseStmt.Exception != null)
                    ValidateExpression(raiseStmt.Exception);
                break;
            case AssertStatement assertStmt:
                ValidateExpression(assertStmt.Test);
                if (assertStmt.Message != null)
                    ValidateExpression(assertStmt.Message);
                break;
        }
    }

    private void ValidateExpression(Expression expr)
    {
        switch (expr)
        {
            case FunctionCall call:
                ValidateFunctionCall(call);
                break;
            case MemberAccess memberAccess:
                // Dunder properties (e.g., __name__, __doc__) are attributes, not methods
                if (DunderDetector.IsDunderProperty(memberAccess.Member))
                {
                    ValidateExpression(memberAccess.Object);
                    break;
                }
                // A dunder MemberAccess that is NOT a direct call target is a capture
                if (DunderDetector.IsDunderMethod(memberAccess.Member))
                {
                    AddError(_context,
                        $"Cannot capture dunder method reference '{memberAccess.Member}'. Dunder methods must be called immediately.",
                        memberAccess.LineStart, memberAccess.ColumnStart,
                        code: DiagnosticCodes.Validation.DunderCapture,
                        span: memberAccess.Span);
                }
                ValidateExpression(memberAccess.Object);
                break;
            case BinaryOp binOp:
                ValidateExpression(binOp.Left);
                ValidateExpression(binOp.Right);
                break;
            case UnaryOp unaryOp:
                ValidateExpression(unaryOp.Operand);
                break;
            case IndexAccess indexAccess:
                ValidateExpression(indexAccess.Object);
                ValidateExpression(indexAccess.Index);
                break;
            case ListLiteral listLit:
                foreach (var elem in listLit.Elements)
                    ValidateExpression(elem);
                break;
            case DictLiteral dictLit:
                foreach (var entry in dictLit.Entries)
                {
                    if (entry.Key != null)
                        ValidateExpression(entry.Key);
                    ValidateExpression(entry.Value);
                }
                break;
            case SetLiteral setLit:
                foreach (var elem in setLit.Elements)
                    ValidateExpression(elem);
                break;
            case TupleLiteral tupleLit:
                foreach (var elem in tupleLit.Elements)
                    ValidateExpression(elem);
                break;
            case ConditionalExpression cond:
                ValidateExpression(cond.Test);
                ValidateExpression(cond.ThenValue);
                ValidateExpression(cond.ElseValue);
                break;
            case ListComprehension listComp:
                ValidateExpression(listComp.Element);
                foreach (var clause in listComp.Clauses)
                {
                    if (clause is ForClause forClause)
                        ValidateExpression(forClause.Iterator);
                    else if (clause is IfClause ifClause)
                        ValidateExpression(ifClause.Condition);
                }
                break;
            case SetComprehension setComp:
                ValidateExpression(setComp.Element);
                foreach (var clause in setComp.Clauses)
                {
                    if (clause is ForClause forClause)
                        ValidateExpression(forClause.Iterator);
                    else if (clause is IfClause ifClause)
                        ValidateExpression(ifClause.Condition);
                }
                break;
            case DictComprehension dictComp:
                ValidateExpression(dictComp.Key);
                ValidateExpression(dictComp.Value);
                foreach (var clause in dictComp.Clauses)
                {
                    if (clause is ForClause forClause)
                        ValidateExpression(forClause.Iterator);
                    else if (clause is IfClause ifClause)
                        ValidateExpression(ifClause.Condition);
                }
                break;
            case Parenthesized paren:
                ValidateExpression(paren.Expression);
                break;
            case FStringLiteral fStr:
                foreach (var part in fStr.Parts)
                {
                    if (part.Expression != null)
                        ValidateExpression(part.Expression);
                }
                break;
        }
    }

    private void ValidateFunctionCall(FunctionCall call)
    {
        if (call.Function is MemberAccess memberAccess
            && DunderDetector.IsDunderMethod(memberAccess.Member)
            && !DunderDetector.IsDunderProperty(memberAccess.Member))
        {
            // This is a dunder method call (e.g., self.__eq__(other))
            var dunderName = memberAccess.Member;

            if (!_inDunderMethod)
            {
                // SPY0460: Calling a dunder from outside a dunder method
                AddError(_context,
                    $"Cannot invoke dunder method '{dunderName}' directly. Use the corresponding operator or built-in function.",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Validation.DunderDirectInvocation,
                    span: memberAccess.Span);
            }
            else if (!IsSelfOrSuper(memberAccess.Object))
            {
                // SPY0461: Dunder call on wrong receiver
                AddError(_context,
                    $"Dunder method '{dunderName}' can only be called on 'self' or 'super()' within another dunder method.",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Validation.DunderWrongReceiver,
                    span: memberAccess.Span);
            }

            // Recurse into the receiver object (but not the MemberAccess itself — we handled it)
            ValidateExpression(memberAccess.Object);
        }
        else
        {
            // Not a dunder call — recurse into the callee expression normally
            ValidateExpression(call.Function);
        }

        // Always validate arguments
        foreach (var arg in call.Arguments)
            ValidateExpression(arg);
        foreach (var kwArg in call.KeywordArguments)
            ValidateExpression(kwArg.Value);
    }

    private static bool IsSelfOrSuper(Expression expr)
    {
        return expr is Identifier { Name: PythonNames.Self } || expr is SuperExpression;
    }
}
