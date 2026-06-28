using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// Utility class for dumping AST nodes in a human-readable tree format.
/// Inherits from <see cref="AstVisitor"/> to leverage centralized node dispatch,
/// eliminating a duplicate switch statement for AST node types.
/// </summary>
internal class AstDumper : AstVisitor
{
    private readonly StringBuilder _output;
    private const string IndentUnit = "  ";

    // Tree-drawing context: these fields are set by VisitChild() before dispatching
    // each child node. Any Visit*() override that calls VisitChild() multiple times
    // must call CaptureContext() FIRST to snapshot the current values, because each
    // VisitChild() call overwrites them.
    private int _depth;
    private string _indent = "";
    private string _prefix = "";
    private string _childPrefix = "";

    public AstDumper()
    {
        _output = new StringBuilder();
    }

    /// <summary>
    /// Dump a module to a human-readable string
    /// </summary>
    public string Dump(Module module)
    {
        _output.Clear();
        _output.AppendLine(CultureInfo.InvariantCulture, $"Module @ L{module.LineStart}:C{module.ColumnStart}");

        if (!string.IsNullOrEmpty(module.DocString))
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{IndentUnit}DocString: \"{EscapeString(module.DocString)}\"");
        }

        _output.AppendLine(CultureInfo.InvariantCulture, $"{IndentUnit}Body: [{module.Body.Length} statement(s)]");

        for (int i = 0; i < module.Body.Length; i++)
        {
            VisitChild(module.Body[i], 2, i == module.Body.Length - 1);
        }

        return _output.ToString();
    }

    /// <summary>
    /// Visit a child node with specific depth and isLast context.
    /// Sets up the indent/prefix fields and dispatches via AstVisitor.
    /// </summary>
    private void VisitChild(Node node, int depth, bool isLast)
    {
        _depth = depth;
        _indent = new string(' ', depth * IndentUnit.Length);
        _prefix = isLast ? "└─ " : "├─ ";
        _childPrefix = isLast ? "   " : "│  ";
        Visit(node);
    }

    #region Default handling

    public override void DefaultVisit(Node node)
    {
        _output.AppendLine(CultureInfo.InvariantCulture, $"{_indent}{_prefix}{node.GetType().Name} @ L{node.LineStart}:C{node.ColumnStart}");
    }

    #endregion

    #region Statements - Simple

    public override void VisitExpressionStatement(ExpressionStatement node)
    {
        var (indent, prefix, depth) = CaptureContext();
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}ExpressionStatement @ L{node.LineStart}:C{node.ColumnStart}");
        VisitChild(node.Expression, depth + 1, true);
    }

    public override void VisitAssignment(Assignment node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}Assignment @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Operator: {node.Operator}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Target:");
        VisitChild(node.Target, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Value:");
        VisitChild(node.Value, depth + 2, true);
    }

    public override void VisitVariableDeclaration(VariableDeclaration node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}VariableDeclaration @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Name: {node.Name}");
        DumpDecorators(node.Decorators, depth, indent, childPrefix);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}IsConst: {node.IsConst}");
        if (node.Type != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Type:");
            DumpTypeAnnotation(node.Type, depth + 2, node.InitialValue == null);
        }
        if (node.InitialValue != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}InitialValue:");
            VisitChild(node.InitialValue, depth + 2, true);
        }
    }

    public override void VisitReturnStatement(ReturnStatement node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}ReturnStatement @ L{node.LineStart}:C{node.ColumnStart}");
        if (node.Value != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Value:");
            VisitChild(node.Value, depth + 1, true);
        }
    }

    public override void VisitPassStatement(PassStatement node)
    {
        _output.AppendLine(CultureInfo.InvariantCulture, $"{_indent}{_prefix}PassStatement @ L{node.LineStart}:C{node.ColumnStart}");
    }

    public override void VisitBreakStatement(BreakStatement node)
    {
        _output.AppendLine(CultureInfo.InvariantCulture, $"{_indent}{_prefix}BreakStatement @ L{node.LineStart}:C{node.ColumnStart}");
    }

    public override void VisitContinueStatement(ContinueStatement node)
    {
        _output.AppendLine(CultureInfo.InvariantCulture, $"{_indent}{_prefix}ContinueStatement @ L{node.LineStart}:C{node.ColumnStart}");
    }

    public override void VisitRaiseStatement(RaiseStatement node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}RaiseStatement @ L{node.LineStart}:C{node.ColumnStart}");
        if (node.Exception != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Exception:");
            VisitChild(node.Exception, depth + 1, true);
        }
    }

    public override void VisitAssertStatement(AssertStatement node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}AssertStatement @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Test:");
        VisitChild(node.Test, depth + 1, node.Message == null);
        if (node.Message != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Message:");
            VisitChild(node.Message, depth + 1, true);
        }
    }

    public override void VisitYieldStatement(YieldStatement node)
    {
        DefaultVisit(node);
    }

    public override void VisitBreakWithFlagStatement(BreakWithFlagStatement node)
    {
        DefaultVisit(node);
    }

    #endregion

    #region Statements - Compound

    public override void VisitIfStatement(IfStatement node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}IfStatement @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Test:");
        VisitChild(node.Test, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}ThenBody: [{node.ThenBody.Length} statement(s)]");
        for (int i = 0; i < node.ThenBody.Length; i++)
        {
            VisitChild(node.ThenBody[i], depth + 2, i == node.ThenBody.Length - 1);
        }
        if (node.ElifClauses.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}ElifClauses: [{node.ElifClauses.Length}]");
            for (int i = 0; i < node.ElifClauses.Length; i++)
            {
                var elif = node.ElifClauses[i];
                var elifIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                var elifPrefix = i == node.ElifClauses.Length - 1 && node.ElseBody.Length == 0 ? "└─ " : "├─ ";
                _output.AppendLine(CultureInfo.InvariantCulture, $"{elifIndent}{elifPrefix}ElifClause @ L{elif.LineStart}:C{elif.ColumnStart}");
                _output.AppendLine(CultureInfo.InvariantCulture, $"{elifIndent}   Test:");
                VisitChild(elif.Test, depth + 3, false);
                _output.AppendLine(CultureInfo.InvariantCulture, $"{elifIndent}   Body: [{elif.Body.Length} statement(s)]");
                for (int j = 0; j < elif.Body.Length; j++)
                {
                    VisitChild(elif.Body[j], depth + 3, j == elif.Body.Length - 1);
                }
            }
        }
        if (node.ElseBody.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}ElseBody: [{node.ElseBody.Length} statement(s)]");
            for (int i = 0; i < node.ElseBody.Length; i++)
            {
                VisitChild(node.ElseBody[i], depth + 2, i == node.ElseBody.Length - 1);
            }
        }
    }

    public override void VisitWhileStatement(WhileStatement node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}WhileStatement @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Test:");
        VisitChild(node.Test, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Body: [{node.Body.Length} statement(s)]");
        for (int i = 0; i < node.Body.Length; i++)
        {
            VisitChild(node.Body[i], depth + 2, i == node.Body.Length - 1);
        }
    }

    public override void VisitForStatement(ForStatement node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}ForStatement @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Target:");
        VisitChild(node.Target, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Iterator:");
        VisitChild(node.Iterator, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Body: [{node.Body.Length} statement(s)]");
        for (int i = 0; i < node.Body.Length; i++)
        {
            VisitChild(node.Body[i], depth + 2, i == node.Body.Length - 1);
        }
    }

    public override void VisitTryStatement(TryStatement node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}TryStatement @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Body: [{node.Body.Length} statement(s)]");
        for (int i = 0; i < node.Body.Length; i++)
        {
            VisitChild(node.Body[i], depth + 2, i == node.Body.Length - 1);
        }
        if (node.Handlers.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Handlers: [{node.Handlers.Length}]");
            for (int i = 0; i < node.Handlers.Length; i++)
            {
                var handler = node.Handlers[i];
                var handlerIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                var handlerPrefix = i == node.Handlers.Length - 1 && node.ElseBody.Length == 0 && node.FinallyBody.Length == 0 ? "└─ " : "├─ ";
                var exceptLabel = handler.IsExceptStar ? "ExceptStarHandler" : "ExceptHandler";
                _output.AppendLine(CultureInfo.InvariantCulture, $"{handlerIndent}{handlerPrefix}{exceptLabel} @ L{handler.LineStart}:C{handler.ColumnStart}");
                if (handler.ExceptionType != null)
                {
                    _output.AppendLine(CultureInfo.InvariantCulture, $"{handlerIndent}   ExceptionType:");
                    DumpTypeAnnotation(handler.ExceptionType, depth + 3, handler.Name == null);
                }
                if (handler.Name != null)
                {
                    _output.AppendLine(CultureInfo.InvariantCulture, $"{handlerIndent}   Name: {handler.Name}");
                }
                _output.AppendLine(CultureInfo.InvariantCulture, $"{handlerIndent}   Body: [{handler.Body.Length} statement(s)]");
                for (int j = 0; j < handler.Body.Length; j++)
                {
                    VisitChild(handler.Body[j], depth + 3, j == handler.Body.Length - 1);
                }
            }
        }
        if (node.ElseBody.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}ElseBody: [{node.ElseBody.Length} statement(s)]");
            for (int i = 0; i < node.ElseBody.Length; i++)
            {
                VisitChild(node.ElseBody[i], depth + 2, i == node.ElseBody.Length - 1 && node.FinallyBody.Length == 0);
            }
        }
        if (node.FinallyBody.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}FinallyBody: [{node.FinallyBody.Length} statement(s)]");
            for (int i = 0; i < node.FinallyBody.Length; i++)
            {
                VisitChild(node.FinallyBody[i], depth + 2, i == node.FinallyBody.Length - 1);
            }
        }
    }

    public override void VisitWithStatement(WithStatement node)
    {
        DefaultVisit(node);
    }

    #endregion

    #region Statements - Definitions

    public override void VisitFunctionDef(FunctionDef node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}FunctionDef @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Name: {node.Name}");
        if (node.DocString != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}DocString: \"{EscapeString(node.DocString)}\"");
        }
        DumpDecorators(node.Decorators, depth, indent, childPrefix);
        if (node.Parameters.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Parameters: [{node.Parameters.Length}]");
            for (int i = 0; i < node.Parameters.Length; i++)
            {
                DumpParameter(node.Parameters[i], depth + 2, i == node.Parameters.Length - 1);
            }
        }
        if (node.ReturnType != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}ReturnType:");
            DumpTypeAnnotation(node.ReturnType, depth + 2, false);
        }
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Body: [{node.Body.Length} statement(s)]");
        for (int i = 0; i < node.Body.Length; i++)
        {
            VisitChild(node.Body[i], depth + 2, i == node.Body.Length - 1);
        }
    }

    public override void VisitClassDef(ClassDef node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}ClassDef @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Name: {node.Name}");
        if (node.DocString != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}DocString: \"{EscapeString(node.DocString)}\"");
        }
        if (node.TypeParameters.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}TypeParameters: [{string.Join(", ", node.TypeParameters.Select(FormatTypeParam))}]");
        }
        DumpDecorators(node.Decorators, depth, indent, childPrefix);
        if (node.BaseClasses.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}BaseClasses: [{node.BaseClasses.Length}]");
            for (int i = 0; i < node.BaseClasses.Length; i++)
            {
                DumpTypeAnnotation(node.BaseClasses[i], depth + 2, i == node.BaseClasses.Length - 1);
            }
        }
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Body: [{node.Body.Length} statement(s)]");
        for (int i = 0; i < node.Body.Length; i++)
        {
            VisitChild(node.Body[i], depth + 2, i == node.Body.Length - 1);
        }
    }

    public override void VisitStructDef(StructDef node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}StructDef @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Name: {node.Name}");
        if (node.DocString != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}DocString: \"{EscapeString(node.DocString)}\"");
        }
        DumpDecorators(node.Decorators, depth, indent, childPrefix);
        if (node.TypeParameters.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}TypeParameters: [{string.Join(", ", node.TypeParameters.Select(FormatTypeParam))}]");
        }
        if (node.BaseClasses.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}BaseClasses: [{node.BaseClasses.Length}]");
            for (int i = 0; i < node.BaseClasses.Length; i++)
            {
                DumpTypeAnnotation(node.BaseClasses[i], depth + 2, i == node.BaseClasses.Length - 1);
            }
        }
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Body: [{node.Body.Length} statement(s)]");
        for (int i = 0; i < node.Body.Length; i++)
        {
            VisitChild(node.Body[i], depth + 2, i == node.Body.Length - 1);
        }
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}InterfaceDef @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Name: {node.Name}");
        if (node.DocString != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}DocString: \"{EscapeString(node.DocString)}\"");
        }
        if (node.TypeParameters.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}TypeParameters: [{string.Join(", ", node.TypeParameters.Select(FormatTypeParam))}]");
        }
        if (node.BaseInterfaces.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}BaseInterfaces: [{node.BaseInterfaces.Length}]");
            for (int i = 0; i < node.BaseInterfaces.Length; i++)
            {
                DumpTypeAnnotation(node.BaseInterfaces[i], depth + 2, i == node.BaseInterfaces.Length - 1);
            }
        }
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Body: [{node.Body.Length} statement(s)]");
        for (int i = 0; i < node.Body.Length; i++)
        {
            VisitChild(node.Body[i], depth + 2, i == node.Body.Length - 1);
        }
    }

    public override void VisitEnumDef(EnumDef node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}EnumDef @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Name: {node.Name}");
        if (node.DocString != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}DocString: \"{EscapeString(node.DocString)}\"");
        }
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Members: [{node.Members.Length}]");
        for (int i = 0; i < node.Members.Length; i++)
        {
            var member = node.Members[i];
            var memIndent = new string(' ', (depth + 2) * IndentUnit.Length);
            var memPrefix = i == node.Members.Length - 1 ? "└─ " : "├─ ";
            if (member.Value != null)
            {
                _output.AppendLine(CultureInfo.InvariantCulture, $"{memIndent}{memPrefix}{member.Name} @ L{member.LineStart}:C{member.ColumnStart} =");
                VisitChild(member.Value, depth + 3, true);
            }
            else
            {
                _output.AppendLine(CultureInfo.InvariantCulture, $"{memIndent}{memPrefix}{member.Name} @ L{member.LineStart}:C{member.ColumnStart}");
            }
        }
    }

    public override void VisitDelegateDef(DelegateDef node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}DelegateDef @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Name: {node.Name}");
        if (node.DocString != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}DocString: \"{EscapeString(node.DocString)}\"");
        }
        if (node.TypeParameters.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}TypeParameters: [{string.Join(", ", node.TypeParameters.Select(FormatTypeParam))}]");
        }
        if (node.Parameters.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Parameters: [{node.Parameters.Length}]");
            for (int i = 0; i < node.Parameters.Length; i++)
            {
                DumpParameter(node.Parameters[i], depth + 2, i == node.Parameters.Length - 1);
            }
        }
        if (node.ReturnType != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}ReturnType:");
            DumpTypeAnnotation(node.ReturnType, depth + 2, true);
        }
    }

    public override void VisitPropertyDef(PropertyDef node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}PropertyDef @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Name: {node.Name}");
        DumpDecorators(node.Decorators, depth, indent, childPrefix);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Accessor: {node.Accessor}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}FunctionStyle: {node.IsFunctionStyle}");
        if (node.ExplicitInterface != null)
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}ExplicitInterface: {node.ExplicitInterface}");
        if (node.Type != null)
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Type: {node.Type.Name}");
        if (node.ReturnType != null)
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}ReturnType: {node.ReturnType.Name}");
        if (node.DefaultValue != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}DefaultValue:");
            VisitChild(node.DefaultValue, depth + 2, true);
        }
        if (node.IsFunctionStyle && node.Body.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Body: [{node.Body.Length}]");
            for (int i = 0; i < node.Body.Length; i++)
            {
                VisitChild(node.Body[i], depth + 2, i == node.Body.Length - 1);
            }
        }
    }

    public override void VisitEventDef(EventDef node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}EventDef @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Name: {node.Name}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Accessor: {node.Accessor}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}FunctionStyle: {node.IsFunctionStyle}");
        if (node.Type != null)
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Type: {node.Type.Name}");
        if (node.Parameters.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Parameters: [{node.Parameters.Length}]");
            for (int i = 0; i < node.Parameters.Length; i++)
            {
                DumpParameter(node.Parameters[i], depth + 2, i == node.Parameters.Length - 1);
            }
        }
        if (node.IsFunctionStyle && node.Body.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Body: [{node.Body.Length}]");
            for (int i = 0; i < node.Body.Length; i++)
            {
                VisitChild(node.Body[i], depth + 2, i == node.Body.Length - 1);
            }
        }
        DumpDecorators(node.Decorators, depth, indent, childPrefix);
    }

    public override void VisitTypeAlias(TypeAlias node)
    {
        DefaultVisit(node);
    }

    #endregion

    #region Statements - Imports

    public override void VisitImportStatement(ImportStatement node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}ImportStatement @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Names: [{node.Names.Length}]");
        for (int i = 0; i < node.Names.Length; i++)
        {
            var import = node.Names[i];
            var impIndent = new string(' ', (depth + 2) * IndentUnit.Length);
            var impPrefix = i == node.Names.Length - 1 ? "└─ " : "├─ ";
            if (import.AsName != null)
            {
                _output.AppendLine(CultureInfo.InvariantCulture, $"{impIndent}{impPrefix}{import.Name} as {import.AsName} @ L{import.LineStart}:C{import.ColumnStart}");
            }
            else
            {
                _output.AppendLine(CultureInfo.InvariantCulture, $"{impIndent}{impPrefix}{import.Name} @ L{import.LineStart}:C{import.ColumnStart}");
            }
        }
    }

    public override void VisitFromImportStatement(FromImportStatement node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}FromImportStatement @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Module: {node.Module}");
        if (node.ImportAll)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}ImportAll: true");
        }
        else
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Names: [{node.Names.Length}]");
            for (int i = 0; i < node.Names.Length; i++)
            {
                var import = node.Names[i];
                var impIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                var impPrefix = i == node.Names.Length - 1 ? "└─ " : "├─ ";
                if (import.AsName != null)
                {
                    _output.AppendLine(CultureInfo.InvariantCulture, $"{impIndent}{impPrefix}{import.Name} as {import.AsName} @ L{import.LineStart}:C{import.ColumnStart}");
                }
                else
                {
                    _output.AppendLine(CultureInfo.InvariantCulture, $"{impIndent}{impPrefix}{import.Name} @ L{import.LineStart}:C{import.ColumnStart}");
                }
            }
        }
    }

    #endregion

    #region Statements - Future

    public override void VisitMatchStatement(MatchStatement node)
    {
        DefaultVisit(node);
    }

    public override void VisitUnionDef(UnionDef node)
    {
        DefaultVisit(node);
    }

    #endregion

    #region Expressions - Literals

    public override void VisitIntegerLiteral(IntegerLiteral node)
    {
        var intSuffix = node.Suffix != null ? $" ({node.Suffix})" : "";
        _output.AppendLine(CultureInfo.InvariantCulture, $"{_indent}{_prefix}IntegerLiteral: {node.Value}{intSuffix} @ L{node.LineStart}:C{node.ColumnStart}");
    }

    public override void VisitFloatLiteral(FloatLiteral node)
    {
        var floatSuffix = node.Suffix != null ? $" ({node.Suffix})" : "";
        _output.AppendLine(FormattableString.Invariant($"{_indent}{_prefix}FloatLiteral: {node.Value}{floatSuffix} @ L{node.LineStart}:C{node.ColumnStart}"));
    }

    public override void VisitStringLiteral(StringLiteral node)
    {
        var strPrefix = node.IsRaw ? "r" : "";
        _output.AppendLine(CultureInfo.InvariantCulture, $"{_indent}{_prefix}StringLiteral: {strPrefix}\"{EscapeString(node.Value)}\" @ L{node.LineStart}:C{node.ColumnStart}");
    }

    public override void VisitBytesLiteral(BytesLiteralExpression node)
    {
        _output.AppendLine(CultureInfo.InvariantCulture, $"{_indent}{_prefix}BytesLiteral: b\"{EscapeString(node.Value)}\" @ L{node.LineStart}:C{node.ColumnStart}");
    }

    public override void VisitFStringLiteral(FStringLiteral node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}FStringLiteral @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Parts: [{node.Parts.Length}]");
        for (int i = 0; i < node.Parts.Length; i++)
        {
            var part = node.Parts[i];
            var partIndent = new string(' ', (depth + 2) * IndentUnit.Length);
            var partPrefix = i == node.Parts.Length - 1 ? "└─ " : "├─ ";
            if (part.Text != null)
            {
                _output.AppendLine(CultureInfo.InvariantCulture, $"{partIndent}{partPrefix}Text: \"{EscapeString(part.Text)}\"");
            }
            else if (part.Expression != null)
            {
                _output.AppendLine(CultureInfo.InvariantCulture, $"{partIndent}{partPrefix}Expression:");
                VisitChild(part.Expression, depth + 3, true);
            }
        }
    }

    public override void VisitTStringLiteral(TStringLiteral node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}TStringLiteral @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Parts: [{node.Parts.Length}]");
        for (int i = 0; i < node.Parts.Length; i++)
        {
            var part = node.Parts[i];
            var partIndent = new string(' ', (depth + 2) * IndentUnit.Length);
            var partPrefix = i == node.Parts.Length - 1 ? "└─ " : "├─ ";
            if (part.Text != null)
            {
                _output.AppendLine(CultureInfo.InvariantCulture, $"{partIndent}{partPrefix}Text: \"{EscapeString(part.Text)}\"");
            }
            else if (part.Expression != null)
            {
                _output.AppendLine(CultureInfo.InvariantCulture, $"{partIndent}{partPrefix}Expression:");
                VisitChild(part.Expression, depth + 3, true);
            }
        }
    }

    public override void VisitBooleanLiteral(BooleanLiteral node)
    {
        _output.AppendLine(CultureInfo.InvariantCulture, $"{_indent}{_prefix}BooleanLiteral: {node.Value} @ L{node.LineStart}:C{node.ColumnStart}");
    }

    public override void VisitNoneLiteral(NoneLiteral node)
    {
        _output.AppendLine(CultureInfo.InvariantCulture, $"{_indent}{_prefix}NoneLiteral @ L{node.LineStart}:C{node.ColumnStart}");
    }

    public override void VisitEllipsisLiteral(EllipsisLiteral node)
    {
        _output.AppendLine(CultureInfo.InvariantCulture, $"{_indent}{_prefix}EllipsisLiteral @ L{node.LineStart}:C{node.ColumnStart}");
    }

    #endregion

    #region Expressions - Collections

    public override void VisitListLiteral(ListLiteral node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}ListLiteral @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Elements: [{node.Elements.Length}]");
        for (int i = 0; i < node.Elements.Length; i++)
        {
            VisitChild(node.Elements[i], depth + 2, i == node.Elements.Length - 1);
        }
    }

    public override void VisitDictLiteral(DictLiteral node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}DictLiteral @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Entries: [{node.Entries.Length}]");
        for (int i = 0; i < node.Entries.Length; i++)
        {
            var entry = node.Entries[i];
            var entryIndent = new string(' ', (depth + 2) * IndentUnit.Length);
            var entryPrefix = i == node.Entries.Length - 1 ? "└─ " : "├─ ";
            _output.AppendLine(CultureInfo.InvariantCulture, $"{entryIndent}{entryPrefix}Entry:");
            if (entry.Key != null)
            {
                _output.AppendLine(CultureInfo.InvariantCulture, $"{entryIndent}   Key:");
                VisitChild(entry.Key, depth + 3, false);
            }
            _output.AppendLine(CultureInfo.InvariantCulture, $"{entryIndent}   Value:");
            VisitChild(entry.Value, depth + 3, true);
        }
    }

    public override void VisitSetLiteral(SetLiteral node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}SetLiteral @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Elements: [{node.Elements.Length}]");
        for (int i = 0; i < node.Elements.Length; i++)
        {
            VisitChild(node.Elements[i], depth + 2, i == node.Elements.Length - 1);
        }
    }

    public override void VisitTupleLiteral(TupleLiteral node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}TupleLiteral @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Elements: [{node.Elements.Length}]");
        for (int i = 0; i < node.Elements.Length; i++)
        {
            VisitChild(node.Elements[i], depth + 2, i == node.Elements.Length - 1);
        }
    }

    #endregion

    #region Expressions - Comprehensions

    public override void VisitListComprehension(ListComprehension node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}ListComprehension @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Element:");
        VisitChild(node.Element, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Clauses: [{node.Clauses.Length}]");
        for (int i = 0; i < node.Clauses.Length; i++)
        {
            DumpComprehensionClause(node.Clauses[i], depth + 2, i == node.Clauses.Length - 1);
        }
    }

    public override void VisitSetComprehension(SetComprehension node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}SetComprehension @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Element:");
        VisitChild(node.Element, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Clauses: [{node.Clauses.Length}]");
        for (int i = 0; i < node.Clauses.Length; i++)
        {
            DumpComprehensionClause(node.Clauses[i], depth + 2, i == node.Clauses.Length - 1);
        }
    }

    public override void VisitDictComprehension(DictComprehension node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}DictComprehension @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Key:");
        VisitChild(node.Key, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Value:");
        VisitChild(node.Value, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Clauses: [{node.Clauses.Length}]");
        for (int i = 0; i < node.Clauses.Length; i++)
        {
            DumpComprehensionClause(node.Clauses[i], depth + 2, i == node.Clauses.Length - 1);
        }
    }

    public override void VisitDictSpreadComprehension(DictSpreadComprehension node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}DictSpreadComprehension @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Spread:");
        VisitChild(node.Spread, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Clauses: [{node.Clauses.Length}]");
        for (int i = 0; i < node.Clauses.Length; i++)
        {
            DumpComprehensionClause(node.Clauses[i], depth + 2, i == node.Clauses.Length - 1);
        }
    }

    #endregion

    #region Expressions - Primaries

    public override void VisitIdentifier(Identifier node)
    {
        _output.AppendLine(CultureInfo.InvariantCulture, $"{_indent}{_prefix}Identifier: {node.Name} @ L{node.LineStart}:C{node.ColumnStart}");
    }

    public override void VisitMemberAccess(MemberAccess node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        var nullCond = node.IsNullConditional ? "?." : ".";
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}MemberAccess ({nullCond}) @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Object:");
        VisitChild(node.Object, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Member: {node.Member}");
    }

    public override void VisitIndexAccess(IndexAccess node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}IndexAccess @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Object:");
        VisitChild(node.Object, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Index:");
        VisitChild(node.Index, depth + 2, true);
    }

    public override void VisitSliceAccess(SliceAccess node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}SliceAccess @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Object:");
        VisitChild(node.Object, depth + 2, false);
        if (node.Start != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Start:");
            VisitChild(node.Start, depth + 2, node.Stop == null && node.Step == null);
        }
        if (node.Stop != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Stop:");
            VisitChild(node.Stop, depth + 2, node.Step == null);
        }
        if (node.Step != null)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Step:");
            VisitChild(node.Step, depth + 2, true);
        }
    }

    public override void VisitFunctionCall(FunctionCall node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}FunctionCall @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Function:");
        VisitChild(node.Function, depth + 2, false);
        if (node.Arguments.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Arguments: [{node.Arguments.Length}]");
            for (int i = 0; i < node.Arguments.Length; i++)
            {
                VisitChild(node.Arguments[i], depth + 2, i == node.Arguments.Length - 1 && node.KeywordArguments.Length == 0);
            }
        }
        if (node.KeywordArguments.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}KeywordArguments: [{node.KeywordArguments.Length}]");
            for (int i = 0; i < node.KeywordArguments.Length; i++)
            {
                var kwarg = node.KeywordArguments[i];
                var kwIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                var kwPrefix = i == node.KeywordArguments.Length - 1 ? "└─ " : "├─ ";
                _output.AppendLine(CultureInfo.InvariantCulture, $"{kwIndent}{kwPrefix}{kwarg.Name} @ L{kwarg.LineStart}:C{kwarg.ColumnStart}:");
                VisitChild(kwarg.Value, depth + 3, true);
            }
        }
    }

    #endregion

    #region Expressions - Operators

    public override void VisitUnaryOp(UnaryOp node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}UnaryOp: {node.Operator} @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Operand:");
        VisitChild(node.Operand, depth + 2, true);
    }

    public override void VisitBinaryOp(BinaryOp node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}BinaryOp: {node.Operator} @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Left:");
        VisitChild(node.Left, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Right:");
        VisitChild(node.Right, depth + 2, true);
    }

    public override void VisitComparisonChain(ComparisonChain node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}ComparisonChain @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Operands: [{node.Operands.Length}]");
        for (int i = 0; i < node.Operands.Length; i++)
        {
            VisitChild(node.Operands[i], depth + 2, i == node.Operands.Length - 1);
        }
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Operators: [{string.Join(", ", node.Operators)}]");
    }

    #endregion

    #region Expressions - Advanced

    public override void VisitConditionalExpression(ConditionalExpression node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}ConditionalExpression @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Test:");
        VisitChild(node.Test, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}ThenValue:");
        VisitChild(node.ThenValue, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}ElseValue:");
        VisitChild(node.ElseValue, depth + 2, true);
    }

    public override void VisitLambdaExpression(LambdaExpression node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}LambdaExpression @ L{node.LineStart}:C{node.ColumnStart}");
        if (node.Parameters.Length > 0)
        {
            _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Parameters: [{node.Parameters.Length}]");
            for (int i = 0; i < node.Parameters.Length; i++)
            {
                DumpParameter(node.Parameters[i], depth + 2, i == node.Parameters.Length - 1);
            }
        }
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Body:");
        VisitChild(node.Body, depth + 2, true);
    }

    public override void VisitTypeCoercion(TypeCoercion node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}TypeCoercion @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Value:");
        VisitChild(node.Value, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}TargetType:");
        DumpTypeAnnotation(node.TargetType, depth + 2, true);
    }

    public override void VisitTypeCheck(TypeCheck node)
    {
        var (indent, prefix, depth) = CaptureContext();
        var childPrefix = _childPrefix;
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}TypeCheck @ L{node.LineStart}:C{node.ColumnStart}");
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Value:");
        VisitChild(node.Value, depth + 2, false);
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}CheckType:");
        DumpTypeAnnotation(node.CheckType, depth + 2, true);
    }

    public override void VisitParenthesized(Parenthesized node)
    {
        var (indent, prefix, depth) = CaptureContext();
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}Parenthesized @ L{node.LineStart}:C{node.ColumnStart}");
        VisitChild(node.Expression, depth + 1, true);
    }

    public override void VisitSuperExpression(SuperExpression node)
    {
        DefaultVisit(node);
    }

    public override void VisitWalrusExpression(WalrusExpression node)
    {
        DefaultVisit(node);
    }

    public override void VisitTryExpression(TryExpression node)
    {
        DefaultVisit(node);
    }

    public override void VisitMaybeExpression(MaybeExpression node)
    {
        DefaultVisit(node);
    }

    public override void VisitStarExpression(StarExpression node)
    {
        DefaultVisit(node);
    }

    public override void VisitSpreadElement(SpreadElement node)
    {
        DefaultVisit(node);
    }

    public override void VisitAwaitExpression(AwaitExpression node)
    {
        DefaultVisit(node);
    }

    public override void VisitMatchExpression(MatchExpression node)
    {
        DefaultVisit(node);
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Captures the current context fields before they are mutated by child visits.
    /// </summary>
    private (string indent, string prefix, int depth) CaptureContext()
    {
        return (_indent, _prefix, _depth);
    }

    private void DumpParameter(Parameter param, int depth, bool isLast)
    {
        var indent = new string(' ', depth * IndentUnit.Length);
        var prefix = isLast ? "└─ " : "├─ ";

        _output.Append(CultureInfo.InvariantCulture, $"{indent}{prefix}Parameter: {param.Name} @ L{param.LineStart}:C{param.ColumnStart}");
        if (param.Type != null)
        {
            _output.Append(CultureInfo.InvariantCulture, $" : {FormatType(param.Type)}");
        }
        if (param.DefaultValue != null)
        {
            _output.AppendLine(" =");
            VisitChild(param.DefaultValue, depth + 1, true);
        }
        else
        {
            _output.AppendLine();
        }
    }

    private void DumpTypeAnnotation(TypeAnnotation type, int depth, bool isLast)
    {
        var indent = new string(' ', depth * IndentUnit.Length);
        var prefix = isLast ? "└─ " : "├─ ";
        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}{FormatType(type)} @ L{type.LineStart}:C{type.ColumnStart}");
    }

    private string FormatType(TypeAnnotation type)
    {
        var result = type.Name;
        if (type.TypeArguments.Length > 0)
        {
            var args = string.Join(", ", type.TypeArguments.Select(FormatType));
            result += $"[{args}]";
        }
        if (type.IsOptional)
        {
            result += "?";
        }
        return result;
    }

    private static string FormatTypeParam(TypeParameterDef tp)
    {
        return tp.Variance switch
        {
            TypeParameterVariance.Covariant => $"out {tp.Name}",
            TypeParameterVariance.Contravariant => $"in {tp.Name}",
            _ => tp.Name
        };
    }

    private string EscapeString(string str)
    {
        return str
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private void DumpDecorators(ImmutableArray<Decorator> decorators, int depth, string indent, string childPrefix)
    {
        if (decorators.Length == 0)
            return;

        _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Decorators: [{decorators.Length}]");
        for (int i = 0; i < decorators.Length; i++)
        {
            var decorator = decorators[i];
            var decIndent = new string(' ', (depth + 2) * IndentUnit.Length);
            var decPrefix = i == decorators.Length - 1 ? "└─ " : "├─ ";

            var nameDisplay = decorator.QualifiedParts.Length > 1
                ? string.Join(".", decorator.QualifiedParts)
                : decorator.Name;

            if (decorator.Arguments.Length > 0 || decorator.KeywordArguments.Length > 0)
            {
                _output.AppendLine(CultureInfo.InvariantCulture, $"{decIndent}{decPrefix}@{nameDisplay}(...) @ L{decorator.LineStart}:C{decorator.ColumnStart}");
                var argDepth = depth + 3;
                var argIndent = new string(' ', argDepth * IndentUnit.Length);
                var totalArgs = decorator.Arguments.Length + decorator.KeywordArguments.Length;
                var argIndex = 0;
                foreach (var arg in decorator.Arguments)
                {
                    argIndex++;
                    VisitChild(arg, argDepth, argIndex == totalArgs);
                }
                foreach (var kwarg in decorator.KeywordArguments)
                {
                    argIndex++;
                    var kwargIsLast = argIndex == totalArgs;
                    var kwPrefix = kwargIsLast ? "└─ " : "├─ ";
                    _output.AppendLine(CultureInfo.InvariantCulture, $"{argIndent}{kwPrefix}{kwarg.Name}=");
                    VisitChild(kwarg.Value, argDepth + 1, true);
                }
            }
            else
            {
                _output.AppendLine(CultureInfo.InvariantCulture, $"{decIndent}{decPrefix}@{nameDisplay} @ L{decorator.LineStart}:C{decorator.ColumnStart}");
            }
        }
    }

    private void DumpComprehensionClause(ComprehensionClause clause, int depth, bool isLast)
    {
        var indent = new string(' ', depth * IndentUnit.Length);
        var prefix = isLast ? "└─ " : "├─ ";
        var childPrefix = isLast ? "   " : "│  ";

        switch (clause)
        {
            case ForClause forClause:
                _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}ForClause{(forClause.IsAsync ? " (async)" : "")} @ L{clause.LineStart}:C{clause.ColumnStart}");
                _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Target:");
                VisitChild(forClause.Target, depth + 2, false);
                _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Iterator:");
                VisitChild(forClause.Iterator, depth + 2, true);
                break;

            case IfClause ifClause:
                _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}IfClause @ L{clause.LineStart}:C{clause.ColumnStart}");
                _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{childPrefix}Condition:");
                VisitChild(ifClause.Condition, depth + 2, true);
                break;

            default:
                _output.AppendLine(CultureInfo.InvariantCulture, $"{indent}{prefix}{clause.GetType().Name} @ L{clause.LineStart}:C{clause.ColumnStart}");
                break;
        }
    }

    #endregion
}
