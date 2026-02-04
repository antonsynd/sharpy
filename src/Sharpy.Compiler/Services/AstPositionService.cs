using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Service for finding AST nodes at specific source positions.
/// </summary>
/// <remarks>
/// This service enables LSP-style position queries:
/// - Hover: Find what node is under the cursor
/// - Go-to-definition: Find identifier to resolve its symbol
/// - Completions: Find context for completion suggestions
///
/// All position parameters use 1-based line and column numbers,
/// matching editor conventions and AST node properties.
///
/// Example usage:
/// <code>
/// var service = new AstPositionService();
/// var node = service.FindInnermostNode(module, line: 5, column: 10);
/// if (node is Identifier id)
/// {
///     // Found an identifier at position
/// }
/// </code>
/// </remarks>
public sealed class AstPositionService
{
    /// <summary>
    /// Finds the innermost AST node containing the given position.
    /// </summary>
    /// <param name="module">The module to search</param>
    /// <param name="line">1-based line number</param>
    /// <param name="column">1-based column number</param>
    /// <returns>The innermost node, or null if position is outside all nodes</returns>
    public Node? FindInnermostNode(Module module, int line, int column)
    {
        if (module is null)
            throw new ArgumentNullException(nameof(module));
        if (line < 1)
            throw new ArgumentOutOfRangeException(nameof(line), "Line number must be at least 1.");
        if (column < 1)
            throw new ArgumentOutOfRangeException(nameof(column), "Column number must be at least 1.");

        var path = new List<Node>();
        FindNodesAtPosition(module, line, column, path);
        return path.Count > 0 ? path[^1] : null;
    }

    /// <summary>
    /// Finds all AST nodes containing the given position, from outermost to innermost.
    /// </summary>
    /// <param name="module">The module to search</param>
    /// <param name="line">1-based line number</param>
    /// <param name="column">1-based column number</param>
    /// <returns>A list of nodes from outermost to innermost, or empty if position is outside all nodes</returns>
    public IReadOnlyList<Node> FindAllContainingNodes(Module module, int line, int column)
    {
        if (module is null)
            throw new ArgumentNullException(nameof(module));
        if (line < 1)
            throw new ArgumentOutOfRangeException(nameof(line), "Line number must be at least 1.");
        if (column < 1)
            throw new ArgumentOutOfRangeException(nameof(column), "Column number must be at least 1.");

        var path = new List<Node>();
        FindNodesAtPosition(module, line, column, path);
        return path;
    }

    /// <summary>
    /// Finds the node of a specific type at the given position.
    /// Returns the innermost node of that type if multiple match.
    /// </summary>
    /// <typeparam name="T">The type of node to find</typeparam>
    /// <param name="module">The module to search</param>
    /// <param name="line">1-based line number</param>
    /// <param name="column">1-based column number</param>
    /// <returns>The innermost node of type T, or null if not found</returns>
    public T? FindNodeOfType<T>(Module module, int line, int column) where T : Node
    {
        var allNodes = FindAllContainingNodes(module, line, column);

        // Search from innermost to outermost
        for (int i = allNodes.Count - 1; i >= 0; i--)
        {
            if (allNodes[i] is T typed)
                return typed;
        }

        return null;
    }

    /// <summary>
    /// Recursively finds all nodes containing the position and adds them to the path.
    /// </summary>
    private void FindNodesAtPosition(Node node, int line, int column, List<Node> path)
    {
        if (!ContainsPosition(node, line, column))
            return;

        path.Add(node);

        // Check children and recurse into the one that contains the position
        foreach (var child in GetChildren(node))
        {
            if (ContainsPosition(child, line, column))
            {
                FindNodesAtPosition(child, line, column, path);
                return; // Only one child can contain the exact position
            }
        }
    }

    /// <summary>
    /// Checks if a node's span contains the given position.
    /// </summary>
    private static bool ContainsPosition(Node node, int line, int column)
    {
        // Handle nodes with no valid span
        if (node.LineStart == 0 && node.LineEnd == 0)
            return false;

        // Check if position is within the node's span
        if (line < node.LineStart || line > node.LineEnd)
            return false;

        // On the start line, column must be >= start column
        if (line == node.LineStart && column < node.ColumnStart)
            return false;

        // On the end line, column must be <= end column
        if (line == node.LineEnd && column > node.ColumnEnd)
            return false;

        return true;
    }

