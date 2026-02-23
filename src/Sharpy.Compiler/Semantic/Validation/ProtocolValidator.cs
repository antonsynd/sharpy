using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
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
/// Post-pass validation of protocol usage. TypeInferenceService handles
/// type inference during type-checking; this validator catches missing
/// protocol implementations after types are resolved.
/// </summary>
internal class ProtocolValidator : SemanticValidatorBase
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
                foreach (var bodyStmt in tryStmt.ElseBody)
                    ValidateStatement(bodyStmt);
                foreach (var bodyStmt in tryStmt.FinallyBody)
                    ValidateStatement(bodyStmt);
                break;
            case WithStatement withStmt:
                foreach (var item in withStmt.Items)
                    ValidateExpression(item.ContextExpression);
                foreach (var bodyStmt in withStmt.Body)
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

        if (!HasProtocol(iterableType, DunderNames.Iter))
        {
            AddError(_context,
                $"Type '{iterableType.GetDisplayName()}' is not iterable " +
                "(missing '__iter__' method).",
                forStmt.Iterator.LineStart, forStmt.Iterator.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: forStmt.Iterator.Span);
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

        if (!HasProtocol(iterableType, DunderNames.Iter))
        {
            AddError(_context,
                $"Type '{iterableType.GetDisplayName()}' is not iterable " +
                "(missing '__iter__' method).",
                iterator.LineStart, iterator.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: iterator.Span);
        }
    }

    private void ValidateIndexAccess(IndexAccess indexAccess)
    {
        var containerType = _context.SemanticInfo.GetExpressionType(indexAccess.Object);
        if (containerType == null || containerType is UnknownType)
            return;

        if (!HasProtocol(containerType, DunderNames.GetItem))
        {
            AddError(_context,
                $"Type '{containerType.GetDisplayName()}' does not support indexing " +
                "(missing '__getitem__' method).",
                indexAccess.LineStart, indexAccess.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: indexAccess.Span);
        }
    }

    private void ValidateMembership(BinaryOp binOp)
    {
        var containerType = _context.SemanticInfo.GetExpressionType(binOp.Right);
        if (containerType == null || containerType is UnknownType)
            return;

        if (!HasProtocol(containerType, DunderNames.Contains))
        {
            AddError(_context,
                $"Type '{containerType.GetDisplayName()}' does not support membership testing " +
                "(missing '__contains__' method).",
                binOp.LineStart, binOp.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: binOp.Span);
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

            if (!HasProtocol(argType, DunderNames.Len))
            {
                AddError(_context,
                    $"Type '{argType.GetDisplayName()}' does not support len() " +
                    "(missing '__len__' method). Consider implementing ISized interface.",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                    span: call.Span);
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
            return dunderName is DunderNames.Len or DunderNames.Iter or DunderNames.Contains or DunderNames.GetItem;
        }

        // Check TupleType
        if (type is TupleType)
        {
            return dunderName is DunderNames.Len or DunderNames.Iter or DunderNames.GetItem;
        }

        // Check generic container types
        if (type is GenericType generic)
        {
            return generic.Name switch
            {
                BuiltinNames.List => dunderName is DunderNames.Len or DunderNames.Iter or DunderNames.Contains or DunderNames.GetItem or DunderNames.SetItem,
                BuiltinNames.Dict => dunderName is DunderNames.Len or DunderNames.Iter or DunderNames.Contains or DunderNames.GetItem or DunderNames.SetItem,
                BuiltinNames.Set => dunderName is DunderNames.Len or DunderNames.Iter or DunderNames.Contains,
                BuiltinNames.Tuple => dunderName is DunderNames.Len or DunderNames.Iter or DunderNames.GetItem,
                // Generator functions return IEnumerable<T> at the semantic level;
                // Iterator<T> is returned by reversed() and other iterator builtins
                "IEnumerable" or "IEnumerator" or BuiltinNames.Iterator => dunderName is DunderNames.Iter,
                _ => false
            };
        }

        // Enum types support __iter__ (via Enum.GetValues<T>())
        if (type is UserDefinedType { Symbol.TypeKind: TypeKind.Enum } && dunderName == DunderNames.Iter)
            return true;

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
        if (dunderName is DunderNames.Str or DunderNames.Hash)
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a CLR type supports a protocol by examining its interfaces.
    /// </summary>
    private bool HasClrProtocol(System.Type clrType, string dunderName)
    {
        // Check for IEnumerable<T> or IEnumerable -> __iter__
        if (dunderName == DunderNames.Iter)
        {
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(clrType))
                return true;

            // Check for Sharpy.Iterator<T> base class
            if (ClrTypeHelper.GetIteratorElementType(clrType) != null)
                return true;
        }

        // ICollection -> __len__, __contains__
        if (dunderName is DunderNames.Len or DunderNames.Contains)
        {
            if (typeof(System.Collections.ICollection).IsAssignableFrom(clrType))
                return true;
        }

        // IList -> __getitem__, __setitem__
        if (dunderName is DunderNames.GetItem or DunderNames.SetItem)
        {
            if (typeof(System.Collections.IList).IsAssignableFrom(clrType))
                return true;
        }

        // IDictionary -> __getitem__, __setitem__, __contains__, __len__
        if (dunderName is DunderNames.GetItem or DunderNames.SetItem or DunderNames.Contains or DunderNames.Len)
        {
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(clrType))
                return true;
        }

        return false;
    }
}
