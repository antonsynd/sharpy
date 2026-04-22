using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Warns about local variables that are assigned but never read.
/// Skips parameters, underscore-prefixed variables, and loop variables.
/// </summary>
internal class UnusedVariableValidator : ValidatingAstWalker
{
    public override string Name => "UnusedVariableValidator";
    public override int Order => 420;

    // Per-function state for tracking definitions from within expressions (walrus operator)
    private Dictionary<string, VariableInfo> _currentDefined = null!;
    private HashSet<string> _currentParameters = null!;

    // Override Visit methods to dispatch to per-function validation.
    // We don't call base for these because ValidateFunction handles its own traversal.

    public override void VisitFunctionDef(FunctionDef node)
    {
        ValidateFunction(node);
        // Don't call base — ValidateFunction handles its own traversal
    }

    public override void VisitClassDef(ClassDef node)
    {
        foreach (var member in node.Body)
        {
            if (member is FunctionDef method)
                ValidateFunction(method);
        }
        // Don't call base — we've handled method traversal
    }

    public override void VisitStructDef(StructDef node)
    {
        foreach (var member in node.Body)
        {
            if (member is FunctionDef method)
                ValidateFunction(method);
        }
        // Don't call base — we've handled method traversal
    }

    private void ValidateFunction(FunctionDef func)
    {
        // Skip abstract methods and stub bodies
        if (func.Decorators.Any(d => d.Name == DecoratorNames.Abstract))
            return;
        if (func.Body.Length == 1 && func.Body[0] is ExpressionStatement { Expression: EllipsisLiteral })
            return;

        var defined = new Dictionary<string, VariableInfo>();
        var read = new HashSet<string>();

        // Parameters are defined but should not be warned about
        var parameters = new HashSet<string>();
        foreach (var param in func.Parameters)
        {
            parameters.Add(param.Name);
        }

        // Save and restore per-function instance state for walrus operator tracking.
        // This is necessary because ValidateFunction is called recursively for nested
        // functions, and without save/restore the instance fields would point to the
        // nested function's data after the recursive call returns.
        var savedDefined = _currentDefined;
        var savedParameters = _currentParameters;
        _currentDefined = defined;
        _currentParameters = parameters;

        var readCollector = new ReadCollector(read, this);

        // Collect definitions and reads from the function body
        foreach (var stmt in func.Body)
        {
            CollectFromStatement(stmt, defined, read, parameters, readCollector);
        }

        // Restore outer scope state
        _currentDefined = savedDefined;
        _currentParameters = savedParameters;

        // Emit warnings for unused variables
        foreach (var (name, info) in defined)
        {
            if (read.Contains(name))
                continue;

            // Skip underscore-prefixed (intentionally unused convention)
            if (name.StartsWith('_'))
                continue;

            // Skip parameters
            if (parameters.Contains(name))
                continue;

            // Skip loop variables
            if (info.IsLoopVariable)
                continue;

            AddWarning(
                $"Local variable '{name}' is assigned but never used",
                info.Line, info.Column,
                code: DiagnosticCodes.Validation.UnusedVariable,
                span: info.Span);
        }
    }