    /// <summary>
    /// Gets all child nodes of a given node.
    /// This handles all AST node types defined in the Sharpy compiler.
    /// </summary>
    private static IEnumerable<Node> GetChildren(Node node)
    {
        return node switch
        {
            // Module
            Module m => m.Body,

            // Statements
            ExpressionStatement es => SingleNode(es.Expression),
            Assignment a => TwoNodes(a.Target, a.Value),
            // Note: vd.Type is TypeAnnotation (not a Node), so only include InitialValue
            VariableDeclaration vd => NullableNode(vd.InitialValue),
            AssertStatement assert => CombineNullable(assert.Test, assert.Message),
            ReturnStatement ret => NullableNode(ret.Value),
            RaiseStatement raise => CombineNullable(raise.Exception, raise.Cause),

            IfStatement ifs => GetIfStatementChildren(ifs),
            WhileStatement ws => Combine(SingleNode(ws.Test), ws.Body, ws.ElseBody),
            ForStatement fs => Combine(TwoNodes(fs.Target, fs.Iterator), fs.Body, fs.ElseBody),
            TryStatement ts => GetTryStatementChildren(ts),

            FunctionDef fd => GetFunctionDefChildren(fd),
            ClassDef cd => GetClassDefChildren(cd),
            StructDef sd => GetStructDefChildren(sd),
            InterfaceDef id => GetInterfaceDefChildren(id),
            EnumDef ed => GetEnumDefChildren(ed),
            // Note: ta.Type and ta.FunctionType are TypeAnnotation/FunctionType (not Nodes)
            TypeAlias _ => Enumerable.Empty<Node>(),

            ImportStatement _ => Enumerable.Empty<Node>(),
            FromImportStatement _ => Enumerable.Empty<Node>(),

            PassStatement _ => Enumerable.Empty<Node>(),
            BreakStatement _ => Enumerable.Empty<Node>(),
            BreakWithFlagStatement _ => Enumerable.Empty<Node>(),
            ContinueStatement _ => Enumerable.Empty<Node>(),

            // Expressions
            IntegerLiteral _ => Enumerable.Empty<Node>(),
            FloatLiteral _ => Enumerable.Empty<Node>(),
            StringLiteral _ => Enumerable.Empty<Node>(),
            BooleanLiteral _ => Enumerable.Empty<Node>(),
            NoneLiteral _ => Enumerable.Empty<Node>(),
            EllipsisLiteral _ => Enumerable.Empty<Node>(),
            Identifier _ => Enumerable.Empty<Node>(),
            SuperExpression _ => Enumerable.Empty<Node>(),

            FStringLiteral fs => GetFStringChildren(fs),

            ListLiteral ll => ll.Elements,
            SetLiteral sl => sl.Elements,
            TupleLiteral tl => tl.Elements,
            DictLiteral dl => GetDictLiteralChildren(dl),

            ListComprehension lc => Combine(SingleNode(lc.Element), lc.Clauses),
            SetComprehension sc => Combine(SingleNode(sc.Element), sc.Clauses),
            DictComprehension dc => Combine(TwoNodes(dc.Key, dc.Value), dc.Clauses),

            ForClause fc => TwoNodes(fc.Target, fc.Iterator),
            IfClause ic => SingleNode(ic.Condition),

            MemberAccess ma => SingleNode(ma.Object),
            IndexAccess ia => TwoNodes(ia.Object, ia.Index),
            SliceAccess sa => GetSliceAccessChildren(sa),
            FunctionCall fc => GetFunctionCallChildren(fc),

            UnaryOp uo => SingleNode(uo.Operand),
            BinaryOp bo => TwoNodes(bo.Left, bo.Right),
            ComparisonChain cc => cc.Operands,

            ConditionalExpression ce => ThreeNodes(ce.Test, ce.ThenValue, ce.ElseValue),
            LambdaExpression le => SingleNode(le.Body),
            // Note: TargetType/CheckType are TypeAnnotation (not Nodes), so only include Value
            TypeCast tc => SingleNode(tc.Value),
            TypeCoercion tc => SingleNode(tc.Value),
            TypeCheck tc => SingleNode(tc.Value),
            Parenthesized p => SingleNode(p.Expression),
            WalrusExpression we => SingleNode(we.Value),
            // Note: ExceptionType is TypeAnnotation (not a Node), so only include Operand
            TryExpression te => SingleNode(te.Operand),
            MaybeExpression me => SingleNode(me.Operand),

            // Note: TypeAnnotation, FunctionType, TupleType are NOT Node subclasses,
            // so we cannot traverse them as AST nodes. They have their own position
            // properties but are separate from the Node hierarchy.

            _ => Enumerable.Empty<Node>()
        };
    }

