using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

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
internal class ProtocolValidator : ValidatingAstWalker
{
    public override string Name => "ProtocolValidator";
    public override int Order => 500; // After access validation (450)

    private ICompilerLogger _logger = NullLogger.Instance;
    private readonly HashSet<IndexAccess> _assignmentTargets = new(ReferenceEqualityComparer.Instance);

    public override void Validate(Module module, SemanticContext context)
    {
        _assignmentTargets.Clear();
        // The authority is built from THIS context's symbol table and registry, so it must not
        // survive into the next Validate call on a reused validator instance.
        _membership = null;
        _logger = context.Logger;
        _logger.LogDebug("Starting protocol validation");
        base.Validate(module, context);
    }

    public override void VisitForStatement(ForStatement node)
    {
        ValidateIteration(node);
        base.VisitForStatement(node);
    }

    public override void VisitForClause(ForClause node)
    {
        ValidateIteratorExpression(node.Iterator, node.IsAsync);
        base.VisitForClause(node);
    }

    public override void VisitIndexAccess(IndexAccess node)
    {
        // Skip __getitem__ validation for nodes that are assignment targets —
        // VisitAssignment already validates __setitem__, and emitting a second
        // "does not support indexing" error for the same node is misleading.
        if (!_assignmentTargets.Contains(node))
        {
            ValidateIndexAccess(node);
        }
        base.VisitIndexAccess(node);
    }

    public override void VisitBinaryOp(BinaryOp node)
    {
        if (node.Operator is BinaryOperator.In or BinaryOperator.NotIn)
        {
            ValidateMembership(node);
        }
        base.VisitBinaryOp(node);
    }

    public override void VisitFunctionCall(FunctionCall node)
    {
        ValidateFunctionCall(node);
        base.VisitFunctionCall(node);
    }

    public override void VisitAssignment(Assignment node)
    {
        // The store-position protocol rule ranges over every position an index access can be
        // written through: a simple target, an augmented target (which READS it too — `b[k] += v`
        // needs both __getitem__ and __setitem__), and an unpacking element (`b[k], y = …`).
        if (node.Target is IndexAccess indexAccess)
        {
            _assignmentTargets.Add(indexAccess);
            ValidateIndexAssignment(indexAccess);
            if (node.Operator != AssignmentOperator.Assign)
                ValidateIndexAccess(indexAccess);
        }
        else if (node.Target is TupleLiteral unpacking)
        {
            foreach (var element in unpacking.Elements.OfType<IndexAccess>())
            {
                _assignmentTargets.Add(element);
                ValidateIndexAssignment(element);
            }
        }
        base.VisitAssignment(node);
    }

    /// <summary>
    /// Reports the strict-Optional protocol error for a <c>T?</c> receiver: protocol operations
    /// (len/in/indexing/iteration) are not available on an Optional directly — it must be narrowed
    /// or unwrapped first. Returns true when an error was reported (caller skips the generic check).
    /// </summary>
    private bool ReportOptionalProtocol(SemanticType type, string operation, int line, int column, TextSpan? span)
    {
        if (type is not OptionalType)
            return false;

        AddError(
            $"Optional type '{type.GetDisplayName()}' does not support {operation} directly. " +
            "Narrow it first (if x is not None:) or unwrap it (x.unwrap()).",
            line, column, code: DiagnosticCodes.Semantic.OptionalRequiresNarrowing, span: span);
        return true;
    }

    private void ValidateIteration(ForStatement forStmt)
    {
        var iterableType = Context.SemanticInfo.GetExpressionType(forStmt.Iterator);
        if (iterableType == null || iterableType is UnknownType)
            return;

        if (ReportOptionalProtocol(iterableType, "iteration",
                forStmt.Iterator.LineStart, forStmt.Iterator.ColumnStart, forStmt.Iterator.Span))
            return;

        // Async for loops use IAsyncEnumerable<T> protocol, not __iter__.
        // Skip the __iter__ check and trust the C# compiler to validate.
        if (!forStmt.IsAsync && !HasProtocol(iterableType, DunderNames.Iter))
        {
            AddError(
                $"Type '{iterableType.GetDisplayName()}' is not iterable " +
                "(missing '__iter__' method).",
                forStmt.Iterator.LineStart, forStmt.Iterator.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: forStmt.Iterator.Span);
        }
    }

    private void ValidateIteratorExpression(Expression iterator, bool isAsync = false)
    {
        var iterableType = Context.SemanticInfo.GetExpressionType(iterator);
        if (iterableType == null || iterableType is UnknownType)
            return;

        if (ReportOptionalProtocol(iterableType, "iteration",
                iterator.LineStart, iterator.ColumnStart, iterator.Span))
            return;

        // Async comprehension clauses use IAsyncEnumerable<T> protocol, not __iter__.
        // Skip the __iter__ check and trust the C# compiler to validate.
        if (!isAsync && !HasProtocol(iterableType, DunderNames.Iter))
        {
            AddError(
                $"Type '{iterableType.GetDisplayName()}' is not iterable " +
                "(missing '__iter__' method).",
                iterator.LineStart, iterator.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: iterator.Span);
        }
    }