    private void CollectFromStatement(Statement stmt, Dictionary<string, VariableInfo> defined,
        HashSet<string> read, HashSet<string> parameters, ReadCollector readCollector)
    {
        switch (stmt)
        {
            case VariableDeclaration varDecl:
                if (!parameters.Contains(varDecl.Name))
                {
                    defined[varDecl.Name] = new VariableInfo(
                        varDecl.LineStart, varDecl.ColumnStart, varDecl.Span, false);
                }
                if (varDecl.InitialValue != null)
                    readCollector.Visit(varDecl.InitialValue);
                break;

            case Assignment assign:
                // Collect reads from the value side first
                readCollector.Visit(assign.Value);

                if (assign.Target is Identifier targetId)
                {
                    if (assign.Operator == AssignmentOperator.Assign)
                    {
                        // Simple assignment defines a variable
                        if (!parameters.Contains(targetId.Name))
                        {
                            defined[targetId.Name] = new VariableInfo(
                                assign.LineStart, assign.ColumnStart, assign.Span, false);
                        }
                    }
                    else
                    {
                        // Augmented assignment (+=, -=, etc.) reads the variable too
                        read.Add(targetId.Name);
                    }
                }
                else if (assign.Target is TupleLiteral tuple)
                {
                    // Tuple unpacking - each element is a definition
                    foreach (var elem in tuple.Elements)
                    {
                        if (elem is Identifier tupleId && !parameters.Contains(tupleId.Name))
                        {
                            defined[tupleId.Name] = new VariableInfo(
                                assign.LineStart, assign.ColumnStart, elem.Span, false);
                        }
                    }
                }
                else
                {
                    // Member access, index, etc. - reads the target
                    readCollector.Visit(assign.Target);
                }
                break;

            case ReturnStatement ret:
                if (ret.Value != null)
                    readCollector.Visit(ret.Value);
                break;

            case ExpressionStatement exprStmt:
                readCollector.Visit(exprStmt.Expression);
                break;

            case IfStatement ifStmt:
                readCollector.Visit(ifStmt.Test);
                foreach (var s in ifStmt.ThenBody)
                    CollectFromStatement(s, defined, read, parameters, readCollector);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    readCollector.Visit(elif.Test);
                    foreach (var s in elif.Body)
                        CollectFromStatement(s, defined, read, parameters, readCollector);
                }
                foreach (var s in ifStmt.ElseBody)
                    CollectFromStatement(s, defined, read, parameters, readCollector);
                break;

            case WhileStatement whileStmt:
                readCollector.Visit(whileStmt.Test);
                foreach (var s in whileStmt.Body)
                    CollectFromStatement(s, defined, read, parameters, readCollector);
                foreach (var s in whileStmt.ElseBody)
                    CollectFromStatement(s, defined, read, parameters, readCollector);
                break;

            case ForStatement forStmt:
                readCollector.Visit(forStmt.Iterator);
                // Loop variable is defined but marked as loop variable (skip warning)
                CollectForTarget(forStmt.Target, defined, parameters, isLoopVariable: true);
                foreach (var s in forStmt.Body)
                    CollectFromStatement(s, defined, read, parameters, readCollector);
                foreach (var s in forStmt.ElseBody)
                    CollectFromStatement(s, defined, read, parameters, readCollector);
                break;

            case TryStatement tryStmt:
                foreach (var s in tryStmt.Body)
                    CollectFromStatement(s, defined, read, parameters, readCollector);
                foreach (var handler in tryStmt.Handlers)
                {
                    if (handler.Name != null)
                    {
                        // except Exception as e: defines variable 'e'
                        defined[handler.Name] = new VariableInfo(
                            handler.LineStart, handler.ColumnStart, handler.Span, false);
                    }
                    foreach (var s in handler.Body)
                        CollectFromStatement(s, defined, read, parameters, readCollector);
                }
                foreach (var s in tryStmt.ElseBody)
                    CollectFromStatement(s, defined, read, parameters, readCollector);
                foreach (var s in tryStmt.FinallyBody)
                    CollectFromStatement(s, defined, read, parameters, readCollector);
                break;

            case WithStatement withStmt:
                foreach (var item in withStmt.Items)
                {
                    readCollector.Visit(item.ContextExpression);
                    if (item.Name != null)
                    {
                        defined[item.Name] = new VariableInfo(
                            item.LineStart, item.ColumnStart, item.Span, false);
                    }
                }
                foreach (var s in withStmt.Body)
                    CollectFromStatement(s, defined, read, parameters, readCollector);
                break;

            case RaiseStatement raiseStmt:
                if (raiseStmt.Exception != null)
                    readCollector.Visit(raiseStmt.Exception);
                break;

            case AssertStatement assertStmt:
                readCollector.Visit(assertStmt.Test);
                if (assertStmt.Message != null)
                    readCollector.Visit(assertStmt.Message);
                break;

            case FunctionDef nestedFunc:
                // Validate the nested function independently for its own unused variables
                ValidateFunction(nestedFunc);
                // Scan the nested function's body for reads of enclosing scope variables (closures)
                CollectReadsFromNestedFunction(nestedFunc, read);
                break;

            case MatchStatement matchStmt:
                readCollector.Visit(matchStmt.Scrutinee);
                foreach (var matchCase in matchStmt.Cases)
                {
                    CollectDefinitionsFromPattern(matchCase.Pattern, defined, parameters);
                    if (matchCase.Guard != null)
                        readCollector.Visit(matchCase.Guard);
                    foreach (var s in matchCase.Body)
                        CollectFromStatement(s, defined, read, parameters, readCollector);
                }
                break;

            case ClassDef:
            case StructDef:
            case InterfaceDef:
            case EnumDef:
            case PropertyDef:
            case EventDef:
                // These define their own scope and are validated at the top level
                break;
        }
    }

    private void CollectDefinitionsFromPattern(Pattern pattern,
        Dictionary<string, VariableInfo> defined, HashSet<string> parameters)
    {
        switch (pattern)
        {
            case BindingPattern binding:
                // Skip constant patterns (RFC 3535) — they reference existing constants, not new variables
                if (Context.SemanticInfo?.GetPatternConstantSymbol(binding) != null)
                    break;
                if (!parameters.Contains(binding.Name.Name))
                {
                    defined[binding.Name.Name] = new VariableInfo(
                        binding.LineStart, binding.ColumnStart, binding.Span, false);
                }
                break;

            case TypePattern typePattern:
                if (typePattern.BindingName != null && !parameters.Contains(typePattern.BindingName.Name))
                {
                    defined[typePattern.BindingName.Name] = new VariableInfo(
                        typePattern.LineStart, typePattern.ColumnStart, typePattern.Span, false);
                }
                break;

            case OrPattern orPattern:
                foreach (var alt in orPattern.Alternatives)
                    CollectDefinitionsFromPattern(alt, defined, parameters);
                break;

            case TuplePattern tuplePattern:
                foreach (var elem in tuplePattern.Elements)
                    CollectDefinitionsFromPattern(elem, defined, parameters);
                break;

            case PropertyPattern propertyPattern:
                foreach (var field in propertyPattern.Fields)
                    CollectDefinitionsFromPattern(field.Pattern, defined, parameters);
                break;

            case PositionalPattern positionalPattern:
                foreach (var elem in positionalPattern.Elements)
                    CollectDefinitionsFromPattern(elem, defined, parameters);
                break;
        }
    }

    private static void CollectForTarget(Expression target, Dictionary<string, VariableInfo> defined,
        HashSet<string> parameters, bool isLoopVariable)
    {
        if (target is Identifier id && !parameters.Contains(id.Name))
        {
            defined[id.Name] = new VariableInfo(
                id.LineStart, id.ColumnStart, id.Span, isLoopVariable);
        }
        else if (target is TupleLiteral tuple)
        {
            foreach (var elem in tuple.Elements)
                CollectForTarget(elem, defined, parameters, isLoopVariable);
        }
    }

    /// <summary>
    /// Scan a nested function for identifier reads that may reference
    /// variables from an enclosing scope (closure captures).
    /// Includes parameter default values and decorator names, which are
    /// evaluated in the enclosing scope, not the nested function's scope.
    /// </summary>
    private static void CollectReadsFromNestedFunction(FunctionDef func, HashSet<string> outerRead)
    {
        var collector = new ReadCollector(outerRead);

        // Parameter default values are evaluated in the enclosing scope
        foreach (var param in func.Parameters)
        {
            if (param.DefaultValue != null)
                collector.Visit(param.DefaultValue);
        }

        // Decorator names may reference enclosing scope variables
        foreach (var decorator in func.Decorators)
            outerRead.Add(decorator.Name);

        // Walk the entire nested function body for identifier reads
        foreach (var stmt in func.Body)
        {
            collector.Visit(stmt);
        }
    }

    /// <summary>
    /// AstVisitor that collects all identifier reads from AST nodes.
    /// DefaultVisit handles recursive traversal into child nodes automatically.
    /// Special handling for WalrusExpression to track definitions.
    /// </summary>
    private sealed class ReadCollector : AstVisitor
    {
        private readonly HashSet<string> _read;
        private readonly UnusedVariableValidator? _validator;

        public ReadCollector(HashSet<string> read, UnusedVariableValidator validator)
        {
            _read = read;
            _validator = validator;
        }

        public ReadCollector(HashSet<string> read)
        {
            _read = read;
            _validator = null;
        }

        public override void VisitIdentifier(Identifier node) => _read.Add(node.Name);

        public override void VisitWalrusExpression(WalrusExpression node)
        {
            // Walrus (name := value) defines the target variable
            if (_validator != null && !_validator._currentParameters.Contains(node.Target))
            {
                _validator._currentDefined[node.Target] = new VariableInfo(
                    node.LineStart, node.ColumnStart, node.Span, false);
            }
            // Visit value for reads but don't visit node as a whole (which would add target as read)
            Visit(node.Value);
        }
    }

    private record VariableInfo(int? Line, int? Column, Text.TextSpan? Span, bool IsLoopVariable);
}