    #region Helper methods for building child collections

    private static IEnumerable<Node> SingleNode(Node node) => [node];
    private static IEnumerable<Node> TwoNodes(Node a, Node b) => [a, b];
    private static IEnumerable<Node> ThreeNodes(Node a, Node b, Node c) => [a, b, c];

    private static IEnumerable<Node> NullableNode(Node? node)
    {
        if (node is not null)
            yield return node;
    }

    private static IEnumerable<Node> CombineNullable(Node? a, Node? b)
    {
        if (a is not null)
            yield return a;
        if (b is not null)
            yield return b;
    }

    private static IEnumerable<Node> Combine(IEnumerable<Node> a, IEnumerable<Node> b)
        => a.Concat(b);

    private static IEnumerable<Node> Combine(IEnumerable<Node> a, IEnumerable<Node> b, IEnumerable<Node> c)
        => a.Concat(b).Concat(c);

    #endregion

    #region Complex node children helpers

    private static IEnumerable<Node> GetIfStatementChildren(IfStatement ifs)
    {
        yield return ifs.Test;
        foreach (var stmt in ifs.ThenBody)
            yield return stmt;
        // Note: ElifClause is not a Node, so we extract its Test and Body
        foreach (var elif in ifs.ElifClauses)
        {
            yield return elif.Test;
            foreach (var stmt in elif.Body)
                yield return stmt;
        }
        foreach (var stmt in ifs.ElseBody)
            yield return stmt;
    }

    private static IEnumerable<Node> GetTryStatementChildren(TryStatement ts)
    {
        foreach (var stmt in ts.Body)
            yield return stmt;
        // Note: ExceptHandler.ExceptionType is TypeAnnotation (not a Node)
        foreach (var handler in ts.Handlers)
        {
            foreach (var stmt in handler.Body)
                yield return stmt;
        }
        foreach (var stmt in ts.ElseBody)
            yield return stmt;
        foreach (var stmt in ts.FinallyBody)
            yield return stmt;
    }

    private static IEnumerable<Node> GetFunctionDefChildren(FunctionDef fd)
    {
        // Note: Parameter.Type and ReturnType are TypeAnnotation (not Nodes)
        // We only include DefaultValue expressions and body statements
        foreach (var param in fd.Parameters)
        {
            if (param.DefaultValue is not null)
                yield return param.DefaultValue;
        }
        foreach (var stmt in fd.Body)
            yield return stmt;
    }

    // Note: BaseClasses/BaseInterfaces are TypeAnnotation arrays (not Nodes)
    private static IEnumerable<Node> GetClassDefChildren(ClassDef cd) => cd.Body;
    private static IEnumerable<Node> GetStructDefChildren(StructDef sd) => sd.Body;
    private static IEnumerable<Node> GetInterfaceDefChildren(InterfaceDef id) => id.Body;

    private static IEnumerable<Node> GetEnumDefChildren(EnumDef ed)
    {
        // Note: EnumMember is not a Node, so we extract Value
        foreach (var member in ed.Members)
        {
            if (member.Value is not null)
                yield return member.Value;
        }
    }

    private static IEnumerable<Node> GetFStringChildren(FStringLiteral fs)
    {
        // Note: FStringPart is not a Node, so we extract Expression
        foreach (var part in fs.Parts)
        {
            if (part.Expression is not null)
                yield return part.Expression;
        }
    }

    private static IEnumerable<Node> GetDictLiteralChildren(DictLiteral dl)
    {
        // Note: DictEntry is not a Node, so we extract Key and Value
        foreach (var entry in dl.Entries)
        {
            yield return entry.Key;
            yield return entry.Value;
        }
    }

    private static IEnumerable<Node> GetSliceAccessChildren(SliceAccess sa)
    {
        yield return sa.Object;
        if (sa.Start is not null)
            yield return sa.Start;
        if (sa.Stop is not null)
            yield return sa.Stop;
        if (sa.Step is not null)
            yield return sa.Step;
    }

    private static IEnumerable<Node> GetFunctionCallChildren(FunctionCall fc)
    {
        yield return fc.Function;
        foreach (var arg in fc.Arguments)
            yield return arg;
        // Note: KeywordArgument is not a Node, so we extract Value
        foreach (var kwarg in fc.KeywordArguments)
            yield return kwarg.Value;
    }

    #endregion
}