    private void ValidateIndexAccess(IndexAccess indexAccess)
    {
        var containerType = Context.SemanticInfo.GetExpressionType(indexAccess.Object);
        if (containerType == null || containerType is UnknownType)
            return;

        if (ReportOptionalProtocol(containerType, "indexing",
                indexAccess.LineStart, indexAccess.ColumnStart, indexAccess.Span))
            return;

        if (!HasProtocol(containerType, DunderNames.GetItem))
        {
            AddError(
                $"Type '{containerType.GetDisplayName()}' does not support indexing " +
                "(missing '__getitem__' method).",
                indexAccess.LineStart, indexAccess.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: indexAccess.Span);
        }
    }

    private void ValidateIndexAssignment(IndexAccess indexAccess)
    {
        var containerType = Context.SemanticInfo.GetExpressionType(indexAccess.Object);
        if (containerType == null || containerType is UnknownType)
            return;

        if (ReportOptionalProtocol(containerType, "item assignment",
                indexAccess.LineStart, indexAccess.ColumnStart, indexAccess.Span))
            return;

        if (!HasProtocol(containerType, DunderNames.SetItem))
        {
            AddError(
                $"Type '{containerType.GetDisplayName()}' does not support item assignment " +
                "(missing '__setitem__' method).",
                indexAccess.LineStart, indexAccess.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: indexAccess.Span);
        }
    }

    private void ValidateMembership(BinaryOp binOp)
    {
        var containerType = Context.SemanticInfo.GetExpressionType(binOp.Right);
        if (containerType == null || containerType is UnknownType)
            return;

        if (ReportOptionalProtocol(containerType, "membership testing",
                binOp.LineStart, binOp.ColumnStart, binOp.Span))
            return;

        if (!HasProtocol(containerType, DunderNames.Contains))
        {
            AddError(
                $"Type '{containerType.GetDisplayName()}' does not support membership testing " +
                "(missing '__contains__' method).",
                binOp.LineStart, binOp.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: binOp.Span);
        }
    }

    private void ValidateFunctionCall(FunctionCall call)
    {
        // Check for len() calls. `` `len`(x) `` is a call to the user's own symbol, not the builtin,
        // so the protocol requirement does not apply to it (SPY0212's resolution half) — matching by
        // name alone made a backtick-escaped class named `len` unconstructible.
        if (AstHelper.UnwrapParenthesized(call.Function) is Identifier id
            && id.Name == "len" && !id.IsNameBacktickEscaped
            && call.Arguments.Length == 1)
        {
            // A BARE `len` can also be the user's own: value-position shadowing is legal (SPY0483),
            // so `def len(x: int)` followed by `len(4)` calls that function and the builtin's sized
            // requirement has nothing to do with it. The callee routing (recorded by the TypeChecker
            // while scopes are live) is the authoritative answer — it covers variable bindings that
            // have no FunctionSymbol and were invisible to the old GetCallTarget guard (#1326).
            if (Context.SemanticInfo.GetCalleeRouting(call) == CalleeRouting.UserSymbol)
                return;
            var callTarget = Context.SemanticInfo.GetCallTarget(call);
            if (callTarget != null && !Context.Builtins.IsBuiltinSymbol(callTarget))
                return;

            var argType = Context.SemanticInfo.GetExpressionType(call.Arguments[0]);
            if (argType == null || argType is UnknownType)
                return;

            if (ReportOptionalProtocol(argType, "len()",
                    call.LineStart, call.ColumnStart, call.Span))
                return;

            if (!HasProtocol(argType, DunderNames.Len))
            {
                AddError(
                    $"Type '{argType.GetDisplayName()}' does not support len() " +
                    "(missing '__len__' method). Consider implementing ISized interface.",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                    span: call.Span);
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="type"/> answers <paramref name="dunderName"/>. Delegates to
    /// <see cref="ProtocolMembership"/>, the single authority (#1808) — this validator used to
    /// carry its own copy, whose comment said "This duplicates logic", and the copy drifted: the
    /// interface-as-its-own-implementor arm lived here while <c>ClassifyTruthiness</c> and
    /// <c>InferReversedElementType</c> asked <c>Symbol.Methods</c> directly and answered "no" for
    /// the same receiver.
    /// </summary>
    private bool HasProtocol(SemanticType type, string dunderName)
        => Membership.Has(type, dunderName);

    private ProtocolMembership Membership
        => _membership ??= new ProtocolMembership(Context.SymbolTable, Context.Builtins);

    private ProtocolMembership? _membership;
}
