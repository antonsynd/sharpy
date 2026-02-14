using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates access level rules in Sharpy code:
/// - Private members (__name) only accessible within the same class
/// - Protected members (_name) only accessible within class hierarchy
/// - Public members accessible everywhere
///
/// This is the pipeline-compatible version of AccessValidator.
/// Unlike the legacy version which is called during expression type-checking,
/// this validator performs a post-pass over the AST.
/// </summary>
internal class AccessValidator : SemanticValidatorBase
{
    public override string Name => "AccessValidator";
    public override int Order => 450; // After control flow (400)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting access validation");

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
        }
    }

    private void ValidateClass(ClassDef classDef)
    {
        var classSymbol = _context.SymbolTable.LookupType(classDef.Name);
        using (_context.Traversal.EnterClass(classSymbol))
        {
            foreach (var member in classDef.Body)
            {
                ValidateStatement(member);
            }
        }
    }

    private void ValidateStruct(StructDef structDef)
    {
        var structSymbol = _context.SymbolTable.LookupType(structDef.Name);
        using (_context.Traversal.EnterClass(structSymbol))
        {
            foreach (var member in structDef.Body)
            {
                ValidateStatement(member);
            }
        }
    }

    private void ValidateFunction(FunctionDef funcDef)
    {
        foreach (var stmt in funcDef.Body)
        {
            ValidateStatement(stmt);
        }
    }

    private void ValidateStatement(Statement stmt)
    {
        switch (stmt)
        {
            case FunctionDef funcDef:
                ValidateFunction(funcDef);
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
                break;
            case ForStatement forStmt:
                ValidateExpression(forStmt.Iterator);
                foreach (var s in forStmt.Body)
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
            case MemberAccess memberAccess:
                ValidateMemberAccess(memberAccess);
                ValidateExpression(memberAccess.Object);
                break;
            case FunctionCall call:
                ValidateExpression(call.Function);
                foreach (var arg in call.Arguments)
                    ValidateExpression(arg);
                foreach (var kwArg in call.KeywordArguments)
                    ValidateExpression(kwArg.Value);
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
        }
    }

    private void ValidateMemberAccess(MemberAccess memberAccess)
    {
        // Get the type of the object being accessed
        var objectType = _context.SemanticInfo.GetExpressionType(memberAccess.Object);
        if (objectType == null)
            return;

        // Get the owning type symbol
        TypeSymbol? owningType = null;
        if (objectType is UserDefinedType udt)
        {
            owningType = udt.Symbol ?? _context.SymbolTable.LookupType(udt.Name);
        }

        if (owningType == null)
            return;

        ValidateMemberAccess(memberAccess.Member, owningType,
            memberAccess.LineStart, memberAccess.ColumnStart, memberAccess.Span);
    }

    private void ValidateMemberAccess(string memberName, TypeSymbol owningType, int? lineStart, int? columnStart,
        TextSpan? span = null)
    {
        var accessLevel = DetermineAccessLevel(memberName);

        switch (accessLevel)
        {
            case AccessLevel.Private:
                // Private members only accessible within the same class
                if (_context.Traversal.CurrentClass != owningType)
                {
                    AddError(_context,
                        $"Cannot access private member '{memberName}' of '{owningType.Name}' from outside the class",
                        lineStart, columnStart, code: DiagnosticCodes.Semantic.AccessViolation,
                        span: span);
                }
                break;

            case AccessLevel.Protected:
                // Protected members accessible within the class hierarchy
                if (_context.Traversal.CurrentClass == null || !IsInHierarchy(_context.Traversal.CurrentClass, owningType))
                {
                    AddError(_context,
                        $"Cannot access protected member '{memberName}' of '{owningType.Name}' from outside the class hierarchy",
                        lineStart, columnStart, code: DiagnosticCodes.Semantic.AccessViolation,
                        span: span);
                }
                break;

            case AccessLevel.Public:
                // Public members accessible everywhere
                break;
        }
    }

    private AccessLevel DetermineAccessLevel(string name)
    {
        if (name.StartsWith("__") && !name.EndsWith("__"))
            return AccessLevel.Private;

        if (name.StartsWith("_") && !name.StartsWith("__"))
            return AccessLevel.Protected;

        return AccessLevel.Public;
    }

    private TypeSymbol? GetBaseType(TypeSymbol symbol)
        => _context.SemanticBinding.GetBaseType(symbol) ?? symbol.BaseType;

    private bool IsInHierarchy(TypeSymbol currentClass, TypeSymbol targetClass)
    {
        // Same class
        if (currentClass == targetClass)
            return true;

        // Check if currentClass is a subclass of targetClass
        var baseType = GetBaseType(currentClass);
        while (baseType != null)
        {
            if (baseType == targetClass)
                return true;
            baseType = GetBaseType(baseType);
        }

        // Check if currentClass is a superclass of targetClass
        baseType = GetBaseType(targetClass);
        while (baseType != null)
        {
            if (baseType == currentClass)
                return true;
            baseType = GetBaseType(baseType);
        }

        return false;
    }
}
