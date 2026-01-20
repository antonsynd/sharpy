using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates protocol usage in Sharpy code:
/// - Iteration protocols (__iter__ for 'for' loops)
/// - Membership protocols (__contains__ for 'in' operator)
/// - Indexing protocols (__getitem__/__setitem__ for subscript access)
/// - Len protocol (__len__ for len() calls)
///
/// This is the pipeline-compatible version of ProtocolValidator.
/// Unlike the legacy version which provides type inference during type-checking,
/// this validator performs post-pass validation only.
/// The legacy ProtocolValidator is still used for type inference.
/// </summary>
public class ProtocolValidatorV2 : SemanticValidatorBase
{
    public override string Name => "ProtocolValidator";
    public override int Order => 500; // After access validation (450)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting protocol validation");

        foreach (var stmt in module.Body)
        {
            ValidateStatement(stmt);
        }
    }

    private void ValidateStatement(Statement stmt)
    {
        switch (stmt)
        {
            case FunctionDef funcDef:
                foreach (var bodyStmt in funcDef.Body)
                    ValidateStatement(bodyStmt);
                break;
            case ClassDef classDef:
                foreach (var member in classDef.Body)
                    ValidateStatement(member);
                break;
            case StructDef structDef:
                foreach (var member in structDef.Body)
                    ValidateStatement(member);
                break;
            case ForStatement forStmt:
                ValidateIteration(forStmt);
                foreach (var bodyStmt in forStmt.Body)
                    ValidateStatement(bodyStmt);
                break;
            case WhileStatement whileStmt:
                ValidateExpression(whileStmt.Test);
                foreach (var bodyStmt in whileStmt.Body)
                    ValidateStatement(bodyStmt);
                break;
            case IfStatement ifStmt:
                ValidateExpression(ifStmt.Test);
                foreach (var bodyStmt in ifStmt.ThenBody)
                    ValidateStatement(bodyStmt);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    ValidateExpression(elif.Test);
                    foreach (var bodyStmt in elif.Body)
                        ValidateStatement(bodyStmt);
                }
                foreach (var bodyStmt in ifStmt.ElseBody)
                    ValidateStatement(bodyStmt);
                break;
            case TryStatement tryStmt:
                foreach (var bodyStmt in tryStmt.Body)
                    ValidateStatement(bodyStmt);
                foreach (var handler in tryStmt.Handlers)
                {
                    foreach (var bodyStmt in handler.Body)
                        ValidateStatement(bodyStmt);
                }
                foreach (var bodyStmt in tryStmt.FinallyBody)
                    ValidateStatement(bodyStmt);
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
        }
    }

    private void ValidateIteration(ForStatement forStmt)
    {
        var iterableType = _context.SemanticInfo.GetExpressionType(forStmt.Iterator);
        if (iterableType == null || iterableType is UnknownType)
            return;

        if (!HasProtocol(iterableType, "__iter__"))
        {
            AddError(_context,
                $"Type '{iterableType.GetDisplayName()}' is not iterable " +
                "(missing '__iter__' method). Consider implementing IIterable<T> interface.",
                forStmt.Iterator.LineStart, forStmt.Iterator.ColumnStart);
        }

        // Also validate the iterator expression
        ValidateExpression(forStmt.Iterator);
    }

    private void ValidateExpression(Expression expr)
    {
        switch (expr)
        {
            case IndexAccess indexAccess:
                ValidateIndexAccess(indexAccess);
                ValidateExpression(indexAccess.Object);
                ValidateExpression(indexAccess.Index);
                break;
            case BinaryOp binOp when binOp.Operator is BinaryOperator.In or BinaryOperator.NotIn:
                ValidateMembership(binOp);
                ValidateExpression(binOp.Left);
                ValidateExpression(binOp.Right);
                break;
            case BinaryOp binOp:
                ValidateExpression(binOp.Left);
                ValidateExpression(binOp.Right);
                break;
            case FunctionCall call:
                ValidateFunctionCall(call);
                ValidateExpression(call.Function);
                foreach (var arg in call.Arguments)
                    ValidateExpression(arg);
                foreach (var kwArg in call.KeywordArguments)
                    ValidateExpression(kwArg.Value);
                break;
            case MemberAccess memberAccess:
                ValidateExpression(memberAccess.Object);
                break;
            case UnaryOp unaryOp:
                ValidateExpression(unaryOp.Operand);
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
            case ListComprehension listComp:
                ValidateComprehension(listComp.Element, listComp.Clauses);
                break;
            case SetComprehension setComp:
                ValidateComprehension(setComp.Element, setComp.Clauses);
                break;
            case DictComprehension dictComp:
                ValidateExpression(dictComp.Key);
                ValidateExpression(dictComp.Value);
                foreach (var clause in dictComp.Clauses)
                {
                    if (clause is ForClause forClause)
                    {
                        ValidateIteratorExpression(forClause.Iterator);
                        ValidateExpression(forClause.Iterator);
                    }
                    else if (clause is IfClause ifClause)
                    {
                        ValidateExpression(ifClause.Condition);
                    }
                }
                break;
            case ConditionalExpression cond:
                ValidateExpression(cond.Test);
                ValidateExpression(cond.ThenValue);
                ValidateExpression(cond.ElseValue);
                break;
            case Parenthesized paren:
                ValidateExpression(paren.Expression);
                break;
        }
    }

    private void ValidateComprehension(Expression element, IReadOnlyList<ComprehensionClause> clauses)
    {
        ValidateExpression(element);
        foreach (var clause in clauses)
        {
            if (clause is ForClause forClause)
            {
                ValidateIteratorExpression(forClause.Iterator);
                ValidateExpression(forClause.Iterator);
            }
            else if (clause is IfClause ifClause)
            {
                ValidateExpression(ifClause.Condition);
            }
        }
    }

    private void ValidateIteratorExpression(Expression iterator)
    {
        var iterableType = _context.SemanticInfo.GetExpressionType(iterator);
        if (iterableType == null || iterableType is UnknownType)
            return;

        if (!HasProtocol(iterableType, "__iter__"))
        {
            AddError(_context,
                $"Type '{iterableType.GetDisplayName()}' is not iterable " +
                "(missing '__iter__' method). Consider implementing IIterable<T> interface.",
                iterator.LineStart, iterator.ColumnStart);
        }
    }

    private void ValidateIndexAccess(IndexAccess indexAccess)
    {
        var containerType = _context.SemanticInfo.GetExpressionType(indexAccess.Object);
        if (containerType == null || containerType is UnknownType)
            return;

        if (!HasProtocol(containerType, "__getitem__"))
        {
            AddError(_context,
                $"Type '{containerType.GetDisplayName()}' does not support indexing " +
                "(missing '__getitem__' method). Consider implementing ISequence<T> interface.",
                indexAccess.LineStart, indexAccess.ColumnStart);
        }
    }

    private void ValidateMembership(BinaryOp binOp)
    {
        var containerType = _context.SemanticInfo.GetExpressionType(binOp.Right);
        if (containerType == null || containerType is UnknownType)
            return;

        if (!HasProtocol(containerType, "__contains__"))
        {
            AddError(_context,
                $"Type '{containerType.GetDisplayName()}' does not support membership testing " +
                "(missing '__contains__' method). Consider implementing IContainer<T> interface.",
                binOp.LineStart, binOp.ColumnStart);
        }
    }

    private void ValidateFunctionCall(FunctionCall call)
    {
        // Check for len() calls
        if (call.Function is Identifier id && id.Name == "len" && call.Arguments.Length == 1)
        {
            var argType = _context.SemanticInfo.GetExpressionType(call.Arguments[0]);
            if (argType == null || argType is UnknownType)
                return;

            if (!HasProtocol(argType, "__len__"))
            {
                AddError(_context,
                    $"Type '{argType.GetDisplayName()}' does not support len() " +
                    "(missing '__len__' method). Consider implementing ISized interface.",
                    call.LineStart, call.ColumnStart);
            }
        }
    }

    /// <summary>
    /// Checks if a type has a specific protocol dunder method.
    /// This duplicates logic from ProtocolValidator for post-pass validation.
    /// </summary>
    private bool HasProtocol(SemanticType type, string dunderName)
    {
        // Check Sharpy built-in types first
        if (type == SemanticType.Str)
        {
            return dunderName is "__len__" or "__iter__" or "__contains__" or "__getitem__";
        }

        // Check TupleType
        if (type is TupleType)
        {
            return dunderName is "__len__" or "__iter__" or "__getitem__";
        }

        // Check generic container types
        if (type is GenericType generic)
        {
            return generic.Name switch
            {
                "list" => dunderName is "__len__" or "__iter__" or "__contains__" or "__getitem__" or "__setitem__",
                "dict" => dunderName is "__len__" or "__iter__" or "__contains__" or "__getitem__" or "__setitem__",
                "set" => dunderName is "__len__" or "__iter__" or "__contains__",
                "tuple" => dunderName is "__len__" or "__iter__" or "__getitem__",
                _ => false
            };
        }

        // Check Sharpy user-defined types
        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            if (udt.Symbol.ProtocolMethods.ContainsKey(dunderName))
                return true;

            if (udt.Symbol.Methods.Any(m => m.Name == dunderName))
                return true;

            // Check CLR type if available
            if (udt.Symbol.ClrType != null && HasClrProtocol(udt.Symbol.ClrType, dunderName))
                return true;
        }

        // Check builtin types with CLR backing
        if (type is BuiltinType builtin && builtin.ClrType != null)
        {
            if (HasClrProtocol(builtin.ClrType, dunderName))
                return true;
        }

        // For other types (including int, bool, etc.), default to false for most protocols
        // except __str__ and __hash__ which all objects have
        if (dunderName is "__str__" or "__hash__")
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a CLR type supports a protocol by examining its interfaces.
    /// </summary>
    private bool HasClrProtocol(System.Type clrType, string dunderName)
    {
        // Check for IEnumerable<T> or IEnumerable -> __iter__
        if (dunderName == "__iter__")
        {
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(clrType))
                return true;

            // Check for Sharpy.Core.Iterator<T> base class
            var currentType = clrType;
            while (currentType != null)
            {
                if (currentType.IsGenericType &&
                    currentType.GetGenericTypeDefinition().FullName == "Sharpy.Core.Iterator`1")
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }

            // Check for IIterable<T> from Sharpy.Core
            var interfaces = clrType.GetInterfaces();
            if (interfaces.Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition().FullName == "Sharpy.Core.Collections.Interfaces.IIterable`1"))
            {
                return true;
            }
        }

        // ICollection -> __len__, __contains__
        if (dunderName is "__len__" or "__contains__")
        {
            if (typeof(System.Collections.ICollection).IsAssignableFrom(clrType))
                return true;
        }

        // IList -> __getitem__, __setitem__
        if (dunderName is "__getitem__" or "__setitem__")
        {
            if (typeof(System.Collections.IList).IsAssignableFrom(clrType))
                return true;
        }

        // IDictionary -> __getitem__, __setitem__, __contains__, __len__
        if (dunderName is "__getitem__" or "__setitem__" or "__contains__" or "__len__")
        {
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(clrType))
                return true;
        }

        return false;
    }
}
